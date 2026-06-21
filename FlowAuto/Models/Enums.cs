namespace FlowAuto.Models;

public enum NodeType
{
    StartProgram,
    ClickElement,
    WaitCondition,
    KeyPress,
    Loop,
    Condition,
    Gate,
    ColorMotion,
    ColorCal,
    Break,
    /// <summary>
    /// End marker for a Loop block pair. LoopStart + LoopEnd form a bracket
    /// that contains the loop body nodes between them.
    /// </summary>
    LoopEnd
}

public enum LocateMode
{
    Coordinate,
    TemplateMatch,
    OCR
}

/// <summary>
/// Condition node now only supports ImageAppear (template match check) and OCRContain.
/// ImageMove (visual motion sensor) has been moved to the standalone ColorMotion node.
/// </summary>
public enum ConditionType
{
    ImageAppear,
    OCRContain
}

public enum GateLogicType
{
    AND,
    OR,
    NOT
}

public enum NumericOperator
{
    GreaterThan,
    LessThan,
    GreaterOrEqual,
    LessOrEqual,
    Equal,
    NotEqual
}

public enum WaitConditionType
{
    ImageAppear,
    ImageDisappear,
    OCRContain,
    WindowExist,
    Timeout
}

public enum PressMode
{
    Press,
    Hold,
    Release
}

/// <summary>
/// Loop node operating mode.
/// </summary>
public enum LoopMode
{
    /// <summary>Fixed iteration count (LoopCount parameter).</summary>
    FixedCount,
    /// <summary>Loop until a break signal is received on the white break-condition input port.</summary>
    BreakCondition
}

/// <summary>
/// ColorMotion node operating mode.
/// </summary>
public enum ColorMotionMode
{
    /// <summary>Monitor target color movement state (moving or stationary).</summary>
    MotionDetect,
    /// <summary>Monitor color state change at a fixed position.</summary>
    StateChange,
    /// <summary>Detect movement direction. Supports two TrackModes:
    /// TemplateMatch (HSV filter + template matching) and ColorTrack (pure HSV center tracking).</summary>
    DirectionDetect
}

/// <summary>
/// Sub-mode for DirectionDetect. Controls how the target is located across frames.
/// </summary>
public enum DirectionDetectMode
{
    /// <summary>HSV color filter + template match on the filtered shape.
    /// Requires a ReferenceImagePath.</summary>
    TemplateMatch,
    /// <summary>Pure HSV color center-of-mass tracking.
    /// No template needed — tracks movement of any pixels matching the target color.</summary>
    ColorTrack
}

/// <summary>
/// Direction result for ColorMotion DirectionDetect mode.
/// Only 5 output ports are exposed: Up, Down, Left, Right, Stationary.
/// Diagonal detection is achieved by combining two ColorMotion nodes with a Gate AND.
/// </summary>
public enum MotionDirection
{
    Up,
    Down,
    Left,
    Right,
    Stationary
}
