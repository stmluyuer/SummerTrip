# SummerTrip Godot 实施路线图

## 1. 文档定位

本文档把 [Godot 架构设计](godot_architecture.md) 拆成可验证的实施阶段。每个阶段都规定输入、修改范围、验证方式和退出条件，避免先铺完整系统再集中排错。

关联资料：

- [Godot 迁移清单](godot_migration_inventory.md)：迁移输入。
- [Godot 架构设计](godot_architecture.md)：目标结构和模块边界。
- [Godot 功能对等验收表](godot_feature_checklist.md)：最终验收标准。
- `migration/dialogue/prologue.dialogue`：已整理的序章剧情。
- `migration/manifests/`：资源、角色、变量和舞台表现映射。
- `tools/check_assets.ps1`：资源清单结构与精确复制源校验。

当前状态：`M0` 进行中，其余阶段未开始。

## 2. 状态定义

| 状态 | 含义 |
| --- | --- |
| `未开始` | 前置条件尚未满足或尚未进入实施 |
| `进行中` | 已开始实现，但退出条件尚未全部通过 |
| `阻塞` | 缺少必须的产品决定、依赖或授权 |
| `完成` | 修改、验证和文档同步均已完成 |

阶段完成以退出条件为准，不以“文件已经创建”或“编辑器里看起来能运行”为准。

## 3. 总体顺序

```text
M0 技术决策
  ↓
M1 工程骨架与依赖验证
  ↓
M2 纯对话竖切
  ↓
M3 舞台表现竖切
  ↓
M4 存读档闭环
  ↓
M5 完整序章
  ↓
M6 菜单与设置
  ↓
M7 AVG 辅助功能
  ↓
M8 Windows 发布验证
```

关键门槛：

- 未完成 `M1` 的版本和编译验证，不批量迁移素材。
- 未完成 `M2`，不制作完整菜单。
- 未完成 `M4` 的稳定 ID 恢复验证，不制作存档槽 UI。
- 未完成 `M5` 的六条剧情路线，不开始回退系统。
- 未完成素材授权核对，不进入正式发布。

## 4. 决策门

### D-001 引擎与对话插件版本

状态：`已确认（2026-07-27）`

锁定组合：

| 组件 | 锁定版本 | 来源与标识 |
| --- | --- | --- |
| Godot | `4.7.1-stable` | 官方维护版本，engine commit `a13da4feb` |
| Dialogue Manager | `v3.10.5` | 官方面向 Godot 4.7 的发布，tag `v3.10.5`，commit `0049d4d`，MIT |

选择 v3.10.5 可以直接沿用当前 `prologue.dialogue` 的 v3 语法，并避免把尚未正式发布的 Dialogue Manager 4 作为生产基线。Dialogue Manager v3.10.5 已获准作为项目生产依赖；插件源码和许可证在 M1 一起加入仓库。

版本选择已经完成，但兼容声明不能代替项目验证。M1 仍必须使用锁定版本实际导入插件、编译 `prologue.dialogue`，并验证额外游戏状态和稳定 ID 恢复行为。

### D-002 分辨率策略

推荐 `1920×1080 + canvas_items + keep`。这会在非 16:9 屏幕上保留黑边，但不会破坏现有背景构图。

如果选择 `expand`，必须先补充：

- 16:10 和 21:9 的舞台安全区。
- 超宽屏背景延展或裁切规则。
- 对话框、选择框和快捷菜单锚点。

### D-003 树下调查背景

`farm_basket` 和 `maple_tree_investigation` 的文字发生在室外，但当前脚本使用 `autumn_room`。

可选处理：

1. 原样保留，保证迁移对等。
2. 使用 `autumn_garden` 临时代替。
3. 等待新的山路/红叶树背景。

该决定不阻塞 `M2`，但必须在 `M5` 完整序章验收前确定。

### D-004 发布平台与素材授权

第一阶段暂按 Windows PC 设计。Linux、macOS 和移动端不进入首个发布范围。

正式发布前，每项背景、角色、UI 和字体必须记录作者、来源、许可证和再发布范围。授权未明确的素材可以用于本地迁移验证，但不能视为可发布资产。

## 5. M0：技术决策与文档基线

状态：`进行中`

### 目标

把会影响目录、依赖或运行接口的决定显式记录下来，让后续实现有一致基准。

### 修改范围

```text
docs/godot_architecture.md
docs/godot_implementation_roadmap.md
docs/godot_migration_inventory.md
docs/godot_feature_checklist.md
migration/manifests/*
tools/check_assets.ps1
```

### 工作项

- [x] 整理原 Ren'Py 剧情、变量、角色和资源。
- [x] 把角色位置、叠色动画和转场参数整理为 `presentation.tsv`。
- [x] 生成序章 Dialogue Manager 源文件。
- [x] 建立目标架构。
- [x] 建立分阶段实施路线。
- [x] 让资源校验脚本以 `migration/manifests/assets.tsv` 为唯一输入。
- [x] 确认 D-001 版本组合。
- [ ] 确认 D-002 分辨率策略。
- [ ] 确认 D-003 背景处理。
- [ ] 建立正式资源授权记录。

### 验证

- 文档中的文件名、模块名和状态模型一致。
- 架构文档、路线图与功能验收表互相链接。
- 迁移清单不再把未确认版本写成已验证组合。
- `tools/check_assets.ps1` 校验新资源清单的列、P0 约束、重复路径和全部精确复制源。
- `presentation.tsv` 中位置、动画与转场参数和 `story_setup.rpy`、`effects.rpy` 一致。
- 现有 `game/` 原文件没有被移动或修改。

### 退出条件

- D-001 和 D-002 已确认。
- Dialogue Manager 的新增生产依赖得到用户明确同意。
- M1 使用的 Godot 可执行文件和插件来源已确定。

## 6. M1：工程骨架与依赖验证

状态：`未开始`

### 目标

建立最小 Godot 工程，确认引擎、插件、中文资源和 headless 验证链路可用。

### 预计文件

```text
godot/project.godot
godot/app/main.tscn
godot/app/main.gd
godot/addons/dialogue_manager/**
godot/content/dialogue/prologue.dialogue
godot/tests/smoke_prologue.gd
```

同时更新仓库 `.gitignore`，忽略 `godot/.godot/` 和本地导出目录，但保留需要版本控制的 Godot 源资源。

### 工作项

1. 创建 `godot/` 子项目，不改变 Ren'Py `game/`。
2. 配置应用名、版本、1920×1080 设计区域和输入动作。
3. 安装并锁定 Dialogue Manager `v3.10.5`（commit `0049d4d`）。
4. 提交第三方许可证，不修改插件源码。
5. 将 `migration/dialogue/prologue.dialogue` 移动到 Godot 项目内。
6. 让插件实际导入并编译 `.dialogue`。
7. 建立最小 headless smoke 脚本。

### 验证

- Godot 编辑器可以打开项目且无导入错误。
- 主场景可以启动并正常退出。
- `prologue.dialogue` 编译无语法错误。
- `start` title 可由运行 API 查询；每个 `[ID:...]` 都能通过 `static_id_to_line_ids()` 唯一解析为一个运行时 ID，并返回相同 `translation_key` 的行或选项。
- headless 检查以非零退出码报告失败。

### 退出条件

- `Godot 4.7.1-stable` 与 `Dialogue Manager v3.10.5` 在本机验证通过。
- 工程从全新导入缓存启动成功。
- 没有把 `.godot/` 缓存、编辑器日志或导出包提交进仓库。

## 7. M2：纯对话竖切

状态：`未开始`

### 目标

不依赖完整美术和菜单，跑通“启动序章 → 显示中文对白 → 到达第一处三选项 → 选择后进入正确分支”。

### 预计文件

```text
godot/core/dialogue/dialogue_context.gd
godot/core/dialogue/dialogue_gateway.gd
godot/core/session/game_session.gd
godot/features/vn/vn_mode.tscn
godot/features/vn/vn_mode.gd
godot/features/vn/vn_director.gd
godot/features/vn/stage/stage_controller.gd
godot/features/vn/ui/dialogue_box.tscn
godot/features/vn/ui/dialogue_box.gd
godot/ui/themes/vn_theme.tres
godot/assets/fonts/SourceHanSansCN-Regular.otf
godot/assets/fonts/SourceHanSansCN-Medium.otf
godot/assets/fonts/SourceHanSansCN-Bold.otf
godot/tests/smoke_prologue.gd
```

### 工作项

1. `GameSession` 实现玩家名和三个剧情整数的默认值。
2. `DialogueContext` 以额外状态提供 `GameState` 与由 `StageController` 实现的 `Presentation`。
3. `DialogueGateway` 封装插件的逐行请求、下一行和选项跳转。
4. `VNDirector` 分别建立最小 `InteractionState` 与 `OperationState`，菜单或保存操作不得覆盖当前对白交互状态。
5. 对话 UI 显示角色名、叙述、正文和选项。
6. 动态玩家名 `{{GameState.player_name}}` 正确显示。
7. 选项出现时普通推进失效。
8. `StageController` 先实现 `Presentation` 的语义接口和逻辑状态记录；本阶段允许不显示正式舞台画面。
9. 只复制正文、名字和界面所需的 Regular、Medium、Bold 三个字体字重。

### 验证

- `ST_PRT_L001` 到 `ST_AHW_L010` 顺序正确。
- 第一处三个选项均显示且 ID 唯一。
- 三个选项分别正确修改：
  - `minoriko_affection`
  - `shizuha_trust + autumn_mystery`
  - `autumn_mystery`
- 鼠标左键、Enter、Space 可以推进；Esc 不会误选选项。
- 中文字体无缺字、乱码或错误换行。

### 退出条件

- 三个首选项分支均能进入目标 title。
- 运行状态不会同时处于打字和选择。
- Dialogue Manager API 只出现在 Gateway 和插件目录。

## 8. M3：舞台表现竖切

状态：`未开始`

### 目标

为 M2 的对话加入背景、立绘、叠色和转场，并验证剧情只使用语义 ID。

### 预计文件

```text
godot/content/catalog/content_catalog.gd
godot/content/catalog/content_catalog.tres
godot/features/vn/stage/stage.tscn
godot/features/vn/stage/stage_controller.gd       # 扩展 M2 的逻辑实现
godot/assets/backgrounds/*
godot/assets/characters/*
godot/assets/ui/dialogue/*
godot/assets/ui/button/*
godot/tests/validate_content.gd
```

### 本阶段新增资源

- `autumn_garden.png`
- `autumn_room.png`
- `minoriko.png`
- `shizuha.png`
- `box_compact.png`
- 选择按钮 idle/hover
- 窗口图标

完整菜单、存档槽、设置控件和手机素材继续延后。

### 工作项

1. 以 `migration/manifests/presentation.tsv` 为参数事实来源，建立背景、角色、位置、叠色和转场目录。
2. 实现 `Presentation.change_scene()`。
3. 实现 `Presentation.show_character()`。
4. 实现左右角色槽、底部锚定和 0.66 缩放。
5. 实现 `slow_dissolve`、`scene_blend` 和 `leaf_shiver`。
6. 未知语义 ID 输出可定位错误。
7. 建立内容目录完整性检查。

### 验证

- 花园和室内背景铺满 1920×1080 设计区域。
- 穰子位于 72%、静叶位于 28% 横坐标；两者 `y=1.02`、锚点 `(0.5,1.0)`、缩放 `0.66`。
- 角色从对应方向位移 64 px 并淡入。
- `leaf_shiver` 先在 0.25 秒到达 0.22 透明度，再以每段 1.6 秒在 0.12 与 0.24 间往返，完整周期为 3.2 秒。
- `change_scene()` 清理旧立绘和旧叠色。
- 故意输入未知 ID 时测试明确失败，而不是静默显示空白。

### 退出条件

- 开场到第一处选项的画面和原版基准一致。
- 剧情文件中不存在 `res://`、节点路径或像素坐标。
- `validate_content.gd` 通过。

## 9. M4：存读档闭环

状态：`未开始`

### 目标

先完成可靠的数据快照和恢复，再制作存档页面。

### 预计文件

```text
godot/core/save/save_schema.gd
godot/core/save/save_service.gd
godot/core/session/profile_data.gd
godot/tests/save_roundtrip.gd
```

### 工作项

1. `GameSession`、`StageController` 和 Director 提供快照。
2. `SaveSchema` 使用统一的 `story.resource_id`、`story.chapter_id` 和 `story.static_line_id`，并校验版本、必填字段、字段类型和受控相对路径。
3. `SaveService` 使用 `user://`，先写不可变代次截图，再以已验证的临时 JSON 替换正式 JSON。
4. 区分 54 个手动槽、1 个快速槽和 6 个轮换自动槽；`index.json` 只是可重建缓存。
5. 读档先解析资源、静态行 ID 和全部舞台语义 ID，在候选会话中预取当前行；全部成功后才提交当前会话和舞台。
6. Profile 的已读 ID 先在候选数据中合并；候选 Profile 原子写入成功后才替换内存 Profile。
7. 验证 `DialogueLine.translation_key` 经 `static_id_to_line_ids()` 唯一解析后恢复当前对白。
8. 恢复时使用 `MutationBehaviour.Skip`，不重放当前行之前的 mutation。
9. 损坏 JSON、缺字段、不安全路径和不支持版本返回可理解错误，失败读档不改变现有会话、舞台或 Profile。

### 必测保存点

1. 第一处三选项出现前。
2. 选中第一项并增加好感之后。
3. 转场完成后的第一句。
4. 当前句处于打字中时请求保存。

### 验证

对每个保存点执行：

```text
建立状态
→ 生成快照
→ 写入磁盘
→ 清空内存状态
→ 读取并恢复
→ 比较剧情、舞台、历史和变量
→ 推进一行
→ 检查 mutation 只执行一次
```

额外验证：

- 临时文件无效时上一份正式存档仍可读取。
- 在截图写入后、JSON 提交前中断时，旧 JSON 仍引用旧截图，新截图只作为可清理孤立文件存在。
- 截图缺失不会导致 JSON 存档崩溃。
- 读取较早存档不会清除 Profile 中后来读过的对白 ID。
- 静态行 ID、资源 ID 或舞台 ID 无效时，读档前的内存状态保持不变。

### 退出条件

- `save_roundtrip.gd` 全部通过。
- 重新启动进程后仍能读取。
- 好感、信任和谜团值没有重复增加。
- 存档核心不依赖任何存档槽 UI 节点。

## 10. M5：完整序章

状态：`未开始`

### 目标

让当前序章的全部对白、选择、状态变化和三个结尾在 Godot 中可到达。

### 修改范围

```text
godot/content/dialogue/prologue.dialogue
godot/content/catalog/content_catalog.tres
godot/features/vn/*
godot/tests/*
docs/godot_feature_checklist.md
```

### 工作项

1. 验证 16 个 title、6 个选择点、15 个选项。
2. 对照原版验证 100 条剧情文本。
3. 确认 D-003 的背景处理。
4. 为 `aki_house_wakeup`、`maple_tree_investigation` 和 `route_choice` 配置自动存档点：仅在前置 mutation/转场完成、第一条可交互行稳定显示后写入 6 槽轮换自动档；结尾在最后一行稳定显示后写入。
5. 记录三个序章结尾到 Profile。
6. 完成历史记录基础数据。

### 验证

逐条执行功能验收表中的六条回归路线，并在以下位置存读档：

- 第一处三选项前。
- 红叶树调查三选项前。
- 最终路线三选项前。
- 结尾最后一句。

### 退出条件

- 六条路线全部通过。
- 三个结尾均可独立到达。
- 原文、选项和变量变更与 Ren'Py 一致。
- 所有 P0 剧情、舞台和存档项目更新为完成。

## 11. M6：菜单与设置

状态：`未开始`

### 目标

把已经验证的数据能力接入完整游戏流程和系统 UI。

### 预计文件

```text
godot/features/menu/title_menu.tscn
godot/features/menu/title_menu.gd
godot/features/menu/game_menu.tscn
godot/features/menu/game_menu.gd
godot/features/save_ui/save_load_screen.tscn
godot/features/save_ui/save_load_screen.gd
godot/features/save_ui/save_slot.tscn
godot/features/save_ui/save_slot.gd
godot/features/history/history_screen.tscn
godot/features/history/history_screen.gd
godot/features/settings/settings_screen.tscn
godot/features/settings/settings_screen.gd
godot/core/settings/settings_service.gd
```

### 工作项

- 主菜单：新游戏、继续、读取、设置、关于、退出。
- 游戏菜单：保存、读取、历史、设置、返回标题。
- 存档页：每页 3×2、1–9 页、384×216 截图，并通过 `story.chapter_id` 显示本地化章节名。
- 设置：窗口/全屏、文字速度、自动等待、快进策略和预留音量。
- 确认弹窗：覆盖、删除、返回标题、退出。
- 通知：快存、快读、截图和错误提示。

### 验证

- 菜单暂停时剧情和游玩计时行为符合设计。
- 从 `WAITING_ADVANCE` 或 `CHOOSING` 打开菜单、保存并返回后，原交互状态保持不变。
- 空槽、损坏槽和截图缺失都有明确状态。
- 继续按钮正确选择最近有效存档。
- 设置在重新启动后仍然生效。
- 所有菜单均可仅用键盘完成操作。

### 退出条件

- P0 菜单、设置和存档槽验收项通过。
- UI 不直接写文件或修改剧情变量。
- 返回标题后旧 `VNMode` 已释放，会话处理符合用户选择。

## 12. M7：AVG 辅助功能

状态：`未开始`

### 目标

在核心流程稳定后补充舒适度功能。

### 实施顺序

1. 隐藏/恢复 UI。
2. 已读状态和只快进已读文本。
3. 自动播放。
4. 快速存档和快速读取入口。
5. 截图。
6. 按句回退，并绑定快捷菜单与鼠标滚轮上滑。

### 验证重点

- 快进不会穿过未允许的未读文本。
- 选项出现时自动播放和快进暂停。
- 转场与读取恢复期间不能误推进。
- 回退同时恢复剧情变量、舞台、历史和当前行。
- 回退缓冲只存在内存，不污染磁盘存档。

### 退出条件

- 功能验收表中所有 P1 AVG 项目通过。
- 连续快进、打开菜单、读档、回退的组合操作不产生状态竞争。

## 13. M8：Windows 发布验证

状态：`未开始`

### 目标

生成可分发的 Windows 序章版本，并在干净环境完成回归。

### 预计文件

```text
godot/export_presets.cfg
第三方许可证记录
资源授权记录
发布说明
```

### 工作项

1. 确认所有正式资源授权。
2. 排除未使用字体、手机素材、源图和测试输出。
3. 建立 Windows 导出预设。
4. 在新用户数据目录测试首次启动。
5. 完成新游戏、存档、退出、重启和读取。
6. 记录 Godot、Dialogue Manager 和游戏版本。

### 验证

- 导出包不依赖开发机绝对路径。
- 无编辑器缓存、调试日志、临时存档或未授权素材。
- 16:9、16:10、21:9 显示验证符合 D-002。
- 六条剧情路线和四个关键存档点在导出包中复测。

### 退出条件

- `EXPORT-001` 完成。
- 干净 Windows 环境可启动、通关、保存并重新读取。
- 已知限制和 Ren'Py 旧存档不兼容在发布说明中明确。

## 14. 验收项与阶段映射

| 验收范围 | 主要阶段 |
| --- | --- |
| STORY-001 ～ STORY-006 | M2、M5 |
| STAGE-001 ～ STAGE-006 | M3、M5 |
| DIALOGUE-001 ～ INPUT-004 | M2、M3、M7 |
| SAVE-001 ～ SAVE-012 | M4、M6 |
| VN-001 ～ VN-007 | M5、M7 |
| MENU-001 ～ SETTINGS-004 | M6 |
| TOOLCHAIN-001 | M0、M1 |
| DISPLAY-001 ～ ASSET-003 | M0、M1、M3、M8 |
| EXPORT-001 ～ EXPORT-002 | M8 |

## 15. 每阶段交付规则

每个阶段完成前执行：

1. 只检查本阶段相关的最小测试和 headless 命令。
2. 检查 Godot 输出中是否有解析错误、丢失资源或孤立信号。
3. 对照架构文档确认依赖方向没有倒置。
4. 更新功能验收表状态。
5. 检查 Git 差异，不提交 `.godot/`、导出包、存档或日志。
6. 以一个可独立理解和回退的本地提交保存该阶段。

如果实现结果需要改变模块职责、存档格式或剧情公开命令，应先更新架构文档，再继续扩散修改。

## 16. 下一步

`D-001` 已完成：项目使用 `Godot 4.7.1-stable` 与 `Dialogue Manager v3.10.5`，并已同意在 M1 将插件作为生产依赖加入仓库。

进入 M1 前只需确认 D-002 使用 `keep` 黑边策略，或决定改为 `expand`。

D-003 的背景问题可以暂时按原版表现保留，到 M5 前再决定。
