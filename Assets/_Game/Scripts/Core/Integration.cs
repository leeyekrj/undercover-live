using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 담당끼리 서로 모르는 연결을 잇는 곳(통합).
    /// - 거대 겹눈(EyeEntity, 담당 D)의 상태 → 괴물 울림 소리 세기(UndercoverAudio, 담당 E), 다가올 때 경고 배너(담당 B).
    /// </summary>
    public class Integration : MonoBehaviour
    {
        float presence;
        EyeEntity.State lastState = EyeEntity.State.Hidden;

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
                // 거대 겹눈이 렌즈 쪽으로 내려오기 시작하면 분명한 경고 (일반 '쳐다봄' 경고와 구분) — 11-18 검수
                if (e.Current != lastState)
                {
                    if (e.Current == EyeEntity.State.Approach) GameEvents.RaiseWarning("그것이 렌즈 쪽으로 내려와요");
                    else if (e.Current == EyeEntity.State.Close) GameEvents.RaiseWarning("눈이 렌즈를 들여다봐요 — 지금 폰을 넣어요");
                    lastState = e.Current;
                }
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
