using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DiagramDesigner.Core
{
    public enum ResizeHandle
    {
        None,
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    [Serializable]
    public abstract class ShapeBase
    {
        private Guid _id = Guid.NewGuid();
        private RectangleF _bounds = new RectangleF(0, 0, 120, 80);
        private string _name = "Shape";
        private string _description = "";
        private Color _fillColor = Color.FromArgb(230, 240, 255);
        private Color _borderColor = Color.FromArgb(80, 120, 180);
        private Color _textColor = Color.FromArgb(40, 40, 40);
        private float _borderWidth = 1.5f;
        private bool _selected = false;
        private bool _hovered = false;
        private bool _isContainer = false;
        private ShapeBase _parent = null;
        private int _zOrder = 0;
        private bool _visible = true;
        private bool _resizable = false;
        private float _minWidth = 30f;
        private float _minHeight = 30f;

        [Browsable(false)]
        public Guid Id
        {
            get { return _id; }
            set { _id = value; }
        }

        [Category("外观")]
        [DisplayName("名称")]
        [Description("实体的显示名称")]
        public string Name
        {
            get { return _name; }
            set
            {
                if (value == null)
                    _name = "";
                else
                    _name = value;
                NotifyChanged();
            }
        }

        [Category("外观")]
        [DisplayName("描述")]
        [Description("实体的详细描述")]
        public string Description
        {
            get { return _description; }
            set
            {
                if (value == null)
                    _description = "";
                else
                    _description = value;
                NotifyChanged();
            }
        }

        [Browsable(false)]
        public RectangleF Bounds
        {
            get { return _bounds; }
            set { _bounds = value; NotifyChanged(); }
        }

        [Category("布局")]
        [DisplayName("X")]
        public float X
        {
            get { return _bounds.X; }
            set { _bounds.X = value; NotifyChanged(); }
        }

        [Category("布局")]
        [DisplayName("Y")]
        public float Y
        {
            get { return _bounds.Y; }
            set { _bounds.Y = value; NotifyChanged(); }
        }

        [Category("布局")]
        [DisplayName("宽度")]
        public float Width
        {
            get { return _bounds.Width; }
            set
            {
                if (value > 10)
                {
                    _bounds.Width = value;
                    NotifyChanged();
                }
            }
        }

        [Category("布局")]
        [DisplayName("高度")]
        public float Height
        {
            get { return _bounds.Height; }
            set
            {
                if (value > 10)
                {
                    _bounds.Height = value;
                    NotifyChanged();
                }
            }
        }

        [Category("外观")]
        [DisplayName("填充颜色")]
        public Color FillColor
        {
            get { return _fillColor; }
            set { _fillColor = value; NotifyChanged(); }
        }

        [Category("外观")]
        [DisplayName("边框颜色")]
        public Color BorderColor
        {
            get { return _borderColor; }
            set { _borderColor = value; NotifyChanged(); }
        }

        [Category("外观")]
        [DisplayName("文字颜色")]
        public Color TextColor
        {
            get { return _textColor; }
            set { _textColor = value; NotifyChanged(); }
        }

        [Category("外观")]
        [DisplayName("边框宽度")]
        public float BorderWidth
        {
            get { return _borderWidth; }
            set { _borderWidth = value; NotifyChanged(); }
        }

        [Browsable(false)]
        public bool Selected
        {
            get { return _selected; }
            set { _selected = value; NotifyChanged(); }
        }

        [Browsable(false)]
        public bool Hovered
        {
            get { return _hovered; }
            set { _hovered = value; }
        }

        [Browsable(false)]
        public bool IsContainer
        {
            get { return _isContainer; }
            set { _isContainer = value; }
        }

        [Browsable(false)]
        public ShapeBase Parent
        {
            get { return _parent; }
            set { _parent = value; }
        }

        [Browsable(false)]
        public int ZOrder
        {
            get { return _zOrder; }
            set { _zOrder = value; }
        }

        [Browsable(false)]
        public bool Visible
        {
            get { return _visible; }
            set { _visible = value; }
        }

        [Category("行为")]
        [DisplayName("可调整大小")]
        [Description("是否允许通过拖拽手柄调整尺寸")]
        public bool Resizable
        {
            get { return _resizable; }
            set { _resizable = value; }
        }

        [Category("行为")]
        [DisplayName("最小宽度")]
        [Description("调整大小时的最小宽度限制")]
        public float MinWidth
        {
            get { return _minWidth; }
            set
            {
                if (value > 0)
                    _minWidth = value;
            }
        }

        [Category("行为")]
        [DisplayName("最小高度")]
        [Description("调整大小时的最小高度限制")]
        public float MinHeight
        {
            get { return _minHeight; }
            set
            {
                if (value > 0)
                    _minHeight = value;
            }
        }

        [Browsable(false)]
        public PointF Center
        {
            get { return new PointF(_bounds.X + _bounds.Width / 2f, _bounds.Y + _bounds.Height / 2f); }
        }

        public virtual void Move(float dx, float dy)
        {
            _bounds.X += dx;
            _bounds.Y += dy;
            NotifyChanged();
        }

        public virtual bool HitTest(PointF pt)
        {
            if (!Visible)
                return false;
            return _bounds.Contains(pt);
        }

        public virtual bool HitTest(RectangleF rect)
        {
            if (!Visible)
                return false;
            return _bounds.IntersectsWith(rect);
        }

        public virtual PointF GetNearestConnectionPoint(PointF from)
        {
            PointF center = Center;
            float halfW = _bounds.Width / 2f;
            float halfH = _bounds.Height / 2f;

            float dx = from.X - center.X;
            float dy = from.Y - center.Y;

            if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
                return new PointF(center.X + halfW, center.Y);

            float absDx = Math.Abs(dx);
            float absDy = Math.Abs(dy);
            float scale;

            if (absDx * halfH > absDy * halfW)
                scale = halfW / absDx;
            else
                scale = halfH / absDy;

            return new PointF(center.X + dx * scale, center.Y + dy * scale);
        }

        public ResizeHandle HitTestResizeHandle(PointF pt, float tolerance)
        {
            if (!_resizable || !_selected)
                return ResizeHandle.None;

            float halfT = tolerance / 2f;
            float t = tolerance + 2f;

            PointF tl = new PointF(_bounds.X, _bounds.Y);
            PointF tc = new PointF(_bounds.X + _bounds.Width / 2f, _bounds.Y);
            PointF tr = new PointF(_bounds.Right, _bounds.Y);
            PointF ml = new PointF(_bounds.X, _bounds.Y + _bounds.Height / 2f);
            PointF mr = new PointF(_bounds.Right, _bounds.Y + _bounds.Height / 2f);
            PointF bl = new PointF(_bounds.X, _bounds.Bottom);
            PointF bc = new PointF(_bounds.X + _bounds.Width / 2f, _bounds.Bottom);
            PointF br = new PointF(_bounds.Right, _bounds.Bottom);

            float dx = Math.Abs(pt.X - tl.X);
            float dy = Math.Abs(pt.Y - tl.Y);
            if (dx <= t && dy <= t) return ResizeHandle.TopLeft;

            dx = Math.Abs(pt.X - tc.X);
            dy = Math.Abs(pt.Y - tc.Y);
            if (dx <= t && dy <= t) return ResizeHandle.TopCenter;

            dx = Math.Abs(pt.X - tr.X);
            dy = Math.Abs(pt.Y - tr.Y);
            if (dx <= t && dy <= t) return ResizeHandle.TopRight;

            dx = Math.Abs(pt.X - ml.X);
            dy = Math.Abs(pt.Y - ml.Y);
            if (dx <= t && dy <= t) return ResizeHandle.MiddleLeft;

            dx = Math.Abs(pt.X - mr.X);
            dy = Math.Abs(pt.Y - mr.Y);
            if (dx <= t && dy <= t) return ResizeHandle.MiddleRight;

            dx = Math.Abs(pt.X - bl.X);
            dy = Math.Abs(pt.Y - bl.Y);
            if (dx <= t && dy <= t) return ResizeHandle.BottomLeft;

            dx = Math.Abs(pt.X - bc.X);
            dy = Math.Abs(pt.Y - bc.Y);
            if (dx <= t && dy <= t) return ResizeHandle.BottomCenter;

            dx = Math.Abs(pt.X - br.X);
            dy = Math.Abs(pt.Y - br.Y);
            if (dx <= t && dy <= t) return ResizeHandle.BottomRight;

            if (pt.X >= _bounds.X - halfT && pt.X <= _bounds.X + halfT &&
                pt.Y >= _bounds.Y && pt.Y <= _bounds.Bottom)
                return ResizeHandle.MiddleLeft;

            if (pt.X >= _bounds.Right - halfT && pt.X <= _bounds.Right + halfT &&
                pt.Y >= _bounds.Y && pt.Y <= _bounds.Bottom)
                return ResizeHandle.MiddleRight;

            if (pt.Y >= _bounds.Y - halfT && pt.Y <= _bounds.Y + halfT &&
                pt.X >= _bounds.X && pt.X <= _bounds.Right)
                return ResizeHandle.TopCenter;

            if (pt.Y >= _bounds.Bottom - halfT && pt.Y <= _bounds.Bottom + halfT &&
                pt.X >= _bounds.X && pt.X <= _bounds.Right)
                return ResizeHandle.BottomCenter;

            return ResizeHandle.None;
        }

        public static Cursor GetResizeCursor(ResizeHandle handle)
        {
            if (handle == ResizeHandle.TopLeft || handle == ResizeHandle.BottomRight)
                return Cursors.SizeNWSE;
            if (handle == ResizeHandle.TopRight || handle == ResizeHandle.BottomLeft)
                return Cursors.SizeNESW;
            if (handle == ResizeHandle.TopCenter || handle == ResizeHandle.BottomCenter)
                return Cursors.SizeNS;
            if (handle == ResizeHandle.MiddleLeft || handle == ResizeHandle.MiddleRight)
                return Cursors.SizeWE;
            return Cursors.Default;
        }

        public abstract void Draw(Graphics g, float scale);
        public abstract ShapeBase Clone();

        public event EventHandler Changed;

        protected void NotifyChanged()
        {
            if (Changed != null)
                Changed(this, EventArgs.Empty);
        }

        protected virtual void DrawSelection(Graphics g, float scale)
        {
            if (!Selected && !Hovered)
                return;

            RectangleF rect = _bounds;
            float offset = 4f / scale;
            rect.Inflate(offset, offset);

            Color penColor = Selected ? Color.FromArgb(0, 120, 215) : Color.Gray;
            using (Pen pen = new Pen(penColor))
            {
                pen.DashStyle = DashStyle.Dash;
                pen.Width = 1f / scale;
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            if (Selected)
            {
                float handleSize = 6f / scale;
                using (Brush brush = new SolidBrush(Color.FromArgb(0, 120, 215)))
                {
                    if (_resizable)
                    {
                        DrawHandle(g, brush, rect.Left, rect.Top, handleSize);
                        DrawHandle(g, brush, rect.X + rect.Width / 2f, rect.Top, handleSize);
                        DrawHandle(g, brush, rect.Right, rect.Top, handleSize);
                        DrawHandle(g, brush, rect.Left, rect.Y + rect.Height / 2f, handleSize);
                        DrawHandle(g, brush, rect.Right, rect.Y + rect.Height / 2f, handleSize);
                        DrawHandle(g, brush, rect.Left, rect.Bottom, handleSize);
                        DrawHandle(g, brush, rect.X + rect.Width / 2f, rect.Bottom, handleSize);
                        DrawHandle(g, brush, rect.Right, rect.Bottom, handleSize);
                    }
                    else
                    {
                        DrawHandle(g, brush, rect.Left, rect.Top, handleSize);
                        DrawHandle(g, brush, rect.Right, rect.Top, handleSize);
                        DrawHandle(g, brush, rect.Left, rect.Bottom, handleSize);
                        DrawHandle(g, brush, rect.Right, rect.Bottom, handleSize);
                    }
                }
            }
        }

        private void DrawHandle(Graphics g, Brush brush, float x, float y, float size)
        {
            g.FillRectangle(brush, x - size / 2f, y - size / 2f, size, size);
        }

        // ===== 渐变填充辅助方法 =====

        public static Color LightenColor(Color c, float factor)
        {
            return Color.FromArgb(
                c.A,
                Math.Min(255, (int)(c.R + (255 - c.R) * factor)),
                Math.Min(255, (int)(c.G + (255 - c.G) * factor)),
                Math.Min(255, (int)(c.B + (255 - c.B) * factor)));
        }

        public static Color DarkenColor(Color c, float factor)
        {
            return Color.FromArgb(
                c.A,
                (int)(c.R * (1f - factor)),
                (int)(c.G * (1f - factor)),
                (int)(c.B * (1f - factor)));
        }

        public static Brush CreateGradientBrush(RectangleF rect, Color baseColor)
        {
            float brightness = baseColor.R * 0.299f + baseColor.G * 0.587f + baseColor.B * 0.114f;
            Color darkColor, lightColor;

            if (brightness > 220f)
            {
                darkColor = DarkenColor(baseColor, 0.06f);
                lightColor = LightenColor(baseColor, 0.12f);
            }
            else if (brightness < 60f)
            {
                darkColor = LightenColor(baseColor, 0.12f);
                lightColor = LightenColor(baseColor, 0.20f);
            }
            else
            {
                darkColor = DarkenColor(baseColor, 0.06f);
                lightColor = LightenColor(baseColor, 0.12f);
            }

            return new LinearGradientBrush(
                new PointF(rect.X, rect.Y),
                new PointF(rect.Right, rect.Bottom),
                darkColor,
                lightColor);
        }
    }
}
