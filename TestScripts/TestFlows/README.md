# FlowAuto 功能测试流程文件 (.flow.json)

本目录包含用于验证 FlowAuto 各节点功能的测试流程文件。
可直接在 FlowAuto 中加载运行。

## 文件列表

| 文件 | 测试目标 | 关键节点 |
|---|---|---|
| `test_full_suite.flow.json` | 🌟 完整功能测试 | 全部9种节点类型 |
| `test_click_element.flow.json` | 鼠标点击精度 | ClickElement (Coordinate + TemplateMatch) |
| `test_colormotion_hsv.flow.json` | HSV颜色运动检测 | ColorMotion (DirectionDetect + WASD) |
| `test_wait_condition.flow.json` | 条件等待 | WaitCondition (5种条件类型) |
| `test_colorcal.flow.json` | 颜色计算 | ColorCal (多目标表达式) |
| `test_statechange.flow.json` | 状态变化检测 | ColorMotion (StateChange) |

## 使用前提

1. **先打开测试页面**: `TestScripts/FlowAuto_TestSuite_v2.html`
2. **准备模板截图**: 将需要的模板图片放到 `TestScripts/templates/` 目录
3. **窗口识别**: 确保浏览器窗口标题包含 "FlowAuto v2"

## 模板截图采集清单

| 文件名 | 用途 | 截图内容 |
|---|---|---|
| `ball_green.png` | ClickElement 小球模板 | 面板1中的绿色小球(#31d8b7) |
| `hsv_ball_green.png` | ColorMotion HSV小球模板 | 面板2中的亮绿小球(#00ff88) |
| `target_circle_blue.png` | WaitCondition 目标模板 | 面板3中的蓝色圆形🎯 |

> 💡 使用 FlowAuto 内置截图工具 (ScreenshotOverlay) 或 Win+Shift+S 截取。

## 加载方法

1. 打开 FlowAuto
2. 点击工具栏 "Load" 按钮
3. 选择对应的 `.flow.json` 文件
4. 点击 "Run" 执行

## 节点类型参考

| NodeType | 名称 | 说明 |
|---|---|---|
| 0 | StartProgram | 启动外部程序 |
| 1 | ClickElement | 定位并点击元素 |
| 2 | WaitCondition | 等待条件满足 |
| 3 | KeyPress | 发送键盘输入 |
| 4 | Loop | 循环执行子节点 |
| 5 | Condition | 条件分支 |
| 6 | Gate | 逻辑门 (AND/OR/NOT) |
| 7 | ColorMotion | 颜色运动检测 |
| 8 | ColorCal | 多目标颜色计算 |
| 9 | Break | 循环中断 |
