using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UndercoverLive
{
    // 집회 진행: 설교(녹음 대상) → 눈가리개 의례(신도는 못 봄) → 눈을 뜨게 하고 기적(맨눈엔 빛, 폰엔 와이어·조수) → 경고 → 끝.
    public class ServiceDirector : MonoBehaviour
    {
        [Header("연결")]
        public Transform victim;
        public GameObject eyeOnlyMiracle;      // 빛 기둥 (EyeOnly)
        public Transform wire;                 // 와이어 (PhoneOnly)
        public Light trickSpot;                // 조수가 켜는 실제 조명 (둘 다 보임)
        public LeaderGaze leaderGaze;
        public CaptureTarget sermonSound;
        public CaptureTarget miracleTarget;
        public GameObject[] blindfolds;        // 신도 머리의 흰 눈가리개

        [Header("문안 (빌더가 cult-copy.json에서 채움)")]
        public string leaderTitle = "눈뜬 자";
        public string membersLabel = "신도들";
        public string lineOpening = "오늘도 바깥의 눈을 두고 오셨지요. 잘하셨습니다.";
        public string linePhoneBan = "기계의 눈은 거짓만 담습니다. 그래서 문 앞에 맡기는 겁니다.";
        public string lineBlindfold = "이제 눈을 가리십시오. 보지 않는 자만이 보게 됩니다.";
        public string chant = "눈을 감으라, 아버지가 보신다";
        public string lineOpenEyes = "눈을 뜨십시오. 이제 아버지의 눈으로 보십시오.";
        public string lineMiracle = "보십시오. 아버지께서 이 형제를 들어 올리십니다.";
        public string lineWarning = "의심하는 눈은 이 자리에 있을 수 없습니다.";
        public string lineClosing = "오늘 본 것은 이 안에 두고 가십시오.";
        public string endReason = "집회가 끝났습니다";

        [Header("진행")]
        public float firstDelay = 10f;
        public float loopInterval = 40f;
        public bool autoLoop = true;
        public float riseHeight = 0.9f;
        [Tooltip("이 횟수만큼 돌면 집회가 끝나고 결과 화면(엔딩)")]
        public int cycles = 2;

        public bool Running { get; private set; }
        public bool Blindfolded { get; private set; }
        Vector3 victimBase;
        float timer;
        int done;

        void Start()
        {
            if (victim != null) victimBase = victim.position;
            SetMiracle(false);
            SetBlindfolds(false);
            timer = firstDelay;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame && !Running) StartCoroutine(Run());
            if (autoLoop && !Running)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f) StartCoroutine(Run());
            }
            UpdateWire();
        }

        void UpdateWire()
        {
            if (wire == null || victim == null) return;
            float top = victim.position.y + 1.05f, ceiling = 7f;
            float len = Mathf.Max(0.1f, ceiling - top);
            wire.position = new Vector3(victim.position.x, top + len * 0.5f, victim.position.z);
            wire.localScale = new Vector3(0.02f, len * 0.5f, 0.02f);
        }

        void SetMiracle(bool on)
        {
            if (eyeOnlyMiracle != null) eyeOnlyMiracle.SetActive(on);
            if (wire != null) wire.gameObject.SetActive(on);
            if (trickSpot != null) trickSpot.enabled = on;
            if (miracleTarget != null) miracleTarget.activeNow = on;
        }

        void SetBlindfolds(bool on)
        {
            Blindfolded = on;
            if (blindfolds == null) return;
            foreach (var b in blindfolds) if (b != null) b.SetActive(on);
        }

        IEnumerator Run()
        {
            Running = true;
            var flow = GameFlow.Instance;

            // 1. 설교 — 녹음 대상
            flow?.Say(leaderTitle, lineOpening, 5f);
            if (sermonSound != null) sermonSound.BeginSound(10f);
            yield return new WaitForSeconds(5f);
            flow?.Say(leaderTitle, linePhoneBan, 5f);
            yield return new WaitForSeconds(5.5f);

            // 2. 눈가리개 의례 — 신도는 아무것도 못 본다. 교주와 관리자만 본다.
            flow?.Say(leaderTitle, lineBlindfold, 4.5f);
            yield return new WaitForSeconds(2.5f);
            SetBlindfolds(true);
            Watcher.CrowdFocus = 0f;
            yield return new WaitForSeconds(2f);
            for (int i = 0; i < 2; i++)
            {
                flow?.Say(membersLabel, chant, 2.6f);
                yield return new WaitForSeconds(2.8f);
            }

            // 3. 기적 — 신도들은 눈을 가린 채 말로만 '본다'. 맨눈(플레이어)엔 빛, 폰엔 와이어.
            flow?.Say(leaderTitle, lineMiracle, 5f);
            if (leaderGaze != null) leaderGaze.sweeping = false;
            SetMiracle(true);
            float t = 0f;
            while (t < 3f)
            {
                t += Time.deltaTime;
                victim.position = victimBase + Vector3.up * riseHeight * Mathf.SmoothStep(0f, 1f, t / 3f);
                yield return null;
            }
            flow?.Say(membersLabel, chant, 2.6f);
            yield return new WaitForSeconds(5f);
            t = 0f;
            while (t < 2f)
            {
                t += Time.deltaTime;
                victim.position = victimBase + Vector3.up * riseHeight * Mathf.SmoothStep(1f, 0f, t / 2f);
                yield return null;
            }
            victim.position = victimBase;
            SetMiracle(false);
            SetBlindfolds(false);
            Watcher.CrowdFocus = 1f;

            // 4. 경고 — 시선 훑기 재개
            flow?.Say(leaderTitle, lineWarning, 4f);
            if (leaderGaze != null) leaderGaze.sweeping = true;
            yield return new WaitForSeconds(4f);

            done++;
            if (done >= cycles)
            {
                flow?.Say(leaderTitle, lineClosing, 5f);
                yield return new WaitForSeconds(5f);
                bool caught = SuspicionManager.Instance != null && SuspicionManager.Instance.IsCaught;
                if (!caught)
                {
                    PhoneController.Instance?.SetState(PhoneState.Pocket);
                    var results = FindAnyObjectByType<ResultsScreen>();
                    if (results != null) results.Show(endReason);
                }
                autoLoop = false;
                Running = false;
                yield break;
            }
            timer = loopInterval;
            Running = false;
        }
    }
}
