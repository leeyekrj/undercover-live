using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 발각 화면(Ended). 왼쪽 = 신호가 끊긴 세로 폰 + 멈춘 마지막 채팅, 오른쪽 = "방송이 종료되었습니다"·압수 내역·다시 하기.
    /// 0~0.2초 붉은 번쩍임·흔들림 → 0.2~0.6초 채팅이 찍힘 → 0.5~1.0초 오른쪽이 나타남. 신호 끊김은 가로 줄무늬 판 몇 개로.
    /// 마지막 채팅은 StreamFeed.RecentLines(담당 B)가 아직 없어 Confiscated 사유와 UIStrings.CaughtChatFallback로 채운다.
    /// 서체 크기 4종: 56 · 18 · 15 · 13.
    /// </summary>
    public class CaughtScreen : MonoBehaviour
    {
        public static CaughtScreen Instance { get; private set; }
        public bool IsShown { get; private set; }

        Canvas canvas;
        RectTransform root, shake;
        CanvasGroup group, rightGroup, flashGroup;
        readonly List<CanvasGroup> chatRows = new List<CanvasGroup>();
        readonly List<Image> noise = new List<Image>();
        RectTransform chatRoot, ghostBox;
        Text reasonText, lostText, recText;
        float noiseTimer;
        System.Random rng = new System.Random(7);

        void Awake()
        {
            Instance = this;
            canvas = UIKit.Canvas("CaughtCanvas", 120, transform);
            root = (RectTransform)canvas.transform;
            group = UIParts.Group(canvas.gameObject, 0f);
            Build();
            canvas.gameObject.SetActive(false);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build()
        {
            var bg = UIKit.Image("Dim", root, new Color(0.031f, 0.031f, 0.039f, 0.86f));
            UIKit.Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            var edge = UIKit.Image("RedEdge", root, UIKit.A(UIKit.Rec, 0.35f), UIKit.EdgeVignette(128, 0.5f));
            UIKit.Stretch(edge.rectTransform);

            shake = UIKit.Rect("Shake", root);
            UIKit.Stretch(shake);

            BuildPhone(shake);
            BuildRight(shake);

            var flash = UIKit.Image("Flash", root, UIKit.A(UIKit.Rec, 0.45f));
            UIKit.Stretch(flash.rectTransform);
            flashGroup = UIParts.Group(flash.gameObject, 0f);
        }

        void BuildPhone(Transform t)
        {
            var phone = UIParts.Panel("Phone", t, UIKit.Hex("#0E0F14"), 28, UIKit.A(UIKit.Rec, 0.55f), 2f);
            UIKit.Place(phone.rectTransform, 220, 144, 440, 792);
            var p = phone.transform;

            // 상태 줄
            UIParts.Pill(p, UIStrings.SignalLost, 13, UIKit.Rec, UIKit.A(UIKit.Rec, 0.14f), UIKit.A(UIKit.Rec, 0.5f), true, 24, 22, 26, 10, 6);
            UIParts.Label(p, "LIVE 0", 13, UIParts.Ink(0.56f), true, TextAnchor.MiddleCenter, 170, 22, 100, 26);
            recText = UIParts.Label(p, "", 13, UIParts.Ink(0.56f), false, TextAnchor.MiddleRight, 260, 22, 156, 26);

            // 화면: 줄무늬 노이즈 + 기울어진 초점 상자 잔상 + 신호 없음
            var screen = UIParts.Panel("Screen", p, UIParts.ScreenBg, 18, UIParts.Ink(0.06f));
            UIKit.Place(screen.rectTransform, 16, 64, 408, 400);
            var mask = UIParts.RoundMask("Clip", screen.transform, 18);
            for (int i = 0; i < 14; i++)
            {
                var n = UIParts.Bar("Noise" + i, mask, UIParts.Ink(0.05f), 0, 0, 408, 2);
                noise.Add(n);
            }
            ghostBox = UIParts.Line("Ghost", mask, UIKit.A(UIKit.Accent, 0.35f), 2, 1.5f).rectTransform;
            ghostBox.anchorMin = ghostBox.anchorMax = new Vector2(0.5f, 0.5f);
            ghostBox.sizeDelta = new Vector2(150, 150);
            ghostBox.anchoredPosition = new Vector2(10, 20);
            ghostBox.localRotation = Quaternion.Euler(0, 0, 7f);
            UIParts.Label(mask, UIStrings.NoSignal, 18, UIKit.Ink, true, TextAnchor.MiddleCenter, 0, 300, 408, 28);
            UIParts.Label(mask, UIStrings.NoSignalSub, 13, UIParts.Ink(0.52f), false, TextAnchor.MiddleCenter, 0, 330, 408, 22);
            RandomizeNoise();

            // 멈춘 채팅
            chatRoot = UIKit.Rect("Chat", p);
            UIKit.Place(chatRoot, 24, 488, 392, 280);
        }

        void BuildRight(Transform t)
        {
            var r = UIKit.Rect("Right", t);
            UIKit.Stretch(r);
            rightGroup = UIParts.Group(r.gameObject, 0f);

            UIParts.Pill(r, UIStrings.CaughtBadge, 13, UIParts.RecSoft, UIKit.A(UIKit.Rec, 0.16f), UIKit.A(UIKit.Rec, 0.5f), true, 760, 252, 34, 14, -1, UIKit.Rec);
            UIParts.Label(r, UIStrings.CaughtTitle, 56, UIKit.Ink, true, TextAnchor.MiddleLeft, 756, 300, 920, 76);
            var body = UIParts.Label(r, UIStrings.CaughtBody, 18, UIParts.Ink(0.72f), false, TextAnchor.UpperLeft, 760, 388, 900, 64);
            body.lineSpacing = 1.25f;

            // 압수 경위 카드 (왼쪽 붉은 선)
            var card = UIParts.Panel("Report", r, UIParts.PanelBg, 12, UIParts.Ink(0.14f));
            UIKit.Place(card.rectTransform, 760, 488, 880, 228);
            UIParts.Bar("Accent", card.transform, UIKit.Rec, 0, 16, 4, 196);
            reasonText = ReportRow(card.transform, 24, UIStrings.CaughtReasonLabel);
            lostText = ReportRow(card.transform, 88, UIStrings.CaughtLostLabel);
            var tip = ReportRow(card.transform, 152, UIStrings.CaughtTipLabel);
            tip.text = UIStrings.CaughtTip;

            // 버튼
            var again = UIParts.Button("Again", r, 760, 752, 220, 56, UIKit.Rec, new Color(0, 0, 0, 0), 10, () => UIFlow.Restart(true));
            UIParts.Key(again.transform, "R", 13, UIKit.Ink, UIKit.A(Color.white, 0.18f), UIKit.A(Color.white, 0.3f), 24, 16, 24, 28);
            UIParts.Label(again.transform, UIStrings.ResultsRestart, 18, UIKit.Ink, true, TextAnchor.MiddleLeft, 64, 0, 140, 56);
            var main = UIParts.Button("Main", r, 996, 752, 180, 56, UIParts.Ink(0f), UIParts.Ink(0.24f), 10, () => UIFlow.Restart(false));
            UIParts.Label(main.transform, UIStrings.ResultsMain, 18, UIKit.Ink, true, TextAnchor.MiddleCenter, 0, 0, 180, 56);
        }

        Text ReportRow(Transform card, float y, string label)
        {
            UIParts.Label(card, label, 13, UIParts.RecSoft, true, TextAnchor.MiddleLeft, 32, y, 120, 24);
            var v = UIParts.Label(card, "", 15, UIParts.Ink(0.84f), false, TextAnchor.UpperLeft, 152, y + 2, 700, 52);
            v.lineSpacing = 1.2f;
            return v;
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

            reasonText.text = reason;
            int clips = CaptureLog.Clips.Count;
            lostText.text = string.Format(UIStrings.CaughtLostFormat, clips);
            float rec = Mathf.Max(0f, Time.time - UIFlow.PlayStartTime);
            recText.text = "REC 중단 " + UIParts.Clock(rec);
            group.alpha = 0f;
            canvas.gameObject.SetActive(true);
            BuildChat(reason);

            GameEvents.SetPhase(GamePhase.Ended);
            StartCoroutine(Intro());
        }

        void BuildChat(string reason)
        {
            foreach (Transform c in chatRoot) Destroy(c.gameObject);
            chatRows.Clear();
            var lines = new List<string>();
            foreach (var l in UIStrings.CaughtChatFallback) lines.Add(l);
            lines.Add(UIStrings.SystemNick + "|" + reason);
            lines.Add(UIStrings.SystemNick + "|" + UIStrings.CaughtSystemLine);
            // 아래에서부터 쌓는다(최근 줄이 아래)
            float rowH = 34f;
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

        IEnumerator Intro()
        {
            group.alpha = 1f;
            rightGroup.alpha = 0f;
            // 0~0.2초: 붉은 번쩍임 + 흔들림
            StartCoroutine(UIParts.Anim(0.35f, k => flashGroup.alpha = 1f - k));
            StartCoroutine(UIParts.Anim(0.3f, k =>
            {
                float a = (1f - k) * 10f;
                shake.anchoredPosition = new Vector2(((float)rng.NextDouble() * 2f - 1f) * a, ((float)rng.NextDouble() * 2f - 1f) * a);
            }, 0f, () => shake.anchoredPosition = Vector2.zero));
            // 0.2~0.6초: 채팅이 찍힌다
            for (int i = 0; i < chatRows.Count; i++)
            {
                var g = chatRows[i];
                StartCoroutine(UIParts.Anim(0.12f, k => g.alpha = k, 0.2f + i * 0.07f));
            }
            // 0.5~1.0초: 오른쪽
            var rt = (RectTransform)rightGroup.transform;
            StartCoroutine(UIParts.Anim(0.5f, k =>
            {
                rightGroup.alpha = k;
                rt.anchoredPosition = new Vector2(16f * (1f - k), 0);
            }, 0.5f));
            yield break;
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
        }
    }
}
