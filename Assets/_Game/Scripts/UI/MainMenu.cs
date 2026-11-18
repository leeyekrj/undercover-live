using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 메인 화면(Menu 단계). Argon 1안: 왼쪽 열에 교단 표식·ON AIR 칩·제목·소개·버튼 3개,
    /// 오른쪽 위 방송 대기 상태, 아래 가는 선과 인용·키 안내. 배경은 MenuCamera가 찍는 실제 3D 장면.
    /// 서체 크기 4종: 84(제목) · 20(버튼·소개) · 16(교단 이름·칩 옆 문구) · 13(나머지).
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        Canvas canvas;
        RectTransform root;
        CanvasGroup group, leftCol;
        Image recDot, ringL, ringR, startDot;
        RectTransform ringRRect, targetBox;
        CanvasGroup targetGroup;
        bool shown;
        float shownAt;

        void Awake()
        {
            canvas = UIKit.Canvas("MainMenuCanvas", 170, transform);
            root = (RectTransform)canvas.transform;
            group = UIParts.Group(canvas.gameObject, 0f);
            Build();
            canvas.gameObject.SetActive(false);
        }

        void Build()
        {
            var t = root;

            // 왼쪽·아래 비네트 (무대 쪽은 열어 둠)
            var vl = UIKit.Image("VignetteLeft", t, UIParts.Shade(0.92f), UIParts.HGradient(new[] { 0f, 0.58f, 1f }, new[] { 1f, 0.68f / 0.92f, 0f }));
            UIKit.Place(vl.rectTransform, 0, 0, 860, 1080);
            var vb = UIKit.Image("VignetteBottom", t, UIParts.Shade(0.86f), UIKit.VerticalFade(128));
            UIKit.Place(vb.rectTransform, 0, 760, 1920, 320);

            // 뷰파인더 모서리
            var cc = UIParts.Ink(0.22f);
            UIParts.Corner(t, 96, 72, 28, 2, cc, false, false);
            UIParts.Corner(t, 1796, 72, 28, 2, cc, true, false);
            UIParts.Corner(t, 96, 980, 28, 2, cc, false, true);
            UIParts.Corner(t, 1796, 980, 28, 2, cc, true, true);

            // 교단 표식: 겹친 두 원 + 이름·구호
            var mark = UIKit.Rect("CultMark", t);
            UIKit.Place(mark, 128, 106, 52, 36);
            var m1 = UIParts.Ring("RingL", mark, UIKit.Accent, 36, 1.5f); UIKit.Place(m1.rectTransform, 0, 0, 36, 36);
            var m2 = UIParts.Ring("RingR", mark, UIKit.Accent, 36, 1.5f); UIKit.Place(m2.rectTransform, 16, 0, 36, 36);
            var pupil = UIParts.Dot("Pupil", mark, UIKit.Accent, 6); UIKit.Place(pupil.rectTransform, 23, 15, 6, 6);
            UIParts.Label(t, UIStrings.CultName + " · " + UIStrings.CultCountdown, 16, UIKit.Accent, true, TextAnchor.MiddleLeft, 196, 100, 520, 24);
            UIParts.Label(t, UIStrings.CultSlogan, 13, UIParts.Ink(0.56f), false, TextAnchor.MiddleLeft, 196, 124, 520, 20);

            // 오른쪽 위: 방송 대기 상태
            var hud = UIParts.Panel("StreamHud", t, UIParts.Shade(0.76f), 8, UIParts.Ink(0.16f));
            UIKit.Place(hud.rectTransform, 1472, 96, 320, 44);
            recDot = UIParts.Dot("RecDot", hud.transform, UIKit.Rec, 10);
            UIKit.Place(recDot.rectTransform, 16, 17, 10, 10);
            UIParts.Label(hud.transform, UIStrings.MenuRecIdle, 13, UIKit.Ink, true, TextAnchor.MiddleLeft, 34, 0, 100, 44);
            var live = UIParts.Pill(hud.transform, UIStrings.MenuLiveIdle, 13, UIKit.Rec, UIKit.A(UIKit.Rec, 0.18f), UIKit.Rec, true, 0, 11, 22, 8, 4);
            live.anchoredPosition = new Vector2(160 - live.sizeDelta.x * 0.5f, -11);
            var bat = UIParts.Label(hud.transform, UIStrings.MenuBattery, 13, UIParts.Ink(0.68f), false, TextAnchor.MiddleRight, 200, 0, 104, 44);
            bat.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 왼쪽 열 (등장 애니메이션 묶음)
            var col = UIKit.Rect("LeftColumn", t);
            UIKit.Stretch(col);
            leftCol = UIParts.Group(col.gameObject);

            // 제목 뒤 겹눈 아우라
            ringL = UIParts.Ring("AuraL", col, UIKit.A(UIKit.Accent, 0.28f), 220, 1.5f);
            UIKit.Place(ringL.rectTransform, 112, 290, 220, 220);
            ringR = UIParts.Ring("AuraR", col, UIKit.A(UIKit.Accent, 0.28f), 220, 1.5f);
            ringRRect = ringR.rectTransform;
            UIKit.Place(ringRRect, 212, 290, 220, 220);

            // ON AIR 칩 + 문구
            var chip = UIParts.Pill(col, UIStrings.MenuTag, 13, UIKit.Ink, UIKit.Rec, new Color(0, 0, 0, 0), true, 128, 348, 28, 10, 4);
            UIParts.Label(col, UIStrings.MenuTagSub, 16, UIKit.Accent, false, TextAnchor.MiddleLeft, 128 + chip.sizeDelta.x + 12, 348, 360, 28);

            // 제목
            UIParts.Label(col, UIStrings.GameTitle, 84, UIKit.Ink, true, TextAnchor.MiddleLeft, 124, 384, 680, 104);

            // 두 줄 소개
            var tag = UIParts.Label(col, UIStrings.MenuTagline, 20, UIParts.Ink(0.74f), false, TextAnchor.UpperLeft, 128, 500, 620, 64);
            tag.lineSpacing = 1.2f;

            // 버튼 3개
            var start = UIParts.Button("Start", col, 128, 592, 380, 68, UIKit.Rec, UIKit.A(Color.white, 0.22f), 10, () => UIFlow.Instance?.StartGame());
            startDot = UIParts.Dot("Dot", start.transform, UIKit.Ink, 12);
            UIKit.Place(startDot.rectTransform, 24, 28, 12, 12);
            UIParts.Label(start.transform, UIStrings.MenuStart, 20, UIKit.Ink, true, TextAnchor.MiddleLeft, 48, 0, 200, 68);
            UIParts.Label(start.transform, UIStrings.MenuStartHint, 13, UIParts.Ink(0.86f), false, TextAnchor.MiddleRight, 180, 0, 176, 68);

            SecondaryButton(col, 674, UIStrings.MenuControls, UIStrings.MenuControlsHint, () => UIFlow.Instance?.Controls.Open());
            SecondaryButton(col, 746, UIStrings.MenuSettings, UIStrings.MenuSettingsHint, () => UIFlow.Instance?.Settings.Open());

            // 무대 위 TARGET 상자 (교주 위치를 따라감)
            targetBox = UIKit.Rect("Target", t);
            targetBox.anchorMin = targetBox.anchorMax = new Vector2(0.5f, 0.5f);
            targetBox.pivot = new Vector2(0.5f, 0.5f);
            targetBox.sizeDelta = new Vector2(200, 200);
            targetGroup = UIParts.Group(targetBox.gameObject, 0f);
            var frame = UIParts.Line("Frame", targetBox, UIKit.A(UIKit.Accent, 0.55f), 1, 1.5f);
            frame.rectTransform.sizeDelta = Vector2.zero;
            var tl = UIParts.Label(targetBox, UIStrings.MenuTarget, 13, UIKit.Accent, true, TextAnchor.LowerLeft, 0, -28, 260, 22);
            tl.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 아래: 가는 선 + 인용 + 키 안내
            UIParts.Bar("BottomLine", t, UIParts.Ink(0.14f), 128, 932, 1664, 1);
            UIParts.Label(t, UIStrings.PhoneBanQuote, 13, UIKit.A(UIKit.Accent, 0.88f), false, TextAnchor.MiddleLeft, 128, 944, 900, 24);
            var keys = UIParts.Label(t, KeyHints(), 13, UIParts.Ink(0.56f), false, TextAnchor.MiddleRight, 892, 944, 900, 24);
            keys.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        static string KeyHints()
        {
            var a = UIStrings.MenuKeyHints;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i + 1 < a.Length; i += 2)
            {
                if (i > 0) sb.Append("  ·  ");
                sb.Append("<color=").Append(UIParts.Tag(UIKit.Ink)).Append("><b>").Append(a[i]).Append("</b></color> ").Append(a[i + 1]);
            }
            return sb.ToString();
        }

        void SecondaryButton(Transform parent, float y, string label, string hint, System.Action onClick)
        {
            var b = UIParts.Button(label, parent, 128, y, 380, 58, UIParts.Ink(0.06f), UIParts.Ink(0.18f), 10, onClick);
            UIParts.Label(b.transform, label, 20, UIKit.Ink, true, TextAnchor.MiddleLeft, 24, 0, 160, 58);
            UIParts.Label(b.transform, hint, 13, UIParts.Ink(0.52f), false, TextAnchor.MiddleRight, 140, 0, 216, 58);
        }

        // ---------- 보이기·숨기기 ----------

        public void Show()
        {
            if (shown) return;
            shown = true;
            shownAt = Time.unscaledTime;
            canvas.gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(UIParts.FadeTo(group, 1f, 0.5f));
            var colRt = (RectTransform)leftCol.transform;
            StartCoroutine(UIParts.Anim(0.4f, k =>
            {
                leftCol.alpha = k;
                colRt.anchoredPosition = new Vector2(0, -16f * (1f - k));
            }, 0.1f));
        }

        public void Hide()
        {
            if (!shown) return;
            shown = false;
            StopAllCoroutines();
            if (!gameObject.activeInHierarchy) { canvas.gameObject.SetActive(false); return; }
            StartCoroutine(HideCo());
        }

        IEnumerator HideCo()
        {
            yield return UIParts.FadeTo(group, 0f, 0.25f);
            canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!shown) return;
            float t = Time.unscaledTime;

            // REC 점 깜박임
            if (recDot != null) recDot.enabled = (t % 1.2f) < 0.7f;
            // 겹눈 아우라: 오른쪽 원이 천천히 다가왔다 멀어지고, 밝기가 숨 쉬듯
            if (ringRRect != null)
            {
                float s = Mathf.Sin(t * Mathf.PI * 2f / 7f);
                ringRRect.anchoredPosition = new Vector2(212 + s * 6f, -290);
                float a = 0.28f + 0.07f * Mathf.Sin(t * Mathf.PI * 2f / 5f);
                ringL.color = UIKit.A(UIKit.Accent, a);
                ringR.color = UIKit.A(UIKit.Accent, a);
            }
            UpdateTarget();

            // Enter = 잠입 시작 (설정·조작법 창이 닫혀 있을 때)
            var kb = Keyboard.current;
            var f = UIFlow.Instance;
            if (kb != null && f != null && !f.Settings.IsOpen && !f.Controls.IsOpen
                && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                f.StartGame();
        }

        void UpdateTarget()
        {
            var f = UIFlow.Instance;
            var mc = f != null ? f.MenuCam : null;
            bool ok = false;
            if (mc != null && mc.Active && mc.Cam != null && mc.TryGetFocus(out var focus))
            {
                var sp = mc.Cam.WorldToScreenPoint(focus - Vector3.up * 0.35f);
                if (sp.z > 0.1f && RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var local))
                {
                    // 왼쪽 열과 겹치면 숨김
                    float x = local.x + root.rect.width * 0.5f;
                    ok = x > 800f && x < root.rect.width - 120f;
                    targetBox.anchoredPosition = local;
                }
            }
            float want = ok && Time.unscaledTime - shownAt > 1.0f ? 1f : 0f;
            targetGroup.alpha = Mathf.MoveTowards(targetGroup.alpha, want, Time.unscaledDeltaTime * 2.5f);
        }
    }
}
