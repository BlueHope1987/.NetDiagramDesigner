using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using DiagramDesigner.Core;

namespace DiagramDesigner.Shapes
{
    [Serializable]
    public class GenericShape : ShapeBase
    {
        private string _shapeTypeName = "";
        private List<ShapeMember> _members = new List<ShapeMember>();
        private List<ShapeState> _states = new List<ShapeState>();
        private string _currentStateName = "Normal";
        private float _memberAreaTop = 0.35f;
        private float _memberLineHeight = 16f;

        public string ShapeTypeName
        {
            get { return _shapeTypeName; }
            set { _shapeTypeName = value; NotifyChanged(); }
        }

        public List<ShapeMember> Members
        {
            get { return _members; }
            set { _members = value; NotifyChanged(); }
        }

        public List<ShapeState> States
        {
            get { return _states; }
            set { _states = value; NotifyChanged(); }
        }

        public string CurrentStateName
        {
            get { return _currentStateName; }
            set
            {
                _currentStateName = value;
                ApplyCurrentState();
                NotifyChanged();
            }
        }

        public float MemberAreaTop
        {
            get { return _memberAreaTop; }
            set { _memberAreaTop = value; NotifyChanged(); }
        }

        public GenericShape()
        {
            Name = "通用图形";
            Bounds = new RectangleF(0, 0, 140, 100);
            FillColor = Color.FromArgb(230, 245, 255);
            BorderColor = Color.FromArgb(60, 130, 200);
        }

        public void AddState(ShapeState state)
        {
            if (state == null)
                return;
            _states.Add(state);
        }

        public void RemoveState(string stateName)
        {
            for (int i = _states.Count - 1; i >= 0; i--)
            {
                if (_states[i].Name == stateName)
                {
                    _states.RemoveAt(i);
                    break;
                }
            }
        }

        public ShapeState GetCurrentState()
        {
            foreach (ShapeState state in _states)
            {
                if (state.Name == _currentStateName)
                    return state;
            }
            return null;
        }

        private void ApplyCurrentState()
        {
            ShapeState state = GetCurrentState();
            if (state != null)
            {
                FillColor = state.FillColor.ToColor();
                BorderColor = state.BorderColor.ToColor();
                TextColor = state.TextColor.ToColor();
            }
        }

        public override void Draw(Graphics g, float scale)
        {
            ShapeType type = ShapeTypeRegistry.Instance.GetShapeType(_shapeTypeName);
            ShapeColors colors = ComputeGradientColors();

            // 优先使用当前状态的自定义绘制指令
            List<RenderCommand> commands = null;
            ShapeState currentState = GetCurrentState();
            if (currentState != null && currentState.UseCustomRenderCommands
                && currentState.CustomRenderCommands != null
                && currentState.CustomRenderCommands.Count > 0)
            {
                commands = currentState.CustomRenderCommands;
            }
            else if (type != null)
            {
                commands = type.RenderCommands;
            }

            if (commands != null && commands.Count > 0)
            {
                foreach (RenderCommand cmd in commands)
                {
                    cmd.Execute(g, Bounds, colors, scale);
                }
            }
            else
            {
                DrawFallback(g, colors, scale);
            }

            DrawName(g, scale);
            DrawMembers(g, scale);
            DrawSelection(g, scale);
        }

        private ShapeColors ComputeGradientColors()
        {
            ShapeColors colors = new ShapeColors();
            colors.FillColor = FillColor;
            colors.BorderColor = BorderColor;
            colors.TextColor = TextColor;
            colors.UseGradient = true;

            float brightness = FillColor.R * 0.299f + FillColor.G * 0.587f + FillColor.B * 0.114f;

            if (brightness > 220f)
            {
                colors.FillColorDark = ShapeBase.DarkenColor(FillColor, 0.06f);
                colors.FillColorLight = ShapeBase.LightenColor(FillColor, 0.12f);
            }
            else if (brightness < 60f)
            {
                colors.FillColorDark = ShapeBase.LightenColor(FillColor, 0.12f);
                colors.FillColorLight = ShapeBase.LightenColor(FillColor, 0.20f);
            }
            else
            {
                colors.FillColorDark = ShapeBase.DarkenColor(FillColor, 0.06f);
                colors.FillColorLight = ShapeBase.LightenColor(FillColor, 0.12f);
            }

            return colors;
        }

        private void DrawFallback(Graphics g, ShapeColors colors, float scale)
        {
            using (Brush brush = ShapeBase.CreateGradientBrush(Bounds, colors.FillColor))
            {
                g.FillRectangle(brush, Bounds);
            }
            using (Pen pen = new Pen(Selected ? Color.FromArgb(0, 120, 215) : colors.BorderColor, BorderWidth / scale))
            {
                g.DrawRectangle(pen, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
            }
        }

        private void DrawName(Graphics g, float scale)
        {
            if (string.IsNullOrEmpty(Name))
                return;

            ShapeType type = ShapeTypeRegistry.Instance.GetShapeType(_shapeTypeName);

            using (Font font = new Font("Microsoft YaHei", 10f / scale, FontStyle.Regular))
            using (Brush brush = new SolidBrush(TextColor))
            {
                StringFormat sf = new StringFormat();
                sf.Trimming = StringTrimming.EllipsisCharacter;

                RectangleF textRect = Bounds;
                textRect.Inflate(-6 / scale, -6 / scale);

                bool hasMembers = (_members != null && _members.Count > 0);

                if (hasMembers)
                {
                    textRect.Height = Bounds.Height * _memberAreaTop;
                }

                // 根据 ShapeType 的名称对齐方式调整
                NameAlignment alignment = NameAlignment.Center;
                if (type != null)
                    alignment = type.NameAlignment;

                switch (alignment)
                {
                    case NameAlignment.TopLeft:
                        sf.Alignment = StringAlignment.Near;
                        sf.LineAlignment = StringAlignment.Near;
                        break;

                    case NameAlignment.TopCenter:
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = hasMembers ? StringAlignment.Center : StringAlignment.Near;
                        break;

                    case NameAlignment.Center:
                    default:
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        break;
                }

                g.DrawString(Name, font, brush, textRect, sf);
            }
        }

        private void DrawMembers(Graphics g, float scale)
        {
            if (_members == null || _members.Count == 0)
                return;

            float top = Bounds.Y + Bounds.Height * _memberAreaTop;
            float left = Bounds.X + 4 / scale;
            float width = Bounds.Width - 8 / scale;
            float lineHeight = _memberLineHeight / scale;

            using (Font font = new Font("Microsoft YaHei", 8f / scale, FontStyle.Regular))
            using (Brush brush = new SolidBrush(TextColor))
            {
                for (int i = 0; i < _members.Count; i++)
                {
                    float y = top + i * lineHeight;
                    if (y + lineHeight > Bounds.Bottom - 2 / scale)
                        break;

                    string sig = _members[i].GetSignature();
                    g.DrawString(sig, font, brush, left, y);
                }
            }

            using (Pen pen = new Pen(Color.FromArgb(200, 200, 200), 0.5f / scale))
            {
                g.DrawLine(pen, Bounds.X, top, Bounds.Right, top);
            }
        }

        public override PointF GetNearestConnectionPoint(PointF from)
        {
            ShapeType type = ShapeTypeRegistry.Instance.GetShapeType(_shapeTypeName);
            if (type != null && type.RenderCommands.Count > 0)
            {
                RenderCommand cmd = type.RenderCommands[0];
                if (cmd.CommandType == RenderCommandType.Ellipse)
                {
                    return GetEllipseConnectionPoint(from);
                }
                else if (cmd.CommandType == RenderCommandType.Polygon)
                {
                    return GetPolygonConnectionPoint(from, cmd);
                }
            }
            return base.GetNearestConnectionPoint(from);
        }

        private PointF GetEllipseConnectionPoint(PointF from)
        {
            PointF center = Center;
            float rx = Bounds.Width / 2f;
            float ry = Bounds.Height / 2f;
            float dx = from.X - center.X;
            float dy = from.Y - center.Y;

            if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
                return new PointF(center.X + rx, center.Y);

            float angle = (float)Math.Atan2(dy, dx);
            return new PointF(center.X + rx * (float)Math.Cos(angle), center.Y + ry * (float)Math.Sin(angle));
        }

        private PointF GetPolygonConnectionPoint(PointF from, RenderCommand cmd)
        {
            return base.GetNearestConnectionPoint(from);
        }

        public override ShapeBase Clone()
        {
            GenericShape clone = new GenericShape();
            clone.Id = Guid.NewGuid();
            clone.ShapeTypeName = this.ShapeTypeName;
            clone.Name = this.Name;
            clone.Description = this.Description;
            clone.Bounds = this.Bounds;
            clone.FillColor = this.FillColor;
            clone.BorderColor = this.BorderColor;
            clone.TextColor = this.TextColor;
            clone.BorderWidth = this.BorderWidth;
            clone.CurrentStateName = this.CurrentStateName;
            clone.MemberAreaTop = this.MemberAreaTop;

            foreach (ShapeMember m in this.Members)
            {
                ShapeMember cm = new ShapeMember();
                cm.MemberType = m.MemberType;
                cm.Name = m.Name;
                cm.TypeName = m.TypeName;
                cm.Visibility = m.Visibility;
                cm.IsStatic = m.IsStatic;
                cm.IsAbstract = m.IsAbstract;
                cm.DefaultValue = m.DefaultValue;
                foreach (ShapeMemberParameter p in m.Parameters)
                {
                    ShapeMemberParameter cp = new ShapeMemberParameter();
                    cp.Name = p.Name;
                    cp.TypeName = p.TypeName;
                    cp.DefaultValue = p.DefaultValue;
                    cm.Parameters.Add(cp);
                }
                clone.Members.Add(cm);
            }

            foreach (ShapeState s in this.States)
            {
                ShapeState cs = new ShapeState();
                cs.Name = s.Name;
                cs.FillColor = new XmlColor(s.FillColor.ToColor());
                cs.BorderColor = new XmlColor(s.BorderColor.ToColor());
                cs.TextColor = new XmlColor(s.TextColor.ToColor());
                cs.HeaderColor = new XmlColor(s.HeaderColor.ToColor());
                cs.Priority = s.Priority;
                cs.UseCustomRenderCommands = s.UseCustomRenderCommands;
                if (s.CustomRenderCommands != null)
                {
                    foreach (RenderCommand rc in s.CustomRenderCommands)
                    {
                        RenderCommand rcCopy = new RenderCommand();
                        rcCopy.CommandType = rc.CommandType;
                        rcCopy.X = rc.X; rcCopy.Y = rc.Y;
                        rcCopy.Width = rc.Width; rcCopy.Height = rc.Height;
                        rcCopy.CornerRadius = rc.CornerRadius;
                        rcCopy.FillColor = new XmlColor(rc.FillColor.ToColor());
                        rcCopy.StrokeColor = new XmlColor(rc.StrokeColor.ToColor());
                        rcCopy.StrokeWidth = rc.StrokeWidth;
                        rcCopy.Text = rc.Text;
                        rcCopy.TextAlign = rc.TextAlign;
                        rcCopy.FontSize = rc.FontSize;
                        rcCopy.IsBold = rc.IsBold;
                        if (rc.PolygonPoints != null)
                        {
                            rcCopy.PolygonPoints = new PointF[rc.PolygonPoints.Length];
                            for (int i = 0; i < rc.PolygonPoints.Length; i++)
                                rcCopy.PolygonPoints[i] = rc.PolygonPoints[i];
                        }
                        rcCopy.UseShapeColors = rc.UseShapeColors;
                        rcCopy.Fill = rc.Fill;
                        rcCopy.Stroke = rc.Stroke;
                        cs.CustomRenderCommands.Add(rcCopy);
                    }
                }
                clone.States.Add(cs);
            }

            return clone;
        }
    }
}
