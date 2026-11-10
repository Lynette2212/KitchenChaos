using System;
using UnityEngine;

namespace KitchenChaos.Core
{
    /// <summary>
    /// 计分与连击系统：基础分 × 速度系数 × 连击倍率。
    /// 第二连单就开始加分。配错菜不打断连击，超时才会清零。
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public int Score { get; private set; }
        public int CompletedOrders { get; private set; }
        public int FailedOrders { get; private set; }
        public int Mistakes { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public int LastGained { get; private set; }

        public int ComboMultiplier => 1 + Mathf.Min(Mathf.Max(0, Combo - 1), 3);

        public event Action ScoreChanged;
        public event Action<int> OrderScored;

        public int RegisterSuccess(int baseScore, float speedBonus)
        {
            Combo++;
            BestCombo = Mathf.Max(BestCombo, Combo);
            int gained = Mathf.RoundToInt(baseScore * speedBonus * ComboMultiplier);
            Score += gained;
            CompletedOrders++;
            LastGained = gained;
            ScoreChanged?.Invoke();
            OrderScored?.Invoke(gained);
            return gained;
        }

        public void RegisterFailure()
        {
            FailedOrders++;
            Mistakes++;
            Combo = 0;
            ScoreChanged?.Invoke();
        }

        /// <summary>错单、煎糊等操作失误：不计入超时单，但会中断连击并影响当日评星。</summary>
        public void RegisterMistake()
        {
            Mistakes++;
            Combo = 0;
            ScoreChanged?.Invoke();
        }

        public void ResetCombo()
        {
            Combo = 0;
            ScoreChanged?.Invoke();
        }

        public void ResetState()
        {
            Score = 0;
            CompletedOrders = 0;
            FailedOrders = 0;
            Mistakes = 0;
            Combo = 0;
            BestCombo = 0;
            LastGained = 0;
            ScoreChanged?.Invoke();
        }
    }
}
