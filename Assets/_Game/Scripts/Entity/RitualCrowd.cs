using System.Collections.Generic;
using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 의례 중 사람들 동작: 신도(눈꺼풀)들의 앉기·박수·손 들기·기도·무릎 꿇기, 무릎 꿇으러 걸어 나가는 눈꺼풀,
    /// 교주 동작, 1·2열의 동시 고개 꺾기(현상 4)와 고개만 돌려 쳐다보기.
    /// 머리 뼈는 다른 담당(일어섰을 때 돌아보기)이 LateUpdate에서 돌린 값 위에 '곱해서' 더한다 → 실행 순서 200.
    /// 동작은 의례 시각만 보고 정한다(구간 건너뛰기에도 맞게).
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class RitualCrowd : MonoBehaviour
    {
        public static RitualCrowd Instance { get; private set; }

        public class Member
        {
            public Transform root, model, head, hips, knee, spine;
            public Animator anim;
            public Renderer vis;
            public Watcher watcher;
            public RuntimeAnimatorController baseCtrl;
            public AnimationClip baseClip, cur;
            public Vector3 baseLocal;
            public int row, seat;
            public bool right;
            public float delay, speed = 1f;
            public float zOff, zOffTarget, measureAt = -1f;
            public float tilt;                // 현상 4 고개 꺾기(도)
            public float stareW, stareUntil = -1f, stareRate;
            public bool Staring => Time.time < stareUntil;
        }

        [Header("수치")]
        public float jerkAngle = 15f;
        public float jerkPeriod = 4f;
        public int frontRowFrom = 10;          // 이 줄부터 '1·2열'(단상 쪽)
        public float stareTurnSpeed = 0.7f;    // 고개 돌리는 속도(가중치/초) — 천천히
        public float stareMaxYaw = 165f;

        ServiceDirector dir;
        RitualClips clips;
        readonly List<Member> believers = new List<Member>();
        readonly List<Member> front = new List<Member>();
        Member leader, victim;
        readonly Dictionary<RuntimeAnimatorController, Dictionary<AnimationClip, AnimatorOverrideController>> aocs = new Dictionary<RuntimeAnimatorController, Dictionary<AnimationClip, AnimatorOverrideController>>();
        Renderer[] victimRenderers;
        bool victimShown = true;

        public IReadOnlyList<Member> Believers => believers;
        public IReadOnlyList<Member> FrontRows => front;
        public Member Victim => victim;
        public Member Leader => leader;
        /// <summary>지금 1·2열이 고개를 꺾고 있는가 (정수리 줄기가 팽팽해짐).</summary>
        public bool JerkTaut { get; private set; }
        /// <summary>무릎 꿇은 눈꺼풀이 자리 잡았는가 (현상 8 촉수가 머리를 감쌈).</summary>
        public bool VictimKneeling { get; private set; }

        // 무릎 꿇으러 걸어가는 길 (맨 앞줄 앞 → 계단 → 교주 앞)
        static readonly Vector3[] VictimPath =
        {
            new Vector3(1.7f, 0f, 7.2f), new Vector3(0.45f, 0f, 8.25f), new Vector3(0.2f, 0.4f, 8.75f),
            new Vector3(0.05f, 0.8f, 9.35f), new Vector3(0f, 0.8f, 11.45f),
        };
        public static Vector3 KneelSpot => VictimPath[VictimPath.Length - 1];
        const float WalkSpeed = 1.05f;
        float pathLen;

        public void Init(ServiceDirector d)
        {
            Instance = this;
            dir = d;
            clips = RitualClips.Get();
            var rnd = new System.Random(23);

            var bRoot = GameObject.Find("Believers");
            if (bRoot != null)
                foreach (Transform b in bRoot.transform)
                {
                    var m = MakeMember(b);
                    if (m == null) continue;
                    ParseName(b.name, m);
                    m.delay = (float)rnd.NextDouble() * 1.6f;
                    m.speed = 0.9f + (float)rnd.NextDouble() * 0.18f;
                    m.watcher = b.GetComponent<Watcher>();
                    believers.Add(m);
                    if (m.row >= frontRowFrom) front.Add(m);
                }
            var cast = GameObject.Find("Cast");
            if (cast != null)
            {
                var lt = cast.transform.Find("Leader");
                if (lt != null) leader = MakeMember(lt);
                var vt = cast.transform.Find("Victim");
                if (vt != null)
                {
                    victim = MakeMember(vt);
                    if (victim != null) victimRenderers = victim.model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    // 걸어 다니는 동안 시선·촬영 판정을 가리지 않게 옛 캡슐 콜라이더는 끈다
                    foreach (var col in vt.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                }
            }
            for (int i = 0; i + 1 < VictimPath.Length; i++) pathLen += Vector3.Distance(VictimPath[i], VictimPath[i + 1]);
            ApplyVictim(0f);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static Member MakeMember(Transform root)
        {
            var model = root.Find("Model");
            var anim = model != null ? model.GetComponent<Animator>() : null;
            if (anim == null || anim.runtimeAnimatorController == null) return null;
            var m = new Member
            {
                root = root, model = model, anim = anim,
                head = anim.GetBoneTransform(HumanBodyBones.Head),
                hips = anim.GetBoneTransform(HumanBodyBones.Hips),
                knee = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg),
                spine = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine),
                baseCtrl = anim.runtimeAnimatorController,
                baseLocal = model.localPosition,
            };
            var clipsArr = m.baseCtrl.animationClips;
            m.baseClip = clipsArr != null && clipsArr.Length > 0 ? clipsArr[0] : null;
            m.cur = m.baseClip;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (smr.enabled) { m.vis = smr; break; }
            return m;
        }

        static void ParseName(string n, Member m)
        {
            // Believer_{row:00}{L|R}{seat}
            if (n.Length >= 13 && n.StartsWith("Believer_"))
            {
                int.TryParse(n.Substring(9, 2), out m.row);
                m.right = n[11] == 'R';
                m.seat = n[12] - '0';
            }
        }

        // ---------- 동작 ----------
        AnimatorOverrideController Aoc(RuntimeAnimatorController baseCtrl, AnimationClip baseClip, AnimationClip clip)
        {
            if (!aocs.TryGetValue(baseCtrl, out var byClip)) { byClip = new Dictionary<AnimationClip, AnimatorOverrideController>(); aocs[baseCtrl] = byClip; }
            if (byClip.TryGetValue(clip, out var a) && a != null) return a;
            a = new AnimatorOverrideController(baseCtrl) { name = baseCtrl.name + "_" + clip.name };
            a[baseClip] = clip;
            byClip[clip] = a;
            return a;
        }

        void SetClip(Member p, AnimationClip clip, float speed = -1f)
        {
            if (p == null || clip == null || p.baseClip == null || p.cur == clip) return;
            p.cur = clip;
            p.anim.runtimeAnimatorController = clip == p.baseClip ? p.baseCtrl : (RuntimeAnimatorController)Aoc(p.baseCtrl, p.baseClip, clip);
            p.anim.speed = speed > 0f ? speed : p.speed;
            p.anim.Play(0, 0, clip.isLooping ? Random.value : 0f);
            p.measureAt = Time.time + 0.35f;
        }

        AnimationClip PoseFor(Member p, float t)
        {
            if (clips == null) return null;
            float u = t - p.delay;
            if (u >= 86f && u < 120f) return clips.sitClap;                                         // 복창하며 박수
            if (u >= 160f && u < 192f && p.right && p.row % 2 == 0) return clips.handRaise;         // 호명된 구역 손 들기
            if (u >= 228f && u < 300f) return clips.praying;                                        // 두 손 모으고 고개 숙임
            if (u >= RitualScript.BlindfoldOn && u < RitualScript.BlindfoldOff)
                return (p.row + p.seat) % 3 == 0 ? clips.handRaise : clips.kneelIdle;               // 하얀 천 쓰고 무릎·손 들기
            if (u >= 393f && u < 405f) return clips.sitClap;                                        // 폐회 박수
            return clips.sitIdle;
        }

        void Update()
        {
            if (dir == null) return;
            float t = dir.RitualTime;
            bool started = dir.Started;

            if (started && clips != null)
            {
                foreach (var p in believers)
                {
                    if (p.Staring) continue;   // 쳐다보는 동안은 멈춤
                    SetClip(p, PoseFor(p, t));
                }
                if (leader != null)
                {
                    bool raise = t >= RitualScript.PillarFrom && t < RitualScript.BlindfoldOff;
                    SetClip(leader, raise ? clips.handRaise : clips.talking, raise ? 0.8f : -1f);
                }
            }
            ApplyVictim(started ? t : 0f);

            // 고개 꺾기 (현상 4): 1·2열이 정확히 같은 순간에
            JerkTaut = false;
            float jerk = 0f;
            if (started && RitualScript.InWindow(RitualScript.WinThreads, t))
            {
                float ph = (t - RitualScript.WinThreads[0]) % jerkPeriod;
                if (ph < 0.15f) jerk = ph / 0.15f;
                else if (ph < 0.8f) jerk = 1f;
                else if (ph < 1.1f) jerk = 1f - (ph - 0.8f) / 0.3f;
                JerkTaut = jerk > 0.5f;
            }
            float dt = Time.deltaTime;
            foreach (var p in front) p.tilt = p.Staring ? 0f : jerk * jerkAngle;

            // 쳐다보기 가중치·멈춤
            foreach (var p in believers) TickStare(p, dt);
        }

        void TickStare(Member p, float dt)
        {
            bool on = p.Staring;
            float target = on ? 1f : 0f;
            float rate = on ? stareTurnSpeed : 2f;
            float prev = p.stareW;
            p.stareW = Mathf.MoveTowards(p.stareW, target, rate * dt);
            if (on && p.anim.speed != 0f) p.anim.speed = 0f;
            if (!on && prev > 0f && p.stareW <= 0f) p.anim.speed = p.speed;
            if (on && p.watcher != null && p.stareRate > 0f && GameEvents.IsPlaying && EntityKit.PhoneAimed)
                p.watcher.suspicion = Mathf.Min(100f, p.watcher.suspicion + p.stareRate * dt);
        }

        /// <summary>그 눈꺼풀이 동작을 멈추고 고개만 돌려 플레이어를 본다. suspicionPerSec: 폰을 든 동안 그 신도의 의심 증가.</summary>
        public void Stare(Member p, float seconds, float suspicionPerSec)
        {
            if (p == null) return;
            p.stareUntil = Mathf.Max(p.stareUntil, Time.time + seconds);
            p.stareRate = suspicionPerSec;
        }

        public Member NearestToPlayer(float maxDist = 4f)
        {
            Vector3 h = EntityKit.PlayerHead();
            Member best = null; float bd = maxDist * maxDist;
            foreach (var p in believers)
            {
                float d = (p.root.position - h).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        // ---------- 무릎 꿇는 눈꺼풀 ----------
        void ApplyVictim(float t)
        {
            if (victim == null) return;
            bool show = t >= RitualScript.VictimWalk;
            if (show != victimShown)
            {
                victimShown = show;
                if (victimRenderers != null) foreach (var r in victimRenderers) if (r != null) r.enabled = show;
            }
            VictimKneeling = false;
            if (!show) { victim.root.position = VictimPath[0]; return; }

            float walked = (t - RitualScript.VictimWalk) * WalkSpeed;
            if (walked < pathLen)
            {
                // 걷는 중
                float acc = 0f;
                for (int i = 0; i + 1 < VictimPath.Length; i++)
                {
                    float seg = Vector3.Distance(VictimPath[i], VictimPath[i + 1]);
                    if (walked <= acc + seg || i + 2 == VictimPath.Length)
                    {
                        float k = Mathf.Clamp01((walked - acc) / Mathf.Max(0.001f, seg));
                        victim.root.position = Vector3.Lerp(VictimPath[i], VictimPath[i + 1], k);
                        Vector3 d = VictimPath[i + 1] - VictimPath[i]; d.y = 0f;
                        if (d.sqrMagnitude > 1e-4f) victim.root.rotation = Quaternion.Slerp(victim.root.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-8f * Time.deltaTime));
                        break;
                    }
                    acc += seg;
                }
                if (clips != null) SetClip(victim, clips.walk);
                return;
            }
            victim.root.position = KneelSpot;
            victim.root.rotation = Quaternion.Slerp(victim.root.rotation, Quaternion.identity, 1f - Mathf.Exp(-6f * Time.deltaTime));   // 교주(+Z)를 향해
            float kneelT = t - RitualScript.VictimWalk - pathLen / WalkSpeed;
            float downLen = clips != null && clips.kneelDown != null ? Mathf.Min(3f, clips.kneelDown.length) : 0f;
            if (clips != null && kneelT < downLen && clips.kneelDown != null) SetClip(victim, clips.kneelDown, 1f);
            else
            {
                if (clips != null) SetClip(victim, clips.kneelIdle ?? victim.baseClip);
                VictimKneeling = true;
            }
        }

        // ---------- 뼈 (다른 담당의 머리 돌리기 뒤에 곱한다) ----------
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 player = EntityKit.PlayerHead();
            foreach (var p in believers)
            {
                // 자리에서 일어서거나 무릎 꿇으면 의자에서 앞으로 비켜 선다
                if (p.measureAt > 0f && Time.time >= p.measureAt && p.knee != null && p.hips != null)
                {
                    p.measureAt = -1f;
                    float floor = p.model.position.y;
                    float kneeY = p.knee.position.y - floor, hipY = p.hips.position.y - floor;
                    p.zOffTarget = kneeY < 0.25f ? 0.48f : (hipY > 0.8f ? 0.3f : 0f);
                }
                if (!Mathf.Approximately(p.zOff, p.zOffTarget))
                {
                    p.zOff = Mathf.MoveTowards(p.zOff, p.zOffTarget, 1.4f * dt);
                    p.model.localPosition = p.baseLocal + Vector3.forward * p.zOff;
                }

                if (p.head == null || (p.vis != null && !p.vis.isVisible)) continue;
                if (p.tilt != 0f)
                    p.head.rotation = Quaternion.AngleAxis(p.tilt, p.model.forward) * p.head.rotation;
                if (p.stareW > 0f)
                {
                    Vector3 to = player - p.head.position; to.y = 0f;
                    Vector3 fwd = p.model.forward; fwd.y = 0f;
                    if (to.sqrMagnitude > 1e-4f && fwd.sqrMagnitude > 1e-4f)
                    {
                        float yaw = Mathf.Clamp(Vector3.SignedAngle(fwd, to, Vector3.up), -stareMaxYaw, stareMaxYaw);
                        p.head.rotation = Quaternion.AngleAxis(yaw * Mathf.SmoothStep(0f, 1f, p.stareW), Vector3.up) * p.head.rotation;
                    }
                }
            }
            // 무릎 꿇은 눈꺼풀: 상체만 미세하게 떤다
            if (victim != null && VictimKneeling && victim.spine != null && dir != null && RitualScript.InWindow(RitualScript.WinKneel, dir.RitualTime))
            {
                float n = (Mathf.PerlinNoise(Time.time * 9f, 0.3f) - 0.5f) * 3f;
                float m = (Mathf.PerlinNoise(0.7f, Time.time * 8f) - 0.5f) * 2f;
                victim.spine.rotation = Quaternion.Euler(n, 0f, m) * victim.spine.rotation;
            }
        }
    }
}
