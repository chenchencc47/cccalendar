# cccalendar 开发说明

## 环境

- Windows 11 x64
- .NET SDK 10.0.400 或同特性带的更新补丁
- Inno Setup 6

仓库通过 `global.json` 和 `dotnet-tools.json` 固定 SDK 特性带与本地工具。首次拉取后执行：

```bat
dotnet tool restore
eng\verify.cmd
```

## 验证

`eng\verify.cmd` 依次执行依赖还原、格式检查、Debug 编译、全部测试和 Cobertura 覆盖率采集。任何一步失败都返回非零退出码。

提交代码前必须运行：

```bat
eng\verify.cmd
```

## 发布

`eng\publish.cmd` 先执行完整验证，然后生成自包含单文件发布、启动冒烟测试和 Inno Setup 安装器：

```bat
eng\publish.cmd
```

输出位置：

- 自包含应用：`artifacts\publish\win-x64`
- 安装器：`artifacts\installer`

## Git

- 主分支为 `main`。
- 提交保持单一目的，格式为 `type(scope): summary`，例如 `feat(tasks): add quadrant rules`。
- 功能提交必须包含先失败后通过的测试；纯文档或机械脚手架可以在 worklist 中说明 Red 不适用。
- 不提交 `artifacts`、数据库、日志、备份、附件、密钥或本机配置。
