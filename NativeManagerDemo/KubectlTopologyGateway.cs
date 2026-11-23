using System;
using System.Collections.Generic;
using System.Globalization;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    /// <summary>执行只读资源查询；非零退出码等失败由实现抛出异常，返回值为 stdout。</summary>
    public interface IResourceQueryExecutor
    {
        string Execute(string tool, IList<string> arguments);
    }

    /// <summary>通过 kubectl 与 Helm 的只读查询构建集群拓扑快照。</summary>
    public class KubectlTopologyGateway
    {
        private readonly IResourceQueryExecutor _executor;

        public KubectlTopologyGateway(IResourceQueryExecutor executor)
        {
            if (executor == null) throw new ArgumentNullException("executor");
            _executor = executor;
        }

        public TopologySnapshot QueryTopology()
        {
            IList<object> clusterItems;
            IList<object> namespacedItems;
            string clusterError;
            string namespacedError;

            bool clusterOk = TryQueryItems("kubectl",
                Arguments("get", "namespaces,nodes,persistentvolumes", "-o", "json"),
                out clusterItems, out clusterError);
            bool namespacedOk = TryQueryItems("kubectl",
                Arguments("get", "deployments,replicasets,statefulsets,daemonsets,pods,services,ingresses,configmaps,secrets,persistentvolumeclaims,jobs,cronjobs", "--all-namespaces", "-o", "json"),
                out namespacedItems, out namespacedError);

            if (!clusterOk && !namespacedOk)
            {
                throw new InvalidOperationException("两组核心 Kubernetes 资源查询均失败，保留旧快照。集群级查询: " +
                    clusterError + "；命名空间级查询: " + namespacedError);
            }

            TopologyBuilder builder = new TopologyBuilder();
            if (!clusterOk) builder.AddWarning("集群级核心资源查询失败: " + clusterError);
            if (!namespacedOk) builder.AddWarning("命名空间级核心资源查询失败: " + namespacedError);
            if (clusterOk) builder.AddItems(clusterItems);
            if (namespacedOk) builder.AddItems(namespacedItems);

            QueryOptionalCrd(builder, "gateways.networking.istio.io", "Istio Gateway");
            QueryOptionalCrd(builder, "virtualservices.networking.istio.io", "Istio VirtualService");
            QueryOptionalCrd(builder, "destinationrules.networking.istio.io", "Istio DestinationRule");

            IList<object> helmItems;
            string helmError;
            bool helmOk = TryQueryArray("helm", Arguments("list", "-A", "-o", "json"),
                out helmItems, out helmError);
            if (!helmOk) builder.AddWarning("Helm 发布查询失败（Helm 可能缺失或无权访问）: " + helmError);

            builder.AddHelm(helmOk ? helmItems : null);
            builder.BuildRelations();
            builder.Finish();
            return builder.Snapshot;
        }

        private void QueryOptionalCrd(TopologyBuilder builder, string resourceName, string displayName)
        {
            IList<object> items;
            string error;
            bool ok = TryQueryItems("kubectl",
                Arguments("get", resourceName, "--all-namespaces", "-o", "json"),
                out items, out error);
            if (!ok)
            {
                builder.AddWarning(displayName + " 查询失败（CRD 或 RBAC 权限可能缺失）: " + error);
                return;
            }
            builder.AddItems(items);
        }

        private bool TryQueryItems(string tool, IList<string> arguments,
            out IList<object> items, out string error)
        {
            try
            {
                string stdout = _executor.Execute(tool, arguments);
                object root = MiniJson.Parse(stdout);
                items = MiniJson.Array(MiniJson.Get(root, "items"));
                if (items == null) throw new FormatException("查询结果缺少 items 数组");
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                items = null;
                error = ErrorText(ex);
                return false;
            }
        }

        private bool TryQueryArray(string tool, IList<string> arguments,
            out IList<object> items, out string error)
        {
            try
            {
                string stdout = _executor.Execute(tool, arguments);
                items = MiniJson.Array(MiniJson.Parse(stdout));
                if (items == null) throw new FormatException("查询结果不是 JSON 数组");
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                items = null;
                error = ErrorText(ex);
                return false;
            }
        }

        private static string ErrorText(Exception ex)
        {
            return string.IsNullOrEmpty(ex.Message) ? ex.GetType().Name : ex.Message;
        }

        private static IList<string> Arguments(params string[] arguments)
        {
            return new List<string>(arguments);
        }

        private sealed class ResourceRecord
        {
            internal object Item;
            internal ManagedEntity Entity;
        }

        private sealed class TopologyBuilder
        {
            private readonly TopologySnapshot _snapshot;
            private readonly Dictionary<string, ManagedEntity> _byId;
            private readonly Dictionary<string, ManagedEntity> _byKey;
            private readonly Dictionary<string, ManagedEntity> _namespaces;
            private readonly Dictionary<string, bool> _relations;
            private readonly List<ResourceRecord> _resources;
            private readonly ManagedEntity _cluster;

            internal TopologyBuilder()
            {
                _snapshot = new TopologySnapshot();
                _snapshot.Source = "cluster";
                _snapshot.IsStale = false;
                _byId = new Dictionary<string, ManagedEntity>(StringComparer.Ordinal);
                _byKey = new Dictionary<string, ManagedEntity>(StringComparer.Ordinal);
                _namespaces = new Dictionary<string, ManagedEntity>(StringComparer.Ordinal);
                _relations = new Dictionary<string, bool>(StringComparer.Ordinal);
                _resources = new List<ResourceRecord>();

                _cluster = NewSynthetic(EntityKind.Cluster, "cluster", "cluster", "");
                _cluster.ApiVersion = "v1";
                _cluster.Status = RunStatus.Running;
                AddEntity(_cluster);
            }

            internal TopologySnapshot Snapshot
            {
                get { return _snapshot; }
            }

            internal void AddWarning(string warning)
            {
                if (!string.IsNullOrEmpty(warning)) _snapshot.Warnings.Add(warning);
            }

            internal void AddItems(IList<object> items)
            {
                int i;
                for (i = 0; i < items.Count; i++)
                {
                    object item = items[i];
                    try
                    {
                        AddItem(item);
                    }
                    catch (Exception ex)
                    {
                        AddWarning("跳过无法解析的 Kubernetes 资源: " + ErrorText(ex));
                    }
                }
            }

            internal void AddHelm(IList<object> helmItems)
            {
                AddLabelBasedHelmReleases();
                if (helmItems != null) AddHelmList(helmItems);
                AddHelmCharts();
            }

            internal void BuildRelations()
            {
                AssignParents();
                AddOwnerReferences();
                AddServiceSelectors();
                AddIngressRoutes();
                AddIstioRelations();
            }

            internal void Finish()
            {
                _snapshot.ObservedAt = DateTime.Now;
                _snapshot.Revision = _snapshot.ObservedAt.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            }

            private void AddItem(object item)
            {
                IDictionary<string, object> map = MiniJson.Object(item);
                if (map == null)
                {
                    AddWarning("跳过非对象形式的 Kubernetes 资源");
                    return;
                }

                string rawKind = MiniJson.String(item, "kind", "");
                EntityKind kind = KindOf(rawKind);
                if (kind == EntityKind.Unknown)
                {
                    if (!string.IsNullOrEmpty(rawKind)) AddWarning("跳过不支持的 Kubernetes 资源类型: " + rawKind);
                    return;
                }

                object metadata = MiniJson.Get(item, "metadata");
                string name = MiniJson.String(metadata, "name", "");
                if (name.Length == 0)
                {
                    AddWarning("跳过缺少 metadata.name 的 " + rawKind + " 资源");
                    return;
                }

                string namespaceName = MiniJson.String(metadata, "namespace", "");
                string uid = MiniJson.String(metadata, "uid", "");
                ManagedEntity entity = new ManagedEntity();
                entity.Id = ResourceId(kind, namespaceName, name, uid);
                entity.Uid = uid;
                entity.Kind = kind;
                entity.Name = name;
                entity.Namespace = namespaceName;
                entity.Source = "cluster";
                entity.ApiVersion = MiniJson.String(item, "apiVersion", DefaultApiVersion(kind));
                entity.ResourceVersion = MiniJson.String(metadata, "resourceVersion", "");
                entity.Replicas = ReplicasOf(kind, item);
                entity.Status = StatusOf(kind, item);
                CopyLabels(MiniJson.Get(metadata, "labels"), entity.Labels);
                entity.ChartName = Label(entity, "helm.sh/chart");
                if (entity.ChartName.Length == 0) entity.ChartName = Label(entity, "chart");

                if (_byId.ContainsKey(entity.Id))
                {
                    AddWarning("跳过 Id 重复的资源: " + entity.Id);
                    return;
                }

                AddEntity(entity);
                ResourceRecord record = new ResourceRecord();
                record.Item = item;
                record.Entity = entity;
                _resources.Add(record);
            }

            private void AddEntity(ManagedEntity entity)
            {
                _snapshot.Entities.Add(entity);
                _byId[entity.Id] = entity;
                string key = Key(entity.Kind, entity.Namespace, entity.Name);
                if (!_byKey.ContainsKey(key)) _byKey[key] = entity;
                if (entity.Kind == EntityKind.Namespace) _namespaces[entity.Name] = entity;
            }

            private void AssignParents()
            {
                int i;
                for (i = 0; i < _snapshot.Entities.Count; i++)
                {
                    ManagedEntity entity = _snapshot.Entities[i];
                    if (entity.Kind != EntityKind.Namespace) continue;
                    _namespaces[entity.Name] = entity;
                    entity.ParentId = _cluster.Id;
                    AddRelation(_cluster, entity, RelationKind.Owns, "");
                }

                for (i = 0; i < _snapshot.Entities.Count; i++)
                {
                    ManagedEntity entity = _snapshot.Entities[i];
                    if (!IsNamespaced(entity) || entity.Namespace.Length == 0) continue;
                    ManagedEntity namespaceEntity;
                    if (!_namespaces.TryGetValue(entity.Namespace, out namespaceEntity))
                        namespaceEntity = EnsureNamespace(entity.Namespace);
                    entity.ParentId = namespaceEntity.Id;
                }
            }

            private ManagedEntity EnsureNamespace(string name)
            {
                ManagedEntity entity;
                if (_namespaces.TryGetValue(name, out entity)) return entity;
                string id = ResourceId(EntityKind.Namespace, "", name, "");
                if (_byId.ContainsKey(id)) id = "namespace/" + name;
                entity = NewSynthetic(EntityKind.Namespace, id, name, "");
                entity.ApiVersion = "v1";
                entity.Status = RunStatus.Unknown;
                entity.ParentId = _cluster.Id;
                AddEntity(entity);
                AddRelation(_cluster, entity, RelationKind.Owns, "");
                return entity;
            }

            private void AddOwnerReferences()
            {
                int i;
                for (i = 0; i < _resources.Count; i++)
                {
                    ResourceRecord record = _resources[i];
                    object metadata = MiniJson.Get(record.Item, "metadata");
                    IList<object> owners = MiniJson.Array(MiniJson.Get(metadata, "ownerReferences"));
                    if (owners == null) continue;
                    int j;
                    for (j = 0; j < owners.Count; j++)
                    {
                        object ownerReference = owners[j];
                        ManagedEntity owner = null;
                        string uid = MiniJson.String(ownerReference, "uid", "");
                        if (uid.Length > 0) _byId.TryGetValue(uid, out owner);
                        if (owner == null)
                        {
                            EntityKind ownerKind = KindOf(MiniJson.String(ownerReference, "kind", ""));
                            string ownerName = MiniJson.String(ownerReference, "name", "");
                            if (ownerKind != EntityKind.Unknown && ownerName.Length > 0)
                                owner = Find(ownerKind, record.Entity.Namespace, ownerName);
                        }
                        if (owner != null) AddRelation(owner, record.Entity, RelationKind.Owns, "");
                    }
                }
            }

            private void AddServiceSelectors()
            {
                int i;
                for (i = 0; i < _resources.Count; i++)
                {
                    ResourceRecord serviceRecord = _resources[i];
                    if (serviceRecord.Entity.Kind != EntityKind.Service) continue;
                    IDictionary<string, object> selector = MiniJson.Object(MiniJson.Get(
                        MiniJson.Get(serviceRecord.Item, "spec"), "selector"));
                    if (selector == null || selector.Count == 0) continue;

                    int j;
                    for (j = 0; j < _resources.Count; j++)
                    {
                        ResourceRecord podRecord = _resources[j];
                        if (podRecord.Entity.Kind != EntityKind.Pod ||
                            podRecord.Entity.Namespace != serviceRecord.Entity.Namespace) continue;
                        if (LabelsMatch(selector, podRecord.Entity.Labels))
                            AddRelation(serviceRecord.Entity, podRecord.Entity, RelationKind.Selects, "");
                    }
                }
            }

            private void AddIngressRoutes()
            {
                int i;
                for (i = 0; i < _resources.Count; i++)
                {
                    ResourceRecord record = _resources[i];
                    if (record.Entity.Kind != EntityKind.Ingress) continue;
                    object spec = MiniJson.Get(record.Item, "spec");
                    AddIngressBackend(record.Entity, MiniJson.Get(spec, "defaultBackend"));
                    AddIngressBackend(record.Entity, MiniJson.Get(spec, "backend"));

                    IList<object> rules = MiniJson.Array(MiniJson.Get(spec, "rules"));
                    if (rules == null) continue;
                    int j;
                    for (j = 0; j < rules.Count; j++)
                    {
                        object http = MiniJson.Get(rules[j], "http");
                        IList<object> paths = MiniJson.Array(MiniJson.Get(http, "paths"));
                        if (paths == null) continue;
                        int k;
                        for (k = 0; k < paths.Count; k++)
                            AddIngressBackend(record.Entity, MiniJson.Get(paths[k], "backend"));
                    }
                }
            }

            private void AddIngressBackend(ManagedEntity ingress, object backend)
            {
                if (backend == null) return;
                string serviceName = MiniJson.String(backend, "serviceName", "");
                if (serviceName.Length == 0)
                    serviceName = MiniJson.String(MiniJson.Get(backend, "service"), "name", "");
                if (serviceName.Length == 0) return;
                ManagedEntity service = Find(EntityKind.Service, ingress.Namespace, serviceName);
                if (service != null) AddRelation(ingress, service, RelationKind.Routes, "HTTP");
            }

            private void AddIstioRelations()
            {
                int i;
                for (i = 0; i < _resources.Count; i++)
                {
                    ResourceRecord record = _resources[i];
                    object spec = MiniJson.Get(record.Item, "spec");
                    if (record.Entity.Kind == EntityKind.DestinationRule)
                    {
                        AddServiceHostRelation(record.Entity, MiniJson.String(spec, "host", ""), RelationKind.Depends, "");
                        continue;
                    }
                    if (record.Entity.Kind != EntityKind.VirtualService) continue;

                    IList<object> gateways = MiniJson.Array(MiniJson.Get(spec, "gateways"));
                    if (gateways != null)
                    {
                        int j;
                        for (j = 0; j < gateways.Count; j++)
                        {
                            string gatewayName = ValueString(gateways[j]);
                            ManagedEntity gateway = FindGateway(record.Entity.Namespace, gatewayName);
                            if (gateway != null)
                                AddRelation(record.Entity, gateway, RelationKind.Depends, "");
                        }
                    }

                    AddVirtualServiceRules(record.Entity, MiniJson.Array(MiniJson.Get(spec, "http")), "HTTP");
                    AddVirtualServiceRules(record.Entity, MiniJson.Array(MiniJson.Get(spec, "tcp")), "TCP");
                    AddVirtualServiceRules(record.Entity, MiniJson.Array(MiniJson.Get(spec, "tls")), "TLS");
                }
            }

            private void AddVirtualServiceRules(ManagedEntity virtualService, IList<object> rules, string protocol)
            {
                if (rules == null) return;
                int i;
                for (i = 0; i < rules.Count; i++)
                {
                    IList<object> routes = MiniJson.Array(MiniJson.Get(rules[i], "route"));
                    if (routes == null) continue;
                    int j;
                    for (j = 0; j < routes.Count; j++)
                    {
                        object destination = MiniJson.Get(routes[j], "destination");
                        string host = MiniJson.String(destination, "host", "");
                        AddServiceHostRelation(virtualService, host, RelationKind.Routes, protocol);
                    }
                }
            }

            private void AddServiceHostRelation(ManagedEntity from, string host,
                RelationKind relationKind, string protocol)
            {
                string serviceName;
                string namespaceName;
                ParseServiceHost(from.Namespace, host, out serviceName, out namespaceName);
                if (serviceName.Length == 0) return;
                ManagedEntity service = Find(EntityKind.Service, namespaceName, serviceName);
                if (service != null) AddRelation(from, service, relationKind, protocol);
            }

            private ManagedEntity FindGateway(string defaultNamespace, string reference)
            {
                if (string.IsNullOrEmpty(reference) ||
                    string.Equals(reference, "mesh", StringComparison.OrdinalIgnoreCase)) return null;
                string namespaceName = defaultNamespace;
                string name = reference;
                int slash = reference.IndexOf('/');
                if (slash >= 0)
                {
                    namespaceName = reference.Substring(0, slash);
                    name = reference.Substring(slash + 1);
                }
                if (namespaceName.Length == 0 || name.Length == 0) return null;
                return Find(EntityKind.IstioGateway, namespaceName, name);
            }

            private void AddLabelBasedHelmReleases()
            {
                int i;
                for (i = 0; i < _resources.Count; i++)
                {
                    ManagedEntity resource = _resources[i].Entity;
                    string managedBy = Label(resource, "app.kubernetes.io/managed-by");
                    string heritage = Label(resource, "heritage");
                    bool helmManaged = string.Equals(managedBy, "Helm", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(heritage, "Tiller", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(heritage, "Helm", StringComparison.OrdinalIgnoreCase);

                    string releaseName = Label(resource, "meta.helm.sh/release-name");
                    bool explicitHelmIdentity = releaseName.Length > 0;
                    if (releaseName.Length == 0 && helmManaged)
                        releaseName = Label(resource, "app.kubernetes.io/instance");
                    if (releaseName.Length == 0 && (helmManaged || resource.ChartName.Length > 0))
                    {
                        releaseName = Label(resource, "release");
                        explicitHelmIdentity = releaseName.Length > 0;
                    }
                    if (releaseName.Length == 0 || (!helmManaged && !explicitHelmIdentity)) continue;

                    string releaseNamespace = Label(resource, "meta.helm.sh/release-namespace");
                    if (releaseNamespace.Length == 0) releaseNamespace = resource.Namespace;
                    if (releaseNamespace.Length == 0) releaseNamespace = "default";
                    ManagedEntity release = EnsureRelease(releaseName, releaseNamespace);
                    if (release.ChartName.Length == 0) release.ChartName = resource.ChartName;
                    AddRelation(release, resource, RelationKind.Manages, "");
                }
            }

            private void AddHelmList(IList<object> helmItems)
            {
                int i;
                for (i = 0; i < helmItems.Count; i++)
                {
                    object item = helmItems[i];
                    if (MiniJson.Object(item) == null)
                    {
                        AddWarning("跳过非对象形式的 Helm 发布记录");
                        continue;
                    }
                    string name = MiniJson.String(item, "name", "");
                    if (name.Length == 0)
                    {
                        AddWarning("跳过缺少 name 的 Helm 发布记录");
                        continue;
                    }
                    string namespaceName = MiniJson.String(item, "namespace", "default");
                    if (namespaceName.Length == 0) namespaceName = "default";
                    ManagedEntity release = EnsureRelease(name, namespaceName);
                    release.ChartName = MiniJson.String(item, "chart", release.ChartName);
                    release.Version = MiniJson.String(item, "app_version", release.Version);
                    release.ResourceVersion = MiniJson.String(item, "revision", release.ResourceVersion);
                    release.Status = HelmStatus(MiniJson.String(item, "status", ""));
                }
            }

            private ManagedEntity EnsureRelease(string name, string namespaceName)
            {
                ManagedEntity release = Find(EntityKind.HelmRelease, namespaceName, name);
                if (release != null) return release;
                string id = ResourceId(EntityKind.HelmRelease, namespaceName, name, "");
                release = NewSynthetic(EntityKind.HelmRelease, id, name, namespaceName);
                release.ApiVersion = "helm.sh/v3";
                release.Status = RunStatus.Unknown;
                AddEntity(release);
                return release;
            }

            private void AddHelmCharts()
            {
                int i;
                for (i = 0; i < _snapshot.Entities.Count; i++)
                {
                    ManagedEntity release = _snapshot.Entities[i];
                    if (release.Kind != EntityKind.HelmRelease || release.ChartName.Length == 0) continue;
                    ManagedEntity chart = Find(EntityKind.HelmChart, "", release.ChartName);
                    if (chart == null)
                    {
                        string id = ResourceId(EntityKind.HelmChart, "", release.ChartName, "");
                        chart = NewSynthetic(EntityKind.HelmChart, id, release.ChartName, "");
                        chart.ApiVersion = "helm.sh/v3";
                        chart.ChartName = release.ChartName;
                        chart.Status = RunStatus.Unknown;
                        AddEntity(chart);
                    }
                    AddRelation(release, chart, RelationKind.Depends, "");
                }
            }

            private void AddRelation(ManagedEntity from, ManagedEntity to,
                RelationKind kind, string protocol)
            {
                if (from == null || to == null || from.Id == to.Id) return;
                ManagedEntity ignored;
                if (!_byId.TryGetValue(from.Id, out ignored) || !_byId.TryGetValue(to.Id, out ignored)) return;
                string relationKey = kind.ToString() + "|" + from.Id + "|" + to.Id;
                if (_relations.ContainsKey(relationKey)) return;
                Relation relation = new Relation();
                relation.Id = relationKey;
                relation.FromId = from.Id;
                relation.ToId = to.Id;
                relation.Kind = kind;
                relation.Protocol = protocol;
                _relations[relationKey] = true;
                _snapshot.Relations.Add(relation);
            }

            private ManagedEntity Find(EntityKind kind, string namespaceName, string name)
            {
                ManagedEntity entity;
                _byKey.TryGetValue(Key(kind, namespaceName, name), out entity);
                return entity;
            }

            private static ManagedEntity NewSynthetic(EntityKind kind, string id, string name, string namespaceName)
            {
                ManagedEntity entity = new ManagedEntity();
                entity.Id = id;
                entity.Kind = kind;
                entity.Name = name;
                entity.Namespace = namespaceName;
                entity.Source = "cluster";
                return entity;
            }

            private static string ResourceId(EntityKind kind, string namespaceName, string name, string uid)
            {
                if (!string.IsNullOrEmpty(uid)) return uid;
                string kindName = kind.ToString().ToLowerInvariant();
                if (namespaceName.Length == 0) return kindName + "/" + name;
                return kindName + "/" + namespaceName + "/" + name;
            }

            private static string Key(EntityKind kind, string namespaceName, string name)
            {
                return ((int)kind).ToString(CultureInfo.InvariantCulture) + "|" + namespaceName + "|" + name;
            }

            private static bool IsNamespaced(ManagedEntity entity)
            {
                if (entity.Kind == EntityKind.Cluster || entity.Kind == EntityKind.Node ||
                    entity.Kind == EntityKind.Namespace || entity.Kind == EntityKind.PersistentVolume ||
                    entity.Kind == EntityKind.HelmChart || entity.Kind == EntityKind.YamlDocument) return false;
                return entity.Namespace.Length > 0;
            }

            private static EntityKind KindOf(string kind)
            {
                if (string.IsNullOrEmpty(kind)) return EntityKind.Unknown;
                switch (kind.ToLowerInvariant())
                {
                    case "namespace": return EntityKind.Namespace;
                    case "node": return EntityKind.Node;
                    case "persistentvolume": return EntityKind.PersistentVolume;
                    case "deployment": return EntityKind.Deployment;
                    case "replicaset": return EntityKind.ReplicaSet;
                    case "statefulset": return EntityKind.StatefulSet;
                    case "daemonset": return EntityKind.DaemonSet;
                    case "pod": return EntityKind.Pod;
                    case "service": return EntityKind.Service;
                    case "ingress": return EntityKind.Ingress;
                    case "configmap": return EntityKind.ConfigMap;
                    case "secret": return EntityKind.Secret;
                    case "persistentvolumeclaim": return EntityKind.PersistentVolumeClaim;
                    case "job": return EntityKind.Job;
                    case "cronjob": return EntityKind.CronJob;
                    case "gateway": return EntityKind.IstioGateway;
                    case "virtualservice": return EntityKind.VirtualService;
                    case "destinationrule": return EntityKind.DestinationRule;
                    default: return EntityKind.Unknown;
                }
            }

            private static string DefaultApiVersion(EntityKind kind)
            {
                switch (kind)
                {
                    case EntityKind.Namespace:
                    case EntityKind.Node:
                    case EntityKind.Pod:
                    case EntityKind.Service:
                    case EntityKind.ConfigMap:
                    case EntityKind.Secret:
                    case EntityKind.PersistentVolume:
                    case EntityKind.PersistentVolumeClaim:
                        return "v1";
                    case EntityKind.Deployment:
                    case EntityKind.ReplicaSet:
                    case EntityKind.StatefulSet:
                    case EntityKind.DaemonSet:
                        return "apps/v1";
                    case EntityKind.Ingress:
                        return "networking.k8s.io/v1";
                    case EntityKind.Job:
                    case EntityKind.CronJob:
                        return "batch/v1";
                    case EntityKind.IstioGateway:
                    case EntityKind.VirtualService:
                    case EntityKind.DestinationRule:
                        return "networking.istio.io/v1beta1";
                    default:
                        return "";
                }
            }

            private static int ReplicasOf(EntityKind kind, object item)
            {
                object spec = MiniJson.Get(item, "spec");
                object status = MiniJson.Get(item, "status");
                switch (kind)
                {
                    case EntityKind.Deployment:
                    case EntityKind.ReplicaSet:
                    case EntityKind.StatefulSet:
                        return MiniJson.Int32(spec, "replicas", 1);
                    case EntityKind.DaemonSet:
                        return MiniJson.Int32(status, "desiredNumberScheduled", -1);
                    case EntityKind.Job:
                        return MiniJson.Int32(spec, "parallelism", 1);
                    default:
                        return -1;
                }
            }

            private static RunStatus StatusOf(EntityKind kind, object item)
            {
                object metadata = MiniJson.Get(item, "metadata");
                if (!string.IsNullOrEmpty(MiniJson.String(metadata, "deletionTimestamp", "")))
                    return RunStatus.Stopped;
                object status = MiniJson.Get(item, "status");
                object spec = MiniJson.Get(item, "spec");
                string phase = MiniJson.String(status, "phase", "");

                switch (kind)
                {
                    case EntityKind.Namespace:
                        if (phase == "Active") return RunStatus.Running;
                        if (phase == "Terminating") return RunStatus.Stopped;
                        return RunStatus.Unknown;
                    case EntityKind.Node:
                        return NodeStatus(item);
                    case EntityKind.Pod:
                        return PodStatus(item, phase);
                    case EntityKind.Deployment:
                    case EntityKind.ReplicaSet:
                    case EntityKind.StatefulSet:
                        return WorkloadStatus(kind, item);
                    case EntityKind.DaemonSet:
                        return DaemonSetStatus(item);
                    case EntityKind.Job:
                        return JobStatus(item);
                    case EntityKind.CronJob:
                        if (MiniJson.Boolean(spec, "suspend", false)) return RunStatus.Stopped;
                        return RunStatus.Running;
                    case EntityKind.PersistentVolume:
                    case EntityKind.PersistentVolumeClaim:
                        if (phase == "Bound" || phase == "Available") return RunStatus.Running;
                        if (phase == "Pending") return RunStatus.Pending;
                        if (phase == "Released") return RunStatus.Stopped;
                        if (phase == "Failed" || phase == "Lost") return RunStatus.Failed;
                        return RunStatus.Unknown;
                    case EntityKind.Service:
                    case EntityKind.Ingress:
                    case EntityKind.ConfigMap:
                    case EntityKind.Secret:
                    case EntityKind.IstioGateway:
                    case EntityKind.VirtualService:
                    case EntityKind.DestinationRule:
                        return RunStatus.Running;
                    default:
                        return RunStatus.Unknown;
                }
            }

            private static RunStatus NodeStatus(object item)
            {
                object ready = Condition(item, "Ready");
                if (ready == null) return RunStatus.Unknown;
                if (!IsTrue(ready, "status")) return RunStatus.Warning;
                if (IsTrue(Condition(item, "MemoryPressure"), "status") ||
                    IsTrue(Condition(item, "DiskPressure"), "status") ||
                    IsTrue(Condition(item, "PIDPressure"), "status") ||
                    IsTrue(Condition(item, "NetworkUnavailable"), "status")) return RunStatus.Warning;
                return RunStatus.Running;
            }

            private static RunStatus PodStatus(object item, string phase)
            {
                if (phase == "Failed") return RunStatus.Failed;
                if (phase == "Succeeded") return RunStatus.Stopped;
                if (phase == "Pending") return RunStatus.Pending;
                if (phase != "Running") return RunStatus.Unknown;

                RunStatus containerStatus = ContainerStatus(MiniJson.Array(MiniJson.Get(
                    MiniJson.Get(item, "status"), "initContainerStatuses")));
                if (containerStatus == RunStatus.Failed) return RunStatus.Failed;
                RunStatus regularStatus = ContainerStatus(MiniJson.Array(MiniJson.Get(
                    MiniJson.Get(item, "status"), "containerStatuses")));
                if (regularStatus == RunStatus.Failed) return RunStatus.Failed;
                if (containerStatus == RunStatus.Warning || regularStatus == RunStatus.Warning)
                    return RunStatus.Warning;
                object ready = Condition(item, "Ready");
                if (ready != null && !IsTrue(ready, "status")) return RunStatus.Warning;
                return RunStatus.Running;
            }

            private static RunStatus ContainerStatus(IList<object> containers)
            {
                if (containers == null) return RunStatus.Unknown;
                RunStatus result = RunStatus.Unknown;
                int i;
                for (i = 0; i < containers.Count; i++)
                {
                    object state = MiniJson.Get(containers[i], "state");
                    object terminated = MiniJson.Get(state, "terminated");
                    if (terminated != null && MiniJson.Int32(terminated, "exitCode", 0) != 0)
                        return RunStatus.Failed;
                    object waiting = MiniJson.Get(state, "waiting");
                    string reason = MiniJson.String(waiting, "reason", "");
                    if (reason == "CrashLoopBackOff" || reason == "ImagePullBackOff" ||
                        reason == "ErrImagePull" || reason == "CreateContainerConfigError" ||
                        reason == "CreateContainerError" || reason == "InvalidImageName" ||
                        reason == "RunContainerError" || reason == "OOMKilled") result = RunStatus.Warning;
                }
                return result;
            }

            private static RunStatus WorkloadStatus(EntityKind kind, object item)
            {
                object spec = MiniJson.Get(item, "spec");
                object status = MiniJson.Get(item, "status");
                int desired = MiniJson.Int32(spec, "replicas", 1);
                if (desired == 0) return RunStatus.Stopped;
                object progressing = Condition(item, "Progressing");
                if (progressing != null && !IsTrue(progressing, "status") &&
                    MiniJson.String(progressing, "reason", "") == "ProgressDeadlineExceeded")
                    return RunStatus.Failed;
                object failed = Condition(item, "Failed");
                if (IsTrue(failed, "status")) return RunStatus.Failed;

                int ready = MiniJson.Int32(status, "readyReplicas", 0);
                int available = MiniJson.Int32(status, "availableReplicas", ready);
                int updated = MiniJson.Int32(status, "updatedReplicas", desired);
                if (ready >= desired && available >= desired &&
                    (kind != EntityKind.Deployment || updated >= desired)) return RunStatus.Running;
                if (ready > 0 || available > 0) return RunStatus.Warning;
                return RunStatus.Pending;
            }

            private static RunStatus DaemonSetStatus(object item)
            {
                object status = MiniJson.Get(item, "status");
                int desired = MiniJson.Int32(status, "desiredNumberScheduled", 0);
                int ready = MiniJson.Int32(status, "numberReady", 0);
                int unavailable = MiniJson.Int32(status, "numberUnavailable", 0);
                if (desired == 0) return RunStatus.Stopped;
                if (ready >= desired && unavailable == 0) return RunStatus.Running;
                if (ready > 0) return RunStatus.Warning;
                return RunStatus.Pending;
            }

            private static RunStatus JobStatus(object item)
            {
                object spec = MiniJson.Get(item, "spec");
                object status = MiniJson.Get(item, "status");
                if (MiniJson.Boolean(spec, "suspend", false)) return RunStatus.Stopped;
                if (IsTrue(Condition(item, "Complete"), "status")) return RunStatus.Stopped;
                if (IsTrue(Condition(item, "Failed"), "status")) return RunStatus.Failed;
                if (MiniJson.Int32(status, "active", 0) > 0) return RunStatus.Running;
                if (MiniJson.Int32(status, "succeeded", 0) > 0) return RunStatus.Stopped;
                if (MiniJson.Int32(status, "failed", 0) > 0) return RunStatus.Failed;
                return RunStatus.Pending;
            }

            private static RunStatus HelmStatus(string status)
            {
                switch (status.ToLowerInvariant())
                {
                    case "deployed": return RunStatus.Running;
                    case "pending-install":
                    case "pending-upgrade":
                    case "pending-rollback":
                        return RunStatus.Pending;
                    case "failed": return RunStatus.Failed;
                    case "uninstalling":
                    case "uninstalled":
                    case "superseded":
                        return RunStatus.Stopped;
                    default: return RunStatus.Unknown;
                }
            }

            private static object Condition(object item, string type)
            {
                IList<object> conditions = MiniJson.Array(MiniJson.Get(MiniJson.Get(item, "status"), "conditions"));
                if (conditions == null) return null;
                int i;
                for (i = 0; i < conditions.Count; i++)
                {
                    object condition = conditions[i];
                    if (string.Equals(MiniJson.String(condition, "type", ""), type,
                        StringComparison.OrdinalIgnoreCase)) return condition;
                }
                return null;
            }

            private static bool IsTrue(object map, string name)
            {
                return map != null && string.Equals(MiniJson.String(map, name, ""), "True",
                    StringComparison.OrdinalIgnoreCase);
            }

            private static bool LabelsMatch(IDictionary<string, object> selector,
                IDictionary<string, string> labels)
            {
                foreach (KeyValuePair<string, object> pair in selector)
                {
                    string value;
                    if (!labels.TryGetValue(pair.Key, out value)) return false;
                    if (value != ValueString(pair.Value)) return false;
                }
                return true;
            }

            private static void CopyLabels(object source, IDictionary<string, string> destination)
            {
                IDictionary<string, object> labels = MiniJson.Object(source);
                if (labels == null) return;
                foreach (KeyValuePair<string, object> pair in labels)
                    destination[pair.Key] = ValueString(pair.Value);
            }

            private static string Label(ManagedEntity entity, string name)
            {
                string value;
                if (entity.Labels.TryGetValue(name, out value)) return value;
                return "";
            }

            private static string ValueString(object value)
            {
                if (value == null) return "";
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            private static void ParseServiceHost(string defaultNamespace, string host,
                out string serviceName, out string namespaceName)
            {
                serviceName = "";
                namespaceName = defaultNamespace;
                if (string.IsNullOrEmpty(host)) return;
                host = host.Trim();
                if (host.Length == 0 || host == "*") return;

                int slash = host.IndexOf('/');
                if (slash > 0)
                {
                    namespaceName = host.Substring(0, slash);
                    serviceName = host.Substring(slash + 1);
                    return;
                }

                string[] parts = host.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return;
                serviceName = parts[0];
                if (parts.Length >= 3 && parts[2] == "svc") namespaceName = parts[1];
                else if (parts.Length == 2) namespaceName = parts[1];
                else if (parts.Length > 2)
                {
                    serviceName = "";
                    return;
                }
                if (namespaceName.Length == 0) serviceName = "";
            }
        }
    }
}
