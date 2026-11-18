using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UndercoverLive.EditorTools
{
    // 웹(WebGL) 빌드. GitHub Pages는 압축 파일에 Content-Encoding 헤더를 못 붙이므로
    // gzip + 압축 풀기 대비책(decompression fallback)으로 내보내 브라우저 쪽 자바스크립트가 직접 푼다.
    public static class WebBuild
    {
        const string DefaultOut = "Builds/WebGL";

        [MenuItem("UndercoverLive/Build WebGL (GitHub Pages)")]
        public static void BuildMenu() { Build(DefaultOut); }

        // 명령줄: Unity -batchmode -buildTarget WebGL -executeMethod UndercoverLive.EditorTools.WebBuild.BuildCli -buildOutput <폴더>
        public static void BuildCli()
        {
            string outPath = DefaultOut;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-buildOutput") outPath = args[i + 1];
            // 배치 빌드(복사본) 전용: 웹 다운로드 크기를 줄인다. 4096px Mixamo 텍스처가 용량의 80%였다(빌드 112MB → 줄임).
            // 에디터 메뉴 빌드에는 적용하지 않아 원본 프로젝트의 빌드 설정을 바꾸지 않는다.
            EditorUserBuildSettings.overrideMaxTextureSize = 1024;
            PlayerSettings.SplashScreen.show = false;
            // 11-18: GitHub Pages → 초당 약 1MB라 41MB 첫 로딩이 40초+ → 더 줄인다(복사본에서만).
            ShrinkForWeb();
            bool ok = false;
            try { ok = Build(outPath); }
            catch (Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// 웹 다운로드 크기 줄이기 (배치 빌드 복사본 전용 — 임포트 설정 .meta를 바꾸므로 원본에서 부르지 않는다).
        /// Mixamo 인물 텍스처: 최대 512px(노멀맵 256px) + 크런치 압축. 인물 FBX: 메시 압축 높음, 블렌드셰이프 끔.
        /// 코드: 관리 코드 제거 Medium, IL2CPP 크기 우선.
        /// </summary>
        static void ShrinkForWeb()
        {
            const string mixamo = "Assets/_Game/Mixamo";
            int tex = 0, mdl = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { mixamo }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                    bool normal = ti.textureType == TextureImporterType.NormalMap || path.ToLowerInvariant().Contains("normal");
                    ti.maxTextureSize = normal ? 256 : 512;
                    ti.crunchedCompression = true;
                    ti.compressionQuality = 50;
                    ti.mipmapEnabled = true;
                    ti.SaveAndReimport();
                    tex++;
                }
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { mixamo }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
                    mi.meshCompression = ModelImporterMeshCompression.High;
                    mi.importBlendShapes = false;
                    mi.isReadable = false;
                    mi.SaveAndReimport();
                    mdl++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            var wg = UnityEditor.Build.NamedBuildTarget.WebGL;
            PlayerSettings.SetManagedStrippingLevel(wg, ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetIl2CppCodeGeneration(wg, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            Debug.Log($"[WebBuild] 웹 용량 줄이기: 텍스처 {tex}개, 모델 {mdl}개, 코드 제거 Medium");
        }

        public static bool Build(string outPath)
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.template = "PROJECT:Undercover";
            // 예전 우회로 넣었던 -lembind가 남아 있으면 지운다 (원인 패키지는 tools/build-web.sh가 복사본에서 뺀다)
            var emArgs = PlayerSettings.WebGL.emscriptenArgs ?? "";
            if (emArgs.Contains("-lembind"))
                PlayerSettings.WebGL.emscriptenArgs = emArgs.Replace("-lembind", "").Trim();

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[WebBuild] {s.result} · {s.totalSize / 1048576f:0.0}MB · {s.totalTime} · errors {s.totalErrors} → {outPath}");
            return s.result == BuildResult.Succeeded;
        }
    }
}
