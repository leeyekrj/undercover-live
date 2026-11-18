using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 담당 A 화면(메인·도입·멈춤·설정·결과·발각)이 같이 쓰는 그리기 도구. UIKit 위에 얹는다.
    /// 둥근 판·테두리·링은 2배 해상도로 그려 1.5px 선도 또렷하게 나온다(pixelsPerUnitMultiplier 2).
    /// 애니메이션은 화면 컴포넌트의 StartCoroutine으로 돌려서 씬이 바뀌면 같이 멈춘다(unscaled 시간).
    /// </summary>
    public static class UIParts
    {
        // ---------- 색 ----------
        public static readonly Color Bg = UIKit.Hex("#0B0B0E");
        public static readonly Color Dark = UIKit.Hex("#0B0B0E");
        public static readonly Color Card = UIKit.Hex("#14151A");
        public static readonly Color PanelBg = UIKit.Hex("#13141A");
        public static readonly Color PanelElev = UIKit.Hex("#191B23");
        public static readonly Color PanelMuted = UIKit.Hex("#101116");
        public static readonly Color PhoneBody = UIKit.Hex("#121318");
        public static readonly Color ScreenBg = UIKit.Hex("#070709");
        public static readonly Color GoldBright = UIKit.Hex("#F5D08A");
        public static readonly Color Green = UIKit.Hex("#4ADE80");
        public static readonly Color RecSoft = UIKit.Hex("#FF8A8E");
        public static readonly Color Blue = UIKit.Hex("#8CC4FF");

        /// <summary>바탕 글자색(F2EFE9)에 투명도.</summary>
        public static Color Ink(float a) => UIKit.A(UIKit.Ink, a);
        /// <summary>어두운 막 rgba(11,11,13,a).</summary>
        public static Color Shade(float a) => new Color(0.043f, 0.043f, 0.051f, a);

        // ---------- 판·선·원 ----------

        /// <summary>둥근 사각형 채움(반지름 r, 캔버스 px).</summary>
        public static Image Fill(string name, Transform parent, Color c, float r)
        {
            int rr = Mathf.Max(1, Mathf.RoundToInt(r * 2f));
            var img = UIKit.Image(name, parent, c, r > 0f ? UIKit.Rounded(rr) : null);
            if (r > 0f) img.pixelsPerUnitMultiplier = 2f;
            return img;
        }

        /// <summary>둥근 테두리(부모를 꽉 채움). px = 선 두께.</summary>
        public static Image Line(string name, Transform parent, Color c, float r, float px = 1f)
        {
            int rr = Mathf.Max(1, Mathf.RoundToInt(r * 2f));
            int bb = Mathf.Max(1, Mathf.RoundToInt(px * 2f));
            var img = UIKit.Image(name, parent, c, UIKit.Rounded(Mathf.Max(rr, bb), bb));
            img.pixelsPerUnitMultiplier = 2f;
            UIKit.Stretch(img.rectTransform);
            return img;
        }

        /// <summary>채움 + 테두리 판. line.a == 0이면 테두리 없음.</summary>
        public static Image Panel(string name, Transform parent, Color fill, float r, Color line, float px = 1f)
        {
            var f = Fill(name, parent, fill, r);
            if (line.a > 0f) Line("Line", f.transform, line, r, px);
            return f;
        }

        /// <summary>지름 d의 링(두께 px).</summary>
        public static Image Ring(string name, Transform parent, Color c, float d, float px)
        {
            var img = UIKit.Image(name, parent, c, UIKit.Circle(Mathf.RoundToInt(d * 2f), Mathf.Max(1, Mathf.RoundToInt(px * 2f))));
            img.rectTransform.sizeDelta = new Vector2(d, d);
            return img;
        }

        /// <summary>지름 d의 채운 원.</summary>
        public static Image Dot(string name, Transform parent, Color c, float d)
        {
            var img = UIKit.Image(name, parent, c, UIKit.Circle(Mathf.Max(16, Mathf.RoundToInt(d * 4f))));
            img.rectTransform.sizeDelta = new Vector2(d, d);
            return img;
        }

        /// <summary>각진 사각형(선·막대).</summary>
        public static Image Bar(string name, Transform parent, Color c, float x, float y, float w, float h)
        {
            var img = UIKit.Image(name, parent, c);
            UIKit.Place(img.rectTransform, x, y, w, h);
            return img;
        }

        /// <summary>뷰파인더 ㄱ자 모서리. (x,y) = 모서리 상자의 왼쪽 위, s = 한 변 길이.</summary>
        public static void Corner(Transform parent, float x, float y, float s, float px, Color c, bool right, bool bottom)
        {
            Bar("CornerH", parent, c, x, bottom ? y + s - px : y, s, px);
            Bar("CornerV", parent, c, right ? x + s - px : x, y, px, s);
        }

        static readonly Dictionary<string, Sprite> grads = new Dictionary<string, Sprite>();

        /// <summary>가로 그라데이션(왼→오). stops 0~1 위치별 알파. 색은 Image.color로.</summary>
        public static Sprite HGradient(float[] stops, float[] alphas) { return Gradient(true, stops, alphas); }

        /// <summary>세로 그라데이션(위→아래). stops 0~1 위치별 알파.</summary>
        public static Sprite VGradient(float[] stops, float[] alphas) { return Gradient(false, stops, alphas); }

        static Sprite Gradient(bool horizontal, float[] stops, float[] alphas)
        {
            string key = (horizontal ? "h" : "v") + string.Join(",", stops) + "|" + string.Join(",", alphas);
            if (grads.TryGetValue(key, out var s) && s != null) return s;
            const int n = 128;
            int w = horizontal ? n : 2, h = horizontal ? 2 : n;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[w * h];
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                float a = Sample(stops, alphas, u);
                var c = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                if (horizontal) { px[i] = c; px[n + i] = c; }
                else { int row = n - 1 - i; px[row * 2] = c; px[row * 2 + 1] = c; }   // 텍스처 y=0이 아래
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            grads[key] = s;
            return s;
        }

        static float Sample(float[] stops, float[] alphas, float u)
        {
            if (u <= stops[0]) return alphas[0];
            for (int i = 1; i < stops.Length; i++)
                if (u <= stops[i])
                {
                    float k = Mathf.InverseLerp(stops[i - 1], stops[i], u);
                    return Mathf.Lerp(alphas[i - 1], alphas[i], k);
                }
            return alphas[alphas.Length - 1];
        }

        /// <summary>부모의 둥근 모양으로 자식을 잘라 내는 판(그라데이션 덧칠용).</summary>
        public static RectTransform RoundMask(string name, Transform parent, float r)
        {
            var img = Fill(name, parent, Color.white, r);
            UIKit.Stretch(img.rectTransform);
            var m = img.gameObject.AddComponent<Mask>();
            m.showMaskGraphic = false;
            return img.rectTransform;
        }

        // ---------- 글자 ----------

        public static Text Label(Transform parent, string text, int size, Color c, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            return UIKit.Text("Text", parent, text, size, c, bold, anchor);
        }

        public static Text Label(Transform parent, string text, int size, Color c, bool bold, TextAnchor anchor, float x, float y, float w, float h)
        {
            var t = UIKit.Text("Text", parent, text, size, c, bold, anchor);
            UIKit.Place(t.rectTransform, x, y, w, h);
            return t;
        }

        /// <summary>한 줄로 썼을 때의 폭(캔버스 px).</summary>
        public static float Width(Text t, string s = null)
        {
            var settings = t.GetGenerationSettings(Vector2.zero);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            float ppu = t.pixelsPerUnit > 0f ? t.pixelsPerUnit : 1f;
            return t.cachedTextGeneratorForLayout.GetPreferredWidth(s ?? t.text, settings) / ppu;
        }

        /// <summary>폭 w 안에서 줄바꿈했을 때의 높이.</summary>
        public static float Height(Text t, float w, string s = null)
        {
            var settings = t.GetGenerationSettings(new Vector2(w, 0f));
            float ppu = t.pixelsPerUnit > 0f ? t.pixelsPerUnit : 1f;
            return t.cachedTextGeneratorForLayout.GetPreferredHeight(s ?? t.text, settings) / ppu;
        }

        /// <summary>한 줄에 안 들어가면 끝을 "…"로 줄인다(서식 없는 글자만).</summary>
        public static void FitOneLine(Text t, string s, float maxW)
        {
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            s = s ?? "";
            if (Width(t, s) <= maxW) { t.text = s; return; }
            int lo = 0, hi = s.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (Width(t, s.Substring(0, mid) + "…") <= maxW) lo = mid; else hi = mid - 1;
            }
            t.text = s.Substring(0, lo).TrimEnd() + "…";
        }

        /// <summary>
        /// 글자 폭에 맞춘 알약(배지). (x,y) 왼쪽 위. 폭은 자동. dot이 있으면 앞에 작은 점.
        /// </summary>
        public static RectTransform Pill(Transform parent, string text, int size, Color fg, Color bg, Color line, bool bold,
            float x, float y, float h, float padX, float r = -1f, Color? dot = null)
        {
            if (r < 0f) r = h * 0.5f;
            var p = Panel("Pill", parent, bg, r, line);
            var t = Label(p.transform, text, size, fg, bold, TextAnchor.MiddleLeft);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            float tw = Mathf.Ceil(Width(t));
            float dotW = dot.HasValue ? 12f : 0f;
            float w = padX * 2f + dotW + tw;
            UIKit.Place(p.rectTransform, x, y, w, h);
            if (dot.HasValue)
            {
                var d = Dot("Dot", p.transform, dot.Value, 6f);
                UIKit.Place(d.rectTransform, padX, (h - 6f) * 0.5f, 6f, 6f);
            }
            UIKit.Place(t.rectTransform, padX + dotW, 0f, tw + 4f, h);
            return p.rectTransform;
        }

        /// <summary>알약을 오른쪽 끝 기준으로 옮긴다.</summary>
        public static void AlignRight(RectTransform rt, float right)
        {
            var p = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(right - rt.sizeDelta.x, p.y);
        }

        /// <summary>키 표시(예: Esc, R).</summary>
        public static RectTransform Key(Transform parent, string key, int size, Color fg, Color bg, Color line, float x, float y, float h, float minW = 0f)
        {
            var rt = Pill(parent, key, size, fg, bg, line, true, x, y, h, 8f, 6f);
            if (rt.sizeDelta.x < minW)
            {
                rt.sizeDelta = new Vector2(minW, h);
                var t = rt.GetComponentInChildren<Text>();
                if (t != null) { t.alignment = TextAnchor.MiddleCenter; UIKit.Stretch(t.rectTransform); }
            }
            return rt;
        }

        // ---------- 버튼·슬라이더 ----------

        /// <summary>둥근 판 버튼(글자는 호출하는 쪽에서 얹는다). 클릭 뒤 선택을 풀어 Space·Enter가 다시 누르지 않게 한다.</summary>
        public static Button Button(string name, Transform parent, float x, float y, float w, float h, Color fill, Color line, float r, Action onClick)
        {
            var img = Fill(name, parent, fill, r);
            img.raycastTarget = true;
            UIKit.Place(img.rectTransform, x, y, w, h);
            if (line.a > 0f) Line("Line", img.transform, line, r, 1f);
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            b.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                onClick?.Invoke();
            });
            img.gameObject.AddComponent<HoverPress>().Init(img, fill);
            return b;
        }

        /// <summary>
        /// 가로 슬라이더(높이 24). 트랙 6px 바탕 + 금빛 채움 + 흰 원 손잡이 20px.
        /// </summary>
        public static Slider Slider(Transform parent, float x, float y, float w, float min, float max, float value, Action<float> onChange)
        {
            var rootImg = UIKit.Image("Slider", parent, new Color(0, 0, 0, 0));
            rootImg.raycastTarget = true;
            var root = rootImg.rectTransform;
            UIKit.Place(root, x, y, w, 24f);

            var track = Fill("Track", root, Ink(0.18f), 3f);
            UIKit.Place(track.rectTransform, 0f, 9f, w, 6f);

            var fillArea = UIKit.Rect("FillArea", root);
            UIKit.Place(fillArea, 0f, 9f, w, 6f);
            var fill = Fill("Fill", fillArea, UIKit.Accent, 3f);
            var frt = fill.rectTransform;
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(0, 1); frt.pivot = new Vector2(0.5f, 0.5f);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;

            var handleArea = UIKit.Rect("HandleArea", root);
            UIKit.Place(handleArea, 0f, 2f, w, 20f);
            var handle = UIKit.Image("Handle", handleArea, UIKit.Ink, UIKit.Circle(64));
            handle.raycastTarget = true;
            var hrt = handle.rectTransform;
            hrt.anchorMin = new Vector2(0, 0); hrt.anchorMax = new Vector2(0, 1); hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.sizeDelta = new Vector2(20f, 0f);

            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = frt;
            s.handleRect = hrt;
            s.targetGraphic = handle;
            s.transition = Selectable.Transition.None;
            var nav = s.navigation; nav.mode = Navigation.Mode.None; s.navigation = nav;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = min; s.maxValue = max; s.wholeNumbers = false;
            s.SetValueWithoutNotify(value);
            s.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return s;
        }

        // ---------- 애니메이션 (unscaled, 컴포넌트에서 StartCoroutine) ----------

        public static float EaseOut(float k) { return 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f); }
        public static float EaseInOut(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

        /// <summary>0→1을 dur초 동안(ease-out, unscaled). delay초 뒤 시작.</summary>
        public static IEnumerator Anim(float dur, Action<float> step, float delay = 0f, Action done = null)
        {
            if (delay > 0f)
            {
                step?.Invoke(0f);
                float w = 0f;
                while (w < delay) { w += Time.unscaledDeltaTime; yield return null; }
            }
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                step?.Invoke(EaseOut(t / dur));
                yield return null;
            }
            step?.Invoke(1f);
            done?.Invoke();
        }

        /// <summary>그룹을 dur초 동안 to 투명도로.</summary>
        public static IEnumerator FadeTo(CanvasGroup g, float to, float dur, float delay = 0f)
        {
            float from = g != null ? g.alpha : 0f;
            return Anim(dur, k => { if (g != null) g.alpha = Mathf.Lerp(from, to, k); }, delay);
        }

        /// <summary>글자를 한 자씩(초당 cps자).</summary>
        public static IEnumerator Type(Text t, string full, float cps, float delay = 0f)
        {
            if (t == null) yield break;
            t.text = "";
            float w = 0f;
            while (w < delay) { w += Time.unscaledDeltaTime; yield return null; }
            float k = 0f;
            while (t != null && k < full.Length)
            {
                k += Time.unscaledDeltaTime * cps;
                t.text = full.Substring(0, Mathf.Min(full.Length, (int)k));
                yield return null;
            }
            if (t != null) t.text = full;
        }

        public static CanvasGroup Group(GameObject go, float alpha = 1f)
        {
            var g = go.GetComponent<CanvasGroup>();
            if (g == null) g = go.AddComponent<CanvasGroup>();
            g.alpha = alpha;
            return g;
        }

        /// <summary>m:ss</summary>
        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        /// <summary>#RRGGBBAA 서식 문자열(서식 있는 글자용).</summary>
        public static string Tag(Color c) { return "#" + ColorUtility.ToHtmlStringRGBA(c); }
    }
}
