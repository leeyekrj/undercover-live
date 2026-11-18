using System;
using System.Collections.Generic;
using UnityEngine;

namespace UndercoverLive
{
    /// <summary>등급. C가 최저(품질 0.25 이상).</summary>
    public enum CaptureGrade { C, B, A, S }

    /// <summary>확보된 클립 한 건의 기록.</summary>
    public class CaptureResult
    {
        public CaptureTarget Target;
        public string TargetId;
        public string TargetName;
        public CaptureCategory Category;
        public CaptureGrade Grade;
        public float AverageQuality;
        public int Viewers;          // 이 클립으로 얻은 시청자
        public float NewsValue;      // 보도 가치 (증거력)
        public float Novelty;        // 새로움 배율
        public float Flow;           // 흐름 배율
        public Texture2D Snapshot;   // 확보 순간 폰 피드 (없을 수 있음)
        public float GameTime;       // Time.time
    }

    /// <summary>이번 판의 클립 기록. 판정기가 Add로 쌓고 결과 화면이 읽는다.</summary>
    public static class CaptureLog
    {
        public static readonly List<CaptureResult> Clips = new List<CaptureResult>();
        /// <summary>클립이 쌓일 때마다 발생 (StreamFeed 등이 구독).</summary>
        public static event Action<CaptureResult> Added;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Clear();
            Added = null;
        }

        public static void Add(CaptureResult r)
        {
            Clips.Add(r);
            Added?.Invoke(r);
        }

        public static void Clear()
        {
            foreach (var c in Clips)
                if (c.Snapshot != null) UnityEngine.Object.Destroy(c.Snapshot);
            Clips.Clear();
        }

        /// <summary>같은 대상을 지금까지 몇 번 확보했는지.</summary>
        public static int CountFor(string targetId)
        {
            int n = 0;
            for (int i = 0; i < Clips.Count; i++)
                if (Clips[i].TargetId == targetId) n++;
            return n;
        }

        /// <summary>since 시각 이후의 확보 수.</summary>
        public static int CountSince(float since)
        {
            int n = 0;
            for (int i = Clips.Count - 1; i >= 0; i--)
            {
                if (Clips[i].GameTime < since) break;
                n++;
            }
            return n;
        }

        public static int TotalViewersGained()
        {
            int s = 0;
            foreach (var c in Clips) s += c.Viewers;
            return s;
        }

        public static float TotalNewsValue()
        {
            float s = 0;
            foreach (var c in Clips) s += c.NewsValue;
            return s;
        }
    }
}
