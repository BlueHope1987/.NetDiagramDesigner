using System;
using System.Collections.Generic;

namespace NativeManagerDemo.Contract
{
    // ============================================================
    // 视图层契约：只认"通用图元"，不认识领域类型，也不知道语义。
    //   绑定层把 TopologySnapshot 投影为 ViewNode / ViewEdge 后传入；
    //   视图只对外发"图元级"事件，业务层再去解释为意图或详情。
    //
    // 归视图（不进入业务契约）：
    //   缩放 / 适应 / 自动布局 / 主题 / 图标与颜色映射
    // ============================================================

    /// <summary>通用节点图元（视图词汇）。</summary>
    public class ViewNode
    {
        public string RefId;                    // 回关联领域 Id，仅作标识，不解释语义
        public string Label;                    // 显示文本（绑定层决定）
        public string IconKey;                  // 图标键（绑定层按 Kind/Status 映射）
        public string VisualState;              // 视觉状态键（绑定层按 RunStatus 映射）
        public float X, Y;
        public float Width, Height;
    }

    /// <summary>通用连线图元（视图词汇）。</summary>
    public class ViewEdge
    {
        public string RefId;
        public string FromRefId;
        public string ToRefId;
        public string Label;
    }

    /// <summary>
    /// 视图包装（包住 DiagramDesigner 控件）只对外暴露三类事件。
    /// 它不提供 SetShapeState / 颜色 / 类型 等领域或图形语义接口。
    /// </summary>
    public interface ITopologyView
    {
        /// <summary>把（已布局的）图元投影到画布。</summary>
        void Render(IList<ViewNode> nodes, IList<ViewEdge> edges);

        event EventHandler<NodeSelectedEventArgs> NodeSelected;    // 选中 / 取消选中
        event EventHandler<NodeActivatedEventArgs> NodeActivated;  // 双击"激活"
        event EventHandler<IntentRaisedEventArgs> IntentRaised;    // 产生写入意图
    }

    public class NodeSelectedEventArgs : EventArgs
    {
        /// <summary>被选中的图元 RefId；null 表示取消选择。</summary>
        public string RefId;
    }

    public class NodeActivatedEventArgs : EventArgs
    {
        public string RefId;                    // 双击激活的图元
    }

    public class IntentRaisedEventArgs : EventArgs
    {
        public Intent Intent;                   // 视图只上报，不解释
    }

    // ============================================================
    // 视图侧的两个纯视觉职责（业务层不参与）
    // ============================================================

    /// <summary>领域事实 → 视觉 的映射：颜色 / 图标 / 视觉状态。</summary>
    public interface IStatusVisualMap
    {
        string IconKeyOf(ManagedEntity e);
        string VisualStateOf(ManagedEntity e);
    }

    /// <summary>依据拓扑快照计算布局，产出带坐标的节点（边由投影层按 Relation 生成）。</summary>
    public interface ITopologyLayout
    {
        IList<ViewNode> Arrange(TopologySnapshot snapshot);
    }
}
