using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 멈춤 메뉴(Paused 단계). 게임 장면은 반투명 막 뒤에 어둡게 남긴다(블러 없음).
    /// 계속하기 / 조작법 / 설정 / 처음부터(메인 화면부터). 서체 크기 3종: 32 · 20 · 13.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        const float CardW = 560f, CardH = 468f;

        Canvas canvas;
        CanvasGroup group;
        RectTransform card;
        Text settingsHint;
        Image recDot;
        bool shown;

        void Awake()
        {
            canvas = UIKit.Canvas("PauseCanvas", 160, transform);
            group = UIParts.Group(canvas.gameObject, 0f);
            Build();
            canvas.gameObject.SetActive(false);
            SettingsStore.Changed += RefreshHint;
        }

        void OnDestroy() { SettingsStore.Changed -= RefreshHint; }

        void Build()
        {
            var root = canvas.transform;
            var dim = UIKit.Image("Dim", root, UIParts.Shade(0.74f));
            UIKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            var c = UIParts.Panel("Card", root, UIParts.Card, 16, UIParts.Ink(0.16f));
            card = c.rectTransform;
            UIKit.Place(card, (1920 - CardW) * 0.5f, (1080 - CardH) * 0.5f, CardW, CardH);
            var t = c.transform;

            var badge = UIParts.Pill(t, UIStrings.PauseBadge, 13, UIKit.Rec, UIKit.A(UIKit.Rec, 0.14f), UIKit.A(UIKit.Rec, 0.4f), true, 40, 36, 26, 10, -1, UIKit.Rec);
            recDot = badge.Find("Dot") != null ? badge.Find("Dot").GetComponent<Image>() : null;
            UIParts.Label(t, UIStrings.PauseTitle, 32, UIKit.Ink, true, TextAnchor.MiddleLeft, 40, 74, 480, 48);

            // 계속하기 (주 버튼)
            var resume = UIParts.Button("Resume", t, 40, 148, 480, 60, UIKit.Rec, new Color(0, 0, 0, 0), 10, () => UIFlow.Instance?.Resume());
            UIParts.Label(resume.transform, UIStrings.PauseResume, 20, UIKit.Ink, true, TextAnchor.MiddleLeft, 24, 0, 300, 60);
            UIParts.Key(resume.transform, "Esc", 13, UIParts.Ink(0.9f), UIKit.A(Color.white, 0.16f), UIKit.A(Color.white, 0.28f), 480 - 24 - 44, 18, 24, 44);

            Row(t, 224, UIStrings.MenuControls, UIStrings.PauseControlsHint, UIKit.Ink, UIParts.Ink(0.06f), UIParts.Ink(0.16f), () => UIFlow.Instance?.Controls.Open());
            settingsHint = Row(t, 292, UIStrings.MenuSettings, "", UIKit.Ink, UIParts.Ink(0.06f), UIParts.Ink(0.16f), () => UIFlow.Instance?.Settings.Open());
            Row(t, 372, UIStrings.PauseRestart, UIStrings.PauseRestartHint, UIKit.Rec, UIParts.Ink(0f), UIKit.A(UIKit.Rec, 0.45f), () => UIFlow.Restart(false));
            UIParts.Bar("Div", t, UIParts.Ink(0.1f), 40, 360, 480, 1);
            RefreshHint();
        }

        Text Row(Transform parent, float y, string label, string hint, Color fg, Color fill, Color line, System.Action onClick)
        {
            var b = UIParts.Button(label, parent, 40, y, 480, 56, fill, line, 10, onClick);
            UIParts.Label(b.transform, label, 20, fg, true, TextAnchor.MiddleLeft, 24, 0, 200, 56);
            return UIParts.Label(b.transform, hint, 13, UIParts.Ink(0.56f), false, TextAnchor.MiddleRight, 180, 0, 276, 56);
        }

        void RefreshHint()
        {
            if (settingsHint == null) return;
            settingsHint.text = "감도 " + SettingsStore.MouseSensitivity.ToString("0.0") + "x · 소리 "
                + Mathf.RoundToInt(SettingsStore.Volume * 100f) + "% · 밝기 " + SettingsPanel.FormatBrightness(SettingsStore.Brightness);
        }

        public void Show()
        {
            if (shown) return;
            shown = true;
            RefreshHint();
            canvas.gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(UIParts.FadeTo(group, 1f, 0.2f));
            StartCoroutine(UIParts.Anim(0.25f, k => card.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, k)));
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
            yield return UIParts.FadeTo(group, 0f, 0.15f);
            canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            if (shown && recDot != null) recDot.enabled = (Time.unscaledTime % 1.2f) < 0.7f;
        }
    }
}
