# 本地使用与发布

在同一个外层目录保留三个独立目录。正式源码仓是 `FM27E0-CAgrowth-source`，日常运行程序和个人数据放在 `FM27E0-CAgrowth-local`，对外发布文件放在 `FM27E0-CAgrowth-releases`。分享时使用 releases 中已校验的 ZIP。

在源码仓打开 PowerShell 后运行以下命令。

```powershell
.\scripts\release.ps1 -DryRun
.\scripts\release.ps1 -Version 1.1.32 -DryRun
.\scripts\release.ps1 -Version 1.1.32
```

- **`-DryRun`** 检查仓库、版本和跟踪文件，列出输出和可清理路径。省略版本时预演下一补丁版。演练不创建缓存或文件。
- **`-SkipLocalUpdate`** 保留自用目录中的 EXE 和运行资料，仍生成和校验对外发布包。
- **`-NoGitCommit`** 生成发布包，按所选参数更新自用目录及清理旧产物。源码仓保留原版本与 Git 状态，源码 ZIP 由已校验的候选树生成。
- 常规发布成功后会提交版本和说明，打轻量标签。提交沿用仓库上一条提交的提交者标识。Git 推送由维护者手动执行。

如需把撰写好的变更记录纳入新版本，可在发布前提交 CHANGELOG.md 的 `## Unreleased` 段落。没有该段落时，脚本添加构建和验证记录。

正式发布从 Git 跟踪的快照构建和测试。源码 ZIP 用 `git archive` 生成，分享 ZIP 从已校验的发布目录生成。两份 ZIP 都会读回校验。自用更新仅清理命名匹配且版本更早的 EXE。旧发布目录含清单外文件时保留，并在命令输出中说明。运行中的旧 EXE 如无法删除，也会保留，关闭后可在下次发布时清理。
