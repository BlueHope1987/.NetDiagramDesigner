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
        private List<ShapeZone> _zones = new List<ShapeZone>();

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

        /// <summary>
        /// 该图形类型的 Zone 列表。每个 ShapeType 应至少包含一个
        /// IsTitleZone=true 的标题 Zone 用于显示名称。
        /// 若 SupportsMembers=true，还应包含一个成员 Zone。
        /// </summary>
        public List<ShapeZone> Zones
        {
            get { return _zones; }
            set { _zones = value; }
        }

        public ShapeType() { }

        /// <summary>
        /// 确保该 ShapeType 拥有默认的标题 Zone 和（若支持成员）成员 Zone。
        /// 若已有同名的 Zone 则不重复创建。
        /// 标题 Zone 的锚定方式由 NameAlignment 决定：
        ///   Center（默认）→ MiddleCenter，适用于大多数图形
        ///   TopCenter     → TopCenter + 向下偏移，适用于类图等
        ///   TopLeft       → TopLeft + 向内偏移，适用于包图等
        /// 容器（IsContainer=true）默认使用 TopCenter。
        /// </summary>
        public void EnsureDefaultZones()
        {
            // 确保有标题 Zone
            bool hasTitleZone = false;
            foreach (ShapeZone z in _zones)
            {
                if (z.IsTitleZone)
                {
                    hasTitleZone = true;
                    break;
                }
            }
            if (!hasTitleZone)
            {
                ShapeZone titleZone;
                if (_nameAlignment == NameAlignment.TopLeft)
                    titleZone = ShapeZone.CreateTopLeftTitleZone(_nameAreaTop);
                else if (_nameAlignment == NameAlignment.TopCenter || _isContainer)
                    titleZone = ShapeZone.CreateTopTitleZone(_nameAreaTop);
                else
                    titleZone = ShapeZone.CreateDefaultTitleZone(_nameAreaTop);
                _zones.Insert(0, titleZone);
            }

            // 若支持成员且有成员区，确保有成员 Zone
            if (_supportsMembers)
            {
                bool hasMemberZone = false;
                foreach (ShapeZone z in _zones)
                {
                    if (z.IsMemberZone)
                    {
                        hasMemberZone = true;
                        break;
                    }
                }
                if (!hasMemberZone)
                {
                    _zones.Add(ShapeZone.CreateDefaultMemberZone(_nameAreaTop));
                }
            }
        }

        /// <summary>
        /// 根据 Zone 列表生成系统行为。
        /// 标题 Zone → InlineEditTitle 行为（设计时）
        /// 成员 Zone → AddMember / DeleteMember 行为（设计时 + 运行时）
        /// 点击 Zone → ZoneClick 行为
        /// 连接 Zone → ZoneConnect 行为
        /// 系统行为不可删除，可切换显隐。
        /// </summary>
        public void GenerateSystemBehaviors()
        {
            // 移除已有的系统行为（根据 ZoneName 匹配）
            for (int i = _customActions.Count - 1; i >= 0; i--)
            {
                if (_customActions[i].IsSystemBehavior && _customActions[i].ZoneName.Length > 0)
                    _customActions.RemoveAt(i);
            }

            EnsureDefaultZones();

            foreach (ShapeZone zone in _zones)
            {
                if (zone.IsTitleZone)
                {
                    ShapeAction a = ShapeAction.CreateSystemBehavior(
                        "编辑标题", ShapeActionType.InlineEditTitle, zone.Name, "edit.png");
                    _customActions.Add(a);
                }
                else if (zone.IsMemberZone)
                {
                    ShapeAction addMem = ShapeAction.CreateSystemBehavior(
                        "添加成员", ShapeActionType.AddMember, zone.Name, "add_member.png");
                    _customActions.Add(addMem);

                    ShapeAction delMem = ShapeAction.CreateSystemBehavior(
                        "删除成员", ShapeActionType.DeleteMember, zone.Name, "delete.png");
                    _customActions.Add(delMem);
                }
                else if (zone.IsClickZone)
                {
                    ShapeAction a = ShapeAction.CreateSystemBehavior(
                        "点击: " + zone.Name, ShapeActionType.ZoneClick, zone.Name, "");
                    _customActions.Add(a);
                }
                else if (zone.IsConnectionZone)
                {
                    ShapeAction a = ShapeAction.CreateSystemBehavior(
                        "连接: " + zone.Name, ShapeActionType.ZoneConnect, zone.Name, "");
                    _customActions.Add(a);
                }
            }
        }

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
                shape.RefWidth = _defaultWidth;
                shape.RefHeight = _defaultHeight;

                // 确保有默认 Zone 并传递给实例
                EnsureDefaultZones();
                foreach (ShapeZone zone in _zones)
                {
                    shape.Zones.Add(zone.Clone());
                }

                // 生成系统行为并传递给实例
                GenerateSystemBehaviors();
                foreach (ShapeAction action in _customActions)
                {
                    shape.SystemActions.Add(action.Clone());
                }

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
