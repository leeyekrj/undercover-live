using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 담당끼리 서로 모르는 연결을 잇는 곳(통합).
    /// - 거대 겹눈(EyeEntity, 담당 D)의 상태 → 괴물 울림 소리 세기(UndercoverAudio, 담당 E).
    /// </summary>
    public class Integration : MonoBehaviour
    {
        float presence;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<Integration>() != null) return;
            new GameObject("Integration").AddComponent<Integration>();
        }

        void Update()
        {
            float target = 0f;
            var e = EyeEntity.Instance;
            if (e != null)
            {
                switch (e.Current)
                {
                    case EyeEntity.State.Descend: target = 0.5f; break;
                    case EyeEntity.State.Hover: target = 0.7f; break;
                    case EyeEntity.State.Approach: target = 0.85f; break;
                    case EyeEntity.State.Close: target = 1f; break;
                    case EyeEntity.State.Retreat: target = 0.6f; break;
                    case EyeEntity.State.Ascend: target = 0.3f; break;
                    case EyeEntity.State.Ending: target = 1f; break;
                }
            }
            presence = Mathf.MoveTowards(presence, target, Time.unscaledDeltaTime * 0.8f);
            UndercoverAudio.SetEntityPresence(presence);
        }

        void OnDestroy() { UndercoverAudio.SetEntityPresence(0f); }
    }
}
