using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CloudNativeDesigner.Core;

namespace CloudNativeDesigner.Controls
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

        // 编辑器画布数据
        private List<PointF> _vertices = new List<PointF>();
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
            this.ClientSize = new Size(720, 560);

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
            _tabControl.Size = new Size(690, 470);
            this.Controls.Add(_tabControl);

            BuildStateTab();
            BuildActionTab();

            _btnOk = new Button();
            _btnOk.Text = "确定";
            _btnOk.DialogResult = DialogResult.OK;
            _btnOk.Location = new Point(520, 520);
            _btnOk.Size = new Size(80, 30);
            this.Controls.Add(_btnOk);

            _btnCancel = new Button();
            _btnCancel.Text = "取消";
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.Location = new Point(610, 520);
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
            int rightW = 485;

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

            // 画布
            _canvasPanel = new Panel();
            _canvasPanel.Location = new Point(rightX, 64);
            _canvasPanel.Size = new Size(485, 360);
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
            _lblDefaultHint.Location = new Point(rightX, 64);
            _lblDefaultHint.Size = new Size(485, 360);
            _lblDefaultHint.ForeColor = Color.FromArgb(120, 120, 120);
            _lblDefaultHint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            _lblDefaultHint.BorderStyle = BorderStyle.FixedSingle;
            _lblDefaultHint.BackColor = Color.FromArgb(245, 245, 245);
            page.Controls.Add(_lblDefaultHint);

            _tabControl.TabPages.Add(page);
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
            int prevSel = _listStates.SelectedIndex;
            _listStates.Items.Clear();
            foreach (ShapeState state in _states)
                _listStates.Items.Add(state.Name);
            if (prevSel >= 0 && prevSel < _listStates.Items.Count)
                _listStates.SelectedIndex = prevSel;
            else if (_listStates.Items.Count > 0 && _editingStateIndex < 0)
                _listStates.SelectedIndex = 0;
            UpdateStateButtons();
        }

        /// <summary>
        /// 状态列表选择变化时，先将当前编辑保存回状态，再加载新选中状态的数据到编辑器。
        /// </summary>
        private void OnStateListSelectedIndexChanged(object sender, EventArgs e)
        {
            // 先保存当前编辑状态
            SaveCurrentEditorToState();

            int idx = _listStates.SelectedIndex;
            _editingStateIndex = idx;
            LoadStateToEditor(idx);
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
                _vertices.Clear();
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
                _vertices.Clear();

            // 从图形命令中同步填充/颜色
            if (hasCustom)
            {
                RenderCommand polyCmd = null;
                foreach (RenderCommand cmd in s.CustomRenderCommands)
                {
                    if (cmd.CommandType == RenderCommandType.Polygon)
                    { polyCmd = cmd; break; }
                }
                if (polyCmd != null)
                {
                    _geomFillColor = polyCmd.FillColor;
                    _geomBorderColor = polyCmd.StrokeColor;
                    _filled = polyCmd.Fill;
                    _chkFilled.Checked = _filled;
                    // 同步状态颜色，使工具栏色块与实际绘制颜色一致
                    _stateFillColor = _geomFillColor;
                    _stateBorderColor = _geomBorderColor;
                }
            }

            UpdateColorButtons();
            UpdateGeometryVisibility();
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
            _listStates.SelectedIndex = _states.Count - 1;
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
            _listStates.SelectedIndex = _states.Count - 1;
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
            _listStates.SelectedIndex = 0;
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
                    _listStates.SelectedIndex = Math.Min(idx, _listStates.Items.Count - 1);
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
            _lblDefaultHint.Visible = !visible;
        }

        #endregion

        #region 编辑器 — 图形加载与构建

        private void LoadVerticesFromCommands(List<RenderCommand> cmds)
        {
            _vertices.Clear();
            if (cmds == null || cmds.Count == 0)
                return;

            RenderCommand polyCmd = null;
            foreach (RenderCommand cmd in cmds)
            {
                if (cmd.CommandType == RenderCommandType.Polygon)
                { polyCmd = cmd; break; }
            }

            if (polyCmd != null && polyCmd.PolygonPoints != null && polyCmd.PolygonPoints.Length >= 3)
            {
                float canvasW = 445f;
                float canvasH = 320f;
                float offsetX = 20f;
                float offsetY = 20f;
                foreach (PointF pt in polyCmd.PolygonPoints)
                    _vertices.Add(new PointF(offsetX + pt.X * canvasW, offsetY + pt.Y * canvasH));
                _closedPath = true;
                _geomFillColor = polyCmd.FillColor;
                _geomBorderColor = polyCmd.StrokeColor;
                _filled = polyCmd.Fill;
            }
        }

        private List<RenderCommand> BuildRenderCommandsFromVertices()
        {
            if (_vertices.Count < 3)
                return null;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (PointF pt in _vertices)
            {
                if (pt.X < minX) minX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y > maxY) maxY = pt.Y;
            }
            float rangeX = maxX - minX;
            float rangeY = maxY - minY;
            if (rangeX < 1) rangeX = 1;
            if (rangeY < 1) rangeY = 1;

            List<PointF> normalized = new List<PointF>();
            foreach (PointF pt in _vertices)
                normalized.Add(new PointF((pt.X - minX) / rangeX, (pt.Y - minY) / rangeY));

            RenderCommand polyCmd = new RenderCommand();
            polyCmd.CommandType = RenderCommandType.Polygon;
            polyCmd.PolygonPoints = normalized.ToArray();
            polyCmd.X = 0; polyCmd.Y = 0;
            polyCmd.Width = 1; polyCmd.Height = 1;
            polyCmd.FillColor = _filled ? _geomFillColor : Color.Transparent;
            polyCmd.StrokeColor = _geomBorderColor;
            polyCmd.StrokeWidth = 2f;
            polyCmd.Fill = _filled;
            return new List<RenderCommand> { polyCmd };
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
            _vertices.Clear();
            _selectedVertex = -1;
            _dragIndex = -1;
            _closedPath = false;
            _canvasPanel.Invalidate();
        }

        #endregion

        #region 编辑器 — 画布事件

        private void OnCanvasPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            if (_vertices.Count == 0)
                return;

            GraphicsPath path = new GraphicsPath();
            for (int i = 0; i < _vertices.Count; i++)
            {
                if (i == 0) path.StartFigure();
                path.AddLine(_vertices[i], _vertices[(i + 1) % _vertices.Count]);
            }
            if (_closedPath) path.CloseFigure();

            if (_filled && _vertices.Count >= 3)
            {
                using (Brush brush = new SolidBrush(Color.FromArgb(180, _geomFillColor)))
                    g.FillPath(brush, path);
            }

            using (Pen pen = new Pen(_geomBorderColor, 2f))
            {
                for (int i = 0; i < _vertices.Count - 1; i++)
                    g.DrawLine(pen, _vertices[i], _vertices[i + 1]);
                if (_closedPath && _vertices.Count >= 3)
                    g.DrawLine(pen, _vertices[_vertices.Count - 1], _vertices[0]);
            }

            for (int i = 0; i < _vertices.Count; i++)
            {
                RectangleF handle = new RectangleF(_vertices[i].X - 5, _vertices[i].Y - 5, 10, 10);
                bool isSelected = (i == _selectedVertex);
                using (Brush brush = new SolidBrush(isSelected ? Color.FromArgb(0, 120, 215) : Color.White))
                using (Pen pen = new Pen(_geomBorderColor, isSelected ? 2f : 1f))
                {
                    g.FillRectangle(brush, handle);
                    g.DrawRectangle(pen, handle.X, handle.Y, handle.Width, handle.Height);
                }
            }
        }

        private void OnCanvasMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int hitIdx = HitTestVertex(e.Location);
                if (hitIdx >= 0)
                {
                    _selectedVertex = hitIdx;
                    _dragIndex = hitIdx;
                    _canvasPanel.Cursor = Cursors.SizeAll;
                }
                else
                {
                    _vertices.Add(new PointF(e.X, e.Y));
                    _selectedVertex = _vertices.Count - 1;
                    _canvasPanel.Invalidate();
                }
            }
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragIndex >= 0)
            {
                _vertices[_dragIndex] = new PointF(e.X, e.Y);
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
                _vertices.RemoveAt(hitIdx);
                if (_selectedVertex >= _vertices.Count)
                    _selectedVertex = _vertices.Count - 1;
                _canvasPanel.Invalidate();
            }
        }

        private int HitTestVertex(Point pt)
        {
            for (int i = 0; i < _vertices.Count; i++)
            {
                float dx = _vertices[i].X - pt.X;
                float dy = _vertices[i].Y - pt.Y;
                if (dx * dx + dy * dy <= 64)
                    return i;
            }
            return -1;
        }

        private void OnAddVertex(object sender, EventArgs e)
        {
            PointF newPt;
            if (_vertices.Count > 0)
            {
                PointF last = _vertices[_vertices.Count - 1];
                newPt = new PointF(last.X + 30, last.Y + 30);
            }
            else
            {
                newPt = new PointF(220, 160);
            }
            _vertices.Add(newPt);
            _selectedVertex = _vertices.Count - 1;
            _canvasPanel.Invalidate();
        }

        private void OnDeleteVertex(object sender, EventArgs e)
        {
            if (_selectedVertex >= 0 && _selectedVertex < _vertices.Count)
            {
                _vertices.RemoveAt(_selectedVertex);
                if (_selectedVertex >= _vertices.Count)
                    _selectedVertex = _vertices.Count - 1;
                _canvasPanel.Invalidate();
            }
        }

        private void OnToggleClosePath(object sender, EventArgs e)
        {
            _closedPath = !_closedPath;
            _canvasPanel.Invalidate();
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

            RenderCommand polyCmd = null;
            foreach (RenderCommand cmd in defaultCmds)
            {
                if (cmd.CommandType == RenderCommandType.Polygon)
                { polyCmd = cmd; break; }
            }
            if (polyCmd != null)
            {
                st.DefaultFillColor = new XmlColor(polyCmd.FillColor);
                st.DefaultBorderColor = new XmlColor(polyCmd.StrokeColor);
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
}
