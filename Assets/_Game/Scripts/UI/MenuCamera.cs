using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UndercoverLive
{
    /// <summary>
    /// 메인·도입 동안만 쓰는 카메라. 맨눈 카메라 설정(cullingMask·URP 후처리)을 복사해
    /// 예배당 뒷줄에서 무대 쪽으로 unscaled 시간으로 아주 천천히 밀고 들어간다(20초에 1.6m, 끝에서 천천히 멈춤).
    /// 그동안 맨눈 카메라는 켜 둔 채 아무것도 그리지 않게 한다(Camera.main이 바뀌지 않게).
    /// 도입 마지막에 HandOff로 맨눈 자리까지 내려앉은 뒤 원래 카메라로 넘긴다.
    /// </summary>
    public class MenuCamera : MonoBehaviour
    {
        [Tooltip("시작 위치: 맨눈 자리에서 뒤로 몇 m")] public float backDistance = 3.4f;
        [Tooltip("시작 위치: 맨눈 높이에서 위로 몇 m (앉은 머리들 위)")] public float rise = 0.6f;
        public float driftDistance = 1.6f;
        public float driftSeconds = 20f;
        [Tooltip("교주를 화면 가운데보다 오른쪽에 두려고 시선을 왼쪽으로 비키는 거리(m)")] public float lookOffsetLeft = 1.1f;

        Camera cam, eye;
        UniversalAdditionalCameraData eyeData;
        int savedMask;
        bool savedPost, saved;
        Vector3 startPos, dir, lookAt, focus;
        bool hasFocus;
        float t;
        bool handing;

        public bool Active { get; private set; }
        public Camera Cam => cam;
        /// <summary>교주 머리 근처(메인 화면 TARGET 상자용). 없으면 false.</summary>
        public bool TryGetFocus(out Vector3 p) { p = focus; return hasFocus; }

        public void Begin()
        {
            if (Active) return;
            var pc = PhoneController.Instance;
            eye = pc != null && pc.EyeCamera != null ? pc.EyeCamera : Camera.main;
            if (eye == null) return;

            if (cam == null)
            {
                var go = new GameObject("MenuCamera");
                go.transform.SetParent(transform, false);
                cam = go.AddComponent<Camera>();
            }
            cam.CopyFrom(eye);
            cam.gameObject.tag = "Untagged";
            cam.targetTexture = null;
            cam.depth = eye.depth + 1f;
            eyeData = eye.GetUniversalAdditionalCameraData();
            var data = cam.GetUniversalAdditionalCameraData();
            if (eyeData != null && data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = eyeData.antialiasing;
                data.antialiasingQuality = eyeData.antialiasingQuality;
                data.renderShadows = eyeData.renderShadows;
                data.volumeLayerMask = eyeData.volumeLayerMask;
                data.volumeTrigger = eyeData.volumeTrigger;
                data.stopNaN = eyeData.stopNaN;
                data.dithering = eyeData.dithering;
            }

            // 출발 자세: 맨눈 자리에서 뒤로·위로
            var ep = eye.transform.position;
            var ef = eye.transform.forward;
            dir = new Vector3(ef.x, 0f, ef.z);
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir.Normalize();
            hasFocus = FindLeader(out focus);
            if (!hasFocus) focus = ep + dir * 14f + Vector3.up * 0.4f;
            var right = Vector3.Cross(Vector3.up, dir).normalized;
            lookAt = focus - right * lookOffsetLeft - Vector3.up * 0.25f;
            startPos = ep - dir * backDistance + Vector3.up * rise;
            t = 0f;
            handing = false;
            Pose(0f);

            // 맨눈 카메라는 켠 채로 그리는 것만 끈다
            if (!saved)
            {
                savedMask = eye.cullingMask;
                savedPost = eyeData != null && eyeData.renderPostProcessing;
                saved = true;
            }
            eye.cullingMask = 0;
            if (eyeData != null) eyeData.renderPostProcessing = false;

            cam.enabled = true;
            Active = true;
        }

        static bool FindLeader(out Vector3 p)
        {
            foreach (var w in FindObjectsByType<Watcher>())
                if (w != null && w.kind == WatcherKind.Leader) { p = w.EyePosition; return true; }
            p = default;
            return false;
        }

        void Pose(float time)
        {
            if (cam == null) return;
            float k = Mathf.Clamp01(time / Mathf.Max(0.01f, driftSeconds));
            float e = 1f - (1f - k) * (1f - k);           // 끝에서 천천히 멈춤
            var pos = startPos + dir * (driftDistance * e);
            pos.y += Mathf.Sin(time * 0.45f) * 0.012f;     // 숨 쉬듯 아주 작은 흔들림
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(lookAt - pos, Vector3.up);
        }

        void LateUpdate()
        {
            if (!Active || handing) return;
            t += Time.unscaledDeltaTime;
            Pose(t);
        }

        /// <summary>맨눈 자리로 내려앉은 뒤(dur초) 원래 카메라로 넘긴다.</summary>
        public void HandOff(float dur, Action done)
        {
            if (!Active || cam == null || eye == null) { End(); done?.Invoke(); return; }
            handing = true;
            StopAllCoroutines();
            StartCoroutine(HandOffCo(dur, done));
        }

        IEnumerator HandOffCo(float dur, Action done)
        {
            var p0 = cam.transform.position;
            var r0 = cam.transform.rotation;
            float f0 = cam.fieldOfView;
            float k = 0f;
            while (k < 1f)
            {
                k += Time.unscaledDeltaTime / Mathf.Max(0.01f, dur);
                float e = UIParts.EaseInOut(k);
                if (eye == null) break;
                cam.transform.position = Vector3.Lerp(p0, eye.transform.position, e);
                cam.transform.rotation = Quaternion.Slerp(r0, eye.transform.rotation, e);
                cam.fieldOfView = Mathf.Lerp(f0, eye.fieldOfView, e);
                yield return null;
            }
            End();
            done?.Invoke();
        }

        /// <summary>메뉴 카메라를 끄고 맨눈 카메라를 되돌린다.</summary>
        public void End()
        {
            StopAllCoroutines();
            handing = false;
            if (eye != null && saved)
            {
                eye.cullingMask = savedMask;
                if (eyeData != null) eyeData.renderPostProcessing = savedPost;
            }
            saved = false;
            if (cam != null) cam.enabled = false;
            Active = false;
        }

        void OnDestroy()
        {
            if (Active) End();
        }
    }
}
