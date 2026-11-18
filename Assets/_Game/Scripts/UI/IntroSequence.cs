using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 도입(Intro 단계). 장면 1 입구에서 폰을 걷음 → 장면 2 숨긴 진짜 폰·오늘 찍을 것 → 장면 3 착석.
    /// 장면마다 2~3초, 클릭·Space로 다음, 건너뛰기(Esc) 표시. 마지막에 어둠이 걷히며 메뉴 카메라가 자리로 내려앉고 Playing.
    /// 서체 크기 3종: 24(제목·대사) · 18(본문) · 13(작은 표시).
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        static readonly float[] Durations = { 2.8f, 3.6f };
        const float SeatTime = 2.0f;

        Canvas canvas;
        RectTransform root;
        CanvasGroup group, dimGroup, chromeGroup, toastGroup;
        CanvasGroup[] scenes;
        Image progressFill;
        Text gateLine, gateThought;
        readonly List<CanvasGroup> objectiveRows = new List<CanvasGroup>();
        RectTransform phoneRect;
        CanvasGroup phoneGroup, phoneReady;
        Image phoneRecDot, lensRing;

        bool playing, finishing;
        int scene;
        float sceneTimer;
        readonly List<Coroutine> sceneCos = new List<Coroutine>();

        void Awake()
        {
            canvas = UIKit.Canvas("IntroCanvas", 150, transform);
            root = (RectTransform)canvas.transform;
            group = UIParts.Group(canvas.gameObject, 0f);
            Build();
            canvas.gameObject.SetActive(false);
        }

        // ---------- 구성 ----------

        void Build()
        {
            // 화면 전체를 덮는 어둠 (클릭 = 다음)
            var dim = UIKit.Image("Dim", root, UIParts.Shade(0.94f));
            UIKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            dimGroup = UIParts.Group(dim.gameObject);
            var next = dim.gameObject.AddComponent<Button>();
            next.transition = Selectable.Transition.None;
            var nav = next.navigation; nav.mode = Navigation.Mode.None; next.navigation = nav;
            next.onClick.AddListener(Next);

            scenes = new[] { BuildGate(), BuildPhone(), BuildSeat() };

            // 아래: 진행선 + 다음 안내 + 건너뛰기
            var chrome = UIKit.Rect("Chrome", root);
            UIKit.Stretch(chrome);
            chromeGroup = UIParts.Group(chrome.gameObject);
            UIParts.Bar("ProgressTrack", chrome, UIParts.Ink(0.14f), 128, 1004, 1664, 3);
            progressFill = UIParts.Bar("ProgressFill", chrome, UIKit.Accent, 128, 1004, 0, 3);
            UIParts.Label(chrome, UIStrings.IntroNextHint, 13, UIParts.Ink(0.56f), false, TextAnchor.MiddleLeft, 128, 960, 400, 32);
            var skip = UIParts.Button("Skip", chrome, 1792 - 168, 956, 168, 36, UIParts.Shade(0.72f), UIParts.Ink(0.16f), 18, Skip);
            UIParts.Label(skip.transform, UIStrings.IntroSkip, 13, UIParts.Ink(0.8f), true, TextAnchor.MiddleLeft, 20, 0, 90, 36);
            UIParts.Key(skip.transform, "Esc", 13, UIParts.Ink(0.8f), UIParts.Ink(0.1f), UIParts.Ink(0.2f), 168 - 16 - 40, 7, 22, 40);
        }

        CanvasGroup Scene(string name)
        {
            var rt = UIKit.Rect(name, root);
            UIKit.Stretch(rt);
            return UIParts.Group(rt.gameObject, 0f);
        }

        // 장면 1: 입구 — 폰을 걷는다
        CanvasGroup BuildGate()
        {
            var g = Scene("Scene1_Gate");
            var t = g.transform;
            var card = UIParts.Panel("GateCard", t, UIParts.Card, 12, UIKit.A(UIKit.Accent, 0.35f));
            UIKit.Place(card.rectTransform, 640, 300, 640, 248);
            UIParts.Label(card.transform, UIStrings.IntroGateTitle, 24, UIKit.Accent, true, TextAnchor.MiddleLeft, 32, 24, 576, 36);
            UIParts.Bar("Div", card.transform, UIParts.Ink(0.1f), 32, 76, 576, 1);

            // 바구니(트레이) + 꺼진 공기계
            var tray = UIParts.Panel("Tray", card.transform, UIParts.Shade(0.6f), 10, UIParts.Ink(0.28f));
            UIKit.Place(tray.rectTransform, 32, 100, 200, 116);
            for (int i = 0; i < 2; i++)
            {
                var ph = UIParts.Panel("Phone" + i, tray.transform, UIParts.Ink(i == 0 ? 0.1f : 0.06f), 7, UIParts.Ink(0.3f));
                UIKit.Place(ph.rectTransform, 56 + i * 52, 22, 40, 72);
                ph.rectTransform.localRotation = Quaternion.Euler(0, 0, i == 0 ? 8f : -6f);
            }
            UIParts.Pill(card.transform, UIStrings.IntroGateBadge, 13, UIKit.Ok, UIKit.A(UIKit.Ok, 0.14f), UIKit.A(UIKit.Ok, 0.4f), true, 256, 108, 28, 12, -1, UIKit.Ok);
            var note = UIParts.Label(card.transform, UIStrings.IntroGateNote, 18, UIParts.Ink(0.8f), false, TextAnchor.UpperLeft, 256, 148, 352, 56);
            note.lineSpacing = 1.15f;
            UIParts.Label(card.transform, UIStrings.CultSlogan, 13, UIParts.Ink(0.48f), false, TextAnchor.MiddleLeft, 256, 192, 352, 24);

            // 대사 + 속마음 (가운데, 타자 효과)
            var spk = UIParts.Pill(t, UIStrings.IntroGateSpeaker, 13, UIKit.Accent, UIKit.A(UIKit.Accent, 0.14f), UIKit.A(UIKit.Accent, 0.45f), true, 0, 588, 26, 10, 6);
            spk.anchoredPosition = new Vector2(960 - spk.sizeDelta.x * 0.5f, -588);
            gateLine = UIParts.Label(t, "", 24, UIKit.Accent, true, TextAnchor.MiddleCenter, 360, 624, 1200, 40);
            gateThought = UIParts.Label(t, "", 18, UIParts.Ink(0.82f), false, TextAnchor.MiddleCenter, 360, 672, 1200, 32);
            return g;
        }

        // 장면 2: 숨긴 진짜 폰 + 오늘 찍을 것
        CanvasGroup BuildPhone()
        {
            var g = Scene("Scene2_Phone");
            var t = g.transform;

            var objectives = UIStrings.Objectives ?? new string[0];
            float cardH = 116 + objectives.Length * 56 + 20;
            var card = UIParts.Panel("Objectives", t, UIParts.Card, 12, UIParts.Ink(0.16f));
            float top = 540 - (cardH + 24 + 76) * 0.5f;
            UIKit.Place(card.rectTransform, 320, top, 760, cardH);
            UIParts.Label(card.transform, UIStrings.IntroObjectivesTitle, 24, UIKit.Ink, true, TextAnchor.MiddleLeft, 36, 28, 500, 36);
            UIParts.Label(card.transform, UIStrings.IntroObjectivesSub, 13, UIParts.Ink(0.52f), false, TextAnchor.MiddleLeft, 36, 64, 500, 22);
            UIParts.Bar("Div", card.transform, UIParts.Ink(0.1f), 36, 100, 688, 1);
            objectiveRows.Clear();
            for (int i = 0; i < objectives.Length; i++)
            {
                var row = UIKit.Rect("Objective" + i, card.transform);
                UIKit.Place(row, 36, 116 + i * 56, 688, 48);
                var rg = UIParts.Group(row.gameObject, 0f);
                var box = UIParts.Panel("Check", row, new Color(0, 0, 0, 0), 5, UIKit.A(UIKit.Accent, 0.7f), 1.5f);
                UIKit.Place(box.rectTransform, 0, 13, 22, 22);
                UIParts.Label(row, (i + 1).ToString("00"), 13, UIKit.A(UIKit.Accent, 0.8f), true, TextAnchor.MiddleLeft, 40, 0, 40, 48);
                UIParts.Label(row, objectives[i], 18, UIKit.Ink, true, TextAnchor.MiddleLeft, 76, 0, 600, 48);
                objectiveRows.Add(rg);
            }

            // 잠입 수칙 띠
            var rule = UIParts.Panel("Rule", t, UIKit.A(UIKit.Rec, 0.14f), 10, UIKit.Rec);
            UIKit.Place(rule.rectTransform, 320, top + cardH + 24, 760, 76);
            UIParts.Label(rule.transform, UIStrings.IntroRuleLabel, 13, UIParts.RecSoft, true, TextAnchor.MiddleLeft, 24, 10, 300, 22);
            UIParts.Label(rule.transform, UIStrings.IntroRule, 18, UIKit.Ink, true, TextAnchor.MiddleLeft, 24, 32, 712, 32);

            // 숨긴 진짜 폰 (세로)
            var phone = UIParts.Panel("HiddenPhone", t, UIParts.PhoneBody, 28, UIParts.Ink(0.22f), 2f);
            phoneRect = phone.rectTransform;
            UIKit.Place(phoneRect, 1140, 180, 420, 680);
            phoneGroup = UIParts.Group(phone.gameObject, 0f);
            phoneRecDot = UIParts.Dot("RecDot", phone.transform, UIKit.Rec, 8);
            UIKit.Place(phoneRecDot.rectTransform, 28, 30, 8, 8);
            UIParts.Label(phone.transform, UIStrings.IntroPhoneStatusRec, 13, UIKit.Rec, true, TextAnchor.MiddleLeft, 42, 22, 120, 24);
            UIParts.Label(phone.transform, UIStrings.IntroPhoneStatusLive, 13, UIKit.Ink, true, TextAnchor.MiddleCenter, 150, 22, 120, 24);
            UIParts.Label(phone.transform, UIStrings.IntroPhoneStatusMute, 13, UIParts.Ink(0.56f), false, TextAnchor.MiddleRight, 290, 22, 102, 24);
            var screen = UIParts.Panel("Screen", phone.transform, UIParts.ScreenBg, 18, UIParts.Ink(0.08f));
            UIKit.Place(screen.rectTransform, 16, 60, 388, 604);
            // 뷰파인더 모서리
            var cc = UIParts.Ink(0.3f);
            UIParts.Corner(screen.transform, 24, 24, 22, 2, cc, false, false);
            UIParts.Corner(screen.transform, 388 - 46, 24, 22, 2, cc, true, false);
            UIParts.Corner(screen.transform, 24, 604 - 46, 22, 2, cc, false, true);
            UIParts.Corner(screen.transform, 388 - 46, 604 - 46, 22, 2, cc, true, true);
            var ready = UIKit.Rect("Ready", screen.transform);
            UIKit.Stretch(ready);
            phoneReady = UIParts.Group(ready.gameObject, 0f);
            lensRing = UIParts.Ring("Lens", ready, UIKit.A(UIKit.Accent, 0.7f), 112, 2f);
            UIKit.Place(lensRing.rectTransform, 194 - 56, 200, 112, 112);
            var lensDot = UIParts.Dot("LensDot", ready, UIKit.Rec, 14);
            UIKit.Place(lensDot.rectTransform, 194 - 7, 256 - 7, 14, 14);
            UIParts.Label(ready, UIStrings.IntroPhoneReady, 18, UIKit.Ink, true, TextAnchor.MiddleCenter, 0, 344, 388, 32);
            UIParts.Label(ready, UIStrings.IntroPhoneReadySub, 13, UIParts.Ink(0.56f), false, TextAnchor.MiddleCenter, 0, 378, 388, 24);
            return g;
        }

        // 장면 3: 착석
        CanvasGroup BuildSeat()
        {
            var g = Scene("Scene3_Seat");
            var toast = UIParts.Panel("Toast", g.transform, UIParts.Shade(0.82f), 32, UIKit.A(UIKit.Accent, 0.45f));
            UIKit.Place(toast.rectTransform, 640, 480, 640, 64);
            UIParts.Label(toast.transform, UIStrings.IntroSeated, 18, UIKit.Ink, true, TextAnchor.MiddleCenter, 0, 0, 640, 64);
            toastGroup = UIParts.Group(toast.gameObject, 1f);
            return g;
        }

        // ---------- 진행 ----------

        public void Play()
        {
            StopAllCoroutines();
            sceneCos.Clear();
            canvas.gameObject.SetActive(true);
            group.alpha = 0f;
            playing = true;
            finishing = false;
            dimGroup.alpha = 1f;
            chromeGroup.alpha = 1f;
            foreach (var s in scenes) { s.alpha = 0f; s.gameObject.SetActive(false); }
            StartCoroutine(UIParts.FadeTo(group, 1f, 0.35f));
            GoTo(0);
        }

        public void Stop()
        {
            if (!playing && !canvas.gameObject.activeSelf) return;
            playing = false;
            finishing = false;
            StopAllCoroutines();
            canvas.gameObject.SetActive(false);
            group.alpha = 0f;
        }

        void GoTo(int i)
        {
            foreach (var c in sceneCos) if (c != null) StopCoroutine(c);
            sceneCos.Clear();
            for (int k = 0; k < scenes.Length; k++)
                if (k != i && scenes[k].gameObject.activeSelf)
                {
                    var old = scenes[k];
                    StartCoroutine(FadeOutAndHide(old, 0.2f));
                }
            scene = i;
            sceneTimer = 0f;
            var g = scenes[i];
            g.gameObject.SetActive(true);
            sceneCos.Add(StartCoroutine(UIParts.FadeTo(g, 1f, 0.3f, 0.1f)));
            if (i == 0) EnterGate();
            else if (i == 1) EnterPhone();
        }

        IEnumerator FadeOutAndHide(CanvasGroup g, float dur)
        {
            yield return UIParts.FadeTo(g, 0f, dur);
            if (g != null && g != scenes[scene]) g.gameObject.SetActive(false);
        }

        void EnterGate()
        {
            sceneCos.Add(StartCoroutine(UIParts.Type(gateLine, UIStrings.PhoneBanQuote, 34f, 0.25f)));
            float lineTime = 0.25f + UIStrings.PhoneBanQuote.Length / 34f;
            sceneCos.Add(StartCoroutine(UIParts.Type(gateThought, UIStrings.IntroGateThought, 40f, lineTime + 0.15f)));
        }

        void EnterPhone()
        {
            phoneGroup.alpha = 0f;
            phoneReady.alpha = 0f;
            var end = new Vector2(1140, -180);
            sceneCos.Add(StartCoroutine(UIParts.Anim(0.4f, k =>
            {
                phoneGroup.alpha = k;
                phoneRect.anchoredPosition = end + new Vector2(0, -60f * (1f - k));
            }, 0.15f)));
            sceneCos.Add(StartCoroutine(UIParts.Anim(0.35f, k =>
            {
                phoneReady.alpha = k;
                lensRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, k);
            }, 0.6f)));
            for (int i = 0; i < objectiveRows.Count; i++)
            {
                var rg = objectiveRows[i];
                var rt = (RectTransform)rg.transform;
                var p = rt.anchoredPosition;
                var basePos = new Vector2(36, p.y);
                rg.alpha = 0f;
                sceneCos.Add(StartCoroutine(UIParts.Anim(0.3f, k =>
                {
                    rg.alpha = k;
                    rt.anchoredPosition = basePos + new Vector2(12f * (1f - k), 0);
                }, 0.3f + i * 0.15f)));
            }
        }

        /// <summary>클릭·Space: 다음 장면.</summary>
        public void Next()
        {
            if (!playing || finishing) return;
            if (scene + 1 < Durations.Length) GoTo(scene + 1);
            else Seat(false);
        }

        /// <summary>건너뛰기: 바로 자리에 앉는다.</summary>
        public void Skip()
        {
            if (!playing || finishing) return;
            Seat(true);
        }

        void Seat(bool fast)
        {
            finishing = true;
            foreach (var c in sceneCos) if (c != null) StopCoroutine(c);
            sceneCos.Clear();
            for (int k = 0; k < scenes.Length - 1; k++)
                if (scenes[k].gameObject.activeSelf) StartCoroutine(FadeOutAndHide(scenes[k], 0.2f));
            scene = scenes.Length - 1;
            StartCoroutine(SeatCo(fast));
        }

        IEnumerator SeatCo(bool fast)
        {
            var seat = scenes[scenes.Length - 1];
            float dur = fast ? 0.5f : 1.4f;
            StartCoroutine(UIParts.FadeTo(dimGroup, 0f, fast ? 0.3f : 0.6f));
            StartCoroutine(UIParts.FadeTo(chromeGroup, 0f, 0.3f));
            if (!fast)
            {
                seat.gameObject.SetActive(true);
                toastGroup.alpha = 1f;
                StartCoroutine(UIParts.FadeTo(seat, 1f, 0.3f, 0.2f));
            }

            bool landed = false;
            var mc = UIFlow.Instance != null ? UIFlow.Instance.MenuCam : null;
            if (mc != null && mc.Active) mc.HandOff(dur, () => landed = true);
            else landed = true;

            float t = 0f;
            float total = fast ? dur : SeatTime;
            float w0 = progressFill.rectTransform.sizeDelta.x;
            while (t < total || !landed)
            {
                t += Time.unscaledDeltaTime;
                progressFill.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(w0, 1664f, Mathf.Clamp01(t / total)), 3f);
                if (t > 6f) break;   // 안전장치
                yield return null;
            }
            if (!fast) yield return UIParts.FadeTo(seat, 0f, 0.3f);
            playing = false;
            finishing = false;
            canvas.gameObject.SetActive(false);
            if (GameEvents.Phase == GamePhase.Intro) GameEvents.SetPhase(GamePhase.Playing);
        }

        void Update()
        {
            if (!playing) return;
            float dt = Time.unscaledDeltaTime;

            if (!finishing)
            {
                sceneTimer += dt;
                float total = 0f; foreach (var d in Durations) total += d;
                float done = 0f; for (int i = 0; i < scene; i++) done += Durations[i];
                float frac = Mathf.Clamp01((done + Mathf.Min(sceneTimer, Durations[scene])) / (total + SeatTime));
                progressFill.rectTransform.sizeDelta = new Vector2(1664f * frac, 3f);

                var kb = Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) { Next(); return; }
                if (sceneTimer >= Durations[scene]) Next();
            }

            if (phoneRecDot != null) phoneRecDot.enabled = (Time.unscaledTime % 1.2f) < 0.7f;
        }
    }
}
