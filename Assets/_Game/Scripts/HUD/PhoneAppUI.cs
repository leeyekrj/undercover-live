using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace UndercoverLive
{
    /// <summary>
    /// 촬영 화면: 폰을 드는 순간 화면 전체가 폰 카메라 앱이 된다 (시안 1안을 덜어 낸 것).
    /// - 화면 전체 = 폰 피드(렌더 텍스처) + 가장자리 18px 어두운 베젤(둥근 모서리) + 왼쪽 카메라 섬 + 아래 홈 바
    /// - 들 때 0.18초 아래에서 올라오며 화면이 켜지고, 내릴 때 반대로. 손에 든 아주 약한 흔들림(UI만, 판정과 무관)
    /// - 가운데 카메라 앱식 모서리 초점 상자 + 대상 이름·예상 등급 + 확보 진행 막대 → 확보 순간 초록·찰칵 플래시·등급 도장
    /// - 오른쪽 세로 줌 표시(1x/2x/4x), 왼쪽 아래 채팅 카드
    /// 상태 줄(REC·LIVE·배터리)과 목표 진행은 BroadcastWidget이 같은 자리에서 이어받는다.
    /// </summary>
    public class PhoneAppUI : MonoBehaviour
    {
        public static PhoneAppUI Instance { get; private set; }

        public const float BezelWidth = 18f;
        public const float RaiseSeconds = 0.18f;

        /// <summary>촬영 화면이 올라온 정도 0~1.</summary>
        public float Blend { get; private set; }
        /// <summary>폰 화면의 지금 위치·기울기 (올라오는 중·손 흔들림). 다른 캔버스의 경고 연출이 따라 움직일 때 쓴다.</summary>
        public Vector2 ScreenOffset => group != null ? group.anchoredPosition : Vector2.zero;
        public Quaternion ScreenRotation => group != null ? group.localRotation : Quaternion.identity;

        RectTransform group, app;
        bool eyeOffByUs;
        float eyeHoldUntil = -99f;
        // 맨눈 카메라를 '빈 화면'으로 바꾸기 전 값 (돌려놓기용)
        int savedMask;
        CameraClearFlags savedClear;
        Color savedBg;
        bool savedPost, savedShadows;
        RawImage feed;
        Image screenOff, flash;
        Text offText;
        ChatView chat;

        // 초점 상자
        RectTransform focus;
        CanvasGroup focusCg;
        Image[] corners = new Image[8];
        RectTransform labelRow;
        Text labelText, gradeText;
        Image gradeBadge;
        Image barTrack, barFill;
        Vector4 boxNorm;      // xMin, yMin, xMax, yMax (0~1)
        CaptureTarget boxTarget;
        string lastLabel, lastGrade;

        // 확보 순간
        RectTransform stamp;
        CanvasGroup stampCg;
        Image stampBg;
        Text stampText;
        float stampStart = -99f, flashStart = -99f;
        Vector2 stampPos;
        bool capturedHold;
        CaptureGrade lastCapGrade;

        // 줌
        Image[] zoomBg = new Image[3];
        Text[] zoomText = new Text[3];
        static readonly float[] ZoomStops = { 4f, 2f, 1f };
        int lastZoomIdx = -1;
        string lastZoomLabel;

        static readonly Color HoldYellow = UIKit.Hex("#EAB308");

        void Awake()
        {
            Instance = this;
            Build();
        }

        void OnEnable()
        {
            CaptureLog.Added += OnCaptured;
            GameEvents.CheckpointEnded += OnCheckpointEnded;
        }

        void OnDisable()
        {
            Unsubscribe();
            RestoreEyeCamera();
        }

        void OnDestroy()
        {
            Unsubscribe();
            RestoreEyeCamera();
            if (Instance == this) Instance = null;
        }

        void Unsubscribe()
        {
            CaptureLog.Added -= OnCaptured;
            GameEvents.CheckpointEnded -= OnCheckpointEnded;
            if (feedSrc != null) feedSrc.LineAdded -= OnLine;
            feedSrc = null;
        }

        // 뺏기 연출(C)이 맨눈 화면을 쓸 수 있게, 검문 실패 직후에는 맨눈 카메라를 끄지 않는다
        void OnCheckpointEnded(bool passed) { if (!passed) eyeHoldUntil = Time.unscaledTime + 4f; }

        StreamFeed feedSrc;

        // ---------- 구성 ----------

        void Build()
        {
            var layer = HUDRoot.Instance.PhoneLayer;
            group = UIKit.Rect("PhoneScreen", layer);
            UIKit.Stretch(group);

            // 피드: 화면 전체 (베젤이 가장자리를 덮는다 → 피드 좌표 = 화면 좌표)
            var fgo = new GameObject("Feed", typeof(RectTransform), typeof(RawImage));
            fgo.transform.SetParent(group, false);
            feed = fgo.GetComponent<RawImage>();
            feed.raycastTarget = false;
            feed.color = Color.white;
            UIKit.Stretch(feed.rectTransform, -2f);

            // 3×3 구도선 (아주 옅게)
            var grid = UIKit.Rect("Grid", group);
            UIKit.Stretch(grid, BezelWidth);
            for (int i = 1; i <= 2; i++)
            {
                var v = UIKit.Image("V" + i, grid, new Color(1, 1, 1, 0.05f));
                v.rectTransform.anchorMin = new Vector2(i / 3f, 0); v.rectTransform.anchorMax = new Vector2(i / 3f, 1);
                v.rectTransform.sizeDelta = new Vector2(1, 0); v.rectTransform.anchoredPosition = Vector2.zero;
                var h = UIKit.Image("H" + i, grid, new Color(1, 1, 1, 0.05f));
                h.rectTransform.anchorMin = new Vector2(0, i / 3f); h.rectTransform.anchorMax = new Vector2(1, i / 3f);
                h.rectTransform.sizeDelta = new Vector2(0, 1); h.rectTransform.anchoredPosition = Vector2.zero;
            }

            // 찰칵 플래시
            flash = UIKit.Image("Flash", group, new Color(1, 1, 1, 0));
            UIKit.Stretch(flash.rectTransform, BezelWidth);

            // 앱 요소 (초점 상자·도장·줌·채팅)
            app = UIKit.Rect("App", group);
            UIKit.Stretch(app);
            BuildFocus();
            BuildStamp();
            BuildZoom();
            chat = new ChatView(app);

            // 화면 꺼짐 (드는 순간 켜지는 전환·배터리 없음)
            screenOff = UIKit.Image("ScreenOff", group, Color.black);
            UIKit.Stretch(screenOff.rectTransform, BezelWidth - 2f);
            offText = HUDDraw.Label("OffText", screenOff.transform, HUDText.BatteryDead, HUDDraw.FM, UIKit.Muted, true, TextAnchor.MiddleCenter);
            UIKit.Stretch(offText.rectTransform);

            // 베젤: 피드 모서리를 깎는 안쪽 둥근 판 + 바깥 테두리 18px + 안쪽 가는 선
            var corner = UIKit.Image("ScreenCorners", group, HUDDraw.Bezel, HUDDraw.InverseRounded(32));
            corner.type = Image.Type.Sliced; corner.fillCenter = false;
            UIKit.Stretch(corner.rectTransform, BezelWidth);
            var body = UIKit.Box("Bezel", group, HUDDraw.Bezel, 48, (int)BezelWidth);
            UIKit.Stretch(body.rectTransform, -1f);
            var inner = UIKit.Box("InnerLine", group, new Color(1, 1, 1, 0.14f), 32, 2);
            UIKit.Stretch(inner.rectTransform, BezelWidth);

            // 왼쪽 카메라 섬 (x=28, 세로 가운데, 26×132) + 렌즈 + 빨간 녹음 점
            var island = UIKit.Box("CameraIsland", group, UIKit.Hex("#07080B"), 13);
            HUDDraw.At(island.rectTransform, HUDDraw.ML, HUDDraw.ML, new Vector2(28f, 0f), new Vector2(26f, 132f));
            var lens = UIKit.Image("Lens", island.transform, UIKit.Hex("#1B2536"), UIKit.Circle(64));
            HUDDraw.At(lens.rectTransform, HUDDraw.TC, HUDDraw.TC, new Vector2(0, -20f), new Vector2(12f, 12f));
            var dot = UIKit.Image("MicDot", island.transform, UIKit.Rec, UIKit.Circle(64));
            HUDDraw.At(dot.rectTransform, HUDDraw.BC, HUDDraw.BC, new Vector2(0, 20f), new Vector2(7f, 7f));

            // 홈 바
            var home = UIKit.Box("HomeBar", group, new Color(1, 1, 1, 0.38f), 3);
            HUDDraw.At(home.rectTransform, HUDDraw.BC, HUDDraw.BC, new Vector2(0, 26f), new Vector2(220f, 5f));

            group.gameObject.SetActive(false);
        }

        void BuildFocus()
        {
            focus = UIKit.Rect("FocusBox", app);
            focusCg = focus.gameObject.AddComponent<CanvasGroup>();
            focusCg.alpha = 0f; focusCg.blocksRaycasts = false;
            // 네 모서리 ㄱ자 (팔 34px, 두께 4px)
            Vector2[] anchors = { HUDDraw.TL, HUDDraw.TR, HUDDraw.BL, HUDDraw.BR };
            for (int i = 0; i < 4; i++)
            {
                var a = anchors[i];
                var hBar = UIKit.Image("C" + i + "h", focus, HoldYellow);
                HUDDraw.At(hBar.rectTransform, a, a, Vector2.zero, new Vector2(34f, 4f));
                var vBar = UIKit.Image("C" + i + "v", focus, HoldYellow);
                HUDDraw.At(vBar.rectTransform, a, a, Vector2.zero, new Vector2(4f, 34f));
                corners[i * 2] = hBar; corners[i * 2 + 1] = vBar;
            }
            // 위: 대상 이름 한 줄 + 예상 등급 배지
            labelRow = UIKit.Rect("Label", focus);
            HUDDraw.At(labelRow, HUDDraw.TC, HUDDraw.BC, new Vector2(0, 12f), new Vector2(300f, 30f));
            labelText = HUDDraw.Label("Name", labelRow, "", HUDDraw.FM, HUDDraw.Mint, true, TextAnchor.MiddleLeft);
            gradeBadge = UIKit.Box("Grade", labelRow, UIKit.Ok, 8);
            gradeText = HUDDraw.Label("Text", gradeBadge.transform, "", HUDDraw.FS, HUDDraw.Dark, true, TextAnchor.MiddleCenter);
            UIKit.Stretch(gradeText.rectTransform);
            // 아래: 확보 진행 막대
            barTrack = UIKit.Box("Track", focus, new Color(1, 1, 1, 0.2f), 3);
            var bt = barTrack.rectTransform;
            bt.anchorMin = new Vector2(0, 0); bt.anchorMax = new Vector2(1, 0); bt.pivot = new Vector2(0.5f, 1f);
            bt.anchoredPosition = new Vector2(0, -14f); bt.sizeDelta = new Vector2(0, 6f);
            barFill = UIKit.Box("Fill", bt, HoldYellow, 3);
            var bf = barFill.rectTransform;
            bf.anchorMin = Vector2.zero; bf.anchorMax = new Vector2(0, 1); bf.pivot = new Vector2(0, 0.5f);
            bf.offsetMin = bf.offsetMax = Vector2.zero;
        }

        void BuildStamp()
        {
            stamp = UIKit.Rect("GradeStamp", app);
            HUDDraw.At(stamp, HUDDraw.BL, HUDDraw.MC, Vector2.zero, new Vector2(132f, 132f));
            stampCg = stamp.gameObject.AddComponent<CanvasGroup>();
            stampCg.alpha = 0f; stampCg.blocksRaycasts = false;
            var fill = UIKit.Box("Fill", stamp, new Color(0, 0, 0, 0.35f), 22);
            UIKit.Stretch(fill.rectTransform);
            stampBg = UIKit.Box("Ring", stamp, UIKit.Ok, 22, 5);
            UIKit.Stretch(stampBg.rectTransform);
            stampText = HUDDraw.Label("Letter", stamp, "S", HUDDraw.FXL, UIKit.Ok, true, TextAnchor.MiddleCenter);
            UIKit.Stretch(stampText.rectTransform);
            stamp.gameObject.SetActive(false);
        }

        void BuildZoom()
        {
            // 오른쪽 가장자리 세로 레일 (4x / 2x / 1x) + 아래 '휠'
            var rail = UIKit.Box("ZoomRail", app, HUDDraw.Card, 32);
            HUDDraw.At(rail.rectTransform, HUDDraw.MR, HUDDraw.MR, new Vector2(-52f, 12f), new Vector2(64f, 176f));
            for (int i = 0; i < 3; i++)
            {
                var c = UIKit.Image("Z" + i, rail.transform, Color.clear, UIKit.Circle(64));
                HUDDraw.At(c.rectTransform, HUDDraw.TC, HUDDraw.TC, new Vector2(0, -9f - i * 54f), new Vector2(46f, 46f));
                var t = HUDDraw.Label("T", c.transform, ZoomStops[i] + "x", HUDDraw.FS, UIKit.A(UIKit.Ink, 0.72f), true, TextAnchor.MiddleCenter);
                UIKit.Stretch(t.rectTransform);
                zoomBg[i] = c; zoomText[i] = t;
            }
            var cap = HUDDraw.Label("Caption", app, HUDText.ZoomCaption, HUDDraw.FS, UIKit.Muted, false, TextAnchor.MiddleCenter);
            HUDDraw.At(cap.rectTransform, HUDDraw.MR, HUDDraw.MR, new Vector2(-52f, -100f), new Vector2(64f, 24f));
        }

        // ---------- 갱신 ----------

        void Update()
        {
            var pc = PhoneController.Instance;
            if (feedSrc == null && StreamFeed.Instance != null)
            {
                feedSrc = StreamFeed.Instance;
                feedSrc.LineAdded += OnLine;
            }

            bool want = pc != null && pc.State == PhoneState.Aimed && GameEvents.Phase != GamePhase.Ended;
            Blend = Mathf.MoveTowards(Blend, want ? 1f : 0f, Time.deltaTime / RaiseSeconds);
            bool show = Blend > 0.001f;
            if (group.gameObject.activeSelf != show) group.gameObject.SetActive(show);
            chat.Tick(Time.unscaledDeltaTime);
            UpdateEyeCamera(pc);
            if (!show) return;

            float H = HUDRoot.Instance.Size.y;
            // 아래에서 올라옴 (올릴 땐 끝이 부드럽게, 내릴 땐 처음이 부드럽게)
            float e = HUDDraw.EaseOut(Blend);
            float t = Time.time;
            float sx = (Mathf.PerlinNoise(t * 0.7f, 3.1f) - 0.5f) * 2.4f;
            float sy = (Mathf.PerlinNoise(5.7f, t * 0.6f) - 0.5f) * 2.0f;
            group.anchoredPosition = new Vector2(sx, -(1f - e) * (H + 40f) + sy);
            group.localRotation = Quaternion.Euler(0, 0, (Mathf.PerlinNoise(t * 0.5f, 9.3f) - 0.5f) * 0.16f);

            if (pc != null && feed.texture != pc.Feed) feed.texture = pc.Feed;

            bool dead = pc != null && pc.Battery01 <= 0f;
            float off = dead ? 1f : Mathf.Clamp01(1f - Blend * 1.35f);
            screenOff.color = new Color(0, 0, 0, off);
            offText.enabled = dead;

            UpdateFocus(pc);
            UpdateStamp();
            UpdateZoom(pc);

            float fk = Mathf.Clamp01((Time.unscaledTime - flashStart) / 0.25f);
            flash.color = new Color(1, 1, 1, fk < 1f ? 0.42f * (1f - fk) : 0f);
        }

        /// <summary>
        /// 폰 화면이 화면 전체를 다 덮었을 때(촬영, 플레이 중)는 안 보이는 맨눈 장면을 그리지 않는다(웹 프레임 확보).
        /// 카메라를 아예 끄면 URP가 화면용 카메라 없이 오버레이 UI를 그리지 않아(폰 카메라는 텍스처로만 그림) HUD가 사라진다.
        /// 그래서 맨눈 카메라는 켜 둔 채 아무것도 안 그리게(cullingMask 0·검은 바탕·후처리/그림자 끔) 바꿨다가 돌려놓는다.
        /// 우리가 바꾼 것만 되돌린다 (다른 담당의 카메라 전환과 다투지 않게).
        /// </summary>
        void UpdateEyeCamera(PhoneController pc)
        {
            var hud = HUDRoot.Instance;
            bool cover = pc != null && pc.EyeCamera != null && pc.IsScreenVisible && GameEvents.IsPlaying
                         && Blend >= 1f && hud != null && hud.Opaque && Time.unscaledTime >= eyeHoldUntil;
            if (cover && !eyeOffByUs && pc.EyeCamera.enabled)
            {
                var cam = pc.EyeCamera;
                savedMask = cam.cullingMask; savedClear = cam.clearFlags; savedBg = cam.backgroundColor;
                var data = cam.GetUniversalAdditionalCameraData();
                savedPost = data != null && data.renderPostProcessing;
                savedShadows = data != null && data.renderShadows;
                cam.cullingMask = 0;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                if (data != null) { data.renderPostProcessing = false; data.renderShadows = false; }
                eyeOffByUs = true;
            }
            else if (!cover) RestoreEyeCamera();
        }

        void RestoreEyeCamera()
        {
            if (!eyeOffByUs) return;
            eyeOffByUs = false;
            var pc = PhoneController.Instance;
            if (pc == null || pc.EyeCamera == null) return;
            var cam = pc.EyeCamera;
            cam.cullingMask = savedMask; cam.clearFlags = savedClear; cam.backgroundColor = savedBg;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = savedPost; data.renderShadows = savedShadows; }
        }

        void UpdateFocus(PhoneController pc)
        {
            var judge = CaptureJudge.Instance;
            bool visible = pc != null && pc.IsScreenVisible && judge != null;
            float sinceCap = judge != null ? Time.time - judge.LastCaptureTime : 99f;
            bool flashing = visible && sinceCap < 0.6f && boxTarget != null && capturedHold;
            bool holding = visible && judge.CurrentTarget != null;
            if (!flashing) capturedHold = false;

            Rect r = default;
            if (holding && !flashing)
            {
                if (judge.CurrentTarget != boxTarget)
                {
                    boxTarget = judge.CurrentTarget;
                    var cr = judge.CurrentRect;
                    boxNorm = new Vector4(cr.xMin, cr.yMin, cr.xMax, cr.yMax);   // 대상이 바뀌면 바로 그 자리로
                }
                r = judge.CurrentRect;
            }
            else if (flashing) r = judge.LastCaptureRect;

            float target = (holding || flashing) ? 1f : 0f;
            focusCg.alpha = Mathf.MoveTowards(focusCg.alpha, target, Time.unscaledDeltaTime / 0.15f);
            if (focusCg.alpha <= 0.001f) { if (!holding) boxTarget = null; return; }

            if (holding || flashing)
            {
                // 너무 작은 대상도 상자가 읽히게 최소 크기 (140×140)
                var size = HUDRoot.Instance.Size;
                float minW = 140f / size.x, minH = 140f / size.y;
                float cx = r.center.x, cy = r.center.y;
                float w = Mathf.Max(r.width, minW), h = Mathf.Max(r.height, minH);
                var goal = new Vector4(cx - w * 0.5f, cy - h * 0.5f, cx + w * 0.5f, cy + h * 0.5f);
                boxNorm = Vector4.Lerp(boxNorm, goal, HUDDraw.Damp(18f, Time.unscaledDeltaTime));
            }
            focus.anchorMin = new Vector2(boxNorm.x, boxNorm.y);
            focus.anchorMax = new Vector2(boxNorm.z, boxNorm.w);
            focus.offsetMin = focus.offsetMax = Vector2.zero;

            float prog = flashing ? 1f : (holding ? judge.HoldProgress : 0f);
            Color col = (flashing || prog >= 0.85f) ? UIKit.Ok : HoldYellow;
            for (int i = 0; i < corners.Length; i++) corners[i].color = col;
            barFill.color = col;
            barFill.rectTransform.anchorMax = new Vector2(prog, 1f);

            if (boxTarget != null)
            {
                string label = HUDText.TargetLabel(boxTarget.Id, boxTarget.Name);
                var g = flashing ? lastCapGrade : CaptureJudge.GradeOf(judge.HoldAverageQuality);
                string grade = (flashing ? "확보 " : "예상 ") + g;
                if (label != lastLabel || grade != lastGrade)
                {
                    lastLabel = label; lastGrade = grade;
                    labelText.text = label;
                    gradeText.text = grade;
                    float lw = HUDDraw.Width(labelText), gw = HUDDraw.Width(gradeText) + 18f;
                    float total = lw + 10f + gw;
                    HUDDraw.At(labelText.rectTransform, HUDDraw.ML, HUDDraw.ML, new Vector2(0, 0), new Vector2(lw + 2f, 30f));
                    HUDDraw.At(gradeBadge.rectTransform, HUDDraw.ML, HUDDraw.ML, new Vector2(lw + 10f, 0), new Vector2(gw, 28f));
                    labelRow.sizeDelta = new Vector2(total, 30f);
                }
                gradeBadge.color = GradeColor(g);
                gradeText.color = g == CaptureGrade.C ? UIKit.Ink : HUDDraw.Dark;
            }
        }

        static Color GradeColor(CaptureGrade g)
        {
            switch (g)
            {
                case CaptureGrade.S: return HUDDraw.Gold;
                case CaptureGrade.A: return UIKit.Ok;
                case CaptureGrade.B: return UIKit.Ink;
                default: return new Color(1, 1, 1, 0.24f);
            }
        }

        void OnCaptured(CaptureResult r)
        {
            var pc = PhoneController.Instance;
            if (pc == null || !pc.IsScreenVisible || r == null) return;
            flashStart = Time.unscaledTime;
            stampStart = Time.unscaledTime;
            lastCapGrade = r.Grade;
            var judge = CaptureJudge.Instance;
            var size = HUDRoot.Instance.Size;
            if (r.Category != CaptureCategory.Sound && judge != null)
            {
                capturedHold = true;
                if (boxTarget == null) boxTarget = r.Target;
                var c = judge.LastCaptureRect.center;
                stampPos = new Vector2(c.x * size.x, c.y * size.y);
            }
            else stampPos = new Vector2(size.x * 0.5f, size.y * 0.5f);
            // 화면 밖으로 넘치지 않게
            stampPos.x = Mathf.Clamp(stampPos.x, 140f, size.x - 140f);
            stampPos.y = Mathf.Clamp(stampPos.y, 160f, size.y - 160f);

            Color gc = GradeColor(r.Grade);
            if (r.Grade == CaptureGrade.C) gc = UIKit.Muted;
            stampBg.color = gc;
            stampText.color = gc;
            stampText.text = r.Grade.ToString();
            stamp.gameObject.SetActive(true);
        }

        void UpdateStamp()
        {
            if (!stamp.gameObject.activeSelf) return;
            float t = Time.unscaledTime - stampStart;
            const float dur = 0.6f;
            if (t >= dur + 0.2f) { stamp.gameObject.SetActive(false); return; }
            float pop = HUDDraw.EaseOut(Mathf.Clamp01(t / 0.15f));
            float s = Mathf.Lerp(1.5f, 1f, pop);
            stamp.localScale = new Vector3(s, s, 1f);
            stamp.localRotation = Quaternion.Euler(0, 0, -8f);
            stamp.anchoredPosition = stampPos;
            stampCg.alpha = t < dur ? Mathf.Clamp01(t / 0.06f) : 1f - Mathf.Clamp01((t - dur) / 0.2f);
        }

        void UpdateZoom(PhoneController pc)
        {
            if (pc == null) return;
            float z = pc.ZoomFactor;
            int idx = 2;
            float best = 99f;
            for (int i = 0; i < 3; i++)
            {
                float d = Mathf.Abs(Mathf.Log(z) - Mathf.Log(ZoomStops[i]));
                if (d < best) { best = d; idx = i; }
            }
            string label = Mathf.Abs(z - ZoomStops[idx]) < 0.05f ? ZoomStops[idx] + "x" : z.ToString("0.0") + "x";
            if (idx == lastZoomIdx && label == lastZoomLabel) return;
            lastZoomIdx = idx; lastZoomLabel = label;
            for (int i = 0; i < 3; i++)
            {
                bool on = i == idx;
                zoomBg[i].color = on ? UIKit.Ink : Color.clear;
                zoomText[i].color = on ? HUDDraw.Dark : UIKit.A(UIKit.Ink, 0.72f);
                zoomText[i].text = on ? label : ZoomStops[i] + "x";
            }
        }

        void OnLine(ChatLine l)
        {
            chat.Add(l);
        }
    }
}
