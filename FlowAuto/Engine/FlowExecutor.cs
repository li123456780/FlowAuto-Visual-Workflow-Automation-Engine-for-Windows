using System.Diagnostics;
using System.Text.Json;
using FlowAuto.Core;
using FlowAuto.Models;
using System.Drawing;
using OpenCvSharp;
using Point = System.Drawing.Point;

namespace FlowAuto.Engine;

public class FlowExecutor
{
    private readonly FlowContext _context;

    public FlowExecutor(FlowContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Execute the entire flow definition.
    /// </summary>
    public async Task ExecuteAsync(FlowDefinition flow)
    {
        _context.Logger.Info("SYSTEM", $"Starting flow: {flow.FlowName}");
        _context.CurrentNodeIndex = 0;

        if (flow.Connections != null && flow.Connections.Count > 0)
        {
            await ExecuteNodesByConnectionsAsync(flow.Nodes, flow.Connections);
        }
        else
        {
            await ExecuteNodeListAsync(flow.Nodes);
        }

        _context.Logger.Success("SYSTEM", $"Flow completed: {flow.FlowName}");
    }

    /// <summary>
    /// Execute nodes in connection-defined order (graph traversal).
    /// Falls back to list order for any unvisited orphan nodes.
    /// </summary>
    private async Task ExecuteNodesByConnectionsAsync(List<FlowNode> nodes, List<FlowConnection> connections)
    {
        // Build lookup: FromId -> list of (ToId, FromPort, ToPort)
        var outgoingMap = new Dictionary<string, List<(string ToId, string FromPort, string ToPort)>>();
        foreach (var conn in connections)
        {
            if (!outgoingMap.TryGetValue(conn.FromId, out var list))
                outgoingMap[conn.FromId] = list = new();
            list.Add((conn.ToId, conn.FromPort, conn.ToPort));
        }

        // Find nodes that no other node points to (root nodes)
        var targetIds = new HashSet<string>(connections.Select(c => c.ToId));
        var roots = nodes.Where(n => !targetIds.Contains(n.NodeId)).ToList();
        var visited = new HashSet<string>();

        // Execute roots first (may be multiple start points)
        foreach (var root in roots)
        {
            await TraverseNodeChainAsync(root, nodes, outgoingMap, visited);
        }

        // Execute any remaining unvisited nodes in list order
        var remaining = nodes.Where(n => !visited.Contains(n.NodeId)).ToList();
        if (remaining.Count > 0)
        {
            await ExecuteNodeListAsync(remaining);
        }
    }

    private async Task TraverseNodeChainAsync(
        FlowNode node, List<FlowNode> allNodes,
        Dictionary<string, List<(string ToId, string FromPort, string ToPort)>> outgoingMap,
        HashSet<string> visited)
    {
        if (!visited.Add(node.NodeId)) return;

        // ── LoopStart: hand off to the two-block loop executor ──
        if (node.NodeType == NodeType.Loop)
        {
            await ExecuteLoopStartAsync(node, allNodes, outgoingMap, visited);
            return; // LoopStart handles its own body + LoopEnd output
        }

        // ── LoopEnd: skip when reached via normal traversal (it's handled inside the loop) ──
        if (node.NodeType == NodeType.LoopEnd)
        {
            _context.Logger.Info(node.NodeName, "LoopEnd reached (end of loop body)");
            return;
        }

        _context.CheckCancellation();
        await _context.WaitIfPausedAsync();

        if (!node.Enabled)
        {
            _context.Logger.Info(node.NodeName, "Skipped (disabled)");
        }
        else
        {
            await ExecuteNodeAsync(node);
        }
        _context.CurrentNodeIndex++;

        // Follow all outgoing connections, with port filtering for branch-capable nodes
        if (outgoingMap.TryGetValue(node.NodeId, out var successors))
        {
            // Determine the active port filter
            string? activePort = null;
            if (node.NodeType == NodeType.Condition)
            {
                activePort = _context.Get<string>($"{node.NodeId}_result") ?? "True";
            }
            else if (node.NodeType == NodeType.ColorCal)
            {
                var resultIdx = _context.Get<int>($"{node.NodeId}_result");
                activePort = resultIdx.ToString();
            }
            else if (node.NodeType == NodeType.ColorMotion)
            {
                // DirectionDetect mode uses direction-named ports
                var mode = node.GetParam<string>("MotionMode") ?? "MotionDetect";
                if (mode == "DirectionDetect")
                {
                    activePort = _context.Get<string>($"{node.NodeId}_direction");
                }
                else
                {
                    activePort = _context.Get<string>($"{node.NodeId}_result") ?? "True";
                }
            }

            foreach (var (toId, fromPort, toPort) in successors)
            {
                // Filter by active port for branch-capable nodes
                if (activePort != null && !string.Equals(fromPort, activePort, StringComparison.OrdinalIgnoreCase))
                    continue;

                // If this connection targets a Loop's BreakCond port, set flag instead of traversing
                if (toPort == "BreakCond")
                {
                    var targetLoop = allNodes.FirstOrDefault(n => n.NodeId == toId);
                    if (targetLoop != null && targetLoop.NodeType == NodeType.Loop)
                    {
                        _context.Set($"{toId}_breakCond", true);
                        _context.Logger.Info(node.NodeName, $"Break condition signaled to Loop: {targetLoop.NodeName}");
                    }
                    continue;
                }

                var nextNode = allNodes.FirstOrDefault(n => n.NodeId == toId);
                if (nextNode != null)
                {
                    await TraverseNodeChainAsync(nextNode, allNodes, outgoingMap, visited);
                }
            }
        }
    }

    private async Task ExecuteNodeListAsync(List<FlowNode> nodes)
    {
        foreach (var node in nodes)
        {
            _context.CheckCancellation();
            await _context.WaitIfPausedAsync();

            if (!node.Enabled)
            {
                _context.Logger.Info(node.NodeName, "Skipped (disabled)");
                continue;
            }

            await ExecuteNodeAsync(node);
            _context.CurrentNodeIndex++;
        }
    }

    public async Task ExecuteNodeAsync(FlowNode node)
    {
        _context.Logger.Info(node.NodeName, $"Executing [{node.NodeType}]");

        int retries = 0;
        while (retries <= node.RetryCount)
        {
            try
            {
                // WaitCondition handles its own timeout internally — don't double-guard
                if (node.NodeType == NodeType.WaitCondition)
                {
                    await ExecuteNodeByTypeAsync(node);
                }
                else
                {
                    using var cts = new CancellationTokenSource(node.TimeoutMs);
                    var task = ExecuteNodeByTypeAsync(node);
                    var completed = await Task.WhenAny(task, Task.Delay(node.TimeoutMs, cts.Token));

                    if (completed != task)
                    {
                        throw new TimeoutException($"Node timed out after {node.TimeoutMs}ms");
                    }

                    await task; // Propagate any exceptions
                }
                _context.Logger.Success(node.NodeName, "Completed");
                return;
            }
            catch (OperationCanceledException)
            {
                _context.Logger.Warning(node.NodeName, "Execution cancelled");
                throw;
            }
            catch (Exception ex) when (retries < node.RetryCount)
            {
                retries++;
                _context.Logger.Retry(node.NodeName, retries, node.RetryCount, ex.Message);
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                _context.Logger.Error(node.NodeName, $"Failed after {retries} retries: {ex.Message}");
                throw;
            }
        }
    }

    private async Task ExecuteNodeByTypeAsync(FlowNode node)
    {
        switch (node.NodeType)
        {
            case NodeType.StartProgram:
                await ExecuteStartProgramAsync(node);
                break;
            case NodeType.ClickElement:
                await ExecuteClickElementAsync(node);
                break;
            case NodeType.WaitCondition:
                await ExecuteWaitConditionAsync(node);
                break;
            case NodeType.KeyPress:
                await ExecuteKeyPressAsync(node);
                break;
            case NodeType.Loop:
                // LoopStart is handled by TraverseNodeChainAsync → ExecuteLoopStartAsync
                // If reached here (fallback / legacy), execute directly
                await ExecuteLoopStartLegacyAsync(node);
                break;
            case NodeType.LoopEnd:
                // LoopEnd is a no-op marker; actual loop logic lives in ExecuteLoopStartAsync
                _context.Logger.Info(node.NodeName, "LoopEnd marker");
                await Task.CompletedTask;
                break;
            case NodeType.Condition:
                await ExecuteConditionAsync(node);
                break;
            case NodeType.Gate:
                await ExecuteGateAsync(node);
                break;
            case NodeType.ColorMotion:
                await ExecuteColorMotionAsync(node);
                break;
            case NodeType.ColorCal:
                await ExecuteColorCalAsync(node);
                break;
            case NodeType.Break:
                await ExecuteBreakAsync(node);
                break;
            default:
                throw new NotSupportedException($"Unknown node type: {node.NodeType}");
        }
    }

    // ============ StartProgram ============

    private async Task ExecuteStartProgramAsync(FlowNode node)
    {
        var filePath = node.GetParam<string>("FilePath") ?? "";
        var workingDir = node.GetParam<string>("WorkingDirectory") ?? "";
        var arguments = node.GetParam<string>("Arguments") ?? "";
        var runAsAdmin = node.GetParam<bool?>("RunAsAdmin") ?? false;
        var waitForWindowMs = node.GetParam<int?>("WaitForWindowMs") ?? 5000;
        var windowKeyword = node.GetParam<string>("WindowTitleKeyword") ?? "";

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException($"Executable not found: {filePath}");

        var psi = new ProcessStartInfo
        {
            FileName = filePath,
            WorkingDirectory = string.IsNullOrEmpty(workingDir) ? Path.GetDirectoryName(filePath) ?? "" : workingDir,
            Arguments = arguments,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        };

        if (runAsAdmin)
            psi.Verb = "runas";

        _context.Logger.Info(node.NodeName, $"Starting: {filePath}");
        Process.Start(psi);

        if (!string.IsNullOrEmpty(windowKeyword) && waitForWindowMs > 0)
        {
            _context.Logger.Info(node.NodeName, $"Waiting for window: \"{windowKeyword}\" ({waitForWindowMs}ms)");
            var hWnd = WindowHelper.WaitForWindow(windowKeyword, waitForWindowMs);
            if (hWnd != IntPtr.Zero)
            {
                _context.SetHwnd(hWnd);
                _context.Logger.Success(node.NodeName, $"Window found: 0x{hWnd:X}");
            }
            else
            {
                _context.Logger.Warning(node.NodeName, $"Window \"{windowKeyword}\" not found within timeout");
            }
        }

        await Task.CompletedTask;
    }

    // ============ ClickElement ============

    private async Task ExecuteClickElementAsync(FlowNode node)
    {
        var targetWindow = node.GetParam<string>("TargetWindow") ?? "";
        var locateMode = node.GetParam<string>("LocateMode") ?? "Coordinate";

        // Build region from parameters
        var region = node.GetParam<Region>("Region") ?? new Region { X = 0, Y = 0, Width = 100, Height = 100 };
        var templatePath = node.GetParam<string>("TemplateImagePath") ?? "";
        var threshold = node.GetParam<double?>("TemplateMatchThreshold") ?? 0.8;
        // Pre/Post delay: prefer node-specified, else global variables, else defaults
        var nodePre = node.GetParam<int?>("PreDelayMs");
        var nodePost = node.GetParam<int?>("PostDelayMs");
        var globalPre = _context.Get<int?>("GlobalPreDelayMs");
        var globalPost = _context.Get<int?>("GlobalPostDelayMs");
        var preDelayMs = nodePre ?? globalPre ?? 500; // default 500ms
        var postDelayMs = nodePost ?? globalPost ?? 500; // default 500ms

        // Scale range
        double minScale = 0.5, maxScale = 1.5, step = 0.1;
        var scaleRange = node.GetParam<TemplateScaleRange>("TemplateScaleRange");
        if (scaleRange != null)
        {
            minScale = scaleRange.Min;
            maxScale = scaleRange.Max;
            step = scaleRange.Step;
        }

        // Get window handle
        var hWnd = _context.CurrentHwnd;
        if (hWnd == IntPtr.Zero && !string.IsNullOrEmpty(targetWindow))
        {
            hWnd = WindowHelper.FindWindowByTitle(targetWindow);
            if (hWnd != IntPtr.Zero)
                _context.SetHwnd(hWnd);
        }

        if (hWnd == IntPtr.Zero)
            throw new InvalidOperationException("No target window found");

        // Activate window
        WindowHelper.ActivateWindow(hWnd);
        await Task.Delay(200);

        var (clientLeft, clientTop, clientWidth, clientHeight) = WindowHelper.GetClientBounds(hWnd);
        // Full window only applies to TemplateMatch and OCR; Coordinate always uses explicit Region
        var useFullWindow = (locateMode != "Coordinate") && (node.GetParam<bool?>("UseFullScreen") ?? true);

        int absoluteX, absoluteY;

        // If full window, override region to span entire client area
        int captureX = useFullWindow ? 0 : region.X;
        int captureY = useFullWindow ? 0 : region.Y;
        int captureW = useFullWindow ? clientWidth : region.Width;
        int captureH = useFullWindow ? clientHeight : region.Height;

        switch (locateMode)
        {
            case "Coordinate":
                absoluteX = clientLeft + captureX + captureW / 2;
                absoluteY = clientTop + captureY + captureH / 2;
                _context.Logger.Info(node.NodeName, $"Coordinates: ({absoluteX}, {absoluteY})");
                break;

            case "TemplateMatch":
                if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                    throw new FileNotFoundException($"Template image not found: {templatePath}");

                // Capture region (or full screen)
                {
                    using var screenBmp = ScreenCapture.CaptureWindowRegion(hWnd, captureX, captureY, captureW, captureH);
                    if (screenBmp == null)
                        throw new InvalidOperationException("Failed to capture screen region");

                    // Load template
                    using var templateMat = ImageRecognition.LoadTemplate(templatePath);

                    // Multi-scale matching
                    var result = ImageRecognition.FindTemplate(screenBmp, templateMat, minScale, maxScale, step, threshold);

                    if (result == null)
                    {
                        // Debug: save the captured screenshot for diagnosis
                        var debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_match");
                        var debugFile = Path.Combine(debugDir,
                            $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{node.NodeName}.png");
                        try
                        {
                            if (!Directory.Exists(debugDir)) Directory.CreateDirectory(debugDir);
                            screenBmp.Save(debugFile, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        catch { /* ignore */ }

                        var msg = $"Template not matched (threshold: {threshold}). " +
                                  $"Template: {Path.GetFileName(templatePath)} ({templateMat.Cols}×{templateMat.Rows}). " +
                                  $"Captured region: ({captureX},{captureY}) {captureW}×{captureH}. " +
                                  $"Debug screenshot saved to: {debugFile}";
                        throw new InvalidOperationException(msg);
                    }

                    absoluteX = clientLeft + captureX + result.Value.point.X;
                    absoluteY = clientTop + captureY + result.Value.point.Y;
                    _context.Logger.Info(node.NodeName, $"Template matched at ({absoluteX}, {absoluteY}), confidence: {result.Value.confidence:F3}");
                }
                break;

            case "OCR":
                {
                    var ocrText = node.GetParam<string>("OCRText") ?? "";
                    if (string.IsNullOrEmpty(ocrText))
                        throw new ArgumentException("OCRText is required for OCR locate mode");

                    using var screenBmp = ScreenCapture.CaptureWindowRegion(hWnd, captureX, captureY, captureW, captureH);
                    if (screenBmp == null)
                        throw new InvalidOperationException("Failed to capture screen region");

                    // Debug: save the captured screenshot for diagnosis
                    var debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_ocr");
                    var debugFile = Path.Combine(debugDir,
                        $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{node.NodeName}.png");

                    string? allOcrText = null;
                    var ocrResult = await Task.Run(() =>
                        OcrHelper.FindText(screenBmp, ocrText, out allOcrText, debugFile));

                    _context.Logger.Info(node.NodeName,
                        $"OCR lang: {OcrHelper.ActiveLanguage}, recognized text: [{allOcrText}]");

                    if (ocrResult == null)
                    {
                        var msg = $"Text \"{ocrText}\" not found via OCR. " +
                                  $"Language: {OcrHelper.ActiveLanguage}. " +
                                  $"Recognized: [{allOcrText}]. " +
                                  $"Debug screenshot saved to: {debugFile}. " +
                                  $"Captured region: ({captureX},{captureY}) {captureW}×{captureH} " +
                                  $"from window client ({clientWidth}×{clientHeight}). " +
                                  $"Tip: For game UI, prefer TemplateMatch over OCR.";
                        throw new InvalidOperationException(msg);
                    }

                    absoluteX = clientLeft + captureX + ocrResult.Value.X;
                    absoluteY = clientTop + captureY + ocrResult.Value.Y;
                    _context.Logger.Info(node.NodeName, $"OCR found \"{ocrText}\" at ({absoluteX}, {absoluteY})");
                }
                break;

            case "HSVClick":
                {
                    using var screenBmp = ScreenCapture.CaptureWindowRegion(hWnd, captureX, captureY, captureW, captureH);
                    if (screenBmp == null)
                        throw new InvalidOperationException("Failed to capture screen region");

                    var targetColor = node.ResolveTargetRgb();
                    var hueTol = node.GetParam<int?>("HueTolerance") ?? 8;
                    var svTol = node.GetParam<int?>("SVTolerance") ?? 30;
                    var center = ImageRecognition.DetectColorCenter(screenBmp, targetColor, hueTol, svTol);

                    if (center == null)
                    {
                        var debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_hsv");
                        var ts = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{node.NodeName}";
                        var debugFile = Path.Combine(debugDir, $"{ts}.png");
                        var filteredFile = Path.Combine(debugDir, $"{ts}_hsvfiltered.png");
                        try
                        {
                            if (!Directory.Exists(debugDir)) Directory.CreateDirectory(debugDir);
                            screenBmp.Save(debugFile, System.Drawing.Imaging.ImageFormat.Png);
                            using var filtered = ImageRecognition.ApplyHsvFilter(screenBmp, targetColor, hueTol, svTol);
                            filtered.Save(filteredFile, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        catch { /* ignore */ }
                        throw new InvalidOperationException(
                            $"Target color not found. RGB={targetColor.R},{targetColor.G},{targetColor.B}, " +
                            $"HueTol={hueTol}, SVTol={svTol}. Debug: {debugFile}, Filtered: {filteredFile}");
                    }

                    absoluteX = clientLeft + captureX + center.Value.X;
                    absoluteY = clientTop + captureY + center.Value.Y;
                    _context.Logger.Info(node.NodeName, $"HSV color center at ({absoluteX}, {absoluteY})");
                }
                break;

            case "HSVTemplateMatch":
                {
                    var refPath = node.GetParam<string>("ReferenceImagePath") ?? "";
                    if (string.IsNullOrEmpty(refPath) || !File.Exists(refPath))
                        throw new FileNotFoundException($"Reference image not found: {refPath}");

                    using var screenBmp = ScreenCapture.CaptureWindowRegion(hWnd, captureX, captureY, captureW, captureH);
                    if (screenBmp == null)
                        throw new InvalidOperationException("Failed to capture screen region");

                    using var templateMat = ImageRecognition.LoadTemplate(refPath);
                    var targetColor = node.ResolveTargetRgb();
                    var hueTol = node.GetParam<int?>("HueTolerance") ?? 8;
                    var svTol = node.GetParam<int?>("SVTolerance") ?? 30;
                    var tplThreshold = node.GetParam<double?>("TemplateMatchThreshold") ?? 0.8;

                    var result = ImageRecognition.FindTemplateWithColorFilter(
                        screenBmp, templateMat, targetColor, hueTol, svTol, tplThreshold);

                    if (result == null)
                    {
                        var debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_hsv_tpl");
                        var ts = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{node.NodeName}";
                        var debugFile = Path.Combine(debugDir, $"{ts}.png");
                        var filteredFile = Path.Combine(debugDir, $"{ts}_hsvfiltered.png");
                        try
                        {
                            if (!Directory.Exists(debugDir)) Directory.CreateDirectory(debugDir);
                            screenBmp.Save(debugFile, System.Drawing.Imaging.ImageFormat.Png);
                            using var filtered = ImageRecognition.ApplyHsvFilter(screenBmp, targetColor, hueTol, svTol);
                            filtered.Save(filteredFile, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        catch { /* ignore */ }
                        throw new InvalidOperationException(
                            $"HSV+TemplateMatch failed. Ref: {Path.GetFileName(refPath)}. " +
                            $"Color RGB={targetColor.R},{targetColor.G},{targetColor.B}. Debug: {debugFile}, Filtered: {filteredFile}");
                    }

                    absoluteX = clientLeft + captureX + result.Value.point.X;
                    absoluteY = clientTop + captureY + result.Value.point.Y;
                    _context.Logger.Info(node.NodeName,
                        $"HSV+Template matched at ({absoluteX}, {absoluteY}), confidence: {result.Value.confidence:F3}");
                }
                break;

            default:
                throw new NotSupportedException($"Unknown locate mode: {locateMode}");
        }

        // Perform click
        InputSimulator.MoveAndClick(absoluteX, absoluteY, preDelayMs, postDelayMs);
    }

    private async Task ExecuteWaitConditionAsync(FlowNode node)
    {
        var targetWindow = node.GetParam<string>("TargetWindow") ?? "";
        var conditionType = node.GetParam<string>("ConditionType") ?? "ImageAppear";
        var checkIntervalMs = node.GetParam<int?>("CheckIntervalMs") ?? 500;
        var timeoutMs = node.TimeoutMs;

        var region = node.GetParam<Region>("Region") ?? new Region { X = 0, Y = 0, Width = 100, Height = 100 };
        var templatePath = node.GetParam<string>("TemplateImagePath") ?? "";
        var threshold = node.GetParam<double?>("TemplateMatchThreshold") ?? 0.8;

        // Get window handle
        var hWnd = _context.CurrentHwnd;
        if (hWnd == IntPtr.Zero && !string.IsNullOrEmpty(targetWindow))
        {
            hWnd = WindowHelper.FindWindowByTitle(targetWindow);
            if (hWnd != IntPtr.Zero)
                _context.SetHwnd(hWnd);
        }

        if (conditionType == "WindowExist" && !string.IsNullOrEmpty(targetWindow))
        {
            _context.Logger.Info(node.NodeName, $"Waiting for window: \"{targetWindow}\" ({timeoutMs}ms)");
            var found = WindowHelper.WaitForWindow(targetWindow, timeoutMs, checkIntervalMs);
            if (found != IntPtr.Zero)
            {
                _context.SetHwnd(found);
                _context.Logger.Success(node.NodeName, "Window found");
            }
            else
                throw new TimeoutException($"Window \"{targetWindow}\" not found within {timeoutMs}ms");
            return;
        }

        if (conditionType == "Timeout")
        {
            var waitMs = node.GetParam<int?>("WaitMs") ?? node.TimeoutMs;
            _context.Logger.Info(node.NodeName, $"Waiting {waitMs}ms...");
            await Task.Delay(waitMs);
            return;
        }

        if (conditionType == "OCRContain")
        {
            var ocrText = node.GetParam<string>("OCRText") ?? "";
            if (string.IsNullOrEmpty(ocrText))
                throw new ArgumentException("OCRText is required for OCRContain condition");

            if (hWnd == IntPtr.Zero)
                throw new InvalidOperationException("No target window available for OCR");

            var useFullWindow = node.GetParam<bool?>("UseFullScreen") ?? true;
            var (cl, ct, cw, ch) = WindowHelper.GetClientBounds(hWnd);
            int ocrCapX = useFullWindow ? 0 : region.X;
            int ocrCapY = useFullWindow ? 0 : region.Y;
            int ocrCapW = useFullWindow ? cw : region.Width;
            int ocrCapH = useFullWindow ? ch : region.Height;

            _context.Logger.Info(node.NodeName, $"Waiting for OCR text \"{ocrText}\" ({timeoutMs}ms)... " +
                $"(OCR lang: {OcrHelper.ActiveLanguage})");

            var ocrSw = Stopwatch.StartNew();
            string? lastRecognized = null;
            while (ocrSw.ElapsedMilliseconds < timeoutMs)
            {
                _context.CheckCancellation();
                await _context.WaitIfPausedAsync();

                using var ocrBmp = ScreenCapture.CaptureWindowRegion(hWnd, ocrCapX, ocrCapY, ocrCapW, ocrCapH);
                if (ocrBmp != null)
                {
                    string? waitOcrText = null;
                    var ocrResult = await Task.Run(() =>
                        OcrHelper.FindText(ocrBmp, ocrText, out waitOcrText, null));
                    lastRecognized = waitOcrText;
                    if (ocrResult != null)
                    {
                        _context.Logger.Success(node.NodeName, $"OCR found \"{ocrText}\" after {ocrSw.ElapsedMilliseconds}ms");
                        return;
                    }
                }
                await Task.Delay(checkIntervalMs);
            }
            throw new TimeoutException(
                $"OCR text \"{ocrText}\" not found within {timeoutMs}ms. " +
                $"OCR lang: {OcrHelper.ActiveLanguage}. " +
                $"Last recognized: [{lastRecognized}]. " +
                $"Tip: For game UI, prefer ImageAppear with TemplateMatch over OCRContain.");
        }

        if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
            throw new FileNotFoundException($"Template image not found: {templatePath}");

        using var templateMat = ImageRecognition.LoadTemplate(templatePath);

        // Read scale range for multi-scale matching (same as ClickElement)
        double minScale = 0.5, maxScale = 1.5, scaleStep = 0.1;
        var scaleRange = node.GetParam<TemplateScaleRange>("TemplateScaleRange");
        if (scaleRange != null)
        {
            minScale = scaleRange.Min;
            maxScale = scaleRange.Max;
            scaleStep = scaleRange.Step;
        }

        var sw = Stopwatch.StartNew();
        var useFullScreen = node.GetParam<bool?>("UseFullScreen") ?? true;
        var (clientLeft2, clientTop2, clientW2, clientH2) = WindowHelper.GetClientBounds(hWnd);
        int capX = useFullScreen ? 0 : region.X;
        int capY = useFullScreen ? 0 : region.Y;
        int capW = useFullScreen ? clientW2 : region.Width;
        int capH = useFullScreen ? clientH2 : region.Height;

        int pollCount = 0;
        int logInterval = Math.Max(1, 2000 / Math.Max(1, checkIntervalMs)); // log every ~2 seconds

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            _context.CheckCancellation();
            await _context.WaitIfPausedAsync();

            if (hWnd == IntPtr.Zero) break;

            using var screenBmp = ScreenCapture.CaptureWindowRegion(hWnd, capX, capY, capW, capH);
            if (screenBmp == null)
            {
                await Task.Delay(checkIntervalMs);
                continue;
            }

            var result = ImageRecognition.FindTemplate(screenBmp, templateMat, minScale, maxScale, scaleStep, threshold);

            bool conditionMet = conditionType switch
            {
                "ImageAppear" => result != null,
                "ImageDisappear" => result == null,
                _ => false
            };

            if (conditionMet)
            {
                _context.Logger.Success(node.NodeName, $"Condition met after {sw.ElapsedMilliseconds}ms" +
                    (result != null ? $", confidence: {result.Value.confidence:F3}" : ""));
                return;
            }

            // Periodic feedback so user knows it's still polling
            pollCount++;
            if (pollCount % logInterval == 0)
            {
                _context.Logger.Info(node.NodeName,
                    $"Still waiting... ({sw.ElapsedMilliseconds}/{timeoutMs}ms, " +
                    $"best confidence: {(result?.confidence ?? 0):F3})");
            }

            await Task.Delay(checkIntervalMs);
        }

        throw new TimeoutException($"Condition not met within {timeoutMs}ms");
    }

    // ============ KeyPress ============

    private async Task ExecuteKeyPressAsync(FlowNode node)
    {
        var targetWindow = node.GetParam<string>("TargetWindow") ?? "";
        var keyName = node.GetParam<string>("KeyName") ?? "";
        var pressMode = node.GetParam<string>("PressMode") ?? "Press";
        var holdDurationMs = node.GetParam<int?>("HoldDurationMs") ?? 500;

        byte scanCode;
        if (node.Parameters.TryGetValue("KeyScanCode", out var scObj) && scObj != null)
        {
            try
            {
                // JsonElement doesn't implement IConvertible — extract raw value
                if (scObj is System.Text.Json.JsonElement je)
                    scanCode = je.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? je.Deserialize<byte>() : Convert.ToByte(je.GetRawText());
                else
                    scanCode = Convert.ToByte(scObj);
            }
            catch
            {
                _context.Logger.Warning(node.NodeName,
                    $"KeyScanCode corrupted ({scObj}), falling back to KeyName: {keyName}");
                if (!string.IsNullOrEmpty(keyName))
                    scanCode = InputSimulator.GetScanCode(keyName);
                else
                    throw new ArgumentException($"KeyScanCode corrupted and no KeyName: {scObj}");
            }
        }
        else if (!string.IsNullOrEmpty(keyName))
        {
            scanCode = InputSimulator.GetScanCode(keyName);
        }
        else
        {
            throw new ArgumentException("No scan code or key name provided");
        }

        // Activate target window if specified
        if (!string.IsNullOrEmpty(targetWindow))
        {
            var hWnd = WindowHelper.FindWindowByTitle(targetWindow);
            if (hWnd != IntPtr.Zero)
            {
                WindowHelper.ActivateWindow(hWnd);
                await Task.Delay(200);
            }
        }
        else if (_context.CurrentHwnd != IntPtr.Zero)
        {
            WindowHelper.ActivateWindow(_context.CurrentHwnd);
            await Task.Delay(200);
        }

        _context.Logger.Info(node.NodeName, $"Key: {keyName} (scan: 0x{scanCode:X2}), Mode: {pressMode}");

        switch (pressMode)
        {
            case "Press":
                InputSimulator.PressKey(scanCode);
                break;
            case "Hold":
                InputSimulator.HoldKey(scanCode, holdDurationMs);
                break;
            case "Release":
                InputSimulator.KeyUp(scanCode);
                break;
        }

        await Task.CompletedTask;
    }

    // ==================== LoopStart / LoopEnd (Two-Block Loop System) ====================

    /// <summary>
    /// Execute a LoopStart node using the two-block loop system.
    /// LoopStart marks the beginning, LoopEnd marks the end.
    /// Body nodes between them (via connections) are executed repeatedly.
    /// After the loop finishes, execution continues from LoopEnd's output.
    /// </summary>
    private async Task ExecuteLoopStartAsync(
        FlowNode loopStart, List<FlowNode> allNodes,
        Dictionary<string, List<(string ToId, string FromPort, string ToPort)>> outgoingMap,
        HashSet<string> visited)
    {
        var loopMode = loopStart.GetParam<string>("LoopMode") ?? "FixedCount";
        var loopCount = loopStart.GetParam<int?>("LoopCount") ?? 1;

        // Find the paired LoopEnd
        var loopEnd = allNodes.FirstOrDefault(n =>
            n.NodeType == NodeType.LoopEnd &&
            n.PairedLoopStartId == loopStart.NodeId);

        if (loopEnd == null)
        {
            // Fallback: try to locate LoopEnd by traversing connections from LoopStart's output
            loopEnd = FindLoopEndByTraversal(loopStart, allNodes, outgoingMap);
        }

        if (loopEnd == null)
        {
            _context.Logger.Warning(loopStart.NodeName,
                "No paired LoopEnd found. Falling back to legacy single-Loop execution.");
            await ExecuteLoopStartLegacyAsync(loopStart);
            return;
        }

        _context.Logger.Info(loopStart.NodeName,
            $"Loop started ({loopMode}" +
            (loopMode == "FixedCount" ? $", count: {loopCount}" : ", BreakCondition") +
            $") — paired with LoopEnd [{loopEnd.NodeName}]");

        // Set loop state in context
        _context.Set($"{loopStart.NodeId}_loop_active", true);
        _context.Set($"{loopStart.NodeId}_breakCond", false);
        _context.Set($"{loopStart.NodeId}_break", false);

        int iteration = 0;
        bool breakRequested = false;

        while (!breakRequested)
        {
            _context.CheckCancellation();
            await _context.WaitIfPausedAsync();

            // ── FixedCount: check BEFORE executing to avoid off-by-one edge cases ──
            if (loopMode == "FixedCount" && loopCount > 0 && iteration >= loopCount)
            {
                _context.Logger.Success(loopStart.NodeName, $"Loop completed ({iteration}/{loopCount} iterations)");
                break;
            }

            // Reset break condition flag each iteration
            _context.Set($"{loopStart.NodeId}_breakCond", false);

            iteration++;
            _context.Logger.Info(loopStart.NodeName, $"Loop iteration {iteration}");

            try
            {
                // Execute one full pass of the loop body using connection traversal
                var iterVisited = new HashSet<string> { loopStart.NodeId };
                await TraverseLoopBodyAsync(loopStart, loopEnd, allNodes, outgoingMap, iterVisited);
            }
            catch (Exception ex)
            {
                _context.Logger.Warning(loopStart.NodeName, $"Loop iteration {iteration} failed: {ex.Message}");
                breakRequested = true;
                break;
            }

            // Check break sources
            if (_context.Get<bool?>($"{loopStart.NodeId}_break") == true)
            {
                breakRequested = true;
                _context.Logger.Info(loopStart.NodeName, $"Break signal received, exiting loop after {iteration} iteration(s)");
                break;
            }

            if (_context.Get<bool?>($"{loopStart.NodeId}_breakCond") == true)
            {
                breakRequested = true;
                _context.Logger.Info(loopStart.NodeName, $"BreakCondition met, exiting loop after {iteration} iteration(s)");
                break;
            }
        }

        // Clean up loop state
        _context.Set($"{loopStart.NodeId}_loop_active", false);
        _context.Set($"{loopStart.NodeId}_break", false);
        _context.Set($"{loopStart.NodeId}_breakCond", false);

        // ── Mark ALL body nodes visited so they are NEVER re-executed after the loop ──
        CollectBodyNodeIds(loopStart, loopEnd, allNodes, outgoingMap, visited);
        visited.Add(loopEnd.NodeId);

        // After loop, follow LoopEnd's output connections
        if (outgoingMap.TryGetValue(loopEnd.NodeId, out var endSucc))
        {
            foreach (var (toId, _, toPort) in endSucc)
            {
                if (toPort == "BreakCond") continue;
                var nextNode = allNodes.FirstOrDefault(n => n.NodeId == toId);
                if (nextNode != null)
                    await TraverseNodeChainAsync(nextNode, allNodes, outgoingMap, visited);
            }
        }
    }

    /// <summary>
    /// Collect all node IDs in the loop body (between LoopStart and LoopEnd via connections)
    /// and add them to the visited set so they are never executed after the loop finishes.
    /// </summary>
    private static void CollectBodyNodeIds(
        FlowNode loopStart, FlowNode loopEnd, List<FlowNode> allNodes,
        Dictionary<string, List<(string ToId, string FromPort, string ToPort)>> outgoingMap,
        HashSet<string> visited)
    {
        if (!outgoingMap.TryGetValue(loopStart.NodeId, out var succs)) return;
        var queue = new Queue<string>();
        foreach (var (toId, _, _) in succs)
            if (toId != loopEnd.NodeId) queue.Enqueue(toId);

        var seen = new HashSet<string> { loopStart.NodeId, loopEnd.NodeId };
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!seen.Add(id)) continue;
            visited.Add(id);

            if (outgoingMap.TryGetValue(id, out var next))
                foreach (var (nid, _, _) in next)
                    if (!seen.Contains(nid) && nid != loopEnd.NodeId)
                        queue.Enqueue(nid);
        }
    }

    /// <summary>
    /// Execute one iteration of the loop body. Traverses from LoopStart's output
    /// connections through the body until reaching LoopEnd (or all paths exhausted).
    /// Uses a per-iteration visited set so nodes can be re-executed each iteration.
    /// </summary>
    private async Task TraverseLoopBodyAsync(
        FlowNode loopStart, FlowNode loopEnd,
        List<FlowNode> allNodes,
        Dictionary<string, List<(string ToId, string FromPort, string ToPort)>> outgoingMap,
        HashSet<string> iterVisited)
    {
        if (!outgoingMap.TryGetValue(loopStart.NodeId, out var startSucc))
            return;

        foreach (var (toId, _, toPort) in startSucc)
        {
            if (toId == loopEnd.NodeId) continue;          // Skip direct LoopStart→LoopEnd
            if (toPort == "BreakCond") continue;           // BreakCond is handled in TraverseNodeChainAsync

            var nextNode = allNodes.FirstOrDefault(n => n.NodeId == toId);
            if (nextNode != null)
                await TraverseLoopBodyNodeAsync(loopStart, nextNode, loopEnd, allNodes, outgoingMap, iterVisited);
        }
    }

    /// <summary>
    /// Traverse a single node within the loop body. Stops at LoopEnd.
    /// BreakCond connections signal the loop to break.
    /// Uses iterVisited to avoid infinite recursion within one iteration
    /// (but nodes are re-visited across iterations since iterVisited is fresh each time).
    /// </summary>
    private async Task TraverseLoopBodyNodeAsync(
        FlowNode loopStart, FlowNode node, FlowNode loopEnd,
        List<FlowNode> allNodes,
        Dictionary<string, List<(string ToId, string FromPort, string ToPort)>> outgoingMap,
        HashSet<string> iterVisited)
    {
        if (node.NodeId == loopEnd.NodeId) return;
        if (!iterVisited.Add(node.NodeId)) return; // Already visited in this iteration

        // Nested LoopStart? Delegate to the two-block executor
        if (node.NodeType == NodeType.Loop)
        {
            // Use a temporary outer visited-like set so the nested loop can track its own state
            var nestedVisited = new HashSet<string>();
            await ExecuteLoopStartAsync(node, allNodes, outgoingMap, nestedVisited);
            return;
        }

        _context.CheckCancellation();
        await _context.WaitIfPausedAsync();

        if (node.Enabled)
            await ExecuteNodeAsync(node);
        else
            _context.Logger.Info(node.NodeName, "Skipped (disabled)");

        // ── Port filtering for branch-capable nodes inside loop body ──
        // ColorCal / Condition / ColorMotion route to specific output ports based on result;
        // skip edges from non-matching ports to avoid executing all branch nodes.
        string? bodyActivePort = null;
        if (node.NodeType == NodeType.Condition)
        {
            bodyActivePort = _context.Get<string>($"{node.NodeId}_result") ?? "True";
        }
        else if (node.NodeType == NodeType.ColorCal)
        {
            var resultIdx = _context.Get<int>($"{node.NodeId}_result");
            bodyActivePort = resultIdx.ToString();
        }
        else if (node.NodeType == NodeType.ColorMotion)
        {
            var mm = node.GetParam<string>("MotionMode") ?? "MotionDetect";
            if (mm == "DirectionDetect")
                bodyActivePort = _context.Get<string>($"{node.NodeId}_direction");
            else
                bodyActivePort = _context.Get<string>($"{node.NodeId}_result") ?? "True";
        }

        if (!outgoingMap.TryGetValue(node.NodeId, out var succs)) return;

        foreach (var (toId, fromPort, toPort) in succs)
        {
            // Filter by active port for branch-capable nodes
            if (bodyActivePort != null && !string.Equals(fromPort, bodyActivePort, StringComparison.OrdinalIgnoreCase))
                continue;
            if (toId == loopEnd.NodeId) continue;     // Reached the end of the loop body

            // BreakCond — signal the enclosing LoopStart
            if (toPort == "BreakCond")
            {
                _context.Set($"{loopStart.NodeId}_breakCond", true);
                _context.Logger.Info(node.NodeName, $"Break condition signaled to Loop: {loopStart.NodeName}");
                continue;
            }

            var nextNode = allNodes.FirstOrDefault(n => n.NodeId == toId);
            if (nextNode != null)
                await TraverseLoopBodyNodeAsync(loopStart, nextNode, loopEnd, allNodes, outgoingMap, iterVisited);
        }
    }

    /// <summary>
    /// Find the LoopEnd node paired with a LoopStart by traversing connections.
    /// The first LoopEnd encountered in the output chain is the paired one.
    /// </summary>
    private FlowNode? FindLoopEndByTraversal(
        FlowNode loopStart, List<FlowNode> allNodes,
        Dictionary<string, List<(string ToId, string FromPort, string ToPort)>> outgoingMap)
    {
        if (!outgoingMap.TryGetValue(loopStart.NodeId, out var startSucc))
            return null;

        var searchVisited = new HashSet<string> { loopStart.NodeId };
        var queue = new Queue<string>();

        foreach (var (toId, _, _) in startSucc)
            queue.Enqueue(toId);

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            if (!searchVisited.Add(nodeId)) continue;

            var node = allNodes.FirstOrDefault(n => n.NodeId == nodeId);
            if (node == null) continue;

            if (node.NodeType == NodeType.LoopEnd)
                return node;

            if (outgoingMap.TryGetValue(nodeId, out var succs))
                foreach (var (nextId, _, _) in succs)
                    queue.Enqueue(nextId);
        }

        return null;
    }

    /// <summary>
    /// Legacy fallback: execute Loop node when no paired LoopEnd is found
    /// (e.g. old-format flow files, or standalone Loop blocks).
    /// </summary>
    private async Task ExecuteLoopStartLegacyAsync(FlowNode node)
    {
        var loopMode = node.GetParam<string>("LoopMode") ?? "FixedCount";
        var loopCount = node.GetParam<int?>("LoopCount") ?? 3;

        _context.Logger.Info(node.NodeName, $"Loop (legacy mode) — {loopMode}, count={loopCount}");

        _context.Set($"{node.NodeId}_loop_active", true);

        if (loopMode == "BreakCondition")
        {
            int iter = 0;
            while (_context.Get<bool?>($"{node.NodeId}_breakCond") != true &&
                   _context.Get<bool?>($"{node.NodeId}_break") != true)
            {
                _context.CheckCancellation();
                await _context.WaitIfPausedAsync();
                iter++;
                _context.Set($"{node.NodeId}_breakCond", false);
                if (node.Children != null && node.Children.Count > 0)
                    await ExecuteNodeListAsync(node.Children);
            }
            _context.Logger.Success(node.NodeName, $"Loop exited via break ({iter} iterations)");
        }
        else
        {
            int i;
            for (i = 0; (loopCount == 0 || i < loopCount); i++)
            {
                _context.CheckCancellation();
                await _context.WaitIfPausedAsync();
                if (_context.Get<bool?>($"{node.NodeId}_break") == true) break;
                if (node.Children != null && node.Children.Count > 0)
                    await ExecuteNodeListAsync(node.Children);
            }
            _context.Logger.Success(node.NodeName, $"Loop completed ({i} iterations)");
        }

        _context.Set($"{node.NodeId}_loop_active", false);
        _context.Set($"{node.NodeId}_break", false);
        _context.Set($"{node.NodeId}_breakCond", false);
    }

    // ============ Condition (ImageAppear + OCRContain only) ============

    private async Task ExecuteConditionAsync(FlowNode node)
    {
        var conditionType = node.GetParam<string>("ConditionType") ?? "ImageAppear";
        var targetWindow = node.GetParam<string>("TargetWindow") ?? "";
        var region = node.GetParam<Region>("Region") ?? new Region { X = 0, Y = 0, Width = 100, Height = 100 };

        var hWnd = _context.CurrentHwnd;
        if (hWnd == IntPtr.Zero && !string.IsNullOrEmpty(targetWindow))
            hWnd = WindowHelper.FindWindowByTitle(targetWindow);

        var useFullScreen = node.GetParam<bool?>("UseFullScreen") ?? true;
        var (clientWb, clientHb) = (0, 0);
        int condX = region.X, condY = region.Y, condW = region.Width, condH = region.Height;
        if (hWnd != IntPtr.Zero)
        {
            var bounds = WindowHelper.GetClientBounds(hWnd);
            clientWb = bounds.Width;
            clientHb = bounds.Height;
            if (useFullScreen)
            {
                condX = 0;
                condY = 0;
                condW = clientWb;
                condH = clientHb;
            }
        }

        bool conditionResult = false;

        if (conditionType == "ImageAppear" && hWnd != IntPtr.Zero)
        {
            var templatePath = node.GetParam<string>("TemplateImagePath") ?? "";
            var threshold = node.GetParam<double?>("TemplateMatchThreshold") ?? 0.8;

            if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                throw new FileNotFoundException($"Template image not found: {templatePath}");

            _context.Logger.Info(node.NodeName, $"Checking ImageAppear: {Path.GetFileName(templatePath)}");

            using var templateMat = ImageRecognition.LoadTemplate(templatePath);
            using var screenBmp = ScreenCapture.CaptureWindowRegion(hWnd, condX, condY, condW, condH);
            if (screenBmp != null)
            {
                // Multi-scale template matching
                double minScale = 0.5, maxScale = 1.5, scaleStep = 0.1;
                var scaleRange = node.GetParam<TemplateScaleRange>("TemplateScaleRange");
                if (scaleRange != null)
                {
                    minScale = scaleRange.Min;
                    maxScale = scaleRange.Max;
                    scaleStep = scaleRange.Step;
                }

                var result = ImageRecognition.FindTemplate(screenBmp, templateMat, minScale, maxScale, scaleStep, threshold);
                conditionResult = result != null;

                if (result != null)
                    _context.Logger.Success(node.NodeName, $"Image found, confidence: {result.Value.confidence:F3}");
                else
                    _context.Logger.Info(node.NodeName, $"Image not found (threshold: {threshold})");
            }
        }
        else if (conditionType == "OCRContain" && hWnd != IntPtr.Zero)
        {
            var ocrText = node.GetParam<string>("OCRText") ?? "";
            if (string.IsNullOrEmpty(ocrText))
                throw new ArgumentException("OCRText is required for OCRContain condition");

            _context.Logger.Info(node.NodeName, $"Checking OCR for: \"{ocrText}\"");
            using var ocrBmp = ScreenCapture.CaptureWindowRegion(hWnd, condX, condY, condW, condH);
            if (ocrBmp != null)
            {
                string? allText = null;
                var ocrResult = await Task.Run(() =>
                    OcrHelper.FindText(ocrBmp, ocrText, out allText, null));
                conditionResult = ocrResult != null;
                _context.Logger.Info(node.NodeName, $"OCR result: found={conditionResult}, text=[{allText}]");
            }
        }

        var branchName = conditionResult ? "True" : "False";
        _context.Logger.Info(node.NodeName, $"Condition: {conditionResult}, taking {branchName} branch");
        _context.Set($"{node.NodeId}_result", branchName);
        // Branch routing now handled by TraverseNodeChainAsync via port filtering
    }

    // ============ Gate (AND/OR/NOT, 2 inputs only) ============

    private async Task ExecuteGateAsync(FlowNode node)
    {
        var logicType = node.GetParam<string>("GateLogicType") ?? "AND";

        // Gate receives two input signals (Input0, Input1)
        // The context stores predecessor results keyed by {nodeId}_result
        // For simplicity, we look up the two predecessor node IDs from connections

        bool input0 = _context.Get<bool>($"{node.NodeId}_input0");
        bool input1 = _context.Get<bool>($"{node.NodeId}_input1");

        bool gateResult = logicType switch
        {
            "AND" => input0 && input1,
            "OR" => input0 || input1,
            "NOT" => !input0,  // NOT only uses Input0
            _ => false
        };

        // Store result in context for downstream nodes
        _context.Set($"{node.NodeId}_result", gateResult);
        _context.Logger.Info(node.NodeName, $"Gate {logicType}: in0={input0}, in1={input1} => {gateResult}");

        await Task.CompletedTask;
    }

    // ============ ColorMotion ============

    private async Task ExecuteColorMotionAsync(FlowNode node)
    {
        var motionMode = node.GetParam<string>("MotionMode") ?? "MotionDetect";
        var targetWindow = node.GetParam<string>("TargetWindow") ?? "";

        var hWnd = _context.CurrentHwnd;
        if (hWnd == IntPtr.Zero && !string.IsNullOrEmpty(targetWindow))
            hWnd = WindowHelper.FindWindowByTitle(targetWindow);
        if (hWnd == IntPtr.Zero)
            throw new InvalidOperationException("No target window found for ColorMotion");

        var region = node.GetParam<Region>("Region") ?? new Region { X = 0, Y = 0, Width = 200, Height = 200 };
        var useFullScreen = node.GetParam<bool?>("UseFullScreen") ?? true;
        var (clientW, clientH) = (0, 0);
        int capX = region.X, capY = region.Y, capW = region.Width, capH = region.Height;
        if (hWnd != IntPtr.Zero)
        {
            var bounds = WindowHelper.GetClientBounds(hWnd);
            clientW = bounds.Width;
            clientH = bounds.Height;
            if (useFullScreen) { capX = 0; capY = 0; capW = clientW; capH = clientH; }
        }

        // HSV params — use ResolveTargetRgb for robust string-based colour reading
        var targetRgb = node.ResolveTargetRgb();
        var hueTol = node.GetParam<int?>("HueTolerance") ?? 8;
        var svTol = node.GetParam<int?>("SVTolerance") ?? 30;

        if (motionMode == "MotionDetect")
        {
            // Monitor color motion: detect if target color is moving
            var checkInterval = node.GetParam<int?>("MoveCheckIntervalMs") ?? 30;
            var durationMs = node.GetParam<int?>("MoveDurationMs") ?? 10000;
            var moveThresholdPx = node.GetParam<int?>("MoveThresholdPx") ?? 5;

            _context.Logger.Info(node.NodeName, $"ColorMotion MotionDetect: {durationMs}ms, threshold={moveThresholdPx}px");

            var stopAt = DateTime.UtcNow.AddMilliseconds(durationMs);
            Point? lastCenter = null;
            bool motionDetected = false;

            while (DateTime.UtcNow < stopAt && !motionDetected)
            {
                _context.CheckCancellation();
                await _context.WaitIfPausedAsync();

                using var frame = ScreenCapture.CaptureWindowRegion(hWnd, capX, capY, capW, capH);
                if (frame == null) break;

                var currentCenter = ImageRecognition.DetectColorCenter(frame, targetRgb, hueTol, svTol);

                if (currentCenter != null && lastCenter != null)
                {
                    int dx = currentCenter.Value.X - lastCenter.Value.X;
                    int dy = currentCenter.Value.Y - lastCenter.Value.Y;
                    if (Math.Abs(dx) > moveThresholdPx || Math.Abs(dy) > moveThresholdPx)
                    {
                        motionDetected = true;
                        _context.Logger.Success(node.NodeName, $"Motion detected: dx={dx}, dy={dy}");
                    }
                }

                if (currentCenter != null) lastCenter = currentCenter;
                await Task.Delay(checkInterval);
            }

            _context.Set($"{node.NodeId}_result", motionDetected);
            _context.Set($"{node.NodeId}_motionDetected", motionDetected);
            _context.Set($"{node.NodeId}_result", motionDetected ? "True" : "False");

            _context.Logger.Info(node.NodeName, $"MotionDetect result: {motionDetected}");
            // Branch routing now handled by TraverseNodeChainAsync via port filtering
        }
        else if (motionMode == "StateChange")
        {
            // Monitor color state change at fixed position
            var checkInterval = node.GetParam<int?>("StateCheckIntervalMs") ?? 100;
            var durationMs = node.GetParam<int?>("StateDurationMs") ?? 30000;
            var changeThreshold = node.GetParam<double?>("ColorChangeThreshold") ?? 0.15;

            _context.Logger.Info(node.NodeName, $"ColorMotion StateChange: {durationMs}ms");

            var stopAt = DateTime.UtcNow.AddMilliseconds(durationMs);
            bool stateChanged = false;
            double? baselineRatio = null;

            while (DateTime.UtcNow < stopAt && !stateChanged)
            {
                _context.CheckCancellation();
                await _context.WaitIfPausedAsync();

                using var frame = ScreenCapture.CaptureWindowRegion(hWnd, capX, capY, capW, capH);
                if (frame == null) break;

                double currentRatio = ImageRecognition.CalculateColorFillRatio(frame, targetRgb, hueTol, svTol);

                if (baselineRatio == null)
                {
                    baselineRatio = currentRatio;
                    _context.Logger.Info(node.NodeName, $"Baseline color ratio: {baselineRatio:F4}");
                }
                else if (Math.Abs(currentRatio - baselineRatio.Value) > changeThreshold)
                {
                    stateChanged = true;
                    _context.Logger.Success(node.NodeName, $"State changed: {baselineRatio:F4} → {currentRatio:F4}");
                }

                await Task.Delay(checkInterval);
            }

            _context.Set($"{node.NodeId}_result", stateChanged);
            _context.Set($"{node.NodeId}_stateChanged", stateChanged);

            _context.Set($"{node.NodeId}_result", stateChanged ? "True" : "False");
            _context.Logger.Info(node.NodeName, $"StateChange result: {stateChanged}");
            // Branch routing now handled by TraverseNodeChainAsync via port filtering
        }
        else if (motionMode == "DirectionDetect")
        {
            // Two sub-modes:
            //   "TemplateMatch" — HSV filter + template matching on the filtered shape
            //   "ColorTrack"   — pure HSV center-of-mass tracking (no template needed)
            var trackMode = node.GetParam<string>("TrackMode") ?? "TemplateMatch";
            var threshold = node.GetParam<double?>("TemplateMatchThreshold") ?? 0.8;
            var checkInterval = node.GetParam<int?>("MoveCheckIntervalMs") ?? 30;
            var durationMs = node.GetParam<int?>("MoveDurationMs") ?? 10000;

            _context.Logger.Info(node.NodeName,
                $"ColorMotion DirectionDetect ({trackMode}): {durationMs}ms");

            Mat? refTemplateMat = null;
            if (trackMode == "TemplateMatch")
            {
                var refImagePath = node.GetParam<string>("ReferenceImagePath") ?? "";
                if (string.IsNullOrEmpty(refImagePath) || !File.Exists(refImagePath))
                    throw new FileNotFoundException($"Reference image not found: {refImagePath}");
                refTemplateMat = ImageRecognition.LoadTemplate(refImagePath);
            }

            var stopAt = DateTime.UtcNow.AddMilliseconds(durationMs);
            Point? lastCenter = null;
            string detectedDirection = "Stationary";
            bool found = false;

            while (DateTime.UtcNow < stopAt && !found)
            {
                _context.CheckCancellation();
                await _context.WaitIfPausedAsync();

                using var frame = ScreenCapture.CaptureWindowRegion(hWnd, capX, capY, capW, capH);
                if (frame == null) break;

                Point? currentCenter;

                if (trackMode == "ColorTrack")
                {
                    // Pure HSV color center detection — no template matching
                    currentCenter = ImageRecognition.DetectColorCenter(frame, targetRgb, hueTol, svTol);
                }
                else
                {
                    // Template matching within HSV-filtered regions
                    var result = ImageRecognition.FindTemplateWithColorFilter(
                        frame, refTemplateMat!, targetRgb, hueTol, svTol, threshold);
                    currentCenter = result?.point;
                }

                if (currentCenter != null)
                {
                    if (lastCenter != null)
                    {
                        int dx = currentCenter.Value.X - lastCenter.Value.X;
                        int dy = currentCenter.Value.Y - lastCenter.Value.Y;
                        detectedDirection = ClassifyDirection(dx, dy);
                        if (detectedDirection != "Stationary")
                        {
                            found = true;
                            _context.Logger.Success(node.NodeName,
                                $"Direction: {detectedDirection} (dx={dx}, dy={dy})");
                        }
                    }
                    lastCenter = currentCenter;
                }

                await Task.Delay(checkInterval);
            }

            refTemplateMat?.Dispose();

            _context.Set($"{node.NodeId}_result", detectedDirection);
            _context.Set($"{node.NodeId}_direction", detectedDirection);

            _context.Logger.Info(node.NodeName, $"DirectionDetect result: {detectedDirection}");
            // Direction branch routing is now handled by connection traversal (activePort filtering)
            // — no internal ExecuteNodeListAsync to avoid double-execution with connected nodes.
        }
        else if (motionMode == "ColorDetect")
        {
            // Simple color presence detection (no shape/motion consideration)
            var checkInterval = node.GetParam<int?>("MoveCheckIntervalMs") ?? 100;
            var durationMs = node.GetParam<int?>("MoveDurationMs") ?? 10000;

            _context.Logger.Info(node.NodeName, $"ColorMotion ColorDetect: {durationMs}ms");

            var stopAt = DateTime.UtcNow.AddMilliseconds(durationMs);
            bool colorFound = false;

            while (DateTime.UtcNow < stopAt && !colorFound)
            {
                _context.CheckCancellation();
                await _context.WaitIfPausedAsync();

                using var frame = ScreenCapture.CaptureWindowRegion(hWnd, capX, capY, capW, capH);
                if (frame == null) break;

                var center = ImageRecognition.DetectColorCenter(frame, targetRgb, hueTol, svTol);
                if (center != null)
                {
                    colorFound = true;
                    _context.Logger.Success(node.NodeName, $"Color detected at ({center.Value.X}, {center.Value.Y})");
                }

                await Task.Delay(checkInterval);
            }

            _context.Set($"{node.NodeId}_result", colorFound);
            _context.Set($"{node.NodeId}_colorFound", colorFound);
            _context.Set($"{node.NodeId}_result", colorFound ? "True" : "False");

            _context.Logger.Info(node.NodeName, $"ColorDetect result: {colorFound}");
            // Branch routing now handled by TraverseNodeChainAsync via port filtering
        }
    }

    /// <summary>
    /// Classify movement direction into 5 cardinal outputs using angle-based sectors (±35°).
    /// Horizontal-dominant movements within ±35° of the X axis map to Left/Right.
    /// Vertical-dominant movements within ±35° of the Y axis map to Up/Down.
    /// </summary>
    private static string ClassifyDirection(int dx, int dy)
    {
        int threshold = 3;
        int absDx = Math.Abs(dx);
        int absDy = Math.Abs(dy);

        if (absDx < threshold && absDy < threshold)
            return "Stationary";

        // angle in degrees: 0=Right, 90=Down, ±180=Left, -90=Up
        double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

        if (angle > -35 && angle <= 35)
            return "Right";
        if (angle > 35 && angle <= 125)
            return "Down";
        if (angle > 125 || angle <= -125)
            return "Left";
        // angle ∈ (-125, -35]
        return "Up";
    }

    // ============ ColorCal (v2.2) ============

    private async Task ExecuteColorCalAsync(FlowNode node)
    {
        // 1. Load detection targets
        var targetsConfig = node.GetParam<List<ColorCalTarget>>("DetectionTargets") ?? new List<ColorCalTarget>();
        if (targetsConfig.Count == 0)
        {
            _context.Logger.Warning(node.NodeName, "No detection targets configured");
            _context.Set($"{node.NodeId}_result", 0);
            return;
        }

        // 2. Detect all targets
        var detectionResults = new List<ColorCalTargetResult>();
        foreach (var target in targetsConfig)
        {
            var result = await DetectSingleTargetAsync(node, target);
            detectionResults.Add(result);
        }

        // 3. Store results in context for expression evaluation
        foreach (var r in detectionResults)
        {
            _context.Set($"{node.NodeId}_{r.Name}_X", r.X);
            _context.Set($"{node.NodeId}_{r.Name}_Y", r.Y);
            _context.Set($"{node.NodeId}_{r.Name}_Found", r.Found);
            _context.Logger.Info(node.NodeName, $"Target '{r.Name}': Found={r.Found}, X={r.X}, Y={r.Y}");
        }

        // 4. Evaluate expression
        var expression = node.GetParam<string>("Expression") ?? "0";
        int resultIndex;
        try
        {
            resultIndex = EvaluateColorCalExpressionV2(expression, detectionResults, targetsConfig, _context, node.NodeId);
        }
        catch (Exception ex)
        {
            _context.Logger.Warning(node.NodeName, $"Expression eval failed: {ex.Message}, using default (0)");
            resultIndex = 0;
        }

        _context.Set($"{node.NodeId}_result", resultIndex);
        _context.Logger.Info(node.NodeName, $"ColorCal expression result: {resultIndex}");
    }

    private async Task<ColorCalTargetResult> DetectSingleTargetAsync(FlowNode node, ColorCalTarget target)
    {
        var result = new ColorCalTargetResult { Name = target.Name };

        var hWnd = _context.CurrentHwnd;
        if (hWnd == IntPtr.Zero && !string.IsNullOrEmpty(target.TargetWindow))
            hWnd = WindowHelper.FindWindowByTitle(target.TargetWindow);
        if (hWnd == IntPtr.Zero)
        {
            _context.Logger.Warning(node.NodeName, $"Target '{target.Name}': No target window found");
            return result;
        }

        var bounds = WindowHelper.GetClientBounds(hWnd);
        int capX = target.Region.X, capY = target.Region.Y;
        int capW = target.Region.Width, capH = target.Region.Height;
        if (target.UseFullScreen)
        {
            capX = 0; capY = 0; capW = bounds.Width; capH = bounds.Height;
        }

        using var frame = ScreenCapture.CaptureWindowRegion(hWnd, capX, capY, capW, capH);
        if (frame == null)
        {
            _context.Logger.Warning(node.NodeName, $"Target '{target.Name}': Failed to capture screen");
            return result;
        }

        var targetRgb = target.GetRgbColor();

        if (target.TrackMode == "TemplateMatch")
        {
            // HSV filter + template matching (shape + color)
            if (!string.IsNullOrEmpty(target.TemplateImagePath) && File.Exists(target.TemplateImagePath))
            {
                try
                {
                    using var templateMat = ImageRecognition.LoadTemplate(target.TemplateImagePath);
                    var matches = ImageRecognition.DetectMultipleTargets(frame, templateMat, targetRgb, target.HueTolerance, target.SVTolerance, target.TemplateMatchThreshold, 1);
                    if (matches.Count > 0)
                    {
                        result.Found = true;
                        result.X = matches[0].X;
                        result.Y = matches[0].Y;
                        result.Confidence = target.TemplateMatchThreshold;
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    _context.Logger.Warning(node.NodeName, $"Target '{target.Name}': Template match failed: {ex.Message}");
                }
            }

            // Fallback to pure HSV detection if template match fails or no template
            var centers = ImageRecognition.DetectMultipleColorCenters(frame, targetRgb, target.HueTolerance, target.SVTolerance, 1);
            if (centers.Count > 0)
            {
                result.Found = true;
                result.X = centers[0].X;
                result.Y = centers[0].Y;
                result.Confidence = 1.0;
            }
        }
        else // ColorTrack
        {
            // Pure HSV color center detection — no template needed
            var centers = ImageRecognition.DetectMultipleColorCenters(frame, targetRgb, target.HueTolerance, target.SVTolerance, 1);
            if (centers.Count > 0)
            {
                result.Found = true;
                result.X = centers[0].X;
                result.Y = centers[0].Y;
                result.Confidence = 1.0;
            }
        }

        return result;
    }

    /// <summary>
    /// Evaluate a C# expression for ColorCal v2.
    /// Variables available: {TargetName}.X, {TargetName}.Y, {TargetName}.Found
    /// Supports: arithmetic, comparison, ternary, Math functions.
    /// Result must be a single integer (branch index).
    /// </summary>
    private static int EvaluateColorCalExpressionV2(string expression, List<ColorCalTargetResult> results, List<ColorCalTarget> targets, FlowContext context, string nodeId)
    {
        expression = expression.Trim();

        // Direct integer
        if (int.TryParse(expression, out var directInt)) return directInt;

        // Pre-process: replace {Name}.X / {Name}.Y / {Name}.Found with actual values
        // Also support index-based aliases: A=first target, B=second, etc.
        var processedExpr = expression;
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            // Name-based: Target1.X, Target2.X, etc.
            processedExpr = processedExpr.Replace($"{r.Name}.X", r.X.ToString());
            processedExpr = processedExpr.Replace($"{r.Name}.Y", r.Y.ToString());
            processedExpr = processedExpr.Replace($"{r.Name}.Found", r.Found ? "1" : "0");
            // Index-based alias: A.X, B.X, C.X, ... (A=0, B=1, C=2, ...)
            char alias = (char)('A' + i);
            processedExpr = processedExpr.Replace($"{alias}.X", r.X.ToString());
            processedExpr = processedExpr.Replace($"{alias}.Y", r.Y.ToString());
            processedExpr = processedExpr.Replace($"{alias}.Found", r.Found ? "1" : "0");
        }

        // Handle nested ternary: recursively parse root-level ? :
        var rootTernary = ParseRootTernary(processedExpr);
        if (rootTernary.HasValue)
        {
            bool conditionResult = EvaluateColorCalCondition(rootTernary.Value.Condition);
            string branchToEval = conditionResult ? rootTernary.Value.TrueExpr : rootTernary.Value.FalseExpr;
            return EvaluateColorCalExpressionV2(branchToEval, results, targets, context, nodeId);
        }

        // Try simple arithmetic evaluation using DataTable.Compute
        try
        {
            var value = EvaluateSimpleExpression(processedExpr);
            return (int)value;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Parse the root-level ternary operator, correctly handling nesting and parentheses.
    /// Returns null if no root-level ternary is found.
    /// </summary>
    private static (string Condition, string TrueExpr, string FalseExpr)? ParseRootTernary(string expr)
    {
        expr = expr.Trim();

        // Strip outer parentheses if they wrap the entire expression
        while (expr.Length > 2 && expr[0] == '(' && expr[expr.Length - 1] == ')')
        {
            int parenDepth = 1;
            bool balanced = true;
            for (int i = 1; i < expr.Length - 1; i++)
            {
                if (expr[i] == '(') parenDepth++;
                else if (expr[i] == ')') parenDepth--;
                if (parenDepth == 0) { balanced = false; break; }
            }
            if (balanced && parenDepth == 1)
                expr = expr.Substring(1, expr.Length - 2).Trim();
            else
                break;
        }

        // Find the root-level ':' — the last ':' at the minimal parenthesis depth
        int minDepth = int.MaxValue;
        int colonIndex = -1;
        int depth = 0;

        for (int i = 0; i < expr.Length; i++)
        {
            char c = expr[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ':' && depth <= minDepth)
            {
                minDepth = depth;
                colonIndex = i;
            }
        }

        if (colonIndex == -1) return null;

        // Compute the depth at the colon position
        depth = 0;
        for (int i = 0; i < colonIndex; i++)
        {
            char c = expr[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
        }
        int targetDepth = depth;

        // Find the matching '?' before the colon at the same depth
        depth = 0;
        int questionIndex = -1;
        for (int i = 0; i < colonIndex; i++)
        {
            char c = expr[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == '?' && depth == targetDepth)
                questionIndex = i;
        }

        if (questionIndex == -1) return null;

        string condition = expr.Substring(0, questionIndex).Trim();
        string trueExpr = expr.Substring(questionIndex + 1, colonIndex - questionIndex - 1).Trim();
        string falseExpr = expr.Substring(colonIndex + 1).Trim();

        return (condition, trueExpr, falseExpr);
    }

    private static bool EvaluateColorCalCondition(string condition)
    {
        condition = condition.Trim();

        // Support direct numeric truthiness: non-zero = true, 0 = false
        if (double.TryParse(condition, out var directValue))
            return Math.Abs(directValue) > 0.001;

        // Support: a > b, a >= b, a < b, a <= b, a == b, a != b
        var match = System.Text.RegularExpressions.Regex.Match(condition,
            @"^([+-]?\d+(?:\.\d+)?)\s*(>=|<=|>|<|==|!=)\s*([+-]?\d+(?:\.\d+)?)$");
        if (!match.Success) return false;

        double left = double.Parse(match.Groups[1].Value);
        string op = match.Groups[2].Value;
        double right = double.Parse(match.Groups[3].Value);

        return op switch
        {
            ">" => left > right,
            ">=" => left >= right,
            "<" => left < right,
            "<=" => left <= right,
            "==" => Math.Abs(left - right) < 0.001,
            "!=" => Math.Abs(left - right) >= 0.001,
            _ => false
        };
    }

    private static double EvaluateSimpleExpression(string expression)
    {
        // Use DataTable.Compute for simple arithmetic
        using var dt = new System.Data.DataTable();
        var result = dt.Compute(expression, "");
        return Convert.ToDouble(result);
    }

    // ============ Break ============

    private async Task ExecuteBreakAsync(FlowNode node)
    {
        _context.Logger.Info(node.NodeName, "Break node triggered");

        // Find the active Loop node and signal break
        // The Break node receives a signal from a Condition/ColorMotion node
        // and sets the break flag on the associated Loop node

        // Signal break - search context for active loop
        bool signaled = false;
        foreach (var key in _context.Variables.Keys)
        {
            if (key.EndsWith("_loop_active") && _context.Get<bool?>(key) == true)
            {
                var loopId = key.Replace("_loop_active", "");
                _context.Set($"{loopId}_break", true);
                _context.Logger.Info(node.NodeName, $"Break signaled to Loop: {loopId}");
                signaled = true;
                break;
            }
        }

        if (!signaled)
            _context.Logger.Warning(node.NodeName, "No active loop found to break");

        // Execute True branch (Break) if connected
        if (node.TrueBranch != null && node.TrueBranch.Count > 0)
            await ExecuteNodeListAsync(node.TrueBranch);

        await Task.CompletedTask;
    }

    // ============ Helpers ============
}

// Support classes
public class Region
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class TemplateScaleRange
{
    public double Min { get; set; } = 0.5;
    public double Max { get; set; } = 1.5;
    public double Step { get; set; } = 0.1;
}
