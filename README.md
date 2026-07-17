# UsageTray

一个轻量的 Windows 任务栏常驻工具，定时读取兼容 CCSwitch 配置的用量接口，并把剩余额度以内嵌工具条的形式显示在任务栏通知区域左侧。

## 功能

- 请求 `GET {{baseUrl}}/v1/usage`
- 使用 `Authorization: Bearer {{apiKey}}`
- 兼容以下余额字段，优先级与 CCSwitch 配置一致：
  - `remaining`
  - `quota.remaining`
  - `balance`
- 兼容 `unit`、`quota.unit`、`is_active`、`isValid`
- 如果接口额外返回 `total`、`limit` 或 `used`，悬停提示中也会展示总量
- 定时刷新、手动刷新、连接测试、开机启动
- Hover 展示服务端返回的今日费用、请求数、Token 拆分、TPM/RPM 和平均响应时间
- Hover 展示累计用量与模型累计 Top 3；右键菜单提供今日摘要
- 左键工具条立即刷新，双击打开设置，右键打开菜单
- Explorer 重启或任务栏尺寸变化后自动重新挂接
- 如果当前系统无法挂接任务栏，自动退化为普通通知区域图标
- API Key 使用 Windows DPAPI 加密，仅当前 Windows 用户可解密
- 后台检查 GitHub Release；发现新版本后由用户确认下载、校验、替换并自动重启
- 专用更新窗口展示 Release Notes：清理常见 Markdown，支持独立滚动阅读
- 更新窗口直接展示 Release Notes 详情，不提供额外跳转入口

## 使用

运行 `UsageTray.exe`，首次启动会弹出设置窗口：

1. 填写 Base URL，例如 `https://example.com`。
2. 填写 API Key。
3. 点击“测试连接”，确认能读到余额。
4. 保存后，余额工具条会常驻在任务栏通知区域左侧。

工具条通过独立进程挂接到 Windows 任务栏，不会向 Explorer 注入 DLL。它贴在通知区域左侧并覆盖该处的空白带，不会重排任务栏按钮；任务栏按钮很多时，两者可能靠得较近。Windows 没有为这种内嵌工具条提供稳定的公开 API，因此新版 Windows 任务栏发生重大变化时，程序会退化为普通通知区域图标。

Base URL 可以填写服务根地址、以 `/v1` 结尾的地址，或完整的 `/v1/usage` 地址，程序会自动避免重复路径。

配置保存在：

```text
%LOCALAPPDATA%\UsageTray\settings.json
```

## 开发与打包

需要 .NET 8 SDK：

```powershell
dotnet build .\UsageTray\UsageTray.csproj
dotnet publish .\UsageTray\UsageTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o .\artifacts\portable-win-x64
```

发布目录中的 `UsageTray.exe` 是可直接运行的 64 位 Windows 单文件程序。

## 发布与自动更新

应用从 [GitHub Releases](https://github.com/RickChen764/UsageTray/releases) 检查更新。每个 Release 必须包含：

```text
UsageTray-win-x64.exe
UsageTray-win-x64.exe.sha256
```

发布新版本时更新 `UsageTray.csproj` 中的版本号并推送同名标签，例如 `v1.2.0`。GitHub Actions 会运行测试、生成自包含单文件、创建 SHA-256 校验文件并发布 Release。

如需为某个版本编写详细说明，可添加 `release-notes/v版本号.md`，例如 `release-notes/v1.2.0.md`。没有专用文件时，发布流程会将两个版本标签之间的提交标题自动整理为变更清单。

客户端启动后检查一次，之后每 6 小时检查。检测到新版本时只提示用户；获得确认后才会下载、校验、备份替换并自动重启。安装失败时恢复旧版本。
GitHub API 遇到匿名限流时会自动改用公开的 Releases 重定向检查，不需要在客户端保存 GitHub Token。
