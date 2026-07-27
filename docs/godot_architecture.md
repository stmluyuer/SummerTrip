# SummerTrip Godot 架构设计

## 1. 文档定位

本文档定义 `SummerTrip` 从 Ren'Py 迁移到 Godot 后的目标架构、模块职责和依赖边界。它回答“最终工程应该怎样组织”，不代替迁移内容清单或功能验收表。

相关文档：

- [Godot 迁移清单](godot_migration_inventory.md)：说明从原工程迁移什么。
- [Godot 实施路线图](godot_implementation_roadmap.md)：说明按什么顺序实现。
- [Godot 功能对等验收表](godot_feature_checklist.md)：说明怎样判断功能完成。
- `migration/manifests/`：保存角色、变量和资源的一对一迁移映射。

当前状态：架构设计已建立，Godot 工程尚未创建，生产依赖尚未安装。

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
- 迁移映射来源：`migration/manifests/*.tsv`。
- Godot 开工后的正式剧情来源：`godot/content/dialogue/*.dialogue`。
- 运行时资源映射来源：Godot `ContentCatalog`。

`migration/dialogue/prologue.dialogue` 在 Godot 工程建立时移动到 `godot/content/dialogue/prologue.dialogue`。移动后不保留第二份可编辑副本。

### 3.5 快照恢复，不重放副作用

存档保存当前剧情位置、剧情变量和舞台状态。读取时直接恢复这些数据，并从保存的稳定对白 ID 取得当前行。

读取过程不得从章节开头重放已经执行过的 `do` 或 `set`，否则好感、信任和谜团值会重复增加。

## 4. 目标目录

```text
godot/
├─ project.godot
├─ addons/
│  └─ dialogue_manager/                 # 第三方依赖，不修改插件源码
├─ app/
│  ├─ main.tscn
│  └─ main.gd
├─ core/
│  ├─ dialogue/
│  │  ├─ dialogue_context.gd
│  │  └─ dialogue_gateway.gd
│  ├─ save/
│  │  ├─ save_schema.gd
│  │  └─ save_service.gd
│  ├─ session/
│  │  ├─ game_session.gd
│  │  └─ profile_data.gd
│  └─ settings/
│     └─ settings_service.gd
├─ features/
│  ├─ vn/
│  │  ├─ vn_mode.tscn
│  │  ├─ vn_mode.gd
│  │  ├─ vn_director.gd
│  │  ├─ stage/
│  │  │  ├─ stage.tscn
│  │  │  └─ stage_controller.gd
│  │  └─ ui/
│  │     ├─ dialogue_box.tscn
│  │     └─ dialogue_box.gd
│  ├─ menu/
│  │  ├─ title_menu.tscn
│  │  ├─ title_menu.gd
│  │  ├─ game_menu.tscn
│  │  └─ game_menu.gd
│  ├─ save_ui/
│  │  ├─ save_load_screen.tscn
│  │  ├─ save_load_screen.gd
│  │  ├─ save_slot.tscn
│  │  └─ save_slot.gd
│  ├─ history/
│  │  ├─ history_screen.tscn
│  │  └─ history_screen.gd
│  └─ settings/
│     ├─ settings_screen.tscn
│     └─ settings_screen.gd
├─ content/
│  ├─ dialogue/
│  │  └─ prologue.dialogue
│  └─ catalog/
│     ├─ content_catalog.gd
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
   ├─ smoke_prologue.gd
   ├─ save_roundtrip.gd
   └─ validate_content.gd
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

只有 `dialogue_gateway.gd` 可以直接调用 Dialogue Manager API。`VNDirector` 只依赖以下项目内接口：

```text
get_line(resource_id, line_or_title_id, context, mutation_mode)
get_next_line(current_line, context)
choose_response(response, context)
```

具体方法签名可以随锁定的插件版本调整，但不得扩散到舞台、UI 和存档模块。

### 8.2 状态注入

现有 `prologue.dialogue` 使用 `GameState.*` 和 `Presentation.*`。它们是剧情脚本的公开命名空间，不要求是 Godot Autoload。

`dialogue_context.gd` 构造类似以下额外状态：

```gdscript
[
    {
        "GameState": game_session,
        "Presentation": stage_controller,
    }
]
```

Dialogue Manager v3 支持把节点、对象或字典作为 `extra_game_states` 传入。这样可以保留现有剧情语法，同时限制变量和舞台控制器的生命周期。

### 8.3 推进方式

正式运行时由 `VNDirector` 主动逐行请求 `DialogueLine`，不直接依赖插件示例气泡：

1. Gateway 根据 title 或稳定 ID 请求当前行。
2. Director 把角色名和正文交给 `DialogueUI`。
3. UI 完成打字后进入等待推进状态。
4. 有选项时进入选择状态，禁止普通推进。
5. 选择后以选项的下一 ID 请求下一行。
6. 没有下一行时结束当前章节。

自定义 UI 可以使用插件提供的 `DialogueLabel` 处理打字、暂停和内联命令，但输入状态仍由 `VNDirector` 统一管理。

### 8.4 稳定 ID

- 每条可显示对白和每个选项必须保留唯一 `[ID:...]`。
- 文案修改不得顺手重命名稳定 ID。
- 存档保存当前可显示行的 ID，而不是数组下标或临时 `next_id`。
- 实现存档前必须用锁定版本验证：以稳定行 ID 请求时，可以直接返回该行且不会重复执行此前的状态变更。
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
current_line_id
history
play_time_seconds
chapter_flags
```

`GameSession` 只接受 JSON 可表示的基础类型，并提供明确的 `to_dict()`、`restore_from_dict()` 和 `reset()`。

运行时归属与 JSON 顶层结构不要求一一对应。生成存档快照时，当前位置、历史和游玩时间从 `GameSession` 提取到 `story`、`history` 和 `play_time_seconds` 顶层字段，`session` 对象只保存玩家名、路线数值和章节标记，避免同一字段在存档中出现两份。

`VNDirector` 持有插件返回的临时 `DialogueLine` 对象；当一行进入可交互的稳定状态时，它把该行的稳定 ID 写回 `GameSession.current_line_id`。历史记录最多保留最近 250 条。

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

`VNDirector` 使用明确状态，而不是多个互相冲突的布尔值：

```text
BOOTING
TRANSITIONING
TYPING
WAITING_ADVANCE
CHOOSING
MENU_PAUSED
SAVING
RESTORING_SAVE
FINISHED
```

允许保存的稳定状态是 `WAITING_ADVANCE` 和 `CHOOSING`。在 `TYPING` 中请求保存时，先完成当前句显示，再生成快照。转场、写入和恢复过程中拒绝新的保存或推进请求。

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
    "line_id": "ST_AHW_L010"
  },
  "session": {},
  "presentation": {},
  "history": [],
  "read_line_ids": [],
  "play_time_seconds": 0.0,
  "screenshot_path": "manual/slot_001.png"
}
```

实际 schema 由 `save_schema.gd` 定义和校验；上例只说明顶层边界。

### 10.2 文件布局

```text
user://
├─ profile.json
├─ settings.json
└─ saves/
   ├─ manual/
   ├─ quick/
   └─ auto/
```

- 手动档每页 3×2，最多 9 页。
- 快速档和自动档使用独立命名空间。
- 截图尺寸为 384×216。
- 文件名由 `SaveService` 统一生成，UI 不拼接磁盘路径。

### 10.3 防损坏写入

每次保存采用替换式写入：

1. 写入同目录临时文件。
2. 关闭文件并重新读取、解析和校验临时文件。
3. 保留上一份有效文件作为备份。
4. 用已验证的临时文件替换正式文件。
5. 任一步失败时返回可显示错误，不删除上一份有效数据。

截图和 JSON 都成功后才更新槽位索引。覆盖或删除存档必须经过 UI 确认。

### 10.4 读取顺序

1. `VNDirector` 进入 `RESTORING_SAVE`，冻结推进和保存输入。
2. `SaveService` 解析并校验 schema、字段类型和游戏版本。
3. `GameSession` 重置后恢复存档数据。
4. 存档内 `read_line_ids` 合并到 `ProfileData`。
5. `StageController` 无动画恢复舞台快照。
6. Gateway 以保存的稳定行 ID 请求当前行，并跳过该位置之前的副作用。
7. `DialogueUI` 直接显示完整当前行或当前选项。
8. Director 回到 `WAITING_ADVANCE` 或 `CHOOSING`。

任何步骤失败都保持原文件不变，并回到可操作的菜单状态。

### 10.5 回退

回退与磁盘存档共用快照字段，但使用内存环形缓冲区。每次稳定进入一条对白或一个选择点时记录轻量快照；不为每句话写磁盘或生成截图。

回退在完整存读档闭环通过后再实现。

## 11. 舞台和资源目录

`ContentCatalog` 将语义 ID 映射为 Godot 资源：

```text
backgrounds.autumn_garden -> Texture2D
characters.minoriko.neutral -> Texture2D
positions.right -> x=0.72, bottom_anchor=true, scale=0.66
overlays.autumn_glow -> color=#d98a2b22
transitions.scene_blend -> fade_out=0.35, hold=0.2, fade_in=0.55
```

约束：

1. 剧情和存档只保存语义 ID。
2. 目录负责验证 ID 是否存在。
3. 未知 ID 必须给出包含资源 ID 和剧情位置的错误，不能静默显示错误素材。
4. 第一阶段只迁移当前竖切所需的 P0 素材。
5. `game/gui/phone/**`、未使用立绘和编辑源图继续延后。
6. 资源授权信息在正式发布前必须补全。

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
vn_quick_save
vn_quick_load
vn_screenshot
ui_accept
ui_cancel
```

Director 根据当前运行状态决定动作是否生效。控件不直接监听所有物理按键，设置页以后可以调整绑定。

### 12.3 分辨率

推荐基线：

- 设计区域：1920×1080。
- 2D 缩放模式：`canvas_items`。
- 宽高比：`keep`，非 16:9 显示器使用黑边。
- UI 使用 Control 锚点和容器，不依赖窗口实际像素。

选择 `keep` 是为了保护现有固定 16:9 背景和立绘构图。该项在写入 `project.godot` 前仍需正式确认；如果改为 `expand`，舞台安全区和超宽屏背景策略必须同时设计。

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
| `tests/smoke_prologue.gd` | `.dialogue` 可编译、入口存在、能到达第一处选择 |
| `tests/save_roundtrip.gd` | 快照写入、进程内清空、读取恢复后字段完全一致 |
| `tests/validate_content.gd` | 剧情使用的背景、角色、位置、叠色和转场 ID 均存在 |

人工验证以 `godot_feature_checklist.md` 的回归路线为准。存档 UI 开始前，必须先通过稳定 ID 恢复和变量不重复增加的专项验证。

## 15. 扩展路径

- 新章节：在 `content/dialogue/` 增加章节文件并复用同一 Context。
- 探索玩法：在 `ModeHost` 下替换为 `ExplorationMode`，保留同一个 `GameSession`。
- 小游戏：通过明确的输入/输出数据进入和退出，不让小游戏直接修改对话 UI。
- 音频：舞台快照增加实际 BGM 状态后，再引入跨模式音频服务。
- CG/成就：写入 `ProfileData`，不混入单个存档路线状态。
- 本地化：稳定对白 ID 继续作为翻译键，不以中文原文作为持久标识。

## 16. 待确认决策

| ID | 决策 | 当前建议 | 状态 |
| --- | --- | --- | --- |
| ARCH-001 | Godot 与 Dialogue Manager 版本组合 | 优先选择正式稳定且互相声明兼容的组合 | 待确认 |
| ARCH-002 | 非 16:9 显示策略 | `canvas_items` + `keep` | 待确认 |
| ARCH-003 | 树下调查段的室内背景 | 保留原表现直到获得新背景或明确修正 | 待确认 |
| ARCH-004 | 正式发布平台 | 第一阶段仅 Windows | 暂定 |
| ARCH-005 | 美术与字体授权 | 发布前补全来源和再发布范围 | 阻塞发布 |

截至 2026-07-27，Dialogue Manager 官方仓库说明 v3.10 面向 Godot 4.4/4.5，而 Godot 4.6+ 对应的 v4 尚未正式发布。因此不能直接把 “Godot 4.6.x + Dialogue Manager 3.10.x” 当作已验证组合。安装生产依赖前必须重新核对上游状态、锁定准确版本并提交其许可证。

## 17. 参考资料

- [Godot 4.6 Best practices](https://docs.godotengine.org/en/4.6/tutorials/best_practices/index.html)
- [Godot Scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html)
- [Godot Project organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/project_organization.html)
- [Godot Multiple resolutions](https://docs.godotengine.org/en/stable/tutorials/rendering/multiple_resolutions.html)
- [Godot FileAccess](https://docs.godotengine.org/en/stable/classes/class_fileaccess.html)
- [Dialogue Manager repository and version guidance](https://github.com/nathanhoad/godot_dialogue_manager)
- [Dialogue Manager v3 conditions, mutations and extra game states](https://github.com/nathanhoad/godot_dialogue_manager/blob/v3.x/docs/Conditions_Mutations.md)
- [Dialogue Manager v3 runtime usage](https://github.com/nathanhoad/godot_dialogue_manager/blob/v3.x/docs/Using_Dialogue.md)
- [Dialogue Manager v3 API](https://github.com/nathanhoad/godot_dialogue_manager/blob/v3.x/docs/API.md)
