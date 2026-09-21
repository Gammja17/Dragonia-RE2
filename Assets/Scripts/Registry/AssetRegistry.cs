using System.Collections.Generic;
using UnityEngine;

namespace Dragonia.Registry
{
    /// <summary>
    /// 갈아끼우는 자리. 게임 코드는 절대 에셋 경로나 프리팹을 직접 들지 않는다.
    /// "RED_DRAGON", "MORGATH", "SLIME" 같은 id 만 알고, id → 실제 프리팹은 오직 여기서 잇는다.
    ///
    /// 나중에 로우폴리에서 제대로 된 모델로 갈아탈 때, 고치는 곳은 이 표 하나다.
    /// (2D 드래고니아의 data/sprites.js 가 하던 일과 같다 — 그 습관을 그대로 가져왔다)
    ///
    /// 쓰는 법: 프로젝트에 하나만 만들어 두고(Create ▸ Dragonia ▸ Asset Registry)
    ///          Resources 폴더에 "AssetRegistry" 라는 이름으로 둔다.
    /// </summary>
    [CreateAssetMenu(menuName = "Dragonia/Asset Registry", fileName = "AssetRegistry")]
    public class AssetRegistry : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            [Tooltip("데이터에 적힌 id. 예: WESTERN, MORGATH, SLIME, HOUSE")]
            public string id;

            [Tooltip("그 id 가 가리킬 프리팹. 비어 있으면 대역(회색 상자)이 대신 선다.")]
            public GameObject prefab;

            [Tooltip("모델마다 키가 제각각이라, 기준 크기에 맞추는 배율. 1 이면 그대로.")]
            public float scale;
        }

        [Header("용 · 적 · 보스")]
        public Entry[] creatures;

        [Header("소품 · 건물")]
        public Entry[] props;

        [Tooltip("아직 모델이 없는 id 가 나오면 이걸 대신 세운다. 회색 상자여도 게임은 돌아간다.")]
        public GameObject placeholder;

        static AssetRegistry _instance;
        public static AssetRegistry Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<AssetRegistry>("AssetRegistry");
                if (_instance == null) Debug.LogError("Resources/AssetRegistry 를 찾지 못했다.");
                return _instance;
            }
        }

        Dictionary<string, Entry> _lookup;

        void Build()
        {
            _lookup = new Dictionary<string, Entry>();
            foreach (var group in new[] { creatures, props })
            {
                if (group == null) continue;
                foreach (var e in group)
                {
                    if (string.IsNullOrEmpty(e.id)) continue;
                    _lookup[e.id] = e;
                }
            }
        }

        /// <summary>id 로 프리팹을 찾는다. 없으면 대역을 돌려준다 — 없다고 멈추지는 않는다.</summary>
        public GameObject Prefab(string id)
        {
            if (_lookup == null) Build();
            if (_lookup.TryGetValue(id, out var e) && e.prefab != null) return e.prefab;
            return placeholder;
        }

        public float ScaleOf(string id)
        {
            if (_lookup == null) Build();
            if (_lookup.TryGetValue(id, out var e) && e.scale > 0f) return e.scale;
            return 1f;
        }

        /// <summary>id 는 있는데 모델이 아직 없는 것들. 무엇을 더 구해야 하는지 한눈에 본다.</summary>
        public List<string> MissingIds()
        {
            if (_lookup == null) Build();
            var missing = new List<string>();
            foreach (var kv in _lookup)
                if (kv.Value.prefab == null) missing.Add(kv.Key);
            missing.Sort();
            return missing;
        }
    }
}
