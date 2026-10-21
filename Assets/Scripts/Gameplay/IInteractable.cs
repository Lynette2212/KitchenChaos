using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 玩家可以拿取/放置食材的对象实现此接口（工作台、食材柜、盘子、垃圾桶、出餐台）。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>主交互：E 键。返回是否产生了有效交互（用于音效/反馈）。</summary>
        bool Interact(PlayerController player);

        /// <summary>世界中悬浮提示文本。</summary>
        string GetPrompt();

        /// <summary>交互世界位置（用于高亮与提示定位）。</summary>
        Transform GetTransform();
    }

    /// <summary>需要长按加工的工作台（切菜台、灶台）额外实现。</summary>
    public interface IProcessingStation
    {
        bool IsProcessing { get; }
        float Progress01 { get; }
    }
}
