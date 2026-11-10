# CLAUDE.md

给 AI 助手（Claude Code 等）在本仓库工作时的说明。人类读者请看 `README.md`。

## 项目定位

Windows 托盘程序，读 MCHOSE（迈从）无线鼠标接收器的私有 HID 协议拿电量并显示。
它**不是**官方驱动，只做「读电量」这一件事，不要扩张成完整驱动。

## 技术栈约束（重要）

- 语言 C#，目标 **.NET Framework 4.8**，用系统自带 `csc.exe` 编译（`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`）。
- **不要引入 .NET SDK 项目、csproj、NuGet 包、第三方 dll**。整个程序保持单文件 + 无依赖，
  这是刻意的取舍：用户双击就能跑，不需要装运行时。
- 因此语言级别停在 **C# 5**：不能用字符串插值 `$"..."`、空传播 `?.`、`nameof`、
  表达式体成员、`using static`、元组、`out var`。用 `string.Format`、显式 null 判断、`delegate { }`。
- 只支持 Windows；UI 用 WinForms + System.Drawing。
- 源码是无 BOM 的 UTF-8，`build.ps1` 必须带 `/codepage:65001`，否则在英文系统（GitHub runner）上
  中文文案会变乱码。

## 常用命令

```powershell
.\build.ps1                  # 构建到 bin\MyMchose.exe
.\build.ps1 -Version 0.0.2   # 指定版本（写入 AssemblyFileVersion）
.\bin\MyMchose.exe           # 托盘模式
.\bin\MyMchose.exe --watch 60  # 诊断模式：把原始 HID 报文写进日志
```

日志与配置：`%APPDATA%\MyMchose\app.log` / `settings.json`。
验证改动时先跑 `--watch`，确认程序能启动、能拿到包。

## 代码结构（都在 `src/MyMchose.cs` 一个文件里）

| 区块 | 职责 |
|---|---|
| `AppSettings` | `settings.json` 读写（手写正则解析，不引 JSON 库） |
| `Log` | 追加日志 |
| `HidNative` | hid.dll / kernel32 P/Invoke，含 OVERLAPPED 异步读写 |
| `MouseDevice` | 枚举设备、开句柄、发请求帧、`Pump` 循环收包、`Watch` 诊断 |
| `BatteryMonitor` | 后台线程常驻监听，线程安全地发布最新读数 |
| `IconPainter` | 用 GDI+ 画托盘图标 |
| `SettingsForm` | 设置窗口 |
| `BatteryForm` | 右下角悬浮电量窗（无边框、置顶、可拖） |
| `TrayContext` | NotifyIcon、菜单、定时器、UI 更新 |
| `Program` | 入口、单实例互斥、`--watch` 分支 |

## 协议事实（别重复踩坑）

详见 `docs/protocol.md`，要点：

- 通道：接收器复合设备 `0x3837:0x1018` 的 `MI_02&Col02`（Usage Page `0xFF01`）。
  **HID 报告 ID `0x4D`**，负载 63 字节。
- 帧格式：`01 01 <cmd> <sub> <args…>`；`02 03` 读设置，`11 03` 是设置包，`01 xx` 是确认包。
- 电量在 **`0x0D` 链路状态包的第 11 字节**（`0x0C` 表示断开）。它是事件上报，不是应答。
- **不存在「读电量」请求命令**：`02 06` 不响应，`02 00`–`02 31` 里只有 `02 03` 有应答，
  重放页面的 `11 03` 设置帧也只回显。不要再花时间扫描命令字。
- 鼠标/键盘顶层集合（`MI_00`、`MI_01 Col01`）被系统拒绝打开（`ERROR_ACCESS_DENIED`），
  不要尝试。
- 61 字节的 `MI_02&Col01` 所有写入都失败；`MagDock`（`0x1012`）只回零。都别用。

## 改动约定

- 改协议相关逻辑 → 同步更新 `docs/protocol.md`。
- 加功能 → 同步更新 `docs/features.md`（如果是在对标官方驱动）和 `README.md`。
- 新增界面文案用中文，沿用现有措辞风格。
- 提交信息用英文、祈使句，一行说清做了什么。
- 不要提交 `bin/`、`obj/`、`_scratch/`（已在 `.gitignore`）。

## 发布

推 tag 触发 `.github/workflows/build.yml`：构建 → 冒烟测试（`--watch 2` 退出码）→ 打包 → 发 Release。

```bash
git tag v0.0.2
git push origin v0.0.2
```

workflow 也可以手动 `Run workflow`，只产出 artifact 不发 Release。

## 不要做的事

- 不要为了「能读到电量」去写任何设置类命令试探设备（有把鼠标写坏 / 进 DFU 的风险）。
- 不要把程序改成常驻管理员权限、不要装驱动、不要动注册表里与设备无关的项。
- 不要在前台弹窗、抢焦点；悬浮窗必须 `WS_EX_NOACTIVATE`。
