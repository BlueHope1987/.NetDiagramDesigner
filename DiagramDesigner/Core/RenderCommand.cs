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
        /// 绘制复合多路径图形。支持逐路径布尔运算（邻居模式）。
        /// 每条路径的 BoolOp 仅与紧邻的下层路径计算。
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
                if (def.Points == null || def.Points.Length < 3 || !def.Visible)
                {
                    absPaths.Add(null); // 占位，保持索引对齐
                    continue;
                }
                PointF[] pts = new PointF[def.Points.Length];
                for (int i = 0; i < def.Points.Length; i++)
                {
                    pts[i] = new PointF(
                        rect.X + def.Points[i].X * rect.Width,
                        rect.Y + def.Points[i].Y * rect.Height);
                }
                GraphicsPath gp = new GraphicsPath();
                gp.AddPolygon(pts);
                absPaths.Add(gp);
            }

            // 邻居模式布尔运算：每条路径的 BoolOp 仅与紧邻下层路径计算
            // renderItems: 每项含 (路径索引, Region)
            List<Region> renderRegions = new List<Region>();
            List<int> regionPathIndices = new List<int>();

            for (int i = 0; i < absPaths.Count; i++)
            {
                if (absPaths[i] == null)
                    continue;

                BooleanOperation op = (i == 0) ? BooleanOperation.None : defs[i].BoolOp;

                if (op == BooleanOperation.None || op == BooleanOperation.Union)
                {
                    // 独立路径：添加为新的渲染区域
                    renderRegions.Add(new Region(absPaths[i]));
                    regionPathIndices.Add(i);
                }
                else
                {
                    // Subtract/Intersect/Xor：查找紧邻下层路径的渲染区域
                    int targetIdx = -1;
                    for (int r = regionPathIndices.Count - 1; r >= 0; r--)
                    {
                        if (regionPathIndices[r] == i - 1)
                        {
                            targetIdx = r;
                            break;
                        }
                    }

                    if (targetIdx >= 0)
                    {
                        // 对紧邻下层路径的区域应用布尔运算
                        Region target = renderRegions[targetIdx];
                        switch (op)
                        {
                            case BooleanOperation.Subtract:
                                target.Exclude(absPaths[i]);
                                break;
                            case BooleanOperation.Intersect:
                                target.Intersect(absPaths[i]);
                                break;
                            case BooleanOperation.Xor:
                                target.Xor(absPaths[i]);
                                break;
                        }
                    }
                    else
                    {
                        // 紧邻下层路径无渲染区域（被消费或为空），
                        // 按独立路径处理
                        renderRegions.Add(new Region(absPaths[i]));
                        regionPathIndices.Add(i);
                    }
                }
            }

            // 填充所有渲染区域
            if (_fill)
            {
                using (Brush brush = CreateFillBrush(rect, colors))
                {
                    foreach (Region r in renderRegions)
                        g.FillRegion(brush, r);
                }
            }

            // 描边：绘制所有原始路径的轮廓
            if (_stroke)
            {
                using (Pen pen = new Pen(stroke, _strokeWidth / scale))
                {
                    foreach (GraphicsPath gp in absPaths)
                    {
                        if (gp != null)
                            g.DrawPath(pen, gp);
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
            List<PathDef> defs = new List<PathDef>();
            for (int i = 0; i < _multiPaths.Count; i++)
            {
                BooleanOperation op = (i == 0) ? BooleanOperation.None : _boolOp;
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
