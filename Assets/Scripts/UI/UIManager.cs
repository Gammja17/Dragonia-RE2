using System.Collections.Generic;
using UnityEngine;

namespace Dragonia.UI
{
    /// <summary>
    /// 화면을 띄우고 감추는 곳. 화면은 전부 에디터에서 만든 프리팹이고, 코드로 짓지 않는다.
    ///
    /// 여기가 하는 일은 셋뿐이다 — 어느 프리팹을 언제 띄울지, 겹쳐 띄운 것 중 맨 위가 무엇인지,
    /// 그리고 화면이 떠 있는 동안 게임을 멈출지.
    ///
    /// 새 화면을 만들려면: 프리팹을 만들고 Panel 컴포넌트를 붙인 다음 아래 panels 에 등록한다.
    /// 코드는 이름으로만 부른다 — UIManager.Open("Journal").
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [System.Serializable]
        public struct PanelEntry
        {
            [Tooltip("코드에서 부를 이름. 예: Journal, Den, Dialogue")]
            public string id;
            public Panel prefab;
            [Tooltip("떠 있는 동안 게임을 멈출지. 대화·일지는 멈추고, 알림은 안 멈춘다.")]
            public bool pausesGame;
        }

        [SerializeField] PanelEntry[] panels;
        [SerializeField] Transform root;         // 캔버스. 프리팹은 여기 밑으로 들어간다

        static UIManager _instance;

        readonly Dictionary<string, PanelEntry> _byId = new Dictionary<string, PanelEntry>();
        readonly List<Panel> _stack = new List<Panel>();

        void Awake()
        {
            _instance = this;
            if (root == null) root = transform;
            foreach (var p in panels)
                if (!string.IsNullOrEmpty(p.id)) _byId[p.id] = p;
        }

        public static void Open(string id) => _instance?.OpenPanel(id);
        public static void Close(string id) => _instance?.ClosePanel(id);

        /// <summary>[Esc]: 맨 위 화면부터 닫는다. 닫을 게 없으면 false — 그때 설정 창을 연다.</summary>
        public static bool CloseTop()
        {
            if (_instance == null || _instance._stack.Count == 0) return false;
            var top = _instance._stack[_instance._stack.Count - 1];
            _instance.ClosePanel(top.Id);
            return true;
        }

        public static bool AnyOpen => _instance != null && _instance._stack.Count > 0;

        void OpenPanel(string id)
        {
            if (!_byId.TryGetValue(id, out var entry) || entry.prefab == null)
            {
                Debug.LogWarning($"UI 프리팹이 등록되지 않았다: {id}");
                return;
            }
            if (_stack.Exists(p => p.Id == id)) return;      // 이미 떠 있다

            var panel = Instantiate(entry.prefab, root);
            panel.Bind(id, this);
            _stack.Add(panel);
            Refresh();
        }

        void ClosePanel(string id)
        {
            int i = _stack.FindIndex(p => p.Id == id);
            if (i < 0) return;
            var panel = _stack[i];
            _stack.RemoveAt(i);
            panel.Dismiss();
            Refresh();
        }

        void Refresh()
        {
            bool pause = false;
            foreach (var p in _stack)
                if (_byId.TryGetValue(p.Id, out var e) && e.pausesGame) pause = true;
            Time.timeScale = pause ? 0f : 1f;
        }
    }

    /// <summary>화면 프리팹의 뿌리에 붙인다. 열고 닫을 때의 연출은 각 프리팹이 알아서 한다.</summary>
    public abstract class Panel : MonoBehaviour
    {
        public string Id { get; private set; }
        protected UIManager Manager { get; private set; }

        public void Bind(string id, UIManager manager)
        {
            Id = id;
            Manager = manager;
            OnOpened();
        }

        public virtual void Dismiss() => Destroy(gameObject);

        protected virtual void OnOpened() { }
    }
}
