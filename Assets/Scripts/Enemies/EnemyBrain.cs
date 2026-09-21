using UnityEngine;

namespace Dragonia.Enemies
{
    /// <summary>
    /// 적의 머리. 2D 드래고니아의 entities/enemyAI.js 를 그대로 옮긴 것이다.
    ///
    /// 거기서 쓰던 네 박자가 3D 액션 보스 문법과 정확히 같아서, 설계를 다시 할 필요가 없었다:
    ///
    ///   다가감(approach) → 예고(tell) → 발동(act) → 숨 고르기(recover)
    ///
    /// 예고가 이 구조의 전부다. 보고 피할 수 있어야 하고, 그래서 크고 분명해야 한다.
    /// 2D 에서는 바닥에 선을 긋고 몸을 달아오르게 했다. 3D 에서는 웅크리는 동작과
    /// 바닥 표식(telegraph)이 그 자리를 대신한다.
    ///
    /// 숨 고르기도 마찬가지로 설계의 일부다 — 때릴 틈이 여기서 나온다.
    /// 돌진이 빗나가면 벽에 박혀 더 오래 기절하는 것도 2D 에서 가져왔다.
    /// </summary>
    public class EnemyBrain : MonoBehaviour
    {
        public enum Move { Chase, Charge, Flank, Kite, Guard, Summon }
        public enum Phase { Idle, Approach, Tell, Act, Recover, Stun }

        [Header("정체")]
        [Tooltip("데이터의 id. 예: SLIME, GOBLIN, MORGATH")]
        public string enemyId = "SLIME";
        public Move behaviour = Move.Chase;

        [Header("수치")]
        public float maxHp = 60f;
        public float speed = 3.2f;
        public float aggroRange = 14f;
        public float attackRange = 2.4f;

        [Header("네 박자 (초)")]
        [Tooltip("예고. 이 시간 동안 플레이어가 피할 수 있어야 한다.")]
        public float tellTime = 0.55f;
        public float actTime = 0.35f;
        [Tooltip("숨 고르기. 때릴 틈이 여기서 난다 — 짧으면 싸움이 답답해진다.")]
        public float recoverTime = 0.9f;
        public float stunTime = 2.2f;

        [Header("돌진")]
        public float chargeSpeed = 12f;
        public float chargeDistance = 9f;

        [Header("예고 표시")]
        [Tooltip("바닥에 깔 경고 표식. 비어 있으면 몸 색만 달아오른다.")]
        public GameObject telegraphPrefab;

        public bool Alive => _hp > 0f;
        public Phase Current => _phase;

        float _hp;
        Phase _phase = Phase.Idle;
        float _phaseTime;
        Vector3 _lockedDir;         // 예고할 때 방향을 잠근다. 발동 중에 따라오면 피할 수가 없다
        GameObject _telegraph;

        Transform _player;
        CharacterController _cc;
        Characters.DragonVisual _visual;

        void Awake()
        {
            _hp = maxHp;
            _cc = GetComponent<CharacterController>();
            _visual = GetComponentInChildren<Characters.DragonVisual>();
            var p = GameObject.FindWithTag("Player");
            if (p != null) _player = p.transform;
        }

        void Update()
        {
            if (!Alive || _player == null) return;
            float dt = Time.deltaTime;
            _phaseTime += dt;

            float dist = Vector3.Distance(transform.position, _player.position);

            switch (_phase)
            {
                case Phase.Idle:
                    if (dist < aggroRange) Go(Phase.Approach);
                    break;

                case Phase.Approach:
                    Approach(dist, dt);
                    break;

                case Phase.Tell:
                    // 예고 중에는 제자리에서 웅크린다. 방향은 시작할 때 잠갔다
                    if (_phaseTime >= tellTime) { ClearTelegraph(); Go(Phase.Act); Strike(); }
                    break;

                case Phase.Act:
                    if (behaviour == Move.Charge) Dash(dt);
                    if (_phaseTime >= actTime) Go(Phase.Recover);
                    break;

                case Phase.Recover:
                    if (_phaseTime >= recoverTime) Go(Phase.Approach);
                    break;

                case Phase.Stun:
                    if (_phaseTime >= stunTime) Go(Phase.Approach);
                    break;
            }
        }

        void Approach(float dist, float dt)
        {
            float reach = behaviour == Move.Charge ? chargeDistance : attackRange;
            if (dist <= reach) { BeginTell(); return; }
            if (dist > aggroRange * 1.6f) { Go(Phase.Idle); return; }

            Vector3 dir = Flat(_player.position - transform.position).normalized;
            if (behaviour == Move.Flank) dir = Quaternion.Euler(0f, 55f, 0f) * dir;    // 옆으로 돌아 들어온다
            if (behaviour == Move.Kite && dist < attackRange * 2f) dir = -dir;         // 거리를 벌린다

            Walk(dir, speed, dt);
            Face(dir, dt);
            if (_visual != null) _visual.SetSpeed(1f);
        }

        void BeginTell()
        {
            _lockedDir = Flat(_player.position - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(_lockedDir);
            Go(Phase.Tell);
            if (_visual != null) { _visual.Play(Characters.Anim.Idle); _visual.SetSpeed(0f); }
            ShowTelegraph();
        }

        void ShowTelegraph()
        {
            if (telegraphPrefab == null) return;
            _telegraph = Instantiate(telegraphPrefab, transform.position, Quaternion.LookRotation(_lockedDir));
            // 돌진은 앞으로 길게, 나머지는 발밑 원
            float length = behaviour == Move.Charge ? chargeDistance : attackRange;
            _telegraph.transform.localScale = new Vector3(1f, 1f, length);
        }

        void ClearTelegraph()
        {
            if (_telegraph != null) Destroy(_telegraph);
        }

        void Strike()
        {
            if (_visual != null) _visual.Play(Characters.Anim.Bite);
            // 실제 판정은 애니메이션 이벤트나 Hitbox 가 맡는다. 여기서는 박자만 센다.
        }

        void Dash(float dt)
        {
            Walk(_lockedDir, chargeSpeed, dt);
            // 빗나가고 벽에 박으면 더 오래 기절한다 — 때릴 틈을 크게 주는 자리다
            if (_cc != null && (_cc.collisionFlags & CollisionFlags.Sides) != 0)
            {
                ClearTelegraph();
                Go(Phase.Stun);
                if (_visual != null) _visual.Play(Characters.Anim.Hit);
            }
        }

        void Walk(Vector3 dir, float v, float dt)
        {
            if (_cc == null) { transform.position += dir * v * dt; return; }
            _cc.Move((dir * v + Vector3.down * 9.8f) * dt);
        }

        void Face(Vector3 dir, float dt)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), 1f - Mathf.Exp(-9f * dt));
        }

        void Go(Phase p)
        {
            _phase = p;
            _phaseTime = 0f;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        public void TakeDamage(float amount)
        {
            if (!Alive) return;
            _hp -= amount;
            if (_visual != null) _visual.Play(_hp > 0f ? Characters.Anim.Hit : Characters.Anim.Die);
            if (_hp <= 0f) { ClearTelegraph(); enabled = false; }
        }

        void OnDisable() => ClearTelegraph();
    }
}
