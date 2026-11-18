using System;
using UnityEngine;

namespace UndercoverLive
{
    public enum WatcherKind { Believer, Staff, Leader, CCTV }

    /// <summary>감시자: 시야 원뿔 안에서 플레이어가 보이면 의심 게이지(0~100)가 찬다.</summary>
    public class Watcher : MonoBehaviour
    {
        public WatcherKind kind = WatcherKind.Believer;
        [Tooltip("시선 기준. 비면 자기 transform. 시야 방향은 eye.forward")]
        public Transform eye;
        [Tooltip("눈치 단계에서 플레이어 쪽으로 돌릴 Transform. 비면 eye. 리더·CCTV는 안 돌림")]
        public Transform turnTarget;
        [Tooltip("시야 전체 각도(도)")]
        public float viewAngle = 50f;
        public float range = 8f;
        public float fillMultiplier = 1f;
        [Range(0, 100)] public float suspicion;

        [Header("속도(초당)")]
        public float aimedRate = 45f;
        public float hiddenRate = 15f;
        public float hiddenMaxDistance = 4f;
        public float decayRate = 10f;
        [Header("거리 보정")]
        public float nearDistance = 3f;
        public float farFactor = 0.3f;
        [Header("단계")]
        public float noticeThreshold = 30f;
        public float whisperThreshold = 70f;
        public float turnSpeed = 4f;
        [Header("쿨다운")]
        [Tooltip("100을 찍은 뒤 이 시간 동안은 다시 차지 않고 식기만 한다 (한 번 들킨 것으로 경계가 연달아 오르지 않게)")]
        public float cooldown = 12f;
        float cooldownUntil;
        /// <summary>신도들이 무대에 몰입한 정도 (기적 중 0 = 아무도 플레이어를 안 봄).</summary>
        public static float CrowdFocus = 1f;
        [Tooltip("시야 가림 검사 대상 레이어")]
        public LayerMask blockMask = ~0;

        public bool Visible { get; private set; }
        public bool Noticing { get; private set; }
        public Vector3 EyePosition => (eye != null ? eye : transform).position;

        /// <summary>30 넘음(눈치).</summary>
        public event Action<Watcher> Noticed;
        /// <summary>70 넘음(수군거림).</summary>
        public event Action<Watcher> Whispered;
        /// <summary>100 도달.</summary>
        public event Action<Watcher> Filled;

        bool whispered;
        bool filledSent;
        Quaternion restLocalRot;
        Transform turnT;
        Vector3 lastTargetPos;
        readonly RaycastHit[] hits = new RaycastHit[16];

        /// <summary>종류별 기본값 적용.</summary>
        public void ApplyKindDefaults()
        {
            switch (kind)
            {
                case WatcherKind.Believer: viewAngle = 50f; range = 4f; fillMultiplier = 0.35f; break;
                case WatcherKind.Staff: viewAngle = 70f; range = 12f; fillMultiplier = 1f; break;
                case WatcherKind.Leader: viewAngle = 60f; range = 18f; fillMultiplier = 1f; break;
                case WatcherKind.CCTV: viewAngle = 55f; range = 18f; fillMultiplier = 1.2f; break;
            }
        }

        [ContextMenu("종류별 기본값 적용")]
        void ApplyKindDefaultsMenu() { ApplyKindDefaults(); }

        void Reset() { ApplyKindDefaults(); }

        void OnEnable()
        {
            if (SuspicionManager.Instance != null) SuspicionManager.Instance.Register(this);
            registered = SuspicionManager.Instance != null;
        }

        void OnDisable()
        {
            if (SuspicionManager.Instance != null) SuspicionManager.Instance.Unregister(this);
            registered = false;
        }

        bool registered;

        void Start()
        {
            if (eye == null) eye = transform;
            turnT = turnTarget != null ? turnTarget : eye;
            restLocalRot = turnT.localRotation;
            if (!registered && SuspicionManager.Instance != null)
            {
                SuspicionManager.Instance.Register(this);
                registered = true;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Visible = false;
            PhoneState state = PhoneState.Pocket;

            var pc = PhoneController.Instance;
            Transform head = null;
            if (pc != null)
            {
                state = pc.State;
                head = pc.Head != null ? pc.Head : (pc.PhoneCamera != null ? pc.PhoneCamera.transform : null);
            }
            if (head != null)
            {
                lastTargetPos = head.position;
                Visible = CanSee(head);
            }

            float rate = 0f;
            if (Visible)
            {
                float dist = Vector3.Distance(EyePosition, head.position);
                switch (state)
                {
                    case PhoneState.Aimed: rate = aimedRate; break;
                    case PhoneState.Hidden: rate = dist <= hiddenMaxDistance ? hiddenRate : 0f; break;
                    default: rate = 0f; break;
                }
                rate *= DistanceFactor(dist) * fillMultiplier;
                if (kind == WatcherKind.Believer) rate *= CrowdFocus;
            }
            if (Time.time < cooldownUntil) rate = 0f;

            if (rate > 0f) suspicion += rate * dt;
            else if (!Visible || Time.time < cooldownUntil) suspicion -= decayRate * dt;
            // 보이지만 Pocket/멀리 Hidden이면 유지(감소 없음)
            suspicion = Mathf.Clamp(suspicion, 0f, 100f);

            UpdateStages();
            UpdateTurn(dt);
        }

        float DistanceFactor(float d)
        {
            if (d <= nearDistance) return 1f;
            float t = Mathf.InverseLerp(nearDistance, Mathf.Max(range, nearDistance + 0.01f), d);
            return Mathf.Lerp(1f, farFactor, t);
        }

        bool CanSee(Transform head)
        {
            Transform e = eye != null ? eye : transform;
            Vector3 from = e.position;
            Vector3 to = head.position;
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist > range || dist < 0.0001f) return dist < 0.0001f;
            if (Vector3.Angle(e.forward, dir) > viewAngle * 0.5f) return false;

            // 가림 검사: 자기 자신·플레이어 소속 콜라이더는 무시
            int n = Physics.RaycastNonAlloc(from, dir / dist, hits, dist, blockMask, QueryTriggerInteraction.Ignore);
            Transform playerRoot = head.root;
            for (int i = 0; i < n; i++)
            {
                Transform t = hits[i].transform;
                if (t.IsChildOf(transform) || t.IsChildOf(e) || t.IsChildOf(playerRoot)) continue;
                return false;
            }
            return true;
        }

        void UpdateStages()
        {
            if (!Noticing && suspicion >= noticeThreshold)
            {
                Noticing = true;
                Noticed?.Invoke(this);
            }
            else if (Noticing && suspicion < noticeThreshold - 5f) Noticing = false;

            if (!whispered && suspicion >= whisperThreshold)
            {
                whispered = true;
                // 수군거림으로 경계가 오르는 건 신도뿐. 관리자·교주·CCTV는 100에서 한 번만 올린다.
                if (kind == WatcherKind.Believer && SuspicionManager.Instance != null) SuspicionManager.Instance.AddAlert($"{name} 수군거림");
                Whispered?.Invoke(this);
            }
            else if (whispered && suspicion < whisperThreshold - 20f) whispered = false;

            if (!filledSent && suspicion >= 100f)
            {
                filledSent = true;
                cooldownUntil = Time.time + cooldown;
                Filled?.Invoke(this);
                if (SuspicionManager.Instance != null) SuspicionManager.Instance.ReportFull(this);
                else suspicion = 60f;
            }
            else if (filledSent && suspicion < 90f) filledSent = false;
        }

        void UpdateTurn(float dt)
        {
            if (kind == WatcherKind.Leader || kind == WatcherKind.CCTV || turnT == null) return;
            Quaternion rest = turnT.parent != null ? turnT.parent.rotation * restLocalRot : restLocalRot;
            Quaternion goal = rest;
            if (Noticing)
            {
                Vector3 d = lastTargetPos - turnT.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0001f) goal = Quaternion.LookRotation(d.normalized, Vector3.up);
            }
            turnT.rotation = Quaternion.Slerp(turnT.rotation, goal, 1f - Mathf.Exp(-turnSpeed * dt));
        }

        void OnDrawGizmos()
        {
            Transform e = eye != null ? eye : transform;
            float ratio = Mathf.Clamp01(suspicion / 100f);
            Color c = ratio < 0.5f ? Color.Lerp(new Color(0.3f, 0.9f, 0.4f), new Color(1f, 0.6f, 0.1f), ratio * 2f)
                                   : Color.Lerp(new Color(1f, 0.6f, 0.1f), Color.red, (ratio - 0.5f) * 2f);
            Gizmos.color = c;
            Vector3 o = e.position;
            float h = viewAngle * 0.5f;
            Vector3 f = e.forward;
            Vector3 up = e.up, right = e.right;
            Vector3 l = Quaternion.AngleAxis(-h, up) * f;
            Vector3 r = Quaternion.AngleAxis(h, up) * f;
            Vector3 u = Quaternion.AngleAxis(-h, right) * f;
            Vector3 d = Quaternion.AngleAxis(h, right) * f;
            Gizmos.DrawLine(o, o + l * range);
            Gizmos.DrawLine(o, o + r * range);
            Gizmos.DrawLine(o, o + u * range);
            Gizmos.DrawLine(o, o + d * range);
            // 끝 호(수평)
            const int seg = 16;
            Vector3 prev = o + l * range;
            for (int i = 1; i <= seg; i++)
            {
                float a = Mathf.Lerp(-h, h, i / (float)seg);
                Vector3 p = o + Quaternion.AngleAxis(a, up) * f * range;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            if (Application.isPlaying && Visible)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(o, lastTargetPos);
            }
        }
    }
}
