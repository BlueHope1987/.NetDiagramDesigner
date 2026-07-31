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
        HostCallback
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
    /// 操作的执行通过宿主回调完成，实现图形行为与控件解耦。
    /// </summary>
    [Serializable]
    public class ShapeAction
    {
        private string _name = "";
        private string _iconName = "";
        private ShapeActionType _actionType = ShapeActionType.HostCallback;
        private string _callbackName = "";
        private string _targetState = "";

        /// <summary>
        /// 操作名称，显示在右键菜单中
        /// </summary>
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        /// <summary>
        /// 图标资源名（如 "add_member.png"）
        /// </summary>
        public string IconName
        {
            get { return _iconName; }
            set { _iconName = value; }
        }

        /// <summary>
        /// 操作类型：HostCallback 需要宿主注册回调，StateChange 自动切换状态
        /// </summary>
        public ShapeActionType ActionType
        {
            get { return _actionType; }
            set { _actionType = value; }
        }

        /// <summary>
        /// 宿主回调名（ActionType = HostCallback 时使用）
        /// 宿主通过 DiagramEditor.RegisterActionCallback(callbackName, handler) 注册
        /// </summary>
        public string CallbackName
        {
            get { return _callbackName; }
            set { _callbackName = value; }
        }

        /// <summary>
        /// 目标状态名（ActionType = StateChange 时使用）
        /// </summary>
        public string TargetState
        {
            get { return _targetState; }
            set { _targetState = value; }
        }

        public ShapeAction() { }

        /// <summary>
        /// 创建宿主回调操作
        /// </summary>
        public static ShapeAction CreateCallback(string name, string callbackName, string iconName)
        {
            ShapeAction a = new ShapeAction();
            a.Name = name;
            a.CallbackName = callbackName;
            a.IconName = iconName;
            a.ActionType = ShapeActionType.HostCallback;
            return a;
        }

        /// <summary>
        /// 创建状态切换操作
        /// </summary>
        public static ShapeAction CreateStateChange(string name, string targetState, string iconName)
        {
            ShapeAction a = new ShapeAction();
            a.Name = name;
            a.TargetState = targetState;
            a.IconName = iconName;
            a.ActionType = ShapeActionType.StateChange;
            return a;
        }
    }
}