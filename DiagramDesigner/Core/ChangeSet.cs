using System;
using System.Collections.Generic;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 增量变更类型枚举。覆盖图形/连线/成员的增删改及文档级提交事件。
    /// </summary>
    public enum ChangeKind
    {
        ShapeAdded,
        ShapeRemoved,
        ShapePropertyChanged,
        ConnectionAdded,
        ConnectionRemoved,
        MemberChanged,
        SelectionChanged,
        DocumentCommitted
    }

    /// <summary>
    /// 单次变更的稳定可序列化契约。宿主据此做局部更新而无需重新解析整篇文档。
    /// 字段顺序与类型保持稳定以支持未来 Undo/Redo 栈或跨进程传递。
    /// </summary>
    [Serializable]
    public class ChangeSet : EventArgs
    {
        private ChangeKind _kind;
        private Guid _shapeId;
        private string _propertyName;
        private object _oldValue;
        private object _newValue;
        private DateTime _timestamp;

        public ChangeKind Kind { get { return _kind; } set { _kind = value; } }
        public Guid ShapeId { get { return _shapeId; } set { _shapeId = value; } }
        public string PropertyName { get { return _propertyName; } set { _propertyName = value; } }
        public object OldValue { get { return _oldValue; } set { _oldValue = value; } }
        public object NewValue { get { return _newValue; } set { _newValue = value; } }
        public DateTime Timestamp { get { return _timestamp; } set { _timestamp = value; } }

        public ChangeSet()
        {
            _timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 批量变更集。由 SuppressEvents() 作用域退出时触发，
    /// 将期间累计的多个 ChangeSet 合并为一次发送，避免高频事件冲击宿主。
    /// </summary>
    [Serializable]
    public class BatchChangeSet : EventArgs
    {
        private List<ChangeSet> _changes;
        private ChangeKind _aggregateKind;

        public List<ChangeSet> Changes { get { return _changes; } set { _changes = value; } }
        public ChangeKind AggregateKind { get { return _aggregateKind; } set { _aggregateKind = value; } }

        public BatchChangeSet()
        {
            _changes = new List<ChangeSet>();
        }
    }
}
