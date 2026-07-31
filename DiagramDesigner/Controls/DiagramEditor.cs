using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using DiagramDesigner.Config;
using DiagramDesigner.Core;
using DiagramDesigner.Serialization;
using DiagramDesigner.Shapes;

namespace DiagramDesigner.Controls
{
    public partial class DiagramEditor : UserControl
    {
        private SplitContainer _mainSplit;
        private DrawingCanvas _canvas;
        private ToolboxPanel _toolbox;
        private PropertyGrid _propertyGrid;
        private Form _propertyPanel;
        private MenuStrip _hostMenu;
        private ToolStrip _toolStrip;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;
        private ToolStripStatusLabel _zoomLabel;
        private bool _contextMenuEnabled = true;
        private bool _showToolbarText = false;
        private bool _layoutApplied = false;
        private bool _showMenuStrip = true;
        private bool _menuInjected = false;
        private bool _menuInjectionScheduled = false;

        // 视图菜单项字段
        private ToolStripMenuItem _menuViewGrid;
        private ToolStripMenuItem _menuViewSnap;
        private ToolStripMenuItem _menuViewToolbar;
        private ToolStripMenuItem _menuViewToolbarText;
        private ToolStripMenuItem _menuViewProperty;
        private ToolStripMenuItem _menuViewToolbox;
        private ToolStripMenuItem _menuViewContextMenu;
        private ToolStripMenuItem _menuViewStatusBar;
        private ToolStripMenuItem _menuViewThemeLight;
        private ToolStripMenuItem _menuViewThemeDark;

        // 工具栏按钮字段
        private ToolStripButton _btnSelect;
        private ToolStripButton _btnConnect;
        private ToolStripButton _btnStraight;
        private ToolStripButton _btnCurve;
        private ToolStripButton _btnOrtho;
        private ToolStripButton _btnZoomIn;
        private ToolStripButton _btnZoomOut;
        private ToolStripButton _btnFit;
        private ToolStripButton _btnFront;
        private ToolStripButton _btnBack;
        private ToolStripButton _btnDelete;

        // 编辑菜单项字段
        private ToolStripMenuItem _menuEditDelete;

        // 图形操作菜单项
        private ToolStripMenuItem _menuShapeToFront;
        private ToolStripMenuItem _menuShapeToBack;

        /// <summary>
        /// 构造复合编辑器控件。初始化顺序：组件 → 工具栏 → 状态栏 →
        /// 属性面板 → 布局 → 事件绑定 → 内置行为注册 → 主题应用。
        /// </summary>
        public DiagramEditor()
        {
            InitializeComponent();
            BuildToolStrip();
            BuildStatusBar();
            BuildPropertyPanel();
            BuildLayout();
            WireEvents();
            RegisterBuiltinActions();
            ApplyTheme();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;

            _mainSplit = new SplitContainer();
            _mainSplit.Dock = DockStyle.Fill;
            _mainSplit.Orientation = Orientation.Vertical;
            _mainSplit.FixedPanel = FixedPanel.Panel1;
            _mainSplit.Panel1MinSize = 120;
            // SplitterDistance 不在此设置，延迟到 OnLayout

            _toolbox = new ToolboxPanel();
            _toolbox.Dock = DockStyle.Fill;

            _canvas = new DrawingCanvas();
            _canvas.Dock = DockStyle.Fill;

            _propertyGrid = new PropertyGrid();
            _propertyGrid.Dock = DockStyle.Fill;
            _propertyGrid.ToolbarVisible = true;
            _propertyGrid.HelpVisible = true;
            _propertyGrid.PropertySort = PropertySort.CategorizedAlphabetical;
            _propertyGrid.SelectedObject = GlobalConfig.Instance;
        }

        private void BuildPropertyPanel()
        {
            _propertyPanel = new Form();
            _propertyPanel.Text = "属性";
            _propertyPanel.Size = new Size(320, 600);
            _propertyPanel.StartPosition = FormStartPosition.Manual;
            _propertyPanel.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            _propertyPanel.ShowInTaskbar = false;
            _propertyPanel.ControlBox = true;
            _propertyPanel.MinimizeBox = false;
            _propertyPanel.MaximizeBox = false;
            _propertyPanel.Controls.Add(_propertyGrid);
            _propertyPanel.Visible = false;
            _propertyPanel.FormClosed += new FormClosedEventHandler(OnPropertyPanelClosed);
            _propertyPanel.Closing += new CancelEventHandler(OnPropertyPanelClosing);
        }

        private void OnPropertyPanelClosing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            _propertyPanel.Hide();
            if (_menuViewProperty != null)
                _menuViewProperty.Checked = false;
        }

        private void OnPropertyPanelClosed(object sender, FormClosedEventArgs e)
        {
            // 窗体被强制关闭时重建
            if (_propertyGrid != null && !_propertyGrid.IsDisposed)
                return;
            _propertyGrid = new PropertyGrid();
            _propertyGrid.Dock = DockStyle.Fill;
            _propertyGrid.ToolbarVisible = true;
            _propertyGrid.HelpVisible = true;
            _propertyGrid.PropertySort = PropertySort.CategorizedAlphabetical;
            _propertyGrid.SelectedObject = GlobalConfig.Instance;
            BuildPropertyPanel();
        }

        private void BuildLayout()
        {
            _mainSplit.Panel1.Controls.Add(_toolbox);
            _mainSplit.Panel2.Controls.Add(_canvas);

            this.Controls.Add(_mainSplit);
            this.Controls.Add(_toolStrip);
            this.Controls.Add(_statusStrip);

            this.Layout += new LayoutEventHandler(OnEditorLayout);
        }

        private void OnEditorLayout(object sender, LayoutEventArgs e)
        {
            if (_layoutApplied)
                return;

            // 等控件有实际尺寸后再设置 SplitterDistance
            if (this.Width > 50 && this.Height > 50)
            {
                ApplySplitterDistances();
                _layoutApplied = true;
                this.Layout -= new LayoutEventHandler(OnEditorLayout);
            }
        }

        private void ApplySplitterDistances()
        {
            // _mainSplit: 工具箱 220px，如果宽度不够则用 1/5
            SafeSetSplitterDistance(_mainSplit, 220, 0.2f);
        }

        private void SafeSetSplitterDistance(SplitContainer split, int desiredPixels, float fallbackRatio)
        {
            try
            {
                int width = split.Width;
                if (width <= 0)
                    return;

                // 检查固定面板方向
                int min = split.Panel1MinSize;
                int max = width - split.Panel2MinSize;
                if (split.FixedPanel == FixedPanel.Panel2)
                {
                    // FixedPanel.Panel2: SplitterDistance 从右侧算起
                    // desiredPixels 是面板2的宽度
                    int dist = width - desiredPixels;
                    if (dist < min) dist = min;
                    if (dist > max) dist = max;
                    if (dist > 0 && dist < width)
                    {
                        split.SplitterDistance = dist;
                    }
                    else
                    {
                        // 回退：按比例
                        int fallback = (int)(width * fallbackRatio);
                        if (fallback >= min && fallback <= max)
                            split.SplitterDistance = fallback;
                    }
                }
                else
                {
                    // FixedPanel.Panel1: SplitterDistance 从左侧算起
                    if (desiredPixels < min) desiredPixels = min;
                    if (desiredPixels > max) desiredPixels = max;
                    if (desiredPixels > 0 && desiredPixels < width)
                    {
                        split.SplitterDistance = desiredPixels;
                    }
                    else
                    {
                        int fallback = (int)(width * fallbackRatio);
                        if (fallback >= min && fallback <= max)
                            split.SplitterDistance = fallback;
                    }
                }
            }
            catch
            {
                // 静默忽略，控件仍可正常显示
            }
        }

        private void BuildToolStrip()
        {
            _toolStrip = new ToolStrip();
            _toolStrip.Dock = DockStyle.Top;
            _toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            _toolStrip.Padding = new Padding(2, 0, 2, 0);
            _toolStrip.ImageScalingSize = new Size(20, 20);

            _btnSelect = new ToolStripButton();
            _btnSelect.Image = LoadIcon("cursor.png");
            _btnSelect.Text = "选择工具";
            _btnSelect.CheckOnClick = true;
            _btnSelect.Checked = true;
            _btnSelect.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnSelect.ToolTipText = "选择工具 (V)";

            _btnConnect = new ToolStripButton();
            _btnConnect.Image = LoadIcon("connect.png");
            _btnConnect.Text = "连线工具";
            _btnConnect.CheckOnClick = true;
            _btnConnect.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnConnect.ToolTipText = "连线工具 (L)";

            _btnStraight = new ToolStripButton();
            _btnStraight.Image = LoadIcon("line_straight.png");
            _btnStraight.Text = "直线模式";
            _btnStraight.CheckOnClick = true;
            _btnStraight.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnStraight.ToolTipText = "直线模式";

            _btnCurve = new ToolStripButton();
            _btnCurve.Image = LoadIcon("line_curve.png");
            _btnCurve.Text = "曲线模式";
            _btnCurve.CheckOnClick = true;
            _btnCurve.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnCurve.ToolTipText = "曲线模式";

            _btnOrtho = new ToolStripButton();
            _btnOrtho.Image = LoadIcon("line_ortho.png");
            _btnOrtho.Text = "折线模式";
            _btnOrtho.CheckOnClick = true;
            _btnOrtho.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnOrtho.ToolTipText = "折线模式";

            _toolStrip.Items.Add(_btnSelect);
            _toolStrip.Items.Add(_btnConnect);
            _toolStrip.Items.Add(new ToolStripSeparator());
            _toolStrip.Items.Add(_btnStraight);
            _toolStrip.Items.Add(_btnCurve);
            _toolStrip.Items.Add(_btnOrtho);
            _toolStrip.Items.Add(new ToolStripSeparator());

            _btnZoomIn = new ToolStripButton();
            _btnZoomIn.Image = LoadIcon("zoom_in.png");
            _btnZoomIn.Text = "放大";
            _btnZoomIn.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnZoomIn.ToolTipText = "放大 (+)";

            _btnZoomOut = new ToolStripButton();
            _btnZoomOut.Image = LoadIcon("zoom_out.png");
            _btnZoomOut.Text = "缩小";
            _btnZoomOut.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnZoomOut.ToolTipText = "缩小 (-)";

            _btnFit = new ToolStripButton();
            _btnFit.Image = LoadIcon("fit_view.png");
            _btnFit.Text = "适应视图";
            _btnFit.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnFit.ToolTipText = "重置视图";

            _toolStrip.Items.Add(_btnZoomIn);
            _toolStrip.Items.Add(_btnZoomOut);
            _toolStrip.Items.Add(_btnFit);
            _toolStrip.Items.Add(new ToolStripSeparator());

            _btnFront = new ToolStripButton();
            _btnFront.Image = LoadIcon("to_front.png");
            _btnFront.Text = "置顶";
            _btnFront.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnFront.ToolTipText = "置于顶层";

            _btnBack = new ToolStripButton();
            _btnBack.Image = LoadIcon("to_back.png");
            _btnBack.Text = "置底";
            _btnBack.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnBack.ToolTipText = "置于底层";

            _toolStrip.Items.Add(_btnFront);
            _toolStrip.Items.Add(_btnBack);
            _toolStrip.Items.Add(new ToolStripSeparator());

            _btnDelete = new ToolStripButton();
            _btnDelete.Image = LoadIcon("delete.png");
            _btnDelete.Text = "删除";
            _btnDelete.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnDelete.ToolTipText = "删除选中 (Delete)";

            _toolStrip.Items.Add(_btnDelete);
        }

        private void BuildStatusBar()
        {
            _statusStrip = new StatusStrip();
            _statusStrip.Dock = DockStyle.Bottom;
            _statusLabel = new ToolStripStatusLabel("就绪");
            _zoomLabel = new ToolStripStatusLabel("缩放: 100%");
            _statusStrip.Items.Add(_statusLabel);
            _statusStrip.Items.Add(new ToolStripStatusLabel(""));
            _statusStrip.Items.Add(_zoomLabel);
        }

        /// <summary>
        /// 绑定画布事件、工具栏按钮事件和属性面板事件。
        /// 画布的 SelectionChanged / DocumentModified / ShapeAdded / ShapeDeleted /
        /// ConnectionAdded / ConnectionDeleted 均在此转发给宿主。
        /// </summary>
        private void WireEvents()
        {
            _canvas.SelectionChanged += new EventHandler(OnCanvasSelectionChanged);
            _canvas.DocumentModified += new EventHandler(OnDocumentModified);
            _canvas.ShapeAdded += new EventHandler<ShapeEventArgs>(OnCanvasShapeAdded);
            _canvas.ShapeDeleted += new EventHandler<ShapeEventArgs>(OnCanvasShapeDeleted);
            _canvas.ConnectionAdded += new EventHandler<ConnectionEventArgs>(OnCanvasConnectionAdded);
            _canvas.ConnectionDeleted += new EventHandler<ConnectionEventArgs>(OnCanvasConnectionDeleted);
            _canvas.MouseMove += new MouseEventHandler(OnCanvasMouseMove);
            _canvas.MouseDown += new MouseEventHandler(OnCanvasMouseDown);
            _propertyGrid.PropertyValueChanged += new PropertyValueChangedEventHandler(OnPropertyValueChanged);

            // 工具栏按钮事件绑定
            _btnSelect.Click += new EventHandler(OnSelectTool);
            _btnConnect.Click += new EventHandler(OnConnectTool);
            _btnStraight.Click += new EventHandler(OnStraightMode);
            _btnCurve.Click += new EventHandler(OnCurveMode);
            _btnOrtho.Click += new EventHandler(OnOrthoMode);
            _btnZoomIn.Click += new EventHandler(OnZoomIn);
            _btnZoomOut.Click += new EventHandler(OnZoomOut);
            _btnFit.Click += new EventHandler(OnZoomFit);
            _btnFront.Click += new EventHandler(OnBringToFront);
            _btnBack.Click += new EventHandler(OnSendToBack);
            _btnDelete.Click += new EventHandler(OnDeleteSelected);
        }

        // ===== 图标资源加载 =====

        private static Bitmap LoadIcon(string name)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                string resourceName = "DiagramDesigner.Icons." + name;
                Stream stream = asm.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    Bitmap bmp = new Bitmap(stream);
                    // 确保透明色正确
                    bmp.MakeTransparent(Color.Transparent);
                    return bmp;
                }
            }
            catch
            {
            }
            // 回退：生成一个简单的占位图标
            Bitmap fallback = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(fallback))
            using (Brush brush = new SolidBrush(Color.FromArgb(180, 200, 220)))
            {
                g.FillRectangle(brush, 0, 0, 16, 16);
            }
            return fallback;
        }

        // ===== 公共属性 =====

        public DrawingCanvas Canvas
        {
            get { return _canvas; }
        }

        public ToolboxPanel Toolbox
        {
            get { return _toolbox; }
        }

        public ToolStrip ToolStrip
        {
            get { return _toolStrip; }
        }

        public StatusStrip StatusStrip
        {
            get { return _statusStrip; }
        }

        public DrawingDocument Document
        {
            get { return _canvas.Document; }
        }

        [Category("面板")]
        [Description("是否显示工具栏")]
        public bool ShowToolbar
        {
            get { return _toolStrip.Visible; }
            set
            {
                _toolStrip.Visible = value;
                if (_menuViewToolbar != null)
                    _menuViewToolbar.Checked = value;
            }
        }

        [Category("面板")]
        [Description("是否显示属性栏")]
        public bool ShowPropertyPanel
        {
            get { return _propertyPanel != null && _propertyPanel.Visible; }
            set
            {
                if (_propertyPanel == null)
                    return;
                if (value)
                {
                    if (!_propertyPanel.Visible)
                    {
                        Form owner = this.FindForm();
                        if (owner != null)
                        {
                            Point pt = this.PointToScreen(new Point(this.Width - _propertyPanel.Width, 0));
                            _propertyPanel.Location = pt;
                            _propertyPanel.Show(owner);
                        }
                    }
                    else
                    {
                        _propertyPanel.Focus();
                    }
                }
                else
                {
                    _propertyPanel.Hide();
                }
                if (_menuViewProperty != null)
                    _menuViewProperty.Checked = value;
            }
        }

        [Category("面板")]
        [Description("是否显示工具箱面板")]
        public bool ShowToolboxPanel
        {
            get { return !_mainSplit.Panel1Collapsed; }
            set
            {
                _mainSplit.Panel1Collapsed = !value;
                if (_menuViewToolbox != null)
                    _menuViewToolbox.Checked = value;
            }
        }

        [Category("面板")]
        [Description("是否启用菜单栏（自动注入宿主窗体菜单）")]
        public bool ShowMenuStrip
        {
            get { return _showMenuStrip; }
            set
            {
                _showMenuStrip = value;
                if (value && !_menuInjected)
                    TryAutoInjectMenu();
            }
        }

        [Category("面板")]
        [Description("是否显示状态栏")]
        public bool ShowStatusBar
        {
            get { return _statusStrip.Visible; }
            set
            {
                _statusStrip.Visible = value;
                if (_menuViewStatusBar != null)
                    _menuViewStatusBar.Checked = value;
            }
        }

        [Category("行为")]
        [Description("是否启用右键菜单")]
        public bool ShowContextMenu
        {
            get { return _contextMenuEnabled; }
            set
            {
                _contextMenuEnabled = value;
                if (_menuViewContextMenu != null)
                    _menuViewContextMenu.Checked = value;
            }
        }

        [Category("外观")]
        [Description("工具栏是否显示文字标签")]
        public bool ShowToolbarText
        {
            get { return _showToolbarText; }
            set
            {
                _showToolbarText = value;
                if (_menuViewToolbarText != null)
                    _menuViewToolbarText.Checked = value;
                ApplyToolbarRenderMode();
            }
        }

        [Category("外观")]
        [Description("编辑器配色主题")]
        public EditorTheme Theme
        {
            get { return GlobalConfig.Instance.Theme; }
            set
            {
                GlobalConfig.Instance.Theme = value;
                ApplyTheme();
            }
        }

        // ===== 公共快捷方法 =====

        public void ZoomIn()
        {
            _canvas.Zoom *= 1.2f;
            UpdateZoomLabel();
        }

        public void ZoomOut()
        {
            _canvas.Zoom /= 1.2f;
            UpdateZoomLabel();
        }

        public void ZoomReset()
        {
            _canvas.Zoom = 1.0f;
            _canvas.Offset = new PointF(0, 0);
            UpdateZoomLabel();
        }

        public void SetTool(CanvasTool tool)
        {
            _canvas.CurrentTool = tool;
            UpdateToolState();
        }

        public void SetConnectionMode(ConnectionMode mode)
        {
            GlobalConfig.Instance.DefaultConnectionMode = mode;
            List<Connection> conns = _canvas.Document.GetSelectedConnections();
            foreach (Connection conn in conns)
            {
                conn.Mode = mode;
            }
            if (_btnStraight != null)
                _btnStraight.Checked = (mode == ConnectionMode.Straight);
            if (_btnCurve != null)
                _btnCurve.Checked = (mode == ConnectionMode.Curve);
            if (_btnOrtho != null)
                _btnOrtho.Checked = (mode == ConnectionMode.Orthogonal);
            _canvas.Invalidate();
        }

        public void DeleteSelected()
        {
            _canvas.DeleteSelected();
        }

        public void SelectAll()
        {
            foreach (ShapeBase s in _canvas.Document.Shapes)
                s.Selected = true;
            _canvas.Invalidate();
        }

        public new void BringToFront()
        {
            _canvas.BringToFront();
        }

        public new void SendToBack()
        {
            _canvas.SendToBack();
        }

        /// <summary>
        /// 获取当前画布中的文档对象
        /// </summary>
        public DrawingDocument GetDocument()
        {
            return _canvas.Document;
        }

        /// <summary>
        /// 用新的文档替换当前画布内容，并应用文档中存储的配置
        /// </summary>
        public void SetDocument(DrawingDocument document)
        {
            if (document == null)
                return;
            _canvas.Document.Clear();
            foreach (ShapeBase shape in document.Shapes)
                _canvas.Document.AddShape(shape);
            foreach (Connection conn in document.Connections)
                _canvas.Document.AddConnection(conn);
            _propertyGrid.SelectedObject = GlobalConfig.Instance;
            _canvas.Invalidate();

            // 应用文档自带的配置（工具栏、属性面板、工具箱等 UI 状态）
            if (document.Config != null)
            {
                _canvas.Config = document.Config;
                ApplyCanvasConfig(document.Config);
            }
        }

        /// <summary>
        /// 用文档和配置替换当前画布内容。这是宿主"构建画布文档并传入"的推荐方式。
        /// </summary>
        public void SetDocument(DrawingDocument document, CanvasConfig config)
        {
            if (document != null)
                document.Config = config;
            SetDocument(document);
        }

        /// <summary>
        /// 清空当前画布，并应用画布配置
        /// </summary>
        public void ClearDocument()
        {
            _canvas.Document.Clear();
            _propertyGrid.SelectedObject = GlobalConfig.Instance;
            _canvas.Invalidate();

            // 应用画布配置
            ApplyCanvasConfig(_canvas.Config);
        }

        // ===== 自动菜单注入 =====

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (this.Parent == null)
            {
                UninjectMenus();
            }
            else
            {
                ScheduleMenuInjection();
            }
        }

        private void ScheduleMenuInjection()
        {
            if (!_showMenuStrip || _menuInjected || _menuInjectionScheduled)
                return;

            _menuInjectionScheduled = true;

            if (this.IsHandleCreated)
            {
                // 句柄已创建，延迟到下一次消息循环
                BeginInvoke(new MethodInvoker(TryAutoInjectMenu));
            }
            else
            {
                // 句柄尚未创建，订阅 HandleCreated 事件
                this.HandleCreated += new EventHandler(OnHandleCreatedForMenuInjection);
            }
        }

        private void OnHandleCreatedForMenuInjection(object sender, EventArgs e)
        {
            this.HandleCreated -= new EventHandler(OnHandleCreatedForMenuInjection);
            TryAutoInjectMenu();
        }

        private void TryAutoInjectMenu()
        {
            _menuInjectionScheduled = false;

            if (!_showMenuStrip || _menuInjected)
                return;

            Form topForm = GetTopLevelForm();
            if (topForm == null)
                return;

            MenuStrip menu = FindMenuStripInForm(topForm);
            if (menu != null)
            {
                _hostMenu = menu;
                InjectMenus();
                ApplyTheme();
            }
            else
            {
                // 尚未找到 MenuStrip，监听宿主 Form 的 ControlAdded 事件等待注入
                topForm.ControlAdded += new ControlEventHandler(OnHostControlAdded);
            }
        }

        private void OnHostControlAdded(object sender, ControlEventArgs e)
        {
            if (e.Control is MenuStrip)
            {
                Form topForm = sender as Form;
                if (topForm != null)
                    topForm.ControlAdded -= new ControlEventHandler(OnHostControlAdded);
                if (!_menuInjected && _showMenuStrip)
                {
                    _hostMenu = (MenuStrip)e.Control;
                    InjectMenus();
                    ApplyTheme();
                }
            }
        }

        private Form GetTopLevelForm()
        {
            Control c = this;
            while (c != null)
            {
                if (c is Form)
                    return (Form)c;
                c = c.Parent;
            }
            return null;
        }

        private MenuStrip FindMenuStripInForm(Form form)
        {
            if (form.MainMenuStrip != null)
                return form.MainMenuStrip;

            foreach (Control c in form.Controls)
            {
                if (c is MenuStrip)
                    return (MenuStrip)c;
            }
            return null;
        }

        private void InjectMenus()
        {
            if (_hostMenu == null)
                return;
            if (_menuInjected)
                return;

            // 清理之前可能残留的注入菜单（应对控件反复增删场景）
            CleanOldInjectedMenus(_hostMenu);

            ToolStripMenuItem editMenu = FindOrCreateMenu(_hostMenu, "编辑(&E)");
            _menuEditDelete = MarkInjectedMenu(CreateMenuItem("删除", "delete.png", new EventHandler(OnEditDelete), Keys.Delete));
            _menuEditDelete.Enabled = false;
            editMenu.DropDownItems.Add(_menuEditDelete);
            editMenu.DropDownItems.Add(MarkInjected(CreateMenuItem("全选", "select_all.png", new EventHandler(OnEditSelectAll), Keys.Control | Keys.A)));

            ToolStripMenuItem viewMenu = FindOrCreateMenu(_hostMenu, "视图(&V)");
            _menuViewGrid = MarkInjectedMenu(CreateCheckMenuItem("网格", "grid.png", GlobalConfig.Instance.ShowGrid, new EventHandler(OnViewGrid)));
            viewMenu.DropDownItems.Add(_menuViewGrid);
            _menuViewSnap = MarkInjectedMenu(CreateCheckMenuItem("对齐", "snap.png", GlobalConfig.Instance.SnapToGrid, new EventHandler(OnViewSnap)));
            viewMenu.DropDownItems.Add(_menuViewSnap);
            viewMenu.DropDownItems.Add(MarkInjected(new ToolStripSeparator()));
            _menuViewToolbar = MarkInjectedMenu(CreateCheckMenuItem("工具栏", "toolbar.png", true, new EventHandler(OnViewToolbar)));
            viewMenu.DropDownItems.Add(_menuViewToolbar);
            _menuViewToolbarText = MarkInjectedMenu(CreateCheckMenuItem("工具栏文字", "menu_text.png", false, new EventHandler(OnViewToolbarText)));
            viewMenu.DropDownItems.Add(_menuViewToolbarText);
            _menuViewProperty = MarkInjectedMenu(CreateCheckMenuItem("属性栏", "properties.png", false, new EventHandler(OnViewProperty)));
            viewMenu.DropDownItems.Add(_menuViewProperty);
            _menuViewToolbox = MarkInjectedMenu(CreateCheckMenuItem("工具箱", "toolbox.png", true, new EventHandler(OnViewToolbox)));
            viewMenu.DropDownItems.Add(_menuViewToolbox);
            _menuViewContextMenu = MarkInjectedMenu(CreateCheckMenuItem("右键菜单", "eye.png", true, new EventHandler(OnViewContextMenu)));
            viewMenu.DropDownItems.Add(_menuViewContextMenu);
            _menuViewStatusBar = MarkInjectedMenu(CreateCheckMenuItem("状态栏", "statusbar.png", true, new EventHandler(OnViewStatusBar)));
            viewMenu.DropDownItems.Add(_menuViewStatusBar);
            viewMenu.DropDownItems.Add(MarkInjected(new ToolStripSeparator()));
            _menuViewThemeLight = MarkInjectedMenu(CreateCheckMenuItem("亮色主题", "sun.png", true, new EventHandler(OnThemeLight)));
            viewMenu.DropDownItems.Add(_menuViewThemeLight);
            _menuViewThemeDark = MarkInjectedMenu(CreateCheckMenuItem("暗色主题", "moon.png", false, new EventHandler(OnThemeDark)));
            viewMenu.DropDownItems.Add(_menuViewThemeDark);
            viewMenu.DropDownItems.Add(MarkInjected(new ToolStripSeparator()));
            viewMenu.DropDownItems.Add(MarkInjected(CreateMenuItem("放大", "zoom_in.png", new EventHandler(OnViewZoomIn))));
            viewMenu.DropDownItems.Add(MarkInjected(CreateMenuItem("缩小", "zoom_out.png", new EventHandler(OnViewZoomOut))));
            viewMenu.DropDownItems.Add(MarkInjected(CreateMenuItem("重置视图", "fit_view.png", new EventHandler(OnViewReset))));

            ToolStripMenuItem toolsMenu = FindOrCreateMenu(_hostMenu, "工具(&T)");
            toolsMenu.DropDownItems.Add(MarkInjected(CreateMenuItem("选择工具", "cursor.png", new EventHandler(OnToolSelect))));
            toolsMenu.DropDownItems.Add(MarkInjected(CreateMenuItem("连线工具", "connect.png", new EventHandler(OnToolConnect))));
            toolsMenu.DropDownItems.Add(MarkInjected(new ToolStripSeparator()));
            toolsMenu.DropDownItems.Add(MarkInjected(CreateMenuItem("直线模式", "line_straight.png", new EventHandler(OnToolStraight))));
            toolsMenu.DropDownItems.Add(MarkInjected(CreateMenuItem("曲线模式", "line_curve.png", new EventHandler(OnToolCurve))));
            toolsMenu.DropDownItems.Add(MarkInjected(CreateMenuItem("折线模式", "line_ortho.png", new EventHandler(OnToolOrtho))));

            ToolStripMenuItem shapeMenu = FindOrCreateMenu(_hostMenu, "图形(&S)");
            _menuShapeToFront = MarkInjectedMenu(CreateMenuItem("置顶", "to_front.png", new EventHandler(OnBringToFront)));
            _menuShapeToFront.Enabled = false;
            shapeMenu.DropDownItems.Add(_menuShapeToFront);
            _menuShapeToBack = MarkInjectedMenu(CreateMenuItem("置底", "to_back.png", new EventHandler(OnSendToBack)));
            _menuShapeToBack.Enabled = false;
            shapeMenu.DropDownItems.Add(_menuShapeToBack);

            UpdateThemeCheckState();
            _menuInjected = true;
        }

        private ToolStripMenuItem FindOrCreateMenu(MenuStrip menuStrip, string text)
        {
            foreach (ToolStripItem item in menuStrip.Items)
            {
                ToolStripMenuItem m = item as ToolStripMenuItem;
                if (m != null && m.Text == text)
                    return m;
            }
            ToolStripMenuItem newMenu = new ToolStripMenuItem(text);
            newMenu.Tag = "CND_TopMenu";
            menuStrip.Items.Add(newMenu);
            return newMenu;
        }

        private ToolStripItem MarkInjected(ToolStripItem item)
        {
            item.Tag = "CND_Injected";
            return item;
        }

        private ToolStripMenuItem MarkInjectedMenu(ToolStripMenuItem item)
        {
            item.Tag = "CND_Injected";
            return item;
        }

        private void CleanOldInjectedMenus(MenuStrip menuStrip)
        {
            if (menuStrip == null)
                return;

            // 递归清理所有标记为注入的菜单项
            for (int i = menuStrip.Items.Count - 1; i >= 0; i--)
            {
                ToolStripMenuItem topItem = menuStrip.Items[i] as ToolStripMenuItem;
                if (topItem != null)
                {
                    CleanInjectedDropDownItems(topItem);
                    // 如果顶级菜单是我们创建的且已空，则删除
                    if ("CND_TopMenu".Equals(topItem.Tag) && topItem.DropDownItems.Count == 0)
                        menuStrip.Items.RemoveAt(i);
                }
            }
        }

        private void CleanInjectedDropDownItems(ToolStripMenuItem parent)
        {
            for (int i = parent.DropDownItems.Count - 1; i >= 0; i--)
            {
                ToolStripItem item = parent.DropDownItems[i];
                ToolStripMenuItem subMenu = item as ToolStripMenuItem;
                if (subMenu != null)
                {
                    // 先递归清理子菜单
                    CleanInjectedDropDownItems(subMenu);
                }
                if ("CND_Injected".Equals(item.Tag))
                    parent.DropDownItems.RemoveAt(i);
            }
        }

        private void UninjectMenus()
        {
            // 取消 ControlAdded 监听
            Form topForm = GetTopLevelForm();
            if (topForm != null)
                topForm.ControlAdded -= new ControlEventHandler(OnHostControlAdded);

            // 清理已注入的菜单项
            if (_hostMenu != null)
                CleanOldInjectedMenus(_hostMenu);

            // 重置状态
            _hostMenu = null;
            _menuInjected = false;
            _menuInjectionScheduled = false;
            _menuEditDelete = null;
            _menuViewGrid = null;
            _menuViewSnap = null;
            _menuViewToolbar = null;
            _menuViewToolbarText = null;
            _menuViewProperty = null;
            _menuViewToolbox = null;
            _menuViewContextMenu = null;
            _menuViewStatusBar = null;
            _menuViewThemeLight = null;
            _menuViewThemeDark = null;
            _menuShapeToFront = null;
            _menuShapeToBack = null;
        }

        private ToolStripMenuItem CreateMenuItem(string text, string iconName, EventHandler handler)
        {
            return CreateMenuItem(text, iconName, handler, Keys.None);
        }

        private ToolStripMenuItem CreateMenuItem(string text, EventHandler handler)
        {
            return CreateMenuItem(text, null, handler, Keys.None);
        }

        private ToolStripMenuItem CreateMenuItem(string text, string iconName, EventHandler handler, Keys shortcutKeys)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text, null, handler);
            if (iconName != null)
                item.Image = LoadIcon(iconName);
            if (shortcutKeys != Keys.None)
                item.ShortcutKeys = shortcutKeys;
            return item;
        }

        private ToolStripMenuItem CreateCheckMenuItem(string text, string iconName, bool checked_, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text, null, handler);
            item.CheckOnClick = true;
            item.Checked = checked_;
            if (iconName != null)
                item.Image = LoadIcon(iconName);
            return item;
        }

        // ===== 事件 =====

        public event EventHandler ThemeChanged;

        /// <summary>图形添加到画布时触发，携带图形类型名与显示名</summary>
        public event EventHandler<ShapeEventArgs> ShapeAdded;
        /// <summary>图形从画布删除时触发</summary>
        public event EventHandler<ShapeEventArgs> ShapeDeleted;
        /// <summary>连线创建时触发</summary>
        public event EventHandler<ConnectionEventArgs> ConnectionAdded;
        /// <summary>连线删除时触发</summary>
        public event EventHandler<ConnectionEventArgs> ConnectionDeleted;
        /// <summary>图形状态切换时触发，携带新旧状态名</summary>
        public event EventHandler<ShapeStateChangedEventArgs> ShapeStateChanged;

        protected virtual void OnThemeChanged(EventArgs e)
        {
            if (ThemeChanged != null)
                ThemeChanged(this, e);
        }

        /// <summary>转发画布的 ShapeAdded 事件</summary>
        private void OnCanvasShapeAdded(object sender, ShapeEventArgs e)
        {
            if (ShapeAdded != null)
                ShapeAdded(this, e);
        }

        /// <summary>转发画布的 ShapeDeleted 事件</summary>
        private void OnCanvasShapeDeleted(object sender, ShapeEventArgs e)
        {
            if (ShapeDeleted != null)
                ShapeDeleted(this, e);
        }

        /// <summary>转发画布的 ConnectionAdded 事件</summary>
        private void OnCanvasConnectionAdded(object sender, ConnectionEventArgs e)
        {
            if (ConnectionAdded != null)
                ConnectionAdded(this, e);
        }

        /// <summary>转发画布的 ConnectionDeleted 事件</summary>
        private void OnCanvasConnectionDeleted(object sender, ConnectionEventArgs e)
        {
            if (ConnectionDeleted != null)
                ConnectionDeleted(this, e);
        }

        /// <summary>触发 ShapeStateChanged 事件（由命令层状态切换调用）</summary>
        internal void RaiseShapeStateChanged(ShapeBase shape, string oldState, string newState)
        {
            if (ShapeStateChanged != null)
                ShapeStateChanged(this, new ShapeStateChangedEventArgs(shape, oldState, newState));
        }

        // ===== 内部辅助 =====

        private void UpdateToolState()
        {
            if (_btnSelect != null)
                _btnSelect.Checked = (_canvas.CurrentTool == CanvasTool.Select);
            if (_btnConnect != null)
                _btnConnect.Checked = (_canvas.CurrentTool == CanvasTool.Connect);
        }

        private void UpdateZoomLabel()
        {
            if (_zoomLabel != null)
            {
                _zoomLabel.Text = string.Format("缩放: {0:0}%", _canvas.Zoom * 100);
            }
        }

        private void ApplyToolbarRenderMode()
        {
            ToolStripItemDisplayStyle style = _showToolbarText
                ? ToolStripItemDisplayStyle.ImageAndText
                : ToolStripItemDisplayStyle.Image;
            if (_btnSelect != null) _btnSelect.DisplayStyle = style;
            if (_btnConnect != null) _btnConnect.DisplayStyle = style;
            if (_btnStraight != null) _btnStraight.DisplayStyle = style;
            if (_btnCurve != null) _btnCurve.DisplayStyle = style;
            if (_btnOrtho != null) _btnOrtho.DisplayStyle = style;
            if (_btnZoomIn != null) _btnZoomIn.DisplayStyle = style;
            if (_btnZoomOut != null) _btnZoomOut.DisplayStyle = style;
            if (_btnFit != null) _btnFit.DisplayStyle = style;
            if (_btnFront != null) _btnFront.DisplayStyle = style;
            if (_btnBack != null) _btnBack.DisplayStyle = style;
            if (_btnDelete != null) _btnDelete.DisplayStyle = style;
        }

        public void ApplyTheme()
        {
            EditorTheme theme = GlobalConfig.Instance.Theme;
            bool isDark = (theme == EditorTheme.Dark);

            _mainSplit.BackColor = GlobalConfig.Instance.ToolPanelBackColor;

            Color backColor = isDark ? Color.FromArgb(30, 35, 48) : Color.FromArgb(245, 248, 252);
            Color foreColor = isDark ? Color.FromArgb(210, 220, 235) : Color.FromArgb(30, 30, 30);

            _toolStrip.BackColor = backColor;
            _toolStrip.ForeColor = foreColor;
            _toolStrip.Renderer = new ToolStripProfessionalRenderer(new MyColorTable(isDark));

            if (_hostMenu != null)
            {
                _hostMenu.BackColor = backColor;
                _hostMenu.ForeColor = foreColor;
                _hostMenu.Renderer = new ToolStripProfessionalRenderer(new MyColorTable(isDark));
            }

            _statusStrip.BackColor = isDark ? Color.FromArgb(25, 30, 42) : Color.FromArgb(240, 242, 248);
            _statusStrip.ForeColor = foreColor;

            UpdateThemeCheckState();
            this.Invalidate(true);
        }

        private void UpdateThemeCheckState()
        {
            bool isDark = (GlobalConfig.Instance.Theme == EditorTheme.Dark);
            if (_menuViewThemeLight != null)
                _menuViewThemeLight.Checked = !isDark;
            if (_menuViewThemeDark != null)
                _menuViewThemeDark.Checked = isDark;
        }

        private void OnPropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            // 如果修改的是画布配置属性，实时应用配置变更
            if (_propertyGrid.SelectedObject == _canvas.Config)
            {
                ApplyCanvasConfig(_canvas.Config);
            }
            _canvas.Invalidate();
        }
    }

    internal class MyColorTable : ProfessionalColorTable
    {
        private bool _dark;

        public MyColorTable(bool dark)
        {
            _dark = dark;
        }

        public override Color ToolStripGradientBegin
        {
            get { return _dark ? Color.FromArgb(45, 50, 65) : Color.FromArgb(250, 252, 255); }
        }

        public override Color ToolStripGradientMiddle
        {
            get { return _dark ? Color.FromArgb(38, 43, 58) : Color.FromArgb(245, 247, 252); }
        }

        public override Color ToolStripGradientEnd
        {
            get { return _dark ? Color.FromArgb(32, 37, 50) : Color.FromArgb(240, 242, 248); }
        }

        public override Color MenuStripGradientBegin
        {
            get { return _dark ? Color.FromArgb(45, 50, 65) : Color.FromArgb(250, 252, 255); }
        }

        public override Color MenuStripGradientEnd
        {
            get { return _dark ? Color.FromArgb(38, 43, 58) : Color.FromArgb(245, 247, 252); }
        }

        public override Color MenuItemSelected
        {
            get { return _dark ? Color.FromArgb(60, 120, 200) : Color.FromArgb(220, 235, 250); }
        }

        public override Color MenuItemSelectedGradientBegin
        {
            get { return _dark ? Color.FromArgb(50, 110, 190) : Color.FromArgb(210, 230, 248); }
        }

        public override Color MenuItemSelectedGradientEnd
        {
            get { return _dark ? Color.FromArgb(45, 100, 175) : Color.FromArgb(200, 225, 245); }
        }

        public override Color SeparatorDark
        {
            get { return _dark ? Color.FromArgb(55, 60, 78) : Color.FromArgb(210, 215, 225); }
        }

        public override Color SeparatorLight
        {
            get { return _dark ? Color.FromArgb(70, 75, 92) : Color.FromArgb(230, 233, 240); }
        }

        public override Color ImageMarginGradientBegin
        {
            get { return _dark ? Color.FromArgb(42, 47, 62) : Color.FromArgb(248, 250, 255); }
        }

        public override Color ImageMarginGradientMiddle
        {
            get { return _dark ? Color.FromArgb(38, 43, 58) : Color.FromArgb(245, 247, 252); }
        }

        public override Color ImageMarginGradientEnd
        {
            get { return _dark ? Color.FromArgb(35, 40, 52) : Color.FromArgb(242, 244, 250); }
        }

        public override Color ButtonSelectedHighlight
        {
            get { return _dark ? Color.FromArgb(70, 140, 220) : Color.FromArgb(200, 225, 248); }
        }

        public override Color ButtonPressedHighlight
        {
            get { return _dark ? Color.FromArgb(60, 120, 200) : Color.FromArgb(190, 218, 242); }
        }

        public override Color ButtonSelectedBorder
        {
            get { return _dark ? Color.FromArgb(90, 160, 240) : Color.FromArgb(160, 200, 235); }
        }

        public override Color StatusStripGradientBegin
        {
            get { return _dark ? Color.FromArgb(28, 32, 45) : Color.FromArgb(242, 244, 250); }
        }

        public override Color StatusStripGradientEnd
        {
            get { return _dark ? Color.FromArgb(25, 28, 40) : Color.FromArgb(235, 238, 245); }
        }
    }
}
