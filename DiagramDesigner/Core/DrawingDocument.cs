using System;
using System.Collections.Generic;
using System.Drawing;

namespace DiagramDesigner.Core
{
    [Serializable]
    public class DrawingDocument
    {
        private List<ShapeBase> _shapes = new List<ShapeBase>();
        private List<Connection> _connections = new List<Connection>();
        private string _name = "未命名";
        private SizeF _pageSize = new SizeF(2000, 1500);
        private CanvasConfig _config = new CanvasConfig();

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public SizeF PageSize
        {
            get { return _pageSize; }
            set { _pageSize = value; }
        }

        /// <summary>
        /// 画布配置，随文档一起序列化存储。
        /// 包含编辑器 UI 状态（工具栏、属性面板、主题等）
        /// </summary>
        public CanvasConfig Config
        {
            get { return _config; }
            set { _config = value; }
        }

        public List<ShapeBase> Shapes { get { return _shapes; } }
        public List<Connection> Connections { get { return _connections; } }

        public void AddShape(ShapeBase shape)
        {
            if (shape == null)
                return;
            _shapes.Add(shape);
            shape.ZOrder = _shapes.Count;
            OnDocumentChanged(EventArgs.Empty);
        }

        public void RemoveShape(ShapeBase shape)
        {
            if (shape == null)
                return;

            List<Connection> toRemove = new List<Connection>();
            foreach (Connection conn in _connections)
            {
                if (conn.FromShape == shape || conn.ToShape == shape)
                    toRemove.Add(conn);
            }
            foreach (Connection conn in toRemove)
                _connections.Remove(conn);

            if (shape.Parent is DiagramDesigner.Shapes.ContainerShape)
            {
                DiagramDesigner.Shapes.ContainerShape container = (DiagramDesigner.Shapes.ContainerShape)shape.Parent;
                container.RemoveChild(shape);
            }

            if (shape is DiagramDesigner.Shapes.ContainerShape)
            {
                DiagramDesigner.Shapes.ContainerShape c = (DiagramDesigner.Shapes.ContainerShape)shape;
                List<ShapeBase> childrenCopy = new List<ShapeBase>(c.Children);
                for (int i = childrenCopy.Count - 1; i >= 0; i--)
                {
                    RemoveShape(childrenCopy[i]);
                }
            }

            _shapes.Remove(shape);
            OnDocumentChanged(EventArgs.Empty);
        }

        public void AddConnection(Connection conn)
        {
            if (conn == null)
                return;
            _connections.Add(conn);
            OnDocumentChanged(EventArgs.Empty);
        }

        public void RemoveConnection(Connection conn)
        {
            if (conn == null)
                return;
            _connections.Remove(conn);
            OnDocumentChanged(EventArgs.Empty);
        }

        public void ClearSelection()
        {
            foreach (ShapeBase shape in _shapes)
                shape.Selected = false;
            foreach (Connection conn in _connections)
                conn.Selected = false;
        }

        public List<ShapeBase> GetSelectedShapes()
        {
            List<ShapeBase> result = new List<ShapeBase>();
            foreach (ShapeBase s in _shapes)
            {
                if (s.Selected)
                    result.Add(s);
            }
            return result;
        }

        public List<Connection> GetSelectedConnections()
        {
            List<Connection> result = new List<Connection>();
            foreach (Connection c in _connections)
            {
                if (c.Selected)
                    result.Add(c);
            }
            return result;
        }

        public void BringToFront(ShapeBase shape)
        {
            if (shape == null || !_shapes.Contains(shape))
                return;
            int maxZ = 0;
            foreach (ShapeBase s in _shapes)
            {
                if (s.ZOrder > maxZ)
                    maxZ = s.ZOrder;
            }
            shape.ZOrder = maxZ + 1;
            SortByZOrder();
            OnDocumentChanged(EventArgs.Empty);
        }

        public void SendToBack(ShapeBase shape)
        {
            if (shape == null || !_shapes.Contains(shape))
                return;
            int minZ = 0;
            foreach (ShapeBase s in _shapes)
            {
                if (s.ZOrder < minZ)
                    minZ = s.ZOrder;
            }
            shape.ZOrder = minZ - 1;
            SortByZOrder();
            OnDocumentChanged(EventArgs.Empty);
        }

        private void SortByZOrder()
        {
            _shapes.Sort(new ZOrderComparer());
        }

        private class ZOrderComparer : IComparer<ShapeBase>
        {
            public int Compare(ShapeBase a, ShapeBase b)
            {
                return a.ZOrder.CompareTo(b.ZOrder);
            }
        }

        public ShapeBase HitTestShape(PointF pt)
        {
            for (int i = _shapes.Count - 1; i >= 0; i--)
            {
                if (_shapes[i].HitTest(pt))
                    return _shapes[i];
            }
            return null;
        }

        public Connection HitTestConnection(PointF pt, float tolerance)
        {
            foreach (Connection conn in _connections)
            {
                if (conn.HitTest(pt, tolerance))
                    return conn;
            }
            return null;
        }

        public List<ShapeBase> GetShapesInRect(RectangleF rect)
        {
            List<ShapeBase> result = new List<ShapeBase>();
            foreach (ShapeBase s in _shapes)
            {
                if (s.HitTest(rect))
                    result.Add(s);
            }
            return result;
        }

        public void Clear()
        {
            _shapes.Clear();
            _connections.Clear();
            OnDocumentChanged(EventArgs.Empty);
        }

        public event EventHandler DocumentChanged;

        protected void OnDocumentChanged(EventArgs e)
        {
            if (DocumentChanged != null)
                DocumentChanged(this, e);
        }
    }
}
