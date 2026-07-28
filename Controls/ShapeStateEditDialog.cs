using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CloudNativeDesigner.Core;

namespace CloudNativeDesigner.Controls
{
    /// <summary>
    /// 图形状态编辑对话框。
    /// 支持编辑状态属性（颜色、优先级）以及该状态专属的自定义多边形图形。
    /// </summary>
    public class ShapeStateEditDialog : Form
    {
        // 通用
        private TabControl _tabControl;
        private Button _btnOk;
        private Button _btnCancel;
        private ColorDialog _colorDialog;
        private List<RenderCommand> _defaultCommands;

        // === 属性标签页 ===
        private TextBox _txtName;
        private Button _btnFillColor;
        private Button _btnBorderColor;
        private Button _btnTextColor;
        private Button _btnHeaderColor;
        private NumericUpDown _numPriority;
        private Color _fillColor;
        private Color _borderColor;
        private Color _textColor;
        private Color _headerColor;

        // === 图形标签页 ===
        private CheckBox _chkCustomGeom;
        private Panel _canvasPanel;
        private Button _btnAddVertex;
        private Button _btnDeleteVertex;
        private Button _btnClosePath;
        private Label _lblDefaultHint;

        private List<PointF> _vertices = new List<PointF>();
        private int _dragIndex = -1;
        private int _selectedVertex = -1;
        private bool _closedPath = false;
        private bool _filled = true;
        private Color _geomFillColor = Color.FromArgb(220, 240, 255);
        private Color _geomBorderColor = Color.FromArgb(80, 120, 180);

        public ShapeState ResultState { get; private set; }

        public ShapeStateEditDialog() : this(null, null) { }

        public ShapeStateEditDialog(ShapeState editState, List<RenderCommand> defaultCommands)
        {
            _defaultCommands = defaultCommands;
            this.Text = (editState == null) ? "添加状态" : "编辑状态";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(520, 450);

            _colorDialog = new ColorDialog();

            _tabControl = new TabControl();
            _tabControl.Location = new Point(10, 10);
            _tabControl.Size = new Size(490, 370);
            this.Controls.Add(_tabControl);

            BuildPropertyTab(editState);
            BuildGeometryTab(editState);

            _btnOk = new Button();
            _btnOk.Text = "确定";
            _btnOk.DialogResult = DialogResult.OK;
            _btnOk.Location = new Point(330, 400);
            _btnOk.Size = new Size(80, 28);
            this.Controls.Add(_btnOk);

            _btnCancel = new Button();
            _btnCancel.Text = "取消";
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.Location = new Point(420, 400);
            _btnCancel.Size = new Size(80, 28);
            this.Controls.Add(_btnCancel);

            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;
        }

        #region 属性标签页

        private void BuildPropertyTab(ShapeState editState)
        {
            TabPage page = new TabPage("属性");

            int y = 12;
            int lblW = 70;
            int xVal = 85;

            Label lblName = new Label();
            lblName.Text = "名称：";
            lblName.Location = new Point(10, y);
            lblName.Size = new Size(lblW, 20);
            lblName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            page.Controls.Add(lblName);

            _txtName = new TextBox();
            _txtName.Location = new Point(xVal, y);
            _txtName.Size = new Size(200, 22);
            _txtName.Text = (editState != null) ? editState.Name : "Normal";
            page.Controls.Add(_txtName);
            y += 36;

            // 填充颜色
            Label lblFill = new Label();
            lblFill.Text = "填充颜色：";
            lblFill.Location = new Point(10, y);
            lblFill.Size = new Size(lblW, 24);
            lblFill.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            page.Controls.Add(lblFill);

            _btnFillColor = new Button();
            _btnFillColor.Location = new Point(xVal, y);
            _btnFillColor.Size = new Size(80, 24);
            _btnFillColor.Click += new EventHandler(OnPickFillColor);
            page.Controls.Add(_btnFillColor);
            y += 34;

            // 边框颜色
            Label lblBorder = new Label();
            lblBorder.Text = "边框颜色：";
            lblBorder.Location = new Point(10, y);
            lblBorder.Size = new Size(lblW, 24);
            lblBorder.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            page.Controls.Add(lblBorder);

            _btnBorderColor = new Button();
            _btnBorderColor.Location = new Point(xVal, y);
            _btnBorderColor.Size = new Size(80, 24);
            _btnBorderColor.Click += new EventHandler(OnPickBorderColor);
            page.Controls.Add(_btnBorderColor);
            y += 34;

            // 文字颜色
            Label lblText = new Label();
            lblText.Text = "文字颜色：";
            lblText.Location = new Point(10, y);
            lblText.Size = new Size(lblW, 24);
            lblText.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            page.Controls.Add(lblText);

            _btnTextColor = new Button();
            _btnTextColor.Location = new Point(xVal, y);
            _btnTextColor.Size = new Size(80, 24);
            _btnTextColor.Click += new EventHandler(OnPickTextColor);
            page.Controls.Add(_btnTextColor);
            y += 34;

            // 标题颜色
            Label lblHeader = new Label();
            lblHeader.Text = "标题颜色：";
            lblHeader.Location = new Point(10, y);
            lblHeader.Size = new Size(lblW, 24);
            lblHeader.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            page.Controls.Add(lblHeader);

            _btnHeaderColor = new Button();
            _btnHeaderColor.Location = new Point(xVal, y);
            _btnHeaderColor.Size = new Size(80, 24);
            _btnHeaderColor.Click += new EventHandler(OnPickHeaderColor);
            page.Controls.Add(_btnHeaderColor);
            y += 34;

            // 优先级
            Label lblPriority = new Label();
            lblPriority.Text = "优先级：";
            lblPriority.Location = new Point(10, y);
            lblPriority.Size = new Size(lblW, 22);
            lblPriority.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            page.Controls.Add(lblPriority);

            _numPriority = new NumericUpDown();
            _numPriority.Location = new Point(xVal, y);
            _numPriority.Size = new Size(80, 22);
            _numPriority.Minimum = 0;
            _numPriority.Maximum = 100;
            _numPriority.Value = (editState != null) ? editState.Priority : 0;
            page.Controls.Add(_numPriority);

            // 初始化颜色
            if (editState != null)
            {
                _fillColor = editState.FillColor.ToColor();
                _borderColor = editState.BorderColor.ToColor();
                _textColor = editState.TextColor.ToColor();
                _headerColor = editState.HeaderColor.ToColor();
            }
            else
            {
                _fillColor = Color.FromArgb(230, 240, 255);
                _borderColor = Color.FromArgb(80, 120, 180);
                _textColor = Color.FromArgb(40, 40, 40);
                _headerColor = Color.FromArgb(80, 130, 180);
            }
            UpdateColorButtons();

            _tabControl.TabPages.Add(page);
        }

        #endregion

        #region 图形标签页

        private void BuildGeometryTab(ShapeState editState)
        {
            TabPage page = new TabPage("图形");

            _chkCustomGeom = new CheckBox();
            _chkCustomGeom.Text = "使用自定义图形（不勾选则使用默认图形）";
            _chkCustomGeom.Location = new Point(10, 10);
            _chkCustomGeom.Size = new Size(400, 22);
            _chkCustomGeom.CheckedChanged += new EventHandler(OnCustomGeomChanged);
            page.Controls.Add(_chkCustomGeom);

            // 画布
            _canvasPanel = new Panel();
            _canvasPanel.Location = new Point(10, 38);
            _canvasPanel.Size = new Size(300, 280);
            _canvasPanel.BackColor = Color.White;
            _canvasPanel.BorderStyle = BorderStyle.FixedSingle;
            _canvasPanel.Paint += new PaintEventHandler(OnCanvasPaint);
            _canvasPanel.MouseDown += new MouseEventHandler(OnCanvasMouseDown);
            _canvasPanel.MouseMove += new MouseEventHandler(OnCanvasMouseMove);
            _canvasPanel.MouseUp += new MouseEventHandler(OnCanvasMouseUp);
            _canvasPanel.DoubleClick += new EventHandler(OnCanvasDoubleClick);
            page.Controls.Add(_canvasPanel);

            int x = 325;
            int y = 38;

            _btnAddVertex = new Button();
            _btnAddVertex.Text = "添加顶点";
            _btnAddVertex.Location = new Point(x, y);
            _btnAddVertex.Size = new Size(120, 28);
            _btnAddVertex.Click += new EventHandler(OnAddVertex);
            page.Controls.Add(_btnAddVertex);
            y += 34;

            _btnDeleteVertex = new Button();
            _btnDeleteVertex.Text = "删除顶点";
            _btnDeleteVertex.Location = new Point(x, y);
            _btnDeleteVertex.Size = new Size(120, 28);
            _btnDeleteVertex.Click += new EventHandler(OnDeleteVertex);
            page.Controls.Add(_btnDeleteVertex);
            y += 34;

            _btnClosePath = new Button();
            _btnClosePath.Text = "闭合路径";
            _btnClosePath.Location = new Point(x, y);
            _btnClosePath.Size = new Size(120, 28);
            _btnClosePath.Click += new EventHandler(OnToggleClosePath);
            page.Controls.Add(_btnClosePath);
            y += 40;

            Button btnCopyDefault = new Button();
            btnCopyDefault.Text = "复制默认图形";
            btnCopyDefault.Location = new Point(x, y);
            btnCopyDefault.Size = new Size(120, 28);
            btnCopyDefault.Click += new EventHandler(OnCopyDefaultGeom);
            page.Controls.Add(btnCopyDefault);
            y += 34;

            Button btnClearGeom = new Button();
            btnClearGeom.Text = "清除图形";
            btnClearGeom.Location = new Point(x, y);
            btnClearGeom.Size = new Size(120, 28);
            btnClearGeom.Click += new EventHandler(OnClearGeom);
            page.Controls.Add(btnClearGeom);
            y += 40;

            Label lblHint = new Label();
            lblHint.Text = "操作提示：\n· 单击画布添加顶点\n· 拖动顶点调整位置\n· 双击顶点删除\n· 至少需要3个顶点";
            lblHint.Location = new Point(x, y);
            lblHint.Size = new Size(120, 80);
            lblHint.ForeColor = Color.FromArgb(100, 100, 100);
            page.Controls.Add(lblHint);

            // 默认图形提示（不勾选时显示）
            _lblDefaultHint = new Label();
            _lblDefaultHint.Text = "当前状态使用默认图形。\n勾选上方复选框以绘制\n该状态专属图形。";
            _lblDefaultHint.Location = new Point(10, 38);
            _lblDefaultHint.Size = new Size(300, 280);
            _lblDefaultHint.ForeColor = Color.FromArgb(120, 120, 120);
            _lblDefaultHint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            _lblDefaultHint.BorderStyle = BorderStyle.FixedSingle;
            _lblDefaultHint.BackColor = Color.FromArgb(245, 245, 245);
            page.Controls.Add(_lblDefaultHint);

            // 加载已有图形数据
            bool hasCustom = (editState != null && editState.UseCustomRenderCommands
                && editState.CustomRenderCommands != null
                && editState.CustomRenderCommands.Count > 0);

            if (hasCustom)
            {
                _chkCustomGeom.Checked = true;
                LoadVerticesFromCommands(editState.CustomRenderCommands);
            }
            else
            {
                _chkCustomGeom.Checked = false;
            }

            UpdateGeometryVisibility();
            _tabControl.TabPages.Add(page);
        }

        private void UpdateGeometryVisibility()
        {
            bool visible = _chkCustomGeom.Checked;
            _canvasPanel.Visible = visible;
            _btnAddVertex.Visible = visible;
            _btnDeleteVertex.Visible = visible;
            _btnClosePath.Visible = visible;
            _lblDefaultHint.Visible = !visible;
        }

        private void OnCustomGeomChanged(object sender, EventArgs e)
        {
            UpdateGeometryVisibility();
        }

        /// <summary>
        /// 从 RenderCommand 列表还原顶点（假设第一条是多边形命令）
        /// </summary>
        private void LoadVerticesFromCommands(List<RenderCommand> cmds)
        {
            _vertices.Clear();
            if (cmds == null || cmds.Count == 0)
                return;

            RenderCommand polyCmd = null;
            foreach (RenderCommand cmd in cmds)
            {
                if (cmd.CommandType == RenderCommandType.Polygon)
                {
                    polyCmd = cmd;
                    break;
                }
            }

            if (polyCmd != null && polyCmd.PolygonPoints != null && polyCmd.PolygonPoints.Length >= 3)
            {
                float canvasW = 260f;
                float canvasH = 240f;
                float offsetX = 20f;
                float offsetY = 20f;

                foreach (PointF pt in polyCmd.PolygonPoints)
                {
                    _vertices.Add(new PointF(offsetX + pt.X * canvasW, offsetY + pt.Y * canvasH));
                }
                _closedPath = true;
                _geomFillColor = polyCmd.FillColor;
                _geomBorderColor = polyCmd.StrokeColor;
                _filled = polyCmd.Fill;
            }
        }

        /// <summary>
        /// 将当前画布顶点构建为 RenderCommand 列表（归一化坐标）
        /// </summary>
        private List<RenderCommand> BuildRenderCommands()
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
            {
                normalized.Add(new PointF((pt.X - minX) / rangeX, (pt.Y - minY) / rangeY));
            }

            RenderCommand polyCmd = new RenderCommand();
            polyCmd.CommandType = RenderCommandType.Polygon;
            polyCmd.PolygonPoints = normalized.ToArray();
            polyCmd.X = 0;
            polyCmd.Y = 0;
            polyCmd.Width = 1;
            polyCmd.Height = 1;
            polyCmd.FillColor = _filled ? _geomFillColor : Color.Transparent;
            polyCmd.StrokeColor = _geomBorderColor;
            polyCmd.StrokeWidth = 2f;
            polyCmd.Fill = _filled;

            return new List<RenderCommand> { polyCmd };
        }

        private void OnCopyDefaultGeom(object sender, EventArgs e)
        {
            if (_defaultCommands == null || _defaultCommands.Count == 0)
            {
                MessageBox.Show("当前自定义形状没有默认图形可供复制。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            LoadVerticesFromCommands(_defaultCommands);
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

        #region 画布事件

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
                if (i == 0)
                    path.StartFigure();
                path.AddLine(_vertices[i], _vertices[(i + 1) % _vertices.Count]);
            }
            if (_closedPath)
                path.CloseFigure();

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
                newPt = new PointF(last.X + 20, last.Y + 20);
            }
            else
            {
                newPt = new PointF(150, 140);
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
            _btnClosePath.Text = _closedPath ? "打开路径" : "闭合路径";
            _canvasPanel.Invalidate();
        }

        #endregion

        #region 颜色选择

        private void UpdateColorButtons()
        {
            _btnFillColor.BackColor = _fillColor;
            _btnBorderColor.BackColor = _borderColor;
            _btnTextColor.BackColor = _textColor;
            _btnHeaderColor.BackColor = _headerColor;
            _btnFillColor.ForeColor = GetContrastColor(_fillColor);
            _btnBorderColor.ForeColor = GetContrastColor(_borderColor);
            _btnTextColor.ForeColor = GetContrastColor(_textColor);
            _btnHeaderColor.ForeColor = GetContrastColor(_headerColor);
        }

        private Color GetContrastColor(Color c)
        {
            float brightness = c.R * 0.299f + c.G * 0.587f + c.B * 0.114f;
            return brightness > 128 ? Color.Black : Color.White;
        }

        private void OnPickFillColor(object sender, EventArgs e)
        {
            _colorDialog.Color = _fillColor;
            if (_colorDialog.ShowDialog() == DialogResult.OK)
            {
                _fillColor = _colorDialog.Color;
                UpdateColorButtons();
            }
        }

        private void OnPickBorderColor(object sender, EventArgs e)
        {
            _colorDialog.Color = _borderColor;
            if (_colorDialog.ShowDialog() == DialogResult.OK)
            {
                _borderColor = _colorDialog.Color;
                UpdateColorButtons();
            }
        }

        private void OnPickTextColor(object sender, EventArgs e)
        {
            _colorDialog.Color = _textColor;
            if (_colorDialog.ShowDialog() == DialogResult.OK)
            {
                _textColor = _colorDialog.Color;
                UpdateColorButtons();
            }
        }

        private void OnPickHeaderColor(object sender, EventArgs e)
        {
            _colorDialog.Color = _headerColor;
            if (_colorDialog.ShowDialog() == DialogResult.OK)
            {
                _headerColor = _colorDialog.Color;
                UpdateColorButtons();
            }
        }

        #endregion

        #region 构建结果

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (this.DialogResult == DialogResult.OK)
            {
                string name = _txtName.Text.Trim();
                if (string.IsNullOrEmpty(name))
                    name = "Normal";

                ResultState = new ShapeState();
                ResultState.Name = name;
                ResultState.FillColor = new XmlColor(_fillColor);
                ResultState.BorderColor = new XmlColor(_borderColor);
                ResultState.TextColor = new XmlColor(_textColor);
                ResultState.HeaderColor = new XmlColor(_headerColor);
                ResultState.Priority = (int)_numPriority.Value;

                if (_chkCustomGeom.Checked)
                {
                    ResultState.UseCustomRenderCommands = true;
                    ResultState.CustomRenderCommands = BuildRenderCommands();
                    if (ResultState.CustomRenderCommands == null)
                    {
                        MessageBox.Show("自定义图形至少需要 3 个顶点，已自动恢复为使用默认图形。", "提示",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        ResultState.UseCustomRenderCommands = false;
                        ResultState.CustomRenderCommands = new List<RenderCommand>();
                    }
                }
                else
                {
                    ResultState.UseCustomRenderCommands = false;
                    ResultState.CustomRenderCommands = new List<RenderCommand>();
                }
            }
        }

        #endregion
    }
}
