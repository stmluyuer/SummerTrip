# SummerTrip Godot 架构设计

## 1. 文档定位

本文档定义 `SummerTrip` 从 Ren'Py 迁移到 Godot 后的目标架构、模块职责和依赖边界。它回答“最终工程应该怎样组织”，不代替迁移内容清单或功能验收表。

相关文档：

- [Godot 迁移清单](godot_migration_inventory.md)：说明从原工程迁移什么。
- [Godot 实施路线图](godot_implementation_roadmap.md)：说明按什么顺序实现。
- [Godot 功能对等验收表](godot_feature_checklist.md)：说明怎样判断功能完成。
- `migration/manifests/`：保存角色、变量和资源的一对一迁移映射。
- `migration/manifests/asset_licenses.tsv`：保存资源授权核实状态和再发布范围。

当前状态：`M0` 至 `M5` 已完成；Godot 4.7.1 .NET 工程、Dialogue Manager v3.10.5、完整序章六条路线、三个结尾、舞台表现和无槽位 UI 的存读档闭环已验证，下一阶段是 `M6` 菜单与设置。

## 2. 设计目标与边界

### 2.1 目标

1. 舒适地支持以对白、选择和分支为主的 AVG 玩法。
2. 保留后续增加探索、小游戏或其他游戏模式的空间。
3. 剧情文本不直接依赖场景节点路径、像素坐标或资源文件路径。
4. 支持手动存档、自动存档、快速存档和按句回退。
5. 读取存档时直接恢复快照，不从章节开头重放剧情。
6. 让剧情状态、舞台表现、界面状态和跨存档数据各自有明确归属。
7. 先完成可验证的序章竖切，再逐步增加完整菜单和高级功能。

### 2.2 当前范围

- 内容范围：`0.1.0-prologue` 序章。
- 设计分辨率：1920×1080。
- 编辑器与脚本基线：Godot 4.7.1 .NET；项目自有运行时代码和测试统一使用 C#，第三方 Dialogue Manager 保持上游 GDScript/C# 混合实现。
- 首要发布目标：Windows PC。
- 输入基线：鼠标和键盘。
- 对话源格式：Dialogue Manager `.dialogue`。
- 原 Ren'Py 工程继续保留，作为迁移校对基准。

### 2.3 暂不纳入首个竖切

- 移动端适配。
- Linux 和 macOS 导出。
- 云存档。
- 语音系统。
- CG 鉴赏、成就和章节选择。
- 完整回退界面。
- 对未使用素材和全部字体字重的一次性迁移。

## 3. 核心设计原则

### 3.1 持久外壳，按模式替换内容

`Main` 是唯一常驻的游戏入口。它保留全局 UI 和当前会话，在 `ModeHost` 下装载或卸载 `VNMode`、未来的探索模式或小游戏模式。

模式切换不应销毁设置服务、存档服务或当前会话。模式内部的舞台、输入和临时动画状态则随模式释放。

### 3.2 场景拥有运行时对象

`GameSession`、`VNDirector` 和 `StageController` 由当前游戏会话或 `VNMode` 持有，通过初始化参数和信号连接协作。它们不作为随处可写的全局对象。

只有满足以下条件的服务才考虑 Autoload：

1. 生命周期确实跨越所有场景。
2. 服务主要管理自己的数据。
3. 业务场景不需要知道服务内部节点结构。

### 3.3 剧情只使用语义命令

剧情文件允许引用：

- `autumn_garden` 这类背景 ID。
- `minoriko`、`neutral`、`right` 这类角色、表情和位置 ID。
- `scene_blend` 这类转场 ID。
- `GameState.minoriko_affection` 这类剧情状态。

剧情文件不得引用：

- `res://assets/...` 资源路径。
- `$Stage/Characters/Left` 之类节点路径。
- 具体像素坐标。
- 存档文件路径或存档槽编号。
- 菜单、输入设备或窗口设置。

### 3.4 单一事实来源

- 原版事实来源：`game/*.rpy`。
- 资源路径映射来源：`migration/manifests/assets.tsv`。
- 资源授权状态来源：`migration/manifests/asset_licenses.tsv`。
- 角色和变量映射来源：`migration/manifests/characters.tsv`、`variables.tsv`。
- 舞台位置、叠色、动画和转场参数来源：`migration/manifests/presentation.tsv`。
- Godot 开工后的正式剧情来源：`godot/content/dialogue/*.dialogue`。
- 运行时资源映射来源：Godot `ContentCatalog`。

序章已经从 `migration/dialogue/prologue.dialogue` 移动到 `godot/content/dialogue/prologue.dialogue`；旧路径不再保留第二份可编辑副本。

### 3.5 快照恢复，不重放副作用

存档保存当前剧情位置、剧情变量和舞台状态。保存的剧情位置是 Dialogue Manager 的静态翻译键，也就是 `.dialogue` 中的 `[ID:...]`，而不是插件生成的运行时 `DialogueLine.id`。

读取时先把静态翻译键唯一解析为运行时行 ID，完成全部预检后再提交会话和舞台快照。读取过程不得从章节开头重放已经执行过的 `do` 或 `set`，否则好感、信任和谜团值会重复增加。

## 4. 目标目录

```text
godot/
├─ project.godot
├─ addons/
│  └─ dialogue_manager/                 # 第三方依赖，不修改插件源码
├─ third_party/
│  └─ dialogue_manager.NOTICE.md        # 上游版本、提交与许可证位置
├─ app/
│  ├─ main.tscn
│  └─ Main.cs
├─ core/
│  ├─ dialogue/
│  │  ├─ DialogueContext.cs
│  │  └─ DialogueGateway.cs
│  ├─ save/
│  │  ├─ SaveSchema.cs
│  │  └─ SaveService.cs
│  ├─ session/
│  │  ├─ GameSession.cs
│  │  └─ ProfileData.cs
│  └─ settings/
│     └─ SettingsService.cs
├─ features/
│  ├─ vn/
│  │  ├─ vn_mode.tscn
│  │  ├─ VNMode.cs
│  │  ├─ VNDirector.cs
│  │  ├─ stage/
│  │  │  ├─ stage.tscn
│  │  │  └─ StageController.cs
│  │  └─ ui/
│  │     ├─ dialogue_box.tscn
│  │     └─ DialogueBox.cs
│  ├─ menu/
│  │  ├─ title_menu.tscn
│  │  ├─ TitleMenu.cs
│  │  ├─ game_menu.tscn
│  │  └─ GameMenu.cs
│  ├─ save_ui/
│  │  ├─ save_load_screen.tscn
│  │  ├─ SaveLoadScreen.cs
│  │  ├─ save_slot.tscn
│  │  └─ SaveSlot.cs
│  ├─ history/
│  │  ├─ history_screen.tscn
│  │  └─ HistoryScreen.cs
│  └─ settings/
│     ├─ settings_screen.tscn
│     └─ SettingsScreen.cs
├─ content/
│  ├─ dialogue/
│  │  └─ prologue.dialogue
│  └─ catalog/
│     ├─ ContentCatalog.cs
│     └─ content_catalog.tres
├─ assets/
│  ├─ backgrounds/
│  ├─ characters/
│  ├─ fonts/
│  └─ ui/
├─ ui/
│  └─ themes/
│     └─ vn_theme.tres
└─ tests/
   ├─ SmokePrologue.cs
   ├─ SaveRoundtrip.cs
   └─ ValidateContent.cs
```

目录按功能组织，场景脚本尽量与场景放在一起。不要建立一个混放所有代码的 `scripts/` 目录，也不要提前创建没有实际职责的空文件。

## 5. 场景树

### 5.1 常驻入口

```text
Main
├─ ModeHost
│  └─ VNMode                        # 当前模式，可被其他模式替换
└─ GlobalUI
   ├─ MenuLayer
   ├─ NotificationLayer
   └─ ConfirmLayer
```

`Main` 的职责：

- 创建或接收当前 `GameSession`。
- 装载和卸载游戏模式。
- 协调标题菜单、继续游戏和返回标题。
- 把会话与共享服务注入当前模式。
- 不直接显示对白或操作角色立绘。

### 5.2 AVG 模式

```text
VNMode
├─ Stage
│  ├─ Background
│  ├─ Overlay
│  ├─ CharacterLayer
│  │  ├─ Left
│  │  ├─ Center
│  │  └─ Right
│  └─ TransitionLayer
├─ DialogueUI
│  ├─ NameLabel
│  ├─ DialogueLabel
│  ├─ ContinueIndicator
│  ├─ ChoiceContainer
│  └─ QuickMenu
└─ VNDirector
```

`VNMode` 是组合根，只负责组装和注入依赖。具体推进规则在 `VNDirector`，视觉表现归 `StageController`，控件显示归 `DialogueUI`。

## 6. 模块职责

| 模块 | 拥有的数据 | 允许调用 | 不负责 |
| --- | --- | --- | --- |
| `Main` | 当前模式、当前会话引用 | 模式、全局 UI、共享服务 | 对白逐句推进、舞台动画 |
| `GameSession` | 玩家名、路线值、剧情标记、当前章节、历史、游玩时间 | 纯数据转换和校验 | 文件读写、节点操作 |
| `ProfileData` | 已读对白、已解锁结局、未来的 CG/成就 | 合并和序列化 | 当前存档路线值 |
| `DialogueGateway` | Dialogue Manager 版本适配 | 插件公开 API | UI、舞台节点、存档文件 |
| `DialogueContext` | `GameState`、`Presentation` 对话命名空间 | 会话和舞台公开接口 | 业务逻辑 |
| `VNDirector` | 当前对白、下一位置、运行状态 | Gateway、Stage、DialogueUI | 磁盘路径、资源文件路径 |
| `StageController` | 当前背景、叠色、角色槽和转场状态 | ContentCatalog、Tween/AnimationPlayer | 剧情分支、存档槽 |
| `DialogueUI` | 当前显示文本和控件临时状态 | 通过信号向 Director 发请求 | 修改剧情变量 |
| `SaveService` | 槽位索引、存档与配置文件 | `FileAccess`、`DirAccess` | 从场景树主动抓取状态 |
| `SettingsService` | 显示、文字速度、自动等待、音量设置 | Godot 窗口和音频 API | 剧情状态 |
| `ContentCatalog` | 语义 ID 到 Godot 资源的映射 | `ResourceLoader` | 运行时进度 |

## 7. 依赖方向和通信

依赖只允许从组合层指向具体能力：

```text
Main
├─ GameSession
├─ SaveService
└─ VNMode
   ├─ VNDirector
   │  ├─ DialogueGateway ──> Dialogue Manager
   │  ├─ DialogueContext ──> GameSession + StageController
   │  ├─ StageController ──> ContentCatalog
   │  └─ DialogueUI
   └─ Stage
```

规则：

1. UI 通过 `advance_requested`、`response_selected`、`save_requested` 等信号表达意图。
2. `VNDirector` 决定意图在当前状态是否有效。
3. `StageController` 完成动画后发出完成信号或返回可等待的调用。
4. `SaveService` 接收已经构造好的基础类型快照；它不搜索 `VNDirector` 或 `Stage`。
5. 加载时 `SaveService` 返回已校验的数据，由 `Main` 和 `VNDirector` 执行恢复。
6. 跨模块访问不得依赖硬编码的远距离 `NodePath`。

## 8. Dialogue Manager 对接

### 8.1 版本隔离

只有 `DialogueGateway.cs` 可以直接调用 Dialogue Manager API。`VNDirector` 只依赖以下项目内接口：

```text
get_line(resource_id, line_or_title_id, context, mutation_mode)
get_next_line(current_line, context)
choose_response(response, context)
```

具体方法签名可以随锁定的插件版本调整，但不得扩散到舞台、UI 和存档模块。

### 8.2 状态注入

现有 `prologue.dialogue` 使用 `GameState.*` 和 `Presentation.*`。它们是剧情脚本的公开命名空间，不要求是 Godot Autoload。

`DialogueContext.cs` 构造类似以下额外状态：

```csharp
new Godot.Collections.Array<Variant>
{
    new Godot.Collections.Dictionary
    {
        ["GameState"] = gameSession,
        ["Presentation"] = stageController,
    },
};
```

Dialogue Manager v3.10.5 支持把节点、对象或字典作为 `extra_game_states` 传入。这样可以保留现有剧情语法，同时限制变量和舞台控制器的生命周期。

### 8.3 推进方式

正式运行时由 `VNDirector` 主动逐行请求 `DialogueLine`，不直接依赖插件示例气泡：

1. 新章节由 Gateway 根据 title 请求第一条可显示行；读档由 Gateway 先把保存的静态翻译键解析为运行时行 ID，再请求当前行。
2. Director 把角色名和正文交给 `DialogueUI`。
3. UI 完成打字后进入等待推进状态。
4. 有选项时进入选择状态，禁止普通推进。
5. 选择后以选项的下一 ID 请求下一行。
6. 没有下一行时结束当前章节。

自定义 UI 可以使用插件提供的 `DialogueLabel` 处理打字、暂停和内联命令，但输入状态仍由 `VNDirector` 统一管理。

### 8.4 稳定 ID

- 每条可显示对白和每个选项必须保留唯一 `[ID:...]`。
- 文案修改不得顺手重命名稳定 ID。
- `[ID:...]` 在 Dialogue Manager v3.10.5 中对应 `DialogueLine.translation_key`，本文统一称为“静态行 ID”。
- 存档保存当前可显示行的 `translation_key`，不得保存数组下标、插件运行时 `id` 或临时 `next_id`。
- Gateway 恢复时调用 `DialogueManager.static_id_to_line_ids(resource, static_id)`；结果必须恰好为一条，否则返回可定位错误。
- Gateway 再以解析出的运行时 ID 调用 `get_next_dialogue_line(..., DialogueManager.MutationBehaviour.Skip)`，避免恢复过程执行 mutation。
- 实现存档前必须用锁定版本验证静态 ID 到运行时 ID 的一对一映射、当前行恢复和变量不重复增加。
- 如果插件升级改变这一行为，只修改 Gateway 和存档恢复适配，不改剧情业务层。

## 9. 状态模型

### 9.1 `GameSession`

每个存档独立的数据：

```text
player_name
minoriko_affection
shizuha_trust
autumn_mystery
story_resource_id
chapter_id
current_static_line_id
history
play_time_seconds
chapter_flags
```

`GameSession` 只接受 JSON 可表示的基础类型，并提供明确的 `to_dict()`、`restore_from_dict()` 和 `reset()`。

运行时归属与 JSON 顶层结构不要求一一对应。生成存档快照时，当前位置、历史和游玩时间从 `GameSession` 提取到 `story`、`history` 和 `play_time_seconds` 顶层字段，`session` 对象只保存玩家名、路线数值和章节标记，避免同一字段在存档中出现两份。

`VNDirector` 持有插件返回的临时 `DialogueLine` 对象；当一行进入可交互的稳定状态时，它把该行的 `translation_key` 写回 `GameSession.current_static_line_id`。历史记录最多保留最近 250 条。

### 9.2 `ProfileData`

跨存档保留的数据：

```text
read_line_ids
unlocked_endings
unlocked_cgs
achievements
profile_version
```

当前序章首先使用 `read_line_ids`。读取旧存档时，只把存档内的已读 ID 合并进 Profile，不能因为读取较早存档而清除后来读过的内容。

### 9.3 舞台状态

`StageController` 可导出的快照：

```text
background_id
overlay_id
overlay_animation_id
characters[]
transition_id
music_id
music_position
```

`characters[]` 至少包含角色 ID、表情 ID、位置 ID 和可见状态。当前没有音乐调用，但保留可空字段，避免未来修改存档顶层结构。

### 9.4 运行时状态

`VNDirector` 把“对白交互状态”和“外层操作状态”分开，避免打开菜单或开始保存后丢失原先是在等待推进还是等待选择：

```text
InteractionState
├─ NONE
├─ TYPING
├─ WAITING_ADVANCE
├─ CHOOSING
└─ FINISHED

OperationState
├─ BOOTING
├─ ACTIVE
├─ TRANSITIONING
├─ MENU_PAUSED
├─ SAVING
└─ RESTORING_SAVE
```

规则：

- 只有交互状态为 `WAITING_ADVANCE` 或 `CHOOSING` 时可以生成存档快照。
- 操作状态为 `ACTIVE` 或 `MENU_PAUSED` 时可以发起保存；从游戏菜单保存完成后回到 `MENU_PAUSED`，关闭菜单后回到 `ACTIVE`，交互状态保持不变。
- 在 `TYPING` 中请求快速保存时，先完成当前句显示并进入 `WAITING_ADVANCE`，再生成快照。
- 进入菜单时如果仍在打字，先完成当前句；选择界面打开菜单后仍保留 `CHOOSING`。
- 转场、写入和恢复过程中拒绝新的保存或推进请求。

## 10. 存读档设计

### 10.1 存档格式

正式存档使用 JSON 元数据和独立 PNG 截图。JSON 只保存基础类型，不保存 `Node`、`Resource`、`Callable` 或 `NodePath`。

```json
{
  "schema_version": 1,
  "game_version": "0.1.0-prologue",
  "saved_at": "ISO-8601 timestamp",
  "story": {
    "resource_id": "prologue",
    "chapter_id": "prologue",
    "static_line_id": "ST_AHW_L010"
  },
  "session": {},
  "presentation": {},
  "history": [],
  "read_line_ids": [],
  "play_time_seconds": 0.0,
  "screenshot_path": "manual/slot_001.png"
}
```

实际 schema 由 `SaveSchema.cs` 定义和校验；上例只说明顶层边界。

### 10.2 文件布局

```text
user://
├─ profile.json
├─ settings.json
└─ saves/
   ├─ index.json
   ├─ manual/
   ├─ quick/
   └─ auto/
```

- 手动档每页 3×2，最多 9 页。
- 快速档使用 1 个独立槽位。
- 自动档使用 6 个轮换槽位。
- 截图尺寸为 384×216。
- 文件名由 `SaveService` 统一生成，UI 不拼接磁盘路径。
- `index.json` 只是可重建的槽位缓存；存档 JSON 才是每个槽位的事实来源。
- 自动存档只在配置的主要 title 已执行完前置 mutation/转场、第一条可交互行稳定显示后触发；结尾自动档在结尾最后一行稳定显示后触发，不在 title 标记本身写入。

### 10.3 防损坏写入

每次保存采用“不可变截图先落盘、JSON 最后提交”的替换式写入：

1. 为本次保存生成新的代次号，并把截图写入同一槽位目录下的唯一文件，例如 `slot_001.g000042.png`。
2. 关闭截图文件并验证 PNG 可读取、尺寸正确。
3. 写入引用该代次截图的临时 JSON。
4. 关闭 JSON 后重新读取、解析并按 `SaveSchema` 校验。
5. 保留上一份有效 JSON 作为备份，再用已验证的临时 JSON 替换正式 JSON；正式 JSON 的替换是槽位提交点。
6. 用相同的临时文件与替换流程更新可重建的 `index.json`。
7. 提交成功后再尽力清理旧截图和孤立临时文件；清理失败不破坏新旧任一有效存档。

如果在 JSON 提交前中断，新截图只是可安全清理的孤立文件，旧 JSON 仍引用旧截图；不会出现旧元数据指向被覆盖截图的情况。`screenshot_path` 必须由 `SaveService` 生成并限制在对应槽位目录内，不能直接信任外部 JSON 中的任意路径。覆盖或删除存档必须经过 UI 确认。

### 10.4 读取顺序

读取分成“预检”和“提交”两个阶段：

1. `VNDirector` 把操作状态切换到 `RESTORING_SAVE`，冻结推进和保存输入，同时保留当前可恢复的内存快照。
2. `SaveService` 解析并校验 schema、字段类型、游戏版本和所有受控相对路径。
3. 根据 `story.resource_id` 载入对白资源；Gateway 把 `story.static_line_id` 解析为恰好一个运行时 ID。
4. 在新的候选 `GameSession` 中恢复数据，并验证所有舞台语义 ID、角色位置和资源都可解析。
5. 使用候选会话作为 `GameState`，以 `MutationBehaviour.Skip` 请求当前 `DialogueLine`，确认其 `translation_key` 与存档一致，并重建可用选项。
6. 只有以上步骤全部成功后才进入提交阶段：替换当前会话、无动画恢复舞台、显示完整当前行或当前选项。
7. 根据返回行是否含可用选项，把交互状态设置为 `WAITING_ADVANCE` 或 `CHOOSING`，操作状态恢复为 `ACTIVE`。
8. 最后构造合并了存档 `read_line_ids` 的候选 Profile；候选文件原子写入成功后，才替换内存中的 `ProfileData`。

提交阶段任一步失败时恢复进入读档前的内存快照，不替换 Profile，并回到可操作的菜单状态。失败不会改写原存档文件。

### 10.5 回退

回退与磁盘存档共用快照字段，但使用内存环形缓冲区。每次稳定进入一条对白或一个选择点时记录轻量快照；不为每句话写磁盘或生成截图。

回退在完整存读档闭环通过后再实现。

## 11. 舞台和资源目录

`ContentCatalog` 将语义 ID 映射为 Godot 资源：

```text
backgrounds.autumn_garden -> Texture2D
characters.minoriko.neutral -> Texture2D
positions.right -> x=0.72, y=1.02, anchor=(0.5,1.0), scale=0.66
overlays.autumn_glow -> color=#d98a2b22
overlay_animations.leaf_shiver -> initial=(0.25,0.22), cycle_leg=(1.6,0.12,0.24), full_cycle=3.2
transitions.scene_blend -> fade_out=0.35, hold=0.2, fade_in=0.55
chapters.prologue -> title_key=chapter.prologue, autosave_titles=[aki_house_wakeup,maple_tree_investigation,route_choice]
```

约束：

1. 剧情和存档只保存语义 ID。
2. 目录负责验证 ID 是否存在。
3. 未知 ID 必须给出包含资源 ID 和剧情位置的错误，不能静默显示错误素材。
4. 第一阶段只迁移当前竖切所需的 P0 素材。
5. `game/gui/phone/**`、未使用立绘和编辑源图继续延后。
6. `asset_licenses.tsv` 必须覆盖 `assets.tsv` 中所有 `action=copy` 的素材；未核实记录只能用于本地验证，正式发布前必须补全并通过发布门禁。

## 12. UI、输入和分辨率

### 12.1 UI 边界

- `DialogueUI` 只处理对话显示和输入意图。
- 菜单 UI 不直接写存档，由 `SaveService` 提供操作结果。
- 通知和确认弹窗属于 `GlobalUI`，不跟随 `VNMode` 销毁。
- 主题参数集中在 `vn_theme.tres`，避免在多个场景重复设置字体和颜色。

### 12.2 输入

输入动作在 `project.godot` 统一定义：

```text
vn_advance
vn_menu
vn_hide_ui
vn_skip
vn_auto
vn_rollback
vn_quick_save
vn_quick_load
vn_screenshot
ui_accept
ui_cancel
```

默认绑定沿用原版：鼠标左键/Enter/Space 推进，鼠标右键/Esc 打开或关闭游戏菜单，Ctrl 快进，H 隐藏 UI，S 截图，鼠标滚轮上滑回退。Director 根据当前运行状态决定动作是否生效；控件不直接监听所有物理按键，设置页以后可以调整绑定。

### 12.3 分辨率

已确认基线：

- 设计区域：1920×1080。
- 2D 缩放模式：`canvas_items`。
- 宽高比：`keep`，非 16:9 显示器使用黑边。
- UI 使用 Control 锚点和容器，不依赖窗口实际像素。

选择 `keep` 是为了保护现有固定 16:9 背景和立绘构图。该决定已于 2026-07-28 确认并显式写入 `project.godot`；16:10 和 21:9 只需验证黑边与 UI 锚点，不扩展舞台可见区域。

## 13. Autoload 策略

首个版本允许的 Autoload：

| 名称 | 原因 |
| --- | --- |
| `DialogueManager` | Dialogue Manager 插件提供的运行入口 |
| `SaveService` | 跨标题菜单和游戏模式访问，且只管理自己的文件与索引 |
| `SettingsService` | 跨场景保持并应用用户设置 |

暂不设为 Autoload：

- `GameSession`
- `ProfileData`
- `VNDirector`
- `StageController` / `Presentation`
- `ContentCatalog`
- `HistoryManager`
- `AudioService`

`ProfileData` 可由 `SaveService` 加载并交给 `Main` 持有。当前项目没有音频调用，等音频实际进入工程后再判断是否需要跨模式服务。

## 14. 验证策略

首批自动验证不新增测试框架依赖，使用 Godot headless 脚本完成：

| 文件 | 验证内容 |
| --- | --- |
| `tests/SmokePrologue.cs` | `.dialogue` 可编译、入口存在、全部静态 ID 唯一且能各自解析为一个运行时 ID、能到达第一处选择 |
| `tests/SaveRoundtrip.cs` | 快照写入、进程内清空、静态 ID 恢复后字段完全一致，失败读档不改变当前会话或 Profile |
| `tests/ValidateContent.cs` | 剧情使用的背景、角色、位置、叠色和转场 ID 均存在 |

人工验证以 `godot_feature_checklist.md` 的回归路线为准。存档 UI 开始前，必须先通过稳定 ID 恢复和变量不重复增加的专项验证。

## 15. 扩展路径

- 新章节：在 `content/dialogue/` 增加章节文件并复用同一 Context。
- 探索玩法：在 `ModeHost` 下替换为 `ExplorationMode`，保留同一个 `GameSession`。
- 小游戏：通过明确的输入/输出数据进入和退出，不让小游戏直接修改对话 UI。
- 音频：舞台快照增加实际 BGM 状态后，再引入跨模式音频服务。
- CG/成就：写入 `ProfileData`，不混入单个存档路线状态。
- 本地化：稳定对白 ID 继续作为翻译键，不以中文原文作为持久标识。

## 16. 决策记录

| ID | 决策 | 当前建议 | 状态 |
| --- | --- | --- | --- |
| ARCH-001 | Godot 与 Dialogue Manager 版本组合 | `Godot 4.7.1-stable` .NET + `Dialogue Manager v3.10.5` | 已确认 |
| ARCH-002 | 非 16:9 显示策略 | `canvas_items` + `keep` | 已确认 |
| ARCH-003 | 树下调查段的室内背景 | 迁移版保留原 `autumn_room` 表现 | 已确认 |
| ARCH-004 | 正式发布平台 | 第一阶段仅 Windows | 暂定 |
| ARCH-005 | 美术与字体授权 | 全部复制资源建台账；未核实项仅限本地验证 | 阻塞发布 |

`ARCH-001` 于 2026-07-27 确认为 `Godot 4.7.1-stable` 与 `Dialogue Manager v3.10.5`，并于 2026-07-28 确认使用官方 .NET 编辑器和 C# 业务代码。Dialogue Manager 的官方发布页明确将 v3.10.5 标记为面向 Godot 4.7；项目锁定插件 tag `v3.10.5`（commit `0049d4d`），继续使用现有 v3 `.dialogue` 语法。M1 已完成插件导入、MIT 许可证落库、序章编译、115 个稳定 ID 唯一恢复以及中文首行查询验证。

`ARCH-002` 与 `ARCH-003` 于 2026-07-28 确认。`ARCH-005` 当前已完成全部 41 个复制资源的状态建档，所有记录仍为 `unverified`，不能解释为已经取得再发布授权。

## 17. 参考资料

- [Godot 4.7.1 release](https://godotengine.org/article/maintenance-release-godot-4-7-1/)
- [Godot 4.7 changed project stretch defaults](https://docs.godotengine.org/en/stable/tutorials/migrating/upgrading_to_godot_4.7.html)
- [Godot 4.7 Best practices](https://docs.godotengine.org/en/4.7/tutorials/best_practices/index.html)
- [Godot Scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html)
- [Godot Project organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/project_organization.html)
- [Godot Multiple resolutions](https://docs.godotengine.org/en/stable/tutorials/rendering/multiple_resolutions.html)
- [Godot FileAccess](https://docs.godotengine.org/en/stable/classes/class_fileaccess.html)
- [Dialogue Manager v3.10.5 for Godot 4.7](https://github.com/nathanhoad/godot_dialogue_manager/releases/tag/v3.10.5)
- [Dialogue Manager v3.10.5 API](https://github.com/nathanhoad/godot_dialogue_manager/blob/v3.10.5/docs/API.md)
- [Dialogue Manager v3.10.5 static translation keys](https://github.com/nathanhoad/godot_dialogue_manager/blob/v3.10.5/docs/Translations.md)
- [Dialogue Manager v3.10.5 static ID resolver](https://github.com/nathanhoad/godot_dialogue_manager/blob/v3.10.5/addons/dialogue_manager/dialogue_manager.gd#L541-L550)
- [Dialogue Manager v3.10.5 conditions, mutations and extra game states](https://github.com/nathanhoad/godot_dialogue_manager/blob/v3.10.5/docs/Conditions_Mutations.md)
- [Dialogue Manager v3.10.5 runtime usage](https://github.com/nathanhoad/godot_dialogue_manager/blob/v3.10.5/docs/Using_Dialogue.md)
- [REUSE Specification 3.3](https://reuse.software/spec/)
