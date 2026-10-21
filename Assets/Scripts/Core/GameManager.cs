using System;
using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Audio;

namespace KitchenChaos.Core
{
    public enum GameState { Ready, Playing, GameOver }

    /// <summary>
    /// 游戏总控：串联计时、订单、计分、声望，并提供开始/重开接口。
    /// 声望（生命值）随订单超时或错误出餐下降，归零立即失败。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public TimerSystem Timer { get; private set; }
        public OrderSystem Orders { get; private set; }
        public ScoreManager Score { get; private set; }

        public GameState State { get; private set; } = GameState.Ready;
        public bool IsPlaying => State == GameState.Playing;
        public bool IsGameOver => State == GameState.GameOver;

        [Header("规则")]
        public int startReputation = 3;
        public float roundDuration = 150f;

        public int Reputation { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<int> ReputationChanged;
        public event Action<string, bool> Toast; // 文本提示，是否正面反馈

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(TimerSystem timer, OrderSystem orders, ScoreManager score)
        {
            Timer = timer;
            Orders = orders;
            Score = score;

            Orders.OrderExpired += OnOrderExpired;
            Timer.Expired += OnTimeUp;
        }

        public void StartGame()
        {
            Reputation = startReputation;
            Score.ResetState();
            Orders.ClearOrders();
            Orders.StartSpawning();
            Timer.BeginRound(roundDuration);
            SetState(GameState.Playing);
            ShowToast("开始营业！", true);
        }

        private void SetState(GameState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        /// <summary>由出餐台调用：用一盘菜尝试完成订单。</summary>
        public bool TryDeliverDish(List<Ingredient> dish)
        {
            if (!IsPlaying || Orders == null) return false;

            DeliveryResult result = Orders.TryDeliver(dish);
            if (result != null && result.order != null)
            {
                int gained = Score.RegisterSuccess(result.baseScore, result.speedBonus);
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.DeliverSuccess);
                ShowToast("出餐成功！+" + gained + " 分（连击 x" + Score.ComboMultiplier + "）", true);
                return true;
            }

            // 没有匹配订单 = 错误出餐，扣声望
            LoseReputation("没有订单需要这道菜！");
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.DeliverFail);
            return false;
        }

        private void OnOrderExpired(Order order)
        {
            Score.RegisterFailure();
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.OrderExpire);
            LoseReputation("订单超时：" + (order.recipe != null ? order.recipe.recipeName : "未知"));
        }

        private void LoseReputation(string reason)
        {
            if (!IsPlaying) return;
            Reputation--;
            ReputationChanged?.Invoke(Reputation);
            ShowToast(reason + "  声望 -1（剩 " + Reputation + "）", false);
            if (Reputation <= 0) EndGame();
        }

        private void OnTimeUp()
        {
            EndGame();
        }

        private void EndGame()
        {
            Orders.StopSpawning();
            Timer.Stop();
            SetState(GameState.GameOver);
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.GameOver);
        }

        public void ShowToast(string message, bool positive)
        {
            Toast?.Invoke(message, positive);
        }

        private void OnDestroy()
        {
            if (Orders != null) Orders.OrderExpired -= OnOrderExpired;
            if (Timer != null) Timer.Expired -= OnTimeUp;
            if (Instance == this) Instance = null;
        }
    }
}
