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
    /// </summary>
    public class CustomShapeDialog : Form
    {
        // 通用控件
        private TextBox _txtName;
        private TabControl _tabControl;
        private Button _btnOk;
        private Button _btnCancel;
        private ColorDialog _colorDialog;

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

        // 多路径工具栏控件
        private Panel _panelPathToolbar;
        private ComboBox _cmbPathSelect;
        private Button _btnNewPath;
        private Button _btnDeletePath;
        private ComboBox _cmbBoolOp;
        private bool _suppressPathChange = false;

        // Zone 编辑控件
        private GroupBox _grpZones;
        private ListBox _listZones;
        private Button _btnAddZone;
        private Button _btnEditZone;
        private Button _btnDeleteZone;
        private List<ShapeZone> _zones = new List<ShapeZone>();

        // 编辑器画布数据（多路径）
        private List<List<PointF>> _paths = new List<List<PointF>>();
        private int _currentPathIndex = 0;
        private BooleanOperation _boolOp = BooleanOperation.None;
        private int _dragIndex = -1;
        private int _selectedVertex = -1;
        private bool _closedPath = false;
        private bool _filled = true;
        private Color _geomFillColor = Color.FromArgb(220, 240, 255);
        private Color _geomBorderColor = Color.FromArgb(80, 120, 180);
        private Color _stateFillColor = Color.FromArgb(230, 240, 255);
        private Color _stateBorderColor = Color.FromArgb(80, 120, 180);
        private Color _stateTextColor = Color.FromArgb(40, 40, 40);
        private Color _stateHeaderColor = Color.FromArgb(80, 130, 180);

        /// <summary>当前正在编辑的状态索引，-1 表示无选择</summary>
        private int _editingStateIndex = -1;

        /// <summary>抑制状态列表 SelectedIndexChanged 事件的标志，用于 RefreshStateList 期间避免事件级联</summary>
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

        public CustomShapeDialog() : this(null) { }

        public CustomShapeDialog(ShapeType editShape)
        {
            this.Text = (editShape == null) ? "创建自定义图形" : "编辑自定义图形";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.MaximizeBox = false;
            this.ClientSize = new Size(720, 640);

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
            _tabControl.Size = new Size(690, 550);
            this.Controls.Add(_tabControl);

            BuildStateTab();
            BuildActionTab();

            _btnOk = new Button();
            _btnOk.Text = "确定";
            _btnOk.DialogResult = DialogResult.OK;
            _btnOk.Location = new Point(520, 600);
            _btnOk.Size = new Size(80, 30);
            this.Controls.Add(_btnOk);

            _btnCancel = new Button();
            _btnCancel.Text = "取消";
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.Location = new Point(610, 600);
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

            // 初始加载第一个状态到编辑器（抑制事件，手动加载）
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

            // 分隔
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

            // 分隔
            tbX += 6;

            _btnCopyDefault = MakeSmallButton("CP", "复制初始图形", tbX, toolbarY, tbBtnW + 10, tbBtnH);
            _btnCopyDefault.Click += new EventHandler(OnCopyDefaultGeom);
            page.Controls.Add(_btnCopyDefault);
            tbX += tbBtnW + 10 + tbGap;

            _btnClearGeom = MakeSmallButton("CLR", "清除图形", tbX, toolbarY, tbBtnW + 10, tbBtnH);
            _btnClearGeom.Click += new EventHandler(OnClearGeom);
            page.Controls.Add(_btnClearGeom);

            // 第三行：多路径工具栏（路径切换 / 新建 / 删除 + 布尔运算）
            _panelPathToolbar = new Panel();
            _panelPathToolbar.Location = new Point(rightX, 62);
            _panelPathToolbar.Size = new Size(485, 26);
            page.Controls.Add(_panelPathToolbar);

            Label lblPath = new Label();
            lblPath.Text = "路径：";
            lblPath.Location = new Point(0, 4);
            lblPath.Size = new Size(34, 20);
            lblPath.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            _panelPathToolbar.Controls.Add(lblPath);

            _cmbPathSelect = new ComboBox();
            _cmbPathSelect.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbPathSelect.Location = new Point(34, 2);
            _cmbPathSelect.Size = new Size(110, 22);
            _cmbPathSelect.SelectedIndexChanged += new EventHandler(OnPathSelectedChanged);
            _panelPathToolbar.Controls.Add(_cmbPathSelect);

            _btnNewPath = MakeSmallButton("+P", "新建路径", 148, 0, 28, 24);
            _btnNewPath.Click += new EventHandler(OnNewPath);
            _panelPathToolbar.Controls.Add(_btnNewPath);

            _btnDeletePath = MakeSmallButton("-P", "删除当前路径", 179, 0, 28, 24);
            _btnDeletePath.Click += new EventHandler(OnDeletePath);
            _panelPathToolbar.Controls.Add(_btnDeletePath);

            Label lblBool = new Label();
            lblBool.Text = "布尔：";
            lblBool.Location = new Point(214, 4);
            lblBool.Size = new Size(34, 20);
            lblBool.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            _panelPathToolbar.Controls.Add(lblBool);

            _cmbBoolOp = new ComboBox();
            _cmbBoolOp.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbBoolOp.Location = new Point(248, 2);
            _cmbBoolOp.Size = new Size(125, 22);
            _cmbBoolOp.Items.Add("无 (独立)");
            _cmbBoolOp.Items.Add("并集 (Union)");
            _cmbBoolOp.Items.Add("差集 (Subtract)");
            _cmbBoolOp.Items.Add("交集 (Intersect)");
            _cmbBoolOp.Items.Add("异或 (Xor)");
            _cmbBoolOp.SelectedIndex = 0;
            _cmbBoolOp.SelectedIndexChanged += new EventHandler(OnBoolOpChanged);
            _panelPathToolbar.Controls.Add(_cmbBoolOp);

            // 画布
            _canvasPanel = new Panel();
            _canvasPanel.Location = new Point(rightX, 92);
            _canvasPanel.Size = new Size(485, 250);
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
            _lblDefaultHint.Location = new Point(rightX, 92);
            _lblDefaultHint.Size = new Size(485, 250);
            _lblDefaultHint.ForeColor = Color.FromArgb(120, 120, 120);
            _lblDefaultHint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            _lblDefaultHint.BorderStyle = BorderStyle.FixedSingle;
            _lblDefaultHint.BackColor = Color.FromArgb(245, 245, 245);
            page.Controls.Add(_lblDefaultHint);

            // === Zone 编辑区 ===
            BuildZoneSection(page, rightX, 346);

            _tabControl.TabPages.Add(page);
        }

        /// <summary>构建状态选项卡底部的 Zone 编辑区</summary>
        private void BuildZoneSection(TabPage page, int x, int y)
        {
            _grpZones = new GroupBox();
            _grpZones.Text = "区域 (Zones)";
            _grpZones.Location = new Point(x, y);
            _grpZones.Size = new Size(485, 178);
            page.Controls.Add(_grpZones);

            _listZones = new ListBox();
            _listZones.Location = new Point(10, 20);
            _listZones.Size = new Size(250, 145);
            _listZones.BorderStyle = BorderStyle.FixedSingle;
            _listZones.SelectedIndexChanged += new EventHandler(OnZoneListSelectedIndexChanged);
            _grpZones.Controls.Add(_listZones);

            int bx = 270;
            int by = 20;
            _btnAddZone = MakeSmallButton("+Z", "添加区域", bx, by, 40, 26);
            _btnAddZone.Click += new EventHandler(OnAddZone);
            _grpZones.Controls.Add(_btnAddZone);
            by += 30;

            _btnEditZone = MakeSmallButton("\u270E", "编辑区域", bx, by, 40, 26);
            _btnEditZone.Click += new EventHandler(OnEditZone);
            _grpZones.Controls.Add(_btnEditZone);
            by += 30;

            _btnDeleteZone = MakeSmallButton("-Z", "删除区域", bx, by, 40, 26);
            _btnDeleteZone.Click += new EventHandler(OnDeleteZone);
            _grpZones.Controls.Add(_btnDeleteZone);

            Label lblZoneHint = new Label();
            lblZoneHint.Text = "区域定义图形内部的逻辑分区\n（标题/成员/子元件）。\n每个状态可拥有独立的 Zone 集合。";
            lblZoneHint.Location = new Point(320, 20);
            lblZoneHint.Size = new Size(155, 90);
            lblZoneHint.ForeColor = Color.FromArgb(100, 100, 100);
            _grpZones.Controls.Add(lblZoneHint);
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

        private ToolTip toolTip1 = new ToolTip();

        private void BuildActionTab()
        {
            TabPage page = new TabPage("行为");
            page.BorderStyle = BorderStyle.None;

            _listActions = new ListBox();
            _listActions.Location = new Point(10, 10);
            _listActions.Size = new Size(460, 340);
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
            lblHint.Text = "提示：\n行为定义图形在右键\n菜单中可用的操作。\n切换状态时可枚举\n选择已定义的目标状态。";
            lblHint.Location = new Point(x, y);
            lblHint.Size = new Size(110, 90);
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

            if (st.CustomActions != null)
            {
                foreach (ShapeAction action in st.CustomActions)
                {
                    ShapeAction copy = new ShapeAction();
                    copy.Name = action.Name;
                    copy.ActionType = action.ActionType;
                    copy.TargetState = action.TargetState;
                    copy.CallbackName = action.CallbackName;
                    copy.IconName = action.IconName;
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
                // 多路径与布尔运算（CompoundPolygon）
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
            // 抑制 SelectedIndexChanged 事件，避免 Items.Clear / SetSelected 期间的事件级联
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

        /// <summary>
        /// 状态列表选择变化时，先将当前编辑保存回状态，再加载新选中状态的数据到编辑器。
        /// 在 RefreshStateList 期间通过 _suppressStateChange 标志抑制此处理器，避免事件级联。
        /// </summary>
        private void OnStateListSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressStateChange)
                return;

            int oldIdx = _editingStateIndex;
            int newIdx = _listStates.SelectedIndex;

            // 仅当索引实际变化时才保存旧状态，避免无谓覆写
            if (oldIdx != newIdx && oldIdx >= 0 && oldIdx < _states.Count)
                SaveCurrentEditorToState();

            _editingStateIndex = newIdx;
            LoadStateToEditor(newIdx);
            UpdateStateButtons();
        }

        /// <summary>将编辑器中的数据保存到当前正在编辑的状态</summary>
        private void SaveCurrentEditorToState()
        {
            if (_editingStateIndex < 0 || _editingStateIndex >= _states.Count)
                return;

            ShapeState s = _states[_editingStateIndex];

            // 初始状态名不可更改
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

            // 保存当前 Zone 列表到状态
            s.CustomZones = CloneShapeZones(_zones);
        }

        /// <summary>将指定状态的数据加载到编辑器控件</summary>
        private void LoadStateToEditor(int idx)
        {
            if (idx < 0 || idx >= _states.Count)
            {
                _txtStateName.Text = "";
                _numPriority.Value = 0;
                _chkFilled.Checked = true;
                _chkCustomGeom.Checked = false;
                _paths.Clear();
                _paths.Add(new List<PointF>());
                _currentPathIndex = 0;
                _selectedVertex = -1;
                _dragIndex = -1;
                _zones.Clear();
                RefreshZoneList();
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
                _paths.Add(new List<PointF>());
                _currentPathIndex = 0;
                _selectedVertex = -1;
                _dragIndex = -1;
                RefreshPathCombo();
            }

            // 从图形命令中同步填充/颜色（兼容 Polygon 与 CompoundPolygon）
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
                    // 同步状态颜色，使工具栏色块与实际绘制颜色一致
                    _stateFillColor = _geomFillColor;
                    _stateBorderColor = _geomBorderColor;
                }
            }

            // 加载该状态的 Zone 列表
            _zones = CloneShapeZones(s.CustomZones);
            RefreshZoneList();

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
            // 先保存当前编辑
            SaveCurrentEditorToState();

            ShapeState newState = new ShapeState();
            newState.Name = "State" + (_states.Count + 1);
            newState.UseCustomRenderCommands = false;
            newState.CustomRenderCommands = new List<RenderCommand>();
            _states.Add(newState);
            RefreshStateList();

            // 选中新状态并加载到编辑器（抑制事件，避免处理器用旧索引保存到错误状态）
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

            // 状态已重排序，必须抑制事件，否则处理器会用旧 _editingStateIndex 保存到错误状态
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
                return; // 初始状态名不可改
            _states[_editingStateIndex].Name = string.IsNullOrEmpty(_txtStateName.Text.Trim())
                ? "State" : _txtStateName.Text.Trim();
            // 刷新列表显示
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
            _chkFilled.Visible = visible;
            _panelPathToolbar.Visible = visible;
            _lblDefaultHint.Visible = !visible;
            // Zone 区域属于状态属性，始终可见
        }

        #endregion

        #region 编辑器 — 图形加载与构建

        /// <summary>获取当前正在编辑的路径（确保至少存在一条路径）</summary>
        private List<PointF> CurrentPath()
        {
            if (_paths.Count == 0)
                _paths.Add(new List<PointF>());
            if (_currentPathIndex < 0 || _currentPathIndex >= _paths.Count)
                _currentPathIndex = 0;
            return _paths[_currentPathIndex];
        }

        private void LoadVerticesFromCommands(List<RenderCommand> cmds)
        {
            _paths.Clear();
            _currentPathIndex = 0;
            _closedPath = false;
            _selectedVertex = -1;
            _dragIndex = -1;

            if (cmds == null || cmds.Count == 0)
            {
                _paths.Add(new List<PointF>());
                RefreshPathCombo();
                return;
            }

            float canvasW = (float)_canvasPanel.Width - 40f;
            float canvasH = (float)_canvasPanel.Height - 40f;
            float offsetX = 20f;
            float offsetY = 20f;

            // 优先查找 CompoundPolygon（多路径 + 布尔运算）
            RenderCommand compoundCmd = null;
            foreach (RenderCommand cmd in cmds)
            {
                if (cmd.CommandType == RenderCommandType.CompoundPolygon)
                { compoundCmd = cmd; break; }
            }

            if (compoundCmd != null && compoundCmd.MultiPaths != null && compoundCmd.MultiPaths.Count > 0)
            {
                _boolOp = compoundCmd.BoolOp;
                foreach (PointF[] pts in compoundCmd.MultiPaths)
                {
                    List<PointF> path = new List<PointF>();
                    if (pts != null)
                    {
                        foreach (PointF pt in pts)
                            path.Add(new PointF(offsetX + pt.X * canvasW, offsetY + pt.Y * canvasH));
                    }
                    _paths.Add(path);
                }
                if (_paths.Count > 0)
                    _closedPath = true;
                _geomFillColor = compoundCmd.FillColor;
                _geomBorderColor = compoundCmd.StrokeColor;
                _filled = compoundCmd.Fill;
                RefreshPathCombo();
                return;
            }

            // 回退：普通 Polygon（向后兼容，仅一条路径）
            RenderCommand polyCmd = null;
            foreach (RenderCommand cmd in cmds)
            {
                if (cmd.CommandType == RenderCommandType.Polygon)
                { polyCmd = cmd; break; }
            }

            if (polyCmd != null && polyCmd.PolygonPoints != null && polyCmd.PolygonPoints.Length >= 3)
            {
                List<PointF> path = new List<PointF>();
                foreach (PointF pt in polyCmd.PolygonPoints)
                    path.Add(new PointF(offsetX + pt.X * canvasW, offsetY + pt.Y * canvasH));
                _paths.Add(path);
                _closedPath = true;
                _boolOp = BooleanOperation.None;
                _geomFillColor = polyCmd.FillColor;
                _geomBorderColor = polyCmd.StrokeColor;
                _filled = polyCmd.Fill;
            }
            else
            {
                _paths.Add(new List<PointF>());
            }
            RefreshPathCombo();
        }

        private List<RenderCommand> BuildRenderCommandsFromVertices()
        {
            // 收集所有有效路径（>=3 个顶点）
            List<List<PointF>> validPaths = new List<List<PointF>>();
            foreach (List<PointF> path in _paths)
            {
                if (path != null && path.Count >= 3)
                    validPaths.Add(path);
            }
            if (validPaths.Count == 0)
                return null;

            // 计算所有路径的全局包围盒，统一归一化
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (List<PointF> path in validPaths)
            {
                foreach (PointF pt in path)
                {
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

            // 单路径：普通 Polygon（向后兼容）
            if (validPaths.Count == 1)
            {
                List<PointF> path = validPaths[0];
                PointF[] normalized = new PointF[path.Count];
                for (int i = 0; i < path.Count; i++)
                    normalized[i] = new PointF((path[i].X - minX) / rangeX, (path[i].Y - minY) / rangeY);

                RenderCommand polyCmd = new RenderCommand();
                polyCmd.CommandType = RenderCommandType.Polygon;
                polyCmd.PolygonPoints = normalized;
                polyCmd.X = 0; polyCmd.Y = 0;
                polyCmd.Width = 1; polyCmd.Height = 1;
                polyCmd.FillColor = _filled ? _geomFillColor : Color.Transparent;
                polyCmd.StrokeColor = _geomBorderColor;
                polyCmd.StrokeWidth = 2f;
                polyCmd.Fill = _filled;
                return new List<RenderCommand> { polyCmd };
            }

            // 多路径：CompoundPolygon
            RenderCommand cmd = new RenderCommand();
            cmd.CommandType = RenderCommandType.CompoundPolygon;
            cmd.MultiPaths = new List<PointF[]>();
            foreach (List<PointF> path in validPaths)
            {
                PointF[] normalized = new PointF[path.Count];
                for (int i = 0; i < path.Count; i++)
                    normalized[i] = new PointF((path[i].X - minX) / rangeX, (path[i].Y - minY) / rangeY);
                cmd.MultiPaths.Add(normalized);
            }
            cmd.BoolOp = _boolOp;
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
            _paths.Add(new List<PointF>());
            _currentPathIndex = 0;
            _selectedVertex = -1;
            _dragIndex = -1;
            _closedPath = false;
            RefreshPathCombo();
            _canvasPanel.Invalidate();
        }

        #endregion

        #region 编辑器 — 画布事件

        private void OnCanvasPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            if (_paths.Count == 0)
                return;

            // 绘制每条路径，当前路径高亮
            for (int p = 0; p < _paths.Count; p++)
            {
                List<PointF> verts = _paths[p];
                if (verts == null || verts.Count == 0)
                    continue;

                bool isCurrent = (p == _currentPathIndex);

                // 填充
                if (_filled && verts.Count >= 3)
                {
                    GraphicsPath gp = new GraphicsPath();
                    for (int i = 0; i < verts.Count; i++)
                        gp.AddLine(verts[i], verts[(i + 1) % verts.Count]);
                    if (_closedPath) gp.CloseFigure();
                    int alpha = isCurrent ? 180 : 70;
                    using (Brush brush = new SolidBrush(Color.FromArgb(alpha, _geomFillColor)))
                        g.FillPath(brush, gp);
                    gp.Dispose();
                }

                // 描边（非当前路径使用半透明描边以区分）
                Color borderColor = isCurrent ? _geomBorderColor : Color.FromArgb(160, _geomBorderColor);
                float penWidth = isCurrent ? 2f : 1f;
                using (Pen pen = new Pen(borderColor, penWidth))
                {
                    for (int i = 0; i < verts.Count - 1; i++)
                        g.DrawLine(pen, verts[i], verts[i + 1]);
                    if (_closedPath && verts.Count >= 3)
                        g.DrawLine(pen, verts[verts.Count - 1], verts[0]);
                }

                // 仅在当前路径绘制顶点手柄，避免视觉混乱
                if (isCurrent)
                {
                    for (int i = 0; i < verts.Count; i++)
                    {
                        RectangleF handle = new RectangleF(verts[i].X - 5, verts[i].Y - 5, 10, 10);
                        bool isSelected = (i == _selectedVertex);
                        using (Brush brush = new SolidBrush(isSelected ? Color.FromArgb(0, 120, 215) : Color.White))
                        using (Pen pen = new Pen(_geomBorderColor, isSelected ? 2f : 1f))
                        {
                            g.FillRectangle(brush, handle);
                            g.DrawRectangle(pen, handle.X, handle.Y, handle.Width, handle.Height);
                        }
                    }
                }
            }

            // 顶部信息标注
            using (Font font = new Font("Segoe UI", 8f))
            using (Brush brush = new SolidBrush(Color.FromArgb(80, 80, 80)))
            {
                g.DrawString(string.Format("路径 {0}/{1}   布尔: {2}", _currentPathIndex + 1, _paths.Count, _boolOp),
                    font, brush, 6, 4);
            }
        }

        private void OnCanvasMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                List<PointF> verts = CurrentPath();
                int hitIdx = HitTestVertex(e.Location);
                if (hitIdx >= 0)
                {
                    _selectedVertex = hitIdx;
                    _dragIndex = hitIdx;
                    _canvasPanel.Cursor = Cursors.SizeAll;
                }
                else
                {
                    verts.Add(new PointF(e.X, e.Y));
                    _selectedVertex = verts.Count - 1;
                    RefreshPathCombo();
                    _canvasPanel.Invalidate();
                }
            }
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragIndex >= 0)
            {
                List<PointF> verts = CurrentPath();
                if (_dragIndex < verts.Count)
                    verts[_dragIndex] = new PointF(e.X, e.Y);
                _canvasPanel.Invalidate();
            }
            else
            {
                int hitIdx = HitTestVertex(e.Location);
                _canvasPanel.Cursor = hitIdx >= 0 ? Cursors.SizeAll : Cursors.Cross;
            }
        }

        private void OnCanvasMouseUp(object sender, MouseEventArgs e)
        {
            if (_dragIndex >= 0)
            {
                _dragIndex = -1;
                _canvasPanel.Cursor = Cursors.Default;
            }
        }

        private void OnCanvasDoubleClick(object sender, EventArgs e)
        {
            MouseEventArgs me = (MouseEventArgs)e;
            int hitIdx = HitTestVertex(me.Location);
            if (hitIdx >= 0)
            {
                List<PointF> verts = CurrentPath();
                verts.RemoveAt(hitIdx);
                if (_selectedVertex >= verts.Count)
                    _selectedVertex = verts.Count - 1;
                RefreshPathCombo();
                _canvasPanel.Invalidate();
            }
        }

        private int HitTestVertex(Point pt)
        {
            List<PointF> verts = CurrentPath();
            for (int i = 0; i < verts.Count; i++)
            {
                float dx = verts[i].X - pt.X;
                float dy = verts[i].Y - pt.Y;
                if (dx * dx + dy * dy <= 64)
                    return i;
            }
            return -1;
        }

        private void OnAddVertex(object sender, EventArgs e)
        {
            List<PointF> verts = CurrentPath();
            PointF newPt;
            if (verts.Count > 0)
            {
                PointF last = verts[verts.Count - 1];
                newPt = new PointF(last.X + 30, last.Y + 30);
            }
            else
            {
                newPt = new PointF(220, 160);
            }
            verts.Add(newPt);
            _selectedVertex = verts.Count - 1;
            RefreshPathCombo();
            _canvasPanel.Invalidate();
        }

        private void OnDeleteVertex(object sender, EventArgs e)
        {
            List<PointF> verts = CurrentPath();
            if (_selectedVertex >= 0 && _selectedVertex < verts.Count)
            {
                verts.RemoveAt(_selectedVertex);
                if (_selectedVertex >= verts.Count)
                    _selectedVertex = verts.Count - 1;
                RefreshPathCombo();
                _canvasPanel.Invalidate();
            }
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
            _paths.Add(new List<PointF>());
            _currentPathIndex = _paths.Count - 1;
            _selectedVertex = -1;
            _dragIndex = -1;
            RefreshPathCombo();
            _canvasPanel.Invalidate();
        }

        private void OnDeletePath(object sender, EventArgs e)
        {
            // 仅剩一条路径时清空其顶点而非移除整条路径
            if (_paths.Count <= 1)
            {
                CurrentPath().Clear();
                _selectedVertex = -1;
                _dragIndex = -1;
                RefreshPathCombo();
                _canvasPanel.Invalidate();
                return;
            }
            _paths.RemoveAt(_currentPathIndex);
            if (_currentPathIndex >= _paths.Count)
                _currentPathIndex = _paths.Count - 1;
            _selectedVertex = -1;
            _dragIndex = -1;
            RefreshPathCombo();
            _canvasPanel.Invalidate();
        }

        private void OnPathSelectedChanged(object sender, EventArgs e)
        {
            if (_suppressPathChange)
                return;
            int idx = _cmbPathSelect.SelectedIndex;
            if (idx >= 0 && idx < _paths.Count)
            {
                _currentPathIndex = idx;
                _selectedVertex = -1;
                _canvasPanel.Invalidate();
            }
        }

        private void OnBoolOpChanged(object sender, EventArgs e)
        {
            if (_cmbBoolOp.SelectedIndex >= 0)
            {
                _boolOp = (BooleanOperation)_cmbBoolOp.SelectedIndex;
                _canvasPanel.Invalidate();
            }
        }

        /// <summary>刷新路径选择下拉框及布尔运算下拉框的显示</summary>
        private void RefreshPathCombo()
        {
            if (_cmbPathSelect == null || _cmbBoolOp == null)
                return;
            _suppressPathChange = true;
            try
            {
                _cmbPathSelect.Items.Clear();
                for (int i = 0; i < _paths.Count; i++)
                {
                    int vc = (_paths[i] != null) ? _paths[i].Count : 0;
                    _cmbPathSelect.Items.Add(string.Format("路径 {0} ({1})", i + 1, vc));
                }
                if (_currentPathIndex < 0 || _currentPathIndex >= _paths.Count)
                    _currentPathIndex = 0;
                if (_paths.Count > 0)
                    _cmbPathSelect.SelectedIndex = _currentPathIndex;

                int targetBool = (int)_boolOp;
                if (_cmbBoolOp.Items.Count > targetBool && _cmbBoolOp.SelectedIndex != targetBool)
                    _cmbBoolOp.SelectedIndex = targetBool;
            }
            finally
            {
                _suppressPathChange = false;
            }
        }

        #endregion

        #region 编辑器 — Zone 管理

        private void RefreshZoneList()
        {
            int prevSel = _listZones.SelectedIndex;
            _listZones.Items.Clear();
            for (int i = 0; i < _zones.Count; i++)
            {
                ShapeZone z = _zones[i];
                string flags = "";
                if (z.IsTitleZone) flags += " [标题]";
                if (z.IsMemberZone) flags += " [成员]";
                _listZones.Items.Add(string.Format("{0} ({1}){2}", z.Name, z.Layout, flags));
            }
            if (prevSel >= 0 && prevSel < _listZones.Items.Count)
                _listZones.SelectedIndex = prevSel;
            else if (_listZones.Items.Count > 0)
                _listZones.SelectedIndex = 0;
            UpdateZoneButtons();
        }

        private void UpdateZoneButtons()
        {
            bool hasSel = (_listZones.SelectedIndex >= 0 && _listZones.SelectedIndex < _zones.Count);
            _btnEditZone.Enabled = hasSel;
            _btnDeleteZone.Enabled = hasSel;
        }

        private void OnZoneListSelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateZoneButtons();
        }

        private void OnAddZone(object sender, EventArgs e)
        {
            ShapeZone zone = new ShapeZone();
            zone.Name = "Zone" + (_zones.Count + 1);
            zone.X = 0.1f; zone.Y = 0.1f;
            zone.Width = 0.8f; zone.Height = 0.8f;
            using (ShapeZoneEditDialog dlg = new ShapeZoneEditDialog(zone))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultZone != null)
                {
                    _zones.Add(dlg.ResultZone);
                    RefreshZoneList();
                    if (_listZones.Items.Count > 0)
                        _listZones.SelectedIndex = _listZones.Items.Count - 1;
                }
            }
        }

        private void OnEditZone(object sender, EventArgs e)
        {
            int idx = _listZones.SelectedIndex;
            if (idx < 0 || idx >= _zones.Count)
                return;
            using (ShapeZoneEditDialog dlg = new ShapeZoneEditDialog(_zones[idx]))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultZone != null)
                {
                    _zones[idx] = dlg.ResultZone;
                    RefreshZoneList();
                }
            }
        }

        private void OnDeleteZone(object sender, EventArgs e)
        {
            int idx = _listZones.SelectedIndex;
            if (idx < 0 || idx >= _zones.Count)
                return;
            if (MessageBox.Show(string.Format("确定删除区域 \"{0}\"？", _zones[idx].Name),
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _zones.RemoveAt(idx);
                RefreshZoneList();
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
                string typeStr = (action.ActionType == ShapeActionType.StateChange) ? "[切换状态]" : "[宿主回调]";
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
                // 保存当前编辑器状态
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

            // 从默认状态复制 Zone 到 ShapeType（作为类型级 Zone 集合）
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

            st.CustomActions = new List<ShapeAction>();
            foreach (ShapeAction a in _actions)
            {
                ShapeAction copy = new ShapeAction();
                copy.Name = a.Name;
                copy.ActionType = a.ActionType;
                copy.TargetState = a.TargetState;
                copy.CallbackName = a.CallbackName;
                copy.IconName = a.IconName;
                st.CustomActions.Add(copy);
            }

            _resultShapeType = st;
        }

        #endregion
    }

    /// <summary>
    /// 图形区域（Zone）编辑对话框。用于添加或编辑单个 ShapeZone。
    /// 风格与 ShapeActionEditDialog 一致，作为 CustomShapeDialog 的子对话框使用。
    /// </summary>
    public class ShapeZoneEditDialog : Form
    {
        private TextBox _txtName;
        private ComboBox _cmbLayout;
        private ComboBox _cmbScaling;
        private NumericUpDown _numX;
        private NumericUpDown _numY;
        private NumericUpDown _numW;
        private NumericUpDown _numH;
        private CheckBox _chkShowBorder;
        private CheckBox _chkTitleZone;
        private CheckBox _chkMemberZone;
        private Button _btnOk;
        private Button _btnCancel;

        public ShapeZone ResultZone { get; private set; }

        public ShapeZoneEditDialog() : this(null) { }

        public ShapeZoneEditDialog(ShapeZone editZone)
        {
            this.Text = (editZone == null) ? "添加区域" : "编辑区域";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(360, 260);

            int y = 12;
            int lblW = 80;
            int xVal = 95;
            int valW = 250;

            // 名称
            Label lblName = new Label();
            lblName.Text = "名称：";
            lblName.Location = new Point(10, y);
            lblName.Size = new Size(lblW, 20);
            lblName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(lblName);

            _txtName = new TextBox();
            _txtName.Location = new Point(xVal, y);
            _txtName.Size = new Size(valW, 22);
            _txtName.Text = (editZone != null) ? editZone.Name : "Zone";
            this.Controls.Add(_txtName);
            y += 32;

            // 布局
            Label lblLayout = new Label();
            lblLayout.Text = "布局：";
            lblLayout.Location = new Point(10, y);
            lblLayout.Size = new Size(lblW, 20);
            lblLayout.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(lblLayout);

            _cmbLayout = new ComboBox();
            _cmbLayout.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbLayout.Location = new Point(xVal, y);
            _cmbLayout.Size = new Size(valW, 22);
            _cmbLayout.Items.Add("None");
            _cmbLayout.Items.Add("Title");
            _cmbLayout.Items.Add("Stack");
            _cmbLayout.Items.Add("Flow");
            _cmbLayout.Items.Add("Member");
            _cmbLayout.SelectedIndex = (editZone != null) ? (int)editZone.Layout : 0;
            this.Controls.Add(_cmbLayout);
            y += 32;

            // 缩放
            Label lblScaling = new Label();
            lblScaling.Text = "缩放：";
            lblScaling.Location = new Point(10, y);
            lblScaling.Size = new Size(lblW, 20);
            lblScaling.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(lblScaling);

            _cmbScaling = new ComboBox();
            _cmbScaling.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbScaling.Location = new Point(xVal, y);
            _cmbScaling.Size = new Size(valW, 22);
            _cmbScaling.Items.Add("None");
            _cmbScaling.Items.Add("Freeze");
            _cmbScaling.SelectedIndex = (editZone != null) ? (int)editZone.Scaling : 0;
            this.Controls.Add(_cmbScaling);
            y += 36;

            // X / Y
            Label lblXY = new Label();
            lblXY.Text = "X / Y：";
            lblXY.Location = new Point(10, y);
            lblXY.Size = new Size(lblW, 20);
            lblXY.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(lblXY);
            _numX = MakeNum(xVal, y);
            _numY = MakeNum(xVal + 130, y);
            this.Controls.Add(_numX);
            this.Controls.Add(_numY);
            y += 32;

            // 宽 / 高
            Label lblWH = new Label();
            lblWH.Text = "宽 / 高：";
            lblWH.Location = new Point(10, y);
            lblWH.Size = new Size(lblW, 20);
            lblWH.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(lblWH);
            _numW = MakeNum(xVal, y);
            _numH = MakeNum(xVal + 130, y);
            this.Controls.Add(_numW);
            this.Controls.Add(_numH);
            y += 36;

            // 复选框
            _chkShowBorder = new CheckBox();
            _chkShowBorder.Text = "显示边框";
            _chkShowBorder.Location = new Point(xVal, y);
            _chkShowBorder.Size = new Size(85, 22);
            _chkShowBorder.Checked = (editZone != null) ? editZone.ShowBorder : false;
            this.Controls.Add(_chkShowBorder);

            _chkTitleZone = new CheckBox();
            _chkTitleZone.Text = "标题区域";
            _chkTitleZone.Location = new Point(xVal + 90, y);
            _chkTitleZone.Size = new Size(85, 22);
            _chkTitleZone.Checked = (editZone != null) ? editZone.IsTitleZone : false;
            this.Controls.Add(_chkTitleZone);

            _chkMemberZone = new CheckBox();
            _chkMemberZone.Text = "成员区域";
            _chkMemberZone.Location = new Point(xVal + 180, y);
            _chkMemberZone.Size = new Size(85, 22);
            _chkMemberZone.Checked = (editZone != null) ? editZone.IsMemberZone : false;
            this.Controls.Add(_chkMemberZone);

            // 预填数值
            if (editZone != null)
            {
                _numX.Value = ClampDecimal(editZone.X);
                _numY.Value = ClampDecimal(editZone.Y);
                _numW.Value = ClampDecimal(editZone.Width);
                _numH.Value = ClampDecimal(editZone.Height);
            }
            else
            {
                _numX.Value = 0m;
                _numY.Value = 0m;
                _numW.Value = 1m;
                _numH.Value = 1m;
            }

            // 确定 / 取消
            _btnOk = new Button();
            _btnOk.Text = "确定";
            _btnOk.DialogResult = DialogResult.OK;
            _btnOk.Location = new Point(190, 222);
            _btnOk.Size = new Size(75, 28);
            this.Controls.Add(_btnOk);

            _btnCancel = new Button();
            _btnCancel.Text = "取消";
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.Location = new Point(280, 222);
            _btnCancel.Size = new Size(75, 28);
            this.Controls.Add(_btnCancel);

            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;
        }

        private static decimal ClampDecimal(float v)
        {
            if (v < 0f) v = 0f;
            if (v > 1f) v = 1f;
            return (decimal)v;
        }

        private NumericUpDown MakeNum(int x, int y)
        {
            NumericUpDown num = new NumericUpDown();
            num.Location = new Point(x, y);
            num.Size = new Size(115, 22);
            num.DecimalPlaces = 2;
            num.Minimum = 0m;
            num.Maximum = 1m;
            num.Increment = 0.05m;
            return num;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (this.DialogResult == DialogResult.OK)
            {
                string name = _txtName.Text.Trim();
                if (string.IsNullOrEmpty(name))
                    name = "Zone";

                ResultZone = new ShapeZone();
                ResultZone.Name = name;
                ResultZone.Layout = (ZoneLayout)_cmbLayout.SelectedIndex;
                ResultZone.Scaling = (ZoneScaling)_cmbScaling.SelectedIndex;
                ResultZone.X = (float)_numX.Value;
                ResultZone.Y = (float)_numY.Value;
                ResultZone.Width = (float)_numW.Value;
                ResultZone.Height = (float)_numH.Value;
                ResultZone.ShowBorder = _chkShowBorder.Checked;
                ResultZone.IsTitleZone = _chkTitleZone.Checked;
                ResultZone.IsMemberZone = _chkMemberZone.Checked;
            }
        }
    }
}
