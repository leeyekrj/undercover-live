using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UndercoverLive
{
    /// <summary>채팅 한 줄의 종류. Warn = 빨간 경고 카드, Donation = 금빛 후원 카드, Mod = 매니저 공지.</summary>
    public enum ChatKind { Chat, Question, Reply, Hater, Donation, Mod, Warn }

    public struct ChatLine
    {
        public string Nick;
        public string Text;
        public ChatKind Kind;
        public int Amount;          // 후원 금액(원). 후원이 아니면 0
        public string Situation;    // 어떤 상황에서 나온 줄인지 (idle_mid, grade_S 등)
        public float Time;          // Time.time
    }

    /// <summary>
    /// 시청자 수와 라이브 채팅 줄 모음. 어떤 줄을 언제 내보낼지는 HUD/ChatDirector가 정하고,
    /// 화면에 그리는 건 HUD/PhoneAppUI(채팅 카드)가 LineAdded를 받아서 한다.
    /// </summary>
    public class StreamFeed : MonoBehaviour
    {
        public static StreamFeed Instance { get; private set; }

        [Header("시청자")]
        public int startViewers = 37;
        public float countSmoothSeconds = 0.8f;
        public float churnAfterSeconds = 30f;
        public float churnInterval = 10f;
        [Range(0f, 0.2f)] public float churnRate = 0.02f;

        public event Action<int> MilestoneReached;
        /// <summary>새 채팅 줄이 들어올 때마다.</summary>
        public event Action<ChatLine> LineAdded;

        public int Viewers { get { return Mathf.RoundToInt(viewers); } }
        public int DisplayedViewers { get { return Mathf.RoundToInt(displayed); } }
        /// <summary>이번 판 최고 동시 시청자.</summary>
        public int PeakViewers { get { return Mathf.RoundToInt(peak); } }
        /// <summary>최근 약 10초 동안 늘어난 시청자 (줄었으면 음수).</summary>
        public int ViewerDelta { get { return samples.Count > 0 ? Mathf.RoundToInt(viewers - samples.Peek()) : 0; } }
        /// <summary>최근 8줄, "닉네임: 내용" 평문 (발각 화면용).</summary>
        public IReadOnlyList<string> RecentLines { get { return recentPlain; } }
        /// <summary>최근 줄들 (오래된 것부터).</summary>
        public IReadOnlyList<ChatLine> Lines { get { return lines; } }

        float viewers, displayed, peak, animStart, animT = 99f;
        float lastCaptureTime, lastChurnTime, nextSample;
        readonly bool[] milestoneDone = new bool[3];
        static readonly int[] milestones = { 1000, 5000, 20000 };
        readonly Queue<float> samples = new Queue<float>();

        const int KeepLines = 16, KeepPlain = 8;
        readonly List<ChatLine> lines = new List<ChatLine>();
        readonly List<string> recentPlain = new List<string>();
        static readonly Regex Tags = new Regex("<[^>]+>");

        void Awake() { Instance = this; }

        void OnDestroy()
        {
            CaptureLog.Added -= OnCaptured;
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            CaptureLog.Added += OnCaptured;
            viewers = displayed = peak = startViewers;
            lastCaptureTime = lastChurnTime = Time.time;
        }

        void OnDisable()
        {
            CaptureLog.Added -= OnCaptured;
        }

        // ---------- 공개 API ----------

        /// <summary>다른 시스템의 경고 ("뒤에 누구 와요!!"). 빨간 경고 카드로 뜬다.</summary>
        public void Warn(string msg)
        {
            Emit(new ChatLine { Nick = ChatDirector.RandomNick(), Text = msg, Kind = ChatKind.Warn, Situation = "warn" });
        }

        public void Push(string nick, string msg)
        {
            Emit(new ChatLine { Nick = nick, Text = msg, Kind = ChatKind.Chat, Situation = "push" });
        }

        /// <summary>채팅 한 줄을 바로 내보낸다 (ChatDirector가 쓴다).</summary>
        public void Emit(ChatLine line)
        {
            line.Time = Time.time;
            if (line.Text == null) line.Text = "";
            lines.Add(line);
            while (lines.Count > KeepLines) lines.RemoveAt(0);
            string who = string.IsNullOrEmpty(line.Nick) ? "매니저" : line.Nick;
            recentPlain.Add(who + ": " + Tags.Replace(line.Text, ""));
            while (recentPlain.Count > KeepPlain) recentPlain.RemoveAt(0);
            LineAdded?.Invoke(line);
        }

        /// <summary>시청자 직접 가감 (이벤트용). 확보로 얻은 게 아니면 흐름엔 영향 없음.</summary>
        public void AddViewers(int amount)
        {
            SetViewers(Mathf.Max(1f, viewers + amount));
        }

        // ---------- 업데이트 ----------

        void Update()
        {
            float now = Time.time;

            // 이탈 (플레이 중에만)
            if (GameEvents.IsPlaying && now - lastCaptureTime >= churnAfterSeconds && now - lastChurnTime >= churnInterval)
            {
                lastChurnTime = now;
                SetViewers(Mathf.Max(1f, viewers * (1f - churnRate)));
            }

            // 10초 변화량용 표본 (1초마다)
            if (now >= nextSample)
            {
                nextSample = now + 1f;
                samples.Enqueue(viewers);
                while (samples.Count > 10) samples.Dequeue();
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
        }

        void OnCaptured(CaptureResult r)
        {
            lastCaptureTime = Time.time;
            SetViewers(viewers + r.Viewers);
        }

        void SetViewers(float v)
        {
            animStart = displayed;
            animT = 0f;
            viewers = v;
            if (viewers > peak) peak = viewers;
            for (int i = 0; i < milestones.Length; i++)
            {
                if (!milestoneDone[i] && viewers >= milestones[i])
                {
                    milestoneDone[i] = true;
                    MilestoneReached?.Invoke(milestones[i]);
                }
            }
        }
    }
}
