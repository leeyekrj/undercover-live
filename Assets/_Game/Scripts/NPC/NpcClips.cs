using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 관리자 걷기 동작 참조(Assets/_Game/Resources/NpcClips.asset). 애니메이터 컨트롤러 에셋은 건드리지 않고
    /// StaffEscort가 PlayableGraph로 서 있기 동작과 섞어 쓴다. 없으면 위치만 옮기며 상하로 흔든다.
    /// </summary>
    [CreateAssetMenu(fileName = "NpcClips", menuName = "UndercoverLive/NPC Clips")]
    public class NpcClips : ScriptableObject
    {
        public AnimationClip walk;

        static NpcClips loaded;
        static bool tried;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { loaded = null; tried = false; }

        /// <summary>제자리 걷기 동작(anim_walking_inplace). 못 찾으면 null.</summary>
        public static AnimationClip Walk
        {
            get
            {
                if (!tried)
                {
                    tried = true;
                    loaded = Resources.Load<NpcClips>("NpcClips");
                }
                AnimationClip c = loaded != null ? loaded.walk : null;
#if UNITY_EDITOR
                if (c == null) c = EditorFallback();
#endif
                return c;
            }
        }

#if UNITY_EDITOR
        static AnimationClip editorClip;
        static AnimationClip EditorFallback()
        {
            if (editorClip != null) return editorClip;
            foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Mixamo/Animations/anim_walking_inplace.fbx"))
                if (o is AnimationClip ac && !ac.name.StartsWith("__preview__")) { editorClip = ac; break; }
            return editorClip;
        }
#endif
    }
}
