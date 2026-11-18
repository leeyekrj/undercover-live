using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 엔딩: 업로드 결과 화면(Argon 결과 A안 + 새 설정 엔딩). 위 상태 줄 · 왼쪽 엔딩 판정 카드(판정 3종 탭) · 오른쪽 자동 제목과 통계 3칸 ·
    /// 아래 16:9 가로 클립 카드 2열×3줄(대상별 최고 1장, 최대 6, 빈 칸은 빈 슬롯) · 맨 아래 메인으로 / R 다시 하기.
    /// 판정(우선순위): ① 진실 보도 = 보도 가치(대상별 최고값 합) ≥ newsThreshold 그리고 결정적 장면 확보 → 뉴스 속보 띠
    /// ② 오컬트 바이럴 = 최고 시청자 ≥ viralViewers 그리고 거대 존재 확보 → 그 클립 썸네일 위 커다란 눈이 천천히 한 번 깜박임
    /// ③ 빈손 — 흐릿한 해프닝.
    /// 등장: 0.0초 상단 → 0.4초 숫자 카운트업 → 1.0초 카드 순차 → 2.0초 판정 도장. 문구는 UIStrings.
    /// 키보드 ←→/↑↓·Enter, R 다시 하기, Esc 메인으로. 서체 크기: 32 · 24 · 18 · 15.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        [Header("엔딩 기준")]
        [Tooltip("진실 보도: 보도 가치(대상별 최고값 합)가 이 이상 + 결정적 장면 확보")]
        public float newsThreshold = 2.4f;
        [Tooltip("오컬트 바이럴: 최고 동시 시청자가 이 이상 + 거대 존재 확보")]
        public int viralViewers = 30000;
        public int maxCards = 6;
        [Tooltip("예전 버전 호환용(지금은 UIKit 서체를 쓴다)")]
        public Font font;

        const int MaxSlots = 6;
        const float CardW = 868f, CardH = 164f, ColGap = 24f, RowGap = 12f, DeckTop = 396f;
        const float ThumbW = 242f, ThumbH = 136f;

        Canvas canvas;
        RectTransform root;
        CanvasGroup group;
        KeyNav nav;
        bool shown;

        // 연출 대상
        Text peakText, clipsText, newsText, headline;
        int peakValue, clipsValue; float newsValue;
        CanvasGroup descGroup;
        RectTransform headlineRect, tickerRect, tickerText;
        CanvasGroup tickerGroup;
        readonly List<RectTransform> cardRects = new List<RectTransform>();
        readonly List<CanvasGroup> cardGroups = new List<CanvasGroup>();
        readonly List<List<RectTransform>> cardBars = new List<List<RectTransform>>();
        readonly List<List<float>> cardBarHeights = new List<List<float>>();
        RectTransform entityThumb;
        Image activeFill, activeLine; Text activeTitle; RectTransform activeTile;
        int verdict;

        public bool IsShown { get { return shown; } }

        void Awake()
        {
            // 씬에 구워진 옛 기준(보도 2.0)은 새 기준으로 올린다
            if (Mathf.Approximately(newsThreshold, 2f)) newsThreshold = 2.4f;
            GameEvents.ServiceFinished += OnServiceFinished;
        }

        void OnDestroy() { GameEvents.ServiceFinished -= OnServiceFinished; }
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
            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame) UIFlow.Restart(true);
                else if (kb.escapeKey.wasPressedThisFrame) UIFlow.Restart(false);
            }
            // 속보 띠 흐름
            if (tickerText != null && tickerGroup != null && tickerGroup.alpha > 0f)
            {
                var p = tickerText.anchoredPosition;
                p.x -= Time.unscaledDeltaTime * 110f;
                float w = tickerText.sizeDelta.x;
                if (p.x < -w) p.x = ((RectTransform)tickerText.parent).rect.width;
                tickerText.anchoredPosition = p;
            }
        }

        // ---------- 데이터 ----------

        static bool IdIs(string id, string prefix) { return !string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(prefix) && id.StartsWith(prefix); }

        static bool IdIn(string id, string[] prefixes)
        {
            if (prefixes == null) return false;
            foreach (var p in prefixes) if (IdIs(id, p)) return true;
            return false;
        }

        static string Key(CaptureResult c) { return string.IsNullOrEmpty(c.TargetId) ? c.TargetName ?? "" : c.TargetId; }

        static bool Better(CaptureResult a, CaptureResult b)
        {
            return a.Grade > b.Grade || (a.Grade == b.Grade && a.Viewers > b.Viewers);
        }

        /// <summary>대상별 최고 1장.</summary>
        static List<CaptureResult> BestPerTarget()
        {
            var best = new Dictionary<string, CaptureResult>();
            foreach (var c in CaptureLog.Clips)
            {
                string k = Key(c);
                if (!best.TryGetValue(k, out var cur) || Better(c, cur)) best[k] = c;
            }
            return new List<CaptureResult>(best.Values);
        }

        /// <summary>보도 가치: 같은 대상은 최고값 1회만 합산.</summary>
        static float NewsSum()
        {
            var max = new Dictionary<string, float>();
            foreach (var c in CaptureLog.Clips)
            {
                string k = Key(c);
                if (!max.TryGetValue(k, out var v) || c.NewsValue > v) max[k] = c.NewsValue;
            }
            float s = 0f;
            foreach (var v in max.Values) s += v;
            return s;
        }

        List<CaptureResult> PickClips(List<CaptureResult> perTarget)
        {
            var all = new List<CaptureResult>(perTarget);
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
            foreach (var c in clips) if (best == null || Better(c, best)) best = c;
            return best;
        }

        static string NameOf(CaptureResult c) { return UIStrings.TargetName(c.TargetId, c.TargetName); }

        /// <summary>0 = 진실 보도, 1 = 오컬트 바이럴, 2 = 빈손.</summary>
        int Verdict(int peak, float news, bool decisive, bool entity)
        {
            if (news >= newsThreshold && decisive) return 0;
            if (peak >= viralViewers && entity) return 1;
            return 2;
        }

        static Color VerdictColor(int v)
        {
            return v == 0 ? UIParts.GoldBright : v == 1 ? UIParts.RecSoft : UIParts.Ink(0.8f);
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
            canvas = UIKit.Canvas("ResultsCanvas", 180, transform);
            root = (RectTransform)canvas.transform;
            group = UIParts.Group(canvas.gameObject, 0f);
            nav = KeyNav.On(canvas);
            var bg = UIKit.Image("Bg", root, UIParts.Bg);
            UIKit.Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            var perTarget = BestPerTarget();
            var clips = PickClips(perTarget);
            float news = NewsSum();
            int peak = PeakViewers(CaptureLog.TotalViewersGained());
            bool decisive = false, entity = false;
            foreach (var c in perTarget)
            {
                if (IdIn(c.TargetId, UIStrings.DecisiveTargetIds)) decisive = true;
                if (IdIs(c.TargetId, UIStrings.EntityTargetId)) entity = true;
            }
            verdict = Verdict(peak, news, decisive, entity);
            var best = Best(clips);

            BuildTopBar(reason);
            BuildVerdictCard(perTarget.Count);
            BuildAnalytics(perTarget, best, peak, news);
            BuildDeck(clips, best);
            if (verdict == 0) BuildTicker();
            BuildBottomBar();
        }

        void BuildTopBar(string reason)
        {
            var t = root;
            var dot = UIParts.Dot("LiveDot", t, UIKit.Rec, 10);
            UIKit.Place(dot.rectTransform, 80, 43, 10, 10);
            var name = UIParts.Label(t, UIStrings.AppName, 15, UIKit.Ink, true, TextAnchor.MiddleLeft, 102, 34, 300, 28);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            float x = 102 + Mathf.Ceil(UIParts.Width(name)) + 14;
            UIParts.Bar("Divider", t, UIParts.Ink(0.22f), x, 40, 1, 16);
            x += 15;
            var status = UIParts.Label(t, UIStrings.StudioStatus, 15, UIParts.Ink(0.72f), false, TextAnchor.MiddleLeft, x, 34, 600, 28);
            status.horizontalOverflow = HorizontalWrapMode.Overflow;
            x += Mathf.Ceil(UIParts.Width(status)) + 14;
            UIParts.Pill(t, UIStrings.StudioTag, 15, UIKit.Accent, UIKit.A(UIKit.Accent, 0.12f), UIKit.A(UIKit.Accent, 0.35f), true, x, 33, 30, 12);
            if (!string.IsNullOrEmpty(reason))
                UIParts.Label(t, reason, 15, UIParts.Ink(0.52f), false, TextAnchor.MiddleRight, 1240, 34, 600, 28);
            UIParts.Bar("TopLine", t, UIParts.Ink(0.12f), 80, 75, 1760, 1);
        }

        void BuildVerdictCard(int targets)
        {
            var card = UIParts.Panel("Verdict", root, UIKit.A(UIParts.PanelElev, 0.96f), 16, UIKit.Accent, 1.5f);
            UIKit.Place(card.rectTransform, 80, 92, 660, 248);
            var mask = UIParts.RoundMask("Glow", card.transform, 16);
            mask.SetAsFirstSibling();
            var grad = UIKit.Image("Grad", mask, UIKit.A(UIKit.Accent, 0.14f), UIParts.HGradient(new[] { 0f, 0.65f }, new[] { 1f, 0f }));
            UIKit.Stretch(grad.rectTransform);
            var t = card.transform;

            UIParts.Pill(t, UIStrings.VerdictBadge, 15, UIParts.Dark, UIKit.Accent, new Color(0, 0, 0, 0), true, 26, 20, 30, 12, -1, UIParts.Dark);
            UIParts.Label(t, string.Format(UIStrings.VerdictMetaFormat, targets), 15, UIParts.Ink(0.72f), false, TextAnchor.MiddleRight, 334, 20, 300, 30);

            headline = UIParts.Label(t, UIStrings.VerdictPending, 32, UIParts.Ink(0.4f), true, TextAnchor.MiddleLeft, 26, 58, 608, 44);
            headline.horizontalOverflow = HorizontalWrapMode.Overflow;
            headlineRect = headline.rectTransform;
            var desc = UIParts.Label(t, V(verdict, 2), 15, UIParts.Ink(0.76f), false, TextAnchor.UpperLeft, 26, 106, 608, 52);
            desc.lineSpacing = 1.15f;
            descGroup = UIParts.Group(desc.gameObject, 0f);

            UIParts.Bar("TierLine", t, UIParts.Ink(0.12f), 26, 164, 608, 1);
            float tw = (608f - 16f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var tile = UIParts.Panel("Tier" + i, t, UIParts.Shade(0.55f), 8, UIParts.Ink(0.12f));
                UIKit.Place(tile.rectTransform, 26 + i * (tw + 8f), 174, tw, 56);
                var title = UIParts.Label(tile.transform, V(i, 0), 15, UIParts.Ink(0.78f), true, TextAnchor.MiddleLeft, 10, 4, tw - 20, 24);
                var sub = UIParts.Label(tile.transform, "", 15, UIParts.Ink(0.5f), false, TextAnchor.MiddleLeft, 10, 28, tw - 20, 24);
                UIParts.FitOneLine(sub, V(i, 3), tw - 20);
                if (i == verdict)
                {
                    activeTile = tile.rectTransform;
                    activeFill = tile;
                    var lt = tile.transform.Find("Line");
                    activeLine = lt != null ? lt.GetComponent<Image>() : null;
                    activeTitle = title;
                    activeTile.pivot = new Vector2(0.5f, 0.5f);
                    activeTile.anchoredPosition += new Vector2(tw * 0.5f, -28f);
                }
            }
        }

        void BuildAnalytics(List<CaptureResult> perTarget, CaptureResult best, int peak, float news)
        {
            var panel = UIParts.Panel("Analytics", root, UIParts.PanelBg, 16, UIParts.Ink(0.22f));
            UIKit.Place(panel.rectTransform, 760, 92, 1080, 248);
            var t = panel.transform;
            int total = perTarget.Count;

            UIParts.Label(t, UIStrings.AutoTitleEyebrow, 15, UIParts.Ink(0.52f), true, TextAnchor.MiddleLeft, 26, 16, 700, 24);
            var badgeText = total > 0 ? UIStrings.UploadDone : UIStrings.UploadNone;
            var badgeCol = total > 0 ? UIParts.Green : UIParts.Ink(0.6f);
            var badge = UIParts.Pill(t, badgeText, 15, badgeCol, UIKit.A(badgeCol, 0.14f), UIKit.A(badgeCol, 0.35f), true, 0, 20, 32, 12, 8, badgeCol);
            UIParts.AlignRight(badge, 1080 - 26);
            string title = best != null ? string.Format(UIStrings.AutoTitleFormat, NameOf(best)) : UIStrings.AutoTitleEmpty;
            float tw = 1080 - 52 - badge.sizeDelta.x - 24;
            var tt = UIParts.Label(t, "", 24, UIKit.Ink, true, TextAnchor.MiddleLeft, 26, 42, tw, 40);
            UIParts.FitOneLine(tt, title, tw);
            UIParts.Bar("Div", t, UIParts.Ink(0.12f), 26, 96, 1028, 1);

            int sound = 0;
            foreach (var c in perTarget) if (c.Category == CaptureCategory.Sound) sound++;
            float mw = (1028f - 32f) / 3f;
            peakValue = peak; clipsValue = total; newsValue = news;
            peakText = Metric(t, 26, mw, UIStrings.MetricPeak, UIStrings.MetricPeakChip,
                best != null ? string.Format(UIStrings.MetricPeakSubFormat, NameOf(best)) : UIStrings.MetricPeakSubNone, false);
            clipsText = Metric(t, 26 + (mw + 16f), mw, UIStrings.MetricClips, Mathf.Min(total, MaxSlots) + " / " + MaxSlots,
                string.Format(UIStrings.MetricClipsSubFormat, total - sound, sound), false);
            bool reached = news >= newsThreshold;
            newsText = Metric(t, 26 + (mw + 16f) * 2f, mw, UIStrings.MetricNews, string.Format(UIStrings.MetricNewsChipFormat, newsThreshold),
                reached ? UIStrings.MetricNewsReached : string.Format(UIStrings.MetricNewsShortFormat, Mathf.Max(0f, newsThreshold - news)), true);
            SetCounts(0f);
        }

        Text Metric(Transform parent, float x, float w, string label, string chip, string sub, bool highlight)
        {
            var card = UIParts.Panel("Metric", parent, UIParts.PanelElev, 12, highlight ? UIKit.A(UIKit.Accent, 0.45f) : UIParts.Ink(0.12f));
            UIKit.Place(card.rectTransform, x, 110, w, 122);
            var t = card.transform;
            UIParts.Label(t, label, 15, UIParts.Ink(0.74f), false, TextAnchor.MiddleLeft, 16, 10, w - 130, 26);
            var c = UIParts.Pill(t, chip, 15, UIParts.Ink(0.72f), UIParts.Ink(0.08f), new Color(0, 0, 0, 0), true, 0, 10, 26, 8, 5);
            UIParts.AlignRight(c, w - 12);
            var v = UIParts.Label(t, "0", 32, highlight ? UIParts.GoldBright : UIKit.Ink, true, TextAnchor.MiddleLeft, 16, 40, w - 32, 44);
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            var s = UIParts.Label(t, "", 15, UIParts.Ink(0.52f), false, TextAnchor.MiddleLeft, 16, 88, w - 32, 24);
            UIParts.FitOneLine(s, sub, w - 32);
            return v;
        }

        void SetCounts(float k)
        {
            if (peakText != null) peakText.text = Mathf.RoundToInt(peakValue * k).ToString("N0") + "명";
            if (clipsText != null) clipsText.text = Mathf.RoundToInt(clipsValue * k) + "개";
            if (newsText != null) newsText.text = (newsValue * k).ToString("0.0");
        }

        // ---------- 클립 카드 (16:9 가로, 2열×3줄) ----------

        void BuildDeck(List<CaptureResult> clips, CaptureResult best)
        {
            var t = root;
            var head = UIParts.Label(t, UIStrings.ClipsHeader, 18, UIKit.Ink, true, TextAnchor.MiddleLeft, 80, 354, 400, 32);
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            float hx = 80 + Mathf.Ceil(UIParts.Width(head)) + 12;
            UIParts.Pill(t, string.Format(UIStrings.ClipsOrderFormat, clips.Count), 15, UIParts.Ink(0.72f), UIParts.Ink(0.08f), new Color(0, 0, 0, 0), true, hx, 356, 28, 10);

            int n = clips.Count;
            if (n == 0)
            {
                float gridH = CardH * 3 + RowGap * 2;
                BuildEmpty(t, 960 - 440, DeckTop + (gridH - 300) * 0.5f, 880, 300);
                return;
            }
            for (int i = 0; i < MaxSlots; i++)
            {
                int col = i % 2, row = i / 2;
                float x = 80 + col * (CardW + ColGap);
                float y = DeckTop + row * (CardH + RowGap);
                if (i < n) BuildCard(t, clips[i], i, x, y, clips[i] == best);
                else BuildEmptySlot(t, i, x, y, i == n);
            }
        }

        void BuildCard(Transform parent, CaptureResult c, int index, float x, float y, bool isBest)
        {
            const float pad = 14f;
            var card = UIParts.Panel("Clip" + index, parent, UIParts.PanelBg, 14, isBest ? UIKit.Accent : UIParts.Ink(0.22f), isBest ? 1.5f : 1f);
            var rt = card.rectTransform;
            UIKit.Place(rt, x, y, CardW, CardH);
            AddAnimated(rt, UIParts.Group(card.gameObject, 0f));
            if (isBest)
            {
                var m = UIParts.RoundMask("Glow", card.transform, 14);
                m.SetAsFirstSibling();
                var g = UIKit.Image("Grad", m, UIKit.A(UIKit.Accent, 0.09f), UIParts.HGradient(new[] { 0f, 0.6f }, new[] { 1f, 0f }));
                UIKit.Stretch(g.rectTransform);
            }
            var ct = card.transform;

            // 16:9 썸네일
            var thumb = UIParts.Panel("Thumb", ct, UIParts.ScreenBg, 8, UIParts.Ink(0.1f));
            UIKit.Place(thumb.rectTransform, pad, pad, ThumbW, ThumbH);
            var clip = UIParts.RoundMask("Clip", thumb.transform, 8);
            var bars = new List<RectTransform>();
            var heights = new List<float>();
            if (c.Category == CaptureCategory.Sound) BuildWaveform(clip, c, bars, heights);
            else BuildSnapshot(clip, c);
            cardBars[cardBars.Count - 1] = bars;
            cardBarHeights[cardBarHeights.Count - 1] = heights;
            if (IdIs(c.TargetId, UIStrings.EntityTargetId)) entityThumb = clip;

            var badge = UIParts.Fill("Grade", thumb.transform, GradeColor(c.Grade), 7);
            UIKit.Place(badge.rectTransform, 8, 8, 32, 32);
            UIParts.Label(badge.transform, c.Grade.ToString(), 18, UIParts.Dark, true, TextAnchor.MiddleCenter, 0, 0, 32, 32);
            var timeTag = UIParts.Pill(thumb.transform, UIParts.Clock(c.GameTime - UIFlow.PlayStartTime), 15, UIParts.Ink(0.8f), UIParts.Shade(0.85f), new Color(0, 0, 0, 0), true, 0, ThumbH - 8 - 26, 26, 7, 5);
            UIParts.AlignRight(timeTag, ThumbW - 8);

            // 오른쪽 정보
            float ix = pad + ThumbW + 20f, iw = CardW - ix - 20f;
            var cats = UIStrings.ClipCategories;
            int ci = (int)c.Category;
            string cat = cats != null && ci >= 0 && ci < cats.Length ? cats[ci] : c.Category.ToString();
            var catPill = UIParts.Pill(ct, cat, 15, UIParts.Ink(0.74f), UIParts.Ink(0.07f), UIParts.Ink(0.18f), true, ix, 16, 28, 9, 6);
            float nx = ix + catPill.sizeDelta.x + 10f;
            var nm = UIParts.Label(ct, "", 18, UIKit.Ink, true, TextAnchor.MiddleLeft, nx, 14, ix + iw - nx, 32);
            UIParts.FitOneLine(nm, NameOf(c), ix + iw - nx);

            var vw = UIParts.Label(ct, string.Format(UIStrings.ClipViewersFormat, c.Viewers), 18, UIParts.Green, true, TextAnchor.MiddleLeft, ix, 50, 260, 28);
            vw.horizontalOverflow = HorizontalWrapMode.Overflow;
            float vx = ix + Mathf.Ceil(UIParts.Width(vw)) + 16f;
            var nv = UIParts.Label(ct, string.Format(UIStrings.ClipNewsFormat, c.NewsValue), 15, UIKit.Accent, true, TextAnchor.MiddleLeft, vx, 50, 220, 28);
            nv.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIParts.Bar("Div", ct, UIParts.Ink(0.12f), ix, 88, iw, 1);

            // 댓글 2줄 (녹음 클립은 첫 줄에 설교 한 줄)
            string[] cm = TargetLines.Comments(c, index);
            var nicks = UIStrings.CommentNicks;
            var quotes = UIStrings.SoundQuotes;
            for (int j = 0; j < 2; j++)
            {
                float cy = 98 + j * 28;
                string msg = cm != null && j < cm.Length ? cm[j] : "";
                string nick = nicks != null && nicks.Length > 0 ? nicks[(index * 2 + j + (int)c.Grade) % nicks.Length] : "";
                if (c.Category == CaptureCategory.Sound && j == 0 && quotes != null && quotes.Length > 0)
                {
                    var q = UIParts.Label(ct, "", 15, UIKit.A(UIKit.Accent, 0.9f), false, TextAnchor.MiddleLeft, ix, cy, iw, 24);
                    UIParts.FitOneLine(q, quotes[index % quotes.Length], iw);
                    continue;
                }
                var av = UIParts.Panel("Avatar", ct, UIParts.Ink(0.14f), 11, UIParts.Ink(0.25f));
                UIKit.Place(av.rectTransform, ix, cy + 1, 22, 22);
                UIParts.Label(av.transform, nick.Length > 0 ? nick.Substring(0, 1) : "", 15, UIKit.Ink, true, TextAnchor.MiddleCenter, 0, 0, 22, 22);
                var nt = UIParts.Label(ct, nick, 15, UIKit.Ink, true, TextAnchor.MiddleLeft, ix + 30, cy, 140, 24);
                nt.horizontalOverflow = HorizontalWrapMode.Overflow;
                float nw = Mathf.Min(130f, Mathf.Ceil(UIParts.Width(nt)));
                UIParts.FitOneLine(nt, nick, 130f);
                float mx = ix + 30 + nw + 10;
                var mt = UIParts.Label(ct, "", 15, UIParts.Ink(0.74f), false, TextAnchor.MiddleLeft, mx, cy, ix + iw - mx, 24);
                UIParts.FitOneLine(mt, msg, ix + iw - mx);
            }
        }

        void AddAnimated(RectTransform rt, CanvasGroup g)
        {
            cardRects.Add(rt); cardGroups.Add(g);
            cardBars.Add(new List<RectTransform>()); cardBarHeights.Add(new List<float>());
        }

        void BuildSnapshot(Transform parent, CaptureResult c)
        {
            var go = new GameObject("Snapshot", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.raycastTarget = false;
            UIKit.Stretch(raw.rectTransform);
            if (c.Snapshot != null)
            {
                raw.texture = c.Snapshot;
                float texA = (float)c.Snapshot.width / Mathf.Max(1, c.Snapshot.height);
                float want = ThumbW / ThumbH;
                if (texA > want) { float u = want / texA; raw.uvRect = new UnityEngine.Rect((1 - u) * 0.5f, 0, u, 1); }
                else { float v = texA / want; raw.uvRect = new UnityEngine.Rect(0, (1 - v) * 0.5f, 1, v); }
            }
            else raw.color = UIKit.Hex("#1A1B22");
        }

        // 녹음 클립: 대상·시각으로 고정된 가로 막대 파형. 잘 녹음될수록 금빛이 진하다.
        void BuildWaveform(Transform parent, CaptureResult c, List<RectTransform> bars, List<float> heights)
        {
            var top = UIKit.Image("WaveTop", parent, UIKit.Hex("#12141C"), UIParts.VGradient(new[] { 0f, 1f }, new[] { 1f, 0f }));
            UIKit.Stretch(top.rectTransform);
            var label = UIParts.Pill(parent, UIStrings.ClipCategories != null && UIStrings.ClipCategories.Length > 3 ? UIStrings.ClipCategories[3] : "녹음",
                15, UIKit.Rec, new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), true, 0, 8, 26, 0, -1, UIKit.Rec);
            UIParts.AlignRight(label, ThumbW - 10);

            const int count = 30;
            const float barW = 4f, areaW = 200f, areaH = 70f, cy = 72f;
            float gap = (areaW - count * barW) / (count - 1);
            float ax = (ThumbW - areaW) * 0.5f;
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
                var bar = UIParts.Fill("Wave" + i, parent, col, 2f);
                var rt = bar.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(ax + barW * 0.5f + i * (barW + gap), -cy);
                rt.sizeDelta = new Vector2(barW, 0f);
                bars.Add(rt); heights.Add(Mathf.Max(6f, amp * areaH));
            }
        }

        void BuildEmptySlot(Transform parent, int i, float x, float y, bool first)
        {
            var slot = UIParts.Panel("EmptySlot" + i, parent, UIKit.A(UIParts.PanelBg, 0.35f), 14, UIParts.Ink(0.1f));
            var rt = slot.rectTransform;
            UIKit.Place(rt, x, y, CardW, CardH);
            AddAnimated(rt, UIParts.Group(slot.gameObject, 0f));
            var thumb = UIParts.Panel("Thumb", slot.transform, UIParts.Ink(0.02f), 8, UIParts.Ink(0.08f));
            UIKit.Place(thumb.rectTransform, 14, 14, ThumbW, ThumbH);
            UIParts.Label(slot.transform, string.Format(UIStrings.SlotEmptyFormat, i + 1), 18, UIParts.Ink(0.4f), true, TextAnchor.MiddleLeft, 14 + ThumbW + 20, 14, 400, 32);
            if (first) UIParts.Label(slot.transform, UIStrings.SlotEmptyHint, 15, UIParts.Ink(0.4f), false, TextAnchor.MiddleLeft, 14 + ThumbW + 20, 50, 500, 28);
        }

        void BuildEmpty(Transform parent, float x, float y, float w, float h)
        {
            var card = UIParts.Panel("Empty", parent, UIKit.A(UIParts.PanelBg, 0.6f), 14, UIParts.Ink(0.12f));
            var rt = card.rectTransform;
            UIKit.Place(rt, x, y, w, h);
            AddAnimated(rt, UIParts.Group(card.gameObject, 0f));
            var t = card.transform;
            var lens = UIParts.Ring("Lens", t, UIParts.Ink(0.3f), 64, 1.5f);
            UIKit.Place(lens.rectTransform, (w - 64) * 0.5f, 64, 64, 64);
            var slash = UIParts.Bar("Slash", t, UIParts.Ink(0.3f), (w - 84) * 0.5f, 95, 84, 2);
            slash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            slash.rectTransform.anchoredPosition += new Vector2(42, -1);
            slash.rectTransform.localRotation = Quaternion.Euler(0, 0, -35f);
            UIParts.Label(t, UIStrings.EmptyClipsTitle, 24, UIKit.Ink, true, TextAnchor.MiddleCenter, 0, 150, w, 36);
            UIParts.Label(t, UIStrings.EmptyClipsBody, 18, UIParts.Ink(0.6f), false, TextAnchor.MiddleCenter, 0, 194, w, 30);
        }

        // 진실 보도: 뉴스 속보 띠 (아래, 흘러감)
        void BuildTicker()
        {
            var band = UIParts.Fill("Ticker", root, UIKit.Rec, 8);
            tickerRect = band.rectTransform;
            UIKit.Place(tickerRect, 80, 928, 1760, 44);
            tickerGroup = UIParts.Group(band.gameObject, 0f);
            var lab = UIParts.Fill("Label", band.transform, UIParts.Dark, 8);
            UIKit.Place(lab.rectTransform, 0, 0, 96, 44);
            UIParts.Label(lab.transform, UIStrings.NewsTickerLabel, 18, UIKit.Ink, true, TextAnchor.MiddleCenter, 0, 0, 96, 44);
            var area = UIKit.Rect("Area", band.transform);
            UIKit.Place(area, 112, 0, 1760 - 128, 44);
            area.gameObject.AddComponent<RectMask2D>();
            var txt = UIParts.Label(area, UIStrings.NewsTicker, 18, UIKit.Ink, true, TextAnchor.MiddleLeft);
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            float w = Mathf.Ceil(UIParts.Width(txt)) + 40f;
            tickerText = txt.rectTransform;
            UIKit.Place(tickerText, 0, 0, w, 44);
        }

        void BuildBottomBar()
        {
            var bar = UIParts.Panel("BottomBar", root, UIParts.PanelMuted, 14, UIParts.Ink(0.12f));
            UIKit.Place(bar.rectTransform, 80, 984, 1760, 64);
            var t = bar.transform;
            float x = 20;
            var k1 = UIParts.Key(t, "R", 15, UIKit.Ink, UIParts.Ink(0.1f), UIParts.Ink(0.22f), x, 18, 28, 30);
            x += k1.sizeDelta.x + 8;
            var h1 = UIParts.Label(t, UIStrings.ResultsRestartHint, 15, UIParts.Ink(0.74f), false, TextAnchor.MiddleLeft, x, 0, 300, 64);
            h1.horizontalOverflow = HorizontalWrapMode.Overflow;
            x += Mathf.Ceil(UIParts.Width(h1)) + 20;
            var k2 = UIParts.Key(t, "Esc", 15, UIKit.Ink, UIParts.Ink(0.1f), UIParts.Ink(0.22f), x, 18, 28, 30);
            x += k2.sizeDelta.x + 8;
            var h2 = UIParts.Label(t, UIStrings.ResultsMainHint, 15, UIParts.Ink(0.74f), false, TextAnchor.MiddleLeft, x, 0, 300, 64);
            h2.horizontalOverflow = HorizontalWrapMode.Overflow;
            x += Mathf.Ceil(UIParts.Width(h2)) + 28;
            UIParts.Label(t, UIStrings.NavHint, 15, UIParts.Ink(0.45f), false, TextAnchor.MiddleLeft, x, 0, 420, 64);

            var main = UIParts.Button("Main", t, 1760 - 20 - 176 - 12 - 136, 10, 136, 44, UIParts.Ink(0f), UIParts.Ink(0.24f), 10, () => UIFlow.Restart(false));
            UIParts.Label(main.transform, UIStrings.ResultsMain, 18, UIKit.Ink, true, TextAnchor.MiddleCenter, 0, 0, 136, 44);
            var again = UIParts.Button("Again", t, 1760 - 20 - 176, 10, 176, 44, UIKit.Accent, UIParts.GoldBright, 10, () => UIFlow.Restart(true));
            UIParts.Key(again.transform, "R", 15, UIParts.Dark, UIKit.A(UIParts.Dark, 0.14f), UIKit.A(UIParts.Dark, 0.3f), 18, 8, 28, 30);
            UIParts.Label(again.transform, UIStrings.ResultsRestart, 18, UIParts.Dark, true, TextAnchor.MiddleLeft, 60, 0, 110, 44);
            nav.Add(main, 10);
            nav.Add(again, 10);
        }

        // ---------- 연출 ----------

        IEnumerator Sequence()
        {
            // 0.0초: 상단과 판 틀
            yield return UIParts.FadeTo(group, 1f, 0.3f);
            nav.Select(1);
            nav.Live = true;
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
            if (verdict == 0 && tickerRect != null)
            {
                var end = tickerRect.anchoredPosition;
                StartCoroutine(UIParts.Anim(0.4f, k =>
                {
                    tickerGroup.alpha = k;
                    tickerRect.anchoredPosition = end + new Vector2(0, -20f * (1f - k));
                }, 0.3f));
            }
            if (verdict == 1 && entityThumb != null)
            {
                yield return Wait(1.2f);
                yield return EyeBlink(entityThumb);
            }
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
            Vector2 end = rt.anchoredPosition, start = end + new Vector2(0, -30f);
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

        // 오컬트 바이럴의 여운: 썸네일 위에 도형으로 그린 커다란 눈이 천천히 한 번 깜박인다
        IEnumerator EyeBlink(RectTransform thumb)
        {
            var dim = UIKit.Image("EyeDim", thumb, new Color(0.02f, 0.01f, 0.02f, 0f));
            UIKit.Stretch(dim.rectTransform);
            const float ew = 176f, eh = 92f;
            var eye = UIKit.Image("Eye", thumb, UIKit.Hex("#E8E0D2"), UIKit.Circle(256));
            var ert = eye.rectTransform;
            ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0.5f);
            ert.sizeDelta = new Vector2(ew, eh);
            eye.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var eg = UIParts.Group(eye.gameObject, 0f);
            var iris = UIKit.Image("Iris", ert, UIKit.Hex("#7A1218"), UIKit.Circle(128));
            iris.rectTransform.sizeDelta = new Vector2(78, 78);
            var irisRing = UIKit.Image("IrisRing", ert, UIKit.A(UIKit.Accent, 0.7f), UIKit.Circle(156, 4));
            irisRing.rectTransform.sizeDelta = new Vector2(78, 78);
            var pupil = UIKit.Image("Pupil", ert, UIKit.Hex("#050405"), UIKit.Circle(128));
            pupil.rectTransform.sizeDelta = new Vector2(16, 54);
            var lidTop = UIKit.Image("LidTop", ert, UIKit.Hex("#0B0B0E"));
            var lt = lidTop.rectTransform;
            lt.anchorMin = new Vector2(0, 1); lt.anchorMax = new Vector2(1, 1); lt.pivot = new Vector2(0.5f, 1f);
            lt.anchoredPosition = Vector2.zero; lt.sizeDelta = new Vector2(0, 0);
            var lidBot = UIKit.Image("LidBottom", ert, UIKit.Hex("#0B0B0E"));
            var lb = lidBot.rectTransform;
            lb.anchorMin = new Vector2(0, 0); lb.anchorMax = new Vector2(1, 0); lb.pivot = new Vector2(0.5f, 0f);
            lb.anchoredPosition = Vector2.zero; lb.sizeDelta = new Vector2(0, 0);

            // 천천히 떠오름
            yield return UIParts.Anim(0.9f, k =>
            {
                eg.alpha = k;
                dim.color = new Color(0.02f, 0.01f, 0.02f, 0.55f * k);
                ert.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, k);
            });
            yield return Wait(0.6f);
            // 한 번, 천천히 감았다 뜬다
            yield return UIParts.Anim(0.45f, k => { float h = eh * 0.5f * UIParts.EaseInOut(k); lt.sizeDelta = new Vector2(0, h); lb.sizeDelta = new Vector2(0, h); });
            yield return Wait(0.15f);
            yield return UIParts.Anim(0.7f, k => { float h = eh * 0.5f * (1f - UIParts.EaseInOut(k)); lt.sizeDelta = new Vector2(0, h); lb.sizeDelta = new Vector2(0, h); });
            // 동공이 아주 조금 커진 채 남는다
            yield return UIParts.Anim(1.2f, k => pupil.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(16f, 22f, k), 54f));
        }
    }
}
