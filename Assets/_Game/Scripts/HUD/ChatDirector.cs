using System;
using System.Collections.Generic;
using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 라이브 채팅 연출 (Paul 판정: "채팅이 너무 똑같다").
    /// 문구는 Resources/ChatLines.json (상황 situation · 종류 kind · 후원 금액 · 대꾸 reply_to).
    /// - 상황 고르기: 시청자 구간, 폰에 잡힌 대상, 흔들림, 줌, 배터리, 확보 등급, 경고, GameEvents.ChatCue
    /// - 반복 금지: 최근 60문장·12닉네임은 다시 안 고르고, 같은 종류가 3줄 연속 나오지 않게
    /// - 리듬: 시청자 수에 따라 기본 속도, 확보·경고 직후 몰아치기, 가끔 조용한 구간, 질문 → 대꾸 짝
    /// 줄은 하나의 대기열을 차례로 지나간다(앞 줄과의 간격 gap). 대꾸는 부모 바로 다음 칸에 끼운다.
    /// </summary>
    public class ChatDirector : MonoBehaviour
    {
        public static ChatDirector Instance { get; private set; }

        // ---------- 데이터 ----------

        [Serializable] class LineData { public string situation; public string kind; public string text; public int amount; public string reply_to; }
        [Serializable] class Root { public string[] nicknames; public LineData[] lines; }

        static LineData[] all;
        static string[] nicks;
        static Dictionary<string, List<int>> bySituation;     // 대꾸가 아닌 줄
        static Dictionary<string, List<int>> repliesByParent; // 부모 문장(원문) → 대꾸 줄
        static bool loaded;

        // 파일에 없는 상황만 남긴 기존 문구
        static readonly string[][] MilestoneLines = {
            new[] { "천 명 넘었다 ㄷㄷ", "시청자 1000 돌파 ㅋㅋ", "오 사람 늘어난다" },
            new[] { "5천 명 ㅁㅊ", "이제 퍼지기 시작했네", "시청자 미쳤다 ㄷㄷㄷ" },
            new[] { "2만 명이라고??", "형 이제 진짜 조심해야 돼", "이 정도면 누가 보고 있을 텐데 ㅠㅠ" },
        };
        static readonly string[] FallbackNicks = { "익명의관찰자", "눈팅만3년", "버블티덕후", "호기심천국" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { loaded = false; all = null; nicks = null; bySituation = null; repliesByParent = null; Instance = null; }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            bySituation = new Dictionary<string, List<int>>();
            repliesByParent = new Dictionary<string, List<int>>();
            all = new LineData[0];
            var nickList = new List<string>();
            try
            {
                var ta = Resources.Load<TextAsset>("ChatLines");
                var root = ta != null ? JsonUtility.FromJson<Root>(ta.text) : null;
                if (root != null && root.lines != null) all = root.lines;
                if (root != null && root.nicknames != null)
                    foreach (var n in root.nicknames)
                        if (!string.IsNullOrEmpty(n) && !HUDText.IsBanned(n)) nickList.Add(n);
            }
            catch (Exception e) { Debug.LogWarning("[ChatDirector] ChatLines.json 읽기 실패: " + e.Message); }
            if (nickList.Count == 0) nickList.AddRange(FallbackNicks);
            nicks = nickList.ToArray();

            for (int i = 0; i < all.Length; i++)
            {
                var l = all[i];
                if (l == null || string.IsNullOrEmpty(l.text) || HUDText.IsBanned(l.text)) continue;
                if (l.kind == "reply" && !string.IsNullOrEmpty(l.reply_to))
                {
                    if (!repliesByParent.TryGetValue(l.reply_to, out var rl)) repliesByParent[l.reply_to] = rl = new List<int>();
                    rl.Add(i);
                    continue;
                }
                string s = string.IsNullOrEmpty(l.situation) ? "idle_mid" : l.situation;
                if (!bySituation.TryGetValue(s, out var list)) bySituation[s] = list = new List<int>();
                list.Add(i);
            }
        }

        static ChatKind KindOf(string k)
        {
            switch (k)
            {
                case "question": return ChatKind.Question;
                case "reply": return ChatKind.Reply;
                case "hater": return ChatKind.Hater;
                case "donation": return ChatKind.Donation;
                case "mod": return ChatKind.Mod;
                default: return ChatKind.Chat;
            }
        }

        static bool IsWarnSituation(string s) { return s == "warn_look" || s == "warn_staff_coming" || s == "caught"; }

        // ---------- 대기열 ----------

        class Item
        {
            public string text, raw, nick, situation;
            public ChatKind kind;      // 데이터상 종류 (3연속 금지 규칙용)
            public int amount;
            public float gap;          // 앞 줄이 나간 뒤 기다릴 초
            public bool urgent;        // 플레이 중이 아니어도 내보냄 (발각 순간)
            public bool isReply;
            public bool noReply;       // 대꾸를 붙이지 않음 (경고 한 줄)
            public float expires;      // 이 시각이 지나면 버림 (멈춤 뒤 묵은 줄 방지)
            public Dictionary<string, string> fill;
        }

        readonly List<Item> queue = new List<Item>();
        float lastFire = -99f, nextIdle, nextDonation, nextMod;
        int modCount;

        // 반복 금지
        const int RecentTextCount = 60, RecentNickCount = 12;
        readonly Queue<string> recentTexts = new Queue<string>();
        readonly HashSet<string> recentTextSet = new HashSet<string>();
        readonly Dictionary<string, int> lastUsedSerial = new Dictionary<string, int>();
        int serial;
        readonly Queue<string> recentNicks = new Queue<string>();
        readonly HashSet<string> recentNickSet = new HashSet<string>();
        ChatKind kindA = ChatKind.Mod, kindB = ChatKind.Donation;   // 마지막 두 줄의 종류 (A가 더 최근)

        readonly Dictionary<string, float> cooldownUntil = new Dictionary<string, float>();
        float lastCaughtTime = -99f;

        StreamFeed feed;

        void Awake()
        {
            Instance = this;
            Load();
        }

        void OnEnable()
        {
            CaptureLog.Added += OnCaptured;
            GameEvents.ChatCue += OnCue;
            GameEvents.PlayerStoodChanged += OnStood;
            GameEvents.PlayerNearStage += OnNearStage;
            GameEvents.CheckpointStarted += OnCheckpointStarted;
            GameEvents.CheckpointEnded += OnCheckpointEnded;
            GameEvents.Confiscated += OnConfiscated;
        }

        void OnDisable() { Unsubscribe(); }

        void Unsubscribe()
        {
            CaptureLog.Added -= OnCaptured;
            GameEvents.ChatCue -= OnCue;
            GameEvents.PlayerStoodChanged -= OnStood;
            GameEvents.PlayerNearStage -= OnNearStage;
            GameEvents.CheckpointStarted -= OnCheckpointStarted;
            GameEvents.CheckpointEnded -= OnCheckpointEnded;
            GameEvents.Confiscated -= OnConfiscated;
            if (feed != null) feed.MilestoneReached -= OnMilestone;
        }

        void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            float now = Time.time;
            nextIdle = now + 2.5f;
            nextDonation = now + UnityEngine.Random.Range(70f, 130f);
            nextMod = now + UnityEngine.Random.Range(35f, 80f);
        }

        void Update()
        {
            if (feed == null)
            {
                feed = StreamFeed.Instance != null ? StreamFeed.Instance : FindAnyObjectByType<StreamFeed>();
                if (feed != null) feed.MilestoneReached += OnMilestone;
                if (feed == null) return;
            }

            float now = Time.time;
            bool playing = GameEvents.IsPlaying;

            // 묵은 줄 버리기
            for (int i = queue.Count - 1; i >= 0; i--)
                if (!queue[i].urgent && now > queue[i].expires) queue.RemoveAt(i);

            if (queue.Count > 0)
            {
                var it = queue[0];
                float since = (playing || it.urgent) ? now - lastFire : -1f;
                if (since >= it.gap)
                {
                    queue.RemoveAt(0);
                    Fire(it);
                    lastFire = now;
                }
            }
            else if (playing && now >= nextIdle)
            {
                RegularTick(now);
            }
        }

        // ---------- 기본 흐름 ----------

        float ViewerT()
        {
            float v = feed != null ? Mathf.Max(1, feed.Viewers) : 37;
            return Mathf.InverseLerp(3f, 4f, Mathf.Log10(v));   // 1천 → 0, 1만 → 1
        }

        string IdleSituation()
        {
            int v = feed != null ? feed.Viewers : 0;
            return v < 1000 ? "idle_small" : v < 10000 ? "idle_mid" : "idle_big";
        }

        void RegularTick(float now)
        {
            var pc = PhoneController.Instance;
            if (pc == null || !pc.IsAudioRecording) { nextIdle = now + 2f; return; }   // 방송이 꺼지면 채팅도 멈춤

            float t = ViewerT();
            float interval = UnityEngine.Random.Range(Mathf.Lerp(4f, 0.6f, t), Mathf.Lerp(7f, 1.5f, t));
            if (pc.State == PhoneState.Pocket) interval *= 1.8f;                    // 음성만 나갈 땐 느리게
            if (UnityEngine.Random.value < 0.07f) interval += UnityEngine.Random.Range(5f, 10f) * Mathf.Lerp(1f, 0.4f, t);   // 가끔 조용한 구간
            nextIdle = now + interval;

            string sit = ChooseSituation(now, pc, t);
            Enqueue(sit, 0f, false, null, false);
        }

        string ChooseSituation(float now, PhoneController pc, float t)
        {
            if (now >= nextDonation && Has("donation"))
            {
                nextDonation = now + UnityEngine.Random.Range(120f, 240f) * Mathf.Lerp(1f, 0.45f, t);
                return "donation";
            }
            if (modCount < 2 && now >= nextMod && Has("mod"))
            {
                modCount++;
                nextMod = now + UnityEngine.Random.Range(150f, 260f);
                return "mod";
            }
            if (pc.IsRecording)
            {
                if (pc.Battery01 <= 0.2f && Ready("battery_low", 30f) && UnityEngine.Random.value < 0.4f) return Use("battery_low", 30f);
                if (pc.AngularSpeed > 90f && Ready("shaky", 10f) && UnityEngine.Random.value < 0.6f) return Use("shaky", 10f);
                var judge = CaptureJudge.Instance;
                if (judge != null && judge.FramedTarget != null)
                {
                    if (pc.State == PhoneState.Aimed && judge.FramedSize01 < 0.6f && pc.Zoom01 < 0.95f && Ready("zoom_request", 18f) && UnityEngine.Random.value < 0.4f)
                        return Use("zoom_request", 18f);
                    string fs = FramingSituation(judge.FramedTarget.Id);
                    if (fs != null && Has(fs) && Ready(fs, 12f) && UnityEngine.Random.value < 0.5f) return Use(fs, 12f);
                }
            }
            float r = UnityEngine.Random.value;
            if (r < 0.12f) return "question";
            if (r < 0.21f) return "hater";
            if (r < 0.23f) return "signal_weak";
            return IdleSituation();
        }

        static string FramingSituation(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var phen = PhenomenaCatalog.ChatSituationFor(id);   // 새 현상(겹눈 존재) 대상 — 담당 D의 표
            if (!string.IsNullOrEmpty(phen)) return phen;
            if (id.StartsWith("leader")) return "framing_leader";
            if (id.StartsWith("crowd")) return "framing_crowd";
            if (id.StartsWith("banner")) return "framing_banner";
            if (id.StartsWith("cctv")) return "framing_cctv";
            if (id.StartsWith("portrait")) return "framing_portrait";
            if (id.StartsWith("countdown")) return "framing_countdown";
            if (id.StartsWith("donation_board")) return "framing_donation_board";
            return null;
        }

        bool Has(string s) { return bySituation != null && bySituation.TryGetValue(s, out var l) && l.Count > 0; }
        bool Ready(string key, float cd) { return !cooldownUntil.TryGetValue(key, out var u) || Time.time >= u; }
        string Use(string key, float cd) { cooldownUntil[key] = Time.time + cd; return key; }

        // ---------- 몰아치기 ----------

        /// <summary>상황 하나에서 min~max줄을 0.2~0.5초 간격으로 몰아서 낸다.</summary>
        public void Burst(string situation, int min, int max, bool front = true, bool urgent = false, Dictionary<string, string> fill = null)
        {
            if (!Has(situation)) return;
            int n = UnityEngine.Random.Range(min, max + 1);
            var items = new List<Item>();
            for (int i = 0; i < n; i++)
            {
                var it = MakeItem(situation, i == 0 ? 0.15f : UnityEngine.Random.Range(0.2f, 0.5f), urgent, fill, items);
                if (it != null) items.Add(it);
            }
            Insert(items, front);
        }

        void OnCaptured(CaptureResult r)
        {
            if (r == null) return;
            var items = new List<Item>();
            string gradeSit = "grade_" + r.Grade;
            // 첫 줄: 대상 전용 문구 (속임수 폭로 대상은 설정 변경 중이라 건너뜀) → 없으면 등급 문구
            if (!HUDText.IsRevealTarget(r.TargetId))
            {
                string[] tl = r.Target != null && r.Target.chatOnCapture != null && r.Target.chatOnCapture.Length > 0 ? r.Target.chatOnCapture : TargetLines.Live(r);
                string pick = PickFromArray(tl);
                if (pick != null)
                    items.Add(new Item { text = pick, raw = pick, situation = "capture", kind = ChatKind.Chat, gap = 0.25f, expires = Time.time + 20f });
            }
            int n = UnityEngine.Random.Range(3, 6);
            while (items.Count < n)
            {
                var it = MakeItem(gradeSit, items.Count == 0 ? 0.25f : UnityEngine.Random.Range(0.2f, 0.5f), false, null, items);
                if (it == null) break;
                items.Add(it);
            }
            Insert(items, true);
            nextIdle = Mathf.Max(nextIdle, Time.time + 2.5f);
        }

        /// <summary>누가 쳐다봄 (WarningFX가 부른다). where = 앞·뒤·왼쪽·오른쪽, who = 채팅 말투 이름.
        /// 경고가 겹겹이 뜨지 않게 채팅은 빨간 줄 하나만 (배너는 WarningFX가 하나).</summary>
        public void WarnLook(string where, string who, WatcherKind kind)
        {
            if (!Ready("warn", 4f)) return;
            Use("warn", 4f);
            var fill = new Dictionary<string, string> { { "{where}", where }, { "{who}", who } };
            var it = MakeItem("warn_look", 0.1f, false, fill, null);
            if (it == null) return;
            it.noReply = true;
            Insert(new List<Item> { it }, true);
        }

        void OnCue(string situation)
        {
            if (string.IsNullOrEmpty(situation) || !Has(situation)) return;
            if (!Ready("cue:" + situation, 5f)) return;
            Use("cue:" + situation, 5f);
            bool caught = situation == "caught";
            if (caught) lastCaughtTime = Time.time;
            bool warn = situation.StartsWith("warn");   // 경고 신호는 짧게 (배너가 이미 알린다)
            Burst(situation, warn ? 1 : 2, warn ? 2 : 4, true, caught);
        }

        void OnStood(bool standing) { if (standing && GameEvents.IsPlaying) OnCue("stand_up"); }
        void OnNearStage() { if (GameEvents.IsPlaying) OnCue("rush_stage"); }
        void OnCheckpointStarted(float seconds) { OnCue("checkpoint"); }
        void OnCheckpointEnded(bool passed) { OnCue(passed ? "checkpoint_survived" : "caught"); }

        void OnConfiscated(string reason)
        {
            // 발각 화면이 RecentLines를 바로 읽을 수 있게, 아직 발각 반응이 없었다면 즉시 두 줄
            if (Time.time - lastCaughtTime < 6f) return;
            lastCaughtTime = Time.time;
            for (int i = 0; i < 2; i++)
            {
                var it = MakeItem("caught", 0f, true, null, null);
                if (it != null) Fire(it);
            }
        }

        void OnMilestone(int m)
        {
            int idx = m >= 20000 ? 2 : m >= 5000 ? 1 : 0;
            string pick = PickFromArray(MilestoneLines[idx]);
            if (pick == null) return;
            Insert(new List<Item> { new Item { text = pick, raw = pick, situation = "milestone", kind = ChatKind.Chat, gap = 0.9f, expires = Time.time + 15f } }, true);
        }

        // ---------- 고르기 ----------

        void Enqueue(string situation, float gap, bool urgent, Dictionary<string, string> fill, bool front)
        {
            var it = MakeItem(situation, gap, urgent, fill, null);
            if (it != null) Insert(new List<Item> { it }, front);
        }

        Item MakeItem(string situation, float gap, bool urgent, Dictionary<string, string> fill, List<Item> pendingBatch)
        {
            int idx = PickIndex(situation, pendingBatch);
            if (idx < 0) return null;
            var l = all[idx];
            var it = new Item
            {
                raw = l.text,
                text = Fill(l.text, fill),
                situation = l.situation,
                kind = KindOf(l.kind),
                amount = l.amount,
                gap = gap,
                urgent = urgent,
                fill = fill,
                expires = Time.time + (urgent ? 60f : 15f),
            };
            Reserve(l.text);
            return it;
        }

        // 고른 순간 바로 '최근'에 넣어 같은 몰아치기 안에서 겹치지 않게 한다
        void Reserve(string raw)
        {
            serial++;
            lastUsedSerial[raw] = serial;
            if (recentTextSet.Add(raw))
            {
                recentTexts.Enqueue(raw);
                while (recentTexts.Count > RecentTextCount) recentTextSet.Remove(recentTexts.Dequeue());
            }
        }

        /// <summary>같은 종류가 이미 두 줄 연속이면 그 종류는 고르지 않는다.</summary>
        ChatKind? ForbiddenKind(List<Item> batch)
        {
            ChatKind a = kindA, b = kindB;
            // 아직 안 나간 대기열·묶음까지 이어서 본다
            foreach (var q in queue) { b = a; a = q.kind; }
            if (batch != null) foreach (var q in batch) { b = a; a = q.kind; }
            return a == b ? a : (ChatKind?)null;
        }

        int PickIndex(string situation, List<Item> batch)
        {
            if (!bySituation.TryGetValue(situation, out var list) || list.Count == 0) return -1;
            var forbid = ForbiddenKind(batch);
            var cand = new List<int>();
            foreach (int i in list)
                if (!recentTextSet.Contains(all[i].text) && (forbid == null || KindOf(all[i].kind) != forbid.Value)) cand.Add(i);
            if (cand.Count > 0) return cand[UnityEngine.Random.Range(0, cand.Count)];

            // 같은 종류 3연속을 피하려면 다른 종류가 필요 → 질문·악플 묶음에서 빌린다
            if (forbid != null)
            {
                foreach (var alt in new[] { "question", "hater" })
                {
                    if (alt == situation || !bySituation.TryGetValue(alt, out var al)) continue;
                    foreach (int i in al)
                        if (!recentTextSet.Contains(all[i].text) && KindOf(all[i].kind) != forbid.Value) cand.Add(i);
                    if (cand.Count > 0) return cand[UnityEngine.Random.Range(0, cand.Count)];
                }
            }
            // 최근 것만 남았으면 가장 오래전에 쓴 것
            foreach (int i in list) if (!recentTextSet.Contains(all[i].text)) cand.Add(i);
            if (cand.Count > 0) return cand[UnityEngine.Random.Range(0, cand.Count)];
            int best = -1, bestSerial = int.MaxValue;
            foreach (int i in list)
            {
                int s = lastUsedSerial.TryGetValue(all[i].text, out var v) ? v : -1;
                if (s < bestSerial) { bestSerial = s; best = i; }
            }
            return best;
        }

        string PickFromArray(string[] arr)
        {
            if (arr == null || arr.Length == 0) return null;
            var cand = new List<string>();
            foreach (var s in arr) if (!string.IsNullOrEmpty(s) && !HUDText.IsBanned(s) && !recentTextSet.Contains(s)) cand.Add(s);
            if (cand.Count == 0) return null;
            string pick = cand[UnityEngine.Random.Range(0, cand.Count)];
            Reserve(pick);
            return pick;
        }

        static string Fill(string text, Dictionary<string, string> fill)
        {
            if (fill == null || string.IsNullOrEmpty(text)) return text;
            foreach (var kv in fill) text = text.Replace(kv.Key, kv.Value);
            return text;
        }

        /// <summary>앞쪽 끼우기는 대기 중인 '대꾸' 바로 뒤에 (질문 → 대꾸 짝이 갈라지지 않게).</summary>
        void Insert(List<Item> items, bool front)
        {
            if (items == null || items.Count == 0) return;
            if (!front) { queue.AddRange(items); Trim(); return; }
            int at = 0;
            while (at < queue.Count && queue[at].isReply) at++;
            queue.InsertRange(at, items);
            Trim();
        }

        void Trim()
        {
            // 너무 밀리면 오래된 일반 줄부터 버린다
            for (int i = queue.Count - 1; i >= 0 && queue.Count > 14; i--)
                if (!queue[i].urgent && !queue[i].isReply) queue.RemoveAt(i);
        }

        // ---------- 내보내기 ----------

        void Fire(Item it)
        {
            if (feed == null) return;
            string nick = it.kind == ChatKind.Mod ? "" : PickNick();
            var kind = IsWarnSituation(it.situation) ? ChatKind.Warn : it.kind;
            feed.Emit(new ChatLine { Nick = nick, Text = it.text, Kind = kind, Amount = it.amount, Situation = it.situation });
            kindB = kindA; kindA = it.kind;
            if (it.situation == "caught") lastCaughtTime = Time.time;

            // 대꾸: 부모 바로 다음 칸에 1~2초 뒤
            if (!it.isReply && !it.noReply && it.raw != null && repliesByParent != null && repliesByParent.TryGetValue(it.raw, out var reps))
            {
                float chance = it.kind == ChatKind.Question ? 0.85f : 0.6f;
                if (UnityEngine.Random.value < chance)
                {
                    var cand = new List<int>();
                    foreach (int i in reps) if (!recentTextSet.Contains(all[i].text)) cand.Add(i);
                    if (cand.Count > 0)
                    {
                        var l = all[cand[UnityEngine.Random.Range(0, cand.Count)]];
                        Reserve(l.text);
                        queue.Insert(0, new Item
                        {
                            raw = l.text, text = Fill(l.text, it.fill), situation = it.situation, kind = ChatKind.Reply,
                            amount = 0, gap = UnityEngine.Random.Range(1f, 2f), urgent = it.urgent, isReply = true,
                            fill = it.fill, expires = Time.time + 10f,
                        });
                    }
                }
            }
        }

        string PickNick()
        {
            if (nicks == null || nicks.Length == 0) return "익명";
            string n = null;
            for (int tries = 0; tries < 24; tries++)
            {
                n = nicks[UnityEngine.Random.Range(0, nicks.Length)];
                if (!recentNickSet.Contains(n)) break;
            }
            if (recentNickSet.Add(n))
            {
                recentNicks.Enqueue(n);
                while (recentNicks.Count > RecentNickCount) recentNickSet.Remove(recentNicks.Dequeue());
            }
            return n;
        }

        /// <summary>아무 시청자 닉네임 하나 (StreamFeed.Warn 등).</summary>
        public static string RandomNick()
        {
            if (Instance != null) return Instance.PickNick();
            Load();
            return nicks != null && nicks.Length > 0 ? nicks[UnityEngine.Random.Range(0, nicks.Length)] : "익명";
        }
    }
}
