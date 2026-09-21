using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dragonia.Combat;

namespace Dragonia.Enemies
{
    /// <summary>
    /// 보스. 2D 드래고니아의 entities/Boss.js 를 옮긴 것이다.
    ///
    /// 어떤 패턴을 어떤 차례로 쓰는지는 코드가 아니라 데이터에 있다
    /// (StreamingAssets/Data/enemies.json 의 BOSSES[id].patterns).
    /// 순서를 바꾸고 싶으면 2D 쪽 data/enemies.js 를 고치고 내보내기를 다시 돌리면 된다.
    ///
    /// 한 박자는 늘 같다: 예고(몸을 일으키고 날개를 편다) → 발동 → 숨 고르기(때릴 틈).
    /// 체력이 절반 밑으로 내려가면 발악에 들어가 탄이 늘고 쉬는 틈이 짧아진다.
    ///
    /// 3D 로 오면서 고친 것:
    ///   - 탄이 머리 높이로 날아가 플레이어 위를 지나갔다. 평면 패턴은 가슴 높이에서 쏘고,
    ///     조준 패턴은 플레이어의 몸통을 3차원으로 겨눈다 (날고 있어도 맞는다)
    ///   - 돌진이 CharacterController 로 움직인다. 기둥이나 벽에 박으면 기절한다 —
    ///     일부러 기둥 뒤로 유인하면 크게 때릴 틈이 난다
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class BossBrain : MonoBehaviour, IDamageable
    {
        [Tooltip("데이터의 보스 id. MORGATH · ZALGORA · GLACIA · BASIL · IGNAR")]
        public string bossId = "MORGATH";

        [Tooltip("패턴 하나가 끝나고 다음까지 쉬는 시간. 때릴 틈이 여기서 난다.")]
        public float restTime = 2.6f, rageRestTime = 1.7f;

        [Tooltip("패턴을 쓰기 전에 몸을 일으키는 시간. 짧게 하면 피할 수가 없어진다.")]
        public float tellTime = 0.7f;

        public float chargeSpeed = 17f;
        public float chargeDamage = 22f;
        public float stunTime = 2.4f;

        // 2D 는 픽셀, 여기는 미터. 용 키가 대략 160px ≈ 4m 라서 40 으로 나눈다.
        const float PX = 1f / 40f;

        public Faction Side => Faction.Enemy;
        public bool Invulnerable => _hidden;
        public bool Alive => _hp > 0f;
        public bool Raging => _hp <= _maxHp * 0.5f;
        public float HpRatio => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;
        public string DisplayName { get; private set; } = "";

        float _hp = 1f, _maxHp = 1f;
        string _element = Element.Ice;
        string[] _patterns = { "AIMED" };
        bool _twin, _hidden, _busy, _charging, _chargeHit, _stunned;
        int _patternIndex;
        Vector3 _chargeDir;

        Transform _player;
        CharacterController _cc;
        Characters.DragonVisual _visual;

        IEnumerator Start()
        {
            _cc = GetComponent<CharacterController>();
            _visual = GetComponentInChildren<Characters.DragonVisual>();
            DisplayName = bossId;

            // 데이터는 씬보다 늦게 도착한다 (웹에서는 받아 와야 한다). 다 읽을 때까지 가만히 있는다
            yield return new WaitUntil(() => Data.GameData.Loaded);

            Load();
            var p = GameObject.FindWithTag("Player");
            if (p != null) _player = p.transform;
            StartCoroutine(Loop());
        }

        void Load()
        {
            JToken def = Data.GameData.Bosses?[bossId];
            if (def == null)
            {
                Debug.LogWarning($"보스 데이터가 없다: {bossId}");
                _maxHp = _hp = 1000f;
                return;
            }
            _maxHp = _hp = def["hp"]?.Value<float>() ?? 1000f;
            _element = def["element"]?.ToString() ?? Element.Fire;
            _twin = def["twin"]?.Value<bool>() ?? false;

            var list = def["patterns"] as JArray;
            if (list != null && list.Count > 0)
            {
                _patterns = new string[list.Count];
                for (int i = 0; i < list.Count; i++) _patterns[i] = list[i].ToString();
            }

            // 색도 데이터에서 온다. 눈빛은 그 보스의 속성 색이다
            if (_visual != null)
            {
                Color body = ParseColor(def["colors"]?["body"], new Color(0.85f, 0.85f, 0.9f));
                Color wing = ParseColor(def["colors"]?["wing"], body);
                // 온통 한 색이면 덩어리로 보인다. 날개는 속성 색 쪽으로 물들인다
                wing = Color.Lerp(wing, Element.ColorOf(_element), 0.55f) * 0.8f; wing.a = 1f;
                body = Color.Lerp(body, new Color(0.62f, 0.66f, 0.74f), 0.35f);
                _visual.Build(def["species"]?.ToString() ?? bossId, body, wing, Element.ColorOf(_element));
            }
        }

        static Color ParseColor(JToken t, Color fallback) =>
            t != null && ColorUtility.TryParseHtmlString(t.ToString(), out var c) ? c : fallback;

        void Update()
        {
            if (!Alive || _player == null) return;
            // 쉬는 동안에는 천천히 플레이어 쪽으로 돌아본다. 패턴 중에는 방향을 잠근다
            if (!_busy && !_stunned) Turn(AimFlat(), Time.deltaTime * 2.2f);
            if (!_charging) _cc.Move(Vector3.down * 12f * Time.deltaTime);
        }

        IEnumerator Loop()
        {
            yield return new WaitForSeconds(1.5f);
            while (Alive)
            {
                if (_player == null) { yield return null; continue; }
                string pattern = _patterns[_patternIndex++ % _patterns.Length];

                // 1) 예고 — 몸을 일으키고 날개를 편다. 이걸 보고 피할 준비를 한다
                _busy = true;
                transform.rotation = Quaternion.LookRotation(AimFlat());
                Play(Characters.Anim.Tell);
                yield return new WaitForSeconds(tellTime);

                // 2) 발동
                if (Alive) yield return StartCoroutine(Run(pattern));

                // 3) 숨 고르기 — 때릴 틈
                _busy = false;
                if (Alive && !_stunned) Play(Characters.Anim.Idle);
                yield return new WaitForSeconds(Raging ? rageRestTime : restTime);
                while (_stunned) yield return null;
            }
        }

        IEnumerator Run(string pattern)
        {
            bool rage = Raging;
            Vector3 flat = AimFlat();

            switch (pattern)
            {
                case "RING":
                {
                    // 사방으로 고르게 퍼지는 구슬. 틈 사이로 빠져나가거나, 뛰어넘거나
                    Play(Characters.Anim.Breath);
                    yield return new WaitForSeconds(0.25f);
                    int n = rage ? 22 : 16;
                    float off = Random.Range(0f, 360f);
                    for (int i = 0; i < n; i++) OrbFlat(Quaternion.Euler(0f, off + i * 360f / n, 0f) * Vector3.forward);
                    Feedback.Shake(0.3f);
                    break;
                }

                case "AIMED":
                {
                    // 플레이어 쪽으로 부채꼴. 잘고라는 두 머리가 불과 번개를 번갈아 뱉는다
                    Play(Characters.Anim.Breath);
                    yield return new WaitForSeconds(0.25f);
                    float[] spread = rage ? new[] { -23f, -11f, 0f, 11f, 23f } : new[] { -13f, 0f, 13f };
                    Vector3 aim = Aim3D();
                    for (int i = 0; i < spread.Length; i++)
                        OrbAimed(Quaternion.Euler(0f, spread[i], 0f) * aim, 13f,
                                 _twin ? (i % 2 == 0 ? Element.Thunder : Element.Fire) : _element);
                    break;
                }

                case "SPIRAL":
                {
                    Play(Characters.Anim.Breath);
                    int shots = rage ? 36 : 24;
                    float angle = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                    for (int i = 0; i < shots && Alive; i++)
                    {
                        OrbFlat(Quaternion.Euler(0f, angle + i * 28f, 0f) * Vector3.forward, 11f);
                        if (i % 12 == 11) Play(Characters.Anim.Breath);
                        yield return new WaitForSeconds(0.06f);
                    }
                    break;
                }

                case "CHARGE":
                {
                    int chain = rage ? 3 : 2;
                    for (int i = 0; i < chain && Alive && !_stunned; i++)
                    {
                        _chargeDir = AimFlat();
                        transform.rotation = Quaternion.LookRotation(_chargeDir);
                        // 돌진할 길을 바닥에 먼저 깔아 보여 준다
                        for (int s = 1; s <= 4; s++)
                            Hazard.Spawn(transform.position + _chargeDir * s * 3.2f, 1.8f, 0.7f, 0f, _element, Faction.Enemy);
                        Play(Characters.Anim.Tell);
                        yield return new WaitForSeconds(0.7f);

                        _charging = true; _chargeHit = false;
                        Play(Characters.Anim.Dive);
                        float t = 0f;
                        while (t < 0.8f && Alive && _charging)
                        {
                            _cc.Move((_chargeDir * chargeSpeed + Vector3.down * 8f) * Time.deltaTime);
                            t += Time.deltaTime;
                            yield return null;
                        }
                        _charging = false;
                        if (!_stunned) Play(Characters.Anim.Idle);
                        yield return new WaitForSeconds(0.4f);
                    }
                    break;
                }

                case "SUMMON":
                    Play(Characters.Anim.Breath);
                    Summon(rage ? 4 : 3);
                    break;

                case "BONE_RAIN":
                {
                    Play(Characters.Anim.Breath);
                    int n = rage ? 9 : 6;
                    for (int i = 0; i < n; i++)
                        Hazard.Spawn(Ground(Near(_player.position, 260f * PX, 200f * PX)), 80f * PX,
                                     0.8f + i * 0.12f, 16f, Element.Ice, Faction.Enemy);
                    break;
                }

                case "ICE_FIELD":
                {
                    Play(Characters.Anim.Breath);
                    int n = rage ? 5 : 3;
                    for (int i = 0; i < n; i++)
                        Hazard.Spawn(Ground(Near(_player.position, 220f * PX, 160f * PX)), 130f * PX,
                                     0.7f, 8f, Element.Ice, Faction.Enemy, 0f, 6f, 5f);
                    break;
                }

                case "QUAKE":
                {
                    Play(Characters.Anim.Breath);
                    int n = rage ? 4 : 3;
                    for (int i = 0; i < n; i++)
                        Hazard.Spawn(Ground(transform.position), (170f + i * 150f) * PX, 0.7f + i * 0.45f,
                                     20f, _element, Faction.Enemy, (70f + i * 150f) * PX);
                    break;
                }

                case "METEOR_RAIN":
                {
                    Play(Characters.Anim.Breath);
                    int n = rage ? 11 : 7;
                    for (int i = 0; i < n; i++)
                        Hazard.Spawn(Ground(Near(_player.position, 320f * PX, 240f * PX)), 110f * PX,
                                     0.9f + i * 0.16f, 24f, Element.Fire, Faction.Enemy, 0f, 2f, 8f);
                    break;
                }

                case "FLAME_WALL":
                {
                    // 플레이어를 가로지르는 불의 벽. 한 군데만 틈이 있다 — 틈을 찾아 달리거나, 날아서 넘는다
                    Play(Characters.Anim.Breath);
                    Vector3 across = Vector3.Cross(Vector3.up, flat).normalized;
                    int gap = Random.Range(2, 9);
                    for (int i = 0; i < 11; i++)
                    {
                        if (i == gap || i == gap + 1) continue;
                        Vector3 at = _player.position + across * ((i - 5) * 95f * PX) + flat * (40f * PX);
                        Hazard.Spawn(Ground(at), 60f * PX, 1.0f, 18f, Element.Fire, Faction.Enemy, 0f, 3.5f, 14f);
                    }
                    break;
                }

                case "HOMING":
                {
                    Play(Characters.Anim.Breath);
                    yield return new WaitForSeconds(0.25f);
                    int n = rage ? 7 : 5;
                    Vector3 aim = Aim3D();
                    for (int i = 0; i < n; i++)
                        OrbAimed(Quaternion.Euler(0f, (i - n / 2) * 24f, 0f) * aim, 10f, _element, 1.2f, 4.5f);
                    break;
                }

                case "TWIN_BEAM":
                {
                    Play(Characters.Anim.Breath);
                    float time = rage ? 4f : 3f;
                    float spin = (Random.value < 0.5f ? -1f : 1f) * (rage ? 63f : 49f);
                    float angle = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg + 50f;
                    float t = 0f, next = 0f;
                    while (t < time && Alive)
                    {
                        angle += spin * Time.deltaTime;
                        if (t >= next)
                        {
                            next += 0.05f;
                            OrbFlat(Quaternion.Euler(0f, angle, 0f) * Vector3.forward, 14f, 1.6f);
                            OrbFlat(Quaternion.Euler(0f, angle + 180f, 0f) * Vector3.forward, 14f, 1.6f);
                        }
                        t += Time.deltaTime;
                        yield return null;
                    }
                    break;
                }

                case "BURROW":
                {
                    _hidden = true;
                    Feedback.Burst(transform.position, new Color(0.8f, 0.65f, 0.3f), 14, 8f);
                    if (_visual != null) _visual.gameObject.SetActive(false);
                    yield return new WaitForSeconds(rage ? 1.6f : 2.2f);
                    Vector3 at = Ground(Near(_player.position, 120f * PX, 120f * PX));
                    _cc.enabled = false; transform.position = at; _cc.enabled = true;
                    Hazard.Spawn(at, 110f * PX, 0.75f, 20f, _element, Faction.Enemy);
                    yield return new WaitForSeconds(0.75f);
                    if (_visual != null) _visual.gameObject.SetActive(true);
                    _hidden = false;
                    break;
                }

                case "BLIZZARD":
                {
                    Play(Characters.Anim.Tell);
                    float time = rage ? 5f : 3.5f;
                    Vector3 wind = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                    var pcc = _player.GetComponent<CharacterController>();
                    float t = 0f;
                    while (t < time && Alive)
                    {
                        if (pcc != null && pcc.enabled) pcc.Move(wind * 3.2f * Time.deltaTime);
                        t += Time.deltaTime;
                        yield return null;
                    }
                    break;
                }

                default:
                    Debug.LogWarning($"아직 안 옮긴 보스 패턴: {pattern}");
                    break;
            }
        }

        // 돌진 중에 무엇과 부딪쳤는가. 플레이어면 들이받고 지나가고, 벽·기둥이면 박혀서 기절한다
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!_charging) return;
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                if (target.Side == Faction.Player && !_chargeHit && !target.Invulnerable)
                {
                    _chargeHit = true;
                    target.TakeDamage(chargeDamage, _element, transform.position);
                }
                return;
            }
            if (Mathf.Abs(hit.normal.y) > 0.5f) return;          // 바닥은 벽이 아니다
            StartCoroutine(Stunned());
        }

        IEnumerator Stunned()
        {
            _charging = false;
            _stunned = true;
            Play(Characters.Anim.Hit);
            Feedback.Shake(1f);
            Feedback.Burst(transform.position + Vector3.up * 1.5f + _chargeDir * 1.5f, new Color(0.8f, 0.8f, 0.75f), 16, 9f);
            yield return new WaitForSeconds(stunTime);
            _stunned = false;
            if (Alive) Play(Characters.Anim.Idle);
        }

        // ---- 도구 ----

        void Play(string anim) { if (_visual != null) _visual.Play(anim); }

        Vector3 MouthPos => _visual != null && _visual.Mouth != null ? _visual.Mouth.position : transform.position + Vector3.up * 2f;

        /// <summary>평면으로 퍼지는 탄. 입 높이에서 쏘면 플레이어 머리 위로 지나가므로 가슴 높이에서 쏜다</summary>
        void OrbFlat(Vector3 dir, float damage = 12f, float life = 4f)
        {
            Vector3 from = transform.position + Vector3.up * 1.1f + dir * 1.8f;
            Projectile.Spawn(from, dir, Faction.Enemy, damage, _element, 13f, life, 0.45f);
        }

        /// <summary>겨눠서 쏘는 탄. 입에서 나가 플레이어의 몸통을 3차원으로 향한다</summary>
        void OrbAimed(Vector3 dir, float damage, string element, float homing = 0f, float life = 4f)
        {
            Projectile.Spawn(MouthPos, dir, Faction.Enemy, damage, element ?? _element, 15f, life, 0.45f, homing, homing > 0f ? _player : null);
        }

        void Summon(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 at = Ground(Near(transform.position, 5f, 5f));
                Feedback.Burst(at + Vector3.up, Element.ColorOf(_element), 8, 5f);
                Minion.Spawn(at, "GHOST", new Color(0.72f, 0.78f, 0.95f), new Color(0.45f, 0.55f, 0.9f), Element.ColorOf(_element));
            }
        }

        Vector3 Near(Vector3 origin, float rx, float rz) =>
            origin + new Vector3(Random.Range(-rx, rx), 0f, Random.Range(-rz, rz));

        static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 80f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<IDamageable>() == null) p.y = hit.point.y;
            else p.y = 0f;
            return p;
        }

        Vector3 AimFlat()
        {
            if (_player == null) return transform.forward;
            Vector3 d = _player.position - transform.position; d.y = 0f;
            return d.sqrMagnitude > 0.001f ? d.normalized : transform.forward;
        }

        Vector3 Aim3D()
        {
            if (_player == null) return transform.forward;
            Vector3 d = _player.position + Vector3.up * 1.1f - MouthPos;
            return d.sqrMagnitude > 0.001f ? d.normalized : transform.forward;
        }

        void Turn(Vector3 dir, float k)
        {
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Mathf.Clamp01(k));
        }

        public void TakeDamage(float amount, string element, Vector3 from)
        {
            if (!Alive || _hidden) return;
            if (_stunned) amount *= 1.5f;                  // 기절해 있을 때가 크게 때릴 기회다
            _hp -= amount;
            if (_visual != null) _visual.Flash();

            if (_hp <= 0f)
            {
                _hp = 0f;
                StopAllCoroutines();
                _charging = false;
                Play(Characters.Anim.Die);
                Feedback.Shake(1.4f);
                Feedback.HitStop(0.25f);
                Feedback.Burst(transform.position + Vector3.up * 2f, Element.ColorOf(_element), 30, 12f);
                StartCoroutine(Restart());
                return;
            }
            // 패턴 도중에는 안 움찔한다. 움찔하게 하면 예고가 끊겨서 피할 수가 없어진다
            if (!_busy && !_stunned) Play(Characters.Anim.Hit);
        }

        IEnumerator Restart()
        {
            yield return new WaitForSecondsRealtime(5f);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
