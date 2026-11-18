using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 설정 창(메인·멈춤 공용). 마우스 감도 0.3~2.0배 · 소리 크기 0~100% · 밝기 -1~+1.
    /// 값은 Core/SettingsStore에 저장한다(저장소가 PlayerPrefs·Changed 이벤트를 맡음). 밝기 적용은 폰·HUD 담당.
    /// 슬라이더를 끄는 동안엔 0.15초 간격으로만 저장소에 넣는다(웹에서 저장이 매 프레임 돌지 않게).
    /// 키보드: ↑↓ 고르기 · ←→ 조절 · Enter 누르기 · Esc 닫기. 서체 크기 3종: 28 · 18 · 15.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        const float W = 720f, H = 532f;
        const float DefSens = 1f, DefVol = 0.9f, DefBright = 0f;

        Canvas canvas;
        CanvasGroup group;
        RectTransform modal;
        Slider sens, vol, bright;
        Text sensVal, volVal, brightVal;
        System.Action onClose;
        KeyNav nav;
        float pSens, pVol, pBright;
        bool dirty;
        float lastCommit;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            canvas = UIKit.Canvas("SettingsCanvas", 265, transform);
            group = UIParts.Group(canvas.gameObject, 0f);
            nav = KeyNav.On(canvas);
            Build();
            canvas.gameObject.SetActive(false);
        }

        void Build()
        {
            var root = canvas.transform;
            var dim = UIKit.Image("Dim", root, UIParts.Shade(0.6f));
            UIKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Close);

            var m = UIParts.Panel("Modal", root, UIParts.Card, 16, UIParts.Ink(0.18f));
            m.raycastTarget = true;
            modal = m.rectTransform;
            UIKit.Place(modal, (1920 - W) * 0.5f, (1080 - H) * 0.5f, W, H);
            var t = m.transform;

            UIParts.Label(t, UIStrings.SettingsTitle, 28, UIKit.Ink, true, TextAnchor.MiddleLeft, 40, 30, 400, 44);
            var close = UIParts.Button("Close", t, W - 40 - 148, 30, 148, 44, UIParts.Ink(0f), UIParts.Ink(0.22f), 10, Close);
            UIParts.Label(close.transform, UIStrings.Close, 15, UIParts.Ink(0.86f), true, TextAnchor.MiddleLeft, 18, 0, 60, 44);
            UIParts.Key(close.transform, "Esc", 15, UIParts.Ink(0.8f), UIParts.Ink(0.08f), UIParts.Ink(0.18f), 148 - 14 - 52, 8, 28, 52);

            sens = Row(t, 104, UIStrings.SettingsSensitivity, UIStrings.SettingsSensitivityHint, 0.3f, 2f, 0.1f, out sensVal, v => { pSens = v; Changed(); });
            vol = Row(t, 208, UIStrings.SettingsVolume, UIStrings.SettingsVolumeHint, 0f, 1f, 0.05f, out volVal, v => { pVol = v; Changed(); });
            bright = Row(t, 312, UIStrings.SettingsBrightness, UIStrings.SettingsBrightnessHint, -1f, 1f, 0.1f, out brightVal, v => { pBright = v; Changed(); });

            UIParts.Bar("Div", t, UIParts.Ink(0.1f), 40, 432, W - 80, 1);
            var reset = UIParts.Button("Reset", t, 40, 452, 160, 44, UIParts.Ink(0f), UIParts.Ink(0.22f), 10, ResetDefaults);
            UIParts.Label(reset.transform, UIStrings.SettingsReset, 15, UIParts.Ink(0.86f), true, TextAnchor.MiddleCenter, 0, 0, 160, 44);
            UIParts.Label(t, UIStrings.NavHintSlider, 15, UIParts.Ink(0.5f), false, TextAnchor.MiddleCenter, 210, 452, W - 420, 44);
            var done = UIParts.Button("Done", t, W - 40 - 160, 452, 160, 44, UIKit.Accent, new Color(0, 0, 0, 0), 10, Close);
            UIParts.Label(done.transform, UIStrings.Close, 15, UIParts.Dark, true, TextAnchor.MiddleCenter, 0, 0, 160, 44);
            nav.Add(reset, 10);
            nav.Add(done, 10);
        }

        Slider Row(Transform t, float y, string label, string hint, float min, float max, float step, out Text value, System.Action<float> onChange)
        {
            // 줄 전체(라벨·값·설명·슬라이더)가 키보드 선택 단위
            var row = UIKit.Rect("Row", t);
            UIKit.Place(row, 28, y - 8, W - 56, 96);
            UIParts.Label(row, label, 18, UIKit.Ink, true, TextAnchor.MiddleLeft, 12, 8, 400, 28);
            value = UIParts.Label(row, "", 15, UIKit.Accent, true, TextAnchor.MiddleRight, W - 56 - 12 - 160, 8, 160, 28);
            UIParts.Label(row, hint, 15, UIParts.Ink(0.6f), false, TextAnchor.MiddleLeft, 12, 36, 500, 24);
            var s = UIParts.Slider(row, 12, 66, W - 80, min, max, min, onChange);
            nav.AddSlider(s, row, step, 10);
            return s;
        }

        public static string FormatBrightness(float b)
        {
            int p = Mathf.RoundToInt(b * 100f);
            return p > 0 ? "+" + p : p.ToString();
        }

        void Labels()
        {
            sensVal.text = pSens.ToString("0.0") + "x";
            volVal.text = Mathf.RoundToInt(pVol * 100f) + "%";
            brightVal.text = FormatBrightness(pBright);
        }

        void Changed()
        {
            dirty = true;
            Labels();
            if (Time.unscaledTime - lastCommit > 0.15f) Commit();
        }

        void Commit()
        {
            if (!dirty) return;
            dirty = false;
            lastCommit = Time.unscaledTime;
            if (!Mathf.Approximately(SettingsStore.MouseSensitivity, pSens)) SettingsStore.MouseSensitivity = pSens;
            if (!Mathf.Approximately(SettingsStore.Volume, pVol)) SettingsStore.Volume = pVol;
            if (!Mathf.Approximately(SettingsStore.Brightness, pBright)) SettingsStore.Brightness = pBright;
        }

        void Sync()
        {
            pSens = SettingsStore.MouseSensitivity;
            pVol = SettingsStore.Volume;
            pBright = SettingsStore.Brightness;
            sens.SetValueWithoutNotify(pSens);
            vol.SetValueWithoutNotify(pVol);
            bright.SetValueWithoutNotify(pBright);
            Labels();
        }

        void ResetDefaults()
        {
            pSens = DefSens; pVol = DefVol; pBright = DefBright;
            sens.SetValueWithoutNotify(pSens);
            vol.SetValueWithoutNotify(pVol);
            bright.SetValueWithoutNotify(pBright);
            dirty = true;
            Labels();
            Commit();
        }

        void Update()
        {
            if (IsOpen && dirty && Time.unscaledTime - lastCommit > 0.15f) Commit();
        }

        public void Open(System.Action closed = null)
        {
            onClose = closed;
            if (IsOpen) return;
            IsOpen = true;
            Sync();
            canvas.gameObject.SetActive(true);
            nav.Select(0);
            nav.Live = true;
            StopAllCoroutines();
            StartCoroutine(UIParts.FadeTo(group, 1f, 0.2f));
            StartCoroutine(UIParts.Anim(0.25f, k => modal.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, k)));
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            nav.Live = false;
            Commit();
            StopAllCoroutines();
            if (gameObject.activeInHierarchy) StartCoroutine(CloseCo());
            else canvas.gameObject.SetActive(false);
            var cb = onClose; onClose = null;
            cb?.Invoke();
        }

        IEnumerator CloseCo()
        {
            yield return UIParts.FadeTo(group, 0f, 0.15f);
            canvas.gameObject.SetActive(false);
        }

        void OnDestroy() { Commit(); }
    }
}
