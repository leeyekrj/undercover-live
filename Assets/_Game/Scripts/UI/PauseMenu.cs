using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 멈춤 메뉴(Paused 단계). 게임 장면은 반투명 막 뒤에 어둡게 남긴다(블러 없음).
    /// 계속하기 / 조작법 / 설정 / 처음부터(메인 화면부터). 키보드 ↑↓·Enter, Esc = 계속하기. 서체 크기 3종: 32 · 20 · 15.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        const float CardW = 600f, CardH = 512f;

        Canvas canvas;
        CanvasGroup group;
        RectTransform card;
        Text settingsHint;
        Image recDot;
        KeyNav nav;
        bool shown;

        void Awake()
        {
            canvas = UIKit.Canvas("PauseCanvas", 250, transform);
            group = UIParts.Group(canvas.gameObject, 0f);
            nav = KeyNav.On(canvas);
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

            var badge = UIParts.Pill(t, UIStrings.PauseBadge, 15, UIKit.Rec, UIKit.A(UIKit.Rec, 0.14f), UIKit.A(UIKit.Rec, 0.4f), true, 40, 36, 30, 12, -1, UIKit.Rec);
            recDot = badge.Find("Dot") != null ? badge.Find("Dot").GetComponent<Image>() : null;
            UIParts.Label(t, UIStrings.PauseTitle, 32, UIKit.Ink, true, TextAnchor.MiddleLeft, 40, 78, 520, 48);

            // 계속하기 (주 버튼)
            const float bw = CardW - 80f;
            var resume = UIParts.Button("Resume", t, 40, 152, bw, 60, UIKit.Rec, new Color(0, 0, 0, 0), 10, () => UIFlow.Instance?.Resume());
            UIParts.Label(resume.transform, UIStrings.PauseResume, 20, UIKit.Ink, true, TextAnchor.MiddleLeft, 24, 0, 300, 60);
            UIParts.Key(resume.transform, "Esc", 15, UIParts.Ink(0.92f), UIKit.A(Color.white, 0.16f), UIKit.A(Color.white, 0.28f), bw - 24 - 52, 16, 28, 52);
            nav.Add(resume, 10);

            nav.Add(Row(t, 228, UIStrings.MenuControls, UIStrings.PauseControlsHint, UIKit.Ink, UIParts.Ink(0.07f), UIParts.Ink(0.18f), () => UIFlow.Instance?.Controls.Open(), out _), 10);
            nav.Add(Row(t, 296, UIStrings.MenuSettings, "", UIKit.Ink, UIParts.Ink(0.07f), UIParts.Ink(0.18f), () => UIFlow.Instance?.Settings.Open(), out settingsHint), 10);
            UIParts.Bar("Div", t, UIParts.Ink(0.1f), 40, 368, bw, 1);
            nav.Add(Row(t, 384, UIStrings.PauseRestart, UIStrings.PauseRestartHint, UIKit.Rec, UIParts.Ink(0f), UIKit.A(UIKit.Rec, 0.5f), () => UIFlow.Restart(false), out _), 10);
            UIParts.Label(t, UIStrings.NavHint, 15, UIParts.Ink(0.5f), false, TextAnchor.MiddleLeft, 40, 456, bw, 24);
            RefreshHint();
        }

        Button Row(Transform parent, float y, string label, string hint, Color fg, Color fill, Color line, System.Action onClick, out Text hintText)
        {
            float bw = CardW - 80f;
            var b = UIParts.Button(label, parent, 40, y, bw, 56, fill, line, 10, onClick);
            UIParts.Label(b.transform, label, 20, fg, true, TextAnchor.MiddleLeft, 24, 0, 200, 56);
            hintText = UIParts.Label(b.transform, hint, 15, UIParts.Ink(0.62f), false, TextAnchor.MiddleRight, 150, 0, bw - 174, 56);
            return b;
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
            nav.Select(0);
            nav.Live = true;
            StopAllCoroutines();
            StartCoroutine(UIParts.FadeTo(group, 1f, 0.2f));
            StartCoroutine(UIParts.Anim(0.25f, k => card.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, k)));
        }

        public void Hide()
        {
            if (!shown) return;
            shown = false;
            nav.Live = false;
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
