# SummerTrip

以秋姐妹为主角的短篇视觉小说序章。误入妖怪之山山脚的旅人，在秋穰子与秋静叶的帮助下调查季节异常；选择会通向穰子、静叶或姐妹共同行动的序章结尾。

仓库保留 Ren'Py 序章作为行为基准，同时进行 Godot .NET 迁移。两套工程的存档不兼容，Godot 侧的完成情况以[功能对等验收表](docs/godot_feature_checklist.md)为准，其中仍有进行中的项目。

## 从哪里开始

| 目的 | 入口 |
| --- | --- |
| 阅读序章体验版的操作和存档说明 | [README.txt](README.txt) |
| 查看 Ren'Py 原版剧情与资源 | [game/](game/) |
| 打开 Godot 工程 | [godot/project.godot](godot/project.godot) |
| 查看迁移架构与实施顺序 | [架构](docs/godot_architecture.md) · [路线图](docs/godot_implementation_roadmap.md) |
| 核对资源来源与授权状态 | [migration/manifests/](migration/manifests/) |

## 源码与运行

GitHub 仓库当前没有提供 Release 安装包。`README.txt` 中双击 `SummerTrip.exe` 的步骤适用于已经导出的 Ren'Py 发布包，克隆源码不会得到这个程序。

- Ren'Py：`game/` 保存原版脚本和资源，供原工程运行及行为对照。
- Godot：使用 Godot **4.7.1 .NET**，导入 `godot/project.godot`，先构建 C# 工程，再由编辑器运行。普通非 .NET 版编辑器无法编译 C#。
- 第一发布目标为 Windows，建议以 1920×1080 查看；Linux、macOS 和移动端的发布验收仍需分别完成。

## 素材使用

资源的作者、来源、许可证和再发布范围尚未全部补齐。资源可用于当前迁移核对，不代表可以公开再分发；发布前需检查授权清单及对应门禁。原版的 `source` 与 `unused` 素材不进入正式构建包。

剧情、操作、三条序章结尾及玩家存档说明仍保留在 [README.txt](README.txt)。
