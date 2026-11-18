using UnityEngine;

namespace UndercoverLive
{
    // 같은 동작을 하는 신도들이 똑같이 움직이지 않게 시작 지점과 속도를 조금씩 흩는다.
    public class AnimStartOffset : MonoBehaviour
    {
        public Vector2 speedRange = new Vector2(0.85f, 1.1f);
        void Start()
        {
            var a = GetComponent<Animator>();
            if (a == null || a.runtimeAnimatorController == null) return;
            a.speed = Random.Range(speedRange.x, speedRange.y);
            a.Play(0, 0, Random.value);
        }
    }
}
