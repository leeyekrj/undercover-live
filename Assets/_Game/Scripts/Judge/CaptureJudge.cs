using System;
using UnityEngine;

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

        [Header("확보 표시 (초점 상자는 HUD/PhoneAppUI가 그린다)")]
        public float flashSeconds = 0.6f;

        [Header("스냅샷")]
        [Tooltip("확보 순간 화면을 이 긴 변 크기로 줄여 저장")]
        public int snapshotSize = 480;
        public int maxSnapshots = 12;

        [Header("소리")]
        [Tooltip("주머니 속 녹음은 먹먹하다 (몰래·촬영은 1)")]
        public float pocketSoundMultiplier = 0.5f;

        public event Action<CaptureResult> Captured;

        public static CaptureJudge Instance { get; private set; }

        public CaptureTarget CurrentTarget { get; private set; }
        public float CurrentQuality { get; private set; }
        public float HoldProgress { get { return CurrentTarget != null && CurrentTarget.holdSeconds > 0f ? Mathf.Clamp01(hold / CurrentTarget.holdSeconds) : 0f; } }
        /// <summary>지금 확보 중인 대상의 폰 화면(뷰포트 0~1) 사각형.</summary>
        public Rect CurrentRect => currentRect;
        /// <summary>지금까지 유지한 평균 품질 (예상 등급 = GradeOf(이 값)).</summary>
        public float HoldAverageQuality => hold > 0f ? qSum / hold : CurrentQuality;
        /// <summary>마지막 영상 확보 순간의 뷰포트 사각형과 시각(Time.time). 소리 확보는 바꾸지 않는다.</summary>
        public Rect LastCaptureRect => flashRect;
        public float LastCaptureTime => flashUntil - flashSeconds;
        /// <summary>판정 기준(최소 품질)과 상관없이 지금 폰 화면에 가장 크게 잡힌 대상 (채팅 상황 고르기용). 없으면 null.</summary>
        public CaptureTarget FramedTarget { get; private set; }
        /// <summary>FramedTarget의 화면 높이 / 필요 높이 (1 미만이면 작게 잡힘 → 줌 요청).</summary>
        public float FramedSize01 { get; private set; }

        // 유지 상태
        float hold, qSum, lastGoodTime;
        Rect currentRect;
        float flashUntil = -99f;
        Rect flashRect;

        static readonly RaycastHit[] hitBuf = new RaycastHit[16];
        static readonly Vector3[] signs = {
            new Vector3(-1,-1,-1), new Vector3(1,-1,-1), new Vector3(-1,1,-1), new Vector3(1,1,-1),
            new Vector3(-1,-1, 1), new Vector3(1,-1, 1), new Vector3(-1,1, 1), new Vector3(1,1, 1) };

        void Awake() { Instance = this; }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (!GameEvents.IsPlaying) return;   // 메뉴·도입·멈춤·엔딩에서는 판정도 멈춘다
            var pc = PhoneController.Instance;
            UpdateSounds(pc);

            bool recording = pc != null && pc.PhoneCamera != null && pc.IsRecording
                             && (pc.State == PhoneState.Hidden || pc.State == PhoneState.Aimed);
            if (!recording)
            {
                ResetHold();
                FramedTarget = null; FramedSize01 = 0f;
                return;
            }

            var cam = pc.PhoneCamera;
            float stateMul = pc.State == PhoneState.Aimed ? aimedMultiplier : hiddenMultiplier;
            float stab = Stability(pc.AngularSpeed);

            CaptureTarget best = null;
            float bestScore = -1f, bestQ = 0f;
            Rect bestRect = default;
            CaptureTarget framed = null;
            float framedQ = 0.08f, framedSize = 0f;

            var all = CaptureTarget.All;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (t == null || !t.activeNow || t.category == CaptureCategory.Sound) continue;
                float q; Rect r;
                // 채팅용 '화면에 잡힌 대상'은 재확보 대기와 상관없이 본다 (판정에는 영향 없음)
                bool cooling = Time.time - t.lastCaptureTime < recaptureCooldown;
                if (!Evaluate(cam, t, stab * stateMul, out q, out r)) continue;
                if (q > framedQ) { framedQ = q; framed = t; framedSize = r.height / Mathf.Max(0.01f, t.requiredHeight); }
                if (cooling) continue;
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
                    flashRect = rect;                       // HUD가 확보 이벤트에서 이 사각형을 읽으므로 먼저 넣는다
                    flashUntil = Time.time + flashSeconds;
                    Register(t, avg, pc);
                }
            }
            else if (CurrentTarget != null && Time.time - lastGoodTime > graceSeconds)
            {
                ResetHold();
            }
            FramedTarget = framed;
            FramedSize01 = framed != null ? framedSize : 0f;
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
            // 보도 가치는 대상마다 최고 1회분만 합산: 이번 값이 이전 최고보다 높을 때 그 차이만 더한다
            // (새로움 배율은 시청자에만). 같은 대상을 반복해 찍어 가치를 무한히 쌓을 수 없다.
            float fullNews = t.evidenceValue * GradeMultiplier(grade);
            float priorNews = 0f;
            foreach (var c in CaptureLog.Clips) if (c.TargetId == t.Id) priorNews += c.NewsValue;
            float news = Mathf.Max(0f, fullNews - priorNews);

            var res = new CaptureResult
            {
                Target = t,
                TargetId = t.Id,
                TargetName = t.Name,
                Category = t.category,
                Grade = grade,
                AverageQuality = avgQuality,
                Viewers = Mathf.RoundToInt(views),
                NewsValue = news,
                Novelty = novelty,
                Flow = flow,
                Snapshot = Snap(pc),
                GameTime = Time.time,
            };
            t.lastCaptureTime = Time.time;
            CaptureLog.Add(res);
            TrimSnapshots();
            Captured?.Invoke(res);
        }

        /// <summary>스냅샷은 최대 maxSnapshots장. 넘치면 오래된 것부터 지운다.</summary>
        void TrimSnapshots()
        {
            int n = 0;
            for (int i = CaptureLog.Clips.Count - 1; i >= 0; i--)
            {
                var c = CaptureLog.Clips[i];
                if (c.Snapshot == null) continue;
                n++;
                if (n > maxSnapshots) { Destroy(c.Snapshot); c.Snapshot = null; }
            }
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

        /// <summary>확보 순간 폰 화면을 작게 (긴 변 snapshotSize px) 떠 둔다. 폰 카메라가 꺼져 있으면(몰래) 한 장만 렌더.</summary>
        Texture2D Snap(PhoneController pc)
        {
            if (pc == null || pc.Feed == null) return null;
            pc.RenderFeedNow();
            var src = pc.Feed;
            float k = Mathf.Min(1f, snapshotSize / (float)Mathf.Max(src.width, src.height));
            int w = Mathf.Max(16, Mathf.RoundToInt(src.width * k)), h = Mathf.Max(16, Mathf.RoundToInt(src.height * k));
            var small = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default);
            Graphics.Blit(src, small);
            var prev = RenderTexture.active;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            RenderTexture.active = small;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(small);
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
                        float muffle = pc.State == PhoneState.Pocket ? pocketSoundMultiplier : 1f;
                        t.soundHeard += dt;
                        t.soundQSum += (1f - 0.75f * Mathf.Clamp01(d / Mathf.Max(0.1f, t.hearingRadius))) * muffle * dt;
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
    }
}
