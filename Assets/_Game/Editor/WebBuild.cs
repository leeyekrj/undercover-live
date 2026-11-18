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
            bool ok = false;
            try { ok = Build(outPath); }
            catch (Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(ok ? 0 : 1);
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
