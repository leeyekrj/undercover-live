using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 상황별 키 안내 (왼쪽 아래 한 줄 칩). 지금 쓸 수 있는 키만:
    /// 맨눈 [우클릭] 폰 들기 · [E] 몰래 찍기 · [F] 일어서기 / 촬영 [우클릭] 내리기 · [4] 숨기기 · [-/+] 줌 /
    /// 몰래 [E] 몰래 끄기 · [우클릭] 폰 들기 / 서 있음 [F] 앉기 · [WASD] 걷기.
    /// 플레이 처음 60초는 계속 보이고, 그 뒤엔 상태가 바뀔 때만 4초 보였다가 흐려진다(0.35 유지).
    /// </summary>
    public class KeyHints : MonoBehaviour
    {
        const float H = 44f, Pad = 14f;
        static readonly Vector2 EyePos = new Vector2(48f, 42f), AimPos = new Vector2(68f, 42f);

        RectTransform chip;
        CanvasGroup cg;
        readonly List<GameObject> items = new List<GameObject>();
        string context;
        float playTime, showUntil;
        bool standing;
        PlayerWalk walk;
        bool walkSearched;

        struct Hint { public string key, label; public Color keyBg, keyFg; }

        void Awake()
        {
            var bg = UIKit.Box("KeyHints", HUDRoot.Instance.HintLayer, HUDDraw.Card, 22);
            chip = bg.rectTransform;
            HUDDraw.At(chip, HUDDraw.BL, HUDDraw.BL, EyePos, new Vector2(400f, H));
            cg = chip.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
        }

        void OnEnable() { GameEvents.PlayerStoodChanged += OnStood; }
        void OnDisable() { GameEvents.PlayerStoodChanged -= OnStood; }
        void OnDestroy() { GameEvents.PlayerStoodChanged -= OnStood; }
        void OnStood(bool s) { standing = s; }

        void Update()
        {
            if (GameEvents.IsPlaying) playTime += Time.deltaTime;

            // 서 있는지: 이벤트가 기본, 없으면 PlayerWalk를 읽기만 한다
            if (!walkSearched) { walk = FindAnyObjectByType<PlayerWalk>(); walkSearched = true; }
            bool st = standing || (walk != null && walk.Standing);

            var pc = PhoneController.Instance;
            var state = pc != null ? pc.State : PhoneState.Pocket;
            string ctx = state + (st ? "_stand" : "");
            if (ctx != context)
            {
                bool first = context == null;
                context = ctx;
                Rebuild(state, st);
                if (!first) showUntil = Time.time + 4f;
            }

            float target = playTime < 60f || Time.time < showUntil ? 1f : 0.35f;
            cg.alpha = Mathf.MoveTowards(cg.alpha, target, Time.unscaledDeltaTime / 0.3f);

            float blend = PhoneAppUI.Instance != null ? PhoneAppUI.Instance.Blend : 0f;
            chip.anchoredPosition = Vector2.Lerp(EyePos, AimPos, HUDDraw.EaseOut(blend));
        }

        void Rebuild(PhoneState state, bool st)
        {
            var list = new List<Hint>();
            Color cap = HUDDraw.KeyCapBg, ink = UIKit.Ink;
            switch (state)
            {
                case PhoneState.Aimed:
                    list.Add(new Hint { key = HUDText.KeyRight, label = HUDText.ActLower, keyBg = cap, keyFg = ink });
                    list.Add(new Hint { key = HUDText.KeyHide, label = HUDText.ActHide, keyBg = UIKit.Rec, keyFg = Color.white });
                    list.Add(new Hint { key = HUDText.KeyZoom, label = HUDText.ActZoom, keyBg = cap, keyFg = ink });
                    if (st) list.Add(new Hint { key = HUDText.KeyF, label = HUDText.ActSit, keyBg = cap, keyFg = ink });
                    break;
                case PhoneState.Hidden:
                    list.Add(new Hint { key = HUDText.KeyE, label = HUDText.ActSneakOff, keyBg = HUDDraw.Teal, keyFg = HUDDraw.Dark });
                    list.Add(new Hint { key = HUDText.KeyRight, label = HUDText.ActRaise, keyBg = cap, keyFg = ink });
                    if (st) list.Add(new Hint { key = HUDText.KeyF, label = HUDText.ActSit, keyBg = cap, keyFg = ink });
                    break;
                default:
                    if (st)
                    {
                        list.Add(new Hint { key = HUDText.KeyF, label = HUDText.ActSit, keyBg = UIKit.Warn, keyFg = HUDDraw.Dark });
                        list.Add(new Hint { key = HUDText.KeyWASD, label = HUDText.ActWalk, keyBg = cap, keyFg = ink });
                        list.Add(new Hint { key = HUDText.KeyRight, label = HUDText.ActRaise, keyBg = cap, keyFg = ink });
                    }
                    else
                    {
                        list.Add(new Hint { key = HUDText.KeyRight, label = HUDText.ActRaise, keyBg = cap, keyFg = ink });
                        list.Add(new Hint { key = HUDText.KeyE, label = HUDText.ActSneak, keyBg = cap, keyFg = ink });
                        list.Add(new Hint { key = HUDText.KeyF, label = HUDText.ActStand, keyBg = cap, keyFg = ink });
                    }
                    break;
            }

            foreach (var go in items) Destroy(go);
            items.Clear();
            float x = Pad;
            for (int i = 0; i < list.Count; i++)
            {
                var h = list[i];
                if (i > 0)
                {
                    var dot = UIKit.Image("Sep", chip, UIKit.Faint, UIKit.Circle(64));
                    HUDDraw.At(dot.rectTransform, HUDDraw.ML, HUDDraw.ML, new Vector2(x, 0), new Vector2(4f, 4f));
                    items.Add(dot.gameObject);
                    x += 4f + 12f;
                }
                float kw = HUDDraw.KeyCap(chip, h.key, h.keyBg, h.keyFg, x, 26f, out var krt);
                items.Add(krt.gameObject);
                x += kw + 8f;
                var t = HUDDraw.Label("L", chip, h.label, HUDDraw.FS, UIKit.Ink, false);
                float lw = HUDDraw.Width(t) + 2f;
                HUDDraw.At(t.rectTransform, HUDDraw.ML, HUDDraw.ML, new Vector2(x, 0), new Vector2(lw, 24f));
                items.Add(t.gameObject);
                x += lw + 12f;
            }
            chip.sizeDelta = new Vector2(x - 12f + Pad, H);
        }
    }
}
