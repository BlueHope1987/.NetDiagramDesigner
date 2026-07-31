using System;
using System.Collections.Generic;
using DiagramDesigner.Core;

namespace DiagramDesigner.Serialization
{
    [Serializable]
    public class DocumentData
    {
        private string _name = "未命名";
        private float _pageWidth = 2000f;
        private float _pageHeight = 1500f;
        private CanvasConfig _config = new CanvasConfig();
        private List<ShapeData> _shapes = new List<ShapeData>();
        private List<ConnectionData> _connections = new List<ConnectionData>();

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public float PageWidth
        {
            get { return _pageWidth; }
            set { _pageWidth = value; }
        }

        public float PageHeight
        {
            get { return _pageHeight; }
            set { _pageHeight = value; }
        }

        /// <summary>
        /// 画布配置，随文档一起序列化存储
        /// </summary>
        public CanvasConfig Config
        {
            get { return _config; }
            set { _config = value; }
        }

        public List<ShapeData> Shapes
        {
            get { return _shapes; }
            set { _shapes = value; }
        }

        public List<ConnectionData> Connections
        {
            get { return _connections; }
            set { _connections = value; }
        }
    }

    [Serializable]
    public class ShapeData
    {
        private string _shapeClass = "GenericShape";
        private string _id = Guid.NewGuid().ToString();
        private string _name = "";
        private string _description = "";
        private float _x = 0f;
        private float _y = 0f;
        private float _width = 120f;
        private float _height = 80f;
        private int _argbFillColor = -1;
        private int _argbBorderColor = -16777216;
        private int _argbTextColor = -16777216;
        private float _borderWidth = 1.5f;
        private int _zOrder = 0;
        private bool _visible = true;
        private string _parentId = "";
        private string _shapeTypeName = "";
        private bool _isContainer = false;
        private string _headerText = "容器";
        private float _headerHeight = 30f;
        private int _argbHeaderColor = -16777216;
        private List<MemberData> _members = new List<MemberData>();
        private List<StateData> _states = new List<StateData>();
        private string _currentStateName = "Normal";
        private float _memberAreaTop = 0.35f;

        public string ShapeClass
        {
            get { return _shapeClass; }
            set { _shapeClass = value; }
        }

        public string Id
        {
            get { return _id; }
            set { _id = value; }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public string Description
        {
            get { return _description; }
            set { _description = value; }
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

        public int ArgbFillColor
        {
            get { return _argbFillColor; }
            set { _argbFillColor = value; }
        }

        public int ArgbBorderColor
        {
            get { return _argbBorderColor; }
            set { _argbBorderColor = value; }
        }

        public int ArgbTextColor
        {
            get { return _argbTextColor; }
            set { _argbTextColor = value; }
        }

        public float BorderWidth
        {
            get { return _borderWidth; }
            set { _borderWidth = value; }
        }

        public int ZOrder
        {
            get { return _zOrder; }
            set { _zOrder = value; }
        }

        public bool Visible
        {
            get { return _visible; }
            set { _visible = value; }
        }

        public string ParentId
        {
            get { return _parentId; }
            set { _parentId = value; }
        }

        public string ShapeTypeName
        {
            get { return _shapeTypeName; }
            set { _shapeTypeName = value; }
        }

        public bool IsContainer
        {
            get { return _isContainer; }
            set { _isContainer = value; }
        }

        public string HeaderText
        {
            get { return _headerText; }
            set { _headerText = value; }
        }

        public float HeaderHeight
        {
            get { return _headerHeight; }
            set { _headerHeight = value; }
        }

        public int ArgbHeaderColor
        {
            get { return _argbHeaderColor; }
            set { _argbHeaderColor = value; }
        }

        public List<MemberData> Members
        {
            get { return _members; }
            set { _members = value; }
        }

        public List<StateData> States
        {
            get { return _states; }
            set { _states = value; }
        }

        public string CurrentStateName
        {
            get { return _currentStateName; }
            set { _currentStateName = value; }
        }

        public float MemberAreaTop
        {
            get { return _memberAreaTop; }
            set { _memberAreaTop = value; }
        }
    }

    [Serializable]
    public class MemberData
    {
        private string _memberType = "Property";
        private string _name = "";
        private string _typeName = "";
        private string _visibility = "Public";
        private bool _isStatic = false;
        private bool _isAbstract = false;
        private string _defaultValue = "";
        private List<ParameterData> _parameters = new List<ParameterData>();

        public string MemberType
        {
            get { return _memberType; }
            set { _memberType = value; }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public string TypeName
        {
            get { return _typeName; }
            set { _typeName = value; }
        }

        public string Visibility
        {
            get { return _visibility; }
            set { _visibility = value; }
        }

        public bool IsStatic
        {
            get { return _isStatic; }
            set { _isStatic = value; }
        }

        public bool IsAbstract
        {
            get { return _isAbstract; }
            set { _isAbstract = value; }
        }

        public string DefaultValue
        {
            get { return _defaultValue; }
            set { _defaultValue = value; }
        }

        public List<ParameterData> Parameters
        {
            get { return _parameters; }
            set { _parameters = value; }
        }
    }

    [Serializable]
    public class ParameterData
    {
        private string _name = "";
        private string _typeName = "";
        private string _defaultValue = "";

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public string TypeName
        {
            get { return _typeName; }
            set { _typeName = value; }
        }

        public string DefaultValue
        {
            get { return _defaultValue; }
            set { _defaultValue = value; }
        }
    }

    [Serializable]
    public class StateData
    {
        private string _name = "Normal";
        private int _argbFillColor = -1;
        private int _argbBorderColor = -16777216;
        private int _argbTextColor = -16777216;
        private int _argbHeaderColor = -16777216;
        private int _priority = 0;

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public int ArgbFillColor
        {
            get { return _argbFillColor; }
            set { _argbFillColor = value; }
        }

        public int ArgbBorderColor
        {
            get { return _argbBorderColor; }
            set { _argbBorderColor = value; }
        }

        public int ArgbTextColor
        {
            get { return _argbTextColor; }
            set { _argbTextColor = value; }
        }

        public int ArgbHeaderColor
        {
            get { return _argbHeaderColor; }
            set { _argbHeaderColor = value; }
        }

        public int Priority
        {
            get { return _priority; }
            set { _priority = value; }
        }
    }

    [Serializable]
    public class ConnectionData
    {
        private string _id = Guid.NewGuid().ToString();
        private string _fromShapeId = "";
        private string _toShapeId = "";
        private string _mode = "Straight";
        private int _argbLineColor = -8355712;
        private float _lineWidth = 1.5f;
        private string _dashStyle = "Solid";
        private bool _arrowAtEnd = true;
        private string _label = "";

        public string Id
        {
            get { return _id; }
            set { _id = value; }
        }

        public string FromShapeId
        {
            get { return _fromShapeId; }
            set { _fromShapeId = value; }
        }

        public string ToShapeId
        {
            get { return _toShapeId; }
            set { _toShapeId = value; }
        }

        public string Mode
        {
            get { return _mode; }
            set { _mode = value; }
        }

        public int ArgbLineColor
        {
            get { return _argbLineColor; }
            set { _argbLineColor = value; }
        }

        public float LineWidth
        {
            get { return _lineWidth; }
            set { _lineWidth = value; }
        }

        public string DashStyle
        {
            get { return _dashStyle; }
            set { _dashStyle = value; }
        }

        public bool ArrowAtEnd
        {
            get { return _arrowAtEnd; }
            set { _arrowAtEnd = value; }
        }

        public string Label
        {
            get { return _label; }
            set { _label = value; }
        }
    }
}
