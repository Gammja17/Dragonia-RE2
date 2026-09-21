using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Dragonia.EditorTools
{
    /// <summary>
    /// WebGL 로 뽑아 docs/game 에 넣는다. GitHub Pages 가 그 폴더를 그대로 낸다.
    ///
    ///   Unity.exe -quit -batchmode -projectPath . -executeMethod Dragonia.EditorTools.WebGLBuilder.Build
    ///
    /// 압축을 끄는 게 핵심이다. GitHub Pages 는 Content-Encoding 헤더를 못 붙여서,
    /// 브라우저가 .br/.gz 를 압축된 줄 모르고 그대로 읽으려다 흰 화면만 뜬다.
    /// 여기서 설정까지 같이 박아 두는 건, 다음에 누가 빌드해도 같은 함정에 안 빠지게 하기 위해서다.
    /// </summary>
    public static class WebGLBuilder
    {
        const string OutDir = "docs/game";

        [MenuItem("Dragonia/WebGL 빌드 (docs/game)")]
        public static void Build()
        {
            Prepare();

            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                Debug.Log("빌드 목록이 비어 있다. 아레나 씬부터 만든다.");
                ArenaBuilder.Build();
                scenes = EditorBuildSettings.scenes;
            }

            var paths = new System.Collections.Generic.List<string>();
            foreach (var s in scenes) if (s.enabled) paths.Add(s.path);

            Directory.CreateDirectory(OutDir);

            var options = new BuildPlayerOptions
            {
                scenes = paths.ToArray(),
                locationPathName = OutDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s2 = report.summary;
            if (s2.result == BuildResult.Succeeded)
                Debug.Log($"WebGL 빌드 성공 → {OutDir} · {s2.totalSize / 1024 / 1024}MB · {s2.totalTime}");
            else
                Debug.LogError($"WebGL 빌드 실패: {s2.result} · 오류 {s2.totalErrors}개");
        }

        /// <summary>GitHub Pages 에서 돌아가게 하는 설정들. 하나라도 빠지면 흰 화면이 나온다.</summary>
        static void Prepare()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                Debug.Log("WebGL 로 플랫폼을 바꾼다 (처음이면 오래 걸린다)");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }

            // 압축을 끈다 — Pages 가 헤더를 못 붙여서 압축본을 읽지 못한다
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;   // 빌드를 가볍게

            // 안 쓰는 클래스를 쳐 내면(코드 스트리핑) 실행 중에 만들어 쓰는 것들이 사라진다.
            // GameObject.CreatePrimitive 가 붙이는 콜라이더들이 그래서 없어졌다:
            //     Can't add component because class 'SphereCollider' doesn't exist!
            // link.xml 로 모듈을 남겨 봤지만 전부 걸리지는 않았다. 덜 쳐 내게 한다.
            // 모델이 붙고 대역을 안 쓰게 되면 그때 다시 올려도 된다.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);

            PlayerSettings.companyName = "gamuza";
            PlayerSettings.productName = "DRAGONIA RE2";
            PlayerSettings.runInBackground = true;

            // 스크립트가 예전 입력(Input.GetAxisRaw)을 쓴다. 새 입력만 켜져 있으면 실행 즉시 예외가 난다.
            // 공개 API 가 없어서 설정 파일을 직접 건드린다. 0=Old, 1=New, 2=Both
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets != null && assets.Length > 0)
            {
                var so = new SerializedObject(assets[0]);
                var handler = so.FindProperty("activeInputHandler");
                if (handler != null && handler.intValue != 2)
                {
                    handler.intValue = 2;
                    so.ApplyModifiedProperties();
                    Debug.Log("입력 방식을 Both 로 맞췄다 (예전 입력도 쓸 수 있게)");
                }
            }
        }
    }
}
