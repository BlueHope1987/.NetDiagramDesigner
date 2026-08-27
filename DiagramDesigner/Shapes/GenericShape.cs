using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using DiagramDesigner.Core;

namespace DiagramDesigner.Shapes
{
    /// <summary>
    /// 通用图形类。支持 ShapeType 定义的渲染指令、多状态切换、
    /// 成员列表以及 Zone 分区布局。
    /// 每个 GenericShape 实例从 ShapeType 复制一份 Zone 定义，
    /// 并可在运行时通过状态的 CustomZones 进行覆盖。
    /// </summary>
    [Serializable]
    public class GenericShape : ShapeBase
    {
        /// <summary>
        /// 全局标志：主画布是否处于设计模式。
        /// 由 DrawingCanvas 在绘制前设置：设计模式 = true，运行模式 = false。
        /// Zone 的实际显示由 Visibility 属性控制：
        ///   设计模式下显示 DesignTime/RunTime，运行模式下仅显示 RunTime。
        /// 图形编辑器中 Zone 始终可见，不受此标志影响。
        /// </summary>
        public static bool RenderZoneVisuals = true;

        private string _shapeTypeName = "";
        private List<ShapeMember> _members = new List<ShapeMember>();
        private List<ShapeState> _states = new List<ShapeState>();
        private string _currentStateName = "Normal";
        private float _memberAreaTop = 0.35f;
        private float _memberLineHeight = 16f;
        private List<ShapeZone> _zones = new List<ShapeZone>();
        private float _refWidth = 140f;
        private float _refHeight = 100f;
        private List<ShapeAction> _systemActions = new List<ShapeAction>();

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

        /// <summary>成员行高（像素），用于成员渲染和命中测试</summary>
        public float MemberLineHeight
        {
            get { return _memberLineHeight; }
            set { _memberLineHeight = value; }
        }

        /// <summary>
        /// 该图形实例的 Zone 列表。从 ShapeType 复制而来，
        /// 可在运行时被状态的 CustomZones 覆盖。
        /// </summary>
        public List<ShapeZone> Zones
        {
            get { return _zones; }
            set { _zones = value; NotifyChanged(); }
        }

        /// <summary>Zone 冻结缩放的参考宽度（通常为创建时的默认宽度）</summary>
        public float RefWidth
        {
            get { return _refWidth; }
            set { _refWidth = value; }
        }

        /// <summary>Zone 冻结缩放的参考高度（通常为创建时的默认高度）</summary>
        public float RefHeight
        {
            get { return _refHeight; }
            set { _refHeight = value; }
        }

        /// <summary>
        /// 系统行为列表。由 ShapeType.GenerateSystemBehaviors 生成，
        /// 包含标题编辑、成员管理、Zone 点击/连接等系统级行为。
        /// </summary>
        public List<ShapeAction> SystemActions
        {
            get { return _systemActions; }
            set { _systemActions = value; }
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

        /// <summary>
        /// 获取当前生效的 Zone 列表。
        /// 优先使用当前状态的 CustomZones，其次使用实例自身的 Zones，
        /// 最后回退到 ShapeType 的 Zones。
        /// </summary>
        private List<ShapeZone> GetEffectiveZones()
        {
            ShapeState currentState = GetCurrentState();
            if (currentState != null && currentState.CustomZones != null
                && currentState.CustomZones.Count > 0)
            {
                return currentState.CustomZones;
            }

            if (_zones != null && _zones.Count > 0)
                return _zones;

            ShapeType type = ShapeTypeRegistry.Instance.GetShapeType(_shapeTypeName);
            if (type != null && type.Zones != null && type.Zones.Count > 0)
            {
                type.EnsureDefaultZones();
                return type.Zones;
            }

            return null;
        }

        /// <summary>
        /// 计算 Zone 在图形边界内的绝对矩形。
        /// 委托给 ShapeZone.GetAnchoredBounds 处理锚定逻辑。
        /// </summary>
        private RectangleF GetZoneBounds(ShapeZone zone)
        {
            return zone.GetAnchoredBounds(Bounds, _refWidth, _refHeight);
        }

        /// <summary>查找标题 Zone</summary>
        private ShapeZone FindZone(List<ShapeZone> zones, bool isTitle)
        {
            if (zones == null)
                return null;
            foreach (ShapeZone z in zones)
            {
                if (isTitle && z.IsTitleZone)
                    return z;
                if (!isTitle && z.IsMemberZone)
                    return z;
            }
            return null;
        }

        /// <summary>查找指定名称的 Zone</summary>
        public ShapeZone FindZoneByName(string name)
        {
            List<ShapeZone> zones = GetEffectiveZones();
            if (zones == null)
                return null;
            foreach (ShapeZone z in zones)
            {
                if (z.Name == name)
                    return z;
            }
            return null;
        }

        /// <summary>
        /// 命中测试：返回指定坐标下的 Zone（从后往前遍历，最上层优先）。
        /// 用于画布点击 Zone 触发系统行为。
        /// </summary>
        public ShapeZone HitTestZone(PointF worldPoint)
        {
            List<ShapeZone> zones = GetEffectiveZones();
            if (zones == null)
                return null;

            for (int i = zones.Count - 1; i >= 0; i--)
            {
                ShapeZone z = zones[i];
                if (!z.IsFunctionalZone)
                    continue;
                RectangleF bounds = GetZoneBounds(z);
                if (bounds.Contains(worldPoint))
                    return z;
            }
            return null;
        }

        /// <summary>
        /// 命中测试成员：返回指定坐标下的成员索引（-1 表示未命中）。
        /// 在成员 Zone 内按行高计算所属行。
        /// </summary>
        public int HitTestMember(PointF worldPoint)
        {
            if (_members == null || _members.Count == 0)
                return -1;

            List<ShapeZone> zones = GetEffectiveZones();
            if (zones == null)
                return -1;

            ShapeZone memberZone = null;
            foreach (ShapeZone z in zones)
            {
                if (z.IsMemberZone)
                {
                    memberZone = z;
                    break;
                }
            }
            if (memberZone == null)
                return -1;

            RectangleF zoneRect = GetZoneBounds(memberZone);
            if (!zoneRect.Contains(worldPoint))
                return -1;

            float relY = worldPoint.Y - zoneRect.Y - 2f;
            if (relY < 0)
                return -1;

            int idx = (int)(relY / _memberLineHeight);
            if (idx >= 0 && idx < _members.Count)
                return idx;

            return -1;
        }

        /// <summary>
        /// 获取成员在画布上的渲染矩形（用于内联编辑定位）。
        /// </summary>
        public RectangleF GetMemberBounds(int memberIndex)
        {
            if (memberIndex < 0 || memberIndex >= _members.Count)
                return RectangleF.Empty;

            List<ShapeZone> zones = GetEffectiveZones();
            if (zones == null)
                return RectangleF.Empty;

            ShapeZone memberZone = null;
            foreach (ShapeZone z in zones)
            {
                if (z.IsMemberZone)
                {
                    memberZone = z;
                    break;
                }
            }
            if (memberZone == null)
                return RectangleF.Empty;

            RectangleF zoneRect = GetZoneBounds(memberZone);
            float top = zoneRect.Y + 2f + memberIndex * _memberLineHeight;
            return new RectangleF(zoneRect.X + 4f, top, zoneRect.Width - 8f, _memberLineHeight);
        }

        /// <summary>获取标题 Zone 的渲染矩形（用于内联编辑定位）</summary>
        public RectangleF GetTitleZoneBounds()
        {
            List<ShapeZone> zones = GetEffectiveZones();
            if (zones == null)
                return RectangleF.Empty;

            foreach (ShapeZone z in zones)
            {
                if (z.IsTitleZone)
                    return GetZoneBounds(z);
            }
            return RectangleF.Empty;
        }

        public override void Draw(Graphics g, float scale)
        {
            ShapeType type = ShapeTypeRegistry.Instance.GetShapeType(_shapeTypeName);
            ShapeColors colors = ComputeGradientColors();
            List<ShapeZone> effectiveZones = GetEffectiveZones();

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

            // 1. 渲染图形主体
            // 可缩放区域的实际边界（差分缩放后），供后续 Zone 可视化渲染使用
            RectangleF? scalableZoneActualRect = null;

            if (commands != null && commands.Count > 0)
            {
                // 查找可缩放区域（最多1个有效）
                ShapeZone scalableZone = null;
                if (effectiveZones != null)
                {
                    foreach (ShapeZone z in effectiveZones)
                    {
                        if (z.IsScalableZone) { scalableZone = z; break; }
                    }
                }

                if (scalableZone != null)
                {
                    // === 九宫格差分缩放 ===
                    // 通过顶点变换器实现逐顶点的分段映射：
                    // X 方向分为三段：[0, zoneX] 固定 / [zoneX, zoneX+zoneW] 拉伸 / [zoneX+zoneW, 1] 固定
                    // Y 方向同理。边距基于参考尺寸（固定不缩放），区域吸收尺寸变化量。

                    float zX = scalableZone.X;
                    float zY = scalableZone.Y;
                    float zW = scalableZone.Width;
                    float zH = scalableZone.Height;
                    float zoneRight = zX + zW;
                    float zoneBottom = zY + zH;

                    // 固定边距（基于参考尺寸）
                    float leftMargin = zX * _refWidth;
                    float topMargin = zY * _refHeight;
                    float rightMargin = (1f - zoneRight) * _refWidth;
                    float bottomMargin = (1f - zoneBottom) * _refHeight;

                    // 区域实际边界（吸收元件尺寸变化量）
                    float zoneActualX = Bounds.X + leftMargin;
                    float zoneActualY = Bounds.Y + topMargin;
                    float zoneActualW = Math.Max(1f, Bounds.Width - leftMargin - rightMargin);
                    float zoneActualH = Math.Max(1f, Bounds.Height - topMargin - bottomMargin);
                    float zoneActualRight = zoneActualX + zoneActualW;
                    float zoneActualBottom = zoneActualY + zoneActualH;

                    // 记录实际区域边界供后续可视化使用
                    scalableZoneActualRect = new RectangleF(zoneActualX, zoneActualY, zoneActualW, zoneActualH);

                    // 构建九宫格顶点变换器
                    RenderCommand.VertexTransformer transformer = delegate(float normX, float normY)
                    {
                        float pixelX, pixelY;

                        // === X 方向分段映射 ===
                        if (normX <= zX)
                        {
                            // 左侧固定区：[0, zoneX] → [Bounds.X, zoneActualX]
                            // 使用参考尺寸保持比例
                            if (zX > 0.0001f)
                                pixelX = Bounds.X + (normX / zX) * leftMargin;
                            else
                                pixelX = Bounds.X;
                        }
                        else if (normX <= zoneRight)
                        {
                            // 中间拉伸区：[zoneX, zoneRight] → [zoneActualX, zoneActualRight]
                            if (zW > 0.0001f)
                                pixelX = zoneActualX + ((normX - zX) / zW) * zoneActualW;
                            else
                                pixelX = zoneActualX;
                        }
                        else
                        {
                            // 右侧固定区：[zoneRight, 1] → [zoneActualRight, Bounds.Right]
                            float rightRange = 1f - zoneRight;
                            if (rightRange > 0.0001f)
                                pixelX = zoneActualRight + ((normX - zoneRight) / rightRange) * rightMargin;
                            else
                                pixelX = zoneActualRight;
                        }

                        // === Y 方向分段映射 ===
                        if (normY <= zY)
                        {
                            if (zY > 0.0001f)
                                pixelY = Bounds.Y + (normY / zY) * topMargin;
                            else
                                pixelY = Bounds.Y;
                        }
                        else if (normY <= zoneBottom)
                        {
                            if (zH > 0.0001f)
                                pixelY = zoneActualY + ((normY - zY) / zH) * zoneActualH;
                            else
                                pixelY = zoneActualY;
                        }
                        else
                        {
                            float bottomRange = 1f - zoneBottom;
                            if (bottomRange > 0.0001f)
                                pixelY = zoneActualBottom + ((normY - zoneBottom) / bottomRange) * bottomMargin;
                            else
                                pixelY = zoneActualBottom;
                        }

                        return new PointF(pixelX, pixelY);
                    };

                    // 设置变换器和区域边界，执行所有指令，然后清除
                    RenderCommand.SetVertexTransformer(transformer);
                    RenderCommand.SetScalableZoneBounds(
                        new RectangleF(zX, zY, zW, zH));
                    try
                    {
                        foreach (RenderCommand cmd in commands)
                        {
                            cmd.Execute(g, Bounds, colors, scale);
                        }
                    }
                    finally
                    {
                        RenderCommand.ClearVertexTransformer();
                        RenderCommand.ClearScalableZoneBounds();
                    }
                }
                else
                {
                    // 无可缩放区域：正常渲染
                    foreach (RenderCommand cmd in commands)
                    {
                        cmd.Execute(g, Bounds, colors, scale);
                    }
                }
            }
            else
            {
                DrawFallback(g, colors, scale);
            }

            // 2. 渲染图片图层（从 ShapeType 定义获取，随元件边界缩放）
            if (type != null && type.ImageLayers != null && type.ImageLayers.Count > 0)
            {
                foreach (ImageLayer imgLayer in type.ImageLayers)
                {
                    if (!imgLayer.Visible) continue;

                    Image img = imgLayer.ToImage();
                    if (img == null) continue;

                    RectangleF imgBounds = imgLayer.GetBounds(Bounds);
                    g.DrawImage(img, imgBounds);
                    img.Dispose();
                }
            }

            // 3. 渲染 Zone 边框和 Zone 内绘制指令
            // 根据 Visibility 控制显示时机：设计模式下显示 DesignTime/RunTime，运行模式下仅显示 RunTime
            // 在图形编辑器中由编辑器自身绘制 Zone，不受此逻辑影响
            if (effectiveZones != null)
            {
                foreach (ShapeZone zone in effectiveZones)
                {
                    // 根据 Visibility 决定是否在主画布上渲染
                    bool shouldRender;
                    if (RenderZoneVisuals)
                        shouldRender = (zone.Visibility == ZoneVisibility.DesignTime || zone.Visibility == ZoneVisibility.RunTime);
                    else
                        shouldRender = (zone.Visibility == ZoneVisibility.RunTime);

                    if (!shouldRender)
                        continue;

                    RectangleF zoneRect = GetZoneBounds(zone);

                    // 功能性 Zone（点击/连接/可缩放）以半透明填充 + 虚线边框显示
                    if (zone.IsClickZone || zone.IsConnectionZone)
                    {
                        Color fillC = Color.FromArgb(60, zone.FillColor.ToColor());
                        using (Brush brush = new SolidBrush(fillC))
                            g.FillRectangle(brush, zoneRect);

                        using (Pen pen = new Pen(zone.BorderColor.ToColor(), 1f / scale))
                        {
                            pen.DashStyle = DashStyle.Dash;
                            g.DrawRectangle(pen, zoneRect.X, zoneRect.Y, zoneRect.Width, zoneRect.Height);
                        }

                        // 连接区域绘制小圆点指示
                        if (zone.IsConnectionZone)
                        {
                            float dotR = 3f / scale;
                            float cx = zoneRect.X + zoneRect.Width / 2f;
                            float cy = zoneRect.Y + zoneRect.Height / 2f;
                            using (Brush dotBrush = new SolidBrush(zone.BorderColor.ToColor()))
                                g.FillEllipse(dotBrush, cx - dotR, cy - dotR, dotR * 2, dotR * 2);
                        }
                    }
                    else if (zone.IsScalableZone)
                    {
                        // 可缩放区域使用差分缩放后的实际边界（非比例边界）
                        RectangleF szRect = scalableZoneActualRect.HasValue
                            ? scalableZoneActualRect.Value : zoneRect;

                        // 可缩放区域：半透明蓝色填充 + 虚线边框 + 标签
                        Color scalableFill = Color.FromArgb(30, 0, 120, 215);
                        using (Brush brush = new SolidBrush(scalableFill))
                            g.FillRectangle(brush, szRect);

                        using (Pen pen = new Pen(Color.FromArgb(0, 120, 215), 1f / scale))
                        {
                            pen.DashStyle = DashStyle.Dash;
                            g.DrawRectangle(pen, szRect.X, szRect.Y, szRect.Width, szRect.Height);
                        }

                        // 绘制缩放方向指示线（双向箭头）
                        float cx = szRect.X + szRect.Width / 2f;
                        float cy = szRect.Y + szRect.Height / 2f;
                        float arrowLen = 8f / scale;
                        using (Pen arrowPen = new Pen(Color.FromArgb(120, 0, 120, 215), 1f / scale))
                        {
                            // 水平双向箭头
                            g.DrawLine(arrowPen, cx - arrowLen, cy, cx + arrowLen, cy);
                            // 垂直双向箭头
                            g.DrawLine(arrowPen, cx, cy - arrowLen, cx, cy + arrowLen);
                        }

                        // 标签
                        using (Font font = new Font("Microsoft YaHei", 7f / scale))
                        using (Brush labelBrush = new SolidBrush(Color.FromArgb(0, 120, 215)))
                        {
                            g.DrawString("[可缩放]", font, labelBrush,
                                szRect.X + 2f / scale, szRect.Y + 2f / scale);
                        }
                    }
                    else
                    {
                        // 普通区域：虚线边框
                        using (Pen pen = new Pen(zone.BorderColor.ToColor(), 0.5f / scale))
                        {
                            pen.DashStyle = DashStyle.Dash;
                            g.DrawRectangle(pen, zoneRect.X, zoneRect.Y, zoneRect.Width, zoneRect.Height);
                        }
                    }

                    // Zone 内绘制指令
                    if (zone.RenderCommands != null && zone.RenderCommands.Count > 0)
                    {
                        foreach (RenderCommand cmd in zone.RenderCommands)
                        {
                            cmd.Execute(g, zoneRect, colors, scale);
                        }
                    }
                }
            }

            // 4. 渲染标题（使用 Title Zone 或回退到旧逻辑）
            DrawName(g, scale, effectiveZones);

            // 5. 渲染成员列表（使用 Member Zone 或回退到旧逻辑）
            DrawMembers(g, scale, effectiveZones);

            // 6. 渲染选中状态
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

        /// <summary>
        /// 绘制图形名称。优先使用 Title Zone 的边界，
        /// 若无 Zone 则回退到基于 MemberAreaTop 的旧逻辑。
        /// </summary>
        private void DrawName(Graphics g, float scale, List<ShapeZone> zones)
        {
            if (string.IsNullOrEmpty(Name))
                return;

            ShapeType type = ShapeTypeRegistry.Instance.GetShapeType(_shapeTypeName);

            using (Font font = new Font("Microsoft YaHei", 10f / scale, FontStyle.Regular))
            using (Brush brush = new SolidBrush(TextColor))
            {
                StringFormat sf = new StringFormat();
                sf.Trimming = StringTrimming.EllipsisCharacter;

                RectangleF textRect;
                bool hasMembers = (_members != null && _members.Count > 0);

                // 优先使用 Title Zone
                ShapeZone titleZone = FindZone(zones, true);
                if (titleZone != null)
                {
                    textRect = GetZoneBounds(titleZone);
                    textRect.Inflate(-6 / scale, -4 / scale);
                }
                else
                {
                    // 回退到旧逻辑
                    textRect = Bounds;
                    textRect.Inflate(-6 / scale, -6 / scale);
                    if (hasMembers)
                        textRect.Height = Bounds.Height * _memberAreaTop;
                }

                // 根据 ShapeType 的名称对齐方式调整
                NameAlignment alignment = NameAlignment.Center;
                if (type != null)
                    alignment = type.NameAlignment;

                switch (alignment)
                {
                    case NameAlignment.TopLeft:
                        // TopLeft 仅决定 Zone 的位置（左上角标签区域），
                        // 文字在 Zone 内仍居中对齐，与 Zone 创建注释一致
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
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

        /// <summary>
        /// 绘制成员列表。优先使用 Member Zone 的边界，
        /// 若无 Zone 则回退到基于 MemberAreaTop 的旧逻辑。
        /// </summary>
        private void DrawMembers(Graphics g, float scale, List<ShapeZone> zones)
        {
            if (_members == null || _members.Count == 0)
                return;

            float top, left;
            float lineHeight = _memberLineHeight / scale;

            // 优先使用 Member Zone
            ShapeZone memberZone = FindZone(zones, false);
            if (memberZone != null)
            {
                RectangleF zoneRect = GetZoneBounds(memberZone);
                top = zoneRect.Y + 2 / scale;
                left = zoneRect.X + 4 / scale;

                // 成员区分隔线
                using (Pen pen = new Pen(Color.FromArgb(200, 200, 200), 0.5f / scale))
                {
                    g.DrawLine(pen, zoneRect.X, zoneRect.Y, zoneRect.Right, zoneRect.Y);
                }
            }
            else
            {
                // 回退到旧逻辑
                top = Bounds.Y + Bounds.Height * _memberAreaTop;
                left = Bounds.X + 4 / scale;

                using (Pen pen = new Pen(Color.FromArgb(200, 200, 200), 0.5f / scale))
                {
                    g.DrawLine(pen, Bounds.X, top, Bounds.Right, top);
                }
            }

            using (Font font = new Font("Microsoft YaHei", 8f / scale, FontStyle.Regular))
            using (Brush brush = new SolidBrush(TextColor))
            {
                float bottomLimit = Bounds.Bottom - 2 / scale;
                if (memberZone != null)
                {
                    RectangleF zoneRect = GetZoneBounds(memberZone);
                    bottomLimit = zoneRect.Bottom - 2 / scale;
                }

                for (int i = 0; i < _members.Count; i++)
                {
                    float y = top + i * lineHeight;
                    if (y + lineHeight > bottomLimit)
                        break;

                    string sig = _members[i].GetSignature();
                    g.DrawString(sig, font, brush, left, y);
                }
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
            clone.RefWidth = this.RefWidth;
            clone.RefHeight = this.RefHeight;

            // 拷贝成员
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

            // 拷贝状态（使用 RenderCommand.Clone）
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
                        cs.CustomRenderCommands.Add(rc.Clone());
                }
                // 拷贝状态的 CustomZones
                if (s.CustomZones != null)
                {
                    foreach (ShapeZone z in s.CustomZones)
                        cs.CustomZones.Add(z.Clone());
                }
                clone.States.Add(cs);
            }

            // 拷贝 Zone
            foreach (ShapeZone z in this.Zones)
                clone.Zones.Add(z.Clone());

            return clone;
        }
    }
}
