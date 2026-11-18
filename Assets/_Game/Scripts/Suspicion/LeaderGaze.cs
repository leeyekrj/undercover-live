using UnityEngine;

namespace UndercoverLive
{
    /// <summary>교주 시선 훑기: Y축 좌우로 천천히 훑다가 랜덤하게 멈춰 응시.</summary>
    public class LeaderGaze : MonoBehaviour
    {
        [Tooltip("타임라인이 끄고 켠다")]
        public bool sweeping = true;
        public float maxYaw = 35f;
        public Vector2 speedRange = new Vector2(12f, 20f);
        public Vector2 holdRange = new Vector2(1f, 3f);
        public Vector2 pauseEvery = new Vector2(3f, 8f);
        [Tooltip("LookAt으로 돌아가는 속도(도/초)")]
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

        public float Yaw => yaw;

        void Start()
        {
            baseLocal = transform.localRotation;
            speed = Random.Range(speedRange.x, speedRange.y);
            nextPause = Random.Range(pauseEvery.x, pauseEvery.y);
            dir = Random.value < 0.5f ? -1f : 1f;
        }

        /// <summary>지정 지점을 seconds초 동안 응시(끝나면 훑기 재개).</summary>
        public void LookAt(Vector3 point, float seconds)
        {
            Transform p = transform.parent;
            Quaternion baseWorld = p != null ? p.rotation * baseLocal : baseLocal;
            Vector3 fwd = baseWorld * Vector3.forward; fwd.y = 0f;
            Vector3 to = point - transform.position; to.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f) return;
            overrideYaw = Mathf.Clamp(Vector3.SignedAngle(fwd, to, Vector3.up), -lookAtMaxYaw, lookAtMaxYaw);
            overrideTimer = seconds;
            overriding = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (overriding)
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
