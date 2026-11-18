using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UndercoverLive
{
    /// <summary>
    /// 화면 밝기 (Paul 판정: "너무 어둡다"). 볼륨 프로파일 에셋은 건드리지 않고 런타임 복제본(Volume.profile)만 바꾼다.
    /// - 맨눈(PP_Eye): 노출 +0.8, 비네트 약하게
    /// - 폰(PP_Phone): 노출 +0.35
    /// - 공통(PP_Global): 블룸 문턱 올리고 세기 내림, 톤매핑 ACES 확인
    /// - 환경광 1.7배
    /// - 설정의 밝기(SettingsStore.Brightness -1~+1)를 두 노출에 ±1 더한다
    /// - 교주 의자(Throne)가 무대 조명에 '빛나는 흰 판'으로 보이던 문제: 짙은 상아색·덜 매끈하게, 등받이를 낮추고 위를 둥글게
    /// </summary>
    public class SceneLook : MonoBehaviour
    {
        [Header("밝기 (EV)")]
        public float eyeExposureBoost = 0.8f;
        public float phoneExposureBoost = 0.35f;
        public float ambientScale = 1.7f;
        public float eyeVignette = 0.2f;
        [Header("빛 번짐")]
        public float bloomThreshold = 1.1f;
        public float bloomIntensity = 0.5f;
        [Header("교주 의자")]
        public Color throneColor = new Color(0.541f, 0.498f, 0.431f);   // #8a7f6e
        public float throneSmoothness = 0.15f;
        public float throneBackHeight = 0.7f;

        ColorAdjustments eyeCA, phoneCA;
        float eyeBase, phoneBase;
        bool applied;

        void Start()
        {
            ApplyVolumes();
            ApplyAmbient();
            FixThrone();
            SettingsStore.Changed += ApplyBrightness;
            ApplyBrightness();
        }

        void OnDestroy()
        {
            SettingsStore.Changed -= ApplyBrightness;
        }

        void ApplyVolumes()
        {
            foreach (var v in FindObjectsByType<Volume>())
            {
                if (v == null || v.sharedProfile == null) continue;
                var p = v.profile;   // 처음 읽을 때 sharedProfile을 복제한 런타임 인스턴스가 된다 (에셋 불변)
                string n = v.gameObject.name;
                if (n.Contains("Eye"))
                {
                    if (p.TryGet(out eyeCA)) eyeBase = eyeCA.postExposure.value + eyeExposureBoost;
                    if (p.TryGet(out Vignette vig)) { vig.intensity.overrideState = true; vig.intensity.value = Mathf.Min(vig.intensity.value, eyeVignette); }
                }
                else if (n.Contains("Phone"))
                {
                    if (p.TryGet(out phoneCA)) phoneBase = phoneCA.postExposure.value + phoneExposureBoost;
                }
                if (p.TryGet(out Bloom bloom))
                {
                    bloom.threshold.overrideState = true;
                    bloom.threshold.value = Mathf.Max(bloom.threshold.value, bloomThreshold);
                    bloom.intensity.overrideState = true;
                    bloom.intensity.value = Mathf.Min(bloom.intensity.value, bloomIntensity);
                }
                if (p.TryGet(out Tonemapping tm) && tm.mode.value == TonemappingMode.None)
                {
                    tm.mode.overrideState = true;
                    tm.mode.value = TonemappingMode.ACES;   // 무대 하이라이트가 하얗게 날지 않게
                }
            }
            applied = true;
        }

        void ApplyBrightness()
        {
            if (!applied) return;
            float b = SettingsStore.Brightness;   // -1 ~ +1
            if (eyeCA != null) { eyeCA.postExposure.overrideState = true; eyeCA.postExposure.value = eyeBase + b; }
            if (phoneCA != null) { phoneCA.postExposure.overrideState = true; phoneCA.postExposure.value = phoneBase + b; }
        }

        void ApplyAmbient()
        {
            switch (RenderSettings.ambientMode)
            {
                case AmbientMode.Flat:
                {
                    var c = RenderSettings.ambientLight * ambientScale;
                    c.a = 1f;
                    RenderSettings.ambientLight = c;
                    var sh = new SphericalHarmonicsL2();
                    sh.AddAmbientLight(c);
                    RenderSettings.ambientProbe = sh;
                    break;
                }
                case AmbientMode.Trilight:
                    RenderSettings.ambientSkyColor *= ambientScale;
                    RenderSettings.ambientEquatorColor *= ambientScale;
                    RenderSettings.ambientGroundColor *= ambientScale;
                    break;
                default:
                    RenderSettings.ambientIntensity *= ambientScale;
                    break;
            }
        }

        void FixThrone()
        {
            var throne = GameObject.Find("Throne");
            if (throne == null) return;
            var t = throne.transform;
            Material ivory = null;
            foreach (var n in new[] { "Seat", "Back", "Base" })
            {
                var part = t.Find(n);
                var r = part != null ? part.GetComponent<Renderer>() : null;
                if (r == null) continue;
                if (ivory == null)
                {
                    ivory = new Material(r.sharedMaterial) { name = "ThroneIvory (runtime)" };
                    SetLit(ivory, throneColor, throneSmoothness);
                }
                r.sharedMaterial = ivory;
            }
            foreach (var n in new[] { "ArmL", "ArmR" })
            {
                var part = t.Find(n);
                var r = part != null ? part.GetComponent<Renderer>() : null;
                if (r == null) continue;
                var m = r.material;   // 런타임 인스턴스
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Min(m.GetFloat("_Smoothness"), 0.45f));
            }

            // 등받이: 높이 70%(바닥 높이는 그대로), 위에 납작한 원기둥을 얹어 둥근 머리
            var back = t.Find("Back");
            if (back != null && ivory != null)
            {
                var s = back.localScale;
                float bottom = back.localPosition.y - s.y * 0.5f;
                float h = s.y * throneBackHeight;
                back.localScale = new Vector3(s.x, h, s.z);
                back.localPosition = new Vector3(back.localPosition.x, bottom + h * 0.5f, back.localPosition.z);

                var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cap.name = "BackTop (runtime)";
                var col = cap.GetComponent<Collider>();
                if (col != null) Destroy(col);   // 가림·시선 판정에 영향 주지 않게
                cap.layer = back.gameObject.layer;
                cap.transform.SetParent(t, false);
                // 원기둥 축을 앞뒤(z)로 눕힌다: 지름 = 등받이 폭, 두께 = 등받이 두께
                cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                cap.transform.localScale = new Vector3(s.x, s.z * 0.5f, s.x * 0.62f);
                cap.transform.localPosition = new Vector3(back.localPosition.x, bottom + h, back.localPosition.z);
                cap.GetComponent<Renderer>().sharedMaterial = ivory;
            }
        }

        static void SetLit(Material m, Color c, float smooth)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        }
    }
}
