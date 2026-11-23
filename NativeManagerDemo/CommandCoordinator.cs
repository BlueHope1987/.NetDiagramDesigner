using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    /// <summary>在启动进程之前进行确认；返回 true 才允许执行。</summary>
    public delegate bool CommandConfirmationHandler(string commandText, string reason);

    public class CoordinatorOutputEventArgs : EventArgs
    {
        public CommandResult Result;
    }

    public class CoordinatorBusyEventArgs : EventArgs
    {
        public bool Busy;
    }

    public class ManagerModeEventArgs : EventArgs
    {
        public ManagerMode Mode;
        public string Message;
    }

    public class TopologyRefreshEventArgs : EventArgs
    {
        public TopologySnapshot Snapshot;
        public bool Success;
        public string Message;
    }

    /// <summary>
    /// 命令行、MMC 操作与拓扑快照的统一入口。UI 调用默认异步执行；
    /// 所有外部进程都经过解析、工具状态检查、白名单与确认策略。
    /// </summary>
    public class CommandCoordinator
    {
        private readonly ITopologyHost _host;
        private readonly TopologyProjector _projector;
        private readonly DesignTopologyStore _design;
        private readonly IProcessRunner _runner;
        private readonly ToolchainDetector _detector;
        private readonly object _sync;
        private ToolchainDetectionResult _tools;
        private TopologySnapshot _onlineSnapshot;
        private ManagerMode _mode;
        private CommandCancellation _cancellation;
        private bool _busy;

        public event EventHandler<CoordinatorOutputEventArgs> OutputReceived;
        public event EventHandler<CoordinatorBusyEventArgs> BusyChanged;
        public event EventHandler<ManagerModeEventArgs> ModeChanged;
        public event EventHandler<TopologyRefreshEventArgs> TopologyRefreshed;
        public event EventHandler DesignChanged;
        public event EventHandler DetectionCompleted;

        /// <summary>高危确认由主窗体提供；未提供时拒绝需要确认的命令。</summary>
        public CommandConfirmationHandler ConfirmCommand;

        public CommandCoordinator(ITopologyHost host, TopologyProjector projector, DesignTopologyStore design)
            : this(host, projector, design, new ProcessRunner(), null)
        {
        }

        internal CommandCoordinator(ITopologyHost host, TopologyProjector projector,
            DesignTopologyStore design, IProcessRunner runner, ToolchainDetector detector)
        {
            if (host == null) throw new ArgumentNullException("host");
            if (projector == null) throw new ArgumentNullException("projector");
            if (design == null) throw new ArgumentNullException("design");
            if (runner == null) throw new ArgumentNullException("runner");
            _host = host;
            _projector = projector;
            _design = design;
            _runner = runner;
            _detector = detector == null ? new ToolchainDetector(runner) : detector;
            _sync = new object();
            _mode = ManagerMode.Checking;
        }

        public ManagerMode Mode
        {
            get { lock (_sync) return _mode; }
        }

        public bool IsBusy
        {
            get { lock (_sync) return _busy; }
        }

        public ToolchainDetectionResult Tools
        {
            get { lock (_sync) return _tools; }
        }

        public TopologySnapshot CurrentSnapshot
        {
            get
            {
                lock (_sync)
                {
                    if (_mode == ManagerMode.DesignDebug) return _design.QueryTopology();
                    return _onlineSnapshot == null ? null : DesignTopologyStore.Clone(_onlineSnapshot);
                }
            }
        }

        /// <summary>命令行入口。外部命令和刷新异步完成，结果通过 OutputReceived 返回。</summary>
        public CommandResult Execute(string input)
        {
            List<string> tokens;
            string parseError;
            if (!CommandLineTokenizer.TryParse(input, out tokens, out parseError))
                return Fail(parseError);
            if (tokens.Count == 0) return Fail("请输入命令；help 查看中文帮助。");

            string verb = tokens[0].ToLowerInvariant();
            if (IsExternalTool(verb)) return ExecuteExternal(input, false);
            try
            {
                switch (verb)
                {
                    case "help": case "?": return Ok(HelpText());
                    case "tools": return Ok(ToolchainStatusText());
                    case "install-help": return Ok(InstallHelpText());
                    case "detect": DetectToolsAsync(); return Ok("已开始重新检测四个工具，请稍候。");
                    case "refresh": return RefreshTopologyAsync();
                    case "mode": return ChangeMode(tokens);
                    case "list": case "ls": return Ok(ListEntities(tokens));
                    case "relations": case "conns": return Ok(ListRelations());
                    case "select": return Select(tokens);
                    case "count": return Ok(CountText());
                    case "add": return AddDesignEntity(tokens);
                    case "delete": case "del": case "rm": return DeleteDesignEntity(tokens);
                    case "connect": case "link": return ConnectDesignEntities(tokens);
                    case "rename": return RenameDesignEntity(tokens);
                    case "replicas": return SetDesignReplicas(tokens, 1);
                    case "set": return SetDesignValue(tokens);
                    case "new": return NewDesign();
                    case "save": return SaveDesign(tokens);
                    case "load": return LoadDesign(tokens);
                    case "fit": _host.FitView(); return Ok("已适应视图。");
                    case "zoom": return Zoom(tokens);
                    case "front": _host.BringToFrontSelection(); return Ok("已将选中图形置顶（仅视觉布局）。");
                    case "back": _host.SendToBackSelection(); return Ok("已将选中图形置底（仅视觉布局）。");
                    default: return Fail("未知命令: " + tokens[0] + "。输入 help 查看安全命令与图形命令。");
                }
            }
            catch (Exception ex)
            {
                return Fail("执行失败: " + ex.Message);
            }
        }

        /// <summary>MMC 与命令行共用同一动词、模式和安全策略。</summary>
        public CommandResult ExecuteVerb(VerbInvokedEventArgs args)
        {
            if (args == null || args.Entity == null || args.Verb == null)
                return Fail("操作参数不完整。");
            ManagerMode mode = Mode;
            if (mode == ManagerMode.Checking) return Fail("正在检测工具，请稍候。");
            if (mode == ManagerMode.DesignDebug)
            {
                if (!args.Verb.SupportsDesign)
                    return Fail("该操作需要在线集群；设计模式不会向集群发送写操作。");
                if (args.Verb.Verb == "scale")
                {
                    if (args.Replicas < 0) return Fail("请先输入新的副本数。");
                    return DesignResult(_design.SetReplicas(args.Entity.Id, args.Replicas));
                }
                if (args.Verb.Verb == "delete")
                {
                    if (!ConfirmLocal("design delete " + args.Entity.Id,
                        "删除本地设计对象 " + args.Entity.Name + "。此操作不修改集群，是否继续？"))
                        return Fail("已取消，设计快照未修改。");
                    return DesignResult(_design.DeleteEntity(args.Entity.Id));
                }
                return Fail("该操作的设计态行为尚未定义。");
            }
            if (args.Verb.RequiresOnline && !CanUseCluster())
                return Fail("kubectl 或集群当前不可用，请使用 tools 查看原因。");
            if (string.IsNullOrEmpty(args.CommandText)) return Fail("该操作没有可执行的安全命令。");
            return ExecuteExternal(args.CommandText, args.Verb.RefreshAfter);
        }

        public void Cancel()
        {
            CommandCancellation cancellation;
            lock (_sync) cancellation = _cancellation;
            if (cancellation != null)
            {
                cancellation.Cancel();
                PublishOutput(new CommandResult(true, "已请求取消；正在终止当前外部进程或资源查询。"));
            }
        }

        public void DetectToolsAsync()
        {
            if (!TryBeginOperation()) { PublishOutput(Fail("已有命令或检测正在执行，请先完成或取消。")); return; }
            SetMode(ManagerMode.Checking, "正在分别检测 kubectl、helm、istioctl 与 yq。 ");
            ThreadPool.QueueUserWorkItem(new WaitCallback(DetectionWorker));
        }

        /// <summary>同步检测供无头自检与非 UI 调用使用。</summary>
        public ToolchainDetectionResult DetectToolsNow()
        {
            ToolchainDetectionResult result = _detector.DetectAll();
            ApplyDetection(result);
            return result;
        }

        public CommandResult UseDesignMode()
        {
            SetMode(ManagerMode.DesignDebug, "已切换到设计·调试模式；新增、删除、换父和连线只修改本地快照。");
            RaiseDesignChanged();
            return Ok("设计·调试模式：不会修改在线集群。");
        }

        public CommandResult RefreshTopologyAsync()
        {
            if (Mode != ManagerMode.Online || !CanUseCluster())
                return Fail("在线刷新需要可用的 kubectl 和可连接的集群；输入 tools 查看检测详情。");
            if (!TryBeginOperation()) return Fail("已有命令正在执行，请先完成或取消。");
            ThreadPool.QueueUserWorkItem(new WaitCallback(RefreshWorker));
            return Ok("已开始读取集群资源；完成前仍保留当前快照。");
        }

        public string ToolchainStatusText()
        {
            ToolchainDetectionResult tools = Tools;
            if (tools == null) return "尚未完成工具检测。输入 detect 重新检测，或稍候查看启动检测结果。";
            StringBuilder text = new StringBuilder();
            text.AppendLine("模式: " + ModeText(Mode));
            IList<ToolDetectionResult> statuses = tools.Tools;
            int i;
            for (i = 0; i < statuses.Count; i++) text.AppendLine(ToolStatusLine(statuses[i]));
            text.Append("提示: yq 可独立离线使用；kubectl、helm、istioctl 的集群操作需要在线模式。");
            return text.ToString();
        }

        public static string InstallHelpText()
        {
            return "工具安装指导（管理器只检测，不会自动下载或执行安装）:\r\n" +
                "kubectl: https://kubernetes.io/docs/tasks/tools/ ；Windows 可用 winget install Kubernetes.kubectl。\r\n" +
                "helm: https://helm.sh/docs/intro/install/ ；Windows 可用 winget install Helm.Helm。\r\n" +
                "istioctl: https://istio.io/latest/docs/ops/diagnostic-tools/istioctl/ ；解压后将 istioctl 放入 PATH。\r\n" +
                "yq: https://github.com/mikefarah/yq#install ；首版兼容 mikefarah/yq 的 eval/eval-all，禁止原地写入。\r\n" +
                "安装后请确认当前用户 PATH 已更新，再输入 detect。集群操作还需要有效 KUBECONFIG 与 RBAC 权限。";
        }

        internal CommandResult ExecuteSynchronously(string commandLine, bool refreshAfter)
        {
            return StartExternal(commandLine, refreshAfter, false);
        }

        private CommandResult ExecuteExternal(string commandLine, bool refreshAfter)
        {
            return StartExternal(commandLine, refreshAfter, true);
        }

        private CommandResult StartExternal(string commandLine, bool refreshAfter, bool asynchronous)
        {
            List<string> tokens;
            string parseError;
            if (!CommandLineTokenizer.TryParse(commandLine, out tokens, out parseError)) return Fail(parseError);
            if (tokens.Count == 0) return Fail("外部命令不能为空。");
            string tool = tokens[0].ToLowerInvariant();
            if (!IsExternalTool(tool)) return Fail("仅允许 kubectl、helm、istioctl 和 yq。");

            List<string> arguments = new List<string>();
            int i;
            for (i = 1; i < tokens.Count; i++) arguments.Add(tokens[i]);
            CommandPolicyDecision decision = CommandPolicy.Evaluate(tool, arguments);
            if (!decision.Allowed) return Fail("安全策略已拒绝: " + decision.Reason);
            ToolDetectionResult status = FindTool(tool);
            if (status == null) return Fail("工具检测尚未完成，请稍候或输入 detect。");
            if (!status.IsAvailable)
                return Fail(tool + " 不可用: " + (string.IsNullOrEmpty(status.ErrorMessage) ? "版本探测失败" : status.ErrorMessage) +
                    "。输入 install-help 查看安装指导。");
            if (tool != "yq" && (Mode != ManagerMode.Online || !CanUseCluster()))
                return Fail(tool + " 的集群操作仅在在线模式开放；当前为" + ModeText(Mode) + "。yq 可离线使用。");
            if (!TryBeginOperation()) return Fail("已有命令正在执行，请先完成或取消。");

            ExternalCommand command = new ExternalCommand(status.ExecutablePath, arguments);
            command.DisplayText = commandLine;
            command.TimeoutMilliseconds = 60000;
            command.MaxOutputCharacters = 524288;
            if (decision.RequiresConfirmation &&
                !ConfirmLocal(commandLine, decision.Reason + "\r\n\r\n完整命令:\r\n" + commandLine))
            {
                EndOperation();
                return Fail("已取消；确认未通过，外部进程从未启动。");
            }

            PreparedExecution prepared = new PreparedExecution();
            prepared.Command = command;
            prepared.RefreshAfter = refreshAfter;
            if (!asynchronous)
            {
                CommandResult result = RunPrepared(prepared);
                EndOperation();
                return result;
            }
            try
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(ExternalWorker), prepared);
                return Ok("命令已通过安全检查并提交执行；完整结果将显示在下方。可用取消按钮或 Ctrl+C 终止。");
            }
            catch (Exception ex)
            {
                EndOperation();
                return Fail("无法启动后台命令: " + ex.Message);
            }
        }

        private void ExternalWorker(object state)
        {
            try
            {
                PreparedExecution prepared = (PreparedExecution)state;
                CommandResult result = RunPrepared(prepared);
                PublishOutput(result);
            }
            catch (Exception ex)
            {
                PublishOutput(Fail("后台命令执行异常: " + ex.Message));
            }
            finally { EndOperation(); }
        }

        private CommandResult RunPrepared(PreparedExecution prepared)
        {
            CommandCancellation cancellation;
            lock (_sync) cancellation = _cancellation;
            ExternalCommandResult raw = _runner.Execute(prepared.Command, cancellation);
            CommandResult result = FormatExternalResult(raw);
            if (raw.Success && prepared.RefreshAfter && Mode == ManagerMode.Online && CanUseCluster())
            {
                TopologyRefreshEventArgs refresh = QueryAndPublishTopology();
                result.Text += Environment.NewLine + Environment.NewLine + refresh.Message;
                if (!refresh.Success) result.Success = false;
            }
            return result;
        }

        private void DetectionWorker(object state)
        {
            try
            {
                ToolchainDetectionResult detected = _detector.DetectAll();
                ApplyDetection(detected);
                if (DetectionCompleted != null) DetectionCompleted(this, EventArgs.Empty);
                StringBuilder output = new StringBuilder(ToolchainStatusText());
                if (Mode == ManagerMode.Online)
                {
                    TopologyRefreshEventArgs refresh = QueryAndPublishTopology();
                    output.AppendLine(); output.Append(refresh.Message);
                }
                PublishOutput(new CommandResult(true, output.ToString()));
            }
            catch (Exception ex)
            {
                SetMode(ManagerMode.DesignDebug, "工具检测异常，已进入设计·调试模式: " + ex.Message);
                PublishOutput(Fail("工具检测失败，已安全进入设计·调试模式: " + ex.Message));
            }
            finally { EndOperation(); }
        }

        private void ApplyDetection(ToolchainDetectionResult detected)
        {
            if (detected == null) throw new ArgumentNullException("detected");
            lock (_sync) _tools = detected;
            bool online = detected.Kubectl.IsAvailable && detected.Kubectl.ClusterReachable.HasValue &&
                detected.Kubectl.ClusterReachable.Value;
            if (online)
            {
                SetMode(ManagerMode.Online, "kubectl 与集群均可用，已进入在线模式。");
            }
            else
            {
                string reason = !detected.Kubectl.IsAvailable
                    ? "kubectl 不可用"
                    : (!detected.Kubectl.ClusterReachable.HasValue || !detected.Kubectl.ClusterReachable.Value
                        ? "集群不可达" : "正在检查集群");
                SetMode(ManagerMode.DesignDebug, reason + "，已进入设计·调试模式；其他已安装工具仍独立显示状态。");
            }
        }

        private void RefreshWorker(object state)
        {
            try
            {
                TopologyRefreshEventArgs result = QueryAndPublishTopology();
                PublishOutput(new CommandResult(result.Success, result.Message));
            }
            catch (Exception ex) { PublishOutput(Fail("刷新异常: " + ex.Message)); }
            finally { EndOperation(); }
        }

        private TopologyRefreshEventArgs QueryAndPublishTopology()
        {
            TopologyRefreshEventArgs args = new TopologyRefreshEventArgs();
            try
            {
                ResourceQueryExecutor executor = new ResourceQueryExecutor(_runner, Tools, _cancellation);
                KubectlTopologyGateway gateway = new KubectlTopologyGateway(executor);
                TopologySnapshot snapshot = gateway.QueryTopology();
                lock (_sync) _onlineSnapshot = snapshot;
                args.Snapshot = DesignTopologyStore.Clone(snapshot);
                args.Success = true;
                args.Message = "集群快照已更新: " + snapshot.Entities.Count + " 个资源，" + snapshot.Relations.Count +
                    " 条关系，" + snapshot.Warnings.Count + " 条警告。";
            }
            catch (Exception ex)
            {
                args.Success = false;
                args.Message = "集群刷新失败: " + ex.Message + "。已保留上一份快照并标记为过期。";
                lock (_sync)
                {
                    if (_onlineSnapshot != null)
                    {
                        _onlineSnapshot.IsStale = true;
                        _onlineSnapshot.Warnings.Add(args.Message);
                        args.Snapshot = DesignTopologyStore.Clone(_onlineSnapshot);
                    }
                }
            }
            if (TopologyRefreshed != null) TopologyRefreshed(this, args);
            return args;
        }

        private CommandResult ChangeMode(IList<string> tokens)
        {
            if (tokens.Count == 1)
                return Ok("当前模式: " + ModeText(Mode) + "。mode design 进入本地设计；mode online 重新连接并刷新。");
            string target = tokens[1].ToLowerInvariant();
            if (target == "design" || target == "debug") return UseDesignMode();
            if (target == "online")
            {
                if (!CanUseCluster()) return Fail("无法进入在线模式: kubectl 不可用或集群不可达。输入 tools 查看原因。");
                SetMode(ManagerMode.Online, "已切换在线模式，正在刷新集群快照。");
                return RefreshTopologyAsync();
            }
            return Fail("用法: mode design | mode online");
        }

        private CommandResult AddDesignEntity(IList<string> tokens)
        {
            CommandResult modeError = RequireDesign("新增对象"); if (modeError != null) return modeError;
            if (tokens.Count < 3) return Fail("用法: add <类型> <名称> [Namespace]，例如 add deployment web default");
            EntityKind kind = ParseKind(tokens[1]);
            if (kind == EntityKind.Unknown) return Fail("未知资源类型: " + tokens[1] + "。输入 help 查看类型示例。");
            return DesignResult(_design.AddEntity(kind, tokens[2], tokens.Count > 3 ? tokens[3] : ""));
        }

        private CommandResult DeleteDesignEntity(IList<string> tokens)
        {
            CommandResult modeError = RequireDesign("删除本地对象"); if (modeError != null) return modeError;
            ManagedEntity entity;
            if (tokens.Count > 1) entity = ResolveEntity(tokens[1]);
            else entity = _projector.GetPrimarySelected();
            if (entity == null) return Fail("未找到对象，或同名对象不唯一。请从画布选择后重试。");
            if (!ConfirmLocal("design delete " + entity.Id,
                "删除本地设计对象 " + DisplayEntity(entity) + "。此操作不修改集群，是否继续？"))
                return Fail("已取消，设计快照未修改。");
            return DesignResult(_design.DeleteEntity(entity.Id));
        }

        private CommandResult ConnectDesignEntities(IList<string> tokens)
        {
            CommandResult modeError = RequireDesign("新增语义连线"); if (modeError != null) return modeError;
            if (tokens.Count < 3) return Fail("用法: connect <源对象> <目标对象> [owns|selects|routes|manages|depends|reads]");
            ManagedEntity from = ResolveEntity(tokens[1]);
            ManagedEntity to = ResolveEntity(tokens[2]);
            if (from == null || to == null) return Fail("连线端点不存在或名称不唯一，可使用 list 查看 Id。");
            RelationKind kind = tokens.Count > 3 ? ParseRelation(tokens[3]) : CanvasIntentController.InferRelation(from, to);
            return DesignResult(_design.AddRelation(from.Id, to.Id, kind));
        }

        private CommandResult RenameDesignEntity(IList<string> tokens)
        {
            CommandResult modeError = RequireDesign("重命名"); if (modeError != null) return modeError;
            if (tokens.Count < 2) return Fail("用法: rename <新名称>（先选择对象）");
            ManagedEntity entity = _projector.GetPrimarySelected();
            if (entity == null) return Fail("请先选择一个设计对象。");
            return DesignResult(_design.RenameEntity(entity.Id, tokens[1]));
        }

        private CommandResult SetDesignValue(IList<string> tokens)
        {
            if (tokens.Count >= 2 && tokens[1].ToLowerInvariant() == "replicas") return SetDesignReplicas(tokens, 2);
            return Fail("设计属性仅支持: set replicas <数量>；位置和大小请直接拖动图形。 ");
        }

        private CommandResult SetDesignReplicas(IList<string> tokens, int valueIndex)
        {
            CommandResult modeError = RequireDesign("副本设计"); if (modeError != null) return modeError;
            if (tokens.Count <= valueIndex) return Fail("用法: set replicas <数量>（先选择工作负载）");
            int replicas;
            if (!int.TryParse(tokens[valueIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out replicas))
                return Fail("副本数必须是整数。");
            ManagedEntity entity = _projector.GetPrimarySelected();
            if (entity == null) return Fail("请先选择一个工作负载。");
            return DesignResult(_design.SetReplicas(entity.Id, replicas));
        }

        private CommandResult NewDesign()
        {
            if (!ConfirmLocal("design new", "新建会替换当前本地设计快照，是否继续？")) return Fail("已取消新建。");
            _design.Replace(DesignTopologyStore.CreateInitialSnapshot());
            SetMode(ManagerMode.DesignDebug, "已新建本地设计快照。");
            RaiseDesignChanged();
            return Ok("已新建设计快照；在线集群未受影响。");
        }

        private CommandResult SaveDesign(IList<string> tokens)
        {
            CommandResult modeError = RequireDesign("保存设计"); if (modeError != null) return modeError;
            if (tokens.Count < 2) return Fail("用法: save <设计.json>；含空格路径请使用引号，Windows 路径建议使用 /。");
            _design.Save(tokens[1]);
            _projector.SaveLayout();
            return Ok("设计快照与视觉布局已保存: " + tokens[1]);
        }

        private CommandResult LoadDesign(IList<string> tokens)
        {
            if (tokens.Count < 2) return Fail("用法: load <设计.json>；含空格路径请使用引号。");
            _design.Load(tokens[1]);
            SetMode(ManagerMode.DesignDebug, "设计文件已载入；不会自动应用到集群。");
            RaiseDesignChanged();
            return Ok("已载入设计快照: " + tokens[1]);
        }

        private CommandResult Select(IList<string> tokens)
        {
            if (tokens.Count < 2) return Fail("用法: select <名称|Id|UID> | all | none");
            if (tokens[1] == "all") { _host.SelectAll(); return Ok("已全选图形。"); }
            if (tokens[1] == "none" || tokens[1] == "clear") { _host.ClearSelection(); return Ok("已取消选择。"); }
            ManagedEntity entity = ResolveEntity(tokens[1]);
            if (entity == null) return Fail("未找到对象或名称不唯一；list 可查看稳定 Id。");
            return _projector.SelectEntity(entity.Id) ? Ok("已选中 " + DisplayEntity(entity)) : Fail("对象尚未投影到画布。");
        }

        private string ListEntities(IList<string> tokens)
        {
            TopologySnapshot snapshot = CurrentSnapshot;
            if (snapshot == null) return "(尚无快照)";
            string ns = tokens.Count > 1 ? tokens[1] : null;
            StringBuilder text = new StringBuilder();
            text.AppendLine("KIND                 NAMESPACE          NAME                 STATUS       ID");
            int i;
            for (i = 0; i < snapshot.Entities.Count; i++)
            {
                ManagedEntity e = snapshot.Entities[i];
                if (ns != null && e.Namespace != ns) continue;
                text.AppendLine(string.Format("{0,-20} {1,-18} {2,-20} {3,-12} {4}",
                    e.Kind.ToString(), e.Namespace, e.Name, e.Status.ToString(), e.Id));
            }
            return text.ToString().TrimEnd();
        }

        private string ListRelations()
        {
            TopologySnapshot snapshot = CurrentSnapshot;
            if (snapshot == null || snapshot.Relations.Count == 0) return "(无语义连线)";
            StringBuilder text = new StringBuilder();
            int i;
            for (i = 0; i < snapshot.Relations.Count; i++)
            {
                Relation relation = snapshot.Relations[i];
                ManagedEntity from = snapshot.FindEntity(relation.FromId);
                ManagedEntity to = snapshot.FindEntity(relation.ToId);
                text.AppendLine(relation.Kind + ": " + (from == null ? relation.FromId : DisplayEntity(from)) +
                    " -> " + (to == null ? relation.ToId : DisplayEntity(to)) + "  [" + relation.Id + "]");
            }
            return text.ToString().TrimEnd();
        }

        private string CountText()
        {
            TopologySnapshot snapshot = CurrentSnapshot;
            return snapshot == null ? "资源 0 · 关系 0" : "资源 " + snapshot.Entities.Count + " · 关系 " + snapshot.Relations.Count;
        }

        private CommandResult Zoom(IList<string> tokens)
        {
            if (tokens.Count < 2) return Fail("用法: zoom in | out | <百分比>");
            string value = tokens[1].ToLowerInvariant();
            if (value == "in") { _host.Zoom(1.2f); return Ok("已放大。"); }
            if (value == "out") { _host.Zoom(1.0f / 1.2f); return Ok("已缩小。"); }
            value = value.Replace("%", "");
            float percent;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out percent) || percent <= 0)
                return Fail("缩放必须是正数百分比，例如 zoom 125。");
            _host.SetZoom(percent / 100.0f);
            return Ok("缩放已设置为 " + percent.ToString("0.#", CultureInfo.InvariantCulture) + "%。");
        }

        private ManagedEntity ResolveEntity(string key)
        {
            TopologySnapshot snapshot = CurrentSnapshot;
            if (snapshot == null) return null;
            ManagedEntity exact = snapshot.FindEntity(key);
            if (exact != null) return exact;
            ManagedEntity match = null;
            int i;
            for (i = 0; i < snapshot.Entities.Count; i++)
            {
                ManagedEntity entity = snapshot.Entities[i];
                if (!string.Equals(entity.Name, key, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(entity.Uid, key, StringComparison.OrdinalIgnoreCase)) continue;
                if (match != null) return null;
                match = entity;
            }
            return match;
        }

        private CommandResult RequireDesign(string operation)
        {
            if (Mode != ManagerMode.DesignDebug)
                return Fail(operation + "只在设计·调试模式开放；在线模式请使用右侧受控操作或完整 kubectl/helm 命令。");
            return null;
        }

        private CommandResult DesignResult(DesignEditResult result)
        {
            if (result.Success) RaiseDesignChanged();
            return new CommandResult(result.Success, result.Message);
        }

        private void RaiseDesignChanged()
        {
            if (DesignChanged != null) DesignChanged(this, EventArgs.Empty);
        }

        private bool ConfirmLocal(string commandText, string reason)
        {
            CommandConfirmationHandler handler = ConfirmCommand;
            return handler != null && handler(commandText, reason);
        }

        private bool CanUseCluster()
        {
            ToolchainDetectionResult tools = Tools;
            return tools != null && tools.Kubectl.IsAvailable && tools.Kubectl.ClusterReachable.HasValue &&
                tools.Kubectl.ClusterReachable.Value;
        }

        private ToolDetectionResult FindTool(string name)
        {
            ToolchainDetectionResult tools = Tools;
            if (tools == null) return null;
            switch (name)
            {
                case "kubectl": return tools.Kubectl;
                case "helm": return tools.Helm;
                case "istioctl": return tools.IstioCtl;
                case "yq": return tools.Yq;
                default: return null;
            }
        }

        private bool TryBeginOperation()
        {
            lock (_sync)
            {
                if (_busy) return false;
                if (_cancellation != null) _cancellation.Dispose();
                _cancellation = new CommandCancellation();
                _busy = true;
            }
            RaiseBusy(true);
            return true;
        }

        private void EndOperation()
        {
            lock (_sync)
            {
                _busy = false;
                if (_cancellation != null) { _cancellation.Dispose(); _cancellation = null; }
            }
            RaiseBusy(false);
        }

        private void RaiseBusy(bool busy)
        {
            if (BusyChanged == null) return;
            CoordinatorBusyEventArgs args = new CoordinatorBusyEventArgs();
            args.Busy = busy;
            BusyChanged(this, args);
        }

        private void SetMode(ManagerMode mode, string message)
        {
            lock (_sync) _mode = mode;
            if (ModeChanged == null) return;
            ManagerModeEventArgs args = new ManagerModeEventArgs();
            args.Mode = mode;
            args.Message = message;
            ModeChanged(this, args);
        }

        private void PublishOutput(CommandResult result)
        {
            if (OutputReceived == null) return;
            CoordinatorOutputEventArgs args = new CoordinatorOutputEventArgs();
            args.Result = result;
            OutputReceived(this, args);
        }

        private static CommandResult FormatExternalResult(ExternalCommandResult raw)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("命令: " + raw.CommandText);
            text.AppendLine(raw.Success ? "结果: 成功" : "结果: 失败");
            text.AppendLine("进程: " + (raw.Started ? "已启动" : "未启动") +
                "；退出码: " + raw.ExitCode + "；耗时: " + raw.Duration.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms");
            if (raw.TimedOut) text.AppendLine("状态: 已超时并请求终止。");
            if (raw.Cancelled) text.AppendLine("状态: 已取消。");
            if (!string.IsNullOrEmpty(raw.StandardOutput))
            {
                text.AppendLine("--- 标准输出 ---"); text.Append(raw.StandardOutput.TrimEnd());
                if (raw.StandardOutputTruncated)
                {
                    text.AppendLine();
                    text.Append("[标准输出已按安全上限截断]");
                }
            }
            if (!string.IsNullOrEmpty(raw.StandardError))
            {
                text.AppendLine(); text.AppendLine("--- 标准错误 ---"); text.Append(raw.StandardError.TrimEnd());
                if (raw.StandardErrorTruncated)
                {
                    text.AppendLine();
                    text.Append("[标准错误已按安全上限截断]");
                }
            }
            if (!string.IsNullOrEmpty(raw.ErrorMessage))
            {
                text.AppendLine(); text.AppendLine("错误信息: " + raw.ErrorMessage);
            }
            return new CommandResult(raw.Success, text.ToString().TrimEnd());
        }

        private static string ToolStatusLine(ToolDetectionResult status)
        {
            StringBuilder line = new StringBuilder();
            line.Append(status.ToolName).Append(": ");
            if (!status.IsInstalled) line.Append("未安装");
            else if (!status.VersionProbeSucceeded) line.Append("已定位，但版本探测失败");
            else line.Append("可用");
            if (!string.IsNullOrEmpty(status.Version)) line.Append(" · ").Append(status.Version);
            if (!string.IsNullOrEmpty(status.ExecutablePath)) line.Append(" · ").Append(status.ExecutablePath);
            if (!string.IsNullOrEmpty(status.ErrorMessage)) line.Append(" · ").Append(status.ErrorMessage);
            if (status.ClusterChecked)
            {
                line.Append(" · 集群: ").Append(status.ClusterReachable.HasValue && status.ClusterReachable.Value ? "可达" : "不可达");
                if (!string.IsNullOrEmpty(status.ClusterMessage)) line.Append("（").Append(status.ClusterMessage).Append("）");
            }
            return line.ToString();
        }

        private static EntityKind ParseKind(string value)
        {
            string key = value.ToLowerInvariant();
            switch (key)
            {
                case "ns": key = "namespace"; break;
                case "deploy": key = "deployment"; break;
                case "rs": key = "replicaset"; break;
                case "sts": key = "statefulset"; break;
                case "ds": key = "daemonset"; break;
                case "po": key = "pod"; break;
                case "svc": key = "service"; break;
                case "ing": key = "ingress"; break;
                case "cm": key = "configmap"; break;
                case "pv": key = "persistentvolume"; break;
                case "pvc": key = "persistentvolumeclaim"; break;
                case "vs": key = "virtualservice.networking.istio.io"; break;
                case "dr": key = "destinationrule.networking.istio.io"; break;
                case "gw": key = "gateway.networking.istio.io"; break;
            }
            Array values = Enum.GetValues(typeof(EntityKind));
            int i;
            for (i = 0; i < values.Length; i++)
            {
                EntityKind kind = (EntityKind)values.GetValue(i);
                if (VerbMatrix.KindName(kind) == key || kind.ToString().ToLowerInvariant() == key) return kind;
            }
            return EntityKind.Unknown;
        }

        private static RelationKind ParseRelation(string value)
        {
            try { return (RelationKind)Enum.Parse(typeof(RelationKind), value, true); }
            catch { return RelationKind.Unknown; }
        }

        private static string DisplayEntity(ManagedEntity entity)
        {
            return VerbMatrix.KindName(entity.Kind) + "/" +
                (string.IsNullOrEmpty(entity.Namespace) ? "" : entity.Namespace + "/") + entity.Name;
        }

        private static string ModeText(ManagerMode mode)
        {
            switch (mode)
            {
                case ManagerMode.Online: return "在线模式";
                case ManagerMode.DesignDebug: return "设计·调试模式";
                default: return "工具检测中";
            }
        }

        private static bool IsExternalTool(string name)
        {
            return name == "kubectl" || name == "helm" || name == "istioctl" || name == "yq";
        }

        private static CommandResult Ok(string text) { return new CommandResult(true, text); }
        private static CommandResult Fail(string text) { return new CommandResult(false, text); }

        private static string HelpText()
        {
            return "安全外部命令（不经过 shell，不支持管道、重定向、多命令或交互终端）:\r\n" +
                "  kubectl get/describe/logs/rollout/scale/delete ...；高危写操作逐条确认。\r\n" +
                "  helm list/status/history/show/install/upgrade/rollback/uninstall ...\r\n" +
                "  istioctl version/analyze/proxy-config ...\r\n" +
                "  yq eval <表达式> <文件>；yq 可离线使用，禁止 -i/--inplace。\r\n" +
                "工具与模式:\r\n" +
                "  tools 查看路径/版本/错误/集群；detect 重新检测；install-help 安装指导。\r\n" +
                "  mode design 本地设计；mode online 在线刷新；refresh 刷新快照。\r\n" +
                "图形联动:\r\n" +
                "  list [Namespace]；relations；select <名称|Id|UID>；count。\r\n" +
                "  设计态: add <类型> <名称> [Namespace]；delete [名称]；connect <源> <目标> [关系]。\r\n" +
                "  rename <新名称>；set replicas <数量>；new；save <json>；load <json>。\r\n" +
                "  类型示例: namespace/deployment/pod/service/configmap/helmrelease/virtualservice。\r\n" +
                "视图: fit；zoom in|out|125；front/back；clear 清屏；执行中 Ctrl+C 取消。\r\n" +
                "在线画布只允许选择、查看和布局；设计态修改不会自动提交到集群。";
        }

        private sealed class PreparedExecution
        {
            internal ExternalCommand Command;
            internal bool RefreshAfter;
        }

        /// <summary>把真实资源网关的只读调用接入同一进程执行器和取消信号。</summary>
        private sealed class ResourceQueryExecutor : IResourceQueryExecutor
        {
            private readonly IProcessRunner _runner;
            private readonly ToolchainDetectionResult _tools;
            private readonly CommandCancellation _cancellation;

            internal ResourceQueryExecutor(IProcessRunner runner, ToolchainDetectionResult tools, CommandCancellation cancellation)
            {
                _runner = runner;
                _tools = tools;
                _cancellation = cancellation;
            }

            public string Execute(string tool, IList<string> arguments)
            {
                CommandPolicyDecision policy = CommandPolicy.Evaluate(tool, arguments);
                if (!policy.Allowed || policy.RequiresConfirmation)
                    throw new InvalidOperationException("内部只读资源查询未通过安全策略: " + policy.Reason);
                ToolDetectionResult status = tool == "helm" ? _tools.Helm : _tools.Kubectl;
                if (status == null || !status.IsAvailable)
                    throw new InvalidOperationException(tool + " 不可用，无法完成该项资源查询。");
                ExternalCommand command = new ExternalCommand(status.ExecutablePath, arguments);
                command.TimeoutMilliseconds = 45000;
                command.MaxOutputCharacters = 8388608;
                ExternalCommandResult result = _runner.Execute(command, _cancellation);
                if (!result.Success)
                {
                    string detail = string.IsNullOrEmpty(result.StandardError) ? result.ErrorMessage : result.StandardError.Trim();
                    throw new InvalidOperationException(tool + " 查询失败（退出码 " + result.ExitCode + "）: " + detail);
                }
                return result.StandardOutput;
            }
        }
    }
}
