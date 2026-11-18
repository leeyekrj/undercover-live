using UnityEngine;
using UnityEngine.InputSystem;

namespace UndercoverLive
{
    // F: 일어서기/앉기. 서 있을 때만 WASD로 걷는다. 앉으면 원래 자리로 돌아간다.
    // 일어서거나 앉으면 GameEvents.RaisePlayerStood → 주변 신도·관리자 반응(CrowdReactions).
    // 무대(앞 경계선 maxZ) 위로는 못 올라간다. 붙잡히면(Locked) 움직일 수 없다.
    // GameEvents.IsPlaying이 아니면 조작을 받지 않는다.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerWalk : MonoBehaviour
    {
        public static PlayerWalk Instance { get; private set; }

        public Transform head;
        public SeatedLook look;
        public float walkSpeed = 1.8f;
        public float seatedEye = 1.22f, standingEye = 1.62f;
        public Vector3 standOffset = new Vector3(0f, 0f, 0.75f);   // 앞줄 의자 사이 빈 곳으로 한 걸음
        [Tooltip("이 z보다 앞(무대 쪽)으로는 못 간다. 무대 계단 앞면 8.5 - 몸 반지름")]
        public float maxZ = 8.2f;

        public bool Standing { get; private set; }
        /// <summary>관리자에게 붙잡힘: 걷기·일어서기/앉기 모두 막힘.</summary>
        public bool Locked { get; private set; }
        /// <summary>앉는 자리(발 위치).</summary>
        public Vector3 SeatPosition => seatPos;

        CharacterController cc;
        Vector3 seatPos;
        float vy, eyeY;

        void Awake()
        {
            Instance = this;
            cc = GetComponent<CharacterController>();
            cc.height = 1.7f; cc.radius = 0.25f; cc.center = new Vector3(0, 0.86f, 0);
            cc.stepOffset = 0.25f;   // 무대 계단(0.4)을 못 넘게
            cc.enabled = false;   // 앉아 있을 땐 꺼 둔다 (의자 콜라이더와 겹침)
            seatPos = transform.position;
            eyeY = seatedEye;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && GameEvents.IsPlaying && !Locked)
            {
                if (kb.fKey.wasPressedThisFrame) Toggle();
                if (Standing) Move(kb);
            }

            float target = Standing ? standingEye : seatedEye;
            float dt = GameEvents.IsPlaying ? Time.deltaTime : 0f;
            eyeY = Mathf.MoveTowards(eyeY, target, dt * 1.6f);
            if (head != null) head.localPosition = new Vector3(0, eyeY, 0);
        }

        void Move(Keyboard kb)
        {
            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed) input.y += 1;
            if (kb.sKey.isPressed) input.y -= 1;
            if (kb.dKey.isPressed) input.x += 1;
            if (kb.aKey.isPressed) input.x -= 1;
            float yaw = head != null ? head.eulerAngles.y : transform.eulerAngles.y;
            Vector3 move = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y);
            if (move.sqrMagnitude > 1f) move.Normalize();
            float dt = Time.deltaTime;
            vy = cc.isGrounded ? -1f : vy - 9.8f * dt;
            Vector3 v = move * walkSpeed + Vector3.up * vy;
            // 무대 앞 경계선을 넘지 않게
            float z = transform.position.z;
            if (v.z > 0f && z + v.z * dt > maxZ) v.z = Mathf.Max(0f, (maxZ - z) / Mathf.Max(dt, 1e-4f));
            cc.Move(v * dt);
            // 밀려서라도 경계선을 넘었으면 되돌린다 (무대 위·커튼 뒤 진입 방지)
            float over = transform.position.z - maxZ;
            if (over > 0.01f) cc.Move(new Vector3(0f, 0f, -over));
        }

        public void Toggle()
        {
            Standing = !Standing;
            if (Standing)
            {
                transform.position = seatPos + standOffset;
                cc.enabled = true;
            }
            else
            {
                cc.enabled = false;
                transform.position = seatPos;
            }
            if (look != null) look.freeYaw = Standing;
            GameEvents.RaisePlayerStood(Standing);
        }

        /// <summary>검문 담당이 붙잡을 때 true.</summary>
        public void SetLocked(bool locked) { Locked = locked; }
    }
}
