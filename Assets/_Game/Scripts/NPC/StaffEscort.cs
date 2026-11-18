using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UndercoverLive
{
    /// <summary>
    /// 관리자 한 명을 실제로 걷게 한다: 통로를 따라 플레이어 옆까지 갔다가 원래 자리로 돌아온다.
    /// 걷는 동안 걷기 동작(NpcClips.Walk)을 서 있기 동작과 PlayableGraph로 섞는다(애니메이터 컨트롤러 에셋은 그대로).
    /// 걷기 동작이 없으면 위치만 옮기며 걷는 듯 상하로 흔든다. 걷는 동안 LeaderGaze(좌우 둘러보기)는 멈춘다.
    /// GameEvents.IsPlaying이 아니면 멈춘다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StaffEscort : MonoBehaviour
    {
        [Tooltip("걷는 속도(m/s)")] public float walkSpeed = 1.25f;
        [Tooltip("몸 돌리는 속도(도/초)")] public float turnSpeed = 320f;
        [Tooltip("걷기 동작 재생 배율 (걷는 속도에 맞춤)")] public float animSpeed = 1f;
        [Tooltip("서기↔걷기 섞는 시간")] public float blendTime = 0.25f;
        [Tooltip("목표가 움직이면 길을 다시 계산하는 간격")] public float replanInterval = 0.5f;
        [Tooltip("걷기 동작이 없을 때 상하 흔들림(m)")] public float bobHeight = 0.03f;

        [NonSerialized] public ChapelLayout layout = new ChapelLayout();

        public bool Moving { get; private set; }
        public bool Arrived { get; private set; }
        /// <summary>원래 자리에서 멀어져 있음(돌아오는 중 포함).</summary>
        public bool Away { get; private set; }
        public float SpeedMultiplier { get; set; } = 1f;
        public Watcher Watcher { get; private set; }
        public HeadLook Head { get; private set; }

        Vector3 postPos;
        Quaternion postRot;
        LeaderGaze gaze;
        Animator anim;
        Transform model;
        Vector3 modelBase;

        List<Vector3> path = new List<Vector3>();
        int idx;
        Func<Vector3> pathGoal, stopRef;
        float stopDistance;
        bool returning;
        float replanTimer;
        Vector3 lastGoal;
        Func<Vector3> faceRef;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable walkPlayable;
        AnimatorControllerPlayable ctrlPlayable;
        float walkWeight, bobPhase;

        void Awake()
        {
            postPos = transform.position;
            postRot = transform.rotation;
            gaze = GetComponent<LeaderGaze>();
            Watcher = GetComponent<Watcher>();
            anim = GetComponentInChildren<Animator>(true);
            if (anim != null) { model = anim.transform; modelBase = model.localPosition; }
            Head = HeadLook.For(transform);
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        /// <summary>
        /// 목표로 걸어간다. pathGoal = 길 계산 목표점, stopReference = 이 점과 수평 거리 stop 이하가 되면 멈춤.
        /// 목표가 움직이면 길을 다시 계산한다. speed = 걷기 속도 배율.
        /// </summary>
        public void GoTo(Func<Vector3> pathGoal, Func<Vector3> stopReference, float stop, float speed = 1f)
        {
            this.pathGoal = pathGoal;
            stopRef = stopReference ?? pathGoal;
            faceRef = stopRef;
            stopDistance = stop;
            SpeedMultiplier = speed;
            returning = false;
            Arrived = false;
            Away = true;
            BeginMove();
            Plan(pathGoal());
            if (Flat(transform.position - stopRef()).magnitude <= stopDistance) Arrive();
        }

        /// <summary>원래 자리로 걸어 돌아간다. 도착하면 원래 방향으로 돌고 둘러보기 재개.</summary>
        public void ReturnToPost()
        {
            if (!Away) return;
            pathGoal = null; stopRef = null; faceRef = null;
            returning = true;
            Arrived = false;
            SpeedMultiplier = 1f;
            BeginMove();
            Plan(postPos);
        }

        /// <summary>도착해 서 있는 동안 이 점을 바라본다.</summary>
        public void Face(Func<Vector3> point) { faceRef = point; }

        /// <summary>머리(뼈)로 플레이어를 볼지.</summary>
        public void LookAtPlayer(bool on) { if (Head != null) Head.Set(HeadLook.Escort, on); }

        void BeginMove()
        {
            Moving = true;
            arrivingRot = false;
            if (gaze != null) gaze.suspended = true;
        }

        void Plan(Vector3 goal)
        {
            path = layout.Plan(transform.position, goal);
            idx = 0;
            lastGoal = goal;
            replanTimer = replanInterval;
        }

        void Arrive()
        {
            Moving = false;
            Arrived = true;
        }

        void ArrivePost()
        {
            Moving = false;
            returning = false;
            Away = false;
            transform.position = postPos;
            faceRef = null;
            arrivingRot = true;
        }

        bool arrivingRot;

        void Update()
        {
            float dt = GameEvents.IsPlaying ? Time.deltaTime : 0f;
            UpdateAnim(dt);
            if (dt <= 0f) return;

            if (Moving) Step(dt);
            else if (arrivingRot)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, postRot, turnSpeed * dt);
                if (Quaternion.Angle(transform.rotation, postRot) < 0.5f)
                {
                    transform.rotation = postRot;
                    arrivingRot = false;
                    if (gaze != null) { gaze.ResetYaw(); gaze.suspended = false; }
                }
            }
            else if (faceRef != null) TurnToward(faceRef() - transform.position, dt);
        }

        void Step(float dt)
        {
            if (!returning && pathGoal != null)
            {
                if (Flat(transform.position - stopRef()).magnitude <= stopDistance) { Arrive(); return; }
                replanTimer -= dt;
                Vector3 g = pathGoal();
                if (replanTimer <= 0f)
                {
                    replanTimer = replanInterval;
                    if ((g - lastGoal).sqrMagnitude > 0.16f) Plan(g);
                }
            }
            if (idx >= path.Count)
            {
                if (returning) { ArrivePost(); return; }
                Vector3 g = pathGoal != null ? pathGoal() : transform.position;
                if (Flat(transform.position - g).magnitude < 0.3f) { Arrive(); return; }
                Plan(g);
                if (path.Count == 0) { Arrive(); return; }
            }
            Vector3 wp = path[idx];
            Vector3 d = Flat(wp - transform.position);
            float stepLen = walkSpeed * SpeedMultiplier * dt;
            Vector3 pos = transform.position;
            if (d.magnitude <= stepLen)
            {
                pos.x = wp.x; pos.z = wp.z;
                idx++;
            }
            else pos += d.normalized * stepLen;
            transform.position = pos;
            if (d.sqrMagnitude > 1e-6f) TurnToward(d, dt);
        }

        void TurnToward(Vector3 dir, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            var want = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ---------- 걷기 동작 ----------

        void UpdateAnim(float dt)
        {
            float target = Moving ? 1f : 0f;
            if (dt > 0f) walkWeight = Mathf.MoveTowards(walkWeight, target, dt / Mathf.Max(0.05f, blendTime));

            bool wantGraph = Moving || walkWeight > 0f;
            if (wantGraph && !graph.IsValid()) TryCreateGraph();
            if (graph.IsValid())
            {
                float ease = walkWeight * walkWeight * (3f - 2f * walkWeight);
                mixer.SetInputWeight(0, 1f - ease);
                mixer.SetInputWeight(1, ease);
                // 멈춤 화면이면 동작도 멈춘다
                ctrlPlayable.SetSpeed(dt > 0f ? 1f : 0f);
                walkPlayable.SetSpeed(dt > 0f ? animSpeed * Mathf.Max(0.5f, SpeedMultiplier) : 0f);
                if (!wantGraph) { graph.Destroy(); }   // 다시 원래 애니메이터 컨트롤러로
            }
            else if (model != null)
            {
                // 걷기 동작이 없으면: 위치만 옮기며 걷는 듯 상하로
                if (dt > 0f) bobPhase += dt * walkSpeed * SpeedMultiplier * 5.2f;
                float bob = Mathf.Abs(Mathf.Sin(bobPhase)) * bobHeight * walkWeight;
                model.localPosition = modelBase + Vector3.up * bob;
            }
        }

        void TryCreateGraph()
        {
            var clip = NpcClips.Walk;
            if (clip == null || anim == null || anim.runtimeAnimatorController == null) return;
            graph = PlayableGraph.Create(name + ".Walk");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Anim", anim);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            ctrlPlayable = AnimatorControllerPlayable.Create(graph, anim.runtimeAnimatorController);
            walkPlayable = AnimationClipPlayable.Create(graph, clip);
            graph.Connect(ctrlPlayable, 0, mixer, 0);
            graph.Connect(walkPlayable, 0, mixer, 1);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);
            output.SetSourcePlayable(mixer);
            graph.Play();
            if (model != null) model.localPosition = modelBase;
        }
    }
}
