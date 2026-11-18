using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>엔딩: 업로드 결과 화면. Show()로 전체 화면 오버레이를 코드로 만든다.</summary>
    public class ResultsScreen : MonoBehaviour
    {
        [Header("엔딩 기준")]
        [Tooltip("총 보도 가치가 이 이상이면 '보도' 엔딩")]
        public float newsThreshold = 2f;
        [Tooltip("최고 동시 시청자가 이 이상이면 '조회수' 엔딩")]
        public int viewerThreshold = 8000;
        public int maxCards = 6;
        public Font font;

        GameObject root;
        CanvasGroup group;
        bool shown;

        public bool IsShown { get { return shown; } }

        public void Show(string reason)
        {
            if (shown) return;
            shown = true;
            if (font == null && PhoneController.Instance != null) font = PhoneController.Instance.UIFont;
            Build(reason);
            StartCoroutine(Intro());
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (shown && kb != null && kb.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ---------- 구성 ----------
        readonly List<RectTransform> cardRects = new List<RectTransform>();
        readonly List<CanvasGroup> cardGroups = new List<CanvasGroup>();

        void Build(string reason)
        {
            var canvasGo = new GameObject("ResultsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            group = canvasGo.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            root = canvasGo;

            var bg = NewImage(canvasGo.transform, "Bg", new Color(0.055f, 0.055f, 0.07f, 1f));
            Stretch(bg.rectTransform);
            // 위쪽 얇은 띠 (스튜디오 헤더 느낌)
            var bar = NewImage(canvasGo.transform, "HeaderBar", new Color(0.09f, 0.09f, 0.115f, 1f));
            Anchor(bar.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 190));

            var clips = PickClips();
            int total = CaptureLog.Clips.Count;
            int totalViewers = CaptureLog.TotalViewersGained();
            float news = CaptureLog.TotalNewsValue();
            int peak = PeakViewers(totalViewers);

            // 1. 제목 + 통계
            var title = NewText(canvasGo.transform, "Title", MakeTitle(clips), 46, TextAnchor.MiddleLeft, Color.white);
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -34), new Vector2(1680, 70));
            var stats = NewText(canvasGo.transform, "Stats",
                "최고 동시 시청자 " + peak.ToString("N0") + "명   ·   확보한 클립 " + total + "개   ·   총 보도 가치 " + news.ToString("0.0"),
                24, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.62f));
            Anchor(stats.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(122, -110), new Vector2(1680, 40));
            if (!string.IsNullOrEmpty(reason))
            {
                var rs = NewText(canvasGo.transform, "Reason", reason, 20, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.38f));
                Anchor(rs.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(122, -146), new Vector2(1680, 32));
            }

            // 2. 클립 카드
            if (clips.Count == 0)
            {
                var none = NewText(canvasGo.transform, "None", "찍힌 장면이 없습니다", 40, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.55f));
                Anchor(none.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1200, 80));
            }
            else
            {
                const float cw = 250f, gap = 22f;
                float rowW = clips.Count * cw + (clips.Count - 1) * gap;
                for (int i = 0; i < clips.Count; i++)
                {
                    float x = -rowW * 0.5f + cw * 0.5f + i * (cw + gap);
                    BuildCard(canvasGo.transform, clips[i], i, new Vector2(x, 28f), cw);
                }
            }

            // 3. 판정 문장
            string verdict = Verdict(peak, news);
            var vt = NewText(canvasGo.transform, "Verdict", verdict, 34, TextAnchor.MiddleCenter, VerdictColor(peak, news));
            Anchor(vt.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 92), new Vector2(1700, 60));

            // 4. 다시 하기
            var hint = NewText(canvasGo.transform, "Hint", "R  다시 하기", 20, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.4f));
            Anchor(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(600, 30));
        }

        void BuildCard(Transform parent, CaptureResult c, int index, Vector2 pos, float w)
        {
            const float snapH = 360f, snapW = 202.5f, cardH = 560f;
            var card = NewImage(parent, "Card" + index, new Color(0.11f, 0.11f, 0.14f, 1f));
            var rt = card.rectTransform;
            Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(w, cardH));
            var cg = card.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cardRects.Add(rt); cardGroups.Add(cg);

            // 스냅샷 (9:16 중앙 크롭)
            var frame = NewImage(card.transform, "SnapFrame", new Color(0.02f, 0.02f, 0.03f, 1f));
            Anchor(frame.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(snapW, snapH));
            // 녹음 클립은 폰이 주머니에 있어 화면이 검다 → 파형으로 보여 준다
            if (c.Category == CaptureCategory.Sound)
            {
                BuildWaveform(frame.transform, c, snapW, snapH);
            }
            else
            {
                var raw = new GameObject("Snapshot", typeof(RawImage)).GetComponent<RawImage>();
                raw.transform.SetParent(frame.transform, false);
                Stretch(raw.rectTransform);
                if (c.Snapshot != null)
                {
                    raw.texture = c.Snapshot;
                    float texA = (float)c.Snapshot.width / Mathf.Max(1, c.Snapshot.height);
                    float want = snapW / snapH;
                    if (texA > want) { float u = want / texA; raw.uvRect = new Rect((1 - u) * 0.5f, 0, u, 1); }
                    else { float v = texA / want; raw.uvRect = new Rect(0, (1 - v) * 0.5f, 1, v); }
                }
                else raw.color = new Color(0.12f, 0.12f, 0.15f, 1f);
            }

            // 등급 배지
            var badge = NewImage(frame.transform, "Badge", GradeColor(c.Grade));
            Anchor(badge.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -8), new Vector2(44, 44));
            var bt = NewText(badge.transform, "G", GradeLetter(c.Grade), 28, TextAnchor.MiddleCenter, new Color(0.05f, 0.05f, 0.06f, 1f));
            Stretch(bt.rectTransform);
            bt.fontStyle = FontStyle.Bold;

            // 대상명, 시청자
            float tx = 14f, tw = w - 28f;
            var name = NewText(card.transform, "Name", c.TargetName, 24, TextAnchor.MiddleLeft, Color.white);
            Anchor(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(tx, -384), new Vector2(tw, 34));
            // 긴 대상명이 옆 카드로 넘치지 않게 카드 폭 안에서 글자 크기를 줄인다
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 14; name.resizeTextMaxSize = 22;
            var viewers = NewText(card.transform, "Viewers", "+" + c.Viewers.ToString("N0") + " 시청자", 22, TextAnchor.MiddleLeft, new Color(0.45f, 0.9f, 0.55f, 1f));
            Anchor(viewers.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(tx, -418), new Vector2(tw, 30));

            // 댓글 2줄
            string[] cm = TargetLines.Comments(c, index);
            var ct = NewText(card.transform, "Comments", cm[0] + "\n" + cm[1], 17, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.7f));
            Anchor(ct.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(tx, -456), new Vector2(tw, 90));
            ct.verticalOverflow = VerticalWrapMode.Truncate;
        }

        // ---------- 데이터 ----------
        List<CaptureResult> PickClips()
        {
            var all = new List<CaptureResult>(CaptureLog.Clips);
            all.Sort((a, b) => b.Viewers.CompareTo(a.Viewers));
            int n = Mathf.Min(maxCards, all.Count);
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

        string MakeTitle(List<CaptureResult> clips)
        {
            if (clips.Count == 0) return "[잠입] 아무것도 찍지 못했습니다";
            CaptureResult best = clips[0];
            foreach (var c in clips)
                if (c.Grade > best.Grade || (c.Grade == best.Grade && c.Viewers > best.Viewers)) best = c;
            return "[잠입] " + best.TargetName + " 실제로 찍혔습니다";
        }

        string Verdict(int peak, float news)
        {
            if (CaptureLog.Clips.Count > 0 && news >= newsThreshold) return "보도 — 방송사 제보가 들어왔습니다";
            if (peak >= viewerThreshold) return "조회수 — 구독자 폭증, 교단의 반박 영상이 올라왔습니다";
            return "빈손 — 조회수 132회";
        }

        Color VerdictColor(int peak, float news)
        {
            if (CaptureLog.Clips.Count > 0 && news >= newsThreshold) return new Color(1f, 0.85f, 0.35f, 1f);
            if (peak >= viewerThreshold) return new Color(0.55f, 0.8f, 1f, 1f);
            return new Color(1, 1, 1, 0.6f);
        }

        static string GradeLetter(CaptureGrade g) { return g.ToString(); }

        static Color GradeColor(CaptureGrade g)
        {
            switch (g)
            {
                case CaptureGrade.S: return new Color(1f, 0.82f, 0.25f, 1f);
                case CaptureGrade.A: return new Color(0.5f, 0.9f, 0.55f, 1f);
                case CaptureGrade.B: return new Color(0.55f, 0.75f, 1f, 1f);
                default: return new Color(0.7f, 0.7f, 0.74f, 1f);
            }
        }

        // 녹음 클립 카드: 대상·시각으로 고정된 막대 파형. 잘 녹음될수록 진하다.
        void BuildWaveform(Transform frame, CaptureResult c, float w, float h)
        {
            const int bars = 26;
            const float barW = 4f, pad = 22f;
            float gap = (w - pad * 2f - bars * barW) / (bars - 1);
            int seed = 17;
            foreach (char ch in (c.TargetId ?? "")) seed = seed * 31 + ch;
            seed ^= Mathf.RoundToInt(c.GameTime * 10f);
            var rng = new System.Random(seed);
            var col = new Color(0.85f, 0.7f, 0.48f, Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(c.AverageQuality)));
            for (int i = 0; i < bars; i++)
            {
                float env = Mathf.Sin(Mathf.PI * (i + 0.5f) / bars);
                float amp = (0.25f + 0.75f * (float)rng.NextDouble()) * Mathf.Lerp(0.3f, 1f, env);
                var bar = NewImage(frame, "Wave" + i, col);
                float x = -w * 0.5f + pad + barW * 0.5f + i * (barW + gap);
                Anchor(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 0f), new Vector2(barW, Mathf.Max(6f, amp * h * 0.36f)));
            }
            var label = NewText(frame, "RecLabel", "<color=#e5484d>●</color>  녹음", 18, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.6f));
            label.supportRichText = true;
            Anchor(label.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(w, 26));
        }

        // ---------- 연출 ----------
        IEnumerator Intro()
        {
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(t / 0.6f);
                yield return null;
            }
            group.alpha = 1f;
            for (int i = 0; i < cardRects.Count; i++)
            {
                StartCoroutine(RiseCard(cardRects[i], cardGroups[i]));
                yield return new WaitForSecondsRealtime(0.08f);
            }
        }

        IEnumerator RiseCard(RectTransform rt, CanvasGroup cg)
        {
            Vector2 end = rt.anchoredPosition;
            Vector2 start = end + new Vector2(0, -40f);
            float t = 0f;
            const float dur = 0.35f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                k = 1f - (1f - k) * (1f - k);
                rt.anchoredPosition = Vector2.LerpUnclamped(start, end, k);
                cg.alpha = k;
                yield return null;
            }
            rt.anchoredPosition = end;
            cg.alpha = 1f;
        }

        // ---------- UI 헬퍼 ----------
        Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text NewText(Transform parent, string name, string text, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
            rt.anchoredPosition = pos;
            // 가로가 늘어나는 앵커(min.x != max.x)는 높이만 지정
            rt.sizeDelta = (min.x != max.x) ? new Vector2(0, size.y) : size;
        }
    }
}
