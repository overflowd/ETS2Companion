using System.Drawing;
using ETS2CompanionLauncher.Models;
using ETS2CompanionLauncher.Services;

namespace ETS2CompanionLauncher.UI;

public sealed class AppEditDialog : Form
{
    private static readonly Color BgDark = Color.FromArgb(18, 21, 28);
    private static readonly Color SurfaceDark = Color.FromArgb(27, 32, 43);
    private static readonly Color InputDark = Color.FromArgb(36, 43, 56);
    private static readonly Color TextPrimary = Color.FromArgb(241, 245, 249);
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
    private static readonly Color AccentBlue = Color.FromArgb(56, 189, 248);

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

        Text = isNew ? "Yeni Uygulama Ekle" : $"Düzenle — {app.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 420);
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9.5f);

        var y = 18;

        Controls.Add(CreateLabel("Uygulama Adı:", 20, y));
        _nameBox = CreateTextBox(20, y + 22, 520, ResultApp.Name);
        Controls.Add(_nameBox);
        y += 62;

        Controls.Add(CreateLabel("Dosya Yolu (.exe / .lnk) veya Steam URL (steam://rungameid/...):", 20, y));
        _pathBox = CreateTextBox(20, y + 22, 345, ResultApp.ExecutablePath);
        Controls.Add(_pathBox);

        var browseBtn = CreateButton("Gözat...", 373, y + 20, 80, SurfaceDark, TextPrimary);
        browseBtn.Click += BrowseBtn_Click;
        Controls.Add(browseBtn);

        var detectBtn = CreateButton("Bul", 460, y + 20, 80, SurfaceDark, AccentBlue);
        detectBtn.Click += DetectBtn_Click;
        Controls.Add(detectBtn);
        y += 62;

        Controls.Add(CreateLabel("Başlatma Parametreleri (İsteğe bağlı):", 20, y));
        _argsBox = CreateTextBox(20, y + 22, 250, ResultApp.Arguments);
        Controls.Add(_argsBox);

        Controls.Add(CreateLabel("İşlem Adı (Çalışma kontrolü için):", 290, y));
        _processBox = CreateTextBox(290, y + 22, 250, ResultApp.ProcessName);
        Controls.Add(_processBox);
        y += 62;

        Controls.Add(CreateLabel("Başlatma Gecikmesi (saniye):", 20, y + 3));
        _delayNumeric = new NumericUpDown
        {
            Location = new Point(210, y),
            Size = new Size(80, 28),
            Minimum = 0,
            Maximum = 60,
            Value = Math.Clamp(ResultApp.LaunchDelaySeconds, 0, 60),
            BackColor = InputDark,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_delayNumeric);
        y += 40;

        _enabledCheck = new CheckBox
        {
            Text = "ETS 2 açıldığında bu uygulamayı otomatik başlat",
            Location = new Point(20, y),
            AutoSize = true,
            Checked = ResultApp.Enabled,
            ForeColor = TextPrimary
        };
        Controls.Add(_enabledCheck);
        y += 28;

        _closeOnExitCheck = new CheckBox
        {
            Text = "ETS 2 kapandığında bu uygulamayı otomatik kapat",
            Location = new Point(20, y),
            AutoSize = true,
            Checked = ResultApp.CloseOnGameExit,
            ForeColor = TextPrimary
        };
        Controls.Add(_closeOnExitCheck);
        y += 28;

        _runAsAdminCheck = new CheckBox
        {
            Text = "Yönetici olarak çalıştır (UAC onayı isteyebilir)",
            Location = new Point(20, y),
            AutoSize = true,
            Checked = ResultApp.RunAsAdmin,
            ForeColor = TextMuted
        };
        Controls.Add(_runAsAdminCheck);
        y += 32;

        _hintLabel = new Label
        {
            Text = "İpucu: Uygulama şu an açıksa 'Bul' butonu dosya yolunu anında tespit eder.",
            Location = new Point(20, y),
            Size = new Size(520, 22),
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8.5f)
        };
        Controls.Add(_hintLabel);

        var cancelBtn = CreateButton("İptal", 360, 368, 86, SurfaceDark, TextPrimary);
        cancelBtn.DialogResult = DialogResult.Cancel;
        Controls.Add(cancelBtn);

        var saveBtn = CreateButton("Kaydet", 454, 368, 86, AccentBlue, Color.FromArgb(15, 23, 42));
        saveBtn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        saveBtn.Click += SaveBtn_Click;
        Controls.Add(saveBtn);

        AcceptButton = saveBtn;
        CancelButton = cancelBtn;
    }

    private void BrowseBtn_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Uygulama dosyasını seçin",
            Filter = "Uygulamalar (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|Tüm Dosyalar (*.*)|*.*",
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

                _hintLabel.ForeColor = Color.FromArgb(34, 197, 94);
                _hintLabel.Text = $"Bulundu ({detected.Source}): {detected.Path}";
            }
            else
            {
                _hintLabel.ForeColor = Color.FromArgb(245, 158, 11);
                _hintLabel.Text = "Otomatik bulunamadı. Lütfen 'Gözat...' ile .exe dosyasını seçin.";
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
            MessageBox.Show(this, "Lütfen bir uygulama adı girin.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        ForeColor = TextMuted
    };

    private static TextBox CreateTextBox(int x, int y, int width, string value) => new()
    {
        Text = value,
        Location = new Point(x, y),
        Size = new Size(width, 26),
        BackColor = InputDark,
        ForeColor = TextPrimary,
        BorderStyle = BorderStyle.FixedSingle
    };

    private static Button CreateButton(string text, int x, int y, int width, Color backColor, Color foreColor)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = foreColor,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(46, 55, 72);
        btn.FlatAppearance.BorderSize = 1;
        return btn;
    }
}
