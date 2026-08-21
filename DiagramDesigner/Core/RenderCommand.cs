using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DiagramDesigner.Core
{
    public enum RenderCommandType
    {
        Rectangle,
        Ellipse,
        Polygon,
        RoundedRect,
        Line,
        Text,
        MemberArea,
        /// <summary>复合多路径图形：由多个封闭路径经布尔运算组合而成</summary>
        CompoundPolygon
    }

    /// <summary>
    /// 布尔运算类型。用于 CompoundPolygon 类型 RenderCommand 的多路径组合。
    /// </summary>
    public enum BooleanOperation
    {
        /// <summary>各路径独立绘制（不组合）</summary>
        None,
        /// <summary>并集：所有路径的合并区域</summary>
        Union,
        /// <summary>差集：第一条路径减去其余路径</summary>
        Subtract,
        /// <summary>交集：所有路径的公共区域</summary>
        Intersect,
        /// <summary>异或：所有路径的对称差</summary>
        Xor
    }

    [Serializable]
    public class RenderCommand
    {
        private RenderCommandType _commandType = RenderCommandType.Rectangle;
        private float _x = 0f;
        private float _y = 0f;
        private float _width = 1f;
        private float _height = 1f;
        private float _cornerRadius = 0f;
        private XmlColor _fillColor = new XmlColor(Color.Transparent);
        private XmlColor _strokeColor = new XmlColor(Color.Black);
        private float _strokeWidth = 1f;
        private string _text = "";
        private string _textAlign = "center";
        private float _fontSize = 10f;
        private bool _isBold = false;
        private PointF[] _polygonPoints = null;
        private bool _useShapeColors = true;
        private bool _fill = true;
        private bool _stroke = true;

        // === 曲线句柄数据（Polygon 类型，与 _polygonPoints 等长）===
        private HandleType[] _polyHandleTypes = null;
        private PointF[] _polyHandleIns = null;
        private PointF[] _polyHandleOuts = null;

        // === 多路径与布尔运算 ===
        private List<PointF[]> _multiPaths = null;
        private BooleanOperation _boolOp = BooleanOperation.None;
        private List<PathDef> _pathDefs = null;

        public RenderCommandType CommandType
        {
            get { return _commandType; }
            set { _commandType = value; }
        }

        public float X
        {
            get { return _x; }
            set { _x = value; }
        }

        public float Y
        {
            get { return _y; }
            set { _y = value; }
        }

        public float Width
        {
            get { return _width; }
            set { _width = value; }
        }

        public float Height
        {
            get { return _height; }
            set { _height = value; }
        }

        public float CornerRadius
        {
            get { return _cornerRadius; }
            set { _cornerRadius = value; }
        }

        public XmlColor FillColor
        {
            get { return _fillColor; }
            set { _fillColor = value; }
        }

        public XmlColor StrokeColor
        {
            get { return _strokeColor; }
            set { _strokeColor = value; }
        }

        public float StrokeWidth
        {
            get { return _strokeWidth; }
            set { _strokeWidth = value; }
        }

        public string Text
        {
            get { return _text; }
            set { _text = value; }
        }

        public string TextAlign
        {
            get { return _textAlign; }
            set { _textAlign = value; }
        }

        public float FontSize
        {
            get { return _fontSize; }
            set { _fontSize = value; }
        }

        public bool IsBold
        {
            get { return _isBold; }
            set { _isBold = value; }
        }

        public PointF[] PolygonPoints
        {
            get { return _polygonPoints; }
            set { _polygonPoints = value; }
        }

        public bool UseShapeColors
        {
            get { return _useShapeColors; }
            set { _useShapeColors = value; }
        }

        public bool Fill
        {
            get { return _fill; }
            set { _fill = value; }
        }

        public bool Stroke
        {
            get { return _stroke; }
            set { _stroke = value; }
        }

        /// <summary>
        /// 多路径列表。每条路径为一个归一化(0~1)顶点数组。
        /// 仅当 CommandType 为 CompoundPolygon 时使用。
        /// </summary>
        public List<PointF[]> MultiPaths
        {
            get { return _multiPaths; }
            set { _multiPaths = value; }
        }

        /// <summary>
        /// 布尔运算类型。仅当 CommandType 为 CompoundPolygon 且 PathDefs 为空时使用（全局布尔运算）。
        /// </summary>
        public BooleanOperation BoolOp
        {
            get { return _boolOp; }
            set { _boolOp = value; }
        }

        /// <summary>
        /// 路径定义列表。每条路径拥有独立的 BoolOp，
        /// 仅与紧邻的下层路径计算（邻居模式）。
        /// 优先于 MultiPaths + BoolOp 使用。
        /// </summary>
        public List<PathDef> PathDefs
        {
            get { return _pathDefs; }
            set { _pathDefs = value; }
        }

        /// <summary>Polygon 类型的顶点句柄类型数组（null 表示无曲线）</summary>
        public HandleType[] PolyHandleTypes
        {
            get { return _polyHandleTypes; }
            set { _polyHandleTypes = value; }
        }

        /// <summary>Polygon 类型的进边控制点偏移数组（归一化坐标）</summary>
        public PointF[] PolyHandleIns
        {
            get { return _polyHandleIns; }
            set { _polyHandleIns = value; }
        }

        /// <summary>Polygon 类型的出边控制点偏移数组（归一化坐标）</summary>
        public PointF[] PolyHandleOuts
        {
            get { return _polyHandleOuts; }
            set { _polyHandleOuts = value; }
        }

        /// <summary>Polygon 是否有曲线句柄</summary>
        public bool PolygonHasCurves
        {
            get
            {
                if (_polyHandleTypes == null) return false;
                foreach (HandleType ht in _polyHandleTypes)
                    if (ht != HandleType.None) return true;
                return false;
            }
        }

        public void Execute(Graphics g, RectangleF bounds, ShapeColors colors, float scale)
        {
            float absX = bounds.X + _x * bounds.Width;
            float absY = bounds.Y + _y * bounds.Height;
            float absW = _width * bounds.Width;
            float absH = _height * bounds.Height;
            RectangleF rect = new RectangleF(absX, absY, absW, absH);

            Color stroke = _useShapeColors ? colors.BorderColor : _strokeColor.ToColor();
            Color textColor = colors.TextColor;

            switch (_commandType)
            {
                case RenderCommandType.Rectangle:
                    DrawRectangle(g, rect, colors, stroke, scale);
                    break;
                case RenderCommandType.Ellipse:
                    DrawEllipse(g, rect, colors, stroke, scale);
                    break;
                case RenderCommandType.RoundedRect:
                    DrawRoundedRect(g, rect, colors, stroke, scale);
                    break;
                case RenderCommandType.Polygon:
                    DrawPolygon(g, rect, colors, stroke, scale);
                    break;
                case RenderCommandType.CompoundPolygon:
                    DrawCompoundPolygon(g, rect, colors, stroke, scale);
                    break;
                case RenderCommandType.Line:
                    DrawLine(g, rect, stroke, scale);
                    break;
                case RenderCommandType.Text:
                    DrawText(g, rect, textColor, scale);
                    break;
            }
        }

        private Brush CreateFillBrush(RectangleF rect, ShapeColors colors)
        {
            if (colors.UseGradient)
            {
                return new LinearGradientBrush(
                    new PointF(rect.X, rect.Y),
                    new PointF(rect.Right, rect.Bottom),
                    colors.FillColorDark,
                    colors.FillColorLight);
            }
            return new SolidBrush(colors.FillColor);
        }

        private void DrawRectangle(Graphics g, RectangleF rect, ShapeColors colors, Color stroke, float scale)
        {
            if (_fill)
            {
                using (Brush brush = CreateFillBrush(rect, colors))
                    g.FillRectangle(brush, rect);
            }
            if (_stroke)
            {
                using (Pen pen = new Pen(stroke, _strokeWidth / scale))
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }
        }

        private void DrawEllipse(Graphics g, RectangleF rect, ShapeColors colors, Color stroke, float scale)
        {
            if (_fill)
            {
                using (Brush brush = CreateFillBrush(rect, colors))
                    g.FillEllipse(brush, rect);
            }
            if (_stroke)
            {
                using (Pen pen = new Pen(stroke, _strokeWidth / scale))
                    g.DrawEllipse(pen, rect);
            }
        }

        private void DrawRoundedRect(Graphics g, RectangleF rect, ShapeColors colors, Color stroke, float scale)
        {
            using (GraphicsPath path = GraphicsUtility.CreateRoundedRectPath(rect, _cornerRadius))
            {
                if (_fill)
                {
                    using (Brush brush = CreateFillBrush(rect, colors))
                        g.FillPath(brush, path);
                }
                if (_stroke)
                {
                    using (Pen pen = new Pen(stroke, _strokeWidth / scale))
                        g.DrawPath(pen, path);
                }
            }
        }

        private void DrawPolygon(Graphics g, RectangleF rect, ShapeColors colors, Color stroke, float scale)
        {
            if (_polygonPoints == null || _polygonPoints.Length < 3)
                return;

            // 有曲线句柄时使用 Bezier 路径渲染
            if (PolygonHasCurves)
            {
                using (GraphicsPath gp = BuildCurveGraphicsPath(_polygonPoints, rect,
                    _polyHandleTypes, _polyHandleIns, _polyHandleOuts, true))
                {
                    if (_fill)
                    {
                        using (Brush brush = CreateFillBrush(rect, colors))
                            g.FillPath(brush, gp);
                    }
                    if (_stroke)
                    {
                        using (Pen pen = new Pen(stroke, _strokeWidth / scale))
                            g.DrawPath(pen, gp);
                    }
                }
                return;
            }

            // 无曲线：快速路径
            PointF[] pts = new PointF[_polygonPoints.Length];
            for (int i = 0; i < _polygonPoints.Length; i++)
            {
                pts[i] = new PointF(
                    rect.X + _polygonPoints[i].X * rect.Width,
                    rect.Y + _polygonPoints[i].Y * rect.Height);
            }

            if (_fill)
            {
                using (Brush brush = CreateFillBrush(rect, colors))
                    g.FillPolygon(brush, pts);
            }
            if (_stroke)
            {
                using (Pen pen = new Pen(stroke, _strokeWidth / scale))
                    g.DrawPolygon(pen, pts);
            }
        }

        /// <summary>
        /// 从归一化顶点和句柄数据构建含贝塞尔曲线的 GraphicsPath。
        /// 每条边：若两端均无句柄则用直线，否则用三次贝塞尔曲线。
        /// </summary>
        public static GraphicsPath BuildCurveGraphicsPath(PointF[] normPoints, RectangleF rect,
            HandleType[] handleTypes, PointF[] handleIns, PointF[] handleOuts, bool closed)
        {
            GraphicsPath gp = new GraphicsPath();
            gp.FillMode = FillMode.Winding;
            int n = normPoints.Length;
            if (n < 2) return gp;

            // 计算绝对坐标顶点
            PointF[] absPts = new PointF[n];
            for (int i = 0; i < n; i++)
                absPts[i] = new PointF(
                    rect.X + normPoints[i].X * rect.Width,
                    rect.Y + normPoints[i].Y * rect.Height);

            int edgeCount = closed ? n : n - 1;
            for (int i = 0; i < edgeCount; i++)
            {
                int next = (i + 1) % n;
                PointF p1 = absPts[i];
                PointF p2 = absPts[next];

                bool hasOut = handleTypes != null && i < handleTypes.Length && handleTypes[i] != HandleType.None;
                bool hasIn = handleTypes != null && next < handleTypes.Length && handleTypes[next] != HandleType.None;

                if (!hasOut && !hasIn)
                {
                    gp.AddLine(p1, p2);
                }
                else
                {
                    // 三次贝塞尔：cp1 从 p1 的 HandleOut，cp2 从 p2 的 HandleIn
                    PointF cp1 = hasOut
                        ? new PointF(p1.X + handleOuts[i].X * rect.Width, p1.Y + handleOuts[i].Y * rect.Height)
                        : p1;
                    PointF cp2 = hasIn
                        ? new PointF(p2.X + handleIns[next].X * rect.Width, p2.Y + handleIns[next].Y * rect.Height)
                        : p2;
                    gp.AddBezier(p1, cp1, cp2, p2);
                }
            }
            if (closed) gp.CloseFigure();
            return gp;
        }

        /// <summary>
        /// 绘制复合多路径图形。支持逐路径布尔运算（邻居模式）。
        /// 每条路径的 BoolOp 作用于紧邻的下层路径（索引 i+1）。
        /// 处理方向：自底向上（从最后一条路径开始）。
        /// 最后一条路径的 BoolOp 始终视为 None（无下层路径）。
        /// 描边：仅绘制布尔运算结果区域的轮廓，而非所有原始路径。
        /// </summary>
        private void DrawCompoundPolygon(Graphics g, RectangleF rect, ShapeColors colors, Color stroke, float scale)
        {
            // 获取有效的路径定义列表
            List<PathDef> defs = GetEffectivePathDefs();
            if (defs == null || defs.Count == 0)
                return;

            // 将归一化路径转换为绝对坐标 GraphicsPath
            List<GraphicsPath> absPaths = new List<GraphicsPath>();
            foreach (PathDef def in defs)
            {
                // 非形状（线体）允许 2+ 顶点；形状需要 3+ 顶点
                int minPoints = def.IsShape ? 3 : 2;
                if (def.Points == null || def.Points.Length < minPoints || !def.Visible)
                {
                    absPaths.Add(null); // 占位，保持索引对齐
                    continue;
                }
                GraphicsPath gp;
                if (def.HasCurves)
                {
                    gp = BuildCurveGraphicsPath(def.Points, rect,
                        def.HandleTypes, def.HandleIns, def.HandleOuts, def.IsShape);
                }
                else
                {
                    PointF[] pts = new PointF[def.Points.Length];
                    for (int i = 0; i < def.Points.Length; i++)
                    {
                        pts[i] = new PointF(
                            rect.X + def.Points[i].X * rect.Width,
                            rect.Y + def.Points[i].Y * rect.Height);
                    }
                    gp = new GraphicsPath();
                    gp.FillMode = FillMode.Winding;
                    if (def.IsShape)
                        gp.AddPolygon(pts);
                    else
                    {
                        // 线体：添加开放路径（不闭合）
                        for (int i = 0; i < pts.Length - 1; i++)
                            gp.AddLine(pts[i], pts[i + 1]);
                    }
                }
                absPaths.Add(gp);
            }

            // === 渲染组：自上而下两两计算 ===
            // 每条路径的 BoolOp 描述如何将累积结果与下方路径组合
            // Subtract 方向：上层减去下层（累积结果 Exclude 下层路径）
            // 路径角色：0=Positive(Union/Xor/基底), 1=Negative(Subtract), 2=Constrained(Intersect)
            List<Region> renderRegions = new List<Region>();
            List<List<int>> regionAllPaths = new List<List<int>>();
            List<List<BooleanOperation>> regionOps = new List<List<BooleanOperation>>();
            List<bool> regionIsModified = new List<bool>();
            List<bool> regionAllXor = new List<bool>();

            Region accumulated = null;
            List<int> currentPaths = null;
            List<BooleanOperation> currentOps = null;
            BooleanOperation pendingOp = BooleanOperation.None;
            bool currentModified = false;
            bool currentAllXor = true;

            for (int i = 0; i < absPaths.Count; i++)
            {
                if (absPaths[i] == null) continue;

                BooleanOperation op = defs[i].BoolOp;

                // 检查下方是否有非空路径
                int nextIdx = -1;
                for (int j = i + 1; j < absPaths.Count; j++)
                    if (absPaths[j] != null) { nextIdx = j; break; }

                if (accumulated == null)
                {
                    // 开始新组
                    accumulated = new Region(absPaths[i]);
                    currentPaths = new List<int>(); currentPaths.Add(i);
                    currentOps = new List<BooleanOperation>(); currentOps.Add(BooleanOperation.None);
                    pendingOp = op;
                    currentModified = false;
                    currentAllXor = true;

                    if (nextIdx < 0 || op == BooleanOperation.None)
                    {
                        renderRegions.Add(accumulated);
                        regionAllPaths.Add(currentPaths);
                        regionOps.Add(currentOps);
                        regionIsModified.Add(false);
                        regionAllXor.Add(false);
                        accumulated = null;
                    }
                }
                else
                {
                    // 用上一条路径的 BoolOp 将累积结果与当前路径组合
                    switch (pendingOp)
                    {
                        case BooleanOperation.Union:
                            accumulated.Union(absPaths[i]);
                            currentAllXor = false;
                            break;
                        case BooleanOperation.Subtract:
                            accumulated.Exclude(absPaths[i]); // 上层减下层
                            currentAllXor = false;
                            break;
                        case BooleanOperation.Intersect:
                            accumulated.Intersect(absPaths[i]);
                            currentAllXor = false;
                            break;
                        case BooleanOperation.Xor:
                            accumulated.Xor(absPaths[i]);
                            // currentAllXor 保持 true
                            break;
                        default:
                            currentAllXor = false;
                            break;
                    }
                    currentModified = true;
                    currentPaths.Add(i);
                    currentOps.Add(pendingOp);
                    pendingOp = op;

                    if (nextIdx < 0 || op == BooleanOperation.None)
                    {
                        renderRegions.Add(accumulated);
                        regionAllPaths.Add(currentPaths);
                        regionOps.Add(currentOps);
                        regionIsModified.Add(currentModified);
                        regionAllXor.Add(currentAllXor);
                        accumulated = null;
                    }
                }
            }

            // === 填充 ===
            if (_fill)
            {
                using (Brush brush = CreateFillBrush(rect, colors))
                {
                    for (int r = 0; r < renderRegions.Count; r++)
                    {
                        // 跳过非形状（线体）路径的填充（检查组内首条路径）
                        int firstIdx = regionAllPaths[r][0];
                        if (firstIdx >= 0 && firstIdx < defs.Count && !defs[firstIdx].IsShape)
                            continue;

                        if (!regionIsModified[r])
                        {
                            using (GraphicsPath combined = new GraphicsPath())
                            {
                                combined.FillMode = FillMode.Winding;
                                foreach (int pidx in regionAllPaths[r])
                                    if (absPaths[pidx] != null)
                                        combined.AddPath(absPaths[pidx], false);
                                g.FillPath(brush, combined);
                            }
                        }
                        else
                        {
                            g.FillRegion(brush, renderRegions[r]);
                        }
                    }
                }
            }

            // === 描边 ===
            // 未修改组直接描边原始路径；已修改组用精确逐路径裁剪统一描边
            if (_stroke)
            {
                using (Pen pen = new Pen(stroke, _strokeWidth / scale))
                {
                    float penWidth = _strokeWidth / scale;
                    float erodeDist = Math.Max(penWidth, 2f) + 2f;

                    for (int r = 0; r < renderRegions.Count; r++)
                    {
                        if (!regionIsModified[r])
                        {
                            // 未修改组：直接描边原始路径
                            foreach (int pidx in regionAllPaths[r])
                                if (pidx >= 0 && pidx < absPaths.Count && absPaths[pidx] != null)
                                    g.DrawPath(pen, absPaths[pidx]);
                        }
                        else
                        {
                            // 已修改组：精确逐路径裁剪统一描边
                            RegionOutlineTracer.StrokeModifiedGroup(
                                g, pen, erodeDist,
                                absPaths, regionAllPaths[r], regionOps[r],
                                renderRegions[r], regionAllXor[r]);
                        }
                    }
                }
            }

            // 释放临时资源
            foreach (Region r in renderRegions)
                r.Dispose();
            foreach (GraphicsPath gp in absPaths)
            {
                if (gp != null)
                    gp.Dispose();
            }
        }

        /// <summary>
        /// 获取有效的路径定义列表。
        /// 优先使用 PathDefs，其次从 MultiPaths + BoolOp 转换。
        /// </summary>
        private List<PathDef> GetEffectivePathDefs()
        {
            if (_pathDefs != null && _pathDefs.Count > 0)
                return _pathDefs;

            if (_multiPaths == null || _multiPaths.Count == 0)
                return null;

            // 从 MultiPaths + 全局 BoolOp 转换
            // 新语义：最后一条路径无 BoolOp（无下层路径），其余使用全局 BoolOp
            List<PathDef> defs = new List<PathDef>();
            for (int i = 0; i < _multiPaths.Count; i++)
            {
                BooleanOperation op = (i == _multiPaths.Count - 1) ? BooleanOperation.None : _boolOp;
                defs.Add(new PathDef(_multiPaths[i], op));
            }
            return defs;
        }

        private void DrawLine(Graphics g, RectangleF rect, Color stroke, float scale)
        {
            using (Pen pen = new Pen(stroke, _strokeWidth / scale))
            {
                g.DrawLine(pen, rect.X, rect.Y, rect.Right, rect.Bottom);
            }
        }

        private void DrawText(Graphics g, RectangleF rect, Color textColor, float scale)
        {
            if (string.IsNullOrEmpty(_text))
                return;

            FontStyle style = _isBold ? FontStyle.Bold : FontStyle.Regular;
            using (Font font = new Font("Microsoft YaHei", _fontSize / scale, style))
            using (Brush brush = new SolidBrush(textColor))
            {
                StringFormat sf = new StringFormat();
                if (_textAlign == "center")
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                }
                else if (_textAlign == "left")
                {
                    sf.Alignment = StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Center;
                }
                else if (_textAlign == "right")
                {
                    sf.Alignment = StringAlignment.Far;
                    sf.LineAlignment = StringAlignment.Center;
                }
                sf.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(_text, font, brush, rect, sf);
            }
        }

        /// <summary>深拷贝当前 RenderCommand</summary>
        public RenderCommand Clone()
        {
            RenderCommand c = new RenderCommand();
            c._commandType = _commandType;
            c._x = _x; c._y = _y;
            c._width = _width; c._height = _height;
            c._cornerRadius = _cornerRadius;
            c._fillColor = new XmlColor(_fillColor.ToColor());
            c._strokeColor = new XmlColor(_strokeColor.ToColor());
            c._strokeWidth = _strokeWidth;
            c._text = _text;
            c._textAlign = _textAlign;
            c._fontSize = _fontSize;
            c._isBold = _isBold;
            if (_polygonPoints != null)
            {
                c._polygonPoints = new PointF[_polygonPoints.Length];
                for (int i = 0; i < _polygonPoints.Length; i++)
                    c._polygonPoints[i] = _polygonPoints[i];
            }
            if (_polyHandleTypes != null)
            {
                c._polyHandleTypes = (HandleType[])_polyHandleTypes.Clone();
                c._polyHandleIns = (PointF[])_polyHandleIns.Clone();
                c._polyHandleOuts = (PointF[])_polyHandleOuts.Clone();
            }
            c._useShapeColors = _useShapeColors;
            c._fill = _fill;
            c._stroke = _stroke;
            c._boolOp = _boolOp;
            if (_multiPaths != null)
            {
                c._multiPaths = new List<PointF[]>();
                foreach (PointF[] path in _multiPaths)
                {
                    if (path != null)
                    {
                        PointF[] copy = new PointF[path.Length];
                        for (int i = 0; i < path.Length; i++)
                            copy[i] = path[i];
                        c._multiPaths.Add(copy);
                    }
                }
            }
            if (_pathDefs != null)
            {
                c._pathDefs = new List<PathDef>();
                foreach (PathDef def in _pathDefs)
                    c._pathDefs.Add(def.Clone());
            }
            return c;
        }
    }

    public class ShapeColors
    {
        public Color FillColor = Color.White;
        public Color BorderColor = Color.Black;
        public Color TextColor = Color.Black;
        public Color HeaderColor = Color.Gray;
        public Color FillColorLight = Color.White;
        public Color FillColorDark = Color.White;
        public bool UseGradient = true;
    }
}
