using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// HUD 공용 크기·색·도형. UIKit 위에 HUD에만 필요한 것(안쪽이 뚫린 둥근 모서리, 의심 호 링, 배치 도우미)을 더한다.
    /// 서체 크기는 화면마다 4종 이하: S(키·배지·작은 이름) · M(상태 줄·채팅·안내) · L(경고 제목) · XL(등급 도장).
    /// </summary>
    public static class HUDDraw
    {
        public const int FS = 16, FM = 18, FL = 22, FXL = 80;

        public static readonly Color Gold = UIKit.Hex("#EAB308");
        public static readonly Color GoldLight = UIKit.Hex("#FACC15");
        public static readonly Color Blue = UIKit.Hex("#38BDF8");
        public static readonly Color Teal = UIKit.Hex("#22D3EE");
        public static readonly Color Mint = UIKit.Hex("#A7F3D0");
        public static readonly Color Bezel = UIKit.Hex("#0D0E12");
        public static readonly Color Dark = UIKit.Hex("#0D0E12");
        public static readonly Color Card = new Color(0.055f, 0.059f, 0.078f, 0.84f);
        public static readonly Color ChatCard = new Color(0.047f, 0.051f, 0.07f, 0.74f);
        public static readonly Color KeyCapBg = new Color(1f, 1f, 1f, 0.18f);

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>둥근 사각형의 바깥(모서리 바깥쪽)만 불투명한 9-slice. 사각 피드 위에 덮어 모서리를 둥글게 깎는다.</summary>
        public static Sprite InverseRounded(int r)
        {
            string key = "inv" + r;
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            int size = r * 2 + 4;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cx = Mathf.Clamp(fx, r, size - r), cy = Mathf.Clamp(fy, r, size - r);
                    float d = Vector2.Distance(new Vector2(fx, fy), new Vector2(cx, cy));
                    float inside = Mathf.Clamp01(r - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)((1f - inside) * 255));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            cache[key] = s;
            return s;
        }

        /// <summary>바깥 반지름 outer, 두께 thickness의 링 (Filled Radial360으로 호를 그린다).</summary>
        public static Sprite Ring(int outer, float thickness)
        {
            string key = "ring" + outer + "_" + thickness;
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            int size = outer * 2 + 4;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - (outer - thickness) + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            cache[key] = s;
            return s;
        }

        static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        }

        // ---------- 배치 ----------

        /// <summary>anchor·pivot을 함께 정해 놓는다. pos는 anchor 기준 px (위쪽이 +).</summary>
        public static RectTransform At(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static readonly Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1), BL = new Vector2(0, 0), BR = new Vector2(1, 0);
        public static readonly Vector2 TC = new Vector2(0.5f, 1), BC = new Vector2(0.5f, 0), MC = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 ML = new Vector2(0, 0.5f), MR = new Vector2(1, 0.5f);

        /// <summary>한 줄 글자 (줄바꿈 없음, 그림자 없음).</summary>
        public static Text Label(string name, Transform parent, string text, int size, Color color, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var t = UIKit.Text(name, parent, text, size, color, bold, anchor);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1f;
            return t;
        }

        /// <summary>한 줄로 썼을 때 폭(px).</summary>
        public static float Width(Text t)
        {
            var o = t.horizontalOverflow;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            float w = t.preferredWidth;
            t.horizontalOverflow = o;
            return Mathf.Ceil(w);
        }

        /// <summary>키 캡 하나 [우클릭] (둥근 판 + 굵은 글자). 폭을 돌려준다.</summary>
        public static float KeyCap(Transform parent, string key, Color bg, Color fg, float x, float h, out RectTransform rt)
        {
            var img = UIKit.Box("Key_" + key, parent, bg, 6);
            var t = Label("Text", img.transform, key, FS, fg, true, TextAnchor.MiddleCenter);
            float w = Width(t) + 16f;
            rt = img.rectTransform;
            At(rt, ML, ML, new Vector2(x, 0), new Vector2(w, h));
            UIKit.Stretch(t.rectTransform);
            return w;
        }

        public static float EaseOut(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }
        public static float Smooth(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

        /// <summary>프레임 독립 지수 감쇠 보간 계수.</summary>
        public static float Damp(float speed, float dt) { return 1f - Mathf.Exp(-speed * dt); }
    }
}
