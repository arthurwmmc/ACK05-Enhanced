using Ack05Enhanced.Core;
using Ack05Enhanced.Windows;

namespace Ack05Enhanced.App;

public sealed class KeymapEditorForm : Form
{
    private const int MinimumWorkspaceHeight = 320;
    private const int MinimumActionPaletteHeight = 160;
    private const int PreferredActionPaletteHeight = 280;

    private static readonly Ack05Control[] BindingCandidates =
    [
        Ack05Control.K1,
        Ack05Control.K2,
        Ack05Control.K3,
        Ack05Control.K4,
        Ack05Control.K5,
        Ack05Control.K6,
        Ack05Control.K7,
        Ack05Control.K8,
        Ack05Control.K9,
        Ack05Control.K10,
        Ack05Control.RollerClockwise,
        Ack05Control.RollerCounterClockwise,
    ];

    private readonly ComboBox _profiles = new();
    private readonly TextBox _profileId = new();
    private readonly TextBox _profileName = new();
    private readonly TextBox _processName = new();
    private readonly TabControl _layers = new();
    private readonly DataGridView _bindings = new();
    private readonly Ack05DeviceMapControl _deviceMap = new();
    private readonly InstrumentButton _comboVisibility = new();
    private readonly Ack05ActionPalette _actionPalette = new();
    private readonly Label _actionSummary = new();
    private readonly NumericUpDown _wheelSteps = new();
    private readonly ComboBox _targetLayer = new();
    private readonly NumericUpDown _actionTapHoldThreshold = new();
    private readonly Button _captureShortcut = new();
    private readonly Button _applyBinding = new();
    private readonly Label _status = new();
    private readonly Button _save = new();
    private readonly InstrumentButton _live = new();
    private readonly Ack05LiveRuntime _liveRuntime = new();
    private readonly Label _selectedKeyTitle = new();
    private readonly Label _selectedKeyCaption = new();
    private readonly Label _inspectorHint = new();
    private readonly EasedHeightPanel _advancedPanel = new();
    private readonly InstrumentButton _advancedToggle = new();
    private readonly InstrumentButton _undo = new();
    private readonly InstrumentButton _redo = new();
    private readonly InstrumentButton _revert = new();
    private readonly InstrumentPanel _actionDock = new();
    private readonly InstrumentSplitContainer _workspaceSplit = new();
    private readonly ListBox _tapHoldDefinitions = new();
    private readonly Ack05DeviceMapControl _tapHoldDeviceMap = new();
    private readonly TextBox _tapHoldName = new();
    private readonly NumericUpDown _tapHoldTerm = new();
    private readonly ComboBox _tapHoldInterrupt = new();
    private readonly ComboBox _tapHoldTargetLayer = new();
    private readonly NumericUpDown _tapHoldWheelSteps = new();
    private readonly Label _tapHoldReferenceSummary = new();
    private readonly Label _tapHoldMapSummary = new();
    private readonly Label _tapHoldMapHint = new();
    private readonly InstrumentButton _tapSlotButton = new();
    private readonly InstrumentButton _holdSlotButton = new();
    private readonly InstrumentButton _assignTapHold = new();
    private readonly ListBox _comboDefinitions = new();
    private readonly Ack05DeviceMapControl _comboDeviceMap = new();
    private readonly TextBox _comboName = new();
    private readonly Label _comboMembers = new();
    private readonly Label _comboSelectionHint = new();
    private readonly NumericUpDown _comboProfileTerm = new();
    private readonly CheckBox _comboUseDefaultTerm = new();
    private readonly NumericUpDown _comboTerm = new();
    private readonly CheckedListBox _comboLayers = new();
    private readonly CheckBox _comboOrdered = new();
    private readonly ComboBox _comboTrigger = new();
    private readonly NumericUpDown _comboHoldTerm = new();
    private readonly ComboBox _comboRelease = new();
    private readonly ComboBox _comboTargetLayer = new();
    private readonly NumericUpDown _comboWheelSteps = new();
    private readonly Label _comboOutput = new();
    private readonly BufferedTableLayoutPanel _rootLayout = new();
    private readonly Panel _workspaceHost = new();
    private readonly BufferedFlowLayoutPanel _workspaceNavigation = new();
    private readonly InstrumentButton _workspaceMenuButton = new();
    private readonly ContextMenuStrip _workspaceMenu = new();
    private readonly Dictionary<string, InstrumentButton> _workspaceButtons = new();
    private readonly Dictionary<string, ToolStripMenuItem> _workspaceMenuItems = new();
    private readonly Dictionary<string, ScrollableControl> _workspaceInspectors =
        new(StringComparer.Ordinal);
    private readonly Stack<Ack05Configuration> _undoHistory = new();
    private readonly Stack<Ack05Configuration> _redoHistory = new();

    private Ack05Configuration _configuration;
    private string _savedBaselineJson;
    private string? _path;
    private string? _loadedFileFingerprint;
    private int _selectedLayer;
    private bool _loading;
    private bool _liveConfirmed;
    private bool _closing;
    private bool _allowClose;
    private bool _bindingEditorLoading;
    private bool _dirty;
    private bool _tapHoldEditorLoading;
    private bool _comboEditorLoading;
    private string _activeWorkspace = "Keymap";
    private TapHoldSlot _tapHoldSlot = TapHoldSlot.Tap;
    private Ack05Control? _tapHoldSelectedControl;
    private Ack05Control? _comboFirstSelection;
    private Ack05KeyboardKey[]? _capturedKeyOrder;
    private CancellationTokenSource? _draftSaveCancellation;
    private int _draftSaveGeneration;
    private int _lastActionPaletteHeight = PreferredActionPaletteHeight;
    private bool _workspaceSplitInitialized;
    private bool _updatingWorkspaceSplit;
    private bool _showComboBadges = true;
    private bool? _compactWorkspaceNavigation;

    public KeymapEditorForm(
        Ack05Configuration configuration,
        string? path,
        string? savedBaselineJson = null,
        string? loadedFileFingerprint = null)
    {
        _configuration = Normalize(configuration);
        _savedBaselineJson = savedBaselineJson ?? Ack05ConfigurationJson.Serialize(_configuration);
        _path = path;
        _loadedFileFingerprint = loadedFileFingerprint ?? RecoveryDraftStore.Fingerprint(path);
        Text = "Instrument Workbench — ACK05 Enhanced";
        Width = 1420;
        Height = 940;
        MinimumSize = new Size(1080, 740);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = InstrumentTheme.Canvas;
        ForeColor = InstrumentTheme.Ink;
        Font = new Font("Cascadia Mono", 9F, FontStyle.Regular, GraphicsUnit.Point);
        DoubleBuffered = true;
        KeyPreview = true;
        FormClosed += (_, _) =>
        {
            Interlocked.Increment(ref _draftSaveGeneration);
            _draftSaveCancellation?.Cancel();
            _draftSaveCancellation?.Dispose();
        };

        BuildLayout();
        Shown += (_, _) => InitializeWorkspaceSplit();
        KeyDown += OnEditorKeyDown;
        _liveRuntime.StatusChanged += OnLiveStatusChanged;
        LoadProfiles();
        SelectDefaultProfile();
        RefreshDocumentState();
        UpdateStatus("Ready");
    }

    private void BuildLayout()
    {
        var root = _rootLayout;
        root.Dock = DockStyle.Fill;
        root.ColumnCount = 1;
        root.RowCount = 4;
        root.Padding = new Padding(20, 14, 20, 14);
        root.BackColor = InstrumentTheme.Canvas;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildContextBar(), 0, 1);

        _workspaceHost.Dock = DockStyle.Fill;
        _workspaceHost.BackColor = InstrumentTheme.Canvas;
        var keymap = BuildKeymapWorkspace();
        keymap.Name = "Keymap";
        var tapHold = BuildTapHoldWorkspace();
        tapHold.Name = "Tap & Hold";
        var combos = BuildComboWorkspace();
        combos.Name = "Combos";
        var profiles = BuildProfilesPanel();
        profiles.Name = "Profiles";
        _workspaceHost.Controls.AddRange([keymap, tapHold, combos, profiles]);

        _actionDock.Dock = DockStyle.Fill;
        _actionDock.Margin = Padding.Empty;
        _actionDock.Padding = new Padding(12);
        _actionDock.CornerRadius = 18;
        _actionDock.AccessibleName = "Action Palette";
        _actionDock.AccessibleDescription = "Drag the horizontal divider above to resize the action list.";
        _actionDock.Controls.Add(_actionPalette);

        _workspaceSplit.Dock = DockStyle.Fill;
        _workspaceSplit.Margin = Padding.Empty;
        _workspaceSplit.Orientation = Orientation.Horizontal;
        _workspaceSplit.FixedPanel = FixedPanel.Panel1;
        _workspaceSplit.IsSplitterFixed = false;
        _workspaceSplit.SplitterWidth = 8;
        _workspaceSplit.BackColor = InstrumentTheme.Canvas;
        _workspaceSplit.Panel1.BackColor = InstrumentTheme.Canvas;
        _workspaceSplit.Panel2.BackColor = InstrumentTheme.Canvas;
        _workspaceSplit.Panel2.Padding = new Padding(0, 4, 0, 8);
        _workspaceSplit.AccessibleName = "Resizable editor and Action Palette";
        _workspaceSplit.SplitterMoved += (_, _) => RememberActionPaletteHeight();
        _workspaceSplit.SizeChanged += (_, _) => InitializeWorkspaceSplit();
        _workspaceSplit.Panel1.Controls.Add(_workspaceHost);
        _workspaceSplit.Panel2.Controls.Add(_actionDock);
        root.Controls.Add(_workspaceSplit, 0, 2);
        root.Controls.Add(BuildStatusBar(), 0, 3);
        ShowWorkspace("Keymap");
    }

    private void InitializeWorkspaceSplit()
    {
        if (_workspaceSplitInitialized || _workspaceSplit.Panel2Collapsed)
        {
            return;
        }

        if (_workspaceSplit.Height <=
            MinimumWorkspaceHeight + MinimumActionPaletteHeight + _workspaceSplit.SplitterWidth)
        {
            return;
        }

        var minimumDistance = MinimumWorkspaceHeight;
        var maximumDistance = _workspaceSplit.Height -
            MinimumActionPaletteHeight - _workspaceSplit.SplitterWidth;

        _updatingWorkspaceSplit = true;
        try
        {
            // Set the distance before the minimums. WinForms validates the current
            // design-time distance immediately when a panel minimum changes.
            _workspaceSplit.SplitterDistance = Math.Clamp(
                _workspaceSplit.Height - PreferredActionPaletteHeight - _workspaceSplit.SplitterWidth,
                minimumDistance,
                maximumDistance);
            _workspaceSplit.Panel1MinSize = MinimumWorkspaceHeight;
            _workspaceSplit.Panel2MinSize = MinimumActionPaletteHeight;
            _lastActionPaletteHeight = _workspaceSplit.Panel2.Height;
            _workspaceSplitInitialized = true;
        }
        finally
        {
            _updatingWorkspaceSplit = false;
        }
    }

    private void RememberActionPaletteHeight()
    {
        if (_updatingWorkspaceSplit ||
            !_workspaceSplitInitialized ||
            _workspaceSplit.Panel2Collapsed)
        {
            return;
        }

        _lastActionPaletteHeight = Math.Max(
            MinimumActionPaletteHeight,
            _workspaceSplit.Panel2.Height);
    }

    private void RestoreActionPaletteHeight()
    {
        InitializeWorkspaceSplit();
        if (!_workspaceSplitInitialized || _workspaceSplit.Panel2Collapsed)
        {
            return;
        }

        var maximumPaletteHeight = _workspaceSplit.Height -
            MinimumWorkspaceHeight - _workspaceSplit.SplitterWidth;
        if (maximumPaletteHeight < MinimumActionPaletteHeight)
        {
            return;
        }

        var paletteHeight = Math.Clamp(
            _lastActionPaletteHeight,
            MinimumActionPaletteHeight,
            maximumPaletteHeight);
        _updatingWorkspaceSplit = true;
        try
        {
            _workspaceSplit.SplitterDistance = _workspaceSplit.Height -
                paletteHeight - _workspaceSplit.SplitterWidth;
        }
        finally
        {
            _updatingWorkspaceSplit = false;
        }
    }

    private Control BuildHeader()
    {
        var header = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = InstrumentTheme.Canvas,
            Margin = Padding.Empty,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Keep the full action row visible at 100% DPI, including control margins.
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 472));

        var brand = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = Padding.Empty };
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        brand.Controls.Add(new Label
        {
            Text = "Instrument Workbench",
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 13F, FontStyle.Regular),
            TextAlign = ContentAlignment.BottomLeft,
        }, 0, 0);
        brand.Controls.Add(new Label
        {
            Text = "ACK05 Enhanced   •   Keymap Editor",
            Dock = DockStyle.Fill,
            ForeColor = InstrumentTheme.Muted,
            TextAlign = ContentAlignment.TopLeft,
        }, 0, 1);
        header.Controls.Add(brand, 0, 0);

        var navigation = _workspaceNavigation;
        navigation.Dock = DockStyle.Fill;
        navigation.FlowDirection = FlowDirection.LeftToRight;
        navigation.WrapContents = false;
        navigation.Padding = new Padding(0, 12, 0, 8);
        navigation.BackColor = InstrumentTheme.Canvas;
        navigation.SizeChanged += (_, _) => UpdateWorkspaceNavigationMode();

        _workspaceMenuButton.Width = 156;
        _workspaceMenuButton.Visible = false;
        _workspaceMenuButton.AccessibleDescription =
            "Open the workspace navigation menu. All workspaces remain available at compact widths.";
        _workspaceMenuButton.Click += (_, _) =>
            _workspaceMenu.Show(_workspaceMenuButton, new Point(0, _workspaceMenuButton.Height));
        navigation.Controls.Add(_workspaceMenuButton);

        foreach (var name in new[] { "Keymap", "Tap & Hold", "Combos", "Profiles" })
        {
            var button = new InstrumentButton { Text = name, Width = name == "Tap & Hold" ? 118 : 96 };
            button.Click += (_, _) => ShowWorkspace(name);
            _workspaceButtons[name] = button;
            navigation.Controls.Add(button);

            var menuItem = new ToolStripMenuItem(name);
            menuItem.Click += (_, _) => ShowWorkspace(name);
            _workspaceMenuItems[name] = menuItem;
            _workspaceMenu.Items.Add(menuItem);
        }
        header.Controls.Add(navigation, 1, 0);

        var actions = new BufferedFlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 8),
            BackColor = InstrumentTheme.Canvas,
        };
        _save.Text = "Save";
        _save.Width = 78;
        _save.Height = 38;
        _save.FlatStyle = FlatStyle.Flat;
        _save.BackColor = InstrumentTheme.Ink;
        _save.ForeColor = Color.White;
        _save.FlatAppearance.BorderSize = 0;
        _save.Click += (_, _) => SaveConfig(saveAs: false);
        actions.Controls.Add(_save);

        _revert.Text = "Revert";
        _revert.Width = 76;
        _revert.Click += (_, _) => RevertToSaved();
        actions.Controls.Add(_revert);
        _redo.Text = "Redo";
        _redo.Width = 64;
        _redo.Click += (_, _) => Redo();
        actions.Controls.Add(_redo);
        _undo.Text = "Undo";
        _undo.Width = 64;
        _undo.Click += (_, _) => Undo();
        actions.Controls.Add(_undo);

        _live.Text = "Output OFF";
        _live.Width = 104;
        _live.AccessibleName = "ACK05 output off";
        _live.AccessibleDescription = "Enable ACK05 device capture and Windows keyboard or mouse output.";
        _live.Click += async (_, _) => await ToggleLiveAsync();
        actions.Controls.Add(_live);

        var more = new InstrumentButton { Text = "•••", Width = 48 };
        var menu = new ContextMenuStrip();
        menu.Items.Add("New Default", null, (_, _) => NewDefault());
        menu.Items.Add("Open...", null, (_, _) => OpenConfig());
        menu.Items.Add("Save As...", null, (_, _) => SaveConfig(saveAs: true));
        menu.Items.Add("Validate", null, (_, _) => ValidateCurrent(showSuccess: true));
        more.Click += (_, _) => menu.Show(more, new Point(0, more.Height));
        actions.Controls.Add(more);
        header.Controls.Add(actions, 2, 0);
        return header;
    }

    private Control BuildContextBar()
    {
        var bar = new BufferedFlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = InstrumentTheme.Canvas,
            Padding = new Padding(0, 5, 0, 7),
        };
        bar.Controls.Add(ContextLabel("PROFILE"));
        _profiles.Width = 210;
        _profiles.Height = 38;
        _profiles.DropDownStyle = ComboBoxStyle.DropDownList;
        _profiles.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
        bar.Controls.Add(_profiles);
        var addProfile = new InstrumentButton { Text = "+", Width = 40 };
        addProfile.Click += (_, _) => AddProfile();
        bar.Controls.Add(addProfile);

        var spacer = new Label { Width = 34, Height = 38 };
        bar.Controls.Add(spacer);
        bar.Controls.Add(ContextLabel("LAYER"));
        _layers.Width = 360;
        _layers.Height = 42;
        _layers.Appearance = TabAppearance.FlatButtons;
        _layers.ItemSize = new Size(54, 32);
        _layers.SizeMode = TabSizeMode.Fixed;
        _layers.SelectedIndexChanged += (_, _) =>
        {
            if (_layers.SelectedTab?.Tag is int layerIndex)
            {
                _selectedLayer = layerIndex;
            }

            var loadBindingEditor = string.Equals(
                _activeWorkspace,
                "Keymap",
                StringComparison.Ordinal);
            LoadBindings(loadBindingEditor);
            if (string.Equals(_activeWorkspace, "Tap & Hold", StringComparison.Ordinal))
            {
                LoadSelectedTapHold();
            }
            else if (string.Equals(_activeWorkspace, "Combos", StringComparison.Ordinal))
            {
                LoadSelectedCombo();
            }
        };
        bar.Controls.Add(_layers);
        var addLayer = new InstrumentButton { Text = "+", Width = 40 };
        addLayer.Click += (_, _) => AddLayer();
        bar.Controls.Add(addLayer);
        var removeLayer = new InstrumentButton { Text = "−", Width = 40 };
        removeLayer.Click += (_, _) => RemoveLastLayer();
        bar.Controls.Add(removeLayer);
        return bar;
    }

    private static Label ContextLabel(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Width = 72,
        Height = 38,
        ForeColor = InstrumentTheme.Muted,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private Control BuildKeymapWorkspace()
    {
        var workspace = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = InstrumentTheme.Canvas,
            Margin = Padding.Empty,
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        workspace.Controls.Add(BuildBindingsPanel(), 0, 0);
        workspace.Controls.Add(BuildActionPanel(), 1, 0);
        _deviceMap.ActionDropped += control => ApplyBindingToControl(control, selectAfterApply: true);
        return workspace;
    }

    private Control BuildTapHoldWorkspace()
    {
        var workspace = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = InstrumentTheme.Canvas,
            Margin = Padding.Empty,
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));

        var library = new InstrumentPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 8, 8),
            Padding = new Padding(20),
        };
        var libraryLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            BackColor = Color.Transparent,
        };
        libraryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        libraryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        libraryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        libraryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        libraryLayout.Controls.Add(new Label
        {
            Text = "Tap & Hold Library",
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 15F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _tapHoldReferenceSummary.Dock = DockStyle.Fill;
        _tapHoldReferenceSummary.ForeColor = InstrumentTheme.Muted;
        _tapHoldReferenceSummary.TextAlign = ContentAlignment.MiddleLeft;
        _tapHoldReferenceSummary.AutoEllipsis = true;
        libraryLayout.Controls.Add(_tapHoldReferenceSummary, 0, 1);

        _tapHoldDefinitions.Dock = DockStyle.Fill;
        _tapHoldDefinitions.BorderStyle = BorderStyle.None;
        _tapHoldDefinitions.BackColor = InstrumentTheme.Surface;
        _tapHoldDefinitions.ForeColor = InstrumentTheme.Ink;
        _tapHoldDefinitions.IntegralHeight = false;
        _tapHoldDefinitions.ItemHeight = 30;
        _tapHoldDefinitions.SelectedIndexChanged += (_, _) => LoadSelectedTapHold();
        libraryLayout.Controls.Add(_tapHoldDefinitions, 0, 2);

        var definitionButtons = new BufferedFlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 6, 0, 0),
        };
        var add = new InstrumentButton { Text = "+ New", Width = 96, Primary = true };
        add.Click += (_, _) => AddTapHoldDefinition();
        var delete = new InstrumentButton { Text = "Delete", Width = 88 };
        delete.Click += (_, _) => DeleteTapHoldDefinition();
        definitionButtons.Controls.AddRange([add, delete]);
        libraryLayout.Controls.Add(definitionButtons, 0, 3);
        library.Controls.Add(libraryLayout);
        workspace.Controls.Add(library, 0, 0);

        var mapShell = new InstrumentPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8),
            Padding = new Padding(18),
        };
        var mapLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            BackColor = Color.Transparent,
        };
        mapLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        mapLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        mapLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mapLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        _tapHoldMapSummary.Dock = DockStyle.Fill;
        _tapHoldMapSummary.Font = new Font(Font.FontFamily, 13F, FontStyle.Regular);
        _tapHoldMapSummary.TextAlign = ContentAlignment.MiddleCenter;
        _tapHoldMapSummary.AutoEllipsis = true;
        mapLayout.Controls.Add(_tapHoldMapSummary, 0, 0);
        _tapHoldMapHint.Dock = DockStyle.Fill;
        _tapHoldMapHint.ForeColor = InstrumentTheme.Muted;
        _tapHoldMapHint.TextAlign = ContentAlignment.MiddleCenter;
        _tapHoldMapHint.AutoEllipsis = true;
        mapLayout.Controls.Add(_tapHoldMapHint, 0, 1);
        _tapHoldDeviceMap.ControlSelected += SelectTapHoldTarget;
        mapLayout.Controls.Add(_tapHoldDeviceMap, 0, 2);
        _assignTapHold.Text = "Assign to selected key";
        _assignTapHold.Dock = DockStyle.Fill;
        _assignTapHold.Margin = new Padding(4, 8, 4, 0);
        _assignTapHold.Primary = true;
        _assignTapHold.Click += (_, _) => AssignTapHoldToSelectedKey();
        mapLayout.Controls.Add(_assignTapHold, 0, 3);
        mapShell.Controls.Add(mapLayout);
        workspace.Controls.Add(mapShell, 1, 0);

        var editorShell = new InstrumentPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 8, 0, 8),
            Padding = new Padding(22, 16, 22, 16),
        };
        var editor = new BufferedFlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
        };
        _workspaceInspectors["Tap & Hold"] = editor;
        editor.Resize += (_, _) =>
        {
            foreach (Control control in editor.Controls)
            {
                control.Width = Math.Max(220, editor.ClientSize.Width - 22);
            }
        };
        editor.Controls.Add(InspectorLabel("TAP & HOLD SETTINGS", 30));

        _tapHoldName.Dock = DockStyle.Fill;
        _tapHoldName.Leave += (_, _) => CommitTapHoldMetadata();
        editor.Controls.Add(InspectorField("Name", "Reusable label shown in the keymap.", _tapHoldName));

        _tapHoldTerm.Minimum = Ack05LayerSwitchSettings.MinimumTapHoldThresholdMs;
        _tapHoldTerm.Maximum = Ack05LayerSwitchSettings.MaximumTapHoldThresholdMs;
        _tapHoldTerm.Increment = 25;
        _tapHoldTerm.Dock = DockStyle.Fill;
        _tapHoldTerm.ValueChanged += (_, _) => CommitTapHoldMetadata();
        editor.Controls.Add(InspectorField("Tapping term", "Release before this time for Tap.", _tapHoldTerm));

        _tapHoldInterrupt.DropDownStyle = ComboBoxStyle.DropDownList;
        _tapHoldInterrupt.Dock = DockStyle.Fill;
        _tapHoldInterrupt.Items.AddRange(
        [
            "Hold on other key press",
            "Hold on other key tap",
            "Timeout only",
        ]);
        _tapHoldInterrupt.SelectedIndexChanged += (_, _) => CommitTapHoldMetadata();
        editor.Controls.Add(InspectorField("Interrupt", "How another physical key resolves Hold.", _tapHoldInterrupt));

        var slots = new BufferedTableLayoutPanel
        {
            Height = 72,
            ColumnCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 10, 0, 6),
        };
        slots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        slots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _tapSlotButton.Height = 62;
        _tapSlotButton.Dock = DockStyle.Fill;
        _tapSlotButton.Margin = new Padding(0, 0, 6, 0);
        _tapSlotButton.Click += (_, _) => SelectTapHoldSlot(TapHoldSlot.Tap);
        _holdSlotButton.Height = 62;
        _holdSlotButton.Dock = DockStyle.Fill;
        _holdSlotButton.Margin = new Padding(6, 0, 0, 0);
        _holdSlotButton.Click += (_, _) => SelectTapHoldSlot(TapHoldSlot.Hold);
        slots.Controls.Add(_tapSlotButton, 0, 0);
        slots.Controls.Add(_holdSlotButton, 1, 0);
        editor.Controls.Add(slots);

        _tapHoldTargetLayer.DropDownStyle = ComboBoxStyle.DropDownList;
        _tapHoldTargetLayer.Dock = DockStyle.Fill;
        _tapHoldTargetLayer.SelectedIndexChanged += (_, _) => CommitTapHoldSlotParameters();
        editor.Controls.Add(InspectorField("Target layer", "Used by TG on Tap or MO on Hold.", _tapHoldTargetLayer));

        _tapHoldWheelSteps.Minimum = -10;
        _tapHoldWheelSteps.Maximum = 10;
        _tapHoldWheelSteps.Value = 1;
        _tapHoldWheelSteps.Dock = DockStyle.Fill;
        _tapHoldWheelSteps.ValueChanged += (_, _) => CommitTapHoldSlotParameters();
        editor.Controls.Add(InspectorField("Wheel steps", "Used only by a Tap mouse-wheel action.", _tapHoldWheelSteps));

        editorShell.Controls.Add(editor);
        workspace.Controls.Add(editorShell, 2, 0);
        return workspace;
    }

    private Control BuildComboWorkspace()
    {
        var workspace = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = InstrumentTheme.Canvas,
            Margin = Padding.Empty,
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));

        var library = new InstrumentPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 8, 8),
            Padding = new Padding(20),
        };
        var libraryLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            BackColor = Color.Transparent,
        };
        libraryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        libraryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        libraryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        libraryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        libraryLayout.Controls.Add(new Label
        {
            Text = "Combos",
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 20F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        libraryLayout.Controls.Add(new Label
        {
            Text = "Exactly two physical keys",
            Dock = DockStyle.Fill,
            ForeColor = InstrumentTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 1);
        _comboDefinitions.Dock = DockStyle.Fill;
        _comboDefinitions.BorderStyle = BorderStyle.None;
        _comboDefinitions.BackColor = InstrumentTheme.Surface;
        _comboDefinitions.SelectedIndexChanged += (_, _) => LoadSelectedCombo();
        libraryLayout.Controls.Add(_comboDefinitions, 0, 2);
        var buttons = new BufferedFlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 6, 0, 0),
        };
        var add = new InstrumentButton { Text = "+ New", Width = 92, Primary = true };
        add.Click += (_, _) => AddComboDefinition();
        var delete = new InstrumentButton { Text = "Delete", Width = 82 };
        delete.Click += (_, _) => DeleteComboDefinition();
        buttons.Controls.AddRange([add, delete]);
        libraryLayout.Controls.Add(buttons, 0, 3);
        library.Controls.Add(libraryLayout);
        workspace.Controls.Add(library, 0, 0);

        var mapShell = new InstrumentPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8),
            Padding = new Padding(18),
        };
        var mapLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            BackColor = Color.Transparent,
        };
        mapLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        mapLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        mapLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _comboMembers.Dock = DockStyle.Fill;
        _comboMembers.Font = new Font(Font.FontFamily, 15F, FontStyle.Regular);
        _comboMembers.TextAlign = ContentAlignment.MiddleCenter;
        mapLayout.Controls.Add(_comboMembers, 0, 0);
        _comboSelectionHint.Dock = DockStyle.Fill;
        _comboSelectionHint.ForeColor = InstrumentTheme.Muted;
        _comboSelectionHint.TextAlign = ContentAlignment.MiddleCenter;
        mapLayout.Controls.Add(_comboSelectionHint, 0, 1);
        _comboDeviceMap.ControlSelected += SelectComboMember;
        mapLayout.Controls.Add(_comboDeviceMap, 0, 2);
        mapShell.Controls.Add(mapLayout);
        workspace.Controls.Add(mapShell, 1, 0);

        var editorShell = new InstrumentPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 8, 0, 8),
            Padding = new Padding(22, 16, 22, 16),
        };
        var editor = new BufferedFlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
        };
        _workspaceInspectors["Combos"] = editor;
        editor.Resize += (_, _) =>
        {
            foreach (Control control in editor.Controls)
            {
                control.Width = Math.Max(220, editor.ClientSize.Width - 22);
            }
        };
        editor.Controls.Add(InspectorLabel("COMBO SETTINGS", 30));
        _comboName.Dock = DockStyle.Fill;
        _comboName.Leave += (_, _) => CommitComboMetadata();
        editor.Controls.Add(InspectorField("Name", "Label shown in the Combo list.", _comboName));

        _comboProfileTerm.Minimum = Ack05ComboDefaults.MinimumTermMs;
        _comboProfileTerm.Maximum = Ack05ComboDefaults.MaximumTermMs;
        _comboProfileTerm.Value = Ack05ComboDefaults.DefaultTermMs;
        _comboProfileTerm.Dock = DockStyle.Fill;
        _comboProfileTerm.ValueChanged += (_, _) => CommitComboProfileTerm();
        editor.Controls.Add(InspectorField("Profile Combo Term", "Default recognition window in milliseconds.", _comboProfileTerm));

        _comboUseDefaultTerm.Text = "Use profile default";
        _comboUseDefaultTerm.Height = 30;
        _comboUseDefaultTerm.CheckedChanged += (_, _) => CommitComboMetadata();
        editor.Controls.Add(_comboUseDefaultTerm);
        _comboTerm.Minimum = Ack05ComboDefaults.MinimumTermMs;
        _comboTerm.Maximum = Ack05ComboDefaults.MaximumTermMs;
        _comboTerm.Value = Ack05ComboDefaults.DefaultTermMs;
        _comboTerm.Dock = DockStyle.Fill;
        _comboTerm.ValueChanged += (_, _) => CommitComboMetadata();
        editor.Controls.Add(InspectorField("This Combo Term", "Override only for this definition.", _comboTerm));

        _comboLayers.Height = 88;
        _comboLayers.CheckOnClick = true;
        _comboLayers.BorderStyle = BorderStyle.FixedSingle;
        _comboLayers.ItemCheck += (_, _) =>
        {
            if (_comboEditorLoading)
            {
                return;
            }

            BeginInvoke(CommitComboMetadata);
        };
        editor.Controls.Add(InspectorField(
            "Layer scope",
            "Combo is eligible only on checked layers.",
            _comboLayers,
            88));

        _comboOrdered.Text = "Require displayed key order";
        _comboOrdered.Height = 30;
        _comboOrdered.CheckedChanged += (_, _) => CommitComboMetadata();
        editor.Controls.Add(_comboOrdered);

        _comboTrigger.DropDownStyle = ComboBoxStyle.DropDownList;
        _comboTrigger.Items.AddRange(["On second press", "Tap only", "Hold together"]);
        _comboTrigger.Dock = DockStyle.Fill;
        _comboTrigger.SelectedIndexChanged += (_, _) => CommitComboMetadata();
        editor.Controls.Add(InspectorField("Trigger", "When the Combo output begins.", _comboTrigger));

        _comboHoldTerm.Minimum = Ack05ComboDefinition.MinimumHoldTermMs;
        _comboHoldTerm.Maximum = Ack05ComboDefinition.MaximumHoldTermMs;
        _comboHoldTerm.Value = Ack05ComboDefinition.DefaultHoldTermMs;
        _comboHoldTerm.Dock = DockStyle.Fill;
        _comboHoldTerm.ValueChanged += (_, _) => CommitComboMetadata();
        editor.Controls.Add(InspectorField("Hold Term", "Used by Tap only and Hold together.", _comboHoldTerm));

        _comboRelease.DropDownStyle = ComboBoxStyle.DropDownList;
        _comboRelease.Items.AddRange(["First member released", "All members released"]);
        _comboRelease.Dock = DockStyle.Fill;
        _comboRelease.SelectedIndexChanged += (_, _) => CommitComboMetadata();
        editor.Controls.Add(InspectorField("Release", "When a held output is released.", _comboRelease));

        _comboOutput.Height = 48;
        _comboOutput.Font = new Font(Font.FontFamily, 13F, FontStyle.Regular);
        _comboOutput.TextAlign = ContentAlignment.MiddleLeft;
        editor.Controls.Add(_comboOutput);

        _comboTargetLayer.DropDownStyle = ComboBoxStyle.DropDownList;
        _comboTargetLayer.Dock = DockStyle.Fill;
        _comboTargetLayer.SelectedIndexChanged += (_, _) => CommitComboOutputParameters();
        editor.Controls.Add(InspectorField("Output target layer", "Used by MO or TG output.", _comboTargetLayer));
        _comboWheelSteps.Minimum = -10;
        _comboWheelSteps.Maximum = 10;
        _comboWheelSteps.Value = 1;
        _comboWheelSteps.Dock = DockStyle.Fill;
        _comboWheelSteps.ValueChanged += (_, _) => CommitComboOutputParameters();
        editor.Controls.Add(InspectorField("Output wheel steps", "Used by Mouse Wheel output.", _comboWheelSteps));

        editorShell.Controls.Add(editor);
        workspace.Controls.Add(editorShell, 2, 0);
        return workspace;
    }

    private Control BuildFutureWorkspace(string title, string description)
    {
        var panel = new InstrumentPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 8),
            Padding = new Padding(42),
        };
        var content = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, BackColor = Color.Transparent };
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        content.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 22F, FontStyle.Regular),
            TextAlign = ContentAlignment.BottomCenter,
        }, 0, 0);
        content.Controls.Add(new Label
        {
            Text = description,
            Dock = DockStyle.Fill,
            ForeColor = InstrumentTheme.Muted,
            TextAlign = ContentAlignment.MiddleCenter,
        }, 0, 1);
        panel.Controls.Add(content);
        return panel;
    }

    private Control BuildStatusBar()
    {
        _status.Dock = DockStyle.Fill;
        _status.ForeColor = InstrumentTheme.Muted;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        return _status;
    }

    private void ShowWorkspace(string name)
    {
        _activeWorkspace = name;
        foreach (Control control in _workspaceHost.Controls)
        {
            control.Visible = string.Equals(control.Name, name, StringComparison.Ordinal);
        }

        foreach (var pair in _workspaceButtons)
        {
            pair.Value.Selected = string.Equals(pair.Key, name, StringComparison.Ordinal);
        }

        _workspaceMenuButton.Text = $"{name}  ▾";
        _workspaceMenuButton.AccessibleName = $"Workspace: {name}";
        foreach (var pair in _workspaceMenuItems)
        {
            pair.Value.Checked = string.Equals(pair.Key, name, StringComparison.Ordinal);
        }

        var usesActionDock = string.Equals(name, "Keymap", StringComparison.Ordinal) ||
            string.Equals(name, "Tap & Hold", StringComparison.Ordinal) ||
            string.Equals(name, "Combos", StringComparison.Ordinal);
        if (!usesActionDock && !_workspaceSplit.Panel2Collapsed)
        {
            RememberActionPaletteHeight();
        }

        var actionDockWasCollapsed = _workspaceSplit.Panel2Collapsed;
        _workspaceSplit.Panel2Collapsed = !usesActionDock;
        if (usesActionDock && actionDockWasCollapsed)
        {
            RestoreActionPaletteHeight();
        }

        if (string.Equals(name, "Tap & Hold", StringComparison.Ordinal))
        {
            _tapHoldSelectedControl ??= CurrentControl() ?? Ack05Control.K1;
            LoadTapHoldDefinitions();
        }
        else if (string.Equals(name, "Combos", StringComparison.Ordinal))
        {
            LoadComboDefinitions();
        }
        else if (string.Equals(name, "Keymap", StringComparison.Ordinal))
        {
            _actionPalette.SetUsage(Ack05ActionPalette.Usage.Binding);
            if (CurrentProfile() is { } profile)
            {
                RefreshKeymapMap(profile);
            }
            LoadSelectedBinding();
        }

        ResetWorkspaceInspector(name);
    }

    private void UpdateWorkspaceNavigationMode()
    {
        if (_workspaceButtons.Count == 0 || _workspaceNavigation.IsDisposed)
        {
            return;
        }

        var expandedWidth = _workspaceButtons.Values.Sum(button =>
            button.Width + button.Margin.Horizontal);
        var useCompact = _workspaceNavigation.ClientSize.Width < expandedWidth;
        if (_compactWorkspaceNavigation == useCompact)
        {
            return;
        }

        _compactWorkspaceNavigation = useCompact;
        _workspaceNavigation.SuspendLayout();
        try
        {
            foreach (var button in _workspaceButtons.Values)
            {
                button.Visible = !useCompact;
            }

            _workspaceMenuButton.Visible = useCompact;
        }
        finally
        {
            _workspaceNavigation.ResumeLayout(performLayout: true);
        }
    }

    private void ResetWorkspaceInspector(string name)
    {
        if (!_workspaceInspectors.TryGetValue(name, out var inspector))
        {
            return;
        }

        inspector.AutoScrollPosition = Point.Empty;
        inspector.PerformLayout();
    }

    private Control BuildProfilesPanel()
    {
        var shell = new InstrumentPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 8),
            Padding = new Padding(48),
        };
        var panel = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 8,
            ColumnCount = 2,
            BackColor = Color.Transparent,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = "Profile Settings",
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 20F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        panel.SetColumnSpan(panel.GetControlFromPosition(0, 0)!, 2);

        panel.Controls.Add(ProfileFieldLabel("Profile ID"), 0, 1);
        _profileId.Dock = DockStyle.Fill;
        panel.Controls.Add(_profileId, 1, 1);
        panel.Controls.Add(ProfileFieldLabel("Display name"), 0, 3);
        _profileName.Dock = DockStyle.Fill;
        panel.Controls.Add(_profileName, 1, 3);
        panel.Controls.Add(ProfileFieldLabel("Foreground process"), 0, 5);
        _processName.Dock = DockStyle.Fill;
        _processName.PlaceholderText = "AfterFX.exe";
        panel.Controls.Add(_processName, 1, 5);

        var buttons = new BufferedFlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
        };
        var apply = new InstrumentButton { Text = "Update profile", Width = 132, Primary = true };
        apply.Click += (_, _) => ApplyProfileFields();
        var add = new InstrumentButton { Text = "Add profile", Width = 112 };
        add.Click += (_, _) => AddProfile();
        var delete = new InstrumentButton { Text = "Delete", Width = 88 };
        delete.Click += (_, _) => DeleteProfile();
        buttons.Controls.AddRange([apply, add, delete]);
        panel.Controls.Add(buttons, 1, 7);

        shell.Controls.Add(panel);
        return shell;
    }

    private static Label ProfileFieldLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        ForeColor = InstrumentTheme.Muted,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private Control BuildBindingsPanel()
    {
        var panel = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 2,
            BackColor = InstrumentTheme.Canvas,
            Margin = new Padding(0, 8, 10, 0),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = "Select a key, then choose or drag an action.",
            Dock = DockStyle.Fill,
            ForeColor = InstrumentTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
        }, 0, 0);

        _comboVisibility.Text = "Combos 0";
        _comboVisibility.Selected = true;
        _comboVisibility.Dock = DockStyle.Fill;
        _comboVisibility.Margin = new Padding(4, 2, 0, 2);
        _comboVisibility.AccessibleDescription =
            "Show or hide current-layer Combo badges on the Keymap without changing configuration.";
        _comboVisibility.Click += (_, _) => ToggleComboBadges();
        panel.Controls.Add(_comboVisibility, 1, 0);

        _deviceMap.ControlSelected += SelectControl;
        _deviceMap.ComboSelected += OpenComboFromKeymap;
        panel.Controls.Add(_deviceMap, 0, 1);
        panel.SetColumnSpan(_deviceMap, 2);

        _bindings.AllowUserToAddRows = false;
        _bindings.AllowUserToDeleteRows = false;
        _bindings.AllowUserToResizeRows = false;
        _bindings.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _bindings.BackgroundColor = InstrumentTheme.Surface;
        _bindings.BorderStyle = BorderStyle.None;
        _bindings.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _bindings.Visible = false;
        _bindings.MultiSelect = false;
        _bindings.ReadOnly = true;
        _bindings.RowHeadersVisible = false;
        _bindings.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _bindings.Columns.Add("Control", "Control");
        _bindings.Columns.Add("Action", "Action");
        _bindings.SelectionChanged += (_, _) => LoadSelectedBinding();
        return panel;
    }

    private Control BuildActionPanel()
    {
        var shell = new InstrumentPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(10, 8, 0, 0),
            Padding = new Padding(24, 20, 24, 18),
            CornerRadius = 18,
        };
        var panel = new BufferedFlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Padding = Padding.Empty,
        };
        _workspaceInspectors["Keymap"] = panel;
        shell.Resize += (_, _) =>
        {
            foreach (Control control in panel.Controls)
            {
                control.Width = Math.Max(120, panel.ClientSize.Width - 22);
            }
        };

        panel.Controls.Add(InspectorLabel("KEY SETTINGS", 26));
        _selectedKeyCaption.Text = "Selected key";
        _selectedKeyCaption.Height = 26;
        _selectedKeyCaption.ForeColor = InstrumentTheme.Muted;
        panel.Controls.Add(_selectedKeyCaption);
        _selectedKeyTitle.Height = 52;
        _selectedKeyTitle.Font = new Font(Font.FontFamily, 22F, FontStyle.Regular);
        _selectedKeyTitle.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(_selectedKeyTitle);
        panel.Controls.Add(InspectorLabel("Current action", 30));

        var currentAction = new InstrumentPanel
        {
            Height = 64,
            CornerRadius = 10,
            SurfaceColor = InstrumentTheme.Raised,
            Padding = new Padding(16, 10, 12, 10),
        };
        _actionSummary.AutoEllipsis = true;
        _actionSummary.Dock = DockStyle.Fill;
        _actionSummary.Font = new Font(Font.FontFamily, 14F, FontStyle.Regular);
        _actionSummary.TextAlign = ContentAlignment.MiddleLeft;
        currentAction.Controls.Add(_actionSummary);
        panel.Controls.Add(currentAction);

        _targetLayer.Dock = DockStyle.Fill;
        _targetLayer.DropDownStyle = ComboBoxStyle.DropDownList;
        _targetLayer.SelectedIndexChanged += (_, _) => OnBindingParameterChanged();
        panel.Controls.Add(InspectorField("Target layer", "Layer to activate while the key is held.", _targetLayer));

        _actionTapHoldThreshold.Minimum = Ack05LayerSwitchSettings.MinimumTapHoldThresholdMs;
        _actionTapHoldThreshold.Maximum = Ack05LayerSwitchSettings.MaximumTapHoldThresholdMs;
        _actionTapHoldThreshold.Increment = 25;
        _actionTapHoldThreshold.Value = Ack05LayerSwitchSettings.DefaultTapHoldThresholdMs;
        _actionTapHoldThreshold.Dock = DockStyle.Fill;
        _actionTapHoldThreshold.ValueChanged += (_, _) => OnBindingParameterChanged();
        panel.Controls.Add(InspectorField("Tap / Hold", "Decision threshold in milliseconds.", _actionTapHoldThreshold));

        _wheelSteps.Dock = DockStyle.Fill;
        _wheelSteps.Minimum = -10;
        _wheelSteps.Maximum = 10;
        _wheelSteps.Value = 1;
        _wheelSteps.ValueChanged += (_, _) => OnBindingParameterChanged();
        panel.Controls.Add(InspectorField("Wheel steps", "Signed steps emitted for one roller pulse.", _wheelSteps));

        var hintCard = new InstrumentPanel
        {
            Height = 64,
            CornerRadius = 10,
            SurfaceColor = Color.FromArgb(236, 237, 234),
            Padding = new Padding(14, 10, 14, 10),
        };
        _inspectorHint.Dock = DockStyle.Fill;
        _inspectorHint.ForeColor = InstrumentTheme.Muted;
        _inspectorHint.TextAlign = ContentAlignment.MiddleLeft;
        hintCard.Controls.Add(_inspectorHint);
        panel.Controls.Add(hintCard);

        _advancedToggle.Text = "Advanced  ▾";
        _advancedToggle.Height = 40;
        _advancedToggle.TextAlign = ContentAlignment.MiddleLeft;
        _advancedToggle.Click += (_, _) => ToggleAdvanced();
        panel.Controls.Add(_advancedToggle);

        _advancedPanel.Height = 0;
        _advancedPanel.ExpandedHeight = 92;
        _advancedPanel.BackColor = Color.Transparent;

        _captureShortcut.Text = "Capture Keyboard Shortcut...";
        _captureShortcut.Dock = DockStyle.Top;
        _captureShortcut.Height = 36;
        _captureShortcut.Click += (_, _) => CaptureKeyboardShortcut();
        _advancedPanel.Controls.Add(_captureShortcut);

        _applyBinding.Text = "Apply current draft";
        _applyBinding.Dock = DockStyle.Top;
        _applyBinding.Height = 36;
        _applyBinding.Click += (_, _) => ApplyBinding();
        _advancedPanel.Controls.Add(_applyBinding);
        _applyBinding.BringToFront();
        panel.Controls.Add(_advancedPanel);

        _actionPalette.SelectionChanged += (_, _) => OnActionPaletteSelectionChanged();
        shell.Controls.Add(panel);
        return shell;
    }

    private static Label InspectorLabel(string text, int height) => new()
    {
        Text = text,
        Height = height,
        ForeColor = InstrumentTheme.Muted,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static Control InspectorField(
        string title,
        string description,
        Control field,
        int fieldHeight = 32)
    {
        var row = new BufferedTableLayoutPanel
        {
            Height = 44 + fieldHeight,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 6, 0, 0),
        };
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, fieldHeight));
        row.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        row.Controls.Add(new Label
        {
            Text = description,
            Dock = DockStyle.Fill,
            ForeColor = InstrumentTheme.Muted,
            Font = new Font("Cascadia Mono", 8.2F),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 1);
        row.Controls.Add(field, 0, 2);
        return row;
    }

    private static Control LabeledField(string label, Control field)
    {
        var row = new BufferedTableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        row.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        row.Controls.Add(field, 1, 0);
        return row;
    }

    private void CaptureKeyboardShortcut()
    {
        using var dialog = new KeyCaptureDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _capturedKeyOrder = dialog.CapturedKeys.ToArray();
        _actionPalette.SetKeyboardKeys(dialog.CapturedKeys);
        UpdateActionEditorEnabled();
        UpdateStatus($"Captured and applied {string.Join(" + ", dialog.CapturedKeys)}.");
    }

    private static Ack05KeyboardKey[] OrderChordKeys(IEnumerable<Ack05KeyboardKey> keys) =>
        keys.OrderBy(key => IsModifier(key) ? 0 : 1).ToArray();

    private static bool IsModifier(Ack05KeyboardKey key) =>
        key is Ack05KeyboardKey.LeftControl or Ack05KeyboardKey.RightControl or
            Ack05KeyboardKey.LeftShift or Ack05KeyboardKey.RightShift or
            Ack05KeyboardKey.LeftAlt or Ack05KeyboardKey.RightAlt or
            Ack05KeyboardKey.LeftWindows or Ack05KeyboardKey.RightWindows;

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, Dock = DockStyle.Fill };
        button.Click += (_, _) => action();
        return button;
    }

    private void LoadProfiles()
    {
        _loading = true;
        _profiles.Items.Clear();
        foreach (var profile in _configuration.Profiles)
        {
            _profiles.Items.Add(profile.Id);
        }

        _loading = false;
    }

    private void SelectDefaultProfile()
    {
        var index = Array.FindIndex(
            _configuration.Profiles,
            profile => string.Equals(
                profile.Id,
                _configuration.DefaultProfileId,
                StringComparison.OrdinalIgnoreCase));
        _profiles.SelectedIndex = Math.Max(index, 0);
    }

    private void LoadSelectedProfile()
    {
        if (_loading || CurrentProfile() is not { } profile)
        {
            return;
        }

        _profileId.Text = profile.Id;
        _profileName.Text = profile.Name;
        _processName.Text = profile.ApplicationMatch?.ProcessName ?? string.Empty;
        LoadLayerTabs(profile);
        LoadBindings();
        if (string.Equals(_activeWorkspace, "Tap & Hold", StringComparison.Ordinal))
        {
            LoadTapHoldDefinitions();
        }
        else if (string.Equals(_activeWorkspace, "Combos", StringComparison.Ordinal))
        {
            LoadComboDefinitions();
        }
    }

    private void LoadLayerTabs(Ack05Profile profile)
    {
        var requested = Math.Clamp(_selectedLayer, 0, profile.Layers.Length - 1);
        var previousEditorLoading = _bindingEditorLoading;
        _bindingEditorLoading = true;
        _layers.TabPages.Clear();
        foreach (var layer in profile.Layers.OrderBy(layer => layer.Index))
        {
            _layers.TabPages.Add(new TabPage(layer.Index.ToString()) { Tag = layer.Index });
        }

        var selected = profile.Layers
            .OrderBy(layer => layer.Index)
            .Select((layer, index) => (layer, index))
            .First(item => item.layer.Index == requested);
        _selectedLayer = selected.layer.Index;
        _layers.SelectedIndex = selected.index;

        _targetLayer.Items.Clear();
        _targetLayer.Items.AddRange(profile.Layers
            .OrderBy(layer => layer.Index)
            .Select(layer => $"Layer {layer.Index}")
            .ToArray());
        _bindingEditorLoading = previousEditorLoading;
    }

    private void LoadBindings(bool loadSelectedBinding = true)
    {
        if (CurrentProfile() is not { } profile)
        {
            return;
        }

        _bindings.Rows.Clear();
        var layer = profile.Layers.Single(item => item.Index == _selectedLayer);
        RefreshKeymapMap(profile);
        foreach (var control in BindingCandidates)
        {
            var action = layer.Bindings.SingleOrDefault(item => item.Control == control)?.Action
                ?? DefaultActionForLayer(_selectedLayer);
            _bindings.Rows.Add(control.ToString(), DescribeAction(action));
        }

        if (_bindings.Rows.Count > 0)
        {
            _bindings.Rows[0].Selected = true;
            if (loadSelectedBinding)
            {
                LoadSelectedBinding();
            }
        }
    }

    private void ToggleComboBadges()
    {
        _showComboBadges = !_showComboBadges;
        if (CurrentProfile() is { } profile)
        {
            RefreshKeymapMap(profile);
        }

        UpdateStatus(_showComboBadges
            ? "Combo badges shown on Keymap."
            : "Combo badges hidden on Keymap; definitions and output are unchanged.");
    }

    private void RefreshKeymapMap(Ack05Profile profile)
    {
        _deviceMap.LoadMap(profile, _selectedLayer, showCombos: _showComboBadges);
        var comboCount = profile.Combos.Count(combo =>
            combo.Controls.Length == 2 && combo.Layers.Contains(_selectedLayer));
        _comboVisibility.Text = _showComboBadges ? $"Combos {comboCount}" : "Combos OFF";
        _comboVisibility.Selected = _showComboBadges;
        _comboVisibility.AccessibleName = _showComboBadges
            ? $"Combo badges shown; {comboCount} on Layer {_selectedLayer}"
            : $"Combo badges hidden; {comboCount} on Layer {_selectedLayer}";
    }

    private void LoadSelectedBinding()
    {
        if (_bindings.SelectedRows.Count == 0 ||
            CurrentProfile() is not { } profile ||
            CurrentControl() is not { } control)
        {
            return;
        }

        var action = profile.Layers
            .Single(layer => layer.Index == _selectedLayer)
            .Bindings
            .SingleOrDefault(binding => binding.Control == control)
            ?.Action ?? DefaultActionForLayer(_selectedLayer);

        _bindingEditorLoading = true;
        try
        {
            _actionPalette.Enabled = true;
            _deviceMap.SelectedControl = control;
            _actionPalette.SetPulseOnly(control is
                Ack05Control.RollerClockwise or Ack05Control.RollerCounterClockwise);
            _actionPalette.LoadAction(action);
            _capturedKeyOrder = action.Keys?.ToArray();

            _wheelSteps.Value = Math.Clamp(action.WheelSteps ?? 1, -10, 10);
            var targetLayer = Math.Clamp(action.TargetLayer ?? Math.Min(1, profile.Layers.Length - 1), 0, profile.Layers.Length - 1);
            _targetLayer.SelectedItem = $"Layer {targetLayer}";
            _actionTapHoldThreshold.Value = Math.Clamp(
                action.TapHoldThresholdMs ?? Ack05LayerSwitchSettings.DefaultTapHoldThresholdMs,
                Ack05LayerSwitchSettings.MinimumTapHoldThresholdMs,
                Ack05LayerSwitchSettings.MaximumTapHoldThresholdMs);
            _selectedKeyCaption.Text = $"Selected key  •  Layer {_selectedLayer}";
            _selectedKeyTitle.Text = ControlDisplayName(control);
            UpdateActionEditorEnabled();
        }
        finally
        {
            _bindingEditorLoading = false;
        }
    }

    private void ApplyProfileFields()
    {
        if (CurrentProfile() is not { } profile)
        {
            return;
        }

        var id = _profileId.Text.Trim();
        var name = _profileName.Text.Trim();
        var process = _processName.Text.Trim();
        var updated = CloneProfile(
            profile,
            id,
            name,
            string.IsNullOrWhiteSpace(process) ? null : process);
        var profiles = _configuration.Profiles
            .Select(item => ReferenceEquals(item, profile) ? updated : item)
            .ToArray();

        var updatedConfiguration = new Ack05Configuration
        {
            SchemaVersion = _configuration.SchemaVersion,
            DefaultProfileId = string.Equals(
                _configuration.DefaultProfileId,
                profile.Id,
                StringComparison.OrdinalIgnoreCase)
                ? id
                : _configuration.DefaultProfileId,
            LayerSwitch = _configuration.LayerSwitch,
            Profiles = profiles,
            ExtensionData = _configuration.ExtensionData,
        };

        RecordMutation();
        _configuration = updatedConfiguration;
        ReloadKeepingProfile(id);
        RefreshDocumentState();
        ApplyLiveConfigurationIfValid();
    }

    private void AddProfile()
    {
        var id = UniqueProfileId("profile");
        var profile = new Ack05Profile
        {
            Id = id,
            Name = "New Profile",
            Layers = DefaultLayers(),
        };

        var updatedConfiguration = new Ack05Configuration
        {
            SchemaVersion = _configuration.SchemaVersion,
            DefaultProfileId = _configuration.DefaultProfileId,
            LayerSwitch = _configuration.LayerSwitch,
            Profiles = [.. _configuration.Profiles, profile],
            ExtensionData = _configuration.ExtensionData,
        };

        RecordMutation();
        _configuration = updatedConfiguration;
        ReloadKeepingProfile(id);
        RefreshDocumentState();
        ApplyLiveConfigurationIfValid();
    }

    private void DeleteProfile()
    {
        if (CurrentProfile() is not { } profile || _configuration.Profiles.Length == 1)
        {
            UpdateStatus("Keep at least one profile.");
            return;
        }

        var profiles = _configuration.Profiles.Where(item => !ReferenceEquals(item, profile)).ToArray();
        var defaultId = string.Equals(
            _configuration.DefaultProfileId,
            profile.Id,
            StringComparison.OrdinalIgnoreCase)
            ? profiles[0].Id
            : _configuration.DefaultProfileId;

        RecordMutation();
        _configuration = new Ack05Configuration
        {
            SchemaVersion = _configuration.SchemaVersion,
            DefaultProfileId = defaultId,
            LayerSwitch = _configuration.LayerSwitch,
            Profiles = profiles,
            ExtensionData = _configuration.ExtensionData,
        };

        ReloadKeepingProfile(defaultId);
        RefreshDocumentState();
        ApplyLiveConfigurationIfValid();
    }

    private void AddLayer()
    {
        if (CurrentProfile() is not { } profile)
        {
            return;
        }

        if (profile.Layers.Length >= Ack05Configuration.MaximumLayerCount)
        {
            UpdateStatus($"A profile can contain up to {Ack05Configuration.MaximumLayerCount} layers.");
            return;
        }

        var index = profile.Layers.Length;
        var updatedProfile = CloneProfileWithLayers(
            profile,
            [.. profile.Layers, NormalizeLayer(null, index)]);
        ReplaceProfileAndReload(profile, updatedProfile, index);
        UpdateStatus($"Added Layer {index}. Assign MO({index}) or TG({index}) to any key to reach it.");
    }

    private void RemoveLastLayer()
    {
        if (CurrentProfile() is not { } profile)
        {
            return;
        }

        if (profile.Layers.Length <= Ack05Configuration.MinimumLayerCount)
        {
            UpdateStatus($"Keep at least {Ack05Configuration.MinimumLayerCount} layers.");
            return;
        }

        var index = profile.Layers.Length - 1;
        var referencing = profile.Layers
            .SelectMany(layer => layer.Bindings.Select(binding => (layer.Index, binding)))
            .FirstOrDefault(item => item.binding.Action.TargetLayer == index);
        if (referencing.binding is not null)
        {
            UpdateStatus(
                $"Layer {index} is still referenced by Layer {referencing.Index} {referencing.binding.Control}; remove that layer action first.");
            return;
        }


        var definitionReference = profile.TapHolds.FirstOrDefault(definition =>
            definition.OnTap.TargetLayer == index || definition.OnHold.TargetLayer == index);
        if (definitionReference is not null)
        {
            UpdateStatus(
                $"Layer {index} is still referenced by Tap & Hold {definitionReference.Id}; change that slot first.");
            return;
        }

        var comboReference = profile.Combos.FirstOrDefault(definition =>
            definition.Layers.Contains(index) || definition.Action.TargetLayer == index);
        if (comboReference is not null)
        {
            UpdateStatus(
                $"Layer {index} is still referenced by Combo {comboReference.Id}; change its scope or output first.");
            return;
        }

        var updatedProfile = CloneProfileWithLayers(
            profile,
            profile.Layers.Where(layer => layer.Index != index).ToArray());
        ReplaceProfileAndReload(profile, updatedProfile, Math.Min(_selectedLayer, index - 1));
        UpdateStatus($"Removed Layer {index}.");
    }

    private void ReplaceProfileAndReload(
        Ack05Profile previous,
        Ack05Profile updated,
        int selectedLayer)
    {
        _selectedLayer = selectedLayer;
        RecordMutation();
        _configuration = new Ack05Configuration
        {
            SchemaVersion = _configuration.SchemaVersion,
            DefaultProfileId = _configuration.DefaultProfileId,
            Profiles = _configuration.Profiles
                .Select(profile => ReferenceEquals(profile, previous) ? updated : profile)
                .ToArray(),
            ExtensionData = _configuration.ExtensionData,
        };
        ReloadKeepingProfile(updated.Id);
        RefreshDocumentState();
        ApplyLiveConfigurationIfValid();
    }

    private void ApplyBinding()
    {
        if (CurrentControl() is { } control)
        {
            ApplyBindingToControl(control, selectAfterApply: true);
        }
    }

    private void ApplyBindingToControl(
        Ack05Control control,
        bool selectAfterApply,
        Ack05Action? actionOverride = null)
    {
        if (CurrentProfile() is not { } profile)
        {
            return;
        }

        var action = actionOverride ?? BuildDraftAction();
        if (action is null)
        {
            UpdateStatus("Select at least one action.");
            return;
        }

        var updatedProfile = ReplaceBinding(profile, _selectedLayer, control, action);
        var updatedConfiguration = new Ack05Configuration
        {
            SchemaVersion = _configuration.SchemaVersion,
            DefaultProfileId = _configuration.DefaultProfileId,
            LayerSwitch = _configuration.LayerSwitch,
            Profiles = _configuration.Profiles
                .Select(item => ReferenceEquals(item, profile) ? updatedProfile : item)
                .ToArray(),
            ExtensionData = _configuration.ExtensionData,
        };

        var errors = Ack05ConfigurationValidator.Validate(updatedConfiguration);
        if (errors.Count > 0)
        {
            UpdateStatus(errors[0]);
            return;
        }

        if (Ack05ConfigurationJson.Serialize(updatedConfiguration) ==
            Ack05ConfigurationJson.Serialize(_configuration))
        {
            UpdateActionSummary();
            return;
        }

        RecordMutation();
        _configuration = updatedConfiguration;
        LoadBindings();
        if (selectAfterApply)
        {
            SelectControl(control);
        }

        RefreshDocumentState();
        ApplyLiveConfigurationIfValid();
        UpdateStatus($"Applied {DescribeAction(action)} to Layer {_selectedLayer} {control}.");
    }

    private Ack05Action? BuildDraftAction()
    {
        var selectedKind = _actionPalette.SelectedKind;
        if (selectedKind is null)
        {
            return null;
        }

        return selectedKind switch
        {
            Ack05ActionKind.Disabled => Ack05Action.Disabled(),
            Ack05ActionKind.Transparent => Ack05Action.Transparent(),
            Ack05ActionKind.KeyboardChord => BuildKeyboardDraft(),
            Ack05ActionKind.MouseWheel => Ack05Action.MouseWheel((int)_wheelSteps.Value),
            Ack05ActionKind.MomentaryLayer => Ack05Action.MomentaryLayer(SelectedTargetLayer()),
            Ack05ActionKind.ToggleLayer => Ack05Action.ToggleLayer(SelectedTargetLayer()),
            Ack05ActionKind.TapToggleHoldLayer => Ack05Action.TapToggleHoldLayer(
                    SelectedTargetLayer(),
                    (int)_actionTapHoldThreshold.Value),
            _ => throw new InvalidOperationException("Unsupported action kind."),
        };
    }

    private Ack05Action BuildKeyboardDraft()
    {
        var checkedKeys = _actionPalette.SelectedKeys.ToArray();
        var keys = _capturedKeyOrder is not null &&
            _capturedKeyOrder.Length == checkedKeys.Length &&
            _capturedKeyOrder.ToHashSet().SetEquals(checkedKeys)
            ? _capturedKeyOrder
            : OrderChordKeys(checkedKeys);
        _capturedKeyOrder = keys;
        return Ack05Action.KeyboardChord(keys);
    }

    private void NewDefault()
    {
        _configuration = Normalize(Ack05Configuration.CreateDefault());
        _path = null;
        _loadedFileFingerprint = null;
        ResetHistoryBaseline();
        LoadProfiles();
        SelectDefaultProfile();
        UpdateStatus("New default configuration.");
        ApplyLiveConfigurationIfValid();
    }

    private void OpenConfig()
    {
        using var dialog = CreateOpenFileDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _configuration = Normalize(ConfigFileStore.Load(dialog.FileName));
            _path = dialog.FileName;
            _loadedFileFingerprint = RecoveryDraftStore.Fingerprint(_path);
            ResetHistoryBaseline();
            LoadProfiles();
            SelectDefaultProfile();
            UpdateStatus($"Loaded {Path.GetFileName(_path)}.");
            ApplyLiveConfigurationIfValid();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveConfig(bool saveAs)
    {
        if (!ValidateCurrent(showSuccess: false))
        {
            MessageBox.Show(this, _status.Text, "Invalid configuration", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var pathBeforeDialog = _path;
        var selectedPathInDialog = saveAs || string.IsNullOrWhiteSpace(_path);
        if (selectedPathInDialog)
        {
            using var dialog = CreateSaveFileDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _path = dialog.FileName;
        }

        if (!selectedPathInDialog &&
            !string.IsNullOrWhiteSpace(_path) &&
            File.Exists(_path) &&
            !string.Equals(
                _loadedFileFingerprint,
                RecoveryDraftStore.Fingerprint(_path),
                StringComparison.Ordinal))
        {
            var choice = MessageBox.Show(
                this,
                "The saved configuration changed outside ACK05 Enhanced.\n\n" +
                "Choose Yes to overwrite it, No to Save As, or Cancel to keep editing.",
                "Configuration changed on disk",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);
            if (choice == DialogResult.No)
            {
                _path = pathBeforeDialog;
                SaveConfig(saveAs: true);
                return;
            }
            if (choice != DialogResult.Yes)
            {
                return;
            }
        }

        try
        {
            var savePath = _path ?? throw new InvalidOperationException("Choose a configuration path before saving.");
            ConfigFileStore.SaveAtomic(savePath, _configuration);
            _loadedFileFingerprint = RecoveryDraftStore.Fingerprint(savePath);
            _savedBaselineJson = Ack05ConfigurationJson.Serialize(_configuration);
            RefreshDocumentState();
            UpdateStatus($"Saved {Path.GetFileName(savePath)}.");
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private OpenFileDialog CreateOpenFileDialog() =>
        new()
        {
            AddExtension = true,
            AutoUpgradeEnabled = false,
            CheckFileExists = true,
            DefaultExt = "json",
            Filter = "ACK05 configuration (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = FileDialogInitialDirectory(),
            RestoreDirectory = true,
        };

    private SaveFileDialog CreateSaveFileDialog() =>
        new()
        {
            AddExtension = true,
            AutoUpgradeEnabled = false,
            DefaultExt = "json",
            FileName = string.IsNullOrWhiteSpace(_path) ? "ack05-config.json" : Path.GetFileName(_path),
            Filter = "ACK05 configuration (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = FileDialogInitialDirectory(),
            OverwritePrompt = true,
            RestoreDirectory = true,
        };

    private string FileDialogInitialDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_path))
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Directory.Exists(documents) ? documents : Environment.CurrentDirectory;
    }

    private async Task ToggleLiveAsync()
    {
        _live.Enabled = false;
        try
        {
            if (_liveRuntime.IsActive)
            {
                UpdateStatus("Turning ACK05 output off...");
                await _liveRuntime.StopAsync();
                SetOutputButtonState("Output OFF", selected: false, accessibleName: "ACK05 output off");
                UpdateStatus("Output off; pressed keys released.");
                return;
            }

            if (!ValidateCurrent(showSuccess: false))
            {
                MessageBox.Show(
                    this,
                    _status.Text,
                    "Invalid configuration",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (!_liveConfirmed)
            {
                var result = MessageBox.Show(
                    this,
                    "Enable ACK05 output?\n\n" +
                    "This sends the documented ACK05 raw-mode command and emits " +
                    "Windows keyboard/mouse input. It does not change drivers, " +
                    "HidHide, firmware, or pairing.",
                    "Enable ACK05 output",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                {
                    return;
                }

                _liveConfirmed = true;
            }

            UpdateStatus("Enabling ACK05 output...");
            await _liveRuntime.StartAsync(_configuration);
            SetOutputButtonState("Output ON", selected: true, accessibleName: "ACK05 output on");
        }
        catch (Exception exception)
        {
            SetOutputButtonState("Output ERR", selected: true, accessibleName: "ACK05 output error");
            UpdateStatus($"Output error: {exception.Message}");
            MessageBox.Show(
                this,
                exception.Message,
                "ACK05 output failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _live.Enabled = true;
        }
    }

    private void ApplyLiveConfigurationIfValid()
    {
        if (!ValidateCurrent(showSuccess: false) || !_liveRuntime.IsRunning)
        {
            return;
        }

        try
        {
            _liveRuntime.UpdateConfiguration(_configuration);
            UpdateStatus("Applied configuration to active output.");
        }
        catch (Exception exception)
        {
            UpdateStatus($"Active output kept its previous configuration: {exception.Message}");
        }
    }

    private void OnLiveStatusChanged(Ack05LiveStatus status)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => OnLiveStatusChanged(status));
            }
            catch (InvalidOperationException) when (IsDisposed || Disposing)
            {
            }

            return;
        }

        switch (status.State)
        {
            case Ack05LiveState.Stopped:
                SetOutputButtonState("Output OFF", selected: false, accessibleName: "ACK05 output off");
                UpdateStatus("Output off; pressed keys released.");
                break;
            case Ack05LiveState.WaitingForDevice:
                SetOutputButtonState(
                    "Output ON",
                    selected: true,
                    accessibleName: "ACK05 output on; waiting for device");
                UpdateStatus(string.IsNullOrWhiteSpace(status.Message)
                    ? "Output on; waiting for ACK05..."
                    : $"Output on; waiting for ACK05 ({status.Message})");
                break;
            case Ack05LiveState.Connected:
                SetOutputButtonState(
                    "Output ON",
                    selected: true,
                    accessibleName: "ACK05 output on and connected");
                UpdateStatus("Output on; ACK05 connected.");
                break;
            case Ack05LiveState.Error:
                SetOutputButtonState("Output ERR", selected: true, accessibleName: "ACK05 output error");
                UpdateStatus($"Output error: {status.Message}");
                break;
        }
    }

    private void SetOutputButtonState(string text, bool selected, string accessibleName)
    {
        _live.Text = text;
        _live.Selected = selected;
        _live.AccessibleName = accessibleName;
        _live.AccessibleDescription = selected
            ? "Turn ACK05 device capture and Windows keyboard or mouse output off."
            : "Enable ACK05 device capture and Windows keyboard or mouse output.";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        PersistRecoveryDraft();
        if (_allowClose || !_liveRuntime.IsActive)
        {
            base.OnFormClosing(e);
            return;
        }

        e.Cancel = true;
        if (_closing)
        {
            return;
        }

        _closing = true;
        _live.Enabled = false;
        UpdateStatus("Turning output off before closing...");
        _ = StopLiveAndCloseAsync();
    }

    private async Task StopLiveAndCloseAsync()
    {
        try
        {
            await _liveRuntime.DisposeAsync();
        }
        catch (Exception exception)
        {
            _closing = false;
            _live.Enabled = true;
            MessageBox.Show(
                this,
                $"Could not turn output off safely: {exception.Message}",
                "ACK05 release failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        _allowClose = true;
        Close();
    }

    private bool ValidateCurrent(bool showSuccess)
    {
        var errors = Ack05ConfigurationValidator.Validate(_configuration);
        if (errors.Count > 0)
        {
            UpdateStatus(errors[0]);
            return false;
        }

        if (showSuccess)
        {
            UpdateStatus("Configuration is valid.");
        }

        return true;
    }

    private void LoadTapHoldDefinitions(string? preferredId = null)
    {
        if (CurrentProfile() is not { } profile)
        {
            return;
        }

        preferredId ??= CurrentTapHoldDefinition()?.Id;
        _tapHoldEditorLoading = true;
        _tapHoldDefinitions.BeginUpdate();
        try
        {
            _tapHoldDefinitions.Items.Clear();
            foreach (var definition in profile.TapHolds)
            {
                _tapHoldDefinitions.Items.Add(new TapHoldListItem(
                    definition.Id,
                    $"{definition.Id}   {definition.Name}"));
            }

            if (_tapHoldDefinitions.Items.Count > 0)
            {
                var index = profile.TapHolds
                    .Select((definition, index) => (definition, index))
                    .FirstOrDefault(item => string.Equals(
                        item.definition.Id,
                        preferredId,
                        StringComparison.OrdinalIgnoreCase)).index;
                _tapHoldDefinitions.SelectedIndex = Math.Clamp(index, 0, profile.TapHolds.Length - 1);
            }
        }
        finally
        {
            _tapHoldDefinitions.EndUpdate();
            _tapHoldEditorLoading = false;
        }

        LoadSelectedTapHold();
    }

    private void LoadSelectedTapHold()
    {
        if (_tapHoldEditorLoading || CurrentProfile() is not { } profile)
        {
            return;
        }

        var definition = CurrentTapHoldDefinition();
        _tapHoldEditorLoading = true;
        try
        {
            var enabled = definition is not null;
            _actionPalette.Enabled = enabled;
            _actionPalette.SetPulseOnly(false);
            _actionPalette.SetUsage(_tapHoldSlot == TapHoldSlot.Tap
                ? Ack05ActionPalette.Usage.Tap
                : Ack05ActionPalette.Usage.Hold);
            _tapHoldName.Enabled = enabled;
            _tapHoldTerm.Enabled = enabled;
            _tapHoldInterrupt.Enabled = enabled;
            _tapSlotButton.Enabled = enabled;
            _holdSlotButton.Enabled = enabled;
            _assignTapHold.Enabled = enabled;
            UpdateTapHoldMap(profile, definition);
            if (definition is null)
            {
                _actionPalette.LoadAction(Ack05Action.Disabled());
                _tapHoldName.Text = string.Empty;
                _tapSlotButton.Text = "TAP  ·  —";
                _holdSlotButton.Text = "HOLD  ·  —";
                _tapHoldReferenceSummary.Text = "No definitions yet. Create one to begin.";
                return;
            }

            _tapHoldName.Text = definition.Name;
            _tapHoldTerm.Value = Math.Clamp(
                definition.TappingTermMs,
                Ack05LayerSwitchSettings.MinimumTapHoldThresholdMs,
                Ack05LayerSwitchSettings.MaximumTapHoldThresholdMs);
            _tapHoldInterrupt.SelectedIndex = (int)definition.InterruptMode;
            _tapHoldTargetLayer.Items.Clear();
            _tapHoldTargetLayer.Items.AddRange(profile.Layers
                .OrderBy(layer => layer.Index)
                .Select(layer => $"Layer {layer.Index}")
                .ToArray());
            _tapSlotButton.Text = $"TAP  ·  {DescribeAction(definition.OnTap)}";
            _holdSlotButton.Text = $"HOLD  ·  {DescribeAction(definition.OnHold)}";
        }
        finally
        {
            _tapHoldEditorLoading = false;
        }

        SelectTapHoldSlot(_tapHoldSlot);
    }

    private void UpdateTapHoldMap(
        Ack05Profile profile,
        Ack05TapHoldDefinition? definition)
    {
        _tapHoldSelectedControl ??= CurrentControl() ?? Ack05Control.K1;
        _tapHoldDeviceMap.LoadMap(profile, _selectedLayer);
        _tapHoldDeviceMap.SelectedControl = _tapHoldSelectedControl;

        var references = definition is null
            ? Array.Empty<(int Layer, Ack05Control Control)>()
            : profile.Layers
                .SelectMany(layer => layer.Bindings
                    .Where(binding => binding.Action.Kind == Ack05ActionKind.TapHold &&
                        string.Equals(
                            binding.Action.TapHoldId,
                            definition.Id,
                            StringComparison.OrdinalIgnoreCase))
                    .Select(binding => (Layer: layer.Index, binding.Control)))
                .ToArray();
        _tapHoldDeviceMap.HighlightControls(
            references
                .Where(reference => reference.Layer == _selectedLayer)
                .Select(reference => reference.Control));

        if (definition is null)
        {
            _tapHoldReferenceSummary.Text = "No definitions yet. Create one to begin.";
            _tapHoldMapSummary.Text = "No Tap & Hold selected";
            _tapHoldMapHint.Text = "Create a definition, then choose its physical target here.";
            _assignTapHold.Text = "Assign to selected key";
            _assignTapHold.Enabled = false;
            return;
        }

        _tapHoldReferenceSummary.Text = FormatTapHoldReferences(references);
        _tapHoldMapSummary.Text =
            $"{DescribeAction(definition.OnTap)}  /  {DescribeAction(definition.OnHold)}";
        var selectedControl = _tapHoldSelectedControl.Value;
        var isPulseOnly = selectedControl is Ack05Control.RollerClockwise or
            Ack05Control.RollerCounterClockwise;
        _tapHoldMapHint.Text = isPulseOnly
            ? "Roller directions are pulse-only. Choose K1-K10."
            : $"Assignment target  ·  Layer {_selectedLayer}  ·  {selectedControl}";
        _assignTapHold.Text = isPulseOnly
            ? "Choose a K1-K10 target"
            : $"Assign to L{_selectedLayer}  ·  {selectedControl}";
        _assignTapHold.Enabled = !isPulseOnly;
    }

    private static string FormatTapHoldReferences(
        IReadOnlyList<(int Layer, Ack05Control Control)> references)
    {
        if (references.Count == 0)
        {
            return "Not assigned to any key";
        }

        var visible = references
            .Take(3)
            .Select(reference => $"L{reference.Layer} {reference.Control}");
        var remainder = references.Count > 3 ? $"  +{references.Count - 3}" : string.Empty;
        return "Used by " + string.Join(", ", visible) + remainder;
    }

    private void SelectTapHoldTarget(Ack05Control control)
    {
        _tapHoldSelectedControl = control;
        if (CurrentProfile() is { } profile)
        {
            UpdateTapHoldMap(profile, CurrentTapHoldDefinition());
        }

        if (control is Ack05Control.RollerClockwise or Ack05Control.RollerCounterClockwise)
        {
            UpdateStatus("Tap & Hold requires K1-K10; roller directions are pulse-only.");
            return;
        }

        UpdateStatus($"Tap & Hold target: Layer {_selectedLayer} {control}.");
    }

    private Ack05TapHoldDefinition? CurrentTapHoldDefinition()
    {
        if (CurrentProfile() is not { } profile ||
            _tapHoldDefinitions.SelectedItem is not TapHoldListItem item)
        {
            return null;
        }

        return profile.TapHolds.FirstOrDefault(definition => string.Equals(
            definition.Id,
            item.Id,
            StringComparison.OrdinalIgnoreCase));
    }

    private void SelectTapHoldSlot(TapHoldSlot slot)
    {
        var definition = CurrentTapHoldDefinition();
        if (definition is null)
        {
            return;
        }

        _tapHoldSlot = slot;
        _tapSlotButton.Selected = slot == TapHoldSlot.Tap;
        _holdSlotButton.Selected = slot == TapHoldSlot.Hold;
        var action = slot == TapHoldSlot.Tap ? definition.OnTap : definition.OnHold;
        _tapHoldEditorLoading = true;
        try
        {
            _actionPalette.SetPulseOnly(false);
            _actionPalette.SetUsage(slot == TapHoldSlot.Tap
                ? Ack05ActionPalette.Usage.Tap
                : Ack05ActionPalette.Usage.Hold);
            _actionPalette.LoadAction(action);
            var target = action.TargetLayer ?? Math.Min(1, CurrentProfile()!.Layers.Length - 1);
            _tapHoldTargetLayer.SelectedItem = $"Layer {target}";
            _tapHoldWheelSteps.Value = Math.Clamp(action.WheelSteps ?? 1, -10, 10);
            UpdateTapHoldParameterVisibility(action.Kind);
        }
        finally
        {
            _tapHoldEditorLoading = false;
        }
    }

    private void ApplyTapHoldSlotFromPalette()
    {
        if (_tapHoldEditorLoading || CurrentTapHoldDefinition() is not { } definition)
        {
            return;
        }

        var action = BuildTapHoldSlotDraft();
        if (action is null)
        {
            return;
        }

        var updated = CloneTapHoldDefinition(
            definition,
            onTap: _tapHoldSlot == TapHoldSlot.Tap ? action : definition.OnTap,
            onHold: _tapHoldSlot == TapHoldSlot.Hold ? action : definition.OnHold);
        CommitTapHoldDefinition(updated);
    }

    private Ack05Action? BuildTapHoldSlotDraft()
    {
        var kind = _actionPalette.SelectedKind;
        if (kind is null)
        {
            return null;
        }

        return kind switch
        {
            Ack05ActionKind.Disabled => Ack05Action.Disabled(),
            Ack05ActionKind.KeyboardChord => Ack05Action.KeyboardChord(
                OrderChordKeys(_actionPalette.SelectedKeys)),
            Ack05ActionKind.MouseWheel when _tapHoldSlot == TapHoldSlot.Tap =>
                Ack05Action.MouseWheel((int)_tapHoldWheelSteps.Value),
            Ack05ActionKind.ToggleLayer when _tapHoldSlot == TapHoldSlot.Tap =>
                Ack05Action.ToggleLayer(SelectedTapHoldTargetLayer()),
            Ack05ActionKind.MomentaryLayer when _tapHoldSlot == TapHoldSlot.Hold =>
                Ack05Action.MomentaryLayer(SelectedTapHoldTargetLayer()),
            _ => null,
        };
    }

    private void CommitTapHoldSlotParameters()
    {
        if (_tapHoldEditorLoading || CurrentTapHoldDefinition() is not { } definition)
        {
            return;
        }

        var current = _tapHoldSlot == TapHoldSlot.Tap ? definition.OnTap : definition.OnHold;
        var updatedAction = current.Kind switch
        {
            Ack05ActionKind.MouseWheel => Ack05Action.MouseWheel((int)_tapHoldWheelSteps.Value),
            Ack05ActionKind.ToggleLayer => Ack05Action.ToggleLayer(SelectedTapHoldTargetLayer()),
            Ack05ActionKind.MomentaryLayer => Ack05Action.MomentaryLayer(SelectedTapHoldTargetLayer()),
            _ => current,
        };
        var updated = CloneTapHoldDefinition(
            definition,
            onTap: _tapHoldSlot == TapHoldSlot.Tap ? updatedAction : definition.OnTap,
            onHold: _tapHoldSlot == TapHoldSlot.Hold ? updatedAction : definition.OnHold);
        CommitTapHoldDefinition(updated);
    }

    private void CommitTapHoldMetadata()
    {
        if (_tapHoldEditorLoading || CurrentTapHoldDefinition() is not { } definition)
        {
            return;
        }

        var name = _tapHoldName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            UpdateStatus("Tap & Hold name is required.");
            return;
        }

        var updated = CloneTapHoldDefinition(
            definition,
            name: name,
            term: (int)_tapHoldTerm.Value,
            interrupt: (Ack05TapHoldInterruptMode)Math.Max(0, _tapHoldInterrupt.SelectedIndex));
        CommitTapHoldDefinition(updated);
    }

    private void CommitTapHoldDefinition(Ack05TapHoldDefinition updated)
    {
        if (CurrentProfile() is not { } profile)
        {
            return;
        }

        var updatedProfile = CloneProfileWithTapHolds(
            profile,
            profile.TapHolds.Select(definition => string.Equals(
                definition.Id,
                updated.Id,
                StringComparison.OrdinalIgnoreCase) ? updated : definition).ToArray());
        if (!TryCommitProfileMutation(profile, updatedProfile, out var error))
        {
            UpdateStatus(error!);
            return;
        }

        LoadTapHoldDefinitions(updated.Id);
        UpdateStatus($"Updated {updated.Id}: {updated.Name}.");
    }

    private void AddTapHoldDefinition()
    {
        if (CurrentProfile() is not { } profile)
        {
            return;
        }

        var index = 0;
        var ids = profile.TapHolds.Select(definition => definition.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        while (ids.Contains($"th{index}"))
        {
            index++;
        }

        var definition = new Ack05TapHoldDefinition
        {
            Id = $"th{index}",
            Name = "B / Left Shift",
            OnTap = Ack05Action.KeyboardChord(Ack05KeyboardKey.B),
            OnHold = Ack05Action.KeyboardChord(Ack05KeyboardKey.LeftShift),
        };
        var updatedProfile = CloneProfileWithTapHolds(
            profile,
            [.. profile.TapHolds, definition]);
        if (!TryCommitProfileMutation(profile, updatedProfile, out var error))
        {
            UpdateStatus(error!);
            return;
        }

        LoadTapHoldDefinitions(definition.Id);
        UpdateStatus($"Created {definition.Id}. Choose Tap and Hold actions below.");
    }

    private void DeleteTapHoldDefinition()
    {
        if (CurrentProfile() is not { } profile || CurrentTapHoldDefinition() is not { } definition)
        {
            return;
        }

        var references = profile.Layers
            .SelectMany(layer => layer.Bindings.Select(binding => (layer.Index, binding)))
            .Where(item => item.binding.Action.Kind == Ack05ActionKind.TapHold &&
                string.Equals(item.binding.Action.TapHoldId, definition.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (references.Length > 0)
        {
            UpdateStatus(
                $"{definition.Id} is still assigned to {references.Length} key binding(s); replace those bindings first.");
            return;
        }

        var updatedProfile = CloneProfileWithTapHolds(
            profile,
            profile.TapHolds.Where(item => !string.Equals(
                item.Id,
                definition.Id,
                StringComparison.OrdinalIgnoreCase)).ToArray());
        if (!TryCommitProfileMutation(profile, updatedProfile, out var error))
        {
            UpdateStatus(error!);
            return;
        }

        LoadTapHoldDefinitions();
        UpdateStatus($"Deleted {definition.Id}.");
    }

    private void AssignTapHoldToSelectedKey()
    {
        var control = _tapHoldSelectedControl ?? CurrentControl();
        if (CurrentTapHoldDefinition() is not { } definition || control is null)
        {
            UpdateStatus("Select a physical K1-K10 key on the Tap & Hold map first.");
            return;
        }

        if (control.Value is Ack05Control.RollerClockwise or Ack05Control.RollerCounterClockwise)
        {
            UpdateStatus("Tap & Hold requires a key with Down and Up events; roller directions are pulse-only.");
            return;
        }

        ApplyBindingToControl(
            control.Value,
            selectAfterApply: true,
            actionOverride: Ack05Action.TapHold(definition.Id));
        LoadTapHoldDefinitions(definition.Id);
    }

    private void LoadComboDefinitions(string? preferredId = null)
    {
        if (CurrentProfile() is not { } profile)
        {
            return;
        }

        preferredId ??= CurrentComboDefinition()?.Id;
        _comboEditorLoading = true;
        _comboDefinitions.BeginUpdate();
        try
        {
            _comboDefinitions.Items.Clear();
            foreach (var definition in profile.Combos)
            {
                _comboDefinitions.Items.Add(new ComboListItem(
                    definition.Id,
                    $"{definition.Controls[0]} + {definition.Controls[1]}   {definition.Name}"));
            }

            if (_comboDefinitions.Items.Count > 0)
            {
                var index = profile.Combos
                    .Select((definition, index) => (definition, index))
                    .FirstOrDefault(item => string.Equals(
                        item.definition.Id,
                        preferredId,
                        StringComparison.OrdinalIgnoreCase)).index;
                _comboDefinitions.SelectedIndex = Math.Clamp(index, 0, profile.Combos.Length - 1);
            }
        }
        finally
        {
            _comboDefinitions.EndUpdate();
            _comboEditorLoading = false;
        }

        LoadSelectedCombo();
    }

    private void OpenComboFromKeymap(string comboId)
    {
        ShowWorkspace("Combos");
        LoadComboDefinitions(comboId);
        UpdateStatus($"Opened Combo {comboId} from Layer {_selectedLayer}.");
    }

    private void LoadSelectedCombo()
    {
        if (_comboEditorLoading || CurrentProfile() is not { } profile)
        {
            return;
        }

        var definition = CurrentComboDefinition();
        _comboEditorLoading = true;
        try
        {
            _comboProfileTerm.Value = Math.Clamp(
                profile.ComboDefaults.TermMs,
                Ack05ComboDefaults.MinimumTermMs,
                Ack05ComboDefaults.MaximumTermMs);
            _comboDeviceMap.LoadMap(profile, _selectedLayer);
            _comboLayers.Items.Clear();
            foreach (var layer in profile.Layers.OrderBy(layer => layer.Index))
            {
                _comboLayers.Items.Add($"Layer {layer.Index}",
                    definition?.Layers.Contains(layer.Index) == true);
            }

            _comboTargetLayer.Items.Clear();
            _comboTargetLayer.Items.AddRange(profile.Layers
                .OrderBy(layer => layer.Index)
                .Select(layer => $"Layer {layer.Index}")
                .ToArray());

            var enabled = definition is not null;
            _actionPalette.Enabled = enabled;
            _actionPalette.SetPulseOnly(false);
            _actionPalette.SetUsage(Ack05ActionPalette.Usage.Combo);
            foreach (Control control in new Control[]
            {
                _comboName, _comboUseDefaultTerm, _comboTerm, _comboLayers,
                _comboOrdered, _comboTrigger, _comboHoldTerm, _comboRelease,
            })
            {
                control.Enabled = enabled;
            }

            if (definition is null)
            {
                _actionPalette.LoadAction(Ack05Action.Disabled());
                _comboName.Text = string.Empty;
                _comboMembers.Text = "No Combo selected";
                _comboSelectionHint.Text = "Create a Combo, then click two keys on the map.";
                _comboOutput.Text = "OUTPUT  ·  —";
                _comboFirstSelection = null;
                return;
            }

            _comboName.Text = definition.Name;
            _comboMembers.Text = $"{definition.Controls[0]}  +  {definition.Controls[1]}  ->  {DescribeAction(definition.Action)}";
            _comboSelectionHint.Text = "Click any two K1-K10 keys to replace the input pair.";
            _comboFirstSelection = null;
            _comboDeviceMap.SelectedControl = definition.Controls[0];
            _comboUseDefaultTerm.Checked = definition.TermMs is null;
            _comboTerm.Enabled = definition.TermMs is not null;
            _comboTerm.Value = Math.Clamp(
                definition.TermMs ?? profile.ComboDefaults.TermMs,
                Ack05ComboDefaults.MinimumTermMs,
                Ack05ComboDefaults.MaximumTermMs);
            _comboOrdered.Checked = definition.Ordered;
            _comboTrigger.SelectedIndex = (int)definition.Trigger;
            _comboHoldTerm.Value = Math.Clamp(
                definition.HoldTermMs ?? Ack05ComboDefinition.DefaultHoldTermMs,
                Ack05ComboDefinition.MinimumHoldTermMs,
                Ack05ComboDefinition.MaximumHoldTermMs);
            _comboRelease.SelectedIndex = (int)definition.Release;
            _comboOutput.Text = $"OUTPUT  ·  {DescribeAction(definition.Action)}";
            _actionPalette.LoadAction(definition.Action);
            _comboTargetLayer.SelectedItem = $"Layer {definition.Action.TargetLayer ?? Math.Min(1, profile.Layers.Length - 1)}";
            _comboWheelSteps.Value = Math.Clamp(definition.Action.WheelSteps ?? 1, -10, 10);
            UpdateComboParameterVisibility(definition);
        }
        finally
        {
            _comboEditorLoading = false;
        }
    }

    private Ack05ComboDefinition? CurrentComboDefinition()
    {
        if (CurrentProfile() is not { } profile ||
            _comboDefinitions.SelectedItem is not ComboListItem item)
        {
            return null;
        }

        return profile.Combos.FirstOrDefault(definition => string.Equals(
            definition.Id,
            item.Id,
            StringComparison.OrdinalIgnoreCase));
    }

    private void SelectComboMember(Ack05Control control)
    {
        if (control is Ack05Control.RollerClockwise or Ack05Control.RollerCounterClockwise)
        {
            UpdateStatus("Combo members must be K1-K10; roller directions are pulse-only.");
            return;
        }

        if (CurrentComboDefinition() is not { } definition)
        {
            return;
        }

        if (_comboFirstSelection is null || _comboFirstSelection == control)
        {
            _comboFirstSelection = control;
            _comboDeviceMap.SelectedControl = control;
            _comboSelectionHint.Text = $"First member: {control}. Click a different key for member two.";
            return;
        }

        var updated = CloneComboDefinition(
            definition,
            controls: [_comboFirstSelection.Value, control]);
        _comboFirstSelection = null;
        CommitComboDefinition(updated);
    }

    private void CommitComboProfileTerm()
    {
        if (_comboEditorLoading || CurrentProfile() is not { } profile)
        {
            return;
        }

        var updated = CloneProfileWithCombos(
            profile,
            new Ack05ComboDefaults
            {
                TermMs = (int)_comboProfileTerm.Value,
                ExtensionData = profile.ComboDefaults.ExtensionData,
            },
            profile.Combos);
        if (!TryCommitProfileMutation(profile, updated, out var error))
        {
            UpdateStatus(error!);
            return;
        }

        LoadComboDefinitions(CurrentComboDefinition()?.Id);
        UpdateStatus($"Profile Combo Term: {_comboProfileTerm.Value} ms.");
    }

    private void CommitComboMetadata()
    {
        if (_comboEditorLoading || CurrentComboDefinition() is not { } definition)
        {
            return;
        }

        var name = _comboName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            UpdateStatus("Combo name is required.");
            return;
        }

        var layers = _comboLayers.CheckedIndices.Cast<int>().ToArray();
        if (layers.Length == 0)
        {
            UpdateStatus("A Combo needs at least one layer in its scope.");
            return;
        }

        var trigger = (Ack05ComboTrigger)Math.Max(0, _comboTrigger.SelectedIndex);
        var updated = CloneComboDefinition(
            definition,
            name: name,
            layers: layers,
            termMs: _comboUseDefaultTerm.Checked ? null : (int)_comboTerm.Value,
            ordered: _comboOrdered.Checked,
            trigger: trigger,
            holdTermMs: trigger == Ack05ComboTrigger.Press ? null : (int)_comboHoldTerm.Value,
            release: (Ack05ComboRelease)Math.Max(0, _comboRelease.SelectedIndex),
            replaceTiming: true);
        CommitComboDefinition(updated);
    }

    private void ApplyComboOutputFromPalette()
    {
        if (_comboEditorLoading || CurrentComboDefinition() is not { } definition)
        {
            return;
        }

        var action = BuildComboOutputDraft();
        if (action is not null)
        {
            CommitComboDefinition(CloneComboDefinition(definition, action: action));
        }
    }

    private Ack05Action? BuildComboOutputDraft()
    {
        return _actionPalette.SelectedKind switch
        {
            null => null,
            Ack05ActionKind.Disabled => Ack05Action.Disabled(),
            Ack05ActionKind.KeyboardChord => Ack05Action.KeyboardChord(
                OrderChordKeys(_actionPalette.SelectedKeys)),
            Ack05ActionKind.MouseWheel => Ack05Action.MouseWheel((int)_comboWheelSteps.Value),
            Ack05ActionKind.MomentaryLayer => Ack05Action.MomentaryLayer(SelectedComboTargetLayer()),
            Ack05ActionKind.ToggleLayer => Ack05Action.ToggleLayer(SelectedComboTargetLayer()),
            _ => null,
        };
    }

    private void CommitComboOutputParameters()
    {
        if (_comboEditorLoading || CurrentComboDefinition() is not { } definition)
        {
            return;
        }

        var action = definition.Action.Kind switch
        {
            Ack05ActionKind.MouseWheel => Ack05Action.MouseWheel((int)_comboWheelSteps.Value),
            Ack05ActionKind.MomentaryLayer => Ack05Action.MomentaryLayer(SelectedComboTargetLayer()),
            Ack05ActionKind.ToggleLayer => Ack05Action.ToggleLayer(SelectedComboTargetLayer()),
            _ => definition.Action,
        };
        CommitComboDefinition(CloneComboDefinition(definition, action: action));
    }

    private int SelectedComboTargetLayer() => Math.Max(0, _comboTargetLayer.SelectedIndex);

    private void CommitComboDefinition(Ack05ComboDefinition updated)
    {
        if (CurrentProfile() is not { } profile)
        {
            return;
        }

        var updatedProfile = CloneProfileWithCombos(
            profile,
            profile.ComboDefaults,
            profile.Combos.Select(definition => string.Equals(
                definition.Id,
                updated.Id,
                StringComparison.OrdinalIgnoreCase) ? updated : definition).ToArray());
        if (!TryCommitProfileMutation(profile, updatedProfile, out var error))
        {
            UpdateStatus(error!);
            return;
        }

        LoadComboDefinitions(updated.Id);
        UpdateStatus($"Updated {updated.Id}: {updated.Name}.");
    }

    private void AddComboDefinition()
    {
        if (CurrentProfile() is not { } profile)
        {
            return;
        }

        var idIndex = 0;
        var ids = profile.Combos.Select(definition => definition.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        while (ids.Contains($"combo{idIndex}"))
        {
            idIndex++;
        }

        var usedPairs = profile.Combos
            .Where(combo => combo.Layers.Contains(_selectedLayer))
            .Select(combo => string.Join("+", combo.Controls.Order()))
            .ToHashSet(StringComparer.Ordinal);
        var keys = BindingCandidates.Take(10).ToArray();
        var pair = keys.SelectMany((first, index) => keys.Skip(index + 1)
                .Select(second => new[] { first, second }))
            .FirstOrDefault(candidate => !usedPairs.Contains(string.Join("+", candidate.Order())));
        if (pair is null)
        {
            UpdateStatus($"Layer {_selectedLayer} has no free two-key Combo pair.");
            return;
        }

        var definition = new Ack05ComboDefinition
        {
            Id = $"combo{idIndex}",
            Name = "Enter",
            Controls = pair,
            Layers = [_selectedLayer],
            Action = Ack05Action.KeyboardChord(Ack05KeyboardKey.Enter),
        };
        var updatedProfile = CloneProfileWithCombos(
            profile,
            profile.ComboDefaults,
            [.. profile.Combos, definition]);
        if (!TryCommitProfileMutation(profile, updatedProfile, out var error))
        {
            UpdateStatus(error!);
            return;
        }

        LoadComboDefinitions(definition.Id);
        UpdateStatus($"Created {definition.Id}. Click two physical keys to set its members.");
    }

    private void DeleteComboDefinition()
    {
        if (CurrentProfile() is not { } profile || CurrentComboDefinition() is not { } definition)
        {
            return;
        }

        var updatedProfile = CloneProfileWithCombos(
            profile,
            profile.ComboDefaults,
            profile.Combos.Where(item => !string.Equals(
                item.Id,
                definition.Id,
                StringComparison.OrdinalIgnoreCase)).ToArray());
        if (!TryCommitProfileMutation(profile, updatedProfile, out var error))
        {
            UpdateStatus(error!);
            return;
        }

        LoadComboDefinitions();
        UpdateStatus($"Deleted {definition.Id}.");
    }

    private void UpdateComboParameterVisibility(Ack05ComboDefinition definition)
    {
        _comboTerm.Enabled = !_comboUseDefaultTerm.Checked;
        if (_comboHoldTerm.Parent is { } holdRow)
        {
            holdRow.Visible = definition.Trigger != Ack05ComboTrigger.Press;
        }

        if (_comboTargetLayer.Parent is { } layerRow)
        {
            layerRow.Visible = definition.Action.Kind is
                Ack05ActionKind.MomentaryLayer or Ack05ActionKind.ToggleLayer;
        }

        if (_comboWheelSteps.Parent is { } wheelRow)
        {
            wheelRow.Visible = definition.Action.Kind == Ack05ActionKind.MouseWheel;
        }
    }

    private bool TryCommitProfileMutation(
        Ack05Profile previous,
        Ack05Profile updated,
        out string? error)
    {
        var configuration = new Ack05Configuration
        {
            SchemaVersion = _configuration.SchemaVersion,
            DefaultProfileId = _configuration.DefaultProfileId,
            Profiles = _configuration.Profiles
                .Select(profile => ReferenceEquals(profile, previous) ? updated : profile)
                .ToArray(),
            ExtensionData = _configuration.ExtensionData,
        };
        var errors = Ack05ConfigurationValidator.Validate(configuration);
        if (errors.Count > 0)
        {
            error = errors[0];
            return false;
        }

        if (Ack05ConfigurationJson.Serialize(configuration) ==
            Ack05ConfigurationJson.Serialize(_configuration))
        {
            error = null;
            return true;
        }

        RecordMutation();
        _configuration = configuration;
        RefreshDocumentState();
        ApplyLiveConfigurationIfValid();
        error = null;
        return true;
    }

    private int SelectedTapHoldTargetLayer()
    {
        if (_tapHoldTargetLayer.SelectedItem is string text &&
            int.TryParse(text.AsSpan("Layer ".Length), out var layer))
        {
            return layer;
        }

        return Math.Min(1, CurrentProfile()?.Layers.Length - 1 ?? 1);
    }

    private void UpdateTapHoldParameterVisibility(Ack05ActionKind kind)
    {
        if (_tapHoldTargetLayer.Parent is { } targetRow)
        {
            targetRow.Visible = kind is Ack05ActionKind.ToggleLayer or Ack05ActionKind.MomentaryLayer;
        }

        if (_tapHoldWheelSteps.Parent is { } wheelRow)
        {
            wheelRow.Visible = kind == Ack05ActionKind.MouseWheel;
        }
    }

    private static Ack05TapHoldDefinition CloneTapHoldDefinition(
        Ack05TapHoldDefinition definition,
        string? name = null,
        int? term = null,
        Ack05TapHoldInterruptMode? interrupt = null,
        Ack05Action? onTap = null,
        Ack05Action? onHold = null) =>
        new()
        {
            Id = definition.Id,
            Name = name ?? definition.Name,
            TappingTermMs = term ?? definition.TappingTermMs,
            InterruptMode = interrupt ?? definition.InterruptMode,
            OnTap = onTap ?? definition.OnTap,
            OnHold = onHold ?? definition.OnHold,
            ExtensionData = definition.ExtensionData,
        };

    private static Ack05ComboDefinition CloneComboDefinition(
        Ack05ComboDefinition definition,
        string? name = null,
        Ack05Control[]? controls = null,
        int[]? layers = null,
        int? termMs = null,
        bool? ordered = null,
        Ack05ComboTrigger? trigger = null,
        int? holdTermMs = null,
        Ack05ComboRelease? release = null,
        Ack05Action? action = null,
        bool replaceTiming = false) =>
        new()
        {
            Id = definition.Id,
            Name = name ?? definition.Name,
            Controls = controls ?? definition.Controls,
            Layers = layers ?? definition.Layers,
            TermMs = replaceTiming ? termMs : definition.TermMs,
            Ordered = ordered ?? definition.Ordered,
            Trigger = trigger ?? definition.Trigger,
            HoldTermMs = replaceTiming ? holdTermMs : definition.HoldTermMs,
            Release = release ?? definition.Release,
            Action = action ?? definition.Action,
            ExtensionData = definition.ExtensionData,
        };

    private void UpdateActionEditorEnabled()
    {
        var kind = _actionPalette.SelectedKind;
        _wheelSteps.Enabled = kind == Ack05ActionKind.MouseWheel;
        var isLayerAction = kind is Ack05ActionKind.MomentaryLayer or
            Ack05ActionKind.ToggleLayer or Ack05ActionKind.TapToggleHoldLayer;
        _targetLayer.Enabled = isLayerAction;
        _actionTapHoldThreshold.Enabled = kind == Ack05ActionKind.TapToggleHoldLayer;
        _captureShortcut.Enabled = !_actionPalette.HasExclusiveSelection;
        _applyBinding.Enabled = kind is not null and not Ack05ActionKind.TapHold;
        if (_wheelSteps.Parent is { } wheelRow)
        {
            wheelRow.Visible = kind == Ack05ActionKind.MouseWheel;
        }

        if (_targetLayer.Parent is { } targetRow)
        {
            targetRow.Visible = isLayerAction;
        }

        if (_actionTapHoldThreshold.Parent is { } thresholdRow)
        {
            thresholdRow.Visible = kind == Ack05ActionKind.TapToggleHoldLayer;
        }

        UpdateActionSummary();
    }

    private void OnActionPaletteSelectionChanged()
    {
        if (string.Equals(_activeWorkspace, "Tap & Hold", StringComparison.Ordinal))
        {
            ApplyTapHoldSlotFromPalette();
            return;
        }

        if (string.Equals(_activeWorkspace, "Combos", StringComparison.Ordinal))
        {
            ApplyComboOutputFromPalette();
            return;
        }

        UpdateActionEditorEnabled();
        if (!_bindingEditorLoading)
        {
            ApplyBinding();
        }
    }

    private void OnBindingParameterChanged()
    {
        UpdateActionSummary();
        if (!_bindingEditorLoading)
        {
            ApplyBinding();
        }
    }

    private void UpdateActionSummary()
    {
        var actionText = _actionPalette.SelectedKind switch
        {
            null => "Select an action",
            Ack05ActionKind.Disabled => "Disabled",
            Ack05ActionKind.Transparent => "Transparent (fall through)",
            Ack05ActionKind.KeyboardChord =>
                $"Keyboard: {string.Join(" + ", _actionPalette.SelectedKeys)}",
            Ack05ActionKind.MouseWheel => $"Mouse Wheel: {_wheelSteps.Value:+#;-#;0}",
            Ack05ActionKind.MomentaryLayer => $"MO({SelectedTargetLayer()})",
            Ack05ActionKind.ToggleLayer => $"TG({SelectedTargetLayer()})",
            Ack05ActionKind.TapToggleHoldLayer =>
                $"Tap/Hold Layer {SelectedTargetLayer()} · {_actionTapHoldThreshold.Value} ms",
            Ack05ActionKind.TapHold => DescribeAction(CurrentBindingAction()),
            var kind => kind.ToString(),
        };
        _actionSummary.Text = actionText;
        _inspectorHint.Text = _actionPalette.SelectedKind switch
        {
            Ack05ActionKind.MomentaryLayer =>
                $"Layer {SelectedTargetLayer()} is active while {_selectedKeyTitle.Text} is held.",
            Ack05ActionKind.ToggleLayer =>
                $"Each press toggles Layer {SelectedTargetLayer()} on or off.",
            Ack05ActionKind.TapToggleHoldLayer =>
                $"Tap toggles Layer {SelectedTargetLayer()}; hold uses it momentarily.",
            Ack05ActionKind.TapHold =>
                "Reusable definition; edit its Tap and Hold slots in the Tap & Hold workspace.",
            Ack05ActionKind.Transparent => "Falls through to the next active lower layer.",
            Ack05ActionKind.Disabled => "This key produces no output on the current layer.",
            Ack05ActionKind.KeyboardChord => "The chord is held and released with the physical key.",
            Ack05ActionKind.MouseWheel => "Emits one configured wheel pulse.",
            _ => "Choose an action from the library below.",
        };
    }

    private void ToggleAdvanced()
    {
        var expand = !_advancedPanel.IsExpanded;
        _advancedToggle.Text = expand ? "Advanced  ▴" : "Advanced  ▾";
        _advancedPanel.SetExpanded(expand);
    }

    private void RecordMutation()
    {
        _undoHistory.Push(_configuration);
        _redoHistory.Clear();
    }

    private void Undo()
    {
        if (_undoHistory.Count == 0)
        {
            return;
        }

        _redoHistory.Push(_configuration);
        RestoreHistoryConfiguration(_undoHistory.Pop(), "Undo");
    }

    private void Redo()
    {
        if (_redoHistory.Count == 0)
        {
            return;
        }

        _undoHistory.Push(_configuration);
        RestoreHistoryConfiguration(_redoHistory.Pop(), "Redo");
    }

    private void RevertToSaved()
    {
        if (!_dirty)
        {
            UpdateStatus("Already at the saved version.");
            return;
        }

        RecordMutation();
        RestoreHistoryConfiguration(
            Normalize(Ack05ConfigurationJson.Deserialize(_savedBaselineJson)),
            "Reverted to saved version");
    }

    private void RestoreHistoryConfiguration(Ack05Configuration configuration, string status)
    {
        var profileId = CurrentProfile()?.Id ?? configuration.DefaultProfileId;
        var control = CurrentControl();
        _configuration = configuration;
        LoadProfiles();
        var profileIndex = Array.FindIndex(
            _configuration.Profiles,
            profile => string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
        _profiles.SelectedIndex = Math.Max(profileIndex, 0);
        if (control is { } selectedControl)
        {
            SelectControl(selectedControl);
        }

        RefreshDocumentState();
        ApplyLiveConfigurationIfValid();
        UpdateStatus(status + ".");
    }

    private void ResetHistoryBaseline()
    {
        _undoHistory.Clear();
        _redoHistory.Clear();
        _savedBaselineJson = Ack05ConfigurationJson.Serialize(_configuration);
        RefreshDocumentState();
    }

    private void RefreshDocumentState()
    {
        _dirty = Ack05ConfigurationJson.Serialize(_configuration) != _savedBaselineJson;
        _undo.Enabled = _undoHistory.Count > 0;
        _redo.Enabled = _redoHistory.Count > 0;
        _revert.Enabled = _dirty;
        _save.Text = _dirty ? "Save •" : "Save";
        Text = _dirty
            ? "Instrument Workbench — ACK05 Enhanced •"
            : "Instrument Workbench — ACK05 Enhanced";
        if (_dirty)
        {
            ScheduleRecoveryDraftSave();
        }
        else
        {
            Interlocked.Increment(ref _draftSaveGeneration);
            _draftSaveCancellation?.Cancel();
            try
            {
                RecoveryDraftStore.Delete();
            }
            catch (Exception exception)
            {
                UpdateStatus($"Could not clear recovery draft: {exception.Message}");
            }
        }
    }

    private void PersistRecoveryDraft()
    {
        Interlocked.Increment(ref _draftSaveGeneration);
        _draftSaveCancellation?.Cancel();
        try
        {
            if (!_dirty)
            {
                RecoveryDraftStore.Delete();
                return;
            }

            RecoveryDraftStore.Save(new RecoveryDraft(
                Ack05ConfigurationJson.Serialize(_configuration),
                _savedBaselineJson,
                _path,
                _loadedFileFingerprint,
                DateTimeOffset.UtcNow));
        }
        catch (Exception exception)
        {
            UpdateStatus($"Working draft could not be saved: {exception.Message}");
        }
    }

    private void ScheduleRecoveryDraftSave()
    {
        _draftSaveCancellation?.Cancel();
        _draftSaveCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _draftSaveCancellation = cancellation;
        var generation = Interlocked.Increment(ref _draftSaveGeneration);
        var snapshot = new RecoveryDraft(
            Ack05ConfigurationJson.Serialize(_configuration),
            _savedBaselineJson,
            _path,
            _loadedFileFingerprint,
            DateTimeOffset.UtcNow);
        _ = SaveRecoveryDraftAfterDelayAsync(snapshot, generation, cancellation.Token);
    }

    private async Task SaveRecoveryDraftAfterDelayAsync(
        RecoveryDraft snapshot,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(650, cancellationToken);
            await Task.Run(
                () => RecoveryDraftStore.Save(
                    snapshot,
                    () => Volatile.Read(ref _draftSaveGeneration) == generation),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (!IsDisposed && IsHandleCreated)
            {
                BeginInvoke(() => UpdateStatus($"Working draft could not be saved: {exception.Message}"));
            }
        }
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (!eventArgs.Control)
        {
            return;
        }

        if (eventArgs.KeyCode == Keys.Z && !eventArgs.Shift)
        {
            Undo();
        }
        else if (eventArgs.KeyCode == Keys.Y ||
                 (eventArgs.KeyCode == Keys.Z && eventArgs.Shift))
        {
            Redo();
        }
        else
        {
            return;
        }

        eventArgs.Handled = true;
        eventArgs.SuppressKeyPress = true;
    }

    private int SelectedTargetLayer() =>
        _targetLayer.SelectedItem is string value &&
        int.TryParse(value.AsSpan("Layer ".Length), out var layer)
            ? layer
            : Math.Min(1, CurrentProfile()?.Layers.Length - 1 ?? 1);

    private void ReloadKeepingProfile(string profileId)
    {
        LoadProfiles();
        var index = Array.FindIndex(
            _configuration.Profiles,
            profile => string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
        _profiles.SelectedIndex = Math.Max(index, 0);
    }

    private Ack05Profile? CurrentProfile() =>
        _profiles.SelectedItem is string id
            ? _configuration.Profiles.Single(profile => profile.Id == id)
            : null;

    private Ack05Control? CurrentControl()
    {
        if (_bindings.SelectedRows.Count == 0)
        {
            return null;
        }

        return Enum.Parse<Ack05Control>((string)_bindings.SelectedRows[0].Cells[0].Value);
    }

    private static string ControlDisplayName(Ack05Control control) => control switch
    {
        Ack05Control.RollerClockwise => "Roller CW",
        Ack05Control.RollerCounterClockwise => "Roller CCW",
        _ => control.ToString(),
    };

    private Ack05Action CurrentBindingAction()
    {
        if (CurrentProfile() is not { } profile || CurrentControl() is not { } control)
        {
            return Ack05Action.Disabled();
        }

        return profile.Layers
            .Single(layer => layer.Index == _selectedLayer)
            .Bindings
            .SingleOrDefault(binding => binding.Control == control)
            ?.Action ?? DefaultActionForLayer(_selectedLayer);
    }

    private void SelectControl(Ack05Control control)
    {
        foreach (DataGridViewRow row in _bindings.Rows)
        {
            if ((string)row.Cells[0].Value == control.ToString())
            {
                row.Selected = true;
                _bindings.CurrentCell = row.Cells[0];
                return;
            }
        }
    }

    private string UniqueProfileId(string prefix)
    {
        var index = 1;
        var ids = _configuration.Profiles.Select(profile => profile.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        while (ids.Contains($"{prefix}-{index}"))
        {
            index++;
        }

        return $"{prefix}-{index}";
    }

    private static Ack05Configuration Normalize(Ack05Configuration configuration)
    {
        return new()
        {
            SchemaVersion = Ack05Configuration.CurrentSchemaVersion,
            DefaultProfileId = configuration.DefaultProfileId,
            LayerSwitch = null,
            Profiles = configuration.Profiles
                .Select(NormalizeProfile)
                .ToArray(),
            ExtensionData = configuration.ExtensionData,
        };
    }

    private static Ack05Profile NormalizeProfile(Ack05Profile profile)
    {
        var layerCount = profile.Layers.Length is >= Ack05Configuration.MinimumLayerCount and
            <= Ack05Configuration.MaximumLayerCount
            ? profile.Layers.Length
            : Ack05Configuration.DefaultLayerCount;
        return new()
        {
            Id = profile.Id,
            Name = profile.Name,
            ApplicationMatch = profile.ApplicationMatch,
            TapHolds = profile.TapHolds,
            ComboDefaults = profile.ComboDefaults,
            Combos = profile.Combos,
            Layers = Enumerable.Range(0, layerCount)
                .Select(index => NormalizeLayer(
                    profile.Layers.SingleOrDefault(layer => layer.Index == index),
                    index))
                .ToArray(),
            ExtensionData = profile.ExtensionData,
        };
    }

    private static Ack05Layer NormalizeLayer(
        Ack05Layer? layer,
        int index)
    {
        var existing = layer?.Bindings.ToDictionary(binding => binding.Control)
            ?? new Dictionary<Ack05Control, Ack05Binding>();
        return new Ack05Layer
        {
            Index = index,
            Bindings = BindingCandidates
                .Select(control => new Ack05Binding
                {
                    Control = control,
                    Action = existing.TryGetValue(control, out var binding)
                        ? binding.Action
                        : DefaultActionForLayer(index),
                    ExtensionData = binding?.ExtensionData,
                })
                .ToArray(),
            ExtensionData = layer?.ExtensionData,
        };
    }

    private static Ack05Layer[] DefaultLayers() =>
        Ack05Configuration.CreateDefault().Profiles[0].Layers;

    private static Ack05Action DefaultActionForLayer(int layerIndex) =>
        layerIndex == 0 ? Ack05Action.Disabled() : Ack05Action.Transparent();

    private static Ack05Profile CloneProfile(
        Ack05Profile profile,
        string id,
        string name,
        string? processName) =>
        new()
        {
            Id = id,
            Name = name,
            ApplicationMatch = processName is null
                ? null
                : new Ack05ApplicationMatch
                {
                    ProcessName = processName,
                    ExtensionData = profile.ApplicationMatch?.ExtensionData,
                },
            Layers = profile.Layers,
            TapHolds = profile.TapHolds,
            ComboDefaults = profile.ComboDefaults,
            Combos = profile.Combos,
            ExtensionData = profile.ExtensionData,
        };

    private static Ack05Profile CloneProfileWithLayers(
        Ack05Profile profile,
        Ack05Layer[] layers) =>
        new()
        {
            Id = profile.Id,
            Name = profile.Name,
            ApplicationMatch = profile.ApplicationMatch,
            Layers = layers,
            TapHolds = profile.TapHolds,
            ComboDefaults = profile.ComboDefaults,
            Combos = profile.Combos,
            ExtensionData = profile.ExtensionData,
        };

    private static Ack05Profile CloneProfileWithTapHolds(
        Ack05Profile profile,
        Ack05TapHoldDefinition[] tapHolds) =>
        new()
        {
            Id = profile.Id,
            Name = profile.Name,
            ApplicationMatch = profile.ApplicationMatch,
            Layers = profile.Layers,
            TapHolds = tapHolds,
            ComboDefaults = profile.ComboDefaults,
            Combos = profile.Combos,
            ExtensionData = profile.ExtensionData,
        };

    private static Ack05Profile CloneProfileWithCombos(
        Ack05Profile profile,
        Ack05ComboDefaults defaults,
        Ack05ComboDefinition[] combos) =>
        new()
        {
            Id = profile.Id,
            Name = profile.Name,
            ApplicationMatch = profile.ApplicationMatch,
            Layers = profile.Layers,
            TapHolds = profile.TapHolds,
            ComboDefaults = defaults,
            Combos = combos,
            ExtensionData = profile.ExtensionData,
        };

    private static Ack05Profile ReplaceBinding(
        Ack05Profile profile,
        int layerIndex,
        Ack05Control control,
        Ack05Action action) =>
        new()
        {
            Id = profile.Id,
            Name = profile.Name,
            ApplicationMatch = profile.ApplicationMatch,
            Layers = profile.Layers
                .Select(layer => layer.Index == layerIndex ? ReplaceBinding(layer, control, action) : layer)
                .ToArray(),
            TapHolds = profile.TapHolds,
            ComboDefaults = profile.ComboDefaults,
            Combos = profile.Combos,
            ExtensionData = profile.ExtensionData,
        };

    private static Ack05Layer ReplaceBinding(Ack05Layer layer, Ack05Control control, Ack05Action action) =>
        new()
        {
            Index = layer.Index,
            Bindings = layer.Bindings
                .Select(binding => binding.Control == control
                    ? new Ack05Binding
                    {
                        Control = control,
                        Action = action,
                        ExtensionData = binding.ExtensionData,
                    }
                    : binding)
                .ToArray(),
            ExtensionData = layer.ExtensionData,
        };

    private static string DescribeAction(Ack05Action action) =>
        action.Kind switch
        {
            Ack05ActionKind.Disabled => "Disabled",
            Ack05ActionKind.Transparent => "Transparent",
            Ack05ActionKind.KeyboardChord => string.Join(" + ", action.Keys ?? []),
            Ack05ActionKind.MouseWheel => $"Wheel {action.WheelSteps:+#;-#;0}",
            Ack05ActionKind.MomentaryLayer => $"MO({action.TargetLayer})",
            Ack05ActionKind.ToggleLayer => $"TG({action.TargetLayer})",
            Ack05ActionKind.TapToggleHoldLayer => $"Tap/Hold({action.TargetLayer})",
            Ack05ActionKind.TapHold => $"TH({action.TapHoldId})",
            _ => action.Kind.ToString(),
        };

    private void UpdateStatus(string message)
    {
        _status.Text = string.IsNullOrWhiteSpace(_path)
            ? message
            : $"{message}  Path: {_path}";
    }

    private enum TapHoldSlot
    {
        Tap,
        Hold,
    }

    private sealed record TapHoldListItem(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record ComboListItem(string Id, string Label)
    {
        public override string ToString() => Label;
    }
}
