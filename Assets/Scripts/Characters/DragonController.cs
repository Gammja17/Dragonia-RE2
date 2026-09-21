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
    ///   왼클릭·F (꾹) 숨결. 2D 와 같은 탄막이다 — 속성마다 연사 속도·갈래 수·위력이 다르고 (데이터 그대로),
    ///              발이 묶이지 않아서 걸으면서, 날면서 쏜다
    ///   오른클릭   땅: 물기 / 공중: 내려찍기 (상대에게 내리꽂고 착지 충격파)
    ///   1 2 3      불 · 얼음 · 번개. 눈빛이 같이 바뀐다
    ///
    /// 기력은 없다. 마음껏 날고 마음껏 쏜다 — 대시만 짧은 재사용 대기가 있다 (무적을 이어 붙이지 못하게).
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
        public float maxHeight = 24f;

        [Header("대시")]
        public float dodgeSpeed = 13f;
        public float dodgeTime = 0.38f;
        [Tooltip("대시 중 안 맞는 구간. 처음과 끝은 맞는다 — 아무 때나 눌러도 다 피해지면 재미가 없다")]
        public float iFrameStart = 0.03f, iFrameEnd = 0.30f;
        [Tooltip("대시를 다시 쓸 수 있을 때까지. 무적 구간을 이어 붙이지 못하게 한다")]
        public float dodgeCooldown = 0.55f;

        [Header("물기")]
        public float biteTime = 0.5f;
        public float biteDamage = 38f;
        public float biteReach = 3.2f;
        public float biteArc = 120f;

        [Header("숨결")]
        [Tooltip("연사 속도·갈래 수·위력·탄속은 elements.json 에서 온다. 여기는 데이터가 없을 때의 값과 3D 로 옮기는 배율")]
        public float shotRate = 0.36f;
        public float shotDamage = 8f;
        [Tooltip("2D 의 탄속(px/초)을 m/초로. 620px/초 → 약 22m/초")]
        public float shotSpeedScale = 1f / 28f;
        [Tooltip("2D 는 화면이 좁아서 탄이 금방 사라진다. 3D 는 거리가 멀어서 더 오래 살린다")]
        public float shotLifeScale = 2.2f;
        [Tooltip("땅에서 쏘는 동안의 걸음 속도 배율")]
        public float breathMoveScale = 0.8f;

        [Header("내려찍기")]
        public float diveSpeed = 24f;
        public float diveDamage = 70f;
        public float diveRadius = 4f;

        [Header("몸")]
        public float maxHp = 100f;
        public float hurtGrace = 0.45f;
        public string element = Element.Fire;

        public float Hp { get; private set; }
        public Faction Side => Faction.Player;
        public bool Alive => Hp > 0f;
        public bool Invulnerable => _iframe || _hurt > 0f || !Alive;
        public bool Airborne => !_grounded;

        enum State { Free, Dodge, Bite, Dive, Dead }
        State _state;
        float _stateTime, _vy, _hurt, _nextShot, _dodgeWait, _deadTimer;
        bool _grounded = true, _winged, _iframe, _biteDone, _breathing;
        Vector3 _dashDir, _diveDir, _knock;

        CharacterController _cc;
        DragonVisual _visual;
        LockOnCamera _cam;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _visual = GetComponentInChildren<DragonVisual>();
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

            if (_visual != null) _visual.SetFlying(!_grounded && _winged);
        }

        // ---------------------------------------------------------------- 평소

        void Free(float dt)
        {
            Vector3 wish = CameraRelative(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            bool moving = wish.sqrMagnitude > 0.01f;

            // 숨결은 상태가 아니다. 누르고 있는 동안 걷기·날기 위에 얹혀서 나간다
            bool wantBreath = LockOnCamera.ReadyForInput && (Input.GetMouseButton(0) || Input.GetKey(KeyCode.F));
            SetBreathing(wantBreath);
            _nextShot -= dt;
            _dodgeWait -= dt;

            bool running = _grounded && moving && !_breathing && Input.GetKey(KeyCode.LeftShift);

            // Space: 땅에서는 뛰고, 공중에서는 날갯짓
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (_grounded) { _vy = jumpPower; _grounded = false; if (_visual != null) _visual.FlapKick(); }
                else
                {
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

            if (Input.GetKeyDown(KeyCode.LeftShift) && _dodgeWait <= 0f) { StartDodge(wish); return; }

            // 첫 클릭은 커서를 잠그는 데 쓰인다. 그 클릭으로 허공을 물지 않게 한다
            if (!LockOnCamera.ReadyForInput) return;

            if (Input.GetMouseButtonDown(1))
            {
                if (_grounded) { _biteDone = false; Enter(State.Bite, Anim.Bite); }
                else StartDive();
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
            _dodgeWait = dodgeTime + dodgeCooldown;
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
            if (_visual != null) _visual.SetBreathing(on);
        }

        /// <summary>
        /// 2D 의 숨결 그대로: 속성마다 정해진 간격으로 한 번에 몇 갈래씩 쏜다.
        /// 불은 세 갈래 산탄, 얼음은 느리고 센 한 발, 번개는 약한 연사.
        /// </summary>
        void BreathStream(float dt)
        {
            Vector3 from = _visual != null && _visual.Mouth != null ? _visual.Mouth.position : transform.position + Vector3.up * 1.5f;
            Vector3 aim = _cam != null ? _cam.AimDirection(from, !_grounded) : transform.forward;
            Face(aim, dt * 1.6f);
            if (_visual != null) _visual.SetAimPitch(Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg);
            if (_nextShot > 0f) return;

            var el = Data.GameData.Elements?[element];
            float rate = el?["rate"]?.ToObject<float>() ?? shotRate;
            float damage = el?["damage"]?.ToObject<float>() ?? shotDamage;
            int pellets = el?["pellets"]?.ToObject<int>() ?? 1;
            float spread = (el?["spread"]?.ToObject<float>() ?? 0f) * Mathf.Rad2Deg;
            float speed = (el?["speed"]?.ToObject<float>() ?? 620f) * shotSpeedScale;
            float life = (el?["life"]?.ToObject<float>() ?? 0.6f) * shotLifeScale;
            float size = (el?["radius"]?.ToObject<float>() ?? 44f) / 100f;

            _nextShot = rate;
            Vector3 up = Vector3.Cross(aim, Vector3.Cross(Vector3.up, aim)).normalized;
            if (up.sqrMagnitude < 0.01f) up = Vector3.up;
            for (int i = 0; i < pellets; i++)
            {
                Vector3 d = Quaternion.AngleAxis((i - (pellets - 1) * 0.5f) * spread, up) * aim;
                Projectile.Spawn(from, d, Faction.Player, damage, element, speed, life, size);
            }
            Feedback.Burst(from + aim * 0.3f, Element.ColorOf(element), 3, 4f);
            if (_visual != null) _visual.Recoil();
        }

        // ---------------------------------------------------------------- 내려찍기

        void StartDive()
        {
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
                if (gliding) _vy = Mathf.Max(_vy, glideFallMax);
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
