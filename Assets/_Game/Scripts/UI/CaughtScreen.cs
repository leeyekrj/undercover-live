using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 발각 화면(Ended) — 엔딩 1 '깨진 유리 눈'.
    /// 1) 신호가 끊긴 세로 폰 + 멈춘 마지막 채팅, 오른쪽에 눈동자님의 속삭임(타자).
    /// 2) 하얀 천이 덮이듯 화면이 하얗게 꺼진다(0.8초). 3) 하얀 바탕 위에 "방송이 종료되었습니다"·압수 내역·다시 하기.
    /// 압수 사유에 "렌즈 너머"가 들어 있으면 역관측 변형: 거대한 동공이 화면을 덮었다가 닫히고, 검은 바탕에 "눈이 마주쳤습니다".
    /// 마지막 채팅은 StreamFeed.RecentLines(담당 B)가 생기기 전까지 압수 사유 + UIStrings.CaughtChatFallback.
    /// 연출 중 클릭·Enter·Space = 끝 화면으로 건너뛰기. R = 다시 하기, Esc = 메인으로. 서체 크기: 56 · 28 · 18 · 15.
    /// </summary>
    public class CaughtScreen : MonoBehaviour
    {
        public static CaughtScreen Instance { get; private set; }
        public bool IsShown { get; private set; }

        Canvas canvas;
        RectTransform root, shake, chatRoot, ghostBox, cloth, afterRoot, eyeRoot;
        CanvasGroup group, phoneGroup, beforeGroup, flashGroup, afterGroup, eyeGroup;
        Image eyeIris, eyePupil;
        Text recText, whisper, badgeText;
        RectTransform speakerChip;
        KeyNav nav;
        readonly List<CanvasGroup> chatRows = new List<CanvasGroup>();
        readonly List<Image> noise = new List<Image>();
        float noiseTimer;
        bool reverse, finished;
        readonly System.Random rng = new System.Random(7);

        void Awake()
        {
            Instance = this;
            canvas = UIKit.Canvas("CaughtCanvas", 190, transform);
            root = (RectTransform)canvas.transform;
            group = UIParts.Group(canvas.gameObject, 0f);
            nav = KeyNav.On(canvas);
            Build();
            canvas.gameObject.SetActive(false);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ---------- 구성 ----------

        void Build()
        {
            var bg = UIKit.Image("Dim", root, new Color(0.031f, 0.031f, 0.039f, 0.88f));
            UIKit.Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            var skipBtn = bg.gameObject.AddComponent<Button>();
            skipBtn.transition = Selectable.Transition.None;
            var nv = skipBtn.navigation; nv.mode = Navigation.Mode.None; skipBtn.navigation = nv;
            skipBtn.onClick.AddListener(SkipToEnd);
            var edge = UIKit.Image("RedEdge", root, UIKit.A(UIKit.Rec, 0.35f), UIKit.EdgeVignette(128, 0.5f));
            UIKit.Stretch(edge.rectTransform);

            shake = UIKit.Rect("Shake", root);
            UIKit.Stretch(shake);
            BuildPhone(shake);
            BuildBefore(shake);

            var flash = UIKit.Image("Flash", root, UIKit.A(UIKit.Rec, 0.45f));
            UIKit.Stretch(flash.rectTransform);
            flashGroup = UIParts.Group(flash.gameObject, 0f);

            // 하얀 천: 위에서 내려온다 (아래 가장자리는 부드럽게)
            cloth = UIKit.Rect("WhiteCloth", root);
            cloth.anchorMin = new Vector2(0, 0); cloth.anchorMax = new Vector2(1, 1); cloth.pivot = new Vector2(0.5f, 0.5f);
            cloth.offsetMin = cloth.offsetMax = Vector2.zero;
            var solid = UIKit.Image("Solid", cloth, UIKit.Hex("#F4F2EE"));
            UIKit.Stretch(solid.rectTransform);
            var fold = UIKit.Image("Edge", cloth, UIKit.Hex("#F4F2EE"), UIParts.VGradient(new[] { 0f, 1f }, new[] { 1f, 0f }));
            var frt = fold.rectTransform;
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 0); frt.pivot = new Vector2(0.5f, 1f);
            frt.anchoredPosition = Vector2.zero; frt.sizeDelta = new Vector2(0, 180);
            cloth.gameObject.SetActive(false);

            // 역관측: 거대한 동공
            eyeRoot = UIKit.Rect("Eye", root);
            eyeRoot.anchorMin = eyeRoot.anchorMax = new Vector2(0.5f, 0.5f);
            eyeRoot.sizeDelta = Vector2.zero;
            eyeGroup = UIParts.Group(eyeRoot.gameObject, 0f);
            eyeIris = UIKit.Image("Iris", eyeRoot, UIKit.Hex("#6B0F14"), UIKit.Circle(256));
            eyeIris.rectTransform.sizeDelta = new Vector2(900, 900);
            eyePupil = UIKit.Image("Pupil", eyeRoot, UIKit.Hex("#050405"), UIKit.Circle(256));
            eyePupil.rectTransform.sizeDelta = new Vector2(300, 300);
            eyeRoot.gameObject.SetActive(false);

            afterRoot = UIKit.Rect("After", root);
            UIKit.Stretch(afterRoot);
            afterGroup = UIParts.Group(afterRoot.gameObject, 0f);
            eyeRoot.SetAsLastSibling();   // 동공은 끝 화면 위에서 닫힌다
        }

        void BuildPhone(Transform t)
        {
            var phone = UIParts.Panel("Phone", t, UIKit.Hex("#0E0F14"), 28, UIKit.A(UIKit.Rec, 0.55f), 2f);
            UIKit.Place(phone.rectTransform, 220, 144, 440, 792);
            phoneGroup = UIParts.Group(phone.gameObject);
            var p = phone.transform;

            UIParts.Pill(p, UIStrings.SignalLost, 15, UIKit.Rec, UIKit.A(UIKit.Rec, 0.14f), UIKit.A(UIKit.Rec, 0.5f), true, 24, 20, 30, 10, 6);
            UIParts.Label(p, "LIVE 0", 15, UIParts.Ink(0.62f), true, TextAnchor.MiddleCenter, 180, 20, 90, 30);
            recText = UIParts.Label(p, "", 15, UIParts.Ink(0.62f), false, TextAnchor.MiddleRight, 260, 20, 156, 30);

            var screen = UIParts.Panel("Screen", p, UIParts.ScreenBg, 18, UIParts.Ink(0.06f));
            UIKit.Place(screen.rectTransform, 16, 64, 408, 400);
            var mask = UIParts.RoundMask("Clip", screen.transform, 18);
            for (int i = 0; i < 14; i++) noise.Add(UIParts.Bar("Noise" + i, mask, UIParts.Ink(0.05f), 0, 0, 408, 2));
            ghostBox = UIParts.Line("Ghost", mask, UIKit.A(UIKit.Accent, 0.35f), 2, 1.5f).rectTransform;
            ghostBox.anchorMin = ghostBox.anchorMax = new Vector2(0.5f, 0.5f);
            ghostBox.sizeDelta = new Vector2(150, 150);
            ghostBox.anchoredPosition = new Vector2(10, 20);
            ghostBox.localRotation = Quaternion.Euler(0, 0, 7f);
            UIParts.Label(mask, UIStrings.NoSignal, 18, UIKit.Ink, true, TextAnchor.MiddleCenter, 0, 296, 408, 28);
            UIParts.Label(mask, UIStrings.NoSignalSub, 15, UIParts.Ink(0.56f), false, TextAnchor.MiddleCenter, 0, 328, 408, 24);
            RandomizeNoise();

            chatRoot = UIKit.Rect("Chat", p);
            UIKit.Place(chatRoot, 24, 488, 392, 280);
        }

        void BuildBefore(Transform t)
        {
            var r = UIKit.Rect("Before", t);
            UIKit.Stretch(r);
            beforeGroup = UIParts.Group(r.gameObject, 0f);
            var badge = UIParts.Pill(r, UIStrings.CaughtBadge, 15, UIParts.RecSoft, UIKit.A(UIKit.Rec, 0.16f), UIKit.A(UIKit.Rec, 0.5f), true, 760, 300, 36, 14, -1, UIKit.Rec);
            badgeText = badge.GetComponentInChildren<Text>();
            speakerChip = UIParts.Pill(r, UIStrings.LeaderTitle, 15, UIKit.Accent, UIKit.A(UIKit.Accent, 0.14f), UIKit.A(UIKit.Accent, 0.45f), true, 760, 372, 30, 12, 6);
            whisper = UIParts.Label(r, "", 28, UIParts.GoldBright, true, TextAnchor.UpperLeft, 760, 420, 900, 160);
            whisper.lineSpacing = 1.25f;
        }

        // 끝 화면: 하얀 바탕(보통) 또는 검은 바탕(역관측)
        void BuildAfter(string reason, int clips)
        {
            foreach (Transform c in afterRoot) Destroy(c.gameObject);
            bool light = !reverse;
            Color ink = light ? UIParts.Dark : UIKit.Ink;
            Color sub = light ? new Color(0.043f, 0.043f, 0.055f, 0.72f) : UIParts.Ink(0.72f);
            Color label = light ? UIKit.Hex("#C8323A") : UIParts.RecSoft;
            Color line = light ? new Color(0, 0, 0, 0.16f) : UIParts.Ink(0.2f);
            if (!light)
            {
                var black = UIKit.Image("Black", afterRoot, UIKit.Hex("#050405"));
                UIKit.Stretch(black.rectTransform);
            }

            const float x = 520f, w = 880f;
            UIParts.Pill(afterRoot, reverse ? UIStrings.ReverseGazeBadge : UIStrings.CaughtBadge, 15, label, UIKit.A(label, 0.1f), UIKit.A(label, 0.45f), true, x, 292, 36, 14, -1, label);
            UIParts.Label(afterRoot, reverse ? UIStrings.ReverseGazeTitle : UIStrings.CaughtTitle, 56, ink, true, TextAnchor.MiddleLeft, x - 4, 340, w, 76);
            var body = UIParts.Label(afterRoot, reverse ? UIStrings.ReverseGazeBody : UIStrings.CaughtBody, 18, sub, false, TextAnchor.UpperLeft, x, 428, w, 60);
            body.lineSpacing = 1.25f;

            UIParts.Bar("Div", afterRoot, line, x, 508, w, 1);
            Row(x, 524, UIStrings.CaughtReasonLabel, reason, label, sub, w);
            Row(x, 560, UIStrings.CaughtLostLabel, string.Format(UIStrings.CaughtLostFormat, clips), label, sub, w);
            Row(x, 596, UIStrings.CaughtTipLabel, UIStrings.CaughtTip, label, sub, w);

            var again = UIParts.Button("Again", afterRoot, x, 664, 220, 56, UIKit.Rec, new Color(0, 0, 0, 0), 10, () => UIFlow.Restart(true));
            UIParts.Key(again.transform, "R", 15, UIKit.Ink, UIKit.A(Color.white, 0.18f), UIKit.A(Color.white, 0.3f), 22, 14, 28, 30);
            UIParts.Label(again.transform, UIStrings.ResultsRestart, 18, UIKit.Ink, true, TextAnchor.MiddleLeft, 66, 0, 140, 56);
            var main = UIParts.Button("Main", afterRoot, x + 236, 664, 200, 56, UIKit.A(ink, 0f), UIKit.A(ink, 0.3f), 10, () => UIFlow.Restart(false));
            UIParts.Key(main.transform, "Esc", 15, ink, UIKit.A(ink, 0.08f), UIKit.A(ink, 0.2f), 20, 14, 28, 44);
            UIParts.Label(main.transform, UIStrings.ResultsMain, 18, ink, true, TextAnchor.MiddleLeft, 78, 0, 110, 56);
            UIParts.Label(afterRoot, UIStrings.NavHint, 15, UIKit.A(ink, 0.5f), false, TextAnchor.MiddleLeft, x, 736, w, 24);

            nav.RingColor = light ? UIParts.Dark : UIKit.Accent;
            nav.GlowColor = light ? new Color(1, 1, 1, 0.14f) : UIParts.Ink(0.1f);
            nav.Add(again, 10);
            nav.Add(main, 10);
        }

        void Row(float x, float y, string label, string value, Color labelCol, Color valueCol, float w)
        {
            UIParts.Label(afterRoot, label, 15, labelCol, true, TextAnchor.MiddleLeft, x, y, 120, 28);
            var v = UIParts.Label(afterRoot, "", 15, valueCol, false, TextAnchor.MiddleLeft, x + 120, y, w - 120, 28);
            UIParts.FitOneLine(v, value, w - 120);
        }

        // ---------- 보이기 ----------

        /// <summary>발각 화면을 띄운다(한 번만). 결과 화면이 이미 떠 있으면 무시.</summary>
        public void Show(string reason)
        {
            if (IsShown) return;
            var rs = FindAnyObjectByType<ResultsScreen>();
            if (rs != null && rs.IsShown) return;
            IsShown = true;
            if (string.IsNullOrEmpty(reason)) reason = UIStrings.CaughtDefaultReason;
            reverse = !string.IsNullOrEmpty(UIStrings.ReverseGazeKey) && reason.Contains(UIStrings.ReverseGazeKey);

            group.alpha = 0f;
            canvas.gameObject.SetActive(true);
            float rec = Mathf.Max(0f, Time.time - UIFlow.PlayStartTime);
            recText.text = "REC 중단 " + UIParts.Clock(rec);
            if (badgeText != null) badgeText.text = reverse ? UIStrings.ReverseGazeBadge : UIStrings.CaughtBadge;
            speakerChip.gameObject.SetActive(!reverse);
            BuildChat(reason);
            BuildAfter(reason, CaptureLog.Clips.Count);

            GameEvents.SetPhase(GamePhase.Ended);
            StartCoroutine(reverse ? ReverseSequence() : ClothSequence());
        }

        void BuildChat(string reason)
        {
            foreach (Transform c in chatRoot) Destroy(c.gameObject);
            chatRows.Clear();
            var lines = new List<string>();
            // 실제 방송 채팅 마지막 줄들(StreamFeed.RecentLines, "닉네임: 내용") — 없으면 대체 문구
            var feed = StreamFeed.Instance;
            if (feed != null && feed.RecentLines != null && feed.RecentLines.Count > 0)
            {
                foreach (var l in feed.RecentLines)
                {
                    int k = l.IndexOf(": ");
                    lines.Add(k > 0 ? l.Substring(0, k) + "|" + l.Substring(k + 2) : l);
                }
            }
            else foreach (var l in UIStrings.CaughtChatFallback) lines.Add(l);
            lines.Add(UIStrings.SystemNick + "|" + reason);
            lines.Add(UIStrings.SystemNick + "|" + UIStrings.CaughtSystemLine);
            const float rowH = 36f;
            int n = Mathf.Min(lines.Count, 7);
            float y0 = 280f - n * rowH;
            for (int i = 0; i < n; i++)
            {
                var parts = lines[lines.Count - n + i].Split('|');
                string nick = parts.Length > 1 ? parts[0] : "";
                string msg = parts.Length > 1 ? parts[1] : parts[0];
                bool sys = nick == UIStrings.SystemNick;
                var row = UIKit.Rect("Line" + i, chatRoot);
                UIKit.Place(row, 0, y0 + i * rowH, 392, rowH);
                var g = UIParts.Group(row.gameObject, 0f);
                var nt = UIParts.Label(row, sys ? "[" + nick + "]" : nick, 15, sys ? UIKit.Rec : UIKit.Accent, true, TextAnchor.MiddleLeft, 0, 0, 160, rowH);
                nt.horizontalOverflow = HorizontalWrapMode.Overflow;
                float nw = Mathf.Ceil(UIParts.Width(nt)) + 8f;
                var mt = UIParts.Label(row, "", 15, sys ? UIKit.Rec : UIParts.Ink(0.9f), sys, TextAnchor.MiddleLeft, nw, 0, 392 - nw, rowH);
                UIParts.FitOneLine(mt, msg, 392 - nw);
                chatRows.Add(g);
            }
        }

        // 0~1초: 번쩍임·흔들림·채팅이 찍힘·오른쪽 등장
        void Opening()
        {
            group.alpha = 1f;
            beforeGroup.alpha = 0f;
            StartCoroutine(UIParts.Anim(0.35f, k => flashGroup.alpha = 1f - k));
            StartCoroutine(UIParts.Anim(0.3f, k =>
            {
                float a = (1f - k) * 10f;
                shake.anchoredPosition = new Vector2(((float)rng.NextDouble() * 2f - 1f) * a, ((float)rng.NextDouble() * 2f - 1f) * a);
            }, 0f, () => shake.anchoredPosition = Vector2.zero));
            for (int i = 0; i < chatRows.Count; i++)
            {
                var g = chatRows[i];
                StartCoroutine(UIParts.Anim(0.12f, k => g.alpha = k, 0.2f + i * 0.07f));
            }
            var rt = (RectTransform)beforeGroup.transform;
            StartCoroutine(UIParts.Anim(0.5f, k =>
            {
                beforeGroup.alpha = k;
                rt.anchoredPosition = new Vector2(16f * (1f - k), 0);
            }, 0.5f));
        }

        IEnumerator ClothSequence()
        {
            Opening();
            yield return UIParts.Type(whisper, UIStrings.CaughtWhisper, 26f, 0.9f);
            yield return Wait(0.7f);
            // 하얀 천이 덮이듯 화면이 하얗게 꺼진다 (0.8초)
            cloth.gameObject.SetActive(true);
            float h = root.rect.height + 200f;
            yield return UIParts.Anim(0.8f, k =>
            {
                float e = UIParts.EaseInOut(k);
                cloth.anchoredPosition = new Vector2(0f, Mathf.Lerp(h, 0f, e));
            });
            cloth.anchoredPosition = Vector2.zero;
            phoneGroup.alpha = 0f; beforeGroup.alpha = 0f;
            yield return Wait(0.15f);
            ShowAfter(0.4f);
        }

        IEnumerator ReverseSequence()
        {
            Opening();
            yield return UIParts.Type(whisper, UIStrings.ReverseGazeLine, 24f, 0.9f);
            yield return Wait(0.5f);
            // 거대한 동공이 떠올라 화면을 덮는다
            eyeRoot.gameObject.SetActive(true);
            eyeIris.rectTransform.sizeDelta = new Vector2(900, 900);
            eyePupil.rectTransform.sizeDelta = new Vector2(260, 260);
            yield return UIParts.Anim(0.25f, k => { eyeGroup.alpha = k; eyeRoot.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, k); });
            float cover = Mathf.Max(root.rect.width, root.rect.height) * 1.35f;
            yield return UIParts.Anim(0.7f, k =>
            {
                float e = k * k;   // 점점 빨리 열림
                eyePupil.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(260f, cover, e);
                eyeIris.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(900f, cover * 1.3f, e);
            });
            // 덮인 동안 끝 화면으로 바꾼다
            phoneGroup.alpha = 0f; beforeGroup.alpha = 0f;
            afterGroup.alpha = 1f;
            yield return Wait(0.25f);
            // 동공이 닫히며 사라진다
            yield return UIParts.Anim(0.45f, k =>
            {
                float e = UIParts.EaseInOut(k);
                eyePupil.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(cover, 0f, e);
                eyeIris.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(cover * 1.3f, 0f, e);
                eyeGroup.alpha = 1f - e * e;
            });
            eyeRoot.gameObject.SetActive(false);
            Finish();
        }

        void ShowAfter(float dur)
        {
            StartCoroutine(UIParts.Anim(dur, k => afterGroup.alpha = k, 0f, Finish));
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            afterGroup.alpha = 1f;
            nav.Select(0);
            nav.Live = true;
        }

        /// <summary>연출을 건너뛰고 끝 화면으로.</summary>
        void SkipToEnd()
        {
            if (!IsShown || finished) return;
            StopAllCoroutines();
            flashGroup.alpha = 0f;
            shake.anchoredPosition = Vector2.zero;
            phoneGroup.alpha = 0f; beforeGroup.alpha = 0f;
            eyeRoot.gameObject.SetActive(false);
            if (!reverse) { cloth.gameObject.SetActive(true); cloth.anchoredPosition = Vector2.zero; }
            group.alpha = 1f;
            Finish();
        }

        static IEnumerator Wait(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
        }

        void RandomizeNoise()
        {
            for (int i = 0; i < noise.Count; i++)
            {
                var n = noise[i];
                float y = (float)rng.NextDouble() * 396f;
                float h = 1f + (float)rng.NextDouble() * 3f;
                float x = -(float)rng.NextDouble() * 60f;
                UIKit.Place(n.rectTransform, x, y, 468, h);
                n.color = i % 5 == 0 ? UIKit.A(UIKit.Rec, 0.10f + (float)rng.NextDouble() * 0.12f) : UIParts.Ink(0.03f + (float)rng.NextDouble() * 0.09f);
            }
            if (ghostBox != null) ghostBox.anchoredPosition = new Vector2(10 + (float)rng.NextDouble() * 6f - 3f, 20f);
        }

        void Update()
        {
            if (!IsShown) return;
            noiseTimer += Time.unscaledDeltaTime;
            if (noiseTimer > 0.07f) { noiseTimer = 0f; RandomizeNoise(); }

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.rKey.wasPressedThisFrame) UIFlow.Restart(true);
            else if (kb.escapeKey.wasPressedThisFrame) UIFlow.Restart(false);
            else if (!finished && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) SkipToEnd();
        }
    }
}
