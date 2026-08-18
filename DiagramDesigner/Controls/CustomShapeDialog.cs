using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DiagramDesigner.Core;

namespace DiagramDesigner.Controls
{
    /// <summary>
    /// 自定义图形构建器（带状态和行为编辑）。
    /// 状态选项卡内联编辑：左侧状态列表+管理按钮，右侧图形编辑器（工具栏+画布）。
    /// 路径下拉菜单集成区域管理，支持路径层叠顺序调整和逐路径布尔运算。
    /// 底部信息窗格提供上下文相关的操作提示。
    /// </summary>
    public class CustomShapeDialog : Form
    {
        // 通用控件
        private TextBox _txtName;
        private TabControl _tabControl;
        private Button _btnOk;
        private Button _btnCancel;
        private ColorDialog _colorDialog;
        private ToolTip toolTip1 = new ToolTip();

        // === 状态选项卡 — 左侧 ===
        private ListBox _listStates;
        private Button _btnAddState;
        private Button _btnCopyState;
        private Button _btnDeleteState;
        private Button _btnSetAsDefault;
        private List<ShapeState> _states = new List<ShapeState>();

        // === 状态选项卡 — 右侧编辑器 ===
        private TextBox _txtStateName;
        private NumericUpDown _numPriority;
        private CheckBox _chkFilled;
        private CheckBox _chkCustomGeom;
        private Panel _canvasPanel;
        private Label _lblDefaultHint;

        // 工具栏按钮
        private Button _btnAddVertex;
        private Button _btnDeleteVertex;
        private Button _btnClosePath;
        private Button _btnFillColor;
        private Button _btnBorderColor;
        private Button _btnTextColor;
        private Button _btnHeaderColor;
        private Button _btnCopyDefault;
        private Button _btnClearGeom;
        private Button _btnCircle;
        private CheckBox _chkIsShape;
        private CheckBox _chkPreview;

        // 多路径工具栏控件
        private Panel _panelPathToolbar;
        private ComboBox _cmbPathSelect;
        private Button _btnNewPath;
        private Button _btnDeletePath;
        private Button _btnMovePathUp;
        private Button _btnMovePathDown;
        private Button _btnAddZoneTB;
        private Button _btnEditZoneTB;
        private Button _btnDeleteZoneTB;
        private Button _btnBoolOp;
        private Button _btnMergePaths;
        private ContextMenuStrip _boolOpMenu;
        private bool _suppressPathChange = false;

        // Zone 列表（每个状态独立）
        private List<ShapeZone> _zones = new List<ShapeZone>();

        // 信息窗格
        private Panel _panelInfo;
        private Label _lblInfo;
        private Timer _infoTimer;
        private int _infoTipIndex = 0;

        // 编辑器画布数据（多路径 + 逐路径布尔运算）
        private List<List<CurveVertex>> _paths = new List<List<CurveVertex>>();
        private List<BooleanOperation> _pathBoolOps = new List<BooleanOperation>();
        private List<bool> _pathIsShape = new List<bool>();  // 每条路径是否为形状（填充+闭合），false=线体
        private int _currentPathIndex = 0;
        private int _dragIndex = -1;
        private List<int> _selectedVertices = new List<int>();
        private bool _closedPath = false;
        private bool _filled = true;
        private Color _geomFillColor = Color.FromArgb(220, 240, 255);
        private Color _geomBorderColor = Color.FromArgb(80, 120, 180);
        private Color _stateFillColor = Color.FromArgb(230, 240, 255);
        private Color _stateBorderColor = Color.FromArgb(80, 120, 180);
        private Color _stateTextColor = Color.FromArgb(40, 40, 40);
        private Color _stateHeaderColor = Color.FromArgb(80, 130, 180);

        // Zone 选中与拖拽状态
        private int _selectedZoneIndex = -1;  // -1 表示未选中 Zone（路径编辑模式）
        private int _zoneDragMode = 0;        // 0=无, 1=移动, 2=调整大小
        private int _zoneDragHandle = -1;     // 拖拽的手柄索引 (0~3 for corners)
        private Point _zoneDragStart;

        // 贝塞尔句柄拖拽状态
        // _dragHandleType: 0=无, 1=拖拽HandleOut, 2=拖拽HandleIn
        private int _dragHandleType = 0;
        private int _dragHandleVertex = -1;
        private bool _suppressIsShapeChange = false;

        // 框选顶点状态
        private bool _isBoxSelecting = false;
        private Point _boxSelectStart;
        private Point _boxSelectEnd;

        // 群体拖拽状态
        private bool _isGroupDragging = false;
        private Point _groupDragStart;
        private Dictionary<int, PointF> _groupDragOrigPositions;

        /// <summary>当前正在编辑的状态索引，-1 表示无选择</summary>
        private int _editingStateIndex = -1;

        /// <summary>抑制状态列表 SelectedIndexChanged 事件的标志</summary>
        private bool _suppressStateChange = false;

        // === 行为选项卡 ===
        private ListBox _listActions;
        private Button _btnAddAction;
        private Button _btnEditAction;
        private Button _btnDeleteAction;
        private List<ShapeAction> _actions = new List<ShapeAction>();

        private ShapeType _resultShapeType = null;
        public ShapeType ResultShapeType { get { return _resultShapeType; } }

        private const string DefaultStateName = "默认";

        // 信息窗格提示文本
        private static readonly string[] _infoTips = new string[]
        {
            "提示：在画布上点击可添加顶点，拖拽顶点可调整位置，双击顶点可删除。",
            "提示：路径下拉菜单中区域（◈）始终显示在路径（▲）之上。选择区域可在画布中拖拽边角调整宽高。",
            "提示：每条路径的布尔运算作用于紧邻的下层路径，新路径插入到当前路径上方。最底层路径运算始终为无。",
            "提示：合并按钮（⊕→1）可将布尔运算结果扁平化为单一新路径，产生干净的外轮廓。",
            "提示：标题区域默认垂直居中水平居中，类图/包图/容器等需容纳内容的图形默认顶部对齐。",
            "提示：添加标题区域后自动注册\"编辑标题\"行为（设计时）；添加成员区域后自动注册\"添加/删除成员\"行为（设计时+运行时）。",
            "提示：点击区域和连接区域为特殊功能区域，添加后自动注册对应的系统行为。连接区域可限定线型和自连。",
            "提示：行为选项卡中的系统行为由区域自动注册，不可删除但可在右键菜单中隐藏。",
            "提示：行为类型选择\"行为序列\"可包含多个子操作（状态切换、宿主回调），按序执行。"
        };

        public CustomShapeDialog() : this(null) { }

        public CustomShapeDialog(ShapeType editShape)
        {
            this.Text = (editShape == null) ? "创建自定义图形" : "编辑自定义图形";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.MaximizeBox = false;
            this.ClientSize = new Size(720, 540);

            _colorDialog = new ColorDialog();

            Label lblName = new Label();
            lblName.Text = "名称：";
            lblName.Location = new Point(10, 14);
            lblName.AutoSize = true;
            this.Controls.Add(lblName);

            _txtName = new TextBox();
            _txtName.Location = new Point(60, 11);
            _txtName.Size = new Size(200, 22);
            _txtName.Text = (editShape == null) ? "CustomShape" : editShape.Name;
            this.Controls.Add(_txtName);

            _tabControl = new TabControl();
            _tabControl.Location = new Point(10, 40);
            _tabControl.Size = new Size(690, 460);
            this.Controls.Add(_tabControl);

            BuildStateTab();
            BuildActionTab();

            _btnOk = new Button();
            _btnOk.Text = "确定";
            _btnOk.DialogResult = DialogResult.OK;
            _btnOk.Location = new Point(520, 505);
            _btnOk.Size = new Size(80, 30);
            this.Controls.Add(_btnOk);

            _btnCancel = new Button();
            _btnCancel.Text = "取消";
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.Location = new Point(610, 505);
            _btnCancel.Size = new Size(80, 30);
            this.Controls.Add(_btnCancel);

            if (editShape != null)
                LoadFromShapeType(editShape);
            else
            {
                ShapeState defaultState = new ShapeState();
                defaultState.Name = DefaultStateName;
                defaultState.UseCustomRenderCommands = true;
                defaultState.CustomRenderCommands = BuildDefaultTriangleCommands();
                _states.Add(defaultState);
                RefreshStateList();
            }

            // 初始加载第一个状态到编辑器
            if (_listStates.Items.Count > 0)
            {
                _suppressStateChange = true;
                _listStates.SelectedIndex = 0;
                _suppressStateChange = false;
                _editingStateIndex = 0;
                LoadStateToEditor(0);
            }

            UpdateStateButtons();
            UpdateActionButtons();

            // 启动信息窗格轮播
            StartInfoTimer();
        }

        private List<RenderCommand> BuildDefaultTriangleCommands()
        {
            RenderCommand polyCmd = new RenderCommand();
            polyCmd.CommandType = RenderCommandType.Polygon;
            polyCmd.PolygonPoints = new PointF[]
            {
                new PointF(0.5f, 0f),
                new PointF(0f, 1f),
                new PointF(1f, 1f)
            };
            polyCmd.X = 0; polyCmd.Y = 0;
            polyCmd.Width = 1; polyCmd.Height = 1;
            polyCmd.FillColor = Color.FromArgb(220, 240, 255);
            polyCmd.StrokeColor = Color.FromArgb(80, 120, 180);
            polyCmd.StrokeWidth = 2f;
            polyCmd.Fill = true;
            return new List<RenderCommand> { polyCmd };
        }

        #region 标签页构建

        private void BuildStateTab()
        {
            TabPage page = new TabPage("状态");
            page.BorderStyle = BorderStyle.None;

            // === 左侧：状态列表 + 管理按钮 ===
            _listStates = new ListBox();
            _listStates.Location = new Point(8, 8);
            _listStates.Size = new Size(170, 380);
            _listStates.BorderStyle = BorderStyle.FixedSingle;
            _listStates.DrawMode = DrawMode.OwnerDrawFixed;
            _listStates.ItemHeight = 24;
            _listStates.DrawItem += new DrawItemEventHandler(OnDrawStateItem);
            _listStates.SelectedIndexChanged += new EventHandler(OnStateListSelectedIndexChanged);
            page.Controls.Add(_listStates);

            int btnY = 392;
            int btnW = 38;
            int btnH = 26;
            int gap = 4;

            _btnAddState = MakeSmallButton("+", "添加状态", 8, btnY, btnW, btnH);
            _btnAddState.Click += new EventHandler(OnAddState);
            page.Controls.Add(_btnAddState);

            _btnCopyState = MakeSmallButton("⧉", "复制状态", 8 + (btnW + gap), btnY, btnW, btnH);
            _btnCopyState.Click += new EventHandler(OnCopyState);
            page.Controls.Add(_btnCopyState);

            _btnDeleteState = MakeSmallButton("×", "删除状态", 8 + (btnW + gap) * 2, btnY, btnW, btnH);
            _btnDeleteState.Click += new EventHandler(OnDeleteState);
            page.Controls.Add(_btnDeleteState);

            _btnSetAsDefault = MakeSmallButton("★", "设为初始", 8 + (btnW + gap) * 3, btnY, btnW, btnH);
            _btnSetAsDefault.Click += new EventHandler(OnSetAsDefault);
            page.Controls.Add(_btnSetAsDefault);

            // === 右侧：图形编辑器 ===
            int rightX = 190;

            // 第一行：状态名称 + 优先级 + 填充
            Label lblStateName = new Label();
            lblStateName.Text = "名称：";
            lblStateName.Location = new Point(rightX, 10);
            lblStateName.Size = new Size(38, 20);
            lblStateName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            page.Controls.Add(lblStateName);

            _txtStateName = new TextBox();
            _txtStateName.Location = new Point(rightX + 40, 8);
            _txtStateName.Size = new Size(100, 22);
            _txtStateName.TextChanged += new EventHandler(OnStateNameChanged);
            page.Controls.Add(_txtStateName);

            Label lblPriority = new Label();
            lblPriority.Text = "优先级：";
            lblPriority.Location = new Point(rightX + 148, 10);
            lblPriority.Size = new Size(48, 20);
            lblPriority.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            page.Controls.Add(lblPriority);

            _numPriority = new NumericUpDown();
            _numPriority.Location = new Point(rightX + 198, 8);
            _numPriority.Size = new Size(50, 22);
            _numPriority.Minimum = 0;
            _numPriority.Maximum = 100;
            _numPriority.ValueChanged += new EventHandler(OnPriorityChanged);
            page.Controls.Add(_numPriority);

            _chkFilled = new CheckBox();
            _chkFilled.Text = "填充";
            _chkFilled.Location = new Point(rightX + 255, 8);
            _chkFilled.Size = new Size(50, 22);
            _chkFilled.CheckedChanged += new EventHandler(OnFilledChanged);
            page.Controls.Add(_chkFilled);

            _chkCustomGeom = new CheckBox();
            _chkCustomGeom.Text = "自定义图形";
            _chkCustomGeom.Location = new Point(rightX + 310, 8);
            _chkCustomGeom.Size = new Size(95, 22);
            _chkCustomGeom.CheckedChanged += new EventHandler(OnCustomGeomChanged);
            page.Controls.Add(_chkCustomGeom);

            // 第二行：工具栏（小按钮）
            int toolbarY = 36;
            int tbX = rightX;
            int tbBtnW = 28;
            int tbBtnH = 24;
            int tbGap = 3;

            _btnAddVertex = MakeSmallButton("+V", "添加顶点", tbX, toolbarY, tbBtnW, tbBtnH);
            _btnAddVertex.Click += new EventHandler(OnAddVertex);
            page.Controls.Add(_btnAddVertex);
            tbX += tbBtnW + tbGap;

            _btnDeleteVertex = MakeSmallButton("-V", "删除选中顶点", tbX, toolbarY, tbBtnW, tbBtnH);
            _btnDeleteVertex.Click += new EventHandler(OnDeleteVertex);
            page.Controls.Add(_btnDeleteVertex);
            tbX += tbBtnW + tbGap;

            _btnClosePath = MakeSmallButton("○", "闭合/打开路径", tbX, toolbarY, tbBtnW, tbBtnH);
            _btnClosePath.Click += new EventHandler(OnToggleClosePath);
            page.Controls.Add(_btnClosePath);
            tbX += tbBtnW + tbGap;

            tbX += 6;

            _btnFillColor = MakeColorButton("填", "状态填充色", tbX, toolbarY, tbBtnW, tbBtnH);
            _btnFillColor.Click += new EventHandler(OnPickStateFillColor);
            page.Controls.Add(_btnFillColor);
            tbX += tbBtnW + tbGap;

            _btnBorderColor = MakeColorButton("边", "状态边框色", tbX, toolbarY, tbBtnW, tbBtnH);
            _btnBorderColor.Click += new EventHandler(OnPickStateBorderColor);
            page.Controls.Add(_btnBorderColor);
            tbX += tbBtnW + tbGap;

            _btnTextColor = MakeColorButton("字", "状态文字色", tbX, toolbarY, tbBtnW, tbBtnH);
            _btnTextColor.Click += new EventHandler(OnPickStateTextColor);
            page.Controls.Add(_btnTextColor);
            tbX += tbBtnW + tbGap;

            _btnHeaderColor = MakeColorButton("标", "状态标题色", tbX, toolbarY, tbBtnW, tbBtnH);
            _btnHeaderColor.Click += new EventHandler(OnPickStateHeaderColor);
            page.Controls.Add(_btnHeaderColor);
            tbX += tbBtnW + tbGap;

            tbX += 6;

            _btnCopyDefault = MakeSmallButton("CP", "复制初始图形", tbX, toolbarY, tbBtnW + 10, tbBtnH);
            _btnCopyDefault.Click += new EventHandler(OnCopyDefaultGeom);
            page.Controls.Add(_btnCopyDefault);
            tbX += tbBtnW + 10 + tbGap;

            _btnClearGeom = MakeSmallButton("CLR", "清除图形", tbX, toolbarY, tbBtnW + 10, tbBtnH);
            _btnClearGeom.Click += new EventHandler(OnClearGeom);
            page.Controls.Add(_btnClearGeom);
            tbX += tbBtnW + 10 + tbGap;

            _btnCircle = MakeSmallButton("○", "绘制圆形（4顶点贝塞尔近似）", tbX, toolbarY, tbBtnW, tbBtnH);
            _btnCircle.Click += new EventHandler(OnCreateCircle);
            page.Controls.Add(_btnCircle);
            tbX += tbBtnW + tbGap;

            // "是否为形状"复选框（默认勾选）：控制当前路径是填充形状还是线体
            _chkIsShape = new CheckBox();
            _chkIsShape.Text = "形状";
            _chkIsShape.Location = new Point(tbX, toolbarY + 2);
            _chkIsShape.Size = new Size(55, tbBtnH);
            _chkIsShape.Checked = true;
            _chkIsShape.CheckedChanged += new EventHandler(OnIsShapeChanged);
            page.Controls.Add(_chkIsShape);
            tbX += 55;

            // "预览"复选框：选中时在画布上呈现布尔运算后的结果
            _chkPreview = new CheckBox();
            _chkPreview.Text = "预览";
            _chkPreview.Location = new Point(tbX, toolbarY + 2);
            _chkPreview.Size = new Size(55, tbBtnH);
            _chkPreview.Checked = false;
            _chkPreview.CheckedChanged += new EventHandler(OnPreviewChanged);
            page.Controls.Add(_chkPreview);

            // 第三行：路径/区域工具栏（单行）
            BuildPathToolbar(page, rightX, 62);

            // 画布
            _canvasPanel = new Panel();
            _canvasPanel.Location = new Point(rightX, 90);
            _canvasPanel.Size = new Size(485, 258);
            _canvasPanel.BackColor = Color.White;
            _canvasPanel.BorderStyle = BorderStyle.FixedSingle;
            _canvasPanel.Paint += new PaintEventHandler(OnCanvasPaint);
            _canvasPanel.MouseDown += new MouseEventHandler(OnCanvasMouseDown);
            _canvasPanel.MouseMove += new MouseEventHandler(OnCanvasMouseMove);
            _canvasPanel.MouseUp += new MouseEventHandler(OnCanvasMouseUp);
            _canvasPanel.DoubleClick += new EventHandler(OnCanvasDoubleClick);
            page.Controls.Add(_canvasPanel);

            // 默认图形提示
            _lblDefaultHint = new Label();
            _lblDefaultHint.Text = "当前状态使用默认/初始图形。\n勾选\"自定义图形\"以绘制专属图形。";
            _lblDefaultHint.Location = new Point(rightX, 90);
            _lblDefaultHint.Size = new Size(485, 258);
            _lblDefaultHint.ForeColor = Color.FromArgb(120, 120, 120);
            _lblDefaultHint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            _lblDefaultHint.BorderStyle = BorderStyle.FixedSingle;
            _lblDefaultHint.BackColor = Color.FromArgb(245, 245, 245);
            page.Controls.Add(_lblDefaultHint);

            // 信息窗格
            BuildInfoPanel(page, rightX, 352);

            _tabControl.TabPages.Add(page);
        }

        /// <summary>
        /// 构建路径/区域工具栏（单行布局）。
        /// 路径下拉 + 上移/下移 + 新建/删除路径 + 布尔按钮(弹出菜单) + 添加/编辑/删除区域
        /// 布尔运算以按钮弹出菜单方式呈现，区域模式时禁用而非隐藏。
        /// </summary>
        private void BuildPathToolbar(TabPage page, int x, int y)
        {
            _panelPathToolbar = new Panel();
            _panelPathToolbar.Location = new Point(x, y);
            _panelPathToolbar.Size = new Size(485, 26);
            page.Controls.Add(_panelPathToolbar);

            // 路径/区域下拉（无标签，节省空间）
            _cmbPathSelect = new ComboBox();
            _cmbPathSelect.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbPathSelect.Location = new Point(0, 2);
            _cmbPathSelect.Size = new Size(140, 22);
            _cmbPathSelect.SelectedIndexChanged += new EventHandler(OnPathSelectedChanged);
            _panelPathToolbar.Controls.Add(_cmbPathSelect);

            int bx = 144;
            _btnMovePathUp = MakeSmallButton("↑", "上移（调整层叠顺序）", bx, 0, 24, 24);
            _btnMovePathUp.Click += new EventHandler(OnMovePathUp);
            _panelPathToolbar.Controls.Add(_btnMovePathUp);
            bx += 26;

            _btnMovePathDown = MakeSmallButton("↓", "下移（调整层叠顺序）", bx, 0, 24, 24);
            _btnMovePathDown.Click += new EventHandler(OnMovePathDown);
            _panelPathToolbar.Controls.Add(_btnMovePathDown);
            bx += 26;

            _btnNewPath = MakeSmallButton("+P", "新建路径", bx, 0, 28, 24);
            _btnNewPath.Click += new EventHandler(OnNewPath);
            _panelPathToolbar.Controls.Add(_btnNewPath);
            bx += 30;

            _btnDeletePath = MakeSmallButton("-P", "删除当前路径", bx, 0, 28, 24);
            _btnDeletePath.Click += new EventHandler(OnDeletePath);
            _panelPathToolbar.Controls.Add(_btnDeletePath);
            bx += 30;

            // 布尔运算按钮（弹出菜单选择）
            _btnBoolOp = MakeSmallButton("B", "布尔运算", bx, 0, 36, 24);
            _btnBoolOp.Click += new EventHandler(OnBoolOpButtonClick);
            _panelPathToolbar.Controls.Add(_btnBoolOp);
            bx += 38;

            // 合并按钮：将布尔运算结果扁平化为单一路径
            _btnMergePaths = MakeSmallButton("⊕→1", "合并（将布尔运算结果生成为单一新形状）", bx, 0, 40, 24);
            _btnMergePaths.Click += new EventHandler(OnMergePaths);
            _panelPathToolbar.Controls.Add(_btnMergePaths);
            bx += 42;

            // 区域按钮
            _btnAddZoneTB = MakeSmallButton("+Z", "添加区域", bx, 0, 28, 24);
            _btnAddZoneTB.Click += new EventHandler(OnAddZone);
            _panelPathToolbar.Controls.Add(_btnAddZoneTB);
            bx += 30;

            _btnEditZoneTB = MakeSmallButton("\u270E", "编辑选中区域", bx, 0, 24, 24);
            _btnEditZoneTB.Click += new EventHandler(OnEditZone);
            _panelPathToolbar.Controls.Add(_btnEditZoneTB);
            bx += 26;

            _btnDeleteZoneTB = MakeSmallButton("-Z", "删除选中区域", bx, 0, 24, 24);
            _btnDeleteZoneTB.Click += new EventHandler(OnDeleteZone);
            _panelPathToolbar.Controls.Add(_btnDeleteZoneTB);

            // 构建布尔运算弹出菜单
            _boolOpMenu = new ContextMenuStrip();
            _boolOpMenu.Items.Add("无 (独立)", null, new EventHandler(OnBoolOpMenuItem));
            _boolOpMenu.Items.Add("并集 (Union)", null, new EventHandler(OnBoolOpMenuItem));
            _boolOpMenu.Items.Add("差集 (Subtract)", null, new EventHandler(OnBoolOpMenuItem));
            _boolOpMenu.Items.Add("交集 (Intersect)", null, new EventHandler(OnBoolOpMenuItem));
            _boolOpMenu.Items.Add("异或 (Xor)", null, new EventHandler(OnBoolOpMenuItem));
        }

        /// <summary>布尔按钮点击：在按钮下方弹出运算菜单</summary>
        private void OnBoolOpButtonClick(object sender, EventArgs e)
        {
            _boolOpMenu.Show(_btnBoolOp, new Point(0, _btnBoolOp.Height));
        }

        /// <summary>布尔运算菜单项选择</summary>
        private void OnBoolOpMenuItem(object sender, EventArgs e)
        {
            ToolStripItem item = sender as ToolStripItem;
            if (item == null)
                return;
            int idx = _boolOpMenu.Items.IndexOf(item);
            if (idx < 0)
                return;
            BooleanOperation op = (BooleanOperation)idx;
            if (_currentPathIndex >= 0 && _currentPathIndex < _pathBoolOps.Count)
            {
                _pathBoolOps[_currentPathIndex] = op;
                UpdateBoolOpButtonText();
                _canvasPanel.Invalidate();
            }
        }

        /// <summary>更新布尔按钮显示当前运算类型</summary>
        private void UpdateBoolOpButtonText()
        {
            if (_btnBoolOp == null)
                return;
            BooleanOperation op = (_currentPathIndex >= 0 && _currentPathIndex < _pathBoolOps.Count)
                ? _pathBoolOps[_currentPathIndex] : BooleanOperation.None;
            switch (op)
            {
                case BooleanOperation.None: _btnBoolOp.Text = "B"; break;
                case BooleanOperation.Union: _btnBoolOp.Text = "B∪"; break;
                case BooleanOperation.Subtract: _btnBoolOp.Text = "B−"; break;
                case BooleanOperation.Intersect: _btnBoolOp.Text = "B∩"; break;
                case BooleanOperation.Xor: _btnBoolOp.Text = "B⊕"; break;
            }
            // 勾选当前菜单项
            int idx = (int)op;
            for (int i = 0; i < _boolOpMenu.Items.Count; i++)
            {
                ToolStripMenuItem mi = _boolOpMenu.Items[i] as ToolStripMenuItem;
                if (mi != null)
                    mi.Checked = (i == idx);
            }
        }

        /// <summary>
        /// 合并按钮：将当前路径与下方路径的布尔运算结果生成为单一新形状。
        /// 使用 Region 计算布尔结果，再通过位图轮廓追踪提取外轮廓多边形。
        /// </summary>
        private void OnMergePaths(object sender, EventArgs e)
        {
            if (_currentPathIndex < 0 || _currentPathIndex >= _paths.Count)
                return;
            List<CurveVertex> currentPath = _paths[_currentPathIndex];
            if (currentPath == null || currentPath.Count < 3)
                return;

            // 查找下方最近的非空路径
            int lowerIdx = -1;
            for (int j = _currentPathIndex + 1; j < _paths.Count; j++)
            {
                if (_paths[j] != null && _paths[j].Count >= 3)
                {
                    lowerIdx = j;
                    break;
                }
            }
            if (lowerIdx < 0)
                return; // 无下方非空路径

            // 获取当前路径的布尔运算类型，默认为并集
            BooleanOperation op = (_currentPathIndex < _pathBoolOps.Count)
                ? _pathBoolOps[_currentPathIndex] : BooleanOperation.None;
            if (op == BooleanOperation.None)
            {
                op = BooleanOperation.Union;
                if (_currentPathIndex < _pathBoolOps.Count)
                    _pathBoolOps[_currentPathIndex] = op;
            }

            // 查找整个布尔组的底部（基础路径）
            int groupBottom = lowerIdx;
            while (true)
            {
                BooleanOperation bottomOp = (groupBottom < _pathBoolOps.Count)
                    ? _pathBoolOps[groupBottom] : BooleanOperation.None;
                if (bottomOp == BooleanOperation.None)
                    break;
                int nextBelow = -1;
                for (int j = groupBottom + 1; j < _paths.Count; j++)
                {
                    if (_paths[j] != null && _paths[j].Count >= 3)
                    {
                        nextBelow = j;
                        break;
                    }
                }
                if (nextBelow < 0)
                    break;
                groupBottom = nextBelow;
            }

            // 收集组内所有有效路径索引（从底部到顶部，降序）
            List<int> groupPathIndices = new List<int>();
            for (int i = groupBottom; i >= _currentPathIndex; i--)
            {
                if (_paths[i] != null && _paths[i].Count >= 3)
                    groupPathIndices.Add(i);
            }
            if (groupPathIndices.Count < 2)
                return;

            // 构建 GraphicsPath 列表并计算边界
            List<GraphicsPath> gps = new List<GraphicsPath>();
            List<BooleanOperation> ops = new List<BooleanOperation>();
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (int idx in groupPathIndices)
            {
                List<CurveVertex> verts = _paths[idx];
                bool hasCurves = false;
                foreach (CurveVertex v in verts)
                    if (v.Handle != HandleType.None) { hasCurves = true; break; }
                gps.Add(BuildEditorPath(verts, true, hasCurves));

                BooleanOperation pathOp = (idx < _pathBoolOps.Count)
                    ? _pathBoolOps[idx] : BooleanOperation.None;
                ops.Add(pathOp);

                foreach (CurveVertex v in verts)
                {
                    minX = Math.Min(minX, v.Position.X);
                    minY = Math.Min(minY, v.Position.Y);
                    maxX = Math.Max(maxX, v.Position.X);
                    maxY = Math.Max(maxY, v.Position.Y);
                }
            }

            // 计算布尔运算结果 Region（从底部路径开始）
            Region resultRegion = new Region(gps[0]);
            for (int i = 1; i < gps.Count; i++)
            {
                BooleanOperation pathOp = ops[i];
                if (pathOp == BooleanOperation.None)
                    pathOp = BooleanOperation.Union;
                switch (pathOp)
                {
                    case BooleanOperation.Union:
                        resultRegion.Union(gps[i]);
                        break;
                    case BooleanOperation.Subtract:
                        resultRegion.Exclude(gps[i]);
                        break;
                    case BooleanOperation.Intersect:
                        resultRegion.Intersect(gps[i]);
                        break;
                    case BooleanOperation.Xor:
                        resultRegion.Xor(gps[i]);
                        break;
                }
            }

            // 追踪轮廓（添加额外边距以容纳曲线扩展）
            // 优先尝试曲线感知合并（保留贝塞尔句柄），失败则回退到位图追踪
            List<CurveVertex> mergedPath = null;

            // 准备曲线感知合并所需的路径和操作列表（按从顶到底的顺序）
            List<List<CurveVertex>> mergePaths = new List<List<CurveVertex>>();
            List<BooleanOperation> mergeOps = new List<BooleanOperation>();
            // groupPathIndices 是从底到顶的降序，需要反转为从顶到底
            for (int i = groupPathIndices.Count - 1; i >= 0; i--)
            {
                int idx = groupPathIndices[i];
                mergePaths.Add(_paths[idx]);
                BooleanOperation pathOp = (idx < _pathBoolOps.Count)
                    ? _pathBoolOps[idx] : BooleanOperation.None;
                mergeOps.Add(pathOp);
            }

            try
            {
                mergedPath = CurveAwareMerge.Merge(
                    mergePaths, mergeOps, resultRegion,
                    minX - 10, minY - 10, maxX + 10, maxY + 10);
            }
            catch { mergedPath = null; }

            // 曲线感知合并失败时回退到位图追踪
            if (mergedPath == null || mergedPath.Count < 3)
            {
                List<PointF> outline = null;
                try
                {
                    List<Region> regions = new List<Region>();
                    regions.Add(resultRegion);
                    outline = TraceRegionOutline(regions, minX - 10, minY - 10, maxX + 10, maxY + 10);
                }
                catch { }

                if (outline != null && outline.Count >= 3)
                {
                    mergedPath = new List<CurveVertex>();
                    foreach (PointF p in outline)
                        mergedPath.Add(new CurveVertex(p));
                }
            }

            // 清理资源
            resultRegion.Dispose();
            foreach (GraphicsPath gp in gps) gp.Dispose();

            if (mergedPath != null && mergedPath.Count >= 3)
            {
                // 从高索引到低索引移除组内路径
                groupPathIndices.Sort();
                for (int i = groupPathIndices.Count - 1; i >= 0; i--)
                {
                    int idx = groupPathIndices[i];
                    _paths.RemoveAt(idx);
                    _pathBoolOps.RemoveAt(idx);
                    _pathIsShape.RemoveAt(idx);
                }

                // 在组顶部原位置插入合并后的路径
                int insertIdx = groupPathIndices[0];
                _paths.Insert(insertIdx, mergedPath);
                _pathBoolOps.Insert(insertIdx, BooleanOperation.None);
                _pathIsShape.Insert(insertIdx, true);
                _currentPathIndex = insertIdx;
            }

            _selectedVertices.Clear();
            _dragIndex = -1;
            _closedPath = true;
            _filled = true;
            RefreshPathCombo();
            UpdateBoolOpButtonText();
            _canvasPanel.Invalidate();
        }

        /// <summary>
        /// 通过位图轮廓追踪从 Region 列表提取外轮廓多边形。
        /// 使用 Moore 边界追踪算法，然后简化多边形。
        /// </summary>
        private List<PointF> TraceRegionOutline(List<Region> regions, float minX, float minY, float maxX, float maxY)
        {
            int padding = 2;
            int w = (int)Math.Ceiling(maxX - minX) + padding * 2;
            int h = (int)Math.Ceiling(maxY - minY) + padding * 2;
            if (w <= 0 || h <= 0)
                return new List<PointF>();

            // 在位图上填充所有 Region
            using (Bitmap bmp = new Bitmap(w, h))
            using (Graphics bg = Graphics.FromImage(bmp))
            {
                bg.Clear(Color.White);
                bg.TranslateTransform(-minX + padding, -minY + padding);
                using (Brush brush = new SolidBrush(Color.Black))
                {
                    foreach (Region r in regions)
                        bg.FillRegion(brush, r);
                }

                // Moore 边界追踪
                List<Point> boundary = MooreBoundaryTrace(bmp);
                if (boundary.Count < 3)
                    return new List<PointF>();

                // 转换回原始坐标
                List<PointF> outline = new List<PointF>();
                foreach (Point p in boundary)
                    outline.Add(new PointF(p.X + minX - padding, p.Y + minY - padding));

                // 简化多边形（RDP 算法，容差 2.0 像素）
                outline = SimplifyPolygon(outline, 2.0f);

                return outline;
            }
        }

        /// <summary>Moore 边界追踪算法：从最上方的黑色像素开始顺时针追踪边界</summary>
        private List<Point> MooreBoundaryTrace(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;

            // 查找起始点：最上方的黑色像素
            int startX = -1, startY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (bmp.GetPixel(x, y).R < 128)
                    {
                        startX = x;
                        startY = y;
                        break;
                    }
                }
                if (startX >= 0)
                    break;
            }
            if (startX < 0)
                return new List<Point>();

            // Moore 邻域追踪
            List<Point> boundary = new List<Point>();
            Point current = new Point(startX, startY);
            Point backtrack = new Point(startX - 1, startY); // 左侧白像素

            // 8 方向顺时针：从 backtrack 方向开始
            int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
            int[] dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

            int maxSteps = w * h * 4;
            int steps = 0;

            do
            {
                boundary.Add(current);
                steps++;
                if (steps > maxSteps)
                    break;

                // 找到 backtrack 相对于 current 的方向
                int bdx = backtrack.X - current.X;
                int bdy = backtrack.Y - current.Y;
                int startDir = 0;
                for (int d = 0; d < 8; d++)
                {
                    if (dx[d] == bdx && dy[d] == bdy)
                    {
                        startDir = d;
                        break;
                    }
                }

                // 顺时针扫描 8 邻域，找到下一个黑色像素
                Point next = current;
                Point nextBacktrack = current;
                bool found = false;
                for (int i = 0; i < 8; i++)
                {
                    int dir = (startDir + i) % 8;
                    int nx = current.X + dx[dir];
                    int ny = current.Y + dy[dir];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                    {
                        nextBacktrack = new Point(nx, ny);
                        continue;
                    }
                    if (bmp.GetPixel(nx, ny).R < 128)
                    {
                        next = new Point(nx, ny);
                        found = true;
                        break;
                    }
                    nextBacktrack = new Point(nx, ny);
                }

                if (!found)
                    break;

                backtrack = nextBacktrack;
                current = next;
            }
            while (!(current.X == startX && current.Y == startY));

            return boundary;
        }

        /// <summary>简化多边形：使用 Ramer-Douglas-Peucker 算法移除冗余点</summary>
        private List<PointF> SimplifyPolygon(List<PointF> points, float tolerance)
        {
            if (points.Count <= 3)
                return points;

            // 先移除连续重复点
            List<PointF> cleaned = new List<PointF>();
            foreach (PointF p in points)
            {
                if (cleaned.Count == 0)
                {
                    cleaned.Add(p);
                }
                else
                {
                    PointF last = cleaned[cleaned.Count - 1];
                    float dx = p.X - last.X;
                    float dy = p.Y - last.Y;
                    if (dx * dx + dy * dy > 0.5f)
                        cleaned.Add(p);
                }
            }
            if (cleaned.Count <= 3)
                return cleaned;

            // RDP 简化闭环：对每条边分别应用 RDP，然后合并
            // 将闭环拆分为两半分别在首尾点处断开
            PointF anchor = cleaned[0];
            PointF farthest = cleaned[cleaned.Count / 2];

            // 从 anchor 到 farthest 简化前半段
            List<PointF> firstHalf = new List<PointF>();
            for (int i = 0; i <= cleaned.Count / 2; i++)
                firstHalf.Add(cleaned[i]);
            List<PointF> simplifiedFirst = RdpSimplify(firstHalf, tolerance);

            // 从 farthest 到 anchor 简化后半段
            List<PointF> secondHalf = new List<PointF>();
            for (int i = cleaned.Count / 2; i < cleaned.Count; i++)
                secondHalf.Add(cleaned[i]);
            secondHalf.Add(anchor);
            List<PointF> simplifiedSecond = RdpSimplify(secondHalf, tolerance);

            // 合并：前半段（去掉末尾）+ 后半段（去掉末尾，因为末尾=anchor=首）
            List<PointF> result = new List<PointF>();
            for (int i = 0; i < simplifiedFirst.Count - 1; i++)
                result.Add(simplifiedFirst[i]);
            for (int i = 1; i < simplifiedSecond.Count - 1; i++)
                result.Add(simplifiedSecond[i]);

            return result.Count >= 3 ? result : cleaned;
        }

        /// <summary>Ramer-Douglas-Peucker 算法：递归简化折线</summary>
        private List<PointF> RdpSimplify(List<PointF> points, float tolerance)
        {
            if (points.Count <= 2)
                return new List<PointF>(points);

            float tolSq = tolerance * tolerance;

            // 找到距离首尾连线最远的点
            PointF first = points[0];
            PointF last = points[points.Count - 1];
            float maxDistSq = 0;
            int maxIdx = 0;

            for (int i = 1; i < points.Count - 1; i++)
            {
                float dSq = PointLineDistanceSq(points[i], first, last);
                if (dSq > maxDistSq)
                {
                    maxDistSq = dSq;
                    maxIdx = i;
                }
            }

            List<PointF> result = new List<PointF>();

            if (maxDistSq > tolSq)
            {
                // 递归简化两段
                List<PointF> left = new List<PointF>();
                for (int i = 0; i <= maxIdx; i++)
                    left.Add(points[i]);
                List<PointF> simplifiedLeft = RdpSimplify(left, tolerance);

                List<PointF> right = new List<PointF>();
                for (int i = maxIdx; i < points.Count; i++)
                    right.Add(points[i]);
                List<PointF> simplifiedRight = RdpSimplify(right, tolerance);

                // 合并（去掉中间重复点）
                for (int i = 0; i < simplifiedLeft.Count - 1; i++)
                    result.Add(simplifiedLeft[i]);
                for (int i = 0; i < simplifiedRight.Count; i++)
                    result.Add(simplifiedRight[i]);
            }
            else
            {
                // 所有点都在容差范围内，只保留首尾
                result.Add(first);
                result.Add(last);
            }

            return result;
        }

        /// <summary>点到线段的垂直距离的平方</summary>
        private static float PointLineDistanceSq(PointF p, PointF a, PointF b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float lenSq = dx * dx + dy * dy;
            if (lenSq < 0.0001f)
            {
                // a 和 b 重合，返回到 a 的距离
                float ddx = p.X - a.X;
                float ddy = p.Y - a.Y;
                return ddx * ddx + ddy * ddy;
            }
            // 投影参数 t
            float t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq;
            t = Math.Max(0f, Math.Min(1f, t)); // 钳制到 [0,1]
            float projX = a.X + t * dx;
            float projY = a.Y + t * dy;
            float ex = p.X - projX;
            float ey = p.Y - projY;
            return ex * ex + ey * ey;
        }

        /// <summary>构建底部信息窗格，提供上下文相关提示并自动轮播</summary>
        private void BuildInfoPanel(TabPage page, int x, int y)
        {
            _panelInfo = new Panel();
            _panelInfo.Location = new Point(x, y);
            _panelInfo.Size = new Size(485, 80);
            _panelInfo.BorderStyle = BorderStyle.FixedSingle;
            _panelInfo.BackColor = Color.FromArgb(248, 250, 252);
            page.Controls.Add(_panelInfo);

            // 标题
            Label lblInfoTitle = new Label();
            lblInfoTitle.Text = " 说明";
            lblInfoTitle.Location = new Point(0, 0);
            lblInfoTitle.Size = new Size(485, 22);
            lblInfoTitle.BackColor = Color.FromArgb(230, 235, 240);
            lblInfoTitle.Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold);
            _panelInfo.Controls.Add(lblInfoTitle);

            _lblInfo = new Label();
            _lblInfo.Location = new Point(8, 26);
            _lblInfo.Size = new Size(469, 48);
            _lblInfo.Text = _infoTips[0];
            _lblInfo.Font = new Font("Microsoft YaHei", 9f);
            _lblInfo.ForeColor = Color.FromArgb(80, 80, 80);
            _lblInfo.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            _panelInfo.Controls.Add(_lblInfo);
        }

        /// <summary>启动信息窗格轮播定时器</summary>
        private void StartInfoTimer()
        {
            _infoTimer = new Timer();
            _infoTimer.Interval = 5000;
            _infoTimer.Tick += new EventHandler(OnInfoTimerTick);
            _infoTimer.Start();
        }

        private void OnInfoTimerTick(object sender, EventArgs e)
        {
            _infoTipIndex = (_infoTipIndex + 1) % _infoTips.Length;
            if (_lblInfo != null)
                _lblInfo.Text = _infoTips[_infoTipIndex];
        }

        private Button MakeSmallButton(string text, string tooltip, int x, int y, int w, int h)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Location = new Point(x, y);
            btn.Size = new Size(w, h);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.Font = new Font("Segoe UI", 8f);
            toolTip1.SetToolTip(btn, tooltip);
            return btn;
        }

        private Button MakeColorButton(string text, string tooltip, int x, int y, int w, int h)
        {
            Button btn = MakeSmallButton(text, tooltip, x, y, w, h);
            btn.BackColor = Color.LightGray;
            return btn;
        }

        private void BuildActionTab()
        {
            TabPage page = new TabPage("行为");
            page.BorderStyle = BorderStyle.None;

            _listActions = new ListBox();
            _listActions.Location = new Point(10, 10);
            _listActions.Size = new Size(460, 400);
            _listActions.BorderStyle = BorderStyle.FixedSingle;
            _listActions.SelectedIndexChanged += new EventHandler(OnActionListSelectedIndexChanged);
            page.Controls.Add(_listActions);

            int x = 485;
            int y = 10;

            _btnAddAction = new Button();
            _btnAddAction.Text = "添加...";
            _btnAddAction.Location = new Point(x, y);
            _btnAddAction.Size = new Size(110, 28);
            _btnAddAction.Click += new EventHandler(OnAddAction);
            page.Controls.Add(_btnAddAction);
            y += 34;

            _btnEditAction = new Button();
            _btnEditAction.Text = "编辑...";
            _btnEditAction.Location = new Point(x, y);
            _btnEditAction.Size = new Size(110, 28);
            _btnEditAction.Click += new EventHandler(OnEditAction);
            page.Controls.Add(_btnEditAction);
            y += 34;

            _btnDeleteAction = new Button();
            _btnDeleteAction.Text = "删除";
            _btnDeleteAction.Location = new Point(x, y);
            _btnDeleteAction.Size = new Size(110, 28);
            _btnDeleteAction.Click += new EventHandler(OnDeleteAction);
            page.Controls.Add(_btnDeleteAction);
            y += 44;

            Label lblHint = new Label();
            lblHint.Text = "提示：\n行为定义图形在右键\n菜单中可用的操作。\n系统行为由区域自动\n注册，不可删除。\n切换状态时可枚举\n选择已定义目标状态。";
            lblHint.Location = new Point(x, y);
            lblHint.Size = new Size(110, 120);
            lblHint.ForeColor = Color.FromArgb(100, 100, 100);
            page.Controls.Add(lblHint);

            _tabControl.TabPages.Add(page);
        }

        #endregion

        #region 数据加载

        private void LoadFromShapeType(ShapeType st)
        {
            _txtName.Text = st.Name;

            List<RenderCommand> defaultCmds = CloneRenderCommands(st.RenderCommands);
            bool hasStates = (st.DefaultStates != null && st.DefaultStates.Count > 0);
            bool defaultStateExists = false;

            if (hasStates)
            {
                foreach (ShapeState state in st.DefaultStates)
                {
                    ShapeState copy = CloneShapeState(state);
                    _states.Add(copy);
                    if (copy.Name == DefaultStateName)
                        defaultStateExists = true;
                }
            }

            if (!defaultStateExists)
            {
                ShapeState defaultState = new ShapeState();
                defaultState.Name = DefaultStateName;
                defaultState.UseCustomRenderCommands = true;
                defaultState.CustomRenderCommands = (defaultCmds != null && defaultCmds.Count > 0) ? defaultCmds : BuildDefaultTriangleCommands();
                defaultState.FillColor = st.DefaultFillColor;
                defaultState.BorderColor = st.DefaultBorderColor;
                defaultState.TextColor = st.DefaultTextColor;
                _states.Insert(0, defaultState);
            }

            RefreshStateList();

            // 加载自定义行为（排除系统行为，系统行为由 Zone 自动生成）
            if (st.CustomActions != null)
            {
                foreach (ShapeAction action in st.CustomActions)
                {
                    if (action.IsSystemBehavior)
                        continue; // 系统行为在保存时自动生成，不加载到编辑列表
                    ShapeAction copy = action.Clone();
                    _actions.Add(copy);
                }
                RefreshActionList();
            }
        }

        private ShapeState CloneShapeState(ShapeState src)
        {
            ShapeState copy = new ShapeState();
            copy.Name = src.Name;
            copy.FillColor = new XmlColor(src.FillColor.ToColor());
            copy.BorderColor = new XmlColor(src.BorderColor.ToColor());
            copy.TextColor = new XmlColor(src.TextColor.ToColor());
            copy.HeaderColor = new XmlColor(src.HeaderColor.ToColor());
            copy.Priority = src.Priority;
            copy.UseCustomRenderCommands = src.UseCustomRenderCommands;
            copy.CustomRenderCommands = CloneRenderCommands(src.CustomRenderCommands);
            copy.CustomZones = CloneShapeZones(src.CustomZones);
            return copy;
        }

        private List<RenderCommand> CloneRenderCommands(List<RenderCommand> src)
        {
            if (src == null)
                return new List<RenderCommand>();
            List<RenderCommand> result = new List<RenderCommand>();
            foreach (RenderCommand rc in src)
            {
                RenderCommand rcCopy = new RenderCommand();
                rcCopy.CommandType = rc.CommandType;
                rcCopy.X = rc.X; rcCopy.Y = rc.Y;
                rcCopy.Width = rc.Width; rcCopy.Height = rc.Height;
                rcCopy.CornerRadius = rc.CornerRadius;
                rcCopy.FillColor = new XmlColor(rc.FillColor.ToColor());
                rcCopy.StrokeColor = new XmlColor(rc.StrokeColor.ToColor());
                rcCopy.StrokeWidth = rc.StrokeWidth;
                rcCopy.Text = rc.Text;
                rcCopy.TextAlign = rc.TextAlign;
                rcCopy.FontSize = rc.FontSize;
                rcCopy.IsBold = rc.IsBold;
                if (rc.PolygonPoints != null)
                {
                    rcCopy.PolygonPoints = new PointF[rc.PolygonPoints.Length];
                    for (int i = 0; i < rc.PolygonPoints.Length; i++)
                        rcCopy.PolygonPoints[i] = rc.PolygonPoints[i];
                }
                rcCopy.UseShapeColors = rc.UseShapeColors;
                rcCopy.Fill = rc.Fill;
                rcCopy.Stroke = rc.Stroke;
                // 多路径与布尔运算
                rcCopy.BoolOp = rc.BoolOp;
                if (rc.MultiPaths != null)
                {
                    rcCopy.MultiPaths = new List<PointF[]>();
                    foreach (PointF[] path in rc.MultiPaths)
                    {
                        if (path != null)
                        {
                            PointF[] pathCopy = new PointF[path.Length];
                            for (int i = 0; i < path.Length; i++)
                                pathCopy[i] = path[i];
                            rcCopy.MultiPaths.Add(pathCopy);
                        }
                    }
                }
                // PathDefs（逐路径布尔运算）
                if (rc.PathDefs != null)
                {
                    rcCopy.PathDefs = new List<PathDef>();
                    foreach (PathDef pd in rc.PathDefs)
                        rcCopy.PathDefs.Add(pd.Clone());
                }
                result.Add(rcCopy);
            }
            return result;
        }

        private List<RenderCommand> GetDefaultRenderCommands()
        {
            foreach (ShapeState s in _states)
            {
                if (s.Name == DefaultStateName && s.UseCustomRenderCommands
                    && s.CustomRenderCommands != null && s.CustomRenderCommands.Count > 0)
                    return CloneRenderCommands(s.CustomRenderCommands);
            }
            foreach (ShapeState s in _states)
            {
                if (s.UseCustomRenderCommands && s.CustomRenderCommands != null && s.CustomRenderCommands.Count > 0)
                    return CloneRenderCommands(s.CustomRenderCommands);
            }
            return BuildDefaultTriangleCommands();
        }

        #endregion

        #region 状态列表 — 内联编辑

        private void RefreshStateList()
        {
            _suppressStateChange = true;
            try
            {
                int prevSel = _listStates.SelectedIndex;
                _listStates.Items.Clear();
                foreach (ShapeState state in _states)
                    _listStates.Items.Add(state.Name);
                if (prevSel >= 0 && prevSel < _listStates.Items.Count)
                    _listStates.SelectedIndex = prevSel;
                else if (_listStates.Items.Count > 0)
                    _listStates.SelectedIndex = 0;
            }
            finally
            {
                _suppressStateChange = false;
            }
            UpdateStateButtons();
        }

        private void OnStateListSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressStateChange)
                return;

            int oldIdx = _editingStateIndex;
            int newIdx = _listStates.SelectedIndex;

            if (oldIdx != newIdx && oldIdx >= 0 && oldIdx < _states.Count)
                SaveCurrentEditorToState();

            _editingStateIndex = newIdx;
            LoadStateToEditor(newIdx);
            UpdateStateButtons();
        }

        private void SaveCurrentEditorToState()
        {
            if (_editingStateIndex < 0 || _editingStateIndex >= _states.Count)
                return;

            ShapeState s = _states[_editingStateIndex];

            if (s.Name != DefaultStateName)
                s.Name = string.IsNullOrEmpty(_txtStateName.Text.Trim()) ? "State" : _txtStateName.Text.Trim();

            s.Priority = (int)_numPriority.Value;
            s.FillColor = new XmlColor(_stateFillColor);
            s.BorderColor = new XmlColor(_stateBorderColor);
            s.TextColor = new XmlColor(_stateTextColor);
            s.HeaderColor = new XmlColor(_stateHeaderColor);

            if (_chkCustomGeom.Checked)
            {
                List<RenderCommand> cmds = BuildRenderCommandsFromVertices();
                if (cmds != null)
                {
                    s.UseCustomRenderCommands = true;
                    s.CustomRenderCommands = cmds;
                }
                else
                {
                    s.UseCustomRenderCommands = false;
                    s.CustomRenderCommands = new List<RenderCommand>();
                }
            }
            else
            {
                s.UseCustomRenderCommands = false;
                s.CustomRenderCommands = new List<RenderCommand>();
            }

            s.CustomZones = CloneShapeZones(_zones);
        }

        private void LoadStateToEditor(int idx)
        {
            if (idx < 0 || idx >= _states.Count)
            {
                _txtStateName.Text = "";
                _numPriority.Value = 0;
                _chkFilled.Checked = true;
                _chkCustomGeom.Checked = false;
                _paths.Clear();
                _paths.Add(new List<CurveVertex>());
                _pathBoolOps.Clear();
                _pathBoolOps.Add(BooleanOperation.None);
                _pathIsShape.Clear();
                _pathIsShape.Add(true);
                _currentPathIndex = 0;
                _selectedVertices.Clear();
                _dragIndex = -1;
                _selectedZoneIndex = -1;
                _zones.Clear();
                RefreshPathCombo();
                UpdateGeometryVisibility();
                return;
            }

            ShapeState s = _states[idx];

            _txtStateName.Text = s.Name;
            _numPriority.Value = s.Priority;
            _stateFillColor = s.FillColor.ToColor();
            _stateBorderColor = s.BorderColor.ToColor();
            _stateTextColor = s.TextColor.ToColor();
            _stateHeaderColor = s.HeaderColor.ToColor();

            bool hasCustom = s.UseCustomRenderCommands
                && s.CustomRenderCommands != null && s.CustomRenderCommands.Count > 0;
            _chkCustomGeom.Checked = hasCustom;

            if (hasCustom)
                LoadVerticesFromCommands(s.CustomRenderCommands);
            else
            {
                _paths.Clear();
                _paths.Add(new List<CurveVertex>());
                _pathBoolOps.Clear();
                _pathBoolOps.Add(BooleanOperation.None);
                _pathIsShape.Clear();
                _pathIsShape.Add(true);
                _currentPathIndex = 0;
                _selectedVertices.Clear();
                _dragIndex = -1;
                RefreshPathCombo();
            }

            if (hasCustom)
            {
                RenderCommand geomCmd = null;
                foreach (RenderCommand cmd in s.CustomRenderCommands)
                {
                    if (cmd.CommandType == RenderCommandType.Polygon
                        || cmd.CommandType == RenderCommandType.CompoundPolygon)
                    { geomCmd = cmd; break; }
                }
                if (geomCmd != null)
                {
                    _geomFillColor = geomCmd.FillColor;
                    _geomBorderColor = geomCmd.StrokeColor;
                    _filled = geomCmd.Fill;
                    _chkFilled.Checked = _filled;
                    _stateFillColor = _geomFillColor;
                    _stateBorderColor = _geomBorderColor;
                }
            }

            _zones = CloneShapeZones(s.CustomZones);
            _selectedZoneIndex = -1;
            RefreshPathCombo();

            UpdateColorButtons();
            UpdateGeometryVisibility();
            _canvasPanel.Invalidate();
        }

        private void UpdateStateButtons()
        {
            bool hasSelection = (_listStates.SelectedIndex >= 0 && _listStates.SelectedIndex < _states.Count);
            _btnCopyState.Enabled = hasSelection;
            _btnDeleteState.Enabled = hasSelection && _states.Count > 1;
            _btnSetAsDefault.Enabled = hasSelection;
        }

        private void OnDrawStateItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _states.Count)
                return;

            e.DrawBackground();
            ShapeState state = _states[e.Index];

            Rectangle colorRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 3, 20, e.Bounds.Height - 6);
            using (Brush brush = new SolidBrush(state.FillColor.ToColor()))
            using (Pen pen = new Pen(state.BorderColor.ToColor(), 1))
            {
                e.Graphics.FillRectangle(brush, colorRect);
                e.Graphics.DrawRectangle(pen, colorRect);
            }

            string display = state.Name;
            if (state.Name == DefaultStateName)
                display += " [初始]";
            if (state.UseCustomRenderCommands && state.CustomRenderCommands != null && state.CustomRenderCommands.Count > 0)
                display += " *";
            using (Brush textBrush = new SolidBrush(e.ForeColor))
            {
                e.Graphics.DrawString(display, e.Font, textBrush,
                    e.Bounds.X + 30, e.Bounds.Y + 2);
            }
            e.DrawFocusRectangle();
        }

        private void OnAddState(object sender, EventArgs e)
        {
            SaveCurrentEditorToState();

            ShapeState newState = new ShapeState();
            newState.Name = "State" + (_states.Count + 1);
            newState.UseCustomRenderCommands = false;
            newState.CustomRenderCommands = new List<RenderCommand>();
            _states.Add(newState);
            RefreshStateList();

            int newIdx = _states.Count - 1;
            _suppressStateChange = true;
            _listStates.SelectedIndex = newIdx;
            _suppressStateChange = false;
            _editingStateIndex = newIdx;
            LoadStateToEditor(newIdx);
            UpdateStateButtons();
        }

        private void OnCopyState(object sender, EventArgs e)
        {
            int idx = _listStates.SelectedIndex;
            if (idx < 0 || idx >= _states.Count)
                return;

            SaveCurrentEditorToState();
            ShapeState copy = CloneShapeState(_states[idx]);
            copy.Name = _states[idx].Name + "_副本";
            _states.Add(copy);
            RefreshStateList();

            int newIdx = _states.Count - 1;
            _suppressStateChange = true;
            _listStates.SelectedIndex = newIdx;
            _suppressStateChange = false;
            _editingStateIndex = newIdx;
            LoadStateToEditor(newIdx);
            UpdateStateButtons();
        }

        private void OnSetAsDefault(object sender, EventArgs e)
        {
            int idx = _listStates.SelectedIndex;
            if (idx < 0 || idx >= _states.Count)
                return;

            SaveCurrentEditorToState();

            if (_states[idx].Name == DefaultStateName)
            {
                MessageBox.Show("该状态已经是初始状态。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            for (int i = 0; i < _states.Count; i++)
            {
                if (_states[i].Name == DefaultStateName)
                {
                    _states[i].Name = _states[idx].Name;
                    break;
                }
            }
            _states[idx].Name = DefaultStateName;

            ShapeState sel = _states[idx];
            _states.RemoveAt(idx);
            _states.Insert(0, sel);

            RefreshStateList();

            _suppressStateChange = true;
            _listStates.SelectedIndex = 0;
            _suppressStateChange = false;
            _editingStateIndex = 0;
            LoadStateToEditor(0);
            UpdateStateButtons();
        }

        private void OnDeleteState(object sender, EventArgs e)
        {
            int idx = _listStates.SelectedIndex;
            if (idx < 0 || idx >= _states.Count)
                return;

            if (_states.Count <= 1)
            {
                MessageBox.Show("至少需要保留一个状态。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_states[idx].Name == DefaultStateName)
            {
                MessageBox.Show("初始状态不可删除，可先将其余状态设为初始后再删除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(string.Format("确定删除状态 \"{0}\"？", _states[idx].Name),
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _states.RemoveAt(idx);
                _editingStateIndex = -1;
                RefreshStateList();
                if (_listStates.Items.Count > 0)
                {
                    int newIdx = Math.Min(idx, _listStates.Items.Count - 1);
                    _suppressStateChange = true;
                    _listStates.SelectedIndex = newIdx;
                    _suppressStateChange = false;
                    _editingStateIndex = newIdx;
                    LoadStateToEditor(newIdx);
                }
                UpdateStateButtons();
            }
        }

        #endregion

        #region 编辑器 — 属性变更

        private void OnStateNameChanged(object sender, EventArgs e)
        {
            if (_editingStateIndex < 0 || _editingStateIndex >= _states.Count)
                return;
            if (_states[_editingStateIndex].Name == DefaultStateName)
                return;
            _states[_editingStateIndex].Name = string.IsNullOrEmpty(_txtStateName.Text.Trim())
                ? "State" : _txtStateName.Text.Trim();
            _listStates.Items[_editingStateIndex] = _states[_editingStateIndex].Name;
        }

        private void OnPriorityChanged(object sender, EventArgs e)
        {
            if (_editingStateIndex < 0 || _editingStateIndex >= _states.Count)
                return;
            _states[_editingStateIndex].Priority = (int)_numPriority.Value;
        }

        private void OnFilledChanged(object sender, EventArgs e)
        {
            _filled = _chkFilled.Checked;
            _canvasPanel.Invalidate();
        }

        private void OnCustomGeomChanged(object sender, EventArgs e)
        {
            UpdateGeometryVisibility();
        }

        private void UpdateGeometryVisibility()
        {
            bool visible = _chkCustomGeom.Checked;
            _canvasPanel.Visible = visible;
            _btnAddVertex.Visible = visible;
            _btnDeleteVertex.Visible = visible;
            _btnClosePath.Visible = visible;
            _btnCopyDefault.Visible = visible;
            _btnClearGeom.Visible = visible;
            _btnCircle.Visible = visible;
            _chkIsShape.Visible = visible;
            _chkFilled.Visible = visible;
            _panelPathToolbar.Visible = visible;
            _lblDefaultHint.Visible = !visible;
        }

        #endregion

        #region 编辑器 — 图形加载与构建

        private static PointF[] ToPointArray(List<CurveVertex> path)
        {
            PointF[] result = new PointF[path.Count];
            for (int i = 0; i < path.Count; i++)
                result[i] = path[i].Position;
            return result;
        }

        private List<CurveVertex> CurrentPath()
        {
            if (_paths.Count == 0)
            {
                _paths.Add(new List<CurveVertex>());
                _pathBoolOps.Add(BooleanOperation.None);
                _pathIsShape.Add(true);
            }
            if (_currentPathIndex < 0 || _currentPathIndex >= _paths.Count)
                _currentPathIndex = 0;
            return _paths[_currentPathIndex];
        }

        /// <summary>
        /// 从 RenderCommand 列表加载顶点到编辑器。
        /// 优先使用 PathDefs（逐路径布尔运算），回退到 MultiPaths（全局布尔运算），
        /// 再回退到普通 Polygon。
        /// </summary>
        private void LoadVerticesFromCommands(List<RenderCommand> cmds)
        {
            _paths.Clear();
            _pathBoolOps.Clear();
            _pathIsShape.Clear();
            _currentPathIndex = 0;
            _closedPath = false;
            _selectedVertices.Clear();
            _dragIndex = -1;

            if (cmds == null || cmds.Count == 0)
            {
                _paths.Add(new List<CurveVertex>());
                _pathBoolOps.Add(BooleanOperation.None);
                _pathIsShape.Add(true);
                RefreshPathCombo();
                return;
            }

            float canvasW = (float)_canvasPanel.Width - 40f;
            float canvasH = (float)_canvasPanel.Height - 40f;
            float offsetX = 20f;
            float offsetY = 20f;

            // 优先查找 CompoundPolygon
            RenderCommand compoundCmd = null;
            foreach (RenderCommand cmd in cmds)
            {
                if (cmd.CommandType == RenderCommandType.CompoundPolygon)
                { compoundCmd = cmd; break; }
            }

            // 1. 优先从 PathDefs 加载（逐路径布尔运算）
            if (compoundCmd != null && compoundCmd.PathDefs != null && compoundCmd.PathDefs.Count > 0)
            {
                foreach (PathDef pd in compoundCmd.PathDefs)
                {
                    List<CurveVertex> path = new List<CurveVertex>();
                    if (pd.Points != null)
                    {
                        for (int i = 0; i < pd.Points.Length; i++)
                        {
                            PointF pt = pd.Points[i];
                            CurveVertex v = new CurveVertex(new PointF(offsetX + pt.X * canvasW, offsetY + pt.Y * canvasH));
                            // 加载句柄数据（归一化→像素）
                            if (pd.HandleTypes != null && i < pd.HandleTypes.Length && pd.HandleTypes[i] != HandleType.None)
                            {
                                v.Handle = pd.HandleTypes[i];
                                v.HandleIn = new PointF(pd.HandleIns[i].X * canvasW, pd.HandleIns[i].Y * canvasH);
                                v.HandleOut = new PointF(pd.HandleOuts[i].X * canvasW, pd.HandleOuts[i].Y * canvasH);
                            }
                            path.Add(v);
                        }
                    }
                    _paths.Add(path);
                    _pathBoolOps.Add(pd.BoolOp);
                    _pathIsShape.Add(pd.IsShape);
                }
                if (_paths.Count > 0)
                    _closedPath = true;
                _geomFillColor = compoundCmd.FillColor;
                _geomBorderColor = compoundCmd.StrokeColor;
                _filled = compoundCmd.Fill;
                RefreshPathCombo();
                return;
            }

            // 2. 回退到 MultiPaths（全局布尔运算，向后兼容）
            if (compoundCmd != null && compoundCmd.MultiPaths != null && compoundCmd.MultiPaths.Count > 0)
            {
                foreach (PointF[] pts in compoundCmd.MultiPaths)
                {
                    List<CurveVertex> path = new List<CurveVertex>();
                    if (pts != null)
                    {
                        foreach (PointF pt in pts)
                            path.Add(new PointF(offsetX + pt.X * canvasW, offsetY + pt.Y * canvasH));
                    }
                    _paths.Add(path);
                    _pathBoolOps.Add(compoundCmd.BoolOp);
                    _pathIsShape.Add(true);
                }
                if (_paths.Count > 0)
                    _closedPath = true;
                _geomFillColor = compoundCmd.FillColor;
                _geomBorderColor = compoundCmd.StrokeColor;
                _filled = compoundCmd.Fill;
                RefreshPathCombo();
                return;
            }

            // 3. 回退到普通 Polygon
            RenderCommand polyCmd = null;
            foreach (RenderCommand cmd in cmds)
            {
                if (cmd.CommandType == RenderCommandType.Polygon)
                { polyCmd = cmd; break; }
            }

            if (polyCmd != null && polyCmd.PolygonPoints != null && polyCmd.PolygonPoints.Length >= 3)
            {
                List<CurveVertex> path = new List<CurveVertex>();
                for (int i = 0; i < polyCmd.PolygonPoints.Length; i++)
                {
                    PointF pt = polyCmd.PolygonPoints[i];
                    CurveVertex v = new CurveVertex(new PointF(offsetX + pt.X * canvasW, offsetY + pt.Y * canvasH));
                    // 加载句柄数据（归一化→像素）
                    if (polyCmd.PolyHandleTypes != null && i < polyCmd.PolyHandleTypes.Length && polyCmd.PolyHandleTypes[i] != HandleType.None)
                    {
                        v.Handle = polyCmd.PolyHandleTypes[i];
                        v.HandleIn = new PointF(polyCmd.PolyHandleIns[i].X * canvasW, polyCmd.PolyHandleIns[i].Y * canvasH);
                        v.HandleOut = new PointF(polyCmd.PolyHandleOuts[i].X * canvasW, polyCmd.PolyHandleOuts[i].Y * canvasH);
                    }
                    path.Add(v);
                }
                _paths.Add(path);
                _pathBoolOps.Add(BooleanOperation.None);
                _pathIsShape.Add(polyCmd.Fill);
                _closedPath = true;
                _geomFillColor = polyCmd.FillColor;
                _geomBorderColor = polyCmd.StrokeColor;
                _filled = polyCmd.Fill;
            }
            else
            {
                _paths.Add(new List<CurveVertex>());
                _pathBoolOps.Add(BooleanOperation.None);
                _pathIsShape.Add(true);
            }
            RefreshPathCombo();
        }

        /// <summary>
        /// 从编辑器顶点构建 RenderCommand 列表。
        /// 多路径时使用 PathDefs（逐路径布尔运算），单路径使用普通 Polygon。
        /// </summary>
        private List<RenderCommand> BuildRenderCommandsFromVertices()
        {
            List<List<CurveVertex>> validPaths = new List<List<CurveVertex>>();
            List<BooleanOperation> validOps = new List<BooleanOperation>();
            List<bool> validIsShape = new List<bool>();
            for (int i = 0; i < _paths.Count; i++)
            {
                if (_paths[i] != null && _paths[i].Count >= 2)
                {
                    validPaths.Add(_paths[i]);
                    validOps.Add(i < _pathBoolOps.Count ? _pathBoolOps[i] : BooleanOperation.None);
                    validIsShape.Add(i < _pathIsShape.Count ? _pathIsShape[i] : true);
                }
            }
            if (validPaths.Count == 0)
                return null;

            // 全局包围盒归一化
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (List<CurveVertex> path in validPaths)
            {
                foreach (CurveVertex v in path)
                {
                    PointF pt = v.Position;
                    if (pt.X < minX) minX = pt.X;
                    if (pt.Y < minY) minY = pt.Y;
                    if (pt.X > maxX) maxX = pt.X;
                    if (pt.Y > maxY) maxY = pt.Y;
                }
            }
            float rangeX = maxX - minX;
            float rangeY = maxY - minY;
            if (rangeX < 1) rangeX = 1;
            if (rangeY < 1) rangeY = 1;

            // 单路径：普通 Polygon
            if (validPaths.Count == 1)
            {
                List<CurveVertex> path = validPaths[0];
                PointF[] normalized = new PointF[path.Count];
                for (int i = 0; i < path.Count; i++)
                    normalized[i] = new PointF((path[i].Position.X - minX) / rangeX, (path[i].Position.Y - minY) / rangeY);

                RenderCommand polyCmd = new RenderCommand();
                polyCmd.CommandType = RenderCommandType.Polygon;
                polyCmd.PolygonPoints = normalized;
                // 提取句柄数据（贝塞尔曲线控制点，像素→归一化）
                HandleType[] handleTypes = new HandleType[path.Count];
                PointF[] handleIns = new PointF[path.Count];
                PointF[] handleOuts = new PointF[path.Count];
                bool hasHandles = false;
                for (int i = 0; i < path.Count; i++)
                {
                    handleTypes[i] = path[i].Handle;
                    handleIns[i] = new PointF(path[i].HandleIn.X / rangeX, path[i].HandleIn.Y / rangeY);
                    handleOuts[i] = new PointF(path[i].HandleOut.X / rangeX, path[i].HandleOut.Y / rangeY);
                    if (path[i].Handle != HandleType.None)
                        hasHandles = true;
                }
                if (hasHandles)
                {
                    polyCmd.PolyHandleTypes = handleTypes;
                    polyCmd.PolyHandleIns = handleIns;
                    polyCmd.PolyHandleOuts = handleOuts;
                }
                polyCmd.X = 0; polyCmd.Y = 0;
                polyCmd.Width = 1; polyCmd.Height = 1;
                bool pathIsShape = validIsShape[0];
                polyCmd.FillColor = pathIsShape ? _geomFillColor : Color.Transparent;
                polyCmd.StrokeColor = _geomBorderColor;
                polyCmd.StrokeWidth = 2f;
                polyCmd.Fill = pathIsShape;
                return new List<RenderCommand> { polyCmd };
            }

            // 多路径：CompoundPolygon + PathDefs
            RenderCommand cmd = new RenderCommand();
            cmd.CommandType = RenderCommandType.CompoundPolygon;
            cmd.PathDefs = new List<PathDef>();
            cmd.MultiPaths = new List<PointF[]>(); // 同时保存 MultiPaths 以兼容旧版
            for (int i = 0; i < validPaths.Count; i++)
            {
                List<CurveVertex> path = validPaths[i];
                int n = path.Count;
                PointF[] normalized = new PointF[n];
                HandleType[] hTypes = new HandleType[n];
                PointF[] hIns = new PointF[n];
                PointF[] hOuts = new PointF[n];
                bool hasH = false;
                for (int j = 0; j < n; j++)
                {
                    normalized[j] = new PointF((path[j].Position.X - minX) / rangeX, (path[j].Position.Y - minY) / rangeY);
                    hTypes[j] = path[j].Handle;
                    hIns[j] = new PointF(path[j].HandleIn.X / rangeX, path[j].HandleIn.Y / rangeY);
                    hOuts[j] = new PointF(path[j].HandleOut.X / rangeX, path[j].HandleOut.Y / rangeY);
                    if (path[j].Handle != HandleType.None) hasH = true;
                }
                cmd.MultiPaths.Add(normalized);
                PathDef pd = new PathDef(normalized, validOps[i]);
                pd.IsShape = validIsShape[i];
                if (hasH)
                {
                    pd.HandleTypes = hTypes;
                    pd.HandleIns = hIns;
                    pd.HandleOuts = hOuts;
                }
                cmd.PathDefs.Add(pd);
            }
            cmd.BoolOp = validOps.Count > 1 ? validOps[0] : BooleanOperation.None;  // 全局 BoolOp 用于旧版兼容（第一条路径的运算）
            cmd.X = 0; cmd.Y = 0;
            cmd.Width = 1; cmd.Height = 1;
            cmd.FillColor = _filled ? _geomFillColor : Color.Transparent;
            cmd.StrokeColor = _geomBorderColor;
            cmd.StrokeWidth = 2f;
            cmd.Fill = _filled;
            return new List<RenderCommand> { cmd };
        }

        private void OnCopyDefaultGeom(object sender, EventArgs e)
        {
            List<RenderCommand> defaults = GetDefaultRenderCommands();
            if (defaults == null || defaults.Count == 0)
            {
                MessageBox.Show("没有初始图形可供复制。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            LoadVerticesFromCommands(defaults);
            _canvasPanel.Invalidate();
        }

        private void OnClearGeom(object sender, EventArgs e)
        {
            _paths.Clear();
            _paths.Add(new List<CurveVertex>());
            _pathBoolOps.Clear();
            _pathBoolOps.Add(BooleanOperation.None);
            _pathIsShape.Clear();
            _pathIsShape.Add(true);
            _currentPathIndex = 0;
            _selectedVertices.Clear();
            _dragIndex = -1;
            _closedPath = false;
            RefreshPathCombo();
            _canvasPanel.Invalidate();
        }

        /// <summary>
        /// 创建圆形：使用4个顶点+对称句柄的贝塞尔近似。
        /// 4个顶点位于圆的上下左右四极点，每个顶点的句柄沿切线方向，
        /// 长度为半径 * 0.5523（圆的贝塞尔近似常数）。
        /// </summary>
        private void OnCreateCircle(object sender, EventArgs e)
        {
            List<CurveVertex> verts = CurrentPath();
            verts.Clear();

            float canvasW = (float)_canvasPanel.Width;
            float canvasH = (float)_canvasPanel.Height;
            float cx = canvasW * 0.5f;
            float cy = canvasH * 0.5f;
            float radius = Math.Min(canvasW, canvasH) * 0.3f;

            // 贝塞尔圆近似常数
            float k = 0.5523f;
            float handleLen = radius * k;

            // 4个顶点：上、右、下、左（顺时针）
            // 每个顶点使用 Symmetric 句柄，切线方向垂直于半径
            CurveVertex top = new CurveVertex(new PointF(cx, cy - radius));
            CurveVertex right = new CurveVertex(new PointF(cx + radius, cy));
            CurveVertex bottom = new CurveVertex(new PointF(cx, cy + radius));
            CurveVertex left = new CurveVertex(new PointF(cx - radius, cy));

            // 设置对称句柄
            top.Handle = HandleType.Symmetric;
            top.HandleOut = new PointF(handleLen, 0);   // 切线向右
            top.HandleIn = new PointF(-handleLen, 0);    // 镜像

            right.Handle = HandleType.Symmetric;
            right.HandleOut = new PointF(0, handleLen);  // 切线向下
            right.HandleIn = new PointF(0, -handleLen);

            bottom.Handle = HandleType.Symmetric;
            bottom.HandleOut = new PointF(-handleLen, 0); // 切线向左
            bottom.HandleIn = new PointF(handleLen, 0);

            left.Handle = HandleType.Symmetric;
            left.HandleOut = new PointF(0, -handleLen);  // 切线向上
            left.HandleIn = new PointF(0, handleLen);

            verts.Add(top);
            verts.Add(right);
            verts.Add(bottom);
            verts.Add(left);

            _closedPath = true;
            _selectedVertices.Clear();
            _dragIndex = -1;
            // 圆形是闭合形状，确保当前路径标记为形状
            SyncPathIsShapeList();
            if (_currentPathIndex >= 0 && _currentPathIndex < _pathIsShape.Count)
            {
                _pathIsShape[_currentPathIndex] = true;
                _suppressIsShapeChange = true;
                _chkIsShape.Checked = true;
                _suppressIsShapeChange = false;
            }
            RefreshPathCombo();
            _canvasPanel.Invalidate();
        }

        /// <summary>"是否为形状"复选框变更：更新当前路径的形状标志</summary>
        private void OnIsShapeChanged(object sender, EventArgs e)
        {
            if (_suppressIsShapeChange)
                return;

            SyncPathIsShapeList();
            if (_currentPathIndex >= 0 && _currentPathIndex < _pathIsShape.Count)
            {
                _pathIsShape[_currentPathIndex] = _chkIsShape.Checked;
                if (!_chkIsShape.Checked)
                {
                    _closedPath = false;
                    _filled = false;
                }
                else
                {
                    _closedPath = true;
                    _filled = true;
                }
            }
            _canvasPanel.Invalidate();
        }

        #endregion

        #region 编辑器 — 画布事件

        /// <summary>
        /// 画布绘制：渲染所有路径（当前高亮）、Zone（虚线边框+异色填充），
        /// 选中的 Zone 显示调整手柄。
        /// </summary>
        private void OnCanvasPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            float canvasW = (float)_canvasPanel.Width;
            float canvasH = (float)_canvasPanel.Height;

            // 预览模式：渲染布尔运算后的结果
            if (_chkPreview != null && _chkPreview.Checked && _selectedZoneIndex < 0)
            {
                RenderPreview(g, canvasW, canvasH);
                RenderZonesAndInfo(g, canvasW, canvasH);
                return;
            }

            // 1. 绘制路径
            if (_paths.Count > 0)
            {
                for (int p = 0; p < _paths.Count; p++)
                {
                    List<CurveVertex> verts = _paths[p];
                    if (verts == null || verts.Count == 0)
                        continue;

                    bool isCurrent = (p == _currentPathIndex && _selectedZoneIndex < 0);

                    // 检测是否有曲线句柄
                    bool hasCurves = false;
                    foreach (CurveVertex v in verts)
                        if (v.Handle != HandleType.None) { hasCurves = true; break; }

                    // 填充
                    if (_filled && verts.Count >= 3)
                    {
                        GraphicsPath gp = BuildEditorPath(verts, _closedPath, hasCurves);
                        int alpha = isCurrent ? 180 : 70;
                        using (Brush brush = new SolidBrush(Color.FromArgb(alpha, _geomFillColor)))
                            g.FillPath(brush, gp);
                        gp.Dispose();
                    }

                    // 描边
                    Color borderColor = isCurrent ? _geomBorderColor : Color.FromArgb(160, _geomBorderColor);
                    float penWidth = isCurrent ? 2f : 1f;
                    using (Pen pen = new Pen(borderColor, penWidth))
                    {
                        if (hasCurves)
                        {
                            using (GraphicsPath gp = BuildEditorPath(verts, _closedPath, true))
                                g.DrawPath(pen, gp);
                        }
                        else
                        {
                            for (int i = 0; i < verts.Count - 1; i++)
                                g.DrawLine(pen, verts[i].Position, verts[i + 1].Position);
                            if (_closedPath && verts.Count >= 3)
                                g.DrawLine(pen, verts[verts.Count - 1].Position, verts[0].Position);
                        }
                    }

                    // 绘制顶点手柄 + 控制柄
                    if (isCurrent)
                    {
                        for (int i = 0; i < verts.Count; i++)
                        {
                            CurveVertex v = verts[i];
                            bool isSelected = _selectedVertices.Contains(i);

                            // 只在选中的顶点上绘制控制柄连线 + 控制点圆
                            if (isSelected && v.Handle != HandleType.None)
                            {
                                PointF vPos = v.Position;
                                PointF hOutAbs = v.GetHandleOutAbs();
                                PointF hInAbs = v.GetHandleInAbs();

                                // 连接线（细虚线）
                                using (Pen hPen = new Pen(Color.FromArgb(100, 100, 100), 1f))
                                {
                                    hPen.DashStyle = DashStyle.Dot;
                                    g.DrawLine(hPen, vPos, hOutAbs);
                                    if (v.Handle == HandleType.Asymmetric)
                                        g.DrawLine(hPen, vPos, hInAbs);
                                }

                                // 控制点圆
                                DrawHandlePoint(g, hOutAbs, Color.FromArgb(255, 128, 0), true);
                                if (v.Handle == HandleType.Asymmetric)
                                    DrawHandlePoint(g, hInAbs, Color.FromArgb(0, 128, 255), true);
                            }

                            // 顶点方块
                            RectangleF handle = new RectangleF(v.Position.X - 5, v.Position.Y - 5, 10, 10);
                            Color vColor = v.Handle == HandleType.None ? _geomBorderColor
                                : v.Handle == HandleType.Symmetric ? Color.FromArgb(0, 160, 0)
                                : Color.FromArgb(160, 0, 160);
                            using (Brush brush = new SolidBrush(isSelected ? Color.FromArgb(0, 120, 215) : Color.White))
                            using (Pen pen = new Pen(vColor, isSelected ? 2f : 1f))
                            {
                                g.FillRectangle(brush, handle);
                                g.DrawRectangle(pen, handle.X, handle.Y, handle.Width, handle.Height);
                            }
                        }
                    }
                }
            }

            // 绘制框选矩形 + Zone + 信息标注
            RenderZonesAndInfo(g, canvasW, canvasH);
        }

        /// <summary>预览复选框变更：重绘画布</summary>
        private void OnPreviewChanged(object sender, EventArgs e)
        {
            _canvasPanel.Invalidate();
        }

        /// <summary>
        /// 预览模式渲染：将所有路径按布尔运算组合后渲染计算结果。
        /// 逻辑与 RenderCommand.DrawCompoundPolygon 一致，但使用编辑器坐标。
        /// </summary>
        private void RenderPreview(Graphics g, float canvasW, float canvasH)
        {
            if (_paths == null || _paths.Count == 0)
                return;

            // 构建每条路径的 GraphicsPath 和 PathDef
            List<GraphicsPath> absPaths = new List<GraphicsPath>();
            List<bool> isShapeList = new List<bool>();
            List<BooleanOperation> boolOps = new List<BooleanOperation>();

            for (int p = 0; p < _paths.Count; p++)
            {
                List<CurveVertex> verts = _paths[p];
                bool isShape = (p < _pathIsShape.Count) ? _pathIsShape[p] : true;
                BooleanOperation op = (p < _pathBoolOps.Count) ? _pathBoolOps[p] : BooleanOperation.None;

                if (verts == null || verts.Count < (isShape ? 3 : 2))
                {
                    absPaths.Add(null);
                    isShapeList.Add(isShape);
                    boolOps.Add(op);
                    continue;
                }

                bool hasCurves = false;
                foreach (CurveVertex v in verts)
                    if (v.Handle != HandleType.None) { hasCurves = true; break; }

                GraphicsPath gp = BuildEditorPath(verts, isShape, hasCurves);
                if (!isShape)
                {
                    // 线体：不闭合
                    gp = new GraphicsPath();
                    for (int i = 0; i < verts.Count - 1; i++)
                    {
                        bool hasOut = verts[i].Handle != HandleType.None;
                        bool hasIn = verts[i + 1].Handle != HandleType.None;
                        if (!hasOut && !hasIn)
                            gp.AddLine(verts[i].Position, verts[i + 1].Position);
                        else
                        {
                            PointF cp1 = hasOut ? verts[i].GetHandleOutAbs() : verts[i].Position;
                            PointF cp2 = hasIn ? verts[i + 1].GetHandleInAbs() : verts[i + 1].Position;
                            gp.AddBezier(verts[i].Position, cp1, cp2, verts[i + 1].Position);
                        }
                    }
                }

                absPaths.Add(gp);
                isShapeList.Add(isShape);
                boolOps.Add(op);
            }

            // === 构建渲染组（与 DrawCompoundPolygon 逻辑一致）===
            List<Region> renderRegions = new List<Region>();
            List<List<int>> regionAllPaths = new List<List<int>>();
            List<bool> regionIsModified = new List<bool>();
            List<bool> regionNeedsRegionFill = new List<bool>();
            List<BooleanOperation> regionOpType = new List<BooleanOperation>();

            for (int i = absPaths.Count - 1; i >= 0; i--)
            {
                if (absPaths[i] == null) continue;

                BooleanOperation op = boolOps[i];
                bool hasLowerPath = false;
                for (int j = i + 1; j < absPaths.Count; j++)
                    if (absPaths[j] != null) { hasLowerPath = true; break; }
                if (!hasLowerPath) op = BooleanOperation.None;

                if (op == BooleanOperation.None)
                {
                    renderRegions.Add(new Region(absPaths[i]));
                    List<int> paths = new List<int>(); paths.Add(i);
                    regionAllPaths.Add(paths);
                    regionIsModified.Add(false);
                    regionNeedsRegionFill.Add(false);
                    regionOpType.Add(BooleanOperation.None);
                }
                else
                {
                    int targetPathIdx = -1;
                    for (int j = i + 1; j < absPaths.Count; j++)
                        if (absPaths[j] != null) { targetPathIdx = j; break; }

                    int targetIdx = -1;
                    if (targetPathIdx >= 0)
                    {
                        for (int r = 0; r < regionAllPaths.Count; r++)
                        {
                            foreach (int pidx in regionAllPaths[r])
                                if (pidx == targetPathIdx) { targetIdx = r; break; }
                            if (targetIdx >= 0) break;
                        }
                    }

                    if (targetIdx >= 0)
                    {
                        Region target = renderRegions[targetIdx];
                        switch (op)
                        {
                            case BooleanOperation.Union:
                                target.Union(absPaths[i]);
                                // Union 多路径时用 Region 填充以确保重叠区域正确填充
                                if (regionAllPaths[targetIdx].Count > 0)
                                    regionNeedsRegionFill[targetIdx] = true;
                                regionOpType[targetIdx] = BooleanOperation.Union;
                                break;
                            case BooleanOperation.Subtract:
                                target.Exclude(absPaths[i]);
                                regionNeedsRegionFill[targetIdx] = true;
                                regionOpType[targetIdx] = BooleanOperation.Subtract;
                                break;
                            case BooleanOperation.Intersect:
                                target.Intersect(absPaths[i]);
                                regionNeedsRegionFill[targetIdx] = true;
                                regionOpType[targetIdx] = BooleanOperation.Intersect;
                                break;
                            case BooleanOperation.Xor:
                                target.Xor(absPaths[i]);
                                regionNeedsRegionFill[targetIdx] = true;
                                regionOpType[targetIdx] = BooleanOperation.Xor;
                                break;
                        }
                        regionIsModified[targetIdx] = true;
                        regionAllPaths[targetIdx].Add(i);
                    }
                    else
                    {
                        renderRegions.Add(new Region(absPaths[i]));
                        List<int> paths = new List<int>(); paths.Add(i);
                        regionAllPaths.Add(paths);
                        regionIsModified.Add(false);
                        regionNeedsRegionFill.Add(false);
                        regionOpType.Add(BooleanOperation.None);
                    }
                }
            }

            // === 填充 ===
            using (Brush fillBrush = new SolidBrush(Color.FromArgb(180, _geomFillColor)))
            {
                for (int r = 0; r < renderRegions.Count; r++)
                {
                    int baseIdx = -1;
                    if (regionAllPaths[r].Count > 0)
                        baseIdx = regionAllPaths[r][0]; // 基础路径是组内最先加入的（底部路径）
                    if (baseIdx >= 0 && baseIdx < isShapeList.Count && !isShapeList[baseIdx])
                        continue;

                    if (!regionNeedsRegionFill[r])
                    {
                        // 未修改组（单路径 None）：直接用 GraphicsPath 填充
                        using (GraphicsPath combined = new GraphicsPath())
                        {
                            combined.FillMode = FillMode.Winding;
                            foreach (int pidx in regionAllPaths[r])
                                if (absPaths[pidx] != null)
                                    combined.AddPath(absPaths[pidx], false);
                            g.FillPath(fillBrush, combined);
                        }
                    }
                    else
                    {
                        // Union/Subtract/Intersect/Xor 组：用 Region 填充（确保重叠区域正确）
                        g.FillRegion(fillBrush, renderRegions[r]);
                    }
                }
            }

            // === 描边 ===
            using (Pen pen = new Pen(_geomBorderColor, 2f))
            {
                for (int r = 0; r < renderRegions.Count; r++)
                {
                    bool isUnionGroup = regionIsModified[r] && regionOpType[r] == BooleanOperation.Union;

                    if (!regionIsModified[r])
                    {
                        foreach (int pidx in regionAllPaths[r])
                            if (pidx >= 0 && pidx < absPaths.Count && absPaths[pidx] != null)
                                g.DrawPath(pen, absPaths[pidx]);
                    }
                    else if (isUnionGroup && regionAllPaths[r].Count > 1)
                    {
                        // Union 组：每条路径只绘制不被其他路径覆盖的外轮廓部分
                        foreach (int pidx in regionAllPaths[r])
                        {
                            if (pidx < 0 || pidx >= absPaths.Count || absPaths[pidx] == null)
                                continue;
                            using (Region otherInterior = new Region())
                            {
                                otherInterior.MakeEmpty();
                                foreach (int otherIdx in regionAllPaths[r])
                                    if (otherIdx != pidx && otherIdx >= 0 && otherIdx < absPaths.Count && absPaths[otherIdx] != null)
                                        otherInterior.Union(absPaths[otherIdx]);
                                GraphicsState state = g.Save();
                                g.SetClip(otherInterior, CombineMode.Exclude);
                                g.DrawPath(pen, absPaths[pidx]);
                                g.Restore(state);
                            }
                        }
                    }
                    else if (isUnionGroup)
                    {
                        foreach (int pidx in regionAllPaths[r])
                            if (pidx >= 0 && pidx < absPaths.Count && absPaths[pidx] != null)
                                g.DrawPath(pen, absPaths[pidx]);
                    }
                    else
                    {
                        // Subtract/Intersect/Xor：裁剪到结果区域后描边
                        // 对于组内存在 Union 子关系的路径，还需排除 Union 伙伴的内部
                        foreach (int pidx in regionAllPaths[r])
                        {
                            if (pidx < 0 || pidx >= absPaths.Count || absPaths[pidx] == null)
                                continue;

                            GraphicsState state = g.Save();

                            bool hasUnionPartner = false;
                            using (Region unionExclude = new Region())
                            {
                                unionExclude.MakeEmpty();

                                // 1. 此路径的 BoolOp 为 Union → 排除其目标路径内部
                                BooleanOperation pathOp = (pidx < boolOps.Count) ? boolOps[pidx] : BooleanOperation.None;
                                if (pathOp == BooleanOperation.Union)
                                {
                                    int targetPathIdx = -1;
                                    for (int j = pidx + 1; j < absPaths.Count; j++)
                                    {
                                        if (absPaths[j] != null) { targetPathIdx = j; break; }
                                    }
                                    if (targetPathIdx >= 0 && regionAllPaths[r].Contains(targetPathIdx))
                                    {
                                        unionExclude.Union(absPaths[targetPathIdx]);
                                        hasUnionPartner = true;
                                    }
                                }

                                // 2. 组内其他路径的 BoolOp 为 Union 且目标是此路径 → 排除该路径内部
                                foreach (int otherIdx in regionAllPaths[r])
                                {
                                    if (otherIdx == pidx || otherIdx < 0 || otherIdx >= absPaths.Count || absPaths[otherIdx] == null)
                                        continue;
                                    BooleanOperation otherOp = (otherIdx < boolOps.Count) ? boolOps[otherIdx] : BooleanOperation.None;
                                    if (otherOp == BooleanOperation.Union)
                                    {
                                        int otherTarget = -1;
                                        for (int j = otherIdx + 1; j < absPaths.Count; j++)
                                        {
                                            if (absPaths[j] != null) { otherTarget = j; break; }
                                        }
                                        if (otherTarget == pidx)
                                        {
                                            unionExclude.Union(absPaths[otherIdx]);
                                            hasUnionPartner = true;
                                        }
                                    }
                                }

                                if (hasUnionPartner)
                                    g.SetClip(unionExclude, CombineMode.Exclude);
                            }

                            g.SetClip(renderRegions[r], CombineMode.Intersect);
                            g.DrawPath(pen, absPaths[pidx]);
                            g.Restore(state);
                        }
                    }
                }
            }

            // 释放资源
            foreach (Region r in renderRegions) r.Dispose();
            foreach (GraphicsPath gp in absPaths) if (gp != null) gp.Dispose();
        }

        /// <summary>渲染 Zone 区域和顶部信息标注（预览模式和普通模式共用）</summary>
        private void RenderZonesAndInfo(Graphics g, float canvasW, float canvasH)
        {
            // 绘制框选矩形
            if (_isBoxSelecting)
            {
                RectangleF boxRect = new RectangleF(
                    Math.Min(_boxSelectStart.X, _boxSelectEnd.X),
                    Math.Min(_boxSelectStart.Y, _boxSelectEnd.Y),
                    Math.Abs(_boxSelectEnd.X - _boxSelectStart.X),
                    Math.Abs(_boxSelectEnd.Y - _boxSelectStart.Y));
                using (Pen boxPen = new Pen(Color.FromArgb(0, 120, 215), 1f))
                {
                    boxPen.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(boxPen, boxRect.X, boxRect.Y, boxRect.Width, boxRect.Height);
                    using (Brush boxBrush = new SolidBrush(Color.FromArgb(30, 0, 120, 215)))
                        g.FillRectangle(boxBrush, boxRect);
                }
            }

            // 2. 绘制 Zone（虚线边框 + 半透明异色填充）
            for (int zi = 0; zi < _zones.Count; zi++)
            {
                ShapeZone zone = _zones[zi];
                RectangleF zr = GetZoneRectOnCanvas(zone, canvasW, canvasH);

                // 半透明填充
                Color fillC = Color.FromArgb(50, zone.FillColor.ToColor());
                using (Brush brush = new SolidBrush(fillC))
                    g.FillRectangle(brush, zr);

                // 虚线边框
                Color borderC = zone.BorderColor.ToColor();
                bool isSelected = (zi == _selectedZoneIndex);
                using (Pen pen = new Pen(borderC, isSelected ? 2f : 1f))
                {
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(pen, zr.X, zr.Y, zr.Width, zr.Height);
                }

                // Zone 标签
                string label = zone.Name;
                if (zone.IsTitleZone) label += " [标题]";
                else if (zone.IsMemberZone) label += " [成员]";
                else if (zone.IsClickZone) label += " [点击]";
                else if (zone.IsConnectionZone) label += " [连接]";
                using (Font font = new Font("Microsoft YaHei", 7.5f))
                using (Brush brush = new SolidBrush(borderC))
                {
                    g.DrawString(label, font, brush, zr.X + 2, zr.Y + 2);
                }

                // 选中 Zone：绘制四角调整手柄
                if (isSelected)
                {
                    PointF[] corners = GetZoneCorners(zr);
                    foreach (PointF corner in corners)
                    {
                        RectangleF handle = new RectangleF(corner.X - 4, corner.Y - 4, 8, 8);
                        using (Brush brush = new SolidBrush(Color.FromArgb(0, 120, 215)))
                        using (Pen pen = new Pen(Color.White, 1f))
                        {
                            g.FillRectangle(brush, handle);
                            g.DrawRectangle(pen, handle.X, handle.Y, handle.Width, handle.Height);
                        }
                    }
                }
            }

            // 3. 顶部信息标注
            using (Font font = new Font("Segoe UI", 8f))
            using (Brush brush = new SolidBrush(Color.FromArgb(80, 80, 80)))
            {
                string info;
                if (_selectedZoneIndex >= 0 && _selectedZoneIndex < _zones.Count)
                {
                    ShapeZone z = _zones[_selectedZoneIndex];
                    info = string.Format("区域: {0}  锚定: {1}  (拖拽边角调整宽高, 约束在图形内)", z.Name, z.Anchor);
                }
                else if (_chkPreview != null && _chkPreview.Checked)
                {
                    info = "[预览模式] 布尔运算结果（取消勾选预览可返回编辑）";
                }
                else
                {
                    BooleanOperation currentOp = (_currentPathIndex < _pathBoolOps.Count)
                        ? _pathBoolOps[_currentPathIndex] : BooleanOperation.None;
                    bool isLastPath = (_currentPathIndex >= _paths.Count - 1);
                    string boolInfo = isLastPath ? "无（最底层）" : currentOp.ToString();
                    string handleHint = "";
                    if (_selectedVertices.Count > 0)
                    {
                        int firstSel = -1;
                        foreach (int idx in _selectedVertices) { firstSel = idx; break; }
                        string selInfo = _selectedVertices.Count > 1
                            ? "   已选: " + _selectedVertices.Count + "个顶点"
                            : "";
                        if (firstSel >= 0 && firstSel < CurrentPath().Count)
                        {
                            HandleType ht = CurrentPath()[firstSel].Handle;
                            handleHint = selInfo + "   句柄: " + (ht == HandleType.None ? "无(右键切换)" : ht == HandleType.Symmetric ? "对称(右键切换)" : "独立(右键切换)");
                        }
                        else
                        {
                            handleHint = selInfo;
                        }
                    }
                    else
                    {
                        handleHint = "   (右键顶点切换句柄: 无/对称/独立  Ctrl多选/反选  框选群体移动)";
                    }
                    string shapeInfo = (_currentPathIndex < _pathIsShape.Count && !_pathIsShape[_currentPathIndex]) ? " [线体]" : "";
                    info = string.Format("路径 {0}/{1}   布尔(→下层): {2}{3}{4}", _currentPathIndex + 1, _paths.Count, boolInfo, handleHint, shapeInfo);
                }
                // 绘制半透明白色背景，防止文字与图形重叠时不可读
                SizeF textSize = g.MeasureString(info, font);
                using (Brush bgBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                    g.FillRectangle(bgBrush, 4, 2, textSize.Width + 4, textSize.Height + 2);
                g.DrawString(info, font, brush, 6, 4);
            }
        }

        /// <summary>计算 Zone 在画布上的像素矩形</summary>
        private RectangleF GetZoneRectOnCanvas(ShapeZone zone, float canvasW, float canvasH)
        {
            // 画布内有 20px 边距，模拟图形边界
            RectangleF shapeBounds = new RectangleF(20, 20, canvasW - 40, canvasH - 40);
            // 预览中使用画布尺寸作为参考尺寸
            return zone.GetAnchoredBounds(shapeBounds, canvasW - 40, canvasH - 40);
        }

        /// <summary>获取 Zone 矩形的四角坐标</summary>
        private PointF[] GetZoneCorners(RectangleF rect)
        {
            return new PointF[]
            {
                new PointF(rect.X, rect.Y),               // 左上
                new PointF(rect.Right, rect.Y),            // 右上
                new PointF(rect.X, rect.Bottom),           // 左下
                new PointF(rect.Right, rect.Bottom)        // 右下
            };
        }

        /// <summary>命中测试 Zone 手柄，返回手柄索引 (0~3) 或 -1</summary>
        private int HitTestZoneHandle(Point pt, RectangleF zoneRect)
        {
            PointF[] corners = GetZoneCorners(zoneRect);
            for (int i = 0; i < corners.Length; i++)
            {
                float dx = corners[i].X - pt.X;
                float dy = corners[i].Y - pt.Y;
                if (dx * dx + dy * dy <= 64)
                    return i;
            }
            return -1;
        }

        /// <summary>命中测试 Zone，返回 Zone 索引或 -1</summary>
        private int HitTestZone(Point pt)
        {
            float canvasW = (float)_canvasPanel.Width;
            float canvasH = (float)_canvasPanel.Height;
            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                RectangleF zr = GetZoneRectOnCanvas(_zones[i], canvasW, canvasH);
                if (zr.Contains(pt))
                    return i;
            }
            return -1;
        }

        private void OnCanvasMouseDown(object sender, MouseEventArgs e)
        {
            // 右键顶点：切换句柄状态 None → Symmetric → Asymmetric → None
            if (e.Button == MouseButtons.Right)
            {
                int hitIdx = HitTestVertex(e.Location);
                if (hitIdx >= 0)
                {
                    List<CurveVertex> verts = CurrentPath();
                    CurveVertex v = verts[hitIdx];
                    _selectedVertices.Clear();
                    _selectedVertices.Add(hitIdx);

                    // 循环切换句柄类型
                    switch (v.Handle)
                    {
                        case HandleType.None:
                            v.Handle = HandleType.Symmetric;
                            InitVertexHandles(verts, hitIdx);
                            break;
                        case HandleType.Symmetric:
                            v.Handle = HandleType.Asymmetric;
                            break;
                        case HandleType.Asymmetric:
                            v.Handle = HandleType.None;
                            v.HandleIn = PointF.Empty;
                            v.HandleOut = PointF.Empty;
                            break;
                    }
                    _canvasPanel.Invalidate();
                }
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                float canvasW = (float)_canvasPanel.Width;
                float canvasH = (float)_canvasPanel.Height;
                bool ctrlKey = (Control.ModifierKeys & Keys.Control) == Keys.Control;

                // 优先检查 Zone 手柄（选中的 Zone）
                if (_selectedZoneIndex >= 0 && _selectedZoneIndex < _zones.Count)
                {
                    RectangleF zr = GetZoneRectOnCanvas(_zones[_selectedZoneIndex], canvasW, canvasH);
                    int handle = HitTestZoneHandle(e.Location, zr);
                    if (handle >= 0)
                    {
                        _zoneDragMode = 2;
                        _zoneDragHandle = handle;
                        _zoneDragStart = e.Location;
                        _canvasPanel.Cursor = Cursors.SizeNWSE;
                        return;
                    }
                }

                // 检查 Zone 内部点击
                int zoneHit = HitTestZone(e.Location);
                if (zoneHit >= 0)
                {
                    _selectedZoneIndex = zoneHit;
                    _zoneDragMode = 1;
                    _zoneDragStart = e.Location;
                    RefreshPathCombo();
                    _canvasPanel.Invalidate();
                    return;
                }

                // 取消 Zone 选中，进入路径编辑模式
                if (_selectedZoneIndex >= 0)
                {
                    _selectedZoneIndex = -1;
                    RefreshPathCombo();
                    _canvasPanel.Invalidate();
                }

                // 优先检查贝塞尔控制柄（仅选中的顶点）
                int[] handleHit = HitTestHandle(e.Location);
                if (handleHit != null)
                {
                    _dragHandleVertex = handleHit[0];
                    _dragHandleType = handleHit[1];
                    _selectedVertices.Clear();
                    _selectedVertices.Add(handleHit[0]);
                    _canvasPanel.Cursor = Cursors.SizeAll;
                    _canvasPanel.Invalidate();
                    return;
                }

                // 路径顶点操作
                List<CurveVertex> verts = CurrentPath();
                int hitIdx = HitTestVertex(e.Location);
                if (hitIdx >= 0)
                {
                    if (ctrlKey)
                    {
                        // Ctrl+点击：反选（切换选中状态）
                        if (_selectedVertices.Contains(hitIdx))
                            _selectedVertices.Remove(hitIdx);
                        else
                            _selectedVertices.Add(hitIdx);
                    }
                    else
                    {
                        // 普通点击：仅选中该顶点
                        if (!_selectedVertices.Contains(hitIdx))
                        {
                            _selectedVertices.Clear();
                            _selectedVertices.Add(hitIdx);
                        }
                    }

                    // 如果选中了顶点，启动群体拖拽
                    if (_selectedVertices.Count > 0)
                    {
                        _isGroupDragging = true;
                        _groupDragStart = e.Location;
                        _groupDragOrigPositions = new Dictionary<int, PointF>();
                        foreach (int idx in _selectedVertices)
                        {
                            if (idx >= 0 && idx < verts.Count)
                                _groupDragOrigPositions[idx] = verts[idx].Position;
                        }
                        _dragIndex = hitIdx;  // 同时设置单顶点拖拽（兼容）
                    }
                    _canvasPanel.Cursor = Cursors.SizeAll;
                    _canvasPanel.Invalidate();
                }
                else
                {
                    // 空白区域：开始框选（Ctrl 时保留已有选中以便反选）
                    _isBoxSelecting = true;
                    _boxSelectStart = e.Location;
                    _boxSelectEnd = e.Location;
                    if (!ctrlKey)
                        _selectedVertices.Clear();
                    _canvasPanel.Invalidate();
                }
            }
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            float canvasW = (float)_canvasPanel.Width;
            float canvasH = (float)_canvasPanel.Height;

            // Zone 拖拽调整大小（约束在图形多边形内）
            if (_zoneDragMode == 2 && _selectedZoneIndex >= 0 && _selectedZoneIndex < _zones.Count)
            {
                ShapeZone zone = _zones[_selectedZoneIndex];
                float dx = e.X - _zoneDragStart.X;
                float dy = e.Y - _zoneDragStart.Y;
                float shapeW = canvasW - 40;
                float shapeH = canvasH - 40;

                // 保存原始值用于约束回退
                float origX = zone.X, origY = zone.Y;
                float origW = zone.Width, origH = zone.Height;

                // 根据手柄索引调整宽高
                switch (_zoneDragHandle)
                {
                    case 0: // 左上
                        zone.Width -= dx / shapeW;
                        zone.X += dx / shapeW;
                        zone.Height -= dy / shapeH;
                        zone.Y += dy / shapeH;
                        break;
                    case 1: // 右上
                        zone.Width += dx / shapeW;
                        zone.Height -= dy / shapeH;
                        zone.Y += dy / shapeH;
                        break;
                    case 2: // 左下
                        zone.Width -= dx / shapeW;
                        zone.X += dx / shapeW;
                        zone.Height += dy / shapeH;
                        break;
                    case 3: // 右下
                        zone.Width += dx / shapeW;
                        zone.Height += dy / shapeH;
                        break;
                }
                // 约束范围
                if (zone.Width < 0.05f) zone.Width = 0.05f;
                if (zone.Height < 0.05f) zone.Height = 0.05f;
                if (zone.Width > 1f) zone.Width = 1f;
                if (zone.Height > 1f) zone.Height = 1f;
                // 绝对定位时 X/Y 不能为负；偏移模式允许负值
                if (zone.Anchor == ZoneAnchor.Absolute)
                {
                    if (zone.X < 0f) zone.X = 0f;
                    if (zone.Y < 0f) zone.Y = 0f;
                }

                // 约束在图形多边形内
                RectangleF shapeBounds = new RectangleF(20, 20, canvasW - 40, canvasH - 40);
                RectangleF zr = zone.GetAnchoredBounds(shapeBounds, canvasW - 40, canvasH - 40);
                GraphicsPath shapePath = GetShapePolygonPath();
                if (shapePath != null)
                {
                    PointF[] corners = GetZoneCorners(zr);
                    bool allInside = true;
                    foreach (PointF c in corners)
                    {
                        if (!shapePath.IsVisible(c)) { allInside = false; break; }
                    }
                    if (!allInside)
                    {
                        zone.X = origX; zone.Y = origY;
                        zone.Width = origW; zone.Height = origH;
                    }
                    shapePath.Dispose();
                }

                _zoneDragStart = e.Location;
                _canvasPanel.Invalidate();
                return;
            }

            // Zone 移动（约束在图形多边形内）
            if (_zoneDragMode == 1 && _selectedZoneIndex >= 0 && _selectedZoneIndex < _zones.Count)
            {
                ShapeZone zone = _zones[_selectedZoneIndex];
                float dx = e.X - _zoneDragStart.X;
                float dy = e.Y - _zoneDragStart.Y;
                float shapeW = canvasW - 40;
                float shapeH = canvasH - 40;
                
                // 保存原始位置，用于约束回退
                float origX = zone.X;
                float origY = zone.Y;
                
                zone.X += dx / shapeW;
                zone.Y += dy / shapeH;
                
                // 约束在图形边界内
                ConstrainZoneToShape(zone, canvasW, canvasH, origX, origY);
                
                _zoneDragStart = e.Location;
                _canvasPanel.Invalidate();
                return;
            }

            // 贝塞尔控制柄拖拽
            if (_dragHandleType > 0 && _dragHandleVertex >= 0)
            {
                List<CurveVertex> verts = CurrentPath();
                if (_dragHandleVertex < verts.Count)
                {
                    CurveVertex v = verts[_dragHandleVertex];
                    if (_dragHandleType == 1) // HandleOut
                        v.SetHandleOutAbs(new PointF(e.X, e.Y));
                    else // HandleIn
                        v.SetHandleInAbs(new PointF(e.X, e.Y));
                }
                _canvasPanel.Invalidate();
                return;
            }

            // 框选顶点
            if (_isBoxSelecting)
            {
                _boxSelectEnd = e.Location;
                _canvasPanel.Invalidate();
                return;
            }

            // 群体拖拽顶点
            if (_isGroupDragging && _groupDragOrigPositions != null)
            {
                List<CurveVertex> verts = CurrentPath();
                float dx = e.X - _groupDragStart.X;
                float dy = e.Y - _groupDragStart.Y;
                foreach (KeyValuePair<int, PointF> kvp in _groupDragOrigPositions)
                {
                    if (kvp.Key >= 0 && kvp.Key < verts.Count)
                        verts[kvp.Key].Position = new PointF(kvp.Value.X + dx, kvp.Value.Y + dy);
                }
                _canvasPanel.Invalidate();
                return;
            }

            // 单顶点拖拽（兼容旧路径，当群体拖拽未激活时）
            if (_dragIndex >= 0 && !_isGroupDragging)
            {
                List<CurveVertex> verts = CurrentPath();
                if (_dragIndex < verts.Count)
                    verts[_dragIndex].Position = new PointF(e.X, e.Y);
                _canvasPanel.Invalidate();
            }
            else if (!_isGroupDragging && !_isBoxSelecting)
            {
                // 更新鼠标样式
                if (_selectedZoneIndex >= 0 && _selectedZoneIndex < _zones.Count)
                {
                    RectangleF zr = GetZoneRectOnCanvas(_zones[_selectedZoneIndex], canvasW, canvasH);
                    int handle = HitTestZoneHandle(e.Location, zr);
                    if (handle >= 0)
                    {
                        _canvasPanel.Cursor = (handle == 0 || handle == 3) ? Cursors.SizeNWSE : Cursors.SizeNESW;
                        return;
                    }
                }
                int zoneHit = HitTestZone(e.Location);
                if (zoneHit >= 0)
                {
                    _canvasPanel.Cursor = Cursors.SizeAll;
                    return;
                }
                // 检查是否悬停在控制柄上
                int[] hHit = HitTestHandle(e.Location);
                if (hHit != null)
                {
                    _canvasPanel.Cursor = Cursors.SizeAll;
                    return;
                }
                int hitIdx = HitTestVertex(e.Location);
                _canvasPanel.Cursor = hitIdx >= 0 ? Cursors.SizeAll : Cursors.Cross;
            }
        }

        private void OnCanvasMouseUp(object sender, MouseEventArgs e)
        {
            if (_zoneDragMode != 0)
            {
                _zoneDragMode = 0;
                _zoneDragHandle = -1;
                _canvasPanel.Cursor = Cursors.Default;
            }

            // 框选完成：选中矩形内的所有顶点
            if (_isBoxSelecting)
            {
                _isBoxSelecting = false;
                bool ctrlKey = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                RectangleF boxRect = new RectangleF(
                    Math.Min(_boxSelectStart.X, _boxSelectEnd.X),
                    Math.Min(_boxSelectStart.Y, _boxSelectEnd.Y),
                    Math.Abs(_boxSelectEnd.X - _boxSelectStart.X),
                    Math.Abs(_boxSelectEnd.Y - _boxSelectStart.Y));

                // 只有框选矩形足够大时才执行选择（避免误触）
                if (boxRect.Width > 3 || boxRect.Height > 3)
                {
                    if (!ctrlKey)
                        _selectedVertices.Clear();

                    List<CurveVertex> verts = CurrentPath();
                    for (int i = 0; i < verts.Count; i++)
                    {
                        if (boxRect.Contains(verts[i].Position))
                        {
                            if (ctrlKey)
                            {
                                // Ctrl 框选：反选
                                if (_selectedVertices.Contains(i))
                                    _selectedVertices.Remove(i);
                                else
                                    _selectedVertices.Add(i);
                            }
                            else
                            {
                                _selectedVertices.Add(i);
                            }
                        }
                    }
                }
                else
                {
                    // 框选矩形太小（实为点击）：添加新顶点
                    List<CurveVertex> verts = CurrentPath();
                    verts.Add(new PointF(e.X, e.Y));
                    if (!ctrlKey)
                        _selectedVertices.Clear();
                    _selectedVertices.Add(verts.Count - 1);
                    RefreshPathCombo();
                }
                _canvasPanel.Invalidate();
            }

            // 群体拖拽结束
            if (_isGroupDragging)
            {
                _isGroupDragging = false;
                _groupDragOrigPositions = null;
            }

            if (_dragIndex >= 0)
            {
                _dragIndex = -1;
                _canvasPanel.Cursor = Cursors.Default;
            }
            if (_dragHandleType != 0)
            {
                _dragHandleType = 0;
                _dragHandleVertex = -1;
                _canvasPanel.Cursor = Cursors.Default;
            }
        }

        private void OnCanvasDoubleClick(object sender, EventArgs e)
        {
            MouseEventArgs me = (MouseEventArgs)e;

            // 双击 Zone 打开编辑
            int zoneHit = HitTestZone(me.Location);
            if (zoneHit >= 0)
            {
                _selectedZoneIndex = zoneHit;
                RefreshPathCombo();
                _canvasPanel.Invalidate();
                OnEditZone(null, null);
                return;
            }

            int hitIdx = HitTestVertex(me.Location);
            if (hitIdx >= 0)
            {
                List<CurveVertex> verts = CurrentPath();
                verts.RemoveAt(hitIdx);
                // 修复选中索引：移除大于 hitIdx 的索引，调整等于的
                List<int> toRemove = new List<int>();
                foreach (int idx in _selectedVertices)
                {
                    if (idx == hitIdx)
                        toRemove.Add(idx);
                    else if (idx > hitIdx)
                        toRemove.Add(idx);
                }
                foreach (int idx in toRemove)
                {
                    _selectedVertices.Remove(idx);
                    if (idx != hitIdx)
                        _selectedVertices.Add(idx - 1);
                }
                RefreshPathCombo();
                _canvasPanel.Invalidate();
            }
        }

        private int HitTestVertex(Point pt)
        {
            List<CurveVertex> verts = CurrentPath();
            for (int i = 0; i < verts.Count; i++)
            {
                float dx = verts[i].Position.X - pt.X;
                float dy = verts[i].Position.Y - pt.Y;
                if (dx * dx + dy * dy <= 64)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 命中测试贝塞尔控制柄。
        /// 返回: null=未命中, 否则 [vertexIndex, handleType] (1=HandleOut, 2=HandleIn)
        /// </summary>
        private int[] HitTestHandle(Point pt)
        {
            List<CurveVertex> verts = CurrentPath();
            for (int i = 0; i < verts.Count; i++)
            {
                CurveVertex v = verts[i];
                if (v.Handle == HandleType.None) continue;

                // 测试 HandleOut
                PointF hOut = v.GetHandleOutAbs();
                float dx = hOut.X - pt.X;
                float dy = hOut.Y - pt.Y;
                if (dx * dx + dy * dy <= 81) // 9px radius
                    return new int[] { i, 1 };

                // Asymmetric 模式下测试 HandleIn
                if (v.Handle == HandleType.Asymmetric)
                {
                    PointF hIn = v.GetHandleInAbs();
                    dx = hIn.X - pt.X;
                    dy = hIn.Y - pt.Y;
                    if (dx * dx + dy * dy <= 81)
                        return new int[] { i, 2 };
                }
            }
            return null;
        }

        /// <summary>
        /// 构建编辑器预览路径（像素坐标，含贝塞尔曲线）。
        /// </summary>
        private GraphicsPath BuildEditorPath(List<CurveVertex> verts, bool closed, bool useCurves)
        {
            GraphicsPath gp = new GraphicsPath();
            int n = verts.Count;
            if (n < 2) return gp;

            if (!useCurves)
            {
                for (int i = 0; i < n - 1; i++)
                    gp.AddLine(verts[i].Position, verts[i + 1].Position);
                if (closed && n >= 3)
                    gp.AddLine(verts[n - 1].Position, verts[0].Position);
                if (closed) gp.CloseFigure();
                return gp;
            }

            int edgeCount = closed ? n : n - 1;
            for (int i = 0; i < edgeCount; i++)
            {
                int next = (i + 1) % n;
                PointF p1 = verts[i].Position;
                PointF p2 = verts[next].Position;

                bool hasOut = verts[i].Handle != HandleType.None;
                bool hasIn = verts[next].Handle != HandleType.None;

                if (!hasOut && !hasIn)
                {
                    gp.AddLine(p1, p2);
                }
                else
                {
                    PointF cp1 = hasOut ? verts[i].GetHandleOutAbs() : p1;
                    PointF cp2 = hasIn ? verts[next].GetHandleInAbs() : p2;
                    gp.AddBezier(p1, cp1, cp2, p2);
                }
            }
            if (closed) gp.CloseFigure();
            return gp;
        }

        /// <summary>绘制控制柄圆点</summary>
        private void DrawHandlePoint(Graphics g, PointF pos, Color color, bool isSelected)
        {
            float r = isSelected ? 6f : 5f;
            RectangleF rect = new RectangleF(pos.X - r, pos.Y - r, r * 2, r * 2);
            using (Brush brush = new SolidBrush(Color.White))
            using (Pen pen = new Pen(color, isSelected ? 2f : 1.5f))
            {
                g.FillEllipse(brush, rect);
                g.DrawEllipse(pen, rect);
            }
        }

        /// <summary>
        /// 为指定顶点初始化默认句柄方向（角平分线法）。
        /// 使用相邻顶点计算进出边方向。
        /// </summary>
        private void InitVertexHandles(List<CurveVertex> verts, int idx)
        {
            int n = verts.Count;
            if (n < 2) return;
            int prev = (idx - 1 + n) % n;
            int next = (idx + 1) % n;
            if (!_closedPath && idx == 0) prev = idx;
            if (!_closedPath && idx == n - 1) next = idx;
            verts[idx].InitDefaultHandles(verts[prev].Position, verts[next].Position);
        }

        /// <summary>
        /// 约束 Zone 在图形多边形边界内。
        /// 检查 Zone 四角是否在多边形内，若超出则回退到原始位置。
        /// </summary>
        private void ConstrainZoneToShape(ShapeZone zone, float canvasW, float canvasH, float origX, float origY)
        {
            RectangleF shapeBounds = new RectangleF(20, 20, canvasW - 40, canvasH - 40);
            RectangleF zr = zone.GetAnchoredBounds(shapeBounds, canvasW - 40, canvasH - 40);

            // 构建图形多边形（使用第一条有效路径的像素坐标）
            GraphicsPath shapePath = GetShapePolygonPath();
            if (shapePath == null) return;

            // 检查 Zone 四角是否在多边形内
            PointF[] corners = GetZoneCorners(zr);
            bool allInside = true;
            foreach (PointF c in corners)
            {
                if (!shapePath.IsVisible(c))
                {
                    allInside = false;
                    break;
                }
            }

            if (!allInside)
            {
                // 回退到原始位置
                zone.X = origX;
                zone.Y = origY;
            }
            shapePath.Dispose();
        }

        /// <summary>获取当前图形的多边形路径（像素坐标），用于 Zone 约束检测</summary>
        private GraphicsPath GetShapePolygonPath()
        {
            // 收集所有有效路径
            List<List<CurveVertex>> validPaths = new List<List<CurveVertex>>();
            foreach (List<CurveVertex> path in _paths)
            {
                if (path != null && path.Count >= 3)
                    validPaths.Add(path);
            }
            if (validPaths.Count == 0) return null;

            GraphicsPath result = new GraphicsPath();
            foreach (List<CurveVertex> path in validPaths)
            {
                bool hasCurves = false;
                foreach (CurveVertex v in path)
                    if (v.Handle != HandleType.None) { hasCurves = true; break; }

                GraphicsPath gp = BuildEditorPath(path, true, hasCurves);
                result.AddPath(gp, false);
            }
            return result;
        }

        private void OnAddVertex(object sender, EventArgs e)
        {
            List<CurveVertex> verts = CurrentPath();
            PointF newPt;
            if (verts.Count > 0)
            {
                PointF last = verts[verts.Count - 1].Position;
                newPt = new PointF(last.X + 30, last.Y + 30);
            }
            else
            {
                newPt = new PointF(220, 160);
            }
            verts.Add(newPt);
            _selectedVertices.Clear();
            _selectedVertices.Add(verts.Count - 1);
            RefreshPathCombo();
            _canvasPanel.Invalidate();
        }

        private void OnDeleteVertex(object sender, EventArgs e)
        {
            List<CurveVertex> verts = CurrentPath();
            if (_selectedVertices.Count == 0)
                return;
            // 从大到小排序，避免删除时索引偏移
            List<int> sorted = new List<int>(_selectedVertices);
            sorted.Sort();
            sorted.Reverse();
            foreach (int idx in sorted)
            {
                if (idx >= 0 && idx < verts.Count)
                    verts.RemoveAt(idx);
            }
            _selectedVertices.Clear();
            RefreshPathCombo();
            _canvasPanel.Invalidate();
        }

        private void OnToggleClosePath(object sender, EventArgs e)
        {
            _closedPath = !_closedPath;
            _canvasPanel.Invalidate();
        }

        #endregion

        #region 编辑器 — 多路径管理

        private void OnNewPath(object sender, EventArgs e)
        {
            // 新路径插入到当前路径上方（当前索引处），当前路径下移
            int insertAt = (_currentPathIndex >= 0 && _currentPathIndex < _paths.Count)
                ? _currentPathIndex : _paths.Count;
            _paths.Insert(insertAt, new List<CurveVertex>());
            _pathBoolOps.Insert(insertAt, BooleanOperation.None);
            _pathIsShape.Insert(insertAt, true);
            _currentPathIndex = insertAt;
            _selectedVertices.Clear();
            _dragIndex = -1;
            _selectedZoneIndex = -1;
            RefreshPathCombo();
            _canvasPanel.Invalidate();
        }

        private void OnDeletePath(object sender, EventArgs e)
        {
            if (_paths.Count <= 1)
            {
                CurrentPath().Clear();
                _selectedVertices.Clear();
                _dragIndex = -1;
                RefreshPathCombo();
                _canvasPanel.Invalidate();
                return;
            }
            _paths.RemoveAt(_currentPathIndex);
            _pathBoolOps.RemoveAt(_currentPathIndex);
            _pathIsShape.RemoveAt(_currentPathIndex);
            if (_currentPathIndex >= _paths.Count)
                _currentPathIndex = _paths.Count - 1;
            _selectedVertices.Clear();
            _dragIndex = -1;
            RefreshPathCombo();
            _canvasPanel.Invalidate();
        }

        /// <summary>上移当前路径（调整层叠顺序）</summary>
        private void OnMovePathUp(object sender, EventArgs e)
        {
            if (_currentPathIndex <= 0 || _currentPathIndex >= _paths.Count)
                return;
            // 交换路径和布尔运算
            List<CurveVertex> tmpPath = _paths[_currentPathIndex];
            _paths[_currentPathIndex] = _paths[_currentPathIndex - 1];
            _paths[_currentPathIndex - 1] = tmpPath;

            BooleanOperation tmpOp = _pathBoolOps[_currentPathIndex];
            _pathBoolOps[_currentPathIndex] = _pathBoolOps[_currentPathIndex - 1];
            _pathBoolOps[_currentPathIndex - 1] = tmpOp;

            _currentPathIndex--;
            RefreshPathCombo();
            _canvasPanel.Invalidate();
        }

        /// <summary>下移当前路径（调整层叠顺序）</summary>
        private void OnMovePathDown(object sender, EventArgs e)
        {
            if (_currentPathIndex < 0 || _currentPathIndex >= _paths.Count - 1)
                return;
            List<CurveVertex> tmpPath = _paths[_currentPathIndex];
            _paths[_currentPathIndex] = _paths[_currentPathIndex + 1];
            _paths[_currentPathIndex + 1] = tmpPath;

            BooleanOperation tmpOp = _pathBoolOps[_currentPathIndex];
            _pathBoolOps[_currentPathIndex] = _pathBoolOps[_currentPathIndex + 1];
            _pathBoolOps[_currentPathIndex + 1] = tmpOp;

            _currentPathIndex++;
            RefreshPathCombo();
            _canvasPanel.Invalidate();
        }

        private void OnPathSelectedChanged(object sender, EventArgs e)
        {
            if (_suppressPathChange)
                return;
            int idx = _cmbPathSelect.SelectedIndex;
            if (idx < 0)
                return;

            int zoneCount = _zones.Count;
            if (idx < zoneCount)
            {
                // 选中了 Zone
                _selectedZoneIndex = idx;
                _selectedVertices.Clear();
                _dragIndex = -1;
            }
            else
            {
                // 选中了路径
                _selectedZoneIndex = -1;
                _currentPathIndex = idx - zoneCount;
                _selectedVertices.Clear();
            }
            UpdatePathToolbarMode();
            _canvasPanel.Invalidate();
        }

        // OnBoolOpChanged 已移除：布尔运算改由 OnBoolOpMenuItem 处理

        /// <summary>
        /// 刷新路径/区域下拉框。
        /// 区域始终显示在路径之上（列表顶部），格式：◈ Zone名 [类型]
        /// 路径格式：▲ 路径 N (顶点数)
        /// </summary>
        private void RefreshPathCombo()
        {
            if (_cmbPathSelect == null || _btnBoolOp == null)
                return;
            _suppressPathChange = true;
            try
            {
                _cmbPathSelect.Items.Clear();

                // Zone 项（始终在顶部）
                for (int i = 0; i < _zones.Count; i++)
                {
                    ShapeZone z = _zones[i];
                    string flag = "";
                    if (z.IsTitleZone) flag = " [标题]";
                    else if (z.IsMemberZone) flag = " [成员]";
                    else if (z.IsClickZone) flag = " [点击]";
                    else if (z.IsConnectionZone) flag = " [连接]";
                    _cmbPathSelect.Items.Add(string.Format("◈ {0}{1}", z.Name, flag));
                }

                // 路径项
                for (int i = 0; i < _paths.Count; i++)
                {
                    int vc = (_paths[i] != null) ? _paths[i].Count : 0;
                    _cmbPathSelect.Items.Add(string.Format("▲ 路径 {0} ({1})", i + 1, vc));
                }

                // 设置选中项
                if (_selectedZoneIndex >= 0 && _selectedZoneIndex < _zones.Count)
                {
                    _cmbPathSelect.SelectedIndex = _selectedZoneIndex;
                }
                else
                {
                    if (_currentPathIndex < 0 || _currentPathIndex >= _paths.Count)
                        _currentPathIndex = 0;
                    int pathSelIdx = _zones.Count + _currentPathIndex;
                    if (pathSelIdx < _cmbPathSelect.Items.Count)
                        _cmbPathSelect.SelectedIndex = pathSelIdx;
                }

                UpdatePathToolbarMode();
            }
            finally
            {
                _suppressPathChange = false;
            }
        }

        /// <summary>根据当前选择（Zone 或路径）更新工具栏控件状态</summary>
        private void UpdatePathToolbarMode()
        {
            bool isZone = (_selectedZoneIndex >= 0);
            // 路径操作按钮
            _btnMovePathUp.Enabled = !isZone && _currentPathIndex > 0;
            _btnMovePathDown.Enabled = !isZone && _currentPathIndex < _paths.Count - 1;
            _btnNewPath.Enabled = true;
            _btnDeletePath.Enabled = !isZone;
            // Zone 操作按钮
            _btnAddZoneTB.Enabled = true;
            _btnEditZoneTB.Enabled = isZone;
            _btnDeleteZoneTB.Enabled = isZone;
            // 布尔运算按钮：路径模式启用，当前路径有下方非空路径时才有意义
            _btnBoolOp.Enabled = !isZone && HasLowerValidPath();
            // 合并按钮：当前路径有下方非空路径时启用
            _btnMergePaths.Enabled = !isZone && HasLowerValidPath();
            if (!isZone)
                UpdateBoolOpButtonText();

            // 同步 _pathIsShape 列表
            SyncPathIsShapeList();
            // 更新"是否为形状"复选框
            if (!isZone && _chkIsShape != null)
            {
                bool isShape = (_currentPathIndex < _pathIsShape.Count) ? _pathIsShape[_currentPathIndex] : true;
                _suppressIsShapeChange = true;
                _chkIsShape.Checked = isShape;
                _suppressIsShapeChange = false;
            }
        }

        /// <summary>确保 _pathIsShape 列表与 _paths 同步</summary>
        private void SyncPathIsShapeList()
        {
            while (_pathIsShape.Count < _paths.Count)
                _pathIsShape.Add(true);
            while (_pathIsShape.Count > _paths.Count)
                _pathIsShape.RemoveAt(_pathIsShape.Count - 1);
        }

        /// <summary>统计有效路径数（顶点数 >= 3）</summary>
        private int CountValidPaths()
        {
            int count = 0;
            foreach (List<CurveVertex> path in _paths)
            {
                if (path != null && path.Count >= 3)
                    count++;
            }
            return count;
        }

        /// <summary>检查当前路径下方是否存在非空有效路径</summary>
        private bool HasLowerValidPath()
        {
            if (_currentPathIndex < 0 || _currentPathIndex >= _paths.Count)
                return false;
            for (int j = _currentPathIndex + 1; j < _paths.Count; j++)
            {
                if (_paths[j] != null && _paths[j].Count >= 3)
                    return true;
            }
            return false;
        }

        #endregion

        #region 编辑器 — Zone 管理

        private void OnAddZone(object sender, EventArgs e)
        {
            using (ShapeZoneEditDialog dlg = new ShapeZoneEditDialog(null))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (dlg.DeleteRequested)
                        return; // 添加模式下不处理删除
                    if (dlg.ResultZone != null)
                    {
                        _zones.Add(dlg.ResultZone);
                        _selectedZoneIndex = _zones.Count - 1;
                        RefreshPathCombo();
                        _canvasPanel.Invalidate();
                    }
                }
            }
        }

        private void OnEditZone(object sender, EventArgs e)
        {
            int idx = _selectedZoneIndex;
            if (idx < 0 || idx >= _zones.Count)
                return;
            using (ShapeZoneEditDialog dlg = new ShapeZoneEditDialog(_zones[idx]))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (dlg.DeleteRequested)
                    {
                        _zones.RemoveAt(idx);
                        _selectedZoneIndex = -1;
                    }
                    else if (dlg.ResultZone != null)
                    {
                        _zones[idx] = dlg.ResultZone;
                    }
                    RefreshPathCombo();
                    _canvasPanel.Invalidate();
                }
            }
        }

        private void OnDeleteZone(object sender, EventArgs e)
        {
            int idx = _selectedZoneIndex;
            if (idx < 0 || idx >= _zones.Count)
                return;
            if (MessageBox.Show(string.Format("确定删除区域 \"{0}\"？", _zones[idx].Name),
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _zones.RemoveAt(idx);
                _selectedZoneIndex = -1;
                RefreshPathCombo();
                _canvasPanel.Invalidate();
            }
        }

        private List<ShapeZone> CloneShapeZones(List<ShapeZone> src)
        {
            List<ShapeZone> result = new List<ShapeZone>();
            if (src == null)
                return result;
            foreach (ShapeZone z in src)
                result.Add(z.Clone());
            return result;
        }

        #endregion

        #region 编辑器 — 颜色选择

        private void UpdateColorButtons()
        {
            _btnFillColor.BackColor = _stateFillColor;
            _btnBorderColor.BackColor = _stateBorderColor;
            _btnTextColor.BackColor = _stateTextColor;
            _btnHeaderColor.BackColor = _stateHeaderColor;
            _btnFillColor.ForeColor = GetContrastColor(_stateFillColor);
            _btnBorderColor.ForeColor = GetContrastColor(_stateBorderColor);
            _btnTextColor.ForeColor = GetContrastColor(_stateTextColor);
            _btnHeaderColor.ForeColor = GetContrastColor(_stateHeaderColor);
        }

        private Color GetContrastColor(Color c)
        {
            float brightness = c.R * 0.299f + c.G * 0.587f + c.B * 0.114f;
            return brightness > 128 ? Color.Black : Color.White;
        }

        private void OnPickStateFillColor(object sender, EventArgs e)
        {
            _colorDialog.Color = _stateFillColor;
            if (_colorDialog.ShowDialog() == DialogResult.OK)
            {
                _stateFillColor = _colorDialog.Color;
                _geomFillColor = _colorDialog.Color;
                UpdateColorButtons();
                _canvasPanel.Invalidate();
            }
        }

        private void OnPickStateBorderColor(object sender, EventArgs e)
        {
            _colorDialog.Color = _stateBorderColor;
            if (_colorDialog.ShowDialog() == DialogResult.OK)
            {
                _stateBorderColor = _colorDialog.Color;
                _geomBorderColor = _colorDialog.Color;
                UpdateColorButtons();
                _canvasPanel.Invalidate();
            }
        }

        private void OnPickStateTextColor(object sender, EventArgs e)
        {
            _colorDialog.Color = _stateTextColor;
            if (_colorDialog.ShowDialog() == DialogResult.OK)
            {
                _stateTextColor = _colorDialog.Color;
                UpdateColorButtons();
            }
        }

        private void OnPickStateHeaderColor(object sender, EventArgs e)
        {
            _colorDialog.Color = _stateHeaderColor;
            if (_colorDialog.ShowDialog() == DialogResult.OK)
            {
                _stateHeaderColor = _colorDialog.Color;
                UpdateColorButtons();
            }
        }

        #endregion

        #region 行为选项卡

        private void RefreshActionList()
        {
            _listActions.Items.Clear();
            foreach (ShapeAction action in _actions)
            {
                string typeStr;
                if (action.HasSubActions)
                    typeStr = "[序列]";
                else if (action.ActionType == ShapeActionType.StateChange)
                    typeStr = "[切换状态]";
                else
                    typeStr = "[宿主回调]";
                _listActions.Items.Add(string.Format("{0} {1}", typeStr, action.Name));
            }
            UpdateActionButtons();
        }

        private void OnActionListSelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            bool hasSelection = (_listActions.SelectedIndex >= 0 && _listActions.SelectedIndex < _actions.Count);
            _btnEditAction.Enabled = hasSelection;
            _btnDeleteAction.Enabled = hasSelection;
        }

        private List<string> GetStateNames()
        {
            List<string> names = new List<string>();
            foreach (ShapeState s in _states)
                names.Add(s.Name);
            return names;
        }

        private void OnAddAction(object sender, EventArgs e)
        {
            using (ShapeActionEditDialog dlg = new ShapeActionEditDialog(null, GetStateNames()))
            {
                if (dlg.ShowDialog() == DialogResult.OK && dlg.ResultAction != null)
                {
                    _actions.Add(dlg.ResultAction);
                    RefreshActionList();
                    _listActions.SelectedIndex = _listActions.Items.Count - 1;
                }
            }
        }

        private void OnEditAction(object sender, EventArgs e)
        {
            int idx = _listActions.SelectedIndex;
            if (idx < 0 || idx >= _actions.Count)
                return;
            using (ShapeActionEditDialog dlg = new ShapeActionEditDialog(_actions[idx], GetStateNames()))
            {
                if (dlg.ShowDialog() == DialogResult.OK && dlg.ResultAction != null)
                {
                    _actions[idx] = dlg.ResultAction;
                    RefreshActionList();
                }
            }
        }

        private void OnDeleteAction(object sender, EventArgs e)
        {
            int idx = _listActions.SelectedIndex;
            if (idx < 0 || idx >= _actions.Count)
                return;
            if (MessageBox.Show(string.Format("确定删除行为 \"{0}\"？", _actions[idx].Name),
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _actions.RemoveAt(idx);
                RefreshActionList();
            }
        }

        #endregion

        #region 构建结果

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (this.DialogResult == DialogResult.OK)
            {
                if (_infoTimer != null)
                    _infoTimer.Stop();

                SaveCurrentEditorToState();

                List<RenderCommand> defaultCmds = GetDefaultRenderCommands();
                if (defaultCmds == null || defaultCmds.Count == 0)
                {
                    MessageBox.Show("初始状态至少需要 3 个顶点的图形。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    e.Cancel = true;
                    return;
                }

                string name = _txtName.Text.Trim();
                if (string.IsNullOrEmpty(name))
                    name = "CustomShape";

                BuildShapeType(name, defaultCmds);
            }
        }

        private void BuildShapeType(string name, List<RenderCommand> defaultCmds)
        {
            ShapeType st = new ShapeType();
            st.Name = name;
            st.Category = "自定义";
            st.DefaultWidth = 120;
            st.DefaultHeight = 100;
            st.RenderCommands = CloneRenderCommands(defaultCmds);

            RenderCommand geomCmd = null;
            foreach (RenderCommand cmd in defaultCmds)
            {
                if (cmd.CommandType == RenderCommandType.Polygon
                    || cmd.CommandType == RenderCommandType.CompoundPolygon)
                { geomCmd = cmd; break; }
            }
            if (geomCmd != null)
            {
                st.DefaultFillColor = new XmlColor(geomCmd.FillColor);
                st.DefaultBorderColor = new XmlColor(geomCmd.StrokeColor);
            }
            else
            {
                st.DefaultFillColor = new XmlColor(Color.FromArgb(220, 240, 255));
                st.DefaultBorderColor = new XmlColor(Color.FromArgb(80, 120, 180));
            }
            st.DefaultTextColor = new XmlColor(Color.FromArgb(40, 40, 40));

            st.DefaultStates = new List<ShapeState>();
            foreach (ShapeState s in _states)
                st.DefaultStates.Add(CloneShapeState(s));

            // 从默认状态复制 Zone 到 ShapeType
            ShapeState zoneSource = null;
            foreach (ShapeState s in _states)
            {
                if (s.Name == DefaultStateName)
                { zoneSource = s; break; }
            }
            if (zoneSource == null && _states.Count > 0)
                zoneSource = _states[0];
            if (zoneSource != null && zoneSource.CustomZones != null)
            {
                foreach (ShapeZone z in zoneSource.CustomZones)
                    st.Zones.Add(z.Clone());
            }

            // 复制用户自定义行为
            st.CustomActions = new List<ShapeAction>();
            foreach (ShapeAction a in _actions)
            {
                st.CustomActions.Add(a.Clone());
            }

            // 根据 Zone 自动生成系统行为
            st.GenerateSystemBehaviors();

            _resultShapeType = st;
        }

        #endregion
    }

    /// <summary>
    /// 图形区域（Zone）编辑对话框。
    /// 支持布局/缩放中文释义、ZoneAnchor 对齐选项、
    /// 点击区域和连接区域配置。
    /// 当 Anchor != Absolute 时，X/Y 为偏移值。
    /// </summary>
    public class ShapeZoneEditDialog : Form
    {
        private TextBox _txtName;
        private ComboBox _cmbLayout;
        private ComboBox _cmbScaling;
        private ComboBox _cmbAnchor;
        private NumericUpDown _numX;
        private NumericUpDown _numY;
        private NumericUpDown _numW;
        private NumericUpDown _numH;
        private Label _lblX;
        private Label _lblY;
        private CheckBox _chkShowBorder;
        private CheckBox _chkTitleZone;
        private CheckBox _chkMemberZone;
        private CheckBox _chkClickZone;
        private CheckBox _chkConnectionZone;

        // 连接区域属性
        private CheckBox _chkCanStart;
        private CheckBox _chkCanEnd;
        private CheckBox _chkAllowSelfConnect;
        private ComboBox _cmbLineTypes;

        // 边框/填充颜色
        private Button _btnBorderColor;
        private Button _btnFillColor;

        private Button _btnOk;
        private Button _btnCancel;
        private Button _btnDelete;

        private ColorDialog _colorDialog = new ColorDialog();

        public ShapeZone ResultZone { get; private set; }
        public bool DeleteRequested { get; private set; }

        public ShapeZoneEditDialog() : this(null) { }

        public ShapeZoneEditDialog(ShapeZone editZone)
        {
            this.Text = (editZone == null) ? "添加区域" : "编辑区域";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(400, 520);

            int y = 12;
            int lblW = 80;
            int xVal = 95;
            int valW = 285;

            // 名称
            AddLabel("名称：", y, lblW);
            _txtName = new TextBox();
            _txtName.Location = new Point(xVal, y);
            _txtName.Size = new Size(valW, 22);
            _txtName.Text = (editZone != null) ? editZone.Name : "Zone";
            this.Controls.Add(_txtName);
            y += 32;

            // 布局（中文释义）
            AddLabel("布局：", y, lblW);
            _cmbLayout = new ComboBox();
            _cmbLayout.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbLayout.Location = new Point(xVal, y);
            _cmbLayout.Size = new Size(valW, 22);
            _cmbLayout.Items.Add("无 — 仅命名区域，不排列子元件");
            _cmbLayout.Items.Add("标题 — 显示图形名称");
            _cmbLayout.Items.Add("堆叠 — 子元件从上到下排列");
            _cmbLayout.Items.Add("流式 — 子元件从左到右排列");
            _cmbLayout.Items.Add("成员 — 显示成员列表");
            _cmbLayout.Items.Add("点击 — 点击后触发行为");
            _cmbLayout.Items.Add("连接 — 作为连线起点/终点");
            _cmbLayout.SelectedIndex = (editZone != null) ? (int)editZone.Layout : 0;
            _cmbLayout.SelectedIndexChanged += new EventHandler(OnLayoutChanged);
            this.Controls.Add(_cmbLayout);
            y += 32;

            // 缩放（中文释义）
            AddLabel("缩放：", y, lblW);
            _cmbScaling = new ComboBox();
            _cmbScaling.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbScaling.Location = new Point(xVal, y);
            _cmbScaling.Size = new Size(valW, 22);
            _cmbScaling.Items.Add("随图形等比缩放");
            _cmbScaling.Items.Add("冻结边角 — 缩放时保持绝对像素尺寸");
            _cmbScaling.SelectedIndex = (editZone != null) ? (int)editZone.Scaling : 0;
            this.Controls.Add(_cmbScaling);
            y += 32;

            // 锚定/对齐
            AddLabel("对齐：", y, lblW);
            _cmbAnchor = new ComboBox();
            _cmbAnchor.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbAnchor.Location = new Point(xVal, y);
            _cmbAnchor.Size = new Size(valW, 22);
            _cmbAnchor.Items.Add("绝对定位 — X/Y 为归一化坐标");
            _cmbAnchor.Items.Add("左上角");
            _cmbAnchor.Items.Add("上居中");
            _cmbAnchor.Items.Add("右上角");
            _cmbAnchor.Items.Add("左居中");
            _cmbAnchor.Items.Add("正中（标题默认）");
            _cmbAnchor.Items.Add("右居中");
            _cmbAnchor.Items.Add("左下角");
            _cmbAnchor.Items.Add("下居中");
            _cmbAnchor.Items.Add("右下角");
            _cmbAnchor.SelectedIndex = (editZone != null) ? (int)editZone.Anchor : 0;
            _cmbAnchor.SelectedIndexChanged += new EventHandler(OnAnchorChanged);
            this.Controls.Add(_cmbAnchor);
            y += 32;

            // X / Y
            _lblX = AddLabel("X：", y, lblW);
            _numX = MakeNum(xVal, y, 130);
            _lblY = AddLabel("Y：", y, 230);
            _numY = MakeNum(xVal + 140, y, 130);
            this.Controls.Add(_numX);
            this.Controls.Add(_numY);
            y += 30;

            // 宽 / 高
            AddLabel("宽 / 高：", y, lblW);
            _numW = MakeNum(xVal, y, 130);
            _numH = MakeNum(xVal + 140, y, 130);
            this.Controls.Add(_numW);
            this.Controls.Add(_numH);
            y += 32;

            // 颜色
            AddLabel("边框色：", y, lblW);
            _btnBorderColor = new Button();
            _btnBorderColor.Location = new Point(xVal, y);
            _btnBorderColor.Size = new Size(60, 22);
            _btnBorderColor.Text = "选取";
            _btnBorderColor.Click += new EventHandler(OnPickBorderColor);
            this.Controls.Add(_btnBorderColor);

            AddLabel("填充色：", y, 165);
            _btnFillColor = new Button();
            _btnFillColor.Location = new Point(255, y);
            _btnFillColor.Size = new Size(60, 22);
            _btnFillColor.Text = "选取";
            _btnFillColor.Click += new EventHandler(OnPickFillColor);
            this.Controls.Add(_btnFillColor);
            y += 32;

            // 类型复选框
            AddLabel("类型：", y, lblW);
            _chkTitleZone = new CheckBox();
            _chkTitleZone.Text = "标题区域";
            _chkTitleZone.Location = new Point(xVal, y);
            _chkTitleZone.Size = new Size(85, 22);
            _chkTitleZone.Checked = (editZone != null) ? editZone.IsTitleZone : false;
            _chkTitleZone.CheckedChanged += new EventHandler(OnZoneTypeChanged);
            this.Controls.Add(_chkTitleZone);

            _chkMemberZone = new CheckBox();
            _chkMemberZone.Text = "成员区域";
            _chkMemberZone.Location = new Point(xVal + 90, y);
            _chkMemberZone.Size = new Size(85, 22);
            _chkMemberZone.Checked = (editZone != null) ? editZone.IsMemberZone : false;
            _chkMemberZone.CheckedChanged += new EventHandler(OnZoneTypeChanged);
            this.Controls.Add(_chkMemberZone);

            _chkClickZone = new CheckBox();
            _chkClickZone.Text = "点击区域";
            _chkClickZone.Location = new Point(xVal + 180, y);
            _chkClickZone.Size = new Size(85, 22);
            _chkClickZone.Checked = (editZone != null) ? editZone.IsClickZone : false;
            _chkClickZone.CheckedChanged += new EventHandler(OnZoneTypeChanged);
            this.Controls.Add(_chkClickZone);

            _chkConnectionZone = new CheckBox();
            _chkConnectionZone.Text = "连接区域";
            _chkConnectionZone.Location = new Point(xVal, y + 24);
            _chkConnectionZone.Size = new Size(85, 22);
            _chkConnectionZone.Checked = (editZone != null) ? editZone.IsConnectionZone : false;
            _chkConnectionZone.CheckedChanged += new EventHandler(OnZoneTypeChanged);
            this.Controls.Add(_chkConnectionZone);

            _chkShowBorder = new CheckBox();
            _chkShowBorder.Text = "显示边框";
            _chkShowBorder.Location = new Point(xVal + 90, y + 24);
            _chkShowBorder.Size = new Size(85, 22);
            _chkShowBorder.Checked = (editZone != null) ? editZone.ShowBorder : false;
            this.Controls.Add(_chkShowBorder);
            y += 54;

            // 连接区域属性（仅当 IsConnectionZone 时显示）
            AddLabel("连线设置：", y, lblW);

            _chkCanStart = new CheckBox();
            _chkCanStart.Text = "可作起点";
            _chkCanStart.Location = new Point(xVal, y);
            _chkCanStart.Size = new Size(80, 22);
            _chkCanStart.Checked = (editZone != null) ? editZone.CanStart : true;
            this.Controls.Add(_chkCanStart);

            _chkCanEnd = new CheckBox();
            _chkCanEnd.Text = "可作终点";
            _chkCanEnd.Location = new Point(xVal + 85, y);
            _chkCanEnd.Size = new Size(80, 22);
            _chkCanEnd.Checked = (editZone != null) ? editZone.CanEnd : true;
            this.Controls.Add(_chkCanEnd);

            _chkAllowSelfConnect = new CheckBox();
            _chkAllowSelfConnect.Text = "允许自连";
            _chkAllowSelfConnect.Location = new Point(xVal + 170, y);
            _chkAllowSelfConnect.Size = new Size(85, 22);
            _chkAllowSelfConnect.Checked = (editZone != null) ? editZone.AllowSelfConnect : false;
            this.Controls.Add(_chkAllowSelfConnect);
            y += 30;

            AddLabel("允许线型：", y, lblW);
            _cmbLineTypes = new ComboBox();
            _cmbLineTypes.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbLineTypes.Location = new Point(xVal, y);
            _cmbLineTypes.Size = new Size(valW, 22);
            _cmbLineTypes.Items.Add("直线, 曲线, 正交线 (全部)");
            _cmbLineTypes.Items.Add("仅直线");
            _cmbLineTypes.Items.Add("仅曲线");
            _cmbLineTypes.Items.Add("仅正交线");
            _cmbLineTypes.Items.Add("直线, 曲线");
            _cmbLineTypes.Items.Add("直线, 正交线");
            _cmbLineTypes.Items.Add("曲线, 正交线");
            if (editZone != null)
            {
                string lt = editZone.AllowedLineTypes;
                if (lt == "Straight,Curve,Orthogonal") _cmbLineTypes.SelectedIndex = 0;
                else if (lt == "Straight") _cmbLineTypes.SelectedIndex = 1;
                else if (lt == "Curve") _cmbLineTypes.SelectedIndex = 2;
                else if (lt == "Orthogonal") _cmbLineTypes.SelectedIndex = 3;
                else if (lt == "Straight,Curve") _cmbLineTypes.SelectedIndex = 4;
                else if (lt == "Straight,Orthogonal") _cmbLineTypes.SelectedIndex = 5;
                else if (lt == "Curve,Orthogonal") _cmbLineTypes.SelectedIndex = 6;
                else _cmbLineTypes.SelectedIndex = 0;
            }
            else
                _cmbLineTypes.SelectedIndex = 0;
            this.Controls.Add(_cmbLineTypes);
            y += 36;

            // 预填数值
            if (editZone != null)
            {
                // 先根据锚定方式调整 X/Y 的范围（允许负值用于偏移模式）
                UpdateXYLabels();
                _numX.Value = ClampDecimal(editZone.X, editZone.Anchor);
                _numY.Value = ClampDecimal(editZone.Y, editZone.Anchor);
                _numW.Value = ClampDecimal(editZone.Width, ZoneAnchor.Absolute);
                _numH.Value = ClampDecimal(editZone.Height, ZoneAnchor.Absolute);
                _btnBorderColor.BackColor = editZone.BorderColor.ToColor();
                _btnFillColor.BackColor = editZone.FillColor.ToColor();
            }
            else
            {
                _numX.Value = 0m;
                _numY.Value = 0m;
                _numW.Value = 0.5m;
                _numH.Value = 0.3m;
                _btnBorderColor.BackColor = Color.FromArgb(180, 180, 180);
                _btnFillColor.BackColor = Color.FromArgb(248, 220, 220);
            }

            // 按钮
            _btnDelete = new Button();
            _btnDelete.Text = "删除区域";
            _btnDelete.Location = new Point(10, 480);
            _btnDelete.Size = new Size(80, 28);
            _btnDelete.ForeColor = Color.Red;
            _btnDelete.Visible = (editZone != null);
            _btnDelete.Click += new EventHandler(OnDelete);
            this.Controls.Add(_btnDelete);

            _btnOk = new Button();
            _btnOk.Text = "确定";
            _btnOk.DialogResult = DialogResult.OK;
            _btnOk.Location = new Point(230, 480);
            _btnOk.Size = new Size(75, 28);
            this.Controls.Add(_btnOk);

            _btnCancel = new Button();
            _btnCancel.Text = "取消";
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.Location = new Point(315, 480);
            _btnCancel.Size = new Size(75, 28);
            this.Controls.Add(_btnCancel);

            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;

            UpdateConnectionVisibility();
            UpdateXYLabels();
        }

        private Label AddLabel(string text, int y, int lblW)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Location = new Point(10, y);
            lbl.Size = new Size(lblW, 20);
            lbl.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(lbl);
            return lbl;
        }

        /// <summary>布局变化时自动设置类型复选框</summary>
        private void OnLayoutChanged(object sender, EventArgs e)
        {
            int layoutIdx = _cmbLayout.SelectedIndex;
            // 自动勾选对应类型
            _chkTitleZone.Checked = (layoutIdx == (int)ZoneLayout.Title);
            _chkMemberZone.Checked = (layoutIdx == (int)ZoneLayout.Member);
            _chkClickZone.Checked = (layoutIdx == (int)ZoneLayout.Click);
            _chkConnectionZone.Checked = (layoutIdx == (int)ZoneLayout.Connection);

            // 标题区域默认锚定为正中
            if (layoutIdx == (int)ZoneLayout.Title && _cmbAnchor.SelectedIndex == 0)
                _cmbAnchor.SelectedIndex = (int)ZoneAnchor.MiddleCenter;

            UpdateConnectionVisibility();
            UpdateXYLabels();
        }

        /// <summary>类型复选框变化时同步布局下拉</summary>
        private void OnZoneTypeChanged(object sender, EventArgs e)
        {
            // 根据复选框状态更新布局下拉
            if (_chkTitleZone.Checked)
                _cmbLayout.SelectedIndex = (int)ZoneLayout.Title;
            else if (_chkMemberZone.Checked)
                _cmbLayout.SelectedIndex = (int)ZoneLayout.Member;
            else if (_chkClickZone.Checked)
                _cmbLayout.SelectedIndex = (int)ZoneLayout.Click;
            else if (_chkConnectionZone.Checked)
                _cmbLayout.SelectedIndex = (int)ZoneLayout.Connection;
            else
                _cmbLayout.SelectedIndex = (int)ZoneLayout.None;

            UpdateConnectionVisibility();
        }

        /// <summary>锚定方式变化时更新 X/Y 标签和范围</summary>
        private void OnAnchorChanged(object sender, EventArgs e)
        {
            UpdateXYLabels();
        }

        /// <summary>更新 X/Y 标签：绝对定位时为"X/Y"，其他时为"偏移X/Y"</summary>
        private void UpdateXYLabels()
        {
            bool isAbsolute = (_cmbAnchor.SelectedIndex == 0);
            _lblX.Text = isAbsolute ? "X：" : "偏移X：";
            _lblY.Text = isAbsolute ? "Y：" : "偏移Y：";
            // 偏移模式允许负值
            if (!isAbsolute)
            {
                _numX.Minimum = -1m;
                _numY.Minimum = -1m;
            }
            else
            {
                _numX.Minimum = 0m;
                _numY.Minimum = 0m;
                if (_numX.Value < 0) _numX.Value = 0;
                if (_numY.Value < 0) _numY.Value = 0;
            }
        }

        /// <summary>连接区域属性仅当勾选连接区域时显示</summary>
        private void UpdateConnectionVisibility()
        {
            bool showConn = _chkConnectionZone.Checked;
            _chkCanStart.Visible = showConn;
            _chkCanEnd.Visible = showConn;
            _chkAllowSelfConnect.Visible = showConn;
            _cmbLineTypes.Visible = showConn;
        }

        private void OnPickBorderColor(object sender, EventArgs e)
        {
            _colorDialog.Color = _btnBorderColor.BackColor;
            if (_colorDialog.ShowDialog() == DialogResult.OK)
                _btnBorderColor.BackColor = _colorDialog.Color;
        }

        private void OnPickFillColor(object sender, EventArgs e)
        {
            _colorDialog.Color = _btnFillColor.BackColor;
            if (_colorDialog.ShowDialog() == DialogResult.OK)
                _btnFillColor.BackColor = _colorDialog.Color;
        }

        private void OnDelete(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定删除此区域？", "确认删除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                DeleteRequested = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        /// <summary>将浮点值钳制到 NumericUpDown 可接受的范围。
        /// 绝对定位：[0, 1]；偏移模式：[-1, 1]。</summary>
        private static decimal ClampDecimal(float v, ZoneAnchor anchor)
        {
            bool isAbsolute = (anchor == ZoneAnchor.Absolute);
            float min = isAbsolute ? 0f : -1f;
            float max = 1f;
            if (v < min) v = min;
            if (v > max) v = max;
            return (decimal)v;
        }

        private NumericUpDown MakeNum(int x, int y, int w)
        {
            NumericUpDown num = new NumericUpDown();
            num.Location = new Point(x, y);
            num.Size = new Size(w, 22);
            num.DecimalPlaces = 2;
            num.Minimum = 0m;
            num.Maximum = 1m;
            num.Increment = 0.05m;
            return num;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (this.DialogResult == DialogResult.OK && !DeleteRequested)
            {
                string name = _txtName.Text.Trim();
                if (string.IsNullOrEmpty(name))
                    name = "Zone";

                ResultZone = new ShapeZone();
                ResultZone.Name = name;
                ResultZone.Layout = (ZoneLayout)_cmbLayout.SelectedIndex;
                ResultZone.Scaling = (ZoneScaling)_cmbScaling.SelectedIndex;
                ResultZone.Anchor = (ZoneAnchor)_cmbAnchor.SelectedIndex;
                ResultZone.X = (float)_numX.Value;
                ResultZone.Y = (float)_numY.Value;
                ResultZone.Width = (float)_numW.Value;
                ResultZone.Height = (float)_numH.Value;
                ResultZone.ShowBorder = _chkShowBorder.Checked;
                ResultZone.IsTitleZone = _chkTitleZone.Checked;
                ResultZone.IsMemberZone = _chkMemberZone.Checked;
                ResultZone.IsClickZone = _chkClickZone.Checked;
                ResultZone.IsConnectionZone = _chkConnectionZone.Checked;
                ResultZone.BorderColor = new XmlColor(_btnBorderColor.BackColor);
                ResultZone.FillColor = new XmlColor(_btnFillColor.BackColor);

                // 连接区域属性
                ResultZone.CanStart = _chkCanStart.Checked;
                ResultZone.CanEnd = _chkCanEnd.Checked;
                ResultZone.AllowSelfConnect = _chkAllowSelfConnect.Checked;
                switch (_cmbLineTypes.SelectedIndex)
                {
                    case 0: ResultZone.AllowedLineTypes = "Straight,Curve,Orthogonal"; break;
                    case 1: ResultZone.AllowedLineTypes = "Straight"; break;
                    case 2: ResultZone.AllowedLineTypes = "Curve"; break;
                    case 3: ResultZone.AllowedLineTypes = "Orthogonal"; break;
                    case 4: ResultZone.AllowedLineTypes = "Straight,Curve"; break;
                    case 5: ResultZone.AllowedLineTypes = "Straight,Orthogonal"; break;
                    case 6: ResultZone.AllowedLineTypes = "Curve,Orthogonal"; break;
                    default: ResultZone.AllowedLineTypes = "Straight,Curve,Orthogonal"; break;
                }
            }
        }
    }
}
