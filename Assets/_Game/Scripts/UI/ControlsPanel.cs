using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 조작법 창. 메인과 멈춤에서 같이 쓴다. 왼쪽 = 폰 쓰는 법(내림·몰래 찍기·들기를 눈높이 그림으로),
    /// 오른쪽 = 키 안내(새 조작 기준, 개발용 키 없음). 서체 크기 4종: 28 · 18 · 15 · 13.
    /// </summary>
    public class ControlsPanel : MonoBehaviour
    {
        const float W = 1040f, H = 680f;

        Canvas canvas;
        CanvasGroup group;
        RectTransform modal;
        System.Action onClose;
        public bool IsOpen { get; private set; }

        void Awake()
        {
            canvas = UIKit.Canvas("ControlsCanvas", 180, transform);
            group = UIParts.Group(canvas.gameObject, 0f);
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
            m.raycastTarget = true;   // 창 안을 눌러도 닫히지 않게
            modal = m.rectTransform;
            UIKit.Place(modal, (1920 - W) * 0.5f, (1080 - H) * 0.5f, W, H);
            var t = m.transform;

            UIParts.Label(t, UIStrings.ControlsTitle, 28, UIKit.Ink, true, TextAnchor.MiddleLeft, 40, 30, 600, 44);
            CloseButton(t, W);

            // 왼쪽: 폰 쓰는 법
            var left = UIParts.Panel("PhoneModes", t, UIParts.Shade(0.55f), 12, UIKit.A(UIKit.Accent, 0.28f));
            UIKit.Place(left.rectTransform, 40, 100, 460, 540);
            UIParts.Label(left.transform, UIStrings.ControlsPhoneTitle, 18, UIKit.Accent, true, TextAnchor.MiddleLeft, 24, 18, 300, 32);
            var modes = UIStrings.PhoneModes;
            for (int i = 0; i * 3 + 2 < modes.Length && i < 3; i++)
                PhoneModeRow(left.transform, 64 + i * 156, i, modes[i * 3], modes[i * 3 + 1], modes[i * 3 + 2]);

            // 오른쪽: 키 안내
            var right = UIParts.Panel("Keys", t, UIParts.Shade(0.55f), 12, UIParts.Ink(0.12f));
            UIKit.Place(right.rectTransform, 540, 100, 460, 540);
            UIParts.Label(right.transform, UIStrings.ControlsKeysTitle, 18, UIKit.Accent, true, TextAnchor.MiddleLeft, 24, 18, 300, 32);
            var keys = UIStrings.ControlKeys;
            int rows = keys.Length / 3;
            float rowH = Mathf.Min(76f, (540f - 72f) / Mathf.Max(1, rows));
            for (int i = 0; i < rows; i++)
            {
                float y = 64 + i * rowH;
                UIParts.Key(right.transform, keys[i * 3], 15, UIKit.Accent, UIKit.A(UIKit.Accent, 0.16f), new Color(0, 0, 0, 0), 24, y + 4, 30, 60);
                UIParts.Label(right.transform, keys[i * 3 + 1], 18, UIKit.Ink, true, TextAnchor.MiddleLeft, 100, y, 340, 28);
                UIParts.Label(right.transform, keys[i * 3 + 2], 15, UIParts.Ink(0.68f), false, TextAnchor.MiddleLeft, 100, y + 28, 340, 24);
            }
        }

        // 눈높이 선 + 폰 위치로 세 상태를 그린다 (0 = 내림, 1 = 몰래, 2 = 들기)
        void PhoneModeRow(Transform parent, float y, int index, string name, string key, string desc)
        {
            var icon = UIParts.Panel("Icon" + index, parent, UIParts.Ink(0.03f), 10, UIParts.Ink(0.1f));
            UIKit.Place(icon.rectTransform, 24, y, 72, 132);
            var it = icon.transform;
            // 눈 (위쪽)
            var eye = UIParts.Ring("Eye", it, UIParts.Ink(0.5f), 14, 1.5f);
            UIKit.Place(eye.rectTransform, 29, 16, 14, 14);
            UIParts.Bar("EyeLine", it, UIParts.Ink(0.16f), 8, 22, 56, 1);
            float py = index == 2 ? 12f : index == 1 ? 52f : 84f;
            bool raised = index == 2;
            var phone = UIParts.Panel("Phone", it, raised ? UIKit.A(UIKit.Accent, 0.85f) : UIParts.Ink(0.08f), 5, raised ? new Color(0, 0, 0, 0) : UIKit.A(UIKit.Accent, 0.7f), 1.5f);
            UIKit.Place(phone.rectTransform, 24, py, 24, 40);
            if (raised) phone.rectTransform.SetAsLastSibling();

            var nm = UIParts.Label(parent, name, 18, UIKit.Ink, true, TextAnchor.MiddleLeft, 116, y + 4, 200, 28);
            float nw = Mathf.Ceil(UIParts.Width(nm));
            UIParts.Key(parent, key, 13, UIKit.Accent, UIKit.A(UIKit.Accent, 0.16f), new Color(0, 0, 0, 0), 116 + nw + 10, y + 6, 24);
            var d = UIParts.Label(parent, desc, 15, UIParts.Ink(0.74f), false, TextAnchor.UpperLeft, 116, y + 40, 320, 92);
            d.lineSpacing = 1.2f;
        }

        void CloseButton(Transform t, float w)
        {
            var b = UIParts.Button("Close", t, w - 40 - 132, 32, 132, 40, UIParts.Ink(0f), UIParts.Ink(0.2f), 10, Close);
            UIParts.Label(b.transform, UIStrings.Close, 13, UIParts.Ink(0.8f), true, TextAnchor.MiddleLeft, 18, 0, 60, 40);
            UIParts.Key(b.transform, "Esc", 13, UIParts.Ink(0.7f), UIParts.Ink(0.08f), UIParts.Ink(0.18f), 132 - 14 - 44, 9, 22, 44);
        }

        public void Open(System.Action closed = null)
        {
            onClose = closed;
            if (IsOpen) return;
            IsOpen = true;
            canvas.gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(UIParts.FadeTo(group, 1f, 0.2f));
            StartCoroutine(UIParts.Anim(0.25f, k => modal.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, k)));
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
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
    }
}
