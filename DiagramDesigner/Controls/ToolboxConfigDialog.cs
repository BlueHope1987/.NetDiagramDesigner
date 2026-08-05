using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DiagramDesigner.Core;

namespace DiagramDesigner.Controls
{
    /// <summary>
    /// 工具箱配置对话框。
    /// 左侧：已注册图形多选列表（勾选=启用，取消=禁用）
    /// 右侧：上移/下移/添加自定义工具/编辑/删除
    /// </summary>
    public class ToolboxConfigDialog : Form
    {
        private CheckedListBox _listTools;
        private Button _btnUp;
        private Button _btnDown;
        private Button _btnAddCustom;
        private Button _btnEdit;
        private Button _btnDelete;
        private Button _btnEnableAll;
        private Button _btnDisableAll;
        private Button _btnOk;
        private Button _btnCancel;
        private List<ToolboxItem> _workingItems;

        public List<ToolboxItem> ResultItems { get { return _workingItems; } }

        public ToolboxConfigDialog(List<ToolboxItem> currentItems, ShapeTypeRegistry registry)
        {
            _workingItems = new List<ToolboxItem>();
            foreach (ToolboxItem item in currentItems)
                _workingItems.Add(item);

            this.Text = "工具箱配置";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(460, 420);
            this.Shown += new EventHandler(OnShown);

            _listTools = new CheckedListBox();
            _listTools.Location = new Point(10, 10);
            _listTools.Size = new Size(320, 340);
            _listTools.CheckOnClick = true;
            _listTools.MultiColumn = false;
            _listTools.BorderStyle = BorderStyle.FixedSingle;
            _listTools.DoubleClick += new EventHandler(OnListDoubleClick);
            _listTools.SelectedIndexChanged += new EventHandler(OnListSelectedIndexChanged);
            this.Controls.Add(_listTools);

            _btnUp = new Button();
            _btnUp.Text = "上移";
            _btnUp.Location = new Point(340, 10);
            _btnUp.Size = new Size(110, 28);
            _btnUp.Click += new EventHandler(OnMoveUp);
            this.Controls.Add(_btnUp);

            _btnDown = new Button();
            _btnDown.Text = "下移";
            _btnDown.Location = new Point(340, 44);
            _btnDown.Size = new Size(110, 28);
            _btnDown.Click += new EventHandler(OnMoveDown);
            this.Controls.Add(_btnDown);

            _btnAddCustom = new Button();
            _btnAddCustom.Text = "添加自定义...";
            _btnAddCustom.Location = new Point(340, 82);
            _btnAddCustom.Size = new Size(110, 28);
            _btnAddCustom.Click += new EventHandler(OnAddCustom);
            this.Controls.Add(_btnAddCustom);

            _btnEdit = new Button();
            _btnEdit.Text = "编辑...";
            _btnEdit.Location = new Point(340, 120);
            _btnEdit.Size = new Size(110, 28);
            _btnEdit.Click += new EventHandler(OnEditCustom);
            this.Controls.Add(_btnEdit);

            _btnDelete = new Button();
            _btnDelete.Text = "删除";
            _btnDelete.Location = new Point(340, 154);
            _btnDelete.Size = new Size(110, 28);
            _btnDelete.Click += new EventHandler(OnDeleteCustom);
            this.Controls.Add(_btnDelete);

            _btnEnableAll = new Button();
            _btnEnableAll.Text = "全部启用";
            _btnEnableAll.Location = new Point(340, 192);
            _btnEnableAll.Size = new Size(110, 28);
            _btnEnableAll.Click += new EventHandler(OnEnableAll);
            this.Controls.Add(_btnEnableAll);

            _btnDisableAll = new Button();
            _btnDisableAll.Text = "全部禁用";
            _btnDisableAll.Location = new Point(340, 226);
            _btnDisableAll.Size = new Size(110, 28);
            _btnDisableAll.Click += new EventHandler(OnDisableAll);
            this.Controls.Add(_btnDisableAll);

            Label lblHint = new Label();
            lblHint.Text = "提示：\n· 勾选 = 启用\n· 双击自定义项可编辑\n· 仅自定义工具可编辑删除";
            lblHint.Location = new Point(340, 264);
            lblHint.Size = new Size(110, 80);
            lblHint.ForeColor = Color.FromArgb(100, 100, 100);
            this.Controls.Add(lblHint);

            _btnOk = new Button();
            _btnOk.Text = "确定";
            _btnOk.DialogResult = DialogResult.OK;
            _btnOk.Location = new Point(270, 380);
            _btnOk.Size = new Size(80, 28);
            this.Controls.Add(_btnOk);

            _btnCancel = new Button();
            _btnCancel.Text = "取消";
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.Location = new Point(360, 380);
            _btnCancel.Size = new Size(80, 28);
            this.Controls.Add(_btnCancel);

            PopulateList();

            // 初始状态下无选中项，编辑/删除按钮禁用
            _btnEdit.Enabled = false;
            _btnDelete.Enabled = false;
        }

        private void OnShown(object sender, EventArgs e)
        {
            _btnOk.Focus();
        }

        private void PopulateList()
        {
            _listTools.Items.Clear();

            // 收集已注册图形中不在列表中的项，追加到末尾（未启用）
            List<string> existingNames = new List<string>();
            foreach (ToolboxItem item in _workingItems)
                existingNames.Add(item.Name);

            List<string> categories = ShapeTypeRegistry.Instance.GetCategories();
            List<ToolboxItem> allItems = new List<ToolboxItem>(_workingItems);

            foreach (string category in categories)
            {
                List<ShapeType> types = ShapeTypeRegistry.Instance.GetTypesByCategory(category);
                foreach (ShapeType type in types)
                {
                    if (!existingNames.Contains(type.Name))
                    {
                        ToolboxItem item = new ToolboxItem();
                        item.Name = type.Name;
                        item.Category = type.Category;
                        item.CreateShape = new CreateShapeHandler(type.CreateInstance);
                        item.Visible = false;
                        allItems.Add(item);
                    }
                }
            }

            _workingItems.Clear();
            foreach (ToolboxItem item in allItems)
                _workingItems.Add(item);

            // 填充列表
            foreach (ToolboxItem item in _workingItems)
            {
                string prefix = "[" + item.Category + "] ";
                string display = prefix + item.Name;
                _listTools.Items.Add(display, item.Visible);
            }
        }

        private bool IsCustomItem(int index)
        {
            if (index < 0 || index >= _workingItems.Count)
                return false;
            return _workingItems[index].Category == "自定义";
        }

        /// <summary>
        /// 根据当前选中项更新编辑/删除按钮的可用状态：
        /// 仅自定义工具可编辑和删除。
        /// </summary>
        private void OnListSelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = _listTools.SelectedIndex;
            bool isCustom = IsCustomItem(idx);
            _btnEdit.Enabled = isCustom;
            _btnDelete.Enabled = isCustom;
        }

        private void OnListDoubleClick(object sender, EventArgs e)
        {
            if (_listTools.SelectedIndex >= 0 && IsCustomItem(_listTools.SelectedIndex))
            {
                OnEditCustom(sender, e);
            }
        }

        private void OnMoveUp(object sender, EventArgs e)
        {
            if (_listTools.SelectedIndex <= 0) return;
            int idx = _listTools.SelectedIndex;
            SwapItems(idx, idx - 1);
            _listTools.SelectedIndex = idx - 1;
        }

        private void OnMoveDown(object sender, EventArgs e)
        {
            if (_listTools.SelectedIndex < 0 || _listTools.SelectedIndex >= _listTools.Items.Count - 1) return;
            int idx = _listTools.SelectedIndex;
            SwapItems(idx, idx + 1);
            _listTools.SelectedIndex = idx + 1;
        }

        private void SwapItems(int a, int b)
        {
            ToolboxItem tmp = _workingItems[a];
            _workingItems[a] = _workingItems[b];
            _workingItems[b] = tmp;

            bool checkedA = _listTools.GetItemChecked(a);
            bool checkedB = _listTools.GetItemChecked(b);
            object textA = _listTools.Items[a];

            _listTools.Items[a] = _listTools.Items[b];
            _listTools.SetItemChecked(a, checkedB);
            _listTools.Items[b] = textA;
            _listTools.SetItemChecked(b, checkedA);
        }

        private void OnAddCustom(object sender, EventArgs e)
        {
            using (CustomShapeDialog dlg = new CustomShapeDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK && dlg.ResultShapeType != null)
                {
                    ShapeType st = dlg.ResultShapeType;
                    ShapeTypeRegistry.Instance.Register(st);

                    ToolboxItem item = new ToolboxItem();
                    item.Name = st.Name;
                    item.Category = "自定义";
                    item.CreateShape = new CreateShapeHandler(st.CreateInstance);
                    item.Visible = true;
                    item.Icon = null;

                    _workingItems.Add(item);
                    _listTools.Items.Add("[自定义] " + st.Name, true);
                    _listTools.SelectedIndex = _listTools.Items.Count - 1;
                    OnListSelectedIndexChanged(null, null);
                }
            }
        }

        private void OnEditCustom(object sender, EventArgs e)
        {
            int idx = _listTools.SelectedIndex;
            if (idx < 0 || !IsCustomItem(idx))
                return;

            ToolboxItem item = _workingItems[idx];
            ShapeType st = ShapeTypeRegistry.Instance.GetShapeType(item.Name);
            if (st == null)
                return;

            using (CustomShapeDialog dlg = new CustomShapeDialog(st))
            {
                if (dlg.ShowDialog() == DialogResult.OK && dlg.ResultShapeType != null)
                {
                    ShapeType newSt = dlg.ResultShapeType;
                    // 从注册表移除旧的，注册新的
                    ShapeTypeRegistry.Instance.Unregister(st.Name);
                    ShapeTypeRegistry.Instance.Register(newSt);

                    // 更新 working item
                    item.Name = newSt.Name;
                    item.Category = newSt.Category;
                    item.CreateShape = new CreateShapeHandler(newSt.CreateInstance);
                    item.Icon = null;

                    // 更新列表显示
                    string display = "[自定义] " + newSt.Name;
                    _listTools.Items[idx] = display;
                }
            }
        }

        private void OnDeleteCustom(object sender, EventArgs e)
        {
            int idx = _listTools.SelectedIndex;
            if (idx < 0 || !IsCustomItem(idx))
            {
                MessageBox.Show("只能删除自定义工具", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ToolboxItem item = _workingItems[idx];
            if (MessageBox.Show(string.Format("确定删除自定义工具 \"{0}\"？", item.Name),
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                ShapeTypeRegistry.Instance.Unregister(item.Name);
                _workingItems.RemoveAt(idx);
                _listTools.Items.RemoveAt(idx);
                OnListSelectedIndexChanged(null, null);
            }
        }

        /// <summary>将列表中所有工具项设为启用</summary>
        private void OnEnableAll(object sender, EventArgs e)
        {
            for (int i = 0; i < _listTools.Items.Count; i++)
                _listTools.SetItemChecked(i, true);
        }

        /// <summary>将列表中所有工具项设为禁用</summary>
        private void OnDisableAll(object sender, EventArgs e)
        {
            for (int i = 0; i < _listTools.Items.Count; i++)
                _listTools.SetItemChecked(i, false);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (this.DialogResult == DialogResult.OK)
            {
                for (int i = 0; i < _listTools.Items.Count && i < _workingItems.Count; i++)
                {
                    _workingItems[i].Visible = _listTools.GetItemChecked(i);
                }
            }
        }
    }
}
