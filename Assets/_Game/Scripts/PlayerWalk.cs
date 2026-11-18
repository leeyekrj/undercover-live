using UnityEngine;
using UnityEngine.InputSystem;

namespace UndercoverLive
{
    // F: 일어서기/앉기. 서 있을 때만 WASD로 걷는다 (기말판 시험용). 앉으면 원래 자리로 돌아간다.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerWalk : MonoBehaviour
    {
        public Transform head;
        public SeatedLook look;
        public float walkSpeed = 1.8f;
        public float seatedEye = 1.22f, standingEye = 1.62f;
        public Vector3 standOffset = new Vector3(0f, 0f, 0.75f);   // 앞줄 의자 사이 빈 곳으로 한 걸음

        public bool Standing { get; private set; }
        CharacterController cc;
        Vector3 seatPos;
        float vy, eyeY;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            cc.height = 1.7f; cc.radius = 0.25f; cc.center = new Vector3(0, 0.86f, 0);
            cc.enabled = false;   // 앉아 있을 땐 꺼 둔다 (의자 콜라이더와 겹침)
            seatPos = transform.position;
            eyeY = seatedEye;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.fKey.wasPressedThisFrame) Toggle();

            if (Standing)
            {
                Vector2 input = Vector2.zero;
                if (kb.wKey.isPressed) input.y += 1;
                if (kb.sKey.isPressed) input.y -= 1;
                if (kb.dKey.isPressed) input.x += 1;
                if (kb.aKey.isPressed) input.x -= 1;
                float yaw = head != null ? head.eulerAngles.y : transform.eulerAngles.y;
                Vector3 move = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y);
                if (move.sqrMagnitude > 1f) move.Normalize();
                vy = cc.isGrounded ? -1f : vy - 9.8f * Time.deltaTime;
                cc.Move((move * walkSpeed + Vector3.up * vy) * Time.deltaTime);
            }

            float target = Standing ? standingEye : seatedEye;
            eyeY = Mathf.MoveTowards(eyeY, target, Time.deltaTime * 1.6f);
            if (head != null) head.localPosition = new Vector3(0, eyeY, 0);
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
        }
    }
}
