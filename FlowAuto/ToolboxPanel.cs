using FlowAuto.Models;

namespace FlowAuto;

public class ToolboxPanel : Panel
{
    public ToolboxPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(30, 30, 35);
        AutoScroll = true;

        InitializeToolbox();
    }

    private void InitializeToolbox()
    {
        var titleLabel = new Label
        {
            Text = "Toolbox",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(8, 8),
            AutoSize = true
        };
        Controls.Add(titleLabel);

        int y = 36;
        AddToolboxItem("Start Program", NodeType.StartProgram, Color.FromArgb(66, 133, 244), ref y);
        AddToolboxItem("Click Element", NodeType.ClickElement, Color.FromArgb(52, 168, 83), ref y);
        AddToolboxItem("Wait Condition", NodeType.WaitCondition, Color.FromArgb(251, 188, 4), ref y);
        AddToolboxItem("Key Press", NodeType.KeyPress, Color.FromArgb(154, 71, 220), ref y);
        AddToolboxItem("Loop Start", NodeType.Loop, Color.FromArgb(255, 152, 0), ref y);
        AddToolboxItem("Loop End", NodeType.LoopEnd, Color.FromArgb(255, 152, 0), ref y);
        AddToolboxItem("Condition", NodeType.Condition, Color.FromArgb(233, 30, 99), ref y);
        AddToolboxItem("Gate", NodeType.Gate, Color.FromArgb(0, 188, 212), ref y);
        AddToolboxItem("ColorCal", NodeType.ColorCal, Color.FromArgb(156, 39, 176), ref y);
        AddToolboxItem("ColorMotion", NodeType.ColorMotion, Color.FromArgb(0, 150, 136), ref y);
    }

    private void AddToolboxItem(string name, NodeType nodeType, Color color, ref int y)
    {
        var panel = new Panel
        {
            Location = new Point(6, y),
            Size = new Size(175, 36),
            BackColor = Color.FromArgb(45, 45, 50),
            Cursor = Cursors.Hand
        };

        var colorBar = new Panel
        {
            Location = new Point(2, 4),
            Size = new Size(4, 28),
            BackColor = color
        };
        panel.Controls.Add(colorBar);

        var label = new Label
        {
            Text = name,
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.White,
            Location = new Point(14, 8),
            AutoSize = true
        };
        panel.Controls.Add(label);

        // Drag support
        panel.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                panel.DoDragDrop(nodeType.ToString(), DragDropEffects.Copy);
            }
        };
        label.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                panel.DoDragDrop(nodeType.ToString(), DragDropEffects.Copy);
            }
        };

        Controls.Add(panel);
        y += 42;
    }
}
