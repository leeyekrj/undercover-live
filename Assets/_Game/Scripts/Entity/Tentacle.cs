using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 캡슐 사슬 촉수. 뿌리(base)에서 끝(tip)까지 마디를 잇고, 마디마다 위상이 다른 사인파로 꿈틀댄다.
    /// 끝으로 갈수록 가늘다. 위치는 매 프레임 계산해서 마디를 옮기므로 계층이 깊지 않다.
    /// 폰 카메라에 보일 때만 갱신한다(웹 성능).
    /// </summary>
    [DefaultExecutionOrder(300)]
    public class Tentacle : MonoBehaviour
    {
        [Header("양 끝 (월드)")]
        public Vector3 basePos;
        public Vector3 tipPos;
        public Transform baseFollow, tipFollow;
        public Vector3 baseOffset, tipOffset;

        [Header("모양")]
        public float rootRadius = 0.12f, tipRadius = 0.03f;
        [Tooltip("가운데가 처지는 정도(m)")] public float sag = 0.3f;
        public Vector3 sagDir = Vector3.down;
        [Tooltip("1 = 둥근 단면, 0.3 = 납작(바닥·벽에 붙은 그림자)")] public float flatten = 1f;
        public Vector3 flatUp = Vector3.up;

        [Header("꿈틀")]
        public float waveAmp = 0.15f, waveSpeed = 1.6f, waveK = 7f;
        [Tooltip("true면 끝으로 갈수록 크게 흔들림(끝이 자유). false면 양 끝은 고정, 가운데만")]
        public bool freeTip = true;
        public float tension = 0f;   // 1이면 팽팽(흔들림·처짐 없음)
        public bool alwaysUpdate;

        Transform[] segs;
        Vector3[] pts;
        float phase;
        bool built;

        public int Count => segs != null ? segs.Length : 0;
        public Renderer[] Renderers { get; private set; }

        public static Tentacle Create(string name, Transform parent, int segments, Material mat, int layer)
        {
            var go = EntityKit.Group(name, parent, layer);
            var t = go.AddComponent<Tentacle>();
            t.segs = new Transform[segments];
            t.pts = new Vector3[segments + 1];
            t.Renderers = new Renderer[segments];
            var mesh = EntityKit.Prim(PrimitiveType.Capsule);
            for (int i = 0; i < segments; i++)
            {
                var s = EntityKit.Obj("Seg" + i, go.transform, mesh, mat, layer);
                t.segs[i] = s.transform;
                t.Renderers[i] = s.GetComponent<Renderer>();
            }
            t.phase = Random.value * 10f;
            return t;
        }

        public void SetMaterial(Material m)
        {
            if (Renderers == null) return;
            foreach (var r in Renderers) if (r != null) r.sharedMaterial = m;
        }

        /// <summary>마디 지점 i (0 = 뿌리, Count = 끝). 마지막 갱신 기준.</summary>
        public Vector3 Point(int i) { return pts != null && pts.Length > 0 ? pts[Mathf.Clamp(i, 0, pts.Length - 1)] : transform.position; }

        /// <summary>0~1 위치의 점.</summary>
        public Vector3 PointAt(float s)
        {
            if (pts == null || pts.Length < 2) return transform.position;
            float f = Mathf.Clamp01(s) * (pts.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), pts.Length - 2);
            return Vector3.Lerp(pts[i], pts[i + 1], f - i);
        }

        public float RadiusAt(float s) { return Mathf.Lerp(rootRadius, tipRadius, s); }

        void LateUpdate()
        {
            if (segs == null) return;
            Vector3 b = baseFollow != null ? baseFollow.position + baseOffset : basePos;
            Vector3 e = tipFollow != null ? tipFollow.position + tipOffset : tipPos;
            if (built && !alwaysUpdate)
            {
                Vector3 c = (b + e) * 0.5f;
                float r = (e - b).magnitude * 0.5f + sag + waveAmp + 0.5f;
                if (!EntityKit.PhoneSees(c, r)) return;
            }
            Rebuild(b, e);
            built = true;
        }

        public void ForceRebuild()
        {
            Vector3 b = baseFollow != null ? baseFollow.position + baseOffset : basePos;
            Vector3 e = tipFollow != null ? tipFollow.position + tipOffset : tipPos;
            Rebuild(b, e);
            built = true;
        }

        void Rebuild(Vector3 b, Vector3 e)
        {
            int n = segs.Length;
            Vector3 d = e - b;
            float len = d.magnitude;
            Vector3 dir = len > 1e-4f ? d / len : Vector3.down;
            Vector3 p1 = Vector3.Cross(dir, Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up).normalized;
            Vector3 p2 = Vector3.Cross(dir, p1).normalized;
            float t = Time.time * waveSpeed + phase;
            float loose = 1f - Mathf.Clamp01(tension);
            for (int i = 0; i <= n; i++)
            {
                float s = i / (float)n;
                float env = freeTip ? Mathf.SmoothStep(0f, 1f, s) : Mathf.Sin(s * Mathf.PI);
                Vector3 p = b + d * s;
                p += sagDir * (sag * 4f * s * (1f - s) * loose);
                float w1 = Mathf.Sin(t - s * waveK), w2 = Mathf.Cos(t * 0.73f - s * waveK * 1.3f + 1.7f);
                p += (p1 * w1 + p2 * w2 * 0.6f) * (waveAmp * env * loose);
                pts[i] = p;
            }
            for (int i = 0; i < n; i++)
            {
                Vector3 a = pts[i], c = pts[i + 1];
                Vector3 sd = c - a;
                float sl = sd.magnitude;
                if (sl < 1e-4f) { sd = dir; sl = 1e-4f; }
                float r = Mathf.Lerp(rootRadius, tipRadius, (i + 0.5f) / n);
                var tr = segs[i];
                tr.position = (a + c) * 0.5f;
                Vector3 up = Mathf.Abs(Vector3.Dot(sd / sl, flatUp)) > 0.97f ? p1 : flatUp;
                tr.rotation = Quaternion.LookRotation(sd / sl, up) * Quaternion.Euler(90f, 0f, 0f);
                // 캡슐: 반지름 0.5·높이 2 → 지름 2r, 길이 sl + 겹침
                float h = (sl + r * 1.2f) * 0.5f;
                tr.localScale = DivideByParent(new Vector3(r * 2f, Mathf.Max(h, r), r * 2f * flatten));
            }
        }

        Vector3 DivideByParent(Vector3 s)
        {
            var ls = transform.lossyScale;
            return new Vector3(s.x / Mathf.Max(1e-4f, ls.x), s.y / Mathf.Max(1e-4f, ls.y), s.z / Mathf.Max(1e-4f, ls.z));
        }
    }
}
