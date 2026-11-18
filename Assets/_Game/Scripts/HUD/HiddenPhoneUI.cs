using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 몰래 화면 (시안 4안): 오른쪽 아래 가슴 높이에 기울여 쥔 폰 뒷면과 손가락 실루엣.
    /// 화면은 못 보니 방송 반응은 몸으로 느끼는 신호로만 알려 준다:
    /// - 카메라 옆 빨간 REC 점 깜박임
    /// - 확보·채팅 폭주 때 2~3px 진동 + 양옆 진동 곡선
    /// - 폰 위 틈새로 새는 화면 빛: 평소 희미한 흰빛, 확보 = 초록, 후원 = 금색, 경고 채팅 = 빨강 깜박임
    /// </summary>
    public class HiddenPhoneUI : MonoBehaviour
    {
        const float W = 250f, Hgt = 400f;

        RectTransform root, body;
        Image recLed, glow;
        Image[] waves = new Image[4];
        float k;                          // 0 = 내려감, 1 = 보임
        float vibrateUntil = -99f, vibrateStart = -99f, vibrateAmp;
        Color glowColor = new Color(1, 1, 1, 0.07f);
        float glowUntil = -99f, glowStart;
        bool glowBlink;
        float lastBurstCheck;
        int linesInWindow;
        StreamFeed feedSrc;

        void Awake()
        {
            Build();
        }

        void OnEnable() { CaptureLog.Added += OnCaptured; }

        void OnDisable() { Unsubscribe(); }
        void OnDestroy() { Unsubscribe(); }

        void Unsubscribe()
        {
            CaptureLog.Added -= OnCaptured;
            if (feedSrc != null) feedSrc.LineAdded -= OnLine;
            feedSrc = null;
        }

        void Build()
        {
            root = UIKit.Rect("HiddenPhone", HUDRoot.Instance.HiddenLayer);
            HUDDraw.At(root, HUDDraw.BR, HUDDraw.BR, new Vector2(-140f, -60f), new Vector2(W, Hgt));
            root.localRotation = Quaternion.Euler(0, 0, 10f);

            // 틈새 빛 (폰 위 가장자리에서 위로 번짐)
            glow = UIKit.Image("ScreenLeak", root, glowColor, UIKit.VerticalFade(64));
            var g = glow.rectTransform;
            g.anchorMin = new Vector2(0, 1); g.anchorMax = new Vector2(1, 1); g.pivot = new Vector2(0.5f, 0f);
            g.anchoredPosition = new Vector2(0, -10f); g.sizeDelta = new Vector2(-16f, 64f);

            // 진동 곡선 (양옆 두 겹)
            for (int i = 0; i < 4; i++)
            {
                bool left = i < 2;
                int size = i % 2 == 0 ? 120 : 176;
                var arc = UIKit.Image("Wave" + i, root, new Color(1, 1, 1, 0f), UIKit.Circle(128, 5));
                arc.type = Image.Type.Filled;
                arc.fillMethod = Image.FillMethod.Radial360;
                arc.fillOrigin = (int)Image.Origin360.Top;
                arc.fillClockwise = true;
                arc.fillAmount = 0.16f;
                var rt = arc.rectTransform;
                HUDDraw.At(rt, new Vector2(left ? 0f : 1f, 0.62f), HUDDraw.MC, Vector2.zero, new Vector2(size, size));
                // 바깥쪽을 향하게: 오른쪽 호는 3시 방향, 왼쪽 호는 9시 방향이 가운데
                rt.localRotation = Quaternion.Euler(0, 0, left ? 90f + 0.16f * 180f : -90f + 0.16f * 180f);
                waves[i] = arc;
            }

            // 몸체
            var b = UIKit.Box("Back", root, UIKit.Hex("#171820"), 28);
            body = b.rectTransform;
            UIKit.Stretch(body);
            var border = UIKit.Box("Edge", body, UIKit.Hex("#2F3240"), 28, 2);
            UIKit.Stretch(border.rectTransform);

            // 카메라 섬 + 렌즈 두 개 + REC 점
            var bump = UIKit.Box("CameraBump", body, UIKit.Hex("#22242D"), 20);
            HUDDraw.At(bump.rectTransform, HUDDraw.TL, HUDDraw.TL, new Vector2(20f, -20f), new Vector2(84f, 116f));
            for (int i = 0; i < 2; i++)
            {
                var ring = UIKit.Image("LensRing" + i, bump.transform, UIKit.Hex("#30333F"), UIKit.Circle(64));
                HUDDraw.At(ring.rectTransform, HUDDraw.TC, HUDDraw.TC, new Vector2(0, -12f - i * 50f), new Vector2(42f, 42f));
                var lens = UIKit.Image("Lens", ring.transform, UIKit.Hex("#050608"), UIKit.Circle(64));
                HUDDraw.At(lens.rectTransform, HUDDraw.MC, HUDDraw.MC, Vector2.zero, new Vector2(30f, 30f));
                var shine = UIKit.Image("Shine", lens.transform, UIKit.Hex("#2A3950"), UIKit.Circle(64));
                HUDDraw.At(shine.rectTransform, HUDDraw.MC, HUDDraw.MC, new Vector2(-5f, 5f), new Vector2(8f, 8f));
            }
            recLed = UIKit.Image("RecLed", body, UIKit.Rec, UIKit.Circle(64));
            HUDDraw.At(recLed.rectTransform, HUDDraw.TL, HUDDraw.TL, new Vector2(116f, -30f), new Vector2(8f, 8f));

            // 손가락 실루엣: 오른쪽 가장자리를 감싼 세 손가락 + 왼쪽 엄지
            Color skin = UIKit.Hex("#241B17");
            for (int i = 0; i < 3; i++)
            {
                var f = UIKit.Box("Finger" + i, root, skin, 15);
                HUDDraw.At(f.rectTransform, HUDDraw.TR, HUDDraw.MC, new Vector2(4f, -150f - i * 52f), new Vector2(58f, 34f));
                f.rectTransform.localRotation = Quaternion.Euler(0, 0, -6f + i * 4f);
            }
            var thumb = UIKit.Box("Thumb", root, skin, 16);
            HUDDraw.At(thumb.rectTransform, HUDDraw.TL, HUDDraw.MC, new Vector2(-2f, -250f), new Vector2(64f, 36f));
            thumb.rectTransform.localRotation = Quaternion.Euler(0, 0, 18f);
            var palm = UIKit.Box("Palm", root, skin, 30);
            HUDDraw.At(palm.rectTransform, HUDDraw.BC, HUDDraw.MC, new Vector2(10f, 10f), new Vector2(W + 40f, 120f));

            root.gameObject.SetActive(false);
        }

        // ---------- 신호 ----------

        void OnCaptured(CaptureResult r)
        {
            var pc = PhoneController.Instance;
            if (pc == null || pc.State != PhoneState.Hidden) return;
            Glow(UIKit.Ok, 0.6f, false);
            Vibrate(3f, 0.45f);
        }

        void OnLine(ChatLine l)
        {
            var pc = PhoneController.Instance;
            if (pc == null || pc.State != PhoneState.Hidden) return;
            if (l.Kind == ChatKind.Donation) { Glow(HUDDraw.Gold, 0.9f, false); Vibrate(2f, 0.35f); }
            else if (l.Kind == ChatKind.Warn) Glow(UIKit.Rec, 1.0f, true);

            // 채팅 폭주: 1.2초 안에 3줄 이상
            if (Time.time - lastBurstCheck > 1.2f) { lastBurstCheck = Time.time; linesInWindow = 0; }
            linesInWindow++;
            if (linesInWindow >= 3) { Vibrate(2f, 0.3f); linesInWindow = 0; lastBurstCheck = Time.time; }
        }

        void Glow(Color c, float seconds, bool blink)
        {
            glowColor = c;
            glowStart = Time.time;
            glowUntil = Time.time + seconds;
            glowBlink = blink;
        }

        void Vibrate(float amp, float seconds)
        {
            vibrateAmp = Mathf.Max(amp, Time.time < vibrateUntil ? vibrateAmp : 0f);
            vibrateStart = Time.time;
            vibrateUntil = Time.time + seconds;
        }

        // ---------- 갱신 ----------

        void Update()
        {
            if (feedSrc == null && StreamFeed.Instance != null) { feedSrc = StreamFeed.Instance; feedSrc.LineAdded += OnLine; }

            var pc = PhoneController.Instance;
            bool want = pc != null && pc.State == PhoneState.Hidden && GameEvents.Phase != GamePhase.Ended;
            k = Mathf.MoveTowards(k, want ? 1f : 0f, Time.deltaTime / 0.2f);
            bool show = k > 0.001f;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;

            float now = Time.time;
            Vector2 basePos = new Vector2(-140f, Mathf.Lerp(-Hgt - 80f, -60f, HUDDraw.EaseOut(k)));
            Vector2 shake = Vector2.zero;
            float vib = now < vibrateUntil ? Mathf.Clamp01((vibrateUntil - now) / Mathf.Max(0.01f, vibrateUntil - vibrateStart)) : 0f;
            if (vib > 0f) shake = new Vector2(Mathf.Sin(now * 95f), Mathf.Sin(now * 71f + 1.3f) * 0.5f) * vibrateAmp * vib;
            root.anchoredPosition = basePos + shake;

            // 진동 곡선: 안쪽 → 바깥쪽 순서로 번졌다 사라짐
            for (int i = 0; i < 4; i++)
            {
                float delay = (i % 2) * 0.08f;
                float t = vib > 0f ? Mathf.Clamp01((now - vibrateStart - delay) / 0.35f) : 1f;
                float a = vib > 0f ? Mathf.Sin(t * Mathf.PI) * 0.55f : 0f;
                waves[i].color = new Color(1, 1, 1, a);
            }

            // REC 점
            bool rec = pc != null && pc.IsRecording;
            recLed.color = rec && (Time.unscaledTime % 1f) < 0.6f ? UIKit.Rec : UIKit.A(UIKit.Rec, 0.15f);

            // 틈새 빛
            Color baseGlow = new Color(1, 1, 1, rec ? 0.07f : 0.02f);
            if (now < glowUntil)
            {
                float p = (now - glowStart) / Mathf.Max(0.01f, glowUntil - glowStart);
                float a = glowBlink ? ((Mathf.FloorToInt((now - glowStart) * 8f) % 2 == 0) ? 0.75f : 0.2f) : Mathf.Lerp(0.8f, 0.15f, p);
                glow.color = UIKit.A(glowColor, a);
            }
            else glow.color = Color.Lerp(glow.color, baseGlow, HUDDraw.Damp(6f, Time.deltaTime));
        }
    }
}
