using System;
using System.Collections.Generic;

namespace NativeManagerDemo.Contract
{
    // ============================================================
    // 领域层（业务唯一对话面）
    //   只描述"被管理的集群事实"，不含任何视图/图形词汇：
    //   没有坐标、尺寸、颜色、绘制方式，也没有"状态色"。
    //
    // 拥有者：业务逻辑层
    // 不拥有：画布怎么画、用什么颜色/图标（归视图层）
    // 事实源：后端部署现状（QueryTopology 的产物），而非本地图形文档
    // ============================================================

    /// <summary>实体种类（被管理的 K8s 资源类型）。</summary>
    public enum EntityKind
    {
        Unknown = 0,
        Cluster,
        Node,
        Namespace,
        Deployment,
        ReplicaSet,
        StatefulSet,
        DaemonSet,
        Pod,
        Service,
        Ingress,
        ConfigMap,
        Secret,
        PersistentVolume,
        PersistentVolumeClaim,
        Job,
        CronJob,
        Etcd,
        HelmRelease,
        HelmChart,
        IstioGateway,
        VirtualService,
        DestinationRule,
        YamlDocument
    }

    /// <summary>
    /// 运行状态。这是"领域事实"，不是颜色；
    /// 它到颜色/图标的映射由视图层（IStatusVisualMap）负责。
    /// </summary>
    public enum RunStatus
    {
        Unknown = 0,
        Pending,
        Running,
        Stopped,
        Warning,
        Failed
    }

    /// <summary>关系语义。</summary>
    public enum RelationKind
    {
        Unknown = 0,
        Calls,
        Reads,
        Writes,
        Publishes,
        Subscribes,
        Depends,
        Owns,
        Selects,
        Routes,
        Manages
    }

    /// <summary>一个被管理的实体（由后端部署现状查询而来）。</summary>
    public class ManagedEntity
    {
        public string Id;                       // 领域内唯一标识
        public EntityKind Kind;
        public string Name;
        public string Namespace;
        public RunStatus Status;
        public string Version;
        public string Endpoint;
        public IDictionary<string, string> Labels;
        public string ParentId;                 // Namespace 容器或显式父对象 Id
        public string Uid;                      // apiserver UID；设计态使用稳定本地 Id
        public string ResourceVersion;
        public string ApiVersion;
        public string Source;                   // cluster / design
        public int Replicas;
        public string ChartName;

        public ManagedEntity()
        {
            Id = "";
            Name = "";
            Namespace = "";
            Version = "";
            Endpoint = "";
            Labels = new Dictionary<string, string>();
            ParentId = "";
            Uid = "";
            ResourceVersion = "";
            ApiVersion = "";
            Source = "design";
            Replicas = -1;
            ChartName = "";
        }
    }

    /// <summary>实体之间的关系（由后端现状查询而来）。</summary>
    public class Relation
    {
        public string Id;
        public string FromId;                   // 指向 ManagedEntity.Id
        public string ToId;
        public RelationKind Kind;               // Calls/Reads/Writes/Publishes/...
        public string Protocol;                 // HTTP / gRPC / SQL / ...（领域语义）
    }

    /// <summary>某次查询得到的完整拓扑快照。</summary>
    public class TopologySnapshot
    {
        public IList<ManagedEntity> Entities;
        public IList<Relation> Relations;
        public string Revision;                 // 观测时间/版本，用于判断是否需重渲染
        public string ContextName;
        public string Source;
        public DateTime ObservedAt;
        public bool IsStale;
        public IList<string> Warnings;

        public TopologySnapshot()
        {
            Entities = new List<ManagedEntity>();
            Relations = new List<Relation>();
            Revision = "";
            ContextName = "";
            Source = "design";
            ObservedAt = DateTime.Now;
            IsStale = false;
            Warnings = new List<string>();
        }

        public ManagedEntity FindEntity(string id)
        {
            int i;
            for (i = 0; i < Entities.Count; i++)
                if (Entities[i].Id == id) return Entities[i];
            return null;
        }
    }
}
