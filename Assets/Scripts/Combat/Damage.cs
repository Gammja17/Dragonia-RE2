using UnityEngine;

namespace Dragonia.Combat
{
    public enum Faction { Player, Enemy }

    /// <summary>
    /// 맞을 수 있는 것. 플레이어와 적이 같은 통로를 쓴다.
    /// 구르는 중의 무적 프레임은 여기서 걸러진다 — 때리는 쪽은 상대가 무적인지 몰라도 된다.
    /// </summary>
    public interface IDamageable
    {
        Faction Side { get; }
        bool Alive { get; }
        bool Invulnerable { get; }
        void TakeDamage(float amount, string element, Vector3 from);
    }

    /// <summary>
    /// 속성. 2D 의 data/elements.js 에서 그대로 왔다 (FIRE · ICE · THUNDER).
    /// 색과 상태이상은 데이터에서 읽는다 — 여기 숫자를 적어 두면 두 저장소가 어긋난다.
    /// </summary>
    public static class Element
    {
        public const string Fire = "FIRE";
        public const string Ice = "ICE";
        public const string Thunder = "THUNDER";

        public static Color ColorOf(string element)
        {
            var el = Data.GameData.Elements;
            if (el != null && element != null && el[element]?["color"] != null &&
                ColorUtility.TryParseHtmlString(el[element]["color"].ToString(), out var c)) return c;
            // 데이터가 아직 안 왔을 때(웹에서는 받아 오는 데 시간이 걸린다)의 색
            switch (element)
            {
                case Ice: return new Color(0.5f, 0.83f, 1f);
                case Thunder: return new Color(1f, 0.89f, 0.48f);
                default: return new Color(1f, 0.48f, 0.16f);
            }
        }
    }

    /// <summary>같은 편끼리는 안 맞는다. 판정하는 쪽마다 새로 짜지 않도록 한 군데 모았다.</summary>
    public static class Hit
    {
        public static bool Apply(Collider col, Faction attacker, float damage, string element, Vector3 from)
        {
            var target = col.GetComponentInParent<IDamageable>();
            if (target == null || target.Side == attacker || !target.Alive || target.Invulnerable) return false;
            target.TakeDamage(damage, element, from);
            return true;
        }
    }
}
