# SummerTrip Godot 迁移清单

## 1. 目的与边界

本文档整理 Ren'Py 版本 `0.1.0-prologue` 迁移到 Godot 时需要保留、转换或重新实现的内容。

- 现有 `game/` 目录是可运行基准，不移动、不改名、不删除。
- 迁移中间产物统一放在 `migration/`。
- 当前已完成序章原文、迁移清单、目标架构以及 M0 至 M7；Godot 4.7.1 .NET、Dialogue Manager v3.10.5、完整序章六条路线、舞台、Profile、存读档、菜单、设置与 AVG 辅助功能均已验证。
- 当前对话迁移稿使用 Dialogue Manager 3 的 `.dialogue` 语法，与已锁定的 Dialogue Manager `v3.10.5` 一致。
- 引擎与对话插件组合已确认为 `Godot 4.7.1-stable` .NET + `Dialogue Manager v3.10.5`；当前下一阶段是 M8 Windows 发布验证。

## 2. 当前项目概况

| 项目 | 当前值 |
| --- | --- |
| 类型 | 中文视觉小说序章 Demo |
| 基准分辨率 | 1920 × 1080，`canvas_items + keep` |
| Ren'Py 脚本 | 9 个 `.rpy` 文件，共 3042 行 |
| 剧情结构 | 16 个 label、6 个选择菜单、3 个结尾 |
| 剧情状态 | 玩家名与 3 个整数变量 |
| 正式场景素材 | 2 张背景、2 张角色立绘 |
| 音频 | 当前剧情没有音乐、音效或语音调用 |
| 现有发布目标 | `project.json` 只配置 Windows 包 |

## 3. 原文件处理原则

当前原文已经比较集中，不需要重排 Ren'Py 源文件。迁移时以以下文件为事实来源：

| Ren'Py 来源 | 内容 | 迁移目标 | 处理方式 |
| --- | --- | --- | --- |
| `game/aki_sisters_prologue.rpy` | 完整序章、选择、跳转、结尾 | `godot/content/dialogue/prologue.dialogue` | 一对一转换，原文不改 |
| `game/story_setup.rpy` | 角色、变量、背景、立绘位置 | `characters.tsv`、`variables.tsv`、`presentation.tsv`、`GameSession`、`ContentCatalog` | 拆分整理 |
| `game/effects.rpy` | 转场和 Tween 参数 | `presentation.tsv`、`Presentation`、AnimationPlayer/Tween | 在 Godot 中重写 |
| `game/ui_screens.rpy` | 对话框、选择框、快捷菜单覆盖 | Godot `Control` 场景 | 在 Godot 中重写 |
| `game/ui_theme.rpy` | 项目配色、尺寸和字体样式 | Godot `Theme` | 转换参数 |
| `game/gui.rpy` | 1920×1080 GUI 基础参数 | `project.godot`、Godot `Theme` | 选择性转换 |
| `game/screens.rpy` | Ren'Py 默认菜单、存档、历史、设置 | Godot 系统 UI | 作为功能参照，不逐行转换 |
| `game/options.rpy` | 名称、版本、平台和偏好默认值 | `project.godot`、Settings | 转换配置 |
| `docs/resource_manifest.tsv` | 旧版精简资源清单 | `migration/manifests/assets.tsv` | 已被替代，只作历史参考 |

`game/script.rpy` 只说明主入口位于 `aki_sisters_prologue.rpy`，不包含需要迁移的剧情。

## 4. Godot 目标模块

| 模块 | 职责 | 来源 |
| --- | --- | --- |
| `VNMode` / `StageController` | 背景、叠色、角色立绘、转场和对话 UI 组合 | `story_setup.rpy`、`effects.rpy`、`ui_screens.rpy` |
| `GameSession` | 玩家名、剧情变量、当前位置、历史和游玩时间 | `story_setup.rpy` |
| `DialogueContext` | 把 `GameState` 与 `Presentation` 命名空间注入对话运行时 | `story_setup.rpy`、`effects.rpy` |
| `VNDirector` / `DialogueGateway` | 驱动 Dialogue Manager、管理运行状态并记录当前位置 | `aki_sisters_prologue.rpy` |
| `SaveService` | 手动/自动/快速存读档、截图、版本检查 | `screens.rpy` |
| `ProfileData` | 保存跨存档的已读文本、结局和未来解锁项 | `gui.rpy`、`screens.rpy` |
| `SettingsService` Autoload | 显示、文字速度、自动播放和音量 | `options.rpy`、`screens.rpy` |
| `MainMenu` / `GameMenu` | 开始、继续、保存、读取、设置、退出 | `screens.rpy`、`ui_screens.rpy` |

## 5. Dialogue Manager 对接约定

转换后的 `prologue.dialogue` 使用两个尚待 Godot 工程实现的公开命名空间。它们通过 Dialogue Manager 的额外游戏状态注入，不要求作为 Godot Autoload：

### `GameState`

```csharp
public string PlayerName { get; set; } = "旅人";
public int MinorikoAffection { get; set; }
public int ShizuhaTrust { get; set; }
public int AutumnMystery { get; set; }
```

### `Presentation`

```csharp
void ChangeScene(
    string backgroundId,
    string overlayId,
    string overlayAnimationId,
    string transitionId
);
void ShowCharacter(string characterId, string expressionId, string positionId);
```

Godot 侧由 `DialogueContext` 注入类似以下结构：

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

约定：

- `change_scene` 原子地设置背景、叠色与转场，对应 Ren'Py 的 `scene`、`show overlay` 和 `with`；调用时清除旧立绘和旧叠色。
- 不需要叠色时传入两个空字符串。
- `transition_id = "none"` 表示立即切换。
- 动画方法可以是协程；Dialogue Manager 的 `do` 会等待方法结束。
- 对话文件中的 `[ID:...]` 是 Dialogue Manager 的静态翻译键，对应 `DialogueLine.translation_key`，供翻译、已读记录和存档定位使用，不应在文案修改时随意重命名。
- 存档保存 `translation_key`，不保存插件运行时 `DialogueLine.id` 或 `next_id`。
- 读取存档时先通过 `DialogueManager.static_id_to_line_ids()` 把静态行 ID 唯一解析为运行时 ID，并用 `MutationBehaviour.Skip` 预取当前行；全部预检通过后才提交 `GameState` 和舞台快照，避免重复执行变量变更或留下半恢复状态。
- Dialogue Manager 的具体调用只放在 `DialogueGateway`，便于锁定版本或以后升级插件。

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
| `bg autumn_room` | `autumn_room` | 1920×1080 全屏背景；树下调查段也按原版保留该背景 |
| `minoriko neutral at minoriko_right` | `minoriko/neutral/right` | `x=0.72`、`y=1.02`、锚点 `(0.5,1.0)`、缩放 `0.66`、从右侧偏移 64 px 并在 0.28 秒淡入 |
| `sizuha neutral at sizuha_left` | `shizuha/neutral/left` | `x=0.28`、`y=1.02`、锚点 `(0.5,1.0)`、缩放 `0.66`、从左侧偏移 64 px 并在 0.28 秒淡入 |
| `autumn_glow at leaf_shiver` | `autumn_glow/leaf_shiver` | `#d98a2b22`；0.25 秒淡入到 0.22，之后每段 1.6 秒在 0.12 与 0.24 间往返，完整周期 3.2 秒 |
| `mystery_glow at leaf_shiver` | `mystery_glow/leaf_shiver` | `#6f8fb833`；0.25 秒淡入到 0.22，之后每段 1.6 秒在 0.12 与 0.24 间往返，完整周期 3.2 秒 |
| `slow_dissolve` | `slow_dissolve` | 0.85 秒溶解 |
| `scene_blend` | `scene_blend` | 0.35 秒淡出、0.2 秒停留、0.55 秒淡入，颜色 `#120b08` |

上述舞台参数的结构化事实来源是 `migration/manifests/presentation.tsv`。其他 UI Tween 参数保留在 `game/effects.rpy` 中，建立 Godot UI 场景时再逐项复制。

## 8. 存读档必须保存的状态

每个存档至少包含：

```text
schema_version
game_version
saved_at
story.resource_id
story.chapter_id
story.static_line_id
session
presentation
history
read_line_ids
play_time_seconds
screenshot_path
```

其中：

- `story.static_line_id` 保存当前 `DialogueLine.translation_key`；`story.chapter_id` 供存档槽通过 `ContentCatalog` 显示可本地化章节名。
- `session` 保存 `variables.tsv` 中标记为 `save_required` 的所有字段。
- `presentation` 保存当前背景、叠色、在场角色、表情、位置和音乐播放状态。
- `read_line_ids` 的跨存档事实来源是 `ProfileData`；读取存档时只合并，不因读取较早存档而删除后来读过的 ID。
- 截图目标尺寸沿用 Ren'Py：384×216。
- 手动档沿用每页 3×2、最多 9 页；快速档使用 1 个独立槽位，自动档使用 6 个轮换槽位。
- 序章自动存档 title 为 `aki_house_wakeup`、`maple_tree_investigation` 和 `route_choice`；必须等 title 前置 mutation/转场完成并稳定显示首行后再写入。
- Ren'Py 旧存档默认不兼容 Godot，新版本需显示明确提示。

## 9. 已知疑点与风险

1. `farm_basket` 和 `maple_tree_investigation` 的叙述发生在山路/红叶树下，但原脚本仍使用 `bg autumn_room`。已于 2026-07-28 确认迁移版原样保留；未来替换背景属于内容改版，不属于迁移纠错。
2. Dialogue Manager 已从官方 `v3.10.5` tag（commit `0049d4d`）原样加入，MIT 许可证和上游版本记录已落库；后续不得直接修改 vendored 插件源码。
3. `.dialogue` 已在 Godot 4.7.1 .NET 中成功编译；16 个 title 和 115 个静态 ID 已通过运行时 smoke 验证，每个静态 ID 恰好解析为一个运行时 ID。
4. `asset_licenses.tsv` 已覆盖 `assets.tsv` 中全部 41 个 `action=copy` 资源，但所有记录仍为 `unverified`、`local_validation_only`；正式发布前仍须补全作者、来源、许可证和可再发布范围。
5. 字体目录约 61 MB；当前剧情实际需要 Regular、Medium 和 Bold。Heavy 只用于尚未被剧情调用的通用输入样式，延后到确认新增姓名输入时再迁移；其他字重可在 Godot 构建验证后排除。
6. 手机版 GUI 素材存在，但第一阶段发布范围已限定为 Windows；移动端素材继续延后，不进入当前工程。

## 10. 本次整理产物

| 文件 | 用途 |
| --- | --- |
| `godot/content/dialogue/prologue.dialogue` | Godot 工程内唯一可编辑的完整序章 Dialogue Manager 原文 |
| `migration/manifests/assets.tsv` | 资源迁移、复用和排除清单 |
| `migration/manifests/asset_licenses.tsv` | 全部复制资源的授权状态与再发布范围记录 |
| `migration/manifests/characters.tsv` | 角色与立绘位置清单 |
| `migration/manifests/presentation.tsv` | 背景、角色位置、叠色动画和转场参数清单 |
| `migration/manifests/variables.tsv` | 剧情状态与存档字段清单 |
| `tools/check_assets.ps1` | 校验资源清单、精确复制源、全部复制资源授权覆盖和发布授权门禁 |
| `docs/godot_architecture.md` | Godot 目标架构、模块边界和数据流 |
| `docs/godot_implementation_roadmap.md` | 分阶段实施顺序、验证方式和退出条件 |
| `docs/godot_feature_checklist.md` | Godot 功能对等验收表 |
