using System;
using System.Collections.Generic;
using System.Drawing;
using DiagramDesigner.Shapes;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 图形名称在边界内的对齐方式
    /// </summary>
    public enum NameAlignment
    {
        /// <summary>居中（默认，适用于大多数图形）</summary>
        Center,
        /// <summary>靠上居中（适用于类图等有成员区域的图形）</summary>
        TopCenter,
        /// <summary>靠左上角</summary>
        TopLeft
    }

    [Serializable]
    public class ShapeType
    {
        private string _name = "";
        private string _category = "基本图形";
        private string _description = "";
        private string _iconName = "";
        private List<RenderCommand> _renderCommands = new List<RenderCommand>();
        private bool _isContainer = false;
        private bool _supportsMembers = false;
        private float _defaultWidth = 120f;
        private float _defaultHeight = 80f;
        private XmlColor _defaultFillColor = new XmlColor(Color.FromArgb(230, 240, 255));
        private XmlColor _defaultBorderColor = new XmlColor(Color.FromArgb(80, 120, 180));
        private XmlColor _defaultTextColor = new XmlColor(Color.FromArgb(40, 40, 40));
        private List<ShapeState> _defaultStates = new List<ShapeState>();
        private NameAlignment _nameAlignment = NameAlignment.Center;
        private float _nameAreaTop = 0.35f;
        private bool _allowRename = false;
        private bool _resizable = false;
        private List<ShapeAction> _customActions = new List<ShapeAction>();

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public string Category
        {
            get { return _category; }
            set { _category = value; }
        }

        public string Description
        {
            get { return _description; }
            set { _description = value; }
        }

        public string IconName
        {
            get { return _iconName; }
            set { _iconName = value; }
        }

        public List<RenderCommand> RenderCommands
        {
            get { return _renderCommands; }
            set { _renderCommands = value; }
        }

        public bool IsContainer
        {
            get { return _isContainer; }
            set { _isContainer = value; }
        }

        public bool SupportsMembers
        {
            get { return _supportsMembers; }
            set { _supportsMembers = value; }
        }

        public float DefaultWidth
        {
            get { return _defaultWidth; }
            set { _defaultWidth = value; }
        }

        public float DefaultHeight
        {
            get { return _defaultHeight; }
            set { _defaultHeight = value; }
        }

        public XmlColor DefaultFillColor
        {
            get { return _defaultFillColor; }
            set { _defaultFillColor = value; }
        }

        public XmlColor DefaultBorderColor
        {
            get { return _defaultBorderColor; }
            set { _defaultBorderColor = value; }
        }

        public XmlColor DefaultTextColor
        {
            get { return _defaultTextColor; }
            set { _defaultTextColor = value; }
        }

        public List<ShapeState> DefaultStates
        {
            get { return _defaultStates; }
            set { _defaultStates = value; }
        }

        /// <summary>
        /// 名称在图形内的对齐方式。类图应设为 TopCenter，一般图形默认 Center。
        /// </summary>
        public NameAlignment NameAlignment
        {
            get { return _nameAlignment; }
            set { _nameAlignment = value; }
        }

        /// <summary>
        /// 名称区域顶部的相对位置（0~1），仅在 SupportsMembers=true 时有效。
        /// 类图设为 0.22 使类名靠上，默认 0.35。
        /// </summary>
        public float NameAreaTop
        {
            get { return _nameAreaTop; }
            set { _nameAreaTop = value; }
        }

        /// <summary>
        /// 是否允许在画布上直接重命名
        /// </summary>
        public bool AllowRename
        {
            get { return _allowRename; }
            set { _allowRename = value; }
        }

        /// <summary>
        /// 该图形类型的实例是否可调整大小
        /// </summary>
        public bool Resizable
        {
            get { return _resizable; }
            set { _resizable = value; }
        }

        /// <summary>
        /// 该图形类型的自定义操作列表。在右键菜单中显示，
        /// 可通过宿主回调或状态切换执行。
        /// </summary>
        public List<ShapeAction> CustomActions
        {
            get { return _customActions; }
            set { _customActions = value; }
        }

        public ShapeType() { }

        public ShapeBase CreateInstance()
        {
            if (_isContainer)
            {
                ContainerShape shape = new ContainerShape();
                shape.Name = _name;
                shape.Bounds = new RectangleF(0, 0, _defaultWidth, _defaultHeight);
                shape.FillColor = _defaultFillColor.ToColor();
                shape.BorderColor = _defaultBorderColor.ToColor();
                shape.TextColor = _defaultTextColor.ToColor();
                return shape;
            }
            else
            {
                GenericShape shape = new GenericShape();
                shape.ShapeTypeName = _name;
                shape.Name = _name;
                shape.Bounds = new RectangleF(0, 0, _defaultWidth, _defaultHeight);
                shape.FillColor = _defaultFillColor.ToColor();
                shape.BorderColor = _defaultBorderColor.ToColor();
                shape.TextColor = _defaultTextColor.ToColor();
                shape.Resizable = _resizable;
                shape.MemberAreaTop = _nameAreaTop;

                foreach (ShapeState state in _defaultStates)
                {
                    shape.AddState(state);
                }
                if (_defaultStates.Count > 0)
                {
                    shape.CurrentStateName = _defaultStates[0].Name;
                }

                return shape;
            }
        }
    }
}
