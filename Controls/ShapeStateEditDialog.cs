using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CloudNativeDesigner.Core;

namespace CloudNativeDesigner.Controls
{
    /// <summary>
    /// 图形状态编辑对话框（属性与图形合并为单页）。
    /// 左侧编辑颜色/优先级等属性，右侧编辑该状态专属多边形图形。
    /// </summary>
    public class ShapeStateEditDialog : Form
    {
        private Button _btnOk;
        private Button _btnCancel;
        private ColorDialog _colorDialog;
        private List<RenderCommand> _defaultCommands;

        // === 属性区（左侧） ===
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

        // === 图形区（右侧） ===
        private CheckBox _chkCustomGeom;
        private Panel _canvasPanel;
        private Button _btnAddVertex;
        private Button _btnDeleteVertex;
        private Button _btnClosePath;
        private Button _btnCopyDefault;
        private Button _btnClearGeom;
        private Label _lblDefaultHint;
        private Button _btnGeomFillColor;
        private Button _btnGeomBorderColor;

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
            this.ClientSize = new Size(720, 420);

            _colorDialog = new ColorDialog();

            BuildLayout(editState);

            _btnOk = new Button();
            _btnOk.Text = "确定";
            _btnOk.DialogResult = DialogResult.OK;
            _btnOk.Location = new Point(540, 385);
            _btnOk.Size = new Size(80, 28);
            this.Controls.Add(_btnOk);

            _btnCancel = new Button();
            _btnCancel.Text = "取消";
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.Location = new Point(630, 385);
            _btnCancel.Size = new Size(80, 28);
            this.Controls.Add(_btnCancel);

            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;
        }

        #region 布局构建

        private void BuildLayout(ShapeState editState)
        {
            // === 左侧属性区 ===
            int leftX = 10;
            int leftW = 200;
            int y = 12;
            int lblW = 70;
            int xVal = 80;

            GroupBox grpProp = new GroupBox();
            grpProp.Text = "属性";
            grpProp.Location = new Point(leftX, 10);
            grpProp.Size = new Size(leftW, 360);
            this.Controls.Add(grpProp);

            Label lblName = new Label();
            lblName.Text = "名称：";
            lblName.Location = new Point(8, y);
            lblName.Size = new Size(lblW, 20);
            lblName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            grpProp.Controls.Add(lblName);

            _txtName = new TextBox();
            _txtName.Location = new Point(xVal, y);
            _txtName.Size = new Size(110, 22);
            _txtName.Text = (editState != null) ? editState.Name : "Normal";
            grpProp.Controls.Add(_txtName);
            y += 34;

            Label lblFill = new Label();
            lblFill.Text = "填充颜色：";
            lblFill.Location = new Point(8, y);
            lblFill.Size = new Size(lblW, 24);
            lblFill.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            grpProp.Controls.Add(lblFill);

            _btnFillColor = new Button();
            _btnFillColor.Location = new Point(xVal, y);
            _btnFillColor.Size = new Size(80, 24);
            _btnFillColor.Click += new EventHandler(OnPickFillColor);
            grpProp.Controls.Add(_btnFillColor);
            y += 30;

            Label lblBorder = new Label();
            lblBorder.Text = "边框颜色：";
            lblBorder.Location = new Point(8, y);
            lblBorder.Size = new Size(lblW, 24);
            lblBorder.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            grpProp.Controls.Add(lblBorder);

            _btnBorderColor = new Button();
            _btnBorderColor.Location = new Point(xVal, y);
            _btnBorderColor.Size = new Size(80, 24);
            _btnBorderColor.Click += new EventHandler(OnPickBorderColor);
            grpProp.Controls.Add(_btnBorderColor);
            y += 30;

            Label lblText = new Label();
            lblText.Text = "文字颜色：";
            lblText.Location = new Point(8, y);
            lblText.Size = new Size(lblW, 24);
            lblText.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            grpProp.Controls.Add(lblText);

            _btnTextColor = new Button();
            _btnTextColor.Location = new Point(xVal, y);
            _btnTextColor.Size = new Size(80, 24);
            _btnTextColor.Click += new EventHandler(OnPickTextColor);
            grpProp.Controls.Add(_btnTextColor);
            y += 30;

            Label lblHeader = new Label();
            lblHeader.Text = "标题颜色：";
            lblHeader.Location = new Point(8, y);
            lblHeader.Size = new Size(lblW, 24);
            lblHeader.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            grpProp.Controls.Add(lblHeader);

            _btnHeaderColor = new Button();
            _btnHeaderColor.Location = new Point(xVal, y);
            _btnHeaderColor.Size = new Size(80, 24);
            _btnHeaderColor.Click += new EventHandler(OnPickHeaderColor);
            grpProp.Controls.Add(_btnHeaderColor);
            y += 30;

            Label lblPriority = new Label();
            lblPriority.Text = "优先级：";
            lblPriority.Location = new Point(8, y);
            lblPriority.Size = new Size(lblW, 22);
            lblPriority.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            grpProp.Controls.Add(lblPriority);

            _numPriority = new NumericUpDown();
            _numPriority.Location = new Point(xVal, y);
            _numPriority.Size = new Size(80, 22);
            _numPriority.Minimum = 0;
            _numPriority.Maximum = 100;
            _numPriority.Value = (editState != null) ? editState.Priority : 0;
            grpProp.Controls.Add(_numPriority);

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

            // === 右侧图形区 ===
            int rightX = 220;
            int rightW = 490;

            GroupBox grpGeom = new GroupBox();
            grpGeom.Text = "图形";
            grpGeom.Location = new Point(rightX, 10);
            grpGeom.Size = new Size(rightW, 360);
            this.Controls.Add(grpGeom);

            _chkCustomGeom = new CheckBox();
            _chkCustomGeom.Text = "使用自定义图形（不勾选则使用默认/初始图形）";
            _chkCustomGeom.Location = new Point(8, 18);
            _chkCustomGeom.Size = new Size(300, 20);
            _chkCustomGeom.CheckedChanged += new EventHandler(OnCustomGeomChanged);
            grpGeom.Controls.Add(_chkCustomGeom);

            // 画布
            _canvasPanel = new Panel();
            _canvasPanel.Location = new Point(8, 42);
            _canvasPanel.Size = new Size(300, 300);
            _canvasPanel.BackColor = Color.White;
            _canvasPanel.BorderStyle = BorderStyle.FixedSingle;
            _canvasPanel.Paint += new PaintEventHandler(OnCanvasPaint);
            _canvasPanel.MouseDown += new MouseEventHandler(OnCanvasMouseDown);
            _canvasPanel.MouseMove += new MouseEventHandler(OnCanvasMouseMove);
            _canvasPanel.MouseUp += new MouseEventHandler(OnCanvasMouseUp);
            _canvasPanel.DoubleClick += new EventHandler(OnCanvasDoubleClick);
            grpGeom.Controls.Add(_canvasPanel);

            // 默认图形提示（不勾选时显示）
            _lblDefaultHint = new Label();
            _lblDefaultHint.Text = "当前状态使用默认/初始图形。\n勾选上方复选框以绘制\n该状态专属图形。";
            _lblDefaultHint.Location = new Point(8, 42);
            _lblDefaultHint.Size = new Size(300, 300);
            _lblDefaultHint.ForeColor = Color.FromArgb(120, 120, 120);
            _lblDefaultHint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            _lblDefaultHint.BorderStyle = BorderStyle.FixedSingle;
            _lblDefaultHint.BackColor = Color.FromArgb(245, 245, 245);
            grpGeom.Controls.Add(_lblDefaultHint);

            // 右侧按钮
            int bx = 320;
            int by = 42;

            _btnAddVertex = new Button();
            _btnAddVertex.Text = "添加顶点";
            _btnAddVertex.Location = new Point(bx, by);
            _btnAddVertex.Size = new Size(120, 26);
            _btnAddVertex.Click += new EventHandler(OnAddVertex);
            grpGeom.Controls.Add(_btnAddVertex);
            by += 30;

            _btnDeleteVertex = new Button();
            _btnDeleteVertex.Text = "删除顶点";
            _btnDeleteVertex.Location = new Point(bx, by);
            _btnDeleteVertex.Size = new Size(120, 26);
            _btnDeleteVertex.Click += new EventHandler(OnDeleteVertex);
            grpGeom.Controls.Add(_btnDeleteVertex);
            by += 30;

            _btnClosePath = new Button();
            _btnClosePath.Text = "闭合路径";
            _btnClosePath.Location = new Point(bx, by);
            _btnClosePath.Size = new Size(120, 26);
            _btnClosePath.Click += new EventHandler(OnToggleClosePath);
            grpGeom.Controls.Add(_btnClosePath);
            by += 34;

            _btnGeomFillColor = new Button();
            _btnGeomFillColor.Text = "填充颜色...";
            _btnGeomFillColor.Location = new Point(bx, by);
            _btnGeomFillColor.Size = new Size(120, 26);
            _btnGeomFillColor.Click += new EventHandler(OnPickGeomFillColor);
            grpGeom.Controls.Add(_btnGeomFillColor);
            by += 30;

            _btnGeomBorderColor = new Button();
            _btnGeomBorderColor.Text = "边框颜色...";
            _btnGeomBorderColor.Location = new Point(bx, by);
            _btnGeomBorderColor.Size = new Size(120, 26);
            _btnGeomBorderColor.Click += new EventHandler(OnPickGeomBorderColor);
            grpGeom.Controls.Add(_btnGeomBorderColor);
            by += 34;

            _btnCopyDefault = new Button();
            _btnCopyDefault.Text = "复制默认图形";
            _btnCopyDefault.Location = new Point(bx, by);
            _btnCopyDefault.Size = new Size(120, 26);
            _btnCopyDefault.Click += new EventHandler(OnCopyDefaultGeom);
            grpGeom.Controls.Add(_btnCopyDefault);
            by += 30;

            _btnClearGeom = new Button();
            _btnClearGeom.Text = "清除图形";
            _btnClearGeom.Location = new Point(bx, by);
            _btnClearGeom.Size = new Size(120, 26);
            _btnClearGeom.Click += new EventHandler(OnClearGeom);
            grpGeom.Controls.Add(_btnClearGeom);
            by += 34;

            Label lblHint = new Label();
            lblHint.Text = "操作提示：\n· 单击画布添加顶点\n· 拖动顶点调整位置\n· 双击顶点删除\n· 至少需要3个顶点";
            lblHint.Location = new Point(bx, by);
            lblHint.Size = new Size(130, 80);
            lblHint.ForeColor = Color.FromArgb(100, 100, 100);
            grpGeom.Controls.Add(lblHint);

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
        }

        private void UpdateGeometryVisibility()
        {
            bool visible = _chkCustomGeom.Checked;
            _canvasPanel.Visible = visible;
            _btnAddVertex.Visible = visible;
            _btnDeleteVertex.Visible = visible;
            _btnClosePath.Visible = visible;
            _btnGeomFillColor.Visible = visible;
            _btnGeomBorderColor.Visible = visible;
            _btnCopyDefault.Visible = visible;
            _btnClearGeom.Visible = visible;
            _lblDefaultHint.Visible = !visible;
        }

        private void OnCustomGeomChanged(object sender, EventArgs e)
        {
            UpdateGeometryVisibility();
        }

        #endregion

        #region 图形加载与构建

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
                float canvasH = 260f;
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
                MessageBox.Show("当前自定义形状没有默认/初始图形可供复制。", "提示",
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

        private void OnPickGeomFillColor(object sender, EventArgs e)
        {
            _colorDialog.Color = _geomFillColor;
            if (_colorDialog.ShowDialog() == DialogResult.OK)
            {
                _geomFillColor = _colorDialog.Color;
                _canvasPanel.Invalidate();
            }
        }

        private void OnPickGeomBorderColor(object sender, EventArgs e)
        {
            _colorDialog.Color = _geomBorderColor;
            if (_colorDialog.ShowDialog() == DialogResult.OK)
            {
                _geomBorderColor = _colorDialog.Color;
                _canvasPanel.Invalidate();
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
                        MessageBox.Show("自定义图形至少需要 3 个顶点，已自动恢复为使用默认/初始图形。", "提示",
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
