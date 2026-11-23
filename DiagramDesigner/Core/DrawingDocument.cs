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

        // 空间索引：加速 Shape 命中测试，结构变化或 Shape 位置变化时需重建
        [NonSerialized]
        private SpatialGrid _shapeGrid = new SpatialGrid();
        [NonSerialized]
        private bool _gridDirty = true;

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
            _gridDirty = true;
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
            _gridDirty = true;
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

        /// <summary>确保空间索引为最新状态（反序列化后首次访问会自动重建）。</summary>
        private void EnsureGrid()
        {
            if (_shapeGrid == null)
                _shapeGrid = new SpatialGrid();
            if (_gridDirty || _shapeGrid.IsDirty)
            {
                _shapeGrid.Rebuild(_shapes);
                _gridDirty = false;
            }
        }

        /// <summary>
        /// 强制重建空间索引。当 Shape 位置发生外部变化（如拖动、缩放结束）后调用，
        /// 以保证命中测试返回正确结果。
        /// </summary>
        public void Reindex()
        {
            _gridDirty = true;
            EnsureGrid();
        }

        public ShapeBase HitTestShape(PointF pt)
        {
            EnsureGrid();
            // 按 ZOrder 倒序遍历命中单元格内的 Shape（后绘制的在上层）
            List<ShapeBase> candidates = _shapeGrid.Query(pt);
            if (candidates != null)
            {
                for (int i = candidates.Count - 1; i >= 0; i--)
                {
                    if (candidates[i].HitTest(pt))
                        return candidates[i];
                }
            }
            return null;
        }

        public Connection HitTestConnection(PointF pt, float tolerance)
        {
            // 连线数量通常远少于 Shape，保持线性扫描
            foreach (Connection conn in _connections)
            {
                if (conn.HitTest(pt, tolerance))
                    return conn;
            }
            return null;
        }

        public List<ShapeBase> GetShapesInRect(RectangleF rect)
        {
            EnsureGrid();
            List<ShapeBase> candidates = _shapeGrid.Query(rect);
            List<ShapeBase> result = new List<ShapeBase>();
            foreach (ShapeBase s in candidates)
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
            _gridDirty = true;
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
