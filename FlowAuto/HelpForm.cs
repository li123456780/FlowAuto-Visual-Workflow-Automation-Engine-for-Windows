using FlowAuto.Models;

namespace FlowAuto;

public partial class HelpForm : Form
{
    public HelpForm(NodeType nodeType)
    {
        Text = $"Help: {nodeType}";
        Size = new Size(520, 500);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(37, 37, 42);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        MaximizeBox = false;

        var rtb = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 35),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Consolas", 10),
            ReadOnly = true,
            WordWrap = true,
            BorderStyle = BorderStyle.None,
            Text = GetHelpText(nodeType)
        };
        Controls.Add(rtb);
    }

    private static string GetHelpText(NodeType type) => type switch
    {
        NodeType.StartProgram => StartProgramHelp,
        NodeType.ClickElement => ClickElementHelp,
        NodeType.WaitCondition => WaitConditionHelp,
        NodeType.KeyPress => KeyPressHelp,
        NodeType.Loop => LoopHelp,
        NodeType.LoopEnd => LoopEndHelp,
        NodeType.Condition => ConditionHelp,
        NodeType.Gate => GateHelp,
        NodeType.ColorMotion => ColorMotionHelp,
        NodeType.ColorCal => ColorCalHelp,
        NodeType.Break => BreakHelp,
        _ => "No help available for this node type."
    };

    // ================================================================
    // HELP CONTENT — English only
    // ================================================================

    private const string StartProgramHelp = """
        START PROGRAM
        =============

        Launches an external program or script as the first step
        in a workflow.

        PROPERTIES
        ----------
        File Path        — Full path to the .exe or script to launch.
        Work Dir         — Working directory for the process.
        Arguments        — Command-line arguments passed to the process.
        Run as Admin     — If checked, the process is started with
                           elevated privileges.
        Wait Window (ms) — Max time to wait for the target window
                           to appear after launch.
        Window Keyword   — A substring used to locate the launched
                           window by its title.

        PORTS
        -----
        Top    : Input  (accepts flow from previous node)
        Bottom : Output (continues to next node after launch)

        BEHAVIOR
        --------
        1. Launches the configured executable.
        2. Waits up to WaitWindowMs for a window whose title
           contains WindowKeyword to appear.
        3. Proceeds to the next connected node.

        TIP
        ---
        Use WindowKeyword to reliably detect that the target app
        has fully loaded before continuing.
        """;

    private const string ClickElementHelp = """
        CLICK ELEMENT
        =============

        Performs a mouse click on a UI element located by
        coordinate, template matching, or OCR.

        PROPERTIES
        ----------
        Target Window — Window title substring to target.
        Locate Mode    — How to find the click target:
            • Coordinate    : click at a fixed (X,Y) inside the
                              target window.
            • TemplateMatch : find a saved template image on screen
                              and click its center.
            • OCR           : find text on screen and click its
                              center.
        Full Window    — If checked, search the entire window.
                         Uncheck to restrict to a sub-region.
        Region         — (X, Y, W, H) sub-region within the window.
        Template Image — PNG image to match (TemplateMatch mode).
        Threshold      — Match confidence threshold (0–1).
        OCR Text       — Text to find on screen (OCR mode).
        Pre-delay (ms) — Wait before performing the click.
        Post-delay(ms) — Wait after performing the click.

        PORTS
        -----
        Top    : Input  (flow from previous node)
        Bottom : Output (continues after click)

        BEHAVIOR
        --------
        1. Locates the target window.
        2. Finds the click position by the selected LocateMode.
        3. Moves the mouse and performs a click.
        4. Waits for Post-delay, then continues.
        """;

    private const string WaitConditionHelp = """
        WAIT CONDITION
        ==============

        Pauses execution until a specified condition is met
        or a timeout occurs.

        PROPERTIES
        ----------
        Target Window — Window title substring to target.
        Condition      — What to wait for:
            • ImageAppear    : a template image appears on screen.
            • ImageDisappear : a template image disappears.
            • OCRContain     : specific text appears on screen.
            • WindowExist    : a window with the given title exists.
            • Timeout        : simply wait for a fixed duration.
        Full Window    — If checked, search the entire window.
        Region         — Sub-region (X, Y, W, H) to watch.
        Template Image — PNG template to watch for (image modes).
        Threshold      — Match confidence threshold (0–1).
        OCR Text       — Text to find (OCR mode).
        Check Interval — How often to re-check (ms).

        PORTS
        -----
        Top    : Input  (flow from previous node)
        Bottom : Output (continues when condition is met)

        BEHAVIOR
        --------
        1. Checks the condition every CheckIntervalMs.
        2. Continues when condition becomes true.
        3. If TimeoutMs elapses first, the node fails and
           the retry/cancel logic applies.

        TIP
        ---
        ImageAppear with a short check interval can be used
        as a "loading screen detector".
        """;

    private const string KeyPressHelp = """
        KEYPRESS
        ========

        Sends a keyboard key event to the target window.

        PROPERTIES
        ----------
        Target Window   — Window title substring to target.
        Key Name        — Descriptive key name (e.g. "Enter",
                          "Left", "A", "F1").
        Scan Code       — Windows virtual-key code. Leave 0 to
                          use Key Name resolution.
        Press Mode      — How to send the key:
            • Press  : press and release immediately.
            • Hold   : press and keep held.
            • Release: release a previously held key.
        Hold Duration   — How long to hold before release (Hold
                          mode only).

        PORTS
        -----
        Top    : Input  (flow from previous node)
        Bottom : Output (continues after key press)

        BEHAVIOR
        --------
        1. Brings the target window into focus.
        2. Sends the key event using the selected press mode.
        3. Continues to the next node.

        TIP
        ---
        Use Hold/Release pairs for holding modifier keys
        (Ctrl, Shift, Alt) during mouse operations.
        """;

    private const string LoopHelp = """
        LOOP
        ====

        Repeats a block of internal nodes. Two modes:
        FixedCount and BreakCondition.

        MODES
        -----
        FixedCount
            Loops exactly LoopCount times (0 = infinite).
            The loop body is a set of child nodes between the
            Loop node's output and input.
            After all iterations complete, flow exits via the
            Complete port.
            If a child fails or a Break node fires, flow exits
            via the Break port.

        BreakCondition
            Loops indefinitely until a break signal arrives
            on the white "Break" port on the LEFT side of the
            Loop node.
            Connect a Condition, Gate, or ColorMotion node's
            True output to this white port. When that upstream
            condition is satisfied, the loop exits.
            In this mode you do NOT need a separate Break node.

        PORTS
        -----
        Top     : Input  (cycle-back from last child)
        Left    : BreakCond (white) — exit signal (BreakCondition only)
        Bottom-L: Complete (green) — loop finished normally
        Bottom-R: Break    (red)   — loop interrupted

        BEHAVIOR (FixedCount)
        ---------------------
        1. Executes child nodes in order.
        2. Cycles back to step 1 until count reached.
        3. Exits Complete port or Break port accordingly.

        BEHAVIOR (BreakCondition)
        -------------------------
        1. Executes child nodes in order.
        2. Checks if BreakCond flag was set during iteration.
        3. If set, exits via Complete port.
        4. Otherwise, cycles back and repeats.

        TIP
        ---
        Use BreakCondition mode with a Condition node checking
        for an "exit" image to create an infinite loop that
        stops when a specific screen state is detected.
        """;

    private const string LoopEndHelp = """
        LOOP END
        ========

        Marks the end of a loop body. Works as a pair with
        Loop Start (the orange "Loop" node).

        TWO-BLOCK LOOP SYSTEM
        ---------------------
        ┌─────────────┐     ┌─────┐     ┌─────┐     ┌──────────┐     ┌─────┐
        │ Loop Start  │ ──→ │Node1│ ──→ │Node2│ ──→ │ Loop End │ ──→ │ Next│
        └─────────────┘     └─────┘     └─────┘     └──────────┘     └─────┘
              ↑                                                 │
              └─────────────── (loop back) ─────────────────────┘

        The Loop Start block defines loop parameters (mode, count).
        The Loop End block marks where the body ends.
        All nodes connected BETWEEN them form the loop body.

        PORTS
        -----
        Top    : Input  — connect the last body node here
        Bottom : Output — connect to the next node after the loop

        BEHAVIOR
        --------
        - The Loop Start block controls iteration.
        - Each iteration executes body nodes from start to end.
        - When the loop finishes (FixedCount reached or BreakCondition
          signal received), execution continues from the Loop End's
          output connection.

        TIP
        ---
        Place Loop Start and Loop End to "bracket" your loop body.
        Drag "Loop Start" from the toolbox — a Loop End is created
        automatically below it.
        """;

    private const string ConditionHelp = """
        CONDITION
        =========

        Evaluates a condition and branches execution based on
        the result.

        PROPERTIES
        ----------
        Condition    — Type of check:
            • ImageAppear : does a template image exist on screen?
            • OCRContain  : does specific text appear on screen?
        Full Window  — If checked, scan the entire window.
        Region       — Sub-region (X, Y, W, H) to scan.
        Template     — PNG template to search for (ImageAppear).
        Threshold    — Match confidence (0–1).
        OCR Text     — Text to find (OCR mode).

        PORTS
        -----
        Top     : Input  (flow from previous node)
        Bottom-L: True   (green) — condition met
        Bottom-R: False  (red)   — condition NOT met

        BEHAVIOR
        --------
        1. Captures the target window or region.
        2. Evaluates the configured condition.
        3. Routes flow to True branch or False branch
           depending on the result.

        TIP
        ---
        Chain multiple Condition nodes with Gate (AND/OR)
        to create complex branching logic.
        """;

    private const string GateHelp = """
        GATE
        ====

        A logical decision node that accepts multiple input
        signals and produces a single output result.

        PROPERTIES
        ----------
        Logic Type — Boolean operation to apply:
            • AND : all connected inputs must be true.
            • OR  : at least one input must be true.
            • NOT : inverts the value of Input0 (only Input0
                    is evaluated; Input1 is ignored).

        PORTS
        -----
        Top-Left  : Input0 (cyan)
        Top-Right : Input1 (cyan)
        Bottom    : Result (cyan)

        BEHAVIOR
        --------
        1. Collects boolean values from connected input ports.
        2. Applies the selected logic operation.
        3. Outputs a single true/false result.

        TYPICAL USAGE
        -------------
        Connect two Condition nodes to Input0 and Input1.
        Set Gate to AND → only proceed when BOTH conditions
        are true simultaneously.

        TIP
        ---
        Combine with Condition.ColorMotion sensor nodes
        to create multi-condition triggers.
        """;

    private const string ColorMotionHelp = """
        COLOR MOTION
        ============

        A visual sensor node that monitors target regions
        for HSV-color-based movement, state changes, or
        directional motion.

        MODES
        -----
        MotionDetect
            Detects whether a color blob is moving or stationary.
            Compares its position over multiple frames.
            Output: True (moving) / False (stationary).

        StateChange
            Monitors for the appearance or disappearance of
            a color in a fixed area.
            Output: True (changed) / False (no change).

        DirectionDetect
            Tracks movement direction of a colored target.
            Two sub-modes (TrackMode):

            • TemplateMatch (default)
              Uses HSV color filter + template matching.
              Requires a Reference Image of the target shape.
              How to get a clean Reference Image:
              → Click the Snip button on the toolbar while
                a ColorMotion node is selected. The captured
                image will be automatically HSV-filtered so
                ONLY the target color's shape remains visible!

            • ColorTrack
              Pure HSV center-of-mass tracking.
              No reference image needed — simply tracks where
              the target color pixels are moving.
              Faster, but less shape-aware.

            Outputs: Up, Down, Left, Right, Stationary.
            Diagonal: combine two nodes via Gate AND.

        SNIP WITH HSV FILTER (NEW)
        --------------------------
        When a ColorMotion node is selected, clicking the
        Screenshot (snip) button on the toolbar will:
        1. Capture the selected screen region.
        2. Apply the HSV color filter using the node's
           Target Color / Hue Tolerance / SV Tolerance.
        3. Save an image where ONLY the target color shape
           is visible (everything else = black).
        This is ideal for creating Reference Images for
        TemplateMatch mode.

        PROPERTIES
        ----------
        Target Window  — Window title substring.
        Full Window    — Scan entire window or a Region.
        Region         — (X, Y, W, H) monitoring area.
        Target Color   — RGB color to filter (converted to HSV
                          internally).
        Hue Tolerance  — HSV hue tolerance for color matching.
        SV Tolerance   — HSV saturation/value tolerance.
        Track Mode     — TemplateMatch or ColorTrack.
        Ref Image      — Reference PNG for TemplateMatch mode.
        Threshold      — Template match threshold (TemplateMatch).
        Check Interval — How often to sample (ms).
        Duration       — Total monitoring duration (ms).
        Move Threshold — Min pixels to count as movement.

        PORTS
        -----
        MotionDetect / StateChange:
            Top     : Input
            Bottom-L: True  (green)
            Bottom-R: False (red)

        DirectionDetect (5 output ports distributed across bottom):
            Up, Down, Left, Right, Stationary

        TIP
        ---
        Use the Pick Color tool in the toolbar to sample
        an RGB value directly from the screen. Then use the
        Snip tool (with HSV filtering) to capture a clean
        reference image.
        """;

    private const string ColorCalHelp = """
        COLOR CAL
        =========

        Multi-target detection node with custom expression
        evaluation and branch routing. Detects multiple colored
        objects simultaneously, computes a single integer result
        via a C# expression, and routes execution to the
        corresponding output port.

        DETECTION TARGETS
        -----------------
        Each target is an independent detection object with:
        Name       — Variable name used in expressions.
        Window     — Target window title.
        Full Window — Scan entire window or a sub-region.
        Region     — (X, Y, W, H) sub-region.
        Color      — RGB color to detect (HSV-filtered).
        Hue Tol    — Hue tolerance for color matching.
        SV Tol     — Saturation/Value tolerance.
        Template   — Optional PNG for template matching
                     (takes priority over HSV detection).
        Tpl Thr.   — Template match threshold.

        EXPRESSION
        ----------
        A C# expression that must return a single integer.
        Reference targets by name with these properties:
            {Name}.X       — detected X pixel coordinate
            {Name}.Y       — detected Y pixel coordinate
            {Name}.Found   — true if target was detected

        Examples:
            Target1.Found ? 0 : 1
            Target1.Found ? (Target1.X > 500 ? 0 : 1) : 2
            (Target1.X + Target2.X) / 2 > 300 ? 1 : 0

        BRANCH ROUTING
        --------------
        Output Count — Number of downstream output ports.
        The integer result of the expression selects which
        port to activate:
            Result 0 → Port 0, Result 1 → Port 1, etc.

        PORTS
        -----
        Top    : Input (flow from previous node)
        Bottom : Port 0, Port 1, ... Port N (colored)

        TIP
        ---
        Create multiple targets for multi-element UI monitoring.
        Use the expression to implement complex decision logic
        based on relative positions of detected objects.
        """;

    private const string BreakHelp = """
        BREAK
        =====

        Auto-generated with a Loop node. Provides an early-exit
        path from an active loop.

        PORTS
        -----
        Top     : Input  (receives signal from Loop's Break port
                          or from a Condition node)
        Bottom-L: Break   (green) — interrupt the enclosing loop
        Bottom-R: Continue (red)  — fallback path

        BEHAVIOR
        --------
        When triggered, sets the loop's break flag, causing
        the enclosing Loop to exit immediately.

        TIP
        ---
        Connect a Condition node's True output to the Break
        node to create an "if condition, stop looping" pattern.
        """;
}
