# MyMchose

Windows 托盘小工具，在任务栏右下角显示 MCHOSE 无线鼠标的剩余电量，类似 TrafficMonitor。

- 托盘图标内直接绘制电量百分比（按电量变色：绿 / 黄 / 红）
- 右键菜单：立即刷新、设置…、退出
- 设置页：刷新周期（默认 60 分钟）、是否在图标上显示数字、开机自启
- 配置保存在 `%APPDATA%\MyMchose\settings.json`，日志在 `%APPDATA%\MyMchose\app.log`

## 构建

只依赖 Windows 自带的 .NET Framework 4.8 C# 编译器，不需要安装 .NET SDK / Visual Studio：

```powershell
.\build.ps1
```

产物：`bin\MyMchose.exe`（单文件，无第三方依赖）。

## 运行

```powershell
.\bin\MyMchose.exe
```

## 原理

鼠标电量属于厂商私有配置，Windows 没有标准接口暴露它。程序找到接收器的厂商自定义 HID 集合，
按厂商协议发送「读取设备信息」指令，再从响应里取出电量字段。

> 协议细节与逆向记录见 `docs/protocol.md`。

## 许可

MIT
