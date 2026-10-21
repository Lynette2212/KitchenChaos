# 厨房狂欢 Kitchen Chaos

一个用 **Unity 6 (6000.6 LTS)** 制作的俯视角烹饪 / 时间管理小游戏，玩法对标《胡闹厨房 / Overcooked》。
全程**无第三方素材依赖**：场景、食材模型、材质、UI 图标与全部音效都由代码程序化生成，克隆后即可运行、可随意修改。

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
| `W A S D` | 移动（俯视，W 朝上） |
| `E` / 空格 | 与面前的柜台交互（取/放、切、装盘、出餐、丢弃） |
| `Esc` / `P` | 暂停 / 继续 |
| 开始界面 | 点击「开始游戏」或按空格 |

- 一次只能手持**一件**物品：一份食材或一个盘子。
- 手持空盘时，对着台面上处理好的食材按 E 会把它装进盘；端满后去出餐口交付。
- 灶台无人也会继续煎，**煎过头会变焦**，焦糊食材无法出餐，请及时取走。

---

## 三、运行方式

1. 用 **Unity Hub** 打开本项目，编辑器版本 **6000.6.5f1**（6000.x LTS 均可）。
2. 打开场景 `Assets/Scenes/KitchenChaos.unity`。
3. 点击顶部 ▶ **Play** 即可游玩。

> 场景里只有一个 `KitchenBuilder` 启动物体，进入播放模式时它会程序化搭出整个厨房
> （地面/墙/全部柜台/玩家/相机/灯光/管理器/HUD）。也可以用编辑器菜单
> **Tools/Kitchen Chaos/Build Kitchen Scene** 把整套场景烘焙进编辑场景。

---

## 四、项目结构

```
Assets/
├── Scenes/KitchenChaos.unity      # 主场景（启动物体 = KitchenBuilder）
├── Resources/Recipes/*.asset      # 6 道配方 ScriptableObject
└── Scripts/
    ├── Data/        IngredientType / Ingredient(状态机) / IngredientCatalog / Recipe / Order
    ├── Gameplay/    PlayerController、IngredientContainer、CuttingCounter、StoveCounter、
    │                PlateHandle、PlateCounter、DeliveryCounter、TrashBin、IInteractable、
    │                InteractableHighlighter、IngredientItem
    ├── Core/        GameManager、OrderSystem、ScoreManager、TimerSystem、KitchenBuilder、
    │                RecipeFactory、CameraFollow、GameplayHUD
    ├── Audio/       AudioManager（代码合成音效）
    ├── Visual/      VisualFactory（代码生成模型/材质/图标）
    └── ...
Assets/Editor/       KitchenSetup（场景/资产生成）、KitchenSmokeTest（无头逻辑自测）
```

### 设计要点
- **食材状态机**：`Raw → Cut → Cooked → Burned`，`CanTransitionTo` 保证合法加工顺序（如生菜只切不煎）。
- **纯代码美术**：`VisualFactory` 用基本体 + 颜色材质拼出食材/柜台；`AudioManager` 用 PCM 采样实时合成
  拾取、切菜、煎烤、出餐、失败等音效。因此仓库不含任何贴图/模型/音频二进制资源，体积小、可自由商用。
- **管线兼容**：材质通过 `Standard`（内置管线）与 URP `UniversalRenderPipelineAsset` 反射自适应创建。

---

## 五、自动校验（无需人工操作）

菜单 **Tools/Kitchen Chaos/Run Smoke Test**，或命令行无头执行：

```powershell
Unity.exe -batchmode -quit -projectPath . `
  -executeMethod KitchenChaos.EditorTools.KitchenSmokeTest.RunBatch
```

它会程序化建厨房并断言完整链路：取食材 → 切/煎状态流转 → 灶台过久变焦 → 装盘组装 →
订单匹配/拒出餐/超时扣声望 → 计分与连击。当前为 **24 项检查全部通过、0 失败**，
结果写入 `Temp/kitchen_smoke_result.txt`。

---

## 六、相对 fixed 分支的修复

- 修复 `Packages/manifest.json` 中指向不存在本地包 `file:../../KitchenChaosComplete` 的损坏依赖。
- 修复 `SetupGuide.cs.meta` 含非十六进制字符的非法 GUID（导致脚本无法编译），并用真实重建流程取代。
- 修复场景 `%TAG!u!` 非法头部与假脚本 GUID 引用；新场景由 Unity 正常序列化生成。
- 用完整的 Overcooked 式玩法替换了原先「单个食材烤熟即可交付」的拖拽原型。
