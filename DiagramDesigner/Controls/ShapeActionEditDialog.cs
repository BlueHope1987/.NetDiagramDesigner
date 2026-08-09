using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DiagramDesigner.Core;

namespace DiagramDesigner.Controls
{
    /// <summary>
    /// 图形行为（右键菜单操作）编辑对话框。
    /// 支持三种模式：
    ///   1. 切换状态 — 切换到指定状态
    ///   2. 宿主回调 — 调用宿主注册的回调函数
    ///   3. 行为序列 — 包含多个子操作，按序执行
    /// 切换状态行为可从已定义状态中枚举选择目标状态。
    /// </summary>
    public class ShapeActionEditDialog : Form
    {
        private TextBox _txtName;
        private ComboBox _cmbType;
        private ComboBox _cmbTargetState;
        private TextBox _txtCallback;
        private TextBox _txtIconName;
        private Label _lblTargetState;
        private Label _lblCallback;
        private Button _btnOk;
        private Button _btnCancel;

        // 行为序列 UI
        private Label _lblSubActions;
        private ListBox _listSubActions;
        private Button _btnAddSub;
        private Button _btnEditSub;
        private Button _btnDeleteSub;
        private Button _btnMoveSubUp;
        private Button _btnMoveSubDown;

        private List<string> _stateNames;
        private List<ShapeAction> _subActions = new List<ShapeAction>();

        public ShapeAction ResultAction { get; private set; }

        public ShapeActionEditDialog() : this(null, null) { }

        public ShapeActionEditDialog(ShapeAction editAction) : this(editAction, null) { }

        public ShapeActionEditDialog(ShapeAction editAction, List<string> stateNames)
        {
            _stateNames = stateNames ?? new List<string>();

            this.Text = (editAction == null) ? "添加行为" : "编辑行为";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(420, 400);

            int y = 12;
            int lblW = 80;
            int xVal = 95;
            int valW = 305;

            // 名称
            AddLabel("名称：", y, lblW);
            _txtName = new TextBox();
            _txtName.Location = new Point(xVal, y);
            _txtName.Size = new Size(valW, 22);
            _txtName.Text = (editAction != null) ? editAction.Name : "";
            this.Controls.Add(_txtName);
            y += 32;

            // 类型
            AddLabel("类型：", y, lblW);
            _cmbType = new ComboBox();
            _cmbType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbType.Location = new Point(xVal, y);
            _cmbType.Size = new Size(valW, 22);
            _cmbType.Items.Add("切换状态");
            _cmbType.Items.Add("宿主回调");
            _cmbType.Items.Add("行为序列（含多个子操作）");
            // 确定初始类型
            if (editAction != null)
            {
                if (editAction.HasSubActions)
                    _cmbType.SelectedIndex = 2;
                else if (editAction.ActionType == ShapeActionType.HostCallback)
                    _cmbType.SelectedIndex = 1;
                else
                    _cmbType.SelectedIndex = 0;
            }
            else
                _cmbType.SelectedIndex = 0;
            _cmbType.SelectedIndexChanged += new EventHandler(OnTypeChanged);
            this.Controls.Add(_cmbType);
            y += 32;

            // 目标状态（StateChange 时显示，枚举选择）
            _lblTargetState = AddLabel("目标状态：", y, lblW);
            _cmbTargetState = new ComboBox();
            _cmbTargetState.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbTargetState.Location = new Point(xVal, y);
            _cmbTargetState.Size = new Size(valW, 22);
            foreach (string name in _stateNames)
                _cmbTargetState.Items.Add(name);
            if (editAction != null && !string.IsNullOrEmpty(editAction.TargetState))
            {
                int selIdx = _cmbTargetState.Items.IndexOf(editAction.TargetState);
                if (selIdx >= 0)
                    _cmbTargetState.SelectedIndex = selIdx;
                else
                {
                    _cmbTargetState.Items.Add(editAction.TargetState);
                    _cmbTargetState.SelectedIndex = _cmbTargetState.Items.Count - 1;
                }
            }
            else if (_cmbTargetState.Items.Count > 0)
                _cmbTargetState.SelectedIndex = 0;
            this.Controls.Add(_cmbTargetState);
            y += 32;

            // 回调名（HostCallback 时显示）
            _lblCallback = AddLabel("回调名：", y, lblW);
            _txtCallback = new TextBox();
            _txtCallback.Location = new Point(xVal, y);
            _txtCallback.Size = new Size(valW, 22);
            _txtCallback.Text = (editAction != null) ? editAction.CallbackName : "";
            this.Controls.Add(_txtCallback);
            y += 32;

            // 图标名
            AddLabel("图标名：", y, lblW);
            _txtIconName = new TextBox();
            _txtIconName.Location = new Point(xVal, y);
            _txtIconName.Size = new Size(valW, 22);
            _txtIconName.Text = (editAction != null) ? editAction.IconName : "";
            this.Controls.Add(_txtIconName);
            y += 36;

            // === 行为序列区域 ===
            _lblSubActions = AddLabel("子操作序列：", y, lblW);
            _lblSubActions.Text = "子操作序列\n（按序执行）：";
            _lblSubActions.Size = new Size(lblW, 32);

            _listSubActions = new ListBox();
            _listSubActions.Location = new Point(xVal, y);
            _listSubActions.Size = new Size(210, 120);
            _listSubActions.BorderStyle = BorderStyle.FixedSingle;
            _listSubActions.SelectedIndexChanged += new EventHandler(OnSubActionSelected);
            this.Controls.Add(_listSubActions);

            int subBtnX = xVal + 215;
            int subBtnY = y;
            int subBtnW = 90;

            _btnAddSub = new Button();
            _btnAddSub.Text = "添加...";
            _btnAddSub.Location = new Point(subBtnX, subBtnY);
            _btnAddSub.Size = new Size(subBtnW, 26);
            _btnAddSub.Click += new EventHandler(OnAddSubAction);
            this.Controls.Add(_btnAddSub);
            subBtnY += 30;

            _btnEditSub = new Button();
            _btnEditSub.Text = "编辑...";
            _btnEditSub.Location = new Point(subBtnX, subBtnY);
            _btnEditSub.Size = new Size(subBtnW, 26);
            _btnEditSub.Click += new EventHandler(OnEditSubAction);
            this.Controls.Add(_btnEditSub);
            subBtnY += 30;

            _btnDeleteSub = new Button();
            _btnDeleteSub.Text = "删除";
            _btnDeleteSub.Location = new Point(subBtnX, subBtnY);
            _btnDeleteSub.Size = new Size(subBtnW, 26);
            _btnDeleteSub.Click += new EventHandler(OnDeleteSubAction);
            this.Controls.Add(_btnDeleteSub);
            subBtnY += 30;

            _btnMoveSubUp = new Button();
            _btnMoveSubUp.Text = "上移";
            _btnMoveSubUp.Location = new Point(subBtnX, subBtnY);
            _btnMoveSubUp.Size = new Size(42, 26);
            _btnMoveSubUp.Click += new EventHandler(OnMoveSubUp);
            this.Controls.Add(_btnMoveSubUp);

            _btnMoveSubDown = new Button();
            _btnMoveSubDown.Text = "下移";
            _btnMoveSubDown.Location = new Point(subBtnX + 48, subBtnY);
            _btnMoveSubDown.Size = new Size(42, 26);
            _btnMoveSubDown.Click += new EventHandler(OnMoveSubDown);
            this.Controls.Add(_btnMoveSubDown);

            y += 128;

            // 加载已有子操作
            if (editAction != null && editAction.HasSubActions)
            {
                foreach (ShapeAction sub in editAction.SubActions)
                    _subActions.Add(sub.Clone());
            }
            RefreshSubActionList();

            // 确定/取消
            _btnOk = new Button();
            _btnOk.Text = "确定";
            _btnOk.DialogResult = DialogResult.OK;
            _btnOk.Location = new Point(230, 360);
            _btnOk.Size = new Size(80, 28);
            this.Controls.Add(_btnOk);

            _btnCancel = new Button();
            _btnCancel.Text = "取消";
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.Location = new Point(320, 360);
            _btnCancel.Size = new Size(80, 28);
            this.Controls.Add(_btnCancel);

            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;

            OnTypeChanged(null, null);
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

        /// <summary>类型变化时切换可见区域</summary>
        private void OnTypeChanged(object sender, EventArgs e)
        {
            int idx = _cmbType.SelectedIndex;
            bool isStateChange = (idx == 0);
            bool isCallback = (idx == 1);
            bool isSequence = (idx == 2);

            _lblTargetState.Visible = isStateChange;
            _cmbTargetState.Visible = isStateChange;
            _lblCallback.Visible = isCallback;
            _txtCallback.Visible = isCallback;

            _lblSubActions.Visible = isSequence;
            _listSubActions.Visible = isSequence;
            _btnAddSub.Visible = isSequence;
            _btnEditSub.Visible = isSequence;
            _btnDeleteSub.Visible = isSequence;
            _btnMoveSubUp.Visible = isSequence;
            _btnMoveSubDown.Visible = isSequence;
        }

        #region 子操作管理

        /// <summary>刷新子操作列表显示</summary>
        private void RefreshSubActionList()
        {
            _listSubActions.Items.Clear();
            foreach (ShapeAction sub in _subActions)
            {
                string typeStr;
                if (sub.ActionType == ShapeActionType.StateChange)
                    typeStr = "[状态] " + sub.TargetState;
                else
                    typeStr = "[回调] " + sub.CallbackName;
                _listSubActions.Items.Add(string.Format("{0} — {1}", sub.Name, typeStr));
            }
            UpdateSubActionButtons();
        }

        private void UpdateSubActionButtons()
        {
            bool hasSel = (_listSubActions.SelectedIndex >= 0 && _listSubActions.SelectedIndex < _subActions.Count);
            _btnEditSub.Enabled = hasSel;
            _btnDeleteSub.Enabled = hasSel;
            _btnMoveSubUp.Enabled = hasSel && _listSubActions.SelectedIndex > 0;
            _btnMoveSubDown.Enabled = hasSel && _listSubActions.SelectedIndex < _subActions.Count - 1;
        }

        private void OnSubActionSelected(object sender, EventArgs e)
        {
            UpdateSubActionButtons();
        }

        /// <summary>添加子操作：弹出简化版对话框（仅状态切换或宿主回调）</summary>
        private void OnAddSubAction(object sender, EventArgs e)
        {
            using (SubActionEditDialog dlg = new SubActionEditDialog(null, _stateNames))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultAction != null)
                {
                    _subActions.Add(dlg.ResultAction);
                    RefreshSubActionList();
                    _listSubActions.SelectedIndex = _subActions.Count - 1;
                }
            }
        }

        /// <summary>编辑子操作</summary>
        private void OnEditSubAction(object sender, EventArgs e)
        {
            int idx = _listSubActions.SelectedIndex;
            if (idx < 0 || idx >= _subActions.Count)
                return;
            using (SubActionEditDialog dlg = new SubActionEditDialog(_subActions[idx], _stateNames))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultAction != null)
                {
                    _subActions[idx] = dlg.ResultAction;
                    RefreshSubActionList();
                    _listSubActions.SelectedIndex = idx;
                }
            }
        }

        /// <summary>删除子操作</summary>
        private void OnDeleteSubAction(object sender, EventArgs e)
        {
            int idx = _listSubActions.SelectedIndex;
            if (idx < 0 || idx >= _subActions.Count)
                return;
            _subActions.RemoveAt(idx);
            RefreshSubActionList();
        }

        /// <summary>子操作上移</summary>
        private void OnMoveSubUp(object sender, EventArgs e)
        {
            int idx = _listSubActions.SelectedIndex;
            if (idx <= 0 || idx >= _subActions.Count)
                return;
            ShapeAction tmp = _subActions[idx];
            _subActions[idx] = _subActions[idx - 1];
            _subActions[idx - 1] = tmp;
            _listSubActions.SelectedIndex = idx - 1;
            RefreshSubActionList();
        }

        /// <summary>子操作下移</summary>
        private void OnMoveSubDown(object sender, EventArgs e)
        {
            int idx = _listSubActions.SelectedIndex;
            if (idx < 0 || idx >= _subActions.Count - 1)
                return;
            ShapeAction tmp = _subActions[idx];
            _subActions[idx] = _subActions[idx + 1];
            _subActions[idx + 1] = tmp;
            _listSubActions.SelectedIndex = idx + 1;
            RefreshSubActionList();
        }

        #endregion

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (this.DialogResult == DialogResult.OK)
            {
                string name = _txtName.Text.Trim();
                if (string.IsNullOrEmpty(name))
                    name = "操作";

                ResultAction = new ShapeAction();
                ResultAction.Name = name;
                ResultAction.IconName = _txtIconName.Text.Trim();

                int typeIdx = _cmbType.SelectedIndex;
                if (typeIdx == 2)
                {
                    // 行为序列
                    ResultAction.ActionType = ShapeActionType.HostCallback; // 占位类型
                    ResultAction.SubActions.Clear();
                    foreach (ShapeAction sub in _subActions)
                        ResultAction.SubActions.Add(sub.Clone());
                }
                else if (typeIdx == 0)
                {
                    ResultAction.ActionType = ShapeActionType.StateChange;
                    ResultAction.TargetState = (_cmbTargetState.SelectedItem != null)
                        ? _cmbTargetState.SelectedItem.ToString() : "";
                }
                else
                {
                    ResultAction.ActionType = ShapeActionType.HostCallback;
                    ResultAction.CallbackName = _txtCallback.Text.Trim();
                }
            }
        }
    }

    /// <summary>
    /// 子操作编辑对话框（简化版，仅支持状态切换和宿主回调）。
    /// 供行为序列编辑器使用，避免无限嵌套。
    /// </summary>
    public class SubActionEditDialog : Form
    {
        private TextBox _txtName;
        private ComboBox _cmbType;
        private ComboBox _cmbTargetState;
        private TextBox _txtCallback;
        private Label _lblTargetState;
        private Label _lblCallback;
        private Button _btnOk;
        private Button _btnCancel;

        private List<string> _stateNames;

        public ShapeAction ResultAction { get; private set; }

        public SubActionEditDialog() : this(null, null) { }

        public SubActionEditDialog(ShapeAction editAction, List<string> stateNames)
        {
            _stateNames = stateNames ?? new List<string>();

            this.Text = (editAction == null) ? "添加子操作" : "编辑子操作";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(340, 180);

            int y = 12;
            int lblW = 80;
            int xVal = 95;

            // 名称
            Label lblName = new Label();
            lblName.Text = "名称：";
            lblName.Location = new Point(10, y);
            lblName.Size = new Size(lblW, 20);
            lblName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(lblName);

            _txtName = new TextBox();
            _txtName.Location = new Point(xVal, y);
            _txtName.Size = new Size(220, 22);
            _txtName.Text = (editAction != null) ? editAction.Name : "";
            this.Controls.Add(_txtName);
            y += 32;

            // 类型
            Label lblType = new Label();
            lblType.Text = "类型：";
            lblType.Location = new Point(10, y);
            lblType.Size = new Size(lblW, 22);
            lblType.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(lblType);

            _cmbType = new ComboBox();
            _cmbType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbType.Location = new Point(xVal, y);
            _cmbType.Size = new Size(220, 22);
            _cmbType.Items.Add("切换状态");
            _cmbType.Items.Add("宿主回调");
            _cmbType.SelectedIndex = (editAction != null && editAction.ActionType == ShapeActionType.HostCallback) ? 1 : 0;
            _cmbType.SelectedIndexChanged += new EventHandler(OnTypeChanged);
            this.Controls.Add(_cmbType);
            y += 32;

            // 目标状态
            _lblTargetState = new Label();
            _lblTargetState.Text = "目标状态：";
            _lblTargetState.Location = new Point(10, y);
            _lblTargetState.Size = new Size(lblW, 20);
            _lblTargetState.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(_lblTargetState);

            _cmbTargetState = new ComboBox();
            _cmbTargetState.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbTargetState.Location = new Point(xVal, y);
            _cmbTargetState.Size = new Size(220, 22);
            foreach (string name in _stateNames)
                _cmbTargetState.Items.Add(name);
            if (editAction != null && !string.IsNullOrEmpty(editAction.TargetState))
            {
                int selIdx = _cmbTargetState.Items.IndexOf(editAction.TargetState);
                if (selIdx >= 0)
                    _cmbTargetState.SelectedIndex = selIdx;
                else
                {
                    _cmbTargetState.Items.Add(editAction.TargetState);
                    _cmbTargetState.SelectedIndex = _cmbTargetState.Items.Count - 1;
                }
            }
            else if (_cmbTargetState.Items.Count > 0)
                _cmbTargetState.SelectedIndex = 0;
            this.Controls.Add(_cmbTargetState);
            y += 32;

            // 回调名
            _lblCallback = new Label();
            _lblCallback.Text = "回调名：";
            _lblCallback.Location = new Point(10, y);
            _lblCallback.Size = new Size(lblW, 20);
            _lblCallback.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(_lblCallback);

            _txtCallback = new TextBox();
            _txtCallback.Location = new Point(xVal, y);
            _txtCallback.Size = new Size(220, 22);
            _txtCallback.Text = (editAction != null) ? editAction.CallbackName : "";
            this.Controls.Add(_txtCallback);

            // 按钮
            _btnOk = new Button();
            _btnOk.Text = "确定";
            _btnOk.DialogResult = DialogResult.OK;
            _btnOk.Location = new Point(150, 140);
            _btnOk.Size = new Size(80, 28);
            this.Controls.Add(_btnOk);

            _btnCancel = new Button();
            _btnCancel.Text = "取消";
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.Location = new Point(240, 140);
            _btnCancel.Size = new Size(80, 28);
            this.Controls.Add(_btnCancel);

            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;

            OnTypeChanged(null, null);
        }

        private void OnTypeChanged(object sender, EventArgs e)
        {
            bool isStateChange = (_cmbType.SelectedIndex == 0);
            _lblTargetState.Visible = isStateChange;
            _cmbTargetState.Visible = isStateChange;
            _lblCallback.Visible = !isStateChange;
            _txtCallback.Visible = !isStateChange;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (this.DialogResult == DialogResult.OK)
            {
                string name = _txtName.Text.Trim();
                if (string.IsNullOrEmpty(name))
                    name = "子操作";

                ResultAction = new ShapeAction();
                ResultAction.Name = name;

                if (_cmbType.SelectedIndex == 0)
                {
                    ResultAction.ActionType = ShapeActionType.StateChange;
                    ResultAction.TargetState = (_cmbTargetState.SelectedItem != null)
                        ? _cmbTargetState.SelectedItem.ToString() : "";
                }
                else
                {
                    ResultAction.ActionType = ShapeActionType.HostCallback;
                    ResultAction.CallbackName = _txtCallback.Text.Trim();
                }
            }
        }
    }
}
