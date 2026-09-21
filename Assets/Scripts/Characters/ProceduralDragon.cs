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
        readonly Transform[] _wingRoot = new Transform[2], _shoulder = new Transform[2], _elbow = new Transform[2];

        Material _mBody, _mDark, _mBelly, _mWing, _mHorn, _mGlow;
        Color _cBody, _cDark, _cBelly, _cWing, _cHorn, _cGlow;

        string _state = Anim.Idle;
        float _stateTime, _t, _phase, _speed, _fly, _flapKick, _flash, _breath, _aimPitch, _aimPitchTarget;
        bool _flying, _breathing, _built;

        const float HeadRest = 30f;
        const float CenterY = 1.2f;

        // ---------------------------------------------------------------- 짓기

        public void Build(Color body, Color wing, Color glow)
        {
            if (_built) return;
            _built = true;

            _cBody = body;
            _cDark = Color.Lerp(body, Color.black, 0.34f);          // 등줄기·팔다리 끝·날개뼈. 한 색이면 덩어리가 안 읽힌다
            _cBelly = Color.Lerp(body, new Color(1f, 0.93f, 0.78f), 0.55f);
            _cWing = wing;
            _cHorn = new Color(0.93f, 0.89f, 0.78f);
            _cGlow = glow;
            _mBody = Primitives.Solid(_cBody);
            _mDark = Primitives.Solid(_cDark);
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
            // 가슴이 가장 굵고 엉덩이로 갈수록 가늘어진다. 좁아지는 쪽이 +Z 라서 뒤로 돌려 놓는다
            var back = Quaternion.Euler(0f, 180f, 0f);
            Tap("Chest", _rig, _mBody, new Vector3(1.0f, 0.95f, 1.3f), new Vector3(0f, 1.28f, 0.3f), 0.78f, 0.8f).localRotation = back;
            Tap("Hips", _rig, _mBody, new Vector3(0.78f, 0.76f, 0.9f), new Vector3(0f, 1.2f, -0.78f), 0.64f, 0.66f).localRotation = back;
            Tap("Breast", _rig, _mBody, new Vector3(0.92f, 0.86f, 0.42f), new Vector3(0f, 1.3f, 1.14f), 0.56f, 0.6f);
            Tap("Saddle", _rig, _mDark, new Vector3(0.5f, 0.1f, 1.9f), new Vector3(0f, 1.71f, -0.1f), 0.5f, 1f).localRotation =
                Quaternion.Euler(-7f, 180f, 0f);

            // 배의 비늘판. 한 장으로 깔면 장난감 같아서 여러 장으로 나눈다
            float[] pz = { 0.95f, 0.5f, 0.02f, -0.5f, -0.98f };
            float[] pw = { 0.6f, 0.7f, 0.64f, 0.54f, 0.44f };
            float[] py = { 0.9f, 0.8f, 0.82f, 0.86f, 0.9f };
            for (int i = 0; i < pz.Length; i++)
                Part("Scute", _rig, _mBelly, new Vector3(pw[i], 0.14f, 0.44f), new Vector3(0f, py[i], pz[i])).localRotation =
                    Quaternion.Euler(i == 0 ? -28f : 4f, 0f, 0f);

            // 등줄기의 가시. 가운데가 제일 크다
            float[] sz = { 0.85f, 0.45f, 0.05f, -0.35f, -0.75f, -1.1f };
            float[] sy = { 1.74f, 1.74f, 1.71f, 1.65f, 1.57f, 1.48f };
            float[] sh = { 0.26f, 0.36f, 0.4f, 0.34f, 0.27f, 0.2f };
            for (int i = 0; i < sz.Length; i++)
                Spike(_rig, new Vector3(0f, sy[i], sz[i]), 0.09f, 0.3f, sh[i], 22f);
        }

        void BuildNeckAndHead()
        {
            // 목 세 마디. 첫 마디가 위로 치켜들고 뒤 마디들이 앞으로 굽어 S 자가 된다. 마디마다 조금씩 가늘어진다
            float[] rest = { -52f, 14f, 16f };
            float[] size = { 0.52f, 0.44f, 0.38f, 0.33f };
            float[] len = { 0.62f, 0.56f, 0.50f };
            Transform parent = _rig;
            Vector3 at = new Vector3(0f, 1.52f, 0.88f);
            for (int i = 0; i < 3; i++)
            {
                float k = size[i + 1] / size[i];
                var p = Pivot("Neck" + i, parent, at);
                Tap("NeckSeg", p, _mBody, new Vector3(size[i], size[i], len[i]), new Vector3(0f, 0f, len[i] * 0.5f), k, k);
                Tap("NeckBelly", p, _mBelly, new Vector3(size[i] * 0.66f, 0.1f, len[i] * 0.92f),
                    new Vector3(0f, -size[i] * 0.47f, len[i] * 0.5f), k, 1f);
                Spike(p, new Vector3(0f, size[i] * 0.46f, len[i] * 0.45f), 0.07f, 0.22f, 0.2f - i * 0.03f, 25f);
                _neck.Add(p); _neckRest.Add(rest[i]);
                parent = p; at = new Vector3(0f, 0f, len[i] - 0.05f);
            }

            _headPivot = Pivot("Head", parent, at);
            Head = _headPivot;
            Tap("Skull", _headPivot, _mBody, new Vector3(0.48f, 0.4f, 0.52f), new Vector3(0f, 0.03f, 0.22f), 0.76f, 0.7f);
            Tap("Snout", _headPivot, _mBody, new Vector3(0.365f, 0.27f, 0.52f), new Vector3(0f, -0.015f, 0.7f), 0.66f, 0.62f);
            Tap("Bridge", _headPivot, _mDark, new Vector3(0.14f, 0.06f, 0.78f), new Vector3(0f, 0.2f, 0.5f), 0.6f, 1f, false).localRotation =
                Quaternion.Euler(9f, 0f, 0f);

            _jaw = Pivot("Jaw", _headPivot, new Vector3(0f, -0.15f, 0.1f));
            Tap("JawBone", _jaw, _mBelly, new Vector3(0.34f, 0.1f, 0.8f), new Vector3(0f, -0.03f, 0.4f), 0.62f, 0.7f);

            Mouth = Pivot("Mouth", _headPivot, new Vector3(0f, -0.1f, 0.95f));

            var up = Quaternion.Euler(-90f, 0f, 0f);       // +Z 로 좁아지는 것을 위로 세운다
            var down = Quaternion.Euler(90f, 0f, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                // 뒤로 젖혀 올라가다 끝이 한 번 더 들리는 큰 뿔
                var hp = Pivot("HornPivot", _headPivot, new Vector3(side * 0.16f, 0.2f, 0.02f));
                hp.localRotation = Quaternion.LookRotation(new Vector3(side * 0.26f, 0.5f, -0.83f));
                Tap("Horn", hp, _mHorn, new Vector3(0.12f, 0.12f, 0.36f), new Vector3(0f, 0f, 0.18f), 0.7f, 0.7f);
                var tip = Pivot("HornTip", hp, new Vector3(0f, 0f, 0.34f));
                tip.localRotation = Quaternion.Euler(-24f, 0f, 0f);
                Tap("Horn", tip, _mHorn, new Vector3(0.084f, 0.084f, 0.4f), new Vector3(0f, 0f, 0.2f), 0.08f, 0.08f);

                // 턱 옆의 작은 뿔과 볼 지느러미
                var sp = Pivot("CheekHorn", _headPivot, new Vector3(side * 0.2f, -0.06f, 0.02f));
                sp.localRotation = Quaternion.LookRotation(new Vector3(side * 0.55f, 0.05f, -0.83f));
                Tap("Horn", sp, _mHorn, new Vector3(0.08f, 0.08f, 0.3f), new Vector3(0f, 0f, 0.15f), 0.1f, 0.1f, false);
                var fp = Pivot("Frill", _headPivot, new Vector3(side * 0.22f, 0.08f, 0.0f));
                fp.localRotation = Quaternion.LookRotation(new Vector3(side * 0.5f, 0.28f, -0.82f));
                Tap("Fin", fp, _mWing, new Vector3(0.03f, 0.26f, 0.36f), new Vector3(0f, 0f, 0.18f), 1f, 0.12f, false);

                // 눈과 그 위를 덮는 눈썹뼈. 눈썹이 안쪽으로 기울어야 사나워 보인다
                Part("Eye", _headPivot, _mGlow, new Vector3(0.05f, 0.1f, 0.15f), new Vector3(side * 0.205f, 0.09f, 0.32f), false);
                Part("Brow", _headPivot, _mDark, new Vector3(0.12f, 0.07f, 0.3f), new Vector3(side * 0.19f, 0.185f, 0.3f), false).localRotation =
                    Quaternion.Euler(14f, side * -8f, side * -12f);
                Part("Nostril", _headPivot, _mDark, new Vector3(0.05f, 0.04f, 0.07f), new Vector3(side * 0.07f, 0.075f, 0.9f), false);

                // 이빨: 윗니 둘은 아래로, 아랫니 하나는 위로
                Tap("Tooth", _headPivot, _mHorn, new Vector3(0.05f, 0.05f, 0.13f), new Vector3(side * 0.1f, -0.2f, 0.86f), 0.1f, 0.1f, false).localRotation = down;
                Tap("Tooth", _headPivot, _mHorn, new Vector3(0.045f, 0.045f, 0.1f), new Vector3(side * 0.14f, -0.19f, 0.62f), 0.1f, 0.1f, false).localRotation = down;
                Tap("Fang", _jaw, _mHorn, new Vector3(0.05f, 0.05f, 0.14f), new Vector3(side * 0.085f, 0.08f, 0.7f), 0.1f, 0.1f, false).localRotation = up;
            }
        }

        void BuildTail()
        {
            // 처졌다가 끝에서 다시 들린다 — 그냥 곧으면 막대기처럼 보인다.
            // 마디의 앞면 크기가 다음 마디의 뒷면 크기와 같아서 계단 없이 한 줄로 가늘어진다
            float[] rest = { -10f, -4f, 0f, 3f, 5f, 6f, 6f };
            const float L = 0.5f;
            Transform parent = _rig;
            Vector3 at = new Vector3(0f, 1.2f, -1.2f);
            var back = Quaternion.Euler(0f, 180f, 0f);
            for (int i = 0; i < rest.Length; i++)
            {
                float s0 = Mathf.Lerp(0.5f, 0.09f, i / (float)rest.Length);
                float s1 = Mathf.Lerp(0.5f, 0.09f, (i + 1f) / rest.Length);
                var p = Pivot("Tail" + i, parent, at);
                Tap("TailSeg", p, _mBody, new Vector3(s0, s0 * 0.92f, L + 0.04f), new Vector3(0f, 0f, -L * 0.5f), s1 / s0, s1 / s0).localRotation = back;
                Tap("TailBelly", p, _mBelly, new Vector3(s0 * 0.6f, 0.06f, L), new Vector3(0f, -s0 * 0.44f, -L * 0.5f), s1 / s0, 1f, false).localRotation = back;
                if (i < 5) Spike(p, new Vector3(0f, s0 * 0.42f, -L * 0.5f), 0.06f, 0.2f, 0.2f - i * 0.025f, 28f);
                _tail.Add(p); _tailRest.Add(rest[i]);
                parent = p; at = new Vector3(0f, 0f, -L);
            }
            // 꼬리 끝의 화살촉 지느러미
            var spade = Primitives.Shape("TailFin", Primitives.Plate(new[]
            {
                new Vector2(0f, 0.12f), new Vector2(0.3f, -0.28f), new Vector2(0.1f, -0.3f), new Vector2(0f, -0.78f),
                new Vector2(-0.1f, -0.3f), new Vector2(-0.3f, -0.28f),
            }), _mWing, new Vector3(1f, 0.045f, 1f));
            spade.transform.SetParent(parent, false);
            spade.transform.localPosition = new Vector3(0f, 0f, -L + 0.05f);
        }

        void BuildLegs()
        {
            // 대각선끼리 같이 움직인다 (앞왼·뒤오른 / 앞오른·뒤왼). 뒷다리가 더 굵다
            AddLeg("LegFL", -1, 0.62f, 0f, -6f, 12f, 1f);
            AddLeg("LegFR", 1, 0.62f, Mathf.PI, -6f, 12f, 1f);
            AddLeg("LegRL", -1, -0.8f, Mathf.PI, -18f, 30f, 1.2f);
            AddLeg("LegRR", 1, -0.8f, 0f, -18f, 30f, 1.2f);
        }

        void AddLeg(string name, int side, float z, float offset, float restHip, float restKnee, float bulk)
        {
            var down = Quaternion.Euler(90f, 0f, 0f);      // +Z 로 좁아지는 것을 아래로 늘어뜨린다
            var hip = Pivot(name, _rig, new Vector3(side * 0.46f, 1.04f, z));
            Tap("Thigh", hip, _mBody, new Vector3(0.3f * bulk, 0.44f * bulk, 0.68f), new Vector3(0f, -0.27f, 0f), 0.68f, 0.6f).localRotation = down;
            var knee = Pivot("Knee", hip, new Vector3(0f, -0.56f, 0f));
            Tap("Shank", knee, _mDark, new Vector3(0.2f * bulk, 0.25f * bulk, 0.5f), new Vector3(0f, -0.23f, 0f), 0.8f, 0.78f).localRotation = down;
            Tap("Foot", knee, _mDark, new Vector3(0.3f, 0.12f, 0.44f), new Vector3(0f, -0.47f, 0.08f), 0.9f, 0.55f);
            for (int c = -1; c <= 1; c++)
                Tap("Claw", knee, _mHorn, new Vector3(0.07f, 0.08f, 0.18f), new Vector3(c * 0.1f, -0.49f, 0.37f), 0.1f, 0.1f, false).localRotation =
                    Quaternion.Euler(8f, c * 14f, 0f);
            _legs.Add(new Leg { hip = hip, knee = knee, offset = offset, restHip = restHip, restKnee = restKnee });
        }

        // 날개막의 윤곽 (XZ 평면, 팔이 +X 로 뻗는다). 뒷전이 손가락뼈 사이에서 안으로 패여 박쥐 날개가 된다
        static readonly Vector2[] InnerSkin =
        {
            new Vector2(0f, 0.05f), new Vector2(1.15f, 0.05f), new Vector2(1.15f, -0.98f),
            new Vector2(0.62f, -0.76f), new Vector2(0.04f, -1.02f),
        };
        static readonly Vector2[] OuterSkin =
        {
            new Vector2(0f, 0.05f), new Vector2(1.58f, -0.04f), new Vector2(1.12f, -0.4f), new Vector2(1.27f, -0.74f),
            new Vector2(0.72f, -0.74f), new Vector2(0.63f, -1.07f), new Vector2(0.2f, -0.86f), new Vector2(0f, -0.98f),
        };
        static readonly Vector2[] Fingers = { new Vector2(1.58f, -0.04f), new Vector2(1.27f, -0.74f), new Vector2(0.63f, -1.07f) };
        static Mesh _innerSkin, _outerSkin;

        void BuildWings()
        {
            if (_innerSkin == null) _innerSkin = Primitives.Plate(InnerSkin);
            if (_outerSkin == null) _outerSkin = Primitives.Plate(OuterSkin);

            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? 1 : -1;
                // 오른쪽 날개 하나만 설계하고 왼쪽은 거울상으로 뒤집는다. 같은 각도를 주면 대칭으로 움직인다
                var root = Pivot(side > 0 ? "WingR" : "WingL", _rig, new Vector3(side * 0.36f, 1.68f, 0.3f));
                root.localScale = new Vector3(side, 1f, 1f);

                var sh = Pivot("Shoulder", root, Vector3.zero);
                Bone(sh, new Vector2(1.15f, 0f), 0.15f, 0.7f);
                Skin(sh, _innerSkin);

                var el = Pivot("Elbow", sh, new Vector3(1.15f, 0f, 0f));
                foreach (var f in Fingers) Bone(el, f, 0.09f, 0.3f);
                Skin(el, _outerSkin);
                Tap("Thumb", el, _mHorn, new Vector3(0.08f, 0.08f, 0.32f), new Vector3(0f, 0.02f, 0.18f), 0.1f, 0.1f, false);

                _wingRoot[i] = root; _shoulder[i] = sh; _elbow[i] = el;
            }
        }

        // 뼈 하나: 관절에서 XZ 평면의 한 점까지 뻗는 가늘어지는 막대
        void Bone(Transform joint, Vector2 to, float thick, float taper)
        {
            var dir = new Vector3(to.x, 0f, to.y);
            var b = Tap("Bone", joint, _mDark, new Vector3(thick, thick, dir.magnitude), dir * 0.5f + Vector3.up * 0.01f, taper, taper);
            b.localRotation = Quaternion.LookRotation(dir.normalized);
        }

        void Skin(Transform joint, Mesh mesh)
        {
            var go = Primitives.Shape("Skin", mesh, _mWing, new Vector3(1f, 0.03f, 1f));
            go.transform.SetParent(joint, false);
            go.transform.localPosition = new Vector3(0f, -0.02f, 0f);
        }

        // 가시 하나: 밑동의 자리를 주면 뒤로 기운 지느러미 모양 쐐기를 세운다
        void Spike(Transform parent, Vector3 basePos, float thick, float length, float height, float lean)
        {
            var rot = Quaternion.Euler(-90f - lean, 0f, 0f);
            var s = Tap("Spike", parent, _mHorn, new Vector3(thick, length, height), basePos + rot * Vector3.forward * (height * 0.5f - 0.03f), 0.4f, 0f, false);
            s.localRotation = rot;
        }

        static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        static Transform Part(string name, Transform parent, Material m, Vector3 size, Vector3 localPos, bool shadow = true)
        {
            var go = Primitives.Shape(name, Primitives.Cube, m, size, shadow);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        // +Z 쪽이 (tx, ty) 배로 좁아지는 상자
        static Transform Tap(string name, Transform parent, Material m, Vector3 size, Vector3 localPos, float tx, float ty, bool shadow = true)
        {
            var go = Primitives.Shape(name, Primitives.Taper(tx, ty), m, size, shadow);
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

        /// <summary>숨을 뿜는 동안 켜 둔다. 걷거나 나는 자세 위에 얹히므로 움직이면서 쏠 수 있다</summary>
        public void SetBreathing(bool on) => _breathing = on;

        /// <summary>조준이 위아래로 얼마나 기울었나 (도, 위가 +). 목과 머리가 그쪽을 본다</summary>
        public void SetAimPitch(float degrees) => _aimPitchTarget = Mathf.Clamp(degrees, -50f, 50f);

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

            // 숨결은 상태가 아니라 덧칠이다: 입을 벌리고 목을 뻗어 조준한 쪽을 본다
            _breath = Mathf.MoveTowards(_breath, _breathing && alive ? 1f : 0f, dt * 8f);
            _aimPitch = Mathf.Lerp(_aimPitch, _aimPitchTarget, 1f - Mathf.Exp(-12f * dt));
            if (_breath > 0f)
            {
                jaw = Mathf.Max(jaw, 40f * _breath);
                neck0 += (12f - _aimPitch * 0.45f) * _breath;
                head += (-6f - _aimPitch * 0.5f) * _breath;
                shake = Mathf.Max(shake, _breath);
            }

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
                // 접을 때는 날개막도 오그라든다. 안 그러면 두 날개가 등 위에서 서로 뚫고 지나간다
                _wingRoot[i].localScale = new Vector3(i == 0 ? 1f : -1f, 1f, Mathf.Lerp(0.5f, 1f, spread));
                _shoulder[i].localRotation = Quaternion.Slerp(foldS, openS, spread);
                _elbow[i].localRotation = Quaternion.Slerp(foldE, openE, spread);
            }

            // 맞았을 때 하얗게 번쩍인다
            if (_flash > 0f)
            {
                _flash = Mathf.MoveTowards(_flash, 0f, dt * 7f);
                _mBody.color = Color.Lerp(_cBody, Color.white, _flash);
                _mDark.color = Color.Lerp(_cDark, Color.white, _flash);
                _mBelly.color = Color.Lerp(_cBelly, Color.white, _flash);
                _mWing.color = Color.Lerp(_cWing, Color.white, _flash);
            }
        }
    }
}
