using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 심장 소리와 맥박 박자. 의심이 높을수록 빠르고 크게 뛴다 (의심 50 → 1.1초, 80 이상 → 0.6초 주기).
    /// 촬영·몰래 중 의심 30 이상, 주머니(맨눈)에서는 60 이상일 때만 작게. 소리는 코드로 만든 저음 '쿵-쿵'.
    /// 가장자리 붉은 맥박(WarningFX)이 Pulse를 읽어 소리와 같은 박자로 깜박인다.
    /// </summary>
    public class Heartbeat : MonoBehaviour
    {
        /// <summary>지금 맥박 세기 0~1 (쿵-쿵 두 번 솟았다 가라앉음).</summary>
        public static float Pulse { get; private set; }
        /// <summary>감시자들의 의심 중 가장 높은 값 0~100.</summary>
        public static float MaxSuspicion { get; private set; }
        /// <summary>가장 의심하는 감시자 (없으면 null).</summary>
        public static Watcher Top { get; private set; }
        /// <summary>맥박이 뛰는 중인가.</summary>
        public static bool Active { get; private set; }

        [Range(0f, 1f)] public float loudness = 0.9f;

        AudioSource src;
        AudioClip clip;
        float lastBeat = -99f, nextBeat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Pulse = 0f; MaxSuspicion = 0f; Top = null; Active = false; }

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.priority = 40;
            clip = MakeClip();
        }

        void OnDestroy()
        {
            if (clip != null) Destroy(clip);
            Pulse = 0f; Active = false; Top = null; MaxSuspicion = 0f;
        }

        void Update()
        {
            ReadSuspicion();
            var pc = PhoneController.Instance;
            bool playing = GameEvents.IsPlaying;
            bool exposed = pc != null && pc.State != PhoneState.Pocket;
            float s = MaxSuspicion;
            Active = playing && ((exposed && s >= 30f) || (!exposed && s >= 60f));

            if (Active)
            {
                if (Time.time >= nextBeat)
                {
                    float vol = exposed ? Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(30f, 90f, s)) : 0.22f;
                    if (clip != null) src.PlayOneShot(clip, vol * loudness);
                    lastBeat = Time.time;
                    nextBeat = Time.time + PeriodFor(s);
                }
                Pulse = Envelope(Time.time - lastBeat);
            }
            else
            {
                nextBeat = Time.time + 0.05f;   // 다시 켜지면 곧바로 한 번 뛴다
                Pulse = Mathf.MoveTowards(Pulse, 0f, Time.deltaTime * 3f);
            }
        }

        void ReadSuspicion()
        {
            float max = 0f;
            Watcher top = null;
            var mgr = SuspicionManager.Instance;
            if (mgr != null)
            {
                var ws = mgr.Watchers;
                for (int i = 0; i < ws.Count; i++)
                {
                    var w = ws[i];
                    if (w != null && w.suspicion > max) { max = w.suspicion; top = w; }
                }
            }
            MaxSuspicion = max;
            Top = top;
        }

        /// <summary>의심 → 박동 주기(초).</summary>
        public static float PeriodFor(float s)
        {
            if (s <= 50f) return Mathf.Lerp(1.35f, 1.1f, Mathf.InverseLerp(30f, 50f, s));
            if (s < 80f) return Mathf.Lerp(1.1f, 0.6f, Mathf.InverseLerp(50f, 80f, s));
            return 0.6f;
        }

        static float Envelope(float t)
        {
            return Mathf.Clamp01(Mathf.Max(Bump(t), Bump(t - 0.22f) * 0.7f));
        }

        static float Bump(float t)
        {
            if (t < 0f) return 0f;
            if (t < 0.04f) return t / 0.04f;
            return Mathf.Exp(-(t - 0.04f) * 9f);
        }

        // ---------- 소리 만들기 ----------

        static AudioClip MakeClip()
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
            int n = Mathf.CeilToInt(rate * 0.62f);
            var data = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float v = Thump(t, 0f, 72f, 1f) + Thump(t, 0.22f, 60f, 0.75f);
                data[i] = v;
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.9f;
            var c = AudioClip.Create("Heartbeat", n, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }

        // 저음 사인 한 번: 짧게 솟고 빠르게 감쇠. 노트북 스피커에서도 들리게 배음을 조금 섞는다.
        static float Thump(float t, float t0, float f, float amp)
        {
            float u = t - t0;
            if (u < 0f) return 0f;
            float env = (1f - Mathf.Exp(-u / 0.004f)) * Mathf.Exp(-u / 0.075f);
            // 처음에 살짝 높았다가 내려가는 음 (쿵 하는 느낌)
            float phase = 2f * Mathf.PI * (f * u + f * 0.6f * 0.02f * (1f - Mathf.Exp(-u / 0.02f)));
            float s = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f) + 0.15f * Mathf.Sin(phase * 3f);
            return amp * env * s;
        }
    }
}
