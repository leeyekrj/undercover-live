using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UndercoverLive
{
    /// <summary>
    /// 게임 단계 흐름: Menu → Intro → Playing ⇄ Paused → Ended.
    /// 씬이 열릴 때마다 한 번 만들어져(RuntimeInitializeOnLoadMethod + sceneLoaded) 화면들을 코드로 짓고,
    /// 단계에 따라 시간(timeScale)·커서·소리 멈춤을 정한다. Esc 처리도 여기 한 곳에서 한다.
    /// </summary>
    public class UIFlow : MonoBehaviour
    {
        public static UIFlow Instance { get; private set; }
        /// <summary>이번 판에서 Playing에 처음 들어간 순간의 Time.time (결과 화면의 클립 시각 표시용).</summary>
        public static float PlayStartTime { get; private set; }

        public MenuCamera MenuCam { get; private set; }
        public MainMenu Menu { get; private set; }
        public IntroSequence Intro { get; private set; }
        public PauseMenu Pause { get; private set; }
        public ControlsPanel Controls { get; private set; }
        public SettingsPanel Settings { get; private set; }
        public CaughtScreen Caught { get; private set; }

        bool playStarted, hadLock;

        // ---------- 씬마다 한 번 만들기 ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            PlayStartTime = 0f;
            restarting = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;   // R·처음부터로 씬을 다시 불러올 때
            Create();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Create();
        }

        static void Create()
        {
            if (FindAnyObjectByType<UIFlow>() != null) return;
            // 게임 씬에서만 (폰이나 진행 스크립트가 있는 씬)
            if (FindAnyObjectByType<PhoneController>() == null && FindAnyObjectByType<GameFlow>() == null) return;
            new GameObject("UIFlow").AddComponent<UIFlow>();
        }

        // ---------- 수명 ----------

        void Awake()
        {
            Instance = this;
            UIKit.EnsureEventSystem();

            MenuCam = Child<MenuCamera>("MenuCameraRig");
            Controls = Child<ControlsPanel>("ControlsPanel");
            Settings = Child<SettingsPanel>("SettingsPanel");
            Menu = Child<MainMenu>("MainMenu");
            Intro = Child<IntroSequence>("IntroSequence");
            Pause = Child<PauseMenu>("PauseMenu");
            Caught = Child<CaughtScreen>("CaughtScreen");

            GameEvents.PhaseChanged += OnPhase;

            bool skip = GameEvents.SkipMenuOnce;
            GameEvents.SkipMenuOnce = false;
            var first = skip ? GamePhase.Intro : GamePhase.Menu;
            var prev = GameEvents.Phase;
            GameEvents.SetPhase(first);
            if (prev == first) OnPhase(prev, first);   // 이미 같은 단계면 이벤트가 안 오므로 직접 적용
        }

        T Child<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        void OnDestroy()
        {
            GameEvents.PhaseChanged -= OnPhase;
            if (Instance == this) Instance = null;
        }

        // ---------- 단계 ----------

        /// <summary>메인 '잠입 시작'.</summary>
        public void StartGame()
        {
            if (GameEvents.Phase == GamePhase.Menu) GameEvents.SetPhase(GamePhase.Intro);
        }

        public void Resume()
        {
            if (GameEvents.Phase == GamePhase.Paused) GameEvents.SetPhase(GamePhase.Playing);
        }

        static bool restarting;

        /// <summary>
        /// 씬을 다시 불러오는 유일한 길(R·다시 하기·메인으로·처음부터). skipMenu = true면 메인을 건너뛰고 도입부터.
        /// '다시 준비하는 중'을 한 프레임 그린 뒤, 클립 기록·정적 이벤트·설정 구독을 비우고 불러온다.
        /// </summary>
        public static void Restart(bool skipMenu)
        {
            if (restarting) return;
            restarting = true;
            var c = UIKit.Canvas("ReloadCanvas", 270);
            var bg = UIKit.Image("Bg", c.transform, UIParts.Bg);
            UIKit.Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            var t = UIParts.Label(c.transform, UIStrings.Reloading, 18, UIParts.Ink(0.72f), true, TextAnchor.MiddleCenter);
            UIKit.Stretch(t.rectTransform);
            UIKit.Run(ReloadCo(skipMenu));
        }

        static IEnumerator ReloadCo(bool skipMenu)
        {
            yield return null;   // 안내가 한 번 그려지게
            yield return null;
            GameEvents.ResetForReload();       // Phase = Menu, 이벤트 비움 (SkipMenuOnce는 유지)
            GameEvents.SkipMenuOnce = skipMenu;
            SettingsStore.ResetForReload();
            CaptureLog.Clear();
            Watcher.CrowdFocus = 1f;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            restarting = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        static void Apply(GamePhase p)
        {
            bool playing = p == GamePhase.Playing;
            Time.timeScale = playing ? 1f : 0f;
            AudioListener.pause = !playing;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
        }

        void OnPhase(GamePhase prev, GamePhase now)
        {
            Apply(now);
            hadLock = false;
            switch (now)
            {
                case GamePhase.Menu:
                    MenuCam.Begin();
                    Pause.Hide();
                    Intro.Stop();
                    Menu.Show();
                    break;
                case GamePhase.Intro:
                    MenuCam.Begin();
                    Menu.Hide();
                    Intro.Play();
                    break;
                case GamePhase.Playing:
                    Menu.Hide();
                    Pause.Hide();
                    Intro.Stop();
                    MenuCam.End();
                    if (!playStarted) { playStarted = true; PlayStartTime = Time.time; }
                    break;
                case GamePhase.Paused:
                    Pause.Show();
                    break;
                case GamePhase.Ended:
                    Pause.Hide();
                    Controls.Close();
                    Settings.Close();
                    break;
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;
            var p = GameEvents.Phase;

            if (esc)
            {
                if (Settings.IsOpen) Settings.Close();
                else if (Controls.IsOpen) Controls.Close();
                else if (p == GamePhase.Playing) GameEvents.SetPhase(GamePhase.Paused);
                else if (p == GamePhase.Paused) GameEvents.SetPhase(GamePhase.Playing);
                else if (p == GamePhase.Intro) Intro.Skip();
                // Menu: 할 일 없음 / Ended: 결과·발각 화면이 직접 처리
                return;
            }

            // 웹에서는 Esc가 브라우저의 포인터 잠금만 풀고 키가 게임에 안 올 때가 있다 → 잠금이 풀리면 멈춤으로
            if (GameEvents.Phase == GamePhase.Playing)
            {
                if (Cursor.lockState == CursorLockMode.Locked) hadLock = true;
                else if (hadLock) { hadLock = false; GameEvents.SetPhase(GamePhase.Paused); }
            }
        }

        void OnApplicationFocus(bool focus)
        {
            // 창을 떠나면 멈춤 (잠금이 풀린 채 돌아가지 않게)
            if (!focus && GameEvents.Phase == GamePhase.Playing) GameEvents.SetPhase(GamePhase.Paused);
        }
    }
}
