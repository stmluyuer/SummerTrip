# SummerTrip Godot 迁移清单

## 1. 目的与边界

本文档整理 Ren'Py 版本 `0.1.0-prologue` 迁移到 Godot 时需要保留、转换或重新实现的内容。

- 现有 `game/` 目录是可运行基准，不移动、不改名、不删除。
- 迁移中间产物统一放在 `migration/`。
- 当前目标是整理完整序章原文和迁移清单，不创建 Godot 工程，也不安装生产依赖。
- 对话目标格式为 Dialogue Manager 3 的 `.dialogue` 格式。
- 建议实现基线为 Godot 4.6.x 与 Dialogue Manager 3.10.x；真正创建工程时再锁定补丁版本。

## 2. 当前项目概况

| 项目 | 当前值 |
| --- | --- |
| 类型 | 中文视觉小说序章 Demo |
| 基准分辨率 | 1920 × 1080 |
| Ren'Py 脚本 | 9 个 `.rpy` 文件，约 2143 行 |
| 剧情结构 | 16 个 label、6 个选择菜单、3 个结尾 |
| 剧情状态 | 玩家名与 3 个整数变量 |
| 正式场景素材 | 2 张背景、2 张角色立绘 |
| 音频 | 当前剧情没有音乐、音效或语音调用 |
| 现有发布目标 | `project.json` 只配置 Windows 包 |

## 3. 原文件处理原则

当前原文已经比较集中，不需要重排 Ren'Py 源文件。迁移时以以下文件为事实来源：

| Ren'Py 来源 | 内容 | 迁移目标 | 处理方式 |
| --- | --- | --- | --- |
| `game/aki_sisters_prologue.rpy` | 完整序章、选择、跳转、结尾 | `migration/dialogue/prologue.dialogue` | 一对一转换，原文不改 |
| `game/story_setup.rpy` | 角色、变量、背景、立绘位置 | `characters.tsv`、`variables.tsv`、Godot Autoload | 拆分整理 |
| `game/effects.rpy` | 转场和 Tween 参数 | `Presentation`、AnimationPlayer/Tween | 在 Godot 中重写 |
| `game/ui_screens.rpy` | 对话框、选择框、快捷菜单覆盖 | Godot `Control` 场景 | 在 Godot 中重写 |
| `game/ui_theme.rpy` | 项目配色、尺寸和字体样式 | Godot `Theme` | 转换参数 |
| `game/gui.rpy` | 1920×1080 GUI 基础参数 | `project.godot`、Godot `Theme` | 选择性转换 |
| `game/screens.rpy` | Ren'Py 默认菜单、存档、历史、设置 | Godot 系统 UI | 作为功能参照，不逐行转换 |
| `game/options.rpy` | 名称、版本、平台和偏好默认值 | `project.godot`、Settings | 转换配置 |
| `docs/resource_manifest.tsv` | 已有精简资源清单 | `migration/manifests/assets.tsv` | 扩充迁移分类 |

`game/script.rpy` 只说明主入口位于 `aki_sisters_prologue.rpy`，不包含需要迁移的剧情。

## 4. Godot 目标模块

| 模块 | 职责 | 来源 |
| --- | --- | --- |
| `VisualNovelStage` | 背景、叠色、角色立绘和对话 UI | `story_setup.rpy`、`effects.rpy`、`ui_screens.rpy` |
| `GameState` Autoload | 玩家名和剧情变量 | `story_setup.rpy` |
| `Presentation` Autoload | 切换场景、显示立绘、显示叠色和执行转场 | `story_setup.rpy`、`effects.rpy` |
| `DialogueController` | 驱动 Dialogue Manager、记录当前位置和已读状态 | `aki_sisters_prologue.rpy` |
| `SaveManager` | 手动/自动/快速存读档、截图、版本检查 | `screens.rpy` |
| `HistoryManager` | 保存最近 250 条显示过的文本 | `gui.rpy`、`screens.rpy` |
| `Settings` Autoload | 显示、文字速度、自动播放和音量 | `options.rpy`、`screens.rpy` |
| `MainMenu` / `GameMenu` | 开始、继续、保存、读取、设置、退出 | `screens.rpy`、`ui_screens.rpy` |

## 5. Dialogue Manager 对接约定

转换后的 `prologue.dialogue` 使用两个尚待 Godot 工程实现的 Autoload：

### `GameState`

```gdscript
var player_name: String = "旅人"
var minoriko_affection: int = 0
var shizuha_trust: int = 0
var autumn_mystery: int = 0
```

### `Presentation`

```gdscript
func change_scene(
    background_id: String,
    overlay_id: String,
    overlay_animation_id: String,
    transition_id: String
) -> void
func show_character(character_id: String, expression_id: String, position_id: String) -> void
```

约定：

- `change_scene` 原子地设置背景、叠色与转场，对应 Ren'Py 的 `scene`、`show overlay` 和 `with`；调用时清除旧立绘和旧叠色。
- 不需要叠色时传入两个空字符串。
- `transition_id = "none"` 表示立即切换。
- 动画方法可以是协程；Dialogue Manager 的 `do` 会等待方法结束。
- 对话文件中的 `[ID:...]` 是稳定原文标识，供翻译、已读记录和存档定位使用，不应在文案修改时随意重命名。
- 读取存档时先恢复 `GameState` 和舞台快照，再恢复到保存的对话 ID，避免重复执行已发生的变量变更。

## 6. 剧情流程

```text
start
└─ prologue_tree
   └─ aki_house_wakeup
      ├─ thank_minoriko
      │  ├─ farm_basket
      │  └─ ask_shizuha_leaf
      ├─ ask_shizuha_leaf
      │  ├─ maple_tree_investigation
      │  └─ farm_basket
      └─ mention_bell
         ├─ ask_festival
         └─ maple_tree_investigation

farm_basket ───────────────┐
maple_tree_investigation   │
├─ touch_green_trace       │
├─ minoriko_checks_crops   ├─ route_choice
└─ shizuha_reads_leaves    │
ask_festival ──────────────┘

route_choice
├─ ending_minoriko_prologue
├─ ending_shizuha_prologue
└─ ending_sisters_prologue
```

## 7. 视觉表现映射

| Ren'Py 标识 | Godot 标识 | 参数 |
| --- | --- | --- |
| `bg autumn_garden` | `autumn_garden` | 1920×1080 全屏背景 |
| `bg autumn_room` | `autumn_room` | 1920×1080 全屏背景 |
| `minoriko neutral at minoriko_right` | `minoriko/neutral/right` | `x=0.72`、底部锚定、缩放 `0.66`、从右侧淡入 |
| `sizuha neutral at sizuha_left` | `shizuha/neutral/left` | `x=0.28`、底部锚定、缩放 `0.66`、从左侧淡入 |
| `autumn_glow at leaf_shiver` | `autumn_glow/leaf_shiver` | `#d98a2b22`，透明度循环 |
| `mystery_glow at leaf_shiver` | `mystery_glow/leaf_shiver` | `#6f8fb833`，透明度循环 |
| `slow_dissolve` | `slow_dissolve` | 0.85 秒溶解 |
| `scene_blend` | `scene_blend` | 0.35 秒淡出、0.2 秒停留、0.55 秒淡入，颜色 `#120b08` |

其他 UI Tween 参数保留在 `game/effects.rpy` 中，建立 Godot UI 场景时再逐项复制。

## 8. 存读档必须保存的状态

每个存档至少包含：

```text
schema_version
game_version
dialogue_file
dialogue_line_id
game_state
presentation_state
history
read_line_ids
play_time_seconds
saved_at
screenshot_path
```

其中：

- `game_state` 保存 `variables.tsv` 中标记为 `save_required` 的所有字段。
- `presentation_state` 保存当前背景、叠色、在场角色、表情、位置和音乐播放状态。
- 截图目标尺寸沿用 Ren'Py：384×216。
- 手动档沿用每页 3×2 槽位；自动档和快速档使用独立命名空间。
- Ren'Py 旧存档默认不兼容 Godot，新版本需显示明确提示。

## 9. 已知疑点与风险

1. `farm_basket` 和 `maple_tree_investigation` 的叙述发生在山路/红叶树下，但原脚本仍使用 `bg autumn_room`。转换文件原样保留该表现，开始制作 Godot 场景前需要确认是占位背景还是脚本错误。
2. Dialogue Manager 尚未加入仓库；安装它属于新增第三方依赖，创建 Godot 工程时需要锁定版本并提交许可证。
3. 当前没有可用于验证 `.dialogue` 编译结果的 Godot 工程或 Dialogue Manager 插件。本阶段只能检查文本、标题、跳转、ID 和缩进结构。
4. 仓库没有完整的美术授权清单。迁移前应补充每项正式资源的作者、来源、许可证和可再发布范围。
5. 字体目录约 61 MB；当前界面实际使用 Regular、Medium、Bold 和 Heavy，其他字重可在 Godot 构建验证后排除。
6. 手机版 GUI 素材存在，但当前发布配置仅包含 Windows。是否支持手机端应在建立 Godot 工程前单独确认。

## 10. 本次整理产物

| 文件 | 用途 |
| --- | --- |
| `migration/dialogue/prologue.dialogue` | 完整序章 Dialogue Manager 原文 |
| `migration/manifests/assets.tsv` | 资源迁移、复用和排除清单 |
| `migration/manifests/characters.tsv` | 角色与立绘位置清单 |
| `migration/manifests/variables.tsv` | 剧情状态与存档字段清单 |
| `docs/godot_feature_checklist.md` | Godot 功能对等验收表 |
