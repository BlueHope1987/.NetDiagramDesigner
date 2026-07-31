using System;
using DiagramDesigner.Shapes;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 图形添加/删除事件参数。携带被操作的图形及其类型名、显示名，
    /// 供宿主在 ShapeAdded / ShapeDeleted 事件中获取上下文信息。
    /// </summary>
    public class ShapeEventArgs : EventArgs
    {
        private ShapeBase _shape;
        private string _shapeTypeName;
        private string _shapeName;

        /// <summary>被操作的图形实例</summary>
        public ShapeBase Shape
        {
            get { return _shape; }
            set { _shape = value; }
        }

        /// <summary>图形类型名（对应 ShapeType.Name），适用于 GenericShape</summary>
        public string ShapeTypeName
        {
            get { return _shapeTypeName; }
            set { _shapeTypeName = value; }
        }

        /// <summary>图形显示名称（对应 ShapeBase.Name）</summary>
        public string ShapeName
        {
            get { return _shapeName; }
            set { _shapeName = value; }
        }

        public ShapeEventArgs(ShapeBase shape)
        {
            _shape = shape;
            _shapeName = (shape != null) ? shape.Name : "";
            _shapeTypeName = "";
            GenericShape gs = shape as GenericShape;
            if (gs != null)
                _shapeTypeName = gs.ShapeTypeName;
        }
    }

    /// <summary>
    /// 连线添加/删除事件参数。携带被操作的连线及其两端图形名称。
    /// </summary>
    public class ConnectionEventArgs : EventArgs
    {
        private Connection _connection;
        private string _fromShapeName;
        private string _toShapeName;

        /// <summary>被操作的连线实例</summary>
        public Connection Connection
        {
            get { return _connection; }
            set { _connection = value; }
        }

        /// <summary>起始图形显示名称</summary>
        public string FromShapeName
        {
            get { return _fromShapeName; }
            set { _fromShapeName = value; }
        }

        /// <summary>目标图形显示名称</summary>
        public string ToShapeName
        {
            get { return _toShapeName; }
            set { _toShapeName = value; }
        }

        public ConnectionEventArgs(Connection conn)
        {
            _connection = conn;
            _fromShapeName = (conn != null && conn.FromShape != null) ? conn.FromShape.Name : "";
            _toShapeName = (conn != null && conn.ToShape != null) ? conn.ToShape.Name : "";
        }
    }

    /// <summary>
    /// 图形状态切换事件参数。携带图形实例及切换前后的状态名，
    /// 供宿主在 ShapeStateChanged 事件中响应状态变化。
    /// </summary>
    public class ShapeStateChangedEventArgs : EventArgs
    {
        private ShapeBase _shape;
        private string _oldStateName;
        private string _newStateName;

        /// <summary>发生状态切换的图形实例</summary>
        public ShapeBase Shape
        {
            get { return _shape; }
            set { _shape = value; }
        }

        /// <summary>切换前的状态名</summary>
        public string OldStateName
        {
            get { return _oldStateName; }
            set { _oldStateName = value; }
        }

        /// <summary>切换后的状态名</summary>
        public string NewStateName
        {
            get { return _newStateName; }
            set { _newStateName = value; }
        }

        public ShapeStateChangedEventArgs(ShapeBase shape, string oldStateName, string newStateName)
        {
            _shape = shape;
            _oldStateName = oldStateName;
            _newStateName = newStateName;
        }
    }
}
