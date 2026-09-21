using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Dragonia.Characters;
using Dragonia.Enemies;
using Dragonia.Registry;
using Dragonia.UI;

namespace Dragonia.EditorTools
{
    /// <summary>
    /// 시험용 아레나 씬을 코드로 짓는다.
    ///
    /// 씬(.unity)과 프리팹(.prefab)은 손으로 쓰면 깨지기 쉬운 형식이라 에디터에게 짓게 시킨다.
    /// 메뉴에서 눌러도 되고, 명령줄에서 -executeMethod 로 불러도 된다.
    ///
    ///   Unity.exe -quit -batchmode -projectPath . -executeMethod Dragonia.EditorTools.ArenaBuilder.Build
    ///
    /// 여기서 만드는 것: 재질 에셋 셋, HUD 프리팹, 그리고 아레나 씬.
    /// </summary>
    public static class ArenaBuilder
    {
        const string ScenePath = "Assets/Scenes/Arena.unity";
        const string RegistryPath = "Assets/Resources/AssetRegistry.asset";
        const string HudPath = "Assets/Prefabs/UI/Hud.prefab";

        static readonly Color Ink = new Color(0.075f, 0.07f, 0.115f, 0.82f);
        static readonly Color Gold = new Color(0.85f, 0.70f, 0.35f);
        static readonly Color Parch = new Color(0.93f, 0.89f, 0.81f);

        [MenuItem("Dragonia/아레나 씬 만들기")]
        public static void Build()
        {
            EnsureMaterials();
            EnsureRegistry();
            var hudPrefab = BuildHudPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            MakeLight();
            MakeArena();
            var player = MakePlayer();
            MakeCamera(player);
            MakeBoss(new Vector3(0f, 0f, 16f), "MORGATH");

            var hud = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab);
            hud.name = "Hud";
            var ui = new GameObject("UIManager").AddComponent<UIManager>();
            ui.transform.SetParent(hud.transform, false);

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"아레나 씬을 만들었다 → {ScenePath}");
        }

        // ---------------------------------------------------------------- 재질

        /// <summary>
        /// 실행 중에 쓰는 재질의 본. Resources 에 에셋으로 둬야 빌드가 그 셰이더 변형을 남긴다.
        /// 반투명·발광을 실행 중에 키워드로 켜면, 웹 빌드에서는 변형이 없어 분홍색이 된다.
        /// </summary>
        public static void EnsureMaterials()
        {
            System.IO.Directory.CreateDirectory("Assets/Resources/Materials");
            var shader = Shader.Find("Standard");

            Make("Base", m => { m.SetFloat("_Glossiness", 0.12f); m.SetFloat("_Metallic", 0f); });
            Make("Glow", m =>
            {
                m.SetFloat("_Glossiness", 0.1f);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", Color.white);
            });
            Make("Marker", m =>
            {
                m.SetFloat("_Mode", 2f);                       // Fade
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                m.SetFloat("_Glossiness", 0f);
                m.color = new Color(1f, 1f, 1f, 0.4f);
            });

            void Make(string name, System.Action<Material> setup)
            {
                string path = $"Assets/Resources/Materials/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
                m.shader = shader;
                setup(m);
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }

        static Material SceneMat(Color c, float gloss = 0.08f)
        {
            var m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Base.mat")) { color = c };
            m.SetFloat("_Glossiness", gloss);
            return m;
        }

        // ---------------------------------------------------------------- 씬

        static void MakeLight()
        {
            var go = new GameObject("달빛");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.78f, 0.84f, 1f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            go.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

            // 모르가스는 "달빛 골짜기의 망령"이다. 어스름한 푸른 안개
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.36f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.24f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.10f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.16f, 0.19f, 0.30f);
            RenderSettings.fogDensity = 0.012f;
        }

        static void MakeArena()
        {
            var root = new GameObject("아레나").transform;
            var groundMat = SceneMat(new Color(0.21f, 0.27f, 0.25f));
            var stoneMat = SceneMat(new Color(0.30f, 0.30f, 0.36f));
            var darkMat = SceneMat(new Color(0.17f, 0.16f, 0.21f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "바닥";
            ground.transform.SetParent(root);
            ground.transform.localScale = new Vector3(9f, 1f, 9f);        // 90m × 90m
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            // 둘레: 각진 바위를 빙 둘러 세운다. 네모난 벽보다 골짜기처럼 보인다
            var rng = new System.Random(7);
            const int N = 40;
            for (int i = 0; i < N; i++)
            {
                float a = i * Mathf.PI * 2f / N;
                float r = 40f + (float)rng.NextDouble() * 3f;
                float h = 7f + (float)rng.NextDouble() * 9f;
                var rock = Cube(root, "둘레 바위", darkMat,
                    new Vector3(Mathf.Sin(a) * r, h * 0.5f - 0.5f, Mathf.Cos(a) * r),
                    new Vector3(8f + (float)rng.NextDouble() * 3f, h, 6f + (float)rng.NextDouble() * 4f));
                rock.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 8f - 4f, -a * Mathf.Rad2Deg + (float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 8f - 4f);
            }

            // 기둥 여섯. 멋이 아니라 규칙이다 — 돌진하는 보스를 여기 박게 만들면 한참 기절한다
            for (int i = 0; i < 6; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / 6f;
                var pillar = Cube(root, "기둥", stoneMat, new Vector3(Mathf.Sin(a) * 19f, 4.5f, Mathf.Cos(a) * 19f), new Vector3(2.6f, 9f, 2.6f));
                pillar.transform.rotation = Quaternion.Euler(0f, i * 17f, 0f);
                var cap = Cube(pillar.transform, "기둥머리", darkMat, Vector3.zero, Vector3.one);
                cap.transform.localPosition = new Vector3(0f, 0.53f, 0f);
                cap.transform.localScale = new Vector3(1.25f, 0.07f, 1.25f);
            }

            // 흩어진 돌. 땅이 허전하면 속도감도 거리감도 안 난다
            for (int i = 0; i < 46; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = 6f + (float)rng.NextDouble() * 30f;
                float s = 0.3f + (float)rng.NextDouble() * 0.9f;
                var stone = Cube(root, "돌", i % 3 == 0 ? darkMat : stoneMat,
                    new Vector3(Mathf.Sin(a) * r, s * 0.25f, Mathf.Cos(a) * r), new Vector3(s * 1.4f, s * 0.7f, s));
                stone.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 20f);
                if (s < 0.7f) Object.DestroyImmediate(stone.GetComponent<Collider>());      // 작은 돌은 밟고 지나간다
            }
        }

        static GameObject Cube(Transform parent, string name, Material mat, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject MakePlayer()
        {
            var go = new GameObject("내 용");
            go.tag = "Player";
            go.transform.position = new Vector3(0f, 0.2f, -6f);

            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.7f; cc.center = new Vector3(0f, 1.05f, 0f);
            cc.stepOffset = 0.4f; cc.slopeLimit = 50f;

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
            cam.fieldOfView = 58f;
            cam.farClipPlane = 220f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.19f, 0.30f);      // 안개색과 같게 — 지평선에서 이음매가 안 보인다
            go.AddComponent<AudioListener>();
            go.AddComponent<LockOnCamera>().follow = player.transform;
            go.transform.position = player.transform.position + new Vector3(0f, 4f, -8f);
        }

        static void MakeBoss(Vector3 at, string bossId)
        {
            var go = new GameObject($"보스: {bossId}");
            go.transform.position = at + Vector3.up * 0.2f;
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var cc = go.AddComponent<CharacterController>();
            cc.height = 3.8f; cc.radius = 1.7f; cc.center = new Vector3(0f, 2.1f, 0f);

            go.AddComponent<BossBrain>().bossId = bossId;

            var visual = new GameObject("모습");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * 2f;
            var v = visual.AddComponent<DragonVisual>();
            v.creatureId = bossId;
            v.bodyColor = new Color(0.80f, 0.82f, 0.88f);
            v.wingColor = new Color(0.36f, 0.55f, 0.75f);
            v.glowColor = new Color(0.5f, 0.85f, 1f);
        }

        // ---------------------------------------------------------------- HUD 프리팹

        static GameObject BuildHudPrefab()
        {
            System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("Hud", typeof(Canvas), typeof(CanvasScaler));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = root.AddComponent<Hud>();
            var rt = root.transform;

            // 왼쪽 아래: 체력과 고른 숨결
            hud.playerHp = Bar(rt, "PlayerHp", new Vector2(0f, 0f), new Vector2(48f, 56f), new Vector2(460f, 26f), new Color(0.88f, 0.27f, 0.23f), out _);
            var chip = Img(rt, "Element", new Vector2(0f, 0f), new Vector2(48f, 94f), new Vector2(22f, 22f), Color.white);
            hud.elementChip = chip;
            Label(rt, "ElementLabel", font, "BREATH  [1] fire  [2] ice  [3] thunder", 18, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(80f, 94f), new Vector2(500f, 24f), Parch);

            // 위 가운데: 보스
            var group = new GameObject("Boss", typeof(RectTransform));
            group.transform.SetParent(rt, false);
            var grt = (RectTransform)group.transform;
            grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 1f); grt.pivot = new Vector2(0.5f, 1f);
            grt.anchoredPosition = new Vector2(0f, -40f); grt.sizeDelta = new Vector2(940f, 80f);
            hud.bossGroup = group;
            hud.bossName = Label(grt, "BossName", font, "MORGATH", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(900f, 38f), Gold);
            hud.bossName.rectTransform.pivot = new Vector2(0.5f, 1f);
            hud.bossHp = Bar(grt, "BossHp", new Vector2(0f, 0f), new Vector2(20f, 8f), new Vector2(900f, 20f), new Color(0.75f, 0.2f, 0.28f), out _);

            // 가운데 큰 글씨, 아래 조작 안내
            hud.center = Label(rt, "Center", font, "", 64, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 120f), Gold);
            hud.center.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            hud.hint = Label(rt, "Hint", font,
                "WASD move   SHIFT dash / hold: run   SPACE jump, mash: FLY   C drop\nLMB (hold) shoot breath - also while flying   RMB bite (air: dive slam)   Q lock-on",
                19, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(1500f, 60f), new Color(Parch.r, Parch.g, Parch.b, 0.75f));
            hud.hint.rectTransform.pivot = new Vector2(0.5f, 0f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>테두리 있는 막대. 채움은 앵커로 줄인다 — 돌려주는 RectTransform 의 anchorMax.x 가 비율이다.</summary>
        static RectTransform Bar(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, out Image fillImage)
        {
            var frame = Img(parent, name, anchor, pos, size, Gold);
            var back = Img(frame.transform, "Back", Vector2.zero, Vector2.zero, Vector2.zero, Ink);
            Stretch(back.rectTransform, 2f);
            back.color = new Color(0.06f, 0.055f, 0.09f, 1f);
            var area = new GameObject("Area", typeof(RectTransform));
            area.transform.SetParent(frame.transform, false);
            Stretch((RectTransform)area.transform, 4f);
            fillImage = Img(area.transform, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, color);
            var f = fillImage.rectTransform;
            f.anchorMin = Vector2.zero; f.anchorMax = Vector2.one; f.offsetMin = f.offsetMax = Vector2.zero;
            return f;
        }

        static Image Img(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = anchor; r.pivot = anchor;
            r.anchoredPosition = pos; r.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color; img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }

        static Text Label(Transform parent, string name, Font font, string text, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = anchor; r.pivot = anchor;
            r.anchoredPosition = pos; r.sizeDelta = box;
            var t = go.GetComponent<Text>();
            t.font = font; t.text = text; t.fontSize = size; t.alignment = align; t.color = color;
            t.fontStyle = FontStyle.Bold; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f); o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        /// <summary>등록표가 없으면 만든다. 모델을 나중에 여기 끼우면 된다.</summary>
        static void EnsureRegistry()
        {
            System.IO.Directory.CreateDirectory("Assets/Resources");
            if (AssetDatabase.LoadAssetAtPath<AssetRegistry>(RegistryPath) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AssetRegistry>(), RegistryPath);
            AssetDatabase.SaveAssets();
        }
    }
}
