# Kitchen Chaos 项目总结

## 目标

按 Kitchen Chaos 的 Core / Data / UI / Gameplay 结构，做一个可运行、可扩展的拖拽式厨房订单微游戏，作为后续 Unity 学习与内容迭代的基线。

## 游戏循环

1. 4 个食材箱提供番茄、生菜、芝士、肉。
2. 玩家把生食材拖到 3 个灶台之一。
3. 灶台烹饪 2.5 秒，完成后变绿。
4. 玩家把熟食拖到右侧订单卡片。
5. 订单所需食材全部提交后，订单完成，获得基础分 × 连击倍率。
6. 120 秒倒计时结束，结算分数；按 R 重开。

## 系统职责

| 系统 | 文件 | 职责 |
|---|---|---|
| GameManager | `Core/GameManager.cs` | 游戏状态、开始/结束、订单交付入口、重开 |
| TimerSystem | `Core/TimerSystem.cs` | 120 秒倒计时和 TimeUp 事件 |
| OrderSystem | `Core/OrderSystem.cs` | 生成订单、维护剩余食材、完成后补新订单 |
| ScoreManager | `Core/ScoreManager.cs` | 总分、连击倍率、完成订单数 |
| SetupGuide | `Core/SetupGuide.cs` | 运行时生成相机、灯光、地面、食材箱、灶台、管理器和 UI |
| Recipe | `Data/Recipe.cs` | ScriptableObject 菜谱：名称、食材、奖励分 |
| Order | `Data/Order.cs` | 一单的运行时状态：菜谱、剩余食材 |
| CookingStation | `Gameplay/CookingStation.cs` | 灶台空闲/烹饪/完成状态与进度 |
| IngredientItem | `Gameplay/IngredientItem.cs` | 食材箱上的类型与显示信息 |
| DragAndDrop | `Gameplay/DragAndDrop.cs` | 鼠标拖拽、落点判定、UI 中的拖拽图标 |
| OrderPanelManager | `UI/OrderPanelManager.cs` | 订单卡片布局、屏幕坐标落点检测 |
| OrderUI | `UI/OrderUI.cs` | 单个订单卡片绘制 |
| ScoreUI | `UI/ScoreUI.cs` | 分数/连击/完成数显示 |
| GameUI | `UI/GameUI.cs` | 倒计时、世界标签、提示消息、结算界面 |

## 数据流

- `SetupGuide` 读取 `Resources/Recipes` 下的 Recipe 资产，交给 `OrderSystem` 生成订单。
- `DragAndDrop` 只负责“拿起/放下”；订单交付通过 `OrderPanelManager → GameManager.Deliver → OrderSystem + ScoreManager`。
- `GameUI`、`ScoreUI`、`OrderUI` 只读取状态并绘制，不修改玩法数据。
- 菜谱是静态数据（ScriptableObject），订单是运行时数据（Order 对象），两者分离。

## 验证

- Unity batchmode 编译通过。
- PlayMode 冒烟测试结果：初始 3 个灶台、4 个食材箱、3 个订单；灶台加入番茄后完成烹饪；交付订单后 `CompletedOrders = 1`、分数增加。
- 测试脚本：`Assets/Editor/KitchenSmokeTest.cs`。

## 已知限制

- 场景和物体是运行时用 Cube 生成的，没有正式模型、材质贴图、音效和动画。
- UI 使用 OnGUI，不是正式的 UGUI/TextMeshPro 界面。
- 食材类型是枚举，新增食材需要改代码；菜谱已数据化。
- 没有主菜单、关卡选择、存档和联机。

## 建议下一步

1. 把运行时生成的厨房换成编辑器里摆放的 Prefab 场景，加入模型和材质。
2. 用 UGUI/TextMeshPro 重做订单卡片和 HUD，支持不同分辨率。
3. 增加切菜站、盘子、交付窗口等 Kitchen Chaos 经典机制。
4. 加入音效、粒子、简单动画和开始/结束菜单。
5. 再考虑把该工程的经验迁移到暗区式搜打撤项目。
