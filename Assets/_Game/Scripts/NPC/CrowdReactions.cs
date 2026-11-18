using System.Collections.Generic;
using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 플레이어가 일어섰을 때 주변 반응.
    /// - 반경 6m 안 신도 몇 명이 시차를 두고 고개를 돌려 본다(앉으면 되돌림).
    /// - 서 있는 동안 화면 아래 수군거림 자막을 가끔(4종 돌림), ChatCue stand_up.
    /// - 6초 넘게 서 있으면 관리자가 와서 "앉으세요"(Checkpoint.RequestSitWarning).
    /// - 무대 앞 경계선까지 4m 이하 → PlayerNearStage, ChatCue rush_stage, 교주가 똑바로 바라보며 한마디, 관리자 둘이 막으러 옴.
    ///   2m 이하 → 붙잡혀 폰을 빼앗김(Checkpoint.GrabAtStage).
    /// GameEvents.IsPlaying이 아니면 멈춘다.
    /// </summary>
    public class CrowdReactions : MonoBehaviour
    {
        [Header("돌아보기")]
        [Tooltip("일어서면 이 반경(m) 안 신도가 돌아본다")] public float lookRadius = 6f;
        [Range(0f, 1f), Tooltip("반경 안 신도 한 명이 돌아볼 확률 (전원 X)")] public float lookChance = 0.5f;
        [Tooltip("가까운 순서로 한 명씩 늦게 돌아보는 간격(초)")] public float lookStagger = 0.16f;
        [Tooltip("돌아보기 시작에 더하는 무작위 지연(초)")] public Vector2 lookJitter = new Vector2(0.1f, 0.6f);

        [Header("수군거림")]
        [Tooltip("일어선 뒤 첫 수군거림까지(초)")] public float whisperFirst = 1.6f;
        [Tooltip("다음 수군거림까지 간격(초)")] public Vector2 whisperEvery = new Vector2(4.5f, 7f);
        public float whisperSeconds = 2.5f;

        [Header("제지·무대")]
        [Tooltip("이 시간(초) 넘게 서 있으면 관리자가 제지하러 온다")] public float standWarnAfter = 6f;
        [Tooltip("무대 앞 경계선까지 이 거리(m) 이하면 교주가 알아채고 관리자가 막으러 온다")] public float nearStageDistance = 4f;
        [Tooltip("무대 앞 경계선까지 이 거리(m) 이하면 붙잡힌다")] public float grabStageDistance = 2f;
        public float leaderLineSeconds = 3f;

        class Person
        {
            public HeadLook head;
            public Transform root;
            public bool decided, looking;
        }

        readonly List<Person> crowd = new List<Person>();
        HeadLook leaderHead;
        LeaderGaze leaderGaze;
        Checkpoint cp;
        bool standing, nearFired, grabFired;
        float standTime, whisperTimer, scanTimer;
        int whisperIdx;

        void Awake()
        {
            GameEvents.PlayerStoodChanged += OnStood;
        }

        void OnDestroy()
        {
            GameEvents.PlayerStoodChanged -= OnStood;
        }

        void Start()
        {
            cp = GetComponent<Checkpoint>();
            foreach (var w in FindObjectsByType<Watcher>())
            {
                if (w.kind == WatcherKind.Believer)
                {
                    var h = HeadLook.For(w.transform);
                    if (h != null) crowd.Add(new Person { head = h, root = w.transform });
                }
                else if (w.kind == WatcherKind.Leader)
                {
                    leaderHead = HeadLook.For(w.transform);
                    leaderGaze = w.GetComponent<LeaderGaze>();
                }
            }
            whisperIdx = Random.Range(0, NpcLines.Whispers.Length);
            var walk = PlayerWalk.Instance;
            if (walk != null && walk.Standing) OnStood(true);
        }

        void OnStood(bool s)
        {
            if (s == standing) return;
            standing = s;
            if (s)
            {
                standTime = 0f;
                whisperTimer = whisperFirst;
                scanTimer = 0f;
                grabFired = false;
                foreach (var p in crowd) p.decided = false;
                ScheduleLooks(true);
                GameEvents.RaiseChatCue("stand_up");
            }
            else
            {
                foreach (var p in crowd)
                {
                    if (p.looking) p.head.Request(HeadLook.Stand, false, Random.Range(0.1f, 0.9f));
                    p.looking = false;
                    p.decided = false;
                }
                ReleaseLeader();
                nearFired = false;
            }
        }

        void Update()
        {
            if (!GameEvents.IsPlaying || !standing) return;
            float dt = Time.deltaTime;
            bool busy = cp != null && cp.Busy;

            scanTimer -= dt;
            if (scanTimer <= 0f)
            {
                scanTimer = 0.4f;
                ScheduleLooks(false);
            }

            // 수군거림 (관리자가 말하는 중엔 자막을 덮지 않게 쉼)
            if (!busy)
            {
                whisperTimer -= dt;
                if (whisperTimer <= 0f)
                {
                    whisperTimer = Random.Range(whisperEvery.x, whisperEvery.y);
                    var lines = NpcLines.Whispers;
                    Say(NpcLines.Believer, lines[whisperIdx % lines.Length], whisperSeconds);
                    whisperIdx++;
                }
            }

            // 오래 서 있으면 제지
            if (!busy)
            {
                standTime += dt;
                if (standTime >= standWarnAfter && cp != null)
                {
                    standTime = 0f;
                    cp.RequestSitWarning();
                }
            }

            // 무대 접근
            Vector3 pp = Checkpoint.PlayerPoint();
            float d = cp != null ? cp.layout.StageDistance(pp) : 99f;
            if (!nearFired && d <= nearStageDistance)
            {
                nearFired = true;
                OnNearStage();
            }
            else if (nearFired && d > nearStageDistance + 1.5f)
            {
                nearFired = false;
                ReleaseLeader();
            }
            if (!grabFired && d <= grabStageDistance && cp != null && !cp.IsOver)
            {
                grabFired = true;
                cp.GrabAtStage();
            }
        }

        void OnNearStage()
        {
            GameEvents.RaisePlayerNearStage();
            GameEvents.RaiseChatCue("rush_stage");
            var pc = PhoneController.Instance;
            if (leaderGaze != null && pc != null && pc.Head != null) leaderGaze.Fixate(pc.Head);
            if (leaderHead != null) leaderHead.Set(HeadLook.Stage, true);
            Say(NpcLines.Leader, NpcLines.LeaderCallOut, leaderLineSeconds);
            if (cp != null) cp.RequestStageIntercept();
        }

        void ReleaseLeader()
        {
            if (cp != null && cp.IsOver) return;   // 붙잡힌 뒤엔 계속 본다
            if (leaderGaze != null) leaderGaze.Release();
            if (leaderHead != null) leaderHead.Set(HeadLook.Stage, false);
        }

        /// <summary>반경 안에 새로 들어온 신도는 한 번씩 돌아볼지 정한다. 멀어지면 되돌린다.</summary>
        void ScheduleLooks(bool initial)
        {
            Vector3 pp = Checkpoint.PlayerPoint();
            var cands = new List<Person>();
            foreach (var p in crowd)
            {
                if (p.root == null) continue;
                float dist = Flat(p.root.position - pp);
                if (p.looking && dist > lookRadius + 1.5f)
                {
                    p.head.Request(HeadLook.Stand, false, Random.Range(0.2f, 0.8f));
                    p.looking = false;
                    p.decided = false;
                    continue;
                }
                if (!p.decided && dist <= lookRadius) cands.Add(p);
            }
            if (Watcher.CrowdFocus < 0.5f) return;   // 눈을 가린 의례 중엔 새로 돌아보지 않음
            cands.Sort((a, b) => Flat(a.root.position - pp).CompareTo(Flat(b.root.position - pp)));
            int rank = 0;
            foreach (var p in cands)
            {
                p.decided = true;
                if (Random.value > lookChance) continue;
                float delay = (initial ? rank * lookStagger : 0f) + Random.Range(lookJitter.x, lookJitter.y);
                p.head.Request(HeadLook.Stand, true, delay);
                p.looking = true;
                rank++;
            }
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        static void Say(string who, string line, float seconds)
        {
            if (GameFlow.Instance != null) GameFlow.Instance.Say(who, line, seconds);
        }
    }
}
