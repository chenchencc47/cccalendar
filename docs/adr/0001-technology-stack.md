# ADR 0001：Windows 桌面技术栈

- 状态：已接受
- 日期：2026-08-16

## 背景

cccalendar 是 Windows 单平台桌面应用，需要稳定使用托盘、多窗口、桌面层级、全局快捷键、系统通知、截图、剪贴板、窗口置顶和本地数据库。界面需要高度定制，但不需要跨平台运行时。

## 决策

采用以下基础技术栈：

- 运行时：.NET 10 LTS，x64。
- 桌面框架：WPF，主支持 Windows 11 22H2（build 22621）及更高版本。
- 架构：模块化单体，保持 Core、Infrastructure、Desktop 三个生产项目。
- UI 模式：MVVM；使用 CommunityToolkit.Mvvm，避免自建命令和通知样板代码。
- 依赖注入、配置与日志抽象：Microsoft.Extensions 系列。
- 数据库：SQLite + Entity Framework Core；集成测试使用真实临时 SQLite 数据库。
- 测试：xUnit；领域测试不依赖 WPF、网络或数据库。
- 日历交换与重复规则：优先采用成熟的 Ical.Net，不自行实现 ICS 解析器。
- HTTP 集成：HttpClient；天气与 LLM 通过应用内接口隔离。
- 图标：统一使用 Lucide 线性图标；没有合适图标时才补充自有矢量资源。
- 开发发布：`win-x64` 自包含发布；安装器在打包阶段选用 Inno Setup。

解决方案初始结构：

```text
src/
  CcCalendar.Core/             领域模型与应用用例
  CcCalendar.Infrastructure/   SQLite、文件、网络和系统适配器
  CcCalendar.Desktop/          WPF UI、组合根和 Windows 窗口
tests/
  CcCalendar.Core.Tests/
  CcCalendar.Infrastructure.Tests/
  CcCalendar.Desktop.Tests/
```

只有在实际出现独立部署或依赖冲突时才继续拆分项目。

## 理由

WPF 的窗口模型和 Win32 互操作适合本项目的大量 Windows 原生功能。.NET 10 是当前 LTS，测试、打包和本地数据库生态成熟。相比 Electron，它不需要携带浏览器运行时；相比 Tauri，它减少了 TypeScript、Rust 和 Windows API 三套边界同时存在的复杂度；相比 WinUI 3，WPF 的托盘、多窗口、自动化测试和长期稳定性更适合个人生产力工具。

## 未选择方案

- Electron：开发速度快，但内存和安装体积偏高，原生窗口能力仍需额外桥接。
- Tauri 2：体积小，但本项目的大量 Windows 特性会增加 Rust、WebView 和前端之间的集成面。
- WinUI 3：视觉更接近新 Windows，但当前项目更看重成熟窗口行为与测试工具链。
- Avalonia：跨平台能力不是当前需求，不能抵消 Windows 专属功能的适配成本。

## 约束

- 领域层不得引用 WPF、EF Core、Windows API 或具体外部服务 SDK。
- 不为尚未实现的模块预建通用框架。
- NuGet 依赖必须是完成当前 work item 所需的最小集合，并使用中央版本管理。
- 外部服务必须有可在测试中替换的窄接口。
- Windows 10 不作为首要验收目标；不依赖 Windows 11 的核心能力可尽量保持兼容。

