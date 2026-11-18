using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 경고 연출 (시안 '경고/'). 자체 캔버스(sortingOrder 116, 공통 표의 115~118)라 자막(100) 위에 뜬다.
    /// - 촬영 중 누가 쳐다보면(Watcher.Noticed) 또는 GameEvents.Warning(문장)이 오면 폰 화면 위 가운데에 앱 푸시 알림이
    ///   0.22초에 내려온다: 빨간 원 '!' + 한 줄 제목 + [Q] 숨기기. 3초 뒤 올라가고, 같은 문장은 4초 안에 다시 안 띄운다.
    ///   한 번 쳐다볼 때 배너 1개 + 채팅 1줄(ChatDirector.WarnLook)만.
    /// - 검문 중(C의 검문 배너가 같은 자리)에는 배너를 띄우지 않고 가장자리 맥박만.
    /// - 가장자리 붉은 맥박: 촬영·몰래 중 가장 높은 의심이 30 이상이면 심장 박자(Heartbeat.Pulse)로 붉게 뛴다.
    ///   촬영 땐 폰 테두리 안쪽(폰과 함께 움직임), 몰래 땐 화면 가장자리.
    /// </summary>
    public class WarningFX : MonoBehaviour
    {
        public const int SortingOrder = 116;
        const float BannerH = 78f, BannerTop = 34f, ShowSeconds = 3f, SlideSeconds = 0.22f;

        Canvas canvas;
        CanvasGroup rootCg;
        RectTransform follow;          // 폰 화면과 같이 움직이는 칸
        RectTransform banner;
        CanvasGroup bannerCg;
        Text title;
        RectTransform titleRt, chipRt;
        float bannerUntil = -99f, bannerK;
        readonly Dictionary<string, float> lastShown = new Dictionary<string, float>();
        bool checkpoint;

        Image edgeAimed, edgeAimedLine, edgeCorners, edgeFull;

        readonly List<Watcher> subscribed = new List<Watcher>();
        float nextScan;

        void Awake()
        {
            canvas = UIKit.Canvas("WarningCanvas", SortingOrder, transform);
            rootCg = canvas.gameObject.AddComponent<CanvasGroup>();
            rootCg.blocksRaycasts = false; rootCg.interactable = false;
            rootCg.alpha = 0f;
            BuildEdges();
            BuildBanner();
        }

        void OnEnable()
        {
            GameEvents.Warning += OnWarning;
            GameEvents.CheckpointStarted += OnCheckpointStarted;
            GameEvents.CheckpointEnded += OnCheckpointEnded;
            GameEvents.Confiscated += OnConfiscated;
        }

        void OnDisable() { Unsubscribe(); }
        void OnDestroy() { Unsubscribe(); }

        void Unsubscribe()
        {
            GameEvents.Warning -= OnWarning;
            GameEvents.CheckpointStarted -= OnCheckpointStarted;
            GameEvents.CheckpointEnded -= OnCheckpointEnded;
            GameEvents.Confiscated -= OnConfiscated;
            foreach (var w in subscribed) if (w != null) w.Noticed -= OnNoticed;
            subscribed.Clear();
        }

        // ---------- 구성 ----------

        void BuildEdges()
        {
            var vig = UIKit.EdgeVignette(128, 0.55f);
            // 몰래: 화면 가장자리
            edgeFull = UIKit.Image("EdgePulseFull", canvas.transform, UIKit.A(UIKit.Rec, 0f), vig);
            UIKit.Stretch(edgeFull.rectTransform);

            // 촬영: 폰 테두리 안쪽. 폰 화면이 오르내리고 흔들리는 만큼 따라 움직인다.
            follow = UIKit.Rect("FollowPhone", canvas.transform);
            UIKit.Stretch(follow);
            edgeAimed = UIKit.Image("EdgePulse", follow, UIKit.A(UIKit.Rec, 0f), vig);
            UIKit.Stretch(edgeAimed.rectTransform, PhoneAppUI.BezelWidth);
            // 둥근 화면 모서리 바깥으로 붉은빛이 새지 않게 베젤색으로 모서리를 덮는다
            edgeCorners = UIKit.Image("Corners", follow, HUDDraw.Bezel, HUDDraw.InverseRounded(32));
            edgeCorners.type = Image.Type.Sliced; edgeCorners.fillCenter = false;
            UIKit.Stretch(edgeCorners.rectTransform, PhoneAppUI.BezelWidth);
            edgeAimedLine = UIKit.Box("EdgeLine", follow, UIKit.A(UIKit.Hex("#FF5C66"), 0f), 32, 2);
            UIKit.Stretch(edgeAimedLine.rectTransform, PhoneAppUI.BezelWidth);
        }

        void BuildBanner()
        {
            var bg = UIKit.Box("PushBanner", follow, new Color(0.07f, 0.063f, 0.086f, 0.94f), 20);
            banner = bg.rectTransform;
            HUDDraw.At(banner, HUDDraw.TC, HUDDraw.TC, new Vector2(0, BannerH + 40f), new Vector2(720f, BannerH));
            bannerCg = banner.gameObject.AddComponent<CanvasGroup>();
            bannerCg.blocksRaycasts = false;
            bannerCg.alpha = 0f;
            var border = UIKit.Box("Border", banner, UIKit.A(UIKit.Rec, 0.75f), 20, 2);
            UIKit.Stretch(border.rectTransform);

            var icon = UIKit.Image("Icon", banner, UIKit.Rec, UIKit.Circle(64));
            HUDDraw.At(icon.rectTransform, HUDDraw.ML, HUDDraw.ML, new Vector2(20f, 0), new Vector2(44f, 44f));
            var bang = HUDDraw.Label("Bang", icon.transform, "!", HUDDraw.FL, Color.white, true, TextAnchor.MiddleCenter);
            UIKit.Stretch(bang.rectTransform);

            title = HUDDraw.Label("Title", banner, "", HUDDraw.FL, Color.white, true);
            titleRt = title.rectTransform;

            // 오른쪽 행동 칩: [Q] 숨기기 (공통 규칙: 숨기기는 어느 상태에서든 Q로 표시)
            var chip = UIKit.Box("Action", banner, UIKit.A(UIKit.Rec, 0.24f), 12);
            chipRt = chip.rectTransform;
            var chipBorder = UIKit.Box("Border", chipRt, UIKit.A(UIKit.Hex("#FF7880"), 0.65f), 12, 1);
            UIKit.Stretch(chipBorder.rectTransform);
            float kx = 12f;
            float kw = HUDDraw.KeyCap(chipRt, HUDText.KeyHide, Color.white, HUDDraw.Dark, kx, 24f, out _);
            var act = HUDDraw.Label("Label", chipRt, HUDText.BannerAction, HUDDraw.FS, UIKit.Hex("#FFD6D9"), true);
            float aw = HUDDraw.Width(act) + 2f;
            HUDDraw.At(act.rectTransform, HUDDraw.ML, HUDDraw.ML, new Vector2(kx + kw + 8f, 0), new Vector2(aw, 24f));
            var chipSize = new Vector2(kx + kw + 8f + aw + 14f, 40f);
            HUDDraw.At(chipRt, HUDDraw.MR, HUDDraw.MR, new Vector2(-18f, 0), chipSize);
            banner.gameObject.SetActive(false);
        }

        // ---------- 신호 ----------

        void ScanWatchers()
        {
            foreach (var w in FindObjectsByType<Watcher>())
            {
                if (w == null || subscribed.Contains(w)) continue;
                w.Noticed += OnNoticed;
                subscribed.Add(w);
            }
        }

        void OnNoticed(Watcher w)
        {
            if (!GameEvents.IsPlaying || w == null) return;
            var pc = PhoneController.Instance;
            if (pc == null || !pc.IsRecording) return;
            Transform head = pc.Head != null ? pc.Head : pc.PhoneCamera != null ? pc.PhoneCamera.transform : null;
            if (head == null) return;
            string where = HUDText.Where(head.InverseTransformPoint(w.EyePosition));
            if (pc.IsScreenVisible) ShowBanner(HUDText.BannerLine(where, w.kind));
            if (ChatDirector.Instance != null) ChatDirector.Instance.WarnLook(where, HUDText.WhoChat(w.kind), w.kind);
        }

        void OnWarning(string text)
        {
            if (!GameEvents.IsPlaying || string.IsNullOrEmpty(text)) return;
            var pc = PhoneController.Instance;
            if (pc != null && pc.IsScreenVisible) ShowBanner(text);
        }

        void OnCheckpointStarted(float seconds) { checkpoint = true; bannerUntil = -99f; }
        void OnCheckpointEnded(bool passed) { checkpoint = false; }
        void OnConfiscated(string reason) { checkpoint = false; bannerUntil = -99f; }

        /// <summary>폰 위 알림 배너 (촬영 화면이 보이고 검문 중이 아닐 때만).</summary>
        public void ShowBanner(string text)
        {
            if (checkpoint) return;
            float now = Time.time;
            if (lastShown.TryGetValue(text, out var t) && now - t < 4f) return;
            lastShown[text] = now;
            title.text = text;
            float tw = Mathf.Min(HUDDraw.Width(title) + 2f, 760f);
            float left = 20f + 44f + 16f;
            HUDDraw.At(titleRt, HUDDraw.ML, HUDDraw.ML, new Vector2(left, 0), new Vector2(tw, 30f));
            float w = left + tw + 24f + chipRt.sizeDelta.x + 18f;
            banner.sizeDelta = new Vector2(Mathf.Max(560f, w), BannerH);
            bannerUntil = now + ShowSeconds;
            banner.gameObject.SetActive(true);
        }

        // ---------- 갱신 ----------

        void Update()
        {
            if (Time.time >= nextScan) { nextScan = Time.time + 2f; ScanWatchers(); }

            // HUD와 같이 플레이 중에만 보인다
            float vis = GameEvents.IsPlaying ? 1f : 0f;
            rootCg.alpha = Mathf.MoveTowards(rootCg.alpha, vis, Time.unscaledDeltaTime / 0.2f);
            bool on = rootCg.alpha > 0.001f;
            if (canvas.enabled != on) canvas.enabled = on;

            var app = PhoneAppUI.Instance;
            float blend = app != null ? app.Blend : 0f;
            if (app != null)
            {
                follow.anchoredPosition = app.ScreenOffset;
                follow.localRotation = app.ScreenRotation;
            }
            bool showPhone = blend > 0.001f;
            edgeCorners.enabled = showPhone;

            var pc = PhoneController.Instance;
            bool screen = pc != null && pc.IsScreenVisible && GameEvents.IsPlaying;

            // 배너: 내려오고(0.22초) · 3초 · 올라감
            float want = screen && !checkpoint && Time.time < bannerUntil ? 1f : 0f;
            bannerK = Mathf.MoveTowards(bannerK, want, Time.unscaledDeltaTime / SlideSeconds);
            if (banner.gameObject.activeSelf)
            {
                float e = want > 0.5f ? HUDDraw.EaseOut(bannerK) : HUDDraw.Smooth(bannerK);
                banner.anchoredPosition = new Vector2(0, Mathf.Lerp(BannerH + 40f, -BannerTop, e));
                bannerCg.alpha = Mathf.Clamp01(bannerK * 1.6f);
                if (bannerK <= 0f && want <= 0f) banner.gameObject.SetActive(false);
            }

            // 가장자리 맥박
            float s = Heartbeat.MaxSuspicion;
            bool exposed = pc != null && pc.State != PhoneState.Pocket && GameEvents.IsPlaying;
            float strength = exposed && s >= 30f ? Mathf.Lerp(0.35f, 0.85f, Mathf.InverseLerp(30f, 90f, s)) : 0f;
            float a = strength * (0.3f + 0.7f * Heartbeat.Pulse);
            edgeAimed.color = UIKit.A(UIKit.Rec, a * blend);
            edgeAimedLine.color = UIKit.A(UIKit.Hex("#FF5C66"), a * 0.9f * blend);
            edgeFull.color = UIKit.A(UIKit.Rec, a * (1f - blend));
        }
    }
}
