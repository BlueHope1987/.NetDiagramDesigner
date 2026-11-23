using System.Drawing;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    /// <summary>资源类型决定填充色，运行状态决定边框色；领域层不保存任何颜色。</summary>
    public static class ResourceVisualPalette
    {
        public static Color FillFor(EntityKind kind)
        {
            switch (kind)
            {
                case EntityKind.Cluster: return Color.FromArgb(220, 230, 242);
                case EntityKind.Node: return Color.FromArgb(215, 227, 239);
                case EntityKind.Namespace: return Color.FromArgb(232, 240, 247);
                case EntityKind.Deployment: return Color.FromArgb(217, 232, 251);
                case EntityKind.ReplicaSet: return Color.FromArgb(227, 237, 252);
                case EntityKind.StatefulSet: return Color.FromArgb(212, 227, 245);
                case EntityKind.DaemonSet: return Color.FromArgb(225, 233, 246);
                case EntityKind.Pod: return Color.FromArgb(234, 243, 255);
                case EntityKind.Service: return Color.FromArgb(216, 243, 238);
                case EntityKind.Ingress: return Color.FromArgb(214, 237, 248);
                case EntityKind.ConfigMap: return Color.FromArgb(241, 231, 250);
                case EntityKind.Secret: return Color.FromArgb(248, 225, 234);
                case EntityKind.PersistentVolume: return Color.FromArgb(230, 224, 212);
                case EntityKind.PersistentVolumeClaim: return Color.FromArgb(239, 232, 220);
                case EntityKind.Job: return Color.FromArgb(226, 240, 217);
                case EntityKind.CronJob: return Color.FromArgb(237, 243, 218);
                case EntityKind.Etcd: return Color.FromArgb(224, 229, 234);
                case EntityKind.HelmRelease: return Color.FromArgb(232, 222, 248);
                case EntityKind.HelmChart: return Color.FromArgb(240, 232, 251);
                case EntityKind.IstioGateway: return Color.FromArgb(224, 231, 255);
                case EntityKind.VirtualService: return Color.FromArgb(230, 233, 250);
                case EntityKind.DestinationRule: return Color.FromArgb(221, 228, 245);
                case EntityKind.YamlDocument: return Color.FromArgb(255, 243, 214);
                default: return Color.FromArgb(236, 239, 241);
            }
        }

        public static Color BorderFor(RunStatus status)
        {
            switch (status)
            {
                case RunStatus.Pending: return Color.FromArgb(176, 125, 22);
                case RunStatus.Running: return Color.FromArgb(46, 125, 80);
                case RunStatus.Stopped: return Color.FromArgb(96, 108, 120);
                case RunStatus.Warning: return Color.FromArgb(217, 119, 6);
                case RunStatus.Failed: return Color.FromArgb(190, 55, 55);
                default: return Color.FromArgb(117, 128, 138);
            }
        }

        public static Color TextFor(EntityKind kind)
        {
            return Color.FromArgb(38, 55, 70);
        }

        public static Color HeaderFor(EntityKind kind)
        {
            if (kind == EntityKind.Namespace) return Color.FromArgb(82, 109, 130);
            Color fill = FillFor(kind);
            return Color.FromArgb(
                (int)(fill.R * 0.58f),
                (int)(fill.G * 0.58f),
                (int)(fill.B * 0.58f));
        }

        public static string StatusText(RunStatus status)
        {
            switch (status)
            {
                case RunStatus.Pending: return "等待中";
                case RunStatus.Running: return "运行中";
                case RunStatus.Stopped: return "已停止";
                case RunStatus.Warning: return "警告";
                case RunStatus.Failed: return "失败";
                default: return "未知";
            }
        }
    }
}
