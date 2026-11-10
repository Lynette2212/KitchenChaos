using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenChaos.Data
{
    public enum OrderUrgency { Normal = 0, Express = 1, Critical = 2 }

    public static class OrderUrgencyInfo
    {
        public static float TimeScale(OrderUrgency urgency)
        {
            switch (urgency)
            {
                case OrderUrgency.Express: return 0.8f;
                case OrderUrgency.Critical: return 0.62f;
                default: return 1f;
            }
        }

        public static float ScoreBonus(OrderUrgency urgency)
        {
            switch (urgency)
            {
                case OrderUrgency.Express: return 1.25f;
                case OrderUrgency.Critical: return 1.6f;
                default: return 1f;
            }
        }

        public static string Name(OrderUrgency urgency)
        {
            switch (urgency)
            {
                case OrderUrgency.Express: return "加急";
                case OrderUrgency.Critical: return "特急";
                default: return "常规";
            }
        }
    }

    /// <summary>
    /// 一张客户订单：对应一个配方，带独立倒计时。超时会损失声望。
    /// 加急/特急订单限时更短，但出餐得分更高。
    /// </summary>
    [Serializable]
    public class Order
    {
        public Recipe recipe;
        public float timeLimit;
        public float timeRemaining;
        public readonly OrderUrgency urgency;
        public bool warned;

        public Order(Recipe r, float limit) : this(r, limit, OrderUrgency.Normal) { }

        public Order(Recipe r, float limit, OrderUrgency urgency)
        {
            recipe = r;
            timeLimit = limit;
            timeRemaining = limit;
            this.urgency = urgency;
        }

        public float UrgencyScoreMultiplier => OrderUrgencyInfo.ScoreBonus(urgency);

        /// <summary>每秒由订单系统驱动。返回 true 表示刚刚超时。</summary>
        public bool Tick(float deltaTime)
        {
            if (timeRemaining <= 0f || deltaTime <= 0f) return false;
            timeRemaining = Mathf.Max(0f, timeRemaining - deltaTime);
            return timeRemaining == 0f;
        }

        public float TimeRatio
        {
            get { return timeLimit <= 0f ? 0f : Mathf.Clamp01(timeRemaining / timeLimit); }
        }

        /// <summary>剩余时间越多，分数系数越高（1.0 ~ 1.5）。</summary>
        public float SpeedBonus
        {
            get { return Mathf.Lerp(1.0f, 1.5f, TimeRatio); }
        }

        public string GetRemainingText()
        {
            return recipe != null ? recipe.GetRequirementText() : string.Empty;
        }
    }
}
