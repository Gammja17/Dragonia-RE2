using UnityEngine;
using UnityEngine.UI;
using Dragonia.Characters;
using Dragonia.Combat;
using Dragonia.Enemies;

namespace Dragonia.UI
{
    /// <summary>
    /// 싸우는 동안 떠 있는 화면. 생김새는 프리팹(Assets/Prefabs/UI/Hud.prefab)에 있고,
    /// 여기서는 숫자를 막대에 옮겨 적기만 한다.
    ///
    /// 막대는 스프라이트 없이 앵커로 줄인다 (anchorMax.x = 비율). 채우기(Filled)는
    /// 스프라이트가 있어야 동작하는데, 아직 UI 그림이 없다.
    ///
    /// 글자가 영어인 건 글꼴 때문이다. 웹 빌드에는 운영체제 글꼴이 없어서 내장 글꼴만 쓸 수 있고,
    /// 내장 글꼴에는 한글이 없다. 한글 글꼴 에셋을 넣으면 그때 바꾼다.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public RectTransform playerHp, bossHp;
        public Image elementChip;
        public Text bossName, center, hint;
        public GameObject bossGroup;

        DragonController _player;
        BossBrain _boss;
        float _shownBoss = 1f, _shownHp = 1f;

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) _player = p.GetComponent<DragonController>();
            _boss = FindFirstObjectByType<BossBrain>();
        }

        void Update()
        {
            if (_player != null)
            {
                _shownHp = Mathf.MoveTowards(_shownHp, _player.Hp / _player.maxHp, Time.unscaledDeltaTime * 1.5f);
                Fill(playerHp, _shownHp);
                if (elementChip != null) elementChip.color = Element.ColorOf(_player.element);
            }

            if (_boss != null)
            {
                if (bossGroup != null) bossGroup.SetActive(true);
                _shownBoss = Mathf.MoveTowards(_shownBoss, _boss.HpRatio, Time.unscaledDeltaTime * 0.8f);
                Fill(bossHp, _shownBoss);
                if (bossName != null) bossName.text = _boss.DisplayName + (_boss.Raging && _boss.Alive ? "  - ENRAGED -" : "");
            }
            else if (bossGroup != null) bossGroup.SetActive(false);

            if (center != null)
            {
                if (_player != null && !_player.Alive) center.text = "YOU FELL";
                else if (_boss != null && !_boss.Alive) center.text = "VICTORY";
                else if (!LockOnCamera.CursorLocked) center.text = "CLICK TO PLAY";
                else center.text = "";
            }
        }

        static void Fill(RectTransform bar, float ratio)
        {
            if (bar == null) return;
            bar.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }
    }
}
