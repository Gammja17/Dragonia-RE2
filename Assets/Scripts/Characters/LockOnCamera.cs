using System.Collections.Generic;
using UnityEngine;

namespace Dragonia.Characters
{
    /// <summary>
    /// 3인칭 카메라와 락온. 2D 에서 마우스로 조준하던 자리를 대신한다.
    ///
    /// 평소에는 어깨 너머에서 따라다니고, 락온하면 나와 상대를 한 화면에 담는다.
    /// 락온은 젤다식 전투의 중심이다 — 이게 없으면 3인칭에서 상대를 계속 놓친다.
    /// </summary>
    public class LockOnCamera : MonoBehaviour
    {
        [Header("따라다니기")]
        public Transform follow;
        public Vector3 offset = new Vector3(0f, 2.2f, 0f);
        public float distance = 6.5f;
        public float height = 2.2f;
        public float followSharpness = 10f;

        [Header("둘러보기 (락온 안 했을 때)")]
        public float lookSensitivity = 2.4f;
        public float minPitch = -25f, maxPitch = 60f;

        [Header("락온")]
        public float lockRange = 22f;
        [Tooltip("화면 한가운데에서 이 각도 안에 있어야 걸린다.")]
        public float lockCone = 65f;
        public LayerMask targetMask = ~0;
        public KeyCode lockKey = KeyCode.Q;

        public Transform Target { get; private set; }

        float _yaw, _pitch = 12f;

        void Start()
        {
            if (follow == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) follow = player.transform;
            }
            _yaw = transform.eulerAngles.y;
        }

        void Update()
        {
            if (Input.GetKeyDown(lockKey)) Toggle();
            // 상대가 죽거나 너무 멀어지면 저절로 풀린다
            if (Target != null && (!Target.gameObject.activeInHierarchy ||
                Vector3.Distance(follow.position, Target.position) > lockRange * 1.4f)) Target = null;

            if (Target == null)
            {
                _yaw += Input.GetAxis("Mouse X") * lookSensitivity;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * lookSensitivity, minPitch, maxPitch);
            }
        }

        void LateUpdate()
        {
            if (follow == null) return;
            Vector3 pivot = follow.position + offset;

            Quaternion rot;
            if (Target != null)
            {
                // 나와 상대의 가운데를 보도록 카메라를 상대 반대편에 둔다
                Vector3 toTarget = Target.position - follow.position;
                toTarget.y = 0f;
                rot = Quaternion.LookRotation(toTarget.normalized) * Quaternion.Euler(14f, 0f, 0f);
            }
            else
            {
                rot = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            Vector3 want = pivot - rot * Vector3.forward * distance + Vector3.up * (height - offset.y);

            // 벽에 파묻히지 않게 당겨 준다
            if (Physics.Linecast(pivot, want, out var hit, ~0, QueryTriggerInteraction.Ignore))
                want = hit.point + hit.normal * 0.3f;

            transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(pivot - transform.position), 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
        }

        void Toggle()
        {
            if (Target != null) { Target = null; return; }
            Target = Best();
        }

        /// <summary>화면 가운데에 가장 가까우면서 가까이 있는 상대를 고른다.</summary>
        Transform Best()
        {
            var hits = Physics.OverlapSphere(follow.position, lockRange, targetMask);
            Transform best = null;
            float bestScore = float.MaxValue;
            Vector3 view = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            foreach (var h in hits)
            {
                var brain = h.GetComponentInParent<Enemies.EnemyBrain>();
                if (brain == null || !brain.Alive) continue;

                Vector3 to = brain.transform.position - follow.position;
                to.y = 0f;
                float angle = Vector3.Angle(view, to);
                if (angle > lockCone) continue;

                float score = angle + to.magnitude * 1.5f;     // 가운데에 가깝고 가까울수록 좋다
                if (score < bestScore) { bestScore = score; best = brain.transform; }
            }
            return best;
        }

        /// <summary>숨결이 날아갈 방향. 락온했으면 그쪽, 아니면 카메라 앞.</summary>
        public Vector3 AimDirection(Vector3 from)
        {
            if (Target != null)
            {
                Vector3 d = (Target.position + Vector3.up * 1f) - from;
                if (d.sqrMagnitude > 0.001f) return d.normalized;
            }
            return Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        }
    }
}
