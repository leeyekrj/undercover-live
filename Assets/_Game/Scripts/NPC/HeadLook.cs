using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 사람 인물(휴머노이드 Animator)의 머리·목 뼈를 LateUpdate에서 플레이어 쪽으로 부드럽게 돌린다(몸 정면 기준 최대 60°).
    /// 여러 이유(일어섬·눈치·검문·무대)를 비트로 받아 하나라도 켜져 있으면 본다. 이유마다 시차(지연)를 줄 수 있다.
    /// 애니메이션이 뼈를 덮어쓴 뒤에만 더하므로 누적되지 않는다(화면 밖이라 애니메이터가 멈춰도 원래 자세로 되돌림).
    /// </summary>
    [DisallowMultipleComponent]
    public class HeadLook : MonoBehaviour
    {
        public const int Stand = 1, Notice = 2, Escort = 4, Stage = 8;
        const int ReasonCount = 4;

        [Tooltip("몸 정면 기준 최대 좌우 각도")] public float maxYaw = 60f;
        [Tooltip("최대 위아래 각도")] public float maxPitch = 35f;
        [Range(0f, 1f), Tooltip("목이 맡는 비율(나머지는 머리)")] public float neckShare = 0.3f;
        [Tooltip("돌아보는 데 걸리는 시간(초)")] public float turnTime = 0.45f;
        [Tooltip("비면 플레이어 눈(PhoneController.Head)")] public Transform target;

        public bool Looking => mask != 0;
        public float Weight => weight;

        Animator anim;
        Transform head, neck, body;
        Vector3 headFwdLocal, neckFwdLocal;
        bool axesReady;
        int mask;
        float weight;
        readonly float[] pendingLeft = new float[ReasonCount];
        readonly bool[] pendingOn = new bool[ReasonCount];
        readonly bool[] pendingActive = new bool[ReasonCount];

        // 누적 방지: 애니메이터가 이번 프레임에 뼈를 안 덮어썼으면 지난 애니메이션 자세로 되돌린다
        Quaternion animHead, animNeck, wroteHead, wroteNeck;
        bool wrote;

        /// <summary>사람 인물(루트)에서 휴머노이드 Animator를 찾아 HeadLook을 붙여 돌려준다. 사람이 아니면 null.</summary>
        public static HeadLook For(Transform person)
        {
            if (person == null) return null;
            var a = person.GetComponentInChildren<Animator>(true);
            if (a == null || a.avatar == null || !a.avatar.isHuman) return null;
            var h = a.GetComponent<HeadLook>();
            if (h == null) h = a.gameObject.AddComponent<HeadLook>();
            return h;
        }

        void Awake()
        {
            anim = GetComponent<Animator>();
            body = transform;
            if (anim != null && anim.isHuman)
            {
                head = anim.GetBoneTransform(HumanBodyBones.Head);
                neck = anim.GetBoneTransform(HumanBodyBones.Neck);
            }
        }

        /// <summary>이유(비트)를 바로 켜거나 끈다.</summary>
        public void Set(int reason, bool on)
        {
            int i = Index(reason);
            if (i >= 0) pendingActive[i] = false;
            if (on) mask |= reason; else mask &= ~reason;
        }

        /// <summary>delay초 뒤에 켜거나 끈다(시차를 두고 돌아보기). 멈춤 중엔 시간이 안 흐른다.</summary>
        public void Request(int reason, bool on, float delay)
        {
            if (delay <= 0f) { Set(reason, on); return; }
            int i = Index(reason);
            if (i < 0) return;
            pendingActive[i] = true;
            pendingOn[i] = on;
            pendingLeft[i] = delay;
        }

        static int Index(int reason)
        {
            switch (reason)
            {
                case Stand: return 0;
                case Notice: return 1;
                case Escort: return 2;
                case Stage: return 3;
            }
            return -1;
        }

        void Update()
        {
            float dt = GameEvents.IsPlaying ? Time.deltaTime : 0f;
            if (dt <= 0f) return;
            for (int i = 0; i < ReasonCount; i++)
            {
                if (!pendingActive[i]) continue;
                pendingLeft[i] -= dt;
                if (pendingLeft[i] > 0f) continue;
                pendingActive[i] = false;
                int bit = 1 << i;
                if (pendingOn[i]) mask |= bit; else mask &= ~bit;
            }
            weight = Mathf.MoveTowards(weight, mask != 0 ? 1f : 0f, dt / Mathf.Max(0.05f, turnTime));
        }

        Vector3 TargetPoint()
        {
            if (target != null) return target.position;
            var pc = PhoneController.Instance;
            if (pc != null && pc.Head != null) return pc.Head.position;
            var cam = Camera.main;
            return cam != null ? cam.transform.position : body.position + body.forward;
        }

        void LateUpdate()
        {
            if (head == null) return;

            // 애니메이터가 이번 프레임 뼈를 안 썼으면(화면 밖 등) 우리가 쓴 값이 남아 있다 → 애니메이션 자세로 되돌림
            if (wrote)
            {
                bool untouched = Quaternion.Angle(head.localRotation, wroteHead) < 0.01f &&
                                 (neck == null || Quaternion.Angle(neck.localRotation, wroteNeck) < 0.01f);
                if (untouched)
                {
                    head.localRotation = animHead;
                    if (neck != null) neck.localRotation = animNeck;
                }
                wrote = false;
            }

            if (!axesReady)
            {
                headFwdLocal = Quaternion.Inverse(head.rotation) * body.forward;
                if (neck != null) neckFwdLocal = Quaternion.Inverse(neck.rotation) * body.forward;
                axesReady = true;
            }
            if (weight <= 0.001f) return;

            animHead = head.localRotation;
            if (neck != null) animNeck = neck.localRotation;

            float w = weight * weight * (3f - 2f * weight);
            Vector3 tp = TargetPoint();
            if (neck != null) Turn(neck, neckFwdLocal, tp, w * neckShare);
            Turn(head, headFwdLocal, tp, w);

            wroteHead = head.localRotation;
            if (neck != null) wroteNeck = neck.localRotation;
            wrote = true;
        }

        void Turn(Transform bone, Vector3 fwdLocal, Vector3 tp, float amount)
        {
            Vector3 cur = bone.rotation * fwdLocal;
            Vector3 to = tp - bone.position;
            if (to.sqrMagnitude < 1e-4f) return;
            Vector3 local = body.InverseTransformDirection(to.normalized);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
            yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
            Vector3 want = body.TransformDirection(Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward);
            Quaternion delta = Quaternion.FromToRotation(cur, want);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, delta, amount) * bone.rotation;
        }
    }
}
