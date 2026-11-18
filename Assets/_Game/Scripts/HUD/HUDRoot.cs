using UnityEngine;
using UnityEngine.SceneManagement;

namespace UndercoverLive
{
    /// <summary>
    /// 플레이 중 HUD 전체의 뿌리. 씬을 불러올 때마다(폰이 있는 씬이면) 한 번 만들어
    /// 캔버스 하나와 층(폰 화면 → 가장자리 → 몰래 폰 → 상태 → 키 안내)을 깔고 HUD 부품을 붙인다.
    /// GameEvents.Phase가 Playing일 때만 보인다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class HUDRoot : MonoBehaviour
    {
        public static HUDRoot Instance { get; private set; }

        public const int SortingOrder = 30;

        public Canvas Canvas { get; private set; }
        public RectTransform Rect { get; private set; }
        /// <summary>층 (아래 → 위).</summary>
        public RectTransform PhoneLayer, EdgeLayer, HiddenLayer, StatusLayer, HintLayer;
        /// <summary>캔버스 크기 (높이 1080 기준 단위).</summary>
        public Vector2 Size => Rect != null ? Rect.rect.size : new Vector2(1920, 1080);

        /// <summary>HUD가 완전히 보이는 중인가 (멈춤·메뉴로 사라지는 중이면 false).</summary>
        public bool Opaque => group != null && group.alpha >= 0.999f;

        CanvasGroup group;
        float glitchUntil = -99f;
        const float GlitchSeconds = 0.3f;

        // ---------- 자동 생성 ----------

        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { hooked = false; Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (s, m) => Create();
            }
            Create();
        }

        static void Create()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<HUDRoot>() != null) return;
            if (FindAnyObjectByType<PhoneController>() == null) return;   // 예배당 씬에만
            new GameObject("HUD").AddComponent<HUDRoot>();
        }

        // ---------- 구성 ----------

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Canvas = UIKit.Canvas("HUDCanvas", SortingOrder, transform);
            Rect = (RectTransform)Canvas.transform;
            group = Canvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = baseAlpha = GameEvents.IsPlaying ? 1f : 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Canvas.enabled = group.alpha > 0f;

            PhoneLayer = Layer("PhoneLayer");
            EdgeLayer = Layer("EdgeLayer");
            HiddenLayer = Layer("HiddenLayer");
            StatusLayer = Layer("StatusLayer");
            HintLayer = Layer("HintLayer");

            // 순서 중요: PhoneAppUI가 Awake에서 만든 자리(경고 배너·맥박 칸)를 WarningFX가 쓴다.
            gameObject.AddComponent<SceneLook>();
            gameObject.AddComponent<Heartbeat>();
            gameObject.AddComponent<ChatDirector>();
            gameObject.AddComponent<PhoneAppUI>();
            gameObject.AddComponent<HiddenPhoneUI>();
            gameObject.AddComponent<BroadcastWidget>();
            gameObject.AddComponent<KeyHints>();
            gameObject.AddComponent<WarningFX>();
        }

        void OnEnable() { GameEvents.CheckpointEnded += OnCheckpointEnded; }
        void OnDisable() { GameEvents.CheckpointEnded -= OnCheckpointEnded; }

        void OnDestroy()
        {
            GameEvents.CheckpointEnded -= OnCheckpointEnded;
            if (Instance == this) Instance = null;
        }

        // 검문 실패: 폰 UI(초점 상자·채팅·상태 줄)가 0.3초 흐트러진다. 뺏기 화면 연출은 C가 덮는다.
        void OnCheckpointEnded(bool passed) { if (!passed) glitchUntil = Time.unscaledTime + GlitchSeconds; }

        RectTransform Layer(string n)
        {
            var rt = UIKit.Rect(n, Rect);
            UIKit.Stretch(rt);
            return rt;
        }

        float baseAlpha;

        void Update()
        {
            float target = GameEvents.IsPlaying ? 1f : 0f;
            baseAlpha = Mathf.MoveTowards(baseAlpha, target, Time.unscaledDeltaTime / 0.2f);

            // 흐트러짐: 층마다 다른 방향으로 흔들리고 알파가 깜박인다
            float now = Time.unscaledTime;
            bool glitch = now < glitchUntil;
            float flicker = 1f;
            if (glitch)
            {
                float k = (glitchUntil - now) / GlitchSeconds;
                flicker = (Mathf.FloorToInt(now * 40f) % 2 == 0) ? 1f : Mathf.Lerp(1f, 0.35f, k);
                Jitter(PhoneLayer, 14f * k, 1); Jitter(StatusLayer, 10f * k, 2); Jitter(HintLayer, 8f * k, 3);
            }
            else if (PhoneLayer.anchoredPosition != Vector2.zero)
            {
                PhoneLayer.anchoredPosition = StatusLayer.anchoredPosition = HintLayer.anchoredPosition = Vector2.zero;
            }
            group.alpha = baseAlpha * flicker;
            bool on = group.alpha > 0.001f;
            if (Canvas.enabled != on) Canvas.enabled = on;
        }

        static void Jitter(RectTransform rt, float amp, int seed)
        {
            float t = Time.unscaledTime * 60f + seed * 17.3f;
            rt.anchoredPosition = new Vector2(Mathf.Sin(t * 1.7f), Mathf.Cos(t * 2.3f)) * amp;
        }
    }
}
