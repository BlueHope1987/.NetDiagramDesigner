using System;
using System.Collections.Generic;
using System.Globalization;

namespace NativeManagerDemo
{
    public enum CommandPolicyVerdict
    {
        Allow = 0,
        Deny = 1,
        RequireConfirmation = 2
    }

    /// <summary>结构化的命令策略判断，不直接启动进程。</summary>
    public sealed class CommandPolicyDecision
    {
        private readonly CommandPolicyVerdict _verdict;
        private readonly string _toolName;
        private readonly string _verb;
        private readonly string _reason;

        internal CommandPolicyDecision(CommandPolicyVerdict verdict, string toolName, string verb, string reason)
        {
            _verdict = verdict;
            _toolName = toolName == null ? "" : toolName;
            _verb = verb == null ? "" : verb;
            _reason = reason == null ? "" : reason;
        }

        public CommandPolicyVerdict Verdict
        {
            get { return _verdict; }
        }

        /// <summary>拒绝时为 false；允许和需确认均为 true。</summary>
        public bool Allowed
        {
            get { return _verdict != CommandPolicyVerdict.Deny; }
        }

        public bool RequiresConfirmation
        {
            get { return _verdict == CommandPolicyVerdict.RequireConfirmation; }
        }

        public string ToolName
        {
            get { return _toolName; }
        }

        public string Verb
        {
            get { return _verb; }
        }

        public string Reason
        {
            get { return _reason; }
        }

        public override string ToString()
        {
            string prefix = _verdict == CommandPolicyVerdict.Allow ? "允许"
                : (_verdict == CommandPolicyVerdict.Deny ? "拒绝" : "需确认");
            return prefix + ": " + _reason;
        }
    }

    /// <summary>受支持工具及其子命令的静态白名单策略。</summary>
    public static class CommandPolicy
    {
        public static CommandPolicyDecision Evaluate(string commandLine)
        {
            List<string> tokens;
            string error;
            if (!CommandLineTokenizer.TryParse(commandLine, out tokens, out error))
                return Deny("", "", "命令解析失败: " + error);
            return Evaluate(tokens);
        }

        public static CommandPolicyDecision Evaluate(ExternalCommand command)
        {
            if (command == null) return Deny("", "", "命令对象不能为空。");
            return Evaluate(command.Executable, command.Arguments);
        }

        /// <summary>判断包含可执行名在内的完整参数列表。</summary>
        public static CommandPolicyDecision Evaluate(IList<string> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return Deny("", "", "命令不能为空。");

            List<string> arguments = new List<string>();
            for (int i = 1; i < tokens.Count; i++) arguments.Add(tokens[i]);
            return Evaluate(tokens[0], arguments);
        }

        /// <summary>判断固定可执行名和独立参数，适用于无头测试及程序化调用。</summary>
        public static CommandPolicyDecision Evaluate(string executable, IList<string> arguments)
        {
            string toolName = GetToolName(executable);
            string verb = arguments != null && arguments.Count > 0
                ? arguments[0].ToLowerInvariant() : "";

            string unsafeError;
            int unsafePosition;
            if (CommandLineTokenizer.TryGetUnsafeTextError(executable, out unsafeError, out unsafePosition))
                return Deny(toolName, verb, unsafeError);
            if (arguments != null)
            {
                for (int i = 0; i < arguments.Count; i++)
                {
                    if (CommandLineTokenizer.TryGetUnsafeTextError(arguments[i], out unsafeError, out unsafePosition))
                        return Deny(toolName, verb, "第 " + (i + 1) + " 个参数未通过安全检查: " + unsafeError);
                }
            }

            if (toolName.Length == 0) return Deny("", verb, "可执行文件不能为空。");
            if (arguments == null || arguments.Count == 0)
                return Deny(toolName, "", "缺少子命令。");

            switch (toolName)
            {
                case "kubectl": return EvaluateKubectl(arguments);
                case "helm": return EvaluateHelm(arguments);
                case "istioctl": return EvaluateIstioCtl(arguments);
                case "yq": return EvaluateYq(arguments);
                default: return Deny(toolName, verb, "仅允许 kubectl、helm、istioctl 和 yq。");
            }
        }

        private static CommandPolicyDecision EvaluateKubectl(IList<string> arguments)
        {
            string verb = arguments[0].ToLowerInvariant();

            if (verb == "edit")
                return Deny("kubectl", verb, "交互式 edit 不在安全白名单内。");
            if (verb == "port-forward" || verb == "proxy")
                return Deny("kubectl", verb, verb + " 会建立长驻连接，不在安全白名单内。");
            if (verb == "attach" || verb == "debug")
                return Deny("kubectl", verb, verb + " 可能进入交互或长驻会话，不在安全白名单内。");
            if (verb == "exec") return EvaluateKubectlExec(arguments);

            if (!IsOneOf(verb, "get", "describe", "logs", "rollout", "scale",
                "cordon", "uncordon", "drain", "delete"))
                return Deny("kubectl", verb, "不允许的 kubectl 子命令: " + verb + "。");

            string longRunningReason = GetLongRunningReason(verb, arguments);
            if (longRunningReason != null) return Deny("kubectl", verb, longRunningReason);

            if (verb == "rollout" && arguments.Count < 2)
                return Deny("kubectl", verb, "rollout 必须指定 status、history、restart 等具体操作。");
            if (verb == "scale") return EvaluateKubectlScale(arguments);

            if (verb == "rollout" && arguments.Count > 1 && arguments[1].ToLowerInvariant() == "restart")
                return Confirm("kubectl", verb, "rollout restart 会触发工作负载滚动重启，需要用户确认。");
            if (verb == "delete")
                return Confirm("kubectl", verb, "delete 会删除集群资源，需要用户确认。");
            if (verb == "drain")
                return Confirm("kubectl", verb, "drain 会驱逐节点上的工作负载，需要用户确认。");
            if (verb == "cordon")
                return Confirm("kubectl", verb, "cordon 会将节点标记为不可调度，需要用户确认。");

            return Allow("kubectl", verb, "属于已允许的 kubectl " + verb + " 操作。");
        }

        private static CommandPolicyDecision EvaluateKubectlExec(IList<string> arguments)
        {
            int separator = -1;
            for (int i = 1; i < arguments.Count; i++)
            {
                if (arguments[i] == "--")
                {
                    separator = i;
                    break;
                }
            }

            int commandIndex;
            if (separator >= 0)
            {
                if (separator < 2)
                    return Deny("kubectl", "exec", "exec 缺少目标 Pod。");
                commandIndex = separator + 1;
            }
            else
            {
                // 支持简单的 kubectl exec <Pod> <命令>；带 kubectl 选项时要求用 -- 消除歧义。
                if (arguments.Count < 3 || arguments[1].StartsWith("-", StringComparison.Ordinal))
                    return Deny("kubectl", "exec", "exec 请使用 kubectl exec <Pod> [选项] -- <命令> 的非交互形式。");
                commandIndex = 2;
            }

            if (commandIndex >= arguments.Count)
                return Deny("kubectl", "exec", "exec 必须显式指定要在容器内执行的命令。");

            int optionScanEnd = separator >= 0 ? commandIndex : arguments.Count;
            for (int i = 1; i < optionScanEnd; i++)
            {
                if (IsInteractiveExecOption(arguments[i]))
                    return Deny("kubectl", "exec", "禁止 exec 的 stdin/TTY 交互选项（如 -i、-t、-it、--stdin、--tty）。");
            }

            return Confirm("kubectl", "exec", "非交互 exec 仍会在容器内执行命令，需要用户确认。");
        }

        private static CommandPolicyDecision EvaluateKubectlScale(IList<string> arguments)
        {
            bool replicasFound = false;
            int replicas = 0;

            for (int i = 1; i < arguments.Count; i++)
            {
                string value = null;
                if (arguments[i] == "--replicas")
                {
                    if (i + 1 >= arguments.Count)
                        return Deny("kubectl", "scale", "--replicas 缺少整数值。");
                    value = arguments[++i];
                }
                else if (arguments[i].StartsWith("--replicas=", StringComparison.Ordinal))
                {
                    value = arguments[i].Substring("--replicas=".Length);
                }

                if (value == null) continue;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out replicas) || replicas < 0)
                    return Deny("kubectl", "scale", "--replicas 必须是大于或等于零的整数。");
                replicasFound = true;
            }

            if (!replicasFound)
                return Deny("kubectl", "scale", "scale 必须显式提供 --replicas，避免执行不明确的目标状态。");
            if (replicas == 0)
                return Confirm("kubectl", "scale", "将副本数缩放到 0 会停止工作负载，需要用户确认。");
            return Allow("kubectl", "scale", "副本数目标大于 0，属于已允许的 scale 操作。");
        }

        private static CommandPolicyDecision EvaluateHelm(IList<string> arguments)
        {
            string verb = arguments[0].ToLowerInvariant();
            if (!IsOneOf(verb, "list", "status", "history", "show", "install", "upgrade", "rollback", "uninstall"))
                return Deny("helm", verb, "不允许的 helm 子命令: " + verb + "。");

            if (IsOneOf(verb, "install", "upgrade", "rollback", "uninstall"))
                return Confirm("helm", verb, "helm " + verb + " 会变更集群中的发布，需要用户确认。");
            return Allow("helm", verb, "属于已允许的 Helm 只读操作。");
        }

        private static CommandPolicyDecision EvaluateIstioCtl(IList<string> arguments)
        {
            string verb = arguments[0].ToLowerInvariant();
            if (!IsOneOf(verb, "version", "analyze", "proxy-config"))
                return Deny("istioctl", verb, "不允许的 istioctl 子命令: " + verb + "。");
            return Allow("istioctl", verb, "属于已允许的 istioctl " + verb + " 操作。");
        }

        private static CommandPolicyDecision EvaluateYq(IList<string> arguments)
        {
            string verb = arguments[0].ToLowerInvariant();
            if (verb != "eval" && verb != "eval-all")
                return Deny("yq", verb, "仅允许 yq eval 和 eval-all。");

            for (int i = 1; i < arguments.Count; i++)
            {
                string argument = arguments[i];
                if (argument == "-i" || argument.StartsWith("-i=", StringComparison.Ordinal)
                    || argument == "--inplace" || argument.StartsWith("--inplace=", StringComparison.Ordinal))
                    return Deny("yq", verb, "禁止 yq 原地写入选项 -i/--inplace；请只输出转换结果。");
            }
            return Allow("yq", verb, "属于不原地修改文件的 yq 查询。");
        }

        private static string GetLongRunningReason(string verb, IList<string> arguments)
        {
            for (int i = 1; i < arguments.Count; i++)
            {
                string argument = arguments[i];
                if (verb == "logs" && IsEnabledOption(argument, "-f", "--follow"))
                    return "logs -f/--follow 会持续跟踪日志，已拒绝长驻命令。";
                if ((verb == "get" || verb == "rollout") && IsEnabledOption(argument, "-w", "--watch"))
                    return verb + " 的 watch 模式会持续运行，已拒绝长驻命令。";
                if (verb == "get" && IsEnabledLongOption(argument, "--watch-only"))
                    return "get --watch-only 会持续运行，已拒绝长驻命令。";
            }
            return null;
        }

        private static bool IsInteractiveExecOption(string argument)
        {
            if (argument == "-i" || argument == "-t" || argument == "-it" || argument == "-ti")
                return true;
            if (argument.StartsWith("-i=", StringComparison.Ordinal)
                || argument.StartsWith("-t=", StringComparison.Ordinal)
                || argument.StartsWith("-it=", StringComparison.Ordinal)
                || argument.StartsWith("-ti=", StringComparison.Ordinal))
                return true;
            if (IsEnabledLongOption(argument, "--stdin") || IsEnabledLongOption(argument, "--tty"))
                return true;
            return argument == "--interactive";
        }

        private static bool IsEnabledOption(string argument, string shortName, string longName)
        {
            if (argument == shortName || IsEnabledLongOption(argument, longName)) return true;
            if (argument.StartsWith(shortName + "=", StringComparison.Ordinal))
                return OptionValueIsEnabled(argument.Substring(shortName.Length + 1));
            return false;
        }

        private static bool IsEnabledLongOption(string argument, string optionName)
        {
            if (argument == optionName) return true;
            if (argument.StartsWith(optionName + "=", StringComparison.Ordinal))
                return OptionValueIsEnabled(argument.Substring(optionName.Length + 1));
            return false;
        }

        private static bool OptionValueIsEnabled(string value)
        {
            // 只有明确的 false/0/f 表示关闭；其他值一律按启用处理，避免无效布尔值绕过限制。
            return !(string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "f", StringComparison.OrdinalIgnoreCase)
                || value == "0");
        }

        private static string GetToolName(string executable)
        {
            if (string.IsNullOrEmpty(executable)) return "";
            string name = executable.Trim();
            int slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
            if (slash >= 0) name = name.Substring(slash + 1);
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            return name.ToLowerInvariant();
        }

        private static bool IsOneOf(string value, params string[] choices)
        {
            for (int i = 0; i < choices.Length; i++)
                if (value == choices[i]) return true;
            return false;
        }

        private static CommandPolicyDecision Allow(string toolName, string verb, string reason)
        {
            return new CommandPolicyDecision(CommandPolicyVerdict.Allow, toolName, verb, reason);
        }

        private static CommandPolicyDecision Deny(string toolName, string verb, string reason)
        {
            return new CommandPolicyDecision(CommandPolicyVerdict.Deny, toolName, verb, reason);
        }

        private static CommandPolicyDecision Confirm(string toolName, string verb, string reason)
        {
            return new CommandPolicyDecision(CommandPolicyVerdict.RequireConfirmation, toolName, verb, reason);
        }
    }
}
