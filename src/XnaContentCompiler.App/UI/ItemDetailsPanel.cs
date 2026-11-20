using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XnaContentCompiler.App.Localization;
using XnaContentCompiler.Core.Content;

namespace XnaContentCompiler.App.UI;

/// <summary>
/// Right-hand property editor. Edits apply to every selected item that supports the
/// option, and tri-state check boxes show mixed values for multi-selections.
/// </summary>
public sealed class ItemDetailsPanel : UserControl
{
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private readonly ErrorProvider _errorProvider;
    private readonly Label _titleLabel;
    private readonly Label _summaryLabel;
    private readonly Label _emptyLabel;
    private readonly TableLayoutPanel _layout;

    private readonly Label _generalHeader;
    private readonly Label _outputNameLabel;
    private readonly Label _sourceLabel;
    private readonly Label _typeLabel;
    private readonly Label _outputPathLabel;
    private readonly TextBox _outputNameBox;
    private readonly Label _sourceValue;
    private readonly Label _typeValue;
    private readonly Label _outputPathValue;

    private readonly Label _textureHeader;
    private readonly Label _formatLabel;
    private readonly ComboBox _formatCombo;
    private readonly CheckBox _mipmapsCheck;
    private readonly CheckBox _premultiplyCheck;
    private readonly CheckBox _resizeCheck;
    private readonly CheckBox _squareCheck;
    private readonly CheckBox _colorKeyCheck;
    private readonly Label _colorKeyColorLabel;
    private readonly Panel _colorSwatch;
    private readonly Button _colorButton;
    private readonly FlowLayoutPanel _colorRow;
    private readonly Label _textureHint;

    private readonly Label _fontHeader;
    private readonly Label _fontFormatLabel;
    private readonly ComboBox _fontFormatCombo;
    private readonly CheckBox _fontPremultiplyCheck;
    private readonly Label _fontHint;

    private readonly Label _audioHeader;
    private readonly Label _audioNote;

    private readonly Label _songHeader;
    private readonly Label _songNote;

    private IReadOnlyList<ContentItem> _items = Array.Empty<ContentItem>();
    private bool _updating;
    private Font? _titleFont;
    private Font? _sectionFont;

    public ItemDetailsPanel()
    {
        AutoScroll = true;
        BackColor = SystemColors.Window;
        Padding = new Padding(14, 10, 14, 10);

        _errorProvider = new ErrorProvider
        {
            BlinkStyle = ErrorBlinkStyle.NeverBlink,
        };
        _errorProvider.ContainerControl = this;

        _titleLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 26,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _summaryLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 22,
            ForeColor = Color.FromArgb(107, 114, 128),
        };

        _emptyLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(107, 114, 128),
        };

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 0,
            Padding = new Padding(0, 4, 0, 0),
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));

        _generalHeader = CreateSectionHeader();
        _outputNameLabel = CreateFieldLabel();
        _outputNameBox = new TextBox { Dock = DockStyle.Fill };
        _sourceLabel = CreateFieldLabel();
        _sourceValue = CreateValueLabel();
        _typeLabel = CreateFieldLabel();
        _typeValue = CreateValueLabel();
        _outputPathLabel = CreateFieldLabel();
        _outputPathValue = CreateValueLabel();

        _textureHeader = CreateSectionHeader();
        _formatLabel = CreateFieldLabel();
        _formatCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
        };
        _mipmapsCheck = CreateCheckBox();
        _premultiplyCheck = CreateCheckBox();
        _resizeCheck = CreateCheckBox();
        _squareCheck = CreateCheckBox();
        _colorKeyCheck = CreateCheckBox();
        _colorKeyColorLabel = CreateFieldLabel();
        _colorButton = new Button { AutoSize = true, Height = 24, Margin = new Padding(4, 0, 0, 0) };
        _colorSwatch = new Panel
        {
            Width = 30,
            Height = 22,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(3, 2, 3, 2),
        };
        _colorRow = CreateColorRow();
        _textureHint = CreateHintLabel();

        _fontHeader = CreateSectionHeader();
        _fontFormatLabel = CreateFieldLabel();
        _fontFormatCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
        };
        _fontPremultiplyCheck = CreateCheckBox();
        _fontHint = CreateHintLabel();

        _audioHeader = CreateSectionHeader();
        _audioNote = CreateHintLabel();

        _songHeader = CreateSectionHeader();
        _songNote = CreateHintLabel();

        AddRow(_generalHeader, null, span: true);
        AddRow(_outputNameLabel, _outputNameBox);
        AddRow(_sourceLabel, _sourceValue);
        AddRow(_typeLabel, _typeValue);
        AddRow(_outputPathLabel, _outputPathValue);

        AddRow(_textureHeader, null, span: true);
        AddRow(_formatLabel, _formatCombo);
        AddRow(null, _mipmapsCheck, spanControl: true);
        AddRow(null, _premultiplyCheck, spanControl: true);
        AddRow(null, _resizeCheck, spanControl: true);
        AddRow(null, _squareCheck, spanControl: true);
        AddRow(null, _colorKeyCheck, spanControl: true);
        AddRow(_colorKeyColorLabel, _colorRow);
        AddRow(_textureHint, null, span: true);

        AddRow(_fontHeader, null, span: true);
        AddRow(_fontFormatLabel, _fontFormatCombo);
        AddRow(null, _fontPremultiplyCheck, spanControl: true);
        AddRow(_fontHint, null, span: true);

        AddRow(_audioHeader, null, span: true);
        AddRow(_audioNote, null, span: true);

        AddRow(_songHeader, null, span: true);
        AddRow(_songNote, null, span: true);

        Controls.Add(_layout);
        Controls.Add(_emptyLabel);
        Controls.Add(_summaryLabel);
        Controls.Add(_titleLabel);

        _outputNameBox.Leave += (_, _) => ApplyOutputName();
        _outputNameBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                ApplyOutputName();
                e.SuppressKeyPress = true;
            }
        };

        _formatCombo.SelectedIndexChanged += (_, _) => ApplyTextureFormat();
        _fontFormatCombo.SelectedIndexChanged += (_, _) => ApplyFontFormat();

        _mipmapsCheck.CheckStateChanged += (_, _) => ApplyTextureFlag(TextureOption.Mipmaps);
        _premultiplyCheck.CheckStateChanged += (_, _) => ApplyTextureFlag(TextureOption.Premultiply);
        _resizeCheck.CheckStateChanged += (_, _) => ApplyTextureFlag(TextureOption.Resize);
        _squareCheck.CheckStateChanged += (_, _) => ApplyTextureFlag(TextureOption.Square);
        _colorKeyCheck.CheckStateChanged += (_, _) => ApplyTextureFlag(TextureOption.ColorKey);

        _colorButton.Click += (_, _) => ChooseColorKey();
        _colorSwatch.Click += (_, _) => ChooseColorKey();

        ApplyLocalization();
        SetSelection(Array.Empty<ContentItem>());
    }

    public event EventHandler? ChangesMade;

    /// <summary>
    /// Applies an edited output name through the owning project so uniqueness rules stay
    /// in one place. When unset only the format is validated.
    /// </summary>
    public delegate bool OutputNameCommitter(
        ContentItem item, string? outputName, out OutputNameError error);

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<ContentItem, string>? OutputPathResolver { get; set; }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public OutputNameCommitter? TryCommitOutputName { get; set; }

    public void ApplyLocalization()
    {
        _updating = true;
        _titleLabel.Text = _loc["details.title"];
        _emptyLabel.Text = _loc["details.noSelection"];

        _generalHeader.Text = _loc["details.section.general"];
        _outputNameLabel.Text = _loc["details.outputName"];
        _sourceLabel.Text = _loc["details.source"];
        _typeLabel.Text = _loc["details.type"];
        _outputPathLabel.Text = _loc["details.outputPath"];

        _textureHeader.Text = _loc["details.section.texture"];
        _formatLabel.Text = _loc["details.format"];
        _mipmapsCheck.Text = _loc["details.mipmaps"];
        _premultiplyCheck.Text = _loc["details.premultiply"];
        _resizeCheck.Text = _loc["details.resizePot"];
        _squareCheck.Text = _loc["details.makeSquare"];
        _colorKeyCheck.Text = _loc["details.colorKey"];
        _colorKeyColorLabel.Text = _loc["details.colorKeyColor"];
        _colorButton.Text = _loc["details.chooseColor"];
        _textureHint.Text = _loc["details.hint.texture"];

        _fontHeader.Text = _loc["details.section.spriteFont"];
        _fontFormatLabel.Text = _loc["details.fontFormat"];
        _fontPremultiplyCheck.Text = _loc["details.fontPremultiply"];
        _fontHint.Text = _loc["details.hint.font"];

        _audioHeader.Text = _loc["details.section.audio"];
        _audioNote.Text = _loc["details.audioNote"];

        _songHeader.Text = _loc["details.section.song"];
        _songNote.Text = _loc["details.songNote"];

        RebuildFormatCombo();
        RebuildFontFormatCombo();

        UpdateHeaderFonts();
        _updating = false;
        RefreshFromSelection();
    }

    public void SetEditingEnabled(bool enabled)
    {
        _layout.Enabled = enabled;
    }

    /// <summary>
    /// Graphics profile of the owning project. Reach limits DXT compression to
    /// power-of-two dimensions; HiDef lifts that restriction.
    /// </summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public GraphicsProfileSetting Profile { get; set; } = GraphicsProfileSetting.Reach;

    public void SetSelection(IReadOnlyList<ContentItem> items)
    {
        _items = items;
        RefreshFromSelection();
    }

    public bool SelectedItemsContain(ContentItem item)
    {
        return _items.Contains(item);
    }

    public void RefreshFromSelection()
    {
        _updating = true;

        var count = _items.Count;
        var single = count == 1 ? _items[0] : null;
        var textures = _items.Where(x => x.Kind == ContentKind.Texture).ToList();
        var fonts = _items.Where(x => x.Kind == ContentKind.SpriteFont).ToList();
        var sounds = _items.Where(x => x.Kind == ContentKind.SoundEffect).ToList();
        var songs = _items.Where(x => x.Kind == ContentKind.Song).ToList();
        var unsupported = _items.Where(x => x.Kind == ContentKind.Unsupported).ToList();

        _emptyLabel.Visible = count == 0;
        _layout.Visible = count > 0;
        _summaryLabel.Visible = count > 1;
        if (count > 1)
            _summaryLabel.Text = _loc.Format("details.multiple", count);
        else if (single != null)
            _summaryLabel.Text = string.Empty;

        var showGeneral = single != null;
        SetRowVisible(_outputNameLabel, showGeneral);
        SetRowVisible(_outputNameBox, showGeneral);
        SetRowVisible(_sourceLabel, showGeneral);
        SetRowVisible(_sourceValue, showGeneral);
        SetRowVisible(_typeLabel, showGeneral);
        SetRowVisible(_typeValue, showGeneral);
        SetRowVisible(_outputPathLabel, showGeneral);
        SetRowVisible(_outputPathValue, showGeneral);

        if (single != null)
        {
            _outputNameBox.Text = single.OutputName;
            _sourceValue.Text = single.SourcePath;
            _typeValue.Text = KindText(single.Kind);
            _outputPathValue.Text = OutputPathResolver?.Invoke(single) ?? string.Empty;
            _errorProvider.SetError(_outputNameBox, string.Empty);
        }

        var showTexture = textures.Count > 0;
        SetSectionVisible(_textureHeader, showTexture);
        SetRowVisible(_formatLabel, showTexture);
        SetRowVisible(_formatCombo, showTexture);
        SetRowVisible(_mipmapsCheck, showTexture);
        SetRowVisible(_premultiplyCheck, showTexture);
        SetRowVisible(_resizeCheck, showTexture);
        SetRowVisible(_squareCheck, showTexture);
        SetRowVisible(_colorKeyCheck, showTexture);
        SetRowVisible(_colorKeyColorLabel, showTexture);
        SetRowVisible(_colorRow, showTexture);
        SetRowVisible(_textureHint, showTexture);

        if (showTexture)
        {
            SetTriState(_mipmapsCheck, textures.Select(x => x.Texture.GenerateMipmaps));
            SetTriState(_premultiplyCheck, textures.Select(x => x.Texture.PremultiplyAlpha));
            SetTriState(_resizeCheck, textures.Select(x => x.Texture.ResizeToPowerOfTwo));
            SetTriState(_squareCheck, textures.Select(x => x.Texture.MakeSquare));
            SetTriState(_colorKeyCheck, textures.Select(x => x.Texture.ColorKeyEnabled));
            SetComboValue(_formatCombo, Distinct(textures.Select(x => x.Texture.OutputFormat)));
            UpdateColorSwatch(textures);
            UpdateTextureHint(textures);
        }

        var showFont = fonts.Count > 0;
        SetSectionVisible(_fontHeader, showFont);
        SetRowVisible(_fontFormatLabel, showFont);
        SetRowVisible(_fontFormatCombo, showFont);
        SetRowVisible(_fontPremultiplyCheck, showFont);
        SetRowVisible(_fontHint, showFont);

        if (showFont)
        {
            SetTriState(_fontPremultiplyCheck, fonts.Select(x => x.SpriteFont.PremultiplyAlpha));
            SetComboValue(_fontFormatCombo, Distinct(fonts.Select(x => x.SpriteFont.TextureFormat)));
        }

        var showSound = sounds.Count > 0;
        SetSectionVisible(_audioHeader, showSound);
        SetRowVisible(_audioNote, showSound);

        var showSong = songs.Count > 0;
        SetSectionVisible(_songHeader, showSong);
        SetRowVisible(_songNote, showSong);

        if (unsupported.Count > 0 && count == unsupported.Count)
        {
            _textureHint.Visible = true;
            _textureHint.Text = _loc["kind.unsupported"];
        }

        _updating = false;
    }

    private void ApplyOutputName()
    {
        if (_updating || _items.Count != 1)
            return;

        var item = _items[0];
        var text = _outputNameBox.Text;

        if (TryCommitOutputName != null)
        {
            if (!TryCommitOutputName(item, text, out var error))
            {
                var message = error switch
                {
                    OutputNameError.Duplicate => _loc["error.duplicateOutput"],
                    OutputNameError.Invalid => _loc["error.invalidOutputName"],
                    _ => _loc["error.invalidOutputName"],
                };

                _errorProvider.SetError(_outputNameBox, message);
                _outputNameBox.Text = item.OutputName;
                return;
            }

            _errorProvider.SetError(_outputNameBox, string.Empty);
            _outputNameBox.Text = item.OutputName;
            _outputPathValue.Text = OutputPathResolver?.Invoke(item) ?? string.Empty;
            ChangesMade?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (!OutputNameValidator.TryNormalize(text, out var normalized))
        {
            _errorProvider.SetError(_outputNameBox, _loc["error.invalidOutputName"]);
            return;
        }

        _errorProvider.SetError(_outputNameBox, string.Empty);
        _outputNameBox.Text = normalized;

        if (!string.Equals(item.OutputName, normalized, StringComparison.OrdinalIgnoreCase))
        {
            item.OutputName = normalized;
            _outputPathValue.Text = OutputPathResolver?.Invoke(item) ?? string.Empty;
            ChangesMade?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyTextureFormat()
    {
        if (_updating || _formatCombo.SelectedItem is not ComboBoxItem { Value: TextureOutputFormat format })
            return;

        foreach (var item in _items.Where(x => x.Kind == ContentKind.Texture))
            item.Texture.OutputFormat = format;

        UpdateTextureHint(_items.Where(x => x.Kind == ContentKind.Texture).ToList());
        ChangesMade?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyFontFormat()
    {
        if (_updating || _fontFormatCombo.SelectedItem is not ComboBoxItem { Value: SpriteFontTextureFormat format })
            return;

        foreach (var item in _items.Where(x => x.Kind == ContentKind.SpriteFont))
            item.SpriteFont.TextureFormat = format;

        ChangesMade?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyTextureFlag(TextureOption option)
    {
        if (_updating)
            return;

        var textures = _items.Where(x => x.Kind == ContentKind.Texture).ToList();
        if (textures.Count == 0)
            return;

        var check = option switch
        {
            TextureOption.Mipmaps => _mipmapsCheck,
            TextureOption.Premultiply => _premultiplyCheck,
            TextureOption.Resize => _resizeCheck,
            TextureOption.Square => _squareCheck,
            _ => _colorKeyCheck,
        };

        if (check.CheckState == CheckState.Indeterminate)
            return;

        var value = check.CheckState == CheckState.Checked;
        foreach (var item in textures)
        {
            switch (option)
            {
                case TextureOption.Mipmaps:
                    item.Texture.GenerateMipmaps = value;
                    break;
                case TextureOption.Premultiply:
                    item.Texture.PremultiplyAlpha = value;
                    break;
                case TextureOption.Resize:
                    item.Texture.ResizeToPowerOfTwo = value;
                    break;
                case TextureOption.Square:
                    item.Texture.MakeSquare = value;
                    break;
                case TextureOption.ColorKey:
                    item.Texture.ColorKeyEnabled = value;
                    break;
            }
        }

        UpdateTextureHint(textures);
        ChangesMade?.Invoke(this, EventArgs.Empty);
    }

    private void ChooseColorKey()
    {
        var textures = _items.Where(x => x.Kind == ContentKind.Texture).ToList();
        if (textures.Count == 0)
            return;

        using var dialog = new ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            Color = ParseColor(textures[0].Texture.ColorKeyColor),
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var value = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        foreach (var item in textures)
            item.Texture.ColorKeyColor = value;

        UpdateColorSwatch(textures);
        ChangesMade?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateColorSwatch(IReadOnlyList<ContentItem> textures)
    {
        var colors = Distinct(textures.Select(x => x.Texture.ColorKeyColor.ToUpperInvariant())).ToList();
        _colorSwatch.BackColor = colors.Count == 1
            ? ParseColor(colors[0])
            : Color.FromArgb(212, 212, 212);
        _colorButton.Enabled = textures.Count > 0;
    }

    private void UpdateTextureHint(IReadOnlyList<ContentItem> textures)
    {
        var needsPot = Profile == GraphicsProfileSetting.Reach
            && textures.Any(x => x.Texture.OutputFormat == TextureOutputFormat.DxtCompressed
                && !x.Texture.ResizeToPowerOfTwo);
        _textureHint.Text = needsPot ? _loc["details.hint.dxt"] : _loc["details.hint.texture"];
        _textureHint.ForeColor = needsPot
            ? Color.FromArgb(180, 83, 9)
            : Color.FromArgb(107, 114, 128);
    }

    private void RebuildFormatCombo()
    {
        var previous = _formatCombo.SelectedItem;
        _formatCombo.Items.Clear();
        _formatCombo.Items.Add(new ComboBoxItem(_loc["details.mixed"], null));
        _formatCombo.Items.Add(new ComboBoxItem(_loc["details.format.color"], TextureOutputFormat.Color));
        _formatCombo.Items.Add(new ComboBoxItem(_loc["details.format.dxt"], TextureOutputFormat.DxtCompressed));
        _formatCombo.Items.Add(new ComboBoxItem(_loc["details.format.noChange"], TextureOutputFormat.NoChange));
        if (previous != null)
            _formatCombo.SelectedIndex = 0;
    }

    private void RebuildFontFormatCombo()
    {
        _fontFormatCombo.Items.Clear();
        _fontFormatCombo.Items.Add(new ComboBoxItem(_loc["details.mixed"], null));
        _fontFormatCombo.Items.Add(new ComboBoxItem(_loc["details.fontFormat.compressed"], SpriteFontTextureFormat.Compressed));
        _fontFormatCombo.Items.Add(new ComboBoxItem(_loc["details.fontFormat.color"], SpriteFontTextureFormat.Color));
        _fontFormatCombo.SelectedIndex = 0;
    }

    private void SetComboValue<T>(ComboBox combo, IReadOnlyList<T> values)
    {
        if (values.Count == 1)
        {
            for (var index = 0; index < combo.Items.Count; index++)
            {
                if (combo.Items[index] is ComboBoxItem item && item.Value is T typed
                    && EqualityComparer<T>.Default.Equals(typed, values[0]))
                {
                    combo.SelectedIndex = index;
                    return;
                }
            }
        }

        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static IReadOnlyList<T> Distinct<T>(IEnumerable<T> values)
    {
        return values.Distinct().ToList();
    }

    private static void SetTriState(CheckBox check, IEnumerable<bool> values)
    {
        var list = values.ToList();
        check.ThreeState = false;

        if (list.Count == 0)
        {
            check.CheckState = CheckState.Indeterminate;
            check.ThreeState = true;
            return;
        }

        if (list.All(x => x))
            check.CheckState = CheckState.Checked;
        else if (list.All(x => !x))
            check.CheckState = CheckState.Unchecked;
        else
        {
            check.ThreeState = true;
            check.CheckState = CheckState.Indeterminate;
        }
    }

    private static Color ParseColor(string value)
    {
        var text = (value ?? string.Empty).Trim().TrimStart('#');
        if (text.Length == 6
            && int.TryParse(text, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var rgb))
        {
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        return Color.FromArgb(255, 0, 255);
    }

    private string KindText(ContentKind kind)
    {
        return kind switch
        {
            ContentKind.Texture => _loc["kind.texture"],
            ContentKind.SpriteFont => _loc["kind.spriteFont"],
            ContentKind.SoundEffect => _loc["kind.soundEffect"],
            ContentKind.Song => _loc["kind.song"],
            _ => _loc["kind.unsupported"],
        };
    }

    private void SetRowVisible(Control? control, bool visible)
    {
        if (control != null)
            control.Visible = visible;
    }

    private void SetSectionVisible(Label header, bool visible)
    {
        header.Visible = visible;
    }

    private FlowLayoutPanel CreateColorRow()
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };
        row.Controls.Add(_colorSwatch);
        row.Controls.Add(_colorButton);
        return row;
    }

    private void AddRow(Control? label, Control? control, bool span = false, bool spanControl = false)
    {
        var row = _layout.RowCount++;
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        if (label != null)
            _layout.Controls.Add(label, 0, row);

        if (control != null)
        {
            if (label == null && spanControl)
            {
                _layout.Controls.Add(control, 0, row);
                _layout.SetColumnSpan(control, 2);
            }
            else
            {
                _layout.Controls.Add(control, 1, row);
            }
        }
        else if (label != null && !spanControl)
        {
            _layout.SetColumnSpan(label, 2);
        }

        if (span)
        {
            if (label != null)
                _layout.SetColumnSpan(label, 2);
            if (control != null)
                _layout.SetColumnSpan(control, 2);
        }

        // Give hint labels and section headers some breathing room.
        if (label is Label && control == null)
            label.Margin = new Padding(3, 8, 3, 4);
    }

    private static Label CreateSectionHeader()
    {
        return new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(37, 99, 235),
            Margin = new Padding(3, 12, 3, 4),
        };
    }

    private static Label CreateFieldLabel()
    {
        return new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(55, 65, 81),
            Margin = new Padding(3, 6, 6, 3),
        };
    }

    private static Label CreateValueLabel()
    {
        return new Label
        {
            AutoSize = false,
            Height = 20,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(107, 114, 128),
            Margin = new Padding(3, 4, 3, 3),
        };
    }

    private static Label CreateHintLabel()
    {
        return new Label
        {
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            ForeColor = Color.FromArgb(107, 114, 128),
            Margin = new Padding(3, 6, 3, 4),
        };
    }

    private static CheckBox CreateCheckBox()
    {
        return new CheckBox
        {
            AutoSize = true,
            Margin = new Padding(3, 6, 3, 3),
        };
    }

    private void UpdateHeaderFonts()
    {
        var previousTitle = _titleFont;
        var previousSection = _sectionFont;

        _titleFont = new Font(Font.FontFamily, 11F, FontStyle.Bold);
        _sectionFont = new Font(Font, FontStyle.Bold);

        _titleLabel.Font = _titleFont;
        _generalHeader.Font = _sectionFont;
        _textureHeader.Font = _sectionFont;
        _fontHeader.Font = _sectionFont;
        _audioHeader.Font = _sectionFont;
        _songHeader.Font = _sectionFont;

        previousTitle?.Dispose();
        previousSection?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont?.Dispose();
            _sectionFont?.Dispose();
            _errorProvider.Dispose();
        }

        base.Dispose(disposing);
    }

    private enum TextureOption
    {
        Mipmaps,
        Premultiply,
        Resize,
        Square,
        ColorKey,
    }

    private sealed class ComboBoxItem
    {
        public ComboBoxItem(string text, object? value)
        {
            Text = text;
            Value = value;
        }

        public string Text { get; }
        public object? Value { get; }

        public override string ToString() => Text;
    }
}
