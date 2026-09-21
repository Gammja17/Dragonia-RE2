using UnityEngine;
using Dragonia.Combat;

namespace Dragonia.Characters
{
    /// <summary>
    /// 내 용. 2D 의 탑뷰 슈팅을 3인칭 젤다식 액션으로 옮긴 것.
    ///
    /// 옮기면서 바뀐 것과 그대로인 것:
    ///   마우스로 조준해 연사      →  락온한 상대(없으면 카메라 앞)로 숨결
    ///   Shift 탁 = 무적 대시      →  구르기. 무적 프레임은 그대로 (2D 에서 이미 쓰던 설계다)
    ///   Shift 꾹 = 달리기         →  그대로, 다만 기력을 먹는다
    ///   근접 공격 없음            →  물기 추가 (젤다식이면 근접이 주가 되어야 한다)
    ///
    /// 체력·허기·성장 같은 수치는 여기 두지 않는다. 그건 데이터에서 오고, 이건 몸을 움직일 뿐이다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DragonController : MonoBehaviour, IDamageable
    {
        [Header("움직임")]
        public float walkSpeed = 3.5f;
        public float runSpeed = 7f;
        public float turnSharpness = 12f;
        public float gravity = -22f;

        [Header("구르기")]
        public float dodgeSpeed = 11f;
        public float dodgeTime = 0.42f;
        [Tooltip("구르는 동안 안 맞는 구간. 시작과 끝을 뺀 가운데만 무적이다.")]
        public float iFrameStart = 0.08f, iFrameEnd = 0.30f;
        public float dodgeCost = 25f;

        [Header("기력")]
        public float maxStamina = 100f;
        public float staminaRegen = 26f;
        public float runDrain = 12f;
        [Tooltip("기력을 다 쓰면 이만큼 쉬어야 다시 찬다.")]
        public float exhaustPause = 0.8f;

        [Header("공격")]
        public float biteTime = 0.5f;
        public float biteDamage = 14f;
        public float biteReach = 2.6f;
        public float biteArc = 100f;          // 앞쪽 이만큼의 부채꼴 안에 있으면 맞는다
        public float breathTime = 0.9f;
        public float breathDamage = 7f;
        [Tooltip("숨결 한 줄기가 몇 알로 나가는지. 알마다 판정이 따로 난다.")]
        public int breathPellets = 6;
        public float breathCost = 18f;

        [Header("몸")]
        public float maxHp = 100f;
        [Tooltip("지금 쓰는 숨결 속성. 2D 의 1/2/3 키와 같다.")]
        public string element = Element.Fire;

        public float Stamina { get; private set; }
        public bool Invulnerable { get; private set; }
        public bool Busy => _state != State.Free;

        public float Hp { get; private set; }
        public Faction Side => Faction.Player;
        public bool Alive => Hp > 0f;

        enum State { Free, Dodge, Bite, Breath }
        State _state;
        float _stateTime;
        float _exhaust;
        Vector3 _dodgeDir;
        Vector3 _velocity;

        CharacterController _cc;
        DragonVisual _visual;
        LockOnCamera _cam;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _visual = GetComponentInChildren<DragonVisual>();
            _cam = Camera.main != null ? Camera.main.GetComponent<LockOnCamera>() : null;
            Stamina = maxStamina;
            Hp = maxHp;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _stateTime += dt;
            Tick(dt);
            Regen(dt);
        }

        void Tick(float dt)
        {
            switch (_state)
            {
                case State.Free: Free(dt); break;
                case State.Dodge: Dodge(dt); break;
                case State.Bite:
                case State.Breath:
                    Move(Vector3.zero, 0f, dt);          // 공격 중엔 발이 묶인다
                    if (_stateTime >= (_state == State.Bite ? biteTime : breathTime)) Finish();
                    break;
            }
        }

        void Free(float dt)
        {
            Vector3 wish = CameraRelative(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            bool wantsRun = Input.GetKey(KeyCode.LeftShift) && wish.sqrMagnitude > 0.01f && Ready(1f);
            float speed = wantsRun ? runSpeed : walkSpeed;
            if (wantsRun) Spend(runDrain * dt);

            Move(wish, speed, dt);

            // 락온 중에는 상대를 계속 바라본다 (젤다식 스트레이프)
            Transform target = _cam != null ? _cam.Target : null;
            if (target != null) FaceTowards(target.position - transform.position, dt);
            else if (wish.sqrMagnitude > 0.01f) FaceTowards(wish, dt);

            if (_visual != null) _visual.SetSpeed(wish.magnitude * (wantsRun ? 1f : 0.5f));

            // 숫자 키로 숨결 속성을 바꾼다 (2D 와 같다)
            if (Input.GetKeyDown(KeyCode.Alpha1)) element = Element.Fire;
            if (Input.GetKeyDown(KeyCode.Alpha2)) element = Element.Ice;
            if (Input.GetKeyDown(KeyCode.Alpha3)) element = Element.Thunder;

            if (Input.GetKeyDown(KeyCode.Space) && Ready(dodgeCost)) StartDodge(wish);
            else if (Input.GetMouseButtonDown(0)) { Enter(State.Bite, Anim.Bite); Invoke(nameof(BiteHit), biteTime * 0.4f); }
            else if (Input.GetMouseButtonDown(1) && Ready(breathCost)) { Spend(breathCost); Enter(State.Breath, Anim.Breath); Breathe(); }
        }

        void StartDodge(Vector3 wish)
        {
            Spend(dodgeCost);
            _dodgeDir = wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(_dodgeDir);
            Enter(State.Dodge, Anim.Dodge);
        }

        void Dodge(float dt)
        {
            // 처음과 끝은 맞는다. 가운데만 무적 — 아무 때나 구르면 다 피해지면 재미가 없다
            Invulnerable = _stateTime >= iFrameStart && _stateTime <= iFrameEnd;
            float k = 1f - Mathf.Clamp01(_stateTime / dodgeTime);      // 뒤로 갈수록 느려진다
            Move(_dodgeDir, dodgeSpeed * (0.35f + k * 0.65f), dt);
            if (_stateTime >= dodgeTime) { Invulnerable = false; Finish(); }
        }

        void Enter(State s, string anim)
        {
            _state = s;
            _stateTime = 0f;
            if (_visual != null) _visual.Play(anim);
        }

        void Finish()
        {
            _state = State.Free;
            _stateTime = 0f;
            if (_visual != null) _visual.Play(Anim.Idle);
        }

        void Move(Vector3 dir, float speed, float dt)
        {
            Vector3 step = dir.normalized * speed;
            if (_cc.isGrounded && _velocity.y < 0f) _velocity.y = -2f;
            _velocity.y += gravity * dt;
            step.y = _velocity.y;
            _cc.Move(step * dt);
        }

        void FaceTowards(Vector3 dir, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), 1f - Mathf.Exp(-turnSharpness * dt));
        }

        /// <summary>입력을 카메라가 보는 방향 기준으로 바꾼다. 3인칭이면 이게 없으면 조작이 안 맞는다.</summary>
        Vector3 CameraRelative(float x, float z)
        {
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null) return new Vector3(x, 0f, z);
            Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            return Vector3.ClampMagnitude(fwd * z + right * x, 1f);
        }

        bool Ready(float cost) => _exhaust <= 0f && Stamina >= cost;

        void Spend(float amount)
        {
            Stamina = Mathf.Max(0f, Stamina - amount);
            if (Stamina <= 0f) _exhaust = exhaustPause;       // 바닥나면 잠깐 아무것도 못 한다
        }

        /// <summary>물기. 앞쪽 부채꼴 안의 상대를 한 번에 친다 — 3인칭에서는 이게 주 공격이다.</summary>
        void BiteHit()
        {
            Vector3 origin = transform.position + Vector3.up;
            foreach (var col in Physics.OverlapSphere(origin, biteReach))
            {
                Vector3 to = col.transform.position - origin;
                to.y = 0f;
                if (Vector3.Angle(transform.forward, to) > biteArc * 0.5f) continue;
                Hit.Apply(col, Faction.Player, biteDamage, element, origin);
            }
        }

        /// <summary>
        /// 숨결. 2D 에서는 마우스로 조준해 연사했지만, 3인칭에서는 락온한 상대를 향해 뿜는다.
        /// 한 줄기가 여러 알로 나가서 가까울수록 많이 맞는다 — 붙어서 쏘는 게 이득이 되도록.
        /// </summary>
        void Breathe()
        {
            var visual = _visual;
            Vector3 from = visual != null && visual.Mouth != null ? visual.Mouth.position : transform.position + Vector3.up * 1.2f;
            Vector3 dir = _cam != null ? _cam.AimDirection(from) : transform.forward;
            transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));

            for (int i = 0; i < breathPellets; i++)
            {
                float spread = (i - (breathPellets - 1) * 0.5f) * 4.5f;
                Vector3 d = Quaternion.Euler(Random.Range(-3f, 3f), spread, 0f) * dir;
                Projectile.Spawn(from, d, Faction.Player, breathDamage, element, 17f, 0.55f, 0.35f);
            }
        }

        public void TakeDamage(float amount, string element, Vector3 from)
        {
            if (!Alive || Invulnerable) return;
            Hp = Mathf.Max(0f, Hp - amount);
            if (_visual != null) _visual.Play(Hp > 0f ? Anim.Hit : Anim.Die);
        }

        void Regen(float dt)
        {
            if (_exhaust > 0f) { _exhaust -= dt; return; }
            if (_state == State.Free) Stamina = Mathf.Min(maxStamina, Stamina + staminaRegen * dt);
        }
    }
}
