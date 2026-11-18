using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 기이현상 화면 연출 (자체 캔버스, 그리기 순서 145 — 공통 규칙 표의 '역관측 연출').
    /// - Glitch: 폰 화면에 짧은 치지직 가로줄 노이즈
    /// - ShakePhone: 폰 카메라 흔들림(좌우 또는 무작위). 판정에는 영향 없음(그리기 직전에만 흔들고 다음 프레임 첫머리에 되돌림)
    /// - SetStare: 역관측 중 맨눈 미세 진동·가장자리 붉은 맥박, 폰 화면 위 동공 잔상·주사선
    /// - PlayEnding: 화면을 거대 동공이 덮는 1.2초 연출 → 콜백
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class EntityStareFX : MonoBehaviour
    {
        static EntityStareFX inst;
        public static bool Ending => inst != null && inst.ending;

        Canvas canvas;
        Image vignette, afterRing, afterSlit, endIris, endSlit, endFill;
        Image[] bars;
        float glitchUntil;
        float phoneShakeUntil, phoneShakeDur, phoneShakeAmp; bool phoneShakeHorizontal;
        float dread, dreadTarget; bool closeAimed;
        bool ending;
        float endT;

        Transform shakenPhone, shakenEye;
        Quaternion savedPhoneRot, savedEyeRot;

        public static EntityStareFX Ensure()
        {
            if (inst != null) return inst;
            var go = new GameObject("EntityStareFX");
            inst = go.AddComponent<EntityStareFX>();
            go.AddComponent<EntityFXRestore>();
            inst.Build();
            return inst;
        }

        void OnDestroy()
        {
            RestoreCams();
            if (inst == this) inst = null;
        }

        void Build()
        {
            canvas = UIKit.Canvas("EntityFXCanvas", 145, transform);
            var gr = canvas.GetComponent<GraphicRaycaster>(); if (gr != null) gr.enabled = false;
            var root = canvas.transform;

            vignette = UIKit.Image("Dread", root, new Color(0.22f, 0f, 0.02f, 0f), UIKit.EdgeVignette(128, 0.42f));
            vignette.type = Image.Type.Simple;
            UIKit.Stretch(vignette.rectTransform);

            afterRing = UIKit.Image("AfterRing", root, new Color(0.85f, 0.35f, 0.08f, 0f), UIKit.Circle(256, 26));
            Center(afterRing.rectTransform, 980, 980);
            afterSlit = UIKit.Image("AfterSlit", root, new Color(0f, 0f, 0f, 0f), UIKit.Rounded(48));
            Center(afterSlit.rectTransform, 96, 760);

            bars = new Image[12];
            for (int i = 0; i < bars.Length; i++)
            {
                bars[i] = UIKit.Image("Bar" + i, root, Color.clear);
                var rt = bars[i].rectTransform;
                rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(1, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(0, 4); rt.anchoredPosition = Vector2.zero;
                bars[i].enabled = false;
            }

            endIris = UIKit.Image("EndIris", root, new Color(0.85f, 0.45f, 0.08f, 0f), UIKit.Circle(512));
            Center(endIris.rectTransform, 1080, 1080);
            endSlit = UIKit.Image("EndSlit", root, new Color(0f, 0f, 0f, 0f), UIKit.Rounded(64));
            endSlit.rectTransform.SetParent(endIris.rectTransform, false);
            Center(endSlit.rectTransform, 110, 900);
            endFill = UIKit.Image("EndFill", root, new Color(0f, 0f, 0f, 0f));
            UIKit.Stretch(endFill.rectTransform);
            endIris.enabled = endSlit.enabled = endFill.enabled = false;
            afterRing.enabled = afterSlit.enabled = false;
        }

        static void Center(RectTransform rt, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
        }

        // ---------- 공개 ----------
        public static void Glitch(float seconds, float strength = 1f)
        {
            var f = Ensure();
            f.glitchUntil = Mathf.Max(f.glitchUntil, Time.time + seconds);
        }

        public static void ShakePhone(float degrees, float seconds, bool horizontal)
        {
            var f = Ensure();
            f.phoneShakeAmp = degrees; f.phoneShakeDur = Mathf.Max(0.05f, seconds);
            f.phoneShakeUntil = Time.time + seconds; f.phoneShakeHorizontal = horizontal;
        }

        public static void SetStare(float dread01, bool closeAimed)
        {
            if (inst == null && dread01 <= 0f) return;
            var f = Ensure();
            f.dreadTarget = Mathf.Clamp01(dread01);
            f.closeAimed = closeAimed;
        }

        public static void PlayEnding(Action done)
        {
            var f = Ensure();
            if (f.ending) return;
            f.ending = true;
            f.StartCoroutine(f.EndingCo(done));
        }

        // ---------- 매 프레임 ----------
        void Update()
        {
            float dt = Time.deltaTime;
            dread = Mathf.MoveTowards(dread, dreadTarget, dt * (dreadTarget > dread ? 1.5f : 0.6f));

            // 가장자리 붉은 맥박 (심장 박동처럼 두 번 뛰고 쉼)
            float beat = Mathf.Repeat(Time.time * 1.25f, 1f);
            float pulse = Mathf.Exp(-beat * 9f) + 0.6f * Mathf.Exp(-Mathf.Abs(beat - 0.22f) * 14f);
            var vc = vignette.color; vc.a = dread * (0.35f + 0.35f * pulse) + (ending ? 0.6f : 0f);
            vignette.color = vc;

            // 폰 화면 위 동공 잔상 (코앞까지 내려왔을 때)
            bool after = closeAimed && !ending;
            afterRing.enabled = afterSlit.enabled = after;
            if (after)
            {
                float k = 0.10f + 0.06f * Mathf.Sin(Time.time * 6f);
                afterRing.color = new Color(0.85f, 0.35f, 0.08f, k);
                afterSlit.color = new Color(0f, 0f, 0f, k * 1.6f);
                float s = 1f + 0.03f * Mathf.Sin(Time.time * 2.3f);
                afterRing.rectTransform.localScale = new Vector3(s, s, 1f);
            }

            // 주사선·치지직
            bool glitch = (Time.time < glitchUntil && EntityKit.PhoneAimed) || ending || (closeAimed && UnityEngine.Random.value < 0.25f);
            for (int i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                bool on = glitch && (ending || Time.time < glitchUntil ? UnityEngine.Random.value < 0.8f : i < 3);
                b.enabled = on;
                if (!on) continue;
                var rt = b.rectTransform;
                rt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(-540f, 540f));
                rt.sizeDelta = new Vector2(0, UnityEngine.Random.Range(2f, 16f));
                float a = UnityEngine.Random.Range(0.12f, 0.4f);
                int c = UnityEngine.Random.Range(0, 3);
                b.color = c == 0 ? new Color(1f, 1f, 1f, a) : c == 1 ? new Color(1f, 0.18f, 0.2f, a) : new Color(0.2f, 1f, 0.95f, a);
            }
        }

        IEnumerator EndingCo(Action done)
        {
            const float dur = 1.2f;
            endIris.enabled = endSlit.enabled = endFill.enabled = true;
            endT = 0f;
            while (endT < dur)
            {
                endT += Time.deltaTime;
                float k = Mathf.Clamp01(endT / dur);
                float grow = k * k * (3f - 2f * k);
                // 홍채가 화면을 덮고, 세로 동공이 벌어지며 전부 검게
                float s = Mathf.Lerp(0.55f, 3.4f, grow);
                endIris.rectTransform.localScale = new Vector3(s, s, 1f);
                endIris.color = Color.Lerp(new Color(0.95f, 0.6f, 0.12f, 0.0f), new Color(0.55f, 0.03f, 0.04f, 1f), Mathf.Clamp01(k * 1.8f));
                endSlit.color = new Color(0f, 0f, 0f, Mathf.Clamp01(k * 2.2f));
                float w = Mathf.Lerp(0.12f, 1.25f, Mathf.Pow(k, 2.2f));
                endSlit.rectTransform.sizeDelta = new Vector2(1080f * w, 1000f);
                endFill.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, (k - 0.55f) / 0.45f));
                yield return null;
            }
            endFill.color = Color.black;
            done?.Invoke();
        }

        // ---------- 카메라 흔들림 ----------
        void LateUpdate()
        {
            var pc = PhoneController.Instance;
            if (pc == null) return;
            float tm = Time.time;

            Vector3 e = Vector3.zero;
            if (tm < phoneShakeUntil)
            {
                float k = (phoneShakeUntil - tm) / phoneShakeDur;
                if (phoneShakeHorizontal) e.y = Mathf.Sin(tm * 42f) * phoneShakeAmp * k;
                else e = new Vector3(Mathf.PerlinNoise(tm * 30f, 0.1f) - 0.5f, Mathf.PerlinNoise(0.4f, tm * 30f) - 0.5f, 0f) * 2f * phoneShakeAmp * k;
            }
            if (closeAimed) e += new Vector3(Mathf.PerlinNoise(tm * 7f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, tm * 7f) - 0.5f, Mathf.PerlinNoise(tm * 5f, 3.3f) - 0.5f) * 1.4f;
            if (ending) e += new Vector3(Mathf.PerlinNoise(tm * 25f, 5f) - 0.5f, Mathf.PerlinNoise(6f, tm * 25f) - 0.5f, 0f) * 5f;
            if (e != Vector3.zero && pc.PhoneCamera != null && shakenPhone == null)
            {
                shakenPhone = pc.PhoneCamera.transform;
                savedPhoneRot = shakenPhone.localRotation;
                shakenPhone.localRotation = savedPhoneRot * Quaternion.Euler(e);
            }

            // 맨눈: 숨 막히는 정적 속 작은 진동
            if (dread > 0.01f && pc.EyeCamera != null && shakenEye == null)
            {
                shakenEye = pc.EyeCamera.transform;
                savedEyeRot = shakenEye.localRotation;
                Vector3 q = new Vector3(Mathf.PerlinNoise(tm * 18f, 7f) - 0.5f, Mathf.PerlinNoise(8f, tm * 18f) - 0.5f, 0f) * 0.5f * dread;
                shakenEye.localRotation = savedEyeRot * Quaternion.Euler(q);
            }
        }

        /// <summary>다음 프레임 첫머리에 흔든 카메라를 되돌린다(판정·조작 코드가 흔들림을 보지 않게).</summary>
        internal static void RestoreCams()
        {
            if (inst == null) return;
            if (inst.shakenPhone != null) { inst.shakenPhone.localRotation = inst.savedPhoneRot; inst.shakenPhone = null; }
            if (inst.shakenEye != null) { inst.shakenEye.localRotation = inst.savedEyeRot; inst.shakenEye = null; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { inst = null; }
    }

    [DefaultExecutionOrder(-900)]
    public class EntityFXRestore : MonoBehaviour
    {
        void Update() { EntityStareFX.RestoreCams(); }
    }
}
