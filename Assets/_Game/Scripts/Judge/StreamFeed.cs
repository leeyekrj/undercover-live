using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>시청자 수와 라이브 채팅.</summary>
    public class StreamFeed : MonoBehaviour
    {
        [Header("시청자")]
        public int startViewers = 37;
        public float countSmoothSeconds = 0.8f;
        public float churnAfterSeconds = 30f;
        public float churnInterval = 10f;
        [Range(0f, 0.2f)] public float churnRate = 0.02f;

        [Header("채팅")]
        public int maxLines = 7;
        public int chatFontSize = 0; // 0이면 줄 높이에서 자동

        public event Action<int> MilestoneReached;

        public int Viewers { get { return Mathf.RoundToInt(viewers); } }
        public int DisplayedViewers { get { return Mathf.RoundToInt(displayed); } }

        float viewers, displayed, animStart, animT = 99f;
        float lastCaptureTime, lastChurnTime;
        readonly bool[] milestoneDone = new bool[3];
        static readonly int[] milestones = { 1000, 5000, 20000 };

        // 채팅
        readonly List<string> lines = new List<string>();
        Text[] lineTexts;
        RectTransform lineParent;
        float nextChatTime;
        struct Pending { public float time; public string nick, msg; }
        readonly List<Pending> pending = new List<Pending>();
        string lastNick;
        float lastLayoutHeight = -1f;

        void OnEnable()
        {
            CaptureLog.Added += OnCaptured;
            viewers = displayed = startViewers;
            lastCaptureTime = lastChurnTime = Time.time;
            nextChatTime = Time.time + 3f;
        }

        void OnDisable()
        {
            CaptureLog.Added -= OnCaptured;
        }

        // ---------- 공개 API ----------

        /// <summary>다른 시스템의 경고 ("뒤에 누구 와요!!"). 굵은 빨강으로 뜬다.</summary>
        public void Warn(string msg)
        {
            AddLine(RandomNick(), "<color=#FF5A5A><b>" + msg + "</b></color>");
        }

        public void Push(string nick, string msg)
        {
            AddLine(nick, msg);
        }

        /// <summary>시청자 직접 가감 (이벤트용). 확보로 얻은 게 아니면 흐름엔 영향 없음.</summary>
        public void AddViewers(int amount)
        {
            SetViewers(Mathf.Max(1f, viewers + amount));
        }

        // ---------- 업데이트 ----------

        void Update()
        {
            var pc = PhoneController.Instance;
            float now = Time.time;

            // 이탈
            if (now - lastCaptureTime >= churnAfterSeconds && now - lastChurnTime >= churnInterval)
            {
                lastChurnTime = now;
                SetViewers(Mathf.Max(1f, viewers * (1f - churnRate)));
            }

            // 표시 숫자 부드럽게
            if (animT < countSmoothSeconds)
            {
                animT += Time.deltaTime;
                float k = Mathf.Clamp01(animT / countSmoothSeconds);
                k = k * k * (3f - 2f * k);
                displayed = Mathf.Lerp(animStart, viewers, k);
            }
            else displayed = viewers;
            if (pc != null && pc.ViewerCountText != null)
                pc.ViewerCountText.text = "LIVE  " + Mathf.RoundToInt(displayed).ToString("N0", CultureInfo.InvariantCulture);

            // 대기 중인 연속 반응
            for (int i = 0; i < pending.Count; )
            {
                if (pending[i].time <= now)
                {
                    AddLine(pending[i].nick, pending[i].msg);
                    pending.RemoveAt(i);
                }
                else i++;
            }

            // 잡담: 폰을 꺼내 녹화 중일 때만
            if (pc != null && pc.IsRecording && now >= nextChatTime)
            {
                float speed = Mathf.Clamp(1f + Mathf.Log10(Mathf.Max(1f, viewers / 37f)) * 0.8f, 1f, 3f);
                nextChatTime = now + UnityEngine.Random.Range(2f, 5f) / speed;
                AddLine(RandomNick(), PickIdle(pc));
            }

            LayoutIfNeeded(pc);
        }

        // ---------- 확보 반응 ----------

        void OnCaptured(CaptureResult r)
        {
            lastCaptureTime = Time.time;
            SetViewers(viewers + r.Viewers);

            // 반응 2~3줄 연달아: 등급 → 종류 → (대상별 또는 등급 추가)
            var list = new List<string>();
            list.Add(Pick(GradeLines[(int)r.Grade]));
            // 대상별 문구가 있으면 그걸 우선 (와이어에 "때리는 거 맞음??"이 붙지 않게)
            var targetLines = TargetLines.Live(r);
            if (r.Target != null && r.Target.chatOnCapture != null && r.Target.chatOnCapture.Length > 0)
                targetLines = r.Target.chatOnCapture;
            list.Add(Pick(targetLines ?? CategoryLines[(int)r.Category]));
            if (UnityEngine.Random.value < 0.6f)
                list.Add(Pick(targetLines ?? GradeLines[(int)r.Grade]));
            float t = Time.time + 0.25f;
            string prev = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0 && list[i] == list[i - 1]) continue;
                string nick = RandomNick(prev);
                prev = nick;
                pending.Add(new Pending { time = t, nick = nick, msg = list[i] });
                t += UnityEngine.Random.Range(0.35f, 0.8f);
            }
        }

        void SetViewers(float v)
        {
            animStart = displayed;
            animT = 0f;
            viewers = v;
            for (int i = 0; i < milestones.Length; i++)
            {
                if (!milestoneDone[i] && viewers >= milestones[i])
                {
                    milestoneDone[i] = true;
                    var m = MilestoneLines[i];
                    pending.Add(new Pending { time = Time.time + 0.9f, nick = RandomNick(), msg = Pick(m) });
                    MilestoneReached?.Invoke(milestones[i]);
                }
            }
        }

        // ---------- 채팅 UI ----------

        void AddLine(string nick, string msg)
        {
            string rich = "<color=#" + NickColor(nick) + ">" + nick + "</color>  " + msg;
            lines.Add(rich);
            while (lines.Count > maxLines) lines.RemoveAt(0);
            Refresh(PhoneController.Instance);
        }

        void LayoutIfNeeded(PhoneController pc)
        {
            if (pc == null || pc.ChatArea == null) return;
            float h = pc.ChatArea.rect.height;
            if (lineTexts == null || lineParent != pc.ChatArea || !Mathf.Approximately(h, lastLayoutHeight))
                Refresh(pc);
        }

        void Refresh(PhoneController pc)
        {
            if (pc == null || pc.ChatArea == null) return;
            var area = pc.ChatArea;
            if (lineTexts == null || lineParent != area || lineTexts.Length != maxLines)
            {
                if (lineTexts != null)
                    foreach (var t in lineTexts) if (t != null) Destroy(t.gameObject);
                lineParent = area;
                lineTexts = new Text[maxLines];
                for (int i = 0; i < maxLines; i++)
                {
                    var go = new GameObject("ChatLine" + i, typeof(RectTransform), typeof(Text), typeof(Shadow));
                    var rt = (RectTransform)go.transform;
                    rt.SetParent(area, false);
                    rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0f);
                    var tx = go.GetComponent<Text>();
                    tx.raycastTarget = false;
                    tx.supportRichText = true;
                    tx.alignment = TextAnchor.LowerLeft;
                    tx.horizontalOverflow = HorizontalWrapMode.Wrap;
                    tx.verticalOverflow = VerticalWrapMode.Truncate;
                    tx.font = pc.UIFont != null ? pc.UIFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    tx.color = Color.white;
                    var sh = go.GetComponent<Shadow>();
                    sh.effectColor = new Color(0, 0, 0, 0.8f);
                    sh.effectDistance = new Vector2(1f, -1f);
                    lineTexts[i] = tx;
                }
            }

            lastLayoutHeight = area.rect.height;
            float lineH = Mathf.Max(8f, lastLayoutHeight / maxLines);
            int size = chatFontSize > 0 ? chatFontSize : Mathf.Max(8, Mathf.RoundToInt(lineH * 0.62f));
            for (int i = 0; i < maxLines; i++)
            {
                var tx = lineTexts[i];
                var rt = (RectTransform)tx.transform;
                rt.sizeDelta = new Vector2(0, lineH);
                rt.anchoredPosition = new Vector2(0, i * lineH); // 0번이 맨 아래(최신)
                tx.fontSize = size;
                tx.resizeTextForBestFit = true;
                tx.resizeTextMinSize = Mathf.Max(8, size / 2);
                tx.resizeTextMaxSize = size;
                int li = lines.Count - 1 - i; // 최신부터
                tx.text = li >= 0 ? lines[li] : "";
                var c = tx.color;
                c.a = Mathf.Lerp(1f, 0.45f, maxLines > 1 ? i / (float)(maxLines - 1) : 0f);
                tx.color = c;
            }
        }

        // ---------- 문구 ----------

        string PickIdle(PhoneController pc)
        {
            float r = UnityEngine.Random.value;
            if (pc.AngularSpeed > 90f && r < 0.5f) return Pick(ShakeLines);
            if (pc.Battery01 < 0.15f && r < 0.5f) return Pick(BatteryLines);
            if (pc.State == PhoneState.Hidden && r < 0.45f) return Pick(HiddenLines);
            if (pc.State == PhoneState.Aimed && r < 0.4f) return Pick(AimedLines);
            return Pick(IdleLines);
        }

        static string Pick(string[] a) { return a[UnityEngine.Random.Range(0, a.Length)]; }

        string RandomNick(string avoid = null)
        {
            string n = Nicks[UnityEngine.Random.Range(0, Nicks.Length)];
            for (int i = 0; i < 4 && (n == lastNick || n == avoid); i++)
                n = Nicks[UnityEngine.Random.Range(0, Nicks.Length)];
            lastNick = n;
            return n;
        }

        static readonly string[] NickPalette = { "FFB86B", "7FD6FF", "A6F29B", "FF8FB1", "D2A8FF", "FFE27A", "8FB4FF", "7CE8CF" };
        static string NickColor(string nick)
        {
            int h = 17;
            for (int i = 0; i < nick.Length; i++) h = h * 31 + nick[i];
            return NickPalette[(h & 0x7fffffff) % NickPalette.Length];
        }

        static readonly string[] Nicks = {
            "새벽감성러", "치킨은살코기", "밤샘중계충", "오늘도야근", "고양이집사77", "초코우유조아",
            "뚜벅이탐험가", "라면먹는곰", "구독자0명", "익명의관찰자", "정시퇴근희망", "냥냥펀치",
            "두부한모", "감자튀김왕", "소리없는아우성", "야식요정", "베개전사", "별빛산책",
            "콩나물국밥", "불면증환자", "커피수혈중", "우주먼지", "버블티덕후", "호기심천국",
            "아무말대잔치", "눈팅만3년", "주말의여유" };

        static readonly string[] IdleLines = {
            "ㅋㅋㅋ 시작부터 분위기 이상한데", "여기 어디임?", "다들 왜 저렇게 조용함", "형 안 들키게 조심해",
            "와 사람 진짜 많다", "ㄷㄷ 긴장되네", "채팅 보고 있음? 형?", "이거 진짜 라이브 맞지?",
            "처음 왔는데 무슨 방송이에요", "뭔가 불안한데 계속 봄", "ㅋㅋ 신도인 척 연기 미쳤다", "저 앞에 있는 사람 뭐하는 거임",
            "소리 좀 키워 봐요", "아직 아무 일도 없네", "형 심박수 올라가는 거 느껴짐" };
        static readonly string[] ShakeLines = {
            "흔들려서 안 보임", "멀미 난다 ㅠㅠ", "폰 좀 고정해 줘 형", "화면 너무 돌아가요", "아무것도 안 보이는데ㅋㅋ", "천천히 천천히" };
        static readonly string[] BatteryLines = {
            "폰 배터리 없는 거 아님?", "배터리 괜찮음? 곧 꺼질 듯", "충전 얼마 안 남았을 텐데", "형 배터리 ㅠㅠ" };
        static readonly string[] HiddenLines = {
            "화면 안 보고 찍는 거 맞음? ㄷㄷ", "몰래 찍는 거 떨린다 ㅋㅋ", "각도 좀 올려야 할 듯", "지금 찍히고 있는 거 맞지?",
            "가슴 높이라 다 보이는 건 아닌데", "들키면 큰일인데 계속 가자" };
        static readonly string[] AimedLines = {
            "오 이제 좀 보이네", "줌 좀 해 봐요", "그 방향 좋아요", "와 선명하다", "너무 대놓고 들고 있는 거 아님?", "누가 보면 어쩌려고 ㅋㅋ" };

        static readonly string[][] GradeLines = {
            // C
            new[] { "흔들려서 하나도 안 보임", "줌 좀 해줘 형", "아 멀어서 모르겠다", "뭐가 찍힌 건지 모르겠는데ㅋㅋ", "화면 너무 흔들려요 ㅠ", "가려서 안 보여요", "초점 어디 갔냐" },
            // B
            new[] { "뭐야 방금 뭐임", "뭔가 찍힌 것 같긴 한데", "그래도 알아보긴 하겠다", "오 뭐 있긴 있네", "조금만 더 가까이 가면 안 됨?", "ㅇㅇ 보이긴 보임" },
            // A
            new[] { "ㄷㄷㄷ 다 찍혔어", "오 이건 잘 나왔다", "방금 그거 선명하게 보였음", "ㅋㅋ 이 구도 좋다", "나이스 형", "오 저거 제대로 찍혔네", "이건 클립감이다" },
            // S
            new[] { "ㅁㅊ 이거 진짜임??", "클립 땄다 클립 땄어", "와 이건 진짜 대박이다 ㄷㄷㄷ", "형 이거 뉴스 나가야 되는 거 아님?", "소름 돋았다 방금", "후원 갑니다 ㄷㄷ 이런 건 줘야지", "화질 미쳤네 다 보여", "역대급 장면 ㅋㅋㅋ 아니 ㅠㅠ" },
        };

        static readonly string[][] CategoryLines = {
            // Atmosphere
            new[] { "분위기 오싹하다 ㅋㅋ", "사람들 표정 뭐임 ㄷㄷ", "여기 진짜 이상한 데 같은데", "노래 소름... 다 같이 부르네", "눈 풀린 거 봐", "아 분위기부터 불편하다" },
            // Evidence
            new[] { "저거 글씨 뭐라고 써 있음?", "저건 증거 아님? ㄷㄷ", "돈다발 뭐야 ㅁㅊ", "캡처해야겠다 저거", "서류 같은 거 맞지? 기록해 놔", "저런 게 왜 여기 있어" },
            // Climax
            new[] { "아니 지금 뭐하는 거임", "ㅁㅊ 저거 실화임??", "이거 경찰에 신고해야 하는 거 아님?", "형 제발 안전하게 나와", "방송 끄지 마 끄지 마", "소름 돋아서 손 떨린다", "저건 진짜 아니다 ㅠㅠ" },
            // Sound
            new[] { "방금 저 말 뭐임?", "녹음 됐지?? 다시 들어 봐야 함", "저런 말을 대놓고 하네", "와 저 발언은 선 넘었는데", "소리 또렷하게 들림 ㄷㄷ", "이거 증거로 쓸 수 있는 거 아님?" },
        };

        static readonly string[][] MilestoneLines = {
            new[] { "천 명 넘었다 ㄷㄷ", "시청자 1000 돌파 ㅋㅋ", "오 사람 늘어난다" },
            new[] { "5천 명 ㅁㅊ", "이제 퍼지기 시작했네", "시청자 미쳤다 ㄷㄷㄷ" },
            new[] { "2만 명이라고??", "형 이제 진짜 조심해야 돼", "이 정도면 누가 보고 있을 텐데 ㅠㅠ" },
        };
    }
}
