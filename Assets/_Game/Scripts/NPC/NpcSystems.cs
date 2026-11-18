using UnityEngine;
using UnityEngine.SceneManagement;

namespace UndercoverLive
{
    /// <summary>
    /// NPC·검문 시스템(검문·뺏기 연출·일어섬 반응)을 씬마다 한 번 코드로 붙인다. 씬 파일은 건드리지 않는다.
    /// 예배당 씬(SuspicionManager가 있는 씬)에서만 만든다.
    /// </summary>
    public static class NpcSystems
    {
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!hooked) { SceneManager.sceneLoaded += OnSceneLoaded; hooked = true; }
            Ensure();
        }

        // RuntimeInitializeOnLoadMethod는 앱 시작 때 한 번뿐 → R로 씬을 다시 불러도 붙도록
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Ensure(); }

        public static void Ensure()
        {
            if (Object.FindAnyObjectByType<Checkpoint>() != null) return;
            if (Object.FindAnyObjectByType<SuspicionManager>() == null) return;
            var go = new GameObject("NPC Systems");
            go.AddComponent<ConfiscateFX>();
            go.AddComponent<Checkpoint>();
        }
    }

    /// <summary>NPC 대사·안내 문구를 한 곳에 모은다(고칠 때 여기만).</summary>
    public static class NpcLines
    {
        public const string Staff = "관리자";
        public const string Believer = "눈꺼풀";
        public const string Leader = "눈동자님";

        public const string Approaching = "관리자가 다가와요";
        public const string StaffBlocking = "관리자들이 막으러 와요";
        public const string AskPhone = "형제님, 잠깐 폰 좀 볼까요?";
        public const string Passed = "…네, 바깥 눈은 입구에 맡기셔야 합니다. 앉으세요.";
        public const string FinalTake = "…아니요. 이건 제가 맡아 두겠습니다.";
        public const string TakeNow = "형제님, 폰 이리 주시죠.";
        public const string SitDown = "형제님, 자리에 앉으세요.";
        public const string BackToSeat = "형제님, 자리로 돌아가시죠.";
        public const string Stop = "멈추세요, 형제님.";
        public const string LeaderCallOut = "…거기, 유리 눈을 품은 분.";
        public const string ConfiscatedReason = "관리자가 폰을 빼앗았습니다";

        public static readonly string[] Whispers =
        {
            "(수군수군) 저 형제님 왜 일어나…",
            "(소곤소곤) 말씀 중에 어딜 가시는 거지…",
            "(수군수군) 새로 오신 분인가 봐…",
            "(소곤소곤) 눈동자님이 보고 계시는데…",
        };

        // 상단 검문 배너 (위 가운데: 경고 배너(B)와 같은 자리, 검문 중엔 이 배너가 우선)
        public const string BannerInspectLabel = "관리자 검문";
        public const string BannerInspectMain = "폰을 보려 해요 · 지금 폰을 숨기세요";
        public const string BannerInspectKey = "4";   // 숨기기(폰 넣기) 키 = 4 (Paul 지시)
        public const string BannerInspectPill = "숨기기 · {0:0.0}초";
        public const string BannerSitLabel = "관리자 제지";
        public const string BannerSitMain = "자리에 앉으라고 해요";
        public const string BannerSitKey = "F";
        public const string BannerSitPill = "앉기 · {0:0.0}초";
        public const string BannerPassLabel = "검문 통과";
        public const string BannerPassMain = "넘어갔지만 경계가 올라갔어요 · 경계 {0} / {1}";
        public const string SignalLost = "신호 끊김";

        public static string Direction(string dir) => $"{dir} {Approaching}";
    }
}
