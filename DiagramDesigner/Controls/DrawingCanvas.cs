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
        private ChangeHistory _history = new ChangeHistory();
        private float _zoom = 1.0f;
        private PointF _offset = new PointF(0, 0);
        private CanvasTool _currentTool = CanvasTool.Select;
        private bool _isDragging = false;
        private bool _isConnecting = false;
        private bool _isSelecting = false;
        private PointF _dragStart;
        private PointF _lastMousePos;
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

        // === Zone 交互与内联编辑 ===
        private TextBox _inlineEditBox;
        private GenericShape _inlineEditingShape;
        private int _inlineEditingMemberIndex = -1;
        private bool _isEndingInlineEdit = false;  // 重入保护
        private ShapeZone _connectionStartZone;

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
            // 主题色可能变化，使背景缓存失效
            InvalidateBackgroundCache();
            Invalidate();
        }

        private void OnDocumentChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            // 尺寸变化时背景缓存失效，下次绘制按新尺寸重生成
            InvalidateBackgroundCache();
            base.OnResize(e);
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

        /// <summary>是否正在拖动图形（供外部查询以避免高频回调）</summary>
        public bool IsDragging
        {
            get { return _isDragging; }
        }

        /// <summary>是否正在缩放图形（供外部查询以避免高频回调）</summary>
        public bool IsResizing
        {
            get { return _isResizing; }
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
        /// <summary>点击区域被点击时触发，携带图形和 Zone 信息</summary>
        public event EventHandler<ZoneClickEventArgs> ZoneClicked;

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

        // === Tier 2 增量变更通知 ===
        private bool _isSuppressing = false;
        private List<ChangeSet> _suppressedChanges = new List<ChangeSet>();
        private bool _isApplyingExternalChange = false;

        /// <summary>宿主正在通过 Editor API 进行外部变更时为 true。
        /// 期间触发的 ChangeSet 事件被静默吞掉，防止宿主回调-事件回弹的循环。</summary>
        public bool IsApplyingExternalChange
        {
            get { return _isApplyingExternalChange; }
        }

        /// <summary>
        /// 增量变更事件。ShapeBase.ShapeChanged 经画布汇聚后转发给宿主。
        /// 同时也用于 ShapeAdded/ShapeRemoved/ConnectionAdded/ConnectionRemoved 的 ChangeSet 通知。
        /// </summary>
        public event EventHandler<ChangeSet> ShapeChanged;
        public event EventHandler<ChangeSet> ConnectionChanged;
        public event EventHandler<BatchChangeSet> BatchChanged;
        public event EventHandler<ChangeSet> DocumentCommitted;

        /// <summary>
        /// 进入事件抑制作用域。期间所有 ChangeSet 累计到 _suppressedChanges，
        /// 退出 using 块时合并为一次 BatchChangeSet 触发。
        /// 典型用法: 宿主批量加载文档或批量替换图形时包裹。
        /// </summary>
        public IDisposable SuppressEvents()
        {
            return new EventSuppressor(this);
        }

        /// <summary>
        /// 进入"宿主外部变更"作用域。期间触发的 ChangeSet 被静默吞掉，
        /// 防止宿主回调 Editor API 时事件回弹造成循环。
        /// 典型用法: 宿主主动调用 UpdateShapeProperty 修改图形属性时包裹。
        /// </summary>
        public IDisposable BeginApplyExternalChange()
        {
            return new ExternalChangeScope(this);
        }

        /// <summary>是否可撤销。</summary>
        public bool CanUndo { get { return _history != null && _history.CanUndo; } }

        /// <summary>是否可重做。</summary>
        public bool CanRedo { get { return _history != null && _history.CanRedo; } }

        /// <summary>撤销最近一次变更。</summary>
        public void Undo()
        {
            if (_history == null || !_history.CanUndo)
                return;
            using (BeginApplyExternalChange())
            {
                _history.Undo(_document);
            }
            Invalidate();
            OnDocumentModified();
        }

        /// <summary>重做最近一次撤销的变更。</summary>
        public void Redo()
        {
            if (_history == null || !_history.CanRedo)
                return;
            using (BeginApplyExternalChange())
            {
                _history.Redo(_document);
            }
            Invalidate();
            OnDocumentModified();
        }

        /// <summary>清空撤销/重做栈（如新建/打开文档后）。</summary>
        public void ClearHistory()
        {
            if (_history != null)
                _history.Clear();
        }

        /// <summary>
        /// 统一 ChangeSet 派发入口。所有变更通知都应通过此方法。
        /// 负责: 重入防护(_isApplyingExternalChange)、抑制累计(_isSuppressing)、事件分流。
        /// </summary>
        private void DispatchChange(ChangeSet change)
        {
            // 外部变更期间不回弹事件
            if (_isApplyingExternalChange)
                return;

            // 记录到撤销/重做栈（历史栈自身应用期间不重复入栈）
            if (_history != null && !_history.IsApplying)
                _history.Push(change);

            // 抑制期间累计，出域时合并
            if (_isSuppressing)
            {
                _suppressedChanges.Add(change);
                return;
            }

            // 分流到具体事件
            switch (change.Kind)
            {
                case ChangeKind.ShapePropertyChanged:
                case ChangeKind.ShapeAdded:
                case ChangeKind.ShapeRemoved:
                    if (ShapeChanged != null)
                        ShapeChanged(this, change);
                    break;
                case ChangeKind.ConnectionAdded:
                case ChangeKind.ConnectionRemoved:
                    if (ConnectionChanged != null)
                        ConnectionChanged(this, change);
                    break;
                case ChangeKind.DocumentCommitted:
                    if (DocumentCommitted != null)
                        DocumentCommitted(this, change);
                    break;
            }
        }

        /// <summary>单个图形的 ShapeChanged 转发器。绑定到每个加入文档的图形。</summary>
        private void OnShapeInternalChanged(object sender, ChangeSet e)
        {
            DispatchChange(e);
        }

        /// <summary>包装 AddShape 流程: 在添加图形后订阅其 ShapeChanged 事件，并派发 ShapeAdded ChangeSet。</summary>
        private void TrackShapeEvents(ShapeBase shape)
        {
            if (shape == null) return;
            shape.ShapeChanged += new EventHandler<ChangeSet>(OnShapeInternalChanged);
        }

        /// <summary>取消订阅图形事件，用于 RemoveShape 流程。</summary>
        private void UntrackShapeEvents(ShapeBase shape)
        {
            if (shape == null) return;
            shape.ShapeChanged -= new EventHandler<ChangeSet>(OnShapeInternalChanged);
        }

        private class EventSuppressor : IDisposable
        {
            private DrawingCanvas _canvas;
            private bool _disposed = false;

            public EventSuppressor(DrawingCanvas canvas)
            {
                _canvas = canvas;
                _canvas._isSuppressing = true;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _canvas._isSuppressing = false;
                if (_canvas._suppressedChanges.Count > 0)
                {
                    BatchChangeSet batch = new BatchChangeSet();
                    batch.Changes = new List<ChangeSet>(_canvas._suppressedChanges);
                    _canvas._suppressedChanges.Clear();
                    if (_canvas.BatchChanged != null)
                        _canvas.BatchChanged(_canvas, batch);
                }
            }
        }

        private class ExternalChangeScope : IDisposable
        {
            private DrawingCanvas _canvas;
            private bool _disposed = false;

            public ExternalChangeScope(DrawingCanvas canvas)
            {
                _canvas = canvas;
                _canvas._isApplyingExternalChange = true;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _canvas._isApplyingExternalChange = false;
            }
        }

        /// <summary>触发 ShapeAdded 事件</summary>
        protected virtual void OnShapeAdded(ShapeBase shape)
        {
            TrackShapeEvents(shape);
            // 现有 ShapeAdded 事件触发保留
            if (ShapeAdded != null)
                ShapeAdded(this, new ShapeEventArgs(shape));
            ChangeSet cs = new ChangeSet();
            cs.Kind = ChangeKind.ShapeAdded;
            cs.ShapeId = shape.Id;
            cs.NewValue = shape;   // 供 Undo/Redo 栈持有实例引用
            cs.Timestamp = DateTime.Now;
            DispatchChange(cs);
        }

        /// <summary>触发 ShapeDeleted 事件</summary>
        protected virtual void OnShapeDeleted(ShapeBase shape)
        {
            UntrackShapeEvents(shape);
            ChangeSet cs = new ChangeSet();
            cs.Kind = ChangeKind.ShapeRemoved;
            cs.ShapeId = shape.Id;
            cs.OldValue = shape;   // 供 Undo/Redo 栈持有实例引用
            cs.Timestamp = DateTime.Now;
            DispatchChange(cs);
            if (ShapeDeleted != null)
                ShapeDeleted(this, new ShapeEventArgs(shape));
        }

        /// <summary>触发 ConnectionAdded 事件</summary>
        protected virtual void OnConnectionAdded(Connection conn)
        {
            if (ConnectionAdded != null)
                ConnectionAdded(this, new ConnectionEventArgs(conn));
            ChangeSet cs = new ChangeSet();
            cs.Kind = ChangeKind.ConnectionAdded;
            cs.ShapeId = conn.Id;
            cs.NewValue = conn;    // 供 Undo/Redo 栈持有实例引用
            cs.Timestamp = DateTime.Now;
            DispatchChange(cs);
        }

        /// <summary>触发 ConnectionDeleted 事件</summary>
        protected virtual void OnConnectionDeleted(Connection conn)
        {
            if (ConnectionDeleted != null)
                ConnectionDeleted(this, new ConnectionEventArgs(conn));
            ChangeSet cs = new ChangeSet();
            cs.Kind = ChangeKind.ConnectionRemoved;
            cs.ShapeId = conn.Id;
            cs.OldValue = conn;    // 供 Undo/Redo 栈持有实例引用
            cs.Timestamp = DateTime.Now;
            DispatchChange(cs);
        }

        #region 内联编辑

        /// <summary>启动标题内联编辑：在标题 Zone 上覆盖 TextBox</summary>
        public void StartInlineEditTitle(GenericShape shape)
        {
            if (_inlineEditBox != null)
                EndInlineEdit();

            _inlineEditingShape = shape;
            _inlineEditingMemberIndex = -1;

            RectangleF titleRect = shape.GetTitleZoneBounds();
            titleRect.Inflate(-4f, -2f);
            Point screenPos = WorldToScreen(new PointF(titleRect.X, titleRect.Y));
            int screenW = Math.Max(60, (int)(titleRect.Width * _zoom));
            int screenH = Math.Max(20, (int)(titleRect.Height * _zoom));

            _inlineEditBox = new TextBox();
            _inlineEditBox.Location = screenPos;
            _inlineEditBox.Size = new Size(screenW, screenH);
            _inlineEditBox.Text = shape.Name;
            _inlineEditBox.Font = new Font("Microsoft YaHei", 10f);
            _inlineEditBox.BorderStyle = BorderStyle.FixedSingle;
            _inlineEditBox.KeyDown += new KeyEventHandler(OnInlineEditKeyDown);
            _inlineEditBox.LostFocus += new EventHandler(OnInlineEditLostFocus);
            this.Controls.Add(_inlineEditBox);
            _inlineEditBox.Focus();
            _inlineEditBox.SelectAll();
        }

        /// <summary>启动成员内联编辑：在成员行上覆盖 TextBox</summary>
        public void StartInlineEditMember(GenericShape shape, int memberIndex)
        {
            if (memberIndex < 0 || memberIndex >= shape.Members.Count)
                return;

            if (_inlineEditBox != null)
                EndInlineEdit();

            _inlineEditingShape = shape;
            _inlineEditingMemberIndex = memberIndex;

            RectangleF memberRect = shape.GetMemberBounds(memberIndex);
            Point screenPos = WorldToScreen(new PointF(memberRect.X, memberRect.Y));
            int screenW = Math.Max(60, (int)(memberRect.Width * _zoom));
            int screenH = Math.Max(16, (int)(memberRect.Height * _zoom));

            _inlineEditBox = new TextBox();
            _inlineEditBox.Location = screenPos;
            _inlineEditBox.Size = new Size(screenW, screenH);
            _inlineEditBox.Text = shape.Members[memberIndex].GetSignature();
            _inlineEditBox.Font = new Font("Microsoft YaHei", 9f);
            _inlineEditBox.BorderStyle = BorderStyle.FixedSingle;
            _inlineEditBox.KeyDown += new KeyEventHandler(OnInlineEditKeyDown);
            _inlineEditBox.LostFocus += new EventHandler(OnInlineEditLostFocus);
            this.Controls.Add(_inlineEditBox);
            _inlineEditBox.Focus();
            _inlineEditBox.SelectAll();
        }

        /// <summary>结束内联编辑：将 TextBox 内容写回图形并移除控件</summary>
        public void EndInlineEdit()
        {
            if (_isEndingInlineEdit)
                return;
            if (_inlineEditBox == null)
                return;

            _isEndingInlineEdit = true;
            try
            {
                string text = _inlineEditBox.Text.Trim();

                if (_inlineEditingMemberIndex >= 0 && _inlineEditingShape != null)
                {
                    // 更新成员名称（从签名中提取或直接使用）
                    if (_inlineEditingMemberIndex < _inlineEditingShape.Members.Count)
                    {
                        _inlineEditingShape.Members[_inlineEditingMemberIndex].Name = text;
                    }
                }
                else if (_inlineEditingShape != null)
                {
                    // 更新标题
                    if (!string.IsNullOrEmpty(text))
                    {
                        _inlineEditingShape.Name = text;
                    }
                }

                this.Controls.Remove(_inlineEditBox);
                _inlineEditBox.Dispose();
                _inlineEditBox = null;
                _inlineEditingShape = null;
                _inlineEditingMemberIndex = -1;

                OnDocumentModified();
                ChangeSet commit = new ChangeSet();
                commit.Kind = ChangeKind.DocumentCommitted;
                commit.ShapeId = Guid.Empty;
                commit.Timestamp = DateTime.Now;
                DispatchChange(commit);
                Invalidate();
            }
            finally
            {
                _isEndingInlineEdit = false;
            }
        }

        private void OnInlineEditKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                EndInlineEdit();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                // 取消编辑：复用 EndInlineEdit 的重入保护
                if (_inlineEditBox != null)
                {
                    _inlineEditingShape = null;
                    _inlineEditingMemberIndex = -1;
                    EndInlineEdit();
                }
                e.SuppressKeyPress = true;
            }
        }

        private void OnInlineEditLostFocus(object sender, EventArgs e)
        {
            EndInlineEdit();
        }

        #endregion

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

            if (_connectStartShape != null && endShape != null)
            {
                // 检查自连限制
                bool isSelfConnect = (_connectStartShape == endShape);
                if (isSelfConnect && _connectionStartZone != null && !_connectionStartZone.AllowSelfConnect)
                {
                    _connectStartShape = null;
                    _connectionStartZone = null;
                    Invalidate();
                    return;
                }

                // 检查终点连接 Zone 的 CanEnd 属性
                GenericShape endGs = endShape as GenericShape;
                if (endGs != null && _connectionStartZone != null)
                {
                    ShapeZone endZone = endGs.HitTestZone(worldPos);
                    if (endZone != null && endZone.IsConnectionZone && !endZone.CanEnd)
                    {
                        _connectStartShape = null;
                        _connectionStartZone = null;
                        Invalidate();
                        return;
                    }
                }

                // 检查起点 Zone 的 CanStart 属性
                if (_connectionStartZone != null && !_connectionStartZone.CanStart)
                {
                    _connectStartShape = null;
                    _connectionStartZone = null;
                    Invalidate();
                    return;
                }

                // 自连限制：无连接 Zone 时禁止自连
                if (isSelfConnect && _connectionStartZone == null)
                {
                    _connectStartShape = null;
                    _connectionStartZone = null;
                    Invalidate();
                    return;
                }

                // 确定连线模式：受连接 Zone 的 AllowedLineTypes 约束
                ConnectionMode connMode = GlobalConfig.Instance.DefaultConnectionMode;
                if (_connectionStartZone != null)
                {
                    string allowed = _connectionStartZone.AllowedLineTypes;
                    if (!string.IsNullOrEmpty(allowed))
                    {
                        // 检查当前模式是否在允许列表中
                        string[] parts = allowed.Split(',');
                        bool modeAllowed = false;
                        foreach (string p in parts)
                        {
                            string trimmed = p.Trim();
                            if (trimmed == connMode.ToString())
                            {
                                modeAllowed = true;
                                break;
                            }
                        }
                        // 若当前模式不被允许，使用第一个允许的模式
                        if (!modeAllowed && parts.Length > 0)
                        {
                            string first = parts[0].Trim();
                            if (first == "Straight") connMode = ConnectionMode.Straight;
                            else if (first == "Curve") connMode = ConnectionMode.Curve;
                            else if (first == "Orthogonal") connMode = ConnectionMode.Orthogonal;
                        }
                    }
                }

                // 创建连线（含允许的自连）
                Connection conn2 = new Connection();
                conn2.FromShape = _connectStartShape;
                conn2.ToShape = endShape;
                conn2.Mode = connMode;
                conn2.FromPoint = _connectStartShape.GetNearestConnectionPoint(endShape.Center);
                conn2.ToPoint = endShape.GetNearestConnectionPoint(_connectStartShape.Center);
                _document.AddConnection(conn2);
                OnConnectionAdded(conn2);
                OnDocumentModified();
            }

            _connectStartShape = null;
            _connectionStartZone = null;
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
            if (disposing)
            {
                // 释放背景渲染缓存
                if (_backgroundCache != null)
                {
                    _backgroundCache.Dispose();
                    _backgroundCache = null;
                }

                // 取消全局配置单例的事件订阅，防止控件销毁后仍被单例持有引用
                GlobalConfig.Instance.Changed -= new EventHandler(OnGlobalConfigChanged);

                // 取消文档事件订阅
                if (_document != null)
                {
                    _document.DocumentChanged -= new EventHandler(OnDocumentChanged);
                }
            }
            base.Dispose(disposing);
        }
    }
}
