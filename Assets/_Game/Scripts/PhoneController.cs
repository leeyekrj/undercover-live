using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace UndercoverLive
{
    public enum PhoneState { Pocket, Hidden, Aimed }

    // 폰 3단계 + 맨눈/폰 두 카메라.
    // 우클릭: 폰 들기/내리기(주머니·몰래 ↔ 촬영), E: 몰래(가슴 높이) 켜고 끄기, 4: 바로 주머니(숨기기),
    // 촬영 중 줌: 휠 또는 -/+ 키(누르고 있으면 연속).
    // 촬영 화면은 HUD/PhoneAppUI가 화면 전체에 그린다. 여기서는 상태·배터리·카메라·피드(렌더 텍스처)만 맡는다.
    public class PhoneController : MonoBehaviour
    {
        public static PhoneController Instance;

        [Header("연결")]
        public Transform Head;                 // 눈 위치 (SeatedLook가 회전)
        public Camera EyeCamera;               // 맨눈 카메라: PhoneOnly 레이어 안 봄
        public Camera PhoneCamera;             // 폰 카메라: EyeOnly 레이어 안 봄
        [Tooltip("시작하면 화면 비율(가로형)에 맞춘 런타임 텍스처로 바뀐다. 판정 스냅샷이 이걸 읽는다.")]
        public RenderTexture Feed;
        public Font UIFont;

        [Header("옛 세로 폰 UI (빌더가 채움, 시작할 때 숨김)")]
        public RectTransform PhoneRoot;
        public RectTransform ScreenArea;
        public RectTransform ChatArea;
        public Text ViewerCountText;
        public Text RecText;
        public Text BatteryText;
        public Text ZoomText;
        public Image Dimmer;
        public RectTransform HiddenPhone;

        [Header("수치")]
        public float batterySeconds = 300f;    // 촬영 기준 5분이면 방전
        public float hiddenDrain = 0.4f;       // 몰래는 촬영의 40%
        public float pocketDrain = 0.05f;      // 녹음만
        public float aimedFov = 58f, hiddenFov = 74f, minFov = 12f;
        public float switchTime = 0.22f;
        [Tooltip("-/+ 키를 누르고 있을 때 초당 줌 변화 (0~1 범위 기준)")]
        public float zoomKeySpeed = 0.6f;
        [Tooltip("폰 피드 텍스처 긴 변 최대 픽셀 (웹 성능)")]
        public int feedMaxSide = 1600;

        public PhoneState State { get; private set; } = PhoneState.Pocket;
        public float Battery01 { get; private set; } = 1f;
        public bool IsRecording => State != PhoneState.Pocket && Battery01 > 0f;
        public bool IsAudioRecording => Battery01 > 0f;
        public bool IsScreenVisible => State == PhoneState.Aimed && Battery01 > 0f;
        public float AngularSpeed { get; private set; }
        public float Zoom01 { get; private set; }
        /// <summary>방송 시간(초). 주머니에서도 음성은 나가므로 배터리가 있는 동안 플레이 중이면 계속 센다.</summary>
        public float RecSeconds => recTime;
        /// <summary>폰 카메라 위치 전환 정도 (0 = 가슴 높이, 1 = 눈앞).</summary>
        public float AimBlend => blend * blend * (3f - 2f * blend);
        /// <summary>지금 줌 배율 (1 = 기본 화각).</summary>
        public float ZoomFactor
        {
            get
            {
                if (PhoneCamera == null) return 1f;
                float a = Mathf.Tan(aimedFov * 0.5f * Mathf.Deg2Rad), b = Mathf.Tan(PhoneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                return b > 1e-4f ? Mathf.Max(1f, a / b) : 1f;
            }
        }
        public event Action<PhoneState> StateChanged;

        float recTime, blend;
        Vector3 lastFwd;
        bool hasLast;
        RenderTexture runtimeFeed;
        int lastScreenW, lastScreenH;
        float resizeAt = -1f;

        // 폰 카메라가 머리 기준 어디 있는지 (촬영 = 눈앞, 몰래 = 가슴 높이에서 살짝 아래)
        static readonly Vector3 aimedOffset = new Vector3(0.0f, -0.02f, 0.28f);
        static readonly Vector3 hiddenOffset = new Vector3(0.12f, -0.42f, 0.22f);
        const float hiddenPitch = 14f;

        void Awake() { Instance = this; }

        void Start()
        {
            HideLegacyUI();
            EnsureFeed();
            ApplyState(true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (runtimeFeed != null)
            {
                if (PhoneCamera != null && PhoneCamera.targetTexture == runtimeFeed) PhoneCamera.targetTexture = null;
                runtimeFeed.Release();
                Destroy(runtimeFeed);
            }
        }

        void HideLegacyUI()
        {
            if (PhoneRoot != null) PhoneRoot.gameObject.SetActive(false);
            if (HiddenPhone != null) HiddenPhone.gameObject.SetActive(false);
            if (Dimmer != null) Dimmer.gameObject.SetActive(false);
        }

        void Update()
        {
            bool playing = GameEvents.IsPlaying;
            if (playing) HandleInput();

            // 배터리·방송 시간 (플레이 중에만)
            if (playing)
            {
                float drain = State == PhoneState.Aimed ? 1f : State == PhoneState.Hidden ? hiddenDrain : pocketDrain;
                Battery01 = Mathf.Max(0f, Battery01 - drain * Time.deltaTime / batterySeconds);
                if (IsAudioRecording) recTime += Time.deltaTime;
            }

            UpdatePhoneCamera();
            WatchScreenSize();
        }

        void HandleInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                SetState(State == PhoneState.Aimed ? PhoneState.Pocket : PhoneState.Aimed);
            if (kb != null)
            {
                if (kb.eKey.wasPressedThisFrame) SetState(State == PhoneState.Hidden ? PhoneState.Pocket : PhoneState.Hidden);
                if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) SetState(PhoneState.Pocket);
                // 1·2·3 바로 고르기 (Paul 11-18: 유지) — 1 주머니 · 2 몰래 · 3 촬영
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) SetState(PhoneState.Pocket);
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) SetState(PhoneState.Hidden);
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) SetState(PhoneState.Aimed);
            }
            if (State == PhoneState.Aimed)
            {
                if (mouse != null)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f) Zoom01 = Mathf.Clamp01(Zoom01 + Mathf.Sign(scroll) * 0.08f);
                }
                if (kb != null)
                {
                    float dz = 0f;
                    if (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed) dz -= 1f;
                    if (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed) dz += 1f;
                    if (dz != 0f) Zoom01 = Mathf.Clamp01(Zoom01 + dz * zoomKeySpeed * Time.deltaTime);
                }
            }
        }

        /// <summary>예전 순환(주머니 → 몰래 → 촬영). 다른 스크립트 호환용.</summary>
        public void Cycle()
        {
            SetState(State == PhoneState.Pocket ? PhoneState.Hidden : State == PhoneState.Hidden ? PhoneState.Aimed : PhoneState.Pocket);
        }

        public void SetState(PhoneState s)
        {
            if (s == State) return;
            State = s;
            if (s != PhoneState.Aimed) Zoom01 = 0f;
            ApplyState(false);
            StateChanged?.Invoke(s);
        }

        void ApplyState(bool instant)
        {
            // 화면에 보일 때(촬영)만 매 프레임 렌더한다. 판정은 WorldToViewportPoint만 쓰니 렌더가 필요 없고,
            // 몰래·주머니에서 스냅샷이 필요하면 RenderFeedNow()로 한 장만 그린다.
            if (PhoneCamera != null) PhoneCamera.enabled = State == PhoneState.Aimed;
            if (instant) blend = State == PhoneState.Aimed ? 1f : 0f;
        }

        /// <summary>폰 카메라가 꺼져 있을 때(몰래·주머니) 피드에 지금 장면을 한 장 그린다 (스냅샷용).</summary>
        public void RenderFeedNow()
        {
            if (PhoneCamera == null || Feed == null || PhoneCamera.enabled) return;
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = Feed };
            try
            {
                if (RenderPipeline.SupportsRenderRequest(PhoneCamera, req)) RenderPipeline.SubmitRenderRequest(PhoneCamera, req);
                else PhoneCamera.Render();
            }
            catch (Exception e) { Debug.LogWarning("[PhoneController] 스냅샷 렌더 실패: " + e.Message); }
        }

        // ---------- 피드 (화면 비율 렌더 텍스처) ----------

        void WatchScreenSize()
        {
            if (Screen.width != lastScreenW || Screen.height != lastScreenH)
            {
                lastScreenW = Screen.width; lastScreenH = Screen.height;
                resizeAt = Time.unscaledTime + 0.3f;   // 창 크기를 끄는 동안 매 프레임 새로 만들지 않게
            }
            if (resizeAt > 0f && Time.unscaledTime >= resizeAt)
            {
                resizeAt = -1f;
                EnsureFeed();
            }
        }

        void EnsureFeed()
        {
            int sw = Mathf.Max(64, Screen.width), sh = Mathf.Max(64, Screen.height);
            lastScreenW = Screen.width; lastScreenH = Screen.height;
            float aspect = sw / (float)sh;
            int w, h;
            if (aspect >= 1f) { w = Mathf.Min(feedMaxSide, sw); h = Mathf.RoundToInt(w / aspect); }
            else { h = Mathf.Min(feedMaxSide, sh); w = Mathf.RoundToInt(h * aspect); }
            w = Mathf.Max(64, w); h = Mathf.Max(64, h);
            if (runtimeFeed != null && runtimeFeed.width == w && runtimeFeed.height == h) return;

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.Default) { name = "PhoneFeedRuntime", antiAliasing = 1 };
            rt.Create();
            if (PhoneCamera != null)
            {
                PhoneCamera.targetTexture = rt;
                PhoneCamera.ResetAspect();
            }
            var old = runtimeFeed;
            runtimeFeed = rt;
            Feed = rt;
            if (old != null) { old.Release(); Destroy(old); }
        }

        // ---------- 폰 카메라 ----------

        void UpdatePhoneCamera()
        {
            if (PhoneCamera == null || Head == null) return;
            float target = State == PhoneState.Aimed ? 1f : 0f;
            blend = Mathf.MoveTowards(blend, target, Time.deltaTime / switchTime);
            float e = blend * blend * (3f - 2f * blend);

            var t = PhoneCamera.transform;
            t.position = Head.TransformPoint(Vector3.Lerp(hiddenOffset, aimedOffset, e));
            t.rotation = Head.rotation * Quaternion.Euler(Mathf.Lerp(hiddenPitch, 0f, e), 0f, 0f);
            float baseFov = Mathf.Lerp(hiddenFov, aimedFov, e);
            PhoneCamera.fieldOfView = Mathf.Lerp(baseFov, minFov, Zoom01 * e);

            // 회전 속도 (판정의 흔들림 계산용)
            var fwd = t.forward;
            if (hasLast && Time.deltaTime > 0f)
            {
                float deg = Vector3.Angle(lastFwd, fwd) / Mathf.Max(Time.deltaTime, 1e-4f);
                AngularSpeed = Mathf.Lerp(AngularSpeed, deg, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
            lastFwd = fwd; hasLast = true;
        }
    }
}
