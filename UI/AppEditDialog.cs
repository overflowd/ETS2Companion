using System.Drawing;
using ETS2CompanionLauncher.Models;
using ETS2CompanionLauncher.Services;

namespace ETS2CompanionLauncher.UI;

public sealed class AppEditDialog : Form
{
    private readonly TextBox _nameBox;
    private readonly TextBox _pathBox;
    private readonly TextBox _argsBox;
    private readonly TextBox _processBox;
    private readonly NumericUpDown _delayNumeric;
    private readonly CheckBox _enabledCheck;
    private readonly CheckBox _closeOnExitCheck;
    private readonly CheckBox _runAsAdminCheck;
    private readonly Label _hintLabel;

    public CompanionApp ResultApp { get; }

    public AppEditDialog(CompanionApp app, bool isNew)
    {
        ResultApp = app.Clone();

        Text = isNew ? "Add Companion App" : $"Edit — {app.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(640, 0);
        BackColor = DarkTheme.Background;
        ForeColor = DarkTheme.TextPrimary;
        Font = new Font("Segoe UI", 9.5f);
        DarkTheme.ApplyDarkTitleBar(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = new Padding(18),
            BackColor = DarkTheme.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        int row = 0;

        // 1. Quick-pick buttons
        var quickPickFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 12)
        };

        var pickRunningBtn = DarkTheme.CreateAutoButton("Pick from Running Apps...");
        pickRunningBtn.Click += (_, _) => OpenPicker(AppPickerMode.Running);

        var pickInstalledBtn = DarkTheme.CreateAutoButton("Pick from Installed / Start Menu...");
        pickInstalledBtn.Click += (_, _) => OpenPicker(AppPickerMode.Installed);

        var browseTopBtn = DarkTheme.CreateAutoButton("Browse .exe...");
        browseTopBtn.Margin = new Padding(0);
        browseTopBtn.Click += BrowseBtn_Click;

        quickPickFlow.Controls.AddRange(new Control[] { pickRunningBtn, pickInstalledBtn, browseTopBtn });
        root.Controls.Add(quickPickFlow, 0, row++);

        // 2. Application Name
        root.Controls.Add(CreateFieldLabel("Application Name:"), 0, row++);
        _nameBox = CreateTextBox(ResultApp.Name);
        _nameBox.Margin = new Padding(0, 0, 0, 12);
        root.Controls.Add(_nameBox, 0, row++);

        // 3. Path + Detect button
        root.Controls.Add(CreateFieldLabel("Executable Path (.exe / .lnk) or Steam URL (steam://rungameid/...):"), 0, row++);
        var pathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 12)
        };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _pathBox = CreateTextBox(ResultApp.ExecutablePath);
        _pathBox.Margin = new Padding(0, 2, 8, 2);

        var detectBtn = DarkTheme.CreateAutoButton("Detect");
        detectBtn.Margin = new Padding(0);
        detectBtn.Click += DetectBtn_Click;

        pathRow.Controls.Add(_pathBox, 0, 0);
        pathRow.Controls.Add(detectBtn, 1, 0);
        root.Controls.Add(pathRow, 0, row++);

        // 4. Arguments & Process Name (2 columns)
        var dualRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 12)
        };
        dualRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        dualRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var argsLabel = CreateFieldLabel("Launch Arguments (optional):");
        argsLabel.Margin = new Padding(0, 0, 10, 4);
        var procLabel = CreateFieldLabel("Process Name (for running check):");
        procLabel.Margin = new Padding(10, 0, 0, 4);

        _argsBox = CreateTextBox(ResultApp.Arguments);
        _argsBox.Margin = new Padding(0, 0, 10, 0);
        _processBox = CreateTextBox(ResultApp.ProcessName);
        _processBox.Margin = new Padding(10, 0, 0, 0);

        dualRow.Controls.Add(argsLabel, 0, 0);
        dualRow.Controls.Add(procLabel, 1, 0);
        dualRow.Controls.Add(_argsBox, 0, 1);
        dualRow.Controls.Add(_processBox, 1, 1);
        root.Controls.Add(dualRow, 0, row++);

        // 5. Launch delay
        var delayRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 12)
        };

        var delayLabel = new Label
        {
            Text = "Launch Delay (seconds):",
            AutoSize = true,
            ForeColor = DarkTheme.TextSecondary,
            Margin = new Padding(0, 5, 12, 0)
        };

        _delayNumeric = new NumericUpDown
        {
            Width = 84,
            Minimum = 0,
            Maximum = 60,
            Value = Math.Clamp(ResultApp.LaunchDelaySeconds, 0, 60),
            BackColor = DarkTheme.Elevated,
            ForeColor = DarkTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        delayRow.Controls.Add(delayLabel);
        delayRow.Controls.Add(_delayNumeric);
        root.Controls.Add(delayRow, 0, row++);

        // 6. Checkboxes
        _enabledCheck = new CheckBox
        {
            Text = "Launch automatically when game starts",
            AutoSize = true,
            Checked = ResultApp.Enabled,
            ForeColor = DarkTheme.TextPrimary,
            Margin = new Padding(0, 0, 0, 6)
        };
        root.Controls.Add(_enabledCheck, 0, row++);

        _closeOnExitCheck = new CheckBox
        {
            Text = "Close automatically when game exits",
            AutoSize = true,
            Checked = ResultApp.CloseOnGameExit,
            ForeColor = DarkTheme.TextPrimary,
            Margin = new Padding(0, 0, 0, 6)
        };
        root.Controls.Add(_closeOnExitCheck, 0, row++);

        _runAsAdminCheck = new CheckBox
        {
            Text = "Run as administrator (may trigger UAC prompt)",
            AutoSize = true,
            Checked = ResultApp.RunAsAdmin,
            ForeColor = DarkTheme.TextSecondary,
            Margin = new Padding(0, 0, 0, 12)
        };
        root.Controls.Add(_runAsAdminCheck, 0, row++);

        // 7. Hint label
        _hintLabel = new Label
        {
            Text = "Tip: Pick any open/installed app above, browse for an .exe, or paste a steam://rungameid/ URL.",
            AutoSize = true,
            MaximumSize = new Size(600, 0),
            ForeColor = DarkTheme.TextSecondary,
            Font = new Font("Segoe UI", 8.5f),
            Margin = new Padding(0, 0, 0, 14)
        };
        root.Controls.Add(_hintLabel, 0, row++);

        // 8. Action buttons
        var footerFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0)
        };

        var saveBtn = DarkTheme.CreateAutoButton("Save", primary: true);
        saveBtn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        saveBtn.Margin = new Padding(8, 0, 0, 0);
        saveBtn.Click += SaveBtn_Click;

        var cancelBtn = DarkTheme.CreateAutoButton("Cancel");
        cancelBtn.Margin = new Padding(0);
        cancelBtn.DialogResult = DialogResult.Cancel;

        footerFlow.Controls.Add(saveBtn);
        footerFlow.Controls.Add(cancelBtn);
        root.Controls.Add(footerFlow, 0, row++);

        AcceptButton = saveBtn;
        CancelButton = cancelBtn;
    }

    private void OpenPicker(AppPickerMode mode)
    {
        using var picker = new AppPickerDialog(mode);
        if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedApp is not null)
        {
            var picked = picker.SelectedApp;
            _nameBox.Text = picked.Name;
            _pathBox.Text = picked.ExecutablePath;
            _processBox.Text = picked.ProcessName;
            _hintLabel.ForeColor = DarkTheme.Success;
            _hintLabel.Text = $"Selected ({picked.Source}): {picked.ExecutablePath}";
        }
    }

    private void BrowseBtn_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select application executable",
            Filter = "Applications (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (!string.IsNullOrWhiteSpace(_pathBox.Text) && File.Exists(_pathBox.Text))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(_pathBox.Text);
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                _nameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }

            if (string.IsNullOrWhiteSpace(_processBox.Text) &&
                dialog.FileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                _processBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void DetectBtn_Click(object? sender, EventArgs e)
    {
        var probe = ResultApp.Clone();
        probe.Name = _nameBox.Text.Trim();
        probe.ProcessName = _processBox.Text.Trim();

        Cursor = Cursors.WaitCursor;
        try
        {
            var detected = AppAutoDetector.Detect(probe);
            if (detected is not null)
            {
                _pathBox.Text = detected.Path;
                if (detected.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    _processBox.Text = Path.GetFileNameWithoutExtension(detected.Path);
                }

                _hintLabel.ForeColor = DarkTheme.Success;
                _hintLabel.Text = $"Found ({detected.Source}): {detected.Path}";
            }
            else
            {
                _hintLabel.ForeColor = DarkTheme.Warning;
                _hintLabel.Text = "Not found automatically. Use 'Pick from...' or 'Browse .exe...' above.";
            }
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void SaveBtn_Click(object? sender, EventArgs e)
    {
        var name = _nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Please enter an application name.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _nameBox.Focus();
            return;
        }

        var path = _pathBox.Text.Trim().Trim('"');
        ResultApp.Name = name;
        ResultApp.ExecutablePath = path;
        ResultApp.Arguments = _argsBox.Text.Trim();
        ResultApp.ProcessName = _processBox.Text.Trim();
        ResultApp.LaunchDelaySeconds = (int)_delayNumeric.Value;
        ResultApp.Enabled = _enabledCheck.Checked;
        ResultApp.CloseOnGameExit = _closeOnExitCheck.Checked;
        ResultApp.RunAsAdmin = _runAsAdminCheck.Checked;

        if (string.IsNullOrWhiteSpace(ResultApp.ProcessName) &&
            !ResultApp.IsUriTarget &&
            path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            ResultApp.ProcessName = Path.GetFileNameWithoutExtension(path);
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static Label CreateFieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = DarkTheme.TextSecondary,
        Margin = new Padding(0, 0, 0, 4)
    };

    private static TextBox CreateTextBox(string value) => new()
    {
        Text = value,
        Dock = DockStyle.Fill,
        BackColor = DarkTheme.Elevated,
        ForeColor = DarkTheme.TextPrimary,
        BorderStyle = BorderStyle.FixedSingle
    };
}
