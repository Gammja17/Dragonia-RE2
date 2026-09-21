using UnityEngine;
using Dragonia.Combat;

namespace Dragonia.Characters
{
    /// <summary>
    /// 3인칭 카메라와 락온. 2D 에서 마우스로 조준하던 자리를 대신한다.
    ///
    /// 첫 판은 화면이 요동쳤다. 원인이 둘이었다:
    ///
    ///   1. 마우스를 잠그지 않았다. 커서를 움직이기만 해도 카메라가 돌아서, 클릭하러
    ///      가는 동안 화면이 휘둘렸다. 이제 클릭하면 커서를 잠그고, 잠긴 동안에만 돈다.
    ///   2. 위치를 보간한 다음, 그 "보간된 위치"에서 다시 바라볼 방향을 계산했다.
    ///      위치와 회전이 서로를 쫓아다니며 출렁였다. 이제 회전은 yaw/pitch 에서 곧바로
    ///      나오고, 위치는 그 회전에서 계산된다. 부드럽게 하는 건 축(pivot) 하나뿐이다.
    ///
    /// 락온도 고쳤다. 처음엔 EnemyBrain 만 찾아서 정작 보스(BossBrain)에는 걸리지 않았다.
    /// 이제 "맞을 수 있는 적"이면 누구에게나 걸린다.
    /// </summary>
    public class LockOnCamera : MonoBehaviour
    {
        [Header("따라다니기")]
        public Transform follow;
        public float pivotHeight = 2.0f;
        public float distance = 7.5f;
        public float flyingDistance = 10f;
        public float pivotSmooth = 0.06f;

        [Header("둘러보기")]
        public float sensitivity = 2.2f;
        public float minPitch = -40f, maxPitch = 70f;

        [Header("락온")]
        public float lockRange = 40f;
        [Tooltip("화면 한가운데에서 이 각도 안에 있어야 걸린다.")]
        public float lockCone = 75f;
        public KeyCode lockKey = KeyCode.Q;

        public Transform Target { get; private set; }
        public static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked || Application.isEditor;

        /// <summary>
        /// 지난 프레임이 끝날 때 이미 잠겨 있었는가. 공격 입력은 이걸 본다 —
        /// 커서를 잠그려고 누른 바로 그 클릭으로 허공을 물지 않게 하려는 것이다.
        /// </summary>
        public static bool ReadyForInput { get; private set; }

        float _yaw, _pitch = 16f, _dist;
        Vector3 _pivot, _pivotVel;
        DragonController _player;

        void Start()
        {
            if (follow == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) follow = p.transform;
            }
            if (follow != null)
            {
                _player = follow.GetComponent<DragonController>();
                _pivot = follow.position + Vector3.up * pivotHeight;
                _yaw = follow.eulerAngles.y;
            }
            _dist = distance;
        }

        void Update()
        {
            // 화면을 누르면 커서를 잠근다. 풀 때는 Esc (웹에서는 브라우저가 알아서 풀어 준다)
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Input.GetKeyDown(lockKey)) Target = Target != null ? null : Best();
            if (Target != null && !StillValid(Target)) Target = null;

            if (Target == null)
            {
                if (Cursor.lockState != CursorLockMode.Locked) return;
                // 웹에서는 가끔 마우스 이동량이 한 프레임에 크게 튄다. 잘라 둔다
                float mx = Mathf.Clamp(Input.GetAxis("Mouse X"), -12f, 12f);
                float my = Mathf.Clamp(Input.GetAxis("Mouse Y"), -12f, 12f);
                _yaw += mx * sensitivity;
                _pitch = Mathf.Clamp(_pitch - my * sensitivity, minPitch, maxPitch);
            }
            else
            {
                // 나와 상대를 한 화면에 담는다. 내가 위로 날아오르면 카메라도 같이 내려다본다
                Vector3 to = TargetPoint - (follow.position + Vector3.up * pivotHeight);
                float flat = new Vector2(to.x, to.z).magnitude;
                float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float wantPitch = Mathf.Clamp(14f - Mathf.Atan2(to.y, Mathf.Max(flat, 0.01f)) * Mathf.Rad2Deg * 0.7f, -25f, 60f);
                float k = 1f - Mathf.Exp(-7f * Time.deltaTime);
                _yaw = Mathf.LerpAngle(_yaw, wantYaw, k);
                _pitch = Mathf.Lerp(_pitch, wantPitch, k);
            }
        }

        void LateUpdate()
        {
            if (follow == null) return;

            // 부드럽게 하는 건 축 하나뿐이다. 회전과 위치는 거기서 곧바로 계산한다
            _pivot = Vector3.SmoothDamp(_pivot, follow.position + Vector3.up * pivotHeight, ref _pivotVel, pivotSmooth);

            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            bool airborne = _player != null && _player.Airborne;
            _dist = Mathf.Lerp(_dist, airborne ? flyingDistance : distance, 1f - Mathf.Exp(-3f * Time.deltaTime));

            // 벽이나 기둥에 파묻히지 않게 당겨 준다. 내 몸은 세지 않는다
            float d = _dist;
            Vector3 back = rot * Vector3.back;
            foreach (var hit in Physics.SphereCastAll(_pivot, 0.3f, back, _dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<IDamageable>() != null) continue;   // 용과 적은 가리지 않는다
                if (hit.distance > 0.05f && hit.distance < d) d = hit.distance;
            }

            transform.position = _pivot + back * d + Feedback.ShakeOffset;
            transform.rotation = rot;
            ReadyForInput = CursorLocked;
        }

        /// <summary>락온한 상대의 몸 한가운데.</summary>
        public Vector3 TargetPoint
        {
            get
            {
                if (Target == null) return Vector3.zero;
                var cc = Target.GetComponent<CharacterController>();
                return cc != null ? cc.bounds.center : Target.position + Vector3.up;
            }
        }

        bool StillValid(Transform t)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return false;
            var d = t.GetComponent<IDamageable>();
            if (d == null || !d.Alive) return false;
            return Vector3.Distance(follow.position, t.position) < lockRange * 1.3f;
        }

        /// <summary>화면 가운데에 가장 가까우면서 가까이 있는 적을 고른다.</summary>
        Transform Best()
        {
            Transform best = null;
            float bestScore = float.MaxValue;
            Vector3 view = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            foreach (var col in Physics.OverlapSphere(follow.position, lockRange))
            {
                var d = col.GetComponentInParent<IDamageable>();
                if (d == null || d.Side != Faction.Enemy || !d.Alive) continue;
                var t = ((Component)d).transform;

                Vector3 to = t.position - follow.position;
                to.y = 0f;
                float angle = Vector3.Angle(view, to);
                if (angle > lockCone) continue;

                float score = angle + to.magnitude * 1.2f;
                if (score < bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        /// <summary>
        /// 숨결이 날아갈 방향. 락온했으면 상대의 몸통, 아니면 카메라가 보는 쪽.
        /// 땅에서는 수평으로만 쏘고(안 그러면 땅에 박힌다), 공중에서는 보는 방향 그대로 쏜다.
        /// </summary>
        public Vector3 AimDirection(Vector3 from, bool airborne)
        {
            if (Target != null)
            {
                Vector3 d = TargetPoint - from;
                if (d.sqrMagnitude > 0.001f) return d.normalized;
            }
            if (airborne) return (transform.position + transform.forward * 60f - from).normalized;   // 화면 한가운데가 가리키는 먼 점
            return Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        }
    }
}
