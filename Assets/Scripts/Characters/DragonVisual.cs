using UnityEngine;

namespace Dragonia.Characters
{
    /// <summary>
    /// 애니메이션 이름표. 게임은 모델이 들고 온 클립 이름을 절대 모르고, 여기 적힌 이름만 안다.
    ///
    /// 새 모델을 끼울 때 하는 일은 하나뿐이다 — Animator 에 이 이름들의 상태를 만들고
    /// 그 모델의 클립을 물려 주는 것. 게임 코드는 한 줄도 안 바뀐다.
    /// </summary>
    public static class Anim
    {
        public const string Idle = "Idle";
        public const string Dodge = "Dodge";     // 대시 (무적 프레임)
        public const string Bite = "Bite";       // 근접 물기
        public const string Breath = "Breath";   // 숨결
        public const string Dive = "Dive";       // 공중에서 내려찍기
        public const string Tell = "Tell";       // 큰 기술 직전의 예고 (보스)
        public const string Hit = "Hit";
        public const string Die = "Die";

        // Animator 파라미터
        public const string PSpeed = "Speed";
        public const string PFlying = "Flying";
    }

    /// <summary>
    /// "용"과 "용의 모습"을 갈라 놓는 곳.
    ///
    /// 부모(DragonController·BossBrain)는 체력·판정·상태를 가진 논리 객체다. 모델은 그 밑에
    /// 매달린 자식일 뿐이고, 이 컴포넌트가 둘 사이의 유일한 통로다. 모델을 통째로 갈아도 부모는 모른다.
    ///
    /// 등록표(AssetRegistry)에 그 id 의 프리팹이 있으면 그걸 세우고 Animator 로 움직인다.
    /// 없으면 상자로 지은 로우폴리 용(ProceduralDragon)이 대신 선다 — 같은 이름표로 움직이므로
    /// 부모 쪽 코드는 어느 쪽인지 알 필요가 없다.
    /// </summary>
    public class DragonVisual : MonoBehaviour
    {
        [Tooltip("이 모습이 어느 id 의 것인지. AssetRegistry 의 id 와 같다.")]
        public string creatureId = "WESTERN";

        [Header("대역 용의 색 (모델이 없을 때)")]
        public Color bodyColor = new Color(0.66f, 0.16f, 0.13f);     // 2D 기본값 #c0392b 를 조금 깊게
        public Color wingColor = new Color(0.88f, 0.63f, 0.13f);     // 2D 기본값 #e0a020
        public Color glowColor = new Color(1f, 0.6f, 0.2f);

        [Header("붙이는 자리 (모델 안의 빈 오브젝트 이름)")]
        public string mouthBone = "Mouth";
        public string headBone = "Head";

        public Transform Mouth { get; private set; }
        public Transform Head { get; private set; }
        public Animator Animator { get; private set; }

        GameObject _model;
        ProceduralDragon _proc;

        void Awake()
        {
            if (_model == null) Build(creatureId);
        }

        /// <summary>색을 바꿔 다시 세운다. 보스는 데이터를 읽은 뒤에야 제 색을 안다.</summary>
        public void Build(string id, Color body, Color wing, Color glow)
        {
            bodyColor = body; wingColor = wing; glowColor = glow;
            Build(id);
        }

        /// <summary>등록표에서 모델을 꺼내 세운다. 게임 도중에 갈아끼워도 된다.</summary>
        public void Build(string id)
        {
            creatureId = id;
            if (_model != null) Destroy(_model);
            _proc = null;

            var registry = Registry.AssetRegistry.Instance;
            var prefab = registry != null ? registry.Prefab(id) : null;

            if (prefab == null)
            {
                _model = new GameObject($"(대역 용) {id}");
                _model.transform.SetParent(transform, false);
                _proc = _model.AddComponent<ProceduralDragon>();
                _proc.Build(bodyColor, wingColor, glowColor);
            }
            else
            {
                _model = Instantiate(prefab, transform, false);
                _model.name = $"모습: {id}";
                _model.transform.localScale = Vector3.one * registry.ScaleOf(id);
            }

            Animator = _model.GetComponentInChildren<Animator>();
            Mouth = Find(mouthBone) ?? MakeSocket(mouthBone, new Vector3(0f, 1.6f, 1.6f));
            Head = Find(headBone) ?? MakeSocket(headBone, new Vector3(0f, 1.9f, 1.2f));
        }

        Transform Find(string boneName)
        {
            foreach (var t in _model.GetComponentsInChildren<Transform>())
                if (t.name == boneName) return t;
            return null;
        }

        // 모델에 그 자리가 없으면 몸 기준으로 대충 만들어 둔다 (숨결이 허공에서 나오지 않게)
        Transform MakeSocket(string socketName, Vector3 local)
        {
            var go = new GameObject(socketName + " (임시)");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            return go.transform;
        }

        // ---- 부모가 부르는 것들. 진짜 모델이면 Animator 로, 대역이면 절차적 애니메이션으로 간다 ----

        public void Play(string state, float fade = 0.08f)
        {
            if (_proc != null) _proc.Play(state);
            else if (Animator != null) Animator.CrossFade(state, fade);
        }

        public void SetSpeed(float v)
        {
            if (_proc != null) _proc.SetSpeed(v);
            else if (Animator != null) Animator.SetFloat(Anim.PSpeed, v);
        }

        public void SetFlying(bool flying)
        {
            if (_proc != null) _proc.SetFlying(flying);
            else if (Animator != null) Animator.SetBool(Anim.PFlying, flying);
        }

        public void FlapKick() { if (_proc != null) _proc.FlapKick(); }
        public void Flash() { if (_proc != null) _proc.Flash(); }

        /// <summary>눈빛 색. 내 용은 고른 숨결 속성에 따라 눈빛이 바뀐다.</summary>
        public void SetGlow(Color c)
        {
            glowColor = c;
            if (_proc != null) _proc.SetGlow(c);
        }
    }
}
