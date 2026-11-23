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
        ClientSize = new Size(600, 442);
        BackColor = DarkTheme.Background;
        ForeColor = DarkTheme.TextPrimary;
        Font = new Font("Segoe UI", 9.5f);
        DarkTheme.ApplyDarkTitleBar(this);

        var y = 18;

        // Quick-pick bar at top
        var pickRunningBtn = DarkTheme.CreateButton("Pick from Running Apps...", 20, y, 185, 30);
        pickRunningBtn.Click += (_, _) => OpenPicker(AppPickerMode.Running);
        Controls.Add(pickRunningBtn);

        var pickInstalledBtn = DarkTheme.CreateButton("Pick from Installed / Start Menu...", 213, y, 225, 30);
        pickInstalledBtn.Click += (_, _) => OpenPicker(AppPickerMode.Installed);
        Controls.Add(pickInstalledBtn);

        var browseTopBtn = DarkTheme.CreateButton("Browse .exe...", 446, y, 134, 30);
        browseTopBtn.Click += BrowseBtn_Click;
        Controls.Add(browseTopBtn);
        y += 46;

        Controls.Add(CreateLabel("Application Name:", 20, y));
        _nameBox = CreateTextBox(20, y + 22, 560, ResultApp.Name);
        Controls.Add(_nameBox);
        y += 60;

        Controls.Add(CreateLabel("Executable Path (.exe / .lnk) or Steam URL (steam://rungameid/...):", 20, y));
        _pathBox = CreateTextBox(20, y + 22, 470, ResultApp.ExecutablePath);
        Controls.Add(_pathBox);

        var detectBtn = DarkTheme.CreateButton("Detect", 498, y + 20, 82, 30);
        detectBtn.Click += DetectBtn_Click;
        Controls.Add(detectBtn);
        y += 60;

        Controls.Add(CreateLabel("Launch Arguments (optional):", 20, y));
        _argsBox = CreateTextBox(20, y + 22, 270, ResultApp.Arguments);
        Controls.Add(_argsBox);

        Controls.Add(CreateLabel("Process Name (for running check):", 310, y));
        _processBox = CreateTextBox(310, y + 22, 270, ResultApp.ProcessName);
        Controls.Add(_processBox);
        y += 60;

        Controls.Add(CreateLabel("Launch Delay (seconds):", 20, y + 3));
        _delayNumeric = new NumericUpDown
        {
            Location = new Point(185, y),
            Size = new Size(80, 28),
            Minimum = 0,
            Maximum = 60,
            Value = Math.Clamp(ResultApp.LaunchDelaySeconds, 0, 60),
            BackColor = DarkTheme.Elevated,
            ForeColor = DarkTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_delayNumeric);
        y += 38;

        _enabledCheck = new CheckBox
        {
            Text = "Launch automatically when game starts",
            Location = new Point(20, y),
            AutoSize = true,
            Checked = ResultApp.Enabled,
            ForeColor = DarkTheme.TextPrimary
        };
        Controls.Add(_enabledCheck);
        y += 26;

        _closeOnExitCheck = new CheckBox
        {
            Text = "Close automatically when game exits",
            Location = new Point(20, y),
            AutoSize = true,
            Checked = ResultApp.CloseOnGameExit,
            ForeColor = DarkTheme.TextPrimary
        };
        Controls.Add(_closeOnExitCheck);
        y += 26;

        _runAsAdminCheck = new CheckBox
        {
            Text = "Run as administrator (may trigger UAC prompt)",
            Location = new Point(20, y),
            AutoSize = true,
            Checked = ResultApp.RunAsAdmin,
            ForeColor = DarkTheme.TextSecondary
        };
        Controls.Add(_runAsAdminCheck);
        y += 30;

        _hintLabel = new Label
        {
            Text = "Tip: Pick any open/installed app above, browse for an .exe, or paste a steam://rungameid/ URL.",
            Location = new Point(20, y),
            Size = new Size(560, 22),
            ForeColor = DarkTheme.TextSecondary,
            Font = new Font("Segoe UI", 8.5f)
        };
        Controls.Add(_hintLabel);

        var cancelBtn = DarkTheme.CreateButton("Cancel", 400, 392, 86, 30);
        cancelBtn.DialogResult = DialogResult.Cancel;
        Controls.Add(cancelBtn);

        var saveBtn = DarkTheme.CreateButton("Save", 494, 392, 86, 30, primary: true);
        saveBtn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        saveBtn.Click += SaveBtn_Click;
        Controls.Add(saveBtn);

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

    private static Label CreateLabel(string text, int x, int y) => new()
    {
        Text = text,
        Location = new Point(x, y),
        AutoSize = true,
        ForeColor = DarkTheme.TextSecondary
    };

    private static TextBox CreateTextBox(int x, int y, int width, string value) => new()
    {
        Text = value,
        Location = new Point(x, y),
        Size = new Size(width, 26),
        BackColor = DarkTheme.Elevated,
        ForeColor = DarkTheme.TextPrimary,
        BorderStyle = BorderStyle.FixedSingle
    };
}
