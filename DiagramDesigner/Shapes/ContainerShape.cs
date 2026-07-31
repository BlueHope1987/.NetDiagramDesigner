using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using DiagramDesigner.Core;

namespace DiagramDesigner.Shapes
{
    [Serializable]
    public class ContainerShape : ShapeBase
    {
        private List<ShapeBase> _children = new List<ShapeBase>();
        private float _headerHeight = 30f;
        private Color _headerColor = Color.FromArgb(80, 130, 180);
        private string _headerText = "容器";

        public ContainerShape()
        {
            Name = "容器";
            IsContainer = true;
            Resizable = true;
            MinWidth = 120f;
            MinHeight = 80f;
            Bounds = new RectangleF(0, 0, 300, 220);
            FillColor = Color.FromArgb(250, 250, 250);
            BorderColor = Color.FromArgb(100, 100, 120);
            HeaderColor = Color.FromArgb(80, 130, 180);
        }

        [Category("外观")]
        [DisplayName("标题栏高度")]
        public float HeaderHeight
        {
            get { return _headerHeight; }
            set { _headerHeight = value; NotifyChanged(); }
        }

        [Category("外观")]
        [DisplayName("标题栏颜色")]
        public Color HeaderColor
        {
            get { return _headerColor; }
            set { _headerColor = value; NotifyChanged(); }
        }

        [Category("外观")]
        [DisplayName("标题文字")]
        public string HeaderText
        {
            get { return _headerText; }
            set
            {
                if (value == null)
                    _headerText = "";
                else
                    _headerText = value;
                NotifyChanged();
            }
        }

        public List<ShapeBase> Children { get { return _children; } }

        public void AddChild(ShapeBase child)
        {
            if (child == null || child == this)
                return;
            if (!_children.Contains(child))
            {
                _children.Add(child);
                child.Parent = this;
                NotifyChanged();
            }
        }

        public void RemoveChild(ShapeBase child)
        {
            if (child == null)
                return;
            _children.Remove(child);
            child.Parent = null;
            NotifyChanged();
        }

        public override void Move(float dx, float dy)
        {
            base.Move(dx, dy);
            foreach (ShapeBase child in _children)
            {
                child.Move(dx, dy);
            }
        }

        public override bool HitTest(PointF pt)
        {
            if (!Visible)
                return false;
            return Bounds.Contains(pt);
        }

        public bool HitTestHeader(PointF pt)
        {
            RectangleF header = Bounds;
            header.Height = _headerHeight;
            return header.Contains(pt);
        }

        public override void Draw(Graphics g, float scale)
        {
            RectangleF rect = Bounds;
            RectangleF headerRect = rect;
            headerRect.Height = _headerHeight;
            RectangleF bodyRect = rect;
            bodyRect.Y += _headerHeight;
            bodyRect.Height -= _headerHeight;

            using (GraphicsPath path = GraphicsUtility.CreateRoundedRectPath(rect, 6f))
            {
                using (Brush brush = ShapeBase.CreateGradientBrush(rect, FillColor))
                {
                    g.FillPath(brush, path);
                }
            }

            using (Brush headerBrush = new SolidBrush(_headerColor))
            {
                GraphicsPath headerPath = new GraphicsPath();
                float r = 6f;
                headerPath.AddArc(headerRect.X, headerRect.Y, r * 2, r * 2, 180, 90);
                headerPath.AddArc(headerRect.Right - r * 2, headerRect.Y, r * 2, r * 2, 270, 90);
                headerPath.AddLine(headerRect.Right, headerRect.Bottom, headerRect.X, headerRect.Bottom);
                headerPath.CloseFigure();
                g.FillPath(headerBrush, headerPath);
                headerPath.Dispose();
            }

            using (Pen pen = new Pen(Selected ? Color.FromArgb(0, 120, 215) : BorderColor, BorderWidth / scale))
            {
                GraphicsUtility.DrawRoundedRectOutline(g, pen, rect, 6f);
                g.DrawLine(pen, rect.X, rect.Y + _headerHeight, rect.Right, rect.Y + _headerHeight);
            }

            DrawHeaderText(g, scale);

            GraphicsState state = g.Save();
            g.Clip = new Region(bodyRect);

            List<ShapeBase> sortedChildren = new List<ShapeBase>(_children);
            sortedChildren.Sort(new ZOrderComparer());
            foreach (ShapeBase child in sortedChildren)
            {
                if (child.Visible)
                    child.Draw(g, scale);
            }

            g.Restore(state);
            DrawSelection(g, scale);
        }

        private class ZOrderComparer : IComparer<ShapeBase>
        {
            public int Compare(ShapeBase a, ShapeBase b)
            {
                return a.ZOrder.CompareTo(b.ZOrder);
            }
        }

        private void DrawHeaderText(Graphics g, float scale)
        {
            using (Font font = new Font("Microsoft YaHei", 10f / scale, FontStyle.Bold))
            using (Brush brush = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Near;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;

                RectangleF textRect = Bounds;
                textRect.Height = _headerHeight;
                textRect.Inflate(-8 / scale, 0);
                g.DrawString(_headerText, font, brush, textRect, sf);
            }
        }

        public override ShapeBase Clone()
        {
            ContainerShape clone = new ContainerShape();
            clone.Id = Guid.NewGuid();
            clone.Name = this.Name;
            clone.Description = this.Description;
            clone.Bounds = this.Bounds;
            clone.FillColor = this.FillColor;
            clone.BorderColor = this.BorderColor;
            clone.TextColor = this.TextColor;
            clone.BorderWidth = this.BorderWidth;
            clone.HeaderHeight = this.HeaderHeight;
            clone.HeaderColor = this.HeaderColor;
            clone.HeaderText = this.HeaderText;
            return clone;
        }
    }
}
