using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// 회색 박스 예배당을 한 번에 짓는 빌더. 다시 실행하면 씬을 새로 만든다.
public static class ChapelGreybox
{
    public const int EyeOnly = 6;    // 맨눈 카메라에만 보이는 레이어 (기적 연출)
    public const int PhoneOnly = 7;  // 폰 카메라에만 보이는 레이어 (속임수·적외선·암호)
    public const string ScenePath = "Assets/_Game/Scenes/Chapel.unity";

    static Shader lit;

    [MenuItem("UndercoverLive/Build Chapel Greybox")]
    public static string Build()
    {
        lit = Shader.Find("Universal Render Pipeline/Lit");
        EnsureLayers();
        System.IO.Directory.CreateDirectory("Assets/_Game/Materials");
        System.IO.Directory.CreateDirectory("Assets/_Game/Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var mFloor = Mat("Floor", new Color(0.16f, 0.12f, 0.10f), 0.35f);
        var mWall = Mat("Wall", new Color(0.50f, 0.47f, 0.42f), 0.1f);
        var mWood = Mat("Wood", new Color(0.30f, 0.19f, 0.11f), 0.3f);
        var mStage = Mat("StageCloth", new Color(0.33f, 0.06f, 0.07f), 0.15f);
        var mBeliever = Mat("Believer", new Color(0.86f, 0.86f, 0.83f), 0.1f);
        var mSkin = Mat("Skin", new Color(0.78f, 0.62f, 0.50f), 0.2f);
        var mLeader = Mat("Leader", new Color(0.92f, 0.90f, 0.85f), 0.2f);
        var mLeaderSash = Mat("LeaderSash", new Color(0.55f, 0.07f, 0.09f), 0.3f);
        var mStaff = Mat("Staff", new Color(0.07f, 0.07f, 0.09f), 0.25f);
        var mVictim = Mat("Victim", new Color(0.62f, 0.64f, 0.70f), 0.1f);
        var mGold = Mat("Gold", new Color(0.78f, 0.60f, 0.22f), 0.75f);
        var mBanner = Mat("Banner", new Color(0.90f, 0.88f, 0.80f), 0.0f);
        var mCandle = Mat("Candle", new Color(0.95f, 0.92f, 0.82f), 0.1f, new Color(1.2f, 0.7f, 0.25f));
        var mIR = Mat("IR_LED", Color.black, 0.5f, new Color(6f, 0.15f, 0.12f));
        var mWire = Mat("Wire", new Color(0.04f, 0.04f, 0.04f), 0.4f);
        var mGlow = Mat("MiracleGlow", new Color(1f, 0.92f, 0.65f), 0f, new Color(4f, 3.2f, 1.6f));
        var mCCTV = Mat("CCTV", new Color(0.10f, 0.10f, 0.11f), 0.5f);
        var mRig = Mat("LightRig", new Color(0.2f, 0.2f, 0.22f), 0.5f);

        var root = new GameObject("Chapel").transform;

        // 홀: 폭 16 · 길이 28 · 높이 7. 단상은 +Z 쪽.
        Box("Floor", root, new Vector3(0, -0.05f, 0), new Vector3(16, 0.1f, 28), mFloor);
        Box("Ceiling", root, new Vector3(0, 7.05f, 0), new Vector3(16, 0.1f, 28), mWall);
        Box("Wall_L", root, new Vector3(-8.05f, 3.5f, 0), new Vector3(0.1f, 7, 28), mWall);
        Box("Wall_R", root, new Vector3(8.05f, 3.5f, 0), new Vector3(0.1f, 7, 28), mWall);
        Box("Wall_Front", root, new Vector3(0, 3.5f, 14.05f), new Vector3(16, 7, 0.1f), mWall);
        Box("Wall_Back", root, new Vector3(0, 3.5f, -14.05f), new Vector3(16, 7, 0.1f), mWall);
        for (int i = 0; i < 4; i++)
        {
            float z = -9 + i * 6;
            Box("Pillar_L" + i, root, new Vector3(-7.4f, 3.5f, z), new Vector3(0.6f, 7, 0.6f), mWall);
            Box("Pillar_R" + i, root, new Vector3(7.4f, 3.5f, z), new Vector3(0.6f, 7, 0.6f), mWall);
        }

        // 단상
        var stage = Group("Stage", root);
        Box("Platform", stage, new Vector3(0, 0.4f, 11.5f), new Vector3(12, 0.8f, 5), mStage);
        Box("Step", stage, new Vector3(0, 0.2f, 8.75f), new Vector3(4, 0.4f, 0.5f), mStage);
        Box("Pulpit", stage, new Vector3(-2.6f, 1.35f, 10.2f), new Vector3(1.0f, 1.1f, 0.6f), mWood);   // 교주를 가리지 않게 옆으로
        Box("Curtain_L", stage, new Vector3(-5.2f, 3.0f, 13.5f), new Vector3(1.8f, 4.4f, 0.2f), mStage);
        Box("Curtain_R", stage, new Vector3(5.2f, 3.0f, 13.5f), new Vector3(1.8f, 4.4f, 0.2f), mStage);
        Box("Banner", stage, new Vector3(0, 4.7f, 13.95f), new Vector3(7.5f, 1.5f, 0.04f), mBanner);
        var eyeText = Label("BannerText_Eye", stage, new Vector3(0, 4.7f, 13.9f), "사랑 · 평화 · 치유", new Color(0.45f, 0.12f, 0.1f), 0.032f);
        eyeText.layer = EyeOnly;
        var phoneText = Label("BannerText_Phone", stage, new Vector3(0, 4.7f, 13.89f), "헌금 목표 3억 · 탈퇴자 관리", new Color(0.05f, 0.05f, 0.05f), 0.026f);
        phoneText.layer = PhoneOnly;

        // 촛불 (단상 가장자리와 제단 위)
        var candles = Group("Candles", root);
        float[] cxs = { -4.5f, -3f, -1.5f, 1.5f, 3f, 4.5f };
        foreach (var cx in cxs) Candle(candles, new Vector3(cx, 0.8f, 9.3f), mCandle, 1.1f);
        Candle(candles, new Vector3(-1.2f, 1.6f, 12.9f), mCandle, 1.4f);
        Candle(candles, new Vector3(1.2f, 1.6f, 12.9f), mCandle, 1.4f);

        // 예배 의자와 신도들. 신도는 +Z(단상)를 바라본다.
        var pews = Group("Chairs", root);
        var mChairFrame = Mat("ChairFrame", new Color(0.32f, 0.33f, 0.35f), 0.55f);
        var mChairSeat = Mat("ChairSeat", new Color(0.13f, 0.15f, 0.22f), 0.2f);
        var believers = Group("Believers", root);
        var rnd = new System.Random(7);
        Vector3 playerSeat = Vector3.zero;
        int row = 0;
        for (float z = -11f; z <= 6.2f; z += 1.55f, row++)
        {
            foreach (int side in new[] { -1, 1 })
            {
                float cx = side * 3.9f;
                var pew = Group($"Row_{row:00}_{(side < 0 ? "L" : "R")}", pews);
                pew.localPosition = new Vector3(cx, 0, z);
                for (int k = 0; k < 5; k++) CultDressing.FoldingChair(pew, new Vector3(-2.2f + k * 1.1f, 0, 0), mChairFrame, mChairSeat);
                for (int s = 0; s < 5; s++)
                {
                    float sx = cx - 2.2f + s * 1.1f;
                    if (row == 6 && side < 0 && s == 3) { playerSeat = new Vector3(sx, 0, z); continue; }
                    if (rnd.NextDouble() < 0.25) continue;
                    var p = Person($"Believer_{row:00}{(side < 0 ? "L" : "R")}{s}", believers, new Vector3(sx, 0.45f, z + 0.02f), mBeliever, mSkin, 0.88f, true);
                    p.transform.localRotation = Quaternion.Euler(0, (float)(rnd.NextDouble() * 16 - 8), 0);
                }
            }
        }

        // 교주와 피해 신도, 관리자
        var people = Group("Cast", root);
        var leader = Person("Leader", people, new Vector3(0, 0.93f, 12.6f), mLeader, mSkin, 1.15f, false);   // 교주 의자에 앉음
        leader.transform.localRotation = Quaternion.Euler(0, 180, 0);
        Box("Sash", leader.transform, new Vector3(0, 0.95f, -0.18f), new Vector3(0.1f, 1.0f, 0.04f), mLeaderSash);
        var victim = Person("Victim", people, new Vector3(1.5f, 0.8f, 10.2f), mVictim, mSkin, 0.8f, true);
        victim.transform.localRotation = Quaternion.Euler(0, 200, 0);
        var staffA = Person("Staff_Aisle", people, new Vector3(0, 0, -9.5f), mStaff, mSkin, 1.0f, false);
        staffA.transform.localRotation = Quaternion.Euler(0, 0, 0);
        var staffB = Person("Staff_Side", people, new Vector3(-6.6f, 0, 7.2f), mStaff, mSkin, 1.0f, false);
        staffB.transform.localRotation = Quaternion.Euler(0, 180, 0);

        // 기적 연출: 맨눈에만 보이는 빛 기둥 (사건 때 켜짐)
        var miracle = Group("Miracle_EyeOnly", root);
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "LightPillar"; beam.transform.SetParent(miracle, false);
        beam.transform.localPosition = new Vector3(1.5f, 3.9f, 10.2f);
        beam.transform.localScale = new Vector3(1.4f, 3.1f, 1.4f);
        beam.GetComponent<Renderer>().sharedMaterial = mGlow;
        Object.DestroyImmediate(beam.GetComponent<Collider>());
        SetLayerRecursive(miracle.gameObject, EyeOnly);
        miracle.gameObject.SetActive(false);

        // 속임수: 폰에만 보이는 와이어와 커튼 뒤 조명 조수
        var trick = Group("Trick_PhoneOnly", root);
        var wire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wire.name = "Wire"; wire.transform.SetParent(trick, false);
        wire.transform.localPosition = new Vector3(1.5f, 4.6f, 10.2f);
        wire.transform.localScale = new Vector3(0.02f, 2.4f, 0.02f);
        wire.GetComponent<Renderer>().sharedMaterial = mWire;
        Object.DestroyImmediate(wire.GetComponent<Collider>());
        var assistant = Person("Assistant", trick, new Vector3(5.6f, 0.8f, 12.6f), mStaff, mSkin, 1.0f, false);
        assistant.transform.localRotation = Quaternion.Euler(0, 230, 0);
        Box("LightRig", trick, new Vector3(5.2f, 1.75f, 12.2f), new Vector3(0.5f, 0.35f, 0.35f), mRig);
        SetLayerRecursive(trick.gameObject, PhoneOnly);

        // 숨은 감시 카메라: 몸체는 누구나 볼 수 있지만 어두운 천장 구석이라 잘 안 보이고, 적외선 불빛은 폰에만 보인다.
        var cctv = Group("CCTV", root);
        CCTV(cctv, "CCTV_FrontL", new Vector3(-7.6f, 6.6f, 13.4f), new Vector3(28, 150, 0), mCCTV, mIR);
        CCTV(cctv, "CCTV_FrontR", new Vector3(7.6f, 6.6f, 13.4f), new Vector3(28, 210, 0), mCCTV, mIR);
        CCTV(cctv, "CCTV_BackR", new Vector3(7.6f, 6.6f, -13.4f), new Vector3(30, 315, 0), mCCTV, mIR);

        // 조명: 어둡게. 무대 스포트라이트와 촛불이 주광.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.13f, 0.11f, 0.10f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.035f, 0.03f, 0.028f);
        RenderSettings.fogDensity = 0.03f;
        var lights = Group("Lights", root);
        var spot = NewLight("StageSpot", lights, LightType.Spot, new Vector3(0, 6.6f, 5.5f), new Color(1f, 0.86f, 0.66f), 9f, 22f);
        spot.transform.LookAt(new Vector3(0.6f, 1.5f, 10.8f));
        spot.spotAngle = 48; spot.innerSpotAngle = 30; spot.shadows = LightShadows.Soft;
        for (int i = 0; i < 4; i++)
        {
            float z = -9 + i * 6;
            NewLight("Sconce_L" + i, lights, LightType.Point, new Vector3(-7.0f, 3.2f, z + 0.5f), new Color(1f, 0.6f, 0.3f), 1.6f, 7f);
            NewLight("Sconce_R" + i, lights, LightType.Point, new Vector3(7.0f, 3.2f, z + 0.5f), new Color(1f, 0.6f, 0.3f), 1.6f, 7f);
        }

        // 플레이어 시점: 앉은 눈높이, 단상을 바라봄. 폰 전용 레이어는 맨눈 카메라에서 뺀다.
        var player = new GameObject("Player").transform;
        player.position = playerSeat + new Vector3(0, 0, 0.02f);
        var head = new GameObject("Head").transform;
        head.SetParent(player, false);
        head.localPosition = new Vector3(0, 1.22f, 0);
        var camGo = new GameObject("EyeCamera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(head, false);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 68; cam.nearClipPlane = 0.05f; cam.farClipPlane = 80;
        cam.cullingMask = ~(1 << PhoneOnly);
        camGo.AddComponent<AudioListener>();
        var camData = camGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
                      ?? camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        head.LookAt(new Vector3(0.3f, 1.9f, 10.8f));

        CultDressing.Dress(root, lit, AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Fonts/Pretendard-Bold.otf"));
        ChapelRig.Apply();
        Debug.Log(ChapelCharacters.Apply());
        var director = Object.FindAnyObjectByType<UndercoverLive.ServiceDirector>();
        if (director != null)
        {
            director.blindfolds = CultDressing.AddBlindfolds(Mat("BlindfoldWhite", new Color(0.95f, 0.95f, 0.93f), 0.1f));
            CultDressing.ApplyCopy(director);
            // 이 문안에서는 교주 자신이 떠오른다
            var leaderT = GameObject.Find("Cast/Leader").transform;
            director.victim = leaderT;
            var pillar = director.eyeOnlyMiracle != null ? director.eyeOnlyMiracle.transform.Find("LightPillar") : null;
            if (pillar != null) pillar.position = new Vector3(leaderT.position.x, 3.9f, leaderT.position.z);
            if (director.trickSpot != null) director.trickSpot.transform.LookAt(leaderT.position + Vector3.up * 1.4f);
            if (director.miracleTarget != null)
            {
                Object.DestroyImmediate(director.miracleTarget);
                var mt = leaderT.gameObject.AddComponent<UndercoverLive.CaptureTarget>();
                mt.category = UndercoverLive.CaptureCategory.Climax; mt.ApplyCategoryDefaults();
                mt.targetId = "miracle_wire"; mt.displayName = "공중부양 기적의 실체 (와이어)"; mt.baseViews = 30000; mt.requiredHeight = 0.25f; mt.holdSeconds = 3f; mt.evidenceValue = 90;
                var rs = new System.Collections.Generic.List<Renderer>(leaderT.GetComponentsInChildren<SkinnedMeshRenderer>(true));
                rs.Add(director.wire.GetComponent<Renderer>());
                mt.renderers = rs.ToArray(); mt.activeNow = false;
                director.miracleTarget = mt;
            }
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();

        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            sv.LookAt(new Vector3(0, 1.5f, 1.5f), Quaternion.Euler(32, 205, 0), 20f);
            sv.Repaint();
        }
        return "built " + ScenePath + " seat=" + playerSeat;
    }

    static void EnsureLayers()
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        layers.GetArrayElementAtIndex(EyeOnly).stringValue = "EyeOnly";
        layers.GetArrayElementAtIndex(PhoneOnly).stringValue = "PhoneOnly";
        tm.ApplyModifiedProperties();
    }

    static Material Mat(string name, Color c, float smooth, Color? emission = null)
    {
        var path = $"Assets/_Game/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
        m.shader = lit;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    // 사람 자리표시자: 몸통 캡슐 + 머리 구. seated면 의자 높이에서 시작.
    static GameObject Person(string name, Transform parent, Vector3 basePos, Material body, Material skin, float scale, bool seated)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = basePos;
        float torsoH = seated ? 0.85f : 1.45f;
        var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        torso.name = "Body";
        torso.transform.SetParent(root.transform, false);
        torso.transform.localScale = new Vector3(0.5f, torsoH * 0.5f, 0.36f) * scale;
        torso.transform.localPosition = new Vector3(0, torsoH * 0.5f * scale, 0);
        torso.GetComponent<Renderer>().sharedMaterial = body;
        var headGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        headGo.name = "Head";
        headGo.transform.SetParent(root.transform, false);
        headGo.transform.localScale = Vector3.one * 0.26f * scale;
        headGo.transform.localPosition = new Vector3(0, (torsoH + 0.14f) * scale, 0);
        headGo.GetComponent<Renderer>().sharedMaterial = skin;
        return root;
    }

    static void Candle(Transform parent, Vector3 pos, Material m, float intensity)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = "Candle";
        c.transform.SetParent(parent, false);
        c.transform.localPosition = pos + new Vector3(0, 0.15f, 0);
        c.transform.localScale = new Vector3(0.07f, 0.15f, 0.07f);
        c.GetComponent<Renderer>().sharedMaterial = m;
        Object.DestroyImmediate(c.GetComponent<Collider>());
        var l = NewLight("Flame", parent, LightType.Point, pos + new Vector3(0, 0.42f, 0), new Color(1f, 0.62f, 0.28f), intensity, 4.5f);
        l.shadows = LightShadows.None;
    }

    static void CCTV(Transform parent, string name, Vector3 pos, Vector3 euler, Material body, Material ir)
    {
        var root = Group(name, parent);
        root.localPosition = pos;
        root.localRotation = Quaternion.Euler(euler);
        Box("Housing", root, new Vector3(0, 0, 0), new Vector3(0.22f, 0.18f, 0.42f), body);
        var led = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        led.name = "IR_Glow";
        led.transform.SetParent(root, false);
        led.transform.localPosition = new Vector3(0, 0, 0.23f);
        led.transform.localScale = Vector3.one * 0.12f;
        led.GetComponent<Renderer>().sharedMaterial = ir;
        Object.DestroyImmediate(led.GetComponent<Collider>());
        led.layer = PhoneOnly;
    }

    static GameObject Label(string name, Transform parent, Vector3 pos, string text, Color color, float charSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.identity;   // 관객(-Z 쪽)에서 바로 읽히게
        var tm = go.AddComponent<TextMesh>();
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Fonts/Pretendard-Bold.otf");
        tm.font = font;
        tm.text = text;
        tm.fontSize = 96;
        tm.characterSize = charSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        return go;
    }

    static Light NewLight(string name, Transform parent, LightType type, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = type; l.color = color; l.intensity = intensity; l.range = range;
        return l;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
    }
}
