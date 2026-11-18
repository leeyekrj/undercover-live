using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UndercoverLive
{
    /// <summary>
    /// 웹 화질·검수 도우미.
    /// 1) 웹 페이지 틀이 고해상도(레티나)로 그리게 하면 UI 글자는 선명해지지만 3D 비용이 커진다.
    ///    그래서 3D만 URP 렌더 배율로 내부 해상도를 낮춘다(화면 오버레이 UI는 원래 해상도 그대로).
    /// 2) 주소에 qa=1이 있거나 에디터일 때만 검수용 키를 켠다: P = 폰 들기/내리기(우클릭 대신), O = 몰래 토글.
    ///    (웹 검수 도구는 합성 마우스 클릭이 게임에 안 들어가서 키보드로 대신한다. 일반 플레이어에겐 꺼져 있다.)
    /// </summary>
    public class WebQuality : MonoBehaviour
    {
        [Tooltip("3D를 그릴 내부 가로 해상도 목표(px). 화면이 이보다 크면 렌더 배율을 낮춘다.")]
        public float targetInternalWidth = 1600f;
        public float minScale = 0.5f;

        bool qaKeys;
        float appliedScale = -1f;
        float originalScale = 1f;
        UniversalRenderPipelineAsset urp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<WebQuality>() != null) return;
            new GameObject("WebQuality").AddComponent<WebQuality>();
        }

        void Awake()
        {
            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) originalScale = urp.renderScale;
#if UNITY_EDITOR
            qaKeys = true;
#else
            string url = Application.absoluteURL ?? "";
            qaKeys = url.Contains("qa=1");
#endif
        }

        void Update()
        {
#if !UNITY_EDITOR
            if (urp != null && Application.platform == RuntimePlatform.WebGLPlayer)
            {
                float s = Mathf.Clamp(targetInternalWidth / Mathf.Max(1, Screen.width), minScale, 1f);
                if (Mathf.Abs(s - appliedScale) > 0.02f) { urp.renderScale = s; appliedScale = s; }
            }
#endif
            if (!qaKeys || !GameEvents.IsPlaying) return;
            var kb = Keyboard.current;
            var pc = PhoneController.Instance;
            if (kb == null || pc == null) return;
            if (kb.pKey.wasPressedThisFrame) pc.SetState(pc.State == PhoneState.Aimed ? PhoneState.Pocket : PhoneState.Aimed);
            if (kb.oKey.wasPressedThisFrame) pc.SetState(pc.State == PhoneState.Hidden ? PhoneState.Pocket : PhoneState.Hidden);
        }

        void OnDestroy()
        {
            // 에디터에서 에셋 값이 바뀐 채 남지 않게 되돌린다
            if (urp != null && appliedScale > 0f) urp.renderScale = originalScale;
        }
    }
}
