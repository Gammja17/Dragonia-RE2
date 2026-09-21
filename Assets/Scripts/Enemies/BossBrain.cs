using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Dragonia.Combat;

namespace Dragonia.Enemies
{
    /// <summary>
    /// 보스. 2D 드래고니아의 entities/Boss.js 를 옮긴 것이다.
    ///
    /// 어떤 패턴을 어떤 차례로 쓰는지는 코드가 아니라 데이터에 있다
    /// (StreamingAssets/Data/enemies.json 의 BOSSES[id].patterns).
    /// 모르가스는 RING → SUMMON → AIMED → BONE_RAIN 을 돌고, 이그나르는 METEOR_RAIN 으로 연다.
    /// 순서를 바꾸고 싶으면 2D 쪽 data/enemies.js 를 고치고 내보내기를 다시 돌리면 된다.
    ///
    /// 체력이 절반 밑으로 내려가면 발악(rage)에 들어가 탄 수가 늘고 쉬는 틈이 짧아진다.
    ///
    /// 모든 패턴은 예고로 시작한다. 2D 에서 지키던 규칙을 그대로 가져왔다 —
    /// 보고 피할 수 없는 공격은 넣지 않는다.
    /// </summary>
    public class BossBrain : MonoBehaviour, IDamageable
    {
        [Tooltip("데이터의 보스 id. MORGATH · ZALGORA · GLACIA · BASIL · IGNAR")]
        public string bossId = "MORGATH";

        [Tooltip("패턴 하나가 끝나고 다음까지 쉬는 시간. 때릴 틈이 여기서 난다.")]
        public float restTime = 2.9f, rageRestTime = 1.9f;

        [Tooltip("패턴을 쓰기 전에 웅크리는 시간. 짧게 하면 피할 수가 없어진다.")]
        public float tellTime = 0.6f;

        // 2D 는 픽셀, 여기는 미터. 용 키가 대략 160px ≈ 4m 라서 40 으로 나눈다.
        const float PX = 1f / 40f;

        public Faction Side => Faction.Enemy;
        public bool Invulnerable => _hidden;      // 땅속에 있는 동안은 못 맞힌다
        public bool Alive => _hp > 0f;
        public bool Raging => _hp <= _maxHp * 0.5f;

        float _hp, _maxHp;
        string _element = Element.Fire;
        string[] _patterns = { "AIMED" };
        bool _twin;                                // 잘고라: 두 머리가 다른 속성을 뱉는다
        int _patternIndex;
        bool _hidden;
        bool _busy;

        Transform _player;
        Characters.DragonVisual _visual;
        Transform _mouth;

        IEnumerator Start()
        {
            // 데이터는 씬보다 늦게 도착한다 (웹에서는 받아 와야 한다). 다 읽을 때까지 가만히 있는다
            yield return new WaitUntil(() => Data.GameData.Loaded);

            Load();
            var p = GameObject.FindWithTag("Player");
            if (p != null) _player = p.transform;
            _visual = GetComponentInChildren<Characters.DragonVisual>();
            _mouth = _visual != null && _visual.Mouth != null ? _visual.Mouth : transform;
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
            if (_visual != null) _visual.Build(def["species"]?.ToString() ?? bossId);
        }

        IEnumerator Loop()
        {
            yield return new WaitForSeconds(1f);
            while (Alive)
            {
                if (_player == null) { yield return null; continue; }

                string pattern = _patterns[_patternIndex++ % _patterns.Length];

                // 1) 예고 — 웅크리고 플레이어를 본다
                _busy = true;
                FacePlayer();
                if (_visual != null) _visual.Play(Characters.Anim.Breath);
                yield return new WaitForSeconds(tellTime);

                // 2) 발동
                if (Alive) yield return StartCoroutine(Run(pattern));

                // 3) 숨 고르기 — 때릴 틈
                _busy = false;
                if (_visual != null) _visual.Play(Characters.Anim.Idle);
                yield return new WaitForSeconds(Raging ? rageRestTime : restTime);
            }
        }

        IEnumerator Run(string pattern)
        {
            bool rage = Raging;
            Vector3 aim = AimDir();

            switch (pattern)
            {
                case "RING": {
                    // 사방으로 고르게 퍼지는 구슬. 틈 사이로 빠져나가야 한다
                    int n = rage ? 22 : 16;
                    float off = Random.Range(0f, Mathf.PI * 2f);
                    for (int i = 0; i < n; i++)
                        Orb(Quaternion.Euler(0f, (off + i / (float)n * Mathf.PI * 2f) * Mathf.Rad2Deg, 0f) * Vector3.forward);
                    break;
                }

                case "AIMED": {
                    // 플레이어 쪽으로 부채꼴. 잘고라는 두 머리가 불과 번개를 번갈아 뱉는다
                    float[] spread = rage ? new[] { -23f, -11f, 0f, 11f, 23f } : new[] { -13f, 0f, 13f };
                    for (int i = 0; i < spread.Length; i++)
                        Orb(Quaternion.Euler(0f, spread[i], 0f) * aim, 13f,
                            _twin ? (i % 2 == 0 ? Element.Thunder : Element.Fire) : _element);
                    break;
                }

                case "SPIRAL": {
                    // 돌아가며 끊임없이 뱉는다. 제자리에 있으면 반드시 맞는다
                    int shots = rage ? 36 : 24;
                    float angle = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                    for (int i = 0; i < shots && Alive; i++)
                    {
                        Orb(Quaternion.Euler(0f, angle + i * 28f, 0f) * Vector3.forward, 11f);
                        yield return new WaitForSeconds(0.06f);
                    }
                    break;
                }

                case "CHARGE": {
                    int chain = rage ? 3 : 2;
                    for (int i = 0; i < chain && Alive; i++)
                    {
                        Vector3 dir = AimDir();
                        // 돌진 길을 바닥에 먼저 깔아 보여 준다
                        float len = 11f;
                        Hazard.Spawn(transform.position + dir * len * 0.5f, len * 0.35f, 0.7f, 22f, _element, Faction.Enemy);
                        yield return new WaitForSeconds(0.7f);
                        float t = 0f;
                        while (t < 0.75f && Alive)
                        {
                            transform.position += dir * 16f * Time.deltaTime;
                            t += Time.deltaTime;
                            yield return null;
                        }
                        yield return new WaitForSeconds(0.35f);
                    }
                    break;
                }

                case "SUMMON":
                    Summon(rage ? new[] { "GHOST", "GHOST", "BAT", "BAT" } : new[] { "GHOST", "BAT", "BAT" });
                    break;

                case "BONE_RAIN": {
                    // 플레이어 주변에 얼음 기둥이 차례로 떨어진다
                    int n = rage ? 9 : 6;
                    for (int i = 0; i < n; i++)
                        Hazard.Spawn(Near(_player.position, 260f * PX, 200f * PX), 80f * PX,
                                     0.8f + i * 0.12f, 16f, Element.Ice, Faction.Enemy);
                    break;
                }

                case "ICE_FIELD": {
                    int n = rage ? 5 : 3;
                    for (int i = 0; i < n; i++)
                        Hazard.Spawn(Near(_player.position, 220f * PX, 160f * PX), 130f * PX,
                                     0.7f, 8f, Element.Ice, Faction.Enemy, 0f, 6f, 5f);
                    break;
                }

                case "QUAKE": {
                    // 발밑에서 고리가 퍼져 나간다. 가운데가 안전하다 — 붙어 있으면 산다
                    int n = rage ? 4 : 3;
                    for (int i = 0; i < n; i++)
                        Hazard.Spawn(transform.position, (170f + i * 150f) * PX, 0.7f + i * 0.45f,
                                     20f, _element, Faction.Enemy, (70f + i * 150f) * PX);
                    break;
                }

                case "METEOR_RAIN": {
                    int n = rage ? 11 : 7;
                    for (int i = 0; i < n; i++)
                        Hazard.Spawn(Near(_player.position, 320f * PX, 240f * PX), 110f * PX,
                                     0.9f + i * 0.16f, 24f, Element.Fire, Faction.Enemy, 0f, 2f, 8f);
                    break;
                }

                case "FLAME_WALL": {
                    // 플레이어를 가로지르는 불의 벽. 한 군데만 틈이 있다 — 그 틈을 찾아 달려야 한다
                    Vector3 across = Vector3.Cross(Vector3.up, aim).normalized;
                    int gap = Random.Range(2, 9);
                    for (int i = 0; i < 11; i++)
                    {
                        if (i == gap || i == gap + 1) continue;
                        Vector3 at = _player.position + across * ((i - 5) * 95f * PX) + aim * (40f * PX);
                        Hazard.Spawn(at, 60f * PX, 1.0f, 18f, Element.Fire, Faction.Enemy, 0f, 3.5f, 14f);
                    }
                    break;
                }

                case "HOMING": {
                    int n = rage ? 7 : 5;
                    for (int i = 0; i < n; i++)
                        Orb(Quaternion.Euler(0f, (i - 2) * 29f, 0f) * aim, 10f, _element, 1.2f, _player);
                    break;
                }

                case "TWIN_BEAM": {
                    // 천천히 도는 광선 둘. 서서 버티면 반드시 쓸린다
                    float time = rage ? 4f : 3f;
                    float spin = (Random.value < 0.5f ? -1f : 1f) * (rage ? 63f : 49f);
                    float angle = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                    float t = 0f;
                    while (t < time && Alive)
                    {
                        angle += spin * Time.deltaTime;
                        Orb(Quaternion.Euler(0f, angle, 0f) * Vector3.forward, 18f, _element, 0f, null, 2.2f);
                        Orb(Quaternion.Euler(0f, angle + 180f, 0f) * Vector3.forward, 18f, _element, 0f, null, 2.2f);
                        t += Time.deltaTime;
                        yield return new WaitForSeconds(0.05f);
                    }
                    break;
                }

                case "BURROW": {
                    // 땅속으로 숨는다. 못 맞히고, 나올 자리는 플레이어 발밑이다
                    _hidden = true;
                    if (_visual != null) _visual.gameObject.SetActive(false);
                    yield return new WaitForSeconds(rage ? 1.6f : 2.2f);
                    transform.position = Near(_player.position, 120f * PX, 120f * PX);
                    Hazard.Spawn(transform.position, 110f * PX, 0.75f, 20f, _element, Faction.Enemy);
                    if (_visual != null) _visual.gameObject.SetActive(true);
                    _hidden = false;
                    break;
                }

                case "BLIZZARD": {
                    // 바람이 계속 민다. 거슬러 버티는 동안 다른 것들이 날아온다
                    float time = rage ? 5f : 3.5f;
                    Vector3 wind = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                    float t = 0f;
                    while (t < time && Alive)
                    {
                        var cc = _player != null ? _player.GetComponent<CharacterController>() : null;
                        if (cc != null) cc.Move(wind * 3.2f * Time.deltaTime);
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

        // ---- 도구 ----

        void Orb(Vector3 dir, float damage = 12f, string element = null, float homing = 0f,
                 Transform chase = null, float life = 4f)
        {
            Projectile.Spawn(_mouth.position, dir, Faction.Enemy, damage, element ?? _element,
                             14f, life, 0.4f, homing, chase);
        }

        void Summon(string[] ids)
        {
            foreach (string id in ids)
            {
                var prefab = Registry.AssetRegistry.Instance?.Prefab(id);
                Vector3 at = Near(transform.position, 4f, 4f);
                if (prefab != null) Instantiate(prefab, at, Quaternion.identity);
                else
                {
                    var go = new GameObject($"(대역) {id}");
                    go.transform.position = at;
                    go.AddComponent<EnemyBrain>().enemyId = id;
                }
            }
        }

        Vector3 Near(Vector3 origin, float rx, float rz) =>
            origin + new Vector3(Random.Range(-rx, rx), 0f, Random.Range(-rz, rz));

        Vector3 AimDir()
        {
            if (_player == null) return transform.forward;
            Vector3 d = _player.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude > 0.001f ? d.normalized : transform.forward;
        }

        void FacePlayer()
        {
            Vector3 d = AimDir();
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
        }

        public void TakeDamage(float amount, string element, Vector3 from)
        {
            if (!Alive || _hidden) return;
            _hp -= amount;
            if (_hp <= 0f)
            {
                _hp = 0f;
                StopAllCoroutines();
                if (_visual != null) _visual.Play(Characters.Anim.Die);
                enabled = false;
                return;
            }
            // 패턴 도중에는 안 움찔한다. 움찔하게 하면 예고가 끊겨서 피할 수가 없어진다
            if (!_busy && _visual != null) _visual.Play(Characters.Anim.Hit);
        }

        public float HpRatio => _maxHp > 0f ? _hp / _maxHp : 0f;
    }
}
