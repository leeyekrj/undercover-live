using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 현상 9·10: 강림 때 천장에서 내려오는 거대 '겹눈' 본체와 역관측.
    /// - 맨눈엔 없다(PhoneOnly). 맨눈엔 황금 빛기둥(ServiceDirector)만.
    /// - 5:05부터 천장에서 내려와 떠 있고, 동공이 폰 카메라를 따라본다. 2초 넘게 비추면 보조 눈 열 개가 일제히 렌즈로 돌아간다.
    /// - 강림 중 누적 3초 넘게 찍으면 플레이어 3m 앞까지 내려와 폰 화면을 가득 채운다(역관측 대상 확보 가능).
    ///   확보 뒤 1.5초 안에 폰을 내리지 않으면(또는 코앞에서 6.5초 버티면) 역관측 엔딩 → 폰 압수 신호.
    ///   내리면 천천히 천장으로 돌아간다.
    /// </summary>
    public class EyeEntity : MonoBehaviour
    {
        public static EyeEntity Instance { get; private set; }
        public enum State { Hidden, Descend, Hover, Approach, Close, Retreat, Ascend, Ending }
        public State Current { get; private set; } = State.Hidden;
        public bool Locked { get; private set; }
        public float Filmed { get; private set; }
        public bool IsEnding => Current == State.Ending;

        [Header("수치")]
        [Tooltip("천장을 뚫고 반쯤 내려온 자리. 눈동자님 머리 위")]
        public Vector3 hoverPos = new Vector3(0f, 6.4f, 10.4f);
        public float radius = 4f;
        public float stareDistance = 3f;
        public float approachSmooth = 0.5f, retreatSmooth = 2.2f;

        ServiceDirector dir;
        RitualCrowd crowd;
        int P;
        GameObject root;
        Transform body, face, iris;
        Material irisMat, auxMatA, auxMatB, bodyMat;
        Transform[] aux;
        Tentacle[] tents;
        Vector3[] tentBaseLocal, tentStageTip;
        Light red, gold;
        CameraOnlyLight redC, goldC;
        CaptureTarget target, stareTarget;
        readonly Aim lockAim = new Aim();
        Vector3 vel;
        float cooldownUntil, capturedAt = -1f, closeAimed, hang, spamNext;
        bool entityCapturedOnce;
        AudioSource sfx;
        StreamFeed feed;
        string[] nicks;

        Vector3 HidePos => hoverPos + Vector3.up * 9f;
        static readonly Color IrisGlow = new Color(2.4f, 1.8f, 1.3f);
        static readonly Vector3 CrowdBelow = new Vector3(0f, 1.0f, 3.0f);

        [System.Serializable] class NickList { public string[] nicknames; }

        public void Init(ServiceDirector d, RitualCrowd c)
        {
            Instance = this;
            dir = d; crowd = c;
            P = EntityKit.PhoneOnly;
            Build();
            CaptureLog.Added += OnCaptured;
            var ta = Resources.Load<TextAsset>("ChatLines");
            if (ta != null) { try { nicks = JsonUtility.FromJson<NickList>(ta.text).nicknames; } catch { nicks = null; } }
            if (nicks == null || nicks.Length == 0) nicks = new[] { "새벽세시", "구독자1", "익명의눈", "밤샘러" };
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false; sfx.spatialBlend = 0f; sfx.volume = 0.7f;
            sfx.clip = MakeRattle();
        }

        void OnDestroy()
        {
            CaptureLog.Added -= OnCaptured;
            if (Instance == this) Instance = null;
            if (root != null) Destroy(root);
        }

        void Build()
        {
            root = EntityKit.Group("P9_GiantEye", null, P);
            root.transform.position = HidePos;
            var flesh = EyeTexture.Flesh();
            bodyMat = EntityKit.Glow(new Color(0.55f, 0.40f, 0.42f), new Color(0.75f, 0.09f, 0.07f), flesh, flesh);
            var tentMat = EntityKit.Glow(new Color(0.2f, 0.13f, 0.14f), new Color(0.3f, 0.035f, 0.03f), flesh, flesh); // 11-18 검수: 형광 빨강 고무처럼 보여 어둡게
            var lidMat = EntityKit.Glow(new Color(0.18f, 0.03f, 0.03f), new Color(0.35f, 0.03f, 0.02f), flesh, flesh, true);
            var abyss = EyeTexture.Get(EyeStyle.Abyss);
            irisMat = EntityKit.Glow(Color.white, IrisGlow, abyss, abyss, true);
            auxMatA = Phenomena.EyeMat(EyeStyle.Red, new Color(2.2f, 0.45f, 0.35f));
            auxMatB = Phenomena.EyeMat(EyeStyle.Gold, new Color(1.9f, 1.4f, 0.7f));

            body = EntityKit.Obj("Body", root.transform, EntityKit.Prim(PrimitiveType.Sphere), bodyMat, P).transform;
            body.localScale = Vector3.one * radius * 2f;
            face = EntityKit.Group("Face", root.transform, P).transform;
            var lid = EntityKit.Obj("Lid", face, EntityKit.Ring(0.84f), lidMat, P).transform;
            lid.localPosition = new Vector3(0, 0, radius + 0.03f); lid.localRotation = Quaternion.Euler(0, 180, 0); lid.localScale = Vector3.one * 5.6f;
            iris = EntityKit.Obj("Iris", face, EntityKit.Disc(), irisMat, P).transform;
            iris.localPosition = new Vector3(0, 0, radius + 0.06f); iris.localRotation = Quaternion.Euler(0, 180, 0); iris.localScale = Vector3.one * 4.8f;

            aux = new Transform[10];
            for (int i = 0; i < aux.Length; i++)
            {
                Vector3 dirL = Quaternion.AngleAxis(i * 36f + 18f, Vector3.forward) * Quaternion.AngleAxis(60f + 6f * (i % 2), Vector3.right) * Vector3.forward;
                aux[i] = EntityKit.EyeBall("Aux" + i, face, 0.95f, bodyMat, i % 2 == 0 ? auxMatA : auxMatB, P, 0.8f);
                aux[i].localPosition = dirL * (radius - 0.12f);
                aux[i].localRotation = Quaternion.LookRotation(dirL);
            }

            Vector3[] tips =
            {
                new Vector3(-2.2f, 1.2f, 11.6f), new Vector3(2.0f, 1.0f, 12.2f), new Vector3(-0.7f, 0.9f, 10.0f), new Vector3(3.6f, 1.5f, 9.6f),
                new Vector3(-3.8f, 1.4f, 9.4f), new Vector3(1.1f, 2.6f, 6.4f), new Vector3(-1.7f, 2.8f, 5.5f),
            };
            tents = new Tentacle[tips.Length]; tentBaseLocal = new Vector3[tips.Length]; tentStageTip = tips;
            for (int i = 0; i < tips.Length; i++)
            {
                float a = i / (float)tips.Length * Mathf.PI * 2f + 0.3f;
                tentBaseLocal[i] = new Vector3(Mathf.Cos(a) * 2.5f, -3.05f, Mathf.Sin(a) * 2.5f);
                var tn = Tentacle.Create("Tentacle" + i, root.transform, 8, tentMat, P);
                tn.rootRadius = 0.48f; tn.tipRadius = 0.06f; tn.sag = 0.8f; tn.sagDir = Vector3.down;
                tn.waveAmp = 0.55f; tn.waveSpeed = 0.9f; tn.waveK = 5f; tn.freeTip = true;
                tents[i] = tn;
            }

            red = MakeLight("RedGlow", new Color(1f, 0.12f, 0.08f), 6f, 16f, out redC);
            red.transform.localPosition = new Vector3(0, -3.6f, 0);
            gold = MakeLight("GoldGlow", new Color(1f, 0.74f, 0.35f), 4f, 9f, out goldC);

            target = root.AddComponent<CaptureTarget>();
            RitualScript.Apply(target, "entity");
            // 홍채·눈꺼풀 테만 잰다(보조 눈까지 넣으면 상자가 천장 위로 올라가 가려짐 판정이 깎인다)
            target.renderers = new[] { iris.GetComponent<Renderer>(), lid.GetComponent<Renderer>() };
            target.activeNow = false;
            stareTarget = root.AddComponent<CaptureTarget>();
            RitualScript.Apply(stareTarget, "entity_stare");
            stareTarget.renderers = new[] { iris.GetComponent<Renderer>(), lid.GetComponent<Renderer>() };
            stareTarget.activeNow = false;

            root.SetActive(false);
        }

        Light MakeLight(string name, Color c, float intensity, float range, out CameraOnlyLight col)
        {
            var go = EntityKit.Group(name, root.transform, P);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            col = go.AddComponent<CameraOnlyLight>();
            col.who = CameraOnlyLight.Who.Phone;
            return l;
        }

        void OnCaptured(CaptureResult r)
        {
            if (r == null) return;
            if (r.TargetId == "entity_stare") capturedAt = Time.time;
            if (r.TargetId == "entity" && !entityCapturedOnce)
            {
                entityCapturedOnce = true;
                GameEvents.RaiseChatCue("fake_doubt");
            }
        }

        void Go(State s)
        {
            Current = s;
            if (s == State.Retreat) { Filmed = 0f; cooldownUntil = Time.time + 5f; closeAimed = 0f; capturedAt = -1f; }
            if (s == State.Close) { closeAimed = 0f; capturedAt = -1f; }
        }

        void Update()
        {
            if (dir == null) return;
            float t = dir.RitualTime;
            float dt = Time.deltaTime;
            bool playing = GameEvents.IsPlaying;
            bool window = dir.Started && t >= RitualScript.EntityDescend && t < RitualScript.EntityTo;
            var pc = PhoneController.Instance;
            var cam = EntityKit.PhoneCam;
            Vector3 hover = hoverPos + Vector3.up * (0.18f * Mathf.Sin(Time.time * 0.6f));
            var tr = root.transform;

            switch (Current)
            {
                case State.Hidden:
                    if (window && !dir.Halted) { root.SetActive(true); tr.position = HidePos; vel = Vector3.zero; Go(State.Descend); }
                    break;
                case State.Descend:
                    tr.position = Vector3.SmoothDamp(tr.position, hover, ref vel, 2.6f);
                    if (t >= RitualScript.EntityHover || (tr.position - hover).sqrMagnitude < 0.25f) Go(State.Hover);
                    if (!window) Go(State.Ascend);
                    break;
                case State.Hover:
                    tr.position = Vector3.SmoothDamp(tr.position, hover, ref vel, 1f);
                    if (!window) { Go(State.Ascend); break; }
                    if (!playing) break;
                    bool inView = EntityKit.FromCenter(iris.position) < 0.42f;
                    if (inView) Filmed += dt;
                    if (lockAim.Feed(inView) >= 2f && !Locked) Lock();
                    if (Filmed >= RitualScript.StareFilmSeconds && Time.time >= cooldownUntil && cam != null && EntityKit.PhoneAimed)
                    {
                        Go(State.Approach);
                        GameEvents.RaiseChatCue("entity_stare");
                        EntityStareFX.Glitch(0.25f);
                    }
                    break;
                case State.Approach:
                case State.Close:
                    if (!playing) break;
                    if (cam == null || pc == null || pc.State != PhoneState.Aimed || !window) { Go(State.Retreat); break; }
                    Vector3 goal = cam.transform.position + cam.transform.forward * (stareDistance + radius + 0.06f);
                    tr.position = Vector3.SmoothDamp(tr.position, goal, ref vel, Current == State.Approach ? approachSmooth : 0.22f);
                    if (Current == State.Approach && (tr.position - goal).sqrMagnitude < 0.8f) Go(State.Close);
                    if (Current == State.Close)
                    {
                        closeAimed += dt;
                        SpamChat();
                        bool lateAfterCapture = capturedAt > 0f && Time.time - capturedAt >= RitualScript.StareGrace;
                        if (lateAfterCapture || closeAimed >= RitualScript.StareMaxClose) BeginEnding();
                    }
                    break;
                case State.Retreat:
                    tr.position = Vector3.SmoothDamp(tr.position, hover, ref vel, retreatSmooth);
                    if ((tr.position - hover).sqrMagnitude < 0.5f) Go(window ? State.Hover : State.Ascend);
                    break;
                case State.Ascend:
                    tr.position = Vector3.SmoothDamp(tr.position, HidePos, ref vel, 1.6f);
                    if ((tr.position - HidePos).sqrMagnitude < 0.5f) { root.SetActive(false); Go(State.Hidden); }
                    break;
                case State.Ending:
                    if (cam != null) tr.position = Vector3.SmoothDamp(tr.position, cam.transform.position + cam.transform.forward * (stareDistance * 0.7f + radius), ref vel, 0.3f);
                    break;
            }

            bool shown = root.activeSelf;
            bool alive = Current == State.Hover || Current == State.Approach || Current == State.Close || Current == State.Retreat;
            target.activeNow = shown && alive && t >= RitualScript.EntityFrom;
            stareTarget.activeNow = shown && Current == State.Close;

            bool near = Current == State.Approach || Current == State.Close || Current == State.Ending;
            EntityStareFX.SetStare(near ? 1f : 0f, Current == State.Close && EntityKit.PhoneAimed);
            if (shown) Animate(t, dt, near);
        }

        void Lock()
        {
            Locked = true;
            if (sfx != null && sfx.clip != null) sfx.Play();
            EntityStareFX.Glitch(0.15f);
            var narrow = EyeTexture.Get(EyeStyle.AbyssNarrow);
            irisMat.SetTexture("_BaseMap", narrow); irisMat.SetTexture("_EmissionMap", narrow);
        }

        void BeginEnding()
        {
            if (Current == State.Ending) return;
            Go(State.Ending);
            stareTarget.activeNow = false;
            GameFlow.Instance?.Say(RitualScript.Leader, RitualScript.StareLeaderLine, 3f);
            for (int i = 0; i < 4; i++) SpamChat(true);
            EntityStareFX.PlayEnding(() =>
            {
                PhoneController.Instance?.SetState(PhoneState.Pocket);
                GameEvents.RaiseConfiscated(RitualScript.StareEndReason);
            });
        }

        void SpamChat(bool force = false)
        {
            if (!force && Time.time < spamNext) return;
            spamNext = Time.time + Random.Range(0.22f, 0.45f);
            if (feed == null) feed = FindAnyObjectByType<StreamFeed>();
            if (feed == null) return;
            feed.Push(nicks[Random.Range(0, nicks.Length)], RitualScript.StareChatSpam);
        }

        void Animate(float t, float dt, bool near)
        {
            var pc = PhoneController.Instance;
            var cam = EntityKit.PhoneCam;
            bool phoneOut = pc != null && pc.State != PhoneState.Pocket && cam != null;
            Vector3 lens = phoneOut ? cam.transform.position : EntityKit.PlayerHead();

            // 본체: 숨 쉬듯 부풀고, 동공이 렌즈를 따라본다 (폰을 넣으면 눈동자님을 내려다본다)
            body.localScale = Vector3.one * radius * 2f * (1f + 0.018f * Mathf.Sin(t * 1.3f));
            body.Rotate(0f, 3f * dt, 0f, Space.Self);
            Vector3 lookAt = phoneOut || near ? lens : CrowdBelow;   // 폰을 넣으면 하얀 천 쓴 눈꺼풀들을 내려다본다
            Vector3 d = lookAt - face.position;
            if (d.sqrMagnitude > 1e-4f)
                face.rotation = Quaternion.Slerp(face.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-(Locked || near ? 5f : 1.1f) * dt));
            float pulse = 1f + 0.15f * Mathf.Sin(t * 2.1f);
            EntityKit.SetEmission(irisMat, IrisGlow * pulse * (near ? 1.45f : 1f));

            // 보조 눈: 잠기기 전엔 제각각 두리번, 잠기면 일제히 렌즈로
            for (int i = 0; i < aux.Length; i++)
            {
                var a = aux[i];
                Vector3 goal = Locked || near
                    ? lens
                    : face.position + face.forward * 10f + new Vector3(Mathf.PerlinNoise(t * 0.3f, i) - 0.5f, Mathf.PerlinNoise(i, t * 0.27f) - 0.5f, 0f) * 14f;
                Vector3 ad = goal - a.position;
                if (ad.sqrMagnitude > 1e-4f)
                    a.rotation = Quaternion.Slerp(a.rotation, Quaternion.LookRotation(ad), 1f - Mathf.Exp(-(Locked || near ? 14f : 2f) * dt));
            }

            // 촉수: 평소엔 단상 바닥·신도 머리 위로 늘어뜨리고, 다가올 땐 몸에 매달려 따라온다
            hang = Mathf.MoveTowards(hang, near || Current == State.Retreat ? 1f : 0f, dt * 0.8f);
            var tr = root.transform;
            for (int i = 0; i < tents.Length; i++)
            {
                Vector3 b = tr.position + tentBaseLocal[i];
                Vector3 sway = new Vector3(Mathf.Sin(t * 0.5f + i), 0f, Mathf.Cos(t * 0.43f + i * 1.7f)) * 0.6f;
                Vector3 hangTip = tr.position + new Vector3(tentBaseLocal[i].x * 1.5f, -7.5f, tentBaseLocal[i].z * 1.5f);
                tents[i].basePos = b;
                tents[i].tipPos = Vector3.Lerp(tentStageTip[i], hangTip, hang) + sway;
            }

            // 빛: 붉은 빛은 아래(단상·신도), 금빛은 동공 앞
            gold.transform.position = iris.position + face.forward * 2f;
            red.intensity = Mathf.Lerp(red.intensity, near ? 8f : 6f, dt * 2f);
            gold.intensity = Mathf.Lerp(gold.intensity, near ? 6f : 4f, dt * 2f);
        }

        /// <summary>'드르륵' — 수십 개 눈알이 한꺼번에 도는 소리 (코드로 만든 짧은 딸깍 연타).</summary>
        static AudioClip MakeRattle()
        {
            const int rate = 22050;
            int n = (int)(rate * 0.65f);
            var data = new float[n];
            var rnd = new System.Random(9);
            float clickEvery = rate / 30f;
            for (int i = 0; i < n; i++)
            {
                float tt = i / (float)rate;
                float env = Mathf.Clamp01(1f - tt / 0.65f);
                float ph = (i % clickEvery) / clickEvery;
                float click = Mathf.Exp(-ph * 26f);
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float body = Mathf.Sin(2f * Mathf.PI * 140f * tt) * 0.35f;
                data[i] = (noise * 0.8f + body) * click * env * 0.6f;
            }
            var clip = AudioClip.Create("EntityRattle", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
