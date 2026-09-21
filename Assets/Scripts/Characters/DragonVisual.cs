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
        public const string Walk = "Walk";
        public const string Run = "Run";
        public const string Dodge = "Dodge";     // 구르기 (무적 프레임)
        public const string Bite = "Bite";       // 근접 물기
        public const string Breath = "Breath";   // 숨결
        public const string Fly = "Fly";
        public const string Hit = "Hit";
        public const string Die = "Die";

        // Animator 파라미터
        public const string PSpeed = "Speed";
        public const string PGrounded = "Grounded";
    }

    /// <summary>
    /// "용"과 "용의 모습"을 갈라 놓는 곳.
    ///
    /// 부모(DragonController)는 체력·판정·상태를 가진 논리 객체다. 모델은 그 밑에 매달린 자식일 뿐이고,
    /// 이 컴포넌트가 둘 사이의 유일한 통로다. 모델을 통째로 갈아도 부모는 아무것도 모른다.
    ///
    /// 붙이는 자리(입·머리·발)는 모델 안에 같은 이름의 빈 오브젝트로 있어야 한다.
    /// 없으면 몸 기준으로 대충 잡아 두므로, 모델이 없어도 게임은 돌아간다.
    /// </summary>
    public class DragonVisual : MonoBehaviour
    {
        [Tooltip("이 모습이 어느 id 의 것인지. AssetRegistry 의 id 와 같다.")]
        public string creatureId = "WESTERN";

        [Header("붙이는 자리 (모델 안의 빈 오브젝트 이름)")]
        public string mouthBone = "Mouth";
        public string headBone = "Head";

        public Transform Mouth { get; private set; }
        public Transform Head { get; private set; }
        public Animator Animator { get; private set; }

        GameObject _model;

        void Awake()
        {
            if (_model == null) Build(creatureId);
        }

        /// <summary>등록표에서 모델을 꺼내 세운다. 게임 도중에 갈아끼워도 된다.</summary>
        public void Build(string id)
        {
            creatureId = id;
            if (_model != null) Destroy(_model);

            var registry = Registry.AssetRegistry.Instance;
            var prefab = registry != null ? registry.Prefab(id) : null;
            if (prefab == null)
            {
                // 모델이 아직 없어도 게임은 돌아가야 한다. 회색 상자를 세운다.
                _model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _model.name = $"(대역) {id}";
                Destroy(_model.GetComponent<Collider>());
            }
            else
            {
                _model = Instantiate(prefab);
                _model.name = $"모습: {id}";
            }

            _model.transform.SetParent(transform, false);
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.identity;
            float s = registry != null ? registry.ScaleOf(id) : 1f;
            _model.transform.localScale = Vector3.one * s;

            Animator = _model.GetComponentInChildren<Animator>();
            Mouth = Find(mouthBone) ?? MakeSocket(mouthBone, new Vector3(0f, 1.1f, 1.2f));
            Head = Find(headBone) ?? MakeSocket(headBone, new Vector3(0f, 1.4f, 0.8f));
        }

        Transform Find(string boneName)
        {
            foreach (var t in _model.GetComponentsInChildren<Transform>())
                if (t.name == boneName) return t;
            return null;
        }

        // 모델에 그 자리가 없으면 몸 기준으로 대충 만들어 둔다 (브레스가 허공에서 나오지 않게)
        Transform MakeSocket(string socketName, Vector3 local)
        {
            var go = new GameObject(socketName + " (임시)");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            return go.transform;
        }

        /// <summary>이름표로만 재생한다. 그 모델에 그 상태가 없으면 조용히 넘어간다.</summary>
        public void Play(string state, float fade = 0.1f)
        {
            if (Animator == null) return;
            Animator.CrossFade(state, fade);
        }

        public void SetSpeed(float v)
        {
            if (Animator != null) Animator.SetFloat(Anim.PSpeed, v);
        }
    }
}
