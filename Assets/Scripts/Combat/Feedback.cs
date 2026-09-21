using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dragonia.Core;

namespace Dragonia.Combat
{
    /// <summary>
    /// 타격감. 2D 의 render/feedback.js 와 같은 자리다.
    ///
    /// 맞고 때릴 때 화면이 아무 반응을 하지 않으면 숫자만 줄어드는 표가 된다.
    /// 첫 시험판이 바로 그랬다 — 물어도 뿜어도 아무 일도 안 일어나서 "공격이 안 된다"로 읽혔다.
    ///
    ///   HitStop  맞은 순간 세상이 아주 잠깐 멈춘다 (때린 맛의 팔 할이 이것이다)
    ///   Shake    화면이 흔들린다
    ///   Burst    파편이 튄다
    ///   Ring     바닥에 충격파가 퍼진다 (내려찍기)
    ///   Spike    바닥에서 기둥이 솟는다 (장판이 터질 때)
    /// </summary>
    public class Feedback : MonoBehaviour
    {
        static Feedback _instance;
        float _shake;
        Coroutine _stop;

        public static Vector3 ShakeOffset { get; private set; }

        static Feedback I
        {
            get
            {
                if (_instance == null) _instance = new GameObject("[타격감]").AddComponent<Feedback>();
                return _instance;
            }
        }

        // 멈춘 채로 씬이 바뀌면 시간이 영영 느린 채로 남는다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook() => SceneManager.sceneLoaded += (_, __) => Time.timeScale = 1f;

        public static void Shake(float amount) => I._shake = Mathf.Max(I._shake, amount);

        public static void HitStop(float seconds)
        {
            if (Time.timeScale == 0f) return;                 // 화면이 떠서 멈춰 있는 중이면 건드리지 않는다
            if (I._stop != null) I.StopCoroutine(I._stop);
            I._stop = I.StartCoroutine(I.Stop(seconds));
        }

        IEnumerator Stop(float seconds)
        {
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
            _stop = null;
        }

        void Update()
        {
            _shake = Mathf.MoveTowards(_shake, 0f, Time.unscaledDeltaTime * 3.5f);
            ShakeOffset = Random.insideUnitSphere * _shake * 0.3f;
        }

        public static void Burst(Vector3 pos, Color color, int count = 9, float power = 7f)
        {
            var mat = Primitives.Glow(color);
            for (int i = 0; i < count; i++)
            {
                var go = Primitives.Box("파편", mat, Vector3.one * Random.Range(0.08f, 0.2f));
                go.transform.position = pos;
                go.transform.rotation = Random.rotation;
                var s = go.AddComponent<Shard>();
                s.velocity = (Random.onUnitSphere + Vector3.up * 0.6f).normalized * Random.Range(power * 0.4f, power);
            }
        }

        public static void Ring(Vector3 pos, Color color, float radius)
        {
            var go = Primitives.Box("충격파", Primitives.Marker(new Color(color.r, color.g, color.b, 0.6f)), new Vector3(0.5f, 0.05f, 0.5f));
            go.transform.position = new Vector3(pos.x, pos.y + 0.05f, pos.z);
            var w = go.AddComponent<Wave>();
            w.radius = radius;
        }

        public static void Spike(Vector3 pos, Color color, float radius)
        {
            var go = Primitives.Box("기둥", Primitives.Glow(color, 0.9f), new Vector3(radius * 0.55f, 0.1f, radius * 0.55f));
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);
            go.AddComponent<Pillar>().height = Mathf.Max(2.2f, radius * 1.4f);
        }

        class Shard : MonoBehaviour
        {
            public Vector3 velocity;
            float _t;
            void Update()
            {
                float dt = Time.deltaTime;
                _t += dt;
                velocity += Vector3.down * 18f * dt;
                transform.position += velocity * dt;
                transform.Rotate(velocity * 40f * dt);
                transform.localScale *= 1f - dt * 2.2f;
                if (_t > 0.6f) Destroy(gameObject);
            }
        }

        class Wave : MonoBehaviour
        {
            public float radius;
            float _t;
            Renderer _r;
            void Start() => _r = GetComponent<Renderer>();
            void Update()
            {
                _t += Time.deltaTime;
                float k = _t / 0.4f;
                float d = Mathf.Lerp(0.5f, radius * 2f, 1f - (1f - k) * (1f - k));
                transform.localScale = new Vector3(d, 0.05f, d);
                var c = _r.material.color; c.a = 0.6f * (1f - k); _r.material.color = c;
                if (k >= 1f) Destroy(gameObject);
            }
        }

        class Pillar : MonoBehaviour
        {
            public float height;
            float _t;
            void Update()
            {
                _t += Time.deltaTime;
                float k = _t / 0.55f;
                // 빠르게 솟았다가 천천히 가라앉는다
                float h = k < 0.2f ? Mathf.Lerp(0.1f, height, k / 0.2f) : Mathf.Lerp(height, 0f, (k - 0.2f) / 0.8f);
                var s = transform.localScale; s.y = Mathf.Max(0.01f, h); transform.localScale = s;
                var p = transform.position; p.y = h * 0.5f; transform.position = p;
                if (k >= 1f) Destroy(gameObject);
            }
        }
    }
}
