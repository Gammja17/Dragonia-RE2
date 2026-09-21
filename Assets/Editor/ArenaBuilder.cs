using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dragonia.Characters;
using Dragonia.Enemies;
using Dragonia.Registry;

namespace Dragonia.EditorTools
{
    /// <summary>
    /// 시험용 아레나 씬을 코드로 짓는다.
    ///
    /// 씬 파일(.unity)은 손으로 쓰면 깨지기 쉬운 형식이라, 에디터에게 짓게 시킨다.
    /// 메뉴에서 눌러도 되고, 명령줄에서 -executeMethod 로 불러도 된다.
    ///
    ///   Unity.exe -quit -batchmode -projectPath . -executeMethod Dragonia.EditorTools.ArenaBuilder.Build
    ///
    /// 모델이 없어도 굴러가도록 만든다 — 회색 캡슐로 서고, 조작감부터 볼 수 있다.
    /// </summary>
    public static class ArenaBuilder
    {
        const string ScenePath = "Assets/Scenes/Arena.unity";
        const string RegistryPath = "Assets/Resources/AssetRegistry.asset";

        [MenuItem("Dragonia/아레나 씬 만들기")]
        public static void Build()
        {
            EnsureRegistry();
            EnsureTag("Player");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            MakeLight();
            MakeGround();
            var player = MakePlayer();
            MakeCamera(player);
            MakeBoss(new Vector3(0f, 0f, 14f), "MORGATH");

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"아레나 씬을 만들었다 → {ScenePath}");
        }

        static void MakeLight()
        {
            var go = new GameObject("해");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.89f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.33f, 0.36f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.25f, 0.24f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.13f, 0.16f);
        }

        static void MakeGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "바닥";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);   // 60m × 60m
            Paint(ground, new Color(0.30f, 0.34f, 0.26f));

            // 아레나 둘레 벽 — 돌진한 보스가 여기 박혀서 기절한다
            for (int i = 0; i < 4; i++)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"벽 {i + 1}";
                float a = i * 90f * Mathf.Deg2Rad;
                wall.transform.position = new Vector3(Mathf.Sin(a) * 30f, 2f, Mathf.Cos(a) * 30f);
                wall.transform.rotation = Quaternion.Euler(0f, i * 90f, 0f);
                wall.transform.localScale = new Vector3(60f, 4f, 1f);
                Paint(wall, new Color(0.22f, 0.20f, 0.24f));
            }
        }

        static GameObject MakePlayer()
        {
            var go = new GameObject("내 용");
            go.tag = "Player";
            go.transform.position = new Vector3(0f, 1f, 0f);

            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.5f; cc.center = new Vector3(0f, 1f, 0f);

            go.AddComponent<DragonController>();

            var visual = new GameObject("모습");
            visual.transform.SetParent(go.transform, false);
            visual.AddComponent<DragonVisual>().creatureId = "WESTERN";
            return go;
        }

        static void MakeCamera(GameObject player)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.backgroundColor = new Color(0.07f, 0.06f, 0.10f);
            go.AddComponent<AudioListener>();
            var lockOn = go.AddComponent<LockOnCamera>();
            lockOn.follow = player.transform;
            go.transform.position = new Vector3(0f, 4f, -7f);
        }

        static void MakeBoss(Vector3 at, string bossId)
        {
            var go = new GameObject($"보스: {bossId}");
            go.transform.position = at;

            var cc = go.AddComponent<CharacterController>();
            cc.height = 3f; cc.radius = 1.2f; cc.center = new Vector3(0f, 1.5f, 0f);

            go.AddComponent<BossBrain>().bossId = bossId;

            var visual = new GameObject("모습");
            visual.transform.SetParent(go.transform, false);
            visual.AddComponent<DragonVisual>().creatureId = bossId;
            visual.transform.localScale = Vector3.one * 2f;
        }

        static void Paint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            // 기본 렌더 파이프라인과 URP 둘 다에서 보이도록 지금 쓰는 셰이더를 따라간다
            var mat = new Material(r.sharedMaterial != null ? r.sharedMaterial.shader : Shader.Find("Standard"));
            mat.color = c;
            r.sharedMaterial = mat;
        }

        /// <summary>등록표가 없으면 만든다. 모델을 나중에 여기 끼우면 된다.</summary>
        static void EnsureRegistry()
        {
            System.IO.Directory.CreateDirectory("Assets/Resources");
            if (AssetDatabase.LoadAssetAtPath<AssetRegistry>(RegistryPath) != null) return;
            var reg = ScriptableObject.CreateInstance<AssetRegistry>();
            AssetDatabase.CreateAsset(reg, RegistryPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"등록표를 만들었다 → {RegistryPath}");
        }

        /// <summary>Player 태그는 기본으로 있지만, 없는 프로젝트에서도 돌아가게 확인해 둔다.</summary>
        static void EnsureTag(string tag)
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0) return;
            var so = new SerializedObject(asset[0]);
            var tags = so.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            so.ApplyModifiedProperties();
        }
    }
}
