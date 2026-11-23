using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NativeManagerDemo
{
    /// <summary>
    /// 右侧对象操作窗格：展示当前选中对象，编辑其关键属性，并提供常用操作。
    /// 所有编辑都经 ITopologyHost 契约下发，保证与命令行、画布交互同源。
    /// </summary>
    public class ObjectOperationsPanel : UserControl
    {
        private ITopologyHost _host;

        private ListBox _selList;
        private Label _typeValue;
        private TextBox _nameBox;
        private TextBox _xBox;
        private TextBox _yBox;
        private TextBox _wBox;
        private TextBox _hBox;
        private Button _fillBtn;
        private Button _borderBtn;
        private ComboBox _stateCombo;
        private Button _applyBtn;

        private Color _fill = Color.LightGray;
        private Color _border = Color.SteelBlue;
        private ShapeInfo _primary;

        /// <summary>面板发起的操作完成后触发，供宿主刷新状态栏等。</summary>
        public event EventHandler Changed;

        public ObjectOperationsPanel()
        {
            BuildUi();
        }

        public void Attach(ITopologyHost host)
        {
            _host = host;
        }

        private void BuildUi()
        {
            this.BackColor = Color.FromArgb(245, 248, 252);

            Label header = new Label();
            header.Text = "对象操作";
            header.Dock = DockStyle.Top;
            header.Height = 28;
            header.TextAlign = ContentAlignment.MiddleLeft;
            header.Font = new Font(this.Font, FontStyle.Bold);
            header.Padding = new Padding(8, 0, 0, 0);
            header.BackColor = Color.FromArgb(230, 238, 248);

            GroupBox selGroup = new GroupBox();
            selGroup.Text = "当前选中";
            selGroup.Dock = DockStyle.Top;
            selGroup.Height = 112;
            _selList = new ListBox();
            _selList.Dock = DockStyle.Fill;
            _selList.IntegralHeight = false;
            selGroup.Controls.Add(_selList);

            GroupBox propsGroup = new GroupBox();
            propsGroup.Text = "属性";
            propsGroup.Dock = DockStyle.Top;
            propsGroup.Height = 240;
            BuildProps(propsGroup);

            GroupBox opsGroup = new GroupBox();
            opsGroup.Text = "操作";
            opsGroup.Dock = DockStyle.Top;
            opsGroup.Height = 106;
            BuildOps(opsGroup);

            // 逆序添加：最后添加者最先停靠（在最上方）
            this.Controls.Add(opsGroup);
            this.Controls.Add(propsGroup);
            this.Controls.Add(selGroup);
            this.Controls.Add(header);
        }

        private void BuildProps(GroupBox g)
        {
            _nameBox = MakeText(g, 10, 24);
            AddLabel(g, "名称", 10, 24);
            _nameBox.Location = new Point(62, 21);
            _nameBox.Width = 190;

            AddLabel(g, "类型", 10, 52);
            _typeValue = new Label();
            _typeValue.Location = new Point(62, 52);
            _typeValue.AutoSize = true;
            _typeValue.ForeColor = Color.FromArgb(90, 100, 115);
            g.Controls.Add(_typeValue);

            AddLabel(g, "X", 10, 80);
            _xBox = MakeText(g, 34, 77); _xBox.Width = 68;
            AddLabel(g, "Y", 120, 80);
            _yBox = MakeText(g, 144, 77); _yBox.Width = 68;

            AddLabel(g, "宽", 10, 108);
            _wBox = MakeText(g, 34, 105); _wBox.Width = 68;
            AddLabel(g, "高", 120, 108);
            _hBox = MakeText(g, 144, 105); _hBox.Width = 68;

            AddLabel(g, "填充", 10, 138);
            _fillBtn = new Button();
            _fillBtn.Location = new Point(62, 135);
            _fillBtn.Size = new Size(70, 24);
            _fillBtn.FlatStyle = FlatStyle.Flat;
            _fillBtn.Click += new EventHandler(OnPickFill);
            g.Controls.Add(_fillBtn);

            AddLabel(g, "边框", 144, 138);
            _borderBtn = new Button();
            _borderBtn.Location = new Point(196, 135);
            _borderBtn.Size = new Size(56, 24);
            _borderBtn.FlatStyle = FlatStyle.Flat;
            _borderBtn.Click += new EventHandler(OnPickBorder);
            g.Controls.Add(_borderBtn);

            AddLabel(g, "状态", 10, 170);
            _stateCombo = new ComboBox();
            _stateCombo.Location = new Point(62, 167);
            _stateCombo.Width = 190;
            _stateCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            g.Controls.Add(_stateCombo);

            _applyBtn = new Button();
            _applyBtn.Text = "应用";
            _applyBtn.Location = new Point(62, 200);
            _applyBtn.Size = new Size(120, 26);
            _applyBtn.FlatStyle = FlatStyle.System;
            _applyBtn.Click += new EventHandler(OnApply);
            g.Controls.Add(_applyBtn);
        }

        private void BuildOps(GroupBox g)
        {
            MakeButton(g, "删除", 10, 24, OnDelete);
            MakeButton(g, "置顶", 94, 24, OnFront);
            MakeButton(g, "置底", 178, 24, OnBack);
            MakeButton(g, "全选", 10, 60, OnSelectAll);
            MakeButton(g, "取消选择", 94, 60, OnClearSel, 90);
        }

        private static Label AddLabel(GroupBox g, string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.AutoSize = true;
            g.Controls.Add(l);
            return l;
        }

        private static TextBox MakeText(GroupBox g, int x, int y)
        {
            TextBox t = new TextBox();
            t.Location = new Point(x, y);
            g.Controls.Add(t);
            return t;
        }

        private static Button MakeButton(GroupBox g, string text, int x, int y, EventHandler handler)
        {
            return MakeButton(g, text, x, y, handler, 78);
        }

        private static Button MakeButton(GroupBox g, string text, int x, int y, EventHandler handler, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(width, 28);
            b.FlatStyle = FlatStyle.System;
            b.Click += handler;
            g.Controls.Add(b);
            return b;
        }

        /// <summary>按当前选中集刷新整个面板。</summary>
        public void RefreshFromSelection()
        {
            if (_host == null) return;

            IList<ShapeInfo> sel = _host.ListSelection();

            _selList.BeginUpdate();
            _selList.Items.Clear();
            foreach (ShapeInfo si in sel)
                _selList.Items.Add(si.Name + "  (" + si.TypeName + ")");
            _selList.EndUpdate();

            _primary = sel.Count > 0 ? sel[0] : null;
            bool has = _primary != null;

            _nameBox.Enabled = has;
            _xBox.Enabled = has;
            _yBox.Enabled = has;
            _wBox.Enabled = has;
            _hBox.Enabled = has;
            _fillBtn.Enabled = has;
            _borderBtn.Enabled = has;
            _stateCombo.Enabled = has;
            _applyBtn.Enabled = has;

            if (!has)
            {
                _nameBox.Text = "";
                _xBox.Text = "";
                _yBox.Text = "";
                _wBox.Text = "";
                _hBox.Text = "";
                _typeValue.Text = "(未选择对象)";
                _stateCombo.Items.Clear();
                return;
            }

            _nameBox.Text = _primary.Name;
            _typeValue.Text = _primary.TypeName;
            _xBox.Text = _primary.X.ToString("0.#");
            _yBox.Text = _primary.Y.ToString("0.#");
            _wBox.Text = _primary.Width.ToString("0.#");
            _hBox.Text = _primary.Height.ToString("0.#");

            _fill = _primary.FillColor;
            _border = _primary.BorderColor;
            _fillBtn.BackColor = _fill;
            _borderBtn.BackColor = _border;

            _stateCombo.Items.Clear();
            if (_primary.States != null)
                foreach (string s in _primary.States)
                    _stateCombo.Items.Add(s);
            if (_primary.StateName != null && _stateCombo.Items.Contains(_primary.StateName))
                _stateCombo.SelectedItem = _primary.StateName;
            _stateCombo.Enabled = has && _stateCombo.Items.Count > 0;
        }

        private void OnApply(object sender, EventArgs e)
        {
            if (_host == null || _primary == null) return;

            _host.SetShapeProperty(_primary.Id, "Name", _nameBox.Text);
            _host.SetShapeProperty(_primary.Id, "X", _xBox.Text);
            _host.SetShapeProperty(_primary.Id, "Y", _yBox.Text);
            _host.SetShapeProperty(_primary.Id, "Width", _wBox.Text);
            _host.SetShapeProperty(_primary.Id, "Height", _hBox.Text);
            _host.SetShapeProperty(_primary.Id, "FillColor", ColorUtil.ToHex(_fill));
            _host.SetShapeProperty(_primary.Id, "BorderColor", ColorUtil.ToHex(_border));
            if (_stateCombo.SelectedItem != null)
                _host.SetShapeState(_primary.Id, _stateCombo.SelectedItem.ToString());

            RaiseChanged();
        }

        private void OnPickFill(object sender, EventArgs e)
        {
            Color c = PickColor(_fill);
            if (c != Color.Empty) { _fill = c; _fillBtn.BackColor = c; }
        }

        private void OnPickBorder(object sender, EventArgs e)
        {
            Color c = PickColor(_border);
            if (c != Color.Empty) { _border = c; _borderBtn.BackColor = c; }
        }

        private Color PickColor(Color current)
        {
            using (ColorDialog dlg = new ColorDialog())
            {
                dlg.Color = current;
                dlg.FullOpen = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    return dlg.Color;
            }
            return Color.Empty;
        }

        private void OnDelete(object sender, EventArgs e)
        {
            if (_host != null) _host.RemoveSelection();
            RaiseChanged();
        }

        private void OnFront(object sender, EventArgs e)
        {
            if (_host != null) _host.BringToFrontSelection();
            RaiseChanged();
        }

        private void OnBack(object sender, EventArgs e)
        {
            if (_host != null) _host.SendToBackSelection();
            RaiseChanged();
        }

        private void OnSelectAll(object sender, EventArgs e)
        {
            if (_host != null) _host.SelectAll();
            RaiseChanged();
        }

        private void OnClearSel(object sender, EventArgs e)
        {
            if (_host != null) _host.ClearSelection();
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
    }
}
