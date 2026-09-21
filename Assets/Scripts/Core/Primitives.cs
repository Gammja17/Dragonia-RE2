using UnityEngine;

namespace Dragonia.Core
{
    /// <summary>
    /// 모델이 아직 없을 때 세우는 대역과 표식을 만든다.
    ///
    /// GameObject.CreatePrimitive 를 쓰지 않는다. 그건 언제나 콜라이더를 같이 붙이는데,
    /// 우리는 보이기만 하면 되니 콜라이더가 필요 없고, 게다가 빌드에서 안 쓰는 클래스를
    /// 쳐 내고 나면 붙일 클래스가 없어서 터진다:
    ///
    ///     Can't add component because class 'SphereCollider' doesn't exist!
    ///
    /// 그래서 그물(mesh)만 코드로 만들어 쓴다. 물리와 아무 상관이 없어지니
    /// 빌드가 무엇을 쳐 내든 멀쩡하다.
    /// </summary>
    public static class Primitives
    {
        static Mesh _cube;
        static Material _material;

        /// <summary>정육면체 그물 하나를 만들어 두고 계속 돌려쓴다.</summary>
        public static Mesh Cube
        {
            get
            {
                if (_cube != null) return _cube;
                _cube = new Mesh { name = "대역 정육면체" };

                // 면마다 꼭짓점을 따로 둔다 — 그래야 면이 각지게 보인다
                Vector3[] v =
                {
                    new(-.5f,-.5f,-.5f), new(.5f,-.5f,-.5f), new(.5f,.5f,-.5f), new(-.5f,.5f,-.5f),  // 앞
                    new(.5f,-.5f,.5f), new(-.5f,-.5f,.5f), new(-.5f,.5f,.5f), new(.5f,.5f,.5f),      // 뒤
                    new(-.5f,.5f,-.5f), new(.5f,.5f,-.5f), new(.5f,.5f,.5f), new(-.5f,.5f,.5f),      // 위
                    new(-.5f,-.5f,.5f), new(.5f,-.5f,.5f), new(.5f,-.5f,-.5f), new(-.5f,-.5f,-.5f),  // 아래
                    new(-.5f,-.5f,.5f), new(-.5f,-.5f,-.5f), new(-.5f,.5f,-.5f), new(-.5f,.5f,.5f),  // 왼
                    new(.5f,-.5f,-.5f), new(.5f,-.5f,.5f), new(.5f,.5f,.5f), new(.5f,.5f,-.5f),      // 오른
                };
                int[] t = new int[36];
                for (int f = 0; f < 6; f++)
                {
                    int o = f * 4, i = f * 6;
                    t[i] = o; t[i + 1] = o + 2; t[i + 2] = o + 1;
                    t[i + 3] = o; t[i + 4] = o + 3; t[i + 5] = o + 2;
                }
                _cube.vertices = v;
                _cube.triangles = t;
                _cube.RecalculateNormals();
                _cube.RecalculateBounds();
                return _cube;
            }
        }

        static Material BaseMaterial
        {
            get
            {
                if (_material != null) return _material;
                // 지금 프로젝트가 쓰는 셰이더를 따라간다 (기본 파이프라인이든 URP 든)
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
                _material = new Material(shader) { name = "대역 재질" };
                return _material;
            }
        }

        /// <summary>보이기만 하는 상자 하나. 콜라이더도 물리도 없다.</summary>
        public static GameObject Box(string name, Color color, Vector3 scale)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = Cube;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = new Material(BaseMaterial) { color = color };
            go.transform.localScale = scale;
            return go;
        }

        /// <summary>반투명하게 그릴 수 있도록 재질을 바꾼다 (예고 표식에 쓴다).</summary>
        public static void MakeTransparent(Renderer r)
        {
            if (r == null) return;
            var m = r.material;
            m.SetFloat("_Surface", 1f);                 // URP: 0 불투명, 1 투명
            m.SetFloat("_Mode", 3f);                    // 기본 파이프라인의 Transparent
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
