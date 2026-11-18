using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UndercoverLive
{
    // 자막·시청자 경고·발각 화면 연결. UI는 코드로 만든다.
    // 키 안내는 폰·HUD 담당이 상황별로 띄운다(예전 왼쪽 아래 안내·H 토글은 없앰).
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance;
        [Tooltip("예전 버전 호환용(지금은 UIKit 서체를 쓴다)")]
        public Font font;

        // 자막: 아래 가운데, 금빛 화자 칩 + 본문, 반투명 받침
        const float MaxLineWidth = 1080f;
        RectTransform plate, chip;
        Image plateImg;
        Text chipText, lineText;
        CanvasGroup subGroup;
        float subtitleUntil;
        bool subVisible;
        Coroutine subFade;

        bool confiscated, checkpointActive, caughtShown;

        void Awake() { Instance = this; }

        void Start()
        {
            var canvas = UIKit.Canvas("FlowCanvas", 100, transform);
            BuildSubtitle(canvas.transform);

            if (SuspicionManager.Instance != null) SuspicionManager.Instance.Caught += OnCaught;
            GameEvents.Confiscated += OnConfiscated;
            GameEvents.CheckpointStarted += OnCheckpointStarted;
            GameEvents.CheckpointEnded += OnCheckpointEnded;
            // 폰을 든 동안엔 의심 표시가 안 보인다 → 시청자가 대신 알려 준다 (화면 가장자리를 보는 건 시청자)
            feed = FindAnyObjectByType<StreamFeed>();
            foreach (var w in FindObjectsByType<Watcher>()) w.Noticed += OnNoticed;
        }

        void OnDestroy()
        {
            if (SuspicionManager.Instance != null) SuspicionManager.Instance.Caught -= OnCaught;
            GameEvents.Confiscated -= OnConfiscated;
            GameEvents.CheckpointStarted -= OnCheckpointStarted;
            GameEvents.CheckpointEnded -= OnCheckpointEnded;
            foreach (var w in FindObjectsByType<Watcher>()) if (w != null) w.Noticed -= OnNoticed;
            if (Instance == this) Instance = null;
        }

        // ---------- 자막 ----------

        void BuildSubtitle(Transform parent)
        {
            plateImg = UIParts.Fill("SubtitlePlate", parent, UIParts.Shade(0.72f), 12);
            plate = plateImg.rectTransform;
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0f);
            plate.pivot = new Vector2(0.5f, 0f);
            plate.anchoredPosition = new Vector2(0f, 72f);
            subGroup = UIParts.Group(plate.gameObject, 0f);

            var chipImg = UIParts.Panel("Speaker", plate, UIKit.A(UIKit.Accent, 0.16f), 6, UIKit.A(UIKit.Accent, 0.45f));
            chip = chipImg.rectTransform;
            chipText = UIParts.Label(chip, "", 16, UIKit.Accent, true, TextAnchor.MiddleCenter);
            chipText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIKit.Stretch(chipText.rectTransform);

            lineText = UIParts.Label(plate, "", 24, UIKit.Ink, false, TextAnchor.MiddleLeft);
            lineText.lineSpacing = 1.1f;
        }

        public void Say(string who, string line, float seconds)
        {
            if (lineText == null) return;
            who = who ?? "";
            line = line ?? "";

            // 칩
            bool hasWho = who.Length > 0;
            chip.gameObject.SetActive(hasWho);
            chipText.text = who;
            float chipW = hasWho ? Mathf.Ceil(UIParts.Width(chipText)) + 24f : 0f;
            const float chipH = 32f, padX = 20f, padY = 14f, gap = 14f;

            // 본문 폭: 한 줄 폭, 최대 MaxLineWidth에서 줄바꿈
            lineText.text = line;
            float oneLine = Mathf.Ceil(UIParts.Width(lineText));
            float textW = Mathf.Min(oneLine + 2f, MaxLineWidth);
            float textH = Mathf.Max(chipH, Mathf.Ceil(UIParts.Height(lineText, textW)));

            float w = padX + chipW + (hasWho ? gap : 0f) + textW + padX;
            float h = textH + padY * 2f;
            plate.sizeDelta = new Vector2(w, h);
            UIKit.Place(chip, padX, padY + (textH - chipH) * 0.5f, chipW, chipH);
            UIKit.Place(lineText.rectTransform, padX + chipW + (hasWho ? gap : 0f), padY, textW, textH);

            subtitleUntil = Time.time + seconds;
            subVisible = true;
            FadeSubtitle(1f, 0.15f);
        }

        void FadeSubtitle(float to, float dur)
        {
            if (subFade != null) StopCoroutine(subFade);
            subFade = StartCoroutine(UIParts.FadeTo(subGroup, to, dur));
        }

        void Update()
        {
            if (subVisible && Time.time > subtitleUntil)
            {
                subVisible = false;
                FadeSubtitle(0f, 0.25f);
            }
        }

        // ---------- 시청자 경고 ----------

        StreamFeed feed;
        float lastWarn = -99f;
        void OnNoticed(Watcher w)
        {
            var pc = PhoneController.Instance;
            if (feed == null || pc == null || !pc.IsRecording || Time.time - lastWarn < 4f) return;
            lastWarn = Time.time;
            var local = pc.Head.InverseTransformPoint(w.EyePosition);
            string where = local.z < -0.5f ? "뒤에" : local.x < -1f ? "왼쪽에" : local.x > 1f ? "오른쪽에" : "앞에";
            string who = w.kind == WatcherKind.Staff ? "검은 옷 입은 사람" : w.kind == WatcherKind.Leader ? "교주" : w.kind == WatcherKind.CCTV ? "천장 카메라" : "사람";
            feed.Warn($"{where} {who}{UIKit.Josa(who)} 쳐다봐요!! 폰 내려요");
        }

        // ---------- 발각 ----------
        // 이제 NPC 담당이 검문·폰 뺏기 연출 뒤 GameEvents.RaiseConfiscated(reason)을 부른다 → 발각 화면.
        // 그 연출이 아직 없을 때를 위한 안전장치: 경계가 가득 차고(Caught) 1.5초 안에 검문도 압수도 안 오면 바로 발각 화면.

        void OnConfiscated(string reason)
        {
            confiscated = true;
            ShowCaught(reason);
        }

        void OnCheckpointStarted(float seconds) { checkpointActive = true; }

        void OnCheckpointEnded(bool passed)
        {
            checkpointActive = false;
            if (!passed) StartCoroutine(WaitConfiscate(4f));
        }

        void OnCaught()
        {
            StartCoroutine(WaitConfiscate(1.5f));
        }

        IEnumerator WaitConfiscate(float seconds)
        {
            yield return new WaitForSeconds(seconds);   // 멈춤 동안엔 같이 멈춘다
            if (confiscated || checkpointActive || caughtShown) yield break;
            ShowCaught(UIStrings.CaughtDefaultReason);
        }

        void ShowCaught(string reason)
        {
            if (caughtShown) return;
            var rs = FindAnyObjectByType<ResultsScreen>();
            if (rs != null && rs.IsShown) return;
            caughtShown = true;
            if (!confiscated) PhoneController.Instance?.SetState(PhoneState.Pocket);
            var cs = CaughtScreen.Instance != null ? CaughtScreen.Instance : FindAnyObjectByType<CaughtScreen>();
            if (cs != null) cs.Show(reason);
            subVisible = false;
            FadeSubtitle(0f, 0.2f);
        }
    }
}
