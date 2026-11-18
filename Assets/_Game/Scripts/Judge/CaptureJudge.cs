using System;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>매 프레임 촬영 판정. 품질 = 보임 × 크기 × 중앙 × 안정 × 단계배율.</summary>
    public class CaptureJudge : MonoBehaviour
    {
        [Header("판정 수치 (기획서 기본값)")]
        public float minQuality = 0.25f;
        public float graceSeconds = 0.3f;
        public float stableBelow = 20f;     // deg/s 이하 안정 1
        public float stableAbove = 120f;    // deg/s 이상 안정 0
        public float aimedMultiplier = 1.0f;
        public float hiddenMultiplier = 0.7f;
        public float flowWindow = 20f;
        public float recaptureCooldown = 4f; // 같은 대상 연속 확보 최소 간격
        [Tooltip("true면 품질 0.25 이상인 대상끼리만 경쟁. false면 품질 낮아도 가치가 크면 자리를 막음(기획서 문자 그대로)")]
        public bool onlyEligibleCompete = true;

        [Header("가려짐 검사")]
        [Tooltip("EyeOnly(6)는 폰에 안 찍히므로 기본 제외")]
        public LayerMask occlusionMask = ~(1 << 6);

        [Header("초점 상자")]
        public float boxThickness = 3f;
        public Color holdColor = new Color(1f, 0.85f, 0.1f, 1f);
        public Color capturedColor = new Color(0.25f, 1f, 0.35f, 1f);
        public float flashSeconds = 0.6f;

        public event Action<CaptureResult> Captured;

        public CaptureTarget CurrentTarget { get; private set; }
        public float CurrentQuality { get; private set; }
        public float HoldProgress { get { return CurrentTarget != null && CurrentTarget.holdSeconds > 0f ? Mathf.Clamp01(hold / CurrentTarget.holdSeconds) : 0f; } }

        // 유지 상태
        float hold, qSum, lastGoodTime;
        Rect currentRect;

        // 초점 상자 UI
        RectTransform boxRoot;
        Image[] bars;
        RectTransform boxParentCached;
        float flashUntil;
        Rect flashRect;

        static readonly RaycastHit[] hitBuf = new RaycastHit[16];
        static readonly Vector3[] signs = {
            new Vector3(-1,-1,-1), new Vector3(1,-1,-1), new Vector3(-1,1,-1), new Vector3(1,1,-1),
            new Vector3(-1,-1, 1), new Vector3(1,-1, 1), new Vector3(-1,1, 1), new Vector3(1,1, 1) };

        void Update()
        {
            var pc = PhoneController.Instance;
            UpdateSounds(pc);

            bool recording = pc != null && pc.PhoneCamera != null && pc.IsRecording
                             && (pc.State == PhoneState.Hidden || pc.State == PhoneState.Aimed);
            if (!recording)
            {
                ResetHold();
                UpdateBox(pc);
                return;
            }

            var cam = pc.PhoneCamera;
            float stateMul = pc.State == PhoneState.Aimed ? aimedMultiplier : hiddenMultiplier;
            float stab = Stability(pc.AngularSpeed);

            CaptureTarget best = null;
            float bestScore = -1f, bestQ = 0f;
            Rect bestRect = default;

            var all = CaptureTarget.All;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (t == null || !t.activeNow || t.category == CaptureCategory.Sound) continue;
                if (Time.time - t.lastCaptureTime < recaptureCooldown) continue;
                float q; Rect r;
                if (!Evaluate(cam, t, stab * stateMul, out q, out r)) continue;
                if (onlyEligibleCompete && q < minQuality) continue;
                float score = t.baseViews * q;
                if (score > bestScore) { bestScore = score; best = t; bestQ = q; bestRect = r; }
            }

            float dt = Time.deltaTime;
            if (best != null && bestQ >= minQuality)
            {
                if (best != CurrentTarget) { ResetHold(); CurrentTarget = best; }
                hold += dt;
                qSum += bestQ * dt;
                lastGoodTime = Time.time;
                CurrentQuality = bestQ;
                currentRect = bestRect;
                if (hold >= best.holdSeconds)
                {
                    float avg = qSum / Mathf.Max(hold, 0.0001f);
                    var rect = currentRect;
                    var t = best;
                    ResetHold();
                    Register(t, avg, pc);
                    flashRect = rect;
                    flashUntil = Time.time + flashSeconds;
                }
            }
            else if (CurrentTarget != null && Time.time - lastGoodTime > graceSeconds)
            {
                ResetHold();
            }
            UpdateBox(pc);
        }

        void ResetHold()
        {
            CurrentTarget = null;
            CurrentQuality = 0f;
            hold = 0f; qSum = 0f;
        }

        float Stability(float angularSpeed)
        {
            if (angularSpeed <= stableBelow) return 1f;
            if (angularSpeed >= stableAbove) return 0f;
            return 1f - (angularSpeed - stableBelow) / (stableAbove - stableBelow);
        }

        /// <summary>한 대상의 품질(0~1)과 뷰포트 사각형. 화면 밖·폰이 안 그리는 대상은 false.</summary>
        bool Evaluate(Camera cam, CaptureTarget t, float stateStab, out float quality, out Rect rect)
        {
            quality = 0f; rect = default;
            if (stateStab <= 0f) return false;
            Bounds b;
            if (!t.TryGetBounds(cam, out b)) return false;

            // 8모서리 투영
            float xMin = 2, xMax = -1, yMin = 2, yMax = -1;
            int valid = 0;
            Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 vp = cam.WorldToViewportPoint(c + Vector3.Scale(e, signs[i]));
                if (vp.z <= 0f) continue;
                valid++;
                if (vp.x < xMin) xMin = vp.x; if (vp.x > xMax) xMax = vp.x;
                if (vp.y < yMin) yMin = vp.y; if (vp.y > yMax) yMax = vp.y;
            }
            if (valid == 0) return false;
            xMin = Mathf.Clamp01(xMin); xMax = Mathf.Clamp01(xMax);
            yMin = Mathf.Clamp01(yMin); yMax = Mathf.Clamp01(yMax);
            float w = xMax - xMin, h = yMax - yMin;
            if (w <= 0.0005f || h <= 0.0005f) return false;
            rect = new Rect(xMin, yMin, w, h);

            float size = Mathf.Min(1f, h / Mathf.Max(0.01f, t.requiredHeight));
            float dist = Vector2.Distance(rect.center, new Vector2(0.5f, 0.5f));
            float center = Mathf.Max(0.5f, 1f - 0.5f * (dist / 0.5f));

            float cheap = size * center * stateStab;
            if (cheap < minQuality) { quality = cheap; return true; } // 가려짐 검사 생략(어차피 미달)

            float vis = Visibility(cam, t, b);
            quality = cheap * vis;
            return true;
        }

        /// <summary>중심+안쪽 8점에서 폰 카메라로 레이를 쏴 가려지지 않은 비율.</summary>
        float Visibility(Camera cam, CaptureTarget t, Bounds b)
        {
            Vector3 origin = cam.transform.position;
            int seen = 0;
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = i == 0 ? b.center : b.center + Vector3.Scale(b.extents, signs[i - 1]) * 0.5f;
                Vector3 vp = cam.WorldToViewportPoint(p);
                if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue; // 화면 밖 = 안 보임
                Vector3 d = p - origin;
                float dist = d.magnitude;
                if (dist < 0.05f) { seen++; continue; }
                int n = Physics.RaycastNonAlloc(origin, d / dist, hitBuf, dist - 0.02f, occlusionMask, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int k = 0; k < n; k++)
                {
                    var col = hitBuf[k].collider;
                    if (col == null) continue;
                    if (col.GetComponentInParent<CaptureTarget>() == t) continue; // 자기 콜라이더 무시
                    blocked = true; break;
                }
                if (!blocked) seen++;
            }
            return seen / 9f;
        }

        // ---------- 확보 ----------

        void Register(CaptureTarget t, float avgQuality, PhoneController pc)
        {
            var grade = GradeOf(avgQuality);
            int prior = CaptureLog.CountFor(t.Id);
            float novelty = prior == 0 ? 1f : (prior == 1 ? 0.4f : 0.1f);
            float flow = Mathf.Min(1.5f, 1f + 0.25f * CaptureLog.CountSince(Time.time - flowWindow));
            float views = t.baseViews * GradeMultiplier(grade) * novelty * flow;

            var res = new CaptureResult
            {
                Target = t,
                TargetId = t.Id,
                TargetName = t.Name,
                Category = t.category,
                Grade = grade,
                AverageQuality = avgQuality,
                Viewers = Mathf.RoundToInt(views),
                NewsValue = t.evidenceValue * GradeMultiplier(grade),
                Novelty = novelty,
                Flow = flow,
                Snapshot = Snap(pc),
                GameTime = Time.time,
            };
            t.lastCaptureTime = Time.time;
            CaptureLog.Add(res);
            Captured?.Invoke(res);
        }

        public static CaptureGrade GradeOf(float q)
        {
            if (q >= 0.85f) return CaptureGrade.S;
            if (q >= 0.65f) return CaptureGrade.A;
            if (q >= 0.45f) return CaptureGrade.B;
            return CaptureGrade.C;
        }

        public static float GradeMultiplier(CaptureGrade g)
        {
            switch (g)
            {
                case CaptureGrade.S: return 2.0f;
                case CaptureGrade.A: return 1.4f;
                case CaptureGrade.B: return 1.0f;
                default: return 0.5f;
            }
        }

        Texture2D Snap(PhoneController pc)
        {
            if (pc == null || pc.Feed == null) return null;
            var rt = pc.Feed;
            var prev = RenderTexture.active;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(false);
            RenderTexture.active = prev;
            return tex;
        }

        // ---------- 소리 ----------

        /// <summary>소리 대상: 말이 이어지는 동안 반경 안 + 녹음 중이면 끝에서 확보. 거리로 품질.</summary>
        void UpdateSounds(PhoneController pc)
        {
            var all = CaptureTarget.All;
            float dt = Time.deltaTime;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (t == null || t.category != CaptureCategory.Sound) continue;

                if (t.SoundPlaying)
                {
                    Vector3 ear;
                    bool hasEar = TryGetEar(pc, out ear);
                    float d = hasEar ? Vector3.Distance(ear, t.transform.position) : float.MaxValue;
                    bool ok = hasEar && pc != null && pc.IsAudioRecording && d <= t.hearingRadius;
                    if (ok)
                    {
                        t.soundHeard += dt;
                        t.soundQSum += (1f - 0.75f * Mathf.Clamp01(d / Mathf.Max(0.1f, t.hearingRadius))) * dt;
                    }
                    else t.soundMissed += dt;
                }
                else if (t.soundFinished)
                {
                    t.soundFinished = false;
                    float total = t.soundHeard + t.soundMissed;
                    // 끝까지 녹음됐는지: 놓친 시간이 0.5초와 전체 10% 중 큰 쪽 이하
                    if (total > 0.1f && t.soundHeard > 0f && t.soundMissed <= Mathf.Max(0.5f, total * 0.1f))
                        Register(t, t.soundQSum / t.soundHeard, pc);
                    t.soundHeard = t.soundMissed = t.soundQSum = 0f;
                }
            }
        }

        static bool TryGetEar(PhoneController pc, out Vector3 pos)
        {
            if (pc != null && pc.PhoneCamera != null) { pos = pc.PhoneCamera.transform.position; return true; }
            var m = Camera.main;
            if (m != null) { pos = m.transform.position; return true; }
            pos = default; return false;
        }

        // ---------- 초점 상자 ----------

        void EnsureBox(PhoneController pc)
        {
            if (pc == null || pc.ScreenArea == null) return;
            if (boxRoot != null && boxParentCached == pc.ScreenArea) return;
            if (boxRoot != null) Destroy(boxRoot.gameObject);
            boxParentCached = pc.ScreenArea;

            var go = new GameObject("FocusBox", typeof(RectTransform));
            boxRoot = go.GetComponent<RectTransform>();
            boxRoot.SetParent(pc.ScreenArea, false);
            boxRoot.pivot = new Vector2(0.5f, 0.5f);
            boxRoot.offsetMin = boxRoot.offsetMax = Vector2.zero;

            bars = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var bg = new GameObject("Bar" + i, typeof(RectTransform), typeof(Image));
                var rt = bg.GetComponent<RectTransform>();
                rt.SetParent(boxRoot, false);
                var img = bg.GetComponent<Image>();
                img.raycastTarget = false;
                bars[i] = img;
                switch (i)
                {
                    case 0: rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); break; // 위
                    case 1: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0); break; // 아래
                    case 2: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 0.5f); break; // 왼
                    default: rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 0.5f); break; // 오른
                }
                rt.anchoredPosition = Vector2.zero;
            }
            SetThickness();
            boxRoot.gameObject.SetActive(false);
        }

        void SetThickness()
        {
            for (int i = 0; i < 4; i++)
            {
                var rt = (RectTransform)bars[i].transform;
                rt.sizeDelta = i < 2 ? new Vector2(0, boxThickness) : new Vector2(boxThickness, 0);
            }
        }

        void UpdateBox(PhoneController pc)
        {
            EnsureBox(pc);
            if (boxRoot == null) return;

            bool flashing = Time.time < flashUntil;
            bool holding = CurrentTarget != null;
            bool show = pc != null && pc.IsScreenVisible && (flashing || holding);
            if (boxRoot.gameObject.activeSelf != show) boxRoot.gameObject.SetActive(show);
            if (!show) return;

            Rect r = flashing ? flashRect : currentRect;
            boxRoot.anchorMin = new Vector2(r.xMin, r.yMin);
            boxRoot.anchorMax = new Vector2(r.xMax, r.yMax);
            boxRoot.offsetMin = boxRoot.offsetMax = Vector2.zero;

            Color col = holdColor;
            if (flashing)
            {
                // 0.6초 동안 초록 깜빡
                float k = (flashUntil - Time.time) / flashSeconds;
                bool on = Mathf.FloorToInt(k * 6f) % 2 == 0;
                col = capturedColor;
                col.a = on ? 1f : 0.25f;
            }
            for (int i = 0; i < 4; i++) bars[i].color = col;
        }

        void OnDestroy()
        {
            if (boxRoot != null) Destroy(boxRoot.gameObject);
        }
    }
}
