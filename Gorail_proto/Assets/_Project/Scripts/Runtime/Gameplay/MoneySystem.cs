using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 돈(재화) 자원 관리(자원기획서 §4).
    /// 탑승/환승 비용은 TurnResolver·PlatformController에서 호출.
    /// </summary>
    public class MoneySystem : MonoBehaviour
    {
        [Header("초기값")]
        [SerializeField] public int initialMoney  = 1_000_000;

        [Header("비용")]
        [SerializeField] public int boardingCost  = 1500; // 탑승 1회
        [SerializeField] public int transferCost  = 300;  // 환승 1회

        public int CurrentMoney { get; private set; }

        /// <summary>잔액이 바뀔 때마다 발생 (새 잔액).</summary>
        public event System.Action<int> MoneyChanged;

        public void Initialize()
        {
            CurrentMoney = initialMoney;
            MoneyChanged?.Invoke(CurrentMoney);
        }

        /// <summary>지출 시도. 잔액 부족이면 false — 지금은 음수 허용(기획 미확정).</summary>
        public bool TrySpend(int amount)
        {
            CurrentMoney -= amount;
            MoneyChanged?.Invoke(CurrentMoney);
            return true;
        }

        public void Earn(int amount)
        {
            if (amount <= 0) return;
            CurrentMoney += amount;
            MoneyChanged?.Invoke(CurrentMoney);
        }

        public void Restore(int money)
        {
            CurrentMoney = money;
            MoneyChanged?.Invoke(CurrentMoney);
        }
    }
}
