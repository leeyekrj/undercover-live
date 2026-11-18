using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UndercoverLive
{
    /// <summary>
    /// 기이현상 공용 도구: 머티리얼 복제(웹 빌드에서 셰이더 변형이 빠지지 않게 씬에 있는 머티리얼을 복제),
    /// 코드로 만든 메시(원판·고리·빛줄기), 콜라이더 없는 렌더러, 폰 카메라 시야 검사.
    /// </summary>
    public static class EntityKit
    {
        // ---------- 레이어 ----------
        static int eyeOnly = -1, phoneOnly = -1;
        public static int EyeOnly { get { if (eyeOnly < 0) { eyeOnly = LayerMask.NameToLayer("EyeOnly"); if (eyeOnly < 0) eyeOnly = 6; } return eyeOnly; } }
        public static int PhoneOnly { get { if (phoneOnly < 0) { phoneOnly = LayerMask.NameToLayer("PhoneOnly"); if (phoneOnly < 0) phoneOnly = 7; } return phoneOnly; } }

        /// <summary>이번 판에 만든 렌더러 수 (성능 확인용).</summary>
        public static int RendererCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            scanned = false; baseOpaque = baseGlow = baseCutout = baseClear = null;
            meshes.Clear(); rings.Clear(); disc = null; strips = null;
            RendererCount = 0; planeFrame = -1;
        }

        /// <summary>씬을 다시 불러올 때 수를 다시 센다.</summary>
        public static void ResetCount() { RendererCount = 0; }

        // ---------- 머티리얼 ----------
        // 기준: Wall(불투명) · LedRed/MiracleGlow(발광 _EMISSION) · Emblem(알파 클립) · 머리카락(반투명)
        static Material baseOpaque, baseGlow, baseCutout, baseClear;
        static bool scanned;

        /// <summary>사용한 기준 머티리얼 이름 (보고용).</summary>
        public static string BaseNames
        {
            get
            {
                Scan();
                return $"불투명={N(baseOpaque)} 발광={N(baseGlow)} 알파클립={N(baseCutout)} 반투명={N(baseClear)}";
            }
        }
        static string N(Material m) => m != null ? m.name : "없음(대체)";

        static void Scan()
        {
            if (scanned) return;
            scanned = true;
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (m == null || m.shader == null || !m.shader.name.Contains("Universal Render Pipeline/Lit")) continue;
                switch (m.name)
                {
                    case "Wall": baseOpaque = m; break;
                    case "Blackout": if (baseOpaque == null) baseOpaque = m; break;
                    case "LedRed": baseGlow = m; break;
                    case "MiracleGlow": if (baseGlow == null) baseGlow = m; break;
                    case "Emblem": baseCutout = m; break;
                }
                if (baseClear == null && m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")) baseClear = m;
            }
        }

        static Material Fallback(bool emission)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh != null ? sh : Shader.Find("Standard"));
            if (emission) m.EnableKeyword("_EMISSION");
            return m;
        }

        /// <summary>불투명 (Wall 복제).</summary>
        public static Material Opaque(Color c, Texture baseMap = null, float smooth = 0.15f)
        {
            Scan();
            var m = baseOpaque != null ? new Material(baseOpaque) : Fallback(false);
            m.name = "Entity_Opaque";
            m.SetColor("_BaseColor", c);
            m.SetTexture("_BaseMap", baseMap);
            m.SetFloat("_Smoothness", smooth);
            return m;
        }

        /// <summary>발광 불투명 (LedRed 복제). 발광맵을 주면 텍스처 색으로 빛난다.</summary>
        public static Material Glow(Color baseColor, Color emission, Texture emissionMap = null, Texture baseMap = null, bool doubleSided = false)
        {
            Scan();
            var m = baseGlow != null ? new Material(baseGlow) : Fallback(true);
            m.name = "Entity_Glow";
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_BaseColor", baseColor);
            m.SetTexture("_BaseMap", baseMap);
            m.SetTexture("_EmissionMap", emissionMap);
            m.SetColor("_EmissionColor", emission);
            m.SetFloat("_Smoothness", 0.35f);
            if (doubleSided) m.SetFloat("_Cull", 0f);
            return m;
        }

        /// <summary>반투명 (씬의 반투명 머리카락 머티리얼 복제). 없으면 어두운 불투명으로 대신.</summary>
        public static Material Clear(Color c)
        {
            Scan();
            if (baseClear == null) return Opaque(new Color(c.r * c.a, c.g * c.a, c.b * c.a), null, 0.1f);
            var m = new Material(baseClear) { name = "Entity_Clear" };
            m.SetTexture("_BaseMap", null);
            m.SetTexture("_BumpMap", null);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Cutoff", 0.01f);
            m.SetFloat("_Smoothness", 0.25f);
            m.SetFloat("_Cull", 2f);
            return m;
        }

        public static void SetEmission(Material m, Color e) { if (m != null) m.SetColor("_EmissionColor", e); }

        // ---------- 메시 ----------
        static readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();
        static readonly Dictionary<int, Mesh> rings = new Dictionary<int, Mesh>();
        static Mesh disc, strips;

        public static Mesh Prim(PrimitiveType t)
        {
            if (meshes.TryGetValue(t, out var m) && m != null) return m;
            var go = GameObject.CreatePrimitive(t);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            go.SetActive(false);
            Object.Destroy(go);
            meshes[t] = m;
            return m;
        }

        /// <summary>지름 1 원판. 앞면이 -Z(Quad와 같음). UV는 원 안에 꽉 차게.</summary>
        public static Mesh Disc()
        {
            if (disc != null) return disc;
            const int S = 40;
            var v = new Vector3[S + 1]; var uv = new Vector2[S + 1]; var n = new Vector3[S + 1];
            var tri = new int[S * 3];
            v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f); n[0] = Vector3.back;
            for (int i = 0; i < S; i++)
            {
                float a = i / (float)S * Mathf.PI * 2f;
                v[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.5f;
                uv[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
                n[i + 1] = Vector3.back;
                int j = (i + 1) % S;
                tri[i * 3] = 0; tri[i * 3 + 1] = j + 1; tri[i * 3 + 2] = i + 1;   // -Z에서 봐서 시계 방향
            }
            disc = new Mesh { name = "EntityDisc", vertices = v, uv = uv, normals = n, triangles = tri };
            disc.RecalculateBounds();
            return disc;
        }

        /// <summary>바깥 지름 1 고리. inner = 안쪽 반지름 비율(0~1).</summary>
        public static Mesh Ring(float inner)
        {
            int key = Mathf.RoundToInt(inner * 1000);
            if (rings.TryGetValue(key, out var m) && m != null) return m;
            const int S = 64;
            var v = new Vector3[S * 2]; var uv = new Vector2[S * 2]; var n = new Vector3[S * 2];
            var tri = new int[S * 6];
            for (int i = 0; i < S; i++)
            {
                float a = i / (float)S * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v[i * 2] = d * 0.5f; v[i * 2 + 1] = d * 0.5f * inner;
                uv[i * 2] = new Vector2(i / (float)S, 1f); uv[i * 2 + 1] = new Vector2(i / (float)S, 0f);
                n[i * 2] = n[i * 2 + 1] = Vector3.back;
                int j = (i + 1) % S;
                int o0 = i * 2, i0 = i * 2 + 1, o1 = j * 2, i1 = j * 2 + 1;
                tri[i * 6] = i0; tri[i * 6 + 1] = o1; tri[i * 6 + 2] = o0;
                tri[i * 6 + 3] = i0; tri[i * 6 + 4] = i1; tri[i * 6 + 5] = o1;
            }
            m = new Mesh { name = "EntityRing", vertices = v, uv = uv, normals = n, triangles = tri };
            m.RecalculateBounds();
            rings[key] = m;
            return m;
        }

        /// <summary>원기둥 둘레에 선 세로 빛줄기 묶음(지름 1, 높이 2 — Unity 원기둥과 같은 크기). 틈으로 뒤가 보인다.</summary>
        public static Mesh Strips()
        {
            if (strips != null) return strips;
            const int S = 18;
            var rnd = new System.Random(5);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var tri = new List<int>();
            for (int i = 0; i < S; i++)
            {
                float a = (i + (float)rnd.NextDouble() * 0.7f) / S * Mathf.PI * 2f;
                float w = 0.03f + (float)rnd.NextDouble() * 0.09f;
                float r = 0.36f + (float)rnd.NextDouble() * 0.14f;
                float top = 1f - (float)rnd.NextDouble() * 0.15f, bot = -1f + (float)rnd.NextDouble() * 0.25f;
                var p0 = new Vector3(Mathf.Cos(a - w), 0, Mathf.Sin(a - w)) * r;
                var p1 = new Vector3(Mathf.Cos(a + w), 0, Mathf.Sin(a + w)) * r;
                var nn = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                int b = v.Count;
                v.Add(p0 + Vector3.up * bot); v.Add(p1 + Vector3.up * bot); v.Add(p0 + Vector3.up * top); v.Add(p1 + Vector3.up * top);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1));
                for (int k = 0; k < 4; k++) n.Add(nn);
                tri.Add(b); tri.Add(b + 2); tri.Add(b + 1);
                tri.Add(b + 1); tri.Add(b + 2); tri.Add(b + 3);
            }
            strips = new Mesh { name = "EntityStrips" };
            strips.SetVertices(v); strips.SetUVs(0, uv); strips.SetNormals(n); strips.SetTriangles(tri, 0);
            strips.RecalculateBounds();
            return strips;
        }

        // ---------- 오브젝트 ----------
        /// <summary>콜라이더 없는 렌더러 하나. 그림자·반사 프로브 끔.</summary>
        public static GameObject Obj(string name, Transform parent, Mesh mesh, Material mat, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            RendererCount++;
            return go;
        }

        public static GameObject Group(string name, Transform parent, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>안구: 공(흰자/살) + 앞에 붙은 홍채 원판. forward가 보는 방향. 지름 size(m).</summary>
        public static Transform EyeBall(string name, Transform parent, float size, Material ball, Material iris, int layer, float irisScale = 0.72f)
        {
            var root = Group(name, parent, layer).transform;
            root.localScale = Vector3.one * size;
            Obj("Ball", root, Prim(PrimitiveType.Sphere), ball, layer);
            var ir = Obj("Iris", root, Disc(), iris, layer).transform;
            ir.localPosition = new Vector3(0, 0, 0.505f);   // 공 표면(0.5)보다 살짝 앞
            ir.localRotation = Quaternion.Euler(0, 180, 0);   // 원판 앞면(-Z)이 바깥(+Z)을 보게
            ir.localScale = Vector3.one * irisScale;
            return root;
        }

        /// <summary>원판 하나짜리 작은 눈(흰자 포함 텍스처). forward가 보는 방향.</summary>
        public static Transform FlatEye(string name, Transform parent, float size, Material mat, int layer)
        {
            var root = Group(name, parent, layer).transform;
            var d = Obj("Disc", root, Disc(), mat, layer).transform;
            d.localRotation = Quaternion.Euler(0, 180, 0);
            root.localScale = Vector3.one * size;
            return root;
        }

        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }

        // ---------- 폰 카메라 ----------
        public static Camera PhoneCam
        {
            get
            {
                var pc = PhoneController.Instance;
                return pc != null ? pc.PhoneCamera : null;
            }
        }

        /// <summary>폰을 들고(정조준) 화면이 켜져 있는가.</summary>
        public static bool PhoneAimed
        {
            get
            {
                var pc = PhoneController.Instance;
                return pc != null && pc.PhoneCamera != null && pc.State == PhoneState.Aimed && pc.IsScreenVisible;
            }
        }

        /// <summary>현상이 쳐다볼 곳: 폰을 꺼냈으면 렌즈, 아니면 플레이어 눈.</summary>
        public static Vector3 WatchPoint()
        {
            var pc = PhoneController.Instance;
            if (pc == null) { var m = Camera.main; return m != null ? m.transform.position : Vector3.zero; }
            if (pc.PhoneCamera != null && pc.State != PhoneState.Pocket) return pc.PhoneCamera.transform.position;
            return pc.Head != null ? pc.Head.position : pc.transform.position + Vector3.up * 1.2f;
        }

        public static Vector3 PlayerHead()
        {
            var pc = PhoneController.Instance;
            if (pc != null && pc.Head != null) return pc.Head.position;
            var m = Camera.main;
            return m != null ? m.transform.position : Vector3.up * 1.2f;
        }

        /// <summary>폰 화면 가운데에서 얼마나 떨어졌나(뷰포트 0~0.7). 뒤쪽이거나 폰을 안 들었으면 9.</summary>
        public static float FromCenter(Vector3 world)
        {
            var cam = PhoneCam;
            if (cam == null || !PhoneAimed) return 9f;
            var vp = cam.WorldToViewportPoint(world);
            if (vp.z <= 0.05f) return 9f;
            return new Vector2(vp.x - 0.5f, vp.y - 0.5f).magnitude;
        }

        static readonly Plane[] planes = new Plane[6];
        static int planeFrame = -1;
        static bool planesOk;

        /// <summary>폰 카메라가 켜져 있고 이 구가 화면에 들어오나 (촉수 갱신 여부).</summary>
        public static bool PhoneSees(Vector3 center, float radius, float maxDistance = 25f)
        {
            var cam = PhoneCam;
            if (cam == null || !cam.enabled || !cam.gameObject.activeInHierarchy) return false;
            if (planeFrame != Time.frameCount)
            {
                planeFrame = Time.frameCount;
                GeometryUtility.CalculateFrustumPlanes(cam, planes);
                planesOk = true;
            }
            if (!planesOk) return false;
            if ((center - cam.transform.position).sqrMagnitude > (maxDistance + radius) * (maxDistance + radius)) return false;
            return GeometryUtility.TestPlanesAABB(planes, new Bounds(center, Vector3.one * radius * 2f));
        }
    }

    /// <summary>폰 화면 가운데 근처에 대상이 머문 시간(초). 0.3초 넘게 벗어나면 0으로.</summary>
    public class Aim
    {
        public float Held { get; private set; }
        float lastSeen = -99f;

        public float Track(Vector3 world, float radius, float minZoom = 0f)
        {
            bool ok = EntityKit.FromCenter(world) < radius;
            if (ok && minZoom > 0f)
            {
                var pc = PhoneController.Instance;
                ok = pc != null && pc.Zoom01 >= minZoom;
            }
            return Feed(ok);
        }

        /// <summary>조건을 직접 넣는다(여러 점 중 하나라도 화면 가운데에 있을 때 등).</summary>
        public float Feed(bool ok)
        {
            ok &= GameEvents.IsPlaying;
            if (ok) { Held += Time.deltaTime; lastSeen = Time.time; }
            else if (Time.time - lastSeen > 0.3f) Held = 0f;
            return Held;
        }

        public void Reset() { Held = 0f; }
    }

    /// <summary>
    /// 한 카메라에만 비추는 조명. URP가 카메라마다 그리기 직전에 켜고 끈다.
    /// (폰에만 보이는 붉은 빛, 맨눈에만 보이는 금빛 기적 조명)
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class CameraOnlyLight : MonoBehaviour
    {
        public enum Who { Phone, Eye }
        public Who who = Who.Phone;
        [Tooltip("꺼 두면 어느 카메라에도 안 비춤")]
        public bool wanted = true;
        Light lt;

        static readonly List<CameraOnlyLight> all = new List<CameraOnlyLight>();
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            all.Clear();
            if (hooked) RenderPipelineManager.beginCameraRendering -= OnBegin;
            hooked = false;
        }

        void Awake() { lt = GetComponent<Light>(); }

        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            if (!hooked) { RenderPipelineManager.beginCameraRendering += OnBegin; hooked = true; }
        }

        void OnDisable()
        {
            all.Remove(this);
            if (all.Count == 0 && hooked) { RenderPipelineManager.beginCameraRendering -= OnBegin; hooked = false; }
        }

        static void OnBegin(ScriptableRenderContext ctx, Camera cam)
        {
            var pc = PhoneController.Instance;
            bool isPhone = pc != null && cam == pc.PhoneCamera;
            bool isEye = pc != null ? cam == pc.EyeCamera : cam == Camera.main;
            for (int i = 0; i < all.Count; i++)
            {
                var l = all[i];
                if (l == null || l.lt == null) continue;
                bool on = l.wanted && (l.who == Who.Phone ? isPhone : isEye);
                if (l.lt.enabled != on) l.lt.enabled = on;
            }
        }
    }
}
