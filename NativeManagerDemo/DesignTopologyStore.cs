using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    public class DesignEditResult
    {
        public bool Success;
        public string Message;
        public string EntityId;
        public string RelationId;

        public DesignEditResult(bool success, string message)
        {
            Success = success;
            Message = message;
            EntityId = "";
            RelationId = "";
        }
    }

    /// <summary>设计·调试模式的本地事实源。所有修改仅作用于此快照。</summary>
    public class DesignTopologyStore
    {
        private TopologySnapshot _snapshot;

        public DesignTopologyStore()
        {
            _snapshot = CreateInitialSnapshot();
        }

        public TopologySnapshot QueryTopology()
        {
            return Clone(_snapshot);
        }

        public void Replace(TopologySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            string error = ValidateSnapshot(snapshot);
            if (error.Length > 0) throw new InvalidOperationException(error);
            snapshot.Source = "design";
            int i;
            for (i = 0; i < snapshot.Entities.Count; i++) snapshot.Entities[i].Source = "design";
            _snapshot = Clone(snapshot);
            Touch();
        }

        public DesignEditResult AddEntity(EntityKind kind, string name, string namespaceName)
        {
            if (kind == EntityKind.Unknown || kind == EntityKind.Cluster)
                return new DesignEditResult(false, "该类型不能作为普通资源放置");
            name = name == null ? "" : name.Trim();
            namespaceName = namespaceName == null ? "" : namespaceName.Trim();
            if (name.Length == 0) return new DesignEditResult(false, "资源名称不能为空");
            if (name.IndexOfAny(new char[] { ' ', '\t', '\r', '\n', '/' }) >= 0)
                return new DesignEditResult(false, "资源名称不能包含空白或 /");
            if (IsNamespaced(kind) && namespaceName.Length == 0) namespaceName = "default";
            if (!IsNamespaced(kind)) namespaceName = "";

            ManagedEntity ns = null;
            if (namespaceName.Length > 0)
            {
                ns = FindNamespace(namespaceName);
                if (ns == null)
                {
                    DesignEditResult nsResult = AddEntity(EntityKind.Namespace, namespaceName, "");
                    if (!nsResult.Success) return nsResult;
                    ns = _snapshot.FindEntity(nsResult.EntityId);
                }
            }
            if (FindByName(kind, name, namespaceName) != null)
                return new DesignEditResult(false, "同一作用域中已存在同名 " + kind + " 资源");

            ManagedEntity entity = NewEntity(kind, name, namespaceName);
            if (ns != null) entity.ParentId = ns.Id;
            _snapshot.Entities.Add(entity);
            if (kind == EntityKind.Namespace) EnsureClusterRelation(entity);
            Touch();
            DesignEditResult result = new DesignEditResult(true, "已在设计快照中新增 " + name + "（未写入集群）");
            result.EntityId = entity.Id;
            return result;
        }

        public DesignEditResult DeleteEntity(string id)
        {
            ManagedEntity entity = _snapshot.FindEntity(id);
            if (entity == null) return new DesignEditResult(false, "未找到要删除的设计对象");
            if (entity.Kind == EntityKind.Cluster) return new DesignEditResult(false, "设计集群根节点不能删除");
            if (entity.Kind == EntityKind.Namespace)
            {
                int i;
                for (i = 0; i < _snapshot.Entities.Count; i++)
                    if (_snapshot.Entities[i].ParentId == id)
                        return new DesignEditResult(false, "Namespace 内仍有对象，请先移出或删除其子对象");
            }

            int index;
            for (index = _snapshot.Relations.Count - 1; index >= 0; index--)
            {
                Relation relation = _snapshot.Relations[index];
                if (relation.FromId == id || relation.ToId == id) _snapshot.Relations.RemoveAt(index);
            }
            _snapshot.Entities.Remove(entity);
            Touch();
            return new DesignEditResult(true, "已从设计快照删除 " + entity.Name + "（未操作集群）");
        }

        public DesignEditResult MoveEntity(string id, string newNamespaceId)
        {
            ManagedEntity entity = _snapshot.FindEntity(id);
            ManagedEntity ns = _snapshot.FindEntity(newNamespaceId);
            if (entity == null || ns == null || ns.Kind != EntityKind.Namespace)
                return new DesignEditResult(false, "目标不是有效的 Namespace 容器");
            if (!IsNamespaced(entity.Kind))
                return new DesignEditResult(false, entity.Kind + " 是集群级或设计辅助对象，不能放入 Namespace");
            if (entity.ParentId == newNamespaceId) return new DesignEditResult(true, "对象已在该 Namespace 中");

            ManagedEntity duplicate = FindByName(entity.Kind, entity.Name, ns.Name);
            if (duplicate != null && duplicate.Id != entity.Id)
                return new DesignEditResult(false, "目标 Namespace 中已存在同类型同名资源");
            entity.Namespace = ns.Name;
            entity.ParentId = ns.Id;
            Touch();
            return new DesignEditResult(true, "已将 " + entity.Name + " 移到设计 Namespace " + ns.Name);
        }

        public DesignEditResult AddRelation(string fromId, string toId, RelationKind kind)
        {
            ManagedEntity from = _snapshot.FindEntity(fromId);
            ManagedEntity to = _snapshot.FindEntity(toId);
            string error = ValidateRelation(from, to, kind);
            if (error.Length > 0) return new DesignEditResult(false, error);
            int i;
            for (i = 0; i < _snapshot.Relations.Count; i++)
            {
                Relation old = _snapshot.Relations[i];
                if (old.FromId == fromId && old.ToId == toId && old.Kind == kind)
                    return new DesignEditResult(false, "相同的语义连线已存在");
            }
            Relation relation = NewRelation(fromId, toId, kind, "");
            _snapshot.Relations.Add(relation);
            Touch();
            DesignEditResult result = new DesignEditResult(true, "已新增设计关系（未操作集群）");
            result.RelationId = relation.Id;
            return result;
        }

        public DesignEditResult RemoveRelation(string relationId)
        {
            int i;
            for (i = 0; i < _snapshot.Relations.Count; i++)
            {
                if (_snapshot.Relations[i].Id == relationId)
                {
                    _snapshot.Relations.RemoveAt(i);
                    Touch();
                    return new DesignEditResult(true, "已删除设计关系");
                }
            }
            return new DesignEditResult(false, "未找到设计关系");
        }

        public DesignEditResult RenameEntity(string id, string newName)
        {
            ManagedEntity entity = _snapshot.FindEntity(id);
            newName = newName == null ? "" : newName.Trim();
            if (entity == null) return new DesignEditResult(false, "未找到设计对象");
            if (newName.Length == 0 || newName.IndexOfAny(new char[] { ' ', '\t', '\r', '\n', '/' }) >= 0)
                return new DesignEditResult(false, "名称不能为空，也不能包含空白或 /");
            ManagedEntity duplicate = FindByName(entity.Kind, newName, entity.Namespace);
            if (duplicate != null && duplicate.Id != id) return new DesignEditResult(false, "同一作用域已存在同名资源");
            entity.Name = newName;
            if (entity.Kind == EntityKind.Namespace)
            {
                int i;
                for (i = 0; i < _snapshot.Entities.Count; i++)
                    if (_snapshot.Entities[i].ParentId == id) _snapshot.Entities[i].Namespace = newName;
            }
            Touch();
            return new DesignEditResult(true, "已修改设计对象名称");
        }

        public DesignEditResult SetReplicas(string id, int replicas)
        {
            ManagedEntity entity = _snapshot.FindEntity(id);
            if (entity == null) return new DesignEditResult(false, "未找到设计对象");
            if (replicas < 0 || replicas > 10000) return new DesignEditResult(false, "副本数应在 0 到 10000 之间");
            entity.Replicas = replicas;
            Touch();
            return new DesignEditResult(true, "已更新设计副本数（未写入集群）");
        }

        public void Save(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("保存路径不能为空", "path");
            File.WriteAllText(path, MiniJson.Stringify(ToJson(_snapshot)), new UTF8Encoding(false));
        }

        public void Load(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("打开路径不能为空", "path");
            object root = MiniJson.Parse(File.ReadAllText(path, Encoding.UTF8));
            TopologySnapshot loaded = FromJson(root);
            Replace(loaded);
        }

        public static bool IsContainer(EntityKind kind)
        {
            return kind == EntityKind.Namespace;
        }

        public static bool IsNamespaced(EntityKind kind)
        {
            switch (kind)
            {
                case EntityKind.Unknown:
                case EntityKind.Cluster:
                case EntityKind.Node:
                case EntityKind.Namespace:
                case EntityKind.PersistentVolume:
                case EntityKind.HelmChart:
                case EntityKind.YamlDocument:
                    return false;
                default:
                    return true;
            }
        }

        public static string ValidateRelation(ManagedEntity from, ManagedEntity to, RelationKind kind)
        {
            if (from == null || to == null) return "连线两端必须是有效资源";
            if (from.Id == to.Id) return "不允许连接到对象自身";
            if (kind == RelationKind.Unknown) return "请选择明确的关系语义";
            if (from.Kind == EntityKind.Cluster && to.Kind == EntityKind.Namespace && kind == RelationKind.Owns) return "";
            if (IsWorkload(from.Kind) && to.Kind == EntityKind.Pod &&
                (kind == RelationKind.Owns || kind == RelationKind.Depends || kind == RelationKind.Manages)) return "";
            if (from.Kind == EntityKind.Service && to.Kind == EntityKind.Pod &&
                (kind == RelationKind.Selects || kind == RelationKind.Calls)) return "";
            if (from.Kind == EntityKind.Ingress && to.Kind == EntityKind.Service && kind == RelationKind.Routes) return "";
            if (from.Kind == EntityKind.VirtualService &&
                (to.Kind == EntityKind.Service || to.Kind == EntityKind.IstioGateway || to.Kind == EntityKind.DestinationRule) &&
                (kind == RelationKind.Routes || kind == RelationKind.Depends)) return "";
            if (from.Kind == EntityKind.DestinationRule && to.Kind == EntityKind.Service && kind == RelationKind.Depends) return "";
            if (from.Kind == EntityKind.HelmRelease && kind == RelationKind.Manages && to.Kind != EntityKind.Namespace) return "";
            if ((from.Kind == EntityKind.Pod || IsWorkload(from.Kind)) &&
                (to.Kind == EntityKind.ConfigMap || to.Kind == EntityKind.Secret || to.Kind == EntityKind.PersistentVolumeClaim) &&
                (kind == RelationKind.Reads || kind == RelationKind.Depends)) return "";
            if (from.Kind == EntityKind.PersistentVolumeClaim && to.Kind == EntityKind.PersistentVolume && kind == RelationKind.Depends) return "";
            return "不允许的从属关系：" + from.Kind + " → " + to.Kind + "（" + kind + "）";
        }

        public static TopologySnapshot CreateInitialSnapshot()
        {
            TopologySnapshot snapshot = new TopologySnapshot();
            snapshot.ContextName = "本地设计";
            ManagedEntity cluster = NewEntity(EntityKind.Cluster, "design-cluster", "");
            ManagedEntity ns = NewEntity(EntityKind.Namespace, "default", "");
            ManagedEntity apps = NewEntity(EntityKind.Namespace, "apps", "");
            snapshot.Entities.Add(cluster);
            snapshot.Entities.Add(ns);
            snapshot.Entities.Add(apps);
            snapshot.Relations.Add(NewRelation(cluster.Id, ns.Id, RelationKind.Owns, ""));
            snapshot.Relations.Add(NewRelation(cluster.Id, apps.Id, RelationKind.Owns, ""));

            ManagedEntity deploy = NewEntity(EntityKind.Deployment, "web", "default");
            deploy.ParentId = ns.Id;
            deploy.Replicas = 2;
            ManagedEntity pod = NewEntity(EntityKind.Pod, "web-demo-pod", "default");
            pod.ParentId = ns.Id;
            ManagedEntity svc = NewEntity(EntityKind.Service, "web", "default");
            svc.ParentId = ns.Id;
            ManagedEntity cm = NewEntity(EntityKind.ConfigMap, "web-config", "default");
            cm.ParentId = ns.Id;
            ManagedEntity release = NewEntity(EntityKind.HelmRelease, "shop", "apps");
            release.ParentId = apps.Id;
            release.ChartName = "example/shop";
            ManagedEntity vs = NewEntity(EntityKind.VirtualService, "shop-route", "apps");
            vs.ParentId = apps.Id;
            snapshot.Entities.Add(deploy);
            snapshot.Entities.Add(pod);
            snapshot.Entities.Add(svc);
            snapshot.Entities.Add(cm);
            snapshot.Entities.Add(release);
            snapshot.Entities.Add(vs);
            snapshot.Relations.Add(NewRelation(deploy.Id, pod.Id, RelationKind.Owns, ""));
            snapshot.Relations.Add(NewRelation(svc.Id, pod.Id, RelationKind.Selects, "HTTP"));
            snapshot.Relations.Add(NewRelation(pod.Id, cm.Id, RelationKind.Reads, ""));
            snapshot.Relations.Add(NewRelation(release.Id, vs.Id, RelationKind.Manages, ""));
            snapshot.Revision = DateTime.Now.ToString("yyyyMMddHHmmss");
            return snapshot;
        }

        internal static ManagedEntity NewEntity(EntityKind kind, string name, string namespaceName)
        {
            ManagedEntity entity = new ManagedEntity();
            entity.Id = "design-" + kind.ToString().ToLowerInvariant() + "-" + Guid.NewGuid().ToString("N");
            entity.Uid = entity.Id;
            entity.Kind = kind;
            entity.Name = name;
            entity.Namespace = namespaceName;
            entity.Status = RunStatus.Unknown;
            entity.Source = "design";
            entity.ApiVersion = "design.local/v1";
            return entity;
        }

        internal static Relation NewRelation(string fromId, string toId, RelationKind kind, string protocol)
        {
            Relation relation = new Relation();
            relation.Id = "rel-" + Guid.NewGuid().ToString("N");
            relation.FromId = fromId;
            relation.ToId = toId;
            relation.Kind = kind;
            relation.Protocol = protocol;
            return relation;
        }

        private static bool IsWorkload(EntityKind kind)
        {
            return kind == EntityKind.Deployment || kind == EntityKind.ReplicaSet ||
                kind == EntityKind.StatefulSet || kind == EntityKind.DaemonSet ||
                kind == EntityKind.Job || kind == EntityKind.CronJob;
        }

        private ManagedEntity FindNamespace(string name)
        {
            int i;
            for (i = 0; i < _snapshot.Entities.Count; i++)
                if (_snapshot.Entities[i].Kind == EntityKind.Namespace && _snapshot.Entities[i].Name == name)
                    return _snapshot.Entities[i];
            return null;
        }

        private ManagedEntity FindByName(EntityKind kind, string name, string namespaceName)
        {
            int i;
            for (i = 0; i < _snapshot.Entities.Count; i++)
            {
                ManagedEntity entity = _snapshot.Entities[i];
                if (entity.Kind == kind && entity.Name == name && entity.Namespace == namespaceName) return entity;
            }
            return null;
        }

        private void EnsureClusterRelation(ManagedEntity ns)
        {
            int i;
            for (i = 0; i < _snapshot.Entities.Count; i++)
            {
                if (_snapshot.Entities[i].Kind == EntityKind.Cluster)
                {
                    _snapshot.Relations.Add(NewRelation(_snapshot.Entities[i].Id, ns.Id, RelationKind.Owns, ""));
                    return;
                }
            }
        }

        private void Touch()
        {
            _snapshot.Revision = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            _snapshot.ObservedAt = DateTime.Now;
            _snapshot.IsStale = false;
        }

        private static string ValidateSnapshot(TopologySnapshot snapshot)
        {
            int i;
            for (i = 0; i < snapshot.Entities.Count; i++)
            {
                ManagedEntity entity = snapshot.Entities[i];
                if (entity == null || string.IsNullOrEmpty(entity.Id) || string.IsNullOrEmpty(entity.Name)) return "设计文件包含无效资源";
                if (entity.Labels == null) entity.Labels = new Dictionary<string, string>();
            }
            for (i = 0; i < snapshot.Relations.Count; i++)
            {
                Relation relation = snapshot.Relations[i];
                if (snapshot.FindEntity(relation.FromId) == null || snapshot.FindEntity(relation.ToId) == null)
                    return "设计文件包含悬空关系: " + relation.Id;
            }
            return "";
        }

        internal static TopologySnapshot Clone(TopologySnapshot source)
        {
            TopologySnapshot clone = FromJson(ToJson(source));
            clone.Source = source.Source;
            clone.IsStale = source.IsStale;
            int i;
            for (i = 0; i < source.Warnings.Count; i++) clone.Warnings.Add(source.Warnings[i]);
            for (i = 0; i < source.Entities.Count && i < clone.Entities.Count; i++)
                clone.Entities[i].Source = source.Entities[i].Source;
            return clone;
        }

        private static IDictionary<string, object> ToJson(TopologySnapshot snapshot)
        {
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["schemaVersion"] = 1;
            root["revision"] = snapshot.Revision;
            root["contextName"] = snapshot.ContextName;
            root["source"] = "design";
            root["observedAt"] = snapshot.ObservedAt.ToString("o");
            List<object> entities = new List<object>();
            int i;
            for (i = 0; i < snapshot.Entities.Count; i++)
            {
                ManagedEntity entity = snapshot.Entities[i];
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["id"] = entity.Id;
                item["uid"] = entity.Uid;
                item["kind"] = entity.Kind.ToString();
                item["name"] = entity.Name;
                item["namespace"] = entity.Namespace;
                item["status"] = entity.Status.ToString();
                item["version"] = entity.Version;
                item["endpoint"] = entity.Endpoint;
                item["parentId"] = entity.ParentId;
                item["resourceVersion"] = entity.ResourceVersion;
                item["apiVersion"] = entity.ApiVersion;
                item["replicas"] = entity.Replicas;
                item["chartName"] = entity.ChartName;
                item["labels"] = entity.Labels;
                entities.Add(item);
            }
            root["entities"] = entities;
            List<object> relations = new List<object>();
            for (i = 0; i < snapshot.Relations.Count; i++)
            {
                Relation relation = snapshot.Relations[i];
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["id"] = relation.Id;
                item["fromId"] = relation.FromId;
                item["toId"] = relation.ToId;
                item["kind"] = relation.Kind.ToString();
                item["protocol"] = relation.Protocol;
                relations.Add(item);
            }
            root["relations"] = relations;
            return root;
        }

        private static TopologySnapshot FromJson(object root)
        {
            if (MiniJson.Object(root) == null) throw new FormatException("设计文件根节点必须是 JSON 对象");
            IList<object> entityItems = MiniJson.Array(MiniJson.Get(root, "entities"));
            IList<object> relationItems = MiniJson.Array(MiniJson.Get(root, "relations"));
            if (entityItems == null || relationItems == null) throw new FormatException("设计文件缺少 entities 或 relations");
            TopologySnapshot snapshot = new TopologySnapshot();
            snapshot.Revision = MiniJson.String(root, "revision", "");
            snapshot.ContextName = MiniJson.String(root, "contextName", "本地设计");
            snapshot.Source = "design";
            string observed = MiniJson.String(root, "observedAt", "");
            DateTime observedAt;
            if (DateTime.TryParse(observed, out observedAt)) snapshot.ObservedAt = observedAt;
            int i;
            for (i = 0; i < entityItems.Count; i++)
            {
                object item = entityItems[i];
                ManagedEntity entity = new ManagedEntity();
                entity.Id = MiniJson.String(item, "id", "");
                entity.Uid = MiniJson.String(item, "uid", entity.Id);
                entity.Kind = ParseEnum<EntityKind>(MiniJson.String(item, "kind", "Unknown"));
                entity.Name = MiniJson.String(item, "name", "");
                entity.Namespace = MiniJson.String(item, "namespace", "");
                entity.Status = ParseEnum<RunStatus>(MiniJson.String(item, "status", "Unknown"));
                entity.Version = MiniJson.String(item, "version", "");
                entity.Endpoint = MiniJson.String(item, "endpoint", "");
                entity.ParentId = MiniJson.String(item, "parentId", "");
                entity.ResourceVersion = MiniJson.String(item, "resourceVersion", "");
                entity.ApiVersion = MiniJson.String(item, "apiVersion", "");
                entity.Source = "design";
                entity.Replicas = MiniJson.Int32(item, "replicas", -1);
                entity.ChartName = MiniJson.String(item, "chartName", "");
                IDictionary<string, object> labels = MiniJson.Object(MiniJson.Get(item, "labels"));
                if (labels != null)
                    foreach (KeyValuePair<string, object> pair in labels)
                        entity.Labels[pair.Key] = pair.Value == null ? "" : pair.Value.ToString();
                snapshot.Entities.Add(entity);
            }
            for (i = 0; i < relationItems.Count; i++)
            {
                object item = relationItems[i];
                Relation relation = new Relation();
                relation.Id = MiniJson.String(item, "id", "rel-" + Guid.NewGuid().ToString("N"));
                relation.FromId = MiniJson.String(item, "fromId", "");
                relation.ToId = MiniJson.String(item, "toId", "");
                relation.Kind = ParseEnum<RelationKind>(MiniJson.String(item, "kind", "Unknown"));
                relation.Protocol = MiniJson.String(item, "protocol", "");
                snapshot.Relations.Add(relation);
            }
            string error = ValidateSnapshot(snapshot);
            if (error.Length > 0) throw new FormatException(error);
            return snapshot;
        }

        private static T ParseEnum<T>(string name)
        {
            try { return (T)Enum.Parse(typeof(T), name, false); }
            catch { return (T)Enum.ToObject(typeof(T), 0); }
        }
    }
}
