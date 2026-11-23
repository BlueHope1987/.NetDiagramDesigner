using System;
using System.Collections.Generic;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    /// <summary>
    /// 模拟"被管理的 K8s 集群"。
    /// 真实系统中这里会对接 K8s API / 事件流；本 Demo 用一份样例拓扑代替。
    /// 仅产出领域快照 TopologySnapshot，画布投影由 TopologyProjector 负责，
    /// 从而保证"后端是唯一事实源"。
    /// </summary>
    public class BackendSimulator
    {
        private int _refreshCount;
        private TopologySnapshot _last;
        private bool _connected;

        public bool Connected { get { return _connected; } }
        public string Endpoint = "sim://cluster-a/managed";

        public BackendSimulator()
        {
        }

        public string Connect()
        {
            _connected = true;
            _last = BuildSnapshot();
            return "已连接后端 " + Endpoint + "，已拉取实际状态";
        }

        public void Disconnect()
        {
            _connected = false;
        }

        /// <summary>再次拉取实际状态；模拟一个健康事件。</summary>
        public string Refresh()
        {
            if (!Connected) return "尚未连接后端（请先执行 连接）";
            _last = BuildSnapshot();

            // 模拟后端推来一个健康事件：轮流让 order-service-0 进入 Warning/Failed
            _refreshCount++;
            string target = "order-service-0";
            RunStatus st = (_refreshCount % 3 == 0) ? RunStatus.Failed
                         : (_refreshCount % 3 == 1) ? RunStatus.Warning
                         : RunStatus.Running;
            ApplyStatusByName(target, st);
            return "已刷新集群实际状态；健康事件: " + target + " -> " + st;
        }

        /// <summary>返回当前拓扑快照（画布的唯一数据来源）。</summary>
        public TopologySnapshot QueryTopology()
        {
            if (_last == null)
                _last = BuildSnapshot();
            return _last;
        }

        private TopologySnapshot BuildSnapshot()
        {
            TopologySnapshot snap = new TopologySnapshot();
            snap.Revision = DateTime.Now.ToString("yyyyMMddHHmmss");

            // ---- 实体 ----
            snap.Entities.Add(MakeEntity("ns-default", EntityKind.Namespace, "default", "default", RunStatus.Running));
            snap.Entities.Add(MakeEntity("node-1", EntityKind.Node, "node-1", "", RunStatus.Running));
            snap.Entities.Add(MakeEntity("etcd-0", EntityKind.Etcd, "etcd-0", "kube-system", RunStatus.Running));

            snap.Entities.Add(MakeEntity("deploy-web", EntityKind.Deployment, "web-app", "default", RunStatus.Running));
            snap.Entities.Add(MakeEntity("deploy-order", EntityKind.Deployment, "order-service", "default", RunStatus.Warning));

            snap.Entities.Add(MakeEntity("pod-web-0", EntityKind.Pod, "web-app-0", "default", RunStatus.Running));
            snap.Entities.Add(MakeEntity("pod-web-1", EntityKind.Pod, "web-app-1", "default", RunStatus.Running));
            snap.Entities.Add(MakeEntity("pod-order-0", EntityKind.Pod, "order-service-0", "default", RunStatus.Running));

            snap.Entities.Add(MakeEntity("svc-web", EntityKind.Service, "web-svc", "default", RunStatus.Running));
            snap.Entities.Add(MakeEntity("ingress-web", EntityKind.Ingress, "web-ingress", "default", RunStatus.Running));

            snap.Entities.Add(MakeEntity("cm-app", EntityKind.ConfigMap, "app-config", "default", RunStatus.Running));
            snap.Entities.Add(MakeEntity("secret-db", EntityKind.Secret, "db-secret", "default", RunStatus.Running));
            snap.Entities.Add(MakeEntity("pv-data", EntityKind.PersistentVolume, "data-pv", "", RunStatus.Running));

            // ---- 关系 ----
            snap.Relations.Add(MakeRelation("ingress-web", "svc-web", RelationKind.Calls, "HTTP"));
            snap.Relations.Add(MakeRelation("svc-web", "pod-web-0", RelationKind.Calls, "HTTP"));
            snap.Relations.Add(MakeRelation("svc-web", "pod-web-1", RelationKind.Calls, "HTTP"));
            snap.Relations.Add(MakeRelation("deploy-web", "pod-web-0", RelationKind.Depends, ""));
            snap.Relations.Add(MakeRelation("deploy-web", "pod-web-1", RelationKind.Depends, ""));
            snap.Relations.Add(MakeRelation("deploy-order", "pod-order-0", RelationKind.Depends, ""));
            snap.Relations.Add(MakeRelation("pod-web-0", "cm-app", RelationKind.Reads, ""));
            snap.Relations.Add(MakeRelation("pod-order-0", "secret-db", RelationKind.Reads, ""));
            snap.Relations.Add(MakeRelation("pod-order-0", "pv-data", RelationKind.Writes, ""));
            snap.Relations.Add(MakeRelation("pod-order-0", "etcd-0", RelationKind.Calls, "gRPC"));

            return snap;
        }

        private void ApplyStatusByName(string name, RunStatus status)
        {
            if (_last == null) return;
            foreach (ManagedEntity e in _last.Entities)
            {
                if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    e.Status = status;
                    return;
                }
            }
        }

        private static ManagedEntity MakeEntity(string id, EntityKind kind, string name, string ns, RunStatus status)
        {
            ManagedEntity e = new ManagedEntity();
            e.Id = id;
            e.Kind = kind;
            e.Name = name;
            e.Namespace = ns;
            e.Status = status;
            e.Version = "v1";
            e.Endpoint = "";
            e.Labels = new Dictionary<string, string>();
            return e;
        }

        private static Relation MakeRelation(string from, string to, RelationKind kind, string protocol)
        {
            Relation r = new Relation();
            r.Id = from + "->" + to;
            r.FromId = from;
            r.ToId = to;
            r.Kind = kind;
            r.Protocol = protocol;
            return r;
        }
    }
}
