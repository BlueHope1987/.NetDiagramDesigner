using System;
using System.Collections.Generic;

namespace NativeManagerDemo.Contract
{
    // ============================================================
    // 领域层接口：查询 + 生命周期操作。
    //   命令行、右侧"对象操作"面板都只与它对话；
    //   它们看不到、也不需要知道图形/控件词汇。
    //   真实实现对接 K8s / kubectl，Demo 用模拟实现。
    // ============================================================

    public interface IClusterGateway
    {
        /// <summary>依据现有部署查询拓扑 —— 这也是画布渲染的唯一数据来源。</summary>
        TopologySnapshot QueryTopology();

        // -------- 生命周期操作（按实体 Id 作用于真实资源）--------
        void Start(string entityId);
        void Stop(string entityId);
        void Restart(string entityId);

        /// <summary>配置类操作。可配项由实体 Kind 决定（如 replicas / image / cpu）。</summary>
        void Configure(string entityId, IDictionary<string, string> options);

        /// <summary>只读：查看详情 / 概要。</summary>
        string Describe(string entityId);
    }

    // ============================================================
    // 意图（画布/面板动作 → 命令）
    //   画布上的"创建/连接"不写本地文档，而是形成 Intent，
    //   经 IIntentExecutor 落成命令、作用于网关；执行后重新
    //   QueryTopology() 并重渲染 —— 后端是唯一事实源。
    // ============================================================

    /// <summary>用户意图的种类。</summary>
    public enum IntentKind
    {
        Unknown = 0,
        CreateEntity,
        RemoveEntity,
        CreateRelation,
        RemoveRelation,
        InvokeOperation
    }

    /// <summary>一次用户意图（视图侧只负责"发出"，不负责"解释"）。</summary>
    public class Intent
    {
        public IntentKind Kind;

        // 创建/删除实体
        public EntityKind NewKind;
        public string NewName;
        public string EntityId;                 // 目标实体（删除/操作）

        // 建立/删除关系
        public string FromId;
        public string ToId;
        public RelationKind RelationKind;
        public string Protocol;

        // 生命周期操作
        public string Operation;                // start / stop / restart / configure
        public IDictionary<string, string> Options;
    }

    /// <summary>意图执行结果（便于命令行回显 / 审计）。</summary>
    public class IntentResult
    {
        public bool Success;
        public string Message;
        public string CommandText;              // 落成的可读命令，便于回显与审计
    }

    /// <summary>
    /// 意图执行器：把意图翻译为命令并作用于 IClusterGateway。
    /// 调用方在 Execute 成功后应重新 QueryTopology() 触发重渲染。
    /// </summary>
    public interface IIntentExecutor
    {
        IntentResult Execute(Intent intent);
    }
}
