using System;
using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 소리 담당(E)의 합성기: 오디오 파일 없이 코드로 PCM을 만들어 AudioClip을 짓는다.
    /// 웹 빌드에서 안전하도록 AudioClip.Create(stream:false) + SetData만 쓴다(OnAudioFilterRead·PCMReaderCallback 안 씀).
    /// 루프 이음매: 음은 루프 길이의 정수 주기로 맞추고(Q), 노이즈는 끝을 시작에 크로스페이드한다(Fold). 박수·허밍은 원형으로 감아 쓴다.
    /// 배경 클립은 RMS 0.25(약 -12dBFS)로, 효과음은 최고점 기준으로 맞춘다. 실제 크기는 AudioSource.volume으로 정한다.
    /// </summary>
    public static class UndercoverAudioSynth
    {
        public const int SrLow = 22050;
        public const int SrHigh = 44100;
        const double TwoPi = Math.PI * 2.0;
        const int LP = 0, HP = 1, BP = 2;

        /// <summary>지금까지 만든 클립의 PCM 크기 합(바이트, 웹 오디오 float32 기준 추정).</summary>
        public static long TotalBytes { get; private set; }
        public static int ClipCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { TotalBytes = 0; ClipCount = 0; }

        // ---------- 기본 부품 ----------

        sealed class Rng
        {
            uint s;
            public Rng(uint seed) { s = seed == 0 ? 0x9E3779B9u : seed * 2654435761u + 1u; }
            /// <summary>[-1, 1)</summary>
            public float Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s >> 8) * (1f / 8388608f) - 1f; }
            /// <summary>[0, 1)</summary>
            public float Value() => (Next() + 1f) * 0.5f;
            public float Range(float a, float b) => a + (b - a) * Value();
        }

        /// <summary>RBJ 2차 필터 (저역·고역·대역 통과).</summary>
        struct Biquad
        {
            float b0, b1, b2, a1, a2, z1, z2;

            public static Biquad Make(int type, float fc, float q, int sr) { var b = new Biquad(); b.Set(type, fc, q, sr); return b; }

            public void Set(int type, float fc, float q, int sr)
            {
                fc = Mathf.Clamp(fc, 10f, sr * 0.45f);
                double w = TwoPi * fc / sr, cw = Math.Cos(w), sw = Math.Sin(w), al = sw / (2.0 * q), a0 = 1.0 + al;
                double nb0, nb1, nb2;
                if (type == LP) { nb0 = (1 - cw) * 0.5; nb1 = 1 - cw; nb2 = (1 - cw) * 0.5; }
                else if (type == HP) { nb0 = (1 + cw) * 0.5; nb1 = -(1 + cw); nb2 = (1 + cw) * 0.5; }
                else { nb0 = al; nb1 = 0; nb2 = -al; }
                b0 = (float)(nb0 / a0); b1 = (float)(nb1 / a0); b2 = (float)(nb2 / a0);
                a1 = (float)(-2.0 * cw / a0); a2 = (float)((1.0 - al) / a0);
            }

            public float Run(float x)
            {
                float y = b0 * x + z1;
                z1 = b1 * x - a1 * y + z2;
                z2 = b2 * x - a2 * y;
                return y;
            }
        }

        /// <summary>회전 사인 발진기 (double 정밀도라 루프 이음매에서 위상이 어긋나지 않는다).</summary>
        struct Osc
        {
            double re, im, cr, ci;
            public Osc(double f, int sr, double phase = 0)
            {
                double w = TwoPi * f / sr;
                cr = Math.Cos(w); ci = Math.Sin(w);
                re = Math.Cos(phase); im = Math.Sin(phase);
            }
            public float Next()
            {
                float v = (float)im;
                double nr = re * cr - im * ci;
                im = re * ci + im * cr;
                re = nr;
                return v;
            }
        }

        /// <summary>루프 길이 안에 정수 주기가 되도록 주파수를 맞춘다.</summary>
        static float Q(float f, float loopSec) => Mathf.Max(1f, Mathf.Round(f * loopSec)) / loopSec;

        /// <summary>x[n..n+fade) 꼬리를 x[0..fade) 머리에 등전력 크로스페이드 → 앞 n개가 이음매 없는 루프.</summary>
        static void Fold(float[] x, int n, int fade)
        {
            for (int i = 0; i < fade; i++)
            {
                float t = (i + 0.5f) / fade;
                x[i] = x[i] * Mathf.Sqrt(t) + x[n + i] * Mathf.Sqrt(1f - t);
            }
        }

        static void Fades(float[] x, int n, int fadeIn, int fadeOut)
        {
            for (int i = 0; i < fadeIn && i < n; i++) x[i] *= i / (float)fadeIn;
            for (int i = 0; i < fadeOut && i < n; i++) x[n - 1 - i] *= i / (float)fadeOut;
        }

        static void NormalizeRms(float[][] ch, int n, float rms, float peak)
        {
            double sum = 0;
            foreach (var c in ch) for (int i = 0; i < n; i++) sum += c[i] * c[i];
            float cur = (float)Math.Sqrt(sum / Math.Max(1, n * ch.Length));
            if (cur < 1e-9f) return;
            float g = rms / cur, max = 0f;
            foreach (var c in ch) for (int i = 0; i < n; i++) { c[i] *= g; max = Mathf.Max(max, Mathf.Abs(c[i])); }
            if (max > peak)   // 튀는 곳만 부드럽게 눌러 준다
                foreach (var c in ch) for (int i = 0; i < n; i++) c[i] = peak * (float)Math.Tanh(c[i] / peak);
        }

        static void NormalizePeak(float[][] ch, int n, float peak)
        {
            float max = 0f;
            foreach (var c in ch) for (int i = 0; i < n; i++) max = Mathf.Max(max, Mathf.Abs(c[i]));
            if (max < 1e-9f) return;
            float g = peak / max;
            foreach (var c in ch) for (int i = 0; i < n; i++) c[i] *= g;
        }

        static AudioClip Make(string name, float[][] ch, int n, int sr)
        {
            int cc = ch.Length;
            var data = new float[n * cc];
            for (int i = 0; i < n; i++)
                for (int k = 0; k < cc; k++) data[i * cc + k] = ch[k][i];
            var clip = AudioClip.Create(name, n, cc, sr, false);
            clip.SetData(data, 0);
            TotalBytes += (long)n * cc * 4;
            ClipCount++;
            return clip;
        }

        /// <summary>
        /// 슈뢰더 잔향(빗살 4 + 전역통과 2). circular면 버퍼를 두 바퀴 돌려 루프 끝 잔향이 머리로 이어진다.
        /// </summary>
        static void Reverb(float[] x, int n, int sr, float wet, float fb, bool circular, float size = 1f)
        {
            int[] combTenthMs = { 297, 371, 411, 437 };
            var acc = new float[n];
            int laps = circular ? 2 : 1;
            foreach (int tm in combTenthMs)
            {
                int d = Mathf.Max(1, (int)(tm * 0.0001f * sr * size));
                var buf = new float[d];
                int p = 0; float damp = 0f;
                for (int lap = 0; lap < laps; lap++)
                    for (int i = 0; i < n; i++)
                    {
                        float y = buf[p];
                        damp = y * 0.65f + damp * 0.35f;
                        buf[p] = x[i] + damp * fb;
                        if (++p >= d) p = 0;
                        if (lap == laps - 1) acc[i] += y;
                    }
            }
            float[] apMs = { 5.0f, 1.7f };
            foreach (float ms in apMs)
            {
                int d = Mathf.Max(1, (int)(ms * 0.001f * sr * size));
                var buf = new float[d];
                var outBuf = new float[n];
                int p = 0; const float g = 0.6f;
                for (int lap = 0; lap < laps; lap++)
                    for (int i = 0; i < n; i++)
                    {
                        float bo = buf[p];
                        float y = -g * acc[i] + bo;
                        buf[p] = acc[i] + g * y;
                        if (++p >= d) p = 0;
                        if (lap == laps - 1) outBuf[i] = y;
                    }
                acc = outBuf;
            }
            for (int i = 0; i < n; i++) x[i] += acc[i] * wet * 0.25f;
        }

        static float Gauss(float f, float c, float bw) { float u = (f - c) / bw; return Mathf.Exp(-u * u); }

        // ---------- 배경 (루프) ----------

        /// <summary>극장 공기: 낮은 대역 노이즈 + 아주 작은 공조 바람. 8초 모노.</summary>
        public static AudioClip RoomTone()
        {
            const float L = 8f; int sr = SrLow, n = (int)(L * sr), fade = sr / 2, len = n + fade;
            var x = new float[len];
            var rng = new Rng(11);
            var lp = Biquad.Make(LP, 170f, 0.7f, sr);
            var hp = Biquad.Make(HP, 28f, 0.7f, sr);
            var hum = Biquad.Make(BP, 72f, 2.5f, sr);
            var air = Biquad.Make(BP, 1700f, 0.6f, sr);
            float brown = 0f;
            for (int i = 0; i < len; i++)
            {
                float w = rng.Next();
                brown = brown * 0.996f + w * 0.06f;
                x[i] = hp.Run(lp.Run(brown)) + hum.Run(w) * 0.2f + air.Run(rng.Next()) * 0.035f;
            }
            Fold(x, n, fade);
            for (int i = 0; i < n; i++) x[i] *= 1f + 0.18f * Mathf.Sin((float)(TwoPi * i / n));   // 8초에 한 번 느린 출렁임
            var ch = new[] { x };
            NormalizeRms(ch, n, 0.25f, 0.9f);
            return Make("ul_roomtone", ch, n, sr);
        }

        static void AddSine(float[] x, int n, int sr, float f, float amp, float lfoCycles, float lfoDepth, double phase = 0)
        {
            var o = new Osc(f, sr, phase);
            var l = new Osc(lfoCycles * sr / (double)n, sr, phase * 1.7);
            for (int i = 0; i < n; i++) x[i] += amp * (1f + lfoDepth * l.Next()) * o.Next();
        }

        static void AddSaw(float[] x, int n, int sr, float f, float amp, int harmonics, float tilt, float lfoCycles, float lfoDepth)
        {
            for (int k = 1; k <= harmonics; k++)
            {
                if (f * k > sr * 0.45f) break;
                AddSine(x, n, sr, f * k, amp / Mathf.Pow(k, tilt), lfoCycles, lfoDepth, k * 0.37);
            }
        }

        /// <summary>낮게 어긋난 드론: A1 근처 사인 두 개(맥놀이) + 5도 + 부드러운 톱니 + 느린 LFO. 12초 모노.</summary>
        public static AudioClip DroneLow()
        {
            const float L = 12f; int sr = SrLow, n = (int)(L * sr);
            var x = new float[n];
            AddSine(x, n, sr, Q(55f, L), 0.35f, 1f, 0.2f);
            AddSine(x, n, sr, Q(55.25f, L), 0.25f, 2f, 0.25f, 1.1);
            AddSine(x, n, sr, Q(82.42f, L), 0.16f, 1f, 0.5f, 2.3);
            AddSaw(x, n, sr, Q(110.08f, L), 0.12f, 10, 1.4f, 3f, 0.3f);
            AddSine(x, n, sr, Q(220.4f, L), 0.035f, 3f, 0.8f, 0.5);
            var ov = new Osc(1.0 / L, sr, 0.3);
            for (int i = 0; i < n; i++) x[i] *= 1f + 0.2f * ov.Next();
            var ch = new[] { x };
            NormalizeRms(ch, n, 0.25f, 0.9f);
            return Make("ul_drone_low", ch, n, sr);
        }

        /// <summary>강림용 불협 드론: 반음·삼전음 무리 + 저역 굉음. 12초 모노.</summary>
        public static AudioClip DroneDissonant()
        {
            const float L = 12f; int sr = SrLow, n = (int)(L * sr), fade = sr, len = n + fade;
            var rum = new float[len];
            var rng = new Rng(17);
            var bp = Biquad.Make(BP, 60f, 1.2f, sr);
            for (int i = 0; i < len; i++) rum[i] = bp.Run(rng.Next());
            Fold(rum, n, fade);
            var rr = new[] { rum };
            NormalizePeak(rr, n, 1f);

            var x = new float[n];
            AddSine(x, n, sr, Q(58.27f, L), 0.25f, 2f, 0.3f);
            AddSine(x, n, sr, Q(77.78f, L), 0.2f, 1f, 0.5f, 0.9);
            AddSaw(x, n, sr, Q(116.54f, L), 0.12f, 8, 1.2f, 5f, 0.6f);
            AddSine(x, n, sr, Q(155.56f, L), 0.08f, 7f, 0.9f, 1.3);
            AddSine(x, n, sr, Q(311.13f, L), 0.035f, 4f, 1f, 2.1);
            AddSine(x, n, sr, Q(329.63f, L), 0.025f, 3f, 1f, 0.4);   // 반음 부딪힘
            var ov = new Osc(2.0 / L, sr);
            for (int i = 0; i < n; i++) x[i] = (x[i] + rum[i] * 0.22f) * (1f + 0.3f * ov.Next());
            var ch = new[] { x };
            NormalizeRms(ch, n, 0.25f, 0.9f);
            return Make("ul_drone_dissonant", ch, n, sr);
        }

        /// <summary>신도 웅성거림: 말하는 사람 10명(대역 통과 노이즈+성대 톱니, 모음 포먼트, 음절 진폭). 8초 스테레오.</summary>
        public static AudioClip Murmur()
        {
            const float L = 8f; int sr = SrLow, n = (int)(L * sr), fade = sr * 3 / 4, len = n + fade;
            var lc = new float[len];
            var rc = new float[len];
            var rng = new Rng(23);
            float[,] vowels = { { 700, 1150 }, { 450, 1850 }, { 300, 2250 }, { 460, 850 }, { 330, 780 }, { 550, 1500 } };
            const int talkers = 10;
            for (int t = 0; t < talkers; t++)
            {
                float pan = rng.Range(-0.85f, 0.85f);
                float gl = Mathf.Cos((pan + 1f) * Mathf.PI * 0.25f), gr = Mathf.Sin((pan + 1f) * Mathf.PI * 0.25f);
                float gain = rng.Range(0.45f, 1f);
                bool female = rng.Value() < 0.4f;
                float f0base = female ? rng.Range(185f, 235f) : rng.Range(95f, 135f);
                float fmul = female ? 1.12f : 1f;
                var f1 = new Biquad();
                var f2 = new Biquad();
                var muffle = Biquad.Make(LP, rng.Range(1300f, 2400f), 0.7f, sr);   // 거리감
                double ph = rng.Value();
                bool speaking = rng.Value() < 0.6f;
                int left = (int)(rng.Range(0.2f, 2f) * sr);
                int phraseLen = Mathf.Max(1, left), phrasePos = 0;
                int sylLen = 1, sylPos = 1;
                float sylAmp = 0f;
                for (int i = 0; i < len; i++)
                {
                    if (left-- <= 0)
                    {
                        speaking = !speaking;
                        left = (int)((speaking ? rng.Range(1.2f, 3.5f) : rng.Range(0.4f, 2.2f)) * sr);
                        phraseLen = Mathf.Max(1, left); phrasePos = 0; sylPos = sylLen;
                    }
                    float env = 0f, f0 = f0base;
                    if (speaking)
                    {
                        if (sylPos >= sylLen)
                        {
                            sylLen = (int)(rng.Range(0.11f, 0.26f) * sr); sylPos = 0;
                            sylAmp = rng.Range(0.4f, 1f);
                            int v = Mathf.Min(5, (int)(rng.Value() * 6f));
                            f1.Set(BP, vowels[v, 0] * fmul, 3.5f, sr);
                            f2.Set(BP, vowels[v, 1] * fmul, 5f, sr);
                        }
                        env = sylAmp * Mathf.Sin(Mathf.PI * sylPos / (float)sylLen);
                        sylPos++;
                        f0 = f0base * Mathf.Clamp(1.12f - 0.25f * phrasePos / phraseLen, 0.85f, 1.12f);   // 문장 끝으로 갈수록 내려감
                        phrasePos++;
                    }
                    ph += f0 / sr; if (ph >= 1.0) ph -= 1.0;
                    float src = (float)(ph * 2.0 - 1.0) * 0.55f + rng.Next() * 0.45f;
                    float y = muffle.Run(f1.Run(src) + f2.Run(src) * 0.5f) * env * gain;
                    lc[i] += y * gl; rc[i] += y * gr;
                }
            }
            // 멀리 깔린 웅웅거림 (밀도)
            var bedL = Biquad.Make(BP, 380f, 0.7f, sr);
            var bedR = Biquad.Make(BP, 420f, 0.7f, sr);
            for (int i = 0; i < len; i++) { lc[i] += bedL.Run(rng.Next()) * 0.05f; rc[i] += bedR.Run(rng.Next()) * 0.05f; }
            Fold(lc, n, fade); Fold(rc, n, fade);
            Reverb(lc, n, sr, 0.35f, 0.72f, true, 1.0f);
            Reverb(rc, n, sr, 0.35f, 0.72f, true, 1.08f);
            var ch = new[] { lc, rc };
            NormalizeRms(ch, n, 0.25f, 0.9f);
            return Make("ul_murmur", ch, n, sr);
        }

        /// <summary>
        /// 박수: 22명이 120BPM(0.5초)마다 친다. 4초 = 8박 = 복창 한 번. 사람마다 박자 버릇·음색·위치가 다르다. 4초 스테레오 44.1k.
        /// </summary>
        public static AudioClip Claps()
        {
            const float L = 4f, beat = 0.5f; int sr = SrHigh, n = (int)(L * sr);
            var lc = new float[n];
            var rc = new float[n];
            var rng = new Rng(37);
            const int people = 22;
            int dur = (int)(0.07f * sr);
            for (int p = 0; p < people; p++)
            {
                float pan = rng.Range(-0.9f, 0.9f);
                float gl = Mathf.Cos((pan + 1f) * Mathf.PI * 0.25f), gr = Mathf.Sin((pan + 1f) * Mathf.PI * 0.25f);
                float gain = rng.Range(0.3f, 1f);
                float bias = rng.Range(-0.02f, 0.02f);
                float fc = rng.Range(900f, 2200f), q = rng.Range(1f, 2f), tau = rng.Range(0.006f, 0.012f);
                for (int b = 0; b < 8; b++)
                {
                    if (rng.Value() < 0.07f) continue;
                    float accent = (b % 2 == 0) ? 1f : 0.8f;
                    int start = (int)((b * beat + bias + rng.Next() * 0.012f) * sr);
                    var bq = Biquad.Make(BP, fc * rng.Range(0.9f, 1.1f), q, sr);
                    int micro = (int)(rng.Range(0.002f, 0.006f) * sr);
                    for (int k = 0; k < dur; k++)
                    {
                        float e = Mathf.Exp(-k / (tau * sr));
                        if (k >= micro) e += 0.6f * Mathf.Exp(-(k - micro) / (tau * sr));
                        if (k < 20) e *= k / 20f;
                        float y = bq.Run(rng.Next()) * e * gain * accent;
                        int idx = ((start + k) % n + n) % n;   // 루프를 넘으면 머리로 감는다
                        lc[idx] += y * gl; rc[idx] += y * gr;
                    }
                }
            }
            Reverb(lc, n, sr, 0.4f, 0.74f, true, 1.1f);
            Reverb(rc, n, sr, 0.4f, 0.74f, true, 1.18f);
            var ch = new[] { lc, rc };
            NormalizePeak(ch, n, 0.9f);
            return Make("ul_claps", ch, n, sr);
        }

        /// <summary>코로 부르는 '음—' 스펙트럼 모양 (F1≈280Hz 강하게, 위 포먼트 약하게).</summary>
        static float HumEnv(float f) => Gauss(f, 280f, 120f) + 0.18f * Gauss(f, 1050f, 280f) + 0.06f * Gauss(f, 2400f, 450f) + 0.02f;

        static float[] HumTable(float f0, int size)
        {
            var t = new float[size];
            for (int k = 1; k * f0 < 3800f; k++)
            {
                float a = HumEnv(k * f0) / Mathf.Sqrt(k);
                double ph = k * 0.7;
                for (int i = 0; i < size; i++) t[i] += a * (float)Math.Sin(TwoPi * k * i / size + ph);
            }
            float max = 1e-6f;
            for (int i = 0; i < size; i++) max = Mathf.Max(max, Mathf.Abs(t[i]));
            for (int i = 0; i < size; i++) t[i] /= max;
            return t;
        }

        /// <summary>
        /// 복창 허밍: "내 눈을 덮고 눈동자님만 본다" 12음절을 8분음표(0.25초) 리듬으로 '음—' 허밍. 남 8 + 여 4.
        /// 4초 = 박수 루프와 같은 길이·박. 마지막 8분음표는 쉼(루프 이음매). 4초 스테레오.
        /// </summary>
        public static AudioClip Chant()
        {
            const float L = 4f, eighth = 0.25f; int sr = SrLow, n = (int)(L * sr);
            // (시작 8분음표, 길이, 반음)
            int[,] syl = { { 0, 1, 0 }, { 1, 1, 0 }, { 2, 1, 0 }, { 3, 1, 1 }, { 4, 2, 0 }, { 6, 1, 0 }, { 7, 1, 0 }, { 8, 1, 0 }, { 9, 1, 1 }, { 10, 1, 0 }, { 11, 1, -2 }, { 12, 3, -5 } };
            int sylCount = syl.GetLength(0);
            const float baseHz = 146.83f;   // D3
            const int TS = 1024;
            int[] notes = { 0, 1, -2, -5 };
            var tables = new float[2, notes.Length][];
            var freqs = new float[2, notes.Length];
            for (int o = 0; o < 2; o++)
                for (int k = 0; k < notes.Length; k++)
                {
                    freqs[o, k] = baseHz * Mathf.Pow(2f, (notes[k] + 12 * o) / 12f);
                    tables[o, k] = HumTable(freqs[o, k], TS);
                }
            // 8분음표 칸 → 음절 번호(-1 = 쉼), 음절 → 음 번호
            var slotMap = new int[16];
            for (int k = 0; k < 16; k++) slotMap[k] = -1;
            var sylNote = new int[sylCount];
            for (int k = 0; k < sylCount; k++)
            {
                for (int c = 0; c < syl[k, 1]; c++) slotMap[syl[k, 0] + c] = k;
                sylNote[k] = Mathf.Max(0, Array.IndexOf(notes, syl[k, 2]));
            }

            var lc = new float[n];
            var rc = new float[n];
            var rng = new Rng(47);
            float gk = 1f - Mathf.Exp(-1f / (0.03f * sr));
            for (int v = 0; v < 12; v++)
            {
                int oct = v < 8 ? 0 : 1;
                float detune = Mathf.Pow(2f, rng.Range(-12f, 12f) / 1200f);
                float offset = rng.Range(0f, 0.04f);   // 늦게만 → 루프 끝은 모두 쉼
                float gain = rng.Range(0.5f, 1f) * (oct == 0 ? 1f : 0.7f);
                float pan = rng.Range(-0.8f, 0.8f);
                float gl = Mathf.Cos((pan + 1f) * Mathf.PI * 0.25f), gr = Mathf.Sin((pan + 1f) * Mathf.PI * 0.25f);
                float vibRate = rng.Range(4.5f, 5.8f), vibPh = rng.Range(0f, 6.28f);
                double ph = rng.Value();
                float gate = 0f;
                int lastNote = sylNote[sylCount - 1];
                for (int i = -n; i < n; i++)   // 한 바퀴 예열 → 루프 머리의 상태가 꼬리와 같다
                {
                    float tsec = Mathf.Repeat((i < 0 ? i + n : i) / (float)sr - offset, L);
                    float slot = tsec / eighth;
                    int s = slotMap[Mathf.Clamp((int)slot, 0, 15)];
                    float artic = 0.35f, gTarget = 0f;   // 쉼에서는 gate가 부드럽게 닫고, 음은 마지막 음 그대로
                    if (s >= 0)
                    {
                        gTarget = 1f;
                        float u = (slot - syl[s, 0]) / syl[s, 1];
                        float bump = u < 0.12f ? u / 0.12f : u > 0.75f ? (1f - u) / 0.25f : 1f;
                        artic = 0.35f + 0.65f * bump;
                        lastNote = sylNote[s];
                    }
                    int noteIdx = lastNote;
                    gate += (gTarget - gate) * gk;
                    float f = freqs[oct, noteIdx] * detune * (1f + 0.003f * Mathf.Sin(vibRate * 6.2832f * tsec + vibPh));
                    ph += f / sr; ph -= Math.Floor(ph);
                    if (i < 0) continue;
                    var tb = tables[oct, noteIdx];
                    float fi = (float)(ph * TS);
                    int i0 = (int)fi; float fr = fi - i0;
                    float w = tb[i0 % TS] * (1f - fr) + tb[(i0 + 1) % TS] * fr;
                    float y = w * gate * artic * gain;
                    lc[i] += y * gl; rc[i] += y * gr;
                }
            }
            Reverb(lc, n, sr, 0.45f, 0.75f, true, 1.1f);
            Reverb(rc, n, sr, 0.45f, 0.75f, true, 1.17f);
            var ch = new[] { lc, rc };
            NormalizeRms(ch, n, 0.25f, 0.9f);
            return Make("ul_chant_hum", ch, n, sr);
        }

        /// <summary>
        /// 괴물 울림(폰 마이크만 듣는 소리): 36Hz 서브 + FM 그르렁 두 겹 + 거친 진폭 + 4초 숨. 8초 모노.
        /// </summary>
        public static AudioClip EntityGrowl()
        {
            const float L = 8f; int sr = SrLow, n = (int)(L * sr), fade = sr / 2, len = n + fade;
            var rough = new float[len];
            var breath = new float[len];
            var rng = new Rng(59);
            var lpR = Biquad.Make(LP, 28f, 0.7f, sr);
            var bpB = Biquad.Make(BP, 130f, 0.8f, sr);
            for (int i = 0; i < len; i++) { rough[i] = lpR.Run(rng.Next()); breath[i] = bpB.Run(rng.Next()); }
            Fold(rough, n, fade); Fold(breath, n, fade);
            NormalizePeak(new[] { rough }, n, 1f);
            NormalizePeak(new[] { breath }, n, 1f);

            var x = new float[n];
            var sub = new Osc(Q(36f, L), sr);
            double fc1 = Q(72f, L), fm1 = Q(36.5f, L), fc2 = Q(109f, L), fm2 = Q(54.75f, L);
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)sr;
                double u = i / (double)n;
                double i1 = 2.2 + 1.4 * Math.Sin(TwoPi * 3 * u);
                double i2 = 3.0 + 1.0 * Math.Sin(TwoPi * 5 * u + 1.0);
                float g1 = (float)Math.Sin(TwoPi * fc1 * t + i1 * Math.Sin(TwoPi * fm1 * t));
                float g2 = (float)Math.Sin(TwoPi * fc2 * t + i2 * Math.Sin(TwoPi * fm2 * t));
                float breathEnv = 0.65f + 0.35f * (float)Math.Sin(TwoPi * 2 * u);   // 4초 들숨·날숨
                float r = 0.55f + 0.45f * rough[i];
                x[i] = breathEnv * (0.55f * sub.Next() + (0.35f * g1 + 0.18f * g2) * r + 0.22f * breath[i]);
            }
            var ch = new[] { x };
            NormalizeRms(ch, n, 0.28f, 0.95f);
            return Make("ul_entity_growl", ch, n, sr);
        }

        // ---------- 효과음 (한 번) ----------

        /// <summary>옷 스침. up = 폰을 꺼내 들 때(올라가는 쓸림), 아니면 넣을 때.</summary>
        public static AudioClip Cloth(bool up)
        {
            int sr = SrHigh; float dur = up ? 0.34f : 0.28f; int n = (int)(dur * sr);
            var x = new float[n];
            var rng = new Rng(up ? 41u : 43u);
            var bp = new Biquad();
            var lp = Biquad.Make(LP, 500f, 0.7f, sr);
            float grain = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                if ((i & 31) == 0) bp.Set(BP, up ? Mathf.Lerp(1100f, 3000f, u) : Mathf.Lerp(2600f, 900f, u), 0.9f, sr);
                float env = up ? Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.7f)), 1.6f)
                               : Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.45f)), 1.4f);
                if (rng.Value() < 0.004f) grain = rng.Range(0.5f, 1f);   // 옷감 마찰 알갱이
                grain *= 0.995f;
                float w = rng.Next();
                x[i] = (bp.Run(w) * (0.6f + 0.8f * grain) + lp.Run(w) * 0.35f) * env;
            }
            Fades(x, n, 32, 256);
            var ch = new[] { x };
            NormalizePeak(ch, n, 0.9f);
            return Make(up ? "ul_cloth_up" : "ul_cloth_down", ch, n, sr);
        }

        static void Click(float[] x, int sr, float at, float fc, float amp, float tau, Rng rng)
        {
            int s0 = (int)(at * sr), dur = (int)(tau * 8f * sr);
            var bp = Biquad.Make(BP, fc, 1.6f, sr);
            for (int k = 0; k < dur && s0 + k < x.Length; k++)
            {
                float e = Mathf.Exp(-k / (tau * sr));
                x[s0 + k] += (bp.Run(rng.Next()) * 3f + Mathf.Sin(Mathf.PI * fc * k / sr) * 0.3f) * e * amp;
            }
        }

        /// <summary>셔터: 딸깍(두 번) + 짧은 모터 기계음.</summary>
        public static AudioClip Shutter()
        {
            int sr = SrHigh, n = (int)(0.24f * sr);
            var x = new float[n];
            var rng = new Rng(53);
            Click(x, sr, 0.000f, 4200f, 1.0f, 0.004f, rng);
            Click(x, sr, 0.048f, 2900f, 0.75f, 0.006f, rng);
            int a = (int)(0.07f * sr), b = (int)(0.17f * sr);
            double ph = 0;
            for (int i = a; i < b; i++)
            {
                float u = (i - a) / (float)(b - a);
                ph += Mathf.Lerp(1150f, 820f, u) / sr;
                float s = Mathf.Sin((float)(ph * TwoPi));
                x[i] += (Mathf.Sign(s) * 0.5f + s * 0.5f) * 0.12f * Mathf.Sin(Mathf.PI * u);
            }
            Click(x, sr, 0.175f, 3400f, 0.35f, 0.003f, rng);
            Fades(x, n, 8, 64);
            var ch = new[] { x };
            NormalizePeak(ch, n, 0.9f);
            return Make("ul_shutter", ch, n, sr);
        }

        static float PulseEnv(float t, float a, float b)
        {
            if (t < a || t > b) return 0f;
            return Mathf.Min(1f, (t - a) / 0.012f) * Mathf.Min(1f, (b - t) / 0.02f);
        }

        /// <summary>진동: 172Hz 버즈 두 번(부르르-부르르) + 떨림.</summary>
        public static AudioClip Buzz()
        {
            int sr = SrHigh, n = (int)(0.6f * sr);
            var x = new float[n];
            var rng = new Rng(57);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = PulseEnv(t, 0f, 0.22f) + PulseEnv(t, 0.32f, 0.54f);
                if (env <= 0f) continue;
                ph += (172f + 6f * Mathf.Sin(6.2832f * 7f * t)) / sr;
                float p = (float)(ph * TwoPi);
                float tone = Mathf.Sin(p) + 0.35f * Mathf.Sin(3f * p) + 0.18f * Mathf.Sin(5f * p) + 0.1f * Mathf.Sin(7f * p);
                float rattle = 1f + 0.35f * Mathf.Sin(6.2832f * 31f * t);
                x[i] = (tone * rattle + rng.Next() * 0.05f) * env;
            }
            var ch = new[] { x };
            NormalizePeak(ch, n, 0.8f);
            return Make("ul_buzz", ch, n, sr);
        }

        /// <summary>뺏길 때 둔탁한 충격: 손이 낚아채는 짧은 소리 + 낮게 떨어지는 쿵.</summary>
        public static AudioClip Thud()
        {
            int sr = SrHigh, n = (int)(0.9f * sr);
            var x = new float[n];
            var rng = new Rng(61);
            var lp = Biquad.Make(LP, 220f, 0.7f, sr);
            var bp = Biquad.Make(BP, 700f, 1.2f, sr);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                ph += (38f + 40f * Mathf.Exp(-t / 0.06f)) / sr;
                float body = Mathf.Sin((float)(ph * TwoPi)) * Mathf.Exp(-t / 0.2f);
                float knock = lp.Run(rng.Next()) * Mathf.Exp(-t / 0.045f) * 2.5f;
                float grab = bp.Run(rng.Next()) * Mathf.Exp(-t / 0.018f) * 1.2f;
                x[i] = body * 0.9f + knock + grab;
            }
            Reverb(x, n, sr, 0.25f, 0.7f, false, 1.3f);
            Fades(x, n, 16, 2048);
            var ch = new[] { x };
            NormalizePeak(ch, n, 0.95f);
            return Make("ul_thud", ch, n, sr);
        }

        /// <summary>신호 끊김: 샘플 앤 홀드로 깨진 지직 + 디지털 버즈 조각, 끝은 뚝 끊긴다(그 뒤 정적).</summary>
        public static AudioClip SignalLoss()
        {
            int sr = SrHigh, n = (int)(1.05f * sr);
            var x = new float[n];
            var rng = new Rng(67);
            var hp = Biquad.Make(HP, 250f, 0.7f, sr);
            int blockLeft = 0, hold = 1, holdPos = 0;
            float held = 0f, blockAmp = 1f, toneHz = 0f;
            bool on = true;
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                if (blockLeft-- <= 0)
                {
                    blockLeft = (int)(rng.Range(0.012f, 0.045f) * sr);
                    hold = 1 << Mathf.Min(4, (int)(rng.Value() * 5f));
                    on = rng.Value() < 0.82f;
                    blockAmp = rng.Range(0.35f, 1f);
                    toneHz = rng.Value() < 0.35f ? rng.Range(90f, 480f) : 0f;
                }
                if (holdPos++ >= hold) { holdPos = 0; held = rng.Next(); }
                float v = on ? held * blockAmp : held * 0.05f;
                if (toneHz > 0f)
                {
                    ph += toneHz / sr;
                    v += Mathf.Sign(Mathf.Sin((float)(ph * TwoPi))) * 0.25f * blockAmp;
                }
                float env = t < 0.08f ? t / 0.08f : 1f;
                x[i] = hp.Run(v) * env;
            }
            Fades(x, n, 0, (int)(0.004f * sr));
            var ch = new[] { x };
            NormalizePeak(ch, n, 0.8f);
            return Make("ul_signal_loss", ch, n, sr);
        }

        /// <summary>극장 나무 바닥 발소리: 뒤꿈치 쿵 + 밑창 끌림 + 짧은 홀 잔향.</summary>
        public static AudioClip Footstep(uint seed)
        {
            int sr = SrHigh, n = (int)(0.42f * sr);
            var x = new float[n];
            var rng = new Rng(seed);
            var lp = Biquad.Make(LP, 320f, 0.7f, sr);
            var bp = Biquad.Make(BP, rng.Range(1300f, 1900f), 1.1f, sr);
            int scuff = (int)(rng.Range(0.018f, 0.03f) * sr);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                ph += (62f + 45f * Mathf.Exp(-t / 0.02f)) / sr;
                float heel = Mathf.Sin((float)(ph * TwoPi)) * Mathf.Exp(-t / 0.035f) * 0.9f + lp.Run(rng.Next()) * Mathf.Exp(-t / 0.03f) * 2.2f;
                float sc = i >= scuff ? bp.Run(rng.Next()) * Mathf.Exp(-(i - scuff) / (float)sr / 0.03f) * 0.9f : 0f;
                x[i] = heel + sc;
            }
            Reverb(x, n, sr, 0.3f, 0.72f, false, 1.4f);
            Fades(x, n, 8, 1024);
            var ch = new[] { x };
            NormalizePeak(ch, n, 0.9f);
            return Make("ul_step_" + seed, ch, n, sr);
        }
    }
}
