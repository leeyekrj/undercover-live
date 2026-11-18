using System.Collections.Generic;
using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 폰 카메라에만 찍히는 기이현상 1~8을 런타임에 만들고 의례 시각에 맞춰 켜고 끈다.
    /// (9 거대 겹눈·10 역관측은 EyeEntity)
    /// 현상마다 찍을 대상(CaptureTarget)을 붙이고, 오래 비추면 반응한다(쳐다봄·찢어짐·움찔함).
    /// 폰에만 보일 것은 전부 PhoneOnly 레이어. 맨눈 쪽 변화(달성판 숫자·명단·등불 깜빡임)는 기본 레이어.
    /// </summary>
    public class Phenomena : MonoBehaviour
    {
        public static Phenomena Instance { get; private set; }

        ServiceDirector dir;
        RitualCrowd crowd;
        Transform rootT;
        int P;

        // 공용 머티리얼
        Material mFleshGlow, mPaleBall, mShadow, mThread, mKneel, mFloor, mEyePale, mEyeRedSmall, mGrainGold, mGrainWhite, mRing;

        public void Init(ServiceDirector d, RitualCrowd c)
        {
            Instance = this;
            dir = d; crowd = c;
            P = EntityKit.PhoneOnly;
            rootT = EntityKit.Group("Phenomena_PhoneOnly", null, P).transform;

            var flesh = EyeTexture.Flesh();
            mFleshGlow = EntityKit.Glow(new Color(0.24f, 0.17f, 0.17f), new Color(0.2f, 0.03f, 0.025f), flesh, flesh);
            mPaleBall = EntityKit.Opaque(new Color(0.86f, 0.80f, 0.70f), null, 0.55f);
            mShadow = EntityKit.Clear(new Color(0.01f, 0f, 0.012f, 0.84f));
            mThread = EntityKit.Clear(new Color(0.86f, 0.84f, 0.80f, 0.42f));
            mKneel = EntityKit.Clear(new Color(0.24f, 0.08f, 0.32f, 0.74f));
            mFloor = EntityKit.Clear(new Color(0f, 0f, 0f, 0.86f));
            mEyePale = EyeMat(EyeStyle.Pale, new Color(0.9f, 0.86f, 0.8f));
            mEyeRedSmall = EyeMat(EyeStyle.Red, new Color(2.0f, 0.45f, 0.35f));
            mGrainGold = EntityKit.Glow(Color.black, new Color(3.2f, 2.3f, 0.7f));
            mGrainWhite = EntityKit.Glow(Color.black, new Color(2.6f, 2.6f, 2.9f));
            mRing = EntityKit.Glow(new Color(0.8f, 0.6f, 0.25f), new Color(2.6f, 1.8f, 0.6f), null, null, true);

            BuildPortrait();
            BuildBanner();
            BuildCeiling();
            BuildThreads();
            BuildShadow();
            BuildDonation();
            BuildFloor();
            BuildKneel();
        }

        void OnDestroy() { if (Instance == this) Instance = null; if (rootT != null) Destroy(rootT.gameObject); }

        public static Material EyeMat(EyeStyle s, Color emission)
        {
            var tex = EyeTexture.Get(s);
            return EntityKit.Glow(Color.white, emission, tex, tex, true);
        }

        static void SetActive(GameObject go, bool on) { if (go != null && go.activeSelf != on) go.SetActive(on); }

        CaptureTarget MakeTarget(GameObject host, string specId, string id, List<Renderer> rs)
        {
            var t = host.AddComponent<CaptureTarget>();
            RitualScript.Apply(t, specId, id);
            t.renderers = rs.ToArray();
            t.activeNow = false;
            return t;
        }

        static void Collect(List<Renderer> list, Transform t) { if (t != null) list.AddRange(t.GetComponentsInChildren<Renderer>(true)); }

        static Quaternion Face(Vector3 from, Vector3 to, Quaternion fallback)
        {
            var d = to - from;
            return d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d) : fallback;
        }

        static float Damp(float rate) => 1f - Mathf.Exp(-rate * Time.deltaTime);

        void Update()
        {
            if (dir == null) return;
            float t = dir.RitualTime;
            TickPortrait(t);
            TickBanner(t);
            TickCeiling(t);
            TickThreads(t);
            TickShadow(t);
            TickDonation(t);
            TickFloor(t);
            TickKneel(t);
        }

        // =====================================================================
        // 1. 초상화의 두 고리와 시선 추적
        // =====================================================================
        GameObject pRoot; Transform[] pEyes; Transform[] pRings; Material pIris; CaptureTarget pTarget;
        readonly Aim pAim = new Aim(); bool pReacted; float pIdle;
        static readonly Vector3 PortraitCenter = new Vector3(0f, 4.32f, 13.80f);

        void BuildPortrait()
        {
            pRoot = EntityKit.Group("P1_Portrait", rootT, P);
            pIris = EyeMat(EyeStyle.Gold, new Color(1.7f, 1.35f, 0.9f));
            var rs = new List<Renderer>();
            pEyes = new Transform[2];
            float[] xs = { -0.18f, 0.172f };
            for (int i = 0; i < 2; i++)
            {
                pEyes[i] = EntityKit.EyeBall("Eye" + i, pRoot.transform, 0.12f, mPaleBall, pIris, P, 0.74f);
                pEyes[i].position = new Vector3(xs[i], 4.32f, 13.80f);
                pEyes[i].rotation = Quaternion.Euler(0, 180, 0);
                Collect(rs, pEyes[i]);
            }
            pRings = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                pRings[i] = EntityKit.Obj("Ring" + i, pRoot.transform, EntityKit.Ring(0.93f), mRing, P).transform;
                pRings[i].position = new Vector3(i == 0 ? -0.24f : 0.275f, 4.52f, 13.845f);
                pRings[i].localScale = Vector3.one * 1.27f;
                rs.Add(pRings[i].GetComponent<Renderer>());
            }
            // 초상화 액자·그림도 같이 재야 화면 높이가 맞는다 (기본 레이어라 폰에도 보임)
            var portrait = GameObject.Find("Chapel/Stage/Portrait");
            if (portrait != null) Collect(rs, portrait.transform);
            pTarget = MakeTarget(pRoot, "portrait_eyes", null, rs);
        }

        void TickPortrait(float t)
        {
            bool on = RitualScript.InWindow(RitualScript.WinPortrait, t);
            SetActive(pRoot, on);
            if (!on) return;
            pTarget.activeNow = true;
            float sep = 0.26f - 0.14f * (0.5f + 0.5f * Mathf.Sin(t * 0.7f));
            for (int i = 0; i < 2; i++)
                pRings[i].position = new Vector3(0.018f + (i == 0 ? -sep : sep), 4.52f + 0.02f * Mathf.Sin(t * 1.3f + i * 2f), 13.845f);
            Vector3 watch = EntityKit.WatchPoint();
            float pop = 0.03f + 0.035f * (0.5f + 0.5f * Mathf.Sin(t * 0.9f));
            float[] xs = { -0.18f, 0.172f };
            for (int i = 0; i < 2; i++)
            {
                pEyes[i].position = new Vector3(xs[i], 4.32f, 13.80f - pop);
                pEyes[i].rotation = Quaternion.Slerp(pEyes[i].rotation, Face(pEyes[i].position, watch, pEyes[i].rotation), Damp(2.5f));
            }
            // 2초 넘게 정조준하면 동공이 세로로 찢어지듯 좁아지고 폰 화면에 치지직
            float held = pAim.Track(PortraitCenter, 0.22f);
            if (held >= 2f && !pReacted)
            {
                pReacted = true;
                var narrow = EyeTexture.Get(EyeStyle.GoldNarrow);
                pIris.SetTexture("_BaseMap", narrow); pIris.SetTexture("_EmissionMap", narrow);
                EntityStareFX.Glitch(0.2f);
            }
            if (held <= 0f) pIdle += Time.deltaTime; else pIdle = 0f;
            if (pReacted && pIdle > 2.5f)
            {
                pReacted = false;
                var gold = EyeTexture.Get(EyeStyle.Gold);
                pIris.SetTexture("_BaseMap", gold); pIris.SetTexture("_EmissionMap", gold);
            }
        }

        // =====================================================================
        // 2. 현수막 본문 변조와 글자 사이 작은 눈알들
        // =====================================================================
        GameObject bRoot; Transform[] bEyes; Vector3 bCenter; readonly Aim bAim = new Aim(); bool bReacting;

        void BuildBanner()
        {
            bRoot = EntityKit.Group("P2_BannerEyes", rootT, P);
            var text = GameObject.Find("BannerText_Phone");
            Bounds bb = new Bounds(new Vector3(0, 6.3f, 13.89f), new Vector3(4.6f, 0.36f, 0.01f));
            Renderer textR = text != null ? text.GetComponent<Renderer>() : null;
            if (textR != null && textR.bounds.size.x > 0.5f) bb = textR.bounds;
            bCenter = bb.center;
            const int perRow = 6;
            bEyes = new Transform[perRow * 2];
            var rs = new List<Renderer>();
            if (textR != null) rs.Add(textR);
            for (int i = 0; i < bEyes.Length; i++)
            {
                int row = i / perRow, k = i % perRow;
                float x = Mathf.Lerp(bb.min.x + 0.25f, bb.max.x - 0.25f, (k + (row == 0 ? 0.25f : 0.75f)) / perRow);
                float y = row == 0 ? bb.max.y + 0.11f : bb.min.y - 0.11f;
                bEyes[i] = EntityKit.FlatEye("Eye" + i, bRoot.transform, 0.15f, mEyePale, P);
                bEyes[i].position = new Vector3(x, y, bb.center.z - 0.03f);
                bEyes[i].rotation = Quaternion.Euler(0, 180, 0);
                Collect(rs, bEyes[i]);
            }
            // 기존 현수막 대상(속뜻 글씨)에 눈알을 더하고 점수표대로
            CaptureTarget bt = text != null ? text.GetComponent<CaptureTarget>() : null;
            if (bt != null) { RitualScript.Apply(bt, "banner"); bt.renderers = rs.ToArray(); bt.activeNow = true; }
        }

        void TickBanner(float t)
        {
            bool on = RitualScript.InWindow(RitualScript.WinBanner, t);
            SetActive(bRoot, on);
            if (!on) return;
            float held = bAim.Track(bCenter, 0.28f, 0.2f);
            bool react = held >= 0.8f || t >= RitualScript.Phases[5].start;   // 폐회 땐 전부 렌즈를 본다
            if (react && !bReacting && held >= 0.8f) EntityStareFX.ShakePhone(0.25f, 0.18f, false);
            bReacting = react;
            Vector3 watch = EntityKit.WatchPoint();
            for (int i = 0; i < bEyes.Length; i++)
            {
                var e = bEyes[i];
                float ph = Mathf.Repeat(t * 0.42f + i * 0.012f, 1f);   // 거의 일제히 깜빡
                float open = 1f - 0.9f * Mathf.Exp(-Mathf.Pow((ph - 0.05f) / 0.028f, 2f));
                e.localScale = new Vector3(0.15f, 0.15f * open, 0.15f);
                Quaternion goal = react ? Face(e.position, watch, e.rotation)
                                        : Quaternion.Euler(6f * Mathf.Sin(t * 0.5f + i), 180f + 14f * Mathf.Sin(t * 0.37f + i * 1.3f), 0f);
                e.rotation = Quaternion.Slerp(e.rotation, goal, Damp(react ? 9f : 2f));
            }
        }

        // =====================================================================
        // 3. 천장 구석의 적외선 겹눈 감시체 (옛 감시 카메라 자리)
        // =====================================================================
        class Cluster
        {
            public GameObject root; public Transform ball; public Transform[] smalls; public Tentacle stalk;
            public Material iris; public Watcher watcher; public CaptureTarget target; public Aim aim = new Aim();
            public float glow = 1f; public int index;
        }
        readonly List<Cluster> clusters = new List<Cluster>();
        Light[] sconces; float[] sconceBase; float nextFlicker; int flickerIdx = -1; float flickerEnd;
        static readonly Vector3 AudienceCenter = new Vector3(0f, 1.2f, -1f);

        void BuildCeiling()
        {
            var spots = new List<KeyValuePair<Vector3, Watcher>>();
            var cctv = GameObject.Find("CCTV");
            if (cctv != null)
                foreach (Transform c in cctv.transform)
                {
                    // 옛 카메라 모형과 적외선 불빛은 숨기고, 그 대상도 끈다
                    foreach (var r in c.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    foreach (var ct in c.GetComponents<CaptureTarget>()) { ct.activeNow = false; ct.enabled = false; }
                    spots.Add(new KeyValuePair<Vector3, Watcher>(c.position, c.GetComponent<Watcher>()));
                }
            if (spots.Count < 4) spots.Add(new KeyValuePair<Vector3, Watcher>(new Vector3(-7.6f, 6.6f, -13.4f), null));

            for (int k = 0; k < spots.Count; k++)
            {
                var cl = new Cluster { index = k, watcher = spots[k].Value };
                Vector3 p = spots[k].Key;
                p = new Vector3(p.x * 0.97f, 6.45f, p.z * 0.98f);
                cl.root = EntityKit.Group("P3_CeilingEyes" + k, rootT, P);
                cl.iris = EyeMat(EyeStyle.Red, new Color(2.4f, 0.4f, 0.3f));
                cl.ball = EntityKit.EyeBall("Ball", cl.root.transform, 0.34f, mFleshGlow, cl.iris, P, 0.78f);
                cl.ball.position = p;
                cl.ball.rotation = Face(p, AudienceCenter, Quaternion.identity);
                cl.stalk = Tentacle.Create("Stalk", cl.root.transform, 3, mFleshGlow, P);
                cl.stalk.basePos = new Vector3(p.x, 6.99f, p.z);
                cl.stalk.tipPos = p + Vector3.up * 0.12f;
                cl.stalk.rootRadius = 0.1f; cl.stalk.tipRadius = 0.07f; cl.stalk.sag = 0f; cl.stalk.waveAmp = 0.05f; cl.stalk.freeTip = false;
                cl.smalls = new Transform[2];
                Vector3[] offs = { new Vector3(0.17f, 0.10f, 0f), new Vector3(-0.15f, 0.04f, 0.08f) };
                for (int i = 0; i < 2; i++)
                {
                    cl.smalls[i] = EntityKit.FlatEye("Small" + i, cl.root.transform, 0.11f, mEyeRedSmall, P);
                    cl.smalls[i].position = p + offs[i];
                }
                var rs = new List<Renderer>();
                Collect(rs, cl.root.transform);
                cl.target = MakeTarget(cl.root, "ceiling_eyes", "ceiling_eyes_" + k, rs);
                clusters.Add(cl);
            }

            var lights = GameObject.Find("Chapel/Lights");
            var list = new List<Light>();
            if (lights != null) foreach (Transform l in lights.transform) if (l.name.StartsWith("Sconce")) { var lt = l.GetComponent<Light>(); if (lt != null) list.Add(lt); }
            sconces = list.ToArray();
            sconceBase = new float[sconces.Length];
            for (int i = 0; i < sconces.Length; i++) sconceBase[i] = sconces[i].intensity;
        }

        void TickCeiling(float t)
        {
            bool on = RitualScript.InWindow(RitualScript.WinCeiling, t);
            Vector3 watch = EntityKit.WatchPoint();
            bool closing = t >= RitualScript.Phases[5].start;
            float dt = Time.deltaTime;
            foreach (var cl in clusters)
            {
                SetActive(cl.root, on);
                if (!on) continue;
                cl.target.activeNow = true;
                Vector3 p = cl.ball.position;
                float held = cl.aim.Track(p, 0.16f);
                bool react = held >= 0.5f;
                Quaternion goal;
                if (react || closing) goal = Face(p, watch, cl.ball.rotation);
                else goal = Quaternion.AngleAxis(35f * Mathf.Sin(t * 0.8f + cl.index * 1.7f), Vector3.up) * Face(p, AudienceCenter, cl.ball.rotation);
                cl.ball.rotation = Quaternion.Slerp(cl.ball.rotation, goal, Damp(react ? 12f : 1.8f));
                for (int i = 0; i < cl.smalls.Length; i++)
                    cl.smalls[i].rotation = Quaternion.Slerp(cl.smalls[i].rotation, react || closing ? Face(cl.smalls[i].position, watch, cl.smalls[i].rotation) : cl.ball.rotation, Damp(6f));
                // 마주치면 붉은 빛이 3배
                float g = react ? 3f : 1f;
                if (!Mathf.Approximately(cl.glow, g))
                {
                    cl.glow = Mathf.MoveTowards(cl.glow, g, 10f * dt);
                    EntityKit.SetEmission(cl.iris, new Color(2.4f, 0.4f, 0.3f) * cl.glow);
                }
                // 감시체와 눈이 마주친 동안 의심이 빠르게 찬다 (초당 약 +60)
                if (react && cl.watcher != null && GameEvents.IsPlaying)
                    cl.watcher.suspicion = Mathf.Min(100f, cl.watcher.suspicion + (cl.watcher.Visible ? 30f : 60f) * dt);
            }

            // 맨눈 쪽: 벽 등불이 이유 없이 깜빡인다
            if (sconces.Length == 0) return;
            if (flickerIdx >= 0 && Time.time >= flickerEnd) { sconces[flickerIdx].intensity = sconceBase[flickerIdx]; flickerIdx = -1; }
            if (on && dir.Started && Time.time >= nextFlicker && flickerIdx < 0)
            {
                flickerIdx = Random.Range(0, sconces.Length);
                sconces[flickerIdx].intensity = sconceBase[flickerIdx] * 0.3f;
                flickerEnd = Time.time + Random.Range(0.06f, 0.14f);
                nextFlicker = Time.time + Random.Range(1f, 2.2f);
            }
        }

        // =====================================================================
        // 4. 동시 고개 꺾기와 정수리 신경 줄기 (고개 꺾기는 RitualCrowd)
        // =====================================================================
        GameObject tRoot; readonly List<Tentacle> threads = new List<Tentacle>(); readonly List<RitualCrowd.Member> threadOwners = new List<RitualCrowd.Member>();
        readonly List<Aim> threadAims = new List<Aim>(); CaptureTarget tTarget;

        void BuildThreads()
        {
            tRoot = EntityKit.Group("P4_Threads", rootT, P);
            var rs = new List<Renderer>();
            if (crowd != null)
            {
                var rnd = new System.Random(41);
                foreach (var m in crowd.FrontRows)
                {
                    if (threads.Count >= 10) break;
                    if (m.head == null) continue;
                    var th = Tentacle.Create("Thread" + threads.Count, tRoot.transform, 2, mThread, P);
                    Vector3 h = m.head.position;
                    th.basePos = new Vector3(h.x + (float)(rnd.NextDouble() - 0.5) * 0.6f, 6.99f, h.z + (float)(rnd.NextDouble() - 0.5) * 0.6f);
                    th.tipFollow = m.head; th.tipOffset = Vector3.up * 0.13f;
                    th.rootRadius = 0.028f; th.tipRadius = 0.02f; th.sag = 0f; th.waveAmp = 0.14f; th.waveSpeed = 2f; th.freeTip = false;
                    rs.AddRange(th.Renderers);
                    threads.Add(th); threadOwners.Add(m); threadAims.Add(new Aim());
                }
            }
            tTarget = MakeTarget(tRoot, "threads", null, rs);
        }

        void TickThreads(float t)
        {
            bool on = RitualScript.InWindow(RitualScript.WinThreads, t) && threads.Count > 0;
            SetActive(tRoot, on);
            if (!on) return;
            tTarget.activeNow = true;
            bool taut = crowd != null && crowd.JerkTaut;
            for (int i = 0; i < threads.Count; i++)
            {
                var th = threads[i];
                th.tension = Mathf.MoveTowards(th.tension, taut ? 1f : 0f, Time.deltaTime * (taut ? 8f : 2f));
                var m = threadOwners[i];
                // 한 사람의 줄기를 2초 넘게 비추면 그 사람이 고개만 돌려 쳐다본다
                if (m.head != null && threadAims[i].Track(m.head.position + Vector3.up * 0.9f, 0.12f) >= 2f && !m.Staring)
                    crowd.Stare(m, 5f, 35f);
            }
        }

        // =====================================================================
        // 6. 눈동자님 등 뒤에서 부풀어 오르는 다중 눈 그림자
        // =====================================================================
        GameObject sRoot; Transform[] sEyes; Material sIris; Tentacle[] sTents; float[] sAngles; CaptureTarget sTarget;
        readonly Aim sAim = new Aim(); bool sFocused;
        static readonly Vector3 ShadowCenter = new Vector3(0f, 3.7f, 13.68f);

        void BuildShadow()
        {
            sRoot = EntityKit.Group("P6_Shadow", rootT, P);
            var rs = new List<Renderer>();
            var body = EntityKit.Obj("Body", sRoot.transform, EntityKit.Prim(PrimitiveType.Sphere), mShadow, P).transform;
            body.position = ShadowCenter; body.localScale = new Vector3(5.6f, 4.4f, 0.3f);
            rs.Add(body.GetComponent<Renderer>());
            sTents = new Tentacle[8]; sAngles = new float[8];
            for (int i = 0; i < 8; i++)
            {
                sAngles[i] = Mathf.Lerp(-15f, 195f, i / 7f) * Mathf.Deg2Rad;
                var tn = Tentacle.Create("Arm" + i, sRoot.transform, 3, mShadow, P);
                tn.rootRadius = 0.24f; tn.tipRadius = 0.05f; tn.flatten = 0.25f; tn.flatUp = Vector3.forward;
                tn.sag = 0f; tn.waveAmp = 0.35f; tn.waveSpeed = 1.1f; tn.freeTip = true;
                rs.AddRange(tn.Renderers);
                sTents[i] = tn;
            }
            sIris = EyeMat(EyeStyle.Gold, new Color(2.0f, 1.5f, 0.6f));
            sEyes = new Transform[7];
            for (int i = 0; i < 7; i++)
            {
                sEyes[i] = EntityKit.FlatEye("Eye" + i, sRoot.transform, 0.34f, sIris, P);
                sEyes[i].position = new Vector3(0.02f * Mathf.Sin(i * 2.1f), 3.0f + i * 0.43f, 13.5f);
                sEyes[i].rotation = Quaternion.Euler(0, 180, 0);
                Collect(rs, sEyes[i]);
            }
            sTarget = MakeTarget(sRoot, "shadow", null, rs);
        }

        Vector3 LeaderHead()
        {
            if (crowd != null && crowd.Leader != null && crowd.Leader.head != null) return crowd.Leader.head.position;
            return new Vector3(0f, 2.65f, 12.6f);
        }

        void TickShadow(float t)
        {
            bool on = RitualScript.InWindow(RitualScript.WinShadow, t);
            SetActive(sRoot, on);
            if (!on) return;
            sTarget.activeNow = true;
            for (int i = 0; i < sTents.Length; i++)
            {
                float a = sAngles[i] + 0.2f * Mathf.Sin(t * 0.9f + i);
                Vector3 dirA = new Vector3(Mathf.Cos(sAngles[i]), Mathf.Sin(sAngles[i]) * 0.85f, 0f);
                Vector3 dirB = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.85f, 0f);
                sTents[i].basePos = ShadowCenter + dirA * 1.6f + Vector3.back * 0.02f;
                Vector3 tip = ShadowCenter + dirB * (3.9f + 0.4f * Mathf.Sin(t * 0.6f + i * 1.7f));
                tip.x = Mathf.Clamp(tip.x, -7.6f, 7.6f); tip.y = Mathf.Clamp(tip.y, 0.9f, 6.9f); tip.z = ShadowCenter.z - 0.02f;
                sTents[i].tipPos = tip;
            }
            float held = sAim.Track(LeaderHead(), 0.2f);
            bool focus = held >= 2.5f;
            if (focus != sFocused)
            {
                sFocused = focus;
                var tex = EyeTexture.Get(focus ? EyeStyle.GoldNarrow : EyeStyle.Gold);
                sIris.SetTexture("_BaseMap", tex); sIris.SetTexture("_EmissionMap", tex);
                EntityKit.SetEmission(sIris, focus ? new Color(3.2f, 2.3f, 0.9f) : new Color(2.0f, 1.5f, 0.6f));
            }
            Vector3 watch = EntityKit.WatchPoint();
            float open = 0.75f + 0.25f * Mathf.Sin(t * 0.8f);
            for (int i = 0; i < sEyes.Length; i++)
            {
                var e = sEyes[i];
                e.localScale = new Vector3(0.34f, 0.34f * open, 0.34f);
                Quaternion goal = focus ? Face(e.position, watch, e.rotation) : Quaternion.Euler(0f, 180f + 10f * Mathf.Sin(t * 0.4f + i), 0f);
                e.rotation = Quaternion.Slerp(e.rotation, goal, Damp(focus ? 10f : 2f));
            }
        }

        // =====================================================================
        // 5. 바깥 짐 비움 달성판을 감싼 맥동 촉수와 빛 흡수 (+ 맨눈: 87→92%, 미납자 명단)
        // =====================================================================
        Transform board, barFill; TextMesh pctText; GameObject namesGo;
        GameObject dRoot; Tentacle[] dTents; Transform[] dEyes; Transform[] dGrains; CaptureTarget dTarget; readonly Aim dAim = new Aim();
        int shownPct = -1;
        static readonly Vector3[] DBase = { new Vector3(-1.55f, 0.55f, -0.1f), new Vector3(1.55f, -0.5f, -0.1f), new Vector3(0.25f, 0.76f, -0.1f) };
        static readonly Vector3[] DTip = { new Vector3(-0.75f, 0.22f, 0.24f), new Vector3(0.9f, -0.18f, 0.24f), new Vector3(0.35f, 0.12f, 0.27f) };

        void BuildDonation()
        {
            var db = GameObject.Find("DonationBoard");
            board = db != null ? db.transform : null;
            dRoot = EntityKit.Group("P5_Donation", rootT, P);
            var rs = new List<Renderer>();
            if (board != null)
            {
                barFill = board.Find("BarFill");
                var pct = board.Find("Pct");
                pctText = pct != null ? pct.GetComponent<TextMesh>() : null;
                var title = board.Find("Title");
                if (title != null)
                {
                    // 미납자 명단 (맨눈에도 보인다 — 보도 가치가 있는 물증)
                    namesGo = Instantiate(title.gameObject, board);
                    namesGo.name = "UnpaidNames";
                    namesGo.transform.localPosition = new Vector3(0f, -0.52f, title.localPosition.z);
                    var tm = namesGo.GetComponent<TextMesh>();
                    tm.text = RitualScript.DonationNames;
                    tm.characterSize = 0.0155f;
                    tm.color = new Color(1f, 0.62f, 0.5f);
                    namesGo.SetActive(false);
                }
                foreach (var n in new[] { "Board", "BarFill", "Title" }) { var c = board.Find(n); if (c != null) { var r = c.GetComponent<Renderer>(); if (r != null) rs.Add(r); } }
                if (namesGo != null) rs.Add(namesGo.GetComponent<Renderer>());
            }
            dTents = new Tentacle[3]; dEyes = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var tn = Tentacle.Create("Arm" + i, dRoot.transform, 6, mFleshGlow, P);
                tn.rootRadius = 0.13f; tn.tipRadius = 0.035f; tn.sag = 0.15f; tn.sagDir = Vector3.left; tn.waveAmp = 0.12f; tn.waveSpeed = 3f; tn.freeTip = true;
                rs.AddRange(tn.Renderers);
                dTents[i] = tn;
                dEyes[i] = EntityKit.FlatEye("TipEye" + i, dRoot.transform, 0.11f, mEyeRedSmall, P);
                Collect(rs, dEyes[i]);
            }
            dGrains = new Transform[5];
            for (int i = 0; i < dGrains.Length; i++)
            {
                dGrains[i] = EntityKit.Obj("Grain" + i, dRoot.transform, EntityKit.Prim(PrimitiveType.Sphere), mGrainGold, P).transform;
                dGrains[i].localScale = Vector3.one * 0.07f;
            }
            dTarget = MakeTarget(dRoot, "donation_board", null, rs);
        }

        void TickDonation(float t)
        {
            // 맨눈·폰 공통: 명단과 달성률
            bool listOn = dir.Started && t >= RitualScript.Phases[2].start && t < RitualScript.WinDonation[1];
            SetActive(namesGo, listOn);
            int pct = dir.Started && t >= 150f ? 87 + Mathf.RoundToInt(5f * Mathf.Clamp01((t - 150f) / 50f)) : 87;
            if (pct != shownPct && barFill != null)
            {
                shownPct = pct;
                float w = 2.5f * pct / 100f;
                barFill.localScale = new Vector3(w, barFill.localScale.y, barFill.localScale.z);
                barFill.localPosition = new Vector3(1.25f - w * 0.5f, barFill.localPosition.y, barFill.localPosition.z);
                if (pctText != null) pctText.text = pct + "%";
            }

            bool on = board != null && RitualScript.InWindow(RitualScript.WinDonation, t);
            SetActive(dRoot, on);
            if (!on) return;
            dTarget.activeNow = true;
            Vector3 front = board.TransformDirection(Vector3.forward);
            float held = dAim.Track(board.position, 0.28f, 0.15f);
            bool react = held >= 1f;
            for (int i = 0; i < 3; i++)
            {
                var tn = dTents[i];
                tn.basePos = board.TransformPoint(DBase[i]);
                Vector3 wig = new Vector3(0.12f * Mathf.Sin(t * 1.7f + i * 2f), 0.08f * Mathf.Sin(t * 2.3f + i), 0.04f * Mathf.Sin(t * 3.1f + i));
                tn.tipPos = board.TransformPoint(DTip[i] + wig);
                var e = dEyes[i];
                Vector3 jit = react ? Random.insideUnitSphere * 0.025f : Vector3.zero;
                e.position = tn.tipPos + front * 0.06f + jit;
                e.rotation = Quaternion.LookRotation(react ? (EntityKit.WatchPoint() - e.position) : front);
                float s = Mathf.MoveTowards(e.localScale.x, react ? 0.13f : 0.045f, Time.deltaTime * 0.5f);
                e.localScale = Vector3.one * s;
            }
            // 빛 알갱이: 달성판을 거쳐 천장 한가운데로
            Vector3 a = board.position + front * 0.3f, c = new Vector3(0f, 6.95f, 4f), m = new Vector3(3.5f, 6.2f, 6f);
            for (int i = 0; i < dGrains.Length; i++)
            {
                float s = Mathf.Repeat(t * 0.16f + i / (float)dGrains.Length, 1f);
                Vector3 p = Vector3.Lerp(Vector3.Lerp(a, m, s), Vector3.Lerp(m, c, s), s);
                dGrains[i].position = p;
                dGrains[i].localScale = Vector3.one * (0.07f * Mathf.Sin(s * Mathf.PI) + 0.01f);
            }
        }

        // =====================================================================
        // 7. 소등 카운트다운의 숫자 폭주와 통로 바닥을 기는 그림자 촉수
        // =====================================================================
        Renderer cdEyeR; GameObject cdPhone; TextMesh cdPhoneText; Vector3 cdPhoneBase; int cdLayer;
        GameObject fRoot; Tentacle[] fTents; Vector3[] fBase, fGoal; float fRetreatUntil; CaptureTarget fTarget; readonly Aim fAim = new Aim();
        float fReach = 1f; float cdNextFlicker; float cdFlickerEnd; bool cdWasOn;

        void BuildFloor()
        {
            var cd = GameObject.Find("Countdown");
            var txt = cd != null ? cd.transform.Find("Text") : null;
            if (txt != null)
            {
                cdEyeR = txt.GetComponent<Renderer>();
                cdLayer = txt.gameObject.layer;
                cdPhone = Instantiate(txt.gameObject, cd.transform);
                cdPhone.name = "Text_PhoneFrenzy";
                cdPhone.layer = P;
                cdPhoneText = cdPhone.GetComponent<TextMesh>();
                cdPhoneBase = cdPhone.transform.localPosition;
                cdPhone.SetActive(false);
            }
            fRoot = EntityKit.Group("P7_FloorTentacles", rootT, P);
            var pc = PhoneController.Instance;
            Vector3 seat = pc != null ? pc.transform.position : new Vector3(-2.8f, 0f, -1.68f);
            seat.y = 0f;
            float[] dz = { -2.4f, -0.9f, 0.6f, 2.1f };
            Vector3[] goal = { new Vector3(-0.5f, 0f, -0.35f), new Vector3(-0.3f, 0f, 0.2f), new Vector3(0.25f, 0f, -0.5f), new Vector3(0.45f, 0f, 0.3f) };
            fTents = new Tentacle[4]; fBase = new Vector3[4]; fGoal = new Vector3[4];
            var rs = new List<Renderer>();
            for (int i = 0; i < 4; i++)
            {
                fBase[i] = new Vector3(-7.9f, 0.04f, seat.z + dz[i]);
                fGoal[i] = seat + goal[i] + Vector3.up * 0.04f;
                var tn = Tentacle.Create("Crawl" + i, fRoot.transform, 5, mFloor, P);
                tn.rootRadius = 0.17f; tn.tipRadius = 0.04f; tn.flatten = 0.3f; tn.flatUp = Vector3.up;
                tn.sag = 0f; tn.waveAmp = 0.28f; tn.waveSpeed = 1.3f; tn.freeTip = true;
                tn.basePos = fBase[i]; tn.tipPos = fGoal[i];
                rs.AddRange(tn.Renderers);
                fTents[i] = tn;
            }
            fTarget = MakeTarget(fRoot, "floor_tentacles", null, rs);
        }

        void TickFloor(float t)
        {
            bool on = RitualScript.InWindow(RitualScript.WinFloor, t);
            // 카운트다운: 이 구간엔 맨눈 글씨(EyeOnly)와 폰 글씨(PhoneOnly)가 갈라진다
            if (cdEyeR != null)
            {
                if (on != cdWasOn)
                {
                    cdWasOn = on;
                    cdEyeR.gameObject.layer = on ? EntityKit.EyeOnly : cdLayer;
                    cdEyeR.enabled = true;
                    SetActive(cdPhone, on);
                }
                if (on)
                {
                    int idx = Mathf.FloorToInt((t - RitualScript.WinFloor[0]) * 2.5f) % RitualScript.CountdownFrenzy.Length;
                    if (cdPhoneText != null) cdPhoneText.text = string.Format(RitualScript.CountdownFormat, RitualScript.CountdownFrenzy[idx]);
                    cdPhone.transform.localPosition = cdPhoneBase + new Vector3(Random.Range(-0.02f, 0.02f), Random.Range(-0.015f, 0.015f), 0f);
                    // 맨눈: 가끔 전압이 떨어진 것처럼 파르르
                    if (Time.time >= cdNextFlicker) { cdFlickerEnd = Time.time + 0.25f; cdNextFlicker = Time.time + Random.Range(2.5f, 5f); }
                    cdEyeR.enabled = !(Time.time < cdFlickerEnd && Mathf.Repeat(Time.time * 22f, 1f) < 0.5f);
                }
            }

            SetActive(fRoot, on);
            if (!on) return;
            fTarget.activeNow = true;
            // 촉수 끝을 비추면 빛을 피하듯 움찔하며 의자 밑으로 숨고, 옆자리 눈꺼풀이 고개를 돌린다
            bool seen = false;
            for (int i = 0; i < fTents.Length; i++)
                if (EntityKit.FromCenter(fTents[i].tipPos) < 0.3f) { seen = true; break; }
            float held = fAim.Feed(seen);
            if (held >= 0.4f && Time.time > fRetreatUntil)
            {
                fRetreatUntil = Time.time + 3f;
                if (crowd != null) crowd.Stare(crowd.NearestToPlayer(), 4f, 30f);
            }
            float goalReach = Time.time < fRetreatUntil ? 0.3f : 1f;
            fReach = Mathf.MoveTowards(fReach, goalReach, Time.deltaTime * (goalReach < fReach ? 3f : 0.4f));
            for (int i = 0; i < fTents.Length; i++)
            {
                float k = (0.62f + 0.38f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / 6f + i * 1.3f))) * fReach;
                fTents[i].tipPos = Vector3.Lerp(fBase[i], fGoal[i], k);
            }
        }

        // =====================================================================
        // 8. 단상 앞 무릎 꿇은 눈꺼풀과 천장에서 내려온 흡착 촉수
        // =====================================================================
        GameObject kRoot; Tentacle kTent; Transform[] kEyes; Transform[] kGrains; Light kLight; CaptureTarget kTarget;
        readonly Aim kAim = new Aim(); float kReleaseUntil; Vector3 kTipFree;

        void BuildKneel()
        {
            kRoot = EntityKit.Group("P8_KneelTentacle", rootT, P);
            var rs = new List<Renderer>();
            kTent = Tentacle.Create("Tentacle", kRoot.transform, 10, mKneel, P);
            kTent.basePos = new Vector3(0f, 6.99f, 11.0f);
            kTent.rootRadius = 0.26f; kTent.tipRadius = 0.12f; kTent.sag = 0.5f; kTent.sagDir = Vector3.back;
            kTent.waveAmp = 0.22f; kTent.waveSpeed = 1.4f; kTent.freeTip = false;
            kTent.tipPos = RitualCrowd.KneelSpot + Vector3.up * 1.1f;
            rs.AddRange(kTent.Renderers);
            kEyes = new Transform[3];
            for (int i = 0; i < 3; i++) { kEyes[i] = EntityKit.FlatEye("Eye" + i, kRoot.transform, 0.15f, mEyePale, P); Collect(rs, kEyes[i]); }
            kGrains = new Transform[6];
            for (int i = 0; i < kGrains.Length; i++)
            {
                kGrains[i] = EntityKit.Obj("Light" + i, kRoot.transform, EntityKit.Prim(PrimitiveType.Sphere), mGrainWhite, P).transform;
                kGrains[i].localScale = Vector3.one * 0.05f;
            }
            var lgo = EntityKit.Group("PurpleGlow", kRoot.transform, P);
            kLight = lgo.AddComponent<Light>();
            kLight.type = LightType.Point; kLight.color = new Color(0.62f, 0.25f, 1f); kLight.intensity = 2.5f; kLight.range = 4.5f; kLight.shadows = LightShadows.None;
            lgo.AddComponent<CameraOnlyLight>().who = CameraOnlyLight.Who.Phone;
            if (crowd != null && crowd.Victim != null) rs.AddRange(crowd.Victim.model.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            kTarget = MakeTarget(kRoot, "kneeling_tentacle", null, rs);
        }

        void TickKneel(float t)
        {
            var v = crowd != null ? crowd.Victim : null;
            bool on = RitualScript.InWindow(RitualScript.WinKneel, t) && v != null && v.head != null && crowd.VictimKneeling;
            SetActive(kRoot, on);
            if (!on) return;
            kTarget.activeNow = true;
            Vector3 head = v.head.position;
            // 3초 넘게 정조준하면 촉수 끝이 머리를 놓고 카메라 쪽을 가리키며 화면이 좌우로 흔들린다
            if (kAim.Track(head, 0.2f) >= 3f && Time.time > kReleaseUntil)
            {
                kReleaseUntil = Time.time + 4f;
                kAim.Reset();
                EntityStareFX.ShakePhone(1.4f, 0.7f, true);
            }
            bool released = Time.time < kReleaseUntil;
            if (released)
            {
                Vector3 w = EntityKit.WatchPoint();
                Vector3 goal = head + (w - head).normalized * 1.3f + Vector3.up * 0.9f;
                kTipFree = Vector3.Lerp(kTipFree == Vector3.zero ? head : kTipFree, goal, Damp(4f));
                kTent.tipFollow = null; kTent.tipPos = kTipFree;
            }
            else
            {
                kTipFree = Vector3.Lerp(kTipFree == Vector3.zero ? head : kTipFree, head + Vector3.up * 0.12f, Damp(3f));
                kTent.tipFollow = null; kTent.tipPos = kTipFree;
            }
            kLight.transform.position = head + Vector3.up * 0.6f;
            // 촉수 표면의 눈알들이 돌아간다
            float[] ss = { 0.32f, 0.52f, 0.72f };
            for (int i = 0; i < kEyes.Length; i++)
            {
                Vector3 p0 = kTent.PointAt(ss[i] - 0.03f), p1 = kTent.PointAt(ss[i] + 0.03f);
                Vector3 axis = (p1 - p0).sqrMagnitude > 1e-6f ? (p1 - p0).normalized : Vector3.down;
                Vector3 side = Vector3.Cross(axis, Vector3.up); if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
                side = Quaternion.AngleAxis(t * 70f + i * 120f, axis) * side.normalized;
                var e = kEyes[i];
                e.position = kTent.PointAt(ss[i]) + side * kTent.RadiusAt(ss[i]) * 0.95f;
                e.rotation = Quaternion.LookRotation(side, axis);
            }
            // 하얀 빛(생기·기억)이 머리에서 촉수를 타고 올라간다
            for (int i = 0; i < kGrains.Length; i++)
            {
                float s = Mathf.Repeat(t * 0.35f + i / (float)kGrains.Length, 1f);
                kGrains[i].position = kTent.PointAt(1f - s);
                kGrains[i].localScale = Vector3.one * (0.055f * Mathf.Sin(s * Mathf.PI) + 0.01f);
            }
        }

        /// <summary>성능 보고용.</summary>
        public static string Stats => $"새 렌더러 {EntityKit.RendererCount}개";
    }
}
