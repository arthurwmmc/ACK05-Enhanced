namespace Ack05Enhanced.App;

internal sealed class OpenTargetDialog : Form
{
    private readonly TextBox _target = new();

    internal OpenTargetDialog(string? initialTarget)
    {
        Text = "Open Target";
        Width = 720;
        Height = 245;
        MinimumSize = new Size(560, 245);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Cascadia Mono", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 3,
            RowCount = 4,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var label = new Label
        {
            Text = "Executable, file, folder, shortcut, or HTTP/HTTPS URL",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        layout.Controls.Add(label, 0, 0);
        layout.SetColumnSpan(label, 3);

        _target.Text = initialTarget ?? string.Empty;
        _target.Dock = DockStyle.Fill;
        _target.PlaceholderText = @"C:\path\to\target  or  https://example.com";
        layout.Controls.Add(_target, 0, 1);

        var browseFile = new Button { Text = "Browse file...", Dock = DockStyle.Fill };
        browseFile.Click += (_, _) => BrowseFile();
        layout.Controls.Add(browseFile, 1, 1);

        var browseFolder = new Button { Text = "Browse folder...", Dock = DockStyle.Fill };
        browseFolder.Click += (_, _) => BrowseFolder();
        layout.Controls.Add(browseFolder, 2, 1);

        var hint = new Label
        {
            Text = "Windows opens the target as if it were double-clicked. " +
                   "Choose the final file directly to replace ‘open folder, then click’.\r\n" +
                   "Output must be ON; this action does not request administrator privileges.",
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            Padding = new Padding(0, 10, 0, 0),
        };
        layout.Controls.Add(hint, 0, 2);
        layout.SetColumnSpan(hint, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        var okay = new Button { Text = "Use target", Width = 108, Height = 32 };
        okay.Click += (_, _) => AcceptTarget();
        var cancel = new Button
        {
            Text = "Cancel",
            Width = 92,
            Height = 32,
            DialogResult = DialogResult.Cancel,
        };
        buttons.Controls.Add(okay);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 3);
        layout.SetColumnSpan(buttons, 3);

        AcceptButton = okay;
        CancelButton = cancel;
        Controls.Add(layout);
    }

    internal string Target => _target.Text.Trim();

    private void BrowseFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a target to open",
            Filter = "All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (File.Exists(Environment.ExpandEnvironmentVariables(Target)))
        {
            dialog.FileName = Environment.ExpandEnvironmentVariables(Target);
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _target.Text = dialog.FileName;
        }
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose a folder to open",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        var expanded = Environment.ExpandEnvironmentVariables(Target);
        if (Directory.Exists(expanded))
        {
            dialog.SelectedPath = expanded;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _target.Text = dialog.SelectedPath;
        }
    }

    private void AcceptTarget()
    {
        if (!IsSupportedTarget(Target))
        {
            MessageBox.Show(
                this,
                "Enter an absolute file/folder path or an absolute HTTP/HTTPS URL.",
                "Open Target",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _target.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool IsSupportedTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Length > 2048)
        {
            return false;
        }

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return true;
        }

        if (target.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return Path.IsPathFullyQualified(Environment.ExpandEnvironmentVariables(target));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
