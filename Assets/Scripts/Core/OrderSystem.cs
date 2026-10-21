using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.Core
{
    /// <summary>
    /// 订单系统：周期性生成订单，维护每张订单的倒计时；
    /// 成功交付或超时都会发出事件，并由 GameManager 联动声望。
    /// </summary>
    public class OrderSystem : MonoBehaviour
    {
        [Header("订单生成节奏")]
        public float firstOrderDelay = 2f;
        public float minSpawnInterval = 4f;
        public float maxSpawnInterval = 8f;
        public int maxActiveOrders = 4;

        [Header("订单时限（秒）")]
        public float minTimeLimit = 25f;
        public float maxTimeLimit = 55f;

        public List<Recipe> AvailableRecipes { get; private set; } = new List<Recipe>();
        public List<Order> ActiveOrders { get; private set; } = new List<Order>();

        public event Action OrdersChanged;
        public event Action<Order> OrderExpired;
        public event Action<Order> OrderDelivered;

        private bool spawning;

        public void Initialize(List<Recipe> recipes)
        {
            AvailableRecipes = recipes ?? new List<Recipe>();
            ActiveOrders.Clear();
            StopAllCoroutines();
            spawning = false;
            OrdersChanged?.Invoke();
        }

        public void StartSpawning()
        {
            if (!spawning) StartCoroutine(SpawnLoop());
        }

        public void StopSpawning()
        {
            spawning = false;
            StopAllCoroutines();
        }

        private IEnumerator SpawnLoop()
        {
            spawning = true;
            yield return new WaitForSeconds(firstOrderDelay);

            while (spawning)
            {
                if (AvailableRecipes.Count > 0 && ActiveOrders.Count < maxActiveOrders)
                {
                    SpawnOrder();
                }
                yield return new WaitForSeconds(UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval));
            }
        }

        private void SpawnOrder()
        {
            Recipe recipe = AvailableRecipes[UnityEngine.Random.Range(0, AvailableRecipes.Count)];
            float limit = UnityEngine.Random.Range(minTimeLimit, maxTimeLimit);
            Order order = new Order(recipe, limit);
            ActiveOrders.Add(order);
            OrdersChanged?.Invoke();
        }

        private void Update()
        {
            if (!spawning || ActiveOrders.Count == 0) return;

            for (int i = ActiveOrders.Count - 1; i >= 0; i--)
            {
                if (ActiveOrders[i].Tick(Time.deltaTime))
                {
                    Order expired = ActiveOrders[i];
                    ActiveOrders.RemoveAt(i);
                    OrderExpired?.Invoke(expired);
                    OrdersChanged?.Invoke();
                }
            }
        }

        /// <summary>尝试用一盘菜匹配并完成订单。返回得分所需信息；不匹配返回 null。</returns>
        public DeliveryResult TryDeliver(List<Ingredient> dish)
        {
            for (int i = 0; i < ActiveOrders.Count; i++)
            {
                Order order = ActiveOrders[i];
                if (order.recipe != null && order.recipe.Matches(dish))
                {
                    ActiveOrders.RemoveAt(i);
                    OrderDelivered?.Invoke(order);
                    OrdersChanged?.Invoke();
                    return new DeliveryResult
                    {
                        order = order,
                        baseScore = order.recipe.score,
                        speedBonus = order.SpeedBonus
                    };
                }
            }
            return null;
        }

        public void ClearOrders()
        {
            ActiveOrders.Clear();
            OrdersChanged?.Invoke();
        }
    }

    public class DeliveryResult
    {
        public Order order;
        public int baseScore;
        public float speedBonus;
    }
}
