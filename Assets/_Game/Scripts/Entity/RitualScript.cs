namespace UndercoverLive
{
    /// <summary>
    /// 의례 시간표·대사·찍을 대상 표를 한곳에 모은 데이터. 대사를 고칠 땐 이 파일만 고친다.
    /// 정본: Argon 이야기안(out-story2.json) + 강림 구간 확정 대사(Argon 재작성).
    /// 시간은 의례 시작부터의 초. 7:00 = 420초에 끝난다.
    /// </summary>
    public static class RitualScript
    {
        public const string Leader = "눈동자님";
        public const string Members = "눈꺼풀들";
        public const string Staff = "관리자";
        public const float EndTime = 420f;
        public const string EndReason = "집회가 끝났습니다";

        // ---------- 구간 ----------
        public sealed class Phase
        {
            public readonly string key, name, chatCue;
            public readonly float start;
            public Phase(string key, string name, float start, string chatCue) { this.key = key; this.name = name; this.start = start; this.chatCue = chatCue; }
        }

        /// <summary>6구간. key는 ServiceDirector.CurrentPhaseKey로 노출된다(소리 담당이 분위기 음향을 맞춘다).</summary>
        public static readonly Phase[] Phases =
        {
            new Phase("entry",    "착석과 개회 — 바깥 빛 털어내기",     0f,   "phase_entry"),
            new Phase("chant",    "교리 복창 — 눈꺼풀 다짐",            60f,  "phase_sermon"),
            new Phase("offering", "바깥 짐 비움 점검",                  135f, "phase_sermon"),
            new Phase("anoint",   "특별 눈 맞춤 — 바깥 눈 지우기",      220f, "phase_sermon"),
            new Phase("descent",  "강림 — 하얀 천과 겹눈 본체",         300f, "phase_blindfold"),
            new Phase("closing",  "폐회와 소등 선포",                   375f, "phase_closing"),
        };

        // ---------- 대사 ----------
        public sealed class Line
        {
            public readonly float t, seconds;
            public readonly string who, text, soundId;
            public Line(float t, string who, string text, float seconds, string soundId = null) { this.t = t; this.who = who; this.text = text; this.seconds = seconds; this.soundId = soundId; }
        }

        /// <summary>
        /// 자막 대사. soundId가 있으면 그 말이 이어지는 동안 녹음 대상(위험 발언)이 된다.
        /// 5:00~6:15 강림 구간은 Argon 확정본(교주는 떠오르지 않는다).
        /// </summary>
        public static readonly Line[] Lines =
        {
            new Line(15f,  Leader,  "사랑하는 눈꺼풀 여러분, 오늘도 바깥의 어지러운 빛에 많이 지치셨지요?", 6f),
            new Line(65f,  Leader,  "주머니 속 차가운 유리 눈은 우릴 속입니다. 입구에 다 맡기셨지요?", 6.5f, "sermon"),
            new Line(95f,  Members, "내 눈을 덮고 눈동자님만 본다. 내 눈을 덮고 눈동자님만 본다.", 5f),
            new Line(125f, Leader,  "육신의 눈은 늘 흔들리고 의심합니다. 두 눈을 깜빡이는 눈꺼풀이 되어야 비로소 평안이 찾아옵니다.", 7f),
            new Line(155f, Leader,  "아직 가족이나 통장 숫자를 보는 분이 있군요. 바깥 눈은 곧 멉니다.", 6f, "sermon_bankbook"),
            new Line(195f, Leader,  "바깥 짐을 다 비워야 어두운 날에 걸려 넘어지지 않습니다. 아직 손에 쥔 숫자가 있다면 지금 다 내려놓으세요.", 7.5f),
            new Line(226f, Leader,  "이리 나와 무릎을 꿇으세요. 당신을 어지럽히던 바깥의 잔상을 제가 직접 거두어 드리겠습니다.", 7f),
            // 강림 (Argon 확정)
            new Line(300f, Leader,  "겹눈의 집 천장이 열리고, 맨눈으로 감당할 수 없는 분이 내려오십니다.", 6f),
            new Line(315f, Staff,   "모든 눈꺼풀은 지금 하얀 천으로 두 눈을 깊게 덮고 고개를 숙이십시오!", 5f),
            new Line(330f, Leader,  "우리의 약한 눈으로는 그분을 뵐 수 없으니, 오직 살갗으로 온기를 느끼세요.", 6f),
            new Line(345f, Members, "두 눈을 덮고 바깥 짐을 모두 바칩니다. 그분의 시선만 우리를 채우소서.", 6f),
            new Line(360f, Leader,  "절대 하얀 천을 들추거나 차가운 유리 눈으로 저 위를 훔쳐보지 마세요.", 6f),
            new Line(372f, Leader,  "천을 걷고 훔쳐보는 자는 내려다보는 그 눈과 영원히 마주치게 됩니다.", 5f),
            // 폐회
            new Line(390f, Leader,  "세상이 소등되는 날까지 이 극장 문을 나가지 마세요. 제가 지켜봅니다.", 6.5f, "sermon_lockdown"),
        };

        // ---------- 그 밖의 신호 ----------
        public sealed class Cue
        {
            public readonly float t; public readonly string key;
            public Cue(float t, string key) { this.t = t; this.key = key; }
        }

        /// <summary>
        /// 시간 신호. "chat:키" = GameEvents.RaiseChatCue(키). 나머지는 ServiceDirector가 처리.
        /// </summary>
        public static readonly Cue[] Cues =
        {
            new Cue(232f, "leader_look_kneeler"),   // 교주가 무릎 꿇은 눈꺼풀을 내려다봄
            new Cue(304f, "chat:eye_vs_phone"),     // 강림 시작 직후 한 번
            new Cue(318f, "chat:phase_miracle"),    // 거대 겹눈이 내려와 떠 있음
        };

        // ---------- 시간 창 ----------
        public const float BlindfoldOn = 315f;     // 관리자 지시와 함께
        public const float BlindfoldOff = 375f;
        public const float PillarFrom = 308f, PillarTo = 376f;
        public const float EntityDescend = 305f, EntityHover = 318f, EntityFrom = 315f, EntityTo = 375f;
        public const float VictimWalk = 230f;      // 무릎 꿇을 눈꺼풀이 단상으로 걸어 나감

        /// <summary>현상별 보이는 시간 창 [시작, 끝) 쌍.</summary>
        public static readonly float[] WinPortrait = { 0f, 90f, 375f, 420f };   // 폐회 때 잔상으로 다시
        public static readonly float[] WinBanner   = { 30f, 420f };
        public static readonly float[] WinCeiling  = { 30f, 420f };
        public static readonly float[] WinThreads  = { 60f, 150f };
        public static readonly float[] WinShadow   = { 120f, 210f };
        public static readonly float[] WinDonation = { 150f, 240f };
        public static readonly float[] WinFloor    = { 210f, 300f };
        public static readonly float[] WinKneel    = { 250f, 300f };

        public static bool InWindow(float[] w, float t)
        {
            for (int i = 0; i + 1 < w.Length; i += 2) if (t >= w[i] && t < w[i + 1]) return true;
            return false;
        }

        // ---------- 역관측 ----------
        public const string StareLeaderLine = "거기, 하얀 천을 쓰지 않은 차가운 유리 눈이 있군요!";
        public const string StareEndReason = "렌즈 너머의 눈과 마주쳤습니다";
        public const string StareChatSpam = "내 눈을 덮고 눈동자님만 본다";
        public const float StareFilmSeconds = 3f;     // 강림 중 누적 촬영 시간이 넘으면 내려온다
        public const float StareGrace = 1.5f;         // 확보 뒤 이 안에 폰을 내려야 산다
        public const float StareMaxClose = 6.5f;      // 확보 못 해도 코앞에서 이만큼 버티면 끝

        // ---------- 달성판 ----------
        public const string DonationNames = "미납  김○○ 3,200만 · 박○○ 1,750만 · 이○○ 980만 · 최○○ 640만";
        public const string CountdownFormat = "바깥 소등까지 {0}일";
        public static readonly string[] CountdownFrenzy = { "41", "19", "07", "00" };

        // ---------- 찍을 대상 표 (Argon 점수표) ----------
        public sealed class TargetSpec
        {
            public readonly string id, name;
            public readonly CaptureCategory category;
            public readonly float views, requiredHeight, hold, evidence;
            public TargetSpec(string id, string name, CaptureCategory c, float views, float reqH, float hold, float evidence)
            { this.id = id; this.name = name; category = c; this.views = views; requiredHeight = reqH; this.hold = hold; this.evidence = evidence; }
        }

        // 보도 가치 척도: 낮음 0.1 · 보통 0.4 · 높음 0.8 · 매우 높음 1.0 · 최상 1.2
        public static readonly TargetSpec[] Targets =
        {
            new TargetSpec("portrait_eyes",     "초상화의 두 고리와 따라오는 눈",     CaptureCategory.Atmosphere, 800,   0.18f, 1.5f, 0.1f),
            new TargetSpec("banner",            "현수막의 속뜻과 글자 사이 눈알",     CaptureCategory.Evidence,   3500,  0.10f, 2f,   0.8f),
            new TargetSpec("ceiling_eyes",      "천장 구석의 붉은 눈 덩어리",         CaptureCategory.Atmosphere, 1500,  0.08f, 1.5f, 0.1f),
            new TargetSpec("threads",           "정수리에 꽂힌 줄기와 꺾이는 고개",   CaptureCategory.Atmosphere, 2500,  0.25f, 1.5f, 0.4f),
            new TargetSpec("donation_board",    "달성판을 움켜쥔 촉수와 미납자 명단", CaptureCategory.Evidence,   5000,  0.25f, 2f,   0.8f),
            new TargetSpec("shadow",            "눈동자님 등 뒤 일곱 눈 그림자",      CaptureCategory.Atmosphere, 4000,  0.30f, 2f,   0.1f),
            new TargetSpec("floor_tentacles",   "통로 바닥을 기는 그림자 촉수",       CaptureCategory.Atmosphere, 3000,  0.20f, 1.5f, 0.1f),
            new TargetSpec("kneeling_tentacle", "무릎 꿇은 눈꺼풀을 감싼 촉수",       CaptureCategory.Climax,     8000,  0.25f, 2.5f, 0.8f),
            new TargetSpec("entity",            "천장의 거대 겹눈",                   CaptureCategory.Climax,     35000, 0.30f, 3f,   1.0f),
            new TargetSpec("entity_stare",      "렌즈 앞까지 내려온 거대 동공",       CaptureCategory.Climax,     50000, 0.50f, 3f,   1.2f),
            new TargetSpec("sermon",            "설교 녹음: 차가운 유리 눈 통제",     CaptureCategory.Sound,      2500,  0.25f, 0f,   0.8f),
            new TargetSpec("sermon_bankbook",   "설교 녹음: 통장 숫자 협박",          CaptureCategory.Sound,      3500,  0.25f, 0f,   0.8f),
            new TargetSpec("sermon_lockdown",   "설교 녹음: 극장 문 감금 예고",       CaptureCategory.Sound,      4500,  0.25f, 0f,   0.8f),
            new TargetSpec("leader",            "설교하는 눈동자님",                  CaptureCategory.Atmosphere, 1500,  0.30f, 1.5f, 0.1f),
            new TargetSpec("crowd",             "넋 놓고 앉은 눈꺼풀들",              CaptureCategory.Atmosphere, 900,   0.30f, 1.5f, 0.1f),
        };

        public static TargetSpec Spec(string id)
        {
            foreach (var s in Targets) if (s.id == id) return s;
            return null;
        }

        /// <summary>표의 값을 CaptureTarget에 넣는다. idOverride가 있으면 id만 바꾼다(ceiling_eyes_0 등).</summary>
        public static void Apply(CaptureTarget t, string specId, string idOverride = null)
        {
            var s = Spec(specId);
            if (t == null || s == null) return;
            t.targetId = idOverride ?? s.id;
            t.displayName = s.name;
            t.category = s.category;
            t.baseViews = s.views;
            t.requiredHeight = s.requiredHeight;
            t.holdSeconds = s.hold;
            t.evidenceValue = s.evidence;
        }
    }
}
