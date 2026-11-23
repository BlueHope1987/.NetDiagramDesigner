using System;
using System.Collections.Generic;
using System.Drawing;
using DiagramDesigner.Core;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    // ============================================================
    // K8s 产品级图形元件库
    //   不使用内置通用图形（矩形/椭圆/菱形等），
    //   而是为每种 K8s 资源定义具有辨识度的产品级轮廓。
    //   全部图形通过 ShapeTypeRegistry.Register 注册到控件，
    //   不修改 DiagramDesigner 任何源码。
    //
    // 视觉规范：
    //   - 统一采用圆角矩形基底，确保拓扑图风格一致
    //   - 每种资源用独特的内部标识（图标/结构）区分
    //   - 状态色（Pending/Running/Warning/Failed 等）由投影层
    //     通过 ConfigureStates 注入，不在此硬编码
    // ============================================================

    /// <summary>K8s 资源图形类型的统一注册入口。</summary>
    public static class K8sShapes
    {
        public const string CategoryCore = "K8s 核心";
        public const string CategoryWorkloads = "工作负载";
        public const string CategoryNetwork = "网络与配置";
        public const string CategoryHelm = "Helm 应用包";
        public const string CategoryIstio = "Istio 服务网格";
        public const string CategoryDesign = "设计辅助";

        // 工具箱同时显示中文含义和 kubectl/helm/istioctl 中的英文资源名。
        public const string Cluster = "集群 Cluster";
        public const string Node = "节点 Node";
        public const string Namespace = "命名空间 Namespace";
        public const string Deployment = "部署 Deployment";
        public const string ReplicaSet = "副本集 ReplicaSet";
        public const string StatefulSet = "有状态集 StatefulSet";
        public const string DaemonSet = "守护集 DaemonSet";
        public const string Pod = "容器组 Pod";
        public const string Service = "服务 Service";
        public const string Ingress = "入口 Ingress";
        public const string ConfigMap = "配置 ConfigMap";
        public const string Secret = "密钥 Secret";
        public const string PersistentVolume = "持久卷 PV";
        public const string PersistentVolumeClaim = "卷声明 PVC";
        public const string Job = "任务 Job";
        public const string CronJob = "定时任务 CronJob";
        public const string Etcd = "配置存储 etcd";
        public const string HelmRelease = "Helm 发布 Release";
        public const string HelmChart = "Helm 模板 Chart";
        public const string IstioGateway = "网格网关 Gateway";
        public const string VirtualService = "虚拟服务 VirtualService";
        public const string DestinationRule = "目标规则 DestinationRule";
        public const string YamlDocument = "YAML 设计文档";

        private static readonly Color Accent = Color.FromArgb(74, 111, 165);

        /// <summary>注册全部命令行管理对象及设计辅助图形。</summary>
        public static void RegisterAll()
        {
            ShapeTypeRegistry.Instance.Register(CreateCluster());
            ShapeTypeRegistry.Instance.Register(CreateNode());
            ShapeTypeRegistry.Instance.Register(CreateNamespace());
            ShapeTypeRegistry.Instance.Register(CreateDeployment());
            ShapeTypeRegistry.Instance.Register(CreateReplicaSet());
            ShapeTypeRegistry.Instance.Register(CreateStatefulSet());
            ShapeTypeRegistry.Instance.Register(CreateDaemonSet());
            ShapeTypeRegistry.Instance.Register(CreatePod());
            ShapeTypeRegistry.Instance.Register(CreateService());
            ShapeTypeRegistry.Instance.Register(CreateIngress());
            ShapeTypeRegistry.Instance.Register(CreateConfigMap());
            ShapeTypeRegistry.Instance.Register(CreateSecret());
            ShapeTypeRegistry.Instance.Register(CreatePersistentVolume());
            ShapeTypeRegistry.Instance.Register(CreatePersistentVolumeClaim());
            ShapeTypeRegistry.Instance.Register(CreateJob());
            ShapeTypeRegistry.Instance.Register(CreateCronJob());
            ShapeTypeRegistry.Instance.Register(CreateEtcd());
            ShapeTypeRegistry.Instance.Register(CreateHelmRelease());
            ShapeTypeRegistry.Instance.Register(CreateHelmChart());
            ShapeTypeRegistry.Instance.Register(CreateIstioGateway());
            ShapeTypeRegistry.Instance.Register(CreateVirtualService());
            ShapeTypeRegistry.Instance.Register(CreateDestinationRule());
            ShapeTypeRegistry.Instance.Register(CreateYamlDocument());
        }

        /// <summary>把 EntityKind 映射到对应的图形类型名称。</summary>
        public static string ShapeNameOf(EntityKind kind)
        {
            switch (kind)
            {
                case EntityKind.Cluster: return Cluster;
                case EntityKind.Node: return Node;
                case EntityKind.Namespace: return Namespace;
                case EntityKind.Deployment: return Deployment;
                case EntityKind.ReplicaSet: return ReplicaSet;
                case EntityKind.StatefulSet: return StatefulSet;
                case EntityKind.DaemonSet: return DaemonSet;
                case EntityKind.Pod: return Pod;
                case EntityKind.Service: return Service;
                case EntityKind.Ingress: return Ingress;
                case EntityKind.ConfigMap: return ConfigMap;
                case EntityKind.Secret: return Secret;
                case EntityKind.PersistentVolume: return PersistentVolume;
                case EntityKind.PersistentVolumeClaim: return PersistentVolumeClaim;
                case EntityKind.Job: return Job;
                case EntityKind.CronJob: return CronJob;
                case EntityKind.Etcd: return Etcd;
                case EntityKind.HelmRelease: return HelmRelease;
                case EntityKind.HelmChart: return HelmChart;
                case EntityKind.IstioGateway: return IstioGateway;
                case EntityKind.VirtualService: return VirtualService;
                case EntityKind.DestinationRule: return DestinationRule;
                case EntityKind.YamlDocument: return YamlDocument;
                default: return Pod;
            }
        }

        // ===== 基底工厂 =====

        private static ShapeType CreateBase(string name, float w, float h)
        {
            EntityKind kind = KindOfName(name);
            ShapeType t = new ShapeType();
            t.Name = name;
            t.Category = CategoryFor(name);
            t.Description = DescriptionFor(name);
            t.DefaultWidth = w;
            t.DefaultHeight = h;
            t.DefaultFillColor = new XmlColor(ResourceVisualPalette.FillFor(kind));
            t.DefaultBorderColor = new XmlColor(ResourceVisualPalette.BorderFor(RunStatus.Unknown));
            t.DefaultTextColor = new XmlColor(ResourceVisualPalette.TextFor(kind));
            t.SupportsMembers = false;
            return t;
        }

        private static EntityKind KindOfName(string name)
        {
            Array kinds = Enum.GetValues(typeof(EntityKind));
            for (int i = 1; i < kinds.Length; i++)
            {
                EntityKind kind = (EntityKind)kinds.GetValue(i);
                if (ShapeNameOf(kind) == name) return kind;
            }
            return EntityKind.Unknown;
        }

        private static string CategoryFor(string name)
        {
            if (name == Deployment || name == ReplicaSet || name == StatefulSet ||
                name == DaemonSet || name == Pod || name == Job || name == CronJob)
                return CategoryWorkloads;
            if (name == Service || name == Ingress || name == ConfigMap || name == Secret ||
                name == PersistentVolume || name == PersistentVolumeClaim)
                return CategoryNetwork;
            if (name == HelmRelease || name == HelmChart) return CategoryHelm;
            if (name == IstioGateway || name == VirtualService || name == DestinationRule)
                return CategoryIstio;
            if (name == YamlDocument) return CategoryDesign;
            return CategoryCore;
        }

        private static string DescriptionFor(string name)
        {
            if (name == Cluster) return "Cluster：kubectl 当前上下文对应的集群入口";
            if (name == Node) return "Node：运行 Pod 的计算节点";
            if (name == Namespace) return "Namespace：唯一真实容器，用于资源分组与隔离";
            if (name == Deployment) return "Deployment：无状态应用副本与滚动升级";
            if (name == ReplicaSet) return "ReplicaSet：维持指定数量的 Pod 副本";
            if (name == StatefulSet) return "StatefulSet：有稳定身份和存储的应用";
            if (name == DaemonSet) return "DaemonSet：在符合条件的节点上各运行一个 Pod";
            if (name == Pod) return "Pod：Kubernetes 最小调度单元";
            if (name == Service) return "Service：通过选择器为一组 Pod 提供稳定访问入口";
            if (name == Ingress) return "Ingress：把外部 HTTP/HTTPS 流量路由到 Service";
            if (name == ConfigMap) return "ConfigMap：保存非敏感配置";
            if (name == Secret) return "Secret：保存密码、令牌等敏感配置";
            if (name == PersistentVolume) return "PersistentVolume：集群级持久存储";
            if (name == PersistentVolumeClaim) return "PersistentVolumeClaim：命名空间内存储申请";
            if (name == Job) return "Job：运行到完成的一次性任务";
            if (name == CronJob) return "CronJob：按计划创建 Job";
            if (name == Etcd) return "etcd：集群键值配置存储（设计辅助对象）";
            if (name == HelmRelease) return "Helm Release：由 helm 安装并管理的一组资源";
            if (name == HelmChart) return "Helm Chart：可参数化安装的应用模板";
            if (name == IstioGateway) return "Istio Gateway：服务网格边缘流量入口";
            if (name == VirtualService) return "VirtualService：按主机和路径定义网格路由";
            if (name == DestinationRule) return "DestinationRule：定义服务子集与流量策略";
            return "YAML Document：本地设计与调试文档，不代表已部署资源";
        }

        // ===== 渲染命令辅助 =====

        private static RenderCommand RoundedRect(float x, float y, float w, float h, float r)
        {
            RenderCommand c = new RenderCommand();
            c.CommandType = RenderCommandType.RoundedRect;
            c.X = x; c.Y = y; c.Width = w; c.Height = h;
            c.CornerRadius = r;
            c.UseShapeColors = true;
            c.Fill = true; c.Stroke = true;
            return c;
        }

        private static RenderCommand Rect(float x, float y, float w, float h)
        {
            RenderCommand c = new RenderCommand();
            c.CommandType = RenderCommandType.Rectangle;
            c.X = x; c.Y = y; c.Width = w; c.Height = h;
            c.UseShapeColors = true;
            c.Fill = true; c.Stroke = true;
            return c;
        }

        private static RenderCommand RectStroke(float x, float y, float w, float h)
        {
            RenderCommand c = new RenderCommand();
            c.CommandType = RenderCommandType.Rectangle;
            c.X = x; c.Y = y; c.Width = w; c.Height = h;
            c.UseShapeColors = false;
            c.StrokeColor = new XmlColor(Accent);
            c.Fill = false; c.Stroke = true;
            c.StrokeWidth = 1.2f;
            return c;
        }

        private static RenderCommand EllipseStroke(float x, float y, float w, float h)
        {
            RenderCommand c = new RenderCommand();
            c.CommandType = RenderCommandType.Ellipse;
            c.X = x; c.Y = y; c.Width = w; c.Height = h;
            c.UseShapeColors = false;
            c.StrokeColor = new XmlColor(Accent);
            c.Fill = false; c.Stroke = true;
            c.StrokeWidth = 1.2f;
            return c;
        }

        private static RenderCommand Polygon(PointF[] pts)
        {
            RenderCommand c = new RenderCommand();
            c.CommandType = RenderCommandType.Polygon;
            c.X = 0; c.Y = 0; c.Width = 1; c.Height = 1;
            c.PolygonPoints = pts;
            c.UseShapeColors = false;
            c.StrokeColor = new XmlColor(Accent);
            c.Fill = false; c.Stroke = true;
            c.StrokeWidth = 1.2f;
            return c;
        }

        private static RenderCommand Line(float x1, float y1, float x2, float y2)
        {
            RenderCommand c = new RenderCommand();
            c.CommandType = RenderCommandType.Line;
            c.X = x1; c.Y = y1; c.Width = x2 - x1; c.Height = y2 - y1;
            c.StrokeColor = new XmlColor(Accent);
            c.UseShapeColors = false;
            c.Fill = false; c.Stroke = true;
            c.StrokeWidth = 1.2f;
            return c;
        }

        private static RenderCommand TextLabel(float x, float y, float w, float h, string text)
        {
            RenderCommand c = new RenderCommand();
            c.CommandType = RenderCommandType.Text;
            c.X = x; c.Y = y; c.Width = w; c.Height = h;
            c.Text = text;
            c.TextAlign = "center";
            c.FontSize = 8f;
            c.IsBold = false;
            c.UseShapeColors = true;
            return c;
        }

        // ===== 各 K8s 资源图形工厂 =====

        // 集群：大圆角容器，内部网格点表示多节点
        private static ShapeType CreateCluster()
        {
            ShapeType t = CreateBase(Cluster, 180f, 130f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 10f));
            // 内部网格点（3x3）
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    float cx = 0.25f + col * 0.25f;
                    float cy = 0.30f + row * 0.18f;
                    t.RenderCommands.Add(EllipseStroke(cx - 0.03f, cy - 0.04f, 0.06f, 0.08f));
                }
            }
            t.RenderCommands.Add(TextLabel(0f, 0.78f, 1f, 0.18f, "CLUSTER"));
            return t;
        }

        // 节点：竖长服务器盒，内部堆叠横线表示机架单元
        private static ShapeType CreateNode()
        {
            ShapeType t = CreateBase(Node, 90f, 150f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 5f));
            // 顶部状态灯条
            RenderCommand led = Rect(0.08f, 0.06f, 0.84f, 0.06f);
            led.UseShapeColors = false;
            led.FillColor = new XmlColor(Color.FromArgb(76, 175, 80));
            led.Stroke = false;
            t.RenderCommands.Add(led);
            // 机架单元横线
            for (int i = 0; i < 5; i++)
            {
                float y = 0.16f + i * 0.15f;
                t.RenderCommands.Add(Line(0.12f, y, 0.88f, y));
            }
            t.RenderCommands.Add(TextLabel(0f, 0.86f, 1f, 0.12f, "NODE"));
            return t;
        }

        // 命名空间：唯一的真实容器，子对象坐标和拖放归属由投影/意图层维护。
        private static ShapeType CreateNamespace()
        {
            ShapeType t = CreateBase(Namespace, 420f, 280f);
            t.IsContainer = true;
            t.Resizable = true;
            t.NameAlignment = NameAlignment.TopLeft;
            return t;
        }

        // 部署：矩形 + 向下箭头 + 三个堆叠的小容器组（副本指示器）
        private static ShapeType CreateDeployment()
        {
            ShapeType t = CreateBase(Deployment, 140f, 110f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 6f));
            // 向下箭头
            t.RenderCommands.Add(Line(0.5f, 0.10f, 0.5f, 0.32f));
            t.RenderCommands.Add(Polygon(new PointF[] {
                new PointF(0.42f, 0.28f), new PointF(0.5f, 0.38f), new PointF(0.58f, 0.28f)
            }));
            // 三个堆叠的小 Pod 轮廓
            for (int i = 0; i < 3; i++)
            {
                float y = 0.44f + i * 0.16f;
                t.RenderCommands.Add(RectStroke(0.20f, y, 0.60f, 0.11f));
            }
            t.RenderCommands.Add(TextLabel(0f, 0.86f, 1f, 0.12f, "DEPLOY"));
            return t;
        }

        // 副本集：两个重叠的矩形（副本概念）
        private static ShapeType CreateReplicaSet()
        {
            ShapeType t = CreateBase(ReplicaSet, 130f, 100f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 6f));
            // 重叠的副本卡片
            t.RenderCommands.Add(RectStroke(0.12f, 0.18f, 0.55f, 0.40f));
            t.RenderCommands.Add(RectStroke(0.33f, 0.34f, 0.55f, 0.40f));
            t.RenderCommands.Add(TextLabel(0f, 0.78f, 1f, 0.18f, "RS"));
            return t;
        }

        // 有状态集：堆叠 Pod + 序号 1/2/3
        private static ShapeType CreateStatefulSet()
        {
            ShapeType t = CreateBase(StatefulSet, 140f, 110f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 6f));
            for (int i = 0; i < 3; i++)
            {
                float y = 0.14f + i * 0.22f;
                t.RenderCommands.Add(RectStroke(0.12f, y, 0.50f, 0.16f));
                // 序号
                RenderCommand num = TextLabel(0.66f, y - 0.01f, 0.20f, 0.18f, (i + 1).ToString());
                t.RenderCommands.Add(num);
            }
            t.RenderCommands.Add(TextLabel(0f, 0.82f, 1f, 0.16f, "STS"));
            return t;
        }

        // 守护进程集：节点盒 + 内嵌 Pod
        private static ShapeType CreateDaemonSet()
        {
            ShapeType t = CreateBase(DaemonSet, 110f, 130f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 5f));
            // 节点横线
            for (int i = 0; i < 3; i++)
            {
                float y = 0.14f + i * 0.14f;
                t.RenderCommands.Add(Line(0.12f, y, 0.88f, y));
            }
            // 内嵌 Pod 标识
            t.RenderCommands.Add(RoundedRect(0.18f, 0.58f, 0.64f, 0.24f, 4f));
            t.RenderCommands.Add(TextLabel(0f, 0.86f, 1f, 0.12f, "DS"));
            return t;
        }

        // 容器组 Pod：胶囊形 + 内部两个小容器盒
        private static ShapeType CreatePod()
        {
            ShapeType t = CreateBase(Pod, 120f, 80f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 16f));
            // 内部容器盒
            t.RenderCommands.Add(RectStroke(0.12f, 0.22f, 0.34f, 0.36f));
            t.RenderCommands.Add(RectStroke(0.54f, 0.22f, 0.34f, 0.36f));
            t.RenderCommands.Add(TextLabel(0f, 0.66f, 1f, 0.28f, "POD"));
            return t;
        }

        // 服务：圆角矩形 + 流量分发线
        private static ShapeType CreateService()
        {
            ShapeType t = CreateBase(Service, 130f, 90f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 8f));
            // 入口线
            t.RenderCommands.Add(Line(0.5f, 0.06f, 0.5f, 0.30f));
            // 分发到三个后端
            t.RenderCommands.Add(Line(0.5f, 0.30f, 0.22f, 0.58f));
            t.RenderCommands.Add(Line(0.5f, 0.30f, 0.5f, 0.58f));
            t.RenderCommands.Add(Line(0.5f, 0.30f, 0.78f, 0.58f));
            // 后端小圆点
            t.RenderCommands.Add(EllipseStroke(0.16f, 0.58f, 0.12f, 0.14f));
            t.RenderCommands.Add(EllipseStroke(0.44f, 0.58f, 0.12f, 0.14f));
            t.RenderCommands.Add(EllipseStroke(0.72f, 0.58f, 0.12f, 0.14f));
            return t;
        }

        // 入口 Ingress：网关形 + 向内箭头
        private static ShapeType CreateIngress()
        {
            ShapeType t = CreateBase(Ingress, 140f, 90f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 6f));
            // 向内的箭头（左入右出）
            t.RenderCommands.Add(Line(0.06f, 0.5f, 0.40f, 0.5f));
            t.RenderCommands.Add(Polygon(new PointF[] {
                new PointF(0.36f, 0.42f), new PointF(0.46f, 0.5f), new PointF(0.36f, 0.58f)
            }));
            t.RenderCommands.Add(Line(0.54f, 0.5f, 0.94f, 0.5f));
            t.RenderCommands.Add(Polygon(new PointF[] {
                new PointF(0.90f, 0.42f), new PointF(0.94f, 0.5f), new PointF(0.90f, 0.58f)
            }));
            t.RenderCommands.Add(TextLabel(0f, 0.10f, 1f, 0.22f, "INGRESS"));
            return t;
        }

        // 配置映射：文档/卷轴 + 三行文字
        private static ShapeType CreateConfigMap()
        {
            ShapeType t = CreateBase(ConfigMap, 110f, 120f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 4f));
            // 顶部卷轴
            t.RenderCommands.Add(RectStroke(0.10f, 0.06f, 0.80f, 0.10f));
            t.RenderCommands.Add(RectStroke(0.10f, 0.84f, 0.80f, 0.10f));
            // 三行配置文字
            for (int i = 0; i < 3; i++)
            {
                float y = 0.24f + i * 0.18f;
                t.RenderCommands.Add(Line(0.16f, y, 0.84f, y));
            }
            t.RenderCommands.Add(TextLabel(0f, 0.66f, 1f, 0.16f, "CM"));
            return t;
        }

        // 密钥：挂锁形状
        private static ShapeType CreateSecret()
        {
            ShapeType t = CreateBase(Secret, 90f, 110f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 5f));
            // 锁身
            t.RenderCommands.Add(RoundedRect(0.18f, 0.40f, 0.64f, 0.42f, 4f));
            // 锁梁（U 形，用三条线近似）
            t.RenderCommands.Add(Line(0.30f, 0.40f, 0.30f, 0.22f));
            t.RenderCommands.Add(Line(0.30f, 0.22f, 0.70f, 0.22f));
            t.RenderCommands.Add(Line(0.70f, 0.22f, 0.70f, 0.40f));
            // 锁孔
            t.RenderCommands.Add(EllipseStroke(0.43f, 0.54f, 0.14f, 0.14f));
            return t;
        }

        // 持久卷：圆柱体（数据库形）
        private static ShapeType CreatePersistentVolume()
        {
            ShapeType t = CreateBase(PersistentVolume, 110f, 120f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 4f));
            // 圆柱侧面
            RenderCommand body = Rect(0.16f, 0.18f, 0.68f, 0.64f);
            body.UseShapeColors = true;
            body.Stroke = false;
            t.RenderCommands.Add(body);
            t.RenderCommands.Add(RectStroke(0.16f, 0.18f, 0.68f, 0.64f));
            // 顶椭圆
            t.RenderCommands.Add(EllipseStroke(0.16f, 0.10f, 0.68f, 0.16f));
            // 底椭圆（半弧，用椭圆近似）
            t.RenderCommands.Add(EllipseStroke(0.16f, 0.74f, 0.68f, 0.16f));
            t.RenderCommands.Add(TextLabel(0f, 0.84f, 1f, 0.14f, "PV"));
            return t;
        }

        // 持久卷声明：圆柱 + 指向它的箭头
        private static ShapeType CreatePersistentVolumeClaim()
        {
            ShapeType t = CreateBase(PersistentVolumeClaim, 120f, 120f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 4f));
            RenderCommand body = Rect(0.28f, 0.18f, 0.54f, 0.60f);
            body.UseShapeColors = true;
            body.Stroke = false;
            t.RenderCommands.Add(body);
            t.RenderCommands.Add(RectStroke(0.28f, 0.18f, 0.54f, 0.60f));
            t.RenderCommands.Add(EllipseStroke(0.28f, 0.12f, 0.54f, 0.12f));
            // 指向圆柱的箭头
            t.RenderCommands.Add(Line(0.08f, 0.48f, 0.24f, 0.48f));
            t.RenderCommands.Add(Polygon(new PointF[] {
                new PointF(0.18f, 0.42f), new PointF(0.28f, 0.48f), new PointF(0.18f, 0.54f)
            }));
            t.RenderCommands.Add(TextLabel(0f, 0.84f, 1f, 0.14f, "PVC"));
            return t;
        }

        // 任务 Job：矩形 + 对勾
        private static ShapeType CreateJob()
        {
            ShapeType t = CreateBase(Job, 110f, 100f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 6f));
            // 对勾
            t.RenderCommands.Add(Line(0.28f, 0.50f, 0.44f, 0.66f));
            t.RenderCommands.Add(Line(0.44f, 0.66f, 0.76f, 0.30f));
            t.RenderCommands.Add(TextLabel(0f, 0.76f, 1f, 0.18f, "JOB"));
            return t;
        }

        // 定时任务 CronJob：时钟表盘
        private static ShapeType CreateCronJob()
        {
            ShapeType t = CreateBase(CronJob, 110f, 110f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 6f));
            // 表盘
            t.RenderCommands.Add(EllipseStroke(0.18f, 0.12f, 0.64f, 0.64f));
            // 时针
            t.RenderCommands.Add(Line(0.50f, 0.44f, 0.50f, 0.28f));
            // 分针
            t.RenderCommands.Add(Line(0.50f, 0.44f, 0.64f, 0.50f));
            // 中心
            t.RenderCommands.Add(EllipseStroke(0.46f, 0.40f, 0.08f, 0.08f));
            t.RenderCommands.Add(TextLabel(0f, 0.80f, 1f, 0.16f, "CRON"));
            return t;
        }

        // etcd：圆柱体 + etcd 字样
        private static ShapeType CreateEtcd()
        {
            ShapeType t = CreateBase(Etcd, 110f, 110f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 4f));
            RenderCommand body = Rect(0.16f, 0.20f, 0.68f, 0.58f);
            body.UseShapeColors = true;
            body.Stroke = false;
            t.RenderCommands.Add(body);
            t.RenderCommands.Add(RectStroke(0.16f, 0.20f, 0.68f, 0.58f));
            t.RenderCommands.Add(EllipseStroke(0.16f, 0.14f, 0.68f, 0.12f));
            t.RenderCommands.Add(EllipseStroke(0.16f, 0.72f, 0.68f, 0.12f));
            t.RenderCommands.Add(TextLabel(0f, 0.42f, 1f, 0.20f, "etcd"));
            return t;
        }

        // Helm Release：发布记录及其管理的一组资源。
        private static ShapeType CreateHelmRelease()
        {
            ShapeType t = CreateBase(HelmRelease, 145f, 105f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 7f));
            t.RenderCommands.Add(RectStroke(0.15f, 0.14f, 0.70f, 0.18f));
            t.RenderCommands.Add(RectStroke(0.15f, 0.38f, 0.70f, 0.18f));
            t.RenderCommands.Add(RectStroke(0.15f, 0.62f, 0.70f, 0.18f));
            t.RenderCommands.Add(Line(0.24f, 0.32f, 0.24f, 0.38f));
            t.RenderCommands.Add(Line(0.24f, 0.56f, 0.24f, 0.62f));
            t.RenderCommands.Add(TextLabel(0f, 0.82f, 1f, 0.16f, "RELEASE"));
            return t;
        }

        // Helm Chart：应用包和可配置模板。
        private static ShapeType CreateHelmChart()
        {
            ShapeType t = CreateBase(HelmChart, 135f, 110f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 6f));
            t.RenderCommands.Add(RectStroke(0.18f, 0.12f, 0.64f, 0.62f));
            t.RenderCommands.Add(Line(0.28f, 0.30f, 0.72f, 0.30f));
            t.RenderCommands.Add(Line(0.28f, 0.43f, 0.65f, 0.43f));
            t.RenderCommands.Add(Line(0.28f, 0.56f, 0.72f, 0.56f));
            t.RenderCommands.Add(Polygon(new PointF[] {
                new PointF(0.64f, 0.12f), new PointF(0.82f, 0.30f), new PointF(0.64f, 0.30f)
            }));
            t.RenderCommands.Add(TextLabel(0f, 0.80f, 1f, 0.17f, "CHART"));
            return t;
        }

        // Istio Gateway：进入服务网格的东西向/南北向流量入口。
        private static ShapeType CreateIstioGateway()
        {
            ShapeType t = CreateBase(IstioGateway, 145f, 100f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 6f));
            t.RenderCommands.Add(Line(0.10f, 0.46f, 0.84f, 0.46f));
            t.RenderCommands.Add(Polygon(new PointF[] {
                new PointF(0.76f, 0.35f), new PointF(0.90f, 0.46f), new PointF(0.76f, 0.57f)
            }));
            t.RenderCommands.Add(Line(0.38f, 0.20f, 0.38f, 0.72f));
            t.RenderCommands.Add(Line(0.56f, 0.20f, 0.56f, 0.72f));
            t.RenderCommands.Add(TextLabel(0f, 0.78f, 1f, 0.18f, "GATEWAY"));
            return t;
        }

        // VirtualService：把请求按规则分发到不同服务或版本。
        private static ShapeType CreateVirtualService()
        {
            ShapeType t = CreateBase(VirtualService, 150f, 105f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 6f));
            t.RenderCommands.Add(Line(0.12f, 0.43f, 0.43f, 0.43f));
            t.RenderCommands.Add(Line(0.43f, 0.43f, 0.76f, 0.20f));
            t.RenderCommands.Add(Line(0.43f, 0.43f, 0.76f, 0.66f));
            t.RenderCommands.Add(EllipseStroke(0.73f, 0.14f, 0.12f, 0.12f));
            t.RenderCommands.Add(EllipseStroke(0.73f, 0.60f, 0.12f, 0.12f));
            t.RenderCommands.Add(TextLabel(0f, 0.78f, 1f, 0.18f, "ROUTES"));
            return t;
        }

        // DestinationRule：服务子集及负载均衡、TLS 等目标策略。
        private static ShapeType CreateDestinationRule()
        {
            ShapeType t = CreateBase(DestinationRule, 150f, 110f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 6f));
            t.RenderCommands.Add(RectStroke(0.12f, 0.18f, 0.22f, 0.38f));
            t.RenderCommands.Add(RectStroke(0.39f, 0.18f, 0.22f, 0.38f));
            t.RenderCommands.Add(RectStroke(0.66f, 0.18f, 0.22f, 0.38f));
            t.RenderCommands.Add(TextLabel(0.12f, 0.26f, 0.22f, 0.18f, "A"));
            t.RenderCommands.Add(TextLabel(0.39f, 0.26f, 0.22f, 0.18f, "B"));
            t.RenderCommands.Add(TextLabel(0.66f, 0.26f, 0.22f, 0.18f, "C"));
            t.RenderCommands.Add(TextLabel(0f, 0.66f, 1f, 0.24f, "SUBSETS"));
            return t;
        }

        // YAML：本地设计/调试输入，不暗示资源已应用到集群。
        private static ShapeType CreateYamlDocument()
        {
            ShapeType t = CreateBase(YamlDocument, 125f, 120f);
            t.RenderCommands.Add(RoundedRect(0f, 0f, 1f, 1f, 5f));
            t.RenderCommands.Add(RectStroke(0.20f, 0.08f, 0.60f, 0.68f));
            t.RenderCommands.Add(Line(0.30f, 0.28f, 0.68f, 0.28f));
            t.RenderCommands.Add(Line(0.30f, 0.40f, 0.62f, 0.40f));
            t.RenderCommands.Add(Line(0.30f, 0.52f, 0.70f, 0.52f));
            t.RenderCommands.Add(Line(0.30f, 0.64f, 0.56f, 0.64f));
            t.RenderCommands.Add(Polygon(new PointF[] {
                new PointF(0.62f, 0.08f), new PointF(0.80f, 0.26f), new PointF(0.62f, 0.26f)
            }));
            t.RenderCommands.Add(TextLabel(0f, 0.80f, 1f, 0.16f, "YAML"));
            return t;
        }
    }
}
