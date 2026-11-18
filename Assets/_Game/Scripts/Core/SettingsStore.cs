using System;
using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// 플레이어 설정(마우스 감도·소리 크기·밝기). PlayerPrefs에 저장하되 웹에서 저장소가 막혀도 동작하게 모든 읽기·쓰기를 감싼다.
    /// 설정 화면(UI 담당)이 값을 바꾸고, 시점(SeatedLook)·밝기(폰/HUD 담당)·소리가 Changed를 받아 적용한다.
    /// </summary>
    public static class SettingsStore
    {
        const string KSens = "ul.mouseSensitivity", KVol = "ul.volume", KBright = "ul.brightness";
        static bool loaded;
        static float sens = 1f, vol = 0.9f, bright = 0f;

        public static event Action Changed;

        /// <summary>마우스 감도 배율 0.3~2.0 (기본 1).</summary>
        public static float MouseSensitivity { get { Load(); return sens; } set { Load(); sens = Mathf.Clamp(value, 0.3f, 2f); Save(KSens, sens); } }
        /// <summary>소리 크기 0~1 (기본 0.9). 바꾸면 AudioListener.volume에 바로 적용.</summary>
        public static float Volume { get { Load(); return vol; } set { Load(); vol = Mathf.Clamp01(value); AudioListener.volume = vol; Save(KVol, vol); } }
        /// <summary>밝기 보정 -1~+1 (기본 0). 노출값 보정으로 쓴다.</summary>
        public static float Brightness { get { Load(); return bright; } set { Load(); bright = Mathf.Clamp(value, -1f, 1f); Save(KBright, bright); } }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                sens = PlayerPrefs.GetFloat(KSens, 1f);
                vol = PlayerPrefs.GetFloat(KVol, 0.9f);
                bright = PlayerPrefs.GetFloat(KBright, 0f);
            }
            catch (Exception) { }
            AudioListener.volume = vol;
        }

        static void Save(string key, float v)
        {
            try { PlayerPrefs.SetFloat(key, v); PlayerPrefs.Save(); } catch (Exception) { }
            Changed?.Invoke();
        }

        /// <summary>씬을 다시 불러오기 직전에 부른다: Changed 구독을 비운다(값은 그대로).</summary>
        public static void ResetForReload() { Changed = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplyOnStart() { Load(); AudioListener.volume = vol; }
    }
}
