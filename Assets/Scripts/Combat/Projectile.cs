using UnityEngine;

namespace Dragonia.Combat
{
    /// <summary>
    /// 날아가는 것. 숨결 한 알, 보스가 뱉는 구슬, 유도탄까지 전부 이걸로 만든다.
    /// 2D 의 entities/Projectile.js 와 같은 자리다.
    ///
    /// 유도(homing)는 약하게만 건다. 3인칭에서 유도가 세면 구르기로 피할 수가 없어져서,
    /// 예고를 보고 피한다는 이 게임의 규칙이 무너진다.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public static GameObject OrbPrefab;

        float _speed, _life, _damage, _radius, _homing;
        string _element;
        Faction _side;
        Vector3 _dir;
        Transform _chase;
        float _t;

        public static Projectile Spawn(Vector3 pos, Vector3 dir, Faction side, float damage, string element,
                                       float speed = 14f, float life = 4f, float radius = 0.4f,
                                       float homing = 0f, Transform chase = null)
        {
            GameObject go;
            if (OrbPrefab != null) go = Instantiate(OrbPrefab, pos, Quaternion.identity);
            else
            {
                go = Core.Primitives.Box("탄", Core.Primitives.Glow(Element.ColorOf(element)), Vector3.one * radius * 1.6f);
                go.transform.position = pos;
                go.transform.rotation = Random.rotation;
            }

            var p = go.AddComponent<Projectile>();
            p._dir = dir.normalized; p._side = side; p._damage = damage; p._element = element;
            p._speed = speed; p._life = life; p._radius = radius; p._homing = homing; p._chase = chase;
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (_t >= _life) { Destroy(gameObject); return; }

            if (_homing > 0f && _chase != null)
            {
                Vector3 want = (_chase.position + Vector3.up - transform.position).normalized;
                _dir = Vector3.RotateTowards(_dir, want, _homing * dt, 0f).normalized;
            }

            Vector3 step = _dir * _speed * dt;

            // 빠른 탄이 벽이나 몸을 뚫고 지나가지 않게, 지나갈 길을 훑는다
            if (Physics.SphereCast(transform.position, _radius, _dir, out var hit, step.magnitude + 0.05f,
                                   ~0, QueryTriggerInteraction.Collide))
            {
                if (Hit.Apply(hit.collider, _side, _damage, _element, transform.position))
                {
                    Feedback.Burst(hit.point, Element.ColorOf(_element), 5, 5f);
                    Destroy(gameObject);
                    return;
                }
                // 같은 편이 아니라 지형에 맞았으면 거기서 끝난다
                if (hit.collider.GetComponentInParent<IDamageable>() == null) { Destroy(gameObject); return; }
            }

            transform.position += step;
            transform.Rotate(260f * dt, 190f * dt, 0f);      // 굴러가며 날아야 상자가 아니라 불덩이로 보인다
        }
    }
}
