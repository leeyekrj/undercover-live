using System;
using System.Collections.Generic;
using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 예배당 바닥 배치(ChapelGreybox 기준)와 관리자 걷는 길 계산.
    /// 의자 줄 사이로 대각선으로 가로지르지 않고, 가운데·양옆 통로와 앞·뒤 빈 곳을 따라 걷는다.
    /// 좌석 블록 안의 목표는 가장 가까운 통로에서 그 줄 사이 틈으로 들어간다.
    /// </summary>
    [Serializable]
    public class ChapelLayout
    {
        [Tooltip("가운데 통로 x")] public float centerAisleX = 0f;
        [Tooltip("양옆 통로 x (±)")] public float sideAisleX = 6.7f;
        [Tooltip("|x - 가운데| 가 이보다 작으면 가운데 통로")] public float aisleHalfWidth = 1.4f;
        [Tooltip("|x| 가 이보다 크면 옆 통로")] public float blockOuterX = 6.4f;
        [Tooltip("의자 줄이 있는 z 범위")] public float rowsMinZ = -11.4f, rowsMaxZ = 6.5f;
        [Tooltip("통로를 옮겨 갈 때 지나는 앞·뒤 빈 곳 z")] public float frontCrossZ = 7.4f, backCrossZ = -12.3f;
        [Tooltip("앉은 자리 앞 줄 사이 틈(자리 z + 이 값)")] public float seatGapOffset = 0.8f;
        [Tooltip("무대 앞 경계선 z (계단 앞면)")] public float stageEdgeZ = 8.5f;

        public bool InRows(Vector3 p) => p.z > rowsMinZ && p.z < rowsMaxZ;

        /// <summary>좌석 블록 안(통로·빈 곳이 아님).</summary>
        public bool InBlock(Vector3 p)
        {
            return InRows(p) && Mathf.Abs(p.x - centerAisleX) > aisleHalfWidth && Mathf.Abs(p.x) < blockOuterX;
        }

        public float NearestAisle(float x)
        {
            float best = centerAisleX, d = Mathf.Abs(x - centerAisleX);
            if (Mathf.Abs(x - sideAisleX) < d) { best = sideAisleX; d = Mathf.Abs(x - sideAisleX); }
            if (Mathf.Abs(x + sideAisleX) < d) best = -sideAisleX;
            return best;
        }

        /// <summary>무대 앞 경계선까지 거리(넘었으면 0 이하).</summary>
        public float StageDistance(Vector3 p) => stageEdgeZ - p.z;

        /// <summary>from → to 걷는 길(from 제외, to 포함). y는 from 높이로 맞춘다.</summary>
        public List<Vector3> Plan(Vector3 from, Vector3 to)
        {
            var pts = new List<Vector3>();
            float y = from.y;
            Vector3 a = new Vector3(from.x, y, from.z);
            if (InBlock(a))
            {
                a = new Vector3(NearestAisle(a.x), y, a.z);
                pts.Add(a);
            }
            Vector3 b = new Vector3(to.x, y, to.z);
            bool bBlock = InBlock(b);
            Vector3 bEntry = bBlock ? new Vector3(NearestAisle(b.x), y, b.z) : b;

            bool aOpen = !InRows(a), bOpen = !InRows(bEntry);
            if (aOpen && bOpen)
            {
                bool aFront = a.z >= rowsMaxZ, bFront = bEntry.z >= rowsMaxZ;
                if (aFront != bFront)
                {
                    float ax = NearestAisle((a.x + bEntry.x) * 0.5f);
                    pts.Add(new Vector3(ax, y, a.z));
                    pts.Add(new Vector3(ax, y, bEntry.z));
                }
            }
            else if (!aOpen && !bOpen)
            {
                float ax = NearestAisle(a.x), bx = NearestAisle(bEntry.x);
                if (!Mathf.Approximately(ax, bx))
                {
                    float viaFront = Mathf.Abs(a.z - frontCrossZ) + Mathf.Abs(bEntry.z - frontCrossZ);
                    float viaBack = Mathf.Abs(a.z - backCrossZ) + Mathf.Abs(bEntry.z - backCrossZ);
                    float zc = viaFront <= viaBack ? frontCrossZ : backCrossZ;
                    pts.Add(new Vector3(ax, y, zc));
                    pts.Add(new Vector3(bx, y, zc));
                }
                else if (Mathf.Abs(a.x - ax) > 0.05f) pts.Add(new Vector3(ax, y, a.z));
            }
            else if (!aOpen)
            {
                // 통로 → 빈 곳: 통로를 따라 그 높이까지 간 뒤 옆으로
                float ax = NearestAisle(a.x);
                pts.Add(new Vector3(ax, y, bEntry.z));
            }
            else
            {
                // 빈 곳 → 통로: 빈 곳에서 통로 입구로 옆걸음 후 통로를 따라
                float bx = NearestAisle(bEntry.x);
                pts.Add(new Vector3(bx, y, a.z));
            }
            pts.Add(bEntry);
            if (bBlock) pts.Add(b);

            // 붙은 점·출발점과 같은 점 정리
            var clean = new List<Vector3>(pts.Count);
            Vector3 prev = new Vector3(from.x, y, from.z);
            foreach (var p in pts)
            {
                if ((p - prev).sqrMagnitude < 0.01f) continue;
                clean.Add(p);
                prev = p;
            }
            return clean;
        }
    }
}
