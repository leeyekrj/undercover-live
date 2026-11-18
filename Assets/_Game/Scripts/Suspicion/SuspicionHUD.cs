using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// PAYDAY식 의심 방향 호 (시안 3안). 화면 가운데 반지름 210px 궤도 위에, 나를 보는 감시자마다 그 방향에 호가 차오른다.
    /// 파랑 '?'(~49) → 주황 '?'(50~99) → 빨강 '!'(100). 호 두께 7px. 가장 의심하는 사람에게만 기호를 붙인다.
    /// 촬영 화면(IsScreenVisible)과 플레이 중이 아닐 때는 숨긴다. 경계 단계는 BroadcastWidget이 상태 카드 옆에 보여 준다.
    /// </summary>
    public class SuspicionHUD : MonoBehaviour
    {
        public Camera viewCamera;               // 비면 Camera.main → PhoneCamera
        public float orbitRadius = 210f;
        public float arcThickness = 7f;
        [Tooltip("이 값부터 호가 보이기 시작 (0~100)")]
        public float showFrom = 4f;
        public float minArcDeg = 12f, maxArcDeg = 64f;
        public int maxArcs = 6;
        public Color blue = new Color(0.22f, 0.741f, 0.973f);     // #38BDF8
        public Color orange = new Color(0.961f, 0.62f, 0.043f);   // #F59E0B
        public Color red = new Color(0.898f, 0.224f, 0.271f);     // #E53945

        Canvas canvas;
        CanvasGroup group;
        RectTransform center;
        Text mark;
        RectTransform markRt;

        class Arc
        {
            public RectTransform rt;
            public Image track, fill;
            public Watcher w;
            public float angle, alpha;
            public bool used;
        }
        readonly List<Arc> arcs = new List<Arc>();
        readonly List<Watcher> ranked = new List<Watcher>();
        float markAngle;

        void Awake()
        {
            Build();
        }

        void Build()
        {
            canvas = UIKit.Canvas("SuspicionCanvas", 25, transform);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;

            center = UIKit.Rect("Orbit", canvas.transform);
            HUDDraw.At(center, HUDDraw.MC, HUDDraw.MC, Vector2.zero, Vector2.zero);

            int outer = Mathf.RoundToInt(orbitRadius + arcThickness * 0.5f);
            var ring = HUDDraw.Ring(outer, arcThickness);
            float size = outer * 2f + 4f;
            for (int i = 0; i < maxArcs; i++)
            {
                var a = new Arc();
                a.rt = UIKit.Rect("Arc" + i, center);
                HUDDraw.At(a.rt, HUDDraw.MC, HUDDraw.MC, Vector2.zero, new Vector2(size, size));
                a.track = MakeArc("Track", a.rt, ring, new Color(1, 1, 1, 0.14f));
                a.fill = MakeArc("Fill", a.rt, ring, blue);
                a.rt.gameObject.SetActive(false);
                arcs.Add(a);
            }

            mark = HUDDraw.Label("Mark", center, "?", 34, blue, true, TextAnchor.MiddleCenter);
            markRt = mark.rectTransform;
            HUDDraw.At(markRt, HUDDraw.MC, HUDDraw.MC, Vector2.zero, new Vector2(48f, 48f));
            var sh = mark.gameObject.AddComponent<Outline>();
            sh.effectColor = new Color(0, 0, 0, 0.45f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
        }

        static Image MakeArc(string n, Transform parent, Sprite s, Color c)
        {
            var img = UIKit.Image(n, parent, c, s);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.fillAmount = 0f;
            UIKit.Stretch(img.rectTransform);
            return img;
        }

        Color ColorFor(float s)
        {
            if (s >= 99.5f) return red;
            return s >= 50f ? orange : blue;
        }

        void LateUpdate()
        {
            var pc = PhoneController.Instance;
            var mgr = SuspicionManager.Instance;
            float dt = Time.unscaledDeltaTime;
            float k = HUDDraw.Damp(14f, dt);

            bool hide = !GameEvents.IsPlaying || (pc != null && pc.IsScreenVisible);
            group.alpha = Mathf.Lerp(group.alpha, hide ? 0f : 1f, k);
            bool on = group.alpha > 0.01f;
            if (canvas.enabled != on) canvas.enabled = on;
            if (!on || mgr == null) return;

            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null && pc != null) cam = pc.PhoneCamera;
            if (cam == null) return;

            // 의심 높은 순
            ranked.Clear();
            var ws = mgr.Watchers;
            for (int i = 0; i < ws.Count; i++)
                if (ws[i] != null && ws[i].suspicion >= showFrom) ranked.Add(ws[i]);
            ranked.Sort((a, b) => b.suspicion.CompareTo(a.suspicion));
            if (ranked.Count > maxArcs) ranked.RemoveRange(maxArcs, ranked.Count - maxArcs);

            foreach (var a in arcs) a.used = false;
            // 이미 맡은 감시자는 같은 호를 계속 쓴다 (깜박임 방지)
            foreach (var w in ranked)
            {
                Arc slot = null;
                foreach (var a in arcs) if (a.w == w) { slot = a; break; }
                if (slot == null) foreach (var a in arcs) if (a.w == null || (!ranked.Contains(a.w) && a.alpha < 0.05f)) { slot = a; slot.w = w; slot.angle = AngleTo(cam, w); slot.alpha = 0f; break; }
                if (slot != null) slot.used = true;
            }

            foreach (var a in arcs)
            {
                bool active = a.used && a.w != null;
                float s = a.w != null ? a.w.suspicion : 0f;
                float targetA = active ? Mathf.Clamp01(Mathf.InverseLerp(showFrom, showFrom + 14f, s)) : 0f;
                a.alpha = Mathf.MoveTowards(a.alpha, targetA, dt / 0.2f);
                bool vis = a.alpha > 0.01f;
                if (a.rt.gameObject.activeSelf != vis) a.rt.gameObject.SetActive(vis);
                if (!vis) { if (!active) a.w = null; continue; }
                if (a.w != null) a.angle = Mathf.LerpAngle(a.angle, AngleTo(cam, a.w), HUDDraw.Damp(12f, dt));

                // 가운데가 감시자 방향인 호: 바탕 = 최대 길이, 채움 = 의심만큼
                float fillDeg = Mathf.Lerp(minArcDeg, maxArcDeg, Mathf.Clamp01(s / 100f));
                SetArc(a.track, a.angle, maxArcDeg, new Color(1, 1, 1, 0.14f * a.alpha));
                Color c = ColorFor(s);
                float pulse = s >= 99.5f ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 14f) : 1f;
                SetArc(a.fill, a.angle, fillDeg, UIKit.A(c, a.alpha * pulse));
            }

            // 기호는 가장 의심하는 사람에게만
            Watcher top = ranked.Count > 0 ? ranked[0] : null;
            if (top != null)
            {
                float s = top.suspicion;
                markAngle = Mathf.LerpAngle(markAngle, AngleTo(cam, top), HUDDraw.Damp(12f, dt));
                float rad = markAngle * Mathf.Deg2Rad;
                markRt.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * (orbitRadius + 30f);
                Color c = ColorFor(s);
                float a = Mathf.Clamp01(Mathf.InverseLerp(showFrom, showFrom + 14f, s));
                mark.color = UIKit.A(c, a);
                mark.text = s >= 99.5f ? "!" : "?";
                float sc = s >= 99.5f ? 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 14f) : 1f;
                markRt.localScale = new Vector3(sc, sc, 1f);
            }
            else mark.color = UIKit.A(mark.color, Mathf.MoveTowards(mark.color.a, 0f, dt / 0.2f));
        }

        static void SetArc(Image img, float centerDeg, float lengthDeg, Color c)
        {
            img.fillAmount = Mathf.Clamp01(lengthDeg / 360f);
            // Filled는 위(12시)에서 시계 방향으로 찬다 → 호의 가운데가 centerDeg에 오게 돌린다 (UI는 반시계가 +)
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, -(centerDeg - lengthDeg * 0.5f));
            img.color = c;
        }

        /// <summary>화면 위쪽 = 0°, 시계 방향 +. 카메라 기준 감시자 눈 방향.</summary>
        static float AngleTo(Camera cam, Watcher w)
        {
            Vector3 local = cam.transform.InverseTransformPoint(w.EyePosition);
            return Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        }
    }
}
