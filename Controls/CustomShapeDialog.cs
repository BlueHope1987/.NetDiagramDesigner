using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CloudNativeDesigner.Core;

namespace CloudNativeDesigner.Controls
{
    /// <summary>
    /// 自定义图形构建器（带状态和行为编辑）。
    /// 默认图形作为初始状态（"默认"状态），不再有独立图形标签页。
    /// 状态标签页管理所有状态（含初始状态），行为标签页管理右键菜单操作。
    /// </summary>
    public class CustomShapeDialog : Form
    {
        // 通用控件
        private TextBox _txtName;
        private TabControl _tabControl;
        private Button _btnOk;
        private Button _btnCancel;

        // === 状态标签页 ===
        private ListBox _listStates;
        private Button _btnAddState;
        private Button _btnEditState;
        private Button _btnDeleteState;
        private Button _btnCopyState;
        private Button _btnSetAsDefault;
        private List<ShapeState> _states = new List<ShapeState>();

        // === 行为标签页 ===
        private ListBox _listActions;
        private Button _btnAddAction;
        private Button _btnEditAction;
        private Button _btnDeleteAction;
        private List<ShapeAction> _actions = new List<ShapeAction>();

        private ShapeType _resultShapeType = null;
        public ShapeType ResultShapeType { get { return _resultShapeType; } }

        /// <summary>初始状态（默认图形）的状态名标识</summary>
        private const string DefaultStateName = "默认";

        /// <summary>创建新自定义图形</summary>
        public CustomShapeDialog() : this(null) { }

        /// <summary>编辑现有自定义图形</summary>
        public CustomShapeDialog(ShapeType editShape)
        {
            this.Text = (editShape == null) ? "创建自定义图形" : "编辑自定义图形";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.MaximizeBox = false;
            this.ClientSize = new Size(540, 500);

            // 名称（位于标签页上方）
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

            // 标签页
            _tabControl = new TabControl();
            _tabControl.Location = new Point(10, 40);
            _tabControl.Size = new Size(510, 410);
            this.Controls.Add(_tabControl);

            BuildStateTab();
            BuildActionTab();

            // 确定/取消
            _btnOk = new Button();
            _btnOk.Text = "确定";
            _btnOk.DialogResult = DialogResult.OK;
            _btnOk.Location = new Point(350, 460);
            _btnOk.Size = new Size(80, 30);
            this.Controls.Add(_btnOk);

            _btnCancel = new Button();
            _btnCancel.Text = "取消";
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.Location = new Point(440, 460);
            _btnCancel.Size = new Size(80, 30);
            this.Controls.Add(_btnCancel);

            // 加载现有图形数据
            if (editShape != null)
            {
                LoadFromShapeType(editShape);
            }
            else
            {
                // 新建时预置一个默认初始状态（含默认三角形图形）
                ShapeState defaultState = new ShapeState();
                defaultState.Name = DefaultStateName;
                defaultState.UseCustomRenderCommands = true;
                defaultState.CustomRenderCommands = BuildDefaultTriangleCommands();
                _states.Add(defaultState);
                RefreshStateList();
            }

            // 初始化按钮可用状态
            UpdateStateButtons();
            UpdateActionButtons();
        }

        /// <summary>
        /// 构建默认三角形 RenderCommand 列表（作为初始状态的默认图形）
        /// </summary>
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

            _listStates = new ListBox();
            _listStates.Location = new Point(10, 10);
            _listStates.Size = new Size(360, 340);
            _listStates.BorderStyle = BorderStyle.FixedSingle;
            _listStates.DrawMode = DrawMode.OwnerDrawFixed;
            _listStates.ItemHeight = 24;
            _listStates.DrawItem += new DrawItemEventHandler(OnDrawStateItem);
            _listStates.SelectedIndexChanged += new EventHandler(OnStateListSelectedIndexChanged);
            page.Controls.Add(_listStates);

            int x = 385;
            int y = 10;

            _btnAddState = new Button();
            _btnAddState.Text = "添加...";
            _btnAddState.Location = new Point(x, y);
            _btnAddState.Size = new Size(110, 28);
            _btnAddState.Click += new EventHandler(OnAddState);
            page.Controls.Add(_btnAddState);
            y += 32;

            _btnEditState = new Button();
            _btnEditState.Text = "编辑...";
            _btnEditState.Location = new Point(x, y);
            _btnEditState.Size = new Size(110, 28);
            _btnEditState.Click += new EventHandler(OnEditState);
            page.Controls.Add(_btnEditState);
            y += 32;

            _btnCopyState = new Button();
            _btnCopyState.Text = "复制状态";
            _btnCopyState.Location = new Point(x, y);
            _btnCopyState.Size = new Size(110, 28);
            _btnCopyState.Click += new EventHandler(OnCopyState);
            page.Controls.Add(_btnCopyState);
            y += 32;

            _btnSetAsDefault = new Button();
            _btnSetAsDefault.Text = "设为初始";
            _btnSetAsDefault.Location = new Point(x, y);
            _btnSetAsDefault.Size = new Size(110, 28);
            _btnSetAsDefault.Click += new EventHandler(OnSetAsDefault);
            page.Controls.Add(_btnSetAsDefault);
            y += 32;

            _btnDeleteState = new Button();
            _btnDeleteState.Text = "删除";
            _btnDeleteState.Location = new Point(x, y);
            _btnDeleteState.Size = new Size(110, 28);
            _btnDeleteState.Click += new EventHandler(OnDeleteState);
            page.Controls.Add(_btnDeleteState);
            y += 40;

            Label lblHint = new Label();
            lblHint.Text = "提示：\n初始状态即默认图形。\n可添加多个状态并\n为每个状态绘制不\n同图形，通过行为\n切换状态显示。";
            lblHint.Location = new Point(x, y);
            lblHint.Size = new Size(110, 110);
            lblHint.ForeColor = Color.FromArgb(100, 100, 100);
            page.Controls.Add(lblHint);

            _tabControl.TabPages.Add(page);
        }

        private void BuildActionTab()
        {
            TabPage page = new TabPage("行为");
            page.BorderStyle = BorderStyle.None;

            _listActions = new ListBox();
            _listActions.Location = new Point(10, 10);
            _listActions.Size = new Size(360, 340);
            _listActions.BorderStyle = BorderStyle.FixedSingle;
            _listActions.SelectedIndexChanged += new EventHandler(OnActionListSelectedIndexChanged);
            page.Controls.Add(_listActions);

            int x = 385;
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
            lblHint.Text = "提示：\n行为定义图形在右键\n菜单中可用的操作。\n类型：切换状态 / \n宿主回调。切换状态\n时可枚举选择已定义\n的目标状态。";
            lblHint.Location = new Point(x, y);
            lblHint.Size = new Size(110, 100);
            lblHint.ForeColor = Color.FromArgb(100, 100, 100);
            page.Controls.Add(lblHint);

            _tabControl.TabPages.Add(page);
        }

        #endregion

        #region 数据加载

        /// <summary>
        /// 从现有 ShapeType 解析状态和行为。
        /// 默认图形（RenderCommands）转换为初始状态。
        /// </summary>
        private void LoadFromShapeType(ShapeType st)
        {
            _txtName.Text = st.Name;

            // 将默认 RenderCommands 作为初始状态
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

            // 如果没有名为"默认"的初始状态，则用默认 RenderCommands 创建一个
            if (!defaultStateExists)
            {
                ShapeState defaultState = new ShapeState();
                defaultState.Name = DefaultStateName;
                defaultState.UseCustomRenderCommands = true;
                defaultState.CustomRenderCommands = (defaultCmds != null) ? defaultCmds : BuildDefaultTriangleCommands();
                // 默认颜色
                defaultState.FillColor = st.DefaultFillColor;
                defaultState.BorderColor = st.DefaultBorderColor;
                defaultState.TextColor = st.DefaultTextColor;
                _states.Insert(0, defaultState);
            }

            RefreshStateList();

            // 加载行为
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

        /// <summary>深拷贝 ShapeState</summary>
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

        /// <summary>深拷贝 RenderCommand 列表</summary>
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

        /// <summary>
        /// 获取初始（默认）状态的 RenderCommand 列表。
        /// </summary>
        private List<RenderCommand> GetDefaultRenderCommands()
        {
            foreach (ShapeState s in _states)
            {
                if (s.Name == DefaultStateName && s.UseCustomRenderCommands
                    && s.CustomRenderCommands != null && s.CustomRenderCommands.Count > 0)
                {
                    return CloneRenderCommands(s.CustomRenderCommands);
                }
            }
            // 回退：找第一个有自定义图形的状态
            foreach (ShapeState s in _states)
            {
                if (s.UseCustomRenderCommands && s.CustomRenderCommands != null && s.CustomRenderCommands.Count > 0)
                {
                    return CloneRenderCommands(s.CustomRenderCommands);
                }
            }
            return BuildDefaultTriangleCommands();
        }

        #endregion

        #region 状态标签页事件

        private void RefreshStateList()
        {
            int prevSel = _listStates.SelectedIndex;
            _listStates.Items.Clear();
            foreach (ShapeState state in _states)
            {
                _listStates.Items.Add(state.Name);
            }
            if (prevSel >= 0 && prevSel < _listStates.Items.Count)
                _listStates.SelectedIndex = prevSel;
            UpdateStateButtons();
        }

        private void OnStateListSelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateStateButtons();
        }

        private void UpdateStateButtons()
        {
            bool hasSelection = (_listStates.SelectedIndex >= 0 && _listStates.SelectedIndex < _states.Count);
            _btnEditState.Enabled = hasSelection;
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

            // 绘制颜色预览块
            Rectangle colorRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 3, 20, e.Bounds.Height - 6);
            using (Brush brush = new SolidBrush(state.FillColor.ToColor()))
            using (Pen pen = new Pen(state.BorderColor.ToColor(), 1))
            {
                e.Graphics.FillRectangle(brush, colorRect);
                e.Graphics.DrawRectangle(pen, colorRect);
            }

            // 绘制状态名
            string display = state.Name;
            if (state.Name == DefaultStateName)
                display += " [初始]";
            if (state.UseCustomRenderCommands && state.CustomRenderCommands != null && state.CustomRenderCommands.Count > 0)
                display += " [自定义图形]";
            using (Brush textBrush = new SolidBrush(e.ForeColor))
            {
                e.Graphics.DrawString(display, e.Font, textBrush,
                    e.Bounds.X + 30, e.Bounds.Y + 2);
            }

            e.DrawFocusRectangle();
        }

        /// <summary>
        /// 获取初始状态的图形，作为其他状态编辑时可复制的默认图形。
        /// </summary>
        private List<RenderCommand> GetDefaultCommandsForStateEditor()
        {
            return GetDefaultRenderCommands();
        }

        private void OnAddState(object sender, EventArgs e)
        {
            List<RenderCommand> defaults = GetDefaultCommandsForStateEditor();
            using (ShapeStateEditDialog dlg = new ShapeStateEditDialog(null, defaults))
            {
                if (dlg.ShowDialog() == DialogResult.OK && dlg.ResultState != null)
                {
                    _states.Add(dlg.ResultState);
                    RefreshStateList();
                    _listStates.SelectedIndex = _listStates.Items.Count - 1;
                }
            }
        }

        private void OnEditState(object sender, EventArgs e)
        {
            int idx = _listStates.SelectedIndex;
            if (idx < 0 || idx >= _states.Count)
                return;

            List<RenderCommand> defaults = GetDefaultCommandsForStateEditor();
            using (ShapeStateEditDialog dlg = new ShapeStateEditDialog(_states[idx], defaults))
            {
                if (dlg.ShowDialog() == DialogResult.OK && dlg.ResultState != null)
                {
                    // 初始状态名不可更改
                    if (_states[idx].Name == DefaultStateName)
                        dlg.ResultState.Name = DefaultStateName;
                    _states[idx] = dlg.ResultState;
                    RefreshStateList();
                }
            }
        }

        private void OnCopyState(object sender, EventArgs e)
        {
            int idx = _listStates.SelectedIndex;
            if (idx < 0 || idx >= _states.Count)
                return;

            ShapeState copy = CloneShapeState(_states[idx]);
            copy.Name = _states[idx].Name + "_副本";
            _states.Add(copy);
            RefreshStateList();
            _listStates.SelectedIndex = _states.Count - 1;
        }

        /// <summary>
        /// 将选中的状态设为初始状态（交换名称和位置到第一位）。
        /// </summary>
        private void OnSetAsDefault(object sender, EventArgs e)
        {
            int idx = _listStates.SelectedIndex;
            if (idx < 0 || idx >= _states.Count)
                return;

            if (_states[idx].Name == DefaultStateName)
            {
                MessageBox.Show("该状态已经是初始状态。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 找到当前初始状态，交换名称
            for (int i = 0; i < _states.Count; i++)
            {
                if (_states[i].Name == DefaultStateName)
                {
                    _states[i].Name = _states[idx].Name;
                    break;
                }
            }
            _states[idx].Name = DefaultStateName;

            // 将该状态移到第一位
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
                MessageBox.Show("至少需要保留一个状态。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_states[idx].Name == DefaultStateName)
            {
                MessageBox.Show("初始状态不可删除，可先将其余状态设为初始后再删除。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(string.Format("确定删除状态 \"{0}\"？", _states[idx].Name),
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _states.RemoveAt(idx);
                RefreshStateList();
            }
        }

        #endregion

        #region 行为标签页事件

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

        /// <summary>
        /// 获取当前所有状态名称列表，供行为编辑器枚举选择。
        /// </summary>
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
                // 验证初始状态存在且有图形
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

            // 默认图形来自初始状态
            st.RenderCommands = CloneRenderCommands(defaultCmds);

            // 从默认图形中提取颜色
            RenderCommand polyCmd = null;
            foreach (RenderCommand cmd in defaultCmds)
            {
                if (cmd.CommandType == RenderCommandType.Polygon)
                {
                    polyCmd = cmd;
                    break;
                }
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

            // 状态
            st.DefaultStates = new List<ShapeState>();
            foreach (ShapeState s in _states)
            {
                st.DefaultStates.Add(CloneShapeState(s));
            }

            // 行为
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
