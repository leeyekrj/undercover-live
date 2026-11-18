using System;
using System.Collections.Generic;
using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 전체 경계 레벨(0~3)과 감시자 목록을 관리하는 싱글턴.
    /// 감시자 게이지가 100이 되면 FullReported → 검문 담당(Checkpoint)이 관리자를 보낸다.
    /// 경계가 3이 되면 AlertMaxed → 관리자가 폰을 빼앗고, 그 연출이 끝날 때 MarkCaught()로 Caught가 발생한다.
    /// 검문 담당이 없는 씬이면 예전처럼 바로 경계 +1 / 즉시 Caught.
    /// </summary>
    public class SuspicionManager : MonoBehaviour
    {
        public static SuspicionManager Instance { get; private set; }

        public const int MaxAlert = 3;

        [Range(0, 3)] public int alertLevel;
        public bool logEvents = true;

        public int AlertLevel => alertLevel;
        public bool IsCaught { get; private set; }

        /// <summary>발각 확정(폰을 빼앗기는 연출이 끝남). 한 번만 발생.</summary>
        public event Action Caught;
        /// <summary>경계 레벨이 바뀔 때(새 레벨 전달).</summary>
        public event Action<int> AlertChanged;
        /// <summary>감시자 게이지가 100에 도달 → 검문 요청. 구독자가 없으면 예전처럼 경계 +1.</summary>
        public event Action<Watcher> FullReported;
        /// <summary>경계가 3에 도달. 구독자가 없으면 바로 Caught.</summary>
        public event Action AlertMaxed;

        readonly List<Watcher> watchers = new List<Watcher>();

        public IReadOnlyList<Watcher> Watchers => watchers;

        /// <summary>게이지가 가장 높은 감시자(없거나 전부 0이면 null).</summary>
        public Watcher MostSuspicious
        {
            get
            {
                Watcher best = null;
                float max = 0.001f;
                for (int i = 0; i < watchers.Count; i++)
                {
                    var w = watchers[i];
                    if (w == null) continue;
                    if (w.suspicion > max) { max = w.suspicion; best = w; }
                }
                return best;
            }
        }

        /// <summary>가장 높은 게이지(0~1).</summary>
        public float MaxSuspicion01
        {
            get
            {
                float max = 0f;
                for (int i = 0; i < watchers.Count; i++)
                {
                    var w = watchers[i];
                    if (w != null && w.suspicion > max) max = w.suspicion;
                }
                return Mathf.Clamp01(max / 100f);
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Watcher.CrowdFocus = 1f;   // 정적 값은 씬을 다시 불러올 때 초기화(기적 도중 R이면 0으로 남지 않게)
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Register(Watcher w)
        {
            if (w != null && !watchers.Contains(w)) watchers.Add(w);
        }

        public void Unregister(Watcher w)
        {
            watchers.Remove(w);
        }

        [Tooltip("한 사건으로 경계가 연달아 오르지 않게, 경계가 오른 뒤 이 시간 동안은 더 오르지 않음 (force면 무시)")]
        public float alertGrace = 6f;
        float lastAlertTime = -999f;

        /// <summary>경계 +1. 3이 되면 AlertMaxed(구독자 없으면 Caught). force = 유예 시간 무시(검문 통과처럼 확정된 사건).</summary>
        public void AddAlert(string reason, bool force = false)
        {
            if (IsCaught || alertLevel >= MaxAlert) return;
            if (!force && Time.time - lastAlertTime < alertGrace) return;
            lastAlertTime = Time.time;
            alertLevel = Mathf.Min(MaxAlert, alertLevel + 1);
            if (logEvents) Debug.Log($"[Suspicion] 경계 {alertLevel}/{MaxAlert} - {reason}");
            AlertChanged?.Invoke(alertLevel);
            if (alertLevel >= MaxAlert)
            {
                if (AlertMaxed != null) AlertMaxed.Invoke();
                else MarkCaught();
            }
        }

        /// <summary>감시자 게이지가 100에 도달. 검문 담당이 있으면 검문 요청, 없으면 경계 +1 후 게이지 40.</summary>
        public void ReportFull(Watcher w)
        {
            if (IsCaught) return;
            if (FullReported != null)
            {
                FullReported.Invoke(w);
                return;
            }
            string who = w != null ? w.name : "?";
            AddAlert($"{who} 감지");
            if (w != null) w.suspicion = 40f;
        }

        /// <summary>폰을 빼앗기는 연출이 끝났을 때 검문 담당이 부른다. Caught를 한 번만 발생.</summary>
        public void MarkCaught()
        {
            if (IsCaught) return;
            IsCaught = true;
            if (logEvents) Debug.Log("[Suspicion] 발각 — 폰 압수");
            Caught?.Invoke();
        }

        /// <summary>모든 감시자 게이지를 배율만큼 줄인다(검문 통과 후 절반).</summary>
        public void ScaleAll(float factor)
        {
            for (int i = 0; i < watchers.Count; i++)
                if (watchers[i] != null) watchers[i].suspicion *= factor;
        }

        /// <summary>디버그·재시작용 초기화.</summary>
        public void ResetAll()
        {
            alertLevel = 0;
            IsCaught = false;
            for (int i = 0; i < watchers.Count; i++)
                if (watchers[i] != null) watchers[i].suspicion = 0f;
            AlertChanged?.Invoke(0);
        }
    }
}
