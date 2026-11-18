namespace UndercoverLive
{
    /// <summary>
    /// 화면 문구를 한 곳에 모은다. 새 설정 = 교단의 금전·감금 착취 + 카메라에만 찍히는 거대한 눈동자 존재.
    /// 확정 전 임시 문구는 [설정 미정] 또는 [문구 미정] 표시. 여기만 갈아끼우면 모든 화면이 따라 바뀐다.
    /// 와이어·커튼 뒤 조수 같은 '속임수 폭로' 문구는 쓰지 않는다.
    /// </summary>
    public static class UIStrings
    {
        // ---------- 공통 ----------
        public static string AppName = "LIVEON STUDIO";          // 가상 방송 앱 이름 (실존 서비스 이름 금지)
        public static string GameTitle = "잠입 라이브";
        public static string CultName = "겹눈의 집";
        public static string LeaderTitle = "눈동자님";
        public static string CultCountdown = "바깥 소등까지 41일";
        public static string CultSlogan = "눈을 감아야 평안이 보인다";
        public static string PhoneBanQuote = "“주머니 속 차가운 유리 눈은 우릴 속입니다. 입구에 다 맡기셨지요?”";
        public static string Close = "닫기";
        /// <summary>키 조작 안내(메뉴·창 아래).</summary>
        public static string NavHint = "↑↓ 고르기  ·  Enter 누르기  ·  Esc 뒤로";
        public static string NavHintSlider = "↑↓ 고르기  ·  ←→ 조절  ·  Esc 닫기";

        // ---------- 메인 화면 ----------
        public static string MenuTag = "ON AIR 준비";
        public static string MenuTagSub = "1인칭 잠입 생중계";
        /// <summary>[설정 미정] 두 줄 소개.</summary>
        public static string MenuTagline = "맨눈엔 평온한 집회가, 숨긴 폰엔 다른 것이 찍힙니다.\n들키지 않고 찍어서 끝까지 버티세요.";
        public static string MenuStart = "잠입 시작";
        public static string MenuStartHint = "바로 입장";
        public static string MenuControls = "조작법";
        public static string MenuControlsHint = "폰 들기 · 몰래 찍기";
        public static string MenuSettings = "설정";
        public static string MenuSettingsHint = "감도 · 소리 · 밝기";
        public static string MenuRecIdle = "REC 대기";
        public static string MenuLiveIdle = "LIVE 0";
        public static string MenuBattery = "배터리 100%";
        public static string MenuTarget = "TARGET · 눈동자님";
        /// <summary>메인 화면 아래 조작 요약: (키, 설명) 쌍.</summary>
        public static string[] MenuKeyHints = { "마우스", "둘러보기", "우클릭", "폰 들기·내리기", "E", "몰래 찍기", "4", "폰 넣기", "Esc", "멈춤" };

        // ---------- 도입 ----------
        public static string IntroGateTitle = "입구 검문 — 겹눈의 집";
        public static string IntroGateBadge = "제출 완료 · 꺼진 공기계 1대";
        public static string IntroGateSpeaker = "안내 방송";
        public static string IntroGateNote = "바깥의 눈은 입구 바구니에 모두 맡기고 들어갑니다.";
        public static string IntroGateThought = "(공기계는 바구니에 냈다. 안주머니 속 진짜 폰은 아직 켜져 있다.)";
        public static string IntroPhoneStatusRec = "REC 00:01";
        public static string IntroPhoneStatusLive = "LIVE 연결됨";
        public static string IntroPhoneStatusMute = "무음";
        public static string IntroPhoneReady = "방송 준비 완료";
        public static string IntroPhoneReadySub = "시청자 0명 · 화면 밝기 최저";
        public static string IntroObjectivesTitle = "오늘 찍을 것";
        public static string IntroObjectivesSub = "숨긴 폰으로 담아 올 장면";
        /// <summary>[설정 미정] 오늘의 목표 체크리스트. ObjectiveTargetIds와 같은 순서로 짝.</summary>
        public static string[] Objectives = { "강림하는 그분의 본모습", "현수막의 속뜻", "바깥 짐 비움 명단", "위험한 설교 녹음" };
        /// <summary>목표와 짝이 되는 촬영 대상 id(앞부분 일치). 실제 id는 통합 때 맞춘다.</summary>
        public static string[] ObjectiveTargetIds = { "entity", "banner", "donation_board", "sermon" };
        public static string IntroRuleLabel = "잠입 수칙";
        public static string IntroRule = "눈동자님과 관리자의 시선을 피할 것 · 들키지 않고 집회가 끝날 때까지 버틸 것";
        public static string IntroSeated = "뒷줄에 앉았습니다 — 우클릭으로 폰을 드세요";
        public static string IntroNextHint = "클릭 · Space  다음";
        public static string IntroSkip = "건너뛰기";

        // ---------- 멈춤 ----------
        public static string PauseBadge = "REC 일시정지";
        public static string PauseTitle = "잠시 멈춤";
        public static string PauseResume = "계속하기";
        public static string PauseControlsHint = "키 안내 보기";
        public static string PauseRestart = "처음부터";
        public static string PauseRestartHint = "메인 화면으로";
        public static string Reloading = "다시 준비하는 중";

        // ---------- 조작법 ----------
        public static string ControlsTitle = "조작법";
        public static string ControlsPhoneTitle = "폰 쓰는 법";
        public static string ControlsKeysTitle = "키 안내";
        /// <summary>폰 상태 3가지: (이름, 키, 설명) 묶음.</summary>
        public static string[] PhoneModes =
        {
            "내림", "기본", "맨눈으로 다 보입니다. 주변 시선 표시가 보이고, 소리는 계속 녹음됩니다.",
            "몰래 찍기", "E", "가슴 높이에서 찍습니다. 폰 화면은 못 보고, 가까운 사람만 눈치챕니다.",
            "들기", "우클릭", "드는 순간 화면 전체가 폰입니다. 잘 찍히지만 보는 모두가 눈치챕니다.",
        };
        /// <summary>키 목록: (키, 동작, 설명) 묶음. 개발용 키(M·H)는 넣지 않는다.</summary>
        public static string[] ControlKeys =
        {
            "마우스", "둘러보기", "앉은 자리에서 고개를 돌립니다",
            "우클릭", "폰 들기 · 내리기", "드는 순간 화면 전체가 폰이 됩니다",
            "E", "몰래 찍기", "가슴 높이에서 찍습니다. 화면은 못 봅니다",
            "4", "폰 넣기", "어느 상태에서든 폰을 바로 주머니에",
            "휠 · -/+", "줌", "폰을 든 채 멀리 있는 글자를 읽습니다",
            "F", "일어서기 · 앉기", "일어서면 눈에 잘 띕니다",
            "Esc", "멈춤", "멈춤 메뉴 · 설정",
        };

        // ---------- 설정 ----------
        public static string SettingsTitle = "설정";
        public static string SettingsSensitivity = "마우스 감도";
        public static string SettingsSensitivityHint = "고개를 돌리는 빠르기";
        public static string SettingsVolume = "소리 크기";
        public static string SettingsVolumeHint = "설교·채팅 알림·발소리 전체";
        public static string SettingsBrightness = "밝기";
        public static string SettingsBrightnessHint = "화면이 어두우면 올리세요";
        public static string SettingsReset = "기본값으로";

        // ---------- 업로드 결과 ----------
        public static string StudioStatus = "스트림 종료 · 클립 자동 업로드 완료";
        public static string StudioTag = "잠입 취재 #01 · 겹눈의 집";
        public static string VerdictBadge = "최종 엔딩 판정";
        public static string VerdictPending = "판정 중";
        /// <summary>
        /// 엔딩 판정 3종(우선순위 순): (탭 이름, 큰 문장, 설명, 탭 보조 문구). 0 = 진실 보도, 1 = 오컬트 바이럴, 2 = 빈손.
        /// Argon 이야기안 '엔딩 4종'에서 줄였다. [설정 미정]
        /// </summary>
        public static string[] Verdicts =
        {
            "진실 보도", "진실 보도 — 소등 직전의 특종", "착취 명단과 감금 발언이 물증이 되어 탐사보도팀과 경찰이 '겹눈의 집'을 급습했습니다.", "압수수색 · 눈꺼풀 구출",
            "오컬트 바이럴", "오컬트 바이럴 — 조회수 200만 회", "영상은 알고리즘을 타고 퍼졌지만 범죄 증거가 모자라 수사는 없었습니다. \"역대급 공포 연출\"이라는 밈으로만 남았습니다.", "조회수 200만 · 밈으로 소비",
            "빈손", "빈손 — 흐릿한 해프닝", "조회수 148회. 댓글엔 \"그래서 뭐가 있다는 거임?\"뿐입니다. 교단의 비밀도, 천장의 존재도 아무도 믿지 않았습니다.", "조회수 148회 · 묻힌 영상",
        };
        /// <summary>진실 보도 엔딩의 뉴스 속보 띠.</summary>
        public static string NewsTickerLabel = "속보";
        public static string NewsTicker = "집회장 압수수색… 그러나 제보 영상 속 천장 거대 물체는 설명 불가";
        /// <summary>결정적 장면 대상 id(앞부분 일치). 진실 보도 조건.</summary>
        public static string[] DecisiveTargetIds = { "entity", "kneeling_tentacle" };
        /// <summary>거대 존재 대상 id(앞부분 일치). 오컬트 바이럴 조건·눈 깜박임 여운.</summary>
        public static string EntityTargetId = "entity";
        /// <summary>자동 제목 틀. {0} = 가장 좋은 클립 이름.</summary>
        public static string AutoTitleFormat = "[잠입] {0} 실제로 찍혔습니다";
        public static string AutoTitleEmpty = "[잠입] 아무것도 찍지 못했습니다";
        public static string AutoTitleEyebrow = "자동 생성 스트림 제목 · 가장 좋은 클립 기준";
        public static string UploadDone = "공개 업로드 완료";
        public static string UploadNone = "올릴 영상 없음";
        public static string MetricPeak = "최고 동시 시청자";
        public static string MetricClips = "확보한 클립";
        public static string MetricNews = "총 보도 가치";
        public static string MetricPeakChip = "PEAK LIVE";
        public static string MetricPeakSubFormat = "가장 큰 반응 · {0}";
        public static string MetricPeakSubNone = "시청자가 거의 없었습니다";
        public static string MetricClipsSubFormat = "영상 {0} · 녹음 {1}";
        public static string MetricNewsChipFormat = "기준 {0:0.0}";
        public static string MetricNewsReached = "보도 기준을 넘겼습니다 (대상별 최고값 합)";
        public static string MetricNewsShortFormat = "보도 기준까지 {0:0.0} 부족";
        public static string VerdictMetaFormat = "집회 완주 · 클립 {0}개";
        /// <summary>클립 종류 표시. CaptureCategory 순서(분위기·증거·결정적 장면·녹음).</summary>
        public static string[] ClipCategories = { "분위기", "증거", "결정적 장면", "녹음" };
        public static string ClipsOrderFormat = "시간순 {0}개";
        public static string SlotEmptyFormat = "빈 슬롯 #{0:00}";
        public static string SlotEmptyHint = "놓친 장면은 다음 잠입에서";
        public static string ClipViewersFormat = "+{0:N0} 시청자";
        public static string ClipNewsFormat = "보도 가치 +{0:0.0}";
        public static string ClipsHeader = "확보한 클립 다시 보기";
        public static string EmptyClipsTitle = "확보한 클립이 없습니다";
        public static string EmptyClipsBody = "다음 잠입에서는 폰을 들어 결정적인 장면을 찍어 보세요.";
        public static string ResultsRestart = "다시 하기";
        public static string ResultsRestartHint = "바로 다시 잠입하기";
        public static string ResultsMain = "메인으로";
        public static string ResultsMainHint = "메인 화면으로";
        /// <summary>업로드 댓글 닉네임 (가상).</summary>
        public static string[] CommentNicks = { "버블티덕후", "눈팅만3년", "호기심천국", "소리없는아우성", "오늘도야근", "익명의관찰자", "구독자0명", "새벽세시" };
        /// <summary>녹음 클립 카드에 띄우는 설교 한 줄.</summary>
        public static string[] SoundQuotes =
        {
            "“주머니 속 차가운 유리 눈은 우릴 속입니다.”",
            "“세상이 소등되는 날까지 이 극장 문을 나가지 마세요.”",
            "“바깥 눈은 곧 멉니다.”",
        };
        /// <summary>
        /// 대상 id(앞부분) → 결과 화면에 보일 이름. 씬에 구운 옛 이름 대신 새 설정 이름을 쓴다. 없으면 대상 이름 그대로.
        /// </summary>
        public static string[] TargetNames =
        {
            "entity", "강림하는 그분의 본모습",
            "kneeling_tentacle", "단상 앞 눈꺼풀의 촉수",
            "banner", "현수막의 속뜻",
            "donation_board", "바깥 짐 비움 명단",
            "sermon", "위험한 설교 녹음",
        };

        public static string TargetName(string id, string fallback)
        {
            if (!string.IsNullOrEmpty(id) && TargetNames != null)
                for (int i = 0; i + 1 < TargetNames.Length; i += 2)
                    if (id.StartsWith(TargetNames[i])) return TargetNames[i + 1];
            return fallback;
        }

        // ---------- 발각 (엔딩 1: 깨진 유리 눈) ----------
        public static string CaughtBadge = "발각 엔딩 · 깨진 유리 눈";
        public static string CaughtWhisper = "“차가운 유리 눈을 숨겨 오셨군요. 이제 당신도 따뜻한 눈꺼풀이 될 시간입니다.”";
        public static string CaughtTitle = "방송이 종료되었습니다";
        public static string CaughtBody = "관리자와 눈꺼풀들이 둘러싸 폰을 빼앗았습니다. 찍은 영상과 녹음은 모두 압수되었습니다.";
        public static string CaughtDefaultReason = "관리자가 폰을 빼앗았습니다";
        public static string CaughtReasonLabel = "발각 원인";
        public static string CaughtLostLabel = "압수된 기록";
        public static string CaughtLostFormat = "확보한 클립 {0}개 · 녹음 전부";
        public static string CaughtTipLabel = "잠입 팁";
        public static string CaughtTip = "채팅이 \"폰 내려요\" 하면 바로 4 키로 폰을 넣으세요.";
        public static string SignalLost = "SIGNAL LOST";
        public static string NoSignal = "신호 없음";
        public static string NoSignalSub = "카메라 입력이 끊겼습니다";
        public static string SystemNick = "시스템";
        public static string CaughtSystemLine = "방송 연결이 끊어졌습니다.";
        /// <summary>발각 화면의 마지막 채팅(StreamFeed.RecentLines가 생기기 전 대체). "닉네임|내용".</summary>
        public static string[] CaughtChatFallback =
        {
            "호기심천국|앞에 교주가 쳐다봐요!! 폰 내려요",
            "소리없는아우성|뒤에 관리자 와요!! 빨리 넣어요",
            "눈팅만3년|어어 손 뻗는다 ㄷㄷㄷ",
        };

        // ---------- 역관측 엔딩 (발각 변형: Confiscated 사유에 아래 말이 들어 있을 때) ----------
        public static string ReverseGazeKey = "렌즈 너머";
        public static string ReverseGazeBadge = "발각 엔딩 · 역관측";
        public static string ReverseGazeTitle = "눈이 마주쳤습니다";
        /// <summary>[문구 미정] 임시 문구.</summary>
        public static string ReverseGazeLine = "렌즈 너머의 눈이 당신을 보았습니다.";
        /// <summary>[문구 미정] 임시 문구.</summary>
        public static string ReverseGazeBody = "렌즈 너머의 눈이 당신을 보았습니다.";
    }
}
