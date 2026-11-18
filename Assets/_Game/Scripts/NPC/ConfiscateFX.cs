using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 폰을 빼앗기는 화면 연출 (자체 캔버스 sortingOrder 140). 만화 같은 손 그림 대신 카메라 연출:
    /// - 촬영 중: 카메라가 거칠게 흔들리며 비틀려 나가고(폰이 손에서 돌아가는 느낌), 관리자 쪽에서 어둡고 따뜻한
    ///   손바닥 그림자(가장자리 부드러운 큰 판, #0c0406)가 렌즈를 덮고, 화면 조각이 가로로 어긋나며(초점 상자·채팅 포함)
    ///   가로 줄 노이즈·색 어긋남 줄이 번쩍인 뒤 신호가 끊긴다. 약 1.5초.
    /// - 몰래: 오른쪽 아래 폰 뒷면이 위로 낚아채이듯 사라지고 맨눈이 흔들린 뒤 짧은 신호 끊김.
    /// - 주머니: 맨눈 흔들림 + 손 그림자 + 짧은 신호 끊김.
    /// 연출 시간은 GameEvents.IsPlaying일 때만 흐른다(멈춤이면 멈췄다가 이어서). 끝(신호 끊김)에 onCut을 부르고
    /// 검은 화면을 잠깐 유지한 뒤 스스로 걷힌다(그 뒤 화면은 결과·발각 화면 담당).
    /// </summary>
    public class ConfiscateFX : MonoBehaviour
    {
        [Header("길이(초)")]
        public float aimedDuration = 1.5f;
        public float hiddenDuration = 1.3f;
        public float pocketDuration = 1.05f;
        [Tooltip("끝의 신호 끊김 구간 길이(초)")] public float cutLength = 0.3f;
        [Tooltip("신호가 끊긴 검은 화면 유지(초, 실제 시간)")] public float blackHold = 0.45f;
        public float fadeOut = 0.35f;

        [Header("흔들림 (도)")]
        [Tooltip("촬영 중: 폰이 비틀려 나가는 기울기")] public float twistRoll = 18f;
        public float twistPitch = 12f, twistYaw = 9f;
        [Tooltip("잔흔들림 세기")] public float jitterDeg = 5f;
        [Tooltip("잔흔들림 위치(m)")] public float jitterMove = 0.012f;
        [Tooltip("몰래·주머니: 맨눈이 붙잡혀 흔들리는 세기")] public float eyeKick = 6f;

        [Header("손바닥 그림자")]
        public Color palmColor = new Color(0.047f, 0.016f, 0.024f, 0.97f);   // #0c0406
        public Color palmWarm = new Color(0.24f, 0.05f, 0.04f, 0.55f);       // 가장자리 붉은빛

        [Header("신호 끊김")]
        public Color signalRed = new Color(1f, 0.302f, 0.353f, 1f);            // #ff4d5a

        public const int SortingOrder = 140;

        public bool Playing { get; private set; }

        Canvas canvas;
        CanvasGroup group;
        RectTransform root, cloneHolder;
        Image palm, warm, black;
        RectTransform signalBox;
        CanvasGroup signalGroup;
        Image signalDot;
        readonly List<RawImage> strips = new List<RawImage>();
        readonly List<float> stripUntil = new List<float>();
        readonly List<Image> lines = new List<Image>();
        readonly List<float> lineUntil = new List<float>();
        Texture2D shot;

        // 카메라 흔들림 (렌더 직전에 더하고 직후 되돌림 → 다른 스크립트의 카메라 값과 섞여 쌓이지 않음)
        bool shakeOn;
        Vector3 shakePhone, shakeEye, movePhone, moveEye;
        readonly Dictionary<Camera, (Vector3 pos, Quaternion rot)> saved = new Dictionary<Camera, (Vector3, Quaternion)>();

        static Sprite softSprite;

        void Awake()
        {
            BuildUI();
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            if (shot != null) Destroy(shot);
        }

        /// <summary>연출 시작. state = 빼앗길 때 폰 상태, grabber = 관리자(손) 위치. onCut = 신호가 끊긴 순간.</summary>
        public void Play(PhoneState state, Vector3 grabber, Action onCut)
        {
            if (Playing) { onCut?.Invoke(); return; }
            StartCoroutine(Run(state, grabber, onCut));
        }

        // ---------- 진행 ----------

        IEnumerator Run(PhoneState state, Vector3 grabber, Action onCut)
        {
            Playing = true;
            float side = SideOf(grabber);

            // 화면 한 장을 떠 둔다 (초점 상자·채팅을 포함한 화면 조각을 어긋나게 할 때 씀)
            yield return new WaitForEndOfFrame();
            try { shot = ScreenCapture.CaptureScreenshotAsTexture(); }
            catch (Exception) { shot = null; }
            foreach (var s in strips) s.texture = shot;

            canvas.enabled = true;
            group.alpha = 1f;
            black.color = new Color(0f, 0f, 0f, 0f);
            signalGroup.alpha = 0f;
            float W = root.rect.width, H = root.rect.height;

            float T = state == PhoneState.Aimed ? aimedDuration : state == PhoneState.Hidden ? hiddenDuration : pocketDuration;
            float cutU = Mathf.Clamp01(1f - cutLength / Mathf.Max(0.1f, T));
            RectTransform clone = null;
            Vector2 phonePos = Vector2.zero;
            if (state == PhoneState.Hidden)
            {
                clone = ClonePhoneBack(out phonePos);
                // 내 사본이 그 자리를 이어받는 순간 원래 폰 뒷면을 내린다
                var pc = PhoneController.Instance;
                if (pc != null) pc.SetState(PhoneState.Pocket);
            }

            float t = 0f, nextGlitch = 0.02f;
            float seed = UnityEngine.Random.value * 50f;
            shakeOn = true;
            while (t < T)
            {
                float dt = GameEvents.IsPlaying ? Time.deltaTime : 0f;
                t += dt;
                float u = Mathf.Clamp01(t / T);
                float cut = u <= cutU ? 0f : Mathf.Clamp01((u - cutU) / Mathf.Max(0.01f, 1f - cutU));

                UpdateShake(state, t, T, u, cut, side, seed);
                UpdatePalm(state, u, side, W, H, phonePos);
                if (clone != null) UpdateClone(clone, phonePos, u, t);

                // 화면 조각 어긋남·노이즈 줄
                bool glitchWindow = state == PhoneState.Aimed ? u > 0.06f : (u > cutU - 0.12f || (u > 0.12f && u < 0.26f));
                if (dt > 0f && glitchWindow && t >= nextGlitch)
                {
                    Burst(t, W, H, state == PhoneState.Aimed ? Mathf.Lerp(2, 4, u) : 2, cut > 0f);
                    float gap = state == PhoneState.Aimed ? Mathf.Lerp(0.15f, 0.05f, u) : 0.07f;
                    nextGlitch = t + gap * UnityEngine.Random.Range(0.7f, 1.3f);
                }
                ExpireGlitches(t);

                // 신호 끊김: 검게 → 노이즈 줄 → '신호 끊김'
                black.color = new Color(0f, 0f, 0f, Mathf.Clamp01(cut * 3f));
                signalGroup.alpha = Mathf.Clamp01((cut - 0.3f) * 4f);
                signalDot.enabled = (Time.unscaledTime % 0.6f) < 0.36f;
                yield return null;
            }

            // 끊김 확정
            shakeOn = false;
            black.color = Color.black;
            signalGroup.alpha = 1f;
            foreach (var s in strips) s.gameObject.SetActive(false);
            palm.enabled = warm.enabled = false;
            if (clone != null) Destroy(clone.gameObject);
            try { onCut?.Invoke(); } catch (Exception e) { Debug.LogException(e); }

            // 결과·발각 화면이 올라올 시간을 두고 걷힌다 (그 화면은 이 캔버스보다 위 180~190)
            float hold = blackHold;
            while (hold > 0f)
            {
                hold -= Time.unscaledDeltaTime;
                signalDot.enabled = (Time.unscaledTime % 0.6f) < 0.36f;
                yield return null;
            }
            float f = 0f;
            while (f < fadeOut)
            {
                f += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(f / fadeOut);
                yield return null;
            }
            canvas.enabled = false;
            foreach (var l in lines) l.gameObject.SetActive(false);
            if (shot != null) { Destroy(shot); shot = null; }
            Playing = false;
        }

        float SideOf(Vector3 grabber)
        {
            var pc = PhoneController.Instance;
            Transform head = pc != null ? pc.Head : (Camera.main != null ? Camera.main.transform : null);
            if (head == null) return 1f;
            return head.InverseTransformPoint(grabber).x >= 0f ? 1f : -1f;
        }

        static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float Noise(float t, float seed) => (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f;

        void UpdateShake(PhoneState state, float t, float T, float u, float cut, float side, float seed)
        {
            float fade = 1f - cut;
            if (state == PhoneState.Aimed)
            {
                // 붙잡힌 순간의 충격 + 점점 비틀려 나감 + 잔흔들림
                float e = Ease((u - 0.04f) / 0.78f);
                float kick = side * 7f * Mathf.Exp(-t * 9f) * Mathf.Cos(t * 40f);
                float amp = jitterDeg * (0.45f + 0.55f * u) * fade;
                float roll = -side * twistRoll * e + kick + Noise(t * 19f, seed) * amp;
                float pitch = twistPitch * e + Noise(t * 17f, seed + 7f) * amp * 0.7f;
                float yaw = side * twistYaw * e + Noise(t * 15f, seed + 13f) * amp * 0.6f;
                shakePhone = new Vector3(pitch, yaw, roll) * fade;
                movePhone = new Vector3(Noise(t * 21f, seed + 3f), Noise(t * 23f, seed + 5f) - 0.6f * e, 0f) * jitterMove * 2f * fade;
                shakeEye = shakePhone * 0.5f;
                moveEye = movePhone * 0.5f;
            }
            else
            {
                // 맨눈: 붙잡히는 순간 덜컥 + 잦아드는 흔들림
                float tg = T * (state == PhoneState.Hidden ? 0.14f : 0.1f);
                float k = t < tg ? Mathf.Clamp01(t / tg) * 0.3f : 1f;
                float s = Mathf.Max(0f, t - tg);
                float decay = Mathf.Exp(-s * 4f);
                float roll = side * eyeKick * decay * Mathf.Cos(s * 30f) * k + Noise(t * 18f, seed) * 2.2f * decay;
                float pitch = -eyeKick * 0.6f * decay * Mathf.Sin(s * 26f) * k + Noise(t * 16f, seed + 9f) * 1.6f * decay;
                float yaw = side * 2.5f * decay * k;
                shakeEye = new Vector3(pitch, yaw, roll) * fade;
                moveEye = new Vector3(Noise(t * 20f, seed + 2f), Noise(t * 22f, seed + 4f), 0f) * jitterMove * decay * fade;
                shakePhone = shakeEye;
                movePhone = moveEye;
            }
        }

        void UpdatePalm(PhoneState state, float u, float side, float W, float H, Vector2 phonePos)
        {
            var prt = palm.rectTransform;
            var wrt = warm.rectTransform;
            Vector2 pos, size;
            float rot;
            if (state == PhoneState.Aimed)
            {
                // 관리자 쪽 화면 밖에서 렌즈를 덮어 온다
                float p = Ease((u - 0.06f) / 0.62f);
                size = new Vector2(W * 1.3f, H * 1.6f);
                float startX = side * (W * 0.5f + size.x * 0.45f);
                float endX = side * W * 0.04f;
                pos = new Vector2(Mathf.Lerp(startX, endX, p), Mathf.Lerp(-H * 0.12f, 0f, p) + Mathf.Sin(u * 21f) * 14f * (1f - p));
                rot = side * Mathf.Lerp(14f, 4f, p);
                palm.enabled = warm.enabled = u > 0.04f;
            }
            else if (state == PhoneState.Hidden)
            {
                // 오른쪽 아래 폰 뒷면을 덮쳐 쥐고 같이 위로 빠져나간다
                size = new Vector2(780f, 860f);
                Vector2 grip = phonePos + new Vector2(150f, -170f);
                Vector2 start = grip + new Vector2(520f, -520f);
                float reach = Ease(u / 0.14f);
                float lift = u < 0.14f ? 0f : EaseIn((u - 0.14f) / 0.32f);
                pos = Vector2.Lerp(start, grip, reach) + new Vector2(0f, lift * (H + 900f));
                rot = Mathf.Lerp(28f, 12f, reach);
                palm.enabled = warm.enabled = u < 0.5f;
            }
            else
            {
                // 주머니: 관리자가 몸으로 시야를 반쯤 가린다
                float p = Ease((u - 0.05f) / 0.5f);
                size = new Vector2(W * 1.05f, H * 1.5f);
                float startX = side * (W * 0.5f + size.x * 0.5f);
                float endX = side * W * 0.36f;
                pos = new Vector2(Mathf.Lerp(startX, endX, p), 0f);
                rot = side * 8f;
                palm.enabled = warm.enabled = u > 0.04f;
            }
            prt.sizeDelta = size;
            prt.anchoredPosition = pos;
            prt.localRotation = Quaternion.Euler(0f, 0f, rot);
            wrt.sizeDelta = size * 1.14f;
            wrt.anchoredPosition = pos;
            wrt.localRotation = prt.localRotation;
        }

        static float EaseIn(float x) { x = Mathf.Clamp01(x); return x * x * x; }

        void UpdateClone(RectTransform clone, Vector2 phonePos, float u, float t)
        {
            if (u < 0.14f)
            {
                // 붙잡혀 떨림
                float j = 6f * Ease(u / 0.14f);
                clone.anchoredPosition = phonePos + new Vector2(Mathf.Sin(t * 70f) * j, Mathf.Cos(t * 63f) * j * 0.6f);
            }
            else
            {
                float lift = EaseIn((u - 0.14f) / 0.32f);
                clone.anchoredPosition = phonePos + new Vector2(-40f * lift, lift * 1300f);
                clone.localRotation = Quaternion.Euler(0f, 0f, cloneBaseRot + 16f * lift);
            }
        }

        float cloneBaseRot;

        /// <summary>몰래 모드의 폰 뒷면(PhoneController.HiddenPhone)을 내 캔버스에 같은 자리·크기로 복제. 없으면 기본 폰 뒷면을 그린다.</summary>
        RectTransform ClonePhoneBack(out Vector2 pos)
        {
            RectTransform src = FindHiddenPhone();
            RectTransform rt;
            if (src != null && src.gameObject.activeInHierarchy)
            {
                // 비활성 상자 안에서 복제 → 폰 담당 스크립트가 깨어나기 전에 걷어 낸다(UI 그리기 부품만 남김)
                var holder = new GameObject("CloneHolder", typeof(RectTransform));
                holder.SetActive(false);
                holder.transform.SetParent(cloneHolder, false);
                var go = Instantiate(src.gameObject, holder.transform);
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(mb is UIBehaviour)) DestroyImmediate(mb);
                rt = (RectTransform)go.transform;
                rt.SetParent(cloneHolder, false);
                Destroy(holder);
                go.SetActive(true);
                Vector2 size = src.rect.size;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = src.pivot;
                rt.sizeDelta = size;
                Vector3 ls = src.lossyScale, ps = cloneHolder.lossyScale;
                rt.localScale = new Vector3(ls.x / Mathf.Max(1e-5f, ps.x), ls.y / Mathf.Max(1e-5f, ps.y), 1f);
                rt.position = src.position;
                rt.rotation = src.rotation;
            }
            else
            {
                rt = UIKit.Rect("PhoneBack", cloneHolder);
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.sizeDelta = new Vector2(200f, 380f);
                rt.anchoredPosition = new Vector2(-170f, -150f);
                rt.localRotation = Quaternion.Euler(0f, 0f, 9f);
                UIKit.Stretch(UIKit.Box("Back", rt, new Color(0.11f, 0.11f, 0.12f), 28).rectTransform);
                UIKit.Place(UIKit.Box("Bump", rt, new Color(0.18f, 0.18f, 0.2f), 18).rectTransform, 22, 22, 84, 84);
                // 가운데 기준 좌표로 바꿔 둔다
                Vector3 world = rt.position;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.position = world;
            }
            // 이후 움직임은 가운데 기준 anchoredPosition으로
            var p = rt.position;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.position = p;
            pos = rt.anchoredPosition;
            cloneBaseRot = rt.localEulerAngles.z;
            return rt;
        }

        /// <summary>
        /// 폰 담당의 몰래 모드 폰 뒷면 UI. 폰 담당 코드 구조가 바뀌어도 컴파일이 깨지지 않게 이름으로 찾는다
        /// (PhoneController의 'HiddenPhone' 필드·속성 → 없으면 씬의 'HiddenPhone' 오브젝트).
        /// </summary>
        static RectTransform FindHiddenPhone()
        {
            var pc = PhoneController.Instance;
            if (pc != null)
            {
                var type = pc.GetType();
                var f = type.GetField("HiddenPhone");
                if (f != null && f.GetValue(pc) is RectTransform rf) return rf;
                var p = type.GetProperty("HiddenPhone");
                if (p != null && p.GetValue(pc) is RectTransform rp) return rp;
            }
            var go = GameObject.Find("HiddenPhone");
            return go != null ? go.transform as RectTransform : null;
        }

        // ---------- 어긋난 화면 조각·노이즈 줄 ----------

        void Burst(float t, float W, float H, float count, bool overBlack)
        {
            int n = Mathf.RoundToInt(count);
            if (!overBlack)
            {
                for (int i = 0; i < n; i++)
                {
                    float y = UnityEngine.Random.Range(H * 0.1f, H * 0.9f);
                    float h = UnityEngine.Random.Range(10f, 46f);
                    float dx = UnityEngine.Random.Range(40f, 170f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                    float life = UnityEngine.Random.Range(0.04f, 0.11f);
                    // 색 어긋남: 같은 띠를 붉게·푸르게 두 겹으로 반대쪽에
                    ShowStrip(y, h, dx, new Color(1f, 0.32f, 0.36f, 0.9f), t + life, H);
                    ShowStrip(y + 3f, h, -dx * 0.55f, new Color(0.35f, 0.92f, 1f, 0.7f), t + life * 0.8f, H);
                }
            }
            int m = overBlack ? n + 2 : Mathf.Max(1, n - 1);
            for (int i = 0; i < m; i++)
            {
                float y = UnityEngine.Random.Range(H * 0.08f, H * 0.92f);
                float h = UnityEngine.Random.Range(2f, 5f);
                var c = UnityEngine.Random.value < 0.7f ? new Color(1f, 1f, 1f, UnityEngine.Random.Range(0.45f, 0.9f))
                                                        : new Color(signalRed.r, signalRed.g, signalRed.b, 0.8f);
                ShowLine(y, h, c, t + UnityEngine.Random.Range(0.03f, 0.08f));
            }
        }

        void ShowStrip(float y, float h, float dx, Color c, float until, float H)
        {
            int i = stripUntil.FindIndex(v => v <= 0f);
            if (i < 0) return;
            var s = strips[i];
            var rt = s.rectTransform;
            rt.offsetMin = new Vector2(dx, y);
            rt.offsetMax = new Vector2(dx, y + h);
            s.uvRect = new Rect(0f, y / H, 1f, h / H);
            s.color = shot != null ? c : new Color(c.r, c.g, c.b, c.a * 0.5f);
            s.gameObject.SetActive(true);
            stripUntil[i] = until;
        }

        void ShowLine(float y, float h, Color c, float until)
        {
            int i = lineUntil.FindIndex(v => v <= 0f);
            if (i < 0) return;
            var l = lines[i];
            var rt = l.rectTransform;
            rt.offsetMin = new Vector2(0f, y);
            rt.offsetMax = new Vector2(0f, y + h);
            l.color = c;
            l.gameObject.SetActive(true);
            lineUntil[i] = until;
        }

        void ExpireGlitches(float t)
        {
            for (int i = 0; i < strips.Count; i++)
                if (stripUntil[i] > 0f && t >= stripUntil[i]) { strips[i].gameObject.SetActive(false); stripUntil[i] = 0f; }
            for (int i = 0; i < lines.Count; i++)
                if (lineUntil[i] > 0f && t >= lineUntil[i]) { lines[i].gameObject.SetActive(false); lineUntil[i] = 0f; }
        }

        // ---------- 카메라 흔들림 ----------

        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (!shakeOn || cam == null || cam.cameraType != CameraType.Game) return;
            var t = cam.transform;
            saved[cam] = (t.localPosition, t.localRotation);
            var pc = PhoneController.Instance;
            bool phone = pc != null && cam == pc.PhoneCamera;
            Vector3 r = phone ? shakePhone : shakeEye;
            Vector3 m = phone ? movePhone : moveEye;
            Quaternion q = t.localRotation * Quaternion.Euler(r.x, r.y, r.z);
            t.localRotation = q;
            t.localPosition += q * m;
        }

        void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == null || !saved.TryGetValue(cam, out var s)) return;
            cam.transform.localPosition = s.pos;
            cam.transform.localRotation = s.rot;
            saved.Remove(cam);
        }

        // ---------- 화면 부품 ----------

        void BuildUI()
        {
            canvas = UIKit.Canvas("ConfiscateCanvas", SortingOrder, transform);
            var gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr != null) gr.enabled = false;
            root = UIKit.Rect("Root", canvas.transform);
            UIKit.Stretch(root);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;

            cloneHolder = UIKit.Rect("Phone", root);
            UIKit.Stretch(cloneHolder);

            var soft = SoftSprite();
            warm = UIKit.Image("PalmWarm", root, palmWarm, soft);
            palm = UIKit.Image("Palm", root, palmColor, soft);
            foreach (var img in new[] { warm, palm })
            {
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                img.enabled = false;
            }

            for (int i = 0; i < 10; i++)
            {
                var go = new GameObject("Strip", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(root, false);
                var s = go.GetComponent<RawImage>();
                s.raycastTarget = false;
                var rt = s.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f);
                go.SetActive(false);
                strips.Add(s);
                stripUntil.Add(0f);
            }

            black = UIKit.Image("Black", root, new Color(0f, 0f, 0f, 0f));
            UIKit.Stretch(black.rectTransform);

            for (int i = 0; i < 10; i++)
            {
                var l = UIKit.Image("Line", root, Color.white);
                var rt = l.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f);
                l.gameObject.SetActive(false);
                lines.Add(l);
                lineUntil.Add(0f);
            }

            // 가운데 '신호 끊김' 상자 (시안: 붉은 테두리 둥근 판, Bold 22)
            signalBox = UIKit.Rect("Signal", root);
            signalBox.anchorMin = signalBox.anchorMax = signalBox.pivot = new Vector2(0.5f, 0.5f);
            signalBox.sizeDelta = new Vector2(288f, 80f);
            signalGroup = signalBox.gameObject.AddComponent<CanvasGroup>();
            signalGroup.alpha = 0f;
            UIKit.Stretch(UIKit.Box("Bg", signalBox, new Color(20 / 255f, 10 / 255f, 12 / 255f, 0.94f), 16).rectTransform);
            UIKit.Stretch(UIKit.Image("Border", signalBox, signalRed, UIKit.Rounded(16, 2)).rectTransform);
            signalDot = UIKit.Image("Dot", signalBox, signalRed, UIKit.Circle(32));
            UIKit.Place(signalDot.rectTransform, 40, 32, 16, 16);
            var txt = UIKit.Text("Text", signalBox, NpcLines.SignalLost, 22, signalRed, true, TextAnchor.MiddleLeft);
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIKit.Place(txt.rectTransform, 72, 0, 200, 80);

            canvas.enabled = false;
        }

        /// <summary>가장자리가 부드럽게 사라지는 둥근 판 (손바닥 그림자).</summary>
        static Sprite SoftSprite()
        {
            if (softSprite != null) return softSprite;
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
            };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, r));
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            softSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return softSprite;
        }
    }
}
