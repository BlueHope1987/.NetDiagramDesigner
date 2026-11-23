using System;
using System.Collections.Generic;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    public enum RiskLevel { None, Low, High }

    public static class VerbGroup
    {
        public const string Common = "常用";
        public const string Workload = "工作负载";
        public const string Network = "网络";
        public const string Storage = "存储";
        public const string Config = "配置";
        public const string Helm = "Helm 应用";
        public const string Danger = "危险操作";
    }

    /// <summary>命令行与 MMC 共用的动词元数据，也是后续 MMC 管理单元扩展点。</summary>
    public class VerbDescriptor
    {
        public string Name;
        public string Description;
        public string Verb;
        public string Group;
        public bool Enabled;
        public RiskLevel Risk;
        public string CommandTemplate;
        public string IconKey;
        public string ParameterHint;
        public bool RequiresOnline;
        public bool SupportsDesign;
        public bool RefreshAfter;
        public string DisabledReason;

        public VerbDescriptor()
        {
            Name = "";
            Description = "";
            Verb = "";
            Group = VerbGroup.Common;
            CommandTemplate = "";
            IconKey = "";
            ParameterHint = "";
            RequiresOnline = true;
            SupportsDesign = false;
            RefreshAfter = false;
            DisabledReason = "";
        }
    }

    public static class VerbMatrix
    {
        public static IList<VerbDescriptor> GetVerbs(EntityKind kind, RunStatus status)
        {
            List<VerbDescriptor> verbs = new List<VerbDescriptor>();
            bool isHelm = kind == EntityKind.HelmRelease;
            bool isYaml = kind == EntityKind.YamlDocument;
            if (!isHelm && !isYaml && kind != EntityKind.Cluster && kind != EntityKind.HelmChart)
                verbs.Add(V("查看详情", "describe", "查看资源定义、状态和事件", VerbGroup.Common, RiskLevel.Low,
                    "kubectl describe {kind} {name}{ns}", true, false, true, false));
            if (kind == EntityKind.HelmRelease)
            {
                verbs.Add(V("发布状态", "status", "查看 Helm Release 状态", VerbGroup.Helm, RiskLevel.Low,
                    "helm status {name}{ns}", true, false, false, false));
                verbs.Add(V("修订历史", "history", "查看 Helm Release 修订历史", VerbGroup.Helm, RiskLevel.Low,
                    "helm history {name}{ns}", true, false, false, false));
                verbs.Add(V("回滚发布", "rollback", "回滚到上一修订，需要确认", VerbGroup.Danger, RiskLevel.High,
                    "helm rollback {name}{ns}", true, false, false, true));
                verbs.Add(V("卸载发布", "uninstall", "卸载 Helm Release 及其资源，需要确认", VerbGroup.Danger, RiskLevel.High,
                    "helm uninstall {name}{ns}", true, false, false, true));
            }
            if (kind == EntityKind.HelmChart)
            {
                verbs.Add(V("查看 Chart", "show", "查看 Chart 基本信息", VerbGroup.Helm, RiskLevel.Low,
                    "helm show chart {chart}", true, false, false, false));
            }

            if (IsWorkload(kind))
            {
                verbs.Add(V("伸缩副本", "scale", "输入新的副本数；设计模式只修改本地快照", VerbGroup.Workload, RiskLevel.Low,
                    "kubectl scale {kind} {name} --replicas={replicas}{ns}", true, true, true, true));
                verbs.Add(V("发布状态", "rollout", "查看滚动发布状态", VerbGroup.Workload, RiskLevel.Low,
                    "kubectl rollout status {kind} {name}{ns}", true, false, false, false));
                verbs.Add(V("滚动重启", "restart", "触发滚动重启，需要确认", VerbGroup.Workload, RiskLevel.High,
                    "kubectl rollout restart {kind} {name}{ns}", true, false, false, true));
            }
            if (kind == EntityKind.Pod)
            {
                verbs.Add(V("查看日志", "logs", "读取最近 200 行 Pod 日志，可随时取消", VerbGroup.Workload, RiskLevel.Low,
                    "kubectl logs {name}{ns} --tail=200", true, false, false, false));
                VerbDescriptor exec = V("交互终端", "exec", "首版未嵌入交互终端；可在系统终端执行 kubectl exec", VerbGroup.Workload,
                    RiskLevel.High, "", false, false, false, false);
                exec.DisabledReason = "不支持交互终端；非交互 exec 可在命令行输入，并需确认";
                verbs.Add(exec);
                VerbDescriptor forward = V("端口转发", "port-forward", "长驻端口转发未在首版开放", VerbGroup.Network,
                    RiskLevel.Low, "", false, false, false, false);
                forward.DisabledReason = "长驻任务会阻塞统一管理器，请使用系统终端";
                verbs.Add(forward);
            }
            if (kind == EntityKind.ConfigMap || kind == EntityKind.Secret)
                verbs.Add(V("查看数据", "data", "查看 YAML 数据；Secret 内容请谨慎分享", VerbGroup.Config, RiskLevel.Low,
                    "kubectl get {kind} {name} -o yaml{ns}", true, false, true, false));
            if (kind == EntityKind.PersistentVolume || kind == EntityKind.PersistentVolumeClaim)
                verbs.Add(V("查看存储详情", "describe-storage", "查看存储绑定、容量和访问模式", VerbGroup.Storage, RiskLevel.Low,
                    "kubectl describe {kind} {name}{ns}", true, false, true, false));
            if (kind == EntityKind.Node)
            {
                verbs.Add(V("封锁调度", "cordon", "标记节点不可调度，需要确认", VerbGroup.Danger, RiskLevel.High,
                    "kubectl cordon {name}", true, false, false, true));
                verbs.Add(V("解除封锁", "uncordon", "恢复节点调度", VerbGroup.Common, RiskLevel.Low,
                    "kubectl uncordon {name}", true, false, false, true));
                verbs.Add(V("排空节点", "drain", "驱逐节点 Pod，需要确认", VerbGroup.Danger, RiskLevel.High,
                    "kubectl drain {name} --ignore-daemonsets --delete-emptydir-data", true, false, false, true));
            }
            if (!isYaml && kind != EntityKind.Cluster && kind != EntityKind.HelmChart && kind != EntityKind.HelmRelease)
            {
                VerbDescriptor edit = V("交互编辑", "edit", "首版未嵌入交互编辑器", VerbGroup.Common, RiskLevel.Low,
                    "", false, false, false, false);
                edit.DisabledReason = "请使用 kubectl get -o yaml 查看后在受控流程中修改";
                verbs.Add(edit);
            }
            if (kind != EntityKind.Cluster && kind != EntityKind.HelmChart)
                verbs.Add(V("删除", "delete", isHelm ? "请使用“卸载发布”" : "删除当前对象；设计模式只删除本地对象",
                    isHelm ? VerbGroup.Helm : VerbGroup.Danger, RiskLevel.High,
                    isHelm ? "" : (isYaml ? "" : "kubectl delete {kind} {name}{ns}"),
                    !isHelm, true, true, true));
            int i;
            for (i = 0; i < verbs.Count; i++)
            {
                VerbDescriptor verb = verbs[i];
                if (verb.Verb == "scale") verb.ParameterHint = "副本数（0 到 10000）";
                if (verb.Verb == "restart" || verb.Verb == "cordon" || verb.Verb == "drain") verb.RefreshAfter = true;
            }
            return verbs;
        }

        public static string RenderCommand(VerbDescriptor verb, ManagedEntity entity)
        {
            if (verb == null || entity == null) return "";
            string command = verb.CommandTemplate;
            command = command.Replace("{kind}", KindName(entity.Kind));
            command = command.Replace("{name}", entity.Name);
            command = command.Replace("{chart}", entity.ChartName);
            command = command.Replace("{replicas}", "{replicas}");
            bool namespaced = DesignTopologyStore.IsNamespaced(entity.Kind);
            if (entity.Kind == EntityKind.HelmRelease) namespaced = true;
            string ns = namespaced ? " -n " + (string.IsNullOrEmpty(entity.Namespace) ? "default" : entity.Namespace) : "";
            command = command.Replace("{ns}", ns);
            return command;
        }

        public static string KindName(EntityKind kind)
        {
            switch (kind)
            {
                case EntityKind.Cluster: return "cluster";
                case EntityKind.Node: return "node";
                case EntityKind.Namespace: return "namespace";
                case EntityKind.Deployment: return "deployment";
                case EntityKind.ReplicaSet: return "replicaset";
                case EntityKind.StatefulSet: return "statefulset";
                case EntityKind.DaemonSet: return "daemonset";
                case EntityKind.Pod: return "pod";
                case EntityKind.Service: return "service";
                case EntityKind.Ingress: return "ingress";
                case EntityKind.ConfigMap: return "configmap";
                case EntityKind.Secret: return "secret";
                case EntityKind.PersistentVolume: return "persistentvolume";
                case EntityKind.PersistentVolumeClaim: return "persistentvolumeclaim";
                case EntityKind.Job: return "job";
                case EntityKind.CronJob: return "cronjob";
                case EntityKind.Etcd: return "etcd";
                case EntityKind.HelmRelease: return "helmrelease";
                case EntityKind.HelmChart: return "helmchart";
                case EntityKind.IstioGateway: return "gateway.networking.istio.io";
                case EntityKind.VirtualService: return "virtualservice.networking.istio.io";
                case EntityKind.DestinationRule: return "destinationrule.networking.istio.io";
                case EntityKind.YamlDocument: return "yamldocument";
                default: return "unknown";
            }
        }

        private static bool IsWorkload(EntityKind kind)
        {
            return kind == EntityKind.Deployment || kind == EntityKind.ReplicaSet || kind == EntityKind.StatefulSet ||
                kind == EntityKind.DaemonSet || kind == EntityKind.Job || kind == EntityKind.CronJob;
        }

        private static VerbDescriptor V(string name, string verb, string description, string group,
            RiskLevel risk, string command, bool enabled, bool supportsDesign, bool requiresOnline, bool refreshAfter)
        {
            VerbDescriptor descriptor = new VerbDescriptor();
            descriptor.Name = name;
            descriptor.Verb = verb;
            descriptor.Description = description;
            descriptor.Group = group;
            descriptor.Risk = risk;
            descriptor.CommandTemplate = command;
            descriptor.Enabled = enabled;
            descriptor.SupportsDesign = supportsDesign;
            descriptor.RequiresOnline = requiresOnline;
            descriptor.RefreshAfter = refreshAfter;
            return descriptor;
        }
    }
}
