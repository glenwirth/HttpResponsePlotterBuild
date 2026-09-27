namespace HttpResponsePlotter;

internal sealed class SettingsForm : Form
{
    private readonly PropertyGrid _grid;
    private AppSettings _working;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public AppSettings? Result { get; private set; }

    public SettingsForm(AppSettings current, string settingsPath)
    {
        _working = current.Clone();

        Text = "Settings";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(600, 700);
        MinimumSize = new Size(420, 400);
        MinimizeBox = false;
        ShowInTaskbar = false;

        _grid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            SelectedObject = _working,
            PropertySort = PropertySort.Categorized,
            ToolbarVisible = false,
        };

        var pathLabel = new Label
        {
            Dock = DockStyle.Top,
            Text = "Saved to: " + settingsPath,
            AutoEllipsis = true,
            Padding = new Padding(6, 6, 6, 0),
            Height = 26,
        };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.None, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var reset = new Button { Text = "Reset to defaults", AutoSize = true };

        ok.Click += (_, _) =>
        {
            var error = _working.Validate();
            if (error is not null)
            {
                MessageBox.Show(this, error, "Invalid setting", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Result = _working;
            DialogResult = DialogResult.OK;
        };
        reset.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Reset all settings to their defaults?", "Reset",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _working = new AppSettings();
                _grid.SelectedObject = _working;
            }
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(6),
        };
        buttons.Controls.AddRange([cancel, ok, reset]);

        Controls.Add(_grid);
        Controls.Add(pathLabel);
        Controls.Add(buttons);

        AcceptButton = ok;
        CancelButton = cancel;
    }
}
