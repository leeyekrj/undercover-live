using System.Collections.Generic;
using UnityEngine;

namespace UndercoverLive
{
    public enum CaptureCategory { Atmosphere, Evidence, Climax, Sound }

    /// <summary>찍을 대상에 붙인다. 영상 대상은 폰 카메라 판정, Sound는 반경+녹음 판정.</summary>
    public class CaptureTarget : MonoBehaviour
    {
        public static readonly List<CaptureTarget> All = new List<CaptureTarget>();

        [Header("식별")]
        public string targetId;
        public string displayName;
        public CaptureCategory category = CaptureCategory.Atmosphere;

        [Header("가치")]
        public float baseViews = 1000f;
        [Tooltip("화면 높이 대비 필요한 크기. 사람·행동 0.25, 문서 0.5")]
        public float requiredHeight = 0.25f;
        [Tooltip("끊기지 않고 유지해야 하는 초. 분위기 1.5 · 증거 2 · 결정적 3")]
        public float holdSeconds = 1.5f;
        [Tooltip("보도 가치 기본치 (분위기 낮음 · 증거/소리 높음)")]
        public float evidenceValue = 0.1f;

        [Header("상태")]
        [Tooltip("이벤트 타임라인이 켜고 끈다")]
        public bool activeNow = true;
        [Tooltip("비워 두면 자식 Renderer를 자동 수집")]
        public Renderer[] renderers;

        [Header("채팅")]
        [Tooltip("이 대상 전용 반응 문구 (선택)")]
        public string[] chatOnCapture;

        [Header("소리 대상")]
        public float hearingRadius = 9f;

        // 소리 판정 상태 (CaptureJudge가 사용)
        float soundEnd;
        public bool SoundPlaying { get; private set; }
        internal bool soundFinished;
        internal float soundHeard, soundMissed, soundQSum;
        internal float lastCaptureTime = -999f;

        public string Id => string.IsNullOrEmpty(targetId) ? gameObject.name : targetId;
        public string Name => string.IsNullOrEmpty(displayName) ? Id : displayName;

        void Reset()
        {
            ApplyCategoryDefaults();
        }

        /// <summary>분류별 기본 유지 시간·가치.</summary>
        public void ApplyCategoryDefaults()
        {
            switch (category)
            {
                case CaptureCategory.Atmosphere: holdSeconds = 1.5f; baseViews = 1000f; evidenceValue = 0.1f; break;
                case CaptureCategory.Evidence: holdSeconds = 2f; baseViews = 5000f; evidenceValue = 0.8f; break;
                case CaptureCategory.Climax: holdSeconds = 3f; baseViews = 30000f; evidenceValue = 1f; break;
                case CaptureCategory.Sound: holdSeconds = 0f; baseViews = 3000f; evidenceValue = 0.7f; break;
            }
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); SoundPlaying = false; }

        /// <summary>소리 시작. seconds 동안 말이 이어진다.</summary>
        public void BeginSound(float seconds)
        {
            soundEnd = Time.time + Mathf.Max(0.1f, seconds);
            SoundPlaying = true;
            soundFinished = false;
            soundHeard = soundMissed = soundQSum = 0f;
            activeNow = true;
        }

        /// <summary>소리 끝. 끝까지 녹음됐다면 판정기가 확보 처리한다.</summary>
        public void EndSound()
        {
            if (!SoundPlaying) return;
            SoundPlaying = false;
            soundFinished = true;
            activeNow = false;
        }

        void Update()
        {
            if (SoundPlaying && Time.time >= soundEnd) EndSound();
        }

        /// <summary>폰 카메라에 보이는 렌더러만 모은 월드 바운드. 없으면 false.</summary>
        public bool TryGetBounds(Camera cam, out Bounds bounds)
        {
            bounds = default;
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>();
            bool any = false;
            int mask = cam != null ? cam.cullingMask : ~0;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if ((mask & (1 << r.gameObject.layer)) == 0) continue; // 폰 카메라가 안 그리는 레이어
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
