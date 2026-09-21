using System.Collections.Generic;
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

        // ---- 상자만으로는 안 나오는 모양 둘: 끝이 좁아지는 상자, 얇은 판 ----

        static readonly Dictionary<long, Mesh> _tapers = new Dictionary<long, Mesh>();

        /// <summary>
        /// 앞(+Z)으로 갈수록 좁아지는 상자. 앞면이 가로 tx, 세로 ty 배로 줄어든다.
        /// (1,1)이면 그냥 상자, (0,0)이면 뿔, (1,0)이면 지느러미 모양 쐐기가 된다.
        /// 목·꼬리·주둥이·뿔·발톱이 전부 이걸로 만들어진다. 같은 비율은 한 번만 만들어 돌려쓴다.
        /// </summary>
        public static Mesh Taper(float tx, float ty)
        {
            long key = Mathf.RoundToInt(tx * 1000f) * 10000L + Mathf.RoundToInt(ty * 1000f);
            if (_tapers.TryGetValue(key, out var cached) && cached != null) return cached;

            float fx = 0.5f * tx, fy = 0.5f * ty;
            Vector3 b0 = new(-.5f, -.5f, -.5f), b1 = new(.5f, -.5f, -.5f), b2 = new(.5f, .5f, -.5f), b3 = new(-.5f, .5f, -.5f);
            Vector3 f0 = new(-fx, -fy, .5f), f1 = new(fx, -fy, .5f), f2 = new(fx, fy, .5f), f3 = new(-fx, fy, .5f);
            var v = new List<Vector3>(); var t = new List<int>();
            Quad(v, t, b0, b1, b2, b3, Vector3.back);
            Quad(v, t, f0, f1, f2, f3, Vector3.forward);
            Quad(v, t, b3, b2, f2, f3, Vector3.up);
            Quad(v, t, b0, b1, f1, f0, Vector3.down);
            Quad(v, t, b0, b3, f3, f0, Vector3.left);
            Quad(v, t, b1, b2, f2, f1, Vector3.right);
            var m = Finish(v, t, $"좁아지는 상자 {tx:0.##}x{ty:0.##}");
            _tapers[key] = m;
            return m;
        }

        /// <summary>
        /// XZ 평면에 놓인 얇은 판 (두께는 y 로 -0.5~0.5, localScale.y 로 정한다). 날개막과 꼬리 끝 지느러미.
        /// 첫 점에서 부채꼴로 나누므로, 첫 점에서 나머지 점이 전부 보이는 모양이어야 한다.
        /// </summary>
        public static Mesh Plate(Vector2[] outline)
        {
            int n = outline.Length;
            float area = 0f;
            for (int i = 0; i < n; i++) { var a = outline[i]; var b = outline[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            float sign = area >= 0f ? 1f : -1f;

            var v = new List<Vector3>(); var t = new List<int>();
            Vector3 Top(int i) => new Vector3(outline[i].x, 0.5f, outline[i].y);
            Vector3 Bot(int i) => new Vector3(outline[i].x, -0.5f, outline[i].y);
            for (int i = 1; i < n - 1; i++)
            {
                Tri(v, t, Top(0), Top(i), Top(i + 1), Vector3.up);
                Tri(v, t, Bot(0), Bot(i), Bot(i + 1), Vector3.down);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector2 e = outline[j] - outline[i];
                Quad(v, t, Bot(i), Bot(j), Top(j), Top(i), new Vector3(e.y, 0f, -e.x) * sign);
            }
            return Finish(v, t, "판");
        }

        // 면마다 꼭짓점을 따로 넣어서 각지게 보이게 한다. 감는 방향은 바깥 방향을 보고 알아서 맞춘다
        static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) (b, c) = (c, b);
            int o = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            t.Add(o); t.Add(o + 1); t.Add(o + 2);
        }

        static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            // 한쪽 변이 점으로 줄어든 면(뿔 끝)에서도 넓이가 남아 있는 쪽 삼각형으로 방향을 잰다
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) n = Vector3.Cross(c - a, d - a);
            if (Vector3.Dot(n, outward) < 0f) { (b, d) = (d, b); }
            int o = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            t.Add(o); t.Add(o + 1); t.Add(o + 2);
            t.Add(o); t.Add(o + 2); t.Add(o + 3);
        }

        static Mesh Finish(List<Vector3> v, List<int> t, string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>아무 그물로나 부품을 만든다. 자잘한 부품은 그림자를 끈다 (그리는 횟수가 절반이 된다).</summary>
        public static GameObject Shape(string name, Mesh mesh, Material shared, Vector3 scale, bool shadow = true)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = shared;
            if (!shadow) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.localScale = scale;
            return go;
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
