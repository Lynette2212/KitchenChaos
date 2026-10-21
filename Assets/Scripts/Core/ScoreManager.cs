using System;
using UnityEngine;

namespace KitchenChaos.Core
{
    /// <summary>
    /// 计分与连击系统：
    /// 基础分 × 订单速度系数 × 连击倍率；连续成功提升倍率，失误/超时短暂打断。
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public int Score { get; private set; }
        public int CompletedOrders { get; private set; }
        public int FailedOrders { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }

        public int ComboMultiplier => 1 + Mathf.Min(Combo, 10);

        public event Action ScoreChanged;
        public event Action<int> OrderScored;

        public int RegisterSuccess(int baseScore, float speedBonus)
        {
            Combo++;
            BestCombo = Mathf.Max(BestCombo, Combo);
            int gained = Mathf.RoundToInt(baseScore * speedBonus * ComboMultiplier);
            Score += gained;
            CompletedOrders++;
            ScoreChanged?.Invoke();
            OrderScored?.Invoke(gained);
            return gained;
        }

        public void RegisterFailure()
        {
            FailedOrders++;
            Combo = 0;
            ScoreChanged?.Invoke();
        }

        public void ResetState()
        {
            Score = 0;
            CompletedOrders = 0;
            FailedOrders = 0;
            Combo = 0;
            BestCombo = 0;
            ScoreChanged?.Invoke();
        }
    }
}
