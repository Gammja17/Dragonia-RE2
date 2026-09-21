using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dragonia.Characters;

namespace Dragonia.EditorTools
{
    /// <summary>
    /// 대역 용이 어떻게 생겼는지 PNG 로 찍어 본다. 웹 빌드(몇 분)를 기다리지 않고
    /// 생김새와 자세를 바로 확인하려는 것이다.
    ///
    ///   Unity.exe -quit -batchmode -projectPath . -executeMethod Dragonia.EditorTools.PreviewShots.Render
    ///   (그림을 그려야 하므로 -nographics 를 붙이면 안 된다)
    /// </summary>
    public static class PreviewShots
    {
        [MenuItem("Dragonia/미리보기 찍기 (Temp/preview_*.png)")]
        public static void Render()
        {
            ArenaBuilder.EnsureMaterials();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.52f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 6f;
            ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.27f, 0.33f, 0.3f) };

            var red = new Color(0.66f, 0.16f, 0.13f); var gold = new Color(0.88f, 0.63f, 0.13f); var fire = new Color(1f, 0.6f, 0.2f);

            // 1) 땅에 서 있는 모습 (날개 접음)
            var a = Make(new Vector3(0f, 0f, 0f), 35f, red, gold, fire);
            Advance(a, 20);
            Shot("1_idle", new Vector3(4.2f, 2.4f, 4.6f), new Vector3(0f, 1.3f, 0.2f));
            Shot("2_side", new Vector3(6.5f, 1.6f, 0f), new Vector3(0f, 1.3f, 0f));

            // 2) 걷는 중
            a.SetSpeed(0.5f); Advance(a, 14);
            Shot("3_walk", new Vector3(5f, 1.8f, 3f), new Vector3(0f, 1.2f, 0f));

            // 3) 무는 순간
            a.SetSpeed(0f); a.Play(Anim.Bite); Advance(a, 14);
            Shot("4_bite", new Vector3(4.5f, 2f, 3.5f), new Vector3(0f, 1.3f, 1f));

            // 4) 숨결
            a.SetBreathing(true); Advance(a, 16);
            Shot("5_breath", new Vector3(4.5f, 2f, 3.5f), new Vector3(0f, 1.4f, 1f));

            // 5) 나는 중 (날개 편 채 날갯짓)
            a.SetBreathing(false); a.Play(Anim.Idle); a.SetFlying(true); a.FlapKick(); a.transform.position = new Vector3(0f, 3f, 0f);
            Advance(a, 9);
            Shot("6_fly", new Vector3(5.5f, 4.6f, 5.5f), new Vector3(0f, 3.6f, 0f));
            Shot("7_fly_front", new Vector3(0f, 4f, 8f), new Vector3(0f, 3.8f, 0f));

            // 6) 보스: 희고 큰 용이 몸을 일으킨 예고 자세
            Object.DestroyImmediate(a.gameObject);
            var b = Make(Vector3.zero, 25f, new Color(0.8f, 0.82f, 0.88f), new Color(0.36f, 0.55f, 0.75f), new Color(0.5f, 0.85f, 1f));
            b.transform.localScale = Vector3.one * 2f;
            b.Play(Anim.Tell); Advance(b, 16);
            Shot("8_boss_tell", new Vector3(9f, 4f, 10f), new Vector3(0f, 3f, 0.5f));

            Debug.Log("미리보기를 찍었다 → Temp/preview_*.png");
        }

        static ProceduralDragon Make(Vector3 pos, float yaw, Color body, Color wing, Color glow)
        {
            var go = new GameObject("Dragon");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var d = go.AddComponent<ProceduralDragon>();
            d.Build(body, wing, glow);
            return d;
        }

        static void Advance(ProceduralDragon d, int frames)
        {
            for (int i = 0; i < frames; i++) d.Tick(1f / 30f);
        }

        static void Shot(string name, Vector3 from, Vector3 lookAt)
        {
            const int W = 960, H = 600;
            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = from;
            cam.transform.LookAt(lookAt);
            cam.fieldOfView = 45f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.55f, 0.7f);

            var rt = new RenderTexture(W, H, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            Directory.CreateDirectory("Temp");
            File.WriteAllBytes($"Temp/preview_{name}.png", tex.EncodeToPNG());

            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGo);
        }
    }
}
