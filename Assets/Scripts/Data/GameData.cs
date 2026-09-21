using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Dragonia.Data
{
    /// <summary>
    /// 2D 드래고니아에서 뽑아 온 이야기와 규칙을 읽는다.
    ///
    /// 퀘스트 17개, 보스 5마리, NPC 대화, 성장 트리, 지도 19장, 일과, 기록 —
    /// 전부 그대로 가져왔다. 3D 로 옮기면서 바뀌는 건 "어떻게 보여 주고 어떻게 싸우는가"지
    /// "무슨 이야기인가"가 아니다.
    ///
    /// 데이터는 StreamingAssets/Data 에 JSON 으로 있고, Tools/export-data.mjs 가 만든다.
    /// 2D 쪽에서 대사를 고치면 그 스크립트만 다시 돌리면 된다 — 손으로 옮겨 적지 않는다.
    ///
    /// 구조가 제각각이라 클래스로 고정하지 않고 JObject 로 들고 있다가 필요한 데서 꺼내 쓴다.
    /// (틀에 맞추려다 원본을 고치기 시작하면, 두 저장소가 서로 어긋나기 시작한다)
    /// </summary>
    public static class GameData
    {
        static readonly Dictionary<string, JObject> _files = new Dictionary<string, JObject>();
        public static bool Loaded { get; private set; }

        public static void LoadAll()
        {
            if (Loaded) return;
            string dir = Path.Combine(Application.streamingAssetsPath, "Data");
            if (!Directory.Exists(dir))
            {
                Debug.LogError($"데이터 폴더가 없다: {dir}\nTools/export-data.mjs 를 먼저 돌려야 한다.");
                return;
            }

            foreach (string path in Directory.GetFiles(dir, "*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith("_")) continue;                 // _functions-to-port.json 같은 메모는 건너뛴다
                try { _files[name] = JObject.Parse(File.ReadAllText(path)); }
                catch (System.Exception e) { Debug.LogError($"{name}.json 을 못 읽었다: {e.Message}"); }
            }
            Loaded = true;
            Debug.Log($"드래고니아 데이터 {_files.Count}개 묶음을 읽었다.");
        }

        /// <summary>파일 이름과 내보낸 이름으로 꺼낸다. 예: Get("quests", "QUESTS")</summary>
        public static JToken Get(string file, string exportName)
        {
            if (!Loaded) LoadAll();
            return _files.TryGetValue(file, out var obj) ? obj[exportName] : null;
        }

        // ---- 자주 쓰는 것들 ----

        public static JArray Quests => Get("quests", "QUESTS") as JArray;
        public static JObject Bosses => Get("enemies", "BOSSES") as JObject;
        public static JObject Enemies => Get("enemies", "ENEMIES") as JObject;
        public static JObject Maps => Get("maps", "MAPS") as JObject;
        public static JArray Stages => Get("elements", "STAGES") as JArray;
        public static JObject Elements => Get("elements", "ELEMENTS") as JObject;
        public static JArray GrowthNodes => Get("growth", "GROWTH_NODES") as JArray;
        public static JObject Skills => Get("skills", "SKILLS") as JObject;
        public static JObject Routines => Get("routines", "ROUTINES") as JObject;

        /// <summary>
        /// 함수였던 값은 { "__fn": "소스" } 로 들어 있다. 조건식과 대사 생성기 81개가 그렇다.
        /// C# 으로 옮기기 전까지는 이걸로 걸러 낸다.
        /// </summary>
        public static bool IsUnportedFunction(JToken token) =>
            token is JObject o && o["__fn"] != null;

        /// <summary>아직 옮기지 않은 함수가 몇 개나 남았는지 — 진행도로 삼는다.</summary>
        public static int CountUnported()
        {
            if (!Loaded) LoadAll();
            int n = 0;
            foreach (var file in _files.Values) n += Count(file);
            return n;

            int Count(JToken t)
            {
                if (IsUnportedFunction(t)) return 1;
                int sum = 0;
                foreach (var child in t.Children()) sum += Count(child);
                return sum;
            }
        }
    }
}
