# 设置指南

## 打开与运行

1. Unity Hub 打开 `KitchenChaos` 工程（Unity 6000.6.5f1）。
2. 菜单 **Tools → Kitchen Chaos → Open Kitchen Scene**。
3. 打开 `Assets/Scenes/KitchenChaos.unity` 后按 Play。

场景里只有一个 `KitchenChaos` 物体，厨房是在运行时由 `SetupGuide` 生成的，所以编辑模式下看到空场景是正常的。

## 自动生成的内容

- 相机、方向光和地面
- 4 个食材箱：番茄、生菜、芝士、肉
- 3 个灶台（烹饪时间 2.5 秒）
- 管理器：GameManager、TimerSystem、OrderSystem、ScoreManager
- UI：GameUI、ScoreUI、OrderPanelManager
- 拖拽系统：DragAndDrop

## 修改菜谱

打开 `Assets/Resources/Recipes/` 下的任意 `.asset`：

- `recipeName`：显示在订单卡片上的菜名
- `ingredients`：需要的熟食类型
- `score`：完成该订单的基础分

如果删光菜谱资产，运行时回退到 `SetupGuide.CreateDefaultRecipes()` 的内置菜谱。

## 调整布局/难度

- 灶台烹饪时间：`SetupGuide.StationCookTime`（默认 2.5 秒）
- 游戏时长：`GameManager.StartGame()` 里的 `Timer.StartTimer(120f)`
- 同时出现的订单数：`OrderSystem.MaxActiveOrders`（默认 3）
- 连击上限：`ScoreManager.AddCompletedOrder()` 中的 `Mathf.Min(5, ...)`

## 常见问题

**进入 Play 后没有厨房？**
确认场景里有 `KitchenChaos` 物体并挂了 `SetupGuide`；用菜单重新生成场景。

**菜单点 Play 后 UI 被挡住？**
订单卡片是屏幕右侧的 OnGUI 卡片；如果分辨率很小，可调 `OrderPanelManager` 的 `CardWidth` / `RightMargin`。

**想重新生成场景？**
菜单 **Tools → Kitchen Chaos → Create Kitchen Scene**，会重建 `Assets/Scenes/KitchenChaos.unity` 并加入 Build Settings。
