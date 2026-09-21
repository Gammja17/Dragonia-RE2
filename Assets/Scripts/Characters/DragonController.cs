using UnityEngine;
using UnityEngine.SceneManagement;
using Dragonia.Combat;

namespace Dragonia.Characters
{
    /// <summary>
    /// 내 용. 2D 의 탑뷰 슈팅을 3인칭 액션으로 옮긴 것.
    ///
    ///   Shift      누르는 순간 대시(가운데 구간 무적), 계속 누르고 있으면 달리기.
    ///              2D 의 "Shift 탁: 대시 / 꾹: 달리기"를 그대로 가져왔다. 공중에서도 된다
    ///   Space      땅에서는 뛰어오르고, 공중에서는 누를 때마다 날갯짓 한 번 —
    ///              연타하면 올라가고, 가만히 두면 날개를 편 채 천천히 내려온다
    ///   C (꾹)     날개를 접고 빨리 떨어진다
    ///   왼클릭·F (꾹) 숨결. 발이 묶이지 않는다 — 걸으면서, 날면서 쏜다. 누르고 있는 동안 기력이 샌다
    ///   오른클릭   땅: 물기 / 공중: 내려찍기 (상대에게 내리꽂고 착지 충격파)
    ///   1 2 3      불 · 얼음 · 번개. 눈빛이 같이 바뀐다
    ///
    /// 나는 데는 기력이 든다. 바닥 장판은 날아서 피할 수 있지만 영원히 떠 있을 수는 없다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DragonController : MonoBehaviour, IDamageable
    {
        [Header("땅")]
        public float walkSpeed = 4.2f;
        public float runSpeed = 8.5f;
        public float turnSharpness = 12f;
        public float gravity = -24f;

        [Header("날기")]
        public float jumpPower = 8f;
        public float flapPower = 7f;
        public float maxRise = 10f;
        public float glideGravity = -6f;
        public float glideFallMax = -2.5f;
        public float flySpeed = 9.5f;
        public float flapCost = 4f;
        public float glideDrain = 1.5f;
        public float maxHeight = 24f;

        [Header("대시")]
        public float dodgeSpeed = 13f;
        public float dodgeTime = 0.38f;
        [Tooltip("대시 중 안 맞는 구간. 처음과 끝은 맞는다 — 아무 때나 눌러도 다 피해지면 재미가 없다")]
        public float iFrameStart = 0.03f, iFrameEnd = 0.30f;
        public float dodgeCost = 20f;

        [Header("기력")]
        public float maxStamina = 100f;
        public float staminaRegen = 30f;
        public float runDrain = 10f;
        public float exhaustPause = 0.9f;

        [Header("물기")]
        public float biteTime = 0.5f;
        public float biteDamage = 38f;
        public float biteReach = 3.2f;
        public float biteArc = 120f;

        [Header("숨결")]
        public float breathDamage = 5f;
        public float breathInterval = 0.085f;
        [Tooltip("뿜는 동안 1초에 새는 기력")]
        public float breathDrain = 14f;
        [Tooltip("이만큼은 있어야 뿜기 시작한다. 바닥난 직후에 찔끔찔끔 나오지 않게")]
        public float breathMin = 8f;
        [Tooltip("땅에서 뿜는 동안의 걸음 속도 배율")]
        public float breathMoveScale = 0.8f;

        [Header("내려찍기")]
        public float diveSpeed = 24f;
        public float diveDamage = 70f;
        public float diveRadius = 4f;
        public float diveCost = 15f;

        [Header("몸")]
        public float maxHp = 100f;
        public float hurtGrace = 0.45f;
        public string element = Element.Fire;

        public float Hp { get; private set; }
        public float Stamina { get; private set; }
        public Faction Side => Faction.Player;
        public bool Alive => Hp > 0f;
        public bool Invulnerable => _iframe || _hurt > 0f || !Alive;
        public bool Airborne => !_grounded;

        enum State { Free, Dodge, Bite, Dive, Dead }
        State _state;
        float _stateTime, _exhaust, _vy, _hurt, _nextPellet, _deadTimer;
        bool _grounded = true, _winged, _iframe, _biteDone, _breathing;
        Vector3 _dashDir, _diveDir, _knock;

        CharacterController _cc;
        DragonVisual _visual;
        LockOnCamera _cam;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _visual = GetComponentInChildren<DragonVisual>();
            Stamina = maxStamina;
            Hp = maxHp;
        }

        void Start()
        {
            _cam = Camera.main != null ? Camera.main.GetComponent<LockOnCamera>() : null;
            if (_visual != null) _visual.SetGlow(Element.ColorOf(element));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _stateTime += dt;
            _hurt -= dt;
            _knock = Vector3.MoveTowards(_knock, Vector3.zero, 22f * dt);

            switch (_state)
            {
                case State.Free: Free(dt); break;
                case State.Dodge: Dodge(dt); break;
                case State.Bite: Bite(dt); break;
                case State.Dive: Dive(dt); break;
                case State.Dead:
                    Step(Vector3.zero, dt, false);
                    _deadTimer -= Time.unscaledDeltaTime;
                    if (_deadTimer <= 0f) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    return;
            }

            // 기력: 땅에 발을 딛고 있을 때만 찬다
            if (_exhaust > 0f) _exhaust -= dt;
            else if (_grounded && _state == State.Free && !_breathing && !Input.GetKey(KeyCode.LeftShift))
                Stamina = Mathf.Min(maxStamina, Stamina + staminaRegen * dt);

            if (_visual != null) _visual.SetFlying(!_grounded && _winged);
        }

        // ---------------------------------------------------------------- 평소

        void Free(float dt)
        {
            Vector3 wish = CameraRelative(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            bool moving = wish.sqrMagnitude > 0.01f;

            // 숨결은 상태가 아니다. 누르고 있는 동안 걷기·날기 위에 얹혀서 나간다
            bool wantBreath = LockOnCamera.ReadyForInput && (Input.GetMouseButton(0) || Input.GetKey(KeyCode.F));
            SetBreathing(wantBreath && Ready(_breathing ? 0.01f : breathMin));

            bool running = _grounded && moving && !_breathing && Input.GetKey(KeyCode.LeftShift) && Ready(1f);
            if (running) Spend(runDrain * dt);

            // Space: 땅에서는 뛰고, 공중에서는 날갯짓
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (_grounded) { _vy = jumpPower; _grounded = false; if (_visual != null) _visual.FlapKick(); }
                else if (Ready(flapCost))
                {
                    Spend(flapCost);
                    _winged = true;
                    _vy = Mathf.Min(maxRise, Mathf.Max(_vy, 0f) + flapPower);
                    if (_visual != null) _visual.FlapKick();
                }
            }

            float speed = _grounded ? (running ? runSpeed : walkSpeed * (_breathing ? breathMoveScale : 1f)) : flySpeed;
            Step(wish * speed, dt, true);

            Transform target = _cam != null ? _cam.Target : null;
            if (_breathing) BreathStream(dt);                       // 몸이 조준한 쪽을 본다 (옆걸음·뒷걸음으로 쏜다)
            else if (target != null) Face(target.position - transform.position, dt);
            else if (moving) Face(wish, dt);

            if (_visual != null) _visual.SetSpeed(_grounded ? wish.magnitude * (running ? 1f : 0.5f) : wish.magnitude * 0.6f);

            if (Input.GetKeyDown(KeyCode.Alpha1)) SetElement(Element.Fire);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetElement(Element.Ice);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetElement(Element.Thunder);

            if (Input.GetKeyDown(KeyCode.LeftShift) && Ready(dodgeCost)) { StartDodge(wish); return; }

            // 첫 클릭은 커서를 잠그는 데 쓰인다. 그 클릭으로 허공을 물지 않게 한다
            if (!LockOnCamera.ReadyForInput) return;

            if (Input.GetMouseButtonDown(1))
            {
                if (_grounded) { _biteDone = false; Enter(State.Bite, Anim.Bite); }
                else if (Ready(diveCost)) StartDive();
            }
        }

        void SetElement(string e)
        {
            element = e;
            if (_visual != null) _visual.SetGlow(Element.ColorOf(e));
        }

        // ---------------------------------------------------------------- 대시

        void StartDodge(Vector3 wish)
        {
            Spend(dodgeCost);
            _dashDir = wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(_dashDir);
            Enter(State.Dodge, Anim.Dodge);
        }

        void Dodge(float dt)
        {
            _iframe = _stateTime >= iFrameStart && _stateTime <= iFrameEnd;
            float k = 1f - Mathf.Clamp01(_stateTime / dodgeTime);
            if (!_grounded) _vy = Mathf.Max(_vy, -1f);                       // 공중 대시는 수평으로 나간다
            Step(_dashDir * dodgeSpeed * (0.4f + k * 0.6f), dt, true);
            if (_stateTime >= dodgeTime) { _iframe = false; Finish(); }
        }

        // ---------------------------------------------------------------- 물기

        void Bite(float dt)
        {
            float k = _stateTime / biteTime;
            // 목이 뻗는 순간에 몸도 같이 앞으로 나간다 — 제자리에서 물면 닿는 느낌이 안 난다
            Vector3 lunge = k > 0.25f && k < 0.5f ? transform.forward * 5f : Vector3.zero;
            Step(lunge, dt, true);
            Transform target = _cam != null ? _cam.Target : null;
            if (target != null && k < 0.3f) Face(target.position - transform.position, dt * 2f);

            if (!_biteDone && k >= 0.42f) { _biteDone = true; BiteHit(); }
            if (k >= 1f) Finish();
        }

        void BiteHit()
        {
            Vector3 origin = transform.position + Vector3.up;
            bool any = false;
            foreach (var col in Physics.OverlapSphere(origin, biteReach))
            {
                Vector3 to = col.bounds.ClosestPoint(origin) - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > biteArc * 0.5f) continue;
                if (Hit.Apply(col, Faction.Player, biteDamage, element, origin))
                {
                    any = true;
                    Feedback.Burst(col.bounds.ClosestPoint(origin + transform.forward), Element.ColorOf(element), 10, 8f);
                }
            }
            if (any) { Feedback.HitStop(0.07f); Feedback.Shake(0.5f); }
        }

        // ---------------------------------------------------------------- 숨결

        void SetBreathing(bool on)
        {
            if (on == _breathing) return;
            _breathing = on;
            _nextPellet = 0.12f;                                    // 입을 벌리는 짧은 틈
            if (_visual != null) _visual.SetBreathing(on);
        }

        void BreathStream(float dt)
        {
            Spend(breathDrain * dt);

            Vector3 from = _visual != null && _visual.Mouth != null ? _visual.Mouth.position : transform.position + Vector3.up * 1.5f;
            Vector3 aim = _cam != null ? _cam.AimDirection(from, !_grounded) : transform.forward;
            Face(aim, dt * 1.6f);
            if (_visual != null) _visual.SetAimPitch(Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg);

            // 한 번에 다 쏘지 않고 줄기로 흘려보낸다. 가까이서 쏠수록 많이 맞는다
            _nextPellet -= dt;
            while (_nextPellet <= 0f)
            {
                _nextPellet += breathInterval;
                Vector3 d = Quaternion.Euler(Random.Range(-3.5f, 3.5f), Random.Range(-5f, 5f), 0f) * aim;
                Projectile.Spawn(from, d, Faction.Player, breathDamage, element, 21f, 0.75f, 0.3f);
            }
        }

        // ---------------------------------------------------------------- 내려찍기

        void StartDive()
        {
            Spend(diveCost);
            Transform target = _cam != null ? _cam.Target : null;
            if (target != null) _diveDir = (_cam.TargetPoint - transform.position).normalized;
            else
            {
                Vector3 f = Camera.main != null ? Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized : transform.forward;
                _diveDir = (f + Vector3.down * 0.9f).normalized;
            }
            if (_diveDir.y > -0.2f) _diveDir = (new Vector3(_diveDir.x, -0.35f, _diveDir.z)).normalized;   // 어쨌든 아래로는 간다
            Face(_diveDir, 1f);
            _winged = false;
            Enter(State.Dive, Anim.Dive);
        }

        void Dive(float dt)
        {
            _vy = 0f;
            var flags = _cc.Move((_diveDir * diveSpeed + _knock) * dt);
            _grounded = (flags & CollisionFlags.Below) != 0 || _cc.isGrounded;
            bool hitSomething = (flags & CollisionFlags.Sides) != 0;
            if (_grounded || hitSomething || _stateTime > 1.1f) Impact();
        }

        void Impact()
        {
            Vector3 at = transform.position;
            bool any = false;
            foreach (var col in Physics.OverlapSphere(at, diveRadius))
                if (Hit.Apply(col, Faction.Player, diveDamage, element, at)) any = true;
            Feedback.Ring(at, Element.ColorOf(element), diveRadius);
            Feedback.Burst(at + Vector3.up * 0.3f, Element.ColorOf(element), 14, 9f);
            Feedback.Shake(any ? 1.1f : 0.6f);
            if (any) Feedback.HitStop(0.1f);
            Finish();
        }

        // ---------------------------------------------------------------- 몸

        /// <summary>한 프레임 움직인다. 중력과 활공, 넉백이 전부 여기서 합쳐진다.</summary>
        void Step(Vector3 planar, float dt, bool applyGravity)
        {
            if (_grounded && _vy < 0f) { _vy = -2f; _winged = false; }
            else if (applyGravity)
            {
                bool folding = Input.GetKey(KeyCode.C);
                bool gliding = _winged && !folding;
                _vy += (gliding ? glideGravity : gravity) * dt;
                if (gliding)
                {
                    _vy = Mathf.Max(_vy, glideFallMax);
                    Spend(glideDrain * dt);
                    if (Stamina <= 0f) _winged = false;          // 기력이 다하면 날개가 접힌다
                }
            }
            if (transform.position.y > maxHeight && _vy > 0f) _vy = 0f;

            var flags = _cc.Move((planar + _knock + Vector3.up * _vy) * dt);
            _grounded = (flags & CollisionFlags.Below) != 0 || _cc.isGrounded;
            if ((flags & CollisionFlags.Above) != 0 && _vy > 0f) _vy = 0f;
        }

        void Face(Vector3 dir, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir),
                                                  1f - Mathf.Exp(-turnSharpness * dt));
        }

        Vector3 CameraRelative(float x, float z)
        {
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null) return new Vector3(x, 0f, z);
            Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            return Vector3.ClampMagnitude(fwd * z + right * x, 1f);
        }

        void Enter(State s, string anim)
        {
            SetBreathing(false);
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

        bool Ready(float cost) => _exhaust <= 0f && Stamina >= cost;

        void Spend(float amount)
        {
            Stamina = Mathf.Max(0f, Stamina - amount);
            if (Stamina <= 0f) _exhaust = exhaustPause;
        }

        public void TakeDamage(float amount, string element, Vector3 from)
        {
            if (Invulnerable) return;
            Hp = Mathf.Max(0f, Hp - amount);
            _hurt = hurtGrace;                         // 겹쳐 날아온 탄에 한꺼번에 녹지 않게 잠깐 봐준다
            Vector3 away = transform.position - from; away.y = 0f;
            _knock = away.sqrMagnitude > 0.01f ? away.normalized * 7f : Vector3.zero;

            if (_visual != null) { _visual.Flash(); }
            Feedback.Shake(0.7f);
            Feedback.HitStop(0.05f);
            Feedback.Burst(transform.position + Vector3.up, new Color(1f, 0.35f, 0.3f), 7, 6f);

            if (Hp <= 0f)
            {
                _state = State.Dead;
                _deadTimer = 3f;
                _iframe = false;
                if (_visual != null) _visual.Play(Anim.Die);
            }
            else if (_state == State.Free && _visual != null) _visual.Play(Anim.Hit);
        }
    }
}
