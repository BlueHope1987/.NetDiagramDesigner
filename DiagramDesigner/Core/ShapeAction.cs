using System;
using System.Collections.Generic;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 图形操作的处理类型
    /// </summary>
    public enum ShapeActionType
    {
        /// <summary>切换图形状态</summary>
        StateChange,
        /// <summary>通过宿主注册的回调处理</summary>
        HostCallback,
        /// <summary>内联编辑标题（系统行为，由画布处理）</summary>
        InlineEditTitle,
        /// <summary>添加成员（系统行为，由画布处理）</summary>
        AddMember,
        /// <summary>删除成员（系统行为，由画布处理）</summary>
        DeleteMember,
        /// <summary>Zone 点击触发（系统行为，由画布处理）</summary>
        ZoneClick,
        /// <summary>Zone 连接触发（系统行为，由画布处理）</summary>
        ZoneConnect
    }

    /// <summary>
    /// 宿主回调事件参数，携带触发操作的图形信息
    /// </summary>
    public class ShapeActionEventArgs : EventArgs
    {
        private ShapeBase _shape;
        private string _actionName;

        public ShapeBase Shape
        {
            get { return _shape; }
            set { _shape = value; }
        }

        public string ActionName
        {
            get { return _actionName; }
            set { _actionName = value; }
        }

        public ShapeActionEventArgs(ShapeBase shape, string actionName)
        {
            _shape = shape;
            _actionName = actionName;
        }
    }

    /// <summary>
    /// 图形操作定义。每个 ShapeType 可挂载多个操作，在右键菜单中显示。
    /// 一个 ShapeAction 可包含子操作序列（SubActions），
    /// 当 SubActions 非空时按序执行所有子操作。
    /// 系统行为（IsSystemBehavior=true）由 Zone 自动注册，不可删除。
    /// </summary>
    [Serializable]
    public class ShapeAction
    {
        private string _name = "";
        private string _iconName = "";
        private ShapeActionType _actionType = ShapeActionType.HostCallback;
        private string _callbackName = "";
        private string _targetState = "";
        private bool _isSystemBehavior = false;
        private string _zoneName = "";
        private List<ShapeAction> _subActions = new List<ShapeAction>();

        /// <summary>操作名称，显示在右键菜单中</summary>
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        /// <summary>图标资源名（如 "add_member.png"）</summary>
        public string IconName
        {
            get { return _iconName; }
            set { _iconName = value; }
        }

        /// <summary>
        /// 操作类型：HostCallback 需要宿主注册回调，StateChange 自动切换状态，
        /// InlineEditTitle/AddMember/DeleteMember 为系统行为由画布处理，
        /// ZoneClick/ZoneConnect 由 Zone 交互触发。
        /// </summary>
        public ShapeActionType ActionType
        {
            get { return _actionType; }
            set { _actionType = value; }
        }

        /// <summary>宿主回调名（ActionType = HostCallback 时使用）</summary>
        public string CallbackName
        {
            get { return _callbackName; }
            set { _callbackName = value; }
        }

        /// <summary>目标状态名（ActionType = StateChange 时使用）</summary>
        public string TargetState
        {
            get { return _targetState; }
            set { _targetState = value; }
        }

        /// <summary>
        /// 是否为系统行为。系统行为由 Zone 自动注册，不可删除，可切换显隐。
        /// </summary>
        public bool IsSystemBehavior
        {
            get { return _isSystemBehavior; }
            set { _isSystemBehavior = value; }
        }

        /// <summary>
        /// 关联的 Zone 名称。系统行为通过此属性与 Zone 关联。
        /// </summary>
        public string ZoneName
        {
            get { return _zoneName; }
            set { _zoneName = value; }
        }

        /// <summary>
        /// 子操作序列。当非空时，按序执行所有子操作，
        /// 主操作的 ActionType/CallbackName/TargetState 被忽略。
        /// </summary>
        public List<ShapeAction> SubActions
        {
            get { return _subActions; }
            set { _subActions = value; }
        }

        /// <summary>是否包含子操作序列</summary>
        public bool HasSubActions
        {
            get { return _subActions != null && _subActions.Count > 0; }
        }

        public ShapeAction() { }

        /// <summary>创建宿主回调操作</summary>
        public static ShapeAction CreateCallback(string name, string callbackName, string iconName)
        {
            ShapeAction a = new ShapeAction();
            a.Name = name;
            a.CallbackName = callbackName;
            a.IconName = iconName;
            a.ActionType = ShapeActionType.HostCallback;
            return a;
        }

        /// <summary>创建状态切换操作</summary>
        public static ShapeAction CreateStateChange(string name, string targetState, string iconName)
        {
            ShapeAction a = new ShapeAction();
            a.Name = name;
            a.TargetState = targetState;
            a.IconName = iconName;
            a.ActionType = ShapeActionType.StateChange;
            return a;
        }

        /// <summary>创建系统行为</summary>
        public static ShapeAction CreateSystemBehavior(string name, ShapeActionType type, string zoneName, string iconName)
        {
            ShapeAction a = new ShapeAction();
            a.Name = name;
            a.ActionType = type;
            a.ZoneName = zoneName;
            a.IconName = iconName;
            a.IsSystemBehavior = true;
            return a;
        }

        /// <summary>深拷贝（含子操作序列）</summary>
        public ShapeAction Clone()
        {
            ShapeAction clone = new ShapeAction();
            clone._name = _name;
            clone._iconName = _iconName;
            clone._actionType = _actionType;
            clone._callbackName = _callbackName;
            clone._targetState = _targetState;
            clone._isSystemBehavior = _isSystemBehavior;
            clone._zoneName = _zoneName;
            if (_subActions != null)
            {
                foreach (ShapeAction sub in _subActions)
                    clone._subActions.Add(sub.Clone());
            }
            return clone;
        }
    }
}
