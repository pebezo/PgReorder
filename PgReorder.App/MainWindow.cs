﻿using System.Diagnostics.CodeAnalysis;
using System.Text;
using PgReorder.Core;
using Terminal.Gui;

namespace PgReorder.App;

public class MainWindow : Window
{
    private enum LeftSideView { Schemas, Tables, Columns }
    private readonly ContextService _context;
    private readonly string _version;

    // Colors
    private readonly ColorScheme _colorScript;
    private readonly ColorScheme _colorNormal;
    private readonly ColorScheme _colorDimmed;
    
    // UI components
    private StatusBar _statusBar;
    private Window _containerLeft;
    private Window _containerRight;
    private TableView _schemasTableView;
    private TableView _tablesTableView;
    private TableView _columnsTableView;
    private TextView _textViewScript;
    private Shortcut _shortcutMoveUp; 
    private Shortcut _shortcutMoveDown; 
    
    public MainWindow(ContextService context, string version)
    {
        _context = context;
        _version = version;

        Application.Force16Colors = true;
        Application.QuitKey = Key.F10;
        // Remove the border around the main window
        BorderStyle = LineStyle.None;
        
        _colorScript = CreateScheme(Color.BrightCyan, Color.Blue, Color.BrightBlue, Color.Blue);
        _colorNormal = CreateScheme(Color.White, Color.Blue, Color.White, Color.BrightBlue);
        _colorDimmed = CreateScheme(Color.DarkGray, Color.Blue, Color.White, Color.BrightBlue);

        BuildLeftPanel();
        BuildRightPanel();
        BuildStatusBar();

        CurrentLeftSide = LeftSideView.Schemas;

        KeyDown += (_, key) =>
        {
            if (key == Key.Esc || key == Key.F2)
            {
                key.Handled = true;
                PreviousLeftSide();
                SetNeedsDraw();
            }

            if (key == Key.F5)
            {
                key.Handled = true;
                MoveColumns(-1);
            }
            
            if (key == Key.F8)
            {
                key.Handled = true;
                MoveColumns(+1);
            }
        };

        KeyDownNotHandled += (_, _) =>
        {
            
        };
    }

    [MemberNotNull(nameof(_containerLeft))]
    [MemberNotNull(nameof(_schemasTableView))]
    [MemberNotNull(nameof(_tablesTableView))]
    [MemberNotNull(nameof(_columnsTableView))]
    private void BuildLeftPanel()
    {
        _schemasTableView = BuildSchemasTableView();
        _tablesTableView = BuildTablesTableView();
        _columnsTableView = BuildColumnsTableView();
        
        _containerLeft = new Window
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(50),
            Height = Dim.Fill(Dim.Func(() => _statusBar.Frame.Height)),
            BorderStyle = LineStyle.Rounded,
            SuperViewRendersLineCanvas = true,
            CanFocus = true,
            TabStop = TabBehavior.TabStop,
            // Override the default '_' rune for the hotkey specifier to something that is unlikely to be found in
            // a schema or table name.
            HotKeySpecifier = new Rune(RuneExtensions.MaxUnicodeCodePoint)
        };

        _containerLeft.Add(_schemasTableView, _tablesTableView, _columnsTableView);

        Add(_containerLeft);
    }

    private TableView BuildSchemasTableView()
    {
        _schemasTableView = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            BorderStyle = LineStyle.None,
            SuperViewRendersLineCanvas = true,
            CanFocus = true,
            Visible = false
        };

        _schemasTableView.Table = new EnumerableTableSource<PgSchema>(_context.Schemas,
            new Dictionary<string, Func<PgSchema, object>>
            {
                { "Schema", p => p.SchemaName },
                { "Owner", p => p.Owner }
            });

        _schemasTableView.FullRowSelect = true;
        _schemasTableView.Style.ShowHeaders = false;
        _schemasTableView.Style.ShowHorizontalHeaderOverline = false;
        _schemasTableView.Style.ShowHorizontalHeaderUnderline = false;
        _schemasTableView.Style.ShowHorizontalBottomline = false;
        _schemasTableView.Style.ShowVerticalCellLines = false;
        _schemasTableView.Style.ShowVerticalHeaderLines = false;

        int longestSchemaName = _context.Schemas.Max(p => (int?)p.SchemaName.Length) ?? 0;
        
        _schemasTableView.Style.ColumnStyles.Add(0, new ColumnStyle
        {
            MaxWidth = longestSchemaName,
            MinWidth = longestSchemaName,
            MinAcceptableWidth = longestSchemaName,
            ColorGetter = _ => _colorNormal
        });
        
        _schemasTableView.Style.ColumnStyles.Add(1, new ColumnStyle
        {
            MaxWidth = 1,
            Alignment = Alignment.End,
            ColorGetter = _ => _colorDimmed 
        });
        _schemasTableView.ColorScheme = _colorNormal;
        
        // TableView typically is a grid where nav keys are biased for moving left/right.
        _schemasTableView.KeyBindings.Remove (Key.Home);
        _schemasTableView.KeyBindings.Add (Key.Home, Command.Start);
        _schemasTableView.KeyBindings.Remove (Key.End);
        _schemasTableView.KeyBindings.Add (Key.End, Command.End);
        _schemasTableView.KeyBindings.Remove (Key.A.WithCtrl);
        // Ideally, TableView.MultiSelect = false would turn off any keybindings for
        // multi-select options. But it currently does not. UI Catalog uses Ctrl-A for
        // a shortcut to About.
        _schemasTableView.MultiSelect = false;

        _schemasTableView.KeyDownNotHandled += (_, key) =>
        {
            // Don't allow the up/down cursor to change focus when it reaches the top/bottom of the script box 
            if (key == Key.CursorDown || key == Key.CursorUp)
            {
                key.Handled = true;
            }
        };

        _schemasTableView.CellActivated += (_, _) =>
        {
            _context.SelectSchema(_schemasTableView.SelectedRow);
            
            RefreshListOfTables();
            NextLeftSide();
        };

        return _schemasTableView;
    }
    
    private TableView BuildTablesTableView()
    {
        _tablesTableView = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            BorderStyle = LineStyle.None,
            SuperViewRendersLineCanvas = true,
            CanFocus = true,
            Visible = false
        };

        _tablesTableView.FullRowSelect = true;
        _tablesTableView.Style.ShowHeaders = false;
        _tablesTableView.Style.ShowHorizontalHeaderOverline = false;
        _tablesTableView.Style.ShowHorizontalHeaderUnderline = false;
        _tablesTableView.Style.ShowHorizontalBottomline = false;
        _tablesTableView.Style.ShowVerticalCellLines = false;
        _tablesTableView.Style.ShowVerticalHeaderLines = false;

        _tablesTableView.Style.ColumnStyles.Add(0, new ColumnStyle
        {
            // These should be set after the list of tables was loaded
            MaxWidth = 0,
            MinWidth = 0,
            MinAcceptableWidth = 0,
            ColorGetter = _ => _colorNormal
        });
        
        _tablesTableView.Style.ColumnStyles.Add(1, new ColumnStyle
        {
            MaxWidth = 1,
            Alignment = Alignment.End,
            ColorGetter = _ => _colorDimmed 
        });
        _tablesTableView.ColorScheme = _colorNormal;
        
        // TableView typically is a grid where nav keys are biased for moving left/right.
        _tablesTableView.KeyBindings.Remove (Key.Home);
        _tablesTableView.KeyBindings.Add (Key.Home, Command.Start);
        _tablesTableView.KeyBindings.Remove (Key.End);
        _tablesTableView.KeyBindings.Add (Key.End, Command.End);
        _tablesTableView.KeyBindings.Remove (Key.A.WithCtrl);
        // Ideally, TableView.MultiSelect = false would turn off any keybindings for
        // multi-select options. But it currently does not. UI Catalog uses Ctrl-A for
        // a shortcut to About.
        _tablesTableView.MultiSelect = false;

        _tablesTableView.KeyDownNotHandled += (_, key) =>
        {
            // Don't allow the up/down cursor to change focus when it reaches the top/bottom of the script box 
            if (key == Key.CursorDown || key == Key.CursorUp)
            {
                key.Handled = true;
            }
        };

        _tablesTableView.CellActivated += (_, _) =>
        {
            _context.SelectTable(_tablesTableView.SelectedRow);
            
            RefreshListOfColumns();
            RefreshScript();
            NextLeftSide();
        };

        return _tablesTableView;
    }

    private void PreviousLeftSide()
    {
        switch (CurrentLeftSide)
        {
            case LeftSideView.Schemas:
                break;
            case LeftSideView.Tables:
                CurrentLeftSide = LeftSideView.Schemas;
                break;
            case LeftSideView.Columns:
                CurrentLeftSide = LeftSideView.Tables;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(CurrentLeftSide), CurrentLeftSide, "Unhandled left side");
        }
    }
    
    private void NextLeftSide()
    {
        switch (CurrentLeftSide)
        {
            case LeftSideView.Schemas:
                CurrentLeftSide = LeftSideView.Tables;
                break;
            case LeftSideView.Tables:
                CurrentLeftSide = LeftSideView.Columns;
                break;
            case LeftSideView.Columns:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(CurrentLeftSide), CurrentLeftSide, "Unhandled left side");
        }
    }

    private LeftSideView CurrentLeftSide
    {
        // ReSharper disable once RedundantAccessorBody
        get => field;
        set
        {
            field = value;
            // Hide all
            _schemasTableView.Visible = false;
            _tablesTableView.Visible = false;
            _columnsTableView.Visible = false;
            _shortcutMoveUp.Visible = false;
            _shortcutMoveDown.Visible = false;

            switch (value)
            {
                case LeftSideView.Schemas:
                    _schemasTableView.Visible = true;
                    _containerLeft.Title = "Schemas";
                    _textViewScript.Text = "<< select a schema from the list";
                    break;
                case LeftSideView.Tables:
                    _tablesTableView.Visible = true;
                    _containerLeft.Title = "Schema: " + _context.SelectedSchema?.SchemaName;
                    _textViewScript.Text = "<< select a table from the list";
                    break;
                case LeftSideView.Columns:
                    _columnsTableView.Visible = true;
                    _containerLeft.Title = "Schema: " + _context.SelectedSchema?.SchemaName + "." + _context.SelectedTable?.TableName;
                    _shortcutMoveUp.Visible = true;
                    _shortcutMoveDown.Visible = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(CurrentLeftSide), value, "Unhandled left side");
            }

            SetNeedsDraw();
        }
    }

    private void MoveColumns(int position)
    {
        if (CurrentLeftSide == LeftSideView.Columns)
        {
            var nextRow = _columnsTableView.SelectedRow + position;
            if (nextRow >= 0 && nextRow < _context.Columns.Count)
            {
                _context.Move(_columnsTableView.SelectedRow, position);
                RefreshListOfColumns();
                RefreshScript();
                _columnsTableView.SelectedRow = nextRow;
            }
        }
    }

    private void RefreshListOfTables()
    {
        _tablesTableView.Table = new EnumerableTableSource<PgTable>(_context.Tables,
            new Dictionary<string, Func<PgTable, object>>
            {
                { "Tables", p => p.TableName },
                { "Owner", p => p.Owner }
            });
        
        int longestTableName = _context.Tables.Max(p => (int?)p.TableName.Length) ?? 0;

        var tableNameColumn = _tablesTableView.Style.ColumnStyles[0];
        tableNameColumn.MaxWidth = longestTableName;
        tableNameColumn.MinWidth = longestTableName;
        tableNameColumn.MinAcceptableWidth = longestTableName;
    }
    
    private TableView BuildColumnsTableView()
    {
        _columnsTableView = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            BorderStyle = LineStyle.None,
            SuperViewRendersLineCanvas = true,
            CanFocus = true,
            Visible = false
        };

        _columnsTableView.FullRowSelect = true;
        _columnsTableView.Style.ShowHeaders = false;
        _columnsTableView.Style.ShowHorizontalHeaderOverline = false;
        _columnsTableView.Style.ShowHorizontalHeaderUnderline = false;
        _columnsTableView.Style.ShowHorizontalBottomline = false;
        _columnsTableView.Style.ShowVerticalCellLines = false;
        _columnsTableView.Style.ShowVerticalHeaderLines = false;

        _columnsTableView.Style.ColumnStyles.Add(0, new ColumnStyle
        {
            // These should be set after the list of tables was loaded
            MaxWidth = 0,
            MinWidth = 0,
            MinAcceptableWidth = 0,
            ColorGetter = _ => _colorNormal
        });
        
        _columnsTableView.Style.ColumnStyles.Add(1, new ColumnStyle
        {
            MaxWidth = 1,
            Alignment = Alignment.End,
            ColorGetter = _ => _colorDimmed 
        });
        _columnsTableView.ColorScheme = _colorNormal;
        
        // TableView typically is a grid where nav keys are biased for moving left/right.
        _columnsTableView.KeyBindings.Remove (Key.Home);
        _columnsTableView.KeyBindings.Add (Key.Home, Command.Start);
        _columnsTableView.KeyBindings.Remove (Key.End);
        _columnsTableView.KeyBindings.Add (Key.End, Command.End);
        _columnsTableView.KeyBindings.Remove (Key.A.WithCtrl);
        // Ideally, TableView.MultiSelect = false would turn off any keybindings for
        // multi-select options. But it currently does not. UI Catalog uses Ctrl-A for
        // a shortcut to About.
        _columnsTableView.MultiSelect = false;

        return _columnsTableView;
    }

    private void RefreshListOfColumns()
    {
        _columnsTableView.Table = new EnumerableTableSource<PgColumn>(_context.Columns,
            new Dictionary<string, Func<PgColumn, object?>>
            {
                { "Name", p => p.ColumnName },
                { "Type", p => p.DataType }
            });
        
        int longestColumnName = _context.Columns.Max(p => p.ColumnName?.Length) ?? 0;

        var columnNameColumn = _columnsTableView.Style.ColumnStyles[0];
        columnNameColumn.MaxWidth = longestColumnName;
        columnNameColumn.MinWidth = longestColumnName;
        columnNameColumn.MinAcceptableWidth = longestColumnName;
    }

    private void RefreshScript()
    {
        var script = _context.GeneratedScript();
        _textViewScript.Text = script;
    }
    
    [MemberNotNull(nameof(_containerRight))]
    [MemberNotNull(nameof(_textViewScript))]
    private void BuildRightPanel()
    {
        _containerRight = new Window
        {
            X = Pos.Right(_containerLeft) - 1,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(Dim.Func(() => _statusBar.Frame.Height)),
            BorderStyle = LineStyle.Rounded,
            Title = "SQL",
            SuperViewRendersLineCanvas = true,
            CanFocus = true,
            TabStop = TabBehavior.TabStop
        };

        _textViewScript = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            BorderStyle = LineStyle.None,
            ColorScheme = _colorScript,
            SuperViewRendersLineCanvas = true,
            ReadOnly = true,
            CanFocus = true,
            TabStop = TabBehavior.TabStop
        };

        _textViewScript.KeyDownNotHandled += (_, key) =>
        {
            // Don't allow the up/down cursor to change focus when it reaches the top/bottom of the script box 
            if (key == Key.CursorDown || key == Key.CursorUp)
            {
                key.Handled = true;
            }
        };

        _containerRight.Add(_textViewScript);

        Add(_containerRight);
    }

    [MemberNotNull(nameof(_statusBar))]
    [MemberNotNull(nameof(_shortcutMoveUp))]
    [MemberNotNull(nameof(_shortcutMoveDown))]
    private void BuildStatusBar()
    {
        _statusBar = new StatusBar
        {
            CanFocus = false,
            AlignmentModes = AlignmentModes.IgnoreFirstOrLast
        };

        _shortcutMoveUp = new Shortcut
        {
            CanFocus = false,
            Title = "Move Up",
            Key = Key.F5,
            Visible = false
        };

        _shortcutMoveDown = new Shortcut
        {
            CanFocus = false,
            Title = "Move Down",
            Key = Key.F8,
            Visible = false
        };
        
        _statusBar.Add(
            new Shortcut
            {
                CanFocus = false,
                Title = "Help",
                Key = Key.F1,
                Action = () =>
                {
                    var helpWindow = new HelpWindow($"PgReorder {_version}", GetHelpBoxMessage());
                    Application.Run(helpWindow);
                    helpWindow.Dispose();
                }
            },
            new Shortcut
            {
                CanFocus = false,
                Title = "Go Back",
                Key = Key.F2
            },
            new Shortcut
            {
                CanFocus = false,
                Title = "Quit",
                Key = Application.QuitKey
            },
            _shortcutMoveUp,
            _shortcutMoveDown,
            new Shortcut
            {
                CanFocus = false,
                Title = $"PgReorder {_version}"
            }
        );

        Add(_statusBar);
    }

    private static string GetHelpBoxMessage()
    {
        StringBuilder sb = new();

        sb.AppendLine();
        sb.AppendLine(@"     ____          ____                          __             ");           
        sb.AppendLine(@"    / __ \ ____ _ / __ \ ___   ____   _____ ____/ /___   _____  "); 
        sb.AppendLine(@"   / /_/ // __ `// /_/ // _ \ / __ \ / ___// __  // _ \ / ___/  ");
        sb.AppendLine(@"  / ____// /_/ // _, _//  __// /_/ // /   / /_/ //  __// /      ");
        sb.AppendLine(@" /_/     \__, //_/ |_| \___/ \____//_/    \__,_/ \___//_/       ");
        sb.AppendLine(@"        /____/                                                  "); 
        sb.AppendLine();
        sb.AppendLine(" F2  | Go back to the previous screen (Esc also works)");
        sb.AppendLine(" ----[ On the columns screen ]");
        sb.AppendLine(" F5  | Move selected column up");
        sb.AppendLine(" F8  | Move selected column down");
        sb.AppendLine();
        sb.AppendLine("https://github.com/pebezo/PgReorder");
        
        return sb.ToString();
    }
    
    private static ColorScheme CreateScheme(
        Color normalFg, Color normalBg,
        Color focusFg, Color focusBg)
    {
        return new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(normalFg, normalBg),
            // flip color for selected nodes
            Focus = new Terminal.Gui.Attribute(focusFg, focusBg),
            HotNormal = new Terminal.Gui.Attribute(normalFg, normalBg),
            HotFocus = new Terminal.Gui.Attribute(normalBg, normalFg),
            Disabled = new Terminal.Gui.Attribute(normalFg, normalBg)
        };
    }
}