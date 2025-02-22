using System.Diagnostics.CodeAnalysis;
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
    private readonly ColorScheme _colorHighlight;
    private readonly ColorScheme _colorNotChecked;
    private readonly ColorScheme _colorChecked;
    private readonly ColorScheme _colorPrimaryKey;
    private readonly ColorScheme _colorForeignKey;
    
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
    private Shortcut _shortcutCopy; 
    
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
        _colorHighlight = CreateScheme(Color.Yellow, Color.Blue, Color.BrightYellow, Color.BrightBlue);
        _colorNotChecked = CreateScheme(Color.Blue, Color.Blue, Color.White, Color.BrightBlue);
        _colorChecked = CreateScheme(Color.Green, Color.Blue, Color.BrightGreen, Color.BrightBlue);
        _colorPrimaryKey = CreateScheme(Color.Yellow, Color.Blue, Color.BrightYellow, Color.BrightBlue);
        _colorForeignKey = CreateScheme(Color.Cyan, Color.Blue, Color.BrightCyan, Color.BrightBlue);

        BuildLeftPanel();
        BuildRightPanel();
        BuildStatusBar();

        CurrentLeftSide = LeftSideView.Schemas;

        KeyDown += (_, key) =>
        {
            if (key == Key.Esc || key == Key.F2 || key == Key.Backspace)
            {
                key.Handled = true;
                PreviousLeftSide();
                SetNeedsDraw();
            }

            if (key == Key.C.WithCtrl || key == Key.F9)
            {
                key.Handled = true;
                CopyScriptToClipboard();
            }
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
        _tablesTableView.KeyBindings.Remove(Key.Home);
        _tablesTableView.KeyBindings.Add(Key.Home, Command.Start);
        _tablesTableView.KeyBindings.Remove(Key.End);
        _tablesTableView.KeyBindings.Add(Key.End, Command.End);
        _tablesTableView.KeyBindings.Remove(Key.A.WithCtrl);
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

        // Selection indicator
        _columnsTableView.Style.ColumnStyles.Add(0, new ColumnStyle
        {
            MinWidth = 1,
            MaxWidth = 1,
            MinAcceptableWidth = 2,
            Alignment = Alignment.Start,
            ColorGetter = e => e.CellValue as string == "*" ? _colorChecked : _colorNotChecked
        });
        
        // PK/FK column
        _columnsTableView.Style.ColumnStyles.Add(1, new ColumnStyle
        {
            MinWidth = 2,
            MaxWidth = 2,
            MinAcceptableWidth = 2,
            Alignment = Alignment.Start,
            ColorGetter = e => (e.CellValue as string) switch
            {
                "PK" => _colorPrimaryKey,
                "FK" => _colorForeignKey,
                _ => _colorHighlight
            }
        });
        
        // Column name
        _columnsTableView.Style.ColumnStyles.Add(2, new ColumnStyle
        {
            // These should be set after the list of tables was loaded
            MaxWidth = 0,
            MinWidth = 0,
            Alignment = Alignment.Start,
            MinAcceptableWidth = 0,
            ColorGetter = _ => _colorNormal
        });
        
        // Column data type           
        _columnsTableView.Style.ColumnStyles.Add(3, new ColumnStyle
        {
            // These should be set after the list of tables was loaded
            MaxWidth = 0,
            MinWidth = 0,
            Alignment = Alignment.End,
            ColorGetter = _ => _colorDimmed 
        });

        _columnsTableView.ColorScheme = _colorNormal;
        
        
        // TableView typically is a grid where nav keys are biased for moving left/right.
        _columnsTableView.KeyBindings.Remove(Key.Home);
        _columnsTableView.KeyBindings.Add(Key.Home, Command.Start);
        _columnsTableView.KeyBindings.Remove(Key.End);
        _columnsTableView.KeyBindings.Add(Key.End, Command.End);
        _columnsTableView.KeyBindings.Remove(Key.A.WithCtrl);
        _columnsTableView.KeyBindings.Remove(Key.Space);
        // Ideally, TableView.MultiSelect = false would turn off any keybindings for
        // multi-select options. But it currently does not. UI Catalog uses Ctrl-A for
        // a shortcut to About.
        _columnsTableView.MultiSelect = false;
        
        _columnsTableView.KeyDownNotHandled += (_, key) =>
        {
            // Don't allow the up/down cursor to change focus when it reaches the top/bottom of the script box 
            if (key == Key.CursorDown || key == Key.CursorUp || key == Key.CursorLeft || key == Key.CursorRight)
            {
                key.Handled = true;
            }

            if (key == Key.F5 || key == Key.CursorUp.WithAlt || key == Key.CursorUp.WithCtrl)
            {
                key.Handled = true;
                MoveColumns(-1);
            }
            
            if (key == Key.F8  || key == Key.CursorDown.WithAlt || key == Key.CursorDown.WithCtrl)
            {
                key.Handled = true;
                MoveColumns(+1);
            }
            
            if (key == Key.Space)
            {
                key.Handled = true;
                ToggleSelectionForCurrentRow();
            }
            
            if ((uint)key.KeyCode == 42 /* star */)
            {
                key.Handled = true;
                ToggleSelection();
            }
            
            if ((uint)key.KeyCode == 43 /* plus */)
            {
                key.Handled = true;
                SelectAll();
            }
            
            if ((uint)key.KeyCode == 45 /* minus */)
            {
                key.Handled = true;
                UnselectAll();
            }
        };

        return _columnsTableView;
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

        _textViewScript.KeyDown += (_, key) =>
        {
            // If nothing is selected in the text view we should copy the entire script. However, if there is a selection
            // then the default copy behavior should be maintained.
            if (key == Key.C.WithCtrl && _textViewScript.SelectedLength == 0) 
            {
                key.Handled = true;
                CopyScriptToClipboard();
            }
        };
        
        _textViewScript.KeyDownNotHandled += (_, key) =>
        {
            // Don't allow the up/down cursor to change focus when it reaches the top/bottom of the script box 
            if (key == Key.CursorDown || key == Key.CursorUp || key == Key.CursorLeft || key == Key.CursorRight)
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
    [MemberNotNull(nameof(_shortcutCopy))]
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
        
        _shortcutCopy = new Shortcut
        {
            CanFocus = false,
            Title = "Copy",
            Key = Key.F9,
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
                    var helpWindow = new HelpWindow($"PgReorder {_version}");
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
            _shortcutCopy,
            new Shortcut
            {
                CanFocus = false,
                Title = $"PgReorder {_version}"
            }
        );

        Add(_statusBar);
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
            _shortcutCopy.Visible = false;

            switch (value)
            {
                case LeftSideView.Schemas:
                    _schemasTableView.Visible = true;
                    _containerLeft.Title = $"Schemas ({_context.Schemas.Count})";
                    _textViewScript.WordWrap = true;
                    _textViewScript.Text = "<< Select a schema from the list using the arrow keys and Enter (or mouse)";
                    break;
                case LeftSideView.Tables:
                    _tablesTableView.Visible = true;
                    _containerLeft.Title = $"{_context.SelectedSchema?.SchemaName} ({_context.Tables.Count})";
                    _textViewScript.WordWrap = true;
                    _textViewScript.Text = "<< Select a table from the list using the arrow key and Enter (or mouse)";
                    break;
                case LeftSideView.Columns:
                    _columnsTableView.Visible = true;
                    _containerLeft.Title = $"{_context.SelectedSchema?.SchemaName}.{_context.SelectedTable?.TableName} ({_context.Columns.Count})";
                    _shortcutMoveUp.Visible = true;
                    _shortcutMoveDown.Visible = true;
                    _shortcutCopy.Visible = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(CurrentLeftSide), value, "Unhandled left side");
            }

            SetNeedsDraw();
        }
    }

    private void MoveColumns(int offset)
    {
        if (CurrentLeftSide == LeftSideView.Columns)
        {
            if (_context.Move(_columnsTableView.SelectedRow, offset))
            {
                RefreshListOfColumns();
                RefreshScript();

                var (first, last) = _context.FindFirstLastIndex();

                if (first is not null && last is not null)
                {
                    _columnsTableView.SelectedRow = offset > 0 ? last.Value : first.Value;
                }
                else
                {
                    _columnsTableView.SelectedRow += offset;
                }
            }
        }
    }

    private void ToggleSelectionForCurrentRow()
    {
        if (CurrentLeftSide == LeftSideView.Columns)
        {
            _context.ToggleSelection(_columnsTableView.SelectedRow);
            SetNeedsDraw();
        }
    }

    private void ToggleSelection()
    {
        if (CurrentLeftSide == LeftSideView.Columns)
        {
            _context.ToggleSelection();
            SetNeedsDraw();
        }
    }

    private void SelectAll()
    {
        if (CurrentLeftSide == LeftSideView.Columns)
        {
            _context.SelectAll();
            SetNeedsDraw();
        }
    }

    private void UnselectAll()
    {
        if (CurrentLeftSide == LeftSideView.Columns)
        {
            _context.UnselectAll();
            SetNeedsDraw();
        }
    }

    private void CopyScriptToClipboard()
    {
        if (!Clipboard.TrySetClipboardData(_textViewScript.Text))
        {
            MessageBox.ErrorQuery("Clipboard", "Was unable to copy script content to clipboard");
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

    private void RefreshListOfColumns()
    {
        _columnsTableView.Table = new EnumerableTableSource<PgColumn>(_context.Columns,
            new Dictionary<string, Func<PgColumn, object?>>
            {
                { "S", p => p.DisplayIsSelected },
                { "C", p => p.DisplayConstraint },
                { "Name", p => p.ColumnName },
                { "Type", p => p.DataType }
            });
        
        int longestColumnName = _context.Columns.Max(p => p.ColumnName?.Length) ?? 0;
        
        var columnNameColumn = _columnsTableView.Style.ColumnStyles[2];
        columnNameColumn.MaxWidth = longestColumnName;
        columnNameColumn.MinWidth = longestColumnName;
        columnNameColumn.MinAcceptableWidth = longestColumnName;
        
        int longestTypeDefinition = _context.Columns.Max(p => p.DataType?.Length) ?? 0;
        
        var columnType = _columnsTableView.Style.ColumnStyles[3];
        columnType.MaxWidth = longestTypeDefinition;
        columnType.MinWidth = longestTypeDefinition;
        columnType.MinAcceptableWidth = longestTypeDefinition;
    }

    private void RefreshScript()
    {
        if (_context.OrderHasChanged)
        {
            var script = _context.GeneratedScript();
            _textViewScript.WordWrap = false;
            _textViewScript.Text = script;
        }
        else
        {
            _textViewScript.WordWrap = true;
            _textViewScript.Text =
                "<< Reorder one or more columns using Alt or Ctrl + arrow keys (or F5/F8)." + 
                Environment.NewLine +
                Environment.NewLine +
                "<< After the columns are in the desired new order hit Ctrl+C to copy the script the clipboard.";    
        }
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