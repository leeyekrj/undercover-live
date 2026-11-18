using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 교주(·관리자) 시선 훑기: Y축 좌우로 천천히 훑다가 랜덤하게 멈춰 응시.
    /// Fixate(대상)이면 훑기를 멈추고 그 대상을 똑바로 본다(무대로 다가온 플레이어).
    /// suspended면 아무것도 안 함(관리자가 걸어가는 동안 몸 방향은 StaffEscort가 정한다).
    /// GameEvents.IsPlaying이 아니면 멈춘다.
    /// </summary>
    public class LeaderGaze : MonoBehaviour
    {
        [Tooltip("타임라인이 끄고 켠다")]
        public bool sweeping = true;
        [Tooltip("다른 스크립트가 몸을 직접 돌리는 동안 멈춤")]
        public bool suspended;
        public float maxYaw = 35f;
        public Vector2 speedRange = new Vector2(12f, 20f);
        public Vector2 holdRange = new Vector2(1f, 3f);
        public Vector2 pauseEvery = new Vector2(3f, 8f);
        [Tooltip("교주(Leader 감시자)면 시작 때 훑는 폭을 이 값으로 (좁은 시선 26°와 함께 플레이어 쪽을 보는 비율 약 20%)")]
        public float leaderSweepYaw = 60f;
        public bool applyLeaderSweep = true;
        [Tooltip("LookAt·Fixate로 돌아가는 속도(도/초)")]
        public float lookAtSpeed = 90f;
        public float lookAtMaxYaw = 70f;

        Quaternion baseLocal;
        float yaw;
        float dir = 1f;
        float speed;
        float holdTimer;
        float nextPause;
        float overrideTimer;
        float overrideYaw;
        bool overriding;
        Transform fixTarget;

        public float Yaw => yaw;
        public bool Fixated => fixTarget != null;

        void Start()
        {
            baseLocal = transform.localRotation;
            var w = GetComponent<Watcher>();
            if (applyLeaderSweep && w != null && w.kind == WatcherKind.Leader) maxYaw = leaderSweepYaw;
            speed = Random.Range(speedRange.x, speedRange.y);
            nextPause = Random.Range(pauseEvery.x, pauseEvery.y);
            dir = Random.value < 0.5f ? -1f : 1f;
        }

        /// <summary>지정 지점을 seconds초 동안 응시(끝나면 훑기 재개).</summary>
        public void LookAt(Vector3 point, float seconds)
        {
            if (!YawToward(point, out overrideYaw)) return;
            overrideTimer = seconds;
            overriding = true;
        }

        /// <summary>대상을 계속 똑바로 본다(Release 전까지). 훑기·LookAt보다 우선.</summary>
        public void Fixate(Transform target) { fixTarget = target; }

        /// <summary>Fixate 해제 → 훑기 재개.</summary>
        public void Release()
        {
            if (fixTarget == null) return;
            fixTarget = null;
            nextPause = Random.Range(pauseEvery.x, pauseEvery.y);
        }

        /// <summary>정면(시작 방향)으로 되돌림. 관리자가 제자리로 돌아온 뒤 튀지 않게.</summary>
        public void ResetYaw()
        {
            yaw = 0f;
            overriding = false;
            holdTimer = 0f;
        }

        bool YawToward(Vector3 point, out float result)
        {
            result = 0f;
            Transform p = transform.parent;
            Quaternion baseWorld = p != null ? p.rotation * baseLocal : baseLocal;
            Vector3 fwd = baseWorld * Vector3.forward; fwd.y = 0f;
            Vector3 to = point - transform.position; to.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f) return false;
            result = Mathf.Clamp(Vector3.SignedAngle(fwd, to, Vector3.up), -lookAtMaxYaw, lookAtMaxYaw);
            return true;
        }

        void Update()
        {
            if (suspended || !GameEvents.IsPlaying) return;
            float dt = Time.deltaTime;
            if (fixTarget != null)
            {
                if (YawToward(fixTarget.position, out float want)) yaw = Mathf.MoveTowards(yaw, want, lookAtSpeed * dt);
            }
            else if (overriding)
            {
                yaw = Mathf.MoveTowards(yaw, overrideYaw, lookAtSpeed * dt);
                if (Mathf.Approximately(yaw, overrideYaw)) overrideTimer -= dt;
                if (overrideTimer <= 0f) { overriding = false; nextPause = Random.Range(pauseEvery.x, pauseEvery.y); }
            }
            else if (sweeping)
            {
                if (holdTimer > 0f)
                {
                    holdTimer -= dt;
                }
                else
                {
                    float target = dir > 0f ? maxYaw : -maxYaw;
                    yaw = Mathf.MoveTowards(yaw, target, speed * dt);
                    if (Mathf.Approximately(yaw, target))
                    {
                        dir = -dir;
                        speed = Random.Range(speedRange.x, speedRange.y);
                    }
                    nextPause -= dt;
                    if (nextPause <= 0f)
                    {
                        holdTimer = Random.Range(holdRange.x, holdRange.y);
                        nextPause = Random.Range(pauseEvery.x, pauseEvery.y);
                        speed = Random.Range(speedRange.x, speedRange.y);
                    }
                }
            }
            transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up) * baseLocal;
        }
    }
}
