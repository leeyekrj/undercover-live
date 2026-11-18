using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 코드로 UI를 만드는 공용 도구. 이미지 파일 없이 둥근 사각형·원·링·그라데이션을 코드로 그려 쓴다.
    /// 기준 캔버스 1920×1080, 높이에 맞춰 확대·축소. 서체는 Pretendard Medium/Bold 둘뿐.
    /// 애니메이션은 Time.timeScale = 0(멈춤·메뉴)에서도 돌게 unscaled 시간으로 돈다.
    /// </summary>
    public static class UIKit
    {
        // ---------- 색 ----------
        public static readonly Color Ink = Hex("#F2EFE9");
        public static readonly Color Muted = new Color(0.949f, 0.937f, 0.914f, 0.58f);
        public static readonly Color Faint = new Color(0.949f, 0.937f, 0.914f, 0.32f);
        public static readonly Color Line = new Color(0.949f, 0.937f, 0.914f, 0.14f);
        public static readonly Color Accent = Hex("#D8B27A");   // 교단 금빛
        public static readonly Color Rec = Hex("#E5484D");      // 방송 빨강
        public static readonly Color Ok = Hex("#38D399");       // 확보 초록
        public static readonly Color Warn = Hex("#F59E0B");
        public static readonly Color Panel = new Color(0.055f, 0.055f, 0.07f, 0.92f);
        public static readonly Color PanelSoft = new Color(0.07f, 0.07f, 0.09f, 0.78f);

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color A(Color c, float a) { c.a = a; return c; }

        // ---------- 서체 ----------
        static Font medium, bold;
        /// <summary>Pretendard Medium. 씬의 PhoneController.UIFont가 Medium이면 그걸 쓴다.</summary>
        public static Font Medium
        {
            get
            {
                if (medium == null)
                {
                    var pc = PhoneController.Instance != null ? PhoneController.Instance : UnityEngine.Object.FindAnyObjectByType<PhoneController>();
                    medium = pc != null && pc.UIFont != null ? pc.UIFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return medium;
            }
            set { medium = value; }
        }
        /// <summary>Pretendard Bold. 따로 지정이 없으면 Medium에 FontStyle.Bold를 쓴다.</summary>
        public static Font Bold { get { return bold != null ? bold : Medium; } set { bold = value; } }

        // ---------- 스프라이트 (코드로 그림, 9-slice) ----------
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>모서리 반지름 r(px)의 둥근 사각형. border>0이면 테두리만(두께 border px).</summary>
        public static Sprite Rounded(int r, int border = 0)
        {
            string key = "rr" + r + "_" + border;
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            int size = r * 2 + 4;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = RoundedAlpha(x + 0.5f, y + 0.5f, size, size, r);
                    if (border > 0) a = Mathf.Min(a, 1f - RoundedAlpha(x + 0.5f, y + 0.5f, size, size, Mathf.Max(0, r - border), border));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            cache[key] = s;
            return s;
        }

        static float RoundedAlpha(float x, float y, int w, int h, float r, float inset = 0f)
        {
            float minX = inset, minY = inset, maxX = w - inset, maxY = h - inset;
            float cx = Mathf.Clamp(x, minX + r, maxX - r), cy = Mathf.Clamp(y, minY + r, maxY - r);
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            float edge = r > 0 ? r : 0f;
            if (x < minX || x > maxX || y < minY || y > maxY) return 0f;
            return Mathf.Clamp01(edge - d + 0.5f + (r > 0 ? 0f : 1f));
        }

        /// <summary>지름 size(px)의 원. thickness>0이면 링.</summary>
        public static Sprite Circle(int size = 64, int thickness = 0)
        {
            string key = "c" + size + "_" + thickness;
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float R = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(R, R));
                    float a = Mathf.Clamp01(R - d);
                    if (thickness > 0) a = Mathf.Min(a, Mathf.Clamp01(d - (R - thickness)));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            cache[key] = s;
            return s;
        }

        /// <summary>세로 그라데이션 띠 (아래 불투명 → 위 투명). 화면 아래 글자 받침 등.</summary>
        public static Sprite VerticalFade(int height = 64)
        {
            string key = "vf" + height;
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = NewTex(2, height);
            var px = new Color32[2 * height];
            for (int y = 0; y < height; y++)
            {
                float t = 1f - y / (float)(height - 1);
                byte a = (byte)(t * t * 255);
                px[y * 2] = px[y * 2 + 1] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, 2, height), new Vector2(0.5f, 0.5f), 100f);
            cache[key] = s;
            return s;
        }

        /// <summary>가운데 투명 → 가장자리 불투명 (비네트·가장자리 맥박용).</summary>
        public static Sprite EdgeVignette(int size = 128, float inner = 0.55f)
        {
            string key = "ev" + size + "_" + inner;
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) * 0.6f + new Vector2(u, v).magnitude * 0.4f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, 1.05f, d));
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

        // ---------- 캔버스·요소 ----------

        /// <summary>화면 전체 오버레이 캔버스 (1920×1080 기준, 높이 맞춤).</summary>
        public static Canvas Canvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortingOrder;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 1f;
            EnsureEventSystem();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            // 새 Input System 쓰는 프로젝트: InputSystemUIInputModule
            var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) es.AddComponent(t); else es.AddComponent<StandaloneInputModule>();
        }

        /// <summary>왼쪽 위 기준 좌표(px)로 놓는 사각형. Argon 시안 좌표(x,y,w,h)를 그대로 옮길 때 쓴다.</summary>
        public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Image(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color; img.raycastTarget = false;
            if (sprite != null) { img.sprite = sprite; img.type = sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple; }
            return img;
        }

        /// <summary>둥근 사각형 판. r = 모서리 반지름(px).</summary>
        public static Image Box(string name, Transform parent, Color color, int r = 12, int border = 0)
        {
            return Image(name, parent, color, Rounded(r, border));
        }

        public static Text Text(string name, Transform parent, string text, int size, Color color, bool bold = false, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = bold ? Bold : Medium;
            if (bold && Bold == Medium) t.fontStyle = FontStyle.Bold;
            t.fontSize = size; t.color = color; t.text = text; t.alignment = anchor;
            t.supportRichText = true; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.05f;
            return t;
        }

        /// <summary>
        /// 버튼: 둥근 판 + 글자. 마우스를 올리면 밝아지고 누르면 살짝 줄어든다(unscaled).
        /// </summary>
        public static Button Button(string name, Transform parent, string label, int fontSize, Color bg, Color fg, Action onClick, int r = 12)
        {
            var img = Box(name, parent, bg, r);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            var lab = Text("Label", img.transform, label, fontSize, fg, true, TextAnchor.MiddleCenter);
            Stretch(lab.rectTransform);
            img.gameObject.AddComponent<HoverPress>().Init(img, bg);
            return btn;
        }

        // ---------- 애니메이션 (unscaled) ----------
        static Runner runner;
        static Runner R
        {
            get
            {
                if (runner == null)
                {
                    var go = new GameObject("UIKit.Runner") { hideFlags = HideFlags.HideAndDontSave };
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    runner = go.AddComponent<Runner>();
                }
                return runner;
            }
        }
        class Runner : MonoBehaviour { }

        public static Coroutine Run(IEnumerator co) => R.StartCoroutine(co);

        /// <summary>0→1을 dur초 동안 (unscaled). ease: 끝으로 갈수록 느려짐.</summary>
        public static Coroutine Tween(float dur, Action<float> step, Action done = null, bool easeOut = true)
        {
            return R.StartCoroutine(TweenCo(dur, step, done, easeOut));
        }

        static IEnumerator TweenCo(float dur, Action<float> step, Action done, bool easeOut)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                if (easeOut) k = 1f - Mathf.Pow(1f - k, 3f);
                step?.Invoke(k);
                yield return null;
            }
            step?.Invoke(1f);
            done?.Invoke();
        }

        public static Coroutine Fade(CanvasGroup g, float to, float dur, Action done = null)
        {
            float from = g.alpha;
            return Tween(dur, k => { if (g != null) g.alpha = Mathf.Lerp(from, to, k); }, done);
        }

        /// <summary>한국어 주격 조사: 받침 있으면 '이', 없으면 '가'.</summary>
        public static string Josa(string word, string withBatchim = "이", string without = "가")
        {
            if (string.IsNullOrEmpty(word)) return withBatchim;
            char c = word[word.Length - 1];
            if (c < 0xAC00 || c > 0xD7A3) return withBatchim;
            return (c - 0xAC00) % 28 != 0 ? withBatchim : without;
        }
    }

    /// <summary>버튼 마우스 반응: 올리면 밝게, 누르면 0.97배.</summary>
    public class HoverPress : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        Image img; Color baseColor; bool over, down;
        public void Init(Image i, Color c) { img = i; baseColor = c; }
        public void OnPointerEnter(PointerEventData e) { over = true; }
        public void OnPointerExit(PointerEventData e) { over = false; down = false; }
        public void OnPointerDown(PointerEventData e) { down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }
        void Update()
        {
            if (img == null) return;
            var target = over ? Color.Lerp(baseColor, Color.white, baseColor.a > 0.5f ? 0.12f : 0.08f) : baseColor;
            if (over && baseColor.a < 0.5f) target.a = Mathf.Min(1f, baseColor.a + 0.1f);
            img.color = Color.Lerp(img.color, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
            float s = down ? 0.97f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(s, s, 1f), 1f - Mathf.Exp(-Time.unscaledDeltaTime * 22f));
        }
    }
}
