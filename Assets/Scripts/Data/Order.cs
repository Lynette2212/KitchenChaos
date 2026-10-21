using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenChaos.Data
{
    /// <summary>
    /// 一张客户订单：对应一个配方，带独立倒计时。超时会损失声望。
    /// </summary>
    [Serializable]
    public class Order
    {
        public Recipe recipe;
        public float timeLimit;
        public float timeRemaining;

        public Order(Recipe r, float limit)
        {
            recipe = r;
            timeLimit = limit;
            timeRemaining = limit;
        }

        /// <summary>每秒由订单系统驱动。返回 true 表示刚刚超时。</summary>
        public bool Tick(float deltaTime)
        {
            if (timeRemaining <= 0f) return false;
            timeRemaining -= deltaTime;
            return timeRemaining <= 0f;
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
