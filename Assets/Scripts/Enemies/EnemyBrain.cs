using UnityEngine;
using Dragonia.Combat;

namespace Dragonia.Enemies
{
    /// <summary>
    /// 졸개의 머리. 2D 드래고니아의 entities/enemyAI.js 를 옮긴 것이다.
    ///
    /// 거기서 쓰던 네 박자가 3D 액션 문법과 정확히 같아서 설계를 다시 할 필요가 없었다:
    ///
    ///   다가감(approach) → 예고(tell) → 발동(act) → 숨 고르기(recover)
    ///
    /// 예고가 이 구조의 전부다. 보고 피할 수 있어야 하고, 그래서 크고 분명해야 한다.
    /// 예고를 시작할 때 방향을 잠근다 — 발동 중에도 따라오면 피할 수가 없다.
    /// 숨 고르기도 설계의 일부다. 때릴 틈이 여기서 나온다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyBrain : MonoBehaviour, IDamageable
    {
        public enum Move { Chase, Flank, Kite }
        public enum Phase { Idle, Approach, Tell, Act, Recover }

        [Header("정체")]
        public string enemyId = "GHOST";
        public Move behaviour = Move.Chase;

        [Header("수치")]
        public float maxHp = 60f;
        public float speed = 3.6f;
        public float damage = 8f;
        public float aggroRange = 40f;
        public float attackRange = 2.2f;

        [Header("네 박자 (초)")]
        [Tooltip("예고. 이 시간 동안 플레이어가 피할 수 있어야 한다.")]
        public float tellTime = 0.55f;
        public float actTime = 0.3f;
        [Tooltip("숨 고르기. 때릴 틈이 여기서 난다 — 짧으면 싸움이 답답해진다.")]
        public float recoverTime = 1.0f;

        public bool Alive => _hp > 0f;
        public Faction Side => Faction.Enemy;
        public bool Invulnerable => false;

        float _hp;
        Phase _phase = Phase.Idle;
        float _phaseTime;
        bool _struck;
        Vector3 _lockedDir;

        Transform _player;
        CharacterController _cc;
        Characters.DragonVisual _visual;

        void Awake()
        {
            _hp = maxHp;
            _cc = GetComponent<CharacterController>();
        }

        void Start()
        {
            _visual = GetComponentInChildren<Characters.DragonVisual>();
            var p = GameObject.FindWithTag("Player");
            if (p != null) _player = p.transform;
        }

        public void Init(float hp, float moveSpeed, float hitDamage)
        {
            maxHp = _hp = hp; speed = moveSpeed; damage = hitDamage;
        }

        void Update()
        {
            if (!Alive || _player == null) return;
            float dt = Time.deltaTime;
            _phaseTime += dt;

            Vector3 to = _player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;

            switch (_phase)
            {
                case Phase.Idle:
                    Walk(Vector3.zero, 0f, dt);
                    if (dist < aggroRange) Go(Phase.Approach);
                    break;

                case Phase.Approach:
                {
                    if (dist <= attackRange) { BeginTell(to); break; }
                    Vector3 dir = to.normalized;
                    if (behaviour == Move.Flank) dir = Quaternion.Euler(0f, 50f, 0f) * dir;     // 옆으로 돌아 들어온다
                    if (behaviour == Move.Kite && dist < attackRange * 2f) dir = -dir;          // 거리를 벌린다
                    Walk(dir, speed, dt);
                    Face(dir, dt);
                    if (_visual != null) _visual.SetSpeed(0.8f);
                    break;
                }

                case Phase.Tell:
                    Walk(Vector3.zero, 0f, dt);
                    if (_phaseTime >= tellTime) { Go(Phase.Act); _struck = false; if (_visual != null) _visual.Play(Characters.Anim.Bite); }
                    break;

                case Phase.Act:
                    // 잠가 둔 방향으로 짧게 덤빈다
                    Walk(_lockedDir, speed * 2.2f, dt);
                    if (!_struck && _phaseTime >= actTime * 0.4f) { _struck = true; Strike(); }
                    if (_phaseTime >= actTime) Go(Phase.Recover);
                    break;

                case Phase.Recover:
                    Walk(Vector3.zero, 0f, dt);
                    if (_phaseTime >= recoverTime) Go(Phase.Approach);
                    break;
            }
        }

        void BeginTell(Vector3 to)
        {
            _lockedDir = to.sqrMagnitude > 0.001f ? to.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(_lockedDir);
            Go(Phase.Tell);
            if (_visual != null) { _visual.SetSpeed(0f); _visual.Play(Characters.Anim.Tell); }
        }

        void Strike()
        {
            Vector3 origin = transform.position + Vector3.up * 0.6f + _lockedDir * 0.8f;
            foreach (var col in Physics.OverlapSphere(origin, 1.3f))
                if (Hit.Apply(col, Faction.Enemy, damage, null, transform.position)) break;
        }

        void Walk(Vector3 dir, float v, float dt)
        {
            if (_cc == null || !_cc.enabled) return;
            _cc.Move((dir * v + Vector3.down * 10f) * dt);
            if (v <= 0f && _visual != null && _phase != Phase.Approach) _visual.SetSpeed(0f);
        }

        void Face(Vector3 dir, float dt)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-9f * dt));
        }

        void Go(Phase p) { _phase = p; _phaseTime = 0f; }

        public void TakeDamage(float amount, string element, Vector3 from)
        {
            if (!Alive) return;
            _hp -= amount;
            if (_visual != null) _visual.Flash();
            if (_hp > 0f)
            {
                if (_phase != Phase.Tell && _phase != Phase.Act && _visual != null) _visual.Play(Characters.Anim.Hit);
                return;
            }
            if (_visual != null) _visual.Play(Characters.Anim.Die);
            Feedback.Burst(transform.position + Vector3.up * 0.6f, new Color(0.75f, 0.8f, 1f), 10, 6f);
            if (_cc != null) _cc.enabled = false;
            Destroy(gameObject, 1.2f);
        }
    }

    /// <summary>보스가 불러내는 졸개. 작은 용 한 마리를 그 자리에서 짓는다.</summary>
    public static class Minion
    {
        public static EnemyBrain Spawn(Vector3 at, string id, Color body, Color wing, Color glow)
        {
            var go = new GameObject("졸개: " + id);
            go.transform.position = at + Vector3.up * 0.1f;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.2f; cc.radius = 0.5f; cc.center = new Vector3(0f, 0.65f, 0f);

            var vgo = new GameObject("모습");
            vgo.transform.SetParent(go.transform, false);
            vgo.transform.localScale = Vector3.one * 0.45f;
            var visual = vgo.AddComponent<Characters.DragonVisual>();
            visual.Build(id, body, wing, glow);

            var brain = go.AddComponent<EnemyBrain>();
            brain.enemyId = id;
            brain.behaviour = Random.value < 0.5f ? EnemyBrain.Move.Chase : EnemyBrain.Move.Flank;
            brain.Init(45f, 4.2f, 8f);
            return brain;
        }
    }
}
