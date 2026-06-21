namespace FlowAuto.Engine;

public class FlowLogger
{
    private readonly ListBox? _listBox;
    private readonly RichTextBox? _richTextBox;
    private readonly Action<string>? _onLog;

    public FlowLogger(ListBox? listBox = null, RichTextBox? richTextBox = null, Action<string>? onLog = null)
    {
        _listBox = listBox;
        _richTextBox = richTextBox;
        _onLog = onLog;
    }

    public void Info(string nodeName, string message)
    {
        Log("INFO", nodeName, message);
    }

    public void Success(string nodeName, string message)
    {
        Log("OK", nodeName, message);
    }

    public void Warning(string nodeName, string message)
    {
        Log("WARN", nodeName, message);
    }

    public void Error(string nodeName, string message)
    {
        Log("ERROR", nodeName, message);
    }

    public void Retry(string nodeName, int attempt, int maxRetries, string message)
    {
        Log("RETRY", nodeName, $"[{attempt}/{maxRetries}] {message}");
    }

    private void Log(string level, string nodeName, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] [{nodeName}] {message}";

        _onLog?.Invoke(line);

        // Update UI controls on UI thread if available
        if (_listBox != null && !_listBox.IsDisposed)
        {
            _listBox.Invoke(() =>
            {
                _listBox.Items.Add(line);
                _listBox.TopIndex = _listBox.Items.Count - 1;
            });
        }

        if (_richTextBox != null && !_richTextBox.IsDisposed)
        {
            _richTextBox.Invoke(() =>
            {
                _richTextBox.AppendText(line + Environment.NewLine);
                _richTextBox.ScrollToCaret();
            });
        }
    }
}
