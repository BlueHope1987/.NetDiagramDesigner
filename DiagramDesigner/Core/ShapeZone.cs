using System;
using System.Collections.Generic;
using System.Drawing;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// Zone 布局方式。决定 Zone 内子元件的排列规则。
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
        Member
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
    /// 图形区域（Zone）模型。Zone 是图形内部的逻辑分区，
    /// 用于容纳标题、成员列表或子元件。
    /// 每个 ShapeType 默认包含一个 Title Zone 用于显示名称。
    /// </summary>
    [Serializable]
    public class ShapeZone
    {
        private string _name = "Zone";
        private ZoneLayout _layout = ZoneLayout.None;
        private ZoneScaling _scaling = ZoneScaling.None;
        private float _x = 0f;
        private float _y = 0f;
        private float _width = 1f;
        private float _height = 1f;
        private bool _showBorder = false;
        private XmlColor _borderColor = new XmlColor(Color.FromArgb(180, 180, 180));
        private string _title = "";
        private bool _isTitleZone = false;
        private bool _isMemberZone = false;
        private List<RenderCommand> _renderCommands = new List<RenderCommand>();

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

        /// <summary>Zone 在图形内的相对 X（0~1）</summary>
        public float X
        {
            get { return _x; }
            set { _x = value; }
        }

        /// <summary>Zone 在图形内的相对 Y（0~1）</summary>
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

        /// <summary>Zone 内的绘制指令（如分隔线、背景等）</summary>
        public List<RenderCommand> RenderCommands
        {
            get { return _renderCommands; }
            set { _renderCommands = value; }
        }

        public ShapeZone() { }

        /// <summary>创建默认标题 Zone</summary>
        public static ShapeZone CreateDefaultTitleZone(float nameAreaTop)
        {
            ShapeZone zone = new ShapeZone();
            zone.Name = "Title";
            zone.Layout = ZoneLayout.Title;
            zone.Scaling = ZoneScaling.Freeze;
            zone.X = 0f;
            zone.Y = 0f;
            zone.Width = 1f;
            zone.Height = nameAreaTop;
            zone.ShowBorder = false;
            zone.IsTitleZone = true;
            return zone;
        }

        /// <summary>创建默认成员 Zone</summary>
        public static ShapeZone CreateDefaultMemberZone(float nameAreaTop)
        {
            ShapeZone zone = new ShapeZone();
            zone.Name = "Members";
            zone.Layout = ZoneLayout.Member;
            zone.Scaling = ZoneScaling.None;
            zone.X = 0f;
            zone.Y = nameAreaTop;
            zone.Width = 1f;
            zone.Height = 1f - nameAreaTop;
            zone.ShowBorder = false;
            zone.IsMemberZone = true;
            return zone;
        }

        /// <summary>深拷贝</summary>
        public ShapeZone Clone()
        {
            ShapeZone clone = new ShapeZone();
            clone._name = _name;
            clone._layout = _layout;
            clone._scaling = _scaling;
            clone._x = _x;
            clone._y = _y;
            clone._width = _width;
            clone._height = _height;
            clone._showBorder = _showBorder;
            clone._borderColor = new XmlColor(_borderColor.ToColor());
            clone._title = _title;
            clone._isTitleZone = _isTitleZone;
            clone._isMemberZone = _isMemberZone;
            clone._renderCommands = CloneRenderCommands(_renderCommands);
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
