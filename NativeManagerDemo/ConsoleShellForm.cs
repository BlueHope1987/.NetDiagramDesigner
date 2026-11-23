using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DiagramDesigner.Config;
using DiagramDesigner.Controls;
using DiagramDesigner.Core;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    /// <summary>MMC 风格三窗格控制台；集群事实、设计事实与视觉布局分别管理。</summary>
    public class ConsoleShellForm : Form
    {
        private DiagramEditor _editor;
        private CommandConsoleControl _console;
        private MmcActionsPane _ops;
        private TopologyLegendControl _legend;

        private SplitContainer _splitMain;
        private SplitContainer _splitInner;
        private MenuStrip _menu;
        private ToolStrip _toolbar;
        private StatusStrip _status;
        private ToolStripStatusLabel _lblConn;
        private ToolStripStatusLabel _lblCount;
        private ToolStripStatusLabel _lblSel;
        private ToolStripStatusLabel _lblZoom;
        private ToolStripStatusLabel _lblTools;
        private ToolStripStatusLabel _lblEndpoint;

        private EditorTopologyHost _editorHost;
        private ITopologyHost _host;
        private TopologyProjector _projector;
        private TopologyLayoutStore _layoutStore;
        private DesignTopologyStore _design;
        private CommandCoordinator _coordinator;
        private CanvasIntentController _canvasIntents;
        private string _currentFile = "";

        public ConsoleShellForm()
        {
            this.Text = "云原生命令与图形管理器 - 候选版";
            this.Size = new Size(1440, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = SystemIcons.Application;

            _editor = new DiagramEditor();
            _editor.Dock = DockStyle.Fill;
            _console = new CommandConsoleControl();
            _console.Dock = DockStyle.Fill;
            _ops = new MmcActionsPane();
            _ops.Dock = DockStyle.Fill;
            _legend = new TopologyLegendControl();
            _legend.Dock = DockStyle.Bottom;
            _legend.Height = 178;

            BuildMenu();
            BuildToolbar();
            BuildStatus();
            BuildLayout();
            ConfigureEditor();
            RegisterShapeTypes();

            _editorHost = new EditorTopologyHost(_editor);
            _host = _editorHost;
            _projector = new TopologyProjector(_host);
            _design = new DesignTopologyStore();
            string layoutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Path.Combine("NativeManagerDemo", "topology-layout.json"));
            _layoutStore = new TopologyLayoutStore(layoutPath);
            try { _layoutStore.Load(); }
            catch (Exception ex) { _console.WriteLine("布局文件读取失败，将使用自动布局: " + ex.Message, LogKind.Error); }
            _projector.AttachLayoutStore(_layoutStore);
            _coordinator = new CommandCoordinator(_host, _projector, _design);
            _coordinator.ConfirmCommand = new CommandConfirmationHandler(ConfirmCommand);
            _canvasIntents = new CanvasIntentController(_editor, _editorHost, _projector, _design);
            _console.Attach(_coordinator);
            _ops.Attach(_projector);

            WireEditorEvents();
            WirePanelEvents();
            WireCoordinatorEvents();

            _projector.Render(_design.QueryTopology());
            ShowWelcome();
            RefreshAll();
        }

        private void BuildLayout()
        {
            _splitInner = new SplitContainer();
            _splitInner.Dock = DockStyle.Fill;
            _splitInner.Orientation = Orientation.Horizontal;
            _splitInner.SplitterWidth = 4;
            _splitInner.Size = new Size(1200, 780);
            _splitInner.Panel1MinSize = 200;
            _splitInner.Panel2MinSize = 140;
            _splitInner.Panel1.Controls.Add(_editor);
            _splitInner.Panel2.Controls.Add(_console);

            Panel right = new Panel();
            right.Dock = DockStyle.Fill;
            right.Controls.Add(_ops);
            right.Controls.Add(_legend);

            _splitMain = new SplitContainer();
            _splitMain.Dock = DockStyle.Fill;
            _splitMain.Orientation = Orientation.Vertical;
            _splitMain.SplitterWidth = 4;
            _splitMain.Size = new Size(1400, 800);
            _splitMain.Panel1MinSize = 300;
            _splitMain.Panel2MinSize = 280;
            _splitMain.Panel1.Controls.Add(_splitInner);
            _splitMain.Panel2.Controls.Add(right);

            this.Controls.Add(_splitMain);
            this.Controls.Add(_status);
            this.Controls.Add(_toolbar);
            this.Controls.Add(_menu);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplySplitterDistance(_splitMain, 0.72f, 320, 300);
            ApplySplitterDistance(_splitInner, 0.62f, 220, 160);
            try { this.BeginInvoke(new MethodInvoker(StartDetection)); }
            catch { StartDetection(); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_coordinator != null) _coordinator.Cancel();
            SaveLayoutQuietly();
            base.OnFormClosing(e);
        }

        /// <summary>保留真实尺寸下的安全夹取，避免 SplitterDistance 越界。</summary>
        private static void ApplySplitterDistance(SplitContainer sc, float ratio, int minPanel1, int minPanel2)
        {
            if (sc == null) return;
            bool vertical = (sc.Orientation == Orientation.Vertical);
            int total = vertical ? sc.Width : sc.Height;
            int lower = Math.Max(sc.Panel1MinSize, minPanel1);
            int upper = total - Math.Max(sc.Panel2MinSize, minPanel2) - sc.SplitterWidth;
            if (upper <= lower) return;
            int distance = (int)(total * ratio);
            if (distance < lower) distance = lower;
            if (distance > upper) distance = upper;
            sc.SplitterDistance = distance;
        }

        private void BuildMenu()
        {
            _menu = new MenuStrip();
            this.MainMenuStrip = _menu;

            ToolStripMenuItem console = new ToolStripMenuItem("控制台(&C)");
            console.DropDownItems.Add(new ToolStripMenuItem("检测工具", null, new EventHandler(OnDetectTools)));
            console.DropDownItems.Add(new ToolStripMenuItem("在线刷新", null, new EventHandler(OnOnlineRefresh)));
            console.DropDownItems.Add(new ToolStripMenuItem("进入设计·调试模式", null, new EventHandler(OnDesignMode)));
            console.DropDownItems.Add(new ToolStripMenuItem("进入在线模式", null, new EventHandler(OnOnlineMode)));
            console.DropDownItems.Add(new ToolStripSeparator());
            console.DropDownItems.Add(new ToolStripMenuItem("工具状态", null, new EventHandler(OnToolStatus)));
            console.DropDownItems.Add(new ToolStripMenuItem("安装指导", null, new EventHandler(OnInstallHelp)));
            console.DropDownItems.Add(new ToolStripMenuItem("取消当前命令", null, new EventHandler(OnCancelCommand)));
            console.DropDownItems.Add(new ToolStripSeparator());
            console.DropDownItems.Add(new ToolStripMenuItem("退出", null, new EventHandler(OnExit)));
            _menu.Items.Add(console);

            ToolStripMenuItem file = new ToolStripMenuItem("文件(&F)");
            file.DropDownItems.Add(new ToolStripMenuItem("新建设计", null, new EventHandler(OnFileNew)));
            file.DropDownItems.Add(new ToolStripMenuItem("打开设计 JSON...", null, new EventHandler(OnFileOpen), Keys.Control | Keys.O));
            file.DropDownItems.Add(new ToolStripMenuItem("保存设计", null, new EventHandler(OnFileSave), Keys.Control | Keys.S));
            file.DropDownItems.Add(new ToolStripMenuItem("设计另存为...", null, new EventHandler(OnFileSaveAs)));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(new ToolStripMenuItem("保存视觉布局", null, new EventHandler(OnSaveLayout)));
            _menu.Items.Add(file);

            ToolStripMenuItem view = new ToolStripMenuItem("视图(&V)");
            view.DropDownItems.Add(new ToolStripMenuItem("浅色主题", null, new EventHandler(OnThemeLight)));
            view.DropDownItems.Add(new ToolStripMenuItem("深色主题", null, new EventHandler(OnThemeDark)));
            view.DropDownItems.Add(new ToolStripSeparator());
            view.DropDownItems.Add(new ToolStripMenuItem("显示/隐藏工具箱", null, new EventHandler(OnToggleToolbox)));
            view.DropDownItems.Add(new ToolStripMenuItem("显示/隐藏编辑器工具栏", null, new EventHandler(OnToggleToolbar)));
            view.DropDownItems.Add(new ToolStripMenuItem("显示/隐藏编辑器状态栏", null, new EventHandler(OnToggleStatusBar)));
            view.DropDownItems.Add(new ToolStripMenuItem("显示/隐藏图例", null, new EventHandler(OnToggleLegend)));
            _menu.Items.Add(view);

            ToolStripMenuItem help = new ToolStripMenuItem("帮助(&H)");
            help.DropDownItems.Add(new ToolStripMenuItem("命令帮助", null, new EventHandler(OnCommandHelp)));
            help.DropDownItems.Add(new ToolStripMenuItem("工具箱与画布帮助", null, new EventHandler(OnCanvasHelp)));
            help.DropDownItems.Add(new ToolStripMenuItem("关于候选版", null, new EventHandler(OnAbout)));
            _menu.Items.Add(help);
        }

        private void BuildToolbar()
        {
            _toolbar = new ToolStrip();
            _toolbar.GripStyle = ToolStripGripStyle.Hidden;
            _toolbar.Items.Add(MakeToolButton("检测工具", new EventHandler(OnDetectTools)));
            _toolbar.Items.Add(MakeToolButton("在线刷新", new EventHandler(OnOnlineRefresh)));
            _toolbar.Items.Add(MakeToolButton("设计模式", new EventHandler(OnDesignMode)));
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(MakeToolButton("工具状态", new EventHandler(OnToolStatus)));
            _toolbar.Items.Add(MakeToolButton("保存布局", new EventHandler(OnSaveLayout)));
            _toolbar.Items.Add(MakeToolButton("删除设计对象", new EventHandler(OnToolDelete)));
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(MakeToolButton("放大", new EventHandler(OnToolZoomIn)));
            _toolbar.Items.Add(MakeToolButton("缩小", new EventHandler(OnToolZoomOut)));
            _toolbar.Items.Add(MakeToolButton("适应", new EventHandler(OnToolFit)));
        }

        private static ToolStripButton MakeToolButton(string text, EventHandler handler)
        {
            ToolStripButton button = new ToolStripButton(text);
            button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            button.Click += handler;
            return button;
        }

        private void BuildStatus()
        {
            _status = new StatusStrip();
            _lblConn = new ToolStripStatusLabel("模式: 检测中");
            _lblCount = new ToolStripStatusLabel("图形 0 · 连线 0");
            _lblSel = new ToolStripStatusLabel("选中 0");
            _lblZoom = new ToolStripStatusLabel("缩放 100%");
            _lblTools = new ToolStripStatusLabel("工具检测尚未开始");
            _lblTools.Spring = true;
            _lblTools.TextAlign = ContentAlignment.MiddleLeft;
            _lblEndpoint = new ToolStripStatusLabel("");
            _status.Items.Add(_lblConn);
            _status.Items.Add(new ToolStripStatusLabel("|"));
            _status.Items.Add(_lblCount);
            _status.Items.Add(new ToolStripStatusLabel("|"));
            _status.Items.Add(_lblSel);
            _status.Items.Add(new ToolStripStatusLabel("|"));
            _status.Items.Add(_lblZoom);
            _status.Items.Add(new ToolStripStatusLabel("|"));
            _status.Items.Add(_lblTools);
            _status.Items.Add(_lblEndpoint);
        }

        private void ConfigureEditor()
        {
            CanvasConfig cfg = new CanvasConfig();
            cfg.ShowToolbar = true;
            cfg.ShowToolboxPanel = true;
            cfg.ShowStatusBar = true;
            cfg.ShowPropertyPanel = false;
            cfg.ShowMenuStrip = false;
            cfg.ShowContextMenu = true;
            cfg.ShowToolbarText = true;
            cfg.Theme = "Light";
            cfg.DesignMode = true;
            _editor.ApplyCanvasConfig(cfg);
            _editor.Canvas.Config = cfg;
        }

        private void RegisterShapeTypes()
        {
            ShapeTypeRegistry.Instance.Clear();
            K8sShapes.RegisterAll();
            _editor.Toolbox.ReloadFromRegistry();
        }

        private void WireEditorEvents()
        {
            _editor.Canvas.SelectionChanged += new EventHandler(OnCanvasSelectionChanged);
            _editor.Canvas.DocumentModified += new EventHandler(OnCanvasDocumentModified);
            _canvasIntents.IntentHandled += new EventHandler<CanvasIntentEventArgs>(OnCanvasIntentHandled);
            _canvasIntents.Changed += new EventHandler(OnCanvasIntentChanged);
        }

        private void WirePanelEvents()
        {
            _console.CommandExecuted += new EventHandler(OnCommandExecuted);
            _console.CancelRequested += new EventHandler(OnCancelCommand);
            _ops.VerbInvoked += new EventHandler<VerbInvokedEventArgs>(OnVerbInvoked);
        }

        private void WireCoordinatorEvents()
        {
            _coordinator.OutputReceived += new EventHandler<CoordinatorOutputEventArgs>(OnCoordinatorOutput);
            _coordinator.BusyChanged += new EventHandler<CoordinatorBusyEventArgs>(OnCoordinatorBusy);
            _coordinator.ModeChanged += new EventHandler<ManagerModeEventArgs>(OnCoordinatorModeChanged);
            _coordinator.TopologyRefreshed += new EventHandler<TopologyRefreshEventArgs>(OnTopologyRefreshed);
            _coordinator.DesignChanged += new EventHandler(OnDesignChanged);
            _coordinator.DetectionCompleted += new EventHandler(OnDetectionCompleted);
        }

        private void StartDetection()
        {
            _console.WriteLine("正在检测工具路径、版本与集群连通性；检测期间可先查看本地示例。", LogKind.Hint);
            _coordinator.DetectToolsAsync();
        }

        private void OnCoordinatorOutput(object sender, CoordinatorOutputEventArgs e)
        {
            if (!MarshalEvent(new EventHandler<CoordinatorOutputEventArgs>(OnCoordinatorOutput), sender, e)) return;
            _console.WriteResult(e.Result);
            RefreshAll();
        }

        private void OnCoordinatorBusy(object sender, CoordinatorBusyEventArgs e)
        {
            if (!MarshalEvent(new EventHandler<CoordinatorBusyEventArgs>(OnCoordinatorBusy), sender, e)) return;
            _console.SetBusy(e.Busy);
            _lblTools.Text = e.Busy ? "命令或资源查询执行中；可点击取消" : ToolSummary();
        }

        private void OnCoordinatorModeChanged(object sender, ManagerModeEventArgs e)
        {
            if (!MarshalEvent(new EventHandler<ManagerModeEventArgs>(OnCoordinatorModeChanged), sender, e)) return;
            _canvasIntents.Mode = e.Mode;
            _ops.Mode = e.Mode;
            _lblConn.Text = "模式: " + ModeText(e.Mode);
            _lblConn.ToolTipText = e.Message;
            if (e.Mode == ManagerMode.DesignDebug)
            {
                _projector.Render(_design.QueryTopology());
            }
            else if (e.Mode == ManagerMode.Online)
            {
                TopologySnapshot snapshot = _coordinator.CurrentSnapshot;
                if (snapshot != null) _projector.Render(snapshot);
                else
                {
                    snapshot = new TopologySnapshot();
                    snapshot.Source = "cluster";
                    snapshot.ContextName = "正在读取集群快照";
                    _projector.Render(snapshot);
                }
            }
            RefreshAll();
        }

        private void OnTopologyRefreshed(object sender, TopologyRefreshEventArgs e)
        {
            if (!MarshalEvent(new EventHandler<TopologyRefreshEventArgs>(OnTopologyRefreshed), sender, e)) return;
            if (_coordinator.Mode == ManagerMode.Online && e.Snapshot != null)
            {
                _projector.Render(e.Snapshot);
                int i;
                for (i = 0; i < e.Snapshot.Warnings.Count; i++)
                    _console.WriteLine("快照警告: " + e.Snapshot.Warnings[i], LogKind.Hint);
            }
            RefreshAll();
        }

        private void OnDesignChanged(object sender, EventArgs e)
        {
            if (!MarshalEvent(new EventHandler(OnDesignChanged), sender, e)) return;
            if (_coordinator.Mode == ManagerMode.DesignDebug) _projector.Render(_design.QueryTopology());
            RefreshAll();
        }

        private void OnDetectionCompleted(object sender, EventArgs e)
        {
            if (!MarshalEvent(new EventHandler(OnDetectionCompleted), sender, e)) return;
            RefreshAll();
        }

        /// <summary>后台事件统一封送回 UI；窗体关闭后静默丢弃迟到事件。</summary>
        private bool MarshalEvent(Delegate handler, object sender, EventArgs e)
        {
            if (this.IsDisposed || this.Disposing) return false;
            if (!this.InvokeRequired) return true;
            try { this.BeginInvoke(handler, new object[] { sender, e }); }
            catch { }
            return false;
        }

        private bool ConfirmCommand(string commandText, string reason)
        {
            if (this.InvokeRequired)
            {
                try { return (bool)this.Invoke(new CommandConfirmationHandler(ConfirmCommand), new object[] { commandText, reason }); }
                catch { return false; }
            }
            DialogResult result = MessageBox.Show(this,
                reason + "\r\n\r\n只有点击“是”才会继续；取消或关闭此对话框均不会启动进程。",
                "确认受控操作", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            return result == DialogResult.Yes;
        }

        private void OnCanvasSelectionChanged(object sender, EventArgs e)
        {
            _ops.RefreshFromSelection();
            RefreshStatus();
        }

        private void OnCanvasDocumentModified(object sender, EventArgs e) { RefreshStatus(); }

        private void OnCanvasIntentHandled(object sender, CanvasIntentEventArgs e)
        {
            _console.WriteLine(e.Message, e.Success ? LogKind.Ok : LogKind.Error);
        }

        private void OnCanvasIntentChanged(object sender, EventArgs e) { RefreshAll(); }
        private void OnCommandExecuted(object sender, EventArgs e) { RefreshAll(); }

        private void OnVerbInvoked(object sender, VerbInvokedEventArgs e)
        {
            string shown = _coordinator.Mode == ManagerMode.Online ? e.CommandText : "design " + e.Verb.Verb + " " + e.Entity.Name;
            _console.WriteLine("> " + shown, LogKind.Echo);
            CommandResult result = _coordinator.ExecuteVerb(e);
            _console.WriteResult(result);
            RefreshAll();
        }

        private void OnDetectTools(object sender, EventArgs e) { RunCommand("detect"); }
        private void OnOnlineRefresh(object sender, EventArgs e) { RunCommand("refresh"); }
        private void OnDesignMode(object sender, EventArgs e) { RunCommand("mode design"); }
        private void OnOnlineMode(object sender, EventArgs e) { RunCommand("mode online"); }
        private void OnToolStatus(object sender, EventArgs e) { RunCommand("tools"); }
        private void OnInstallHelp(object sender, EventArgs e) { RunCommand("install-help"); }
        private void OnCancelCommand(object sender, EventArgs e) { if (_coordinator != null) _coordinator.Cancel(); }
        private void OnToolDelete(object sender, EventArgs e) { RunCommand("delete"); }
        private void OnToolZoomIn(object sender, EventArgs e) { RunCommand("zoom in"); }
        private void OnToolZoomOut(object sender, EventArgs e) { RunCommand("zoom out"); }
        private void OnToolFit(object sender, EventArgs e) { RunCommand("fit"); }

        private void RunCommand(string command)
        {
            _console.WriteLine("> " + command, LogKind.Echo);
            CommandResult result = _coordinator.Execute(command);
            _console.WriteResult(result);
            RefreshAll();
        }

        private void OnFileNew(object sender, EventArgs e)
        {
            CommandResult result = _coordinator.Execute("new");
            _console.WriteLine("> new", LogKind.Echo);
            _console.WriteResult(result);
            if (result.Success) { _currentFile = ""; UpdateTitle(); }
            RefreshAll();
        }

        private void OnFileOpen(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "云原生设计 JSON (*.json)|*.json|所有文件 (*.*)|*.*";
                dialog.DefaultExt = "json";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string path = dialog.FileName;
                CommandResult result = _coordinator.Execute("load " + QuoteArgument(path));
                _console.WriteLine("> load " + QuoteArgument(path), LogKind.Echo);
                _console.WriteResult(result);
                if (result.Success) { _currentFile = path; UpdateTitle(); }
                RefreshAll();
            }
        }

        private void OnFileSave(object sender, EventArgs e)
        {
            if (_coordinator.Mode != ManagerMode.DesignDebug)
            {
                _console.WriteLine("保存设计文件请先切换到设计·调试模式；在线快照不会被改写为设计事实。", LogKind.Error);
                return;
            }
            if (_currentFile.Length == 0) { OnFileSaveAs(sender, e); return; }
            SaveTo(_currentFile);
        }

        private void OnFileSaveAs(object sender, EventArgs e)
        {
            if (_coordinator.Mode != ManagerMode.DesignDebug) { OnDesignMode(sender, e); return; }
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "云原生设计 JSON (*.json)|*.json|所有文件 (*.*)|*.*";
                dialog.DefaultExt = "json";
                if (_currentFile.Length > 0) dialog.FileName = _currentFile;
                else dialog.FileName = "cloud-native-design.json";
                if (dialog.ShowDialog(this) == DialogResult.OK) SaveTo(dialog.FileName);
            }
        }

        private void SaveTo(string path)
        {
            string command = "save " + QuoteArgument(path);
            _console.WriteLine("> " + command, LogKind.Echo);
            CommandResult result = _coordinator.Execute(command);
            _console.WriteResult(result);
            if (result.Success) { _currentFile = path; UpdateTitle(); }
            RefreshAll();
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value == null ? "" : value.Replace("\\", "\\\\").Replace("\"", "\\\"")) + "\"";
        }

        private void OnSaveLayout(object sender, EventArgs e)
        {
            try
            {
                _projector.SaveLayout();
                _console.WriteLine("已保存资源 Id 对应的坐标、容器尺寸、缩放和视口。", LogKind.Ok);
            }
            catch (Exception ex) { _console.WriteLine("布局保存失败: " + ex.Message, LogKind.Error); }
        }

        private void SaveLayoutQuietly()
        {
            try { if (_projector != null) _projector.SaveLayout(); }
            catch { }
        }

        private void UpdateTitle()
        {
            this.Text = "云原生命令与图形管理器 - 候选版" +
                (_currentFile.Length == 0 ? "" : " - " + Path.GetFileName(_currentFile));
        }

        private void OnThemeLight(object sender, EventArgs e) { SetTheme(false); }
        private void OnThemeDark(object sender, EventArgs e) { SetTheme(true); }

        private void SetTheme(bool dark)
        {
            EditorTheme theme = dark ? EditorTheme.Dark : EditorTheme.Light;
            GlobalConfig.Instance.Theme = theme;
            _editor.Theme = theme;
            CanvasConfig cfg = _editor.Canvas.Config;
            cfg.Theme = dark ? "Dark" : "Light";
            _editor.ApplyCanvasConfig(cfg);
            _editor.ApplyTheme();
            TopologySnapshot snapshot = _projector.Current;
            if (snapshot != null) _projector.Render(snapshot);
            _console.WriteLine("主题已切换为: " + (dark ? "深色" : "浅色"), LogKind.Hint);
            RefreshAll();
        }

        private void OnToggleToolbox(object sender, EventArgs e)
        {
            CanvasConfig cfg = _editor.Canvas.Config;
            cfg.ShowToolboxPanel = !cfg.ShowToolboxPanel;
            _editor.ApplyCanvasConfig(cfg);
        }

        private void OnToggleToolbar(object sender, EventArgs e)
        {
            CanvasConfig cfg = _editor.Canvas.Config;
            cfg.ShowToolbar = !cfg.ShowToolbar;
            _editor.ApplyCanvasConfig(cfg);
        }

        private void OnToggleStatusBar(object sender, EventArgs e)
        {
            CanvasConfig cfg = _editor.Canvas.Config;
            cfg.ShowStatusBar = !cfg.ShowStatusBar;
            _editor.ApplyCanvasConfig(cfg);
        }

        private void OnToggleLegend(object sender, EventArgs e) { _legend.Visible = !_legend.Visible; }

        private void OnCommandHelp(object sender, EventArgs e)
        {
            _console.WriteLine(_coordinator.Execute("help").Text, LogKind.Info);
            _console.FocusInput();
        }

        private void OnCanvasHelp(object sender, EventArgs e)
        {
            _console.WriteLine("工具箱按 K8s 核心、工作负载、网络与配置、Helm、Istio、设计辅助分组；名称采用中文/英文并附用途说明。\r\n" +
                "设计模式下，从工具箱拖入画布即可新增本地资源。Namespace 是唯一容器：namespaced 资源需完整放入容器，跨 Namespace 拖动会校验同名冲突。\r\n" +
                "连线表达拥有、选择、路由、读取、依赖或 Helm/Istio 管理关系，不等于任意容器嵌套。填充色表示资源类型，边框色表示运行状态，右下图例可随时查看。\r\n" +
                "在线模式下，选择、查看和布局保留可用；新增、删除、换父与语义连线会被拒绝并还原。集群写操作只从右侧操作或安全命令行发起。", LogKind.Info);
        }

        private void OnAbout(object sender, EventArgs e) { ShowWelcome(); }
        private void OnExit(object sender, EventArgs e) { this.Close(); }

        private void ShowWelcome()
        {
            _console.WriteBanner("云原生命令与图形管理器 · 候选版",
                "kubectl / helm / istioctl / yq · 统一安全策略 · 本地设计与在线快照");
            _console.WriteLine("输入 help 查看命令；tools 查看工具；install-help 查看安装指导。", LogKind.Hint);
            _console.WriteLine("首版不嵌入交互终端，不支持任意脚本、shell 管道或长驻任务；Namespace 不支持多层容器嵌套。", LogKind.Hint);
        }

        private void RefreshAll()
        {
            RefreshStatus();
            _ops.RefreshFromSelection();
            _editor.Canvas.Invalidate();
        }

        private void RefreshStatus()
        {
            if (_host == null || _coordinator == null) return;
            _lblConn.Text = "模式: " + ModeText(_coordinator.Mode);
            _lblCount.Text = string.Format("图形 {0} · 连线 {1}", _host.ShapeCount, _host.ConnectionCount);
            _lblSel.Text = "选中 " + _host.ListSelection().Count;
            _lblZoom.Text = string.Format("缩放 {0:0}%", _host.GetZoom() * 100f);
            if (!_coordinator.IsBusy) _lblTools.Text = ToolSummary();
            TopologySnapshot snapshot = _projector.Current;
            if (snapshot == null) _lblEndpoint.Text = "";
            else
            {
                string source = snapshot.Source == "design" ? "本地设计" : (string.IsNullOrEmpty(snapshot.ContextName) ? "集群快照" : snapshot.ContextName);
                _lblEndpoint.Text = source + (snapshot.IsStale ? " · 已过期" : "") +
                    (snapshot.Warnings.Count > 0 ? " · 警告 " + snapshot.Warnings.Count : "");
                _lblEndpoint.ToolTipText = snapshot.ObservedAt == DateTime.MinValue ? "" : snapshot.ObservedAt.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        private string ToolSummary()
        {
            ToolchainDetectionResult tools = _coordinator.Tools;
            if (tools == null) return "工具检测中";
            return "kubectl:" + ShortTool(tools.Kubectl) + "  helm:" + ShortTool(tools.Helm) +
                "  istioctl:" + ShortTool(tools.IstioCtl) + "  yq:" + ShortTool(tools.Yq);
        }

        private static string ShortTool(ToolDetectionResult status)
        {
            if (status.IsAvailable) return "可用";
            return status.IsInstalled ? "异常" : "缺失";
        }

        private static string ModeText(ManagerMode mode)
        {
            switch (mode)
            {
                case ManagerMode.Online: return "在线";
                case ManagerMode.DesignDebug: return "设计·调试";
                default: return "检测中";
            }
        }
    }
}
