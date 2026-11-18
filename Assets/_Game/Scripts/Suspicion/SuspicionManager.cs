using System;
using System.Collections.Generic;
using UnityEngine;

namespace UndercoverLive
{
    /// <summary>전체 경계 레벨(0~3)과 감시자 목록을 관리하는 싱글턴.</summary>
    public class SuspicionManager : MonoBehaviour
    {
        public static SuspicionManager Instance { get; private set; }

        public const int MaxAlert = 3;

        [Range(0, 3)] public int alertLevel;
        public bool logEvents = true;

        public int AlertLevel => alertLevel;
        public bool IsCaught { get; private set; }

        /// <summary>경계 레벨이 3이 되면 한 번만 발생.</summary>
        public event Action Caught;
        /// <summary>경계 레벨이 바뀔 때(새 레벨 전달).</summary>
        public event Action<int> AlertChanged;

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

        /// <summary>경계 +1. 3이 되면 Caught 발생(한 번만).</summary>
        [Tooltip("한 사건으로 경계가 연달아 오르지 않게, 경계가 오른 뒤 이 시간 동안은 더 오르지 않음")]
        public float alertGrace = 6f;
        float lastAlertTime = -999f;

        public void AddAlert(string reason)
        {
            if (IsCaught) return;
            if (Time.time - lastAlertTime < alertGrace) return;
            lastAlertTime = Time.time;
            alertLevel = Mathf.Min(MaxAlert, alertLevel + 1);
            if (logEvents) Debug.Log($"[Suspicion] 경계 {alertLevel}/{MaxAlert} - {reason}");
            AlertChanged?.Invoke(alertLevel);
            if (alertLevel >= MaxAlert)
            {
                IsCaught = true;
                Caught?.Invoke();
            }
        }

        /// <summary>감시자 게이지가 100에 도달. 경계 +1, 그 감시자 게이지는 60으로(검문 대신 임시).</summary>
        public void ReportFull(Watcher w)
        {
            string who = w != null ? w.name : "?";
            AddAlert($"{who} 감지");
            if (w != null) w.suspicion = 40f;
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
