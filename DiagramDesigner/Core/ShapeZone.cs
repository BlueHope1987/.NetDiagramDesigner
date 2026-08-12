using System;
using System.Collections.Generic;
using System.Drawing;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// Zone 布局方式。决定 Zone 内子元件的排列规则及 Zone 的特殊行为。
    /// </summary>
    public enum ZoneLayout
    {
        /// <summary>无布局：Zone 仅作为命名区域，不排列子元件</summary>
        None,
        /// <summary>标题区域：用于显示图形名称，所有 ShapeType 默认拥有一个 Title Zone</summary>
        Title,
        /// <summary>垂直堆叠：子元件在 Zone 内从上到下依次排列</summary>
        Stack,
        /// <summary>水平流式：子元件在 Zone 内从左到右排列，超出换行</summary>
        Flow,
        /// <summary>成员列表：用于显示 ShapeMember 列表（如类图的属性/方法区）</summary>
        Member,
        /// <summary>点击区域：点击后触发已注册的行为序列</summary>
        Click,
        /// <summary>连接区域：作为连线起点或终点，可限定线型及是否允许同元件内相连</summary>
        Connection
    }

    /// <summary>
    /// Zone 缩放行为。决定图形缩放时 Zone 边角是否冻结。
    /// </summary>
    public enum ZoneScaling
    {
        /// <summary>随图形等比缩放（默认）</summary>
        None,
        /// <summary>冻结边角：缩放时保持 Zone 的绝对像素尺寸不变，
        /// 常用于标题区域，使文字在缩放时不漂移</summary>
        Freeze
    }

    /// <summary>
    /// Zone 锚定方式。决定 Zone 在图形内的定位逻辑。
    /// 当 Anchor != Absolute 时，X/Y 作为相对于锚点的偏移值。
    /// </summary>
    public enum ZoneAnchor
    {
        /// <summary>绝对定位：X/Y 为归一化坐标（0~1），直接指定位置</summary>
        Absolute,
        /// <summary>左上角锚定</summary>
        TopLeft,
        /// <summary>上居中锚定</summary>
        TopCenter,
        /// <summary>右上角锚定</summary>
        TopRight,
        /// <summary>左居中锚定</summary>
        MiddleLeft,
        /// <summary>正中锚定（标题 Zone 默认）</summary>
        MiddleCenter,
        /// <summary>右居中锚定</summary>
        MiddleRight,
        /// <summary>左下角锚定</summary>
        BottomLeft,
        /// <summary>下居中锚定</summary>
        BottomCenter,
        /// <summary>右下角锚定</summary>
        BottomRight
    }

    /// <summary>
    /// 图形区域（Zone）模型。Zone 是图形内部的逻辑分区，
    /// 用于容纳标题、成员列表、点击触发或连线锚点。
    /// 每个 ShapeType 默认包含一个 Title Zone（锚定为 MiddleCenter）用于显示名称。
    /// </summary>
    [Serializable]
    public class ShapeZone
    {
        private string _name = "Zone";
        private ZoneLayout _layout = ZoneLayout.None;
        private ZoneScaling _scaling = ZoneScaling.None;
        private ZoneAnchor _anchor = ZoneAnchor.Absolute;
        private float _x = 0f;
        private float _y = 0f;
        private float _width = 1f;
        private float _height = 1f;
        private bool _showBorder = false;
        private XmlColor _borderColor = new XmlColor(Color.FromArgb(180, 180, 180));
        private XmlColor _fillColor = new XmlColor(Color.FromArgb(255, 248, 220, 220));
        private string _title = "";
        private bool _isTitleZone = false;
        private bool _isMemberZone = false;
        private bool _isClickZone = false;
        private bool _isConnectionZone = false;
        private List<RenderCommand> _renderCommands = new List<RenderCommand>();

        // === 连接区域属性 ===
        private bool _canStart = true;
        private bool _canEnd = true;
        private string _allowedLineTypes = "Straight,Curve,Orthogonal";
        private bool _allowSelfConnect = false;

        #region 基本属性

        /// <summary>Zone 名称，在同一 ShapeType 内唯一标识</summary>
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        /// <summary>布局方式</summary>
        public ZoneLayout Layout
        {
            get { return _layout; }
            set { _layout = value; }
        }

        /// <summary>缩放行为</summary>
        public ZoneScaling Scaling
        {
            get { return _scaling; }
            set { _scaling = value; }
        }

        /// <summary>
        /// 锚定方式。当非 Absolute 时，X/Y 作为偏移值。
        /// </summary>
        public ZoneAnchor Anchor
        {
            get { return _anchor; }
            set { _anchor = value; }
        }

        /// <summary>
        /// Zone 在图形内的相对 X。
        /// 当 Anchor=Absolute 时为归一化坐标（0~1）；
        /// 当 Anchor 为其他值时为相对锚点的偏移量。
        /// </summary>
        public float X
        {
            get { return _x; }
            set { _x = value; }
        }

        /// <summary>
        /// Zone 在图形内的相对 Y。
        /// 当 Anchor=Absolute 时为归一化坐标（0~1）；
        /// 当 Anchor 为其他值时为相对锚点的偏移量。
        /// </summary>
        public float Y
        {
            get { return _y; }
            set { _y = value; }
        }

        /// <summary>Zone 相对宽度（0~1）</summary>
        public float Width
        {
            get { return _width; }
            set { _width = value; }
        }

        /// <summary>Zone 相对高度（0~1）</summary>
        public float Height
        {
            get { return _height; }
            set { _height = value; }
        }

        /// <summary>是否显示 Zone 边框</summary>
        public bool ShowBorder
        {
            get { return _showBorder; }
            set { _showBorder = value; }
        }

        /// <summary>边框颜色</summary>
        public XmlColor BorderColor
        {
            get { return _borderColor; }
            set { _borderColor = value; }
        }

        /// <summary>Zone 填充颜色（预览时以半透明显示）</summary>
        public XmlColor FillColor
        {
            get { return _fillColor; }
            set { _fillColor = value; }
        }

        /// <summary>标题文本（仅 Title 布局时使用，留空则显示图形名称）</summary>
        public string Title
        {
            get { return _title; }
            set { _title = value; }
        }

        /// <summary>标记为默认标题 Zone（每个 ShapeType 应有且仅有一个）</summary>
        public bool IsTitleZone
        {
            get { return _isTitleZone; }
            set { _isTitleZone = value; }
        }

        /// <summary>标记为成员列表 Zone</summary>
        public bool IsMemberZone
        {
            get { return _isMemberZone; }
            set { _isMemberZone = value; }
        }

        /// <summary>标记为点击触发 Zone</summary>
        public bool IsClickZone
        {
            get { return _isClickZone; }
            set { _isClickZone = value; }
        }

        /// <summary>标记为连接 Zone</summary>
        public bool IsConnectionZone
        {
            get { return _isConnectionZone; }
            set { _isConnectionZone = value; }
        }

        /// <summary>Zone 内的绘制指令（如分隔线、背景等）</summary>
        public List<RenderCommand> RenderCommands
        {
            get { return _renderCommands; }
            set { _renderCommands = value; }
        }

        #endregion

        #region 连接区域属性

        /// <summary>连接区域：是否可作为连线起点</summary>
        public bool CanStart
        {
            get { return _canStart; }
            set { _canStart = value; }
        }

        /// <summary>连接区域：是否可作为连线终点</summary>
        public bool CanEnd
        {
            get { return _canEnd; }
            set { _canEnd = value; }
        }

        /// <summary>
        /// 连接区域：允许的线型，逗号分隔。
        /// 可选值：Straight, Curve, Orthogonal
        /// </summary>
        public string AllowedLineTypes
        {
            get { return _allowedLineTypes; }
            set { _allowedLineTypes = value; }
        }

        /// <summary>连接区域：是否允许同元件内相连</summary>
        public bool AllowSelfConnect
        {
            get { return _allowSelfConnect; }
            set { _allowSelfConnect = value; }
        }

        #endregion

        public ShapeZone() { }

        #region 便捷判断

        /// <summary>是否为特殊功能 Zone（标题/成员/点击/连接）</summary>
        public bool IsFunctionalZone
        {
            get { return _isTitleZone || _isMemberZone || _isClickZone || _isConnectionZone; }
        }

        #endregion

        #region 工厂方法

        /// <summary>
        /// 创建默认标题 Zone。
        /// 普通图形：锚定为 MiddleCenter（垂直居中水平居中）。
        /// </summary>
        public static ShapeZone CreateDefaultTitleZone(float nameAreaTop)
        {
            ShapeZone zone = new ShapeZone();
            zone.Name = "Title";
            zone.Layout = ZoneLayout.Title;
            zone.Scaling = ZoneScaling.Freeze;
            zone.Anchor = ZoneAnchor.MiddleCenter;
            zone.X = 0f;
            zone.Y = 0f;
            zone.Width = 1f;
            zone.Height = nameAreaTop;
            zone.ShowBorder = false;
            zone.IsTitleZone = true;
            return zone;
        }

        /// <summary>
        /// 创建类图等需要顶部标题的 Zone。
        /// 锚定为 TopCenter（顶边对齐到图形顶部），Y 向下偏移 0.02（约3px）。
        /// 文字在 Zone 内垂直居中，距图形顶边约 15px。
        /// </summary>
        public static ShapeZone CreateTopTitleZone(float nameAreaTop)
        {
            ShapeZone zone = CreateDefaultTitleZone(nameAreaTop);
            zone.Anchor = ZoneAnchor.TopCenter;
            zone.Y = 0.02f;  // 向下偏移，避免贴边
            zone.Height = nameAreaTop;
            return zone;
        }

        /// <summary>
        /// 创建包图等需要左上角标题的 Zone。
        /// 标题区域宽度与上方标签状区域（0~0.4）一致，并在标签区域内居中对齐。
        /// 锚定为 TopLeft（左上角对齐到图形左上角），X=0, Y=0, Width=0.4。
        /// 文字在 Zone 内水平+垂直居中，自然落在标签区域中央。
        /// </summary>
        public static ShapeZone CreateTopLeftTitleZone(float nameAreaTop)
        {
            ShapeZone zone = CreateDefaultTitleZone(nameAreaTop);
            zone.Anchor = ZoneAnchor.TopLeft;
            zone.X = 0f;       // 与标签区域左边缘对齐
            zone.Y = 0f;       // 与标签区域上边缘对齐
            zone.Width = 0.4f; // 与标签区域宽度一致
            zone.Height = nameAreaTop;
            return zone;
        }

        /// <summary>创建默认成员 Zone</summary>
        public static ShapeZone CreateDefaultMemberZone(float nameAreaTop)
        {
            ShapeZone zone = new ShapeZone();
            zone.Name = "Members";
            zone.Layout = ZoneLayout.Member;
            zone.Scaling = ZoneScaling.None;
            zone.Anchor = ZoneAnchor.Absolute;
            zone.X = 0f;
            zone.Y = nameAreaTop;
            zone.Width = 1f;
            zone.Height = 1f - nameAreaTop;
            zone.ShowBorder = false;
            zone.IsMemberZone = true;
            return zone;
        }

        /// <summary>创建点击区域 Zone</summary>
        public static ShapeZone CreateClickZone(string name)
        {
            ShapeZone zone = new ShapeZone();
            zone.Name = name;
            zone.Layout = ZoneLayout.Click;
            zone.Anchor = ZoneAnchor.Absolute;
            zone.X = 0.1f;
            zone.Y = 0.1f;
            zone.Width = 0.3f;
            zone.Height = 0.2f;
            zone.ShowBorder = true;
            zone.IsClickZone = true;
            return zone;
        }

        /// <summary>创建连接区域 Zone</summary>
        public static ShapeZone CreateConnectionZone(string name)
        {
            ShapeZone zone = new ShapeZone();
            zone.Name = name;
            zone.Layout = ZoneLayout.Connection;
            zone.Anchor = ZoneAnchor.TopRight;
            zone.X = 0f;
            zone.Y = 0f;
            zone.Width = 0.15f;
            zone.Height = 0.15f;
            zone.ShowBorder = true;
            zone.IsConnectionZone = true;
            zone.CanStart = true;
            zone.CanEnd = true;
            zone.AllowSelfConnect = false;
            return zone;
        }

        #endregion

        #region 锚定位置计算

        /// <summary>
        /// 根据锚定方式和图形边界，计算 Zone 的绝对矩形。
        /// 当 Anchor != Absolute 时，X/Y 为偏移量（以参考尺寸为基准的像素级偏移）。
        /// 对齐规则（边角对齐，而非居中于锚点）：
        ///   Top*    → Zone 顶边对齐到锚点 Y
        ///   Middle*  → Zone 垂直居中于锚点 Y
        ///   Bottom*  → Zone 底边对齐到锚点 Y
        ///   *Left    → Zone 左边对齐到锚点 X
        ///   *Center  → Zone 水平居中于锚点 X
        ///   *Right   → Zone 右边对齐到锚点 X
        /// 这样 Zone 自然落在图形内部，偏移量表示"距边的像素距离"。
        /// </summary>
        public RectangleF GetAnchoredBounds(RectangleF shapeBounds, float refWidth, float refHeight)
        {
            float absW, absH;

            if (_scaling == ZoneScaling.Freeze)
            {
                absW = _width * refWidth;
                absH = _height * refHeight;
            }
            else
            {
                absW = _width * shapeBounds.Width;
                absH = _height * shapeBounds.Height;
            }

            float absX, absY;

            if (_anchor == ZoneAnchor.Absolute)
            {
                absX = shapeBounds.X + _x * shapeBounds.Width;
                absY = shapeBounds.Y + _y * shapeBounds.Height;
            }
            else
            {
                // 计算锚点在图形内的绝对位置
                float anchorX = shapeBounds.X;
                float anchorY = shapeBounds.Y;

                switch (_anchor)
                {
                    case ZoneAnchor.TopLeft:
                        anchorX = shapeBounds.X;
                        anchorY = shapeBounds.Y;
                        break;
                    case ZoneAnchor.TopCenter:
                        anchorX = shapeBounds.X + shapeBounds.Width / 2f;
                        anchorY = shapeBounds.Y;
                        break;
                    case ZoneAnchor.TopRight:
                        anchorX = shapeBounds.Right;
                        anchorY = shapeBounds.Y;
                        break;
                    case ZoneAnchor.MiddleLeft:
                        anchorX = shapeBounds.X;
                        anchorY = shapeBounds.Y + shapeBounds.Height / 2f;
                        break;
                    case ZoneAnchor.MiddleCenter:
                        anchorX = shapeBounds.X + shapeBounds.Width / 2f;
                        anchorY = shapeBounds.Y + shapeBounds.Height / 2f;
                        break;
                    case ZoneAnchor.MiddleRight:
                        anchorX = shapeBounds.Right;
                        anchorY = shapeBounds.Y + shapeBounds.Height / 2f;
                        break;
                    case ZoneAnchor.BottomLeft:
                        anchorX = shapeBounds.X;
                        anchorY = shapeBounds.Bottom;
                        break;
                    case ZoneAnchor.BottomCenter:
                        anchorX = shapeBounds.X + shapeBounds.Width / 2f;
                        anchorY = shapeBounds.Bottom;
                        break;
                    case ZoneAnchor.BottomRight:
                        anchorX = shapeBounds.Right;
                        anchorY = shapeBounds.Bottom;
                        break;
                }

                // 水平方向：*Left → 左边对齐, *Center → 居中, *Right → 右边对齐
                if (_anchor == ZoneAnchor.TopLeft || _anchor == ZoneAnchor.MiddleLeft || _anchor == ZoneAnchor.BottomLeft)
                    absX = anchorX + _x * refWidth;
                else if (_anchor == ZoneAnchor.TopRight || _anchor == ZoneAnchor.MiddleRight || _anchor == ZoneAnchor.BottomRight)
                    absX = anchorX - absW + _x * refWidth;
                else // *Center
                    absX = anchorX - absW / 2f + _x * refWidth;

                // 垂直方向：Top* → 顶边对齐, Middle* → 居中, Bottom* → 底边对齐
                if (_anchor == ZoneAnchor.TopLeft || _anchor == ZoneAnchor.TopCenter || _anchor == ZoneAnchor.TopRight)
                    absY = anchorY + _y * refHeight;
                else if (_anchor == ZoneAnchor.BottomLeft || _anchor == ZoneAnchor.BottomCenter || _anchor == ZoneAnchor.BottomRight)
                    absY = anchorY - absH + _y * refHeight;
                else // Middle*
                    absY = anchorY - absH / 2f + _y * refHeight;
            }

            return new RectangleF(absX, absY, absW, absH);
        }

        #endregion

        /// <summary>深拷贝</summary>
        public ShapeZone Clone()
        {
            ShapeZone clone = new ShapeZone();
            clone._name = _name;
            clone._layout = _layout;
            clone._scaling = _scaling;
            clone._anchor = _anchor;
            clone._x = _x;
            clone._y = _y;
            clone._width = _width;
            clone._height = _height;
            clone._showBorder = _showBorder;
            clone._borderColor = new XmlColor(_borderColor.ToColor());
            clone._fillColor = new XmlColor(_fillColor.ToColor());
            clone._title = _title;
            clone._isTitleZone = _isTitleZone;
            clone._isMemberZone = _isMemberZone;
            clone._isClickZone = _isClickZone;
            clone._isConnectionZone = _isConnectionZone;
            clone._renderCommands = CloneRenderCommands(_renderCommands);
            clone._canStart = _canStart;
            clone._canEnd = _canEnd;
            clone._allowedLineTypes = _allowedLineTypes;
            clone._allowSelfConnect = _allowSelfConnect;
            return clone;
        }

        /// <summary>拷贝 RenderCommand 列表（辅助方法）</summary>
        public static List<RenderCommand> CloneRenderCommands(List<RenderCommand> src)
        {
            if (src == null)
                return new List<RenderCommand>();
            List<RenderCommand> result = new List<RenderCommand>();
            foreach (RenderCommand rc in src)
                result.Add(rc.Clone());
            return result;
        }
    }
}
