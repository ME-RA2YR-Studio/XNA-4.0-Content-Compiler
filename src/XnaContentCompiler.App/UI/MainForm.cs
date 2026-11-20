using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XnaContentCompiler.App.Localization;
using XnaContentCompiler.App.Settings;
using XnaContentCompiler.Core.Building;
using XnaContentCompiler.Core.Content;

namespace XnaContentCompiler.App.UI;

public sealed class MainForm : Form
{
    private static readonly Color Accent = Color.FromArgb(37, 99, 235);
    private static readonly Color Muted = Color.FromArgb(107, 114, 128);
    private static readonly Color SuccessColor = Color.FromArgb(21, 128, 61);
    private static readonly Color DangerColor = Color.FromArgb(185, 28, 28);
    private static readonly Color WarningColor = Color.FromArgb(180, 83, 9);

    private readonly LocalizationService _loc = LocalizationService.Instance;
    private readonly AppSettings _settings;
    private readonly ContentBuildService _buildService = new();
    private readonly Dictionary<ToolStripItem, string> _textKeys = new();
    private readonly Dictionary<Guid, ItemUiState> _itemStates = new();
    private readonly Dictionary<Guid, ItemBuildResult> _itemResults = new();

    private ContentProject _project = new();
    private string? _startupProjectPath;
    private bool _dirty;
    private bool _building;
    private bool _refreshingGrid;
    private bool _applyingOutputDirectory;
    private CancellationTokenSource? _cts;

    private MenuStrip _menu = null!;
    private ToolStrip _toolStrip = null!;
    private StatusStrip _statusStrip = null!;
    private SplitContainer _mainSplit = null!;
    private SplitContainer _contentSplit = null!;
    private TableLayoutPanel _contentLayout = null!;
    private DataGridView _grid = null!;
    private string _emptyHintText = string.Empty;
    private TextBox _outputBox = null!;
    private Label _outputLabel = null!;
    private Button _browseOutput = null!;
    private Label _itemsTitle = null!;
    private ItemDetailsPanel _details = null!;
    private RichTextBox _log = null!;
    private Label _logTitle = null!;
    private Button _clearLog = null!;
    private Button _saveLog = null!;
    private ContextMenuStrip _gridMenu = null!;
    private ToolStripMenuItem _contextBuildSelected = null!;
    private ToolStripMenuItem _contextRevealSource = null!;
    private ToolStripMenuItem _contextCopySourcePath = null!;
    private ToolStripMenuItem _contextCopyOutputPath = null!;
    private ToolStripMenuItem _contextRemove = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private ToolStripProgressBar _progress = null!;
    private ToolStripStatusLabel _countLabel = null!;

    private ToolStripMenuItem _fileMenu = null!;
    private ToolStripMenuItem _editMenu = null!;
    private ToolStripMenuItem _buildMenu = null!;
    private ToolStripMenuItem _languageMenu = null!;
    private ToolStripMenuItem _helpMenu = null!;
    private ToolStripMenuItem _recentMenu = null!;
    private ToolStripMenuItem _menuNew = null!;
    private ToolStripMenuItem _menuOpen = null!;
    private ToolStripMenuItem _menuSave = null!;
    private ToolStripMenuItem _menuSaveAs = null!;
    private ToolStripMenuItem _menuExit = null!;
    private ToolStripMenuItem _menuAddFiles = null!;
    private ToolStripMenuItem _menuAddFolder = null!;
    private ToolStripMenuItem _menuRemove = null!;
    private ToolStripMenuItem _menuClear = null!;
    private ToolStripMenuItem _menuBuildNow = null!;
    private ToolStripMenuItem _menuBuildSelected = null!;
    private ToolStripMenuItem _menuCancelBuild = null!;
    private ToolStripMenuItem _profileMenu = null!;
    private ToolStripMenuItem _menuProfileReach = null!;
    private ToolStripMenuItem _menuProfileHiDef = null!;
    private ToolStripMenuItem _menuOpenOutput = null!;
    private ToolStripMenuItem _menuEnglish = null!;
    private ToolStripMenuItem _menuChinese = null!;
    private ToolStripMenuItem _menuUsage = null!;
    private ToolStripMenuItem _menuAbout = null!;

    private ToolStripButton _toolNew = null!;
    private ToolStripButton _toolOpen = null!;
    private ToolStripButton _toolSave = null!;
    private ToolStripButton _toolAddFiles = null!;
    private ToolStripButton _toolAddFolder = null!;
    private ToolStripButton _toolRemove = null!;
    private ToolStripButton _toolBuild = null!;
    private ToolStripButton _toolBuildSelected = null!;
    private ToolStripButton _toolCancel = null!;
    private ToolStripButton _toolOpenOutput = null!;

    public MainForm(AppSettings settings)
    {
        _settings = settings;

        BuildUi();
        AppIcon.Apply(this);
        _loc.LanguageChanged += (_, _) => ApplyLocalization();
        SubscribeBuildEvents();

        ApplyLocalization();
        CreateNewProject(skipConfirmation: true);
        SetBuildingUi(false);
    }

    public void OpenProjectOnStartup(string path)
    {
        _startupProjectPath = path;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        RestoreWindowPlacement();
        ConfigureSplitters();
    }

    private void ConfigureSplitters()
    {
        if (_mainSplit.Height >= 520)
        {
            _mainSplit.Panel1MinSize = 300;
            _mainSplit.Panel2MinSize = 150;
            var max = _mainSplit.Height - _mainSplit.Panel2MinSize - _mainSplit.SplitterWidth;
            var desired = (int)(_mainSplit.Height * 0.72);
            _mainSplit.SplitterDistance = Math.Clamp(desired, _mainSplit.Panel1MinSize, max);
        }

        if (_contentSplit.Width >= 820)
        {
            _contentSplit.Panel1MinSize = 480;
            _contentSplit.Panel2MinSize = 300;
            var max = _contentSplit.Width - _contentSplit.Panel2MinSize - _contentSplit.SplitterWidth;
            var desired = (int)(_contentSplit.Width * 0.67);
            _contentSplit.SplitterDistance = Math.Clamp(desired, _contentSplit.Panel1MinSize, max);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (!string.IsNullOrWhiteSpace(_startupProjectPath))
        {
            var path = _startupProjectPath;
            _startupProjectPath = null;
            LoadProject(path, confirmSave: false);
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        // Re-evaluate per-row status so files deleted or restored outside the tool are
        // reflected as soon as the window regains focus.
        if (_building || _refreshingGrid || _grid.IsCurrentCellInEditMode)
            return;

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is ContentItem item)
                UpdateGridRow(item, row);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_building)
        {
            var answer = MessageBox.Show(
                this,
                _loc["dialog.closeDuringBuild.text"],
                _loc["dialog.closeDuringBuild.title"],
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (answer != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _cts?.Cancel();
        }

        if (!ConfirmSaveChanges())
        {
            e.Cancel = true;
            return;
        }

        SaveWindowPlacement();
        TrySaveSettings();
        base.OnFormClosing(e);
    }

    private void BuildUi()
    {
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;

        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1180, 780);
        ClientSize = new Size(
            Math.Min(1180, Math.Max(940, workingArea.Width - 80)),
            Math.Min(780, Math.Max(640, workingArea.Height - 80)));
        MinimumSize = new Size(940, 640);
        KeyPreview = true;
        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        _menu = BuildMenu();
        _toolStrip = BuildToolBar();
        _statusStrip = BuildStatusStrip();
        _mainSplit = BuildMainSplit();

        Controls.Add(_mainSplit);
        Controls.Add(_toolStrip);
        Controls.Add(_menu);
        Controls.Add(_statusStrip);

        ResumeLayout(performLayout: true);
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip
        {
            Dock = DockStyle.Top,
            Padding = new Padding(4, 2, 0, 2),
        };

        _fileMenu = new ToolStripMenuItem();
        _menuNew = AddMenuItem(_fileMenu, "menu.newProject", (_, _) => CreateNewProject(skipConfirmation: false),
            Keys.Control | Keys.N);
        _menuOpen = AddMenuItem(_fileMenu, "menu.openProject", (_, _) => OpenProjectDialog(),
            Keys.Control | Keys.O);
        _menuSave = AddMenuItem(_fileMenu, "menu.saveProject", (_, _) => SaveProject(),
            Keys.Control | Keys.S);
        _menuSaveAs = AddMenuItem(_fileMenu, "menu.saveProjectAs", (_, _) => SaveProjectAs(),
            Keys.Control | Keys.Shift | Keys.S);
        _fileMenu.DropDownItems.Add(new ToolStripSeparator());
        _recentMenu = new ToolStripMenuItem();
        _fileMenu.DropDownItems.Add(_recentMenu);
        _fileMenu.DropDownItems.Add(new ToolStripSeparator());
        _menuAddFiles = AddMenuItem(_fileMenu, "menu.addFiles", (_, _) => AddFilesDialog());
        _menuAddFolder = AddMenuItem(_fileMenu, "menu.addFolder", (_, _) => AddFolderDialog());
        _fileMenu.DropDownItems.Add(new ToolStripSeparator());
        _menuExit = AddMenuItem(_fileMenu, "menu.exit", (_, _) => Close());

        _editMenu = new ToolStripMenuItem();
        _menuRemove = AddMenuItem(_editMenu, "menu.removeSelected", (_, _) => RemoveSelectedItems());
        _menuClear = AddMenuItem(_editMenu, "menu.clearItems", (_, _) => ClearAllItems());

        _buildMenu = new ToolStripMenuItem();
        _menuBuildNow = AddMenuItem(_buildMenu, "menu.buildNow", (_, _) => StartBuildAsync(),
            Keys.F6);
        _menuBuildSelected = AddMenuItem(_buildMenu, "menu.buildSelected",
            (_, _) => StartBuildAsync(GetSelectedItems()),
            Keys.Control | Keys.Shift | Keys.B);
        _menuCancelBuild = AddMenuItem(_buildMenu, "menu.cancelBuild", (_, _) => CancelBuild());
        _buildMenu.DropDownItems.Add(new ToolStripSeparator());
        _profileMenu = new ToolStripMenuItem();
        _textKeys[_profileMenu] = "menu.profile";
        _menuProfileReach = AddMenuItem(_profileMenu, "menu.profileReach",
            (_, _) => SetProfile(GraphicsProfileSetting.Reach));
        _menuProfileHiDef = AddMenuItem(_profileMenu, "menu.profileHiDef",
            (_, _) => SetProfile(GraphicsProfileSetting.HiDef));
        _buildMenu.DropDownItems.Add(_profileMenu);
        _buildMenu.DropDownItems.Add(new ToolStripSeparator());
        _menuOpenOutput = AddMenuItem(_buildMenu, "menu.openOutputFolder", (_, _) => OpenOutputFolder());

        _languageMenu = new ToolStripMenuItem();
        _menuEnglish = AddMenuItem(_languageMenu, "menu.languageEnglish",
            (_, _) => ChangeLanguage(AppLanguage.English));
        _menuChinese = AddMenuItem(_languageMenu, "menu.languageChinese",
            (_, _) => ChangeLanguage(AppLanguage.ChineseSimplified));

        _helpMenu = new ToolStripMenuItem();
        _menuUsage = AddMenuItem(_helpMenu, "menu.usage", (_, _) => ShowUsage());
        _menuAbout = AddMenuItem(_helpMenu, "menu.about", (_, _) => ShowAbout());

        _textKeys[_fileMenu] = "menu.file";
        _textKeys[_editMenu] = "menu.edit";
        _textKeys[_buildMenu] = "menu.build";
        _textKeys[_languageMenu] = "menu.language";
        _textKeys[_helpMenu] = "menu.help";

        menu.Items.AddRange(new ToolStripItem[]
        {
            _fileMenu, _editMenu, _buildMenu, _languageMenu, _helpMenu,
        });

        return menu;
    }

    private ToolStrip BuildToolBar()
    {
        var toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(8, 4, 8, 4),
            RenderMode = ToolStripRenderMode.System,
        };

        _toolNew = AddToolButton(toolStrip, "toolbar.new", (_, _) => CreateNewProject(skipConfirmation: false));
        _toolOpen = AddToolButton(toolStrip, "toolbar.open", (_, _) => OpenProjectDialog());
        _toolSave = AddToolButton(toolStrip, "toolbar.save", (_, _) => SaveProject());
        toolStrip.Items.Add(new ToolStripSeparator());
        _toolAddFiles = AddToolButton(toolStrip, "toolbar.addFiles", (_, _) => AddFilesDialog());
        _toolAddFolder = AddToolButton(toolStrip, "toolbar.addFolder", (_, _) => AddFolderDialog());
        _toolRemove = AddToolButton(toolStrip, "toolbar.remove", (_, _) => RemoveSelectedItems());
        toolStrip.Items.Add(new ToolStripSeparator());
        _toolBuild = AddToolButton(toolStrip, "toolbar.build", (_, _) => StartBuildAsync());
        _toolBuildSelected = AddToolButton(toolStrip, "toolbar.buildSelected",
            (_, _) => StartBuildAsync(GetSelectedItems()));
        _toolCancel = AddToolButton(toolStrip, "toolbar.cancel", (_, _) => CancelBuild());
        toolStrip.Items.Add(new ToolStripSeparator());
        _toolOpenOutput = AddToolButton(toolStrip, "toolbar.openOutput", (_, _) => OpenOutputFolder());

        return toolStrip;
    }

    private StatusStrip BuildStatusStrip()
    {
        var strip = new StatusStrip { SizingGrip = true };

        _statusLabel = new ToolStripStatusLabel
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _progress = new ToolStripProgressBar
        {
            Style = ProgressBarStyle.Blocks,
            Visible = false,
            Width = 180,
        };
        _countLabel = new ToolStripStatusLabel
        {
            BorderSides = ToolStripStatusLabelBorderSides.Left,
        };

        strip.Items.AddRange(new ToolStripItem[] { _statusLabel, _progress, _countLabel });
        return strip;
    }

    private SplitContainer BuildMainSplit()
    {
        var main = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
        };

        _contentSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
        };

        _contentLayout = BuildContentPanel();
        _contentSplit.Panel1.Controls.Add(_contentLayout);

        var detailsHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            BackColor = Color.FromArgb(229, 231, 235),
        };
        _details = new ItemDetailsPanel
        {
            Dock = DockStyle.Fill,
        };
        _details.OutputPathResolver = item =>
        {
            if (string.IsNullOrWhiteSpace(_project.OutputDirectory))
                return _loc["project.outputNotSet"];

            try
            {
                return _project.GetOutputPath(item);
            }
            catch (ArgumentException)
            {
                return _loc["error.invalidOutputName"];
            }
        };
        _details.ChangesMade += (_, _) => OnDetailsChanged();
        _details.TryCommitOutputName = (ContentItem item, string? name, out OutputNameError error)
            => _project.TryRenameItem(item, name, out error);
        detailsHost.Controls.Add(_details);
        _contentSplit.Panel2.Controls.Add(detailsHost);

        main.Panel1.Controls.Add(_contentSplit);
        main.Panel2.Controls.Add(BuildLogPanel());

        return main;
    }

    private TableLayoutPanel BuildContentPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8, 6, 8, 0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(BuildOutputBar(), 0, 0);
        layout.Controls.Add(BuildItemsHeader(), 0, 1);

        var gridHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
        _grid = CreateGrid();
        gridHost.Controls.Add(_grid);
        layout.Controls.Add(gridHost, 0, 2);

        return layout;
    }

    private Panel BuildOutputBar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 34,
            Margin = new Padding(0, 0, 0, 4),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _outputLabel = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 8, 0),
        };
        _outputBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 8, 4),
        };
        _outputBox.Leave += (_, _) => CommitOutputDirectoryText();
        _outputBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitOutputDirectoryText();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _outputBox.Text = _project.OutputDirectory ?? string.Empty;
                e.SuppressKeyPress = true;
            }
        };
        _browseOutput = new Button
        {
            AutoSize = true,
            Height = 26,
            MinimumSize = new Size(88, 26),
            Margin = new Padding(0, 3, 0, 3),
        };
        _browseOutput.Click += (_, _) => BrowseOutputDirectory();

        layout.Controls.Add(_outputLabel, 0, 0);
        layout.Controls.Add(_outputBox, 1, 0);
        layout.Controls.Add(_browseOutput, 2, 0);
        panel.Controls.Add(layout);

        return panel;
    }

    private Panel BuildItemsHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 28,
            Margin = new Padding(0),
        };
        _itemsTitle = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(2, 0, 0, 0),
            ForeColor = Color.FromArgb(55, 65, 81),
        };
        panel.Controls.Add(_itemsTitle);
        return panel;
    }

    private DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            EnableHeadersVisualStyles = false,
            MultiSelect = true,
            ReadOnly = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AllowDrop = true,
        };
        grid.ColumnHeadersHeight = 30;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.RowTemplate.Height = 26;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(243, 244, 246);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(55, 65, 81);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(17, 24, 39);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);

        var nameColumn = new DataGridViewTextBoxColumn
        {
            Name = "Name",
            FillWeight = 26,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        var typeColumn = new DataGridViewTextBoxColumn
        {
            Name = "Type",
            FillWeight = 14,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = true,
        };
        var sourceColumn = new DataGridViewTextBoxColumn
        {
            Name = "Source",
            FillWeight = 40,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = true,
        };
        var statusColumn = new DataGridViewTextBoxColumn
        {
            Name = "Status",
            FillWeight = 14,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = true,
        };
        var sizeColumn = new DataGridViewTextBoxColumn
        {
            Name = "Size",
            FillWeight = 10,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleRight,
            },
        };

        grid.Columns.AddRange(nameColumn, typeColumn, sourceColumn, statusColumn, sizeColumn);
        grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        grid.ContextMenuStrip = BuildGridContextMenu();

        grid.SelectionChanged += (_, _) => UpdateSelection();
        grid.CellBeginEdit += (_, e) =>
        {
            if (_building || e.ColumnIndex != 0)
                e.Cancel = true;
        };
        grid.CellEndEdit += (_, e) => CommitOutputNameEdit(e.RowIndex);
        grid.CellDoubleClick += (_, e) =>
        {
            if (!_building && e.RowIndex >= 0 && e.ColumnIndex == 0)
                grid.BeginEdit(selectAll: true);
        };
        grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F2 && !_building
                && grid.CurrentCell != null && grid.CurrentCell.ColumnIndex == 0)
            {
                grid.BeginEdit(selectAll: true);
                e.Handled = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.A)
            {
                grid.SelectAll();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Delete && !_building && grid.SelectedRows.Count > 0)
            {
                RemoveSelectedItems();
                e.Handled = true;
            }
        };
        grid.DragEnter += OnDragEnter;
        grid.DragDrop += OnDragDrop;
        grid.Paint += (_, e) =>
        {
            if (grid.Rows.Count > 0)
                return;

            var area = grid.ClientRectangle;
            area.Y += grid.ColumnHeadersHeight;
            area.Height = Math.Max(0, area.Height - grid.ColumnHeadersHeight);
            TextRenderer.DrawText(
                e.Graphics,
                _emptyHintText,
                grid.Font,
                area,
                Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        };

        return grid;
    }

    private ContextMenuStrip BuildGridContextMenu()
    {
        _gridMenu = new ContextMenuStrip();

        _contextBuildSelected = new ToolStripMenuItem();
        _contextBuildSelected.Click += (_, _) => StartBuildAsync(GetSelectedItems());

        _contextRevealSource = new ToolStripMenuItem();
        _contextRevealSource.Click += (_, _) => RevealSelectedSource();

        _contextCopySourcePath = new ToolStripMenuItem();
        _contextCopySourcePath.Click += (_, _) =>
        {
            var item = GetSelectedItems().FirstOrDefault();
            if (item != null)
                CopyToClipboard(item.SourcePath);
        };

        _contextCopyOutputPath = new ToolStripMenuItem();
        _contextCopyOutputPath.Click += (_, _) =>
        {
            var item = GetSelectedItems().FirstOrDefault();
            if (item != null)
                CopyToClipboard(TryGetOutputPath(item));
        };

        _contextRemove = new ToolStripMenuItem();
        _contextRemove.Click += (_, _) => RemoveSelectedItems();

        _gridMenu.Items.AddRange(new ToolStripItem[]
        {
            _contextBuildSelected,
            new ToolStripSeparator(),
            _contextRevealSource,
            _contextCopySourcePath,
            _contextCopyOutputPath,
            new ToolStripSeparator(),
            _contextRemove,
        });

        _gridMenu.Opening += (_, _) =>
        {
            var items = GetSelectedItems();
            var hasSelection = items.Count > 0;

            _contextBuildSelected.Enabled = hasSelection && !_building;
            _contextRevealSource.Enabled = hasSelection;
            _contextCopySourcePath.Enabled = items.Count == 1;
            _contextCopyOutputPath.Enabled = items.Count == 1;
            _contextRemove.Enabled = hasSelection && !_building;
        };

        return _gridMenu;
    }

    private void CommitOutputNameEdit(int rowIndex)
    {
        if (_building || rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            return;

        var row = _grid.Rows[rowIndex];
        if (row.Tag is not ContentItem item)
            return;

        var entered = row.Cells[0].Value as string ?? string.Empty;
        var previous = item.OutputName;

        row.Cells[0].ErrorText = string.Empty;

        if (entered == previous)
            return;

        if (!_project.TryRenameItem(item, entered, out var error))
        {
            row.Cells[0].Value = previous;

            var message = error switch
            {
                OutputNameError.Duplicate => _loc["error.duplicateOutput"],
                OutputNameError.Invalid => _loc["error.invalidOutputName"],
                _ => _loc["error.invalidOutputName"],
            };

            // Cell-level error text shows a non-modal error glyph and tooltip.
            row.Cells[0].ErrorText = message;
            _statusLabel.Text = message;
            return;
        }

        row.Cells[0].ErrorText = string.Empty;
        row.Cells[0].Value = item.OutputName;

        _itemStates.Remove(item.Id);
        _itemResults.Remove(item.Id);
        _dirty = true;

        UpdateGridRow(item);
        if (_details.SelectedItemsContain(item))
            _details.SetSelection(GetSelectedItems());

        UpdateTitle();
        _statusLabel.Text = _loc["statusbar.ready"];
    }

    private void RevealSelectedSource()
    {
        var item = GetSelectedItems().FirstOrDefault();
        if (item == null)
            return;

        try
        {
            if (File.Exists(item.SourcePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{item.SourcePath}\"",
                    UseShellExecute = true,
                });
                return;
            }

            var directory = Path.GetDirectoryName(item.SourcePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true,
                });
                return;
            }

            ShowWarning(_loc.Format("error.sourceNotFound", item.SourcePath));
        }
        catch (Exception ex)
        {
            ShowWarning(_loc.Format("context.revealFailed", ex.Message));
        }
    }

    private void CopyToClipboard(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            Clipboard.SetText(text);
            _statusLabel.Text = _loc["context.copied"];
        }
        catch (Exception)
        {
            // The clipboard can be locked by another process; retrying is not useful here.
        }
    }

    private void SaveBuildLog()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = _loc["log.filter"],
            DefaultExt = "log",
            AddExtension = true,
            OverwritePrompt = true,
            Title = _loc["log.save"],
            FileName = BuildDefaultLogFileName(),
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            File.WriteAllText(dialog.FileName, _log.Text, new System.Text.UTF8Encoding(false));
            _statusLabel.Text = _loc.Format("log.saved", dialog.FileName);
        }
        catch (Exception ex)
        {
            ShowError(_loc.Format("log.saveFailed", ex.Message));
        }
    }

    private string BuildDefaultLogFileName()
    {
        var name = string.IsNullOrWhiteSpace(_project.Name) ? "build" : _project.Name;
        return $"{name}-{DateTime.Now:yyyyMMdd-HHmmss}.log";
    }

    private Panel BuildLogPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Window,
        };

        _logTitle = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            ForeColor = Color.FromArgb(55, 65, 81),
        };
        _clearLog = new Button
        {
            Dock = DockStyle.Right,
            Width = 86,
            Height = 24,
            Margin = new Padding(0),
        };
        _clearLog.Click += (_, _) => _log.Clear();

        _saveLog = new Button
        {
            Dock = DockStyle.Right,
            Width = 104,
            Height = 24,
            Margin = new Padding(0),
        };
        _saveLog.Click += (_, _) => SaveBuildLog();

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.FromArgb(249, 250, 251),
            Padding = new Padding(4, 3, 4, 3),
        };
        header.Controls.Add(_logTitle);
        header.Controls.Add(_saveLog);
        header.Controls.Add(_clearLog);

        _log = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Window,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
            HideSelection = false,
        };

        panel.Controls.Add(_log);
        panel.Controls.Add(header);
        return panel;
    }

    private ToolStripMenuItem AddMenuItem(
        ToolStripMenuItem parent,
        string textKey,
        EventHandler? click,
        Keys shortcut = Keys.None)
    {
        var item = new ToolStripMenuItem
        {
            ShortcutKeys = shortcut,
            ShowShortcutKeys = shortcut != Keys.None,
        };
        if (click != null)
            item.Click += click;
        _textKeys[item] = textKey;
        parent.DropDownItems.Add(item);
        return item;
    }

    private ToolStripButton AddToolButton(ToolStrip parent, string textKey, EventHandler click)
    {
        var button = new ToolStripButton
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
        };
        button.Click += click;
        _textKeys[button] = textKey;
        parent.Items.Add(button);
        return button;
    }

    private void ApplyLocalization()
    {
        var interfaceFont = UiFonts.GetInterfaceFont(_loc.Language);
        var boldFont = UiFonts.GetBoldInterfaceFont(_loc.Language);

        Font = interfaceFont;
        _menu.Font = interfaceFont;
        _toolStrip.Font = interfaceFont;
        _grid.Font = interfaceFont;
        _grid.ColumnHeadersDefaultCellStyle.Font = boldFont;
        _details.Font = interfaceFont;
        _log.Font = UiFonts.GetMonoFont();
        _itemsTitle.Font = boldFont;
        _logTitle.Font = boldFont;
        _toolBuild.Font = boldFont;

        foreach (var pair in _textKeys)
        {
            pair.Key.Text = _loc[pair.Value];
            pair.Key.ToolTipText = _loc[pair.Value];
        }

        _outputLabel.Text = _loc["project.outputLabel"];
        _browseOutput.Text = _loc["project.browse"];
        _outputBox.PlaceholderText = _loc["project.outputNotSet"];
        _itemsTitle.Text = _loc["project.itemsTitle"];
        _emptyHintText = _loc["project.emptyHint"];
        _grid.Invalidate();
        _logTitle.Text = _loc["log.title"];
        _clearLog.Text = _loc["log.clear"];
        _saveLog.Text = _loc["log.save"];

        _contextBuildSelected.Text = _loc["context.buildSelected"];
        _contextRevealSource.Text = _loc["context.revealSource"];
        _contextCopySourcePath.Text = _loc["context.copySourcePath"];
        _contextCopyOutputPath.Text = _loc["context.copyOutputPath"];
        _contextRemove.Text = _loc["context.remove"];

        _grid.Columns[0].HeaderText = _loc["grid.name"];
        _grid.Columns[0].ToolTipText = _loc["grid.nameHint"];
        _grid.Columns[1].HeaderText = _loc["grid.type"];
        _grid.Columns[2].HeaderText = _loc["grid.source"];
        _grid.Columns[3].HeaderText = _loc["grid.status"];
        _grid.Columns[4].HeaderText = _loc["grid.size"];

        _menuEnglish.Checked = _loc.Language == AppLanguage.English;
        _menuChinese.Checked = _loc.Language == AppLanguage.ChineseSimplified;
        RefreshProfileMenu();

        Text = BuildWindowTitle();
        UpdateStatusCount();
        RebuildRecentMenu();
        _details.ApplyLocalization();
        RefreshGrid(GetSelectedItemIds());

        if (!_building)
            _statusLabel.Text = _loc["statusbar.ready"];
    }

    private void ChangeLanguage(AppLanguage language)
    {
        if (_loc.Language == language)
            return;

        _loc.SetLanguage(language);
        _settings.Language = LocalizationService.ToSettingValue(language);
        TrySaveSettings();
    }

    private void SetProfile(GraphicsProfileSetting profile)
    {
        if (_building || _project.Profile == profile)
            return;

        _project.Profile = profile;
        _dirty = true;
        UpdateTitle();

        // Every XNB embeds the profile flag, so previous results are stale.
        _itemStates.Clear();
        _itemResults.Clear();
        RefreshGrid(GetSelectedItemIds());

        AppendLog(BuildLogLevel.Info,
            _loc.Format("log.profileChanged",
                _loc[profile == GraphicsProfileSetting.HiDef ? "menu.profileHiDef" : "menu.profileReach"]));

        RefreshProfileMenu();
    }

    private void RefreshProfileMenu()
    {
        _menuProfileReach.Checked = _project.Profile == GraphicsProfileSetting.Reach;
        _menuProfileHiDef.Checked = _project.Profile == GraphicsProfileSetting.HiDef;
    }

    private async void StartBuildAsync(IReadOnlyCollection<ContentItem>? scope = null)
    {
        if (_building)
            return;

        if (_project.Items.Count == 0)
        {
            ShowInfo(_loc["dialog.build.noItems"]);
            return;
        }

        if (scope != null && scope.Count == 0)
        {
            ShowInfo(_loc["dialog.build.noSelection"]);
            return;
        }

        if (string.IsNullOrWhiteSpace(_project.OutputDirectory))
        {
            ShowInfo(_loc["dialog.build.noOutput"]);
            return;
        }

        try
        {
            _project.OutputDirectory = Path.GetFullPath(_project.OutputDirectory);
        }
        catch (Exception)
        {
            ShowError(_loc["dialog.outputInvalid"]);
            return;
        }

        _settings.LastOutputDirectory = _project.OutputDirectory;
        TrySaveSettings();

        var buildItems = scope?.ToList();

        _itemStates.Clear();
        _itemResults.Clear();
        foreach (var item in buildItems ?? (IEnumerable<ContentItem>)_project.Items)
            UpdateGridRow(item);

        AppendLog(BuildLogLevel.Info,
            buildItems == null
                ? _loc["log.buildStarted"]
                : _loc.Format("log.buildStartedSelected", buildItems.Count));

        SetBuildingUi(true);
        _progress.Maximum = Math.Max(1, buildItems?.Count ?? _project.Items.Count);
        _progress.Value = 0;

        _cts = new CancellationTokenSource();
        BuildSummary? summary = null;
        Exception? failure = null;

        try
        {
            summary = await Task.Run(() => _buildService.Build(_project, buildItems, _cts.Token));
        }
        catch (BuildConfigurationException ex)
        {
            failure = ex;
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            _cts.Dispose();
            _cts = null;

            if (!IsDisposed && !Disposing)
                SetBuildingUi(false);
        }

        // The window may have been closed while the build was running.
        if (IsDisposed || Disposing)
            return;

        if (failure != null)
        {
            var message = failure is BuildConfigurationException configuration
                ? LocalizeBuildError(configuration.Code, configuration.Message)
                : _loc.Format("error.unknown", failure.Message);
            AppendLog(BuildLogLevel.Error, message);
            ShowError(message);
            _statusLabel.Text = _loc["statusbar.finished"];
            return;
        }

        if (summary is null)
            return;

        var attempted = summary.Items.Select(x => x.Item.Id).ToHashSet();
        foreach (var item in buildItems ?? _project.Items.ToList())
        {
            if (attempted.Contains(item.Id))
                continue;

            if (item.Kind == ContentKind.Unsupported)
                continue;

            SetItemState(item, summary.Canceled ? ItemUiState.Canceled : ItemUiState.Ready);
        }

        var duration = FormatDuration(summary.Duration);
        if (summary.Canceled)
        {
            _statusLabel.Text = _loc["statusbar.canceled"];
            AppendLog(BuildLogLevel.Warning, _loc["log.buildCanceled"]);
            ShowWarning(_loc.Format("dialog.build.canceled", summary.Succeeded, summary.Failed));
        }
        else
        {
            _statusLabel.Text = _loc["statusbar.finished"];
            AppendLog(
                summary.Failed == 0 ? BuildLogLevel.Success : BuildLogLevel.Warning,
                _loc.Format("log.buildSummary", summary.Succeeded, summary.Failed, duration));

            var text = _loc.Format("dialog.build.summary", duration, summary.Succeeded, summary.Failed);
            if (summary.Failed > 0)
                ShowWarning(text);
            else
                ShowInfo(text);
        }
    }

    private void CancelBuild()
    {
        if (!_building || _cts == null)
            return;

        _cts.Cancel();
        _toolCancel.Enabled = false;
        _menuCancelBuild.Enabled = false;
        _statusLabel.Text = _loc["statusbar.canceling"];
    }

    private void SubscribeBuildEvents()
    {
        _buildService.Log += (_, entry) => PostToUi(() => AppendLog(entry));
        _buildService.Progress += (_, progress) => PostToUi(() => OnBuildProgress(progress));
        _buildService.ItemStarting += (_, item) => PostToUi(() => SetItemState(item, ItemUiState.Building));
        _buildService.ItemFinished += (_, result) => PostToUi(() => OnItemFinished(result));
    }

    private void OnBuildProgress(BuildProgress progress)
    {
        if (!_building)
            return;

        _progress.Maximum = Math.Max(1, progress.Total);
        _progress.Value = Math.Min(Math.Max(0, progress.Current - 1), _progress.Maximum);
        _statusLabel.Text = _loc.Format(
            "statusbar.building",
            progress.Current,
            progress.Total,
            Path.GetFileName(progress.Item.SourcePath));
    }

    private void OnItemFinished(ItemBuildResult result)
    {
        _itemResults[result.Item.Id] = result;
        SetItemState(result.Item, result.Success ? ItemUiState.Succeeded : ItemUiState.Failed);

        if (_progress.Maximum > 0 && _progress.Value < _progress.Maximum)
            _progress.Value = Math.Min(_progress.Value + 1, _progress.Maximum);
    }

    private void SetBuildingUi(bool building)
    {
        _building = building;
        var editable = !building;

        _toolNew.Enabled = editable;
        _toolOpen.Enabled = editable;
        _toolSave.Enabled = editable;
        _toolAddFiles.Enabled = editable;
        _toolAddFolder.Enabled = editable;
        _toolBuild.Enabled = editable;
        _toolBuildSelected.Enabled = editable;
        _toolCancel.Enabled = building;
        _toolOpenOutput.Enabled = editable;

        _menuNew.Enabled = editable;
        _menuOpen.Enabled = editable;
        _menuSave.Enabled = editable;
        _menuSaveAs.Enabled = editable;
        _menuAddFiles.Enabled = editable;
        _menuAddFolder.Enabled = editable;
        _menuExit.Enabled = editable;
        _menuBuildNow.Enabled = editable;
        _menuBuildSelected.Enabled = editable;
        _menuCancelBuild.Enabled = building;
        _menuProfileReach.Enabled = editable;
        _menuProfileHiDef.Enabled = editable;
        _menuOpenOutput.Enabled = editable;

        _outputBox.Enabled = editable;
        _browseOutput.Enabled = editable;
        _details.SetEditingEnabled(editable);
        _progress.Visible = building;
        UpdateSelection();
    }

    private void CreateNewProject(bool skipConfirmation)
    {
        if (!skipConfirmation && !ConfirmSaveChanges())
            return;

        _project = new ContentProject
        {
            OutputDirectory = DefaultOutputDirectory(),
        };
        _dirty = false;
        _itemStates.Clear();
        _itemResults.Clear();
        _log.Clear();

        RefreshAll();
        UpdateTitle();
        _statusLabel.Text = _loc["statusbar.ready"];
    }

    private void OpenProjectDialog()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = _loc["app.projectFilter"],
            DefaultExt = "xnccproj",
            Title = _loc["dialog.openProject"],
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadProject(dialog.FileName, confirmSave: true);
    }

    private bool LoadProject(string path, bool confirmSave)
    {
        if (confirmSave && !ConfirmSaveChanges())
            return false;

        try
        {
            var project = ProjectSerializer.Load(path);
            _project = project;
            _project.OutputDirectory = Path.GetFullPath(_project.OutputDirectory);
            _dirty = false;
            _itemStates.Clear();
            _itemResults.Clear();
            _log.Clear();

            _settings.AddRecentProject(path);
            _settings.LastOutputDirectory = _project.OutputDirectory;
            TrySaveSettings();

            RefreshAll();
            UpdateTitle();
            _statusLabel.Text = _loc["statusbar.ready"];
            AppendLog(BuildLogLevel.Info, _loc.Format("log.projectOpened", path));
            return true;
        }
        catch (Exception ex)
        {
            ShowError(_loc.Format("dialog.load.failed", ex.Message));
            return false;
        }
    }

    private bool SaveProject()
    {
        if (string.IsNullOrWhiteSpace(_project.FilePath))
            return SaveProjectAs();

        return SaveProjectTo(_project.FilePath);
    }

    private bool SaveProjectAs()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = _loc["app.projectFilter"],
            DefaultExt = "xnccproj",
            AddExtension = true,
            OverwritePrompt = true,
            Title = _loc["dialog.saveProject"],
            FileName = string.IsNullOrWhiteSpace(_project.Name) ? "ContentProject" : _project.Name,
            InitialDirectory = DefaultProjectDirectory(),
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return false;

        return SaveProjectTo(dialog.FileName);
    }

    private bool SaveProjectTo(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_project.OutputDirectory))
                _project.OutputDirectory = DefaultOutputDirectory();

            ProjectSerializer.Save(_project, path);
            _dirty = false;
            _settings.AddRecentProject(path);
            TrySaveSettings();
            UpdateTitle();
            RebuildRecentMenu();
            AppendLog(BuildLogLevel.Info, _loc.Format("log.projectSaved", path));
            return true;
        }
        catch (Exception ex)
        {
            ShowError(_loc.Format("dialog.save.failed", ex.Message));
            return false;
        }
    }

    private bool ConfirmSaveChanges()
    {
        if (!_dirty)
            return true;

        var answer = MessageBox.Show(
            this,
            _loc["dialog.unsaved.text"],
            _loc["dialog.unsaved.title"],
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button3);

        return answer switch
        {
            DialogResult.Yes => SaveProject(),
            DialogResult.No => true,
            _ => false,
        };
    }

    private void AddFilesDialog()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = _loc["app.fileFilter"],
            Title = _loc["dialog.selectFiles"],
            Multiselect = true,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddSourceFiles(dialog.FileNames);
    }

    private void AddFolderDialog()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = _loc["dialog.selectFolder"],
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var files = EnumerateSupportedFiles(dialog.SelectedPath);
        if (files.Count == 0)
        {
            ShowWarning(_loc["dialog.addFolder.empty"]);
            return;
        }

        AddSourceFiles(files);
    }

    private void AddSourceFiles(IEnumerable<string> paths)
    {
        var existing = new HashSet<string>(
            _project.Items.Select(x => x.SourcePath),
            StringComparer.OrdinalIgnoreCase);
        var accepted = new List<string>();
        var duplicates = 0;
        var skipped = 0;

        foreach (var path in paths)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                continue;

            var kind = ContentTypeRegistry.Detect(fullPath);
            if (kind == ContentKind.Unsupported)
            {
                skipped++;
                continue;
            }

            if (!existing.Add(fullPath))
            {
                duplicates++;
                continue;
            }

            accepted.Add(fullPath);
        }

        // CreateItems performs a single collision pass instead of one per file.
        var added = _project.CreateItems(accepted);

        if (added.Count > 0)
        {
            _dirty = true;
            RefreshGrid(added.Select(x => x.Id).ToList());
            AppendLog(BuildLogLevel.Info, _loc.Format("log.added", added.Count));
            UpdateTitle();
            UpdateStatusCount();
        }

        if (skipped > 0)
            AppendLog(BuildLogLevel.Warning, _loc.Format("dialog.addSkipped", skipped));
        if (duplicates > 0)
            AppendLog(BuildLogLevel.Warning, _loc.Format("dialog.addDuplicates", duplicates));

        if (added.Count == 0 && skipped > 0)
            ShowWarning(_loc.Format("dialog.addSkipped", skipped));
        else if (added.Count == 0 && duplicates > 0)
            ShowInfo(_loc.Format("dialog.addDuplicates", duplicates));
    }

    private List<string> EnumerateSupportedFiles(string folder)
    {
        var results = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                if (ContentTypeRegistry.Detect(file) != ContentKind.Unsupported)
                    results.Add(file);
            }
        }
        catch (Exception ex)
        {
            AppendLog(BuildLogLevel.Warning, ex.Message);
        }

        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results;
    }

    private void RemoveSelectedItems()
    {
        if (_building)
            return;

        var items = GetSelectedItems().ToList();
        if (items.Count == 0)
            return;

        var answer = MessageBox.Show(
            this,
            _loc.Format("dialog.confirmRemove.text", items.Count),
            _loc["dialog.confirmRemove.title"],
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
            return;

        foreach (var item in items)
        {
            _project.RemoveItem(item);
            _itemStates.Remove(item.Id);
            _itemResults.Remove(item.Id);
        }

        _dirty = true;
        RefreshAll();
        UpdateTitle();
        AppendLog(BuildLogLevel.Info, _loc.Format("log.removed", items.Count));
    }

    private void ClearAllItems()
    {
        if (_building || _project.Items.Count == 0)
            return;

        var answer = MessageBox.Show(
            this,
            _loc["dialog.clearItems.text"],
            _loc["dialog.clearItems.title"],
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
            return;

        _project.Items.Clear();
        _itemStates.Clear();
        _itemResults.Clear();
        _dirty = true;
        RefreshAll();
        UpdateTitle();
        UpdateStatusCount();
    }

    private void BrowseOutputDirectory()
    {
        if (_building)
            return;

        using var dialog = new FolderBrowserDialog
        {
            Description = _loc["dialog.browseOutput"],
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_project.OutputDirectory)
                ? _project.OutputDirectory
                : DefaultOutputDirectory(),
            ShowNewFolderButton = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        ApplyOutputDirectory(dialog.SelectedPath);
    }

    /// <summary>
    /// Applies a new output directory and refreshes every place that displays it:
    /// the path box, the per-item output path in the details panel and the row tooltips.
    /// </summary>
    private void ApplyOutputDirectory(string path)
    {
        if (_applyingOutputDirectory)
            return;

        _applyingOutputDirectory = true;
        try
        {
            try
            {
                _project.OutputDirectory = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                ShowError(_loc["dialog.outputInvalid"]);
                _outputBox.Text = _project.OutputDirectory ?? string.Empty;
                return;
            }

            _dirty = true;
            _itemStates.Clear();
            _itemResults.Clear();

            RefreshOutputPathDisplay();
            UpdateTitle();
            RememberOutputDirectory();

            if (!_building)
                _statusLabel.Text = _loc["statusbar.ready"];
        }
        finally
        {
            _applyingOutputDirectory = false;
        }
    }

    private void CommitOutputDirectoryText()
    {
        if (_building || _applyingOutputDirectory)
            return;

        var text = _outputBox.Text.Trim();
        var current = _project.OutputDirectory ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            _outputBox.Text = current;
            return;
        }

        if (string.Equals(text, current, StringComparison.OrdinalIgnoreCase))
            return;

        // Relative paths typed by the user are resolved next to the project file.
        var resolved = Path.IsPathRooted(text)
            ? text
            : Path.Combine(_project.ProjectDirectory, text);

        ApplyOutputDirectory(resolved);
    }

    private void RefreshOutputPathDisplay()
    {
        _outputBox.Text = _project.OutputDirectory ?? string.Empty;

        if (!_grid.IsCurrentCellInEditMode)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is ContentItem item)
                    UpdateGridRow(item, row);
            }
        }

        _details.SetSelection(GetSelectedItems());
    }

    private void OpenOutputFolder()
    {
        var directory = _project.OutputDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            ShowWarning(_loc["dialog.build.noOutput"]);
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ShowError(_loc.Format("error.io", ex.Message));
        }
    }

    private void OnDetailsChanged()
    {
        _dirty = true;
        UpdateTitle();

        foreach (var item in GetSelectedItems())
        {
            _itemStates.Remove(item.Id);
            _itemResults.Remove(item.Id);
            UpdateGridRow(item);
        }
    }

    private void UpdateSelection()
    {
        if (_refreshingGrid)
            return;

        var items = GetSelectedItems();
        _details.SetSelection(items);

        var hasSelection = items.Count > 0 && !_building;
        _toolRemove.Enabled = hasSelection;
        _toolBuildSelected.Enabled = hasSelection;
        _menuRemove.Enabled = hasSelection;
        _menuBuildSelected.Enabled = hasSelection;
        _menuClear.Enabled = _project.Items.Count > 0 && !_building;
    }

    private List<ContentItem> GetSelectedItems()
    {
        var result = new List<ContentItem>();
        foreach (DataGridViewRow row in _grid.SelectedRows)
        {
            if (row.Tag is ContentItem item)
                result.Add(item);
        }

        return result;
    }

    private List<Guid> GetSelectedItemIds()
    {
        return GetSelectedItems().Select(x => x.Id).ToList();
    }

    private void RefreshAll()
    {
        RefreshGrid(GetSelectedItemIds());
        RefreshProfileMenu();
        _outputBox.Text = _project.OutputDirectory ?? string.Empty;
        UpdateStatusCount();
    }

    private void RefreshGrid(IReadOnlyCollection<Guid>? selectIds)
    {
        _refreshingGrid = true;
        _details.Profile = _project.Profile;
        _grid.SuspendLayout();
        try
        {
            if (_grid.IsCurrentCellInEditMode)
                _grid.EndEdit();

            _grid.Rows.Clear();

            foreach (var item in _project.Items)
                AddGridRow(item);

            _grid.Invalidate();

            var wanted = selectIds?.ToHashSet() ?? new HashSet<Guid>();
            var firstRow = -1;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is not ContentItem item)
                    continue;

                if (firstRow < 0)
                    firstRow = row.Index;

                row.Selected = wanted.Contains(item.Id);
            }

            if (firstRow >= 0 && _grid.SelectedRows.Count == 0)
            {
                _grid.Rows[firstRow].Selected = true;
                _grid.CurrentCell = _grid.Rows[firstRow].Cells[0];
            }
        }
        finally
        {
            _grid.ResumeLayout(performLayout: true);
            _refreshingGrid = false;
        }

        UpdateDuplicateNameMarkers();
        UpdateSelection();
        UpdateStatusCount();
    }

    /// <summary>
    /// Marks rows whose output name collides with another item. The build would reject
    /// them, so the conflict is surfaced before the user starts a build.
    /// </summary>
    private void UpdateDuplicateNameMarkers()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _project.Items)
        {
            if (!OutputNameValidator.TryNormalize(item.OutputName, out var normalized))
                continue;

            counts[normalized] = counts.TryGetValue(normalized, out var count) ? count + 1 : 1;
        }

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is not ContentItem item)
                continue;

            var duplicate = OutputNameValidator.TryNormalize(item.OutputName, out var normalized)
                && counts.TryGetValue(normalized, out var count)
                && count > 1;

            if (!duplicate)
                continue;

            row.Cells[0].ErrorText = _loc["error.duplicateOutput"];
        }
    }

    private void AddGridRow(ContentItem item)
    {
        var index = _grid.Rows.Add();
        var row = _grid.Rows[index];
        row.Tag = item;
        UpdateGridRow(item, row);
    }

    private void UpdateGridRow(ContentItem item, DataGridViewRow? existingRow = null)
    {
        var row = existingRow ?? FindRow(item.Id);
        if (row == null)
            return;

        row.Cells[0].Value = item.OutputName;
        row.Cells[0].ToolTipText = TryGetOutputPath(item);
        row.Cells[1].Value = KindText(item.Kind);
        row.Cells[2].Value = item.FileName;
        row.Cells[2].ToolTipText = item.SourcePath;

        var (statusText, statusColor, statusTooltip) = GetStatus(item);
        row.Cells[3].Value = statusText;
        row.Cells[3].Style.ForeColor = statusColor;
        row.Cells[3].ToolTipText = statusTooltip;

        if (_itemResults.TryGetValue(item.Id, out var result) && result.Success)
        {
            row.Cells[4].Value = FormatSize(result.OutputBytes);
            row.Cells[4].ToolTipText = result.OutputPath ?? string.Empty;
        }
        else
        {
            row.Cells[4].Value = string.Empty;
            row.Cells[4].ToolTipText = string.Empty;
        }
    }

    private DataGridViewRow? FindRow(Guid id)
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is ContentItem item && item.Id == id)
                return row;
        }

        return null;
    }

    private Tuple<string, Color, string> GetStatus(ContentItem item)
    {
        if (item.Kind == ContentKind.Unsupported)
            return Tuple.Create(_loc["status.unsupported"], Muted, _loc["error.unsupportedType"]);

        if (!File.Exists(item.SourcePath))
            return Tuple.Create(_loc["status.missingSource"], DangerColor, item.SourcePath);

        var state = _itemStates.TryGetValue(item.Id, out var value) ? value : ItemUiState.Ready;
        return state switch
        {
            ItemUiState.Building => Tuple.Create(_loc["status.building"], Accent, string.Empty),
            ItemUiState.Succeeded => Tuple.Create(_loc["status.succeeded"], SuccessColor, string.Empty),
            ItemUiState.Failed => Tuple.Create(
                _loc["status.failed"],
                DangerColor,
                _itemResults.TryGetValue(item.Id, out var result)
                    ? LocalizeItemError(result)
                    : string.Empty),
            ItemUiState.Canceled => Tuple.Create(_loc["status.canceled"], WarningColor, string.Empty),
            _ => Tuple.Create(_loc["status.ready"], Muted, string.Empty),
        };
    }

    private string LocalizeItemError(ItemBuildResult result)
    {
        var detail = result.ErrorDetail ?? string.Empty;
        var message = result.ErrorCode switch
        {
            BuildErrorCode.None => string.Empty,
            BuildErrorCode.SourceNotFound => _loc["error.sourceNotFound"],
            BuildErrorCode.UnsupportedContentType => _loc["error.unsupportedType"],
            BuildErrorCode.InvalidOutputName => _loc["error.invalidOutputName"],
            BuildErrorCode.DuplicateOutputName => _loc["error.duplicateOutput"],
            BuildErrorCode.InvalidOutputDirectory => _loc["error.outputDirectory"],
            BuildErrorCode.TextureBuildFailed => _loc.Format("error.textureBuild", detail),
            BuildErrorCode.FontBuildFailed => _loc.Format("error.fontBuild", detail),
            BuildErrorCode.AudioBuildFailed => _loc.Format("error.audioBuild", detail),
            BuildErrorCode.SongBuildFailed => _loc.Format("error.songBuild", detail),
            BuildErrorCode.XnaCompatibilityFailed => _loc.Format("error.xnaCompat", detail),
            BuildErrorCode.IoError => _loc.Format("error.io", detail),
            BuildErrorCode.Canceled => _loc["status.canceled"],
            _ => _loc.Format("error.unknown", detail),
        };

        if (result.ErrorCode == BuildErrorCode.TextureBuildFailed
            && detail.Contains("powers of two", StringComparison.OrdinalIgnoreCase))
        {
            message += Environment.NewLine + _loc["error.dxtHint"];
        }

        return message;
    }

    private string LocalizeBuildError(BuildErrorCode code, string detail)
    {
        return code switch
        {
            BuildErrorCode.InvalidOutputDirectory => _loc["error.outputDirectory"],
            BuildErrorCode.IoError => _loc.Format("error.io", detail),
            _ => _loc.Format("error.unknown", detail),
        };
    }

    private string TryGetOutputPath(ContentItem item)
    {
        try
        {
            return _project.GetOutputPath(item);
        }
        catch (ArgumentException)
        {
            return _loc["error.invalidOutputName"];
        }
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

    private void SetItemState(ContentItem item, ItemUiState state)
    {
        _itemStates[item.Id] = state;
        UpdateGridRow(item);
    }

    private void UpdateStatusCount()
    {
        _countLabel.Text = _loc.Format("statusbar.itemCount", _project.Items.Count);
    }

    private string BuildWindowTitle()
    {
        var name = _project.Name;
        if (string.IsNullOrWhiteSpace(name))
            name = _loc["app.untitled"];

        return $"{name}{(_dirty ? "*" : string.Empty)} - {_loc["app.title"]}";
    }

    private void UpdateTitle()
    {
        Text = BuildWindowTitle();
    }

    private void RebuildRecentMenu()
    {
        _recentMenu.Text = _loc["menu.recent"];
        _recentMenu.DropDownItems.Clear();

        if (_settings.RecentProjects.Count == 0)
        {
            _recentMenu.DropDownItems.Add(new ToolStripMenuItem(_loc["menu.recentEmpty"])
            {
                Enabled = false,
            });
            return;
        }

        foreach (var path in _settings.RecentProjects.ToList())
        {
            var item = new ToolStripMenuItem(Path.GetFileNameWithoutExtension(path))
            {
                ToolTipText = path,
                Tag = path,
            };
            item.Click += (_, _) => OpenRecentProject(path);
            _recentMenu.DropDownItems.Add(item);
        }
    }

    private void OpenRecentProject(string path)
    {
        if (!File.Exists(path))
        {
            _settings.RecentProjects.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
            TrySaveSettings();
            RebuildRecentMenu();
            ShowWarning(_loc.Format("dialog.recentMissing", path));
            return;
        }

        LoadProject(path, confirmSave: true);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        var data = e.Data;
        if (data != null && data.GetDataPresent(DataFormats.FileDrop))
            e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (_building)
            return;

        var data = e.Data;
        if (data == null || !data.GetDataPresent(DataFormats.FileDrop))
            return;

        var dropped = data.GetData(DataFormats.FileDrop) as string[];
        if (dropped == null || dropped.Length == 0)
            return;

        var projectFile = dropped
            .Where(File.Exists)
            .FirstOrDefault(x => x.EndsWith(".xnccproj", StringComparison.OrdinalIgnoreCase));

        if (projectFile != null)
        {
            LoadProject(projectFile, confirmSave: true);
            return;
        }

        var paths = new List<string>();
        foreach (var path in dropped)
        {
            if (Directory.Exists(path))
                paths.AddRange(EnumerateSupportedFiles(path));
            else if (File.Exists(path))
                paths.Add(path);
        }

        AddSourceFiles(paths);
    }

    private void AppendLog(BuildLogEntry entry)
    {
        AppendLog(entry.Level, entry.Message, entry.Timestamp);
    }

    private void AppendLog(BuildLogLevel level, string message)
    {
        AppendLog(level, message, DateTime.Now);
    }

    private void AppendLog(BuildLogLevel level, string message, DateTime timestamp)
    {
        var color = level switch
        {
            BuildLogLevel.Error => DangerColor,
            BuildLogLevel.Warning => WarningColor,
            BuildLogLevel.Success => SuccessColor,
            BuildLogLevel.Debug => Muted,
            _ => Color.FromArgb(31, 41, 55),
        };

        _log.SelectionStart = _log.TextLength;
        _log.SelectionLength = 0;
        _log.SelectionColor = color;
        _log.AppendText($"[{timestamp:HH:mm:ss}] {message}{Environment.NewLine}");
        _log.SelectionColor = _log.ForeColor;
        _log.ScrollToCaret();
    }

    private void PostToUi(Action action)
    {
        if (IsDisposed || Disposing)
            return;

        try
        {
            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }
        catch (InvalidOperationException)
        {
            // The window handle can disappear while a background build is finishing.
            // ObjectDisposedException derives from this type as well.
        }
    }

    private void ShowUsage()
    {
        using var dialog = new UsageForm();
        dialog.ShowDialog(this);
    }

    private void ShowAbout()
    {
        using var dialog = new AboutForm();
        dialog.ShowDialog(this);
    }

    private void ShowInfo(string message)
    {
        MessageBox.Show(this, message, _loc["dialog.info"], MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowWarning(string message)
    {
        MessageBox.Show(this, message, _loc["dialog.warning"], MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, _loc["dialog.error"], MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void TrySaveSettings()
    {
        try
        {
            SettingsStore.Save(_settings);
        }
        catch (Exception)
        {
            // Preferences are optional; a read-only profile directory should not break the app.
        }
    }

    private void RestoreWindowPlacement()
    {
        var placement = _settings.Window;
        var bounds = new Rectangle(placement.X, placement.Y, placement.Width, placement.Height);

        if (Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(bounds)))
        {
            var workingArea = Screen.FromRectangle(bounds).WorkingArea;
            var width = Math.Min(bounds.Width, workingArea.Width);
            var height = Math.Min(bounds.Height, workingArea.Height);
            var x = Math.Clamp(bounds.X, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - width));
            var y = Math.Clamp(bounds.Y, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - height));
            Bounds = new Rectangle(x, y, width, height);
        }

        if (placement.Maximized)
            WindowState = FormWindowState.Maximized;
    }

    private void SaveWindowPlacement()
    {
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        _settings.Window = new WindowSettings
        {
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = WindowState == FormWindowState.Maximized,
        };
    }

    private string DefaultOutputDirectory()
    {
        var last = _settings.LastOutputDirectory;
        if (!string.IsNullOrWhiteSpace(last))
        {
            try
            {
                var fullPath = Path.GetFullPath(last);
                if (Directory.Exists(fullPath) || Directory.Exists(Path.GetDirectoryName(fullPath)))
                    return fullPath;
            }
            catch (Exception)
            {
                // Fall back to the documents folder for invalid stored values.
            }
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
            documents = Environment.CurrentDirectory;

        return Path.Combine(documents, "XNA Content Compiler", "Content");
    }

    private void RememberOutputDirectory()
    {
        if (string.IsNullOrWhiteSpace(_project.OutputDirectory))
            return;

        _settings.LastOutputDirectory = _project.OutputDirectory;
        TrySaveSettings();
    }

    private static string DefaultProjectDirectory()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
            documents = Environment.CurrentDirectory;

        return Path.Combine(documents, "XNA Content Compiler");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes / (1024.0 * 1024.0):0.0} MB";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        return $"{duration.TotalSeconds:0.0}s";
    }

    private enum ItemUiState
    {
        Ready,
        Building,
        Succeeded,
        Failed,
        Canceled,
    }
}
