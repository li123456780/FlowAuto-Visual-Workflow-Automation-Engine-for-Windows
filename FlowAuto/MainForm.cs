using System.Runtime.InteropServices;
using System.Text.Json;
using FlowAuto.Core;
using FlowAuto.Engine;
using FlowAuto.Models;

namespace FlowAuto;

public partial class MainForm : Form
{
    private FlowCanvas _canvas = null!;
    private ToolboxPanel _toolbox = null!;
    private PropertyPanel _propertyPanel = null!;
    private RichTextBox _logBox = null!;
    private FlowLogger _logger = null!;
    private FlowContext? _execContext;
    private FlowExecutor? _executor;
    private string _currentFilePath = "";

    // Toolbar buttons
    private Button _btnNew = null!;
    private Button _btnLoad = null!;
    private Button _btnSave = null!;
    private Button _btnRun = null!;
    private Button _btnPause = null!;
    private Button _btnStop = null!;
    private Button _btnScreenshot = null!;
    private Button _btnWindowPicker = null!;
    private Button _btnRegionPicker = null!;
    private Button _btnColorPicker = null!;
    private Button _btnKeyPicker = null!;
    private Button _btnSettings = null!;

    // Global settings
    private int _globalPreDelayMs = 500;
    private int _globalPostDelayMs = 500;

    private Label _statusLabel = null!;
    private SplitContainer _mainSplit = null!;
    private SplitContainer _rightSplit = null!;

    public MainForm()
    {
        Text = "FlowAuto - Visual Workflow Automation Engine";
        Size = new Size(1400, 900);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 30, 35);
        KeyPreview = true;

        InitializeComponents();
    }

    private void InitializeComponents()
    {
        // Top toolbar
        var toolbar = CreateToolbar();

        // Status bar
        _statusLabel = new Label
        {
            Text = "Ready",
            ForeColor = Color.FromArgb(180, 180, 180),
            BackColor = Color.FromArgb(20, 20, 25),
            Dock = DockStyle.Bottom,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        // Main split container
        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 3,
            BackColor = Color.FromArgb(45, 45, 50)
        };

        // Left: Toolbox
        var toolboxContainer = new Panel { Dock = DockStyle.Fill };
        _toolbox = new ToolboxPanel();
        toolboxContainer.Controls.Add(_toolbox);

        // Center: Canvas
        _canvas = new FlowCanvas { Dock = DockStyle.Fill };
        _canvas.NodesChanged += OnCanvasChanged;
        _canvas.NodeSelected += OnNodeSelected;
        _canvas.SelectionCleared += () => _propertyPanel.ShowNode(null);

        // Right split: Property panel + Log
        _rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 3,
            BackColor = Color.FromArgb(45, 45, 50)
        };

        // Right-top: Property panel
        _propertyPanel = new PropertyPanel();
        var propContainer = new Panel { Dock = DockStyle.Fill };
        var propHeader = new Label
        {
            Text = "Properties",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(37, 37, 42),
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };
        propContainer.Controls.Add(propHeader);
        propContainer.Controls.Add(_propertyPanel);

        // Right-bottom: Log
        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 25),
            ForeColor = Color.FromArgb(200, 200, 200),
            Font = new Font("Consolas", 9),
            ReadOnly = true,
            WordWrap = false
        };
        var logContainer = new Panel { Dock = DockStyle.Fill };
        var logHeader = new Label
        {
            Text = "Execution Log",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(37, 37, 42),
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };
        logContainer.Controls.Add(_logBox);
        logContainer.Controls.Add(logHeader);

        _rightSplit.Panel1.Controls.Add(propContainer);
        _rightSplit.Panel2.Controls.Add(logContainer);

        // Layout splits
        _mainSplit.Panel1.Controls.Add(toolboxContainer);
        _mainSplit.Panel2.Controls.Add(_canvas);

        // Main layout
        var mainPanel = new Panel { Dock = DockStyle.Fill };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

        layout.Controls.Add(_mainSplit, 0, 0);
        layout.Controls.Add(_rightSplit, 1, 0);

        mainPanel.Controls.Add(layout);
        mainPanel.Controls.Add(toolbar);
        mainPanel.Controls.Add(_statusLabel);

        toolbar.Dock = DockStyle.Top;
        _statusLabel.Dock = DockStyle.Bottom;

        Controls.Add(mainPanel);

        _logger = new FlowLogger(richTextBox: _logBox);

        // Set split container properties after layout is complete
        Load += (s, e) =>
        {
            _mainSplit.Panel1MinSize = 150;
            _mainSplit.Panel2MinSize = 250;
            _mainSplit.SplitterDistance = Math.Min(280, _mainSplit.Width - _mainSplit.Panel2MinSize - 10);

            _rightSplit.Panel1MinSize = 100;
            _rightSplit.Panel2MinSize = 100;
            _rightSplit.SplitterDistance = Math.Min(350, _rightSplit.Height - _rightSplit.Panel2MinSize - 10);
        };

        // Start with empty canvas
    }

    // ============ Global hotkeys ============

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Ctrl+S: Stop execution
        if (keyData == (Keys.Control | Keys.S))
        {
            if (_execContext?.Cts != null && !_execContext.Cts.IsCancellationRequested)
            {
                StopFlow();
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private Panel CreateToolbar()
    {
        var toolbar = new Panel
        {
            Height = 40,
            BackColor = Color.FromArgb(44, 44, 49)
        };

        int x = 8;
        _btnNew = CreateToolButton("New", x, () => NewFlow()); x += 52;
        _btnLoad = CreateToolButton("Load", x, () => LoadFlow(), width: 60); x += 66;
        _btnSave = CreateToolButton("Save", x, () => SaveFlow()); x += 52;
        x += 12;
        _btnRun = CreateToolButton("Run", x, () => RunFlow(), Color.FromArgb(52, 168, 83)); x += 52;
        _btnPause = CreateToolButton("Pause", x, () => PauseFlow(), Color.FromArgb(251, 188, 4)); x += 52;
        _btnStop = CreateToolButton("Stop", x, () => StopFlow(), Color.FromArgb(233, 30, 99)); x += 52;
        x += 12;
        _btnScreenshot = CreateToolButton("Snip", x, OpenScreenshotTool); x += 52;
        _btnWindowPicker = CreateToolButton("Pick Win", x, OpenWindowPicker, width: 72); x += 78;
        _btnRegionPicker = CreateToolButton("Pick Rgn", x, OpenRegionPicker, width: 72); x += 78;
        _btnColorPicker = CreateToolButton("Pick Color", x, OpenColorPicker, width: 78, color: Color.FromArgb(156, 39, 176)); x += 84;
        _btnKeyPicker = CreateToolButton("Pick Key", x, OpenKeyPicker, width: 72, color: Color.FromArgb(33, 150, 243)); x += 78;
        x += 12;
        _btnSettings = CreateToolButton("Settings", x, OpenGlobalSettings, width: 72); x += 78;

        toolbar.Controls.AddRange([
            _btnNew, _btnLoad, _btnSave, _btnRun, _btnPause, _btnStop, _btnScreenshot, _btnWindowPicker, _btnRegionPicker, _btnColorPicker, _btnKeyPicker, _btnSettings
        ]);

        return toolbar;
    }

    private Button CreateToolButton(string text, int x, Action action, Color? color = null, int width = 46)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, 6),
            Size = new Size(width, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = color ?? Color.FromArgb(60, 60, 65),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (s, e) => action();
        return btn;
    }

    // ============ Canvas events ============

    private void OnCanvasChanged()
    {
        _statusLabel.Text = $"Nodes: {_canvas.Nodes.Count}";
    }

    private void OnNodeSelected(int index)
    {
        if (index >= 0 && index < _canvas.Nodes.Count)
        {
            _propertyPanel.ShowNode(_canvas.Nodes[index]);
        }
    }

    // ============ File operations ============

    private void NewFlow()
    {
        if (_canvas.Nodes.Count > 0)
        {
            var result = MessageBox.Show("Clear current flow?", "New Flow", MessageBoxButtons.YesNo);
            if (result != DialogResult.No)
                _canvas.ClearNodes();
        }
    }

    private void LoadFlow()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Flow Files|*.flow.json|All Files|*.*",
            Title = "Load Flow"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var flow = JsonSerializer.Deserialize<FlowDefinition>(json);
            if (flow == null) return;

            _canvas.ClearNodes();
            foreach (var node in flow.Nodes)
            {
                _canvas.Nodes.Add(node);
            }
            // Load connections if present in JSON
            if (flow.Connections != null && flow.Connections.Count > 0)
            {
                _canvas.LoadConnections(flow.Connections);
            }
            else
            {
                // Fallback: auto-connect for backward compatibility
                AutoConnectNodes();
            }
            _canvas.Invalidate();
            _currentFilePath = dlg.FileName;
            _statusLabel.Text = $"Loaded: {Path.GetFileName(dlg.FileName)}";
            _logger.Info("SYSTEM", $"Loaded flow: {flow.FlowName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AutoConnectNodes()
    {
        _canvas.Connections.Clear();
        for (int i = 0; i < _canvas.Nodes.Count - 1; i++)
        {
            var fromNode = _canvas.Nodes[i];
            var toNode = _canvas.Nodes[i + 1];
            // Determine appropriate ports based on node types
            string fromPort = fromNode.NodeType switch
            {
                NodeType.Condition or NodeType.Loop => "True",
                NodeType.ColorCal => "0",
                _ => "Output"
            };
            _canvas.AddConnection(fromNode.NodeId, toNode.NodeId, fromPort);
        }
    }

    private void SaveFlow()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "Flow Files|*.flow.json|All Files|*.*",
            Title = "Save Flow",
            DefaultExt = ".flow.json",
            FileName = string.IsNullOrEmpty(_currentFilePath) ? "unnamed.flow.json" : Path.GetFileName(_currentFilePath)
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            var flow = GetFlowDefinition();
            var json = JsonSerializer.Serialize(flow, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json);
            _currentFilePath = dlg.FileName;
            _statusLabel.Text = $"Saved: {Path.GetFileName(dlg.FileName)}";
            _logger.Info("SYSTEM", $"Saved flow: {flow.FlowName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public FlowDefinition GetFlowDefinition()
    {
        return new FlowDefinition
        {
            FlowName = Path.GetFileNameWithoutExtension(_currentFilePath) ?? "Unnamed Flow",
            Nodes = _canvas.Nodes.ToList(),
            Connections = _canvas.Connections.ToList()
        };
    }

    // ============ Execution ============

    private async void RunFlow()
    {
        var flow = GetFlowDefinition();
        if (flow.Nodes.Count == 0)
        {
            MessageBox.Show("No nodes to execute.", "Info");
            return;
        }

        _logBox.Clear();
        _execContext = new FlowContext(_logger);
        _executor = new FlowExecutor(_execContext);
        _execContext.Cts = new CancellationTokenSource();

        // Inject global delay settings into context
        _execContext.Set("GlobalPreDelayMs", _globalPreDelayMs);
        _execContext.Set("GlobalPostDelayMs", _globalPostDelayMs);

        SetExecutionButtons(running: true);

        try
        {
            await Task.Run(async () =>
            {
                try
                {
                    await _executor.ExecuteAsync(flow);
                }
                catch (OperationCanceledException)
                {
                    _logger.Warning("SYSTEM", "Execution stopped by user");
                }
                catch (Exception ex)
                {
                    _logger.Error("SYSTEM", $"Execution failed: {ex.Message}");
                }
            });
        }
        finally
        {
            SetExecutionButtons(running: false);
        }
    }

    private void PauseFlow()
    {
        if (_execContext == null) return;

        if (!_execContext.IsPaused)
        {
            _execContext.IsPaused = true;
            _execContext.PauseTcs = new TaskCompletionSource<bool>();
            _btnPause.Text = "Resume";
            _btnPause.BackColor = Color.FromArgb(52, 168, 83);
            _statusLabel.Text = "PAUSED";
        }
        else
        {
            _execContext.IsPaused = false;
            _execContext.PauseTcs?.TrySetResult(true);
            _btnPause.Text = "Pause";
            _btnPause.BackColor = Color.FromArgb(251, 188, 4);
            _statusLabel.Text = "Running...";
        }
    }

    private void StopFlow()
    {
        _execContext?.Cts?.Cancel();
        if (_execContext?.IsPaused == true)
        {
            _execContext.IsPaused = false;
            _execContext.PauseTcs?.TrySetResult(false);
        }
        SetExecutionButtons(running: false);
        _statusLabel.Text = "Stopped";
        _logger.Info("SYSTEM", "Stopping...");
    }

    private void SetExecutionButtons(bool running)
    {
        _btnRun.Enabled = !running;
        _btnPause.Enabled = running;
        _btnStop.Enabled = running;
        _btnPause.Text = "Pause";
        _btnPause.BackColor = Color.FromArgb(251, 188, 4);

        if (running)
            _statusLabel.Text = "Running...";
        else
            _statusLabel.Text = "Ready";
    }

    // ============ Screenshot Tool ============

    private void OpenScreenshotTool()
    {
        // ── If current node is ColorMotion or ClickElement, pass HSV params so the snip is filtered ──
        System.Drawing.Color? hsvColor = null;
        int hueTol = 8, svTol = 30;
        if (_propertyPanel.CurrentNode != null &&
            (_propertyPanel.CurrentNode.NodeType == Models.NodeType.ColorMotion ||
             _propertyPanel.CurrentNode.NodeType == Models.NodeType.ClickElement))
        {
            var node = _propertyPanel.CurrentNode;
            hsvColor = node.ResolveTargetRgb();
            hueTol = node.GetParam<int?>("HueTolerance") ?? 8;
            svTol = node.GetParam<int?>("SVTolerance") ?? 30;
        }

        new ScreenshotOverlay(screenshotPath =>
        {
            foreach (var idx in _canvas.SelectedNodeIndices)
            {
                var node = _canvas.Nodes[idx];
                node.SetParam("TemplateImagePath", screenshotPath);
                // For ColorMotion/ClickElement HSVTemplateMatch, also set as ReferenceImagePath
                if (node.NodeType == Models.NodeType.ColorMotion || node.NodeType == Models.NodeType.ClickElement)
                    node.SetParam("ReferenceImagePath", screenshotPath);
            }
            if (_propertyPanel.CurrentNode != null)
                _propertyPanel.ShowNode(_propertyPanel.CurrentNode);
            _logger.Info("SYSTEM", $"Screenshot saved: {screenshotPath} ({_canvas.SelectedNodeIndices.Count} node(s))");
        }, hsvColor, hueTol, svTol).ShowDialog(this);
    }

    // ============ Window Picker ============

    private void OpenWindowPicker()
    {
        _statusLabel.Text = "Move mouse over target window and press Ctrl...";
        MessageBox.Show("Move your mouse over the target window and press Ctrl to capture its title.",
            "Window Picker", MessageBoxButtons.OK, MessageBoxIcon.Information);

        // Start monitoring
        StartWindowPicker();
    }

    private void StartWindowPicker()
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                // Check if Ctrl is held
                if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
                {
                    IntPtr hWnd = WindowHelper.GetForegroundWindow();
                    if (hWnd != IntPtr.Zero)
                    {
                        var sb = new System.Text.StringBuilder(256);
                        WindowHelper.GetWindowText(hWnd, sb, sb.Capacity);
                        string title = sb.ToString();

                        this.Invoke(() =>
                    {
                        foreach (var idx in _canvas.SelectedNodeIndices)
                        {
                            var node = _canvas.Nodes[idx];
                            node.SetParam("TargetWindow", title);
                        }
                        if (_propertyPanel.CurrentNode != null)
                            _propertyPanel.ShowNode(_propertyPanel.CurrentNode);
                        _statusLabel.Text = $"Window captured: {title}";
                        _logger.Info("SYSTEM", $"Window picked: {title} ({_canvas.SelectedNodeIndices.Count} node(s))");
                    });
                        break;
                    }
                }
                Thread.Sleep(100);
            }
        });
        thread.IsBackground = true;
        thread.Start();
        _statusLabel.Text = "Waiting for Ctrl press...";
    }

    // ============ Region Picker ============

    private void OpenRegionPicker()
    {
        if (_propertyPanel.CurrentNode == null)
        {
            MessageBox.Show("Please select a node first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var node = _propertyPanel.CurrentNode;
        var targetWindow = node.GetParam<string>("TargetWindow") ?? "";

        // ColorCal: try per-target TargetWindow from DetectionTargets
        if (string.IsNullOrEmpty(targetWindow) && node.NodeType == NodeType.ColorCal)
        {
            var targets = node.GetParam<List<ColorCalTarget>>("DetectionTargets");
            if (targets != null && targets.Count > 0)
                targetWindow = targets[0].TargetWindow ?? "";
        }

        if (string.IsNullOrEmpty(targetWindow))
        {
            if (_execContext != null && _execContext.CurrentHwnd != IntPtr.Zero)
            {
                OpenWindowRegionPicker(_execContext.CurrentHwnd);
                return;
            }
            MessageBox.Show("Please set a Target Window first (use Pick Win, or manually type a window title).",
                "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var hWnd = WindowHelper.FindWindowByTitle(targetWindow);
        if (hWnd == IntPtr.Zero)
        {
            MessageBox.Show($"Window \"{targetWindow}\" not found. Make sure the target window is open.",
                "Window Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        OpenWindowRegionPicker(hWnd);
    }

    private void OpenWindowRegionPicker(IntPtr hWnd)
    {
        this.WindowState = FormWindowState.Minimized;

        var picker = new WindowRegionPicker(hWnd, region =>
        {
            this.Invoke(() =>
            {
                foreach (var idx in _canvas.SelectedNodeIndices)
                {
                    var selNode = _canvas.Nodes[idx];
                    // ColorCal: fill Region for ALL targets
                    if (selNode.NodeType == NodeType.ColorCal)
                    {
                        var targets = selNode.GetParam<List<ColorCalTarget>>("DetectionTargets");
                        if (targets != null)
                        {
                            foreach (var t in targets)
                            {
                                t.Region = new Engine.Region { X = region.X, Y = region.Y, Width = region.Width, Height = region.Height };
                            }
                            selNode.SetParam("DetectionTargets", targets);
                        }
                    }
                    else
                    {
                        selNode.SetParam("Region", region);
                    }
                }
                if (_propertyPanel.CurrentNode != null)
                    _propertyPanel.ShowNode(_propertyPanel.CurrentNode);
                _statusLabel.Text = $"Region: ({region.X}, {region.Y}) {region.Width}x{region.Height}";
                _logger.Info("SYSTEM", $"Region picked: ({region.X},{region.Y}) {region.Width}x{region.Height} ({_canvas.SelectedNodeIndices.Count} node(s))");
            });
        });

        picker.FormClosed += (s, e) =>
        {
            this.Invoke(() =>
            {
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            });
        };

        picker.Show();
    }

    // ============ Color Picker ============

    private void OpenColorPicker()
    {
        using var picker = new ColorPickerForm((color, hueTol, svTol) =>
        {
            int filledCount = 0;
            foreach (var idx in _canvas.SelectedNodeIndices)
            {
                var node = _canvas.Nodes[idx];
                if (node.NodeType == NodeType.ColorMotion ||
                    node.NodeType == NodeType.ColorCal ||
                    node.NodeType == NodeType.ClickElement)
                {
                    node.SetParam("TargetRgb", $"{color.R},{color.G},{color.B}");
                    node.SetParam("HueTolerance", hueTol);
                    node.SetParam("SVTolerance", svTol);
                    filledCount++;
                }
            }
            if (_propertyPanel.CurrentNode != null)
                _propertyPanel.ShowNode(_propertyPanel.CurrentNode);
            _statusLabel.Text = $"Color picked: RGB({color.R},{color.G},{color.B}) H±{hueTol} SV±{svTol}";
            _logger.Info("SYSTEM", $"Color picked: RGB({color.R},{color.G},{color.B}) HueTol={hueTol} SVTol={svTol} ({filledCount} node(s))");
        });

        if (picker.ShowDialog(this) == DialogResult.OK)
        {
            // Color already applied via callback
        }
    }

    // ============ Global Settings ============

    private void OpenGlobalSettings()
    {
        var form = new Form
        {
            Text = "Global Settings",
            Size = new Size(360, 200),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(37, 37, 42)
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(16),
            BackColor = Color.FromArgb(37, 37, 42)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Pre-delay
        var lblPre = new Label { Text = "Pre-click Delay (ms):", ForeColor = Color.White, AutoSize = true, TextAlign = ContentAlignment.MiddleRight };
        var nudPre = new NumericUpDown { Minimum = 0, Maximum = 99999, Value = _globalPreDelayMs, BackColor = Color.FromArgb(50, 50, 55), ForeColor = Color.White, Dock = DockStyle.Fill };
        table.Controls.Add(lblPre, 0, 0);
        table.Controls.Add(nudPre, 1, 0);

        // Post-delay
        var lblPost = new Label { Text = "Post-click Delay (ms):", ForeColor = Color.White, AutoSize = true, TextAlign = ContentAlignment.MiddleRight };
        var nudPost = new NumericUpDown { Minimum = 0, Maximum = 99999, Value = _globalPostDelayMs, BackColor = Color.FromArgb(50, 50, 55), ForeColor = Color.White, Dock = DockStyle.Fill };
        table.Controls.Add(lblPost, 0, 1);
        table.Controls.Add(nudPost, 1, 1);

        // Buttons
        var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0) };
        var btnCancel = new Button { Text = "Cancel", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 60, 65), ForeColor = Color.White, Size = new Size(80, 28) };
        var btnOk = new Button { Text = "OK", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(52, 168, 83), ForeColor = Color.White, Size = new Size(80, 28) };
        btnCancel.Click += (s, e) => form.DialogResult = DialogResult.Cancel;
        btnOk.Click += (s, e) => form.DialogResult = DialogResult.OK;
        btnPanel.Controls.Add(btnCancel);
        btnPanel.Controls.Add(btnOk);
        table.Controls.Add(btnPanel, 1, 2);

        form.Controls.Add(table);

        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _globalPreDelayMs = (int)nudPre.Value;
            _globalPostDelayMs = (int)nudPost.Value;
            _statusLabel.Text = $"Global delays: Pre={_globalPreDelayMs}ms, Post={_globalPostDelayMs}ms";
            _logger.Info("SYSTEM", $"Global settings updated: Pre={_globalPreDelayMs}ms, Post={_globalPostDelayMs}ms");
        }
    }

    // ============ Example ============

    private void CreateExampleFlow()
    {
        var flow = new FlowDefinition
        {
            FlowName = "Example: Launch & Click",
            Nodes = new List<FlowNode>
            {
                FlowCanvas.CreateDefaultNode(NodeType.StartProgram),
                FlowCanvas.CreateDefaultNode(NodeType.ClickElement),
                FlowCanvas.CreateDefaultNode(NodeType.WaitCondition),
                FlowCanvas.CreateDefaultNode(NodeType.KeyPress)
            }
        };

        foreach (var node in flow.Nodes)
        {
            _canvas.Nodes.Add(node);
        }
        AutoConnectNodes();
        OnCanvasChanged();
    }

    // ============ Pick Key (Low-level Keyboard Hook) ============

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hHook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hHook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

    private IntPtr _keyHookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _keyHookProc;

    private void OpenKeyPicker()
    {
        if (_keyHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyHookId);
            _keyHookId = IntPtr.Zero;
        }

        _keyHookProc = KeyHookCallback;
        _keyHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyHookProc, GetModuleHandle(null), 0);

        if (_keyHookId == IntPtr.Zero)
        {
            MessageBox.Show(this, "Failed to install keyboard hook.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _statusLabel.Text = "Press any key to capture its scan code...";
        _logger.Info("SYSTEM", "Key picker active — press any key...");
    }

    private IntPtr KeyHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            byte scanCode = (byte)kb.scanCode;

            // Unhook immediately to capture only the first key press
            if (_keyHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyHookId);
                _keyHookId = IntPtr.Zero;
            }

            this.Invoke(() =>
            {
                bool known = InputSimulator.IsKnownScanCode(scanCode);
                string? keyName = InputSimulator.GetKeyName(scanCode);
                string displayName = keyName ?? $"VK 0x{kb.vkCode:X}";

                // Fill all selected KeyPress nodes
                int filledCount = 0;
                foreach (var idx in _canvas.SelectedNodeIndices)
                {
                    var node = _canvas.Nodes[idx];
                    if (node.NodeType == NodeType.KeyPress)
                    {
                        node.SetParam("KeyScanCode", scanCode);
                        if (!string.IsNullOrEmpty(keyName))
                            node.SetParam("KeyName", keyName);
                        filledCount++;
                    }
                }

                if (_propertyPanel.CurrentNode != null)
                    _propertyPanel.ShowNode(_propertyPanel.CurrentNode);

                string msg = $"Key captured: {displayName} (ScanCode: 0x{scanCode:X2})";
                if (!known)
                    msg += " ⚠️ Unknown scan code — may cause corrupted warnings";

                _statusLabel.Text = msg;
                _logger.Info("SYSTEM", $"{msg}{(filledCount > 0 ? $" — filled {filledCount} KeyPress node(s)" : "")}");

                MessageBox.Show(this, msg, "Pick Key", MessageBoxButtons.OK,
                    known ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });

            return (IntPtr)1; // Block the key from further propagation
        }
        return CallNextHookEx(_keyHookId, nCode, wParam, lParam);
    }
}
