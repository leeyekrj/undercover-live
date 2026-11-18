using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 메뉴·창 키보드 조작: ↑↓(W/S) 고르기, Enter/Space 누르기, ←→(A/D) 슬라이더 조절(슬라이더가 아니면 옆 항목으로).
    /// Esc(뒤로)는 UIFlow·각 화면이 맡는다. 화면마다 하나씩 두고, 켜진 것 중 그리기 순서(sortingOrder)가 가장 높은 하나만 키를 받는다.
    /// 고른 항목은 금빛 테두리 + 살짝 밝게. 마우스를 올려도 같은 항목이 골라진다(마우스·키보드 함께).
    /// </summary>
    public class KeyNav : MonoBehaviour
    {
        class Item
        {
            public Action press;
            public Action<int> adjust;
            public Image ring, glow;
        }

        static readonly List<KeyNav> all = new List<KeyNav>();
        readonly List<Item> items = new List<Item>();
        int index;
        int liveFrame = -1;
        bool live;
        float repeatAt;
        int repeatDir;

        public int Priority { get; set; }
        /// <summary>고른 항목 테두리·밝기 색(Add 전에 정한다). 밝은 바탕 화면이면 어둡게.</summary>
        public Color RingColor = UIKit.Accent;
        public Color GlowColor = new Color(0.949f, 0.937f, 0.914f, 0.10f);
        public int Index => index;
        public int Count => items.Count;

        /// <summary>켜고 끄기. 켜진 첫 프레임의 키 입력은 무시한다(앞 화면에서 누른 Enter가 새 화면을 또 누르지 않게).</summary>
        public bool Live
        {
            get { return live; }
            set
            {
                if (value && !live) liveFrame = Time.frameCount;
                live = value;
                Refresh();
            }
        }

        public static KeyNav On(Canvas canvas)
        {
            var n = canvas.gameObject.AddComponent<KeyNav>();
            n.Priority = canvas.sortingOrder;
            return n;
        }

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable() { all.Remove(this); }
        void OnDestroy() { all.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); }

        /// <summary>버튼 항목. r = 버튼 모서리 반지름.</summary>
        public int Add(Button b, float r)
        {
            var it = new Item { press = () => { if (b != null && b.interactable) b.onClick.Invoke(); } };
            Decorate(it, (RectTransform)b.transform, r);
            items.Add(it);
            int i = items.Count - 1;
            b.gameObject.AddComponent<NavHover>().Init(this, i);
            Refresh();
            return i;
        }

        /// <summary>슬라이더 항목. row = 테두리를 두를 줄(라벨+슬라이더), step = ←→ 한 번에 바뀌는 양.</summary>
        public int AddSlider(Slider s, RectTransform row, float step, float r)
        {
            var it = new Item { adjust = d => { if (s != null) s.value = Mathf.Clamp(s.value + d * step, s.minValue, s.maxValue); } };
            Decorate(it, row, r);
            items.Add(it);
            int i = items.Count - 1;
            var hover = row.GetComponent<Image>();
            if (hover == null) { hover = row.gameObject.AddComponent<Image>(); hover.color = new Color(0, 0, 0, 0); }
            hover.raycastTarget = true;
            row.gameObject.AddComponent<NavHover>().Init(this, i);
            Refresh();
            return i;
        }

        void Decorate(Item it, RectTransform target, float r)
        {
            // 살짝 밝게: 버튼 바탕 바로 위, 글자 아래
            it.glow = UIParts.Fill("NavGlow", target, GlowColor, Mathf.Max(1f, r));
            UIKit.Stretch(it.glow.rectTransform);
            it.glow.transform.SetAsFirstSibling();
            // 금빛 테두리: 4px 바깥
            it.ring = UIParts.Line("NavRing", target, RingColor, r + 4f, 2f);
            it.ring.rectTransform.offsetMin = new Vector2(-4f, -4f);
            it.ring.rectTransform.offsetMax = new Vector2(4f, 4f);
        }

        public void Select(int i)
        {
            if (items.Count == 0) return;
            index = Mathf.Clamp(i, 0, items.Count - 1);
            Refresh();
        }

        void Refresh()
        {
            for (int k = 0; k < items.Count; k++)
            {
                bool on = live && k == index;
                if (items[k].ring != null) items[k].ring.enabled = on;
                if (items[k].glow != null) items[k].glow.enabled = on;
            }
        }

        static KeyNav Top()
        {
            KeyNav best = null;
            foreach (var n in all)
                if (n != null && n.live && n.isActiveAndEnabled && n.items.Count > 0 && (best == null || n.Priority > best.Priority)) best = n;
            return best;
        }

        void Update()
        {
            if (!live || items.Count == 0 || Time.frameCount == liveFrame) return;
            if (Top() != this) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            bool up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            bool down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            bool left = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
            bool right = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            bool ok = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;

            if (up) Move(-1);
            else if (down) Move(1);
            else if (left || right)
            {
                int d = right ? 1 : -1;
                var it = items[index];
                if (it.adjust != null) { it.adjust(d); repeatDir = d; repeatAt = Time.unscaledTime + 0.35f; }
                else Move(d);
            }
            else if (ok)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                items[index].press?.Invoke();
            }

            // 슬라이더는 꾹 누르면 계속 조절
            if (repeatDir != 0)
            {
                bool held = repeatDir > 0 ? (kb.rightArrowKey.isPressed || kb.dKey.isPressed) : (kb.leftArrowKey.isPressed || kb.aKey.isPressed);
                if (!held || items[index].adjust == null) repeatDir = 0;
                else if (Time.unscaledTime >= repeatAt) { items[index].adjust(repeatDir); repeatAt = Time.unscaledTime + 0.06f; }
            }
        }

        void Move(int d)
        {
            if (items.Count == 0) return;
            index = (index + d + items.Count) % items.Count;
            repeatDir = 0;
            Refresh();
        }
    }

    /// <summary>마우스를 올리면 그 항목을 고른다.</summary>
    public class NavHover : MonoBehaviour, IPointerEnterHandler
    {
        KeyNav nav; int index;
        public void Init(KeyNav n, int i) { nav = n; index = i; }
        public void OnPointerEnter(PointerEventData e) { if (nav != null && nav.Live) nav.Select(index); }
    }
}
