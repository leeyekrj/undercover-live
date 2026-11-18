using System.IO;
using UnityEngine;
using UnityEditor;

// Mixamo FBX에 묶인 텍스처·재질을 꺼내고, 머리카락·속눈썹은 투명 잘라내기(알파 클립)로 바꾼다.
public static class MixamoFix
{
    static readonly string[] Chars = { "char_man_casual", "char_woman_casual", "char_man_suit" };

    public static string Run()
    {
        string log = "";
        foreach (var f in Chars)
        {
            string path = $"Assets/_Game/Mixamo/Characters/{f}.fbx";
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            string texDir = $"Assets/_Game/Mixamo/Characters/{f}_Textures";
            Directory.CreateDirectory(texDir);
            bool ok = mi.ExtractTextures(texDir);
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            string matDir = $"Assets/_Game/Mixamo/Characters/{f}_Materials";
            Directory.CreateDirectory(matDir);
            int mats = 0;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is Material m)
                {
                    string err = AssetDatabase.ExtractAsset(m, $"{matDir}/{m.name}.mat");
                    if (string.IsNullOrEmpty(err)) mats++;
                }
            }
            AssetDatabase.WriteImportSettingsIfDirty(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            int clipped = 0, textured = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { matDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null) continue;
                if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) textured++;
                string n = m.name.ToLower();
                if (n.Contains("hair") || n.Contains("lash") || n.Contains("brow"))
                {
                    m.SetFloat("_Surface", 0);
                    m.SetFloat("_AlphaClip", 1);
                    m.SetFloat("_Cutoff", 0.35f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.SetFloat("_Cull", 0);
                    m.renderQueue = 2450;
                    clipped++;
                }
                // 발광 맵이 켜진 재질은 하얗게 빛나는 실루엣이 된다 → 끔
                m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black);
                if (n.Contains("cigar")) { m.SetColor("_BaseColor", new Color(0, 0, 0, 0)); }
                EditorUtility.SetDirty(m);
            }
            log += $"{f}: 텍스처꺼냄 {ok}, 재질 {mats}, 텍스처붙음 {textured}, 머리투명 {clipped} | ";
        }
        AssetDatabase.SaveAssets();
        return log;
    }

    // 새 인물용: 텍스처·재질을 꺼내고 이름 규칙(1001=몸, 1002=머리카락, Diffuse/Normal)으로 자동 연결한다.
    public static string Extend(params string[] names)
    {
        string log = "";
        foreach (var f in names)
        {
            string path = $"Assets/_Game/Mixamo/Characters/{f}.fbx";
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            if (mi == null) { log += f + ": 없음 | "; continue; }
            string texDir = $"Assets/_Game/Mixamo/Characters/{f}_Textures";
            string matDir = $"Assets/_Game/Mixamo/Characters/{f}_Materials";
            Directory.CreateDirectory(texDir); Directory.CreateDirectory(matDir);
            mi.ExtractTextures(texDir);
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Material m0) AssetDatabase.ExtractAsset(m0, $"{matDir}/{m0.name}.mat");
            AssetDatabase.WriteImportSettingsIfDirty(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var texs = new System.Collections.Generic.List<string>();
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir })) texs.Add(AssetDatabase.GUIDToAssetPath(g));
            string Pick(string kind, bool hair)
            {
                string best = null;
                foreach (var t in texs)
                {
                    string n = Path.GetFileNameWithoutExtension(t).ToLower();
                    if (!n.Contains(kind)) continue;
                    bool isHairTex = n.Contains("1002") || n.Contains("hair");
                    if (hair == isHairTex) return t;
                    if (best == null) best = t;
                }
                return best;
            }
            int linked = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { matDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                bool hair = m.name.ToLower().Contains("hair") || m.name.ToLower().Contains("lash");
                string dif = Pick("diffuse", hair), nrm = Pick("normal", hair);
                if (nrm != null)
                {
                    var ti = (TextureImporter)AssetImporter.GetAtPath(nrm);
                    if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                }
                if (dif != null) { m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dif)); m.SetColor("_BaseColor", Color.white); linked++; }
                if (nrm != null) { m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nrm)); m.EnableKeyword("_NORMALMAP"); }
                m.SetFloat("_Smoothness", 0.3f);
                m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black);
                if (hair)
                {
                    m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", 0.35f); m.EnableKeyword("_ALPHATEST_ON");
                    m.SetFloat("_Cull", 0); m.renderQueue = 2450;
                }
                EditorUtility.SetDirty(m);
            }
            log += $"{f}: 텍스처 {texs.Count}, 연결 재질 {linked} | ";
        }
        AssetDatabase.SaveAssets();
        return log;
    }
}
