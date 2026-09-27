using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using DiagramDesigner.Config;
using DiagramDesigner.Controls;
using DiagramDesigner.Core;
using DiagramDesigner.Shapes;

namespace DemoApp
{
    /// <summary>
    /// 编辑器事件追踪窗体。非模式弹出，实时显示宿主窗体接收到的
    /// 编辑器控件事件、画布鼠标/键盘事件、控件接口特性（属性/方法/事件）。
    /// 用于测试和发现编辑器控件封装的局限性。
    /// </summary>
    public class EventMonitorForm : Form
    {
        private DiagramEditor _editor;

        // 布局控件
        private TabControl _tabs;
        private TabPage _tabEvents;
        private TabPage _tabState;
        private TabPage _tabInterface;

        // 事件日志页
        private ListBox _eventLog;
        private ToolStrip _logToolbar;
        private ToolStripButton _btnClear;
        private ToolStripButton _btnPause;
        private ToolStripLabel _lblCount;
        private ToolStripLabel _lblFilter;
        private ToolStripComboBox _cmbFilter;
        private bool _paused = false;
        private int _eventCount = 0;
        private int _maxLogLines = 500;

        // 编辑器状态页
        private PropertyGrid _stateGrid;
        private Timer _stateTimer;

        // 事件日志批量刷新
        private Timer _logFlushTimer;
        private List<LogEntry> _pendingEntries = new List<LogEntry>();
        private bool _logFlushScheduled = false;

        // 接口特性页
        private TreeView _interfaceTree;

        // 事件过滤类别
        private const string FILTER_ALL = "全部";
        private const string FILTER_EDITOR = "编辑器事件";
        private const string FILTER_CANVAS = "画布事件";
        private const string FILTER_MOUSE = "鼠标事件";
        private const string FILTER_KEY = "键盘事件";
        private const string FILTER_CHANGE = "增量变更";

        /// <summary>当前过滤类别</summary>
        private string _currentFilter = FILTER_ALL;

        // 暂存完整日志条目（含类别），用于切换过滤时重建
        private List<LogEntry> _allEntries = new List<LogEntry>();

        /// <summary>日志条目内部结构</summary>
        private struct LogEntry
        {
            public string TimeStamp;
            public string Category;
            public string Text;
        }

        public EventMonitorForm(DiagramEditor editor)
        {
            _editor = editor;

            this.Text = "事件追踪监视器";
            this.Size = new Size(520, 560);
            this.StartPosition = FormStartPosition.Manual;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.ShowInTaskbar = false;
            this.MinimizeBox = false;
            this.MaximizeBox = true;
            this.TopMost = false;

            BuildLayout();
            SubscribeEvents();
            LoadInterfaceInfo();
            StartStatePolling();

            // 事件日志批量刷新定时器
            _logFlushTimer = new Timer();
            _logFlushTimer.Interval = 50;
            _logFlushTimer.Tick += new EventHandler(OnLogFlushTimerTick);
        }

        #region 布局构建

        private void BuildLayout()
        {
            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;

            // === 事件日志页 ===
            _tabEvents = new TabPage("事件日志");
            BuildEventLogTab();
            _tabs.TabPages.Add(_tabEvents);

            // === 编辑器状态页 ===
            _tabState = new TabPage("编辑器状态");
            BuildStateTab();
            _tabs.TabPages.Add(_tabState);

            // === 接口特性页 ===
            _tabInterface = new TabPage("接口特性");
            BuildInterfaceTab();
            _tabs.TabPages.Add(_tabInterface);

            this.Controls.Add(_tabs);
        }

        private void BuildEventLogTab()
        {
            // 工具栏
            _logToolbar = new ToolStrip();
            _logToolbar.Dock = DockStyle.Top;
            _logToolbar.GripStyle = ToolStripGripStyle.Hidden;
            _logToolbar.Padding = new Padding(2, 0, 2, 0);

            _btnClear = new ToolStripButton("清空");
            _btnClear.DisplayStyle = ToolStripItemDisplayStyle.Text;
            _btnClear.Click += new EventHandler(OnClearLog);

            _btnPause = new ToolStripButton("暂停");
            _btnPause.DisplayStyle = ToolStripItemDisplayStyle.Text;
            _btnPause.CheckOnClick = true;
            _btnPause.Click += new EventHandler(OnPauseToggle);

            _lblCount = new ToolStripLabel("事件: 0");

            _lblFilter = new ToolStripLabel("过滤:");

            _cmbFilter = new ToolStripComboBox();
            _cmbFilter.Items.AddRange(new string[] { FILTER_ALL, FILTER_EDITOR, FILTER_CANVAS, FILTER_MOUSE, FILTER_KEY, FILTER_CHANGE });
            _cmbFilter.SelectedIndex = 0;
            _cmbFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbFilter.SelectedIndexChanged += new EventHandler(OnFilterChanged);

            _logToolbar.Items.Add(_btnClear);
            _logToolbar.Items.Add(_btnPause);
            _logToolbar.Items.Add(new ToolStripSeparator());
            _logToolbar.Items.Add(_lblCount);
            _logToolbar.Items.Add(new ToolStripSeparator());
            _logToolbar.Items.Add(_lblFilter);
            _logToolbar.Items.Add(_cmbFilter);

            // 事件列表
            _eventLog = new ListBox();
            _eventLog.Dock = DockStyle.Fill;
            _eventLog.Font = new Font("Consolas", 8.25f);
            _eventLog.HorizontalScrollbar = true;
            _eventLog.ScrollAlwaysVisible = true;

            _tabEvents.Controls.Add(_eventLog);
            _tabEvents.Controls.Add(_logToolbar);
        }

        private void BuildStateTab()
        {
            _stateGrid = new PropertyGrid();
            _stateGrid.Dock = DockStyle.Fill;
            _stateGrid.ToolbarVisible = false;
            _stateGrid.HelpVisible = false;
            _stateGrid.PropertySort = PropertySort.Categorized;

            _tabState.Controls.Add(_stateGrid);
        }

        private void BuildInterfaceTab()
        {
            _interfaceTree = new TreeView();
            _interfaceTree.Dock = DockStyle.Fill;
            _interfaceTree.Font = new Font("Consolas", 8.25f);

            _tabInterface.Controls.Add(_interfaceTree);
        }

        #endregion

        #region 事件订阅

        /// <summary>
        /// 订阅编辑器控件的全部公共事件，以及画布的鼠标/键盘事件。
        /// </summary>
        private void SubscribeEvents()
        {
            // === 编辑器级事件 ===
            _editor.ShapeAdded += new EventHandler<ShapeEventArgs>(OnShapeAdded);
            _editor.ShapeDeleted += new EventHandler<ShapeEventArgs>(OnShapeDeleted);
            _editor.ConnectionAdded += new EventHandler<ConnectionEventArgs>(OnConnectionAdded);
            _editor.ConnectionDeleted += new EventHandler<ConnectionEventArgs>(OnConnectionDeleted);
            _editor.ShapeStateChanged += new EventHandler<ShapeStateChangedEventArgs>(OnShapeStateChanged);
            _editor.ThemeChanged += new EventHandler(OnThemeChanged);

            // === 画布级事件（通过 Editor.Canvas 访问）===
            _editor.Canvas.SelectionChanged += new EventHandler(OnSelectionChanged);
            _editor.Canvas.DocumentModified += new EventHandler(OnDocumentModified);
            _editor.Canvas.ShapeAdded += new EventHandler<ShapeEventArgs>(OnCanvasShapeAdded);
            _editor.Canvas.ShapeDeleted += new EventHandler<ShapeEventArgs>(OnCanvasShapeDeleted);
            _editor.Canvas.ConnectionAdded += new EventHandler<ConnectionEventArgs>(OnCanvasConnectionAdded);
            _editor.Canvas.ConnectionDeleted += new EventHandler<ConnectionEventArgs>(OnCanvasConnectionDeleted);
            _editor.Canvas.ZoneClicked += new EventHandler<ZoneClickEventArgs>(OnZoneClicked);

            // === 画布鼠标/键盘事件 ===
            _editor.Canvas.MouseDown += new MouseEventHandler(OnCanvasMouseDown);
            _editor.Canvas.MouseUp += new MouseEventHandler(OnCanvasMouseUp);
            _editor.Canvas.MouseMove += new MouseEventHandler(OnCanvasMouseMove);
            _editor.Canvas.MouseClick += new MouseEventHandler(OnCanvasMouseClick);
            _editor.Canvas.MouseDoubleClick += new MouseEventHandler(OnCanvasMouseDoubleClick);
            _editor.Canvas.MouseWheel += new MouseEventHandler(OnCanvasMouseWheel);
            _editor.Canvas.KeyDown += new KeyEventHandler(OnCanvasKeyDown);
            _editor.Canvas.KeyUp += new KeyEventHandler(OnCanvasKeyUp);
            _editor.Canvas.KeyPress += new KeyPressEventHandler(OnCanvasKeyPress);

            // === 工具箱事件 ===
            _editor.Toolbox.ToolboxChanged += new EventHandler(OnToolboxChanged);

            // === 工具栏按钮事件（通过反射追踪 Click）===
            HookToolStripEvents(_editor.ToolStrip);

            // === Tier 2 增量变更事件 ===
            _editor.ShapeChanged += new EventHandler<ChangeSet>(OnEditorShapeChanged);
            _editor.ConnectionChanged += new EventHandler<ChangeSet>(OnEditorConnectionChanged);
            _editor.BatchChanged += new EventHandler<BatchChangeSet>(OnEditorBatchChanged);
            _editor.DocumentCommitted += new EventHandler<ChangeSet>(OnEditorDocumentCommitted);
        }

        /// <summary>通过反射遍历 ToolStrip 的所有按钮，订阅 Click 事件</summary>
        private void HookToolStripEvents(ToolStrip ts)
        {
            if (ts == null) return;
            foreach (ToolStripItem item in ts.Items)
            {
                if (item is ToolStripButton)
                {
                    ToolStripButton btn = (ToolStripButton)item;
                    btn.Click += new EventHandler(OnToolStripButtonClick);
                }
            }
        }

        private void OnToolStripButtonClick(object sender, EventArgs e)
        {
            ToolStripButton btn = sender as ToolStripButton;
            string name = (btn != null && btn.Text != null) ? btn.Text : "?";
            Log(FILTER_EDITOR, "工具栏点击: " + name);
        }

        #endregion

        #region 编辑器事件处理

        private void OnShapeAdded(object sender, ShapeEventArgs e)
        {
            Log(FILTER_EDITOR, string.Format(
                "[ShapeAdded] 图形={0}, 类型={1}", e.ShapeName, e.ShapeTypeName));
        }

        private void OnShapeDeleted(object sender, ShapeEventArgs e)
        {
            Log(FILTER_EDITOR, string.Format(
                "[ShapeDeleted] 图形={0}", e.ShapeName));
        }

        private void OnConnectionAdded(object sender, ConnectionEventArgs e)
        {
            Log(FILTER_EDITOR, string.Format(
                "[ConnectionAdded] {0} -> {1}", e.FromShapeName, e.ToShapeName));
        }

        private void OnConnectionDeleted(object sender, ConnectionEventArgs e)
        {
            Log(FILTER_EDITOR, string.Format(
                "[ConnectionDeleted] {0} -> {1}", e.FromShapeName, e.ToShapeName));
        }

        private void OnShapeStateChanged(object sender, ShapeStateChangedEventArgs e)
        {
            Log(FILTER_EDITOR, string.Format(
                "[ShapeStateChanged] {0}: {1} -> {2}",
                e.Shape.Name, e.OldStateName, e.NewStateName));
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            Log(FILTER_EDITOR, "[ThemeChanged] 主题已切换");
        }

        #endregion

        #region Tier 2 增量变更事件处理

        /// <summary>
        /// 将 Guid 压缩为前 8 位十六进制字符串，便于在日志中紧凑显示。
        /// </summary>
        private static string ShortGuid(Guid id)
        {
            if (id == Guid.Empty)
                return "(空)";
            string s = id.ToString("N");
            return s.Length >= 8 ? s.Substring(0, 8) : s;
        }

        /// <summary>
        /// 将 ChangeSet 格式化为紧凑日志文本。
        /// 包含 Kind、ShapeId(短)、PropertyName、OldValue → NewValue。
        /// </summary>
        private static string FormatChangeSet(ChangeSet cs)
        {
            if (cs == null) return "(null)";

            StringBuilder sb = new StringBuilder();
            sb.Append("Kind=").Append(cs.Kind.ToString());

            if (cs.Kind == ChangeKind.ShapePropertyChanged ||
                cs.Kind == ChangeKind.ShapeAdded ||
                cs.Kind == ChangeKind.ShapeRemoved)
            {
                sb.Append(", Shape=").Append(ShortGuid(cs.ShapeId));
            }
            else if (cs.Kind == ChangeKind.ConnectionAdded ||
                     cs.Kind == ChangeKind.ConnectionRemoved)
            {
                sb.Append(", Conn=").Append(ShortGuid(cs.ShapeId));
            }

            if (!string.IsNullOrEmpty(cs.PropertyName))
            {
                sb.Append(", Prop=").Append(cs.PropertyName);
            }

            if (cs.OldValue != null || cs.NewValue != null)
            {
                sb.Append(": ")
                  .Append(cs.OldValue == null ? "(null)" : cs.OldValue.ToString())
                  .Append(" → ")
                  .Append(cs.NewValue == null ? "(null)" : cs.NewValue.ToString());
            }

            return sb.ToString();
        }

        private void OnEditorShapeChanged(object sender, ChangeSet e)
        {
            Log(FILTER_CHANGE, "[ShapeChanged] " + FormatChangeSet(e));
        }

        private void OnEditorConnectionChanged(object sender, ChangeSet e)
        {
            Log(FILTER_CHANGE, "[ConnectionChanged] " + FormatChangeSet(e));
        }

        private void OnEditorBatchChanged(object sender, BatchChangeSet e)
        {
            int count = (e != null && e.Changes != null) ? e.Changes.Count : 0;
            string agg = (e != null) ? e.AggregateKind.ToString() : "?";
            Log(FILTER_CHANGE, string.Format(
                "[BatchChanged] 共 {0} 条变更, 聚合类型={1}", count, agg));
        }

        private void OnEditorDocumentCommitted(object sender, ChangeSet e)
        {
            Log(FILTER_CHANGE, "[DocumentCommitted] 文档已提交（可持久化）");
        }

        #endregion

        #region 画布事件处理

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            List<ShapeBase> shapes = _editor.Document.GetSelectedShapes();
            List<Connection> conns = _editor.Document.GetSelectedConnections();
            Log(FILTER_CANVAS, string.Format(
                "[SelectionChanged] 选中 {0} 个图形, {1} 条连线",
                shapes.Count, conns.Count));
        }

        private void OnDocumentModified(object sender, EventArgs e)
        {
            Log(FILTER_CANVAS, "[DocumentModified] 文档已修改");
        }

        private void OnCanvasShapeAdded(object sender, ShapeEventArgs e)
        {
            Log(FILTER_CANVAS, string.Format(
                "[Canvas.ShapeAdded] 图形={0}, 类型={1}", e.ShapeName, e.ShapeTypeName));
        }

        private void OnCanvasShapeDeleted(object sender, ShapeEventArgs e)
        {
            Log(FILTER_CANVAS, string.Format(
                "[Canvas.ShapeDeleted] 图形={0}", e.ShapeName));
        }

        private void OnCanvasConnectionAdded(object sender, ConnectionEventArgs e)
        {
            Log(FILTER_CANVAS, string.Format(
                "[Canvas.ConnectionAdded] {0} -> {1}", e.FromShapeName, e.ToShapeName));
        }

        private void OnCanvasConnectionDeleted(object sender, ConnectionEventArgs e)
        {
            Log(FILTER_CANVAS, string.Format(
                "[Canvas.ConnectionDeleted] {0} -> {1}", e.FromShapeName, e.ToShapeName));
        }

        private void OnZoneClicked(object sender, ZoneClickEventArgs e)
        {
            string zoneName = (e.Zone != null) ? e.Zone.Name : "?";
            string shapeName = (e.Shape != null) ? e.Shape.Name : "?";
            Log(FILTER_CANVAS, string.Format(
                "[ZoneClicked] 图形={0}, 区域={1}", shapeName, zoneName));
        }

        private void OnToolboxChanged(object sender, EventArgs e)
        {
            Log(FILTER_EDITOR, "[ToolboxChanged] 工具箱已变更");
        }

        #endregion

        #region 鼠标/键盘事件处理

        private void OnCanvasMouseDown(object sender, MouseEventArgs e)
        {
            Log(FILTER_MOUSE, string.Format(
                "[MouseDown] 按钮={0}, 坐标=({1},{2}), 点击={3}",
                e.Button, e.X, e.Y, e.Clicks));
        }

        private void OnCanvasMouseUp(object sender, MouseEventArgs e)
        {
            Log(FILTER_MOUSE, string.Format(
                "[MouseUp] 按钮={0}, 坐标=({1},{2})",
                e.Button, e.X, e.Y));
        }

        // 鼠标移动事件频率太高，仅在调试需要时开启
        private bool _logMouseMove = false;

        /// <summary>是否记录鼠标移动事件（频率高，默认关闭）</summary>
        public bool LogMouseMove
        {
            get { return _logMouseMove; }
            set { _logMouseMove = value; }
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (!_logMouseMove) return;
            Log(FILTER_MOUSE, string.Format(
                "[MouseMove] 坐标=({0},{1})", e.X, e.Y));
        }

        private void OnCanvasMouseClick(object sender, MouseEventArgs e)
        {
            Log(FILTER_MOUSE, string.Format(
                "[MouseClick] 按钮={0}, 坐标=({1},{2})",
                e.Button, e.X, e.Y));
        }

        private void OnCanvasMouseDoubleClick(object sender, MouseEventArgs e)
        {
            Log(FILTER_MOUSE, string.Format(
                "[MouseDoubleClick] 按钮={0}, 坐标=({1},{2})",
                e.Button, e.X, e.Y));
        }

        private void OnCanvasMouseWheel(object sender, MouseEventArgs e)
        {
            Log(FILTER_MOUSE, string.Format(
                "[MouseWheel] 滚轮={0}, 坐标=({1},{2})",
                e.Delta, e.X, e.Y));
        }

        private void OnCanvasKeyDown(object sender, KeyEventArgs e)
        {
            Log(FILTER_KEY, string.Format(
                "[KeyDown] 键={0}({1}), 修饰={2}",
                e.KeyCode, (int)e.KeyCode, e.Modifiers));
        }

        private void OnCanvasKeyUp(object sender, KeyEventArgs e)
        {
            Log(FILTER_KEY, string.Format(
                "[KeyUp] 键={0}({1})", e.KeyCode, (int)e.KeyCode));
        }

        private void OnCanvasKeyPress(object sender, KeyPressEventArgs e)
        {
            Log(FILTER_KEY, string.Format(
                "[KeyPress] 字符='{0}' (0x{1:X2})",
                e.KeyChar, (int)e.KeyChar));
        }

        #endregion

        #region 日志记录

        private void Log(string category, string text)
        {
            if (_paused) return;

            string ts = DateTime.Now.ToString("HH:mm:ss.fff");
            LogEntry entry = new LogEntry();
            entry.TimeStamp = ts;
            entry.Category = category;
            entry.Text = text;

            _allEntries.Add(entry);

            // 超出上限时移除最早的条目
            if (_allEntries.Count > _maxLogLines)
                _allEntries.RemoveAt(0);

            // 暂存到待刷新列表，批量更新 UI 避免高频事件导致卡顿
            _pendingEntries.Add(entry);
            _eventCount++;

            if (!_logFlushScheduled)
            {
                _logFlushScheduled = true;
                // 延迟 50ms 批量刷新，合并多次事件为一次 UI 更新
                _logFlushTimer.Start();
            }
        }

        /// <summary>定时器回调：批量将待刷新条目写入 ListBox</summary>
        private void OnLogFlushTimerTick(object sender, EventArgs e)
        {
            _logFlushTimer.Stop();
            _logFlushScheduled = false;

            if (_pendingEntries.Count == 0)
                return;

            _eventLog.BeginUpdate();

            int added = 0;
            foreach (LogEntry entry in _pendingEntries)
            {
                if (MatchesFilter(entry.Category))
                {
                    string line = string.Format("[{0}] {1}", entry.TimeStamp, entry.Text);
                    _eventLog.Items.Add(line);
                    added++;
                }
            }
            _pendingEntries.Clear();

            // 保持列表不超过上限
            while (_eventLog.Items.Count > _maxLogLines)
                _eventLog.Items.RemoveAt(0);

            if (added > 0)
                _eventLog.SelectedIndex = _eventLog.Items.Count - 1;

            _eventLog.EndUpdate();
            _lblCount.Text = "事件: " + _eventCount.ToString();
        }

        private bool MatchesFilter(string category)
        {
            if (_currentFilter == FILTER_ALL) return true;
            return category == _currentFilter;
        }

        private void AppendToLogList(string ts, string category, string text)
        {
            string line = string.Format("[{0}] {1}", ts, text);
            _eventLog.Items.Add(line);
            _eventLog.SelectedIndex = _eventLog.Items.Count - 1;
        }

        private void RebuildFilteredLog()
        {
            _eventLog.BeginUpdate();
            _eventLog.Items.Clear();
            foreach (LogEntry entry in _allEntries)
            {
                if (MatchesFilter(entry.Category))
                {
                    string line = string.Format("[{0}] {1}", entry.TimeStamp, entry.Text);
                    _eventLog.Items.Add(line);
                }
            }
            if (_eventLog.Items.Count > 0)
                _eventLog.SelectedIndex = _eventLog.Items.Count - 1;
            _eventLog.EndUpdate();
        }

        private void OnClearLog(object sender, EventArgs e)
        {
            _allEntries.Clear();
            _pendingEntries.Clear();
            _eventLog.Items.Clear();
            _eventCount = 0;
            _lblCount.Text = "事件: 0";
        }

        private void OnPauseToggle(object sender, EventArgs e)
        {
            _paused = _btnPause.Checked;
            _btnPause.Text = _paused ? "恢复" : "暂停";
        }

        private void OnFilterChanged(object sender, EventArgs e)
        {
            _currentFilter = (string)_cmbFilter.SelectedItem;
            RebuildFilteredLog();
        }

        #endregion

        #region 编辑器状态轮询

        /// <summary>
        /// 创建一个包装对象，实时反映编辑器的关键状态属性。
        /// </summary>
        private class EditorStateSnapshot
        {
            private DiagramEditor _editor;

            public EditorStateSnapshot(DiagramEditor editor) { _editor = editor; }

            [System.ComponentModel.Category("画布")]
            public float Zoom
            {
                get { return _editor.Canvas.Zoom; }
            }

            [System.ComponentModel.Category("画布")]
            public PointF Offset
            {
                get { return _editor.Canvas.Offset; }
            }

            [System.ComponentModel.Category("画布")]
            public string CurrentTool
            {
                get { return _editor.Canvas.CurrentTool.ToString(); }
            }

            [System.ComponentModel.Category("画布")]
            public int ShapeCount
            {
                get { return _editor.Document.Shapes.Count; }
            }

            [System.ComponentModel.Category("画布")]
            public int ConnectionCount
            {
                get { return _editor.Document.Connections.Count; }
            }

            [System.ComponentModel.Category("画布")]
            public int SelectedShapeCount
            {
                get { return _editor.Document.GetSelectedShapes().Count; }
            }

            [System.ComponentModel.Category("画布")]
            public int SelectedConnectionCount
            {
                get { return _editor.Document.GetSelectedConnections().Count; }
            }

            [System.ComponentModel.Category("画布")]
            public string DesignMode
            {
                get
                {
                    if (_editor.Canvas == null || _editor.Canvas.Config == null)
                        return "?";
                    return _editor.Canvas.Config.DesignMode ? "是" : "否";
                }
            }

            [System.ComponentModel.Category("画布")]
            public string Theme
            {
                get { return _editor.Theme.ToString(); }
            }

            [System.ComponentModel.Category("面板可见性")]
            public bool ShowToolbar
            {
                get { return _editor.ShowToolbar; }
            }

            [System.ComponentModel.Category("面板可见性")]
            public bool ShowToolboxPanel
            {
                get { return _editor.ShowToolboxPanel; }
            }

            [System.ComponentModel.Category("面板可见性")]
            public bool ShowStatusBar
            {
                get { return _editor.ShowStatusBar; }
            }

            [System.ComponentModel.Category("面板可见性")]
            public bool ShowPropertyPanel
            {
                get { return _editor.ShowPropertyPanel; }
            }

            [System.ComponentModel.Category("面板可见性")]
            public bool ShowContextMenu
            {
                get { return _editor.ShowContextMenu; }
            }

            [System.ComponentModel.Category("面板可见性")]
            public bool ShowMenuStrip
            {
                get { return _editor.ShowMenuStrip; }
            }

            [System.ComponentModel.Category("面板可见性")]
            public bool ShowToolbarText
            {
                get { return _editor.ShowToolbarText; }
            }

            [System.ComponentModel.Category("注册表")]
            public int RegisteredShapeTypes
            {
                get { return ShapeTypeRegistry.Instance.GetAllTypes().Count; }
            }

            [System.ComponentModel.Category("增量变更")]
            public string IsApplyingExternalChange
            {
                get { return _editor.IsApplyingExternalChange ? "是（宿主外部变更中，事件回弹已抑制）" : "否"; }
            }
        }

        private void StartStatePolling()
        {
            _stateGrid.SelectedObject = new EditorStateSnapshot(_editor);

            _stateTimer = new Timer();
            _stateTimer.Interval = 500; // 每 500ms 刷新一次状态
            _stateTimer.Tick += new EventHandler(OnStateTimerTick);
            _stateTimer.Start();
        }

        private void OnStateTimerTick(object sender, EventArgs e)
        {
            if (this.IsDisposed || !this.IsHandleCreated)
                return;

            // 刷新属性网格以反映实时状态
            _stateGrid.Refresh();
        }

        #endregion

        #region 接口特性（反射）

        /// <summary>
        /// 通过反射加载 DiagramEditor、DrawingCanvas、ToolboxPanel 的
        /// 公共属性、方法和事件，展示控件接口全貌。
        /// </summary>
        private void LoadInterfaceInfo()
        {
            _interfaceTree.BeginUpdate();
            _interfaceTree.Nodes.Clear();

            // DiagramEditor 接口
            TreeNode editorNode = new TreeNode("DiagramEditor (编辑器控件)");
            AddMemberNodes(editorNode, typeof(DiagramEditor));
            _interfaceTree.Nodes.Add(editorNode);

            // DrawingCanvas 接口
            TreeNode canvasNode = new TreeNode("DrawingCanvas (画布控件)");
            AddMemberNodes(canvasNode, typeof(DrawingCanvas));
            _interfaceTree.Nodes.Add(canvasNode);

            // ToolboxPanel 接口
            TreeNode toolboxNode = new TreeNode("ToolboxPanel (工具箱面板)");
            AddMemberNodes(toolboxNode, typeof(ToolboxPanel));
            _interfaceTree.Nodes.Add(toolboxNode);

            // ShapeBase 接口
            TreeNode shapeNode = new TreeNode("ShapeBase (图形基类)");
            AddMemberNodes(shapeNode, typeof(ShapeBase));
            _interfaceTree.Nodes.Add(shapeNode);

            // GenericShape 接口
            TreeNode genShapeNode = new TreeNode("GenericShape (通用图形)");
            AddMemberNodes(genShapeNode, typeof(GenericShape));
            _interfaceTree.Nodes.Add(genShapeNode);

            // DrawingDocument 接口
            TreeNode docNode = new TreeNode("DrawingDocument (画布文档)");
            AddMemberNodes(docNode, typeof(DrawingDocument));
            _interfaceTree.Nodes.Add(docNode);

            // Connection 接口
            TreeNode connNode = new TreeNode("Connection (连线)");
            AddMemberNodes(connNode, typeof(Connection));
            _interfaceTree.Nodes.Add(connNode);

            // ShapeType 接口
            TreeNode stNode = new TreeNode("ShapeType (图形类型定义)");
            AddMemberNodes(stNode, typeof(ShapeType));
            _interfaceTree.Nodes.Add(stNode);

            foreach (TreeNode node in _interfaceTree.Nodes)
                node.Expand();

            _interfaceTree.EndUpdate();
        }

        /// <summary>为指定类型添加属性、方法、事件子节点</summary>
        private void AddMemberNodes(TreeNode parent, Type type)
        {
            // 公共属性
            TreeNode propsNode = new TreeNode("公共属性");
            PropertyInfo[] props = type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo pi in props)
            {
                string text = string.Format("{0} : {1}", pi.Name, GetFriendlyTypeName(pi.PropertyType));
                if (pi.CanRead && !pi.CanWrite)
                    text += " (只读)";
                propsNode.Nodes.Add(text);
            }
            if (propsNode.Nodes.Count == 0)
                propsNode.Nodes.Add("(无)");

            // 公共方法（排除 Object 基类成员和 get_/set_ 访问器）
            TreeNode methodsNode = new TreeNode("公共方法");
            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance);
            List<string> methodNames = new List<string>();
            foreach (MethodInfo mi in methods)
            {
                // 跳过属性访问器和 Object 基类方法
                if (mi.IsSpecialName) continue;
                if (mi.DeclaringType == typeof(object)) continue;

                StringBuilder sb = new StringBuilder();
                sb.Append(mi.Name);
                sb.Append("(");
                ParameterInfo[] pms = mi.GetParameters();
                for (int i = 0; i < pms.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(GetFriendlyTypeName(pms[i].ParameterType));
                    sb.Append(" ");
                    sb.Append(pms[i].Name);
                }
                sb.Append(")");
                if (mi.ReturnType != typeof(void))
                    sb.Append(" : ").Append(GetFriendlyTypeName(mi.ReturnType));
                methodNames.Add(sb.ToString());
            }
            methodNames.Sort();
            foreach (string m in methodNames)
                methodsNode.Nodes.Add(m);
            if (methodsNode.Nodes.Count == 0)
                methodsNode.Nodes.Add("(无)");

            // 公共事件
            TreeNode eventsNode = new TreeNode("公共事件");
            EventInfo[] events = type.GetEvents(
                BindingFlags.Public | BindingFlags.Instance);
            foreach (EventInfo ei in events)
            {
                string text = string.Format("{0} : {1}",
                    ei.Name, GetFriendlyTypeName(ei.EventHandlerType));
                eventsNode.Nodes.Add(text);
            }
            if (eventsNode.Nodes.Count == 0)
                eventsNode.Nodes.Add("(无)");

            parent.Nodes.Add(propsNode);
            parent.Nodes.Add(methodsNode);
            parent.Nodes.Add(eventsNode);
        }

        /// <summary>获取类型的友好显示名</summary>
        private string GetFriendlyTypeName(Type type)
        {
            if (type == null) return "?";

            // 处理泛型
            if (type.IsGenericType)
            {
                string name = type.Name;
                int tickIdx = name.IndexOf('`');
                if (tickIdx > 0)
                    name = name.Substring(0, tickIdx);
                Type[] args = type.GetGenericArguments();
                StringBuilder sb = new StringBuilder();
                sb.Append(name);
                sb.Append("<");
                for (int i = 0; i < args.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(GetFriendlyTypeName(args[i]));
                }
                sb.Append(">");
                return sb.ToString();
            }

            // 处理数组
            if (type.IsArray)
            {
                return GetFriendlyTypeName(type.GetElementType()) + "[]";
            }

            // 基本类型别名
            switch (type.Name)
            {
                case "Boolean": return "bool";
                case "Int32": return "int";
                case "Int64": return "long";
                case "Single": return "float";
                case "Double": return "double";
                case "String": return "string";
                case "Void": return "void";
                case "Byte": return "byte";
                case "Char": return "char";
                case "Object": return "object";
                default: return type.Name;
            }
        }

        #endregion

        #region 窗体生命周期

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 停止定时器
            if (_stateTimer != null)
            {
                _stateTimer.Stop();
                _stateTimer.Dispose();
                _stateTimer = null;
            }

            // 取消所有事件订阅，防止窗体关闭后仍持有编辑器引用
            UnsubscribeEvents();

            base.OnFormClosing(e);
        }

        private void UnsubscribeEvents()
        {
            try
            {
                _editor.ShapeAdded -= new EventHandler<ShapeEventArgs>(OnShapeAdded);
                _editor.ShapeDeleted -= new EventHandler<ShapeEventArgs>(OnShapeDeleted);
                _editor.ConnectionAdded -= new EventHandler<ConnectionEventArgs>(OnConnectionAdded);
                _editor.ConnectionDeleted -= new EventHandler<ConnectionEventArgs>(OnConnectionDeleted);
                _editor.ShapeStateChanged -= new EventHandler<ShapeStateChangedEventArgs>(OnShapeStateChanged);
                _editor.ThemeChanged -= new EventHandler(OnThemeChanged);

                _editor.Canvas.SelectionChanged -= new EventHandler(OnSelectionChanged);
                _editor.Canvas.DocumentModified -= new EventHandler(OnDocumentModified);
                _editor.Canvas.ShapeAdded -= new EventHandler<ShapeEventArgs>(OnCanvasShapeAdded);
                _editor.Canvas.ShapeDeleted -= new EventHandler<ShapeEventArgs>(OnCanvasShapeDeleted);
                _editor.Canvas.ConnectionAdded -= new EventHandler<ConnectionEventArgs>(OnCanvasConnectionAdded);
                _editor.Canvas.ConnectionDeleted -= new EventHandler<ConnectionEventArgs>(OnCanvasConnectionDeleted);
                _editor.Canvas.ZoneClicked -= new EventHandler<ZoneClickEventArgs>(OnZoneClicked);

                _editor.Canvas.MouseDown -= new MouseEventHandler(OnCanvasMouseDown);
                _editor.Canvas.MouseUp -= new MouseEventHandler(OnCanvasMouseUp);
                _editor.Canvas.MouseMove -= new MouseEventHandler(OnCanvasMouseMove);
                _editor.Canvas.MouseClick -= new MouseEventHandler(OnCanvasMouseClick);
                _editor.Canvas.MouseDoubleClick -= new MouseEventHandler(OnCanvasMouseDoubleClick);
                _editor.Canvas.MouseWheel -= new MouseEventHandler(OnCanvasMouseWheel);
                _editor.Canvas.KeyDown -= new KeyEventHandler(OnCanvasKeyDown);
                _editor.Canvas.KeyUp -= new KeyEventHandler(OnCanvasKeyUp);
                _editor.Canvas.KeyPress -= new KeyPressEventHandler(OnCanvasKeyPress);

                _editor.Toolbox.ToolboxChanged -= new EventHandler(OnToolboxChanged);

                // Tier 2 增量变更事件
                _editor.ShapeChanged -= new EventHandler<ChangeSet>(OnEditorShapeChanged);
                _editor.ConnectionChanged -= new EventHandler<ChangeSet>(OnEditorConnectionChanged);
                _editor.BatchChanged -= new EventHandler<BatchChangeSet>(OnEditorBatchChanged);
                _editor.DocumentCommitted -= new EventHandler<ChangeSet>(OnEditorDocumentCommitted);
            }
            catch
            {
                // 控件可能已释放，忽略
            }
        }

        #endregion
    }
}
