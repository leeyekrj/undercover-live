using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UndercoverLive
{
    /// <summary>
    /// 소리 담당(E): 오디오 파일 없이 코드로 만든 소리로 집회장 분위기와 효과음을 낸다.
    /// 씬이 열릴 때마다 한 번 만들어진다(RuntimeInitializeOnLoadMethod + sceneLoaded). 클립은 처음 한 번만 만들어 다시 쓴다.
    ///
    /// 배경(루프): 룸톤 · 드론(낮음 / 강림 불협) · 신도 웅성거림 · 복창 허밍 · 박수 · 괴물 울림(폰을 든 상태에서만).
    /// 효과음: 옷 스침(폰 들기·내리기·일어서기) · 셔터(확보) · 진동(몰래 상태 채팅 폭주) · 관리자 발소리(3D) · 뺏길 때 충격음 + 신호 끊김 → 정적.
    ///
    /// 읽는 상태: GameEvents(Phase·ChatCue phase_* 키·일어섬·경고·검문·압수·예배 끝), PhoneController.StateChanged, CaptureLog.Added,
    /// (보조) ServiceDirector.Blindfolded — phase_blindfold 신호가 없을 때만 눈가리개 의례로 본다.
    /// 멈춤 화면은 UI 담당이 AudioListener.pause로 소리를 멈추므로, 배경 소스는 ignoreListenerPause로 두고 단계별 크기를 여기서 직접 정한다.
    ///
    /// 공개 API
    /// - UndercoverAudio.SetEntityPresence(0~1): 거대 겹눈 존재감. 폰을 든 상태(정조준 100%, 몰래 50%)에서만 들린다. 0이면 무음.
    /// - UndercoverAudio.NotifyChatBurst(): 채팅이 몰려들 때. 몰래 상태면 폰 진동(2.5초에 한 번까지).
    /// </summary>
    [DisallowMultipleComponent]
    public class UndercoverAudio : MonoBehaviour
    {
        public static UndercoverAudio Instance { get; private set; }

        static float entityPresence;
        /// <summary>거대 겹눈 존재감 0~1 (이야기 담당이 거리에 따라 부른다).</summary>
        public static float EntityPresence => entityPresence;
        public static void SetEntityPresence(float v) => entityPresence = Mathf.Clamp01(v);

        /// <summary>채팅이 몰려듦 → 몰래 상태면 폰 진동.</summary>
        public static void NotifyChatBurst() { if (Instance != null) Instance.TryBuzz(0f); }

        // ---------- 크기 (AudioSource.volume, 배경 클립은 RMS 0.25 기준 → 0.18 ≈ -27dBFS, 0.36 ≈ -21dBFS, 0.6 ≈ -16dBFS) ----------
        const float RoomVol = 0.18f;
        const float DroneMenu = 0.14f, DroneCalm = 0.30f, DroneTense = 0.58f, DroneDisMax = 0.70f;
        const float ChantVol = 0.50f, ClapVol = 0.55f, EntityMax = 0.95f;
        const float ClothVol = 0.50f, ShutterVol = 0.60f, BuzzVol = 0.45f, ThudVol = 0.95f, StaticVol = 0.75f, StepVol = 0.85f;
        const float BuzzCooldown = 2.5f;

        // ---------- 클립 캐시 (씬을 다시 불러도 유지) ----------
        static AudioClip cRoom, cDroneLow, cDroneDis, cMurmur, cChant, cClaps, cEntity;
        static AudioClip cClothUp, cClothDown, cShutter, cBuzz, cThud, cStatic, cStepA, cStepB;
        static bool loggedSize;

        enum Rite { None, Entry, Sermon, Blindfold, Miracle, Closing }
        enum Chorus { None, ChantClap, ChantOnly, ClapOnly }

        sealed class Bed
        {
            public AudioSource src;
            public float vol, target, pitch = 1f, tauUp = 1f, tauDown = 1f;
        }

        Bed bRoom, bLow, bDis, bMurmur, bChant, bClaps, bEntity;
        Bed[] beds;
        AudioSource sfx, cloth, steps;
        AudioListener listener;
        PhoneController phone;
        PhoneState lastPhone = PhoneState.Pocket;
        ServiceDirector director;

        // 의례·상황
        Rite rite = Rite.None;
        bool riteFromFallback;
        Rite riteBeforeFallback;
        float playClock, tension = 0.1f;
        bool standing, nearStage, checkpointActive, finished;

        // 복창·박수 시간표
        Chorus chorusKind;
        bool chorusOn;
        float chorusTimer, chorusWindow, chorusOnLen, chorusOffLen, chorusGain = 1f, chantPitch = 1f;

        // 발소리
        bool walking, walkingStaff, approaching, stepAlt;
        int stepsLeft;
        float stepTimer, stepGap, stepDist, stepVolume, nextShortWalk;
        Vector3 walkDir;

        // 효과음 예약·뺏기
        float buzzAt = -1f, lastBuzz = -99f, staticAt = -1f, suppressClothUntil;
        bool confiscating, silenced;

        // ---------- 씬마다 한 번 만들기 ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            entityPresence = 0f;
            loggedSize = false;
            cRoom = cDroneLow = cDroneDis = cMurmur = cChant = cClaps = cEntity = null;
            cClothUp = cClothDown = cShutter = cBuzz = cThud = cStatic = cStepA = cStepB = null;
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
            if (FindAnyObjectByType<UndercoverAudio>() != null) return;
            if (FindAnyObjectByType<PhoneController>() == null && FindAnyObjectByType<GameFlow>() == null) return;
            new GameObject("UndercoverAudio").AddComponent<UndercoverAudio>();
        }

        // ---------- 수명 ----------

        void Awake()
        {
            Instance = this;
            entityPresence = 0f;

            bRoom = NewBed("Room", 2f, 2f);
            bLow = NewBed("DroneLow", 3f, 3f);
            bDis = NewBed("DroneDissonant", 4f, 3f);
            bMurmur = NewBed("Murmur", 0.8f, 2f);
            bChant = NewBed("Chant", 0.1f, 0.25f);
            bClaps = NewBed("Claps", 0.08f, 0.25f);
            bEntity = NewBed("Entity", 0.15f, 0.12f);
            beds = new[] { bRoom, bLow, bDis, bMurmur, bChant, bClaps, bEntity };

            sfx = NewSource(gameObject, true);      // 셔터·진동·충격·신호 끊김 (발각 화면으로 넘어가도 끝까지)
            cloth = NewSource(gameObject, true);    // 옷 스침 (피치를 매번 바꾼다)

            var stepGo = new GameObject("StaffSteps");
            stepGo.transform.SetParent(transform, false);
            steps = NewSource(stepGo, false);
            steps.spatialBlend = 1f;
            steps.rolloffMode = AudioRolloffMode.Logarithmic;
            steps.minDistance = 1.2f;
            steps.maxDistance = 30f;

            GameEvents.PhaseChanged += OnPhase;
            GameEvents.ChatCue += OnChatCue;
            GameEvents.PlayerStoodChanged += OnStood;
            GameEvents.PlayerNearStage += OnNearStage;
            GameEvents.Warning += OnWarning;
            GameEvents.CheckpointStarted += OnCheckpointStarted;
            GameEvents.CheckpointEnded += OnCheckpointEnded;
            GameEvents.Confiscated += OnConfiscated;
            GameEvents.ServiceFinished += OnServiceFinished;
            CaptureLog.Added += OnCapture;
        }

        void Start()
        {
            director = FindAnyObjectByType<ServiceDirector>();
            listener = FindAnyObjectByType<AudioListener>();
            StartCoroutine(BuildClips());
        }

        void OnDestroy()
        {
            GameEvents.PhaseChanged -= OnPhase;
            GameEvents.ChatCue -= OnChatCue;
            GameEvents.PlayerStoodChanged -= OnStood;
            GameEvents.PlayerNearStage -= OnNearStage;
            GameEvents.Warning -= OnWarning;
            GameEvents.CheckpointStarted -= OnCheckpointStarted;
            GameEvents.CheckpointEnded -= OnCheckpointEnded;
            GameEvents.Confiscated -= OnConfiscated;
            GameEvents.ServiceFinished -= OnServiceFinished;
            CaptureLog.Added -= OnCapture;
            if (phone != null) phone.StateChanged -= OnPhoneState;
            if (Instance == this) Instance = null;
        }

        Bed NewBed(string name, float tauUp, float tauDown)
        {
            var b = new Bed { tauUp = tauUp, tauDown = tauDown };
            b.src = NewSource(gameObject, true);
            return b;
        }

        static AudioSource NewSource(GameObject go, bool ignorePause)
        {
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.volume = 0f;
            s.spatialBlend = 0f;
            s.dopplerLevel = 0f;
            s.ignoreListenerPause = ignorePause;
            return s;
        }

        /// <summary>클립을 한 프레임에 하나씩 만든다(웹에서 첫 화면이 멈추지 않게). 메뉴 드론이 먼저.</summary>
        IEnumerator BuildClips()
        {
            if (cDroneLow == null) cDroneLow = UndercoverAudioSynth.DroneLow();
            StartLoop(bLow, cDroneLow);
            yield return null;
            if (cRoom == null) cRoom = UndercoverAudioSynth.RoomTone();
            StartLoop(bRoom, cRoom);
            yield return null;
            if (cClothUp == null) cClothUp = UndercoverAudioSynth.Cloth(true);
            if (cClothDown == null) cClothDown = UndercoverAudioSynth.Cloth(false);
            if (cShutter == null) cShutter = UndercoverAudioSynth.Shutter();
            if (cBuzz == null) cBuzz = UndercoverAudioSynth.Buzz();
            yield return null;
            if (cThud == null) cThud = UndercoverAudioSynth.Thud();
            if (cStatic == null) cStatic = UndercoverAudioSynth.SignalLoss();
            if (cStepA == null) cStepA = UndercoverAudioSynth.Footstep(71);
            if (cStepB == null) cStepB = UndercoverAudioSynth.Footstep(73);
            yield return null;
            if (cMurmur == null) cMurmur = UndercoverAudioSynth.Murmur();
            StartLoop(bMurmur, cMurmur);
            yield return null;
            if (cDroneDis == null) cDroneDis = UndercoverAudioSynth.DroneDissonant();
            StartLoop(bDis, cDroneDis);
            yield return null;
            if (cEntity == null) cEntity = UndercoverAudioSynth.EntityGrowl();
            StartLoop(bEntity, cEntity);
            yield return null;
            if (cChant == null) cChant = UndercoverAudioSynth.Chant();
            bChant.src.clip = cChant; bChant.src.loop = true;
            yield return null;
            if (cClaps == null) cClaps = UndercoverAudioSynth.Claps();
            bClaps.src.clip = cClaps; bClaps.src.loop = true;

            if (!loggedSize)
            {
                loggedSize = true;
                Debug.Log($"[소리] 코드로 만든 클립 {UndercoverAudioSynth.ClipCount}개, PCM 약 {UndercoverAudioSynth.TotalBytes / (1024f * 1024f):0.0}MB");
            }
        }

        static void StartLoop(Bed b, AudioClip c)
        {
            if (b == null || b.src == null || c == null) return;
            b.src.clip = c;
            b.src.loop = true;
            b.src.volume = b.vol;
            b.src.Play();
        }

        // ---------- 매 프레임 ----------

        void Update()
        {
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            BindPhone();
            if (listener == null) listener = FindAnyObjectByType<AudioListener>();

            if (GameEvents.IsPlaying)
            {
                float gdt = Time.deltaTime;
                playClock += gdt;
                CheckBlindfoldFallback();
                TickChorus(gdt);
                TickSteps(gdt);
            }

            if (staticAt > 0f && now >= staticAt) FireStatic();
            if (buzzAt > 0f && now >= buzzAt)
            {
                buzzAt = -1f;
                if (!silenced && phone != null && phone.State == PhoneState.Hidden) Play(sfx, cBuzz, BuzzVol);
            }

            ComputeTargets(dt, now);
            foreach (var b in beds) Apply(b, dt);

            // 꺼진 복창·박수는 멈춰 둔다 (다음에 같이 처음부터 틀어 박이 맞게)
            if (!chorusOn)
            {
                if (bChant.src.isPlaying && bChant.vol <= 0f) bChant.src.Stop();
                if (bClaps.src.isPlaying && bClaps.vol <= 0f) bClaps.src.Stop();
            }
        }

        void BindPhone()
        {
            if (phone != null || PhoneController.Instance == null) return;
            phone = PhoneController.Instance;
            lastPhone = phone.State;
            phone.StateChanged += OnPhoneState;
        }

        void CheckBlindfoldFallback()
        {
            if (director == null) return;
            bool blind = director.Blindfolded;
            if (blind && rite != Rite.Blindfold && rite != Rite.Miracle && rite != Rite.Closing)
            {
                riteBeforeFallback = rite;
                SetRite(Rite.Blindfold);
                riteFromFallback = true;
            }
            else if (!blind && riteFromFallback)
            {
                riteFromFallback = false;
                SetRite(riteBeforeFallback);
            }
        }

        static float RiteTension(Rite r)
        {
            switch (r)
            {
                case Rite.Entry: return 0.15f;
                case Rite.Sermon: return 0.3f;
                case Rite.Blindfold: return 0.85f;
                case Rite.Miracle: return 1f;
                case Rite.Closing: return 0.3f;
                default: return 0.12f;
            }
        }

        void ComputeTargets(float dt, float now)
        {
            float room = 0f, low = 0f, dis = 0f, mur = 0f, ent = 0f;
            float pLow = 1f, pDis = 1f, pEnt = 1f;
            float chant = 0f, claps = 0f;
            bool fast = false;

            if (silenced)
            {
                fast = true;   // 신호가 끊긴 뒤 정적
            }
            else
            {
                switch (GameEvents.Phase)
                {
                    case GamePhase.Menu:
                        low = DroneMenu; pLow = 0.94f;
                        break;
                    case GamePhase.Intro:
                        room = RoomVol; low = 0.24f; pLow = 0.97f; mur = 0.26f;
                        break;
                    case GamePhase.Paused:
                        low = DroneMenu * 0.85f; pLow = 0.95f;
                        break;
                    case GamePhase.Ended:
                        if (finished) { low = DroneMenu * 0.85f; pLow = 0.94f; }
                        break;
                    case GamePhase.Playing:
                        PlayingTargets(dt, now, out room, out low, out dis, out mur, out ent, out pLow, out pDis, out pEnt, out chant, out claps);
                        break;
                }
            }

            Set(bRoom, room, 1f, fast);
            Set(bLow, low, pLow, fast);
            Set(bDis, dis, pDis, fast);
            Set(bMurmur, mur, 1f, fast);
            Set(bEntity, ent, pEnt, fast);
            Set(bChant, chant, chantPitch, fast);
            Set(bClaps, claps, 1f, fast);
        }

        void PlayingTargets(float dt, float now, out float room, out float low, out float dis, out float mur, out float ent,
                            out float pLow, out float pDis, out float pEnt, out float chant, out float claps)
        {
            // 긴장도: 의례 단계 + 시간이 갈수록 조금씩 + 상황
            float floor = rite == Rite.Closing ? 0.25f : Mathf.Lerp(0.1f, 0.55f, playClock / 300f);
            float target = Mathf.Max(RiteTension(rite), floor);
            if (nearStage) target += 0.15f;
            if (checkpointActive) target += 0.1f;
            target = Mathf.Clamp01(target);
            tension += (target - tension) * (1f - Mathf.Exp(-dt / 4f));

            bool descent = rite == Rite.Blindfold || rite == Rite.Miracle;
            room = descent ? RoomVol * 0.65f : RoomVol;
            low = Mathf.Lerp(DroneCalm, DroneTense, tension);
            dis = Mathf.Clamp01((tension - 0.5f) / 0.5f) * DroneDisMax;
            if (descent)
            {
                // 강림: 크게 부풀며 두 드론의 음높이가 서로 어긋나 맥놀이가 일렁인다
                low *= 1f + 0.12f * Mathf.Sin(now * 0.31f);
                pLow = 0.95f + 0.012f * Mathf.Sin(now * 0.37f);
                pDis = 1f + 0.015f * Mathf.Sin(now * 0.23f + 1f);
            }
            else
            {
                pLow = Mathf.Lerp(0.97f, 1.02f, Mathf.Clamp01(tension / 0.75f));
                pDis = 1f + 0.008f * Mathf.Sin(now * 0.2f);
            }

            // 웅성거림: 설교 중엔 작게, 일어서거나 검문·무대 돌진 때 커짐
            switch (rite)
            {
                case Rite.Sermon: mur = 0.12f; break;
                case Rite.Blindfold: mur = 0.04f; break;
                case Rite.Miracle: mur = 0.05f; break;
                case Rite.Closing: mur = 0.18f; break;
                case Rite.Entry: mur = 0.24f; break;
                default: mur = 0.2f; break;
            }
            if (chorusOn) mur *= 0.5f;
            if (standing) mur = Mathf.Max(mur, descent ? 0.25f : 0.6f);
            if (checkpointActive) mur = Mathf.Max(mur, 0.5f);
            if (nearStage) mur = Mathf.Max(mur, 0.8f);
            if (walking && walkingStaff) mur = Mathf.Max(mur, 0.3f);

            // 괴물 울림: 폰 마이크만 듣는다 → 폰을 든 상태에서만
            float hold = 0f;
            if (phone != null && phone.Battery01 > 0f)
                hold = phone.State == PhoneState.Aimed ? 1f : phone.State == PhoneState.Hidden ? 0.5f : 0f;
            ent = Mathf.Pow(entityPresence, 1.2f) * hold * EntityMax;
            pEnt = 0.85f + 0.25f * entityPresence;

            // 복창·박수
            chant = chorusOn && chorusKind != Chorus.ClapOnly ? ChantVol * chorusGain : 0f;
            claps = chorusOn && chorusKind != Chorus.ChantOnly ? ClapVol * chorusGain : 0f;
        }

        static void Set(Bed b, float vol, float pitch, bool fast)
        {
            b.target = vol;
            b.pitch = pitch;
            if (fast) { b.tauUp = Mathf.Min(b.tauUp, 0.06f); b.tauDown = 0.06f; }
        }

        static void Apply(Bed b, float dt)
        {
            if (b.src == null) return;
            float tau = b.target > b.vol ? b.tauUp : b.tauDown;
            b.vol += (b.target - b.vol) * (1f - Mathf.Exp(-dt / Mathf.Max(0.01f, tau)));
            if (b.target <= 0f && b.vol < 0.0005f) b.vol = 0f;
            b.src.volume = b.vol;
            b.src.pitch = Mathf.Lerp(b.src.pitch, b.pitch, 1f - Mathf.Exp(-dt / 0.6f));
        }

        // ---------- 의례 단계 ----------

        void SetRite(Rite r)
        {
            riteFromFallback = false;
            if (r == rite) return;
            rite = r;
            PlanChorus(r);
        }

        void PlanChorus(Rite r)
        {
            StopChorus();
            chorusGain = 1f; chantPitch = 1f;
            switch (r)
            {
                case Rite.Sermon:      // 복창: 박수 + 허밍 8초(두 번), 6초 쉼, 75초 동안
                    chorusKind = Chorus.ChantClap; chorusOnLen = 8f; chorusOffLen = 6f; chorusWindow = 75f; chorusTimer = 1.5f;
                    break;
                case Rite.Blindfold:   // 눈가리개를 쓴 눈꺼풀 복창: 허밍만, 조금 낮게
                    chorusKind = Chorus.ChantOnly; chantPitch = 0.94f; chorusGain = 0.8f;
                    chorusOnLen = 8f / 0.94f; chorusOffLen = 7f; chorusWindow = 9999f; chorusTimer = 3f;
                    break;
                case Rite.Miracle:     // 강림: 드론 아래 작게
                    chorusKind = Chorus.ChantOnly; chantPitch = 0.9f; chorusGain = 0.45f;
                    chorusOnLen = 8f / 0.9f; chorusOffLen = 10f; chorusWindow = 9999f; chorusTimer = 4f;
                    break;
                case Rite.Closing:     // 폐회: 박수 한 번
                    chorusKind = Chorus.ClapOnly; chorusGain = 0.8f;
                    chorusOnLen = 8f; chorusOffLen = 0f; chorusWindow = 10f; chorusTimer = 2f;
                    break;
                default:
                    chorusKind = Chorus.None;
                    break;
            }
        }

        void TickChorus(float dt)
        {
            if (chorusKind == Chorus.None || silenced) return;
            chorusWindow -= dt;
            chorusTimer -= dt;
            if (chorusTimer > 0f) return;
            if (!chorusOn)
            {
                if (chorusWindow <= 0f) { chorusKind = Chorus.None; return; }
                StartChorus();
                chorusTimer = chorusOnLen;
            }
            else
            {
                StopChorus();
                chorusTimer = chorusOffLen;
                if (chorusOffLen <= 0f || chorusWindow <= 0f) chorusKind = Chorus.None;
            }
        }

        void StartChorus()
        {
            bool useChant = chorusKind != Chorus.ClapOnly && bChant.src.clip != null;
            bool useClaps = chorusKind != Chorus.ChantOnly && bClaps.src.clip != null;
            if (!useChant && !useClaps) return;
            chorusOn = true;
            // 같은 프레임에 처음부터 → 허밍 음절과 박수 박이 맞는다
            bChant.src.Stop(); bClaps.src.Stop();
            bChant.vol = 0f; bClaps.vol = 0f;
            bChant.src.volume = 0f; bClaps.src.volume = 0f;
            bChant.src.pitch = chantPitch; bChant.pitch = chantPitch;
            if (useChant) bChant.src.Play();
            if (useClaps) bClaps.src.Play();
        }

        void StopChorus()
        {
            chorusOn = false;   // 크기가 0이 되면 Update에서 Stop
        }

        // ---------- 이벤트 ----------

        void OnPhase(GamePhase prev, GamePhase now)
        {
            if (now == GamePhase.Paused)
            {
                if (bChant.src.isPlaying) bChant.src.Pause();
                if (bClaps.src.isPlaying) bClaps.src.Pause();
            }
            else if (prev == GamePhase.Paused)
            {
                bChant.src.UnPause();
                bClaps.src.UnPause();
            }
            if (now != GamePhase.Playing && now != GamePhase.Paused) walking = false;
        }

        void OnChatCue(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            switch (key)
            {
                case "phase_entry": SetRite(Rite.Entry); break;
                case "phase_sermon": SetRite(Rite.Sermon); break;
                case "phase_blindfold": SetRite(Rite.Blindfold); break;
                case "phase_miracle": SetRite(Rite.Miracle); break;
                case "phase_closing": SetRite(Rite.Closing); break;
                case "warn_staff_coming": StartSteps(true, 0); break;
            }
            TryBuzz(0.15f);   // 몰래 상태라면 채팅이 몰려든 것 → 진동
        }

        void OnStood(bool up)
        {
            standing = up;
            if (!up) nearStage = false;
            else if (GameEvents.IsPlaying && !silenced) PlayCloth(cClothDown, ClothVol * 0.8f, 0.72f);   // 좌석에서 일어나는 옷 스침
        }

        void OnNearStage() { nearStage = true; }

        void OnPhoneState(PhoneState s)
        {
            var prev = lastPhone;
            lastPhone = s;
            if (!GameEvents.IsPlaying || silenced || confiscating || Time.unscaledTime < suppressClothUntil) return;
            if (s == PhoneState.Hidden && prev == PhoneState.Pocket) PlayCloth(cClothUp, ClothVol, Random.Range(0.95f, 1.05f));
            else if (s == PhoneState.Aimed && prev == PhoneState.Hidden) PlayCloth(cClothUp, ClothVol * 0.65f, Random.Range(1.15f, 1.25f));
            else if (s == PhoneState.Aimed) PlayCloth(cClothUp, ClothVol * 1.1f, Random.Range(0.95f, 1.05f));
            else if (s == PhoneState.Hidden) PlayCloth(cClothDown, ClothVol * 0.6f, Random.Range(1.1f, 1.2f));
            else PlayCloth(cClothDown, ClothVol, Random.Range(0.95f, 1.05f));
        }

        void OnCapture(CaptureResult r)
        {
            if (silenced) return;
            bool pocket = phone == null || phone.State == PhoneState.Pocket;
            Play(sfx, cShutter, pocket ? ShutterVol * 0.5f : ShutterVol);
            TryBuzz(0.3f);   // 확보되면 채팅이 몰려든다
        }

        void TryBuzz(float delay)
        {
            if (silenced || phone == null || phone.State != PhoneState.Hidden) return;
            float now = Time.unscaledTime;
            if (now < lastBuzz + BuzzCooldown) return;
            lastBuzz = now;
            buzzAt = now + Mathf.Max(0.001f, delay);
        }

        void OnWarning(string text)
        {
            if (silenced || !GameEvents.IsPlaying) return;
            bool staff = text != null && (text.Contains("관리자") || text.Contains("다가"));
            StartSteps(staff, SideFrom(text));
        }

        void OnCheckpointStarted(float seconds)
        {
            checkpointActive = true;
            walking = false;   // 관리자가 옆에 와서 멈춤
        }

        void OnCheckpointEnded(bool passed)
        {
            checkpointActive = false;
            if (passed)
            {
                // 관리자가 원래 자리로 걸어 돌아간다
                if (walkDir.sqrMagnitude < 0.01f && listener != null) walkDir = Flat(listener.transform.right);
                walking = true; walkingStaff = false; approaching = false;
                stepDist = 1.3f; stepsLeft = 6; stepGap = 0.6f; stepTimer = 1.6f; stepVolume = StepVol * 0.8f;
            }
            else
            {
                walking = false;
                BeginConfiscation(true);
            }
        }

        void OnConfiscated(string reason)
        {
            if (!confiscating) BeginConfiscation(false);
            else if (staticAt > 0f) FireStatic();   // 연출이 예상보다 빨리 끝났으면 지금 끊는다
        }

        void OnServiceFinished()
        {
            finished = true;
            chorusKind = Chorus.None;
            StopChorus();
        }

        // ---------- 뺏기 ----------

        void BeginConfiscation(bool withThud)
        {
            confiscating = true;
            suppressClothUntil = Time.unscaledTime + 10f;
            chorusKind = Chorus.None;
            StopChorus();
            if (withThud)
            {
                Play(sfx, cThud, ThudVol);
                staticAt = Time.unscaledTime + 1.5f;   // 뺏기 연출(약 1.5초) 끝의 신호 끊김에 맞춤. Confiscated가 먼저 오면 그때.
            }
            else FireStatic();
        }

        void FireStatic()
        {
            staticAt = -1f;
            Play(sfx, cStatic, StaticVol);
            silenced = true;   // 지직 아래로 모든 배경이 빠르게 빠지고, 지직이 뚝 끊기면 정적
        }

        // ---------- 발소리 ----------

        /// <summary>경고 문장에서 방향을 읽는다: -1 왼쪽, 1 오른쪽, 2 뒤, 3 앞, 0 모름.</summary>
        static int SideFrom(string t)
        {
            if (string.IsNullOrEmpty(t)) return 0;
            if (t.Contains("왼")) return -1;
            if (t.Contains("오른")) return 1;
            if (t.Contains("뒤")) return 2;
            if (t.Contains("앞")) return 3;
            return 0;
        }

        void StartSteps(bool staff, int side)
        {
            if (silenced || listener == null) return;
            float now = Time.unscaledTime;
            if (walking && walkingStaff && approaching)
            {
                if (staff) stepsLeft = Mathf.Max(stepsLeft, 6);   // 이미 오는 중이면 몇 걸음 더
                return;
            }
            if (!staff)
            {
                if (now < nextShortWalk) return;   // 잦은 경고마다 발소리가 나지 않게
                nextShortWalk = now + 8f;
            }

            var head = listener.transform;
            Vector3 fwd = Flat(head.forward), right = Flat(head.right);
            if (side == 0) side = Random.value < 0.5f ? -1 : 1;
            switch (side)
            {
                case -1: walkDir = -right * 0.85f - fwd * 0.45f; break;
                case 1: walkDir = right * 0.85f - fwd * 0.45f; break;
                case 2: walkDir = -fwd; break;
                default: walkDir = fwd * 0.9f + right * Random.Range(-0.3f, 0.3f); break;
            }
            walkDir.Normalize();

            walking = true; walkingStaff = staff; approaching = true;
            stepsLeft = staff ? 14 : 3;
            stepDist = staff ? 7.5f : Random.Range(5f, 7f);
            stepGap = staff ? 0.58f : 0.62f;
            stepVolume = staff ? StepVol : StepVol * 0.55f;
            stepTimer = 0.05f;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }

        void TickSteps(float dt)
        {
            if (!walking || listener == null) return;
            stepTimer -= dt;
            if (stepTimer > 0f) return;
            stepTimer = stepGap * Random.Range(0.94f, 1.06f);

            var clip = stepAlt ? cStepB : cStepA;
            stepAlt = !stepAlt;
            if (clip != null)
            {
                steps.transform.position = listener.transform.position + walkDir * stepDist + Vector3.down * 1.2f;
                steps.pitch = Random.Range(0.92f, 1.06f);
                steps.PlayOneShot(clip, stepVolume);
            }

            stepDist = approaching ? Mathf.Max(1.1f, stepDist - 0.55f) : stepDist + 0.6f;
            stepsLeft--;
            if (stepsLeft <= 0 || (approaching && stepDist <= 1.1f)) walking = false;
        }

        // ---------- 재생 ----------

        static void Play(AudioSource s, AudioClip c, float vol)
        {
            if (s != null && c != null) s.PlayOneShot(c, vol);
        }

        void PlayCloth(AudioClip c, float vol, float pitch)
        {
            if (cloth == null || c == null) return;
            cloth.pitch = pitch;
            cloth.PlayOneShot(c, vol);
        }
    }
}
