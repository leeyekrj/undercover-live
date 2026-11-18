using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 상시 방송 위젯 (맨눈·몰래·촬영 모두). Paul 지시: 시청자 수는 항상 보여야 한다.
    /// - 왼쪽 위 카드: ● REC 타이머(주머니 '음성만' / 몰래 '몰래') · LIVE 시청자 수(촬영 땐 +변화량) · 배터리
    ///   맨눈에선 (48,36) 작은 카드 → 촬영 화면에선 같은 순서로 (68,40) 상태 줄이 된다(폰이 올라오는 만큼 이동·확대).
    /// - 카드 오른쪽: 경계 단계 ●●○ (경계가 1 이상일 때)
    /// - 오른쪽 위: 확보한 클립 n/4 + 4칸 막대 (촬영 중이면 다음 칸에 확보 진행이 초록으로 찬다)
    /// </summary>
    public class BroadcastWidget : MonoBehaviour
    {
        const float H = 48f, Pad = 16f, Gap = 12f;
        const float EyeScale = 0.88f, GoalEyeScale = 0.8f;
        static readonly Vector2 EyePos = new Vector2(48f, -36f), AimPos = new Vector2(68f, -40f);
        static readonly Vector2 GoalEyePos = new Vector2(-48f, -36f), GoalAimPos = new Vector2(-48f, -40f);

        RectTransform card, row;
        Image recDot, livePill, divA, divB, battShell, battFill, battNub;
        Text recText, modeText, liveText, viewersText, deltaText, battText;
        RectTransform alertChip;
        CanvasGroup alertCg;
        Image[] alertDots = new Image[3];
        Text alertText;

        RectTransform goal;
        Text goalLabel, goalCount;
        Image[] slotFill = new Image[4];
        Image[] slotTrack = new Image[4];

        string sRec, sMode, sViewers, sDelta, sBatt;
        int lastAlert = -1;
        float layoutW;

        void Awake()
        {
            var layer = HUDRoot.Instance.StatusLayer;
            BuildCard(layer);
            BuildAlert(layer);
            BuildGoal(layer);
        }

        // ---------- 구성 ----------

        void BuildCard(Transform layer)
        {
            var bg = UIKit.Box("BroadcastCard", layer, HUDDraw.Card, 14);
            card = bg.rectTransform;
            HUDDraw.At(card, HUDDraw.TL, HUDDraw.TL, EyePos, new Vector2(420f, H));
            var line = UIKit.Box("Line", card, new Color(1, 1, 1, 0.16f), 14, 1);
            UIKit.Stretch(line.rectTransform);
            row = UIKit.Rect("Row", card);
            UIKit.Stretch(row);

            recDot = UIKit.Image("RecDot", row, UIKit.Rec, UIKit.Circle(64));
            recText = HUDDraw.Label("Rec", row, "REC 00:00", HUDDraw.FS, UIKit.Ink, true);
            modeText = HUDDraw.Label("Mode", row, "", HUDDraw.FS, UIKit.Muted, true);
            divA = UIKit.Image("DivA", row, new Color(1, 1, 1, 0.18f));
            livePill = UIKit.Box("LivePill", row, UIKit.Rec, 7);
            liveText = HUDDraw.Label("Live", livePill.transform, "LIVE", HUDDraw.FS, Color.white, true, TextAnchor.MiddleCenter);
            UIKit.Stretch(liveText.rectTransform);
            viewersText = HUDDraw.Label("Viewers", row, "0", HUDDraw.FS, UIKit.Ink, true);
            deltaText = HUDDraw.Label("Delta", row, "", HUDDraw.FS, UIKit.Hex("#4ADE80"), true);
            divB = UIKit.Image("DivB", row, new Color(1, 1, 1, 0.18f));
            battShell = UIKit.Box("BattShell", row, UIKit.Ink, 3, 2);
            var inner = UIKit.Rect("Inner", battShell.transform);
            UIKit.Stretch(inner, 3f);
            battFill = UIKit.Box("BattFill", inner, UIKit.Ink, 1);
            var bf = battFill.rectTransform;
            bf.anchorMin = Vector2.zero; bf.anchorMax = new Vector2(1, 1); bf.pivot = new Vector2(0, 0.5f);
            bf.offsetMin = bf.offsetMax = Vector2.zero;
            battNub = UIKit.Box("BattNub", row, UIKit.Ink, 1);
            battText = HUDDraw.Label("Batt", row, "100%", HUDDraw.FS, UIKit.Ink, true);
        }

        void BuildAlert(Transform layer)
        {
            var bg = UIKit.Box("AlertChip", layer, HUDDraw.Card, 14);
            alertChip = bg.rectTransform;
            HUDDraw.At(alertChip, HUDDraw.TL, HUDDraw.TL, EyePos, new Vector2(132f, H));
            alertCg = alertChip.gameObject.AddComponent<CanvasGroup>();
            alertCg.alpha = 0f; alertCg.blocksRaycasts = false;
            alertText = HUDDraw.Label("Label", alertChip, HUDText.AlertLabel, HUDDraw.FS, UIKit.Warn, true);
            HUDDraw.At(alertText.rectTransform, HUDDraw.ML, HUDDraw.ML, new Vector2(Pad, 0), new Vector2(44f, 24f));
            for (int i = 0; i < 3; i++)
            {
                var d = UIKit.Image("Dot" + i, alertChip, Color.white, UIKit.Circle(64));
                HUDDraw.At(d.rectTransform, HUDDraw.ML, HUDDraw.ML, new Vector2(Pad + 46f + i * 18f, 0), new Vector2(11f, 11f));
                alertDots[i] = d;
            }
        }

        void BuildGoal(Transform layer)
        {
            var bg = UIKit.Box("GoalCard", layer, HUDDraw.Card, 14);
            goal = bg.rectTransform;
            HUDDraw.At(goal, HUDDraw.TR, HUDDraw.TR, GoalEyePos, new Vector2(300f, 64f));
            var line = UIKit.Box("Line", goal, new Color(1, 1, 1, 0.16f), 14, 1);
            UIKit.Stretch(line.rectTransform);
            goalLabel = HUDDraw.Label("Label", goal, HUDText.GoalLabel, HUDDraw.FS, UIKit.Ink, true);
            HUDDraw.At(goalLabel.rectTransform, HUDDraw.TL, HUDDraw.TL, new Vector2(Pad, -10f), new Vector2(180f, 24f));
            goalCount = HUDDraw.Label("Count", goal, "0 / 4", HUDDraw.FS, HUDDraw.Gold, true, TextAnchor.MiddleRight);
            HUDDraw.At(goalCount.rectTransform, HUDDraw.TR, HUDDraw.TR, new Vector2(-Pad, -10f), new Vector2(90f, 24f));
            float slotW = (300f - Pad * 2f - 6f * 3f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var tr = UIKit.Box("Slot" + i, goal, new Color(1, 1, 1, 0.16f), 4);
                HUDDraw.At(tr.rectTransform, HUDDraw.BL, HUDDraw.BL, new Vector2(Pad + i * (slotW + 6f), 12f), new Vector2(slotW, 8f));
                var f = UIKit.Box("Fill", tr.transform, HUDDraw.Gold, 4);
                var fr = f.rectTransform;
                fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0, 1); fr.pivot = new Vector2(0, 0.5f);
                fr.offsetMin = fr.offsetMax = Vector2.zero;
                slotTrack[i] = tr; slotFill[i] = f;
            }
        }

        // ---------- 갱신 ----------

        void Update()
        {
            var pc = PhoneController.Instance;
            var feed = StreamFeed.Instance;
            float blend = PhoneAppUI.Instance != null ? PhoneAppUI.Instance.Blend : 0f;
            float e = HUDDraw.EaseOut(blend);

            // 자리·크기: 맨눈 작은 카드 ↔ 촬영 상태 줄
            card.anchoredPosition = Vector2.Lerp(EyePos, AimPos, e);
            float sc = Mathf.Lerp(EyeScale, 1f, e);
            card.localScale = new Vector3(sc, sc, 1f);
            goal.anchoredPosition = Vector2.Lerp(GoalEyePos, GoalAimPos, e);
            float gs = Mathf.Lerp(GoalEyeScale, 1f, e);
            goal.localScale = new Vector3(gs, gs, 1f);

            bool live = pc != null && pc.IsAudioRecording;
            bool aimed = pc != null && pc.State == PhoneState.Aimed;

            // REC
            float rs = pc != null ? pc.RecSeconds : 0f;
            int m = (int)(rs / 60f), s = (int)(rs % 60f);
            string rec = "REC " + m.ToString("00") + ":" + s.ToString("00");
            string mode = pc == null || !live ? "" : pc.State == PhoneState.Pocket ? HUDText.RecVoiceOnly : pc.State == PhoneState.Hidden ? HUDText.RecSneak : "";
            bool blinkOn = (Time.unscaledTime % 1.2f) < 0.75f;
            recDot.color = live ? UIKit.A(UIKit.Rec, blinkOn ? 1f : 0.25f) : UIKit.Faint;
            modeText.color = pc != null && pc.State == PhoneState.Hidden ? HUDDraw.Teal : UIKit.Muted;

            // 시청자
            int v = feed != null ? feed.DisplayedViewers : 0;
            string viewers = live ? v.ToString("N0", CultureInfo.InvariantCulture) : HUDText.LiveOff;
            int d = feed != null ? feed.ViewerDelta : 0;
            string delta = aimed && live && d > 0 ? "+" + d.ToString("N0", CultureInfo.InvariantCulture) : "";
            livePill.color = live ? UIKit.Rec : new Color(0.32f, 0.32f, 0.36f, 1f);

            // 배터리
            float b = pc != null ? pc.Battery01 : 1f;
            int pct = Mathf.CeilToInt(b * 100f);
            string batt = pct + "%";
            Color bc = pct <= 20 ? (blinkOn ? UIKit.Rec : UIKit.A(UIKit.Rec, 0.5f)) : pct <= 50 ? UIKit.Hex("#FCD34D") : UIKit.Ink;
            battShell.color = bc; battFill.color = bc; battNub.color = bc; battText.color = bc;
            battFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(b), 1f);
            battFill.enabled = b > 0.01f;

            if (rec != sRec || mode != sMode || viewers != sViewers || delta != sDelta || batt != sBatt)
            {
                bool widthChange = mode != sMode || delta != sDelta || (viewers?.Length ?? 0) != (sViewers?.Length ?? 0) || batt.Length != (sBatt?.Length ?? 0) || sRec == null;
                sRec = rec; sMode = mode; sViewers = viewers; sDelta = delta; sBatt = batt;
                recText.text = rec; modeText.text = mode; viewersText.text = viewers; deltaText.text = delta; battText.text = batt;
                if (widthChange) Layout();
            }

            UpdateAlert(sc);
            UpdateGoal(pc, aimed);
        }

        void Layout()
        {
            float x = Pad;
            Place(recDot.rectTransform, ref x, 10f, 10f, 8f);
            Place(recText.rectTransform, ref x, HUDDraw.Width(recText) + 2f, 24f, modeText.text.Length > 0 ? 8f : Gap);
            if (modeText.text.Length > 0) Place(modeText.rectTransform, ref x, HUDDraw.Width(modeText) + 2f, 24f, Gap);
            modeText.enabled = modeText.text.Length > 0;
            Place(divA.rectTransform, ref x, 1f, 18f, Gap);
            Place(livePill.rectTransform, ref x, HUDDraw.Width(liveText) + 18f, 26f, 8f);
            // 숫자 폭은 가장 넓은 숫자(8)로 잡아 숫자가 바뀌어도 흔들리지 않게
            string real = viewersText.text;
            viewersText.text = System.Text.RegularExpressions.Regex.Replace(real, "[0-9]", "8");
            float vw = HUDDraw.Width(viewersText) + 2f;
            viewersText.text = real;
            Place(viewersText.rectTransform, ref x, vw, 24f, deltaText.text.Length > 0 ? 6f : Gap);
            deltaText.enabled = deltaText.text.Length > 0;
            if (deltaText.enabled) Place(deltaText.rectTransform, ref x, HUDDraw.Width(deltaText) + 2f, 24f, Gap);
            Place(divB.rectTransform, ref x, 1f, 18f, Gap);
            Place(battShell.rectTransform, ref x, 26f, 14f, 2f);
            Place(battNub.rectTransform, ref x, 3f, 6f, 6f);
            Place(battText.rectTransform, ref x, HUDDraw.Width(battText) + 2f, 24f, 0f);
            layoutW = x + Pad;
            card.sizeDelta = new Vector2(layoutW, H);
        }

        static void Place(RectTransform rt, ref float x, float w, float h, float gapAfter)
        {
            HUDDraw.At(rt, HUDDraw.ML, HUDDraw.ML, new Vector2(x, 0f), new Vector2(w, h));
            x += w + gapAfter;
        }

        void UpdateAlert(float cardScale)
        {
            var mgr = SuspicionManager.Instance;
            int level = mgr != null ? mgr.AlertLevel : 0;
            alertCg.alpha = Mathf.MoveTowards(alertCg.alpha, level > 0 ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);
            alertChip.localScale = card.localScale;
            alertChip.anchoredPosition = card.anchoredPosition + new Vector2((layoutW + 8f) * cardScale, 0f);
            if (level != lastAlert)
            {
                lastAlert = level;
                Color on = level >= SuspicionManager.MaxAlert ? UIKit.Rec : UIKit.Warn;
                alertText.color = on;
                for (int i = 0; i < 3; i++) alertDots[i].color = i < level ? on : new Color(1, 1, 1, 0.22f);
            }
        }

        readonly HashSet<string> distinct = new HashSet<string>();
        int countedClips = -1;

        /// <summary>목표 진행은 클립 수가 아니라 서로 다른 대상 수 (같은 대상을 여러 번 찍어도 한 칸).</summary>
        int DistinctTargets()
        {
            var clips = CaptureLog.Clips;
            if (clips.Count != countedClips)
            {
                countedClips = clips.Count;
                distinct.Clear();
                foreach (var c in clips) if (c != null && !string.IsNullOrEmpty(c.TargetId)) distinct.Add(c.TargetId);
            }
            return distinct.Count;
        }

        void UpdateGoal(PhoneController pc, bool aimed)
        {
            int n = Mathf.Min(4, DistinctTargets());
            string count = n + " / 4";
            if (goalCount.text != count) goalCount.text = count;
            var judge = CaptureJudge.Instance;
            // 다음 칸 초록 진행은 아직 확보하지 않은 대상을 찍을 때만
            bool fresh = judge != null && judge.CurrentTarget != null && !distinct.Contains(judge.CurrentTarget.Id);
            float holding = aimed && fresh && pc != null && pc.IsScreenVisible ? judge.HoldProgress : 0f;
            for (int i = 0; i < 4; i++)
            {
                float f = i < n ? 1f : (i == n ? holding : 0f);
                slotFill[i].rectTransform.anchorMax = new Vector2(f, 1f);
                slotFill[i].color = i < n ? HUDDraw.Gold : UIKit.Ok;
                slotFill[i].enabled = f > 0.001f;
            }
        }
    }
}
