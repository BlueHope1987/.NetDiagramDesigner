using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DiagramDesigner.Config;
using DiagramDesigner.Core;
using DiagramDesigner.Shapes;

namespace DiagramDesigner.Controls
{
    /// <summary>
    /// DrawingCanvas 的交互逻辑分部类：拖放接收、键盘快捷键。
    /// 鼠标交互（按下/移动/抬起/滚轮/双击）见本文件后续区域。
    /// </summary>
    public partial class DrawingCanvas : Control
    {
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
                // Zone 命中测试：连接区域和点击区域拦截鼠标事件
                GenericShape gs = shape as GenericShape;
                if (gs != null)
                {
                    ShapeZone zone = gs.HitTestZone(worldPos);
                    if (zone != null && (zone.IsConnectionZone || zone.IsClickZone))
                    {
                        // 选中图形
                        if (!shape.Selected && (Control.ModifierKeys & Keys.Control) != Keys.Control)
                            _document.ClearSelection();
                        shape.Selected = true;
                        OnSelectionChanged();

                        if (zone.IsConnectionZone)
                        {
                            // 从连接区域开始连线
                            _connectStartShape = shape;
                            _connectionStartZone = zone;
                            _connectStartPoint = worldPos;
                            _connectCurrentPoint = worldPos;
                            _isConnecting = true;
                            Invalidate();
                            return;
                        }
                        else // IsClickZone
                        {
                            // 触发点击区域行为
                            if (ZoneClicked != null)
                                ZoneClicked(this, new ZoneClickEventArgs(gs, zone));
                            Invalidate();
                            return;
                        }
                    }
                }

                bool ctrlKey = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                if (ctrlKey)
                {
                    // Ctrl+点击：反选（切换选中状态）
                    shape.Selected = !shape.Selected;
                }
                else
                {
                    if (!shape.Selected)
                        _document.ClearSelection();
                    shape.Selected = true;
                }

                // 如果反选后该图形被取消选中，直接刷新并返回
                if (ctrlKey && !shape.Selected)
                {
                    OnSelectionChanged();
                    Invalidate();
                    return;
                }

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
                // Ctrl+空白：保留已有选中，开始框选（反选模式）
                if ((Control.ModifierKeys & Keys.Control) != Keys.Control)
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
                // 拖动中不发 DocumentModified，松开鼠标时统一触发
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

                // 拖动中不发 DocumentModified，松开鼠标时统一触发
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
                // Shape 尺寸变化后重建空间索引
                _document.Reindex();
                // 拖动结束后统一触发文档修改通知
                OnDocumentModified();
                ChangeSet commit = new ChangeSet();
                commit.Kind = ChangeKind.DocumentCommitted;
                commit.ShapeId = Guid.Empty;
                commit.Timestamp = DateTime.Now;
                DispatchChange(commit);
                Invalidate();
            }

            if (_isDragging)
            {
                _isDragging = false;

                UpdateContainerMembership(_draggingShapes);
                _draggingShapes.Clear();
                _dragOriginalPositions = null;
                // Shape 位置变化后重建空间索引
                _document.Reindex();
                // 拖动结束后统一触发文档修改通知
                OnDocumentModified();
                ChangeSet commit = new ChangeSet();
                commit.Kind = ChangeKind.DocumentCommitted;
                commit.ShapeId = Guid.Empty;
                commit.Timestamp = DateTime.Now;
                DispatchChange(commit);
                Invalidate();
            }

            if (_isConnecting)
            {
                EndConnection(ScreenToWorld(e.Location));
            }

            if (_isSelecting)
            {
                _isSelecting = false;
                bool ctrlKey = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                List<ShapeBase> shapes = _document.GetShapesInRect(_selectionRect);
                foreach (ShapeBase s in shapes)
                {
                    if (ctrlKey)
                        s.Selected = !s.Selected;  // Ctrl 框选：反选
                    else
                        s.Selected = true;
                }
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

        /// <summary>
        /// 双击事件：在标题 Zone 上启动内联标题编辑，
        /// 在成员上启动内联成员编辑。
        /// </summary>
        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            MouseEventArgs me = (MouseEventArgs)e;
            if (me.Button != MouseButtons.Left)
                return;

            PointF worldPos = ScreenToWorld(me.Location);
            ShapeBase shape = _document.HitTestShape(worldPos);
            GenericShape gs = shape as GenericShape;
            if (gs == null)
                return;

            // 标题 Zone 双击 → 内联编辑标题（仅设计时）
            ShapeZone zone = gs.HitTestZone(worldPos);
            if (zone == null)
            {
                // 非功能 Zone 也检查标题 Zone（HitTestZone 只返回功能 Zone）
                zone = gs.FindZoneByName("Title");
            }
            if (zone != null && zone.IsTitleZone && _config.DesignMode)
            {
                RectangleF titleRect = gs.GetTitleZoneBounds();
                if (titleRect.Contains(worldPos))
                {
                    StartInlineEditTitle(gs);
                    return;
                }
            }

            // 成员双击 → 内联编辑成员
            int memberIdx = gs.HitTestMember(worldPos);
            if (memberIdx >= 0)
            {
                StartInlineEditMember(gs, memberIdx);
                return;
            }
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
            else if (e.Control && e.KeyCode == Keys.Z)
            {
                Undo();
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                Redo();
            }
        }
    }
}
