# MyMchose

[![build](https://github.com/Bpazy/my-mchose/actions/workflows/build.yml/badge.svg)](https://github.com/Bpazy/my-mchose/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/Bpazy/my-mchose)](https://github.com/Bpazy/my-mchose/releases)

Windows 托盘小工具，在任务栏右下角显示 MCHOSE 无线鼠标的剩余电量。

原理是直接跟鼠标接收器（`VID 0x3837`）的厂商自定义 HID 集合对话，不依赖官方驱动、不需要常开浏览器。

## 功能

- 托盘图标绘制电池图形，按电量变色（绿 / 黄 / 红）
- 右下角悬浮小窗显示百分比，可拖动、位置记忆、置顶不抢焦点（TrafficMonitor 风格）
- 右键菜单：立即刷新 / 显示悬浮电量窗 / 设置… / 退出
- 设置页：刷新周期（默认 60 分钟）、图标是否显示数字、开机自启
- 上次读到的电量会持久化，重启后先显示上次的值
- 单实例运行，日志写在 `%APPDATA%\MyMchose\app.log`

## 下载

到 [Releases](https://github.com/Bpazy/my-mchose/releases) 下载：

- `MyMchose-<版本>-win-x64.zip`：exe + README + LICENSE
- 或单独的 `MyMchose.exe`

单文件、零依赖，基于系统自带的 .NET Framework 4.8，双击即用。

## 使用

1. 运行 `MyMchose.exe`，托盘和右下角会出现图标/小窗
2. 首次启动电量显示 `--`，把鼠标**关开一次**（或拔插接收器）让它上报一次电量
3. 右键托盘图标可进设置；悬浮窗可以拖到任意位置，位置会记住

> Windows 10 默认把新图标放进「隐藏的图标」溢出区。想让它常驻任务栏：
> 点 `^` 把图标拖到任务栏，或 `设置 → 个性化 → 任务栏 → 选择哪些图标显示在任务栏上`。

## 已知限制

- **没有「主动读电量」的命令**。这款接收器只在链路事件（关开鼠标、接收器重插）时上报电量包，
  所以两次事件之间显示的是上次的值，官方客户端的行为也一样。
- 充电状态暂时无法区分：`0x0D` 包里没有可靠的充电位，驱动前端用的 `0x0A 0x05` 包在这台设备上从未出现。
- 目前只验证了 MCHOSE A7 V3 Ultra+。同厂商其它型号理论上可用，但 LOD / 回报率档位表不同。
- 仅 Windows（WinForms + HID）。

## 构建

只依赖 Windows 自带的 .NET Framework 4.8 C# 编译器，不需要 .NET SDK、NuGet 或 Visual Studio：

```powershell
.\build.ps1                 # 默认版本 0.0.1
.\build.ps1 -Version 0.0.2  # 指定版本，写入 exe 的 AssemblyFileVersion
```

产物：`bin\MyMchose.exe`。

## 调试

程序带一个监听模式，用来抓接收器的原始报文：

```powershell
.\bin\MyMchose.exe --watch 60
```

它会把每个 HID 包的十六进制写进 `%APPDATA%\MyMchose\app.log`，解析出电量时额外记一行 `battery 71%`。
新设备适配基本都靠这个模式抓包。

## 发布

推 tag 即可，GitHub Actions 会自动构建、冒烟测试、打包并发 Release：

```bash
git tag v0.0.2
git push origin v0.0.2
```

也可以在 Actions 页面手动 `Run workflow`（只产出 artifact，不发 Release）。

## 目录

```
src/MyMchose.cs          单文件程序：设置、HID、图标绘制、托盘、悬浮窗
build.ps1                csc 构建脚本（支持 -Version）
docs/protocol.md         HID 协议逆向记录
docs/features.md         官方驱动 / 客户端的功能清单
.github/workflows/       打 tag 自动构建发布
```

## 文档

- [协议笔记](docs/protocol.md)：报告 ID、帧格式、电量包偏移
- [驱动功能清单](docs/features.md)：网页驱动与 M HUB 客户端各有哪些能力

## 许可

MIT
