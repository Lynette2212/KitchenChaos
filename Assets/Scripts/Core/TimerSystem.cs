using System;
using UnityEngine;

namespace KitchenChaos.Core
{
    public class TimerSystem : MonoBehaviour
    {
        public float Duration = 120f;
        public float TimeRemaining { get; private set; }
        public bool IsRunning { get; private set; }

        public event Action TimeUp;

        public void StartTimer(float duration)
        {
            Duration = Mathf.Max(1f, duration);
            TimeRemaining = Duration;
            IsRunning = true;
        }

        public void StopTimer()
        {
            IsRunning = false;
        }

        private void Update()
        {
            if (!IsRunning) return;

            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                IsRunning = false;
                TimeUp?.Invoke();
            }
        }
    }
}
