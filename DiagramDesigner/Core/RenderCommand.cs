using System;
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
        MemberArea
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
