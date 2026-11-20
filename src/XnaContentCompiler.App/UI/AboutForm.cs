using System.Drawing;
using System.Windows.Forms;
using XnaContentCompiler.App.Localization;

namespace XnaContentCompiler.App.UI;

public sealed class UsageForm : Form
{
    public UsageForm()
    {
        var loc = LocalizationService.Instance;

        Text = loc["usage.title"];
        Font = UiFonts.GetInterfaceFont(loc.Language);
        AppIcon.Apply(this);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        ClientSize = new Size(660, 540);
        MinimumSize = new Size(520, 420);

        var text = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Window,
            Text = loc["usage.text"],
            Font = UiFonts.GetMonoFont(),
            DetectUrls = false,
        };

        var closeButton = new Button
        {
            Text = loc["dialog.close"],
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(88, 28),
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 10, 0, 0),
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 12),
        };
        buttonPanel.Controls.Add(closeButton);

        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 14, 16, 4),
        };
        contentPanel.Controls.Add(text);

        Controls.Add(contentPanel);
        Controls.Add(buttonPanel);

        AcceptButton = closeButton;
        CancelButton = closeButton;
    }
}

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        var loc = LocalizationService.Instance;

        Text = loc["about.title"];
        Font = UiFonts.GetInterfaceFont(loc.Language);
        AppIcon.Apply(this);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(480, 300);

        var title = new Label
        {
            Text = loc["app.title"],
            AutoSize = true,
            Font = new Font(Font.FontFamily, 15F, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            Margin = new Padding(0, 0, 0, 6),
        };

        var productVersion = Application.ProductVersion;
        var revisionSeparator = productVersion.IndexOf('+');
        if (revisionSeparator >= 0)
            productVersion = productVersion[..revisionSeparator];

        var version = new Label
        {
            Text = loc.Format("app.versionLabel", productVersion),
            AutoSize = true,
            ForeColor = Color.FromArgb(107, 114, 128),
            Margin = new Padding(0, 0, 0, 14),
        };

        var description = new Label
        {
            Text = loc["about.description"],
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            Margin = new Padding(0, 0, 0, 12),
        };

        var components = new Label
        {
            Text = loc["about.components"],
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            ForeColor = Color.FromArgb(75, 85, 99),
            Margin = new Padding(0, 0, 0, 12),
        };

        var copyright = new Label
        {
            Text = loc["about.copyright"],
            AutoSize = true,
            ForeColor = Color.FromArgb(107, 114, 128),
            Margin = new Padding(0, 0, 0, 0),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(20, 18, 20, 8),
        };
        layout.Controls.Add(title);
        layout.Controls.Add(version);
        layout.Controls.Add(description);
        layout.Controls.Add(components);
        layout.Controls.Add(copyright);

        var okButton = new Button
        {
            Text = loc["dialog.ok"],
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(88, 28),
            Anchor = AnchorStyles.Right,
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(20, 4, 20, 16),
        };
        buttonPanel.Controls.Add(okButton);

        Controls.Add(layout);
        Controls.Add(buttonPanel);

        AcceptButton = okButton;
        CancelButton = okButton;
    }
}
