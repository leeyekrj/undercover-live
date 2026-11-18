using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UndercoverLive
{
    /// <summary>
    /// 의례 시간표 (7분). 착석 → 복창 → 바깥 짐 점검 → 특별 눈 맞춤 → 강림 → 폐회.
    /// 대사·시각·대상 표는 RitualScript 한곳에. 기이현상(Phenomena)·거대 겹눈(EyeEntity)·사람 동작(RitualCrowd)은
    /// 이 스크립트의 RitualTime을 보고 움직인다. GameEvents.IsPlaying일 때만 시간이 흐른다.
    /// 옛 '와이어·커튼 뒤 조수' 속임수는 런타임에 걷어 낸다(씬은 그대로).
    /// 개발용: M = 강림으로 건너뛰기, N = 다음 구간으로.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ServiceDirector : MonoBehaviour
    {
        public static ServiceDirector Instance { get; private set; }

        [Header("연결 (씬 빌더가 채움)")]
        [Tooltip("옛 빌더 호환 — 지금 씬에선 교주를 가리킨다. 쓰지 않음")]
        public Transform victim;
        [Tooltip("맨눈에만 보이는 빛기둥 묶음 (EyeOnly)")]
        public GameObject eyeOnlyMiracle;
        [Tooltip("옛 속임수 — 런타임에 끈다")]
        public Transform wire;
        [Tooltip("옛 속임수 조명 — 런타임에 끈다")]
        public Light trickSpot;
        public LeaderGaze leaderGaze;
        [Tooltip("위험 발언 녹음 대상 1: 차가운 유리 눈 통제")]
        public CaptureTarget sermonSound;
        [Tooltip("옛 공중부양 대상 — 런타임에 끈다")]
        public CaptureTarget miracleTarget;
        [Tooltip("신도 머리의 흰 눈가리개(하얀 천)")]
        public GameObject[] blindfolds;

        // 옛 씬 빌더(CultDressing.ApplyCopy)가 채우는 칸. 대사는 RitualScript가 정본이라 쓰지 않는다.
        [HideInInspector] public string leaderTitle = RitualScript.Leader;
        [HideInInspector] public string membersLabel = RitualScript.Members;
        [HideInInspector] public string chant, lineOpening, linePhoneBan, lineBlindfold, lineMiracle, lineWarning, lineClosing;

        public string endReason = RitualScript.EndReason;

        [Header("개발용")]
        [Tooltip("M = 강림으로 건너뛰기, N = 다음 구간으로")]
        public bool devKeys = true;

        /// <summary>시간표가 돌고 있음 (시작했고 끝나거나 멈추지 않음).</summary>
        public bool Running { get; private set; }
        public bool Started { get; private set; }
        /// <summary>폰 압수·발각·역관측 엔딩으로 멈춤.</summary>
        public bool Halted { get; private set; }
        public bool Finished { get; private set; }
        /// <summary>신도들이 하얀 천을 쓰고 있음 (신도 시야 0).</summary>
        public bool Blindfolded { get; private set; }
        /// <summary>의례 시작부터 흐른 초 (멈춤 동안 안 흐름). 0~420.</summary>
        public float RitualTime { get; private set; }
        public int PhaseIndex { get; private set; } = -1;
        /// <summary>entry · chant · offering · anoint · descent · closing (시작 전 "").</summary>
        public string CurrentPhaseKey => PhaseIndex >= 0 ? RitualScript.Phases[PhaseIndex].key : "";
        public string CurrentPhaseName => PhaseIndex >= 0 ? RitualScript.Phases[PhaseIndex].name : "";
        /// <summary>구간이 바뀔 때 (key).</summary>
        public event System.Action<string> PhaseStarted;

        int lineIdx, cueIdx;
        bool checkpointActive; float checkpointSince;
        readonly Dictionary<string, CaptureTarget> sounds = new Dictionary<string, CaptureTarget>();
        float[] blindDelay;
        bool[] blindOn;
        Transform pillar;
        float pillarGrow;
        bool crowdFocusSet;
        SuspicionManager suspicion;

        void Awake()
        {
            Instance = this;
#if !UNITY_EDITOR
            // 공개판에서는 건너뛰기 키(M·N)를 끈다. 검수 주소(?qa=1)에서만 켠다.
            devKeys = (Application.absoluteURL ?? "").Contains("qa=1");
#endif
        }

        void Start()
        {
            EntityKit.ResetCount();
            RetireOldTrick();
            SetupTargets();
            SetupPillar();

            int n = blindfolds != null ? blindfolds.Length : 0;
            blindDelay = new float[n]; blindOn = new bool[n];
            var rnd = new System.Random(3);
            for (int i = 0; i < n; i++) { blindDelay[i] = (float)rnd.NextDouble() * 4f; if (blindfolds[i] != null) blindfolds[i].SetActive(false); }

            var sys = new GameObject("RitualSystems");
            var crowd = sys.AddComponent<RitualCrowd>(); crowd.Init(this);
            var phen = sys.AddComponent<Phenomena>(); phen.Init(this, crowd);
            var ent = sys.AddComponent<EyeEntity>(); ent.Init(this, crowd);
            EntityStareFX.Ensure();

            GameEvents.CheckpointStarted += OnCheckpointStarted;
            GameEvents.CheckpointEnded += OnCheckpointEnded;
            GameEvents.Confiscated += OnConfiscated;
            suspicion = SuspicionManager.Instance;
            if (suspicion != null) suspicion.Caught += OnCaught;

            Debug.Log($"[Ritual] 준비 끝: {Phenomena.Stats} · 기준 머티리얼 {EntityKit.BaseNames}");
        }

        void OnDestroy()
        {
            GameEvents.CheckpointStarted -= OnCheckpointStarted;
            GameEvents.CheckpointEnded -= OnCheckpointEnded;
            GameEvents.Confiscated -= OnConfiscated;
            if (suspicion != null) suspicion.Caught -= OnCaught;
            if (crowdFocusSet) Watcher.CrowdFocus = 1f;
            if (Instance == this) Instance = null;
        }

        void OnCheckpointStarted(float s) { checkpointActive = true; checkpointSince = Time.time; }
        void OnCheckpointEnded(bool passed) { checkpointActive = false; }
        void OnConfiscated(string reason) { Halt(); }
        void OnCaught() { Halt(); }

        // ---------- 옛 속임수 걷기 ----------
        void RetireOldTrick()
        {
            var trick = GameObject.Find("Trick_PhoneOnly");
            if (trick != null) trick.SetActive(false);
            if (wire != null) wire.gameObject.SetActive(false);
            if (trickSpot != null) trickSpot.enabled = false;
            if (miracleTarget != null) { miracleTarget.activeNow = false; miracleTarget.enabled = false; }
            foreach (var ct in FindObjectsByType<CaptureTarget>(FindObjectsInactive.Include))
            {
                string id = ct.Id;
                if (id == "miracle_wire" || id == "assistant" || id.StartsWith("cctv")) { ct.activeNow = false; ct.enabled = false; }
            }
        }

        // ---------- 대상 ----------
        void SetupTargets()
        {
            foreach (var ct in FindObjectsByType<CaptureTarget>())
            {
                if (!ct.enabled) continue;
                if (ct.Id == "leader") RitualScript.Apply(ct, "leader");
                else if (ct.Id == "crowd") RitualScript.Apply(ct, "crowd");
            }
            Vector3 pos = sermonSound != null ? sermonSound.transform.position : new Vector3(0f, 2.63f, 12.6f);
            float radius = sermonSound != null ? Mathf.Max(16f, sermonSound.hearingRadius) : 16f;
            if (sermonSound != null) { RitualScript.Apply(sermonSound, "sermon"); sermonSound.hearingRadius = radius; sounds["sermon"] = sermonSound; }
            foreach (var id in new[] { "sermon_bankbook", "sermon_lockdown" })
            {
                var go = new GameObject("SermonSound_" + id);
                go.transform.position = pos;
                var t = go.AddComponent<CaptureTarget>();
                RitualScript.Apply(t, id);
                t.hearingRadius = radius;
                t.activeNow = false;
                sounds[id] = t;
            }
        }

        public CaptureTarget SoundTarget(string id) => id != null && sounds.TryGetValue(id, out var t) ? t : null;

        // ---------- 맨눈 빛기둥 ----------
        void SetupPillar()
        {
            if (eyeOnlyMiracle == null)
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (go.name == "Miracle_EyeOnly" && go.scene.IsValid()) { eyeOnlyMiracle = go; break; }
            if (eyeOnlyMiracle == null) return;
            eyeOnlyMiracle.SetActive(false);
            pillar = eyeOnlyMiracle.transform.Find("LightPillar");
            if (pillar == null) return;
            int L = EntityKit.EyeOnly;
            // 옛 불투명 원기둥은 무대를 통째로 가려서 끄고, 틈이 있는 빛줄기 + 옅은 반투명 기둥으로 바꾼다.
            var old = pillar.GetComponent<Renderer>();
            if (old != null) old.enabled = false;
            pillar.localScale = Vector3.one;
            pillar.rotation = Quaternion.identity;
            // 무릎 꿇은 눈꺼풀(0, 11.45)과 눈동자님(0, 12.6)을 함께 감싸는 자리
            pillar.position = new Vector3(0f, 3.5f, 12.0f);
            var rays = EntityKit.Obj("Rays", pillar, EntityKit.Strips(), EntityKit.Glow(Color.black, new Color(3.0f, 2.35f, 1.2f), EyeTexture.GlowFade(), null, true), L);
            rays.transform.localScale = new Vector3(2.4f, 3.5f, 2.4f);
            var core = EntityKit.Obj("Core", pillar, EntityKit.Prim(PrimitiveType.Cylinder), EntityKit.Clear(new Color(1f, 0.86f, 0.55f, 0.14f)), L);
            core.transform.localScale = new Vector3(1.7f, 3.5f, 1.7f);
            var lgo = EntityKit.Group("WarmSpot", pillar, L);
            lgo.transform.position = new Vector3(0f, 6.9f, 12.0f);
            lgo.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Spot; l.color = new Color(1f, 0.84f, 0.55f); l.intensity = 10f; l.range = 10f; l.spotAngle = 42f; l.innerSpotAngle = 20f; l.shadows = LightShadows.None;
            lgo.AddComponent<CameraOnlyLight>().who = CameraOnlyLight.Who.Eye;
            pillar.localScale = new Vector3(1f, 0.001f, 1f);
        }

        // ---------- 매 프레임 ----------
        void Update()
        {
            DevKeys();
            if (Finished || Halted) return;
            if (!GameEvents.IsPlaying) return;
            if (!Started) Begin();

            RitualTime += Time.deltaTime;
            Advance();
            UpdateWorld();
            if (RitualTime >= RitualScript.EndTime) TryFinish();
        }

        void Begin()
        {
            Started = true;
            Running = true;
        }

        void Advance()
        {
            var phases = RitualScript.Phases;
            while (PhaseIndex + 1 < phases.Length && phases[PhaseIndex + 1].start <= RitualTime) EnterPhase(PhaseIndex + 1);
            var lines = RitualScript.Lines;
            while (lineIdx < lines.Length && lines[lineIdx].t <= RitualTime) Fire(lines[lineIdx++]);
            var cues = RitualScript.Cues;
            while (cueIdx < cues.Length && cues[cueIdx].t <= RitualTime) FireCue(cues[cueIdx++].key);
        }

        void EnterPhase(int i)
        {
            PhaseIndex = i;
            var p = RitualScript.Phases[i];
            if (!string.IsNullOrEmpty(p.chatCue)) GameEvents.RaiseChatCue(p.chatCue);
            PhaseStarted?.Invoke(p.key);
        }

        void Fire(RitualScript.Line line)
        {
            GameFlow.Instance?.Say(line.who, line.text, line.seconds);
            var s = SoundTarget(line.soundId);
            if (s != null) s.BeginSound(line.seconds);
        }

        void FireCue(string key)
        {
            if (key.StartsWith("chat:")) { GameEvents.RaiseChatCue(key.Substring(5)); return; }
            if (key == "leader_look_kneeler" && leaderGaze != null)
                leaderGaze.LookAt(RitualCrowd.KneelSpot + Vector3.up * 0.8f, 18f);
        }

        void UpdateWorld()
        {
            float t = RitualTime;
            // 하얀 천: 관리자 지시(5:15)에 맞춰 몇 초에 걸쳐 하나둘 쓰고, 폐회 때 벗는다
            if (blindfolds != null)
                for (int i = 0; i < blindfolds.Length; i++)
                {
                    bool on = t >= RitualScript.BlindfoldOn + blindDelay[i] && t < RitualScript.BlindfoldOff + blindDelay[i] * 0.5f;
                    if (on != blindOn[i] && blindfolds[i] != null) { blindOn[i] = on; blindfolds[i].SetActive(on); }
                }
            bool blind = t >= RitualScript.BlindfoldOn + 1f && t < RitualScript.BlindfoldOff;
            if (blind != Blindfolded || !crowdFocusSet)
            {
                Blindfolded = blind;
                Watcher.CrowdFocus = blind ? 0f : 1f;   // 하얀 천을 쓴 신도는 플레이어를 못 본다
                crowdFocusSet = true;
            }

            // 맨눈 빛기둥: 천장에서 내려와 무대를 감싸고, 폐회 때 사그라든다
            if (eyeOnlyMiracle != null && pillar != null)
            {
                bool on = t >= RitualScript.PillarFrom && t < RitualScript.PillarTo;
                pillarGrow = Mathf.MoveTowards(pillarGrow, on ? 1f : 0f, Time.deltaTime / (on ? 2.5f : 1.5f));
                bool vis = pillarGrow > 0.001f;
                if (eyeOnlyMiracle.activeSelf != vis) eyeOnlyMiracle.SetActive(vis);
                if (vis)
                {
                    float g = Mathf.SmoothStep(0f, 1f, pillarGrow);
                    pillar.localScale = new Vector3(1f, Mathf.Max(0.001f, g), 1f);
                    pillar.position = new Vector3(0f, 7f - 3.5f * g, 12.0f);
                }
            }
        }

        void TryFinish()
        {
            if (Finished || Halted) return;
            // 검문이 진행 중이면 끝날 때까지 기다린다 (안전장치 20초)
            if (checkpointActive && Time.time - checkpointSince < 20f) return;
            // 역관측 엔딩 연출 중이면 그쪽이 끝낸다
            if (EntityStareFX.Ending || (EyeEntity.Instance != null && EyeEntity.Instance.IsEnding)) return;
            if (SuspicionManager.Instance != null && SuspicionManager.Instance.IsCaught) { Halt(); return; }

            Finished = true;
            Running = false;
            if (crowdFocusSet) Watcher.CrowdFocus = 1f;
            PhoneController.Instance?.SetState(PhoneState.Pocket);
            var results = FindAnyObjectByType<ResultsScreen>();
            if (results != null) results.Show(endReason);
            GameEvents.RaiseServiceFinished();
        }

        void Halt()
        {
            if (Halted || Finished) return;
            Halted = true;
            Running = false;
        }

        // ---------- 개발용 ----------
        void DevKeys()
        {
            if (!devKeys || Finished || Halted || !GameEvents.IsPlaying) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.mKey.wasPressedThisFrame) JumpTo(PhaseStart("descent"));
            else if (kb.nKey.wasPressedThisFrame)
            {
                int next = PhaseIndex + 1;
                JumpTo(next < RitualScript.Phases.Length ? RitualScript.Phases[next].start : RitualScript.EndTime - 3f);
            }
        }

        static float PhaseStart(string key)
        {
            foreach (var p in RitualScript.Phases) if (p.key == key) return p.start;
            return 0f;
        }

        /// <summary>시간표를 앞으로 건너뛴다(개발·시험용). 건너뛴 대사·신호는 내보내지 않는다.</summary>
        public void JumpTo(float time)
        {
            if (!Started) Begin();
            if (time <= RitualTime) return;
            RitualTime = time;
            var lines = RitualScript.Lines;
            while (lineIdx < lines.Length && lines[lineIdx].t < time) lineIdx++;
            var cues = RitualScript.Cues;
            while (cueIdx < cues.Length && cues[cueIdx].t < time) cueIdx++;
            var phases = RitualScript.Phases;
            int target = 0;
            for (int i = 0; i < phases.Length; i++) if (phases[i].start <= time) target = i;
            if (target - 1 > PhaseIndex) PhaseIndex = target - 1;   // 도착 구간의 채팅 신호만 보낸다
            Debug.Log($"[Ritual] 건너뛰기 → {time:0}초 ({phases[target].key})");
        }
    }
}
