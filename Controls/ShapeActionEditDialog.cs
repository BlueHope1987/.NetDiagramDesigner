using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CloudNativeDesigner.Core;

namespace CloudNativeDesigner.Controls
{
    /// <summary>
    /// 图形行为（右键菜单操作）编辑对话框。
    /// 用于添加或编辑单个 ShapeAction。
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

        private List<string> _stateNames;

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
            this.ClientSize = new Size(340, 220);

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

            // 目标状态（StateChange 时显示，枚举选择）
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
            // 填充已定义状态名
            foreach (string name in _stateNames)
                _cmbTargetState.Items.Add(name);
            // 选中当前值
            if (editAction != null && !string.IsNullOrEmpty(editAction.TargetState))
            {
                int selIdx = _cmbTargetState.Items.IndexOf(editAction.TargetState);
                if (selIdx >= 0)
                    _cmbTargetState.SelectedIndex = selIdx;
                else
                {
                    // 若目标状态不在列表中，添加到末尾并选中
                    _cmbTargetState.Items.Add(editAction.TargetState);
                    _cmbTargetState.SelectedIndex = _cmbTargetState.Items.Count - 1;
                }
            }
            else if (_cmbTargetState.Items.Count > 0)
            {
                _cmbTargetState.SelectedIndex = 0;
            }
            this.Controls.Add(_cmbTargetState);
            y += 32;

            // 回调名（HostCallback 时显示）
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
            y += 32;

            // 图标名
            Label lblIcon = new Label();
            lblIcon.Text = "图标名：";
            lblIcon.Location = new Point(10, y);
            lblIcon.Size = new Size(lblW, 20);
            lblIcon.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(lblIcon);

            _txtIconName = new TextBox();
            _txtIconName.Location = new Point(xVal, y);
            _txtIconName.Size = new Size(220, 22);
            _txtIconName.Text = (editAction != null) ? editAction.IconName : "";
            this.Controls.Add(_txtIconName);

            // 确定/取消
            _btnOk = new Button();
            _btnOk.Text = "确定";
            _btnOk.DialogResult = DialogResult.OK;
            _btnOk.Location = new Point(150, 180);
            _btnOk.Size = new Size(80, 28);
            this.Controls.Add(_btnOk);

            _btnCancel = new Button();
            _btnCancel.Text = "取消";
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.Location = new Point(240, 180);
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
                    name = "操作";

                ResultAction = new ShapeAction();
                ResultAction.Name = name;
                ResultAction.IconName = _txtIconName.Text.Trim();

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
