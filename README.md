# Kitchen Chaos

一个用 Unity 6.6 做的简化版厨房订单微游戏：从食材箱拖食材到灶台，等待烹饪完成，再把熟食拖到右侧订单，在 120 秒内尽量多完成订单。

## 快速开始

1. 用 Unity Hub 打开本工程。
2. 顶部菜单 **Tools → Kitchen Chaos → Open Kitchen Scene**。
3. 点击 Play。

场景进入播放后会自动生成厨房、灶台、食材箱和 UI，不需要手动搭场景。

## 操作

- 左键按住下方食材箱：开始拖拽生食材
- 松手到中间灶台：开始烹饪（2.5 秒）
- 灶台变绿后，按住灶台拖出熟食
- 松手到右侧订单卡片：提交食材
- 订单需要的食材全部提交后完成订单，获得分数和连击倍率
- 倒计时 120 秒；结束后按 **R** 重新开始

## 项目结构

```text
Assets/
├── Scripts/
│   ├── Core/        GameManager, OrderSystem, TimerSystem, ScoreManager, SetupGuide
│   ├── Data/        Recipe (ScriptableObject), Order
│   ├── UI/          GameUI, ScoreUI, OrderUI, OrderPanelManager
│   ├── Gameplay/    IngredientItem, CookingStation, DragAndDrop
│   └── SetupGuide.md
├── Editor/          KitchenSetup（生成场景/菜谱）、KitchenSmokeTest
├── Resources/
│   └── Recipes/     5 个可编辑菜谱资产
└── Scenes/
    └── KitchenChaos.unity
```

## 数据

菜谱在 `Assets/Resources/Recipes/` 里，打开任意 `.asset` 可以改菜名、所需食材和奖励分数。运行时优先读取这些资产；如果文件夹为空，`SetupGuide` 会使用内置默认菜谱。

## 状态

- 编译通过
- PlayMode 冒烟测试通过：3 个灶台、4 个食材箱、3 个订单；烹饪出“熟番茄”；订单交付成功并加分
- 目前使用运行时生成的基础几何体 + OnGUI 界面，属于可玩原型，不包含正式美术、音效和动画
