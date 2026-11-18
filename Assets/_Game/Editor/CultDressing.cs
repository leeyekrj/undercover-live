using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UndercoverLive;

// '눈의 교단' 집회장 꾸미기: 교주 의자·초상화·상징 현수막·카운트다운·헌신 목표판·가린 창문·눈가리개, 문안 적용.
public static class CultDressing
{
    [System.Serializable]
    public class Sermon { public string opening, phone_ban, before_miracle, miracle, warning, closing; }
    [System.Serializable]
    public class Copy
    {
        public string name = "맑은눈 공동체", leader_title = "눈뜬 자", member_title = "식구";
        public string slogan_eye = "보지 말고 믿으라", slogan_phone = "헌금 목표 3억 · 탈퇴자 관리 명단";
        public string chant = "눈을 감으라, 아버지가 보신다";
        public string countdown = "심판까지 D-{D}", donation_board = "이달 헌신 목표";
        public Sermon sermon;
    }

    const string CopyPath = "Assets/_Game/cult-copy.json";
    const string TexDir = "Assets/_Game/Textures";

    public static Copy LoadCopy()
    {
        var c = new Copy();
        if (File.Exists(CopyPath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(CopyPath), c);
        return c;
    }

    // 무대·벽 소품. ChapelGreybox가 홀을 지은 뒤 부른다.
    public static void Dress(Transform root, Shader lit, Font font)
    {
        var copy = LoadCopy();
        Directory.CreateDirectory(TexDir);
        var stage = root.Find("Stage");

        var mWhite = Mat("ThroneWhite", lit, new Color(0.92f, 0.91f, 0.87f), 0.35f);
        var mGold = Mat("Gold", lit, new Color(0.78f, 0.60f, 0.22f), 0.75f);
        var mBlack = Mat("BoardBlack", lit, new Color(0.03f, 0.03f, 0.035f), 0.2f);
        var mBanner = Mat("BannerCloth", lit, new Color(0.42f, 0.05f, 0.06f), 0.1f);
        var mBlackout = Mat("Blackout", lit, new Color(0.06f, 0.06f, 0.07f), 0.05f);
        var mTape = Mat("Tape", lit, new Color(0.55f, 0.52f, 0.45f), 0.1f);
        var mRed = Mat("LedRed", lit, Color.black, 0.2f, new Color(3f, 0.15f, 0.1f));
        var mBarFill = Mat("BarFill", lit, new Color(0.85f, 0.65f, 0.2f), 0.5f, new Color(0.6f, 0.4f, 0.05f));

        // 교주 의자 (제단 대신)
        var throne = new GameObject("Throne").transform; throne.SetParent(stage, false);
        throne.localPosition = new Vector3(0, 0.8f, 12.7f);
        Box("Seat", throne, new Vector3(0, 0.5f, 0), new Vector3(1.1f, 0.14f, 0.9f), mWhite);
        Box("Back", throne, new Vector3(0, 1.35f, 0.42f), new Vector3(1.1f, 1.7f, 0.14f), mWhite);
        Box("ArmL", throne, new Vector3(-0.6f, 0.72f, 0), new Vector3(0.12f, 0.35f, 0.85f), mGold);
        Box("ArmR", throne, new Vector3(0.6f, 0.72f, 0), new Vector3(0.12f, 0.35f, 0.85f), mGold);
        Box("Base", throne, new Vector3(0, 0.22f, 0), new Vector3(1.0f, 0.44f, 0.8f), mWhite);

        // 교주 초상화 (Argon 그림이 있으면 그걸, 없으면 자리표시)
        var portrait = new GameObject("Portrait").transform; portrait.SetParent(stage, false);
        portrait.localPosition = new Vector3(0, 3.95f, 13.92f);
        Box("Frame", portrait, Vector3.zero, new Vector3(2.5f, 3.2f, 0.08f), mGold);
        var pTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/leader_portrait.png");
        var mPortrait = Mat("Portrait", lit, pTex != null ? new Color(0.55f, 0.53f, 0.5f) : new Color(0.2f, 0.16f, 0.12f), 0.0f);
        if (pTex != null) mPortrait.SetTexture("_BaseMap", pTex);
        var face = Quad("Canvas", portrait, new Vector3(0, 0, -0.045f), new Vector2(2.2f, 2.93f), mPortrait);

        // 상징 현수막 두 장 (겹쳐진 두 원 안의 눈)
        var emblem = EmblemTexture();
        var mEmblem = Mat("Emblem", lit, Color.white, 0.2f, new Color(0.55f, 0.42f, 0.12f));
        mEmblem.SetTexture("_BaseMap", emblem); mEmblem.SetTexture("_EmissionMap", emblem);
        mEmblem.SetFloat("_AlphaClip", 1); mEmblem.SetFloat("_Cutoff", 0.5f); mEmblem.EnableKeyword("_ALPHATEST_ON"); mEmblem.renderQueue = 2450;
        foreach (float x in new[] { -3.9f, 3.9f })
        {
            var hang = new GameObject(x < 0 ? "EmblemBanner_L" : "EmblemBanner_R").transform; hang.SetParent(stage, false);
            hang.localPosition = new Vector3(x, 4.1f, 13.93f);
            Box("Cloth", hang, Vector3.zero, new Vector3(1.4f, 3.8f, 0.03f), mBanner);
            Quad("Emblem", hang, new Vector3(0, 0.75f, -0.02f), new Vector2(1.2f, 1.2f), mEmblem);
        }

        // 위쪽 긴 현수막: 맨눈 문구 / 폰으로 읽히는 속뜻
        var top = stage.Find("Banner");
        if (top != null) { top.localPosition = new Vector3(0, 6.3f, 13.95f); top.localScale = new Vector3(9f, 0.9f, 0.04f); }
        var eyeText = GameObject.Find("BannerText_Eye");
        var phoneText = GameObject.Find("BannerText_Phone");
        if (eyeText != null) { eyeText.transform.localPosition = new Vector3(0, 6.3f, 13.9f); eyeText.GetComponent<TextMesh>().text = copy.slogan_eye; eyeText.GetComponent<TextMesh>().characterSize = 0.03f; }
        if (phoneText != null) { phoneText.transform.localPosition = new Vector3(0, 6.3f, 13.89f); phoneText.GetComponent<TextMesh>().text = copy.slogan_phone; phoneText.GetComponent<TextMesh>().characterSize = 0.022f; }

        // 종말 카운트다운판 (왼쪽 벽, 객석을 향함)
        var cd = new GameObject("Countdown").transform; cd.SetParent(root, false);
        cd.localPosition = new Vector3(-7.95f, 4.6f, 6.5f); cd.localRotation = Quaternion.Euler(0, 90, 0);
        Box("Board", cd, Vector3.zero, new Vector3(3.0f, 1.0f, 0.06f), mBlack);
        Label("Text", cd, new Vector3(0, 0, -0.05f), copy.countdown.Replace("{D}", "41"), new Color(1f, 0.15f, 0.1f), 0.05f, font);

        // 헌신 목표 막대판 (오른쪽 벽)
        var db = new GameObject("DonationBoard").transform; db.SetParent(root, false);
        db.localPosition = new Vector3(7.95f, 4.4f, 6.5f); db.localRotation = Quaternion.Euler(0, -90, 0);
        Box("Board", db, Vector3.zero, new Vector3(3.0f, 1.4f, 0.06f), mBlack);
        Label("Title", db, new Vector3(0, 0.36f, -0.05f), copy.donation_board, new Color(0.95f, 0.85f, 0.6f), 0.035f, font);
        Box("BarBack", db, new Vector3(0, -0.2f, 0.04f), new Vector3(2.5f, 0.32f, 0.02f), mTape);
        Box("BarFill", db, new Vector3(1.25f - 2.5f * 0.87f * 0.5f, -0.2f, 0.05f), new Vector3(2.5f * 0.87f, 0.28f, 0.02f), mBarFill);
        Label("Pct", db, new Vector3(0, -0.2f, -0.07f), "87%", Color.black, 0.03f, font);

        // 가린 창문 (옛 극장 옆벽)
        for (int i = 0; i < 3; i++)
        {
            float z = -8f + i * 6f;
            foreach (float side in new[] { -1f, 1f })
            {
                var w = new GameObject("Blackout").transform; w.SetParent(root, false);
                w.localPosition = new Vector3(side * 7.97f, 4.2f, z);
                w.localRotation = Quaternion.Euler(0, side < 0 ? 90 : -90, 0);
                Box("Panel", w, Vector3.zero, new Vector3(1.6f, 2.2f, 0.04f), mBlackout);
                Box("TapeA", w, new Vector3(0, 0, -0.03f), new Vector3(2.4f, 0.08f, 0.01f), mTape).transform.localRotation = Quaternion.Euler(0, 0, 54);
                Box("TapeB", w, new Vector3(0, 0, -0.03f), new Vector3(2.4f, 0.08f, 0.01f), mTape).transform.localRotation = Quaternion.Euler(0, 0, -54);
            }
        }
    }

    // 접이식 의자 하나 (장의자 대신)
    public static void FoldingChair(Transform parent, Vector3 pos, Material frame, Material seat)
    {
        var c = new GameObject("Chair").transform; c.SetParent(parent, false); c.localPosition = pos;
        Box("Seat", c, new Vector3(0, 0.45f, 0), new Vector3(0.46f, 0.05f, 0.44f), seat);
        Box("Back", c, new Vector3(0, 0.75f, -0.21f), new Vector3(0.44f, 0.34f, 0.04f), seat);
        foreach (var x in new[] { -0.2f, 0.2f })
        {
            Box("LegF", c, new Vector3(x, 0.22f, 0.18f), new Vector3(0.025f, 0.45f, 0.025f), frame);
            Box("LegB", c, new Vector3(x, 0.45f, -0.2f), new Vector3(0.025f, 0.9f, 0.025f), frame);
        }
    }

    // 신도 머리에 흰 눈가리개 (평소엔 꺼 둠)
    public static GameObject[] AddBlindfolds(Material white)
    {
        var list = new List<GameObject>();
        foreach (Transform b in GameObject.Find("Believers").transform)
        {
            var model = b.Find("Model");
            var anim = model != null ? model.GetComponent<Animator>() : null;
            var head = anim != null ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null) continue;
            var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(band.GetComponent<Collider>());
            band.name = "Blindfold";
            band.transform.position = head.position + model.forward * 0.035f + Vector3.up * 0.095f;
            band.transform.rotation = model.rotation;
            band.transform.localScale = new Vector3(0.2f, 0.028f, 0.215f);
            band.transform.SetParent(head, true);
            band.GetComponent<Renderer>().sharedMaterial = white;
            band.layer = b.gameObject.layer;
            band.SetActive(false);
            list.Add(band);
        }
        return list.ToArray();
    }

    // 문안을 진행 스크립트에 넣는다
    public static void ApplyCopy(ServiceDirector d)
    {
        var c = LoadCopy();
        d.leaderTitle = c.leader_title;
        d.membersLabel = c.member_title + "들";
        d.chant = c.chant;
        if (c.sermon != null)
        {
            if (!string.IsNullOrEmpty(c.sermon.opening)) d.lineOpening = c.sermon.opening;
            if (!string.IsNullOrEmpty(c.sermon.phone_ban)) d.linePhoneBan = c.sermon.phone_ban;
            if (!string.IsNullOrEmpty(c.sermon.before_miracle)) d.lineBlindfold = c.sermon.before_miracle;
            if (!string.IsNullOrEmpty(c.sermon.miracle)) d.lineMiracle = c.sermon.miracle;
            if (!string.IsNullOrEmpty(c.sermon.warning)) d.lineWarning = c.sermon.warning;
            if (!string.IsNullOrEmpty(c.sermon.closing)) d.lineClosing = c.sermon.closing;
        }
    }

    // 겹쳐진 두 원 안의 눈 — 코드로 그린 상징
    static Texture2D EmblemTexture()
    {
        string path = $"{TexDir}/emblem.png";
        const int N = 512;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color32[N * N];
        Vector2 c1 = new Vector2(N * 0.5f - 62, N * 0.5f), c2 = new Vector2(N * 0.5f + 62, N * 0.5f);
        float R = 150, ring = 13;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d1 = Mathf.Abs(Vector2.Distance(p, c1) - R), d2 = Mathf.Abs(Vector2.Distance(p, c2) - R);
                bool on = d1 < ring * 0.5f || d2 < ring * 0.5f;
                // 눈: 두 원이 겹친 렌즈 모양 안에 눈동자
                Vector2 q = p - new Vector2(N * 0.5f, N * 0.5f);
                bool inLens = Vector2.Distance(p, c1) < R && Vector2.Distance(p, c2) < R;
                float iris = q.magnitude;
                if (inLens && iris < 46 && iris > 30) on = true;
                if (inLens && iris < 16) on = true;
                px[y * N + x] = on ? new Color32(235, 190, 90, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material Mat(string name, Shader lit, Color c, float smooth, Color? emission = null)
    {
        var path = $"Assets/_Game/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
        m.shader = lit; m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth);
        if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static GameObject Quad(string name, Transform parent, Vector3 pos, Vector2 size, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localRotation = Quaternion.identity;   // 쿼드 앞면이 객석(-Z)을 향하게
        g.transform.localScale = new Vector3(size.x, size.y, 1);
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static GameObject Label(string name, Transform parent, Vector3 pos, string text, Color color, float charSize, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, -pos.z);
        go.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var tm = go.AddComponent<TextMesh>();
        tm.font = font; tm.text = text; tm.fontSize = 96; tm.characterSize = charSize;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        return go;
    }
}
