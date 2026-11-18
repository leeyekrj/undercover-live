using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 촬영 화면 왼쪽 아래 채팅 카드 더미. 새 줄은 맨 아래에서 올라오고 위의 줄은 밀려 올라가며 흐려진다.
    /// 일반 = 어두운 반투명 카드, 후원 = 금빛 카드 + 금액, 경고 = 빨간 카드, 공지 = 금색 테두리.
    /// </summary>
    public class ChatView
    {
        const int MaxCards = 5;
        const float Width = 470f, PadX = 14f, PadY = 9f, Gap = 8f;

        static readonly string[] NickPalette = { "FFB86B", "7FD6FF", "A6F29B", "FF8FB1", "D2A8FF", "FFE27A", "8FB4FF", "7CE8CF" };

        class Card
        {
            public RectTransform rt;
            public CanvasGroup cg;
            public Image bg, border, pill;
            public Text text, pillText;
            public float h, y, targetY, alpha, targetAlpha;
            public bool dying;
        }

        readonly RectTransform root;
        readonly List<Card> cards = new List<Card>();   // 오래된 것 → 최신
        readonly Stack<Card> pool = new Stack<Card>();

        public ChatView(RectTransform parent)
        {
            root = UIKit.Rect("Chat", parent);
            // 왼쪽 아래 기준 (아래 키 안내 칩 위 24px)
            HUDDraw.At(root, HUDDraw.BL, HUDDraw.BL, new Vector2(68f, 110f), new Vector2(Width, 300f));
        }

        public void Add(ChatLine l)
        {
            var c = pool.Count > 0 ? pool.Pop() : Make();
            c.rt.gameObject.SetActive(true);
            c.dying = false;
            Style(c, l);
            c.y = -18f; c.alpha = 0f;
            cards.Add(c);
            Relayout();
        }

        Card Make()
        {
            var c = new Card();
            c.bg = UIKit.Box("Card", root, HUDDraw.ChatCard, 12);
            c.rt = c.bg.rectTransform;
            c.cg = c.rt.gameObject.AddComponent<CanvasGroup>();
            c.cg.blocksRaycasts = false; c.cg.interactable = false;
            c.border = UIKit.Box("Border", c.rt, Color.clear, 12, 2);
            UIKit.Stretch(c.border.rectTransform);
            c.text = UIKit.Text("Text", c.rt, "", HUDDraw.FM, UIKit.Ink, false, TextAnchor.UpperLeft);
            c.text.lineSpacing = 1f;
            c.pill = UIKit.Box("Amount", c.rt, HUDDraw.Gold, 8);
            c.pillText = HUDDraw.Label("Text", c.pill.transform, "", HUDDraw.FS, HUDDraw.Dark, true, TextAnchor.MiddleCenter);
            UIKit.Stretch(c.pillText.rectTransform);
            return c;
        }

        void Style(Card c, ChatLine l)
        {
            string nickHex = NickColor(l.Nick);
            string body = l.Text ?? "";
            Color bg = HUDDraw.ChatCard, border = Color.clear, ink = UIKit.Ink;
            bool showPill = false;
            switch (l.Kind)
            {
                case ChatKind.Donation:
                    bg = new Color(0.918f, 0.702f, 0.031f, 0.22f);
                    border = UIKit.A(HUDDraw.GoldLight, 0.85f);
                    nickHex = "FACC15";
                    showPill = l.Amount > 0;
                    break;
                case ChatKind.Warn:
                    bg = UIKit.A(UIKit.Rec, 0.94f);
                    nickHex = "FFFFFF";
                    ink = Color.white;
                    break;
                case ChatKind.Mod:
                    bg = new Color(0.847f, 0.698f, 0.478f, 0.14f);
                    border = UIKit.A(UIKit.Accent, 0.6f);
                    ink = UIKit.Accent;
                    break;
            }
            c.bg.color = bg;
            c.border.color = border;
            c.border.enabled = border.a > 0f;
            string inkHex = ColorUtility.ToHtmlStringRGB(ink);
            bool boldBody = l.Kind == ChatKind.Warn;
            string msg = boldBody ? "<b>" + body + "</b>" : body;
            c.text.text = string.IsNullOrEmpty(l.Nick)
                ? "<color=#" + inkHex + ">" + msg + "</color>"
                : "<b><color=#" + nickHex + ">" + l.Nick + "</color></b>  <color=#" + inkHex + ">" + msg + "</color>";

            // 크기: 한 줄로 맞으면 내용 폭, 넘치면 최대 폭에서 줄바꿈
            float pillW = 0f;
            c.pill.gameObject.SetActive(showPill);
            if (showPill)
            {
                c.pillText.text = l.Amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + "원";
                pillW = HUDDraw.Width(c.pillText) + 16f;
            }
            float maxInner = Width - PadX * 2f - (showPill ? pillW + 10f : 0f);
            c.text.horizontalOverflow = HorizontalWrapMode.Overflow;
            float tw = Mathf.Ceil(c.text.preferredWidth);
            float innerW = Mathf.Min(tw, maxInner);
            c.text.horizontalOverflow = HorizontalWrapMode.Wrap;
            var trt = c.text.rectTransform;
            HUDDraw.At(trt, HUDDraw.TL, HUDDraw.TL, new Vector2(PadX, -PadY), new Vector2(innerW + 2f, 10f));
            float th = Mathf.Ceil(c.text.preferredHeight);
            trt.sizeDelta = new Vector2(innerW + 2f, th);
            float w = innerW + PadX * 2f + (showPill ? pillW + 10f : 0f);
            c.h = th + PadY * 2f;
            HUDDraw.At(c.rt, HUDDraw.BL, HUDDraw.BL, new Vector2(0, 0), new Vector2(Mathf.Min(w, Width), c.h));
            if (showPill) HUDDraw.At(c.pill.rectTransform, HUDDraw.MR, HUDDraw.MR, new Vector2(-10f, 0f), new Vector2(pillW, 26f));
        }

        void Relayout()
        {
            float y = 0f;
            int live = 0;
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                var c = cards[i];
                if (c.dying) continue;
                if (live >= MaxCards) { c.dying = true; c.targetAlpha = 0f; continue; }
                c.targetY = y;
                c.targetAlpha = live < 3 ? 1f : live == 3 ? 0.72f : 0.45f;
                y += c.h + Gap;
                live++;
            }
        }

        public void Tick(float dt)
        {
            float k = HUDDraw.Damp(16f, dt), ka = HUDDraw.Damp(12f, dt);
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                var c = cards[i];
                if (!c.dying) c.y = Mathf.Lerp(c.y, c.targetY, k);
                c.alpha = Mathf.Lerp(c.alpha, c.targetAlpha, ka);
                c.rt.anchoredPosition = new Vector2(0f, c.y);
                c.cg.alpha = c.alpha;
                if (c.dying && c.alpha < 0.02f)
                {
                    c.rt.gameObject.SetActive(false);
                    cards.RemoveAt(i);
                    pool.Push(c);
                }
            }
        }

        static string NickColor(string nick)
        {
            if (string.IsNullOrEmpty(nick)) return NickPalette[0];
            int h = 17;
            for (int i = 0; i < nick.Length; i++) h = h * 31 + nick[i];
            return NickPalette[(h & 0x7fffffff) % NickPalette.Length];
        }
    }
}
