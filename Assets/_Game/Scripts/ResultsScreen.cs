using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 엔딩: 업로드 결과 화면(Argon 결과 A안). 위 상태 줄 · 왼쪽 엔딩 판정 카드(판정 3종 탭) · 오른쪽 자동 제목과 통계 3칸 ·
    /// 아래 클립 카드 줄(최대 6, 0·1·6개 모두 무너지지 않게) · 맨 아래 메인으로 / R 다시 하기.
    /// 등장: 0.0초 상단 → 0.4초 숫자 카운트업 → 1.0초 카드 순차 → 2.0초 판정 도장. 문구는 UIStrings.
    /// 서체 크기 4종: 32 · 24 · 16 · 13.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        [Header("엔딩 기준")]
        [Tooltip("총 보도 가치가 이 이상이면 '보도' 엔딩")]
        public float newsThreshold = 2f;
        [Tooltip("최고 동시 시청자가 이 이상이면 '조회수' 엔딩")]
        public int viewerThreshold = 8000;
        public int maxCards = 6;
        [Tooltip("예전 버전 호환용(지금은 UIKit 서체를 쓴다)")]
        public Font font;

        const int MaxSlots = 6;

        Canvas canvas;
        RectTransform root;
        CanvasGroup group;
        bool shown;

        // 연출 대상
        Text peakText, clipsText, newsText, headline, desc;
        int peakValue, clipsValue; float newsValue;
        CanvasGroup descGroup;
        RectTransform headlineRect;
        readonly List<RectTransform> cardRects = new List<RectTransform>();
        readonly List<CanvasGroup> cardGroups = new List<CanvasGroup>();
        readonly List<List<RectTransform>> cardBars = new List<List<RectTransform>>();
        readonly List<List<float>> cardBarHeights = new List<List<float>>();
        Image activeFill, activeLine; Text activeTitle; RectTransform activeTile;
        int verdict;

        public bool IsShown { get { return shown; } }

        void OnEnable() { GameEvents.ServiceFinished += OnServiceFinished; }
        void OnDisable() { GameEvents.ServiceFinished -= OnServiceFinished; }
        void OnServiceFinished() { Show(""); }

        public void Show(string reason)
        {
            if (shown) return;
            if (CaughtScreen.Instance != null && CaughtScreen.Instance.IsShown) return;
            shown = true;
            GameEvents.SetPhase(GamePhase.Ended);
            Build(reason);
            StartCoroutine(Sequence());
        }

        void Update()
        {
            if (!shown) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.rKey.wasPressedThisFrame) UIFlow.Restart(true);
            else if (kb.escapeKey.wasPressedThisFrame) UIFlow.Restart(false);
        }

        // ---------- 데이터 ----------

        List<CaptureResult> PickClips()
        {
            var all = new List<CaptureResult>(CaptureLog.Clips);
            all.Sort((a, b) => b.Viewers.CompareTo(a.Viewers));
            int n = Mathf.Min(Mathf.Clamp(maxCards, 0, MaxSlots), all.Count);
            var top = all.GetRange(0, n);
            top.Sort((a, b) => a.GameTime.CompareTo(b.GameTime)); // 표시는 시간순
            return top;
        }

        int PeakViewers(int fallbackTotal)
        {
            var feed = FindAnyObjectByType<StreamFeed>();
            int v = feed != null ? feed.Viewers : 0;
            return Mathf.Max(v, fallbackTotal > 0 ? fallbackTotal : 0);
        }

        static CaptureResult Best(List<CaptureResult> clips)
        {
            CaptureResult best = null;
            foreach (var c in clips)
                if (best == null || c.Grade > best.Grade || (c.Grade == best.Grade && c.Viewers > best.Viewers)) best = c;
            return best;
        }

        /// <summary>0 = 보도, 1 = 조회수, 2 = 빈손.</summary>
        int Verdict(int peak, float news)
        {
            if (CaptureLog.Clips.Count > 0 && news >= newsThreshold) return 0;
            if (peak >= viewerThreshold) return 1;
            return 2;
        }

        static Color VerdictColor(int v)
        {
            return v == 0 ? UIParts.GoldBright : v == 1 ? UIParts.Blue : UIParts.Ink(0.8f);
        }

        static string V(int v, int field)
        {
            var a = UIStrings.Verdicts;
            int i = v * 4 + field;
            return a != null && i < a.Length ? a[i] : "";
        }

        static Color GradeColor(CaptureGrade g)
        {
            switch (g)
            {
                case CaptureGrade.S: return UIKit.Hex("#F59E0B");
                case CaptureGrade.A: return UIKit.Hex("#4ADE80");
                case CaptureGrade.B: return UIKit.Hex("#60A5FA");
                default: return UIKit.Hex("#9CA3AF");
            }
        }

        // ---------- 구성 ----------

        void Build(string reason)
        {
            canvas = UIKit.Canvas("ResultsCanvas", 110, transform);
            root = (RectTransform)canvas.transform;
            group = UIParts.Group(canvas.gameObject, 0f);
            var bg = UIKit.Image("Bg", root, UIParts.Bg);
            UIKit.Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            var clips = PickClips();
            int total = CaptureLog.Clips.Count;
            float news = CaptureLog.TotalNewsValue();
            int peak = PeakViewers(CaptureLog.TotalViewersGained());
            verdict = Verdict(peak, news);
            var best = Best(clips);

            BuildTopBar(reason);
            BuildVerdictCard();
            BuildAnalytics(clips, best, total, peak, news);
            BuildDeck(clips, best);
            BuildBottomBar();
        }

        void BuildTopBar(string reason)
        {
            var t = root;
            var dot = UIParts.Dot("LiveDot", t, UIKit.Rec, 10);
            UIKit.Place(dot.rectTransform, 80, 43, 10, 10);
            var name = UIParts.Label(t, UIStrings.AppName, 13, UIKit.Ink, true, TextAnchor.MiddleLeft, 102, 36, 300, 24);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            float x = 102 + Mathf.Ceil(UIParts.Width(name)) + 14;
            UIParts.Bar("Divider", t, UIParts.Ink(0.22f), x, 41, 1, 14);
            x += 15;
            var status = UIParts.Label(t, UIStrings.StudioStatus, 13, UIParts.Ink(0.72f), false, TextAnchor.MiddleLeft, x, 36, 600, 24);
            status.horizontalOverflow = HorizontalWrapMode.Overflow;
            x += Mathf.Ceil(UIParts.Width(status)) + 14;
            UIParts.Pill(t, UIStrings.StudioTag, 13, UIKit.Accent, UIKit.A(UIKit.Accent, 0.12f), UIKit.A(UIKit.Accent, 0.35f), true, x, 35, 26, 10);
            if (!string.IsNullOrEmpty(reason))
                UIParts.Label(t, reason, 13, UIParts.Ink(0.48f), false, TextAnchor.MiddleRight, 1240, 36, 600, 24);
            UIParts.Bar("TopLine", t, UIParts.Ink(0.12f), 80, 75, 1760, 1);
        }

        void BuildVerdictCard()
        {
            var card = UIParts.Panel("Verdict", root, UIKit.A(UIParts.PanelElev, 0.96f), 16, UIKit.Accent, 1.5f);
            UIKit.Place(card.rectTransform, 80, 92, 660, 248);
            var mask = UIParts.RoundMask("Glow", card.transform, 16);
            mask.SetAsFirstSibling();
            var grad = UIKit.Image("Grad", mask, UIKit.A(UIKit.Accent, 0.14f), UIParts.HGradient(new[] { 0f, 0.65f }, new[] { 1f, 0f }));
            UIKit.Stretch(grad.rectTransform);
            var t = card.transform;

            UIParts.Pill(t, UIStrings.VerdictBadge, 13, UIParts.Dark, UIKit.Accent, new Color(0, 0, 0, 0), true, 26, 22, 26, 12, -1, UIParts.Dark);
            UIParts.Label(t, string.Format(UIStrings.VerdictMetaFormat, CaptureLog.Clips.Count), 13, UIParts.Ink(0.72f), false, TextAnchor.MiddleRight, 334, 22, 300, 26);

            headline = UIParts.Label(t, UIStrings.VerdictPending, 32, UIParts.Ink(0.4f), true, TextAnchor.MiddleLeft, 26, 58, 608, 44);
            headline.horizontalOverflow = HorizontalWrapMode.Overflow;
            headlineRect = headline.rectTransform;
            desc = UIParts.Label(t, V(verdict, 2), 16, UIParts.Ink(0.72f), false, TextAnchor.UpperLeft, 26, 106, 608, 48);
            desc.lineSpacing = 1.15f;
            descGroup = UIParts.Group(desc.gameObject, 0f);

            UIParts.Bar("TierLine", t, UIParts.Ink(0.12f), 26, 166, 608, 1);
            float tw = (608f - 16f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var tile = UIParts.Panel("Tier" + i, t, UIParts.Shade(0.55f), 8, UIParts.Ink(0.12f));
                UIKit.Place(tile.rectTransform, 26 + i * (tw + 8f), 178, tw, 50);
                var title = UIParts.Label(tile.transform, V(i, 0), 13, UIParts.Ink(0.72f), true, TextAnchor.MiddleLeft, 10, 4, tw - 20, 22);
                var sub = UIParts.Label(tile.transform, "", 13, UIParts.Ink(0.48f), false, TextAnchor.MiddleLeft, 10, 24, tw - 20, 22);
                UIParts.FitOneLine(sub, V(i, 3), tw - 20);
                if (i == verdict)
                {
                    activeTile = tile.rectTransform;
                    activeFill = tile;
                    var lt = tile.transform.Find("Line");
                    activeLine = lt != null ? lt.GetComponent<Image>() : null;
                    activeTitle = title;
                    // 판정 전에도 살짝 보이게: 도장 때 색이 들어온다
                    activeTile.pivot = new Vector2(0.5f, 0.5f);
                    activeTile.anchoredPosition += new Vector2(tw * 0.5f, -25f);
                }
            }
        }

        void BuildAnalytics(List<CaptureResult> clips, CaptureResult best, int total, int peak, float news)
        {
            var panel = UIParts.Panel("Analytics", root, UIParts.PanelBg, 16, UIParts.Ink(0.22f));
            UIKit.Place(panel.rectTransform, 760, 92, 1080, 248);
            var t = panel.transform;

            UIParts.Label(t, UIStrings.AutoTitleEyebrow, 13, UIParts.Ink(0.48f), true, TextAnchor.MiddleLeft, 26, 18, 700, 22);
            var badgeText = total > 0 ? UIStrings.UploadDone : UIStrings.UploadNone;
            var badgeCol = total > 0 ? UIParts.Green : UIParts.Ink(0.56f);
            var badge = UIParts.Pill(t, badgeText, 13, badgeCol, UIKit.A(badgeCol, 0.14f), UIKit.A(badgeCol, 0.35f), true, 0, 22, 30, 12, 8, badgeCol);
            UIParts.AlignRight(badge, 1080 - 26);
            string title = best != null ? string.Format(UIStrings.AutoTitleFormat, best.TargetName) : UIStrings.AutoTitleEmpty;
            var tt = UIParts.Label(t, "", 24, UIKit.Ink, true, TextAnchor.MiddleLeft, 26, 42, 1080 - 52 - badge.sizeDelta.x - 24, 40);
            UIParts.FitOneLine(tt, title, 1080 - 52 - badge.sizeDelta.x - 24);
            UIParts.Bar("Div", t, UIParts.Ink(0.12f), 26, 96, 1028, 1);

            int sound = 0;
            foreach (var c in CaptureLog.Clips) if (c.Category == CaptureCategory.Sound) sound++;
            float mw = (1028f - 32f) / 3f;
            peakValue = peak; clipsValue = total; newsValue = news;
            peakText = Metric(t, 26, mw, UIStrings.MetricPeak, UIStrings.MetricPeakChip,
                best != null ? string.Format(UIStrings.MetricPeakSubFormat, best.TargetName) : UIStrings.MetricPeakSubNone, false);
            clipsText = Metric(t, 26 + (mw + 16f), mw, UIStrings.MetricClips, Mathf.Min(total, MaxSlots) + " / " + MaxSlots,
                string.Format(UIStrings.MetricClipsSubFormat, total - sound, sound), false);
            bool reached = total > 0 && news >= newsThreshold;
            newsText = Metric(t, 26 + (mw + 16f) * 2f, mw, UIStrings.MetricNews, string.Format(UIStrings.MetricNewsChipFormat, newsThreshold),
                reached ? UIStrings.MetricNewsReached : string.Format(UIStrings.MetricNewsShortFormat, Mathf.Max(0f, newsThreshold - news)), true);
            SetCounts(0f);
        }

        Text Metric(Transform parent, float x, float w, string label, string chip, string sub, bool highlight)
        {
            var card = UIParts.Panel("Metric", parent, UIParts.PanelElev, 12, highlight ? UIKit.A(UIKit.Accent, 0.45f) : UIParts.Ink(0.12f));
            UIKit.Place(card.rectTransform, x, 112, w, 118);
            var t = card.transform;
            UIParts.Label(t, label, 13, UIParts.Ink(0.72f), false, TextAnchor.MiddleLeft, 18, 12, w - 120, 22);
            var c = UIParts.Pill(t, chip, 13, UIParts.Ink(0.72f), UIParts.Ink(0.08f), new Color(0, 0, 0, 0), true, 0, 12, 22, 7, 4);
            UIParts.AlignRight(c, w - 14);
            var v = UIParts.Label(t, "0", 32, highlight ? UIParts.GoldBright : UIKit.Ink, true, TextAnchor.MiddleLeft, 18, 38, w - 36, 44);
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            var s = UIParts.Label(t, "", 13, UIParts.Ink(0.48f), false, TextAnchor.MiddleLeft, 18, 86, w - 36, 20);
            UIParts.FitOneLine(s, sub, w - 36);
            return v;
        }

        void SetCounts(float k)
        {
            if (peakText != null) peakText.text = Mathf.RoundToInt(peakValue * k).ToString("N0") + "명";
            if (clipsText != null) clipsText.text = Mathf.RoundToInt(clipsValue * k) + "개";
            if (newsText != null) newsText.text = (newsValue * k).ToString("0.0");
        }

        // ---------- 클립 카드 줄 ----------

        void BuildDeck(List<CaptureResult> clips, CaptureResult best)
        {
            var t = root;
            var head = UIParts.Label(t, UIStrings.ClipsHeader, 16, UIKit.Ink, true, TextAnchor.MiddleLeft, 80, 356, 400, 30);
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            float hx = 80 + Mathf.Ceil(UIParts.Width(head)) + 12;
            UIParts.Pill(t, string.Format(UIStrings.ClipsOrderFormat, clips.Count), 13, UIParts.Ink(0.72f), UIParts.Ink(0.08f), new Color(0, 0, 0, 0), true, hx, 359, 24, 10);

            const float top = 396f, h = 568f;
            int n = clips.Count;
            if (n == 0)
            {
                BuildEmpty(t, 960 - 440, top + (h - 360) * 0.5f, 880, 360);
                return;
            }
            bool guide = n <= 4;
            float cw = guide ? 312f : 276f, gap = guide ? 24f : 20f, gw = 240f;
            float rowW = n * cw + (n - 1) * gap + (guide ? gap + gw : 0f);
            float x0 = 960f - rowW * 0.5f;
            for (int i = 0; i < n; i++)
                BuildCard(t, clips[i], i, x0 + i * (cw + gap), top, cw, h, clips[i] == best);
            if (guide) BuildSlotGuide(t, x0 + n * (cw + gap), top, gw, h, MaxSlots - n);
        }

        void BuildCard(Transform parent, CaptureResult c, int index, float x, float y, float w, float h, bool isBest)
        {
            const float pad = 14f, stageH = 352f;
            var card = UIParts.Panel("Clip" + index, parent, UIParts.PanelBg, 14, isBest ? UIKit.Accent : UIParts.Ink(0.22f), isBest ? 1.5f : 1f);
            var rt = card.rectTransform;
            UIKit.Place(rt, x, y, w, h);
            var cg = UIParts.Group(card.gameObject, 0f);
            cardRects.Add(rt); cardGroups.Add(cg);
            if (isBest)
            {
                var m = UIParts.RoundMask("Glow", card.transform, 14);
                m.SetAsFirstSibling();
                var g = UIKit.Image("Grad", m, UIKit.A(UIKit.Accent, 0.09f), UIParts.VGradient(new[] { 0f, 0.38f }, new[] { 1f, 0f }));
                UIKit.Stretch(g.rectTransform);
            }
            var ct = card.transform;

            // 9:16 화면
            float sw = w - pad * 2f;
            var stage = UIParts.Panel("Stage", ct, UIParts.ScreenBg, 10, UIParts.Ink(0.1f));
            UIKit.Place(stage.rectTransform, pad, pad, sw, stageH);
            var clip = UIParts.RoundMask("Clip", stage.transform, 10);
            const float pw = 198f;
            float px = (sw - pw) * 0.5f;
            var bars = new List<RectTransform>();
            var heights = new List<float>();
            if (c.Category == CaptureCategory.Sound) BuildWaveform(clip, c, index, px, pw, stageH, bars, heights);
            else BuildSnapshot(clip, c, px, pw, stageH);
            UIParts.Bar("EdgeL", clip, UIParts.Ink(0.14f), px - 1f, 0, 1, stageH);
            UIParts.Bar("EdgeR", clip, UIParts.Ink(0.14f), px + pw, 0, 1, stageH);
            cardBars.Add(bars); cardBarHeights.Add(heights);

            // 등급 배지
            var badge = UIParts.Fill("Grade", stage.transform, GradeColor(c.Grade), 8);
            UIKit.Place(badge.rectTransform, 12, 12, 34, 34);
            UIParts.Label(badge.transform, c.Grade.ToString(), 16, UIParts.Dark, true, TextAnchor.MiddleCenter, 0, 0, 34, 34);
            // 종류·시각
            var cats = UIStrings.ClipCategories;
            int ci = (int)c.Category;
            string cat = cats != null && ci >= 0 && ci < cats.Length ? cats[ci] : c.Category.ToString();
            var typeTag = UIParts.Pill(stage.transform, cat, 13, UIParts.Ink(0.72f), UIParts.Shade(0.82f), UIParts.Ink(0.22f), true, 0, 12, 26, 8, 6);
            UIParts.AlignRight(typeTag, sw - 12);
            var timeTag = UIParts.Pill(stage.transform, UIParts.Clock(c.GameTime - UIFlow.PlayStartTime), 13, UIParts.Ink(0.72f), UIParts.Shade(0.85f), new Color(0, 0, 0, 0), true, 0, stageH - 10 - 24, 24, 7, 5);
            UIParts.AlignRight(timeTag, sw - 12);

            // 이름·수치
            float by = pad + stageH + 14f;
            var nm = UIParts.Label(ct, "", 16, UIKit.Ink, true, TextAnchor.MiddleLeft, pad, by, sw, 24);
            UIParts.FitOneLine(nm, c.TargetName, sw);
            var vw = UIParts.Label(ct, string.Format(UIStrings.ClipViewersFormat, c.Viewers), 16, UIParts.Green, true, TextAnchor.MiddleLeft, pad, by + 32, sw * 0.6f, 24);
            vw.horizontalOverflow = HorizontalWrapMode.Overflow;
            var nv = UIParts.Label(ct, string.Format(UIStrings.ClipNewsFormat, c.NewsValue), 13, UIKit.Accent, true, TextAnchor.MiddleRight, pad + sw * 0.5f, by + 32, sw * 0.5f, 24);
            nv.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIParts.Bar("Div", ct, UIParts.Ink(0.12f), pad, by + 66, sw, 1);

            // 댓글 2줄
            string[] cm = TargetLines.Comments(c, index);
            var nicks = UIStrings.CommentNicks;
            for (int j = 0; j < 2 && cm != null && j < cm.Length; j++)
            {
                float cy = by + 80 + j * 30;
                string nick = nicks != null && nicks.Length > 0 ? nicks[(index * 2 + j + (int)c.Grade) % nicks.Length] : "";
                var av = UIParts.Panel("Avatar", ct, UIParts.Ink(0.14f), 11, UIParts.Ink(0.25f));
                UIKit.Place(av.rectTransform, pad, cy, 22, 22);
                UIParts.Label(av.transform, nick.Length > 0 ? nick.Substring(0, 1) : "", 13, UIKit.Ink, true, TextAnchor.MiddleCenter, 0, 0, 22, 22);
                var nt = UIParts.Label(ct, nick, 13, UIKit.Ink, true, TextAnchor.MiddleLeft, pad + 30, cy, 120, 22);
                nt.horizontalOverflow = HorizontalWrapMode.Overflow;
                float nw = Mathf.Min(110f, Mathf.Ceil(UIParts.Width(nt)));
                UIParts.FitOneLine(nt, nick, 110f);
                float mx = pad + 30 + nw + 8;
                var mt = UIParts.Label(ct, "", 13, UIParts.Ink(0.72f), false, TextAnchor.MiddleLeft, mx, cy, pad + sw - mx, 22);
                UIParts.FitOneLine(mt, cm[j], pad + sw - mx);
            }
        }

        void BuildSnapshot(Transform parent, CaptureResult c, float x, float w, float h)
        {
            var go = new GameObject("Snapshot", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.raycastTarget = false;
            UIKit.Place(raw.rectTransform, x, 0, w, h);
            if (c.Snapshot != null)
            {
                raw.texture = c.Snapshot;
                float texA = (float)c.Snapshot.width / Mathf.Max(1, c.Snapshot.height);
                float want = w / h;
                if (texA > want) { float u = want / texA; raw.uvRect = new UnityEngine.Rect((1 - u) * 0.5f, 0, u, 1); }
                else { float v = texA / want; raw.uvRect = new UnityEngine.Rect(0, (1 - v) * 0.5f, 1, v); }
            }
            else raw.color = UIKit.Hex("#1A1B22");
        }

        // 녹음 클립: 대상·시각으로 고정된 막대 파형 + 설교 한 줄. 잘 녹음될수록 금빛이 진하다.
        void BuildWaveform(Transform parent, CaptureResult c, int index, float x, float w, float h, List<RectTransform> bars, List<float> heights)
        {
            var bgImg = UIKit.Image("WaveBg", parent, UIKit.Hex("#0B0C10"));
            UIKit.Place(bgImg.rectTransform, x, 0, w, h);
            var top = UIKit.Image("WaveTop", parent, UIKit.Hex("#12141C"), UIParts.VGradient(new[] { 0f, 1f }, new[] { 1f, 0f }));
            UIKit.Place(top.rectTransform, x, 0, w, h);

            var label = UIParts.Pill(parent, UIStrings.ClipCategories != null && UIStrings.ClipCategories.Length > 3 ? UIStrings.ClipCategories[3] : "녹음",
                13, UIKit.Rec, new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), true, 0, 52, 22, 0, -1, UIKit.Rec);
            label.anchoredPosition = new Vector2(x + (w - label.sizeDelta.x) * 0.5f, -52);

            const int count = 18;
            const float barW = 5f, areaW = 164f, areaH = 116f, cy = 150f;
            float gap = (areaW - count * barW) / (count - 1);
            float ax = x + (w - areaW) * 0.5f;
            int seed = 17;
            foreach (char ch in (c.TargetId ?? "")) seed = seed * 31 + ch;
            seed ^= Mathf.RoundToInt(c.GameTime * 10f);
            var rng = new System.Random(seed);
            bool good = c.AverageQuality >= 0.5f;
            var col = good ? UIKit.A(UIKit.Accent, Mathf.Lerp(0.6f, 0.95f, Mathf.Clamp01(c.AverageQuality))) : UIKit.A(UIKit.Hex("#8B92A5"), 0.65f);
            for (int i = 0; i < count; i++)
            {
                float env = Mathf.Sin(Mathf.PI * (i + 0.5f) / count);
                float amp = (0.3f + 0.7f * (float)rng.NextDouble()) * Mathf.Lerp(0.35f, 1f, env);
                float bh = Mathf.Max(8f, amp * areaH);
                var bar = UIParts.Fill("Wave" + i, parent, col, 2.5f);
                var rt = bar.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(ax + barW * 0.5f + i * (barW + gap), -cy);
                rt.sizeDelta = new Vector2(barW, 0f);
                bars.Add(rt); heights.Add(bh);
            }

            var quotes = UIStrings.SoundQuotes;
            if (quotes != null && quotes.Length > 0)
            {
                string q = quotes[index % quotes.Length];
                var probe = UIParts.Label(parent, q, 13, UIParts.Ink(0.72f), false, TextAnchor.MiddleCenter);
                float tw = w - 32f - 20f;
                float th = Mathf.Ceil(UIParts.Height(probe, tw));
                float bh = th + 16f;
                var box = UIParts.Panel("Quote", parent, UIParts.Ink(0.05f), 8, UIParts.Ink(0.12f));
                UIKit.Place(box.rectTransform, x + 16, h - 40 - bh, w - 32, bh);
                probe.transform.SetParent(box.transform, false);
                UIKit.Place(probe.rectTransform, 10, 8, tw, th);
            }
        }

        void BuildSlotGuide(Transform parent, float x, float y, float w, float h, int left)
        {
            var card = UIParts.Panel("SlotGuide", parent, UIKit.A(UIParts.PanelBg, 0.45f), 14, UIParts.Ink(0.16f));
            var rt = card.rectTransform;
            UIKit.Place(rt, x, y, w, h);
            cardRects.Add(rt); cardGroups.Add(UIParts.Group(card.gameObject, 0f));
            cardBars.Add(new List<RectTransform>()); cardBarHeights.Add(new List<float>());
            var t = card.transform;
            UIParts.Pill(t, string.Format(UIStrings.SlotGuideBadgeFormat, left, MaxSlots), 13, UIParts.Ink(0.56f), UIParts.Ink(0.06f), new Color(0, 0, 0, 0), true, 18, 20, 24, 8, 6);
            var title = UIParts.Label(t, UIStrings.SlotGuideTitle, 16, UIParts.Ink(0.72f), true, TextAnchor.UpperLeft, 18, 56, w - 36, 48);
            title.lineSpacing = 1.15f;
            int rows = Mathf.Min(left, 5);
            for (int i = 0; i < rows; i++)
            {
                var slot = UIParts.Panel("Slot", t, UIParts.Shade(0.6f), 10, UIParts.Ink(0.12f));
                UIKit.Place(slot.rectTransform, 18, 124 + i * 56, w - 36, 44);
                UIParts.Label(slot.transform, string.Format(UIStrings.SlotEmptyFormat, MaxSlots - left + i + 1), 13, UIParts.Ink(0.48f), true, TextAnchor.MiddleLeft, 14, 0, w - 64, 44);
            }
            UIParts.Bar("FootLine", t, UIParts.Ink(0.12f), 18, h - 64, w - 36, 1);
            UIParts.Label(t, UIStrings.SlotGuideFoot, 13, UIParts.Ink(0.48f), false, TextAnchor.UpperLeft, 18, h - 52, w - 36, 40);
        }

        void BuildEmpty(Transform parent, float x, float y, float w, float h)
        {
            var card = UIParts.Panel("Empty", parent, UIKit.A(UIParts.PanelBg, 0.6f), 14, UIParts.Ink(0.12f));
            var rt = card.rectTransform;
            UIKit.Place(rt, x, y, w, h);
            cardRects.Add(rt); cardGroups.Add(UIParts.Group(card.gameObject, 0f));
            cardBars.Add(new List<RectTransform>()); cardBarHeights.Add(new List<float>());
            var t = card.transform;
            var lens = UIParts.Ring("Lens", t, UIParts.Ink(0.3f), 64, 1.5f);
            UIKit.Place(lens.rectTransform, (w - 64) * 0.5f, 84, 64, 64);
            var slash = UIParts.Bar("Slash", t, UIParts.Ink(0.3f), (w - 84) * 0.5f, 115, 84, 2);
            slash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            slash.rectTransform.anchoredPosition += new Vector2(42, -1);
            slash.rectTransform.localRotation = Quaternion.Euler(0, 0, -35f);
            UIParts.Label(t, UIStrings.EmptyClipsTitle, 24, UIKit.Ink, true, TextAnchor.MiddleCenter, 0, 176, w, 36);
            UIParts.Label(t, UIStrings.EmptyClipsBody, 16, UIParts.Ink(0.56f), false, TextAnchor.MiddleCenter, 0, 220, w, 28);
        }

        void BuildBottomBar()
        {
            var bar = UIParts.Panel("BottomBar", root, UIParts.PanelMuted, 14, UIParts.Ink(0.12f));
            UIKit.Place(bar.rectTransform, 80, 984, 1760, 64);
            var t = bar.transform;
            float x = 20;
            var k1 = UIParts.Key(t, "R", 13, UIKit.Ink, UIParts.Ink(0.1f), UIParts.Ink(0.22f), x, 20, 24, 28);
            x += k1.sizeDelta.x + 8;
            var h1 = UIParts.Label(t, UIStrings.ResultsRestartHint, 13, UIParts.Ink(0.72f), false, TextAnchor.MiddleLeft, x, 0, 300, 64);
            h1.horizontalOverflow = HorizontalWrapMode.Overflow;
            x += Mathf.Ceil(UIParts.Width(h1)) + 20;
            var k2 = UIParts.Key(t, "Esc", 13, UIKit.Ink, UIParts.Ink(0.1f), UIParts.Ink(0.22f), x, 20, 24, 28);
            x += k2.sizeDelta.x + 8;
            UIParts.Label(t, UIStrings.ResultsMainHint, 13, UIParts.Ink(0.72f), false, TextAnchor.MiddleLeft, x, 0, 300, 64);

            var again = UIParts.Button("Again", t, 1760 - 20 - 168, 10, 168, 44, UIKit.Accent, UIParts.GoldBright, 10, () => UIFlow.Restart(true));
            UIParts.Key(again.transform, "R", 13, UIParts.Dark, UIKit.A(UIParts.Dark, 0.14f), UIKit.A(UIParts.Dark, 0.3f), 20, 10, 24, 28);
            UIParts.Label(again.transform, UIStrings.ResultsRestart, 16, UIParts.Dark, true, TextAnchor.MiddleLeft, 60, 0, 100, 44);
            var main = UIParts.Button("Main", t, 1760 - 20 - 168 - 12 - 124, 10, 124, 44, UIParts.Ink(0f), UIParts.Ink(0.22f), 10, () => UIFlow.Restart(false));
            UIParts.Label(main.transform, UIStrings.ResultsMain, 16, UIKit.Ink, true, TextAnchor.MiddleCenter, 0, 0, 124, 44);
        }

        // ---------- 연출 ----------

        IEnumerator Sequence()
        {
            // 0.0초: 상단과 판 틀
            yield return UIParts.FadeTo(group, 1f, 0.3f);
            // 0.4초: 숫자 카운트업
            StartCoroutine(UIParts.Anim(0.8f, SetCounts, 0.1f));
            yield return Wait(0.7f);
            // 1.0초: 카드 순차
            for (int i = 0; i < cardRects.Count; i++)
            {
                StartCoroutine(RiseCard(i));
                yield return Wait(0.08f);
            }
            float waited = 1.0f + cardRects.Count * 0.08f;
            if (waited < 2.0f) yield return Wait(2.0f - waited);
            // 2.0초: 판정 도장
            Stamp();
        }

        static IEnumerator Wait(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
        }

        IEnumerator RiseCard(int i)
        {
            var rt = cardRects[i];
            var cg = cardGroups[i];
            Vector2 end = rt.anchoredPosition, start = end + new Vector2(0, -40f);
            var bars = cardBars[i];
            var hs = cardBarHeights[i];
            yield return UIParts.Anim(0.35f, k =>
            {
                rt.anchoredPosition = Vector2.LerpUnclamped(start, end, k);
                cg.alpha = k;
            });
            if (bars.Count > 0)
                yield return UIParts.Anim(0.4f, k =>
                {
                    for (int b = 0; b < bars.Count; b++)
                        if (bars[b] != null) bars[b].sizeDelta = new Vector2(bars[b].sizeDelta.x, hs[b] * k);
                });
        }

        void Stamp()
        {
            var vc = VerdictColor(verdict);
            headline.text = V(verdict, 1);
            headline.color = vc;
            UIParts.FitOneLine(headline, V(verdict, 1), 608f);
            headlineRect.pivot = new Vector2(0f, 0.5f);
            headlineRect.anchoredPosition = new Vector2(26f, -80f);
            StartCoroutine(UIParts.Anim(0.3f, k =>
            {
                headlineRect.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, k);
                headline.color = UIKit.A(vc, k);
            }));
            StartCoroutine(UIParts.FadeTo(descGroup, 1f, 0.3f, 0.15f));
            if (activeTile != null)
            {
                activeFill.color = UIKit.A(vc, 0.16f);
                if (activeLine != null) activeLine.color = vc;
                if (activeTitle != null) activeTitle.color = vc;
                StartCoroutine(UIParts.Anim(0.3f, k => activeTile.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, k)));
            }
        }
    }
}
