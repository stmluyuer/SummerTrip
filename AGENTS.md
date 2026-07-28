# SummerTrip 项目工作约定

## 项目边界与事实来源

- 仓库同时保留可运行的 Ren'Py 序章基准和正在迁移的 Godot 4.7.1 .NET 工程。
- `game/` 是 Ren'Py 行为基准；除非任务明确要求修改原版，不移动、改名或删除其中的剧情和资源。
- `godot/content/dialogue/prologue.dialogue` 是 Godot 侧唯一可编辑的序章对话文件。
- `migration/manifests/` 是角色、变量、表现参数、资源路径和授权状态的结构化事实来源。
- `docs/godot_architecture.md`、`docs/godot_implementation_roadmap.md`、`docs/godot_feature_checklist.md` 和 `docs/godot_migration_inventory.md` 分别负责架构、实施顺序、验收状态和迁移输入；实现或状态变化后同步更新相关文档。
- `godot/addons/dialogue_manager/` 是锁定到 v3.10.5 的上游依赖。不要直接修改 vendored 源码；版本、提交和许可证记录在 `godot/third_party/dialogue_manager.NOTICE.md`。

## 开始工作与 Git

- 开始前运行 `git status --short`，识别并保留用户已有修改，不把无关文件混入本次工作。
- 优先做最小、聚焦的改动，避免无关重构、批量格式化或目录调整。
- 完成合理且可验证的任务修改后，自动创建本地 commit；只提交本次任务拥有的文件，不提交缓存、日志、导出包、敏感文件或无法确认归属的改动。
- commit 信息使用英文，简要说明修改目的。未经明确要求不要推送、发布或重写 Git 历史。

## 实现约束

- 当前 Godot 迁移顺序以路线图为准，不跨过阶段门禁提前实现后续系统。
- 剧情文件只使用语义 ID，不写入 `res://` 资源路径、场景节点路径或像素坐标。
- Dialogue Manager API 只通过项目规划的 `DialogueGateway` 边界接入，不把插件调用扩散到 UI、舞台和存档模块。
- 新增依赖、外部服务、权限、网络暴露、数据库迁移或破坏性改动前，先说明原因、影响和替代方案并取得确认。
- 正式资源必须在 `migration/manifests/assets.tsv` 中登记；所有 `action=copy` 的资源必须在 `asset_licenses.tsv` 中有对应授权记录。

## 构建与验证

- 优先运行与改动直接相关的最小验证；功能改动交付前必须构建对应工程。
- 资源或授权清单变更：
  - 本地一致性：`./tools/check_assets.ps1`
  - 正式发布门禁：`./tools/check_assets.ps1 -RequireVerifiedLicenses`
- Godot C# 代码、工程配置或插件接入变更：
  - 构建：在 `godot/` 下运行 `dotnet build SummerTrip.csproj`
  - 使用 Godot 4.7.1 .NET 控制台程序重新导入：`<godot-console> --headless --path <repo>/godot --editor --quit`
  - 运行序章 smoke：`<godot-console> --headless --path <repo>/godot res://tests/smoke_prologue.tscn`
- 找不到 Godot 可执行文件时，先从 PATH、项目文档或本机已安装的 Godot 4.7.1 .NET 目录确认；不要改用非 .NET 版本或其他引擎版本冒充验证。
- Ren'Py 代码或 UI 变更应运行可用的 Ren'Py lint/启动验证；本机没有对应工具时明确说明未验证项。
- 文档或清单修改至少检查链接、路径、数量、阶段状态和实现是否一致。
- 不为小改动运行无关的全量验证；无法运行命令时说明原因和建议的复核方式。

## 发布与资源授权

- 本项目不是 Wox 插件，不部署到 Wox 目录。
- 当前第一发布目标是 Windows；Linux、macOS 和移动端仍需按路线图单独验收。
- 未经明确要求不要创建导出包或覆盖本机发布目录。需要导出时先确认 Godot/Ren'Py 目标、输出目录和现有导出脚本。
- `asset_licenses.tsv` 中存在 `unverified` 或非 `redistributable` 的正式资源时，只允许本地迁移验证，不得视为可公开再发布资产。
- 发布前必须通过 `-RequireVerifiedLicenses`、Windows 导出验证和功能验收表中的对应项目。

## 交付前自检

- 检查语法、逻辑、命名、架构边界和现有行为是否符合预期。
- 检查是否泄露密钥、token、密码、个人信息或本机绝对发布路径。
- 检查是否引入不必要的依赖、复杂度、缓存文件或第三方源码改动。
- 报告改了什么、为什么、如何验证、剩余风险，以及用户接下来可以做什么；只声明实际运行并看到结果的验证。
