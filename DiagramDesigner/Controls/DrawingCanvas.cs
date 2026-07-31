using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DiagramDesigner.Config;
using DiagramDesigner.Core;
using DiagramDesigner.Shapes;

namespace DiagramDesigner.Controls
{
    public enum CanvasTool
    {
        Select,
        Connect
    }

    public partial class DrawingCanvas : Control
    {
        private DrawingDocument _document = new DrawingDocument();
        private float _zoom = 1.0f;
        private PointF _offset = new PointF(0, 0);
        private CanvasTool _currentTool = CanvasTool.Select;
        private bool _isDragging = false;
        private bool _isConnecting = false;
        private bool _isSelecting = false;
        private PointF _dragStart;
        private PointF _lastMousePos;
        private ShapeBase _dragShape;
        private List<ShapeBase> _draggingShapes = new List<ShapeBase>();
        private PointF[] _dragOriginalPositions;
        private ShapeBase _connectStartShape;
        private PointF _connectStartPoint;
        private PointF _connectCurrentPoint;
        private RectangleF _selectionRect;
        private ShapeBase _hoveredShape;
        private Connection _hoveredConnection;
        private bool _isResizing = false;
        private ResizeHandle _resizeHandle = ResizeHandle.None;
        private ShapeBase _resizeShape = null;
        private RectangleF _resizeOriginalBounds;
        private PointF _resizeStartPoint;
        private CanvasConfig _config = new CanvasConfig();

        public DrawingCanvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            this.BackColor = GlobalConfig.Instance.CanvasBackground;
            this.AllowDrop = true;
            this.Focus();

            GlobalConfig.Instance.Changed += new EventHandler(OnGlobalConfigChanged);
            _document.DocumentChanged += new EventHandler(OnDocumentChanged);
        }

        private void OnGlobalConfigChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        private void OnDocumentChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        public DrawingDocument Document { get { return _document; } }

        /// <summary>
        /// 画布配置。宿主可通过此对象读取/设置编辑器的 UI 状态和工具配置。
        /// 加载画布时先设置 Config，再将其传递给 DiagramEditor 初始化。
        /// </summary>
        public CanvasConfig Config
        {
            get { return _config; }
            set { _config = value; }
        }

        public CanvasTool CurrentTool
        {
            get { return _currentTool; }
            set
            {
                _currentTool = value;
                if (value == CanvasTool.Select)
                    Cursor = Cursors.Default;
                else if (value == CanvasTool.Connect)
                    Cursor = Cursors.Cross;
            }
        }

        public float Zoom
        {
            get { return _zoom; }
            set
            {
                _zoom = value;
                if (_zoom < 0.1f)
                    _zoom = 0.1f;
                if (_zoom > 5.0f)
                    _zoom = 5.0f;
                Invalidate();
            }
        }

        public PointF Offset
        {
            get { return _offset; }
            set { _offset = value; Invalidate(); }
        }

        public event EventHandler SelectionChanged;
        public event EventHandler DocumentModified;

        /// <summary>图形添加到画布时触发，携带图形类型名与显示名</summary>
        public event EventHandler<ShapeEventArgs> ShapeAdded;
        /// <summary>图形从画布删除时触发</summary>
        public event EventHandler<ShapeEventArgs> ShapeDeleted;
        /// <summary>连线创建时触发，携带两端图形名称</summary>
        public event EventHandler<ConnectionEventArgs> ConnectionAdded;
        /// <summary>连线删除时触发</summary>
        public event EventHandler<ConnectionEventArgs> ConnectionDeleted;

        public void OnSelectionChanged()
        {
            if (SelectionChanged != null)
                SelectionChanged(this, EventArgs.Empty);
        }

        protected virtual void OnDocumentModified()
        {
            if (DocumentModified != null)
                DocumentModified(this, EventArgs.Empty);
        }

        /// <summary>触发 ShapeAdded 事件</summary>
        protected virtual void OnShapeAdded(ShapeBase shape)
        {
            if (ShapeAdded != null)
                ShapeAdded(this, new ShapeEventArgs(shape));
        }

        /// <summary>触发 ShapeDeleted 事件</summary>
        protected virtual void OnShapeDeleted(ShapeBase shape)
        {
            if (ShapeDeleted != null)
                ShapeDeleted(this, new ShapeEventArgs(shape));
        }

        /// <summary>触发 ConnectionAdded 事件</summary>
        protected virtual void OnConnectionAdded(Connection conn)
        {
            if (ConnectionAdded != null)
                ConnectionAdded(this, new ConnectionEventArgs(conn));
        }

        /// <summary>触发 ConnectionDeleted 事件</summary>
        protected virtual void OnConnectionDeleted(Connection conn)
        {
            if (ConnectionDeleted != null)
                ConnectionDeleted(this, new ConnectionEventArgs(conn));
        }

        /// <summary>
        /// 鼠标按下事件分发。按优先级判断：
        /// 中键平移 → Connect工具连线 → ResizeHandle调整尺寸 →
        /// Shape选中/拖拽 → Connection选中 → 空白框选
        /// </summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            this.Focus();

            PointF worldPos = ScreenToWorld(e.Location);

            if (e.Button == MouseButtons.Middle)
            {
                _lastMousePos = new PointF(e.X, e.Y);
                Cursor = Cursors.Hand;
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            if (_currentTool == CanvasTool.Connect)
            {
                StartConnection(worldPos);
                return;
            }

            ShapeBase shape = _document.HitTestShape(worldPos);
            Connection conn = _document.HitTestConnection(worldPos, 6f / _zoom);

            if (shape != null)
            {
                if (!shape.Selected && (Control.ModifierKeys & Keys.Control) != Keys.Control)
                    _document.ClearSelection();

                shape.Selected = true;

                ResizeHandle handle = shape.HitTestResizeHandle(worldPos, 8f / _zoom);
                if (handle != ResizeHandle.None)
                {
                    _isResizing = true;
                    _resizeHandle = handle;
                    _resizeShape = shape;
                    _resizeOriginalBounds = shape.Bounds;
                    _resizeStartPoint = worldPos;
                    OnSelectionChanged();
                    Invalidate();
                    return;
                }

                _dragShape = shape;
                _isDragging = true;
                _dragStart = worldPos;

                _draggingShapes = _document.GetSelectedShapes();

                _draggingShapes = ExpandWithChildren(_draggingShapes);

                _dragOriginalPositions = new PointF[_draggingShapes.Count];
                for (int i = 0; i < _draggingShapes.Count; i++)
                {
                    ShapeBase s = _draggingShapes[i];
                    _dragOriginalPositions[i] = new PointF(s.X, s.Y);
                }

                OnSelectionChanged();
            }
            else if (conn != null)
            {
                _document.ClearSelection();
                conn.Selected = true;
                OnSelectionChanged();
            }
            else
            {
                _document.ClearSelection();
                _isSelecting = true;
                _dragStart = worldPos;
                _selectionRect = new RectangleF(worldPos.X, worldPos.Y, 0, 0);
                OnSelectionChanged();
            }

            Invalidate();
        }

        private List<ShapeBase> ExpandWithChildren(List<ShapeBase> shapes)
        {
            List<ShapeBase> result = new List<ShapeBase>();
            for (int i = 0; i < shapes.Count; i++)
            {
                ShapeBase s = shapes[i];
                if (!result.Contains(s))
                    result.Add(s);
                if (s is ContainerShape)
                {
                    ContainerShape cs = (ContainerShape)s;
                    for (int j = 0; j < cs.Children.Count; j++)
                    {
                        ShapeBase child = cs.Children[j];
                        if (!result.Contains(child))
                            result.Add(child);
                    }
                }
            }
            return result;
        }

        private void UpdateContainerMembership(List<ShapeBase> movedShapes)
        {
            for (int i = 0; i < movedShapes.Count; i++)
            {
                ShapeBase shape = movedShapes[i];
                if (shape is ContainerShape)
                    continue;

                ShapeBase oldParent = shape.Parent;
                ContainerShape newParent = null;

                RectangleF shapeRect = shape.Bounds;

                for (int j = 0; j < _document.Shapes.Count; j++)
                {
                    ShapeBase candidate = _document.Shapes[j];
                    if (candidate == shape)
                        continue;
                    if (candidate is ContainerShape)
                    {
                        ContainerShape container = (ContainerShape)candidate;
                        if (IsFullyInsideContainer(shapeRect, container))
                        {
                            if (newParent == null ||
                                container.ZOrder > newParent.ZOrder)
                            {
                                newParent = container;
                            }
                        }
                    }
                }

                if (oldParent != newParent)
                {
                    if (oldParent is ContainerShape)
                    {
                        ((ContainerShape)oldParent).RemoveChild(shape);
                    }

                    if (newParent != null)
                    {
                        newParent.AddChild(shape);
                    }
                    else
                    {
                        shape.Parent = null;
                    }
                }
            }
        }

        private bool IsFullyInsideContainer(RectangleF shapeRect, ContainerShape container)
        {
            RectangleF headerRect = container.Bounds;
            headerRect.Height = container.HeaderHeight;
            RectangleF bodyRect = container.Bounds;
            bodyRect.Y += container.HeaderHeight;
            bodyRect.Height -= container.HeaderHeight;

            if (shapeRect.X < bodyRect.X + 4 ||
                shapeRect.Y < bodyRect.Y + 4 ||
                shapeRect.Right > bodyRect.Right - 4 ||
                shapeRect.Bottom > bodyRect.Bottom - 4)
                return false;

            return true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            PointF worldPos = ScreenToWorld(e.Location);

            if (e.Button == MouseButtons.Middle)
            {
                _offset.X += e.X - _lastMousePos.X;
                _offset.Y += e.Y - _lastMousePos.Y;
                _lastMousePos = new PointF(e.X, e.Y);
                Invalidate();
                return;
            }

            if (_isResizing && _resizeShape != null)
            {
                float dx = worldPos.X - _resizeStartPoint.X;
                float dy = worldPos.Y - _resizeStartPoint.Y;

                float newX = _resizeOriginalBounds.X;
                float newY = _resizeOriginalBounds.Y;
                float newW = _resizeOriginalBounds.Width;
                float newH = _resizeOriginalBounds.Height;

                if (_resizeHandle == ResizeHandle.TopLeft ||
                    _resizeHandle == ResizeHandle.MiddleLeft ||
                    _resizeHandle == ResizeHandle.BottomLeft)
                {
                    newX = _resizeOriginalBounds.X + dx;
                    newW = _resizeOriginalBounds.Width - dx;
                }
                if (_resizeHandle == ResizeHandle.TopRight ||
                    _resizeHandle == ResizeHandle.MiddleRight ||
                    _resizeHandle == ResizeHandle.BottomRight)
                {
                    newW = _resizeOriginalBounds.Width + dx;
                }
                if (_resizeHandle == ResizeHandle.TopLeft ||
                    _resizeHandle == ResizeHandle.TopCenter ||
                    _resizeHandle == ResizeHandle.TopRight)
                {
                    newY = _resizeOriginalBounds.Y + dy;
                    newH = _resizeOriginalBounds.Height - dy;
                }
                if (_resizeHandle == ResizeHandle.BottomLeft ||
                    _resizeHandle == ResizeHandle.BottomCenter ||
                    _resizeHandle == ResizeHandle.BottomRight)
                {
                    newH = _resizeOriginalBounds.Height + dy;
                }

                float minW = _resizeShape.MinWidth;
                float minH = _resizeShape.MinHeight;

                if (newW < minW)
                {
                    if (_resizeHandle == ResizeHandle.TopLeft ||
                        _resizeHandle == ResizeHandle.MiddleLeft ||
                        _resizeHandle == ResizeHandle.BottomLeft)
                    {
                        newX = _resizeOriginalBounds.Right - minW;
                    }
                    newW = minW;
                }
                if (newH < minH)
                {
                    if (_resizeHandle == ResizeHandle.TopLeft ||
                        _resizeHandle == ResizeHandle.TopCenter ||
                        _resizeHandle == ResizeHandle.TopRight)
                    {
                        newY = _resizeOriginalBounds.Bottom - minH;
                    }
                    newH = minH;
                }

                if (GlobalConfig.Instance.SnapToGrid)
                {
                    float grid = GlobalConfig.Instance.GridSize;
                    newX = (float)Math.Round(newX / grid) * grid;
                    newY = (float)Math.Round(newY / grid) * grid;
                    newW = (float)Math.Round(newW / grid) * grid;
                    newH = (float)Math.Round(newH / grid) * grid;
                    if (newW < minW) newW = minW;
                    if (newH < minH) newH = minH;
                }

                _resizeShape.Bounds = new RectangleF(newX, newY, newW, newH);
                OnDocumentModified();
                Invalidate();
            }
            else if (_isDragging && _draggingShapes.Count > 0)
            {
                float dx = worldPos.X - _dragStart.X;
                float dy = worldPos.Y - _dragStart.Y;

                for (int i = 0; i < _draggingShapes.Count; i++)
                {
                    ShapeBase s = _draggingShapes[i];
                    float newX = _dragOriginalPositions[i].X + dx;
                    float newY = _dragOriginalPositions[i].Y + dy;

                    if (GlobalConfig.Instance.SnapToGrid)
                    {
                        float grid = GlobalConfig.Instance.GridSize;
                        newX = (float)Math.Round(newX / grid) * grid;
                        newY = (float)Math.Round(newY / grid) * grid;
                    }

                    s.X = newX;
                    s.Y = newY;
                }

                OnDocumentModified();
                Invalidate();
            }
            else if (_isConnecting)
            {
                _connectCurrentPoint = worldPos;
                Invalidate();
            }
            else if (_isSelecting)
            {
                float x = Math.Min(_dragStart.X, worldPos.X);
                float y = Math.Min(_dragStart.Y, worldPos.Y);
                float w = Math.Abs(worldPos.X - _dragStart.X);
                float h = Math.Abs(worldPos.Y - _dragStart.Y);
                _selectionRect = new RectangleF(x, y, w, h);
                Invalidate();
            }
            else
            {
                ShapeBase shape = _document.HitTestShape(worldPos);
                Connection conn = _document.HitTestConnection(worldPos, 6f / _zoom);

                bool needInvalidate = false;
                if (shape != _hoveredShape)
                {
                    if (_hoveredShape != null)
                        _hoveredShape.Hovered = false;
                    _hoveredShape = shape;
                    if (_hoveredShape != null)
                        _hoveredShape.Hovered = true;
                    needInvalidate = true;
                }
                if (conn != _hoveredConnection)
                {
                    _hoveredConnection = conn;
                    needInvalidate = true;
                }

                if (needInvalidate)
                    Invalidate();

                if (_currentTool == CanvasTool.Connect)
                {
                    Cursor = (shape != null) ? Cursors.Cross : Cursors.Default;
                }
                else
                {
                    if (shape != null && shape.Selected && shape.Resizable)
                    {
                        ResizeHandle rh = shape.HitTestResizeHandle(worldPos, 8f / _zoom);
                        if (rh != ResizeHandle.None)
                        {
                            Cursor = ShapeBase.GetResizeCursor(rh);
                        }
                        else
                        {
                            Cursor = Cursors.SizeAll;
                        }
                    }
                    else if (shape != null)
                    {
                        Cursor = Cursors.SizeAll;
                    }
                    else
                    {
                        Cursor = Cursors.Default;
                    }
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Middle)
            {
                Cursor = _currentTool == CanvasTool.Connect ? Cursors.Cross : Cursors.Default;
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            if (_isResizing)
            {
                _isResizing = false;
                _resizeHandle = ResizeHandle.None;
                _resizeShape = null;
                Invalidate();
            }

            if (_isDragging)
            {
                _isDragging = false;
                _dragShape = null;

                UpdateContainerMembership(_draggingShapes);
                _draggingShapes.Clear();
                _dragOriginalPositions = null;
                Invalidate();
            }

            if (_isConnecting)
            {
                EndConnection(ScreenToWorld(e.Location));
            }

            if (_isSelecting)
            {
                _isSelecting = false;
                List<ShapeBase> shapes = _document.GetShapesInRect(_selectionRect);
                foreach (ShapeBase s in shapes)
                    s.Selected = true;
                if (shapes.Count > 0)
                    OnSelectionChanged();
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            float oldZoom = _zoom;
            float zoomDelta = e.Delta > 0 ? 1.1f : 0.9f;
            Zoom *= zoomDelta;

            PointF worldPos = ScreenToWorld(new Point(e.X, e.Y), oldZoom);
            _offset.X = e.X - worldPos.X * _zoom;
            _offset.Y = e.Y - worldPos.Y * _zoom;

            Invalidate();
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            if (e.Data.GetDataPresent(typeof(ToolboxItem)))
            {
                e.Effect = DragDropEffects.Copy;
                Invalidate();
            }
        }

        /// <summary>
        /// 拖放接收：从工具箱拖入图形类型，在鼠标位置创建实例。
        /// 支持网格吸附和容器自动归属检测。
        /// </summary>
        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);

            if (!e.Data.GetDataPresent(typeof(ToolboxItem)))
                return;

            ToolboxItem item = e.Data.GetData(typeof(ToolboxItem)) as ToolboxItem;
            if (item == null || item.CreateShape == null)
                return;

            Point clientPt = PointToClient(new Point(e.X, e.Y));
            PointF worldPos = ScreenToWorld(clientPt);

            ShapeBase shape = item.CreateShape();
            shape.X = worldPos.X - shape.Width / 2f;
            shape.Y = worldPos.Y - shape.Height / 2f;

            if (GlobalConfig.Instance.SnapToGrid)
            {
                float grid = GlobalConfig.Instance.GridSize;
                shape.X = (float)Math.Round(shape.X / grid) * grid;
                shape.Y = (float)Math.Round(shape.Y / grid) * grid;
            }

            _document.AddShape(shape);
            OnShapeAdded(shape);

            foreach (ShapeBase s in _document.Shapes)
            {
                if (s != shape && s is ContainerShape)
                {
                    ContainerShape container = (ContainerShape)s;
                    if (container.HitTest(worldPos))
                    {
                        container.AddChild(shape);
                        break;
                    }
                }
            }

            _document.ClearSelection();
            shape.Selected = true;
            OnSelectionChanged();
            OnDocumentModified();
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _document.ClearSelection();
                _isConnecting = false;
                _isDragging = false;
                _isResizing = false;
                _isSelecting = false;
                CurrentTool = CanvasTool.Select;
                OnSelectionChanged();
                Invalidate();
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (ShapeBase s in _document.Shapes)
                    s.Selected = true;
                OnSelectionChanged();
                Invalidate();
            }
        }

        private void StartConnection(PointF worldPos)
        {
            ShapeBase shape = _document.HitTestShape(worldPos);
            if (shape != null)
            {
                _connectStartShape = shape;
                _connectStartPoint = shape.GetNearestConnectionPoint(worldPos);
                _connectCurrentPoint = worldPos;
                _isConnecting = true;
            }
        }

        private void EndConnection(PointF worldPos)
        {
            _isConnecting = false;
            ShapeBase endShape = _document.HitTestShape(worldPos);

            if (_connectStartShape != null && endShape != null && _connectStartShape != endShape)
            {
                Connection conn = new Connection();
                conn.FromShape = _connectStartShape;
                conn.ToShape = endShape;
                conn.Mode = GlobalConfig.Instance.DefaultConnectionMode;
                conn.FromPoint = _connectStartShape.GetNearestConnectionPoint(endShape.Center);
                conn.ToPoint = endShape.GetNearestConnectionPoint(_connectStartShape.Center);
                _document.AddConnection(conn);
                OnConnectionAdded(conn);
                OnDocumentModified();
            }

            _connectStartShape = null;
            Invalidate();
        }

        public void DeleteSelected()
        {
            List<ShapeBase> shapes = _document.GetSelectedShapes();
            List<Connection> conns = _document.GetSelectedConnections();

            foreach (Connection c in conns)
            {
                _document.RemoveConnection(c);
                OnConnectionDeleted(c);
            }
            foreach (ShapeBase s in shapes)
            {
                _document.RemoveShape(s);
                OnShapeDeleted(s);
            }

            OnSelectionChanged();
            OnDocumentModified();
            Invalidate();
        }

        public new void BringToFront()
        {
            List<ShapeBase> shapes = _document.GetSelectedShapes();
            foreach (ShapeBase s in shapes)
                _document.BringToFront(s);
            Invalidate();
        }

        public new void SendToBack()
        {
            List<ShapeBase> shapes = _document.GetSelectedShapes();
            foreach (ShapeBase s in shapes)
                _document.SendToBack(s);
            Invalidate();
        }

        public PointF ScreenToWorld(Point screenPt, float? zoomOverride)
        {
            float z = zoomOverride.HasValue ? zoomOverride.Value : _zoom;
            return new PointF((screenPt.X - _offset.X) / z, (screenPt.Y - _offset.Y) / z);
        }

        public PointF ScreenToWorld(Point screenPt)
        {
            return new PointF((screenPt.X - _offset.X) / _zoom, (screenPt.Y - _offset.Y) / _zoom);
        }

        public Point WorldToScreen(PointF worldPt)
        {
            return new Point((int)(worldPt.X * _zoom + _offset.X), (int)(worldPt.Y * _zoom + _offset.Y));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
