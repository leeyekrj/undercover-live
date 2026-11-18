using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>PAYDAY식 의심 표시. 정조준(IsScreenVisible) 중에는 전부 숨긴다.</summary>
    public class SuspicionHUD : MonoBehaviour
    {
        public Camera viewCamera;               // 비면 Camera.main → PhoneCamera
        public float ringSize = 120f;
        public float arrowRadius = 86f;
        public bool showDotsAtZero = false;
        public Color blue = new Color(0.35f, 0.62f, 1f);
        public Color orange = new Color(1f, 0.6f, 0.15f);
        public Color red = new Color(0.95f, 0.15f, 0.15f);

        Canvas canvas;
        CanvasGroup ringGroup, dotsGroup;
        Image ringBg, ringFill, arrow;
        Text mark;
        Image[] dots = new Image[3];
        RectTransform ringRt, arrowRt;
        Texture2D ringTex, arrowTex, dotTex;
        Sprite ringSprite, arrowSprite, dotSprite;
        float arrowAngle;
        bool fontSet;

        void Awake()
        {
            Build();
        }

        void OnDestroy()
        {
            if (ringSprite != null) Destroy(ringSprite);
            if (arrowSprite != null) Destroy(arrowSprite);
            if (dotSprite != null) Destroy(dotSprite);
            if (ringTex != null) Destroy(ringTex);
            if (arrowTex != null) Destroy(arrowTex);
            if (dotTex != null) Destroy(dotTex);
        }

        void Build()
        {
            var cgo = new GameObject("SuspicionCanvas");
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            ringTex = MakeRing(256, 0.78f, 1f);
            arrowTex = MakeTriangle(64);
            dotTex = MakeDisc(64);
            ringSprite = Sprite.Create(ringTex, new Rect(0, 0, ringTex.width, ringTex.height), new Vector2(0.5f, 0.5f), 100f);
            arrowSprite = Sprite.Create(arrowTex, new Rect(0, 0, arrowTex.width, arrowTex.height), new Vector2(0.5f, 0.5f), 100f);
            dotSprite = Sprite.Create(dotTex, new Rect(0, 0, dotTex.width, dotTex.height), new Vector2(0.5f, 0.5f), 100f);

            // 링 묶음(화면 가운데)
            var ringGo = NewUI("Ring", cgo.transform);
            ringRt = ringGo.GetComponent<RectTransform>();
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            ringRt.anchoredPosition = Vector2.zero;
            ringRt.sizeDelta = new Vector2(ringSize, ringSize);
            ringGroup = ringGo.AddComponent<CanvasGroup>();
            ringGroup.alpha = 0f;
            ringGroup.blocksRaycasts = false;
            ringGroup.interactable = false;

            ringBg = AddImage("Back", ringGo.transform, ringSprite, new Color(0f, 0f, 0f, 0.35f), ringSize);
            ringFill = AddImage("Fill", ringGo.transform, ringSprite, blue, ringSize);
            ringFill.type = Image.Type.Filled;
            ringFill.fillMethod = Image.FillMethod.Radial360;
            ringFill.fillOrigin = (int)Image.Origin360.Top;
            ringFill.fillClockwise = true;
            ringFill.fillAmount = 0f;

            var markGo = NewUI("Mark", ringGo.transform);
            var mrt = markGo.GetComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
            mrt.offsetMin = mrt.offsetMax = Vector2.zero;
            mark = markGo.AddComponent<Text>();
            mark.text = "?";
            mark.alignment = TextAnchor.MiddleCenter;
            mark.fontSize = Mathf.RoundToInt(ringSize * 0.5f);
            mark.fontStyle = FontStyle.Bold;
            mark.color = blue;
            mark.raycastTarget = false;
            mark.horizontalOverflow = HorizontalWrapMode.Overflow;
            mark.verticalOverflow = VerticalWrapMode.Overflow;
            TrySetFont();

            // 방향 화살표(링 바깥을 돈다)
            var arrowGo = NewUI("Arrow", ringGo.transform);
            arrowRt = arrowGo.GetComponent<RectTransform>();
            arrowRt.anchorMin = arrowRt.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRt.sizeDelta = new Vector2(22f, 22f);
            arrow = arrowGo.AddComponent<Image>();
            arrow.sprite = arrowSprite;
            arrow.raycastTarget = false;
            arrow.color = blue;

            // 경계 레벨 점 3개(왼쪽 위)
            var dotsGo = NewUI("AlertDots", cgo.transform);
            var drt = dotsGo.GetComponent<RectTransform>();
            drt.anchorMin = drt.anchorMax = new Vector2(0f, 1f);
            drt.pivot = new Vector2(0f, 1f);
            drt.anchoredPosition = new Vector2(36f, -36f);
            drt.sizeDelta = new Vector2(80f, 20f);
            dotsGroup = dotsGo.AddComponent<CanvasGroup>();
            dotsGroup.alpha = 0f;
            dotsGroup.blocksRaycasts = false;
            dotsGroup.interactable = false;
            for (int i = 0; i < 3; i++)
            {
                dots[i] = AddImage("Dot" + i, dotsGo.transform, dotSprite, Color.white, 16f);
                var r = dots[i].rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(i * 26f, 0f);
            }
        }

        void TrySetFont()
        {
            if (mark == null) return;
            var pc = PhoneController.Instance;
            if (pc != null && pc.UIFont != null) { mark.font = pc.UIFont; fontSet = true; return; }
            if (mark.font == null) mark.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static GameObject NewUI(string n, Transform parent)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static Image AddImage(string n, Transform parent, Sprite s, Color c, float size)
        {
            var go = NewUI(n, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = s;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        void LateUpdate()
        {
            var pc = PhoneController.Instance;
            var mgr = SuspicionManager.Instance;
            if (!fontSet) TrySetFont();

            bool hide = pc != null && pc.IsScreenVisible;
            float max01 = mgr != null ? mgr.MaxSuspicion01 : 0f;
            Watcher top = mgr != null ? mgr.MostSuspicious : null;
            float dt = Time.unscaledDeltaTime;
            float k = 1f - Mathf.Exp(-14f * dt);

            // 링
            float ringTarget = (!hide && max01 > 0.001f) ? 1f : 0f;
            ringGroup.alpha = Mathf.Lerp(ringGroup.alpha, ringTarget, k);

            bool full = max01 >= 0.999f;
            Color col = full ? red : (max01 >= 0.5f ? orange : blue);
            ringFill.fillAmount = max01;
            ringFill.color = col;
            mark.color = col;
            arrow.color = col;
            mark.text = full ? "!" : "?";
            float pulse = full ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 14f) : 1f;
            ringRt.localScale = Vector3.one * pulse;

            // 화살표
            if (top != null && ringTarget > 0f)
            {
                Camera cam = viewCamera != null ? viewCamera : Camera.main;
                if (cam == null && pc != null) cam = pc.PhoneCamera;
                if (cam != null)
                {
                    Vector3 local = cam.transform.InverseTransformPoint(top.EyePosition);
                    float target = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg; // 위쪽=0, 시계 방향 +
                    arrowAngle = Mathf.LerpAngle(arrowAngle, target, 1f - Mathf.Exp(-12f * dt));
                    float rad = arrowAngle * Mathf.Deg2Rad;
                    arrowRt.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * arrowRadius;
                    arrowRt.localRotation = Quaternion.Euler(0f, 0f, -arrowAngle);
                }
            }

            // 경계 점
            int level = mgr != null ? mgr.AlertLevel : 0;
            float dotsTarget = (!hide && (level > 0 || showDotsAtZero)) ? 1f : 0f;
            dotsGroup.alpha = Mathf.Lerp(dotsGroup.alpha, dotsTarget, k);
            for (int i = 0; i < 3; i++)
            {
                bool on = i < level;
                dots[i].color = on ? (level >= 3 ? red : orange) : new Color(1f, 1f, 1f, 0.25f);
            }
        }

        // ---- 런타임 텍스처 ----
        static Texture2D MakeRing(int size, float inner, float outer)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float aa = 1.5f / (size * 0.5f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (size * 0.5f);
                    float a = Mathf.Clamp01((r - (inner - aa)) / aa) * Mathf.Clamp01(((outer - aa) - r) / aa + 1f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        static Texture2D MakeDisc(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(c - r + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        // 위쪽을 가리키는 삼각형
        static Texture2D MakeTriangle(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);          // 0 아래 → 1 위
                float half = (1f - v) * 0.5f;             // 아래가 넓다
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1) - 0.5f;
                    float a = Mathf.Clamp01((half - Mathf.Abs(u)) * size * 0.5f + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }
    }
}
