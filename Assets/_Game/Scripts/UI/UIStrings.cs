namespace UndercoverLive
{
    /// <summary>
    /// 화면 문구를 한 곳에 모은다. 설정이 "카메라에만 찍히는 눈동자 존재"로 바뀌는 중이라
    /// 기적·증거를 설명하는 문구는 확정 전 임시다(표시: [설정 미정]). 여기만 갈아끼우면 모든 화면이 따라 바뀐다.
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

        // ---------- 메인 화면 ----------
        public static string MenuTag = "ON AIR 준비";
        public static string MenuTagSub = "1인칭 잠입 생중계";
        /// <summary>[설정 미정] 두 줄 소개.</summary>
        public static string MenuTagline = "맨눈엔 평온한 집회가, 숨긴 폰엔 다른 것이 찍힙니다.\n들키지 않고 찍어서 끝까지 버티세요.";
        public static string MenuStart = "잠입 시작";
        public static string MenuStartHint = "클릭하면 바로 입장";
        public static string MenuControls = "조작법";
        public static string MenuControlsHint = "폰 들기 · 몰래 찍기 · 줌";
        public static string MenuSettings = "설정";
        public static string MenuSettingsHint = "감도 · 소리 · 밝기";
        public static string MenuRecIdle = "REC 대기";
        public static string MenuLiveIdle = "LIVE 0";
        public static string MenuBattery = "배터리 100%";
        public static string MenuTarget = "TARGET · 눈동자님";
        /// <summary>메인 화면 아래 조작 요약: (키, 설명) 쌍.</summary>
        public static string[] MenuKeyHints = { "마우스", "둘러보기", "우클릭", "폰 들기", "E", "몰래 찍기", "Esc", "멈춤" };

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
        /// <summary>[설정 미정] 오늘의 목표 체크리스트.</summary>
        public static string[] Objectives = { "교주가 떠오르는 순간", "천장의 붉은 점", "현수막의 속뜻", "설교 녹음" };
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

        // ---------- 조작법 ----------
        public static string ControlsTitle = "조작법";
        public static string ControlsPhoneTitle = "폰 쓰는 법";
        public static string ControlsKeysTitle = "키 안내";
        public static string Close = "닫기";
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
            "휠", "줌", "폰을 든 채 멀리 있는 글자를 읽습니다",
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
        /// <summary>엔딩 판정 3종: (탭 이름, 큰 문장, 설명, 탭 보조 문구). 순서 = 보도·조회수·빈손. [설정 미정]</summary>
        public static string[] Verdicts =
        {
            "보도", "보도 — 방송사 제보가 들어왔습니다", "확보한 영상이 보도 가치 기준을 넘겼습니다. 탐사보도팀이 제보를 받아 후속 취재를 시작합니다.", "방송사 제보 · 후속 취재",
            "조회수", "조회수 — 구독자가 폭증했습니다", "영상은 크게 퍼졌지만 증거로는 부족합니다. 교단이 반박 영상을 올렸습니다.", "구독자 폭증 · 교단 반박",
            "빈손", "빈손 — 조회수 132회", "건질 만한 장면이 없었습니다. 영상은 조용히 묻혔습니다.", "조회수 132회 · 묻힌 영상",
        };
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
        public static string MetricNewsReached = "보도 기준을 넘겼습니다";
        public static string MetricNewsShortFormat = "보도 기준까지 {0:0.0} 부족";
        public static string VerdictMetaFormat = "집회 완주 · 클립 {0}개 확보";
        /// <summary>클립 종류 표시. CaptureCategory 순서(분위기·증거·결정적 장면·녹음).</summary>
        public static string[] ClipCategories = { "분위기", "증거", "결정적 장면", "녹음" };
        public static string ClipsOrderFormat = "시간순 {0}개";
        public static string SlotGuideBadgeFormat = "남은 슬롯 {0} / {1}";
        public static string SlotEmptyFormat = "빈 슬롯 #{0:00}";
        public static string ClipViewersFormat = "+{0:N0} 시청자";
        public static string ClipNewsFormat = "보도 가치 +{0:0.0}";
        public static string ClipsHeader = "확보한 클립 다시 보기";
        public static string EmptyClipsTitle = "확보한 클립이 없습니다";
        public static string EmptyClipsBody = "다음 잠입에서는 폰을 들어 결정적인 장면을 찍어 보세요.";
        public static string SlotGuideTitle = "놓친 장면은 다음 잠입에서";
        public static string SlotGuideFoot = "결과에는 클립이 6개까지 남습니다";
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

        // ---------- 발각 ----------
        public static string CaughtBadge = "발각 엔딩 · 폰을 빼앗김";
        public static string CaughtTitle = "방송이 종료되었습니다";
        public static string CaughtBody = "관리자에게 녹화 중인 폰을 빼앗겼습니다.\n찍은 영상과 녹음은 모두 압수되어 올릴 수 없습니다.";
        public static string CaughtDefaultReason = "관리자가 폰을 빼앗았습니다";
        public static string CaughtReasonLabel = "발각 원인";
        public static string CaughtLostLabel = "압수된 기록";
        public static string CaughtLostFormat = "확보한 클립 {0}개 · 녹음 전부 (업로드 불가)";
        public static string CaughtTipLabel = "잠입 팁";
        public static string CaughtTip = "폰을 든 동안엔 주변이 안 보입니다. 채팅이 \"폰 내려요\" 하면 바로 우클릭으로 내리세요.";
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
    }
}
