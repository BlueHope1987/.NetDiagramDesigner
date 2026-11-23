using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using DiagramDesigner.Core;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    /// <summary>
    /// 脱离控件与 UI 的内存版宿主，用于命令行引擎的自检。
    /// 这体现了本 Demo 的设计意图：命令逻辑不依赖 WinForms，可独立验证。
    /// </summary>
    internal class FakeTopologyHost : ITopologyHost
    {
        private readonly List<ShapeInfo> _shapes = new List<ShapeInfo>();
        private readonly List<Guid> _selected = new List<Guid>();
        private readonly List<string> _connections = new List<string>();
        private readonly List<string> _types = new List<string>();

        private float _zoom = 1f;
        private PointF _offset = new PointF(0, 0);
        private readonly Dictionary<Guid, Guid> _parents = new Dictionary<Guid, Guid>();

        public FakeTopologyHost()
        {
            Array kinds = Enum.GetValues(typeof(EntityKind));
            for (int i = 0; i < kinds.Length; i++)
            {
                string name = K8sShapes.ShapeNameOf((EntityKind)kinds.GetValue(i));
                if (!_types.Contains(name)) _types.Add(name);
            }
        }

        public IList<ShapeInfo> ListShapes()
        {
            List<ShapeInfo> list = new List<ShapeInfo>();
            foreach (ShapeInfo s in _shapes) list.Add(Copy(s));
            return list;
        }

        public IList<ShapeInfo> ListSelection()
        {
            List<ShapeInfo> list = new List<ShapeInfo>();
            foreach (ShapeInfo s in _shapes)
                if (_selected.Contains(s.Id)) list.Add(Copy(s));
            return list;
        }

        public int ShapeCount { get { return _shapes.Count; } }
        public int ConnectionCount { get { return _connections.Count; } }

        public IList<string> ListTypeNames() { return new List<string>(_types); }

        public string DescribeConnections()
        {
            if (_connections.Count == 0) return "(无连线)";
            return string.Join("\n", _connections.ToArray());
        }

        public ShapeInfo AddShape(string typeName, string name, float x, float y)
        {
            if (!_types.Contains(typeName)) return null;
            ShapeInfo s = new ShapeInfo();
            s.Id = Guid.NewGuid();
            s.Name = name;
            s.TypeName = typeName;
            s.X = x;
            s.Y = y;
            s.Width = 120f;
            s.Height = 80f;
            s.FillColor = Color.LightGray;
            s.BorderColor = Color.SteelBlue;
            s.StateName = "";
            s.States = new string[0];
            _shapes.Add(s);
            return Copy(s);
        }

        public int RemoveSelection()
        {
            int n = 0;
            for (int i = _shapes.Count - 1; i >= 0; i--)
            {
                if (_selected.Contains(_shapes[i].Id))
                {
                    _shapes.RemoveAt(i);
                    n++;
                }
            }
            _selected.Clear();
            return n;
        }

        public void SelectAll()
        {
            _selected.Clear();
            foreach (ShapeInfo s in _shapes) _selected.Add(s.Id);
        }

        public void ClearSelection() { _selected.Clear(); }

        public bool SelectByName(string name)
        {
            _selected.Clear();
            foreach (ShapeInfo s in _shapes)
            {
                if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    _selected.Add(s.Id);
                    return true;
                }
            }
            return false;
        }

        public bool SelectById(Guid id)
        {
            ShapeInfo shape = Find(id);
            if (shape == null) return false;
            _selected.Clear();
            _selected.Add(id);
            return true;
        }

        public bool RenameSelection(string newName)
        {
            int n = 0;
            foreach (ShapeInfo s in _shapes)
                if (_selected.Contains(s.Id)) { s.Name = newName; n++; }
            return n > 0;
        }

        public bool SetProperty(string prop, string value)
        {
            if (_selected.Count == 0) return false;
            bool any = false;
            List<Guid> ids = new List<Guid>(_selected);
            foreach (Guid id in ids)
                if (SetShapeProperty(id, prop, value)) any = true;
            return any;
        }

        public bool SetShapeProperty(Guid id, string prop, string value)
        {
            ShapeInfo s = Find(id);
            if (s == null) return false;
            string p = (prop == null ? "" : prop.Trim().ToLowerInvariant());
            try
            {
                switch (p)
                {
                    case "name": s.Name = value; return true;
                    case "x": s.X = float.Parse(value); return true;
                    case "y": s.Y = float.Parse(value); return true;
                    case "w": case "width": s.Width = float.Parse(value); return true;
                    case "h": case "height": s.Height = float.Parse(value); return true;
                    case "fill": case "fillcolor": s.FillColor = ColorUtil.Parse(value); return true;
                    case "border": case "bordercolor": s.BorderColor = ColorUtil.Parse(value); return true;
                    default: return false;
                }
            }
            catch { return false; }
        }

        public bool SetShapeState(Guid id, string stateName)
        {
            ShapeInfo s = Find(id);
            if (s == null) return false;
            foreach (string name in s.States)
            {
                if (string.Equals(name, stateName, StringComparison.OrdinalIgnoreCase))
                {
                    s.StateName = stateName;
                    return true;
                }
            }
            return false;
        }

        public bool ConfigureStates(Guid id, string[] stateNames, string initial)
        {
            ShapeInfo s = Find(id);
            if (s == null) return false;
            s.States = new List<string>(stateNames).ToArray();
            if (!string.IsNullOrEmpty(initial)) s.StateName = initial;
            return true;
        }

        public bool ConfigureResourceStates(Guid id, EntityKind kind, string initial)
        {
            ShapeInfo shape = Find(id);
            if (shape == null) return false;
            shape.FillColor = ResourceVisualPalette.FillFor(kind);
            RunStatus status = RunStatus.Unknown;
            try { status = (RunStatus)Enum.Parse(typeof(RunStatus), initial); }
            catch { }
            shape.BorderColor = ResourceVisualPalette.BorderFor(status);
            shape.StateName = initial;
            return true;
        }

        public bool SetShapeBounds(Guid id, RectangleF bounds)
        {
            ShapeInfo shape = Find(id);
            if (shape == null) return false;
            shape.X = bounds.X;
            shape.Y = bounds.Y;
            shape.Width = bounds.Width;
            shape.Height = bounds.Height;
            return true;
        }

        public bool SetContainer(Guid childId, Guid containerId)
        {
            if (Find(childId) == null) return false;
            if (containerId == Guid.Empty) _parents.Remove(childId);
            else _parents[childId] = containerId;
            return true;
        }

        public Guid GetContainer(Guid childId)
        {
            Guid parent;
            return _parents.TryGetValue(childId, out parent) ? parent : Guid.Empty;
        }

        public Guid ConnectById(Guid fromId, Guid toId, string label)
        {
            ShapeInfo from = Find(fromId);
            ShapeInfo to = Find(toId);
            if (from == null || to == null) return Guid.Empty;
            _connections.Add(from.Name + " -> " + to.Name + (label.Length > 0 ? " [" + label + "]" : ""));
            return Guid.NewGuid();
        }

        public bool Connect(string fromName, string toName, string label)
        {
            ShapeInfo f = FindByName(fromName);
            ShapeInfo t = FindByName(toName);
            if (f == null || t == null) return false;
            _connections.Add(f.Name + " -> " + t.Name + (label.Length > 0 ? " [" + label + "]" : ""));
            return true;
        }

        public void BringToFrontSelection() { }
        public void SendToBackSelection() { }

        public void Zoom(float factor) { _zoom = _zoom * factor; }
        public void SetZoom(float absolute) { _zoom = absolute; }
        public float GetZoom() { return _zoom; }
        public PointF GetViewportOffset() { return _offset; }
        public void SetViewport(float zoom, PointF offset) { _zoom = zoom; _offset = offset; }
        public IDisposable SuppressEvents() { return new EmptyDisposable(); }
        public IDisposable BeginApplyExternalChange() { return new EmptyDisposable(); }
        public void FitView() { }
        public bool SetConnectionMode(string mode)
        {
            string m = (mode == null ? "" : mode.Trim().ToLowerInvariant());
            return m == "straight" || m == "curve" || m == "ortho" || m == "orthogonal";
        }

        public void NewDocument()
        {
            _shapes.Clear();
            _selected.Clear();
            _connections.Clear();
            _parents.Clear();
        }

        public void SaveDocument(string path) { }
        public void LoadDocument(string path) { }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose() { }
        }

        private ShapeInfo Find(Guid id)
        {
            foreach (ShapeInfo s in _shapes) if (s.Id == id) return s;
            return null;
        }

        private ShapeInfo FindByName(string name)
        {
            foreach (ShapeInfo s in _shapes)
                if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        private static ShapeInfo Copy(ShapeInfo s)
        {
            ShapeInfo c = new ShapeInfo();
            c.Id = s.Id;
            c.Name = s.Name;
            c.TypeName = s.TypeName;
            c.X = s.X;
            c.Y = s.Y;
            c.Width = s.Width;
            c.Height = s.Height;
            c.FillColor = s.FillColor;
            c.BorderColor = s.BorderColor;
            c.StateName = s.StateName;
            c.States = s.States;
            return c;
        }
    }

    /// <summary>候选版无头自检；不创建窗口，也不访问真实集群。</summary>
    internal static class SelfTest
    {
        public static int Run()
        {
            int pass = 0;
            int fail = 0;
            Console.WriteLine("=== NativeManagerDemo 云原生候选版自检 ===");
            TestTokenizerAndPolicy(ref pass, ref fail);
            TestJsonAndDesign(ref pass, ref fail);
            TestGateway(ref pass, ref fail);
            TestShapeLibrary(ref pass, ref fail);
            TestProjector(ref pass, ref fail);
            TestCoordinator(ref pass, ref fail);
            TestProcessPreCancellation(ref pass, ref fail);
            Console.WriteLine(string.Format("=== 自检完成: 通过 {0} 项, 失败 {1} 项 ===", pass, fail));
            return fail == 0 ? 0 : 1;
        }

        private static void TestTokenizerAndPolicy(ref int pass, ref int fail)
        {
            List<string> tokens = CommandLineTokenizer.Parse("yq eval \".metadata.name\" \"my file.yaml\"");
            Check("分词保留引号内空格", tokens.Count == 4 && tokens[3] == "my file.yaml", ref pass, ref fail);
            tokens = CommandLineTokenizer.Parse("yq eval '' sample.yaml");
            Check("分词保留空参数", tokens.Count == 4 && tokens[2] == "", ref pass, ref fail);
            bool rejected = false;
            try { CommandLineTokenizer.Parse("kubectl get pods | findstr web"); }
            catch (CommandLineParseException) { rejected = true; }
            Check("shell 管道在解析期拒绝", rejected, ref pass, ref fail);
            Check("kubectl get 只读允许", CommandPolicy.Evaluate("kubectl get pods -A").Verdict == CommandPolicyVerdict.Allow, ref pass, ref fail);
            Check("logs -f 长驻拒绝", !CommandPolicy.Evaluate("kubectl logs web -f").Allowed, ref pass, ref fail);
            Check("get --watch 长驻拒绝", !CommandPolicy.Evaluate("kubectl get pods --watch").Allowed, ref pass, ref fail);
            Check("非交互 exec 需确认", CommandPolicy.Evaluate("kubectl exec web -- hostname").RequiresConfirmation, ref pass, ref fail);
            Check("无分隔符 exec 仍扫描交互参数", !CommandPolicy.Evaluate("kubectl exec web hostname -it").Allowed, ref pass, ref fail);
            Check("rollout restart 需确认", CommandPolicy.Evaluate("kubectl rollout restart deployment web").RequiresConfirmation, ref pass, ref fail);
            Check("scale 正数允许", CommandPolicy.Evaluate("kubectl scale deployment web --replicas=3").Verdict == CommandPolicyVerdict.Allow, ref pass, ref fail);
            Check("scale 到零需确认", CommandPolicy.Evaluate("kubectl scale deployment web --replicas=0").RequiresConfirmation, ref pass, ref fail);
            Check("scale 缺少副本拒绝", !CommandPolicy.Evaluate("kubectl scale deployment web").Allowed, ref pass, ref fail);
            Check("helm uninstall 需确认", CommandPolicy.Evaluate("helm uninstall shop -n apps").RequiresConfirmation, ref pass, ref fail);
            Check("istioctl 非白名单拒绝", !CommandPolicy.Evaluate("istioctl install").Allowed, ref pass, ref fail);
            Check("yq eval 允许", CommandPolicy.Evaluate("yq eval .kind sample.yaml").Allowed, ref pass, ref fail);
            Check("yq 原地写入拒绝", !CommandPolicy.Evaluate("yq eval -i .kind sample.yaml").Allowed, ref pass, ref fail);
            Check("其他可执行程序拒绝", !CommandPolicy.Evaluate("cmd /c dir").Allowed, ref pass, ref fail);
        }

        private static void TestJsonAndDesign(ref int pass, ref int fail)
        {
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["text"] = "中文\n\"quoted\"";
            List<object> values = new List<object>();
            values.Add(1); values.Add(true); values.Add(null);
            root["values"] = values;
            object parsed = MiniJson.Parse(MiniJson.Stringify(root));
            Check("MiniJson Unicode/转义往返", MiniJson.String(parsed, "text", "") == "中文\n\"quoted\"", ref pass, ref fail);
            Check("MiniJson 数组往返", MiniJson.Array(MiniJson.Get(parsed, "values")).Count == 3, ref pass, ref fail);

            DesignTopologyStore store = new DesignTopologyStore();
            DesignEditResult duplicateNs = store.AddEntity(EntityKind.Deployment, "web", "apps");
            Check("同名资源可位于不同 Namespace", duplicateNs.Success, ref pass, ref fail);
            Check("同作用域同名资源拒绝", !store.AddEntity(EntityKind.Deployment, "web", "apps").Success, ref pass, ref fail);
            TopologySnapshot snapshot = store.QueryTopology();
            ManagedEntity defaultWeb = Find(snapshot, EntityKind.Deployment, "default", "web");
            ManagedEntity defaultNs = Find(snapshot, EntityKind.Namespace, "", "default");
            Check("Namespace 同名冲突时换父拒绝", !store.MoveEntity(defaultWeb.Id,
                Find(snapshot, EntityKind.Namespace, "", "apps").Id).Success, ref pass, ref fail);
            Check("非空 Namespace 删除拒绝", !store.DeleteEntity(defaultNs.Id).Success, ref pass, ref fail);
            ManagedEntity service = Find(snapshot, EntityKind.Service, "default", "web");
            ManagedEntity pod = Find(snapshot, EntityKind.Pod, "default", "web-demo-pod");
            Check("Service selector 语义合法", store.AddRelation(service.Id, pod.Id, RelationKind.Selects).Message.Contains("已存在") ||
                DesignTopologyStore.ValidateRelation(service, pod, RelationKind.Selects).Length == 0, ref pass, ref fail);
            Check("反向 selector 语义拒绝", DesignTopologyStore.ValidateRelation(pod, service, RelationKind.Selects).Length > 0, ref pass, ref fail);
            Check("Node 不属于 Namespace", !DesignTopologyStore.IsNamespaced(EntityKind.Node), ref pass, ref fail);
            Check("副本设计只改快照", store.SetReplicas(defaultWeb.Id, 5).Success &&
                store.QueryTopology().FindEntity(defaultWeb.Id).Replicas == 5, ref pass, ref fail);

            string path = Path.Combine(Path.GetTempPath(), "native-manager-selftest-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                store.Save(path);
                DesignTopologyStore loaded = new DesignTopologyStore();
                loaded.Load(path);
                TopologySnapshot restored = loaded.QueryTopology();
                ManagedEntity moved = restored.FindEntity(duplicateNs.EntityId);
                Check("设计 JSON 保存/载入", restored.Entities.Count == store.QueryTopology().Entities.Count && moved != null, ref pass, ref fail);
                Check("设计 parentId 持久化", moved != null && moved.ParentId.Length > 0 &&
                    restored.FindEntity(moved.ParentId).Kind == EntityKind.Namespace, ref pass, ref fail);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        private static void TestGateway(ref int pass, ref int fail)
        {
            FakeGatewayExecutor executor = new FakeGatewayExecutor();
            KubectlTopologyGateway gateway = new KubectlTopologyGateway(executor);
            TopologySnapshot snapshot = gateway.QueryTopology();
            Check("真实网关解析双 Namespace 资源", FindByUid(snapshot, "dep-a") != null && FindByUid(snapshot, "dep-b") != null, ref pass, ref fail);
            Check("ownerReferences 正向连接", HasRelation(snapshot, "dep-a", "pod-a", RelationKind.Owns), ref pass, ref fail);
            Check("ownerReferences 不跨同名 Namespace", !HasRelation(snapshot, "dep-a", "pod-b", RelationKind.Owns), ref pass, ref fail);
            Check("Service selector 正向连接", HasRelation(snapshot, "svc-a", "pod-a", RelationKind.Selects), ref pass, ref fail);
            Check("Service selector 不跨 Namespace", !HasRelation(snapshot, "svc-a", "pod-b", RelationKind.Selects), ref pass, ref fail);
            executor.FailCluster = true;
            TopologySnapshot partial = gateway.QueryTopology();
            Check("单组查询失败保留部分快照", partial.Entities.Count > 0 && partial.Warnings.Count > 0, ref pass, ref fail);
        }

        private static void TestShapeLibrary(ref int pass, ref int fail)
        {
            K8sShapes.RegisterAll();
            Array kinds = Enum.GetValues(typeof(EntityKind));
            bool allRegistered = true;
            bool paletteMatches = true;
            bool descriptionsPresent = true;
            List<string> categories = new List<string>();
            for (int i = 1; i < kinds.Length; i++)
            {
                EntityKind kind = (EntityKind)kinds.GetValue(i);
                ShapeType type = ShapeTypeRegistry.Instance.GetShapeType(K8sShapes.ShapeNameOf(kind));
                if (type == null) { allRegistered = false; continue; }
                if (type.DefaultFillColor.ToColor() != ResourceVisualPalette.FillFor(kind)) paletteMatches = false;
                if (string.IsNullOrEmpty(type.Description) || type.Description == type.Name) descriptionsPresent = false;
                if (!categories.Contains(type.Category)) categories.Add(type.Category);
            }
            Check("全部管理对象均注册为产品图形", allRegistered, ref pass, ref fail);
            Check("图形默认填充色匹配资源类型", paletteMatches, ref pass, ref fail);
            Check("Namespace 注册为唯一容器", ShapeTypeRegistry.Instance.GetShapeType(K8sShapes.Namespace).IsContainer,
                ref pass, ref fail);
            Check("工具箱按六组显示且有说明", categories.Count == 6 && descriptionsPresent, ref pass, ref fail);
        }

        private static void TestProjector(ref int pass, ref int fail)
        {
            FakeTopologyHost host = new FakeTopologyHost();
            TopologyProjector projector = new TopologyProjector(host);
            DesignTopologyStore design = new DesignTopologyStore();
            DesignEditResult added = design.AddEntity(EntityKind.Deployment, "web", "apps");
            TopologyLayoutStore layout = new TopologyLayoutStore("");
            projector.AttachLayoutStore(layout);
            TopologySnapshot snapshot = design.QueryTopology();
            projector.Render(snapshot);
            ManagedEntity defaultWeb = Find(snapshot, EntityKind.Deployment, "default", "web");
            Guid first = projector.GetShapeForEntity(defaultWeb.Id);
            Guid second = projector.GetShapeForEntity(added.EntityId);
            Check("同名资源按 Id 生成不同图形", first != Guid.Empty && second != Guid.Empty && first != second, ref pass, ref fail);
            Check("资源类型决定不同填充色", ResourceVisualPalette.FillFor(EntityKind.Deployment) !=
                ResourceVisualPalette.FillFor(EntityKind.Service), ref pass, ref fail);
            Check("RunStatus 决定不同边框色", ResourceVisualPalette.BorderFor(RunStatus.Running) !=
                ResourceVisualPalette.BorderFor(RunStatus.Failed), ref pass, ref fail);
            projector.SelectEntity(added.EntityId);
            host.SetShapeBounds(second, new RectangleF(333, 222, 240, 160));
            host.SetViewport(1.35f, new PointF(44, 55));
            projector.Render(snapshot);
            Check("刷新后按稳定 Id 恢复选中", projector.GetPrimarySelected() != null &&
                projector.GetPrimarySelected().Id == added.EntityId, ref pass, ref fail);
            ShapeInfo bounds = FindShape(host, projector.GetShapeForEntity(added.EntityId));
            Check("刷新后保留坐标与容器尺寸", bounds != null && bounds.X == 333 && bounds.Y == 222 &&
                bounds.Width == 240 && bounds.Height == 160, ref pass, ref fail);
            Check("刷新后保留缩放与视口", Math.Abs(host.GetZoom() - 1.35f) < 0.001f &&
                host.GetViewportOffset().X == 44 && host.GetViewportOffset().Y == 55, ref pass, ref fail);
        }

        private static void TestCoordinator(ref int pass, ref int fail)
        {
            string oldPath = Environment.GetEnvironmentVariable("PATH");
            string directory = Path.Combine(Path.GetTempPath(), "native-manager-tools-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string[] tools = new string[] { "kubectl", "helm", "istioctl", "yq" };
            for (int i = 0; i < tools.Length; i++) File.WriteAllText(Path.Combine(directory, tools[i]), "fake");
            try
            {
                Environment.SetEnvironmentVariable("PATH", directory + Path.PathSeparator + oldPath);
                CoordinatorBundle online = CreateBundle(true, true);
                Check("kubectl 与集群可用时在线", online.Coordinator.Mode == ManagerMode.Online, ref pass, ref fail);
                int before = online.Runner.ExecuteCount;
                online.Coordinator.ConfirmCommand = new CommandConfirmationHandler(RejectConfirmation);
                CommandResult denied = online.Coordinator.ExecuteSynchronously("kubectl delete pod web -n default", false);
                Check("确认前拒绝则零进程启动", !denied.Success && online.Runner.ExecuteCount == before &&
                    denied.Text.Contains("从未启动"), ref pass, ref fail);
                online.Coordinator.ConfirmCommand = new CommandConfirmationHandler(AcceptConfirmation);
                CommandResult confirmed = online.Coordinator.ExecuteSynchronously("kubectl delete pod web -n default", false);
                Check("确认后才启动外部进程", confirmed.Success && online.Runner.ExecuteCount == before + 1, ref pass, ref fail);
                online.Runner.ExitCode = 2;
                online.Runner.StandardError = "synthetic failure";
                CommandResult exit = online.Coordinator.ExecuteSynchronously("kubectl get pods", false);
                Check("非零退出码与 stderr 完整回传", !exit.Success && exit.Text.Contains("退出码: 2") &&
                    exit.Text.Contains("synthetic failure"), ref pass, ref fail);
                online.Runner.ExitCode = 0;
                online.Runner.StandardError = "";

                RefreshRecorder recorder = new RefreshRecorder();
                online.Coordinator.TopologyRefreshed += new EventHandler<TopologyRefreshEventArgs>(recorder.Handle);
                CommandResult writeRefresh = online.Coordinator.ExecuteSynchronously(
                    "kubectl scale deployment web --replicas=2 -n default", true);
                Check("写成功后自动刷新快照", writeRefresh.Success && recorder.Latest != null && recorder.Latest.Success, ref pass, ref fail);
                online.Runner.FailQueries = true;
                CommandResult refreshFailed = online.Coordinator.ExecuteSynchronously(
                    "kubectl scale deployment web --replicas=3 -n default", true);
                Check("刷新失败保留旧快照并标记过期", !refreshFailed.Success && recorder.Latest != null &&
                    !recorder.Latest.Success && recorder.Latest.Snapshot != null && recorder.Latest.Snapshot.IsStale, ref pass, ref fail);

                CoordinatorBundle offline = CreateBundle(false, true);
                Check("集群不可达进入设计模式", offline.Coordinator.Mode == ManagerMode.DesignDebug, ref pass, ref fail);
                int offlineBefore = offline.Runner.ExecuteCount;
                Check("离线集群命令不启动", !offline.Coordinator.ExecuteSynchronously("kubectl get pods", false).Success &&
                    offline.Runner.ExecuteCount == offlineBefore, ref pass, ref fail);
                Check("离线 yq 独立可用", offline.Coordinator.ExecuteSynchronously("yq eval .kind sample.yaml", false).Success,
                    ref pass, ref fail);
                CoordinatorBundle missingYq = CreateBundle(false, false);
                int missingBefore = missingYq.Runner.ExecuteCount;
                Check("工具不可用时给出错误且不启动", !missingYq.Coordinator.ExecuteSynchronously("yq eval .kind sample.yaml", false).Success &&
                    missingYq.Runner.ExecuteCount == missingBefore, ref pass, ref fail);
                Check("工具状态包含安装指导入口", missingYq.Coordinator.ToolchainStatusText().Contains("yq") &&
                    CommandCoordinator.InstallHelpText().Contains("detect"), ref pass, ref fail);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", oldPath);
                try { Directory.Delete(directory, true); } catch { }
            }
        }

        private static void TestProcessPreCancellation(ref int pass, ref int fail)
        {
            using (CommandCancellation cancellation = new CommandCancellation())
            {
                cancellation.Cancel();
                ExternalCommandResult result = new ProcessRunner().Execute(
                    new ExternalCommand("definitely-not-a-real-executable"), cancellation);
                Check("预取消时进程从未启动", result.Cancelled && !result.Started, ref pass, ref fail);
            }
        }

        private static CoordinatorBundle CreateBundle(bool cluster, bool yqAvailable)
        {
            CoordinatorBundle bundle = new CoordinatorBundle();
            bundle.Host = new FakeTopologyHost();
            bundle.Design = new DesignTopologyStore();
            bundle.Projector = new TopologyProjector(bundle.Host);
            bundle.Runner = new FakeProcessRunner();
            bundle.Runner.ClusterSuccess = cluster;
            bundle.Runner.FailYqVersion = !yqAvailable;
            bundle.Coordinator = new CommandCoordinator(bundle.Host, bundle.Projector, bundle.Design, bundle.Runner, null);
            bundle.Coordinator.DetectToolsNow();
            return bundle;
        }

        private static bool RejectConfirmation(string command, string reason) { return false; }
        private static bool AcceptConfirmation(string command, string reason) { return true; }

        private static ManagedEntity Find(TopologySnapshot snapshot, EntityKind kind, string ns, string name)
        {
            for (int i = 0; i < snapshot.Entities.Count; i++)
                if (snapshot.Entities[i].Kind == kind && snapshot.Entities[i].Namespace == ns && snapshot.Entities[i].Name == name)
                    return snapshot.Entities[i];
            return null;
        }

        private static ManagedEntity FindByUid(TopologySnapshot snapshot, string uid)
        {
            for (int i = 0; i < snapshot.Entities.Count; i++) if (snapshot.Entities[i].Uid == uid) return snapshot.Entities[i];
            return null;
        }

        private static bool HasRelation(TopologySnapshot snapshot, string fromUid, string toUid, RelationKind kind)
        {
            ManagedEntity from = FindByUid(snapshot, fromUid);
            ManagedEntity to = FindByUid(snapshot, toUid);
            if (from == null || to == null) return false;
            for (int i = 0; i < snapshot.Relations.Count; i++)
                if (snapshot.Relations[i].FromId == from.Id && snapshot.Relations[i].ToId == to.Id && snapshot.Relations[i].Kind == kind)
                    return true;
            return false;
        }

        private static ShapeInfo FindShape(FakeTopologyHost host, Guid id)
        {
            IList<ShapeInfo> shapes = host.ListShapes();
            for (int i = 0; i < shapes.Count; i++) if (shapes[i].Id == id) return shapes[i];
            return null;
        }

        private static void Check(string name, bool ok, ref int pass, ref int fail)
        {
            if (ok) { pass++; Console.WriteLine("[PASS] " + name); }
            else { fail++; Console.WriteLine("[FAIL] " + name); }
        }

        private sealed class CoordinatorBundle
        {
            internal FakeTopologyHost Host;
            internal DesignTopologyStore Design;
            internal TopologyProjector Projector;
            internal FakeProcessRunner Runner;
            internal CommandCoordinator Coordinator;
        }

        private sealed class RefreshRecorder
        {
            internal TopologyRefreshEventArgs Latest;
            internal void Handle(object sender, TopologyRefreshEventArgs args) { Latest = args; }
        }

        private sealed class FakeProcessRunner : IProcessRunner
        {
            internal int ExecuteCount;
            internal int ExitCode;
            internal string StandardError = "";
            internal bool ClusterSuccess;
            internal bool FailYqVersion;
            internal bool FailQueries;

            public ExternalCommandResult Execute(ExternalCommand command) { return Execute(command, null); }

            public ExternalCommandResult Execute(ExternalCommand command, CommandCancellation cancellation)
            {
                ExecuteCount++;
                ExternalCommandResult result = new ExternalCommandResult();
                result.CommandText = command.DisplayText;
                result.Started = true;
                string tool = Path.GetFileName(command.Executable).ToLowerInvariant();
                string verb = command.Arguments.Count > 0 ? command.Arguments[0] : "";
                if (verb == "version" || (tool == "yq" && verb == "--version"))
                {
                    result.Success = !(tool == "yq" && FailYqVersion);
                    result.ExitCode = result.Success ? 0 : 1;
                    result.StandardOutput = result.Success ? tool + " v1-selftest" : "";
                    result.StandardError = result.Success ? "" : "synthetic missing tool";
                    return result;
                }
                if (tool == "kubectl" && verb == "cluster-info")
                {
                    result.Success = ClusterSuccess;
                    result.ExitCode = ClusterSuccess ? 0 : 1;
                    result.StandardOutput = ClusterSuccess ? "cluster running" : "";
                    result.StandardError = ClusterSuccess ? "" : "synthetic unreachable";
                    return result;
                }
                bool query = (tool == "kubectl" && verb == "get") || (tool == "helm" && verb == "list");
                if (query && FailQueries)
                {
                    result.Success = false; result.ExitCode = 1; result.StandardError = "query failed";
                    return result;
                }
                if (tool == "kubectl" && verb == "get") result.StandardOutput = "{\"items\":[]}";
                else if (tool == "helm" && verb == "list") result.StandardOutput = "[]";
                else result.StandardOutput = "synthetic command output";
                result.StandardError = StandardError;
                result.ExitCode = ExitCode;
                result.Success = ExitCode == 0;
                return result;
            }
        }

        private sealed class FakeGatewayExecutor : IResourceQueryExecutor
        {
            internal bool FailCluster;

            public string Execute(string tool, IList<string> arguments)
            {
                if (tool == "helm") return "[]";
                string joined = string.Join(" ", new List<string>(arguments).ToArray());
                if (joined.Contains("namespaces,nodes,persistentvolumes"))
                {
                    if (FailCluster) throw new InvalidOperationException("synthetic cluster query failure");
                    return "{\"items\":[" + Namespace("a") + "," + Namespace("b") + "]}";
                }
                if (joined.Contains("deployments,replicasets")) return NamespacedItems();
                return "{\"items\":[]}";
            }

            private static string Namespace(string name)
            {
                return "{\"kind\":\"Namespace\",\"metadata\":{\"name\":\"" + name + "\",\"uid\":\"ns-" + name + "\"}}";
            }

            private static string NamespacedItems()
            {
                return "{\"items\":[" +
                    Workload("Deployment", "web", "a", "dep-a") + "," + Workload("Deployment", "web", "b", "dep-b") + "," +
                    Pod("a", "pod-a", "dep-a") + "," + Pod("b", "pod-b", "dep-b") + "," +
                    Service("a", "svc-a") + "," + Service("b", "svc-b") + "]}";
            }

            private static string Workload(string kind, string name, string ns, string uid)
            {
                return "{\"kind\":\"" + kind + "\",\"metadata\":{\"name\":\"" + name + "\",\"namespace\":\"" + ns +
                    "\",\"uid\":\"" + uid + "\"},\"spec\":{\"replicas\":1},\"status\":{\"readyReplicas\":1}}";
            }

            private static string Pod(string ns, string uid, string owner)
            {
                return "{\"kind\":\"Pod\",\"metadata\":{\"name\":\"web-pod\",\"namespace\":\"" + ns + "\",\"uid\":\"" + uid +
                    "\",\"labels\":{\"app\":\"web\"},\"ownerReferences\":[{\"uid\":\"" + owner + "\"}]},\"status\":{\"phase\":\"Running\"}}";
            }

            private static string Service(string ns, string uid)
            {
                return "{\"kind\":\"Service\",\"metadata\":{\"name\":\"web\",\"namespace\":\"" + ns + "\",\"uid\":\"" + uid +
                    "\"},\"spec\":{\"selector\":{\"app\":\"web\"}}}";
            }
        }
    }
}
