using System.Collections.Generic;
using UnityEngine;

namespace UndercoverLive
{
    public enum EyeStyle
    {
        Gold,         // 황금 홍채 + 세로 동공 (초상화·그림자 눈)
        GoldNarrow,   // 같은 눈, 세로로 찢어지듯 좁아진 동공
        Red,          // 붉은 적외선 눈 (천장 눈 덩어리·촉수 눈)
        Pale,         // 작은 흰 눈알 (현수막·촉수)
        Abyss,        // 거대 겹눈 본체 홍채 (홍채만, 다중 동심원 + 심연 동공)
        AbyssNarrow,  // 렌즈에 초점을 맞춘 본체 홍채
    }

    /// <summary>
    /// 코드로 그리는 눈 텍스처. 홍채 방사 무늬·색 그러데이션·동공(둥근/세로)·흰자 핏줄·가장자리 어둡게.
    /// 원판 메시(EntityKit.Disc)의 UV(가운데 0.5, 반지름 0.5)에 맞춰 그린다. 한 번 그리면 캐시.
    /// </summary>
    public static class EyeTexture
    {
        static readonly Dictionary<int, Texture2D> cache = new Dictionary<int, Texture2D>();
        static Texture2D flesh, glowFade;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); flesh = null; glowFade = null; }

        struct Spec
        {
            public int size, seed;
            public float irisR;                 // 0~1 (1 = 홍채만)
            public Color sclera, vein, inner, outer, pupil;
            public bool slit;
            public float pupilW, pupilH;        // 세로 동공 반폭·반높이 / 둥근 동공은 pupilW = 반지름
            public float fibers, rings, highlight, scleraShade;
        }

        public static Texture2D Get(EyeStyle style)
        {
            int key = (int)style;
            if (cache.TryGetValue(key, out var t) && t != null) return t;
            t = Draw(SpecFor(style));
            t.name = "Eye_" + style;
            cache[key] = t;
            return t;
        }

        static Spec SpecFor(EyeStyle s)
        {
            var sp = new Spec { size = 256, seed = (int)s * 7 + 3, fibers = 44, rings = 0, highlight = 0.8f, scleraShade = 0.65f };
            switch (s)
            {
                case EyeStyle.Gold:
                case EyeStyle.GoldNarrow:
                    sp.irisR = 0.64f;
                    sp.sclera = new Color(0.88f, 0.82f, 0.70f);
                    sp.vein = new Color(0.62f, 0.07f, 0.05f);
                    sp.inner = new Color(1.0f, 0.86f, 0.34f);
                    sp.outer = new Color(0.62f, 0.30f, 0.04f);
                    sp.pupil = new Color(0.02f, 0.01f, 0.0f);
                    sp.slit = true;
                    sp.pupilW = s == EyeStyle.Gold ? 0.075f : 0.026f;
                    sp.pupilH = s == EyeStyle.Gold ? 0.50f : 0.58f;
                    break;
                case EyeStyle.Red:
                    sp.irisR = 0.70f;
                    sp.sclera = new Color(0.34f, 0.05f, 0.05f);
                    sp.vein = new Color(0.08f, 0.0f, 0.0f);
                    sp.inner = new Color(1.0f, 0.30f, 0.16f);
                    sp.outer = new Color(0.55f, 0.02f, 0.02f);
                    sp.pupil = new Color(0.03f, 0.0f, 0.0f);
                    sp.slit = false; sp.pupilW = 0.20f;
                    sp.highlight = 0.5f;
                    break;
                case EyeStyle.Pale:
                    sp.size = 128;
                    sp.irisR = 0.46f;
                    sp.sclera = new Color(0.93f, 0.91f, 0.87f);
                    sp.vein = new Color(0.70f, 0.12f, 0.10f);
                    sp.inner = new Color(0.58f, 0.52f, 0.40f);
                    sp.outer = new Color(0.20f, 0.17f, 0.13f);
                    sp.pupil = new Color(0.01f, 0.01f, 0.01f);
                    sp.slit = false; sp.pupilW = 0.17f;
                    sp.fibers = 30;
                    break;
                case EyeStyle.Abyss:
                case EyeStyle.AbyssNarrow:
                    sp.size = 512;
                    sp.irisR = 1.0f;
                    sp.inner = new Color(1.0f, 0.80f, 0.28f);
                    sp.outer = new Color(0.50f, 0.02f, 0.03f);
                    sp.pupil = new Color(0.0f, 0.0f, 0.0f);
                    sp.slit = true;
                    sp.pupilW = s == EyeStyle.Abyss ? 0.11f : 0.04f;
                    sp.pupilH = 0.80f;
                    sp.fibers = 72; sp.rings = 7; sp.highlight = 0.55f;
                    break;
            }
            return sp;
        }

        static Texture2D Draw(Spec sp)
        {
            int N = sp.size;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            var px = new Color32[N * N];
            var rnd = new System.Random(sp.seed);

            // 핏줄: 가장자리에서 홍채 쪽으로 구불구불 들어오는 선
            const int V = 16;
            var va = new float[V]; var vf = new float[V]; var vp = new float[V]; var vl = new float[V];
            for (int k = 0; k < V; k++)
            {
                va[k] = (float)(rnd.NextDouble() * Mathf.PI * 2);
                vf[k] = 6f + (float)rnd.NextDouble() * 10f;
                vp[k] = (float)rnd.NextDouble() * 6.28f;
                vl[k] = 0.35f + (float)rnd.NextDouble() * 0.6f;   // 얼마나 깊이 들어오나
            }
            float fibPhase = (float)rnd.NextDouble() * 6.28f;
            Vector2 hl = new Vector2(-0.34f, 0.36f) * sp.irisR;

            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ang = Mathf.Atan2(v, u);
                    Color c;
                    if (r > 1f) { px[y * N + x] = new Color32(0, 0, 0, 255); continue; }

                    if (r > sp.irisR)
                    {
                        // 흰자
                        float rr = (r - sp.irisR) / Mathf.Max(0.001f, 1f - sp.irisR);
                        c = sp.sclera;
                        float vein = 0f;
                        for (int k = 0; k < V; k++)
                        {
                            if (rr < 1f - vl[k]) continue;
                            float ca = va[k] + 0.22f * Mathf.Sin(rr * vf[k] + vp[k]);
                            float d = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, ca * Mathf.Rad2Deg)) * Mathf.Deg2Rad * r;
                            float w = 0.006f + 0.014f * rr;
                            float s = Mathf.Exp(-(d * d) / (w * w)) * Mathf.SmoothStep(0f, 1f, (rr - (1f - vl[k])) / 0.25f);
                            if (s > vein) vein = s;
                        }
                        c = Color.Lerp(c, sp.vein, vein * 0.85f);
                        // 공처럼 보이게 가장자리 어둡게
                        float shade = 1f - sp.scleraShade * Mathf.Pow(rr, 2.2f);
                        c *= shade;
                        // 홍채 바로 바깥 그림자
                        c *= 1f - 0.35f * Mathf.Exp(-rr * 9f);
                    }
                    else
                    {
                        float t = r / sp.irisR;
                        // 방사 무늬
                        float fib = 0.5f + 0.5f * Mathf.Sin(ang * sp.fibers + 2.6f * Mathf.Sin(ang * 3f + t * 6f + fibPhase));
                        float fib2 = 0.5f + 0.5f * Mathf.Sin(ang * sp.fibers * 0.47f + t * 9f);
                        c = Color.Lerp(sp.inner, sp.outer, Mathf.Pow(t, 0.85f));
                        c *= 0.70f + 0.30f * (fib * 0.65f + fib2 * 0.35f);
                        if (sp.rings > 0) c *= 0.74f + 0.26f * (0.5f + 0.5f * Mathf.Cos(t * sp.rings * Mathf.PI * 2f));
                        // 동공 둘레 밝은 띠
                        c += sp.inner * 0.30f * Mathf.Exp(-Mathf.Pow((t - 0.36f) / 0.07f, 2f));
                        // 홍채 가장자리 어두운 테
                        c *= 1f - 0.72f * Mathf.SmoothStep(0.84f, 1.0f, t);
                    }

                    // 동공
                    float pd;
                    if (sp.slit)
                    {
                        float pw = sp.pupilW * sp.irisR, ph = sp.pupilH * sp.irisR;
                        // 세로로 찢어진 동공: 위아래가 뾰족한 렌즈 모양
                        float yy = v / ph;
                        float halfW = pw * Mathf.Sqrt(Mathf.Max(0f, 1f - yy * yy)) * (1f - 0.25f * yy * yy);
                        pd = Mathf.Abs(yy) >= 1f ? 2f : Mathf.Abs(u) / Mathf.Max(0.0001f, halfW);
                    }
                    else pd = r / (sp.pupilW * Mathf.Max(0.2f, sp.irisR));
                    if (pd < 1.15f)
                    {
                        float k = Mathf.SmoothStep(1.15f, 0.9f, pd);
                        Color pc = sp.pupil;
                        if (sp.irisR >= 0.99f) pc = Color.Lerp(new Color(0.22f, 0.0f, 0.02f), sp.pupil, Mathf.Clamp01(1f - pd)); // 심연: 안쪽에 핏빛
                        c = Color.Lerp(c, pc, k);
                    }

                    // 맺힌 빛
                    if (sp.highlight > 0f)
                    {
                        float hd = (new Vector2(u, v) - hl).sqrMagnitude;
                        c += Color.white * sp.highlight * Mathf.Exp(-hd / (0.0035f * sp.irisR + 0.0008f));
                    }
                    c.a = 1f;
                    px[y * N + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>검붉은 살결 + 핏줄 (촉수·본체 몸통). 발광맵으로 쓰면 핏줄만 빛난다.</summary>
        public static Texture2D Flesh()
        {
            if (flesh != null) return flesh;
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, name = "Flesh" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float n = Noise(u * 8f, v * 8f, 1) * 0.6f + Noise(u * 23f, v * 23f, 2) * 0.4f;
                    // 핏줄: 뒤틀린 사인 등고선
                    float w = Mathf.Sin((u * 9f + Noise(u * 4f, v * 4f, 3) * 2.2f) * Mathf.PI * 2f) * Mathf.Sin((v * 7f + Noise(v * 5f, u * 5f, 4) * 2f) * Mathf.PI * 2f);
                    float vein = Mathf.SmoothStep(0.86f, 1f, Mathf.Abs(w));
                    Color c = Color.Lerp(new Color(0.10f, 0.02f, 0.03f), new Color(0.26f, 0.05f, 0.06f), n);
                    c = Color.Lerp(c, new Color(0.85f, 0.12f, 0.08f), vein * 0.85f);
                    c.a = 1f;
                    px[y * N + x] = c;
                }
            tex.SetPixels32(px); tex.Apply(true, true);
            flesh = tex;
            return tex;
        }

        /// <summary>세로 빛 띠의 발광맵: 가운데 밝고 위아래로 사라짐 (맨눈 빛기둥).</summary>
        public static Texture2D GlowFade()
        {
            if (glowFade != null) return glowFade;
            const int H = 128;
            var tex = new Texture2D(4, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "GlowFade" };
            var px = new Color32[4 * H];
            for (int y = 0; y < H; y++)
            {
                float v = y / (H - 1f);
                float k = Mathf.SmoothStep(0f, 1f, v / 0.25f) * Mathf.SmoothStep(0f, 1f, (1f - v) / 0.12f);
                var c = new Color(1f, 0.86f, 0.55f) * k; c.a = 1f;
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            glowFade = tex;
            return tex;
        }

        // 값 노이즈 (부드러운 보간)
        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + s * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static float Noise(float x, float y, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            float a = Hash(xi, yi, s), b = Hash(xi + 1, yi, s), c = Hash(xi, yi + 1, s), d = Hash(xi + 1, yi + 1, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
    }
}
