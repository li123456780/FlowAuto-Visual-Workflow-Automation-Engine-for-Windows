# FlowAuto User Manual

> Version 5.1 | 2026-06-21

## Table of Contents

1. [Introduction](#1-introduction)
2. [Getting Started](#2-getting-started)
3. [Interface Overview](#3-interface-overview)
4. [Node Types](#4-node-types)
   - 4.1 [StartProgram](#41-startprogram)
   - 4.2 [ClickElement](#42-clickelement)
   - 4.3 [WaitCondition](#43-waitcondition)
   - 4.4 [KeyPress](#44-keypress)
   - 4.5 [Gate](#45-gate)
   - 4.6 [Loop](#46-loop)
   - 4.7 [Condition](#47-condition)
   - 4.8 [ColorMotion](#48-colormotion)
   - 4.9 [ColorCal](#49-colorcal)
5. [Editor Operations](#5-editor-operations)
6. [Flow File Management](#6-flow-file-management)
7. [Execution](#7-execution)
8. [Screenshot Tool](#8-screenshot-tool)
9. [Window Picker](#9-window-picker)
10. [Color Picker](#10-color-picker)
11. [Help System](#11-help-system)
12. [Example Flows](#12-example-flows)
13. [FAQ](#13-faq)
14. [Appendix A: Scan Code Reference](#appendix-a-scan-code-reference)

---

## 1. Introduction

FlowAuto is a visual flow orchestration automation engine that lets you freely combine and configure automation steps through a graphical interface (GUI), such as launching programs, waiting for windows, image recognition, simulating keyboard and mouse operations, and more. Flows are saved as JSON files and executed.

**Core Use Case**: Launch a program → wait for window → image-recognize and click a button → keyboard operation → run other tools, all without writing code.

---

## 2. Getting Started

### 2.1 System Requirements

| Requirement         | Description                        |
| ------------------- | ---------------------------------- |
| OS                  | Windows 10/11 (x64)                |
| .NET Runtime        | .NET 8.0 Desktop Runtime (x64)     |
| VC++ Runtime        | Visual C++ Redistributable (x64)   |

### 2.2 Launch

Double-click `FlowAuto.exe`. A UAC (User Account Control) prompt will appear — click "Yes" to grant administrator privileges.

> FlowAuto requires administrator rights for window control and keyboard/mouse simulation.

---

## 3. Interface Overview

| Region             | Location       | Function                                       |
| ------------------ | -------------- | ---------------------------------------------- |
| **Toolbar**        | Top            | New, Load, Save, Run, Pause, Stop, tools       |
| **Toolbox**        | Left           | 9 node types, drag to canvas                   |
| **Canvas**         | Center         | Visual node card and connection editing area   |
| **Properties**     | Right (top)    | Edit selected node parameters                  |
| **Log**            | Right (bottom) | Runtime log output                             |

### 3.1 Toolbar Buttons

| Button         | Function    | Description                                  |
| -------------- | ----------- | -------------------------------------------- |
| **New**        | New flow    | Clear canvas, start a new flow               |
| **Load**       | Load        | Load from `*.flow.json` file                 |
| **Save**       | Save        | Save current flow as `*.flow.json`           |
| **Run**        | Run         | Execute the entire flow (green)              |
| **Pause**      | Pause/Resume| Toggle pause/resume (yellow/green)           |
| **Stop**       | Stop        | Immediately terminate execution (red)        |
| **Snip**       | Screenshot  | Fullscreen screenshot tool for templates     |
| **Pick Win**   | Pick Window | Hover target window, press Ctrl to capture   |
| **Pick Rgn**   | Pick Region | Drag to select a coordinate region on window |
| **Pick Color** | Color Picker| Screen color pick + HSV tolerance config     |
| **Pick Key**   | Pick Key    | Press any key to capture scan code (v5.1)    |
| **Settings**   | Settings    | Configure global pre/post click delays       |

> **Shortcut**: `Ctrl+S` stops the running flow at any time.

### 3.2 Global Settings

Click **Settings** to open the settings dialog.

| Parameter             | Default | Description                                          |
| --------------------- | ------- | ---------------------------------------------------- |
| **Pre-click Delay**   | 500     | Default wait before all click actions (overridable)  |
| **Post-click Delay**  | 500     | Default wait after all click actions (overridable)   |

**Fallback Priority** (high to low):
1. Node's own `PreDelayMs` / `PostDelayMs` (if explicitly set)
2. Global settings value
3. Built-in default (500ms each)

---

## 4. Node Types

All nodes share these **common properties**:

| Property       | Type    | Default | Description                     |
| -------------- | ------- | ------- | ------------------------------- |
| Name           | Text    | —       | Custom node name                |
| Enabled        | Toggle  | true    | Whether this node is enabled    |
| Timeout (ms)   | Number  | 30000   | Execution timeout (milliseconds)|
| Retry Count    | Number  | 3       | Retry attempts on failure       |

---

### 4.1 StartProgram

Launch an external executable.

| Parameter          | Type    | Description                                         |
| ------------------ | ------- | --------------------------------------------------- |
| FilePath           | Text    | Full path to executable, e.g. `D:\Game\app.exe`     |
| WorkingDirectory   | Text    | Working directory (optional)                        |
| Arguments          | Text    | Launch arguments (optional)                         |
| RunAsAdmin         | Toggle  | Run as administrator                                |
| WaitForWindowMs    | Number  | Max wait time for window to appear                  |
| WindowTitleKeyword | Text    | Window title keyword to confirm successful launch   |

**Execution flow**: Start program → wait → find window with keyword → store window handle for subsequent nodes.

---

### 4.2 ClickElement

Locate and click a position in the target window.

| Parameter              | Type      | Description                                              |
| ---------------------- | --------- | -------------------------------------------------------- |
| TargetWindow           | Text      | Target window title keyword                              |
| LocateMode             | Dropdown  | Coordinate / TemplateMatch / OCR / HSVClick / HSVTemplateMatch |
| Region                 | Region    | Search region (relative to window client area)           |
| TemplateImagePath      | Text      | Template image path (TemplateMatch / HSVTemplateMatch mode) |
| TemplateMatchThreshold | Decimal   | Match threshold, default 0.8                             |
| TemplateScaleRange     | Range     | Multi-scale match range (Min/Max/Step)                   |
| OCRText                | Text      | Text to recognize (OCR mode)                             |
| ClickOffset            | Coordinate| Click position offset from match center                  |
| TargetRgb              | RGB       | Target color (HSVClick / HSVTemplateMatch mode)          |
| HueTolerance           | Number    | Hue tolerance (HSV modes), default 8                     |
| SVTolerance            | Number    | Saturation/Value tolerance (HSV modes), default 30       |
| PreDelayMs             | Number    | Delay before click after mouse move (default 500ms)      |
| PostDelayMs            | Number    | Delay after click (default 500ms)                        |

**Five locate modes**:
1. **Coordinate**: Click the center of the Region area
2. **TemplateMatch**: Screenshot → template match → click match center. Supports 0.5x~1.5x multi-scale matching
3. **OCR**: Recognize specified text position and click
4. **HSVClick** (v5.1): Apply HSV color filtering → click the center of the largest matching color region. No template image required
5. **HSVTemplateMatch** (v5.1): Apply HSV color filtering → perform template matching on the filtered image → click match center. Requires a template image and supports 0.5x~1.5x multi-scale matching on the filtered image

---

### 4.3 WaitCondition

Wait for a condition to be true before continuing.

| Parameter              | Type      | Description                                                |
| ---------------------- | --------- | ---------------------------------------------------------- |
| TargetWindow           | Text      | Target window title keyword                                |
| ConditionType          | Dropdown  | ImageAppear / ImageDisappear / OCRContain / WindowExist / Timeout |
| WaitMs                 | Number    | Wait duration (Timeout type only); falls back to node TimeoutMs |
| Region                 | Region    | Monitor region                                             |
| TemplateImagePath      | Text      | Template image (ImageAppear / ImageDisappear)              |
| TemplateMatchThreshold | Decimal   | Match threshold, default 0.8                               |
| OCRText                | Text      | Text to wait for (OCRContain)                              |
| CheckIntervalMs        | Number    | Check interval (ms), default 500                           |
| TimeoutMs              | Number    | Timeout (ms), default 30000                                |

**Five condition types**:

- **ImageAppear**: Wait for template image to appear in region
- **ImageDisappear**: Wait for template image to disappear from region
- **OCRContain**: Wait for specified text to appear in window
- **WindowExist**: Wait for window with specified title to appear
- **Timeout**: Wait a fixed duration then continue

---

### 4.4 KeyPress

Send keyboard input to the target window.

| Parameter      | Type      | Description                                             |
| -------------- | --------- | ------------------------------------------------------- |
| KeyScanCode    | Number    | Keyboard scan code (e.g. A=30, F=33, ESC=1)             |
| KeyName        | Text      | Key name helper (e.g. "F", "ESC")                       |
| PressMode      | Dropdown  | Press / Hold / Release                                  |
| HoldDurationMs | Number    | Hold duration (Hold mode)                               |
| TargetWindow   | Text      | Window to activate (optional)                           |

**Pick Key (v5.1)**: Click the **Pick Key** toolbar button, then press any key on your keyboard. FlowAuto will register a temporary low-level keyboard hook, capture the first `WM_KEYDOWN` event, extract the hardware scan code, and automatically fill `KeyScanCode` and `KeyName` into the selected KeyPress node(s). A popup will confirm the captured key and scan code.

**Built-in scan code table** (decimal):

| Key   | Code | Key  | Code | Key   | Code |
| ----- | ---- | ---- | ---- | ----- | ---- |
| ESC   | 1    | F1   | 59   | SPACE | 57   |
| ENTER | 28   | F2   | 60   | TAB   | 15   |
| F3    | 61   | SHIFT| 42   | F4    | 62   |
| CTRL  | 29   | F5   | 63   | A     | 30   |
| F6    | 64   | D    | 32   | F7    | 65   |
| E     | 18   | F8   | 66   | F     | 33   |
| F9    | 67   | Q    | 16   | F10   | 68   |
| W     | 17   | F11  | 87   | S     | 31   |
| Z     | 44   | F12  | 88   | 0     | 11   |
| 1     | 2    | 2-8  | 3-9  | 9     | 10   |

> See [Appendix A](#appendix-a-scan-code-reference) for full mapping. KeyName supports case-insensitive letter/function key names.

---

### 4.5 Gate

Gate node accepts **two upstream signals**, performs a logical operation, and produces a **single result** for downstream nodes.

| Parameter     | Type     | Description                                    |
| ------------- | -------- | ---------------------------------------------- |
| GateLogicType | Dropdown | **AND** / **OR** / **NOT**                     |

#### Port Design

Gate displays **2 input ports** (top, labeled 0 / 1) and **1 output port** (bottom, labeled Result):

- **Input0 / Input1**: Receive execution signals from upstream nodes
- **Result**: Output the computed result, connect to the next node

> Unconnected input ports are ignored. NOT mode only uses Input0.

#### Logic Types

| Type   | Rule                                          | Typical Scenario                  |
| ------ | --------------------------------------------- | --------------------------------- |
| AND    | Both inputs must be true for result = true    | Multiple conditions must all pass |
| OR     | Any input true → result = true                | Satisfy any one condition         |
| NOT    | Invert the result of Input0                   | Continue when condition fails     |

---

### 4.6 Loop

Repeat child nodes. Loop supports two modes and has two exit ports.

| Parameter | Type     | Description                                          |
| --------- | -------- | ---------------------------------------------------- |
| LoopMode  | Dropdown | **FixedCount** / **BreakCondition**                  |
| LoopCount | Number   | Loop count, FixedCount mode only (0 = infinite)      |

#### Mode 1: FixedCount

Execute a fixed number of times.

#### Mode 2: BreakCondition

Loop executes indefinitely until a break signal is received. A **⚪ white BreakCond port** appears on the **left side** of the Loop node:

- Connect a Condition / ColorMotion True output to this white port
- When the condition is met, the Loop automatically exits
- Loop exits from the **Complete port** (green)

> No separate Break node is needed. The loop termination logic is handled entirely within Loop.

#### Port Design

| Port       | Color  | Position       | Description                            |
| ---------- | ------ | -------------- | -------------------------------------- |
| Input      | White  | Top center     | Loop body re-entry                     |
| BreakCond  | White  | Left center    | BreakCondition mode only, receives break signal |
| Complete   | Green  | Bottom left    | Normal loop completion exit            |
| Break      | Red    | Bottom right   | Break / abnormal exit                  |

---

### 4.7 Condition

Conditional branching. Based on a condition check, route execution to different branches.

| Parameter     | Type     | Description                                      |
| ------------- | -------- | ------------------------------------------------ |
| ConditionType | Dropdown | **ImageAppear** / **OCRContain**                 |
| UseFullScreen | Toggle   | Use entire window (disable to specify Region)    |
| Region        | Region   | Detection region                                 |

#### Port Design

Condition displays **1 input port** (top) and **2 output ports** (bottom):

- **True** (green): Condition met, execution continues from this port
- **False** (red): Condition not met, execution continues from this port

#### ImageAppear

Detect if a template image exists in the target window. Outputs True (found) / False (not found).

| Parameter              | Type    | Default | Description          |
| ---------------------- | ------- | ------- | -------------------- |
| TemplateImagePath      | Text    | —       | Template image path  |
| TemplateMatchThreshold | Decimal | 0.8     | Match threshold      |

Unlike WaitCondition.ImageAppear, Condition.ImageAppear **checks only once** (instant snapshot), not repeated polling.

#### OCRContain

Detect if specified text exists in the window region.

| Parameter | Type | Description    |
| --------- | ---- | -------------- |
| OCRText   | Text | Text to detect |

---

### 4.8 ColorMotion

Independent color visual monitoring, motion detection, and direction analysis node. Supports three modes.

| Parameter       | Type     | Default      | Description              |
| --------------- | -------- | ------------ | ------------------------ |
| MotionMode      | Dropdown | MotionDetect | MotionDetect / StateChange / DirectionDetect |
| TargetWindow    | Text     | —            | Target window keyword    |
| UseFullScreen   | Toggle   | true         | Use entire window        |
| Region          | Region   | 200x200      | Detection region         |

#### Common HSV Parameters

| Parameter      | Type    | Default      | Description                  |
| -------------- | ------- | ------------ | ---------------------------- |
| TargetRgb      | RGB     | (49,218,183) | Target color                 |
| HueTolerance   | Number  | 8            | Hue tolerance (0-179)        |
| SVTolerance    | Number  | 30           | Saturation/Value tolerance   |

#### Mode 1: MotionDetect

Monitor whether a target color is moving. Outputs **True (moving) / False (stationary)**.

| Parameter           | Type   | Default | Description            |
| ------------------- | ------ | ------- | ---------------------- |
| MoveCheckIntervalMs | Number | 30      | Frame interval (ms)    |
| MoveDurationMs      | Number | 10000   | Max monitor duration   |
| MoveThresholdPx     | Number | 5       | Movement threshold (px)|

**Use cases**: Monitor if a specific color object in the screen is moving (e.g. progress bar slider, game character marker).

> **v5.1 Update**: MotionDetect now supports pure color tracking without considering shape. When `TrackMode` (or internal logic) uses color-only mode, it tracks the center-of-mass of all pixels matching the HSV range, regardless of their shape. This is useful when the target's shape is unpredictable but its color is consistent.

#### Mode 2: StateChange

Monitor color changes at a fixed position (appear/disappear/proportion change). Outputs **True (changed) / False (unchanged)**.

| Parameter            | Type    | Default | Description              |
| -------------------- | ------- | ------- | ------------------------ |
| StateCheckIntervalMs | Number  | 100     | Check interval (ms)      |
| StateDurationMs      | Number  | 30000   | Max monitor duration     |
| ColorChangeThreshold | Decimal | 0.15    | Color ratio change (0-1) |

**Use cases**: Detect UI button color changes, status indicator on/off, health bar changes.

#### Mode 3: DirectionDetect

Two tracking sub-modes (switch via **TrackMode** parameter):

**TrackMode = TemplateMatch** (default)

HSV color filtering + template matching against a reference image. Requires a reference image.

| Parameter              | Type    | Default | Description              |
| ---------------------- | ------- | ------- | ------------------------ |
| ReferenceImagePath     | Text    | —       | Reference image          |
| TemplateMatchThreshold | Decimal | 0.8     | Template confidence      |

> 📷 **HSV Snip Tip**: When a ColorMotion node is selected on the canvas, clicking the **Snip** toolbar button will automatically HSV-filter the captured region — only the target color shape remains. This filtered image is perfect for ReferenceImagePath!

**TrackMode = ColorTrack** (🆕 New)

Pure HSV color center-of-mass tracking. No reference image needed — tracks movement of any pixels matching the target color.

- ✅ No ReferenceImagePath required
- ✅ Doesn't care about shape, only pixel color position
- ✅ Faster (no template matching step)

**Common parameters for both modes**:

| Parameter           | Type   | Default | Description            |
| ------------------- | ------ | ------- | ---------------------- |
| MoveCheckIntervalMs | Number | 30      | Frame interval (ms)    |
| MoveDurationMs      | Number | 10000   | Max monitor duration   |

**Output ports**: 5 direction ports:

| Port       | Color  | Meaning          |
| ---------- | ------ | ---------------- |
| Up         | Blue   | Moving upward    |
| Down       | Green  | Moving downward  |
| Left       | Yellow | Moving left      |
| Right      | Red    | Moving right     |
| Stationary | Gray   | Stationary       |

> **Diagonal detection**: Use two ColorMotion nodes (e.g. Left + Down) with their True outputs connected to a **Gate (AND mode)**. When both conditions are true simultaneously → diagonal movement detected.

---

### 4.9 ColorCal

Multi-target detection and coordinate custom computation with branch routing.

#### Workflow

```
1. Define detection targets (Name + HSV/Template config)
   ↓
2. Batch-identify all targets, get pixel coordinates
   ↓
3. C# expression evaluation (based on target coordinates) → single integer
   ↓
4. Route to the successor node matching the integer index
```

#### Parameters

| Parameter        | Type   | Default | Description                                        |
| ---------------- | ------ | ------- | -------------------------------------------------- |
| DetectionTargets | List   | 1       | Detection target list, each independently named    |
| Expression       | Text   | "0"     | C# expression, must return a single integer        |
| SuccessorCount   | Number | 2       | Number of successor nodes (determines port count)  |

#### Target Configuration

Each detection target has:

| Parameter              | Type    | Default      | Description                        |
| ---------------------- | ------- | ------------ | ---------------------------------- |
| Name                   | Text    | TargetN      | Object name (referenced in expr.)  |
| TargetWindow           | Text    | —            | Target window keyword              |
| UseFullScreen          | Toggle  | true         | Use entire window                  |
| Region                 | Region  | 200x200      | Detection region                   |
| TargetRgb              | RGB     | (49,218,183) | Target color                       |
| HueTolerance           | Number  | 8            | Hue tolerance                      |
| SVTolerance            | Number  | 30           | S/V tolerance                      |
| TemplateImagePath      | Text    | —            | Template image (optional)          |
| TemplateMatchThreshold | Decimal | 0.8          | Template confidence threshold      |

#### Expression Variables

Expressions **can only reference detection target names defined within this node**:

| Variable       | Type | Description                    |
| -------------- | ---- | ------------------------------ |
| `{Name}.X`     | int  | Target center X coordinate     |
| `{Name}.Y`     | int  | Target center Y coordinate     |
| `{Name}.Found` | bool | Whether target was found       |

**Examples**:

| Expression                                          | Meaning                                                |
| --------------------------------------------------- | ------------------------------------------------------ |
| `Target1.Found ? 0 : 1`                             | Found → port 0, not found → port 1                     |
| `Target1.X > 500 ? 0 : 1`                           | Right of 500 → port 0, left → port 1                   |
| `Target1.Found ? (Target1.X > Target2.X ? 0 : 1) : 2`| T1 right of T2 → port 0, left → port 1, not found → port 2 |
| `(Target1.X - Target2.X) > 100 ? 0 : 1`             | Horizontal gap > 100px → port 0, else → port 1          |

#### Port Design

ColorCal displays **N output ports** at the bottom (N = SuccessorCount), labeled **0, 1, 2...**:

- Expression result **0** → execute node connected to port 0
- Expression result **1** → execute node connected to port 1
- And so on

---

## 5. Editor Operations

### 5.1 Add Nodes

Drag a node type from the **Toolbox** (left panel) onto the canvas and release to create a new node.

### 5.2 Select and Move

- **Click** a node card: Select the node, properties panel shows its parameters
- **Drag** a node card: Move the node on the canvas
- **Right-click** a node card: Context menu (Delete, Copy, Edit)

### 5.3 Edit Properties

Select a node, then modify parameters in the **Properties panel** (right side). Changes take effect immediately.

### 5.4 Node Connections

Nodes are connected via Bézier curves with arrows indicating execution direction. Connections are created by dragging from an output connector to an input connector.

### 5.5 Node Colors

| Node Type      | Color    | Description                   |
| -------------- | -------- | ----------------------------- |
| StartProgram   | Blue     | Launch program                |
| ClickElement   | Green    | Click element                 |
| WaitCondition  | Yellow   | Wait for condition            |
| KeyPress       | Purple   | Keyboard input                |
| Loop           | Orange   | Loop container                |
| Condition      | Pink     | Conditional branching         |
| Gate           | Cyan     | Logic gate (2-input, 1-output)|
| ColorMotion    | Teal     | Color motion detection        |
| ColorCal       | Deep Purple | Multi-target color computation |

---

## 6. Flow File Management

### 6.1 Save

1. Click **Save** on the toolbar
2. Choose location and filename
3. File saved as `*.flow.json`

### 6.2 Load

1. Click **Load** on the toolbar
2. Select an existing `*.flow.json` file
3. Flow appears on the canvas

### 6.3 JSON Format

Flow files are standard JSON, editable with any text editor. See [Example Flows](#12-example-flows) for the full structure.

---

## 7. Execution

### 7.1 Run

1. Confirm flow editing is complete
2. Click **Run** on the toolbar
3. Flow executes from the start
4. Log panel shows real-time status

### 7.2 Pause / Resume

- **Pause**: Suspend current execution
- **Continue**: Resume from the paused point

### 7.3 Stop

- **Stop**: Immediately terminate flow execution

### 7.4 Error Handling

Each node is controlled by timeout and retry mechanisms:

- If not completed within **TimeoutMs**, a timeout is recorded
- Failed operations are automatically retried up to **RetryCount**
- After final failure, an error log is recorded and the flow stops

### 7.5 Log Format

```
[14:30:25] [Launch App] [INFO] Starting D:\Game\launcher.exe
[14:30:27] [Launch App] [INFO] Process started, waiting for window...
[14:30:30] [Launch App] [OK] Window found: GameWindow (HWND: 0x000503A2)
[14:30:30] [Click Start] [INFO] Starting template match...
[14:30:31] [Click Start] [OK] Match success, confidence: 0.92, click: (1300, 820)
```

---

## 8. Screenshot Tool

### 8.1 Open

Click the **Snip** toolbar button or the **Capture** button next to the TemplateImagePath parameter.

### 8.2 Capture

1. Screen dims (semi-transparent overlay)
2. Click and drag to select the target region
3. Release to save screenshot to `Templates/` directory
4. Path is auto-filled into the current node's image path parameter

### 8.3 HSV Color Filter Snip (🆕 New)

When a **ColorMotion or ClickElement (HSV modes) node is selected** on the canvas, the Screenshot tool automatically applies HSV color filtering:

1. Select a ColorMotion or ClickElement (HSVClick / HSVTemplateMatch) node
2. Click Snip button
3. Drag to select region
4. The saved image contains **only the target color shape** — all other pixels are black

This filtered image is ideal for:
- ColorMotion → DirectionDetect → TemplateMatch mode's **ReferenceImagePath**
- ClickElement → HSVTemplateMatch mode's **TemplateImagePath**

> **v5.1**: HSV Snip now also works with ClickElement nodes. When ClickElement's `LocateMode` is set to `HSVTemplateMatch`, Snip will use the node's current `TargetRgb`, `HueTolerance`, and `SVTolerance` values to filter the captured image before saving the template.

### 8.4 Tips

- Capture features with **distinctiveness**
- Avoid pure color or repetitive texture areas
- Recommended template size: 50~200 pixels
- Images auto-scale for matching (0.5x~1.5x), DPI-compatible

---

## 9. Window Picker

### 9.1 Usage

1. Click **Pick** next to the TargetWindow parameter
2. Prompt: "Hover over the target window, press Ctrl to confirm"
3. Move mouse over the target window
4. Press **Ctrl** to confirm
5. Window title is auto-filled

### 9.2 Title Matching

TargetWindow uses **substring matching** (Contains), not exact match. Setting "Game" matches "MyGame v2.0".

---

## 10. Color Picker

### 10.1 Open

Click the **Pick Color** button (purple) on the toolbar.

### 10.2 Pick Color

1. Click **"🎯 Pick Color from Screen"**, the window becomes semi-transparent
2. **Click** anywhere on screen to pick that pixel's color
3. Returns to the picker showing RGB / HSV / HEX values

### 10.3 Adjust HSV Tolerance

| Slider           | Range  | Default | Description                     |
| ---------------- | ------ | ------- | ------------------------------- |
| **Hue Tolerance**| 1-60   | 8       | HSV hue tolerance (higher = looser) |
| **S/V Tolerance**| 1-120  | 30      | Saturation/Value tolerance      |

Dragging the sliders shows the live HSV filter range: `H∈[min-max]  S,V∈[min-max]`.

### 10.4 Apply to Node

1. Select a target node on the canvas (Condition / ColorMotion / ColorCal)
2. Click **"✓ Apply to Node"**
3. Color values and HSV tolerances are written to the selected node:
   - `TargetRgb`: RGB color value
   - `HueTolerance`: Hue tolerance
   - `SVTolerance`: S/V tolerance

> The picker only works with HSV-enabled node types (Condition, ColorMotion, ColorCal).

---

## 11. Help System

Each node's property panel includes a **? Help** button at the bottom. Click it to open a help window with detailed English documentation for that specific node type, including:

- Overview and purpose
- Parameter descriptions
- Port design
- Usage tips
- Example scenarios

Help content covers all 10 node types with complete English documentation.

---

## 12. Example Flows

### 12.1 Complete Example Flow

```json
{
  "FlowName": "Auto Launch & Click",
  "Version": "1.0",
  "Nodes": [
    {
      "NodeId": "guid-1",
      "NodeType": "StartProgram",
      "NodeName": "Launch App",
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
      "NodeName": "Click Start",
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
      "NodeName": "Wait for Load",
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
      "NodeName": "Press F",
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

### 12.2 ColorMotion + Direction + Gate Example

```json
{
  "FlowName": "Visual Direction Tracking",
  "Nodes": [
    {
      "NodeId": "loop-1",
      "NodeType": "Loop",
      "NodeName": "Continuous Monitor",
      "Parameters": { "LoopMode": "BreakCondition" }
    },
    {
      "NodeId": "motion-1",
      "NodeType": "ColorMotion",
      "NodeName": "Direction Detect",
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
      "NodeName": "Press W",
      "Parameters": { "KeyName": "W", "PressMode": "Press" }
    },
    {
      "NodeId": "press-down",
      "NodeType": "KeyPress",
      "NodeName": "Press S",
      "Parameters": { "KeyName": "S", "PressMode": "Press" }
    }
  ]
}
```

> **Notes**:
> 1. **loop-1** (Loop, BreakCondition mode) provides infinite looping with break signal support
> 2. **motion-1** (ColorMotion.DirectionDetect) tracks red target via HSV + template match
> 3. Detected direction triggers corresponding key press node
> 4. Stationary = no action
> 5. Connect a Condition's True output to Loop's left white port for conditional exit

---

## 13. FAQ

### Q: Double-clicking FlowAuto.exe does nothing?

**A**: Check for the UAC prompt and click "Yes". If no prompt:

1. Install [.NET 8.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
2. Install [Visual C++ Redistributable (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe)

### Q: Template matching always fails?

**A**: Try:

1. Lower TemplateMatchThreshold (e.g. 0.7 or 0.6)
2. Expand TemplateScaleRange
3. Ensure the template matches the target area (resolution, DPI)
4. Verify Region settings

### Q: Keyboard simulation not working?

**A**: FlowAuto uses hardware scan code simulation:

1. Ensure running as administrator
2. Verify KeyScanCode value (see scan code table)
3. Some games have anti-cheat that blocks simulated input

### Q: Window not found?

**A**: TargetWindow uses substring matching:

1. Verify the keyword spelling
2. Use the window picker (Pick button) to auto-capture the title
3. Increase WaitForWindowMs

### Q: How to stop a running flow?

**A**: Click **Stop** on the toolbar, or press `Ctrl+S`. You can also disable nodes before execution (Enabled = false).

### Q: ColorMotion not detecting target color/movement?

**A**: Try:

1. Increase **HueTolerance** (8→20) and **SVTolerance** (30→50)
2. Ensure Region contains the target element
3. Use **Pick Color** tool to precisely pick the target color
4. Adjust **MoveThresholdPx** or **ColorChangeThreshold** for sensitivity

### Q: How to detect diagonal movement (e.g. down-left)?

**A**: Use two ColorMotion nodes + Gate AND:

1. ColorMotion A: DirectionDetect mode, detect **Left**
2. ColorMotion B: DirectionDetect mode, detect **Down**
3. Connect both True outputs to Gate's Input0 and Input1
4. Set Gate to **AND** mode
5. When both output True simultaneously → "down-left" movement

### Q: How to use Loop BreakCondition mode?

**A**:
1. Set Loop's LoopMode to **BreakCondition**
2. A  white BreakCond port appears on the left side
3. Connect a Condition/ColorMotion True output to the white port
4. The loop body runs continuously until the condition is met → Loop exits
5. No separate Break node is needed

### Q: What's the difference between ColorMotion and Condition.ImageAppear?

**A**:

| Feature         | Condition.ImageAppear | ColorMotion                      |
| --------------- | --------------------- | -------------------------------- |
| Main purpose    | Single image check    | Full color visual monitoring     |
| Output          | True/False branches   | Dual / multi-direction branches  |
| Image detection | ✅ One-time check      | StateChange mode available       |
| Motion detect   |                     | ✅ (MotionDetect)                |
| State change    | ❌                    | ✅ (StateChange)                 |
| Direction       | ❌                    | ✅ (DirectionDetect, 5 directions + Gate AND for diagonals) |
| HSV filtering   | ❌                    | ✅ All modes                     |

Use Condition.ImageAppear for simple image checks; use ColorMotion for HSV color tracking, motion analysis, and direction detection.

### Q: How to write ColorCal C# expressions?

**A**: Expressions reference target names defined in the node:

- `{Name}.Found` → boolean, whether target was found
- `{Name}.X` / `{Name}.Y` → pixel coordinates
- Supports ternary operator, comparisons, arithmetic

Examples:
- `Target1.Found ? 0 : 1` → found → port 0, else → port 1
- `Target1.X > 500 ? 0 : 1` → right of 500 → port 0
- `Target1.Found ? (Target1.X > Target2.X ? 0 : 1) : 2` → complex routing

### Q: Can I create Break nodes manually?

**A**: No. Break nodes are no longer auto-created. In BreakCondition mode, the Loop node has a built-in ⚪ BreakCond port on its left side. Simply connect a condition node's True output to this port — no Break node needed.

### Q: How to use the Color Picker?

**A**: Click **Pick Color** → "Pick Color from Screen" → click to pick color → adjust HSV tolerance sliders → select target node → "Apply to Node". Works with Condition, ColorMotion, ColorCal nodes.

### Q: How to use Pick Key?

**A** (v5.1): Click the **Pick Key** toolbar button → press any key on your keyboard → FlowAuto captures the scan code via a temporary low-level keyboard hook and fills `KeyScanCode` and `KeyName` into the selected KeyPress node(s). A confirmation popup shows the captured key and its scan code.

### Q: HSV + TemplateMatch confidence is very low (0.1~0.3)?

**A**: This was a known issue in older versions. **v5.1** fixed two root causes:
1. Template matching is now performed on the **HSV-filtered image** instead of the original screenshot ROI
2. Multi-scale matching now covers **0.5x~1.5x** with finer steps

If you still see low confidence:
1. Check `debug_hsv_tpl/` folder for debug images (original + HSV-filtered)
2. Adjust `HueTolerance` / `SVTolerance` so the filtered image clearly shows the target shape
3. Ensure the template image was captured while the ClickElement node (with HSV modes) was selected

### Q: ColorCal executes all branches instead of just the matching one?

**A**: This was a branching routing bug fixed in v5.1. In the fixed version, ColorCal / Condition / ColorMotion only execute the successor node connected to the port matching the result (e.g., result 0 → only port 0's successor). If you still experience this, ensure your flow file was saved with the latest version.

---

## Appendix A: Scan Code Reference

Hardware scan code mapping used by FlowAuto:

| Key  | Hex | Key  | Hex | Key   | Hex |
| ---- | --- | ---- | --- | ----- | --- |
| ESC  | 01  | 1    | 02  | 2     | 03  |
| 3    | 04  | 4    | 05  | 5     | 06  |
| 6    | 07  | 7    | 08  | 8     | 09  |
| 9    | 0A  | 0    | 0B  | TAB   | 0F  |
| Q    | 10  | W    | 11  | E     | 12  |
| R    | 13  | T    | 14  | Y     | 15  |
| U    | 16  | I    | 17  | O     | 18  |
| P    | 19  | A    | 1E  | S     | 1F  |
| D    | 20  | F    | 21  | G     | 22  |
| H    | 23  | J    | 24  | K     | 25  |
| L    | 26  | Z    | 2C  | X     | 2D  |
| C    | 2E  | V    | 2F  | B     | 30  |
| N    | 31  | M    | 32  | SHIFT | 2A  |
| CTRL | 1D  | SPACE| 39  | ENTER | 1C  |
| F1   | 3B  | F2   | 3C  | F3    | 3D  |
| F4   | 3E  | F5   | 3F  | F6    | 40  |
| F7   | 41  | F8   | 42  | F9    | 43  |
| F10  | 44  | F11  | 57  | F12   | 58  |

> KeyScanCode accepts decimal values (e.g. A = 30, which is 0x1E in hex). KeyName supports case-insensitive matching.
