using System;
using UnityEngine;

namespace KitchenChaos.Core
{
    public class ScoreManager : MonoBehaviour
    {
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int CompletedOrders { get; private set; }
        public int ComboMultiplier => Mathf.Max(1, Combo);

        public event Action Changed;

        public void ResetScore()
        {
            Score = 0;
            Combo = 0;
            CompletedOrders = 0;
            Changed?.Invoke();
        }

        public int AddCompletedOrder(int baseScore)
        {
            Combo = Mathf.Min(5, Combo + 1);
            int gained = baseScore * Combo;
            Score += gained;
            CompletedOrders++;
            Changed?.Invoke();
            return gained;
        }

        public void BreakCombo()
        {
            Combo = 0;
            Changed?.Invoke();
        }
    }
}
