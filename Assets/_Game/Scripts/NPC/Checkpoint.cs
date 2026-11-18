using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 검문 진행.
    /// - 누군가의 의심이 100 → 가장 가까운 관리자가 통로를 따라 실제로 걸어와 플레이어 옆에서 검문.
    ///   유예(2.5초) 안에 폰이 주머니면 통과(경계 +1, 모든 의심 절반, 관리자 복귀), 아니면 폰을 빼앗김(ConfiscateFX).
    /// - 경계가 3이 되면 바로 관리자를 보내 유예 없이 빼앗는다(통과 직후라면 그 관리자가 그대로).
    /// - 일어선 채 오래 있으면 제지(앉으면 넘어감, 안 앉으면 검문). 무대로 가면 관리자 둘이 막으러 오고, 가까이 가면 붙잡혀 빼앗김.
    /// GameEvents.IsPlaying이 아니면 모든 진행(걷기·카운트다운·연출)이 멈췄다가 이어진다.
    /// </summary>
    public class Checkpoint : MonoBehaviour
    {
        public static Checkpoint Instance { get; private set; }

        /// <summary>검문(관리자 접근·제지·검문·뺏기) 진행 중. 예배 끝 결과 화면 등이 이 동안 뜨지 않게 읽는다.</summary>
        public static bool InProgress => Instance != null && Instance.mode != Mode.Idle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        [Header("검문")]
        [Tooltip("관리자가 말을 건 뒤 폰을 내릴 수 있는 시간(초)")] public float graceSeconds = 2.5f;
        [Tooltip("폰을 이미 내렸어도 검문 대사가 들리도록 기다리는 최소 시간(초)")] public float minCheckSeconds = 0.9f;
        [Tooltip("관리자가 멈추는 플레이어와의 수평 거리(m)")] public float approachDistance = 0.95f;
        [Tooltip("이 시간(초) 넘게 쫓아오면 걸음을 재촉")] public float hurryAfter = 8f;
        public float hurrySpeed = 1.5f;
        [Tooltip("통과하면 모든 감시자 의심에 곱하는 값")] public float passSuspicionScale = 0.5f;
        [Tooltip("경계가 3이 되면 바로 관리자를 보내 유예 없이 빼앗는다. 끄면 다음 검문 때 유예 없이 빼앗는다")]
        public bool confiscateAtMaxAlert = true;
        [Tooltip("검문 배너를 폰을 든(촬영) 화면에서도 띄울지. 위 가운데는 경고 배너(B)와 같은 자리라 검문 중엔 이 배너가 우선")]
        public bool bannerWhileAimed = true;

        [Header("제지·무대")]
        [Tooltip("'앉으세요' 뒤 앉을 때까지 기다리는 시간(초)")] public float sitComplySeconds = 3f;
        [Tooltip("무대로 갈 때 막으러 오는 걸음 배율")] public float stageRushSpeed = 1.35f;
        [Tooltip("붙잡으러 뛰어오는 걸음 배율")] public float grabRunSpeed = 1.8f;
        [Tooltip("두 번째 관리자가 서는 거리(m)")] public float secondStaffDistance = 1.7f;

        [Header("예배당 배치")]
        public ChapelLayout layout = new ChapelLayout();

        /// <summary>검문·제지·뺏기 중 하나가 진행 중.</summary>
        public bool Busy => mode != Mode.Idle;
        /// <summary>폰을 빼앗는 중이거나 끝남.</summary>
        public bool IsOver => mode == Mode.Confiscating || mode == Mode.Done;

        enum Mode { Idle, Inspect, SitWarn, Stage, Confiscating, Done }
        Mode mode = Mode.Idle;

        readonly List<StaffEscort> staff = new List<StaffEscort>();
        ConfiscateFX fx;
        Banner banner;
        SuspicionManager mgr;
        bool maxedPending;     // 경계 3 → 다음(또는 지금) 검문은 유예 없이
        bool upgradePending;   // 제지하러 오는 중에 의심 100 → 도착하면 검문

        static float Dt => GameEvents.IsPlaying ? Time.deltaTime : 0f;

        void Awake()
        {
            Instance = this;
            fx = GetComponent<ConfiscateFX>();
            if (fx == null) fx = gameObject.AddComponent<ConfiscateFX>();
        }

        void Start()
        {
            mgr = SuspicionManager.Instance;
            if (mgr != null)
            {
                mgr.FullReported += OnFull;
                mgr.AlertMaxed += OnAlertMaxed;
            }
            foreach (var w in FindObjectsByType<Watcher>())
            {
                if (w.kind != WatcherKind.Staff) continue;
                var e = w.GetComponent<StaffEscort>();
                if (e == null) e = w.gameObject.AddComponent<StaffEscort>();
                e.layout = layout;
                staff.Add(e);
            }
            banner = new Banner(transform);
        }

        void OnDestroy()
        {
            if (mgr != null)
            {
                mgr.FullReported -= OnFull;
                mgr.AlertMaxed -= OnAlertMaxed;
            }
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var pc = PhoneController.Instance;
            bool aimed = pc != null && pc.State == PhoneState.Aimed;
            if (banner != null) banner.Tick((bannerWhileAimed || !aimed) && !(fx != null && fx.Playing));
        }

        // ---------- 요청 ----------

        void OnFull(Watcher w)
        {
            if (IsOver || mode == Mode.Inspect) return;
            if (mode == Mode.SitWarn || mode == Mode.Stage) { upgradePending = true; return; }
            Begin(InspectFlow(Nearest(), maxedPending, false, true));
        }

        void OnAlertMaxed()
        {
            maxedPending = true;
            if (!confiscateAtMaxAlert || IsOver) return;
            if (mode == Mode.Idle) Begin(InspectFlow(Nearest(), true, false, true));
            else if (mode == Mode.SitWarn || mode == Mode.Stage) upgradePending = true;
            // Inspect 중이면 통과 직후 그 관리자가 그대로 빼앗는다
        }

        /// <summary>일어선 채 오래 있음 → 관리자가 와서 "앉으세요". (CrowdReactions가 부름)</summary>
        public void RequestSitWarning()
        {
            if (mode == Mode.Idle) Begin(SitWarnFlow());
        }

        /// <summary>무대로 다가감 → 관리자 둘이 막으러 온다. (CrowdReactions가 부름)</summary>
        public void RequestStageIntercept()
        {
            if (mode != Mode.Idle && mode != Mode.SitWarn) return;
            Begin(StageFlow());
        }

        /// <summary>무대 바로 앞 → 붙잡혀 빼앗김. (CrowdReactions가 부름)</summary>
        public void GrabAtStage()
        {
            if (IsOver) return;
            Begin(GrabFlow());
        }

        void Begin(IEnumerator flow)
        {
            StopAllCoroutines();
            if (banner != null) banner.Hide();
            StartCoroutine(flow);
        }

        // ---------- 흐름 ----------

        IEnumerator InspectFlow(StaffEscort s, bool noGrace, bool alreadyThere, bool announce)
        {
            mode = Mode.Inspect;
            upgradePending = false;
            if (s == null)
            {
                // 관리자가 없는 씬: 예전처럼 경계만 올린다
                if (mgr != null) mgr.AddAlert("검문(관리자 없음)");
                mode = Mode.Idle;
                yield break;
            }
            s.LookAtPlayer(true);
            if (!alreadyThere)
            {
                if (announce) Announce(s);
                yield return Approach(s, 1f, null);
            }
            s.Face(PlayerPoint);

            if (noGrace || maxedPending)
            {
                yield return TakePhone(s, NpcLines.TakeNow);
                yield break;
            }

            Say(NpcLines.Staff, NpcLines.AskPhone, 3f);
            GameEvents.RaiseCheckpointStarted(graceSeconds);
            GameEvents.RaiseChatCue("checkpoint");
            banner.ShowInspect(graceSeconds);

            float t = 0f;
            bool passed;
            while (true)
            {
                bool pocket = PhonePocketed;
                if (pocket && t >= minCheckSeconds) { passed = true; break; }
                if (t >= graceSeconds) { passed = pocket; break; }
                t += Dt;
                banner.SetRemaining(Mathf.Max(0f, graceSeconds - t), graceSeconds);
                yield return null;
            }
            banner.Hide();

            if (!passed)
            {
                yield return TakePhone(s, null);
                yield break;
            }

            // 통과
            Say(NpcLines.Staff, NpcLines.Passed, 3f);
            if (mgr != null) mgr.AddAlert("검문 통과", true);   // 경계 3이 되면 OnAlertMaxed → maxedPending
            GameEvents.RaiseCheckpointEnded(true);
            GameEvents.RaiseChatCue("checkpoint_survived");
            if (mgr != null) mgr.ScaleAll(passSuspicionScale);
            banner.ShowPassed(mgr != null ? mgr.AlertLevel : 0, SuspicionManager.MaxAlert);

            if (maxedPending && confiscateAtMaxAlert)
            {
                yield return Wait(1.8f);
                Say(NpcLines.Staff, NpcLines.FinalTake, 2.5f);
                yield return Wait(0.9f);
                yield return TakePhone(s, null);
                yield break;
            }
            yield return Wait(1.2f);
            s.LookAtPlayer(false);
            s.ReturnToPost();
            mode = Mode.Idle;
        }

        IEnumerator SitWarnFlow()
        {
            mode = Mode.SitWarn;
            upgradePending = false;
            var s = Nearest();
            if (s == null) { mode = Mode.Idle; yield break; }
            s.LookAtPlayer(true);
            Announce(s);
            yield return Approach(s, 1f, () => !PlayerStanding || upgradePending);

            if (upgradePending)
            {
                yield return InspectFlow(s, maxedPending, s.Arrived, false);
                yield break;
            }
            if (!PlayerStanding)
            {
                banner.Hide();
                yield return Release(s, 0.4f);
                yield break;
            }

            s.Face(PlayerPoint);
            Say(NpcLines.Staff, NpcLines.SitDown, 3f);
            yield return WaitForSit(s);
            if (mode != Mode.SitWarn) yield break;   // WaitForSit 안에서 검문으로 넘어감
            yield return Release(s, 0.6f);
        }

        IEnumerator StageFlow()
        {
            mode = Mode.Stage;
            upgradePending = false;
            var order = Ordered();
            if (order.Count == 0) { mode = Mode.Idle; yield break; }
            for (int i = 0; i < order.Count && i < 2; i++)
            {
                order[i].LookAtPlayer(true);
                order[i].GoTo(PathGoal, PlayerPoint, i == 0 ? approachDistance : secondStaffDistance, stageRushSpeed);
            }
            GameEvents.RaiseWarning(NpcLines.StaffBlocking);
            GameEvents.RaiseChatCue("warn_staff_coming");

            StaffEscort first = null;
            while (first == null)
            {
                for (int i = 0; i < order.Count && i < 2; i++) if (order[i].Arrived) { first = order[i]; break; }
                if (!PlayerStanding || upgradePending) break;
                yield return null;
            }
            if (first == null) first = order[0];

            if (upgradePending)
            {
                yield return InspectFlow(first, maxedPending, first.Arrived, false);
                if (mode == Mode.Idle) ReleaseOthers(first);
                yield break;
            }
            if (!PlayerStanding)
            {
                banner.Hide();
                yield return Wait(0.4f);
                ReleaseAll(order);
                mode = Mode.Idle;
                yield break;
            }

            first.Face(PlayerPoint);
            Say(NpcLines.Staff, NpcLines.BackToSeat, 3f);
            mode = Mode.SitWarn;   // 이후는 제지와 같다
            yield return WaitForSit(first);
            if (mode == Mode.Idle) { ReleaseOthers(first); yield break; }
            if (mode != Mode.SitWarn) yield break;
            banner.Hide();
            yield return Wait(0.6f);
            ReleaseAll(order);
            mode = Mode.Idle;
        }

        /// <summary>"앉으세요" 뒤 기다림. 앉으면 그대로 끝(mode 유지), 안 앉거나 의심 100이면 검문으로.</summary>
        IEnumerator WaitForSit(StaffEscort s)
        {
            banner.ShowSit(sitComplySeconds);
            float t = 0f;
            while (t < sitComplySeconds && PlayerStanding && !upgradePending)
            {
                t += Dt;
                banner.SetRemaining(Mathf.Max(0f, sitComplySeconds - t), sitComplySeconds);
                yield return null;
            }
            banner.Hide();
            if (!PlayerStanding && !upgradePending) yield break;
            yield return InspectFlow(s, maxedPending, true, false);
        }

        IEnumerator GrabFlow()
        {
            mode = Mode.Confiscating;
            var walk = PlayerWalk.Instance;
            if (walk != null) walk.SetLocked(true);
            Say(NpcLines.Staff, NpcLines.Stop, 2f);
            var order = Ordered();
            for (int i = 0; i < order.Count && i < 2; i++)
            {
                order[i].LookAtPlayer(true);
                order[i].GoTo(PathGoal, PlayerPoint, i == 0 ? approachDistance : secondStaffDistance, grabRunSpeed);
            }
            StaffEscort arrived = null;
            float t = 0f;
            while (arrived == null && t < 8f)
            {
                for (int i = 0; i < order.Count && i < 2; i++) if (order[i].Arrived) { arrived = order[i]; break; }
                t += Dt;
                yield return null;
            }
            if (arrived == null && order.Count > 0) arrived = order[0];
            if (arrived != null) arrived.Face(PlayerPoint);
            yield return TakePhone(arrived, null);
        }

        /// <summary>폰 빼앗기: CheckpointEnded(false) → ChatCue caught → 화면 연출 → 폰 주머니로 → Confiscated → Caught.</summary>
        IEnumerator TakePhone(StaffEscort s, string line)
        {
            mode = Mode.Confiscating;
            banner.Hide();
            var walk = PlayerWalk.Instance;
            if (walk != null) walk.SetLocked(true);
            if (s != null) s.LookAtPlayer(true);
            if (!string.IsNullOrEmpty(line))
            {
                Say(NpcLines.Staff, line, 2.5f);
                yield return Wait(0.7f);
            }
            GameEvents.RaiseCheckpointEnded(false);
            GameEvents.RaiseChatCue("caught");

            var pc = PhoneController.Instance;
            PhoneState st = pc != null ? pc.State : PhoneState.Pocket;
            Vector3 grabber = s != null ? s.transform.position + Vector3.up * 1.3f : PlayerPoint();
            bool cut = false;
            fx.Play(st, grabber, () => cut = true);
            while (!cut) yield return null;

            if (pc != null) pc.SetState(PhoneState.Pocket);
            GameEvents.RaiseConfiscated(NpcLines.ConfiscatedReason);
            if (mgr != null) mgr.MarkCaught();
            mode = Mode.Done;
        }

        IEnumerator Approach(StaffEscort s, float speed, Func<bool> abort)
        {
            s.GoTo(PathGoal, PlayerPoint, approachDistance, speed);
            float t = 0f;
            while (!s.Arrived)
            {
                if (abort != null && abort()) yield break;
                t += Dt;
                if (t > hurryAfter) s.SpeedMultiplier = Mathf.Max(s.SpeedMultiplier, hurrySpeed);
                yield return null;
            }
        }

        IEnumerator Release(StaffEscort s, float delay)
        {
            yield return Wait(delay);
            s.LookAtPlayer(false);
            s.ReturnToPost();
            mode = Mode.Idle;
        }

        void ReleaseOthers(StaffEscort keep)
        {
            foreach (var s in staff)
            {
                if (s == null || s == keep || !s.Away) continue;
                s.LookAtPlayer(false);
                s.ReturnToPost();
            }
        }

        void ReleaseAll(List<StaffEscort> list)
        {
            foreach (var s in list)
            {
                if (s == null) continue;
                s.LookAtPlayer(false);
                s.ReturnToPost();
            }
        }

        static IEnumerator Wait(float seconds)
        {
            while (seconds > 0f)
            {
                seconds -= Dt;
                yield return null;
            }
        }

        // ---------- 도움 ----------

        /// <summary>관리자 접근 알림: 화면 표시는 경고 배너(B)가 Warning으로 그린다.</summary>
        void Announce(StaffEscort s)
        {
            GameEvents.RaiseWarning(NpcLines.Direction(DirectionWord(s.transform.position)));
            GameEvents.RaiseChatCue("warn_staff_coming");
        }

        StaffEscort Nearest()
        {
            var list = Ordered();
            return list.Count > 0 ? list[0] : null;
        }

        List<StaffEscort> Ordered()
        {
            var list = new List<StaffEscort>();
            foreach (var s in staff) if (s != null && s.isActiveAndEnabled) list.Add(s);
            Vector3 p = PlayerPoint();
            list.Sort((a, b) => (a.transform.position - p).sqrMagnitude.CompareTo((b.transform.position - p).sqrMagnitude));
            return list;
        }

        static bool PhonePocketed
        {
            get
            {
                var pc = PhoneController.Instance;
                return pc == null || pc.State == PhoneState.Pocket;
            }
        }

        static bool PlayerStanding
        {
            get
            {
                var w = PlayerWalk.Instance;
                return w != null && w.Standing;
            }
        }

        /// <summary>플레이어 발 위치(수평 거리 기준점).</summary>
        public static Vector3 PlayerPoint()
        {
            var w = PlayerWalk.Instance;
            if (w != null) return w.transform.position;
            var pc = PhoneController.Instance;
            if (pc != null && pc.Head != null) { var h = pc.Head.position; h.y = 0f; return h; }
            return Vector3.zero;
        }

        /// <summary>관리자가 걸어갈 목표점: 앉아 있으면 그 줄 앞 틈, 서 있으면 플레이어 자리.</summary>
        Vector3 PathGoal()
        {
            var w = PlayerWalk.Instance;
            if (w != null && !w.Standing) return w.SeatPosition + Vector3.forward * layout.seatGapOffset;
            return PlayerPoint();
        }

        static string DirectionWord(Vector3 from)
        {
            var pc = PhoneController.Instance;
            Transform head = pc != null ? pc.Head : null;
            if (head == null) return "근처에서";
            Vector3 local = head.InverseTransformPoint(from);
            float a = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(a) <= 45f) return "앞에서";
            if (Mathf.Abs(a) >= 135f) return "뒤에서";
            return a > 0f ? "오른쪽에서" : "왼쪽에서";
        }

        static void Say(string who, string line, float seconds)
        {
            if (GameFlow.Instance != null) GameFlow.Instance.Say(who, line, seconds);
        }

        // ---------- 상단 검문 배너 (시안 '검문/' 상단 배너: 문구 + [4] 숨기기 남은 시간 + 줄어드는 막대) ----------

        class Banner
        {
            enum Kind { None, Inspect, Sit, Passed }

            static readonly Color RedBg = new Color(24 / 255f, 8 / 255f, 12 / 255f, 0.95f);
            static readonly Color RedLine = UIKit.Hex("#FF4D5A");
            static readonly Color RedIcon = UIKit.Hex("#E5484D");
            static readonly Color RedLabel = UIKit.Hex("#FF9AA2");
            static readonly Color RedDeep = UIKit.Hex("#991B1B");
            static readonly Color AmberBg = new Color(32 / 255f, 18 / 255f, 10 / 255f, 0.92f);
            static readonly Color AmberLine = UIKit.Hex("#F59E0B");
            static readonly Color AmberLabel = UIKit.Hex("#FED7AA");
            static readonly Color AmberDeep = UIKit.Hex("#9A3412");

            const float X = 480f, BaseY = 36f, W = 960f, H = 96f, PillW = 216f;

            readonly RectTransform root;
            readonly CanvasGroup group;
            readonly Image bg, border, icon, pill, keyChip, barBg, barFill;
            readonly Text label, main, pillText, keyText;
            Kind kind;
            string pillFmt;
            float appear, passedLeft;

            public Banner(Transform parent)
            {
                var canvas = UIKit.Canvas("CheckpointCanvas", 110, parent);
                root = UIKit.Rect("Banner", canvas.transform);
                PlaceTopCenter(root, X, BaseY, W, H);
                group = root.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;

                bg = UIKit.Box("Bg", root, RedBg, 22);
                UIKit.Stretch(bg.rectTransform);
                border = UIKit.Image("Border", root, RedLine, UIKit.Rounded(22, 2));
                UIKit.Stretch(border.rectTransform);

                // 경고 표시: 둥근 판 + 막대·점 (글자 대신 도형)
                icon = UIKit.Box("Icon", root, RedIcon, 12);
                UIKit.Place(icon.rectTransform, 24, 20, 56, 56);
                UIKit.Place(UIKit.Box("Bar", icon.transform, Color.white, 3).rectTransform, 25, 11, 6, 24);
                UIKit.Place(UIKit.Box("Dot", icon.transform, Color.white, 3).rectTransform, 25, 39, 6, 6);

                label = Line("Label", root, 15, RedLabel);
                UIKit.Place(label.rectTransform, 104, 16, 560, 24);
                main = Line("Main", root, 22, Color.white);
                UIKit.Place(main.rectTransform, 104, 42, 580, 36);

                // 오른쪽: [키] 동작 · 남은 시간 알약 + 줄어드는 막대
                pill = UIKit.Box("Pill", root, Color.white, 10);
                UIKit.Place(pill.rectTransform, W - 24 - PillW, 16, PillW, 40);
                keyChip = UIKit.Box("Key", pill.transform, RedDeep, 6);
                UIKit.Place(keyChip.rectTransform, 8, 8, 24, 24);
                keyText = UIKit.Text("K", keyChip.transform, NpcLines.BannerInspectKey, 15, Color.white, true, TextAnchor.MiddleCenter);
                UIKit.Stretch(keyText.rectTransform);
                pillText = Line("Text", pill.transform, 16, RedDeep);
                UIKit.Place(pillText.rectTransform, 40, 0, PillW - 48, 40);

                barBg = UIKit.Image("BarBg", root, new Color(1f, 1f, 1f, 0.22f));
                UIKit.Place(barBg.rectTransform, W - 24 - PillW, 68, PillW, 8);
                barFill = UIKit.Image("BarFill", barBg.transform, RedLine);
                var fr = barFill.rectTransform;
                fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.pivot = new Vector2(0f, 0.5f);
                fr.offsetMin = fr.offsetMax = Vector2.zero;
                kind = Kind.None;
            }

            static Text Line(string name, Transform parent, int size, Color c)
            {
                var t = UIKit.Text(name, parent, "", size, c, true, TextAnchor.MiddleLeft);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                return t;
            }

            static void PlaceTopCenter(RectTransform rt, float x, float y, float w, float h)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(x - 960f, -y);
                rt.sizeDelta = new Vector2(w, h);
            }

            void Style(bool red, string lab, string mainText, string key, string pillFormatOrNull)
            {
                bg.color = red ? RedBg : AmberBg;
                border.color = red ? RedLine : AmberLine;
                icon.color = red ? RedIcon : AmberLine;
                label.color = red ? RedLabel : AmberLabel;
                label.text = lab;
                main.text = mainText;
                bool timer = pillFormatOrNull != null;
                pillFmt = pillFormatOrNull;
                pill.gameObject.SetActive(timer);
                barBg.gameObject.SetActive(timer);
                keyChip.color = red ? RedDeep : AmberDeep;
                keyText.text = key ?? "";
                pillText.color = red ? RedDeep : AmberDeep;
                barFill.color = red ? RedLine : AmberLine;
                main.rectTransform.sizeDelta = new Vector2(timer ? 580f : 820f, 36f);
                appear = 0f;
            }

            public void ShowInspect(float seconds)
            {
                kind = Kind.Inspect;
                Style(true, NpcLines.BannerInspectLabel, NpcLines.BannerInspectMain, NpcLines.BannerInspectKey, NpcLines.BannerInspectPill);
                SetRemaining(seconds, seconds);
            }

            public void ShowSit(float seconds)
            {
                kind = Kind.Sit;
                Style(false, NpcLines.BannerSitLabel, NpcLines.BannerSitMain, NpcLines.BannerSitKey, NpcLines.BannerSitPill);
                SetRemaining(seconds, seconds);
            }

            public void ShowPassed(int level, int max)
            {
                kind = Kind.Passed;
                Style(false, NpcLines.BannerPassLabel, string.Format(NpcLines.BannerPassMain, level, max), null, null);
                passedLeft = 2.2f;
            }

            public void SetRemaining(float left, float total)
            {
                if (pillFmt != null) pillText.text = string.Format(pillFmt, left);
                barFill.rectTransform.anchorMax = new Vector2(total > 0f ? Mathf.Clamp01(left / total) : 0f, 1f);
            }

            public void Hide() { kind = Kind.None; }

            /// <summary>매 프레임. allowed = 지금 화면에 보여도 되는가.</summary>
            public void Tick(bool allowed)
            {
                float udt = Time.unscaledDeltaTime;
                if (kind == Kind.Passed && GameEvents.IsPlaying)
                {
                    passedLeft -= udt;
                    if (passedLeft <= 0f) kind = Kind.None;
                }
                bool show = kind != Kind.None && allowed;
                if (show)
                {
                    appear = Mathf.Min(1f, appear + udt / 0.22f);
                    float e = 1f - Mathf.Pow(1f - appear, 3f);
                    group.alpha = e;
                    root.anchoredPosition = new Vector2(X - 960f, -BaseY + 16f * (1f - e));
                }
                else
                {
                    group.alpha = Mathf.MoveTowards(group.alpha, 0f, udt / 0.15f);
                    if (group.alpha <= 0f) appear = 0f;
                }
            }
        }
    }
}
