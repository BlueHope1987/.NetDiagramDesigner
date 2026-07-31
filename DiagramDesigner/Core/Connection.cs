using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DiagramDesigner.Core
{
    public enum ConnectionMode
    {
        Straight,
        Curve,
        Orthogonal
    }

    [Serializable]
    public class Connection
    {
        private Guid _id = Guid.NewGuid();
        private ShapeBase _fromShape;
        private ShapeBase _toShape;
        private PointF _fromPoint;
        private PointF _toPoint;
        private ConnectionMode _mode = ConnectionMode.Straight;
        private Color _lineColor = Color.FromArgb(100, 100, 100);
        private float _lineWidth = 1.5f;
        private bool _selected = false;
        private bool _allowIntersection = true;
        private DashStyle _dashStyle = DashStyle.Solid;
        private bool _arrowAtEnd = true;
        private string _label = "";

        [Browsable(false)]
        public Guid Id
        {
            get { return _id; }
            set { _id = value; }
        }

        [Browsable(false)]
        public ShapeBase FromShape
        {
            get { return _fromShape; }
            set { _fromShape = value; }
        }

        [Browsable(false)]
        public ShapeBase ToShape
        {
            get { return _toShape; }
            set { _toShape = value; }
        }

        [Browsable(false)]
        public PointF FromPoint
        {
            get { return _fromPoint; }
            set { _fromPoint = value; }
        }

        [Browsable(false)]
        public PointF ToPoint
        {
            get { return _toPoint; }
            set { _toPoint = value; }
        }

        [Category("外观")]
        [DisplayName("连线模式")]
        public ConnectionMode Mode
        {
            get { return _mode; }
            set { _mode = value; NotifyChanged(); }
        }

        [Category("外观")]
        [DisplayName("线条颜色")]
        public Color LineColor
        {
            get { return _lineColor; }
            set { _lineColor = value; NotifyChanged(); }
        }

        [Category("外观")]
        [DisplayName("线条宽度")]
        public float LineWidth
        {
            get { return _lineWidth; }
            set { _lineWidth = value; NotifyChanged(); }
        }

        [Category("外观")]
        [DisplayName("线型")]
        public DashStyle DashStyle
        {
            get { return _dashStyle; }
            set { _dashStyle = value; NotifyChanged(); }
        }

        [Category("外观")]
        [DisplayName("末端箭头")]
        public bool ArrowAtEnd
        {
            get { return _arrowAtEnd; }
            set { _arrowAtEnd = value; NotifyChanged(); }
        }

        [Category("外观")]
        [DisplayName("标签")]
        public string Label
        {
            get { return _label; }
            set
            {
                if (value == null)
                    _label = "";
                else
                    _label = value;
                NotifyChanged();
            }
        }

        [Browsable(false)]
        public bool Selected
        {
            get { return _selected; }
            set { _selected = value; }
        }

        [Browsable(false)]
        public bool AllowIntersection
        {
            get { return _allowIntersection; }
            set { _allowIntersection = value; }
        }

        public void UpdateEndpoints()
        {
            if (_fromShape != null)
            {
                PointF target = _toShape != null ? _toShape.Center : _toPoint;
                _fromPoint = _fromShape.GetNearestConnectionPoint(target);
            }
            if (_toShape != null)
            {
                PointF source = _fromShape != null ? _fromShape.Center : _fromPoint;
                _toPoint = _toShape.GetNearestConnectionPoint(source);
            }
        }

        public void Draw(Graphics g, float scale)
        {
            UpdateEndpoints();

            PointF[] points = GetDrawPoints();

            Color penColor = _selected ? Color.FromArgb(0, 120, 215) : _lineColor;
            using (Pen pen = new Pen(penColor, _lineWidth / scale))
            {
                pen.DashStyle = _dashStyle;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                if (points.Length >= 2)
                {
                    if (_mode == ConnectionMode.Curve && points.Length >= 4)
                    {
                        using (GraphicsPath path = new GraphicsPath())
                        {
                            path.AddBezier(points[0], points[1], points[2], points[3]);
                            g.DrawPath(pen, path);
                        }
                    }
                    else if (_mode == ConnectionMode.Orthogonal && points.Length >= 4)
                    {
                        g.DrawLines(pen, points);
                    }
                    else
                    {
                        g.DrawLine(pen, points[0], points[points.Length - 1]);
                    }
                }

                if (_arrowAtEnd && points.Length >= 2)
                {
                    DrawArrow(g, points[points.Length - 2], points[points.Length - 1], scale);
                }
            }

            if (_label.Length > 0 && points.Length >= 2)
            {
                PointF mid = GetLabelPosition(points);
                using (Font font = new Font("Microsoft YaHei", 9f / scale))
                using (Brush brush = new SolidBrush(_lineColor))
                {
                    SizeF size = g.MeasureString(_label, font);
                    g.DrawString(_label, font, brush, mid.X - size.Width / 2, mid.Y - size.Height / 2);
                }
            }
        }

        private PointF[] GetDrawPoints()
        {
            if (_mode == ConnectionMode.Straight)
                return new PointF[] { _fromPoint, _toPoint };

            if (_mode == ConnectionMode.Curve)
            {
                PointF mid1 = new PointF((_fromPoint.X + _toPoint.X) / 2f, _fromPoint.Y);
                PointF mid2 = new PointF((_fromPoint.X + _toPoint.X) / 2f, _toPoint.Y);
                return new PointF[] { _fromPoint, mid1, mid2, _toPoint };
            }

            if (_mode == ConnectionMode.Orthogonal)
            {
                float midX = (_fromPoint.X + _toPoint.X) / 2f;
                return new PointF[] { _fromPoint, new PointF(midX, _fromPoint.Y), new PointF(midX, _toPoint.Y), _toPoint };
            }

            return new PointF[] { _fromPoint, _toPoint };
        }

        private PointF GetLabelPosition(PointF[] points)
        {
            if (_mode == ConnectionMode.Straight)
                return new PointF((points[0].X + points[1].X) / 2f, (points[0].Y + points[1].Y) / 2f);

            if (points.Length >= 4)
                return new PointF((points[1].X + points[2].X) / 2f, (points[1].Y + points[2].Y) / 2f);

            return points[0];
        }

        private void DrawArrow(Graphics g, PointF from, PointF to, float scale)
        {
            float arrowLength = 10f / scale;
            float arrowAngle = 0.5f;

            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float angle = (float)Math.Atan2(dy, dx);

            PointF p1 = new PointF(
                to.X - arrowLength * (float)Math.Cos(angle - arrowAngle),
                to.Y - arrowLength * (float)Math.Sin(angle - arrowAngle));
            PointF p2 = new PointF(
                to.X - arrowLength * (float)Math.Cos(angle + arrowAngle),
                to.Y - arrowLength * (float)Math.Sin(angle + arrowAngle));

            Color arrowColor = _selected ? Color.FromArgb(0, 120, 215) : _lineColor;
            using (Brush brush = new SolidBrush(arrowColor))
            {
                g.FillPolygon(brush, new PointF[] { to, p1, p2 });
            }
        }

        public bool HitTest(PointF pt, float tolerance)
        {
            PointF[] points = GetDrawPoints();
            if (points.Length < 2)
                return false;

            for (int i = 0; i < points.Length - 1; i++)
            {
                if (DistanceToSegment(pt, points[i], points[i + 1]) < tolerance)
                    return true;
            }
            return false;
        }

        private float DistanceToSegment(PointF p, PointF a, PointF b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float len2 = dx * dx + dy * dy;

            if (len2 < 0.0001f)
                return Distance(p, a);

            float t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2));
            float projX = a.X + t * dx;
            float projY = a.Y + t * dy;
            return Distance(p, new PointF(projX, projY));
        }

        private float Distance(PointF a, PointF b)
        {
            return (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        }

        public event EventHandler Changed;

        private void NotifyChanged()
        {
            if (Changed != null)
                Changed(this, EventArgs.Empty);
        }
    }
}
