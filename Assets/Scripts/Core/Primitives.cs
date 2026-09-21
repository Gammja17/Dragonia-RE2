using UnityEngine;

namespace Dragonia.Core
{
    /// <summary>
    /// 상자 하나를 만드는 도구. 로우폴리 용, 탄, 예고 표식, 파편이 전부 이걸로 만들어진다.
    ///
    /// GameObject.CreatePrimitive 를 쓰지 않는다. 그건 언제나 콜라이더를 같이 붙이는데,
    /// 우리는 보이기만 하면 되고, 게다가 빌드가 안 쓰는 클래스를 쳐 내고 나면
    /// 붙일 클래스가 없어서 터진다 (Can't add component because class 'SphereCollider' doesn't exist!).
    ///
    /// 재질은 Shader.Find 로 찾지 않고 Resources/Materials 의 에셋에서 복사한다.
    /// 빌드는 "어떤 에셋도 안 쓰는 셰이더 변형"을 쳐 내기 때문에, 반투명이나 발광을
    /// 실행 중에 켜려고 하면 웹 빌드에서는 그 변형이 없어 분홍색이 된다.
    /// 에셋 셋(Base · Marker · Glow)은 Editor/ArenaBuilder 가 만들어 둔다.
    /// </summary>
    public static class Primitives
    {
        static Mesh _cube;
        static Material _base, _marker, _glow;

        /// <summary>정육면체 그물 하나를 만들어 두고 계속 돌려쓴다. 면마다 꼭짓점을 따로 둬서 각지게 보인다.</summary>
        public static Mesh Cube
        {
            get
            {
                if (_cube != null) return _cube;
                _cube = new Mesh { name = "상자" };
                Vector3[] v =
                {
                    new(-.5f,-.5f,-.5f), new(.5f,-.5f,-.5f), new(.5f,.5f,-.5f), new(-.5f,.5f,-.5f),
                    new(.5f,-.5f,.5f), new(-.5f,-.5f,.5f), new(-.5f,.5f,.5f), new(.5f,.5f,.5f),
                    new(-.5f,.5f,-.5f), new(.5f,.5f,-.5f), new(.5f,.5f,.5f), new(-.5f,.5f,.5f),
                    new(-.5f,-.5f,.5f), new(.5f,-.5f,.5f), new(.5f,-.5f,-.5f), new(-.5f,-.5f,-.5f),
                    new(-.5f,-.5f,.5f), new(-.5f,-.5f,-.5f), new(-.5f,.5f,-.5f), new(-.5f,.5f,.5f),
                    new(.5f,-.5f,-.5f), new(.5f,-.5f,.5f), new(.5f,.5f,.5f), new(.5f,.5f,-.5f),
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

        static Material Load(ref Material cache, string name)
        {
            if (cache != null) return cache;
            cache = Resources.Load<Material>("Materials/" + name);
            if (cache == null)
            {
                // 에셋이 아직 없을 때(에디터에서 처음 돌릴 때)만 여기로 온다
                var shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
                cache = new Material(shader) { name = name + " (임시)" };
            }
            return cache;
        }

        /// <summary>불투명한 단색.</summary>
        public static Material Solid(Color c) => new Material(Load(ref _base, "Base")) { color = c };

        /// <summary>반투명. 바닥 예고 표식과 충격파에 쓴다.</summary>
        public static Material Marker(Color c) => new Material(Load(ref _marker, "Marker")) { color = c };

        /// <summary>스스로 빛나는 색. 눈, 탄, 파편.</summary>
        public static Material Glow(Color c, float intensity = 1.6f)
        {
            var m = new Material(Load(ref _glow, "Glow")) { color = c };
            m.SetColor("_EmissionColor", c * intensity);
            return m;
        }

        /// <summary>재질을 같이 쓰는 상자. 용처럼 상자가 많은 것은 색마다 재질 하나를 나눠 쓴다.</summary>
        public static GameObject Box(string name, Material shared, Vector3 scale)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = Cube;
            go.AddComponent<MeshRenderer>().sharedMaterial = shared;
            go.transform.localScale = scale;
            return go;
        }

        public static GameObject Box(string name, Color color, Vector3 scale) => Box(name, Solid(color), scale);
    }
}
