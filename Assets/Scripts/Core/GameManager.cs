using UnityEngine;
using UnityEngine.SceneManagement;
using KitchenChaos.Data;
using KitchenChaos.Gameplay;
using KitchenChaos.UI;

namespace KitchenChaos.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState
        {
            Playing,
            GameOver
        }

        public GameState State { get; private set; }
        public bool IsPlaying => State == GameState.Playing;
        public TimerSystem Timer { get; private set; }
        public OrderSystem Orders { get; private set; }
        public ScoreManager Score { get; private set; }
        public DragAndDrop Drag { get; private set; }
        public GameUI UI { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void Initialize(TimerSystem timer, OrderSystem orders, ScoreManager score, DragAndDrop drag, GameUI ui)
        {
            Timer = timer;
            Orders = orders;
            Score = score;
            Drag = drag;
            UI = ui;

            Timer.TimeUp += HandleTimeUp;
            StartGame();
        }

        public void StartGame()
        {
            State = GameState.Playing;
            Score.ResetScore();
            Timer.StartTimer(120f);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (UI != null) UI.ShowMessage("把下方食材拖到灶台，烹饪好后拖到右侧订单。");
        }

        private void HandleTimeUp()
        {
            if (!IsPlaying) return;

            State = GameState.GameOver;
            if (Drag != null) Drag.enabled = false;
            if (UI != null) UI.ShowMessage("时间到！");
        }

        public void Deliver(Order order, IngredientType type)
        {
            if (!IsPlaying) return;

            bool completed;
            int baseScore;
            if (!Orders.TryDeliver(order, type, out completed, out baseScore))
            {
                Score.BreakCombo();
                if (UI != null) UI.ShowMessage("这个订单不需要" + Recipe.GetCookedName(type) + "，连击中断。");
                return;
            }

            if (completed)
            {
                int gained = Score.AddCompletedOrder(baseScore);
                if (UI != null) UI.ShowMessage("订单完成！+" + gained + " 分");
            }
            else if (UI != null)
            {
                UI.ShowMessage("已提交 " + Recipe.GetCookedName(type));
            }
        }

        private void Update()
        {
            if (State != GameState.GameOver) return;

            if (Input.GetKeyDown(KeyCode.R))
            {
                int buildIndex = SceneManager.GetActiveScene().buildIndex;
                if (buildIndex >= 0) SceneManager.LoadScene(buildIndex);
            }
        }
    }
}
