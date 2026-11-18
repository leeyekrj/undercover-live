using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UndercoverLive
{
    public enum PhoneState { Pocket, Hidden, Aimed }

    // 폰 3단계 + 맨눈/폰 두 카메라 + 폰 화면 UI.
    // 우클릭: 주머니 → 몰래 → 정조준 순환, Q: 바로 주머니, 휠: 정조준 중 줌.
    public class PhoneController : MonoBehaviour
    {
        public static PhoneController Instance;

        [Header("연결")]
        public Transform Head;                 // 눈 위치 (SeatedLook가 회전)
        public Camera EyeCamera;               // 맨눈 카메라: PhoneOnly 레이어 안 봄
        public Camera PhoneCamera;             // 폰 카메라: EyeOnly 레이어 안 봄
        public RenderTexture Feed;
        public Font UIFont;

        [Header("UI (빌더가 채움)")]
        public RectTransform PhoneRoot;        // 화면 위 폰 전체
        public RectTransform ScreenArea;       // 피드가 보이는 영역
        public RectTransform ChatArea;
        public Text ViewerCountText;
        public Text RecText;
        public Text BatteryText;
        public Text ZoomText;
        public Image Dimmer;                   // 정조준 때 주변을 누르는 어둠
        public RectTransform HiddenPhone;      // 몰래 모드: 손에 든 폰 뒷면

        [Header("수치")]
        public float batterySeconds = 300f;    // 정조준 기준 5분이면 방전
        public float hiddenDrain = 0.4f;       // 몰래는 정조준의 40%
        public float pocketDrain = 0.05f;      // 녹음만
        public float aimedFov = 58f, hiddenFov = 74f, minFov = 12f;
        public float switchTime = 0.22f;
        public float aimedScreenX = 330f;      // 폰 UI를 화면 오른쪽으로
        [Range(0, 1)] public float dimAmount = 0.45f;   // 정조준 때 맨눈 쪽을 누르는 정도

        public PhoneState State { get; private set; } = PhoneState.Pocket;
        public float Battery01 { get; private set; } = 1f;
        public bool IsRecording => State != PhoneState.Pocket && Battery01 > 0f;
        public bool IsAudioRecording => Battery01 > 0f;
        public bool IsScreenVisible => State == PhoneState.Aimed && Battery01 > 0f;
        public float AngularSpeed { get; private set; }
        public float Zoom01 { get; private set; }
        public event Action<PhoneState> StateChanged;

        float recTime, blend, lastYaw, lastPitch;
        Vector3 lastFwd;
        bool hasLast;

        // 폰 카메라가 머리 기준 어디 있는지 (정조준 = 눈앞, 몰래 = 가슴 높이에서 살짝 아래)
        static readonly Vector3 aimedOffset = new Vector3(0.0f, -0.02f, 0.28f);
        static readonly Vector3 hiddenOffset = new Vector3(0.12f, -0.42f, 0.22f);
        const float hiddenPitch = 14f;

        void Awake() { Instance = this; }

        void Start()
        {
            ApplyState(true);
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) Cycle();
            if (kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) SetState(PhoneState.Pocket);
                if (kb.digit1Key.wasPressedThisFrame) SetState(PhoneState.Pocket);
                if (kb.digit2Key.wasPressedThisFrame) SetState(PhoneState.Hidden);
                if (kb.digit3Key.wasPressedThisFrame) SetState(PhoneState.Aimed);
            }
            if (mouse != null && State == PhoneState.Aimed)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) Zoom01 = Mathf.Clamp01(Zoom01 + Mathf.Sign(scroll) * 0.08f);
            }

            // 배터리
            float drain = State == PhoneState.Aimed ? 1f : State == PhoneState.Hidden ? hiddenDrain : pocketDrain;
            Battery01 = Mathf.Max(0f, Battery01 - drain * Time.deltaTime / batterySeconds);
            if (IsRecording) recTime += Time.deltaTime;

            UpdatePhoneCamera();
            UpdateUI();
        }

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
            if (PhoneCamera != null) PhoneCamera.enabled = State != PhoneState.Pocket;
            if (instant) blend = State == PhoneState.Aimed ? 1f : 0f;
        }

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
            if (hasLast)
            {
                float deg = Vector3.Angle(lastFwd, fwd) / Mathf.Max(Time.deltaTime, 1e-4f);
                AngularSpeed = Mathf.Lerp(AngularSpeed, deg, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
            lastFwd = fwd; hasLast = true;
        }

        void UpdateUI()
        {
            float a = blend * blend * (3f - 2f * blend);
            if (PhoneRoot != null)
            {
                PhoneRoot.gameObject.SetActive(a > 0.01f);
                // 아래에서 올라오는 연출
                // 오른손에 든 위치: 맨눈 시야(왼쪽·가운데)와 폰 화면(오른쪽)이 한 화면에 겹쳐 보이게
                PhoneRoot.anchoredPosition = new Vector2(aimedScreenX, Mathf.Lerp(-900f, 0f, a));
            }
            if (Dimmer != null) Dimmer.color = new Color(0, 0, 0, dimAmount * a);
            if (HiddenPhone != null) HiddenPhone.gameObject.SetActive(State == PhoneState.Hidden);

            if (RecText != null)
            {
                bool on = IsRecording && (Time.unscaledTime % 1f) < 0.6f;
                int m = (int)(recTime / 60), s = (int)(recTime % 60);
                RecText.text = (on ? "<color=#ff3b30>●</color>" : "<color=#00000000>●</color>") + $" REC {m:00}:{s:00}";
            }
            if (BatteryText != null)
            {
                int pct = Mathf.CeilToInt(Battery01 * 100f);
                BatteryText.text = pct <= 15 ? $"<color=#ff3b30>{pct}%</color>" : $"{pct}%";
            }
            if (ZoomText != null)
            {
                float z = Mathf.Lerp(1f, aimedFov / minFov, Zoom01);
                ZoomText.text = Zoom01 > 0.01f ? $"{z:0.0}x" : "1x";
            }
        }
    }
}
