using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UndercoverLive;

// 회색 예배당 위에 시점 장치(폰 카메라·폰 화면·맨눈/폰 후처리 분리), 감시자, 판정 대상, 진행을 붙인다.
public static class ChapelRig
{
    const int EyeOnly = ChapelGreybox.EyeOnly, PhoneOnly = ChapelGreybox.PhoneOnly;
    static Font font, fontBold;

    public static void Apply()
    {
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Fonts/Pretendard-Medium.otf");
        fontBold = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Fonts/Pretendard-Bold.otf");

        var player = GameObject.Find("Player").transform;
        var head = player.Find("Head");
        var eyeCam = head.Find("EyeCamera").GetComponent<Camera>();
        var look = head.gameObject.AddComponent<SeatedLook>();
        var walk = player.gameObject.AddComponent<PlayerWalk>();   // F: 일어서기/앉기, 서면 WASD
        walk.head = head; walk.look = look;

        // ---------- 폰 카메라 ----------
        const string rtPath = "Assets/_Game/PhoneFeed.renderTexture";
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
        if (rt == null)
        {
            rt = new RenderTexture(720, 1280, 24) { name = "PhoneFeed", antiAliasing = 2 };
            AssetDatabase.CreateAsset(rt, rtPath);
        }
        var pcGo = new GameObject("PhoneCamera");
        pcGo.transform.SetParent(player, false);
        var pcam = pcGo.AddComponent<Camera>();
        pcam.targetTexture = rt;
        pcam.fieldOfView = 58; pcam.nearClipPlane = 0.03f; pcam.farClipPlane = 80;
        pcam.cullingMask = ~(1 << EyeOnly);
        pcam.depth = -2;
        var pdata = pcGo.AddComponent<UniversalAdditionalCameraData>();
        pdata.renderPostProcessing = true;
        pdata.volumeLayerMask = (1 << 0) | (1 << PhoneOnly);
        var edata = eyeCam.GetComponent<UniversalAdditionalCameraData>();
        edata.volumeLayerMask = (1 << 0) | (1 << EyeOnly);

        // ---------- 맨눈 / 폰 후처리 분리 ----------
        // 공통: 톤매핑·블룸. 맨눈: 따뜻하고 비네트(분위기에 취한 눈). 폰: 차갑고 노이즈(있는 그대로 찍는 센서).
        Volume("PP_Global", 0, Profile("PP_Global", p =>
        {
            var tm = p.Add<Tonemapping>(true); tm.mode.value = TonemappingMode.ACES;
            var b = p.Add<Bloom>(true); b.intensity.value = 0.7f; b.threshold.value = 0.95f; b.scatter.value = 0.7f;
        }));
        Volume("PP_Eye", EyeOnly, Profile("PP_Eye", p =>
        {
            var v = p.Add<Vignette>(true); v.intensity.value = 0.34f; v.smoothness.value = 0.5f;
            var c = p.Add<ColorAdjustments>(true); c.colorFilter.value = new Color(1f, 0.92f, 0.80f); c.saturation.value = 8f; c.postExposure.value = 0.45f;
        }));
        Volume("PP_Phone", PhoneOnly, Profile("PP_Phone", p =>
        {
            var g = p.Add<FilmGrain>(true); g.type.value = FilmGrainLookup.Medium3; g.intensity.value = 0.45f; g.response.value = 0.75f;
            var c = p.Add<ColorAdjustments>(true); c.colorFilter.value = new Color(0.92f, 0.97f, 1f); c.saturation.value = -12f; c.postExposure.value = 0.95f; c.contrast.value = 6f;
            var ca = p.Add<ChromaticAberration>(true); ca.intensity.value = 0.12f;
        }));

        // ---------- 폰 화면 UI ----------
        var canvasGo = new GameObject("PhoneCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1f;

        var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var dimmer = Img("Dimmer", canvasGo.transform, null, new Color(0, 0, 0, 0));
        Stretch(dimmer.rectTransform);

        var phoneRoot = new GameObject("Phone", typeof(RectTransform)).GetComponent<RectTransform>();
        phoneRoot.SetParent(canvasGo.transform, false);
        phoneRoot.sizeDelta = new Vector2(500, 960);
        var body = Img("Body", phoneRoot, rounded, new Color(0.05f, 0.05f, 0.06f));
        body.type = Image.Type.Sliced; body.pixelsPerUnitMultiplier = 0.12f;
        Stretch(body.rectTransform);
        var screenGo = new GameObject("Screen", typeof(RawImage));
        screenGo.transform.SetParent(phoneRoot, false);
        var screen = screenGo.GetComponent<RawImage>();
        screen.texture = rt; screen.raycastTarget = false;
        screen.rectTransform.sizeDelta = new Vector2(468, 832);
        var island = Img("Island", screen.rectTransform, rounded, Color.black);
        island.type = Image.Type.Sliced; island.pixelsPerUnitMultiplier = 0.3f;
        Place(island.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(118, 32));

        var recText = Txt("Rec", screen.rectTransform, "REC 00:00", 21, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(22, -64), new Vector2(200, 30));
        recText.rectTransform.pivot = new Vector2(0, 0.5f);
        var livePill = Img("LivePill", screen.rectTransform, rounded, new Color(0.9f, 0.16f, 0.15f, 0.92f));
        livePill.type = Image.Type.Sliced; livePill.pixelsPerUnitMultiplier = 0.35f;
        Place(livePill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(176, 34));
        var viewerText = Txt("Viewers", livePill.rectTransform, "LIVE  37", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176, 34));
        viewerText.font = fontBold;
        var batteryText = Txt("Battery", screen.rectTransform, "100%", 21, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-22, -64), new Vector2(120, 30));
        batteryText.rectTransform.pivot = new Vector2(1, 0.5f);
        var zoomText = Txt("Zoom", screen.rectTransform, "1x", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 318), new Vector2(90, 30));

        var chat = Img("Chat", screen.rectTransform, null, new Color(0, 0, 0, 0.32f));
        var chatRt = chat.rectTransform;
        chatRt.anchorMin = new Vector2(0, 0); chatRt.anchorMax = new Vector2(1, 0); chatRt.pivot = new Vector2(0.5f, 0);
        chatRt.offsetMin = new Vector2(0, 0); chatRt.offsetMax = new Vector2(0, 0);
        chatRt.sizeDelta = new Vector2(0, 290);

        // 몰래 모드: 화면 오른쪽 아래, 손에 든 폰 뒷면만 보임 (화면은 못 봄)
        var hidden = new GameObject("HiddenPhone", typeof(RectTransform)).GetComponent<RectTransform>();
        hidden.SetParent(canvasGo.transform, false);
        hidden.anchorMin = hidden.anchorMax = new Vector2(1, 0); hidden.pivot = new Vector2(1, 0);
        hidden.anchoredPosition = new Vector2(-170, -150); hidden.sizeDelta = new Vector2(200, 380);
        hidden.localRotation = Quaternion.Euler(0, 0, 9);
        var back = Img("Back", hidden, rounded, new Color(0.11f, 0.11f, 0.12f));
        back.type = Image.Type.Sliced; back.pixelsPerUnitMultiplier = 0.14f; Stretch(back.rectTransform);
        var bump = Img("CameraBump", hidden, rounded, new Color(0.18f, 0.18f, 0.2f));
        bump.type = Image.Type.Sliced; bump.pixelsPerUnitMultiplier = 0.3f;
        Place(bump.rectTransform, new Vector2(0, 1), new Vector2(22, -22), new Vector2(84, 84)); bump.rectTransform.pivot = new Vector2(0, 1);
        var lens = Img("Lens", bump.rectTransform, AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"), new Color(0.02f, 0.02f, 0.03f));
        Place(lens.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46));
        hidden.gameObject.SetActive(false);

        // ---------- 컨트롤러 연결 ----------
        var phone = player.gameObject.AddComponent<PhoneController>();
        phone.Head = head; phone.EyeCamera = eyeCam; phone.PhoneCamera = pcam; phone.Feed = rt; phone.UIFont = font;
        phone.PhoneRoot = phoneRoot; phone.ScreenArea = screen.rectTransform; phone.ChatArea = chatRt;
        phone.ViewerCountText = viewerText; phone.RecText = recText; phone.BatteryText = batteryText; phone.ZoomText = zoomText;
        phone.Dimmer = dimmer; phone.HiddenPhone = hidden;
        phoneRoot.gameObject.SetActive(false);

        // ---------- 매니저 ----------
        var managers = new GameObject("Managers");
        managers.AddComponent<SuspicionManager>();
        managers.AddComponent<SuspicionHUD>();
        managers.AddComponent<CaptureJudge>();
        managers.AddComponent<StreamFeed>();
        var flow = managers.AddComponent<GameFlow>(); flow.font = font;
        managers.AddComponent<ResultsScreen>();

        // ---------- 감시자 ----------
        foreach (Transform b in GameObject.Find("Believers").transform) AddWatcher(b, WatcherKind.Believer, b.Find("Head"));
        var cast = GameObject.Find("Cast").transform;
        foreach (var sn in new[] { "Staff_Aisle", "Staff_Side" })
        {
            var staff = cast.Find(sn);
            AddWatcher(staff, WatcherKind.Staff, staff.Find("Head"));
            var sg = staff.gameObject.AddComponent<LeaderGaze>();   // 관리자도 좌우로 둘러본다 → 틈이 생김
            sg.sweeping = true; sg.maxYaw = 55f;
        }
        var leader = cast.Find("Leader");
        AddWatcher(leader, WatcherKind.Leader, leader.Find("Head"));
        var gaze = leader.gameObject.AddComponent<LeaderGaze>();
        gaze.sweeping = true;
        foreach (Transform c in GameObject.Find("CCTV").transform) AddWatcher(c, WatcherKind.CCTV, c);

        // ---------- 판정 대상 ----------
        var victim = cast.Find("Victim");
        var miracleT = Target(victim.gameObject, "miracle_wire", "공중부양 기적의 실체 (와이어)", CaptureCategory.Climax, 30000, 0.25f, 3f, 90);
        var trick = GameObject.Find("Trick_PhoneOnly").transform;
        miracleT.renderers = new Renderer[] { victim.Find("Body").GetComponent<Renderer>(), victim.Find("Head").GetComponent<Renderer>(), trick.Find("Wire").GetComponent<Renderer>() };
        miracleT.activeNow = false;
        var assistant = trick.Find("Assistant");
        var asT = Target(assistant.gameObject, "assistant", "커튼 뒤 조명 조수", CaptureCategory.Evidence, 6000, 0.22f, 2f, 40);
        asT.activeNow = true;
        int i = 0;
        foreach (Transform c in GameObject.Find("CCTV").transform)
        {
            var ct = Target(c.gameObject, "cctv_" + (i++), "숨은 감시 카메라 (적외선)", CaptureCategory.Evidence, 3000, 0.10f, 2f, 25);
            ct.activeNow = true;
        }
        var bannerPhone = GameObject.Find("BannerText_Phone");
        var bt = Target(bannerPhone, "banner", "현수막의 속뜻", CaptureCategory.Evidence, 5000, 0.10f, 2f, 50);
        bt.renderers = new Renderer[] { bannerPhone.GetComponent<Renderer>() };
        bt.activeNow = true;
        var lt = Target(leader.gameObject, "leader", "설교하는 교주", CaptureCategory.Atmosphere, 1500, 0.3f, 1.5f, 5);
        lt.activeNow = true;
        var crowd = GameObject.Find("Believers");
        var crT = Target(crowd, "crowd", "넋 놓고 앉은 신도들", CaptureCategory.Atmosphere, 900, 0.3f, 1.5f, 2);
        crT.activeNow = true;
        var sermon = new GameObject("SermonSound");
        sermon.transform.position = leader.position + Vector3.up * 1.7f;
        var st = Target(sermon, "sermon", "설교 녹음: 폰 금지 교리", CaptureCategory.Sound, 4000, 0.25f, 0f, 60);
        st.hearingRadius = 16f;

        // ---------- 진행 ----------
        var trickSpot = new GameObject("TrickSpot").AddComponent<Light>();
        trickSpot.transform.SetParent(trick, false);
        trickSpot.transform.position = new Vector3(5.0f, 2.0f, 12.0f);
        trickSpot.transform.LookAt(victim.position + Vector3.up * 1.2f);
        trickSpot.type = LightType.Spot; trickSpot.color = new Color(1f, 0.95f, 0.85f); trickSpot.intensity = 6f;
        trickSpot.range = 10f; trickSpot.spotAngle = 30f; trickSpot.enabled = false;
        var director = managers.AddComponent<ServiceDirector>();
        director.victim = victim;
        director.eyeOnlyMiracle = GameObject.Find("Miracle_EyeOnly") ?? FindInactive("Miracle_EyeOnly");
        director.wire = trick.Find("Wire");
        director.trickSpot = trickSpot;
        director.leaderGaze = gaze;
        director.sermonSound = st;
        director.miracleTarget = miracleT;
    }

    static GameObject FindInactive(string name)
    {
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == name && go.scene.IsValid()) return go;
        return null;
    }

    static void AddWatcher(Transform t, WatcherKind kind, Transform eye)
    {
        var w = t.gameObject.AddComponent<Watcher>();
        w.kind = kind;
        w.ApplyKindDefaults();
        w.eye = eye != null ? eye : t;
    }

    static CaptureTarget Target(GameObject go, string id, string name, CaptureCategory cat, int views, float reqH, float hold, int evidence)
    {
        var t = go.AddComponent<CaptureTarget>();
        t.category = cat;
        t.ApplyCategoryDefaults();
        t.targetId = id; t.displayName = name; t.baseViews = views; t.requiredHeight = reqH;
        if (hold > 0) t.holdSeconds = hold;
        t.evidenceValue = evidence;
        return t;
    }

    static VolumeProfile Profile(string name, System.Action<VolumeProfile> fill)
    {
        string path = $"Assets/_Game/Settings/{name}.asset";
        System.IO.Directory.CreateDirectory("Assets/_Game/Settings");
        AssetDatabase.DeleteAsset(path);
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, path);
        fill(p);
        foreach (var c in p.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, p); }
        EditorUtility.SetDirty(p);
        return p;
    }

    static void Volume(string name, int layer, VolumeProfile profile)
    {
        var go = new GameObject(name) { layer = layer };
        var v = go.AddComponent<Volume>();
        v.isGlobal = true; v.sharedProfile = profile; v.priority = layer == 0 ? 0 : 1;
    }

    static Image Img(string name, Transform parent, Sprite sprite, Color c)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite; img.color = c; img.raycastTarget = false;
        return img;
    }

    static Text Txt(string name, Transform parent, string text, int size, TextAnchor anchor, Vector2 anchorPos, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(Text), typeof(Shadow));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font; t.text = text; t.fontSize = size; t.alignment = anchor; t.color = Color.white;
        t.supportRichText = true; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        Place(t.rectTransform, anchorPos, pos, sizeDelta);
        go.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
        return t;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
