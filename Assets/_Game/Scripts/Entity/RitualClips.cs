using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 의례 동작 클립 묶음 (Resources/RitualClips.asset). 씬의 애니메이터 컨트롤러는 상태가 하나뿐이라,
    /// 런타임에 AnimatorOverrideController로 그 상태의 클립을 바꿔 끼운다. 클립이 빌드에 들어가도록 이 에셋이 참조한다.
    /// </summary>
    public class RitualClips : ScriptableObject
    {
        public AnimationClip sitIdle, sitClap, handRaise, praying, kneelIdle, kneelDown, standIdle, cheering, talking, walk;

        static RitualClips loaded;
        static bool tried;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { loaded = null; tried = false; }

        /// <summary>없으면 null (그때는 동작을 바꾸지 않는다).</summary>
        public static RitualClips Get()
        {
            if (!tried)
            {
                tried = true;
                loaded = Resources.Load<RitualClips>("RitualClips");
                if (loaded == null) Debug.LogWarning("[Ritual] Resources/RitualClips 없음 — 신도 동작은 앉기만 유지");
            }
            return loaded;
        }
    }
}
