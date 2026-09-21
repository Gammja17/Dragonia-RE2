using System.Collections.Generic;
using UnityEngine;

namespace Dragonia.Combat
{
    /// <summary>
    /// 바닥에 예고를 깔았다가 터지는 장판. 2D 의 entities/Hazard.js 를 옮긴 것이다.
    ///
    /// 보스 패턴 열넷 중 여섯(BONE_RAIN · ICE_FIELD · QUAKE · METEOR_RAIN · FLAME_WALL)이
    /// 전부 이 하나로 만들어진다. 반지름과 터지는 시각만 다를 뿐이다.
    ///
    /// 예고가 이 구조의 전부다. delay 동안 바닥이 차오르고, 다 차면 터진다.
    /// 보고 피할 수 있어야 하므로 delay 를 0.7초 밑으로 내리면 안 된다.
    ///
    ///   inner 가 0 보다 크면 도넛 모양 — 가운데가 안전하다 (QUAKE 의 퍼져 나가는 고리)
    ///   linger 가 0 보다 크면 터진 뒤에도 남아 계속 깎는다 (불바다 · 얼음벌판)
    /// </summary>
    public class Hazard : MonoBehaviour
    {
        public static GameObject MarkerPrefab;      // 바닥 표식. 없으면 코드로 원을 만든다

        float _radius, _inner, _delay, _linger, _damage, _dps;
        string _element;
        Faction _side;
        float _t;
        bool _burst;
        Transform _marker;
        readonly HashSet<IDamageable> _ticked = new HashSet<IDamageable>();

        public static Hazard Spawn(Vector3 pos, float radius, float delay, float damage, string element,
                                   Faction side, float inner = 0f, float linger = 0f, float dps = 0f)
        {
            var go = new GameObject("Hazard");
            go.transform.position = pos;
            var h = go.AddComponent<Hazard>();
            h._radius = radius; h._inner = inner; h._delay = Mathf.Max(0.7f, delay);
            h._damage = damage; h._element = element; h._side = side;
            h._linger = linger; h._dps = dps;
            h.MakeMarker();
            return h;
        }

        void MakeMarker()
        {
            if (MarkerPrefab != null)
            {
                _marker = Instantiate(MarkerPrefab, transform).transform;
            }
            else
            {
                // 표식 프리팹이 아직 없어도 보여야 한다. 납작한 상자로 대신한다
                var disc = Core.Primitives.Box("예고", Element.ColorOf(_element), new Vector3(_radius * 2f, 0.04f, _radius * 2f));
                disc.transform.SetParent(transform, false);
                disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                Core.Primitives.MakeTransparent(disc.GetComponent<Renderer>());
                _marker = disc.transform;
            }
            Tint(0.25f);
        }

        void Tint(float alpha)
        {
            if (_marker == null) return;
            var r = _marker.GetComponentInChildren<Renderer>();
            if (r == null) return;
            var c = Element.ColorOf(_element);
            c.a = alpha;
            r.material.color = c;
        }

        void Update()
        {
            _t += Time.deltaTime;

            if (!_burst)
            {
                // 차오른다. 끝에 가까울수록 진해져서 "곧 터진다"가 몸으로 읽힌다
                float k = Mathf.Clamp01(_t / _delay);
                Tint(0.2f + k * 0.55f);
                if (_marker != null)
                {
                    float pulse = 1f + Mathf.Sin(_t * 18f) * 0.02f * k;
                    _marker.localScale = new Vector3(_radius * 2f * pulse, 0.04f, _radius * 2f * pulse);
                }
                if (_t >= _delay) Burst();
                return;
            }

            // 터진 뒤 남아 있는 동안은 계속 깎는다
            if (_dps > 0f) DamageInside(_dps * Time.deltaTime, false);
            if (_t >= _delay + _linger) Destroy(gameObject);
        }

        void Burst()
        {
            _burst = true;
            Tint(0.5f);
            DamageInside(_damage, true);
            if (_linger <= 0f) Destroy(gameObject, 0.12f);
        }

        void DamageInside(float amount, bool once)
        {
            foreach (var col in Physics.OverlapSphere(transform.position, _radius))
            {
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || target.Side == _side || target.Invulnerable) continue;

                // 도넛이면 가운데는 안전하다
                if (_inner > 0f)
                {
                    float d = Vector3.Distance(transform.position, col.transform.position);
                    if (d < _inner) continue;
                }
                if (once)
                {
                    if (_ticked.Contains(target)) continue;
                    _ticked.Add(target);
                }
                target.TakeDamage(amount, _element, transform.position);
            }
        }
    }
}
