using UnityEngine;
using UnityEngine.InputSystem;

namespace UndercoverLive
{
    // 의자에 앉은 채 고개만 돌린다. 걷기 없음.
    public class SeatedLook : MonoBehaviour
    {
        public float sensitivity = 0.09f;
        public float yawLimit = 110f;         // 좌우 한계 (뒤를 완전히 돌아볼 수는 없음)
        public float pitchUp = 55f, pitchDown = 45f;
        public float aimedSensitivityScale = 0.55f;
        public bool freeYaw;                  // 일어서서 걸을 땐 한 바퀴 돌 수 있음

        float yaw, pitch;
        Quaternion baseRot;

        void Start()
        {
            baseRot = Quaternion.Euler(0, transform.parent ? 0 : transform.eulerAngles.y, 0);
            var e = transform.localEulerAngles;
            yaw = e.y > 180 ? e.y - 360 : e.y;
            pitch = e.x > 180 ? e.x - 360 : e.x;
            // 커서 잠금·풀림은 게임 단계(UIFlow)가 맡는다. Esc는 멈춤 메뉴.
        }

        void Update()
        {
            // 메뉴·도입·멈춤·엔딩 동안엔 시점이 돌지 않는다
            if (!GameEvents.IsPlaying) return;
            var mouse = Mouse.current;
            // 웹에서 잠금이 풀렸을 때 화면을 누르면 다시 잠근다 (Playing일 때만)
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            }
            if (mouse == null || Cursor.lockState != CursorLockMode.Locked) return;

            float s = sensitivity * SettingsStore.MouseSensitivity;
            var phone = PhoneController.Instance;
            if (phone != null && phone.State == PhoneState.Aimed) s *= aimedSensitivityScale * Mathf.Lerp(1f, 0.35f, phone.Zoom01);
            Vector2 d = mouse.delta.ReadValue() * s;
            yaw = freeYaw ? yaw + d.x : Mathf.Clamp(yaw + d.x, -yawLimit, yawLimit);
            pitch = Mathf.Clamp(pitch - d.y, -pitchUp, pitchDown);
            transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
        }

        // 디버그·테스트용: 특정 지점을 바라보게
        public void LookAtPoint(Vector3 world)
        {
            var dir = transform.parent ? transform.parent.InverseTransformDirection(world - transform.position) : world - transform.position;
            var q = Quaternion.LookRotation(dir);
            var e = q.eulerAngles;
            yaw = e.y > 180 ? e.y - 360 : e.y;
            pitch = e.x > 180 ? e.x - 360 : e.x;
            transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
        }
    }
}
