using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UndercoverLive
{
    // 자막·조작 안내·발각 화면. UI는 코드로 만든다.
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance;
        public Font font;

        Text subtitle, hint, endTitle, endBody;
        GameObject endPanel;
        float subtitleUntil;
        bool ended;

        void Awake() { Instance = this; }

        void Start()
        {
            if (font == null && PhoneController.Instance != null) font = PhoneController.Instance.UIFont;
            var canvasGo = new GameObject("FlowCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            subtitle = MakeText(canvasGo.transform, "Subtitle", 30, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0, 54), new Vector2(1500, 120));
            hint = MakeText(canvasGo.transform, "Hint", 19, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(28, 22), new Vector2(900, 140));
            hint.rectTransform.pivot = new Vector2(0, 0);
            hint.color = new Color(1, 1, 1, 0.55f);
            hint.text = "마우스  둘러보기\n우클릭  폰 꺼내기 → 들기 → 넣기   (1·2·3 바로 선택, Q 넣기)\n휠  줌 (들었을 때)      F  일어서기·앉기 (서면 WASD 이동)\nM  기적 바로 시작      Esc  커서 풀기      H  안내 숨기기";

            endPanel = new GameObject("EndPanel", typeof(Image));
            endPanel.transform.SetParent(canvasGo.transform, false);
            var prt = endPanel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
            endPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.94f);
            endTitle = MakeText(endPanel.transform, "Title", 54, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1400, 90));
            endBody = MakeText(endPanel.transform, "Body", 24, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1200, 300));
            endBody.rectTransform.pivot = new Vector2(0.5f, 1f);
            endPanel.SetActive(false);

            if (SuspicionManager.Instance != null) SuspicionManager.Instance.Caught += OnCaught;
            // 폰을 든 동안엔 의심 표시가 안 보인다 → 시청자가 대신 알려 준다 (화면 가장자리를 보는 건 시청자)
            feed = FindAnyObjectByType<StreamFeed>();
            foreach (var w in FindObjectsByType<Watcher>(FindObjectsSortMode.None)) w.Noticed += OnNoticed;
        }

        Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Vector2 anchorPos, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(Text), typeof(Shadow));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = anchor; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; t.color = Color.white;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchorPos; rt.pivot = new Vector2(0.5f, anchorPos.y);
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            go.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            return t;
        }

        public void Say(string who, string line, float seconds)
        {
            if (subtitle == null) return;
            subtitle.text = $"<color=#d8b27a>{who}</color>  {line}";
            subtitleUntil = Time.time + seconds;
        }

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
            feed.Warn($"{where} {who}이 쳐다봐요!! 폰 내려요");
        }

        void OnCaught()
        {
            if (ended) return;
            var rs = FindAnyObjectByType<ResultsScreen>();
            if (rs != null && rs.IsShown) return;
            ended = true;
            PhoneController.Instance?.SetState(PhoneState.Pocket);
            endPanel.SetActive(true);
            endTitle.text = "방송이 종료되었습니다";
            endBody.text = "관리자가 폰을 빼앗았습니다. 찍은 영상은 압수되었습니다.\n\n<color=#888888>R  처음부터</color>";
        }

        void Update()
        {
            if (subtitle != null && Time.time > subtitleUntil) subtitle.text = "";
            var kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame && hint != null) hint.enabled = !hint.enabled;
            if (ended && kb != null && kb.rKey.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
