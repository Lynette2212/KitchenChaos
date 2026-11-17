# 厨房狂欢 Kitchen Chaos

一个用 **Unity 6 (6000.6.5f1)** 制作的俯视角烹饪 / 时间管理小游戏，玩法对标《胡闹厨房 / Overcooked》。
厨房、柜台、厨师、顾客和相机预置在场景中，可在 Scene 视图直接调整并保存；运行时只初始化游戏逻辑。模型、贴图和音频使用项目内资源，素材许可见各素材目录。

> 当前分支 `feature/fullgame-assets`：在 `fixed` 原型基础上补全了完整的烹饪玩法、组装交付、订单/声望/连击系统、程序化素材与音效，并修复了 `fixed` 分支的编译损坏。

---

## 一、游戏玩法

你是厨房主厨，在限定时间内根据右上角不断到来的**订单小票**做菜：取食材 → 切 / 煎 → 装盘组装 → 送到出餐口。订单有倒计时，超时会扣声望；在时间结束前尽量多、尽量快地出餐拿高分（连续出餐有连击加成）。

### 食材处理链
| 操作 | 位置 | 结果 |
|------|------|------|
| 取食材 | 食材柜（无限供应） | 获得生食材 |
| 切 | 切菜台（按住 E 看进度条） | 生 → 已切 |
| 煎 | 灶台（自动煎，含熟/糊） | 生/已切 → 已煎 → 过久变焦 |
| 装盘 | 餐盘台取空盘，对台面食材按 E | 多份食材组装到一个盘子 |
| 出餐 | 出餐口（手持成品盘按 E） | 匹配订单得分，不匹配/焦糊拒收 |
| 丢弃 | 垃圾桶 | 扔掉手里拿错/做坏的东西 |

### 内置菜谱（6 道）
田园沙拉（番茄+卷心菜，均切）、香煎肉饼（肉饼煎）、芝士汉堡（面包+肉饼煎+芝士）、
蘑菇番茄（蘑菇切+煎、番茄切）、蘑菇汉堡、终极汉堡（面包+肉饼+芝士+番茄+蘑菇）。

---

## 二、操作方式

| 按键 | 作用 |
|------|------|
| `W A S D` / 方向键 | 按相机方向移动 |
| `E` | 与附近柜台交互；长按切菜 |
| `X` | 取回未切好的食材 |
| `Shift` / 空格 | 短距离冲刺 |
| `Esc` / `P` | 暂停 / 继续 |
| `F11` / `Alt+Enter` | 切换全屏 |
| 开始界面 | 点击「开始营业」或按 Enter |

- 一次只能手持**一件**物品：一份食材或一个盘子。
- 手持空盘时，对着台面上处理好的食材按 E 会把它装进盘；端满后去出餐口交付。
- 灶台无人也会继续煎，**煎过头会变焦**，焦糊食材无法出餐，请及时取走。

---

## 三、运行方式

1. 用 **Unity Hub** 打开本项目，编辑器版本 **6000.6.5f1**。
2. 打开场景 `Assets/Scenes/KitchenChaos.unity`，或菜单 **Tools/Kitchen Chaos/Open Kitchen Scene**。
3. 停止 Play，在 **Scene** 视图选中对象，用 **W / E / R** 切换移动、旋转、缩放工具，**Ctrl+S** 保存。
4. 点击 **Play** 开始测试；退出 Play 后仍使用保存的编辑布局。Play 期间临时调整不会自动保存。

`KitchenChaos` 下的主要层级：
- `Stations`：完整柜台对象。移动柜台根对象，让模型、碰撞体和食材锚点一起移动。
- `Actors/Player`：厨师出生位置和朝向；`ChefVisual` 可单独调整模型大小、角度。
- `Actors/CustomerWindow`：取餐窗及顾客。
- `Camera/Main Camera`：游戏相机，位置、旋转及 Camera 的 Size 均可直接编辑。
- `Environment`、`Lighting`、`Systems`：环境、灯光及运行系统。

**Tools/Kitchen Chaos/Make Scene Editable** 只用于首次转换空的厨房启动根对象；已经转换的场景不会重建或覆盖布局。转换前原场景备份在 `Backups/SceneAuthoring`，生成材质等资源保存在 `Assets/SceneResources/KitchenChaos`。

---

## 四、项目结构

```
Assets/
├── Scenes/KitchenChaos.unity      # 可直接编辑的预置主场景
├── SceneResources/KitchenChaos/   # 持久化的场景材质等资源
├── Resources/Recipes/*.asset      # 6 道配方 ScriptableObject
└── Scripts/
    ├── Data/        IngredientType / Ingredient(状态机) / IngredientCatalog / Recipe / Order
    ├── Gameplay/    PlayerController、IngredientContainer、CuttingCounter、StoveCounter、
    │                PlateHandle、PlateCounter、DeliveryCounter、TrashBin、IInteractable、
    │                InteractableHighlighter、IngredientItem
    ├── Core/        GameManager、OrderSystem、ScoreManager、TimerSystem、KitchenBuilder、
    │                RecipeFactory、TopDownCamera、GameplayHUD
    ├── Audio/       AudioManager（资源音频、音源与合成音效）
    ├── Visual/      ChefModel、CustomerView、VisualFactory（模型实例与运行时图标）
    └── ...
Assets/Editor/       KitchenSetup（场景/资产生成）、KitchenSmokeTest（无头逻辑自测）
```

### 设计要点
- **食材状态机**：`Raw → Cut → Cooked → Burned`，`CanTransitionTo` 保证合法加工顺序（如生菜只切不煎）。
- **场景布局与行为分离**：静态厨房和角色视觉保存在 Scene 中，`KitchenBuilder.Start` 只绑定系统；出生点、相机姿态与角色动画基准来自保存的 Transform。
- **资源持久化**：编辑器生成的临时材质转为项目资源；运行时取还库存盘只切换预置对象显隐，保留各盘的编辑布局。
- **渲染与输入**：使用 URP 与 Input System；素材来源及授权以各素材目录中的许可说明为准。

---

## 五、自动回归

先关闭占用此项目的编辑器，将 `Unity.exe` 替换为本机 Unity 编辑器路径。测试会自行退出，不加 `-quit`：

```powershell
Unity.exe -batchmode -nographics -projectPath . `
  -executeMethod KitchenChaos.EditorTools.KitchenSmokeTest.RunBatch `
  -logFile Logs/campaign-tests.log
```

它打开正式场景，检查取食材、切煎、装盘、订单与计分，结果写入 `Logs/campaign-smoke.txt`。完整玩家按键检查使用 `KitchenSmokeTest.RunWindowed`，不加 `-batchmode` 或 `-nographics`。

场景持久化专项使用 `KitchenSceneAuthoringTests.RunBatch`，参数同上；它在独立临时场景副本中修改布局，验证保存重开、进出 Play 与出生点复位，结束后清理副本并重新打开正式场景，不改正式布局。自动回归之外仍需在编辑器中检查渲染和实际操作。

---

## 六、相对 fixed 分支的修复

- 修复 `Packages/manifest.json` 中指向不存在本地包 `file:../../KitchenChaosComplete` 的损坏依赖。
- 修复 `SetupGuide.cs.meta` 含非十六进制字符的非法 GUID（导致脚本无法编译），并用真实重建流程取代。
- 修复场景 `%TAG!u!` 非法头部与假脚本 GUID 引用；新场景由 Unity 正常序列化生成。
- 用完整的 Overcooked 式玩法替换了原先「单个食材烤熟即可交付」的拖拽原型。
