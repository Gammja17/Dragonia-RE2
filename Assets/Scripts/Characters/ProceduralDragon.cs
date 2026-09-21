using System.Collections.Generic;
using UnityEngine;
using Dragonia.Core;

namespace Dragonia.Characters
{
    /// <summary>
    /// 상자를 이어 붙여 만든 로우폴리 용. 제대로 된 모델이 생기기 전까지의 대역이다.
    ///
    /// 그래도 "용"으로 읽혀야 조작감을 판단할 수 있다 — 회색 캡슐로는 어디가 앞인지,
    /// 지금 물고 있는지 구르고 있는지 알 수가 없다. 그래서 몸통·목·머리·턱·뿔·꼬리·다리 넷·
    /// 접히는 날개 한 쌍을 갖추고, 움직임은 전부 코드로 만든다 (클립이 없으니까).
    ///
    /// 애니메이션 이름은 Anim 의 이름표를 그대로 쓴다. 나중에 Animator 달린 진짜 모델로
    /// 갈아 끼워도 게임 코드는 똑같이 Play(Anim.Bite) 를 부른다 — DragonVisual 이 받아서
    /// 모델이 있으면 Animator 로, 없으면 여기로 넘긴다.
    ///
    /// 앞은 +Z, 발은 y=0.
    /// </summary>
    public class ProceduralDragon : MonoBehaviour
    {
        public Transform Mouth { get; private set; }
        public Transform Head { get; private set; }

        struct Leg { public Transform hip, knee; public float offset, restHip, restKnee; }

        Transform _center, _rig, _headPivot, _jaw;
        readonly List<Transform> _neck = new List<Transform>();
        readonly List<float> _neckRest = new List<float>();
        readonly List<Transform> _tail = new List<Transform>();
        readonly List<float> _tailRest = new List<float>();
        readonly List<Leg> _legs = new List<Leg>();
        readonly Transform[] _shoulder = new Transform[2], _elbow = new Transform[2];

        Material _mBody, _mBelly, _mWing, _mHorn, _mGlow;
        Color _cBody, _cBelly, _cWing, _cHorn, _cGlow;

        string _state = Anim.Idle;
        float _stateTime, _t, _phase, _speed, _fly, _flapKick, _flash;
        bool _flying, _built;

        const float HeadRest = 30f;
        const float CenterY = 1.2f;

        // ---------------------------------------------------------------- 짓기

        public void Build(Color body, Color wing, Color glow)
        {
            if (_built) return;
            _built = true;

            _cBody = body;
            _cBelly = Color.Lerp(body, new Color(1f, 0.93f, 0.78f), 0.55f);
            _cWing = wing;
            _cHorn = new Color(0.93f, 0.89f, 0.78f);
            _cGlow = glow;
            _mBody = Primitives.Solid(_cBody);
            _mBelly = Primitives.Solid(_cBelly);
            _mWing = Primitives.Solid(_cWing);
            _mHorn = Primitives.Solid(_cHorn);
            _mGlow = Primitives.Glow(_cGlow);

            // 구르기·쓰러짐은 발밑이 아니라 몸 한가운데를 축으로 돌아야 한다
            _center = Pivot("Center", transform, new Vector3(0f, CenterY, 0f));
            _rig = Pivot("Rig", _center, new Vector3(0f, -CenterY, 0f));

            BuildBody();
            BuildNeckAndHead();
            BuildTail();
            BuildLegs();
            BuildWings();
            Tick(0f);
        }

        void BuildBody()
        {
            Part("Chest", _rig, _mBody, new Vector3(0.95f, 0.9f, 1.05f), new Vector3(0f, 1.25f, 0.42f));
            Part("Hips", _rig, _mBody, new Vector3(0.8f, 0.76f, 1.0f), new Vector3(0f, 1.18f, -0.5f));
            Part("Belly", _rig, _mBelly, new Vector3(0.66f, 0.3f, 1.6f), new Vector3(0f, 0.86f, -0.05f));
            // 등줄기의 가시. 모로 세워 마름모로 보이게 한다
            for (int i = 0; i < 5; i++)
            {
                var s = Part("Spike", _rig, _mHorn, new Vector3(0.09f, 0.24f, 0.24f),
                             new Vector3(0f, 1.74f - i * 0.045f, 0.62f - i * 0.4f));
                s.localRotation = Quaternion.Euler(45f, 0f, 0f);
            }
        }

        void BuildNeckAndHead()
        {
            // 목 세 마디. 첫 마디가 위로 치켜들고 뒤 마디들이 앞으로 굽어 S 자가 된다
            float[] rest = { -52f, 14f, 16f };
            float[] size = { 0.44f, 0.40f, 0.36f };
            float[] len = { 0.62f, 0.56f, 0.50f };
            Transform parent = _rig;
            Vector3 at = new Vector3(0f, 1.52f, 0.88f);
            for (int i = 0; i < 3; i++)
            {
                var p = Pivot("Neck" + i, parent, at);
                Part("NeckSeg", p, _mBody, new Vector3(size[i], size[i], len[i]), new Vector3(0f, 0f, len[i] * 0.5f));
                Part("NeckBelly", p, _mBelly, new Vector3(size[i] * 0.7f, 0.08f, len[i] * 0.9f), new Vector3(0f, -size[i] * 0.5f, len[i] * 0.5f));
                _neck.Add(p); _neckRest.Add(rest[i]);
                parent = p; at = new Vector3(0f, 0f, len[i] - 0.05f);
            }

            _headPivot = Pivot("Head", parent, at);
            Head = _headPivot;
            Part("Skull", _headPivot, _mBody, new Vector3(0.44f, 0.38f, 0.5f), new Vector3(0f, 0.03f, 0.22f));
            Part("Snout", _headPivot, _mBody, new Vector3(0.32f, 0.2f, 0.46f), new Vector3(0f, -0.01f, 0.66f));
            Part("Brow", _headPivot, _mHorn, new Vector3(0.46f, 0.06f, 0.16f), new Vector3(0f, 0.2f, 0.36f));

            _jaw = Pivot("Jaw", _headPivot, new Vector3(0f, -0.15f, 0.1f));
            Part("JawBone", _jaw, _mBelly, new Vector3(0.3f, 0.09f, 0.74f), new Vector3(0f, -0.03f, 0.4f));
            Part("Fangs", _jaw, _mHorn, new Vector3(0.24f, 0.08f, 0.1f), new Vector3(0f, 0.05f, 0.7f));

            Mouth = Pivot("Mouth", _headPivot, new Vector3(0f, -0.1f, 0.95f));

            for (int side = -1; side <= 1; side += 2)
            {
                // 뒤로 젖혀 올라가는 뿔
                var hp = Pivot("HornPivot", _headPivot, new Vector3(side * 0.15f, 0.2f, 0.02f));
                hp.localRotation = Quaternion.Euler(34f, side * 14f, 0f);
                Part("Horn", hp, _mHorn, new Vector3(0.08f, 0.08f, 0.52f), new Vector3(0f, 0f, -0.26f));
                Part("Eye", _headPivot, _mGlow, new Vector3(0.05f, 0.09f, 0.13f), new Vector3(side * 0.225f, 0.08f, 0.34f));
                var fin = Part("Fin", _headPivot, _mWing, new Vector3(0.03f, 0.16f, 0.24f), new Vector3(side * 0.235f, 0.0f, -0.06f));
                fin.localRotation = Quaternion.Euler(-20f, side * 18f, 0f);
            }
        }

        void BuildTail()
        {
            // 처졌다가 끝에서 다시 들린다 — 그냥 곧으면 막대기처럼 보인다
            float[] rest = { -10f, -4f, 0f, 3f, 5f, 6f };
            Transform parent = _rig;
            Vector3 at = new Vector3(0f, 1.22f, -0.98f);
            for (int i = 0; i < rest.Length; i++)
            {
                float s = Mathf.Lerp(0.5f, 0.12f, i / (float)(rest.Length - 1));
                var p = Pivot("Tail" + i, parent, at);
                Part("TailSeg", p, _mBody, new Vector3(s, s * 0.9f, 0.52f), new Vector3(0f, 0f, -0.26f));
                if (i % 2 == 0 && i < 5)
                {
                    var sp = Part("TailSpike", p, _mHorn, new Vector3(0.07f, 0.16f, 0.16f), new Vector3(0f, s * 0.5f, -0.26f));
                    sp.localRotation = Quaternion.Euler(45f, 0f, 0f);
                }
                _tail.Add(p); _tailRest.Add(rest[i]);
                parent = p; at = new Vector3(0f, 0f, -0.5f);
            }
            Part("TailFin", parent, _mWing, new Vector3(0.4f, 0.04f, 0.46f), new Vector3(0f, 0f, -0.62f));
        }

        void BuildLegs()
        {
            // 대각선끼리 같이 움직인다 (앞왼·뒤오른 / 앞오른·뒤왼)
            AddLeg("LegFL", -1, 0.62f, 0f, -6f, 12f);
            AddLeg("LegFR", 1, 0.62f, Mathf.PI, -6f, 12f);
            AddLeg("LegRL", -1, -0.72f, Mathf.PI, -18f, 30f);
            AddLeg("LegRR", 1, -0.72f, 0f, -18f, 30f);
        }

        void AddLeg(string name, int side, float z, float offset, float restHip, float restKnee)
        {
            var hip = Pivot(name, _rig, new Vector3(side * 0.44f, 1.04f, z));
            Part("Thigh", hip, _mBody, new Vector3(0.27f, 0.62f, 0.34f), new Vector3(0f, -0.29f, 0f));
            var knee = Pivot("Knee", hip, new Vector3(0f, -0.56f, 0f));
            Part("Shank", knee, _mBody, new Vector3(0.2f, 0.5f, 0.24f), new Vector3(0f, -0.23f, 0f));
            Part("Foot", knee, _mBelly, new Vector3(0.27f, 0.1f, 0.4f), new Vector3(0f, -0.47f, 0.07f));
            Part("Claws", knee, _mHorn, new Vector3(0.25f, 0.06f, 0.1f), new Vector3(0f, -0.49f, 0.3f));
            _legs.Add(new Leg { hip = hip, knee = knee, offset = offset, restHip = restHip, restKnee = restKnee });
        }

        void BuildWings()
        {
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? 1 : -1;
                // 오른쪽 날개 하나만 설계하고 왼쪽은 거울상으로 뒤집는다. 같은 각도를 주면 대칭으로 움직인다
                var root = Pivot(side > 0 ? "WingR" : "WingL", _rig, new Vector3(side * 0.36f, 1.68f, 0.3f));
                root.localScale = new Vector3(side, 1f, 1f);

                var sh = Pivot("Shoulder", root, Vector3.zero);
                Part("ArmIn", sh, _mBody, new Vector3(1.15f, 0.1f, 0.14f), new Vector3(0.575f, 0f, 0f));
                Part("SkinIn", sh, _mWing, new Vector3(1.1f, 0.03f, 0.85f), new Vector3(0.58f, -0.02f, -0.46f));

                var el = Pivot("Elbow", sh, new Vector3(1.15f, 0f, 0f));
                Part("ArmOut", el, _mBody, new Vector3(1.25f, 0.08f, 0.12f), new Vector3(0.62f, 0f, 0f));
                Part("SkinOut", el, _mWing, new Vector3(1.2f, 0.03f, 0.8f), new Vector3(0.62f, -0.02f, -0.42f));
                Part("SkinTip", el, _mWing, new Vector3(0.5f, 0.03f, 0.45f), new Vector3(1.12f, -0.02f, -0.2f));
                Part("Thumb", el, _mHorn, new Vector3(0.08f, 0.08f, 0.3f), new Vector3(0f, 0.02f, 0.18f));

                _shoulder[i] = sh; _elbow[i] = el;
            }
        }

        static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        static Transform Part(string name, Transform parent, Material m, Vector3 size, Vector3 localPos)
        {
            var go = Primitives.Box(name, m, size);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        // ---------------------------------------------------------------- 바깥에서 부르는 것

        public void Play(string state)
        {
            if (_state == Anim.Die) return;                  // 쓰러진 뒤에는 아무것도 안 듣는다
            _state = state;
            _stateTime = 0f;
        }

        /// <summary>0 = 멈춤, 0.5 = 걷기, 1 = 달리기</summary>
        public void SetSpeed(float v) => _speed = Mathf.Clamp01(v);

        public void SetFlying(bool flying) => _flying = flying;

        /// <summary>날갯짓 한 번. Space 를 누를 때마다 부른다</summary>
        public void FlapKick() => _flapKick = 1f;

        public void Flash() => _flash = 1f;

        public void SetGlow(Color c)
        {
            _cGlow = c;
            if (_mGlow == null) return;
            _mGlow.color = c;
            _mGlow.SetColor("_EmissionColor", c * 1.6f);
        }

        void Update() => Tick(Time.deltaTime);

        // ---------------------------------------------------------------- 움직임

        static float Ease(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

        /// <summary>한 프레임 분량의 자세를 만든다. 에디터의 미리보기는 이걸 직접 부른다</summary>
        public void Tick(float dt)
        {
            if (!_built) return;
            _t += dt;
            _stateTime += dt;
            _phase += dt * (3.5f + 8.5f * _speed);
            _fly = Mathf.MoveTowards(_fly, _flying ? 1f : 0f, dt * 5f);
            _flapKick = Mathf.MoveTowards(_flapKick, 0f, dt * 3.2f);

            // 이번 프레임에 기본 자세 위에 얹을 것들
            float neck0 = 0f, neck1 = 0f, head = 0f, jaw = 0f;
            float pitch = 0f, roll = 0f, drop = 0f, spread = _fly, tuck = _fly, shake = 0f, wingLift = 0f;
            float st = _stateTime;

            switch (_state)
            {
                case Anim.Bite:
                {
                    float k = st / 0.5f;
                    if (k < 0.3f) { float a = k / 0.3f; neck0 = -14f * a; head = -10f * a; jaw = 38f * a; }
                    else if (k < 0.5f) { float a = Ease((k - 0.3f) / 0.2f); neck0 = Mathf.Lerp(-14f, 38f, a); neck1 = 10f * a; head = Mathf.Lerp(-10f, -22f, a); jaw = Mathf.Lerp(38f, 0f, a * a); }
                    else if (k < 1f) { float a = Ease((k - 0.5f) / 0.5f); neck0 = Mathf.Lerp(38f, 0f, a); neck1 = Mathf.Lerp(10f, 0f, a); head = Mathf.Lerp(-22f, 0f, a); }
                    else _state = Anim.Idle;
                    break;
                }
                case Anim.Breath:
                {
                    float k = st / 0.9f;
                    if (k < 0.25f) { float a = Ease(k / 0.25f); neck0 = -16f * a; head = -18f * a; pitch = -5f * a; jaw = 10f * a; }
                    else if (k < 0.85f) { float a = Ease((k - 0.25f) / 0.12f); neck0 = Mathf.Lerp(-16f, 20f, a); head = Mathf.Lerp(-18f, 6f, a); jaw = 42f; shake = 1f; }
                    else if (k < 1f) { float a = Ease((k - 0.85f) / 0.15f); neck0 = Mathf.Lerp(20f, 0f, a); head = Mathf.Lerp(6f, 0f, a); jaw = Mathf.Lerp(42f, 0f, a); }
                    else _state = Anim.Idle;
                    break;
                }
                case Anim.Tell:
                {
                    // 보스가 뭔가 쓰기 직전. 몸을 일으키고 날개를 편다 — 이게 "피해라"는 신호다
                    float a = Ease(st / 0.35f);
                    pitch = -18f * a; neck0 = -12f * a; head = -14f * a;
                    jaw = (20f + Mathf.Sin(_t * 14f) * 6f) * a;
                    spread = Mathf.Max(spread, 0.85f * a);
                    wingLift = 38f * a + Mathf.Sin(_t * 9f) * 4f * a;      // 날개를 치켜들고 부르르 떤다
                    break;
                }
                case Anim.Dodge:
                {
                    float k = st / 0.38f;
                    if (k < 1f) { roll = 360f * Ease(k); drop = -0.25f * Mathf.Sin(k * Mathf.PI); spread = 0f; tuck = Mathf.Max(tuck, 0.6f); }
                    else _state = Anim.Idle;
                    break;
                }
                case Anim.Dive:
                    pitch = 55f * Ease(st / 0.15f); spread = 0.12f; tuck = 1f; jaw = 30f; neck0 = 25f;
                    break;
                case Anim.Hit:
                {
                    float k = st / 0.25f;
                    if (k < 1f) { pitch = -14f * (1f - k); head = -12f * (1f - k); }
                    else _state = Anim.Idle;
                    break;
                }
                case Anim.Die:
                {
                    float a = Ease(st / 0.7f);
                    roll = 88f * a; drop = -0.55f * a; jaw = 26f * a; neck0 = 30f * a; spread = 0.35f * a; tuck = 0f;
                    break;
                }
            }

            bool alive = _state != Anim.Die;
            float life = alive ? 1f : 0f;

            // 몸 전체: 걸을 때 들썩이고, 날 때는 날갯짓과 반대로 출렁인다
            float flapHz = 1.3f + 2.2f * _flapKick;
            float flapAmp = 9f + 34f * _flapKick;
            float flap = Mathf.Sin(_t * flapHz * Mathf.PI * 2f) * flapAmp;
            float bob = (Mathf.Sin(_phase * 2f) * 0.045f * _speed + Mathf.Sin(_t * 1.8f) * 0.015f) * (1f - _fly) * life
                        - flap * 0.004f * _fly;
            float flyPitch = (8f + 10f * _speed) * _fly * (_state == Anim.Dive ? 0f : 1f);
            _center.localPosition = new Vector3(0f, CenterY + bob + drop, 0f);
            _center.localRotation = Quaternion.Euler(pitch + flyPitch, 0f, roll);

            // 목과 머리
            float sway = Mathf.Sin(_t * 1.3f) * 2f * life;
            float jitter = shake * Mathf.Sin(_t * 46f) * 1.6f;
            _neck[0].localRotation = Quaternion.Euler(_neckRest[0] + neck0 + sway - flyPitch * 0.6f, jitter, 0f);
            _neck[1].localRotation = Quaternion.Euler(_neckRest[1] + neck1 - sway * 0.5f, 0f, 0f);
            _neck[2].localRotation = Quaternion.Euler(_neckRest[2], 0f, 0f);
            _headPivot.localRotation = Quaternion.Euler(HeadRest + head - sway * 0.5f, jitter, 0f);
            _jaw.localRotation = Quaternion.Euler(jaw, 0f, 0f);

            // 꼬리: 뿌리에서 끝으로 물결이 흘러간다
            for (int i = 0; i < _tail.Count; i++)
            {
                float w = (i + 1f) / _tail.Count;
                float yaw = Mathf.Sin(_t * 2.2f - i * 0.55f) * (5f + 13f * w) * (0.5f + _speed * 0.8f) * life;
                float p = _tailRest[i] + Mathf.Sin(_t * 1.7f - i * 0.4f) * 2.5f * life - 6f * _fly * w;
                _tail[i].localRotation = Quaternion.Euler(p, yaw, 0f);
            }

            // 다리: 걸을 때는 앞뒤로, 날 때는 뒤로 접는다
            foreach (var leg in _legs)
            {
                float s = Mathf.Sin(_phase + leg.offset), c = Mathf.Cos(_phase + leg.offset);
                float swing = s * 30f * _speed * (1f - tuck);
                float lift = Mathf.Max(0f, c) * 36f * _speed * (1f - tuck);
                leg.hip.localRotation = Quaternion.Euler(leg.restHip + swing + 48f * tuck, 0f, 0f);
                leg.knee.localRotation = Quaternion.Euler(leg.restKnee + lift + 34f * tuck, 0f, 0f);
            }

            // 날개: 땅에서는 등 위로 접고, 날 때는 펴서 친다
            var foldS = Quaternion.Euler(0f, 80f, 0f) * Quaternion.Euler(0f, 0f, 16f + Mathf.Sin(_t * 1.8f) * 1.5f);
            // 팔꿈치는 위로 넘겨 접는다. 옆으로(Y축) 접으면 바깥 날개막이 뒤집혀 몸 밖으로 삐져나온다
            var foldE = Quaternion.Euler(0f, 0f, 170f);
            var openS = Quaternion.Euler(0f, 8f, 0f) * Quaternion.Euler(0f, 0f, 12f + flap + wingLift);
            var openE = Quaternion.Euler(0f, -12f, 0f) * Quaternion.Euler(0f, 0f, flap * 0.55f - 6f + wingLift * 0.4f);
            for (int i = 0; i < 2; i++)
            {
                _shoulder[i].localRotation = Quaternion.Slerp(foldS, openS, spread);
                _elbow[i].localRotation = Quaternion.Slerp(foldE, openE, spread);
            }

            // 맞았을 때 하얗게 번쩍인다
            if (_flash > 0f)
            {
                _flash = Mathf.MoveTowards(_flash, 0f, dt * 7f);
                _mBody.color = Color.Lerp(_cBody, Color.white, _flash);
                _mBelly.color = Color.Lerp(_cBelly, Color.white, _flash);
                _mWing.color = Color.Lerp(_cWing, Color.white, _flash);
            }
        }
    }
}
