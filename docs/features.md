# MCHOSE 驱动功能清单（web 端与 M HUB 客户端）

> 来源：web 驱动 `https://www.mchose.com.cn` 的打包产物（`main-CZx0cRYH.js`，7.6 MB）
> 与官方客户端 `D:\MyPrograms\M HUB`（Electron，`resources/app.asar` 29.8 MB + `out/preload/index.js`）。
> 客户端的主进程是 bytenode 字节码（`index.jsc`），渲染层仍是同一套 web 页面，
> 因此「设备配置」部分两者完全一致，客户端多出来的是原生能力。

## 一、设备配置

页签由 `removedTabs` 控制，不同型号按 `featSupport` 裁剪。

### 1. 按键 `keySetting`

* 五个主键：左 / 右 / 中 / 前进 / 后退，外加 DPI 键
* 每个键三要素：`type`（类型）+ `value`（键值），例如鼠标左键 `00/0001`、右键 `00/0002`、中键 `00/0004`、前进 `00/0010`、后退 `00/0008`
* DPI 键默认为「切换 DPI」：`01/000000`
* 键值表：`mouseMap` / `keyboardMap` / `consumerMap` / `dpiMap` / `systemkeyWinMap` / `disableMap` / `switchOnboardMap`
* 支持高级按键（组合键、宏、切换板载配置、禁用）

### 2. DPI `dpiSetting`

* 最多 6 档（`dpiSum`），当前档位 `usbDpiIndex`（0 基）
* 范围按型号：`dpiMax` 常见 12000 / 18000 / 26000 / 42000 / 50000，A7 V3 Ultra+ 为 50000
* X/Y 轴独立（`dpiHasY`、`dpiY0..dpiY5`）
* 指示灯颜色、方向（`dpiDirection`）、DPI 档位循环切换

### 3. 性能 `performanceSetting`

| 项 | 字段 | 说明 |
|---|---|---|
| 回报率 | `usbRateIndex` / `gRateIndex` | 8K 机型可选 125 / 500 / 1000 / 2000 / 4000 / 8000 Hz；另有游戏模式档 |
| 静默高度 | `lodIndex` | 0.7 / 0.9 / 1.2 / 1.4 / 1.7 mm |
| 按键消抖 | `leftKeyDebounce` / `rightKeyDebounce` | 毫秒 |
| 休眠 | `sleep` / `sleepMode` | 时间档位 + 模式 |
| 传感器 / 旋转 | `sensor` / `rotateVal` | 旋转角度 |
| 游戏模式 | `gameMode` | 开 / 关 |

### 4. 灯光 `lightSetting`

* 灯效模式 `lightMode`、颜色表 `lightModeColor1..N`、亮度 `brightness`、速度 `speed`、多色 `multiColor`
* 部分型号另有 `magDocklightSetting`（充电底座灯效）

### 5. 其他 `otherSetting`

* 恢复出厂设置 `restoreFactorySetting`
* 自适应功耗 / 省电提示（`adaptivePowerMode`）
* 低电量提醒开关
* 板载配置管理（见下）

## 二、板载配置与云

* 板载槽位：`maxConfigNum`（A7 V3 Ultra+ 为 4），默认 3 个，可命名、切换 `profileIndex`
* 配置导入 / 导出（`importData` / `exportData`）
* 云端模板：`getCloudTemplateList`、分享 / 取消分享、按 `name + id` 拉取
* 宏：`macroList`、`activeMacroM7`（本地）、云端宏（`mouse-macro`），支持按键序列、延时、循环

## 三、固件升级（OTA）

* 按型号下发 `Mouse_Main` / `Mouse_Sub` / `Dongle_Main` / `Dongle_Sub` 固件，版本号比对后升级
* 配置来自 `configCenter/custom/newMouseConfig.json`（base64 → zip → `data.json`）
* 升级通道走 `workerUpgrade`，接收器与鼠标分别升级

## 四、客户端额外能力（M HUB）

`resources/native-addons` 下的原生模块 + `out/preload/index.js` 暴露的 270 个 IPC 通道：

| 能力 | 通道 / 模块 |
|---|---|
| USB 描述符直读 | `getUsbDeviceDescriptor`、`getUsbStringDescriptor`、`getFWVersionFromUsbDescriptor`、`getUsbVersion`；依赖 `node_modules/usb` |
| 音频处理 | `audio-sdk:*`、`audio-thx:*`、`setEQ_Slider`、`setVIRTUALBASS_Level`、`setXearSurroundMode`、`setVOICECLARITY_LEVEL`、`setAINREnable`、`setMagicVoice_Selection`、`setADAPTIVEVOLUME_MODE` |
| 游戏 / 屏幕感知 | `game-interaction:*`、`screen-sync:*`、`scanGameList`、`onActiveAppChange`（`game-analysis`、`visual-capture`） |
| 输入监听与模拟 | `raw-input:keyboard`、`keyboard-capture:*`、`input-simulator:key-down/key-up`（宏可在任意窗口触发） |
| 多媒体 | `startLyricsMonitor`、`readKuGouKrc`、`readQQMusicQrc` |
| 系统集成 | `createTrayWindow`、`setAutoStart`、`showWinToast`、`openWindowsMouseSetting`、`localFiles:*` |
| 升级器 | `updater:*`、`otaDevice`、`getFirmwareBinData` |

> 注意：客户端主进程字节码里没有任何 `battery` 相关代码，`battery_info` 只是主进程 → 渲染层的转发通道；
> 电量依旧由渲染层走 HID，与 web 端同一套逻辑。

## 五、对 MyMchose 的意义

**已经在用**：

* `0x0D` 链路状态包 → 电量（第 11 字节）
* `02 03` 读设置 → `11 03` 应答里的 6 档 DPI、`dpiSum`、当前档位

**可以借鉴**：

* 低电量提醒（`mouseLowBatteryNotifySwitch`）
* 开机自启、托盘菜单、悬浮窗（已实现）
* OTA / 固件版本提示（暂不做）

**做不到**：

* 没有「主动读电量」的命令。`02 06` 在这款接收器上不响应，`02 00`–`02 31` 只有 `02 03` 有应答，
  重放页面的 `11 03` 设置帧也只回显。电量只能等链路事件（关开鼠标、接收器重插）。
