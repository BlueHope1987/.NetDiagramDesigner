using System;
using System.Collections.Generic;
using System.Reflection;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 基于 ChangeSet 的撤销/重做栈（命令模式）。
    /// 画布在派发 ChangeSet 时同步 Push 到本栈；Undo/Redo 时
    /// 根据 ChangeKind 应用正向或逆向操作。
    /// 
    /// 约定（画布侧负责填充）：
    ///   ShapeAdded      → NewValue 存放 ShapeBase 实例
    ///   ShapeRemoved    → OldValue 存放 ShapeBase 实例
    ///   ConnectionAdded → NewValue 存放 Connection 实例
    ///   ConnectionRemoved → OldValue 存放 Connection 实例
    ///   ShapePropertyChanged → PropertyName / OldValue / NewValue 已由 ShapeBase 填充
    /// </summary>
    public class ChangeHistory
    {
        private readonly Stack<ChangeSet> _undoStack = new Stack<ChangeSet>();
        private readonly Stack<ChangeSet> _redoStack = new Stack<ChangeSet>();
        private int _maxSize = 100;
        private bool _isApplying = false;

        /// <summary>撤销/重做应用期间为 true，画布据此避免将回写操作再次入栈。</summary>
        public bool IsApplying { get { return _isApplying; } }

        public bool CanUndo { get { return _undoStack.Count > 0; } }
        public bool CanRedo { get { return _redoStack.Count > 0; } }

        public int MaxSize
        {
            get { return _maxSize; }
            set { _maxSize = value <= 0 ? 100 : value; }
        }

        /// <summary>记录一次可撤销变更。</summary>
        public void Push(ChangeSet change)
        {
            if (change == null || _isApplying)
                return;
            // 提交标记与选择变化不参与撤销
            if (change.Kind == ChangeKind.DocumentCommitted ||
                change.Kind == ChangeKind.SelectionChanged)
                return;

            _undoStack.Push(change);
            if (_undoStack.Count > _maxSize)
            {
                // 丢弃栈底（最旧）的记录
                Stack<ChangeSet> tmp = new Stack<ChangeSet>();
                while (_undoStack.Count > 1)
                    tmp.Push(_undoStack.Pop());
                _undoStack.Clear();
                while (tmp.Count > 0)
                    _undoStack.Push(tmp.Pop());
            }
            _redoStack.Clear();
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        /// <summary>撤销最近一次变更。</summary>
        public void Undo(DrawingDocument document)
        {
            if (!CanUndo || document == null)
                return;
            ChangeSet change = _undoStack.Pop();
            _isApplying = true;
            try
            {
                ApplyChange(document, change, forward: false);
            }
            finally
            {
                _isApplying = false;
            }
            _redoStack.Push(change);
        }

        /// <summary>重做最近一次撤销的变更。</summary>
        public void Redo(DrawingDocument document)
        {
            if (!CanRedo || document == null)
                return;
            ChangeSet change = _redoStack.Pop();
            _isApplying = true;
            try
            {
                ApplyChange(document, change, forward: true);
            }
            finally
            {
                _isApplying = false;
            }
            _undoStack.Push(change);
        }

        /// <summary>
        /// 应用变更。forward=true 执行正向（重做），forward=false 执行逆向（撤销）。
        /// </summary>
        private void ApplyChange(DrawingDocument document, ChangeSet change, bool forward)
        {
            switch (change.Kind)
            {
                case ChangeKind.ShapeAdded:
                    if (forward)
                    {
                        ShapeBase addShape = change.NewValue as ShapeBase;
                        if (addShape != null && !document.Shapes.Contains(addShape))
                            document.AddShape(addShape);
                    }
                    else
                    {
                        ShapeBase s = FindShape(document, change.ShapeId);
                        if (s != null)
                            document.RemoveShape(s);
                    }
                    break;

                case ChangeKind.ShapeRemoved:
                    if (forward)
                    {
                        ShapeBase s = FindShape(document, change.ShapeId);
                        if (s != null)
                            document.RemoveShape(s);
                    }
                    else
                    {
                        ShapeBase removedShape = change.OldValue as ShapeBase;
                        if (removedShape != null && !document.Shapes.Contains(removedShape))
                            document.AddShape(removedShape);
                    }
                    break;

                case ChangeKind.ShapePropertyChanged:
                    ShapeBase target = FindShape(document, change.ShapeId);
                    if (target != null)
                    {
                        object value = forward ? change.NewValue : change.OldValue;
                        SetPropertyByName(target, change.PropertyName, value);
                    }
                    break;

                case ChangeKind.ConnectionAdded:
                    if (forward)
                    {
                        Connection conn = change.NewValue as Connection;
                        if (conn != null && !document.Connections.Contains(conn))
                            document.AddConnection(conn);
                    }
                    else
                    {
                        Connection c = FindConnection(document, change.ShapeId);
                        if (c != null)
                            document.RemoveConnection(c);
                    }
                    break;

                case ChangeKind.ConnectionRemoved:
                    if (forward)
                    {
                        Connection c = FindConnection(document, change.ShapeId);
                        if (c != null)
                            document.RemoveConnection(c);
                    }
                    else
                    {
                        Connection removedConn = change.OldValue as Connection;
                        if (removedConn != null && !document.Connections.Contains(removedConn))
                            document.AddConnection(removedConn);
                    }
                    break;

                case ChangeKind.MemberChanged:
                    // 成员变更较为复杂，暂不支持细粒度撤销，留待后续扩展
                    break;
            }

            // 结构或位置变化后重建空间索引
            document.Reindex();
        }

        private static ShapeBase FindShape(DrawingDocument document, Guid id)
        {
            foreach (ShapeBase s in document.Shapes)
            {
                if (s.Id == id)
                    return s;
            }
            return null;
        }

        private static Connection FindConnection(DrawingDocument document, Guid id)
        {
            foreach (Connection c in document.Connections)
            {
                if (c.Id == id)
                    return c;
            }
            return null;
        }

        /// <summary>通过反射按属性名设置值。用于 ShapePropertyChanged 的撤销/重做。</summary>
        private static void SetPropertyByName(ShapeBase shape, string propertyName, object value)
        {
            if (shape == null || string.IsNullOrEmpty(propertyName))
                return;
            PropertyInfo prop = shape.GetType().GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                try
                {
                    prop.SetValue(shape, value, null);
                }
                catch
                {
                    // 类型不匹配或只读等情况忽略，保证撤销流程不中断
                }
            }
        }
    }
}
