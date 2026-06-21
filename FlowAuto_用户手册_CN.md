# FlowAuto 用户手册

> 版本 5.1 | 2026-06-21

## 目录

1. [简介](#1-简介)
2. [启动与运行](#2-启动与运行)
3. [界面概览](#3-界面概览)
4. [节点类型详解](#4-节点类型详解)
   - 4.1 [StartProgram — 启动程序](#41-startprogram--启动程序)
   - 4.2 [ClickElement — 点击界面元素](#42-clickelement--点击界面元素)
   - 4.3 [WaitCondition — 等待条件](#43-waitcondition--等待条件)
   - 4.4 [KeyPress — 按键操作](#44-keypress--按键操作)
   - 4.5 [Gate — 逻辑门](#45-gate--逻辑门)
   - 4.6 [Loop — 循环容器](#46-loop--循环容器)
   - 4.7 [Condition — 条件分支](#47-condition--条件分支)
   - 4.8 [ColorMotion — 颜色运动节点](#48-colormotion--颜色运动节点)
   - 4.9 [ColorCal — 颜色计算节点](#49-colorcal--颜色计算节点)
5. [编辑器操作指南](#5-编辑器操作指南)
6. [流程文件管理](#6-流程文件管理)
7. [执行流程](#7-执行流程)
8. [截图工具使用](#8-截图工具使用)
9. [窗口选择器](#9-窗口选择器)
10. [取色器工具](#10-取色器工具)
11. [帮助系统](#11-帮助系统)
12. [示例流程](#12-示例流程)
13. [常见问题](#13-常见问题)
14. [附录 A：扫描码速查表](#附录-a扫描码速查表)

---

## 1. 简介

FlowAuto 是一个可视化流程编排自动化引擎，允许你通过图形界面（GUI）自由组合配置自动化步骤，如启动程序、等待窗口、图像识别、模拟键盘鼠标操作等，然后将流程保存为 JSON 文件并运行。

**核心场景示例**：启动一个程序 → 等待窗口出现 → 识图点击按钮 → 按键操作 → 运行其他工具，全程无需编写代码。

---

## 2. 启动与运行

### 2.1 系统要求

| 要求        | 说明                             |
| ----------- | -------------------------------- |
| 操作系统    | Windows 10/11 (x64)              |
| .NET 运行时 | .NET 8.0 Desktop Runtime (x64)   |
| VC++ 运行时 | Visual C++ Redistributable (x64) |

如果没有安装 .NET 8.0 Desktop Runtime：前往 [微软官网](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) 下载安装。

### 2.2 启动程序

双击 `FlowAuto.exe`，系统会弹出**用户账户控制（UAC）窗口**，点击"是"授予管理员权限后程序启动。

> FlowAuto 需要管理员权限才能正常执行窗口控制和键盘鼠标模拟操作。

---

## 3. 界面概览

| 区域               | 位置     | 功能                               |
| ------------------ | -------- | ---------------------------------- |
| **工具栏**   | 顶部     | 新建、加载、保存、运行、暂停、停止 |
| **工具箱**   | 左侧     | 9 种节点类型的列表，可拖拽到画布   |
| **流程画布** | 中间     | 节点卡片与连线的可视化编辑区域     |
| **属性面板** | 右侧上方 | 编辑选中节点的所有参数             |
| **执行日志** | 右侧下方 | 运行时的日志输出                   |

### 3.1 工具栏按钮

| 按钮                 | 功能      | 说明                                       |
| -------------------- | --------- | ------------------------------------------ |
| **New**        | 新建      | 清空当前画布，开始新流程                   |
| **Load**       | 加载      | 从 `*.flow.json` 文件加载流程            |
| **Save**       | 保存      | 将当前流程保存为 `*.flow.json` 文件      |
| **Run**        | 运行      | 开始执行整个流程（绿色）                   |
| **Pause**      | 暂停/继续 | 暂停 / 恢复正在执行的流程（黄色/绿色切换） |
| **Stop**       | 停止      | 立即终止正在执行的流程（红色）             |
| **Snip**       | 截图      | 打开全屏截图工具，框选区域保存为模板图片   |
| **Pick Win**   | 选取窗口  | 鼠标悬停目标窗口后按 Ctrl 自动获取窗口标题 |
| **Pick Rgn**   | 选取区域  | 在目标窗口上拖拽框选坐标区域               |
| **Pick Color** | 取色器    | 屏幕取色 + HSV 容差配置                    |
| **Pick Key**   | 拾取按键  | 按下任意键捕获扫描码（v5.1 新增）        |
| **Settings**   | 全局设置  | 配置全局前/后摇延迟等参数                  |

> **快捷键**：`Ctrl+S` 可随时停止正在执行的流程。

### 3.2 全局设置

点击工具栏 **Settings** 按钮打开全局设置对话框。

| 参数                            | 默认值 | 说明                                       |
| ------------------------------- | ------ | ------------------------------------------ |
| **Pre-click Delay (ms)**  | 500    | 所有点击动作前默认等待时间（可被节点覆盖） |
| **Post-click Delay (ms)** | 500    | 所有点击动作后默认等待时间（可被节点覆盖） |

**前/后摇回退规则**（优先级从高到低）：

1. 节点的 `PreDelayMs` / `PostDelayMs` 参数（如果显式设置了非默认值）
2. 全局设置中的值
3. 内置默认值（各 500ms）

> **设计原因**：游戏和桌面应用中，鼠标操作前后留出缓冲时间可避免"点击过快被吞"的问题。

---

## 4. 节点类型详解

所有节点共享以下**通用属性**：

| 属性         | 类型 | 默认值 | 说明                     |
| ------------ | ---- | ------ | ------------------------ |
| Name         | 文本 | —     | 节点的自定义名称         |
| Enabled      | 开关 | true   | 是否启用此节点           |
| Timeout (ms) | 数字 | 30000  | 节点执行超时时间（毫秒） |
| Retry Count  | 数字 | 3      | 失败后重试次数           |

---

### 4.1 StartProgram — 启动程序

启动一个外部可执行程序。

| 参数               | 类型 | 说明                                            |
| ------------------ | ---- | ----------------------------------------------- |
| FilePath           | 文本 | 可执行文件完整路径，如 `D:\Game\launcher.exe` |
| WorkingDirectory   | 文本 | 工作目录（可选）                                |
| Arguments          | 文本 | 启动参数（可选）                                |
| RunAsAdmin         | 开关 | 是否以管理员身份运行                            |
| WaitForWindowMs    | 数字 | 启动后等待窗口出现的最大时间                    |
| WindowTitleKeyword | 文本 | 用于确认启动成功的窗口标题关键词                |

**执行流程**：启动程序 → 等待指定时间 → 查找包含关键词的窗口 → 将窗口句柄存入上下文供后续节点使用。

---

### 4.2 ClickElement — 点击界面元素

在目标窗口中定位并点击指定位置。

| 参数                   | 类型 | 说明                                           |
| ---------------------- | ---- | ---------------------------------------------- |
| TargetWindow           | 文本 | 目标窗口的标题关键词                           |
| LocateMode             | 下拉 | 定位模式：Coordinate / TemplateMatch / OCR / HSVClick / HSVTemplateMatch |
| Region                 | 区域 | 搜索区域（相对于窗口客户区）                   |
| TemplateImagePath      | 文本 | 模板图片路径（TemplateMatch / HSVTemplateMatch 模式） |
| TemplateMatchThreshold | 小数 | 匹配阈值，默认 0.8                             |
| TemplateScaleRange     | 范围 | 多尺度匹配范围（Min/Max/Step）                 |
| OCRText                | 文本 | 要识别的文字（OCR 模式）                       |
| ClickOffset            | 坐标 | 点击位置相对于匹配中心的偏移                   |
| TargetRgb              | RGB  | 目标颜色（HSVClick / HSVTemplateMatch 模式）   |
| HueTolerance           | 数字 | 色相容差（HSV 模式），默认 8                   |
| SVTolerance            | 数字 | 饱和度/明度容差（HSV 模式），默认 30           |
| PreDelayMs             | 数字 | 移动鼠标后点击前的延迟（默认 500ms，全局可配） |
| PostDelayMs            | 数字 | 点击后的延迟（默认 500ms，全局可配）           |

**五种定位模式**：

1. **Coordinate（坐标）**：直接点击 Region 区域的中心点
2. **TemplateMatch（模板匹配）**：截屏 → 模板匹配 → 点击匹配中心。支持 0.5x~1.5x 多尺度匹配
3. **OCR（文字识别）**：识别指定文字位置后点击
4. **HSVClick**（v5.1 新增）：先进行 HSV 颜色过滤 → 点击最大匹配颜色区域的中心。不需要模板图片
5. **HSVTemplateMatch**（v5.1 新增）：先进行 HSV 颜色过滤 → 在过滤后的图像上进行模板匹配 → 点击匹配中心。需要模板图片，支持在过滤图像上进行 0.5x~1.5x 多尺度匹配

---

### 4.3 WaitCondition — 等待条件

等待某个条件成立后才继续执行后续节点。

| 参数                   | 类型 | 说明                                                              |
| ---------------------- | ---- | ----------------------------------------------------------------- |
| TargetWindow           | 文本 | 目标窗口标题关键词                                                |
| ConditionType          | 下拉 | ImageAppear / ImageDisappear / OCRContain / WindowExist / Timeout |
| WaitMs                 | 数字 | 等待时长（仅 Timeout 类型），如不设置则使用节点 TimeoutMs         |
| Region                 | 区域 | 监控区域                                                          |
| TemplateImagePath      | 文本 | 模板图片（ImageAppear / ImageDisappear）                          |
| TemplateMatchThreshold | 小数 | 匹配阈值，默认 0.8                                                |
| OCRText                | 文本 | 等待出现的文字（OCRContain）                                      |
| CheckIntervalMs        | 数字 | 检查间隔（毫秒），默认 500                                        |
| TimeoutMs              | 数字 | 超时时间（毫秒），默认 30000                                      |

**五种条件类型**：

- **ImageAppear**：等待指定区域出现模板图片
- **ImageDisappear**：等待指定区域的模板图片消失
- **OCRContain**：等待窗口中出现指定文字
- **WindowExist**：等待指定窗口标题的窗口出现
- **Timeout**：等待固定时间后继续

---

### 4.4 KeyPress — 按键操作

向目标窗口发送键盘按键。

| 参数           | 类型 | 说明                                                 |
| -------------- | ---- | ---------------------------------------------------- |
| KeyScanCode    | 数字 | 键盘扫描码（如 A=30, F=33, ESC=1）                   |
| KeyName        | 文本 | 按键名称辅助字段（如 "F", "ESC"）                    |
| PressMode      | 下拉 | Press（按下抬起）/ Hold（持续按住）/ Release（释放） |
| HoldDurationMs | 数字 | 按住时长（Hold 模式）                                |
| TargetWindow   | 文本 | 要激活的窗口标题（可选）                             |

**Pick Key 拾取按键**（v5.1 新增）：点击工具栏 **Pick Key** 按钮，然后按下键盘上的任意按键。FlowAuto 会注册一个临时低级键盘钩子，捕获首次 `WM_KEYDOWN` 事件并提取硬件扫描码，自动将 `KeyScanCode` 和 `KeyName` 回填到选中的 KeyPress 节点中，同时弹出确认框显示捕获到的按键和扫描码。

**内置扫描码表**（十进制，对应 KeyScanCode 参数）：

| 按键  | 扫描码 | 按键 | 扫描码 |
| ----- | ------ | ---- | ------ |
| ESC   | 1      | F1   | 59     |
| SPACE | 57     | F2   | 60     |
| ENTER | 28     | F3   | 61     |
| TAB   | 15     | F4   | 62     |
| SHIFT | 42     | F5   | 63     |
| CTRL  | 29     | F6   | 64     |
| A     | 30     | F7   | 65     |
| D     | 32     | F8   | 66     |
| E     | 18     | F9   | 67     |
| F     | 33     | F10  | 68     |
| Q     | 16     | F11  | 87     |
| W     | 17     | F12  | 88     |
| S     | 31     | Z    | 44     |
| 0     | 11     | 1    | 2      |
| 9     | 10     | 2~8  | 3~9    |

> 完整映射见 [附录 A](#附录-a扫描码速查表)。KeyName 支持大小写不敏感的字母/功能键名称。

---

### 4.5 Gate — 逻辑门

Gate 节点接收**两个上游节点的信号**，通过基础逻辑运算产生**单一结果**，输出给下游节点。

| 参数          | 类型 | 说明                                         |
| ------------- | ---- | -------------------------------------------- |
| GateLogicType | 下拉 | **AND** / **OR** / **NOT** |

#### 端口设计

Gate 节点在画布上显示 **2 个输入端口**（顶部，标记为 0 / 1）和 **1 个输出端口**（底部，标记为 Result）：

- **Input0 / Input1**：接收上游节点的执行完成信号
- **Result**：输出运算结果，连接到下一个要继续执行的节点

> 未连接的输入端口会被忽略。NOT 模式只使用 Input0。

#### 逻辑类型说明

| 类型          | 运算规则                           | 典型场景                         |
| ------------- | ---------------------------------- | -------------------------------- |
| **AND** | 两个输入都为 true，结果才为 true   | 多个条件必须同时满足才能继续     |
| **OR**  | 任意一个输入为 true，结果即为 true | 多个条件中满足一个即可继续       |
| **NOT** | 对 Input0 的结果取反               | 条件反转，例如"条件不成立时继续" |

---

### 4.6 Loop — 循环容器

将子节点重复执行。Loop 支持两种工作模式，具有两个出口。**不需要单独的 Break 节点**。

| 参数      | 类型 | 说明                                                                   |
| --------- | ---- | ---------------------------------------------------------------------- |
| LoopMode  | 下拉 | **FixedCount**（固定次数）/ **BreakCondition**（条件中断） |
| LoopCount | 数字 | 循环次数，仅 FixedCount 模式（0 = 无限循环）                           |

#### 模式一：FixedCount（固定次数）

按指定次数循环执行。

#### 模式二：BreakCondition（条件中断）

循环无限执行，直到收到中断信号。Loop 节点**左侧**出现一个 **⚪ 白色 BreakCond 端口**：

- 将 Condition / ColorMotion 等判断节点的 True 输出连接到这个白色端口
- 当判断条件满足时，Loop 自动退出循环
- Loop 从 **Complete 端口**（绿色）离开

> **无需单独的 Break 节点**：循环结束判断逻辑完全由 Loop 节点自身处理。

#### 端口设计

| 端口      | 颜色 | 位置       | 说明                                 |
| --------- | ---- | ---------- | ------------------------------------ |
| Input     | 白色 | 顶部中央   | 循环体回连入口                       |
| BreakCond | 白色 | 左侧中央   | 仅 BreakCondition 模式，接收中断信号 |
| Complete  | 绿色 | 底部左 1/3 | 循环正常完成出口                     |
| Break     | 红色 | 底部右 1/3 | 循环异常/中断出口                    |

#### BreakCondition 模式示例

```
Loop (BreakCondition 模式)
  ├── 循环体节点...
  ├── Condition (ImageAppear, 检测"停止"图标)
  │     └── True 输出 → 连接到 Loop 左侧 ⚪ BreakCond 端口
  └── 当 "停止" 图标出现时 → Loop 退出
```

---

### 4.7 Condition — 条件分支

根据条件判断选择执行不同分支。

| 参数          | 类型 | 说明                                         |
| ------------- | ---- | -------------------------------------------- |
| ConditionType | 下拉 | **ImageAppear** / **OCRContain** |
| UseFullScreen | 开关 | 是否使用整个窗口（关闭时可指定 Region）      |
| Region        | 区域 | 检测区域                                     |

#### 端口设计

Condition 节点在画布上显示 **1 个输入端口**（顶部）和 **2 个输出端口**（底部）：

- **True**（绿色）：条件成立时，流程从此出口继续
- **False**（红色）：条件不成立时，流程从此出口继续

#### ImageAppear — 图片存在判断

检测目标窗口中是否存在指定模板图片，输出 True（找到）/ False（未找到）。

| 参数                   | 类型 | 默认值 | 说明         |
| ---------------------- | ---- | ------ | ------------ |
| TemplateImagePath      | 文本 | —     | 模板图片路径 |
| TemplateMatchThreshold | 小数 | 0.8    | 匹配阈值     |

与 WaitCondition.ImageAppear 不同，Condition.ImageAppear **只检测一次**（即时快照判断），不循环等待。

#### OCRContain — 文字包含判断

检测窗口区域中是否存在指定文字。

| 参数    | 类型 | 说明         |
| ------- | ---- | ------------ |
| OCRText | 文本 | 要检测的文字 |

---

### 4.8 ColorMotion — 颜色运动节点

独立承载颜色视觉监测、运动判定相关逻辑。支持三种工作模式。

| 参数          | 类型 | 默认值       | 说明                                         |
| ------------- | ---- | ------------ | -------------------------------------------- |
| MotionMode    | 下拉 | MotionDetect | MotionDetect / StateChange / DirectionDetect |
| TargetWindow  | 文本 | —           | 目标窗口标题关键词                           |
| UseFullScreen | 开关 | true         | 是否使用整个窗口                             |
| Region        | 区域 | 200×200     | 检测区域                                     |

#### 通用 HSV 参数

| 参数         | 类型 | 默认值       | 说明                     |
| ------------ | ---- | ------------ | ------------------------ |
| TargetRgb    | RGB  | (49,218,183) | 目标颜色                 |
| HueTolerance | 数字 | 8            | 色相容差（0-179）        |
| SVTolerance  | 数字 | 30           | 饱和度/明度容差（0-255） |

#### 模式一：MotionDetect — 颜色运动监测

监测目标颜色的运动状态，输出 **True（检测到运动）/ False（静止）** 两种结果。

| 参数                | 类型 | 默认值 | 说明                 |
| ------------------- | ---- | ------ | -------------------- |
| MoveCheckIntervalMs | 数字 | 30     | 帧间检测间隔（毫秒） |
| MoveDurationMs      | 数字 | 10000  | 最长监控时长（毫秒） |
| MoveThresholdPx     | 数字 | 5      | 位移判定阈值（像素） |

**适用场景**：监控画面中特定颜色物体是否在移动（如进度条滑块、游戏角色标记）。

> **v5.1 更新**：MotionDetect 现在支持纯颜色追踪，不考虑具体形状。当使用颜色优先模式时，系统会追踪所有符合 HSV 范围的像素的质心位置变化，无论这些像素组成什么形状。适用于目标形状不确定但颜色稳定的场景。

#### 模式二：StateChange — 颜色状态变化监测

监测固定位置的颜色变化情况（出现/消失/占比变化），输出 **True（状态改变）/ False（无变化）**。

| 参数                 | 类型 | 默认值 | 说明                    |
| -------------------- | ---- | ------ | ----------------------- |
| StateCheckIntervalMs | 数字 | 100    | 检查间隔（毫秒）        |
| StateDurationMs      | 数字 | 30000  | 最长监控时长（毫秒）    |
| ColorChangeThreshold | 小数 | 0.15   | 颜色占比变化阈值（0-1） |

**适用场景**：检测 UI 按钮颜色变化、状态指示灯亮灭、血条/能量条变化。

#### 模式三：DirectionDetect — 目标运动方向判定

有两种追踪方式（通过 **TrackMode** 参数切换）：

**TrackMode = TemplateMatch**（默认）

基于 HSV 颜色过滤筛选目标区域，结合模板匹配算法比对预存基准图片，识别目标的具体运动方向。

| 参数                   | 类型 | 默认值       | 说明                 |
| ---------------------- | ---- | ------------ | -------------------- |
| ReferenceImagePath     | 文本 | —           | 基准参考图片         |
| TemplateMatchThreshold | 小数 | 0.8          | 模板匹配置信度阈值   |

> 📷 **HSV 截图技巧**：当画布上选中 ColorMotion 节点时，点击工具栏 **Snip 截图按钮**——截取的区域会自动应用当前节点的 HSV 颜色过滤，生成的图片**仅保留目标颜色形状**（其余像素为黑色）。这张图非常适合直接用作 ReferenceImagePath！

**TrackMode = ColorTrack**（新增 🆕）

纯 HSV 颜色中心追踪。不需要基准图片——直接监测 target color 在画面中的运动轨迹，计算颜色质心的位移来判定方向。

- ✅ 不需要 ReferenceImagePath
- ✅ 不关心目标的具体形状，只管颜色在哪里
- ✅ 速度更快（跳过了模板匹配步骤）

| 参数                | 类型 | 默认值 | 说明                 |
| ------------------- | ---- | ------ | -------------------- |
| MoveCheckIntervalMs | 数字 | 30     | 帧间检测间隔（毫秒） |
| MoveDurationMs      | 数字 | 10000  | 最长监控时长（毫秒） |

**两种模式共用参数**：

| 参数                | 类型 | 默认值 | 说明                 |
| ------------------- | ---- | ------ | -------------------- |
| MoveCheckIntervalMs | 数字 | 30     | 帧间检测间隔（毫秒） |
| MoveDurationMs      | 数字 | 10000  | 最长监控时长（毫秒） |

**输出端口**：5 个方向输出端口：

| 端口       | 颜色 | 含义                 |
| ---------- | ---- | -------------------- |
| Up         | 蓝色 | 向上移动             |
| Down       | 绿色 | 向下移动             |
| Left       | 黄色 | 向左移动             |
| Right      | 红色 | 向右移动             |
| Stationary | 灰色 | 静止（未检测到移动） |

> **对角线检测技巧**：如需检测"左下"等对角线方向，部署**两个 ColorMotion 节点**（一个检测 Left，一个检测 Down），将两者的 True 输出连接到同一个 **Gate（AND 模式）**。当两个条件同时满足 → 即对角线移动。

---

### 4.9 ColorCal — 颜色计算节点

ColorCal 是面向**多目标检测与坐标自定义运算分支逻辑**的高级节点。支持创建多个独立检测对象，每个对象可单独配置 HSV 颜色过滤和模板匹配规则，然后通过 C# 表达式基于多组坐标完成运算，最终根据整型结果路由到对应的后继节点。

#### 核心工作流程

```
1. 定义检测对象（名称 + HSV/模板配置）
   ↓
2. 批量识别所有目标，获取像素坐标
   ↓
3. 表达式运算（基于检测对象坐标）→ 输出单一整型
   ↓
4. 根据整型值匹配后继节点序号，执行对应分支
```

#### 参数说明

| 参数             | 类型 | 默认值 | 说明                                    |
| ---------------- | ---- | ------ | --------------------------------------- |
| DetectionTargets | 列表 | 1个    | 检测对象列表，每个对象独立命名和配置    |
| Expression       | 文本 | "0"    | C# 表达式，必须返回单一整型（分支索引） |
| SuccessorCount   | 数字 | 2      | 后继节点数量（决定输出端口数 0/1/2...） |

#### 检测对象配置

每个检测对象包含以下参数：

| 参数                   | 类型 | 默认值       | 说明                               |
| ---------------------- | ---- | ------------ | ---------------------------------- |
| Name                   | 文本 | TargetN      | 对象名称（表达式中引用）           |
| TargetWindow           | 文本 | —           | 目标窗口标题关键词                 |
| UseFullScreen          | 开关 | true         | 是否使用整个窗口                   |
| Region                 | 区域 | 200×200     | 检测区域                           |
| TargetRgb              | RGB  | (49,218,183) | 目标颜色                           |
| HueTolerance           | 数字 | 8            | 色相容差                           |
| SVTolerance            | 数字 | 30           | 饱和度/明度容差                    |
| TemplateImagePath      | 文本 | —           | 模板图片（可选，优先使用模板匹配） |
| TemplateMatchThreshold | 小数 | 0.8          | 模板匹配置信度阈值                 |

#### 表达式变量

表达式中**仅可引用当前节点内已创建的检测对象名称**，不允许自定义外部变量：

| 变量             | 类型 | 说明                              |
| ---------------- | ---- | --------------------------------- |
| `{Name}.X`     | int  | 检测对象中心点 X 坐标             |
| `{Name}.Y`     | int  | 检测对象中心点 Y 坐标             |
| `{Name}.Found` | bool | 是否检测到目标（1=true, 0=false） |

**表达式示例**：

| 表达式                                                  | 含义                                                       |
| ------------------------------------------------------- | ---------------------------------------------------------- |
| `Target1.Found ? 0 : 1`                               | 检测到 Target1 走端口 0，否则走端口 1                      |
| `Target1.X > 500 ? 0 : 1`                             | Target1 在屏幕右侧走端口 0，左侧走端口 1                   |
| `Target1.Found ? (Target1.X > Target2.X ? 0 : 1) : 2` | Target1 在 Target2 右侧→端口0，左侧→端口1，未找到→端口2 |
| `(Target1.X - Target2.X) > 100 ? 0 : 1`               | 两目标水平间距>100像素→端口0，否则→端口1                 |

#### 端口设计

ColorCal 节点在画布底部显示 **N 个输出端口**（N = SuccessorCount），标记为 **0, 1, 2...**：

- 表达式计算结果为 **0** → 执行端口 0 连接的后续节点
- 表达式计算结果为 **1** → 执行端口 1 连接的后续节点
- 以此类推

#### 适用场景

- **双目标相对位置判断**：追踪两个颜色目标的位置关系，根据相对位置选择不同策略
- **多区域状态监测**：同时监测多个 UI 元素的状态，综合判断后路由
- **坐标运算决策**：基于像素坐标进行距离、角度等几何运算，驱动分支逻辑

---

## 5. 编辑器操作指南

### 5.1 添加节点

从**左侧工具箱**拖动节点类型到画布上释放，即可创建新节点。

### 5.2 选中与移动

- **单击**节点卡片：选中节点，右侧属性面板显示该节点的参数
- **拖拽**节点卡片：在画布上移动节点位置
- **右键**节点卡片：弹出菜单（删除、复制、编辑）

### 5.3 编辑属性

选中节点后，在**右侧属性面板**中直接修改各项参数。修改实时生效。

### 5.4 节点连线

节点之间通过连线连接，从输出端口拖拽到输入端口。连线以贝塞尔曲线绘制，带有箭头指示执行方向。

### 5.5 节点颜色

每种节点类型有不同的卡片颜色，便于识别：

| 节点类型      | 颜色   | 说明                   |
| ------------- | ------ | ---------------------- |
| StartProgram  | 蓝色   | 启动程序               |
| ClickElement  | 绿色   | 点击元素               |
| WaitCondition | 黄色   | 等待条件               |
| KeyPress      | 紫色   | 按键操作               |
| Loop          | 橙色   | 循环容器               |
| Condition     | 粉红色 | 条件分支               |
| Gate          | 青色   | 逻辑门（双输入单输出） |
| ColorMotion   | 青绿色 | 颜色运动检测           |
| ColorCal      | 深紫色 | 多目标颜色计算         |

---

## 6. 流程文件管理

### 6.1 保存流程

1. 点击工具栏 **Save** 按钮
2. 选择保存位置和文件名
3. 文件以 `*.flow.json` 格式保存

### 6.2 加载流程

1. 点击工具栏 **Load** 按钮
2. 选择已有的 `*.flow.json` 文件
3. 流程将在画布上显示

### 6.3 JSON 文件格式

流程文件是标准的 JSON 格式，可以用任何文本编辑器手动编辑。完整结构参见 [示例流程](#12-示例流程)。

---

## 7. 执行流程

### 7.1 运行

1. 确认流程编辑完成
2. 点击工具栏 **Run** 按钮
3. 流程从头开始顺序执行
4. 日志面板实时显示执行状态和结果

### 7.2 暂停与继续

- 点击 **Pause**：暂停当前流程，执行会被挂起
- 点击 **Continue**：从暂停点继续执行

### 7.3 停止

- 点击 **Stop**：立即终止流程执行

### 7.4 错误处理

每个节点的执行都受超时和重试机制控制：

- 如果在 **TimeoutMs** 时间内未完成，记录超时
- 根据 **RetryCount** 自动重试失败的操作
- 最终失败后，记录错误日志并停止流程

### 7.5 日志格式

```
[14:30:25] [打开启动器] [INFO] 正在启动 D:\Game\launcher.exe
[14:30:27] [打开启动器] [INFO] 进程启动成功，等待窗口...
[14:30:30] [打开启动器] [OK] 找到窗口: GameWindow (HWND: 0x000503A2)
[14:30:30] [点击开始按钮] [INFO] 开始模板匹配...
[14:30:31] [点击开始按钮] [OK] 匹配成功，置信度: 0.92，点击坐标: (1300, 820)
```

---

## 8. 截图工具使用

### 8.1 打开截图工具

点击工具栏 **Snip** 按钮（绿色），或者点击属性面板中的 **Capture** 按钮。

### 8.2 框选截图

1. 屏幕变暗（半透明遮罩层覆盖整个屏幕）
2. 鼠标按下并拖拽框选目标区域
3. 松开鼠标后截图自动保存到 `Templates/` 目录
4. 路径自动填充到当前选中节点的图片路径参数

### 8.3 HSV 颜色过滤截图（🆕 新增）

当画布上**选中了 ColorMotion 或 ClickElement（HSV 模式）节点**时，截图工具会自动应用该节点的 HSV 颜色过滤参数：

1. 选定 ColorMotion 或 ClickElement（HSVClick / HSVTemplateMatch）节点
2. 点击 Snip 按钮
3. 框选区域
4. 生成的图片**只会保留 Target Color 对应颜色的图形**，其余像素全部变黑

这样生成的图片适合直接用于：
- ColorMotion > DirectionDetect > TemplateMatch 模式的 **ReferenceImagePath**
- ClickElement > HSVTemplateMatch 模式的 **TemplateImagePath**

> **v5.1**：HSV Snip 现在也支持 ClickElement 节点。当 ClickElement 的 `LocateMode` 设为 `HSVTemplateMatch` 时，Snip 会使用节点当前的 `TargetRgb`、`HueTolerance`、`SVTolerance` 参数对截图进行过滤后再保存模板。

### 8.4 截图技巧

- 截取的特征图要有**独特性和辨识度**
- 避免截取纯色或重复纹理区域
- 推荐的模板尺寸：50~200 像素
- 图片会自动缩放匹配（0.5x~1.5x），对 DPI 差异有很好的兼容性

---

## 9. 窗口选择器

### 9.1 使用方法

1. 在 TargetWindow 参数旁，点击 **Pick** 按钮
2. 界面会提示"将鼠标悬停在目标窗口上，按 Ctrl 键确认"
3. 将鼠标移动到目标窗口上
4. 按 **Ctrl** 键确认
5. 窗口标题自动填充到参数中

### 9.2 窗口标题匹配

TargetWindow 使用**包含匹配**（Contains），不是精确匹配。例如设置 "Game" 可以匹配到 "MyGame v2.0" 窗口。

---

## 10. 取色器工具

### 10.1 打开取色器

点击工具栏 **Pick Color** 按钮（紫色），打开 HSV 取色器对话框。

### 10.2 拾取颜色

1. 点击 **"🎯 Pick Color from Screen"** 按钮，程序窗口变半透明
2. 在屏幕上任意位置**单击鼠标左键**拾取该像素的颜色
3. 拾取完成后自动回到取色器窗口，显示 RGB / HSV / HEX 值

### 10.3 调整 HSV 容差

取色器提供两个滑块来调整 HSV 过滤范围：

| 滑块                    | 范围  | 默认值 | 说明                           |
| ----------------------- | ----- | ------ | ------------------------------ |
| **Hue Tolerance** | 1-60  | 8      | HSV 色相容差（越大匹配越宽松） |
| **S/V Tolerance** | 1-120 | 30     | 饱和度/明度容差                |

拖动滑块时，底部会实时显示 HSV 过滤范围：`H∈[min-max]  S,V∈[min-max]`。

### 10.4 应用到节点

1. 在画布上先选中目标节点（Condition / ColorMotion / ColorCal）
2. 点击 **"✓ Apply to Node"** 按钮
3. 取色器自动将颜色值和 HSV 容差参数写入当前选中节点：
   - `TargetRgb`：RGB 颜色值
   - `HueTolerance`：色相容差
   - `SVTolerance`：S/V 容差

> 取色器仅对支持 HSV 颜色过滤的节点类型生效（Condition、ColorMotion、ColorCal）。选中其他类型节点时，应用按钮不会生效。

---

## 11. 帮助系统

每个节点的属性面板底部都有一个 **? Help** 按钮。点击它可以打开一个帮助窗口，显示该节点类型的详细英文文档，包括：

- 节点概述与用途
- 参数详细说明
- 端口设计
- 使用技巧
- 示例场景

帮助系统覆盖全部 9 种节点类型，提供完整的英文技术文档。

---

## 12. 示例流程

### 12.1 完整示例流程

```json
{
  "FlowName": "自动启动并点击",
  "Version": "1.0",
  "Nodes": [
    {
      "NodeId": "guid-1",
      "NodeType": "StartProgram",
      "NodeName": "打开启动器",
      "Enabled": true,
      "TimeoutMs": 10000,
      "RetryCount": 3,
      "Parameters": {
        "FilePath": "D:\\Game\\launcher.exe",
        "RunAsAdmin": false,
        "WaitForWindowMs": 5000,
        "WindowTitleKeyword": "Game"
      }
    },
    {
      "NodeId": "guid-2",
      "NodeType": "ClickElement",
      "NodeName": "点击开始按钮",
      "Enabled": true,
      "TimeoutMs": 15000,
      "Parameters": {
        "TargetWindow": "Game",
        "LocateMode": "TemplateMatch",
        "Region": { "X": 1200, "Y": 720, "Width": 200, "Height": 200 },
        "TemplateImagePath": "templates/start_btn.png",
        "TemplateMatchThreshold": 0.8,
        "PreDelayMs": 500,
        "PostDelayMs": 1000
      }
    },
    {
      "NodeId": "guid-3",
      "NodeType": "WaitCondition",
      "NodeName": "等待游戏加载",
      "Enabled": true,
      "TimeoutMs": 60000,
      "Parameters": {
        "TargetWindow": "Game",
        "ConditionType": "ImageAppear",
        "Region": { "X": 800, "Y": 900, "Width": 400, "Height": 100 },
        "TemplateImagePath": "templates/loading.png",
        "CheckIntervalMs": 1000
      }
    },
    {
      "NodeId": "guid-4",
      "NodeType": "KeyPress",
      "NodeName": "按F键",
      "Enabled": true,
      "Parameters": {
        "KeyScanCode": 33,
        "KeyName": "F",
        "PressMode": "Press"
      }
    }
  ]
}
```

### 12.2 颜色运动 + 方向判断示例

```json
{
  "FlowName": "视觉方向追踪",
  "Nodes": [
    {
      "NodeId": "loop-1",
      "NodeType": "Loop",
      "NodeName": "持续监控",
      "Parameters": { "LoopMode": "BreakCondition" }
    },
    {
      "NodeId": "motion-1",
      "NodeType": "ColorMotion",
      "NodeName": "方向检测",
      "Parameters": {
        "TargetWindow": "MyGame",
        "MotionMode": "DirectionDetect",
        "Region": { "X": 200, "Y": 100, "Width": 800, "Height": 600 },
        "TargetRgb": { "R": 255, "G": 0, "B": 0 },
        "HueTolerance": 8,
        "SVTolerance": 30,
        "ReferenceImagePath": "templates/target.png",
        "MoveCheckIntervalMs": 30,
        "MoveDurationMs": 5000
      }
    },
    {
      "NodeId": "press-up",
      "NodeType": "KeyPress",
      "NodeName": "按上键",
      "Parameters": { "KeyName": "W", "PressMode": "Press" }
    },
    {
      "NodeId": "press-down",
      "NodeType": "KeyPress",
      "NodeName": "按下键",
      "Parameters": { "KeyName": "S", "PressMode": "Press" }
    }
  ]
}
```

> **说明**：
>
> 1. **loop-1**（Loop，BreakCondition 模式）提供无限循环结构，持续监控
> 2. **motion-1**（ColorMotion.DirectionDetect）通过 HSV+模板匹配追踪红色目标
> 3. 根据检测到的方向自动触发对应按键
> 4. 检测到静止时不执行任何操作
> 5. 将 Condition 的 True 输出连接到 Loop 左侧白色端口可实现条件退出

---

## 13. 常见问题

### Q: 双击 FlowAuto.exe 没有任何反应？

**A**: 检查是否出现 UAC 弹窗，如果出现请点击"是"。如果没有弹窗：

1. 确认已安装 [.NET 8.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
2. 确认已安装 [Visual C++ Redistributable (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe)

### Q: 模板匹配总是失败？

**A**: 尝试以下方法：

1. 降低 TemplateMatchThreshold（如 0.7 或 0.6）
2. 调整 TemplateScaleRange（扩大缩放范围）
3. 确保截取的模板与目标区域内容一致（分辨率、DPI）
4. 确认 Region 区域设置正确

### Q: 键盘模拟无效？

**A**: FlowAuto 使用硬件扫描码方式模拟键盘，与 SendKeys 不同：

1. 确保以管理员权限运行
2. 确认 KeyScanCode 值正确（参见内置扫描码表）
3. 部分游戏有反作弊保护，可能拦截模拟输入

### Q: 窗口找不到？

**A**: TargetWindow 使用包含匹配：

1. 确认窗口标题关键词拼写正确
2. 使用窗口选择器（Pick 按钮）自动获取标题
3. 增加 WaitForWindowMs 等待时间

### Q: 流程执行中如何停止？

**A**: 点击工具栏 **Stop** 按钮即可立即终止，或按 `Ctrl+S` 快捷键。也可以在执行前关闭不需要的节点（设置 Enabled = false）。

### Q: ColorMotion 检测不到目标颜色/运动？

**A**: 尝试以下调整：

1. 增大 **HueTolerance**（如 8→20）和 **SVTolerance**（如 30→50）
2. 确认 Region 检测区域包含目标元素
3. 使用工具栏 **Pick Color（取色器）** 精确拾取目标颜色
4. 增大 **MoveThresholdPx** 或 **ColorChangeThreshold** 调整灵敏度

### Q: Gate 节点如何使用？

**A**: Gate 节点接收两个上游信号，进行逻辑运算后输出单一结果：

1. 将两个节点的输出连接到 Gate 的 Input0 和 Input1
2. 选择逻辑类型：AND（同时满足）/ OR（满足其一）/ NOT（取反）
3. Gate 的 Result 端口连接到后续节点

### Q: 如何检测对角线移动（如左下）？

**A**: 使用两个 ColorMotion 节点 + Gate AND 组合：

1. ColorMotion A：DirectionDetect 模式，检测 **Left**
2. ColorMotion B：DirectionDetect 模式，检测 **Down**
3. 将两者的 True 输出分别连接到 Gate 的 Input0 和 Input1
4. Gate 设为 **AND** 模式
5. 当 A 和 B 同时输出 True → 即为"左下"移动

### Q: Loop 的 BreakCondition 模式怎么用？

**A**:

1. 将 Loop 的 LoopMode 设为 **BreakCondition**
2. Loop 左侧出现 ⚪ 白色 BreakCond 端口
3. 将 Condition/ColorMotion 判断节点的 True 输出连接到白色端口
4. 循环体会不断执行，直到判断条件成立 → Loop 退出
5. **不需要单独的 Break 节点**

### Q: ColorMotion 和 Condition.ImageAppear 有什么区别？

**A**:

| 功能         | Condition.ImageAppear | ColorMotion                                     |
| ------------ | --------------------- | ----------------------------------------------- |
| 主要用途     | 图片存在判断（快照）  | 完整的颜色视觉监测                              |
| 输出类型     | True/False 双分支     | 双分支 / 多方向分支                             |
| 图片存在检测 | ✅ 模板匹配一次判断   | 可用 StateChange 模式替代                       |
| 运动检测     | ❌                    | ✅ 支持（MotionDetect）                         |
| 状态变化检测 | ❌                    | ✅ 支持（StateChange）                          |
| 方向判定     | ❌                    | ✅ 支持（DirectionDetect，5 方向 + 组合对角线） |
| HSV 颜色过滤 | ❌                    | ✅ 全部模式支持                                 |

建议：简单的图片存在判断用 Condition.ImageAppear；需要 HSV 颜色追踪、运动分析、方向判定时用 ColorMotion。

### Q: ColorCal 的 C# 表达式怎么写？

**A**: 表达式支持三元运算符、比较运算符、算术运算：

- `Target1.Found ? 0 : 1` → 检测到走端口 0，否则走端口 1
- `Target1.X > 500 ? 0 : 1` → 右侧走端口 0，左侧走端口 1
- `Target1.Found ? (Target1.X > Target2.X ? 0 : 1) : 2` → 复杂多路分支
- `(Target1.X - Target2.X) > 100 ? 0 : 1` → 距离判断

> 表达式中只能引用当前节点内定义的检测对象名称，如 `Target1.X`、`Target2.Found`。

### Q: 取色器如何使用？

**A**: 点击工具栏 **Pick Color** 按钮 → 点击"Pick Color from Screen" → 在屏幕上单击拾取颜色 → 调整 HSV 容差滑块 → 选中目标节点 → 点击"Apply to Node"一键应用。支持 Condition、ColorMotion、ColorCal 节点。

### Q: Pick Key 拾取按键怎么用？

**A**（v5.1 新增）：点击工具栏 **Pick Key** 按钮 → 在键盘上按下任意按键 → FlowAuto 通过临时低级键盘钩子捕获扫描码，自动回填 `KeyScanCode` 和 `KeyName` 到选中的 KeyPress 节点，并弹出确认框显示捕获结果。

### Q: HSV + TemplateMatch 置信度很低（0.1~0.3）？

**A**：这是旧版本的已知问题。**v5.1** 修复了两个根本原因：
1. 模板匹配现在在 **HSV 过滤后的图像** 上进行，而不是原始截图 ROI
2. 多尺度匹配范围扩大至 **0.5x~1.5x**，步进更精细

如果置信度仍然偏低：
1. 检查 `debug_hsv_tpl/` 目录下的调试图（原始截图 + HSV 过滤结果）
2. 调整 `HueTolerance` / `SVTolerance`，确保过滤后的图像能清晰显示目标形状
3. 确认截取模板时选中了 ClickElement（HSV 模式）节点，确保截图经过 HSV 过滤

### Q: ColorCal 执行了所有分支，而不是只执行匹配的分支？

**A**：这是 v5.1 已修复的分支路由 BUG。修复后，ColorCal / Condition / ColorMotion 只会执行与结果匹配的端口所连接的后续节点（例如结果为 0 → 仅执行端口 0 的后续节点）。如果仍遇到此问题，请确认使用的是 v5.1+ 版本。

---

## 附录 A：扫描码速查表

FlowAuto 内部使用的扫描码映射（基于硬件扫描码标准）：

| 按键 | 码值(hex) | 按键  | 码值(hex) | 按键  | 码值(hex) |
| ---- | --------- | ----- | --------- | ----- | --------- |
| ESC  | 0x01      | 1     | 0x02      | 2     | 0x03      |
| 3    | 0x04      | 4     | 0x05      | 5     | 0x06      |
| 6    | 0x07      | 7     | 0x08      | 8     | 0x09      |
| 9    | 0x0A      | 0     | 0x0B      | TAB   | 0x0F      |
| Q    | 0x10      | W     | 0x11      | E     | 0x12      |
| R    | 0x13      | T     | 0x14      | Y     | 0x15      |
| U    | 0x16      | I     | 0x17      | O     | 0x18      |
| P    | 0x19      | A     | 0x1E      | S     | 0x1F      |
| D    | 0x20      | F     | 0x21      | G     | 0x22      |
| H    | 0x23      | J     | 0x24      | K     | 0x25      |
| L    | 0x26      | Z     | 0x2C      | X     | 0x2D      |
| C    | 0x2E      | V     | 0x2F      | B     | 0x30      |
| N    | 0x31      | M     | 0x32      | SHIFT | 0x2A      |
| CTRL | 0x1D      | SPACE | 0x39      | ENTER | 0x1C      |
| F1   | 0x3B      | F2    | 0x3C      | F3    | 0x3D      |
| F4   | 0x3E      | F5    | 0x3F      | F6    | 0x40      |
| F7   | 0x41      | F8    | 0x42      | F9    | 0x43      |
| F10  | 0x44      | F11   | 0x57      | F12   | 0x58      |

> **注意**：KeyScanCode 参数只接受十进制值，例如 A = 30（0x1E 的十进制）。KeyName 参数支持大小写不敏感的名称匹配。
