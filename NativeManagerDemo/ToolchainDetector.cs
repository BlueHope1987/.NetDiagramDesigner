using System;
using System.Collections.Generic;
using System.IO;

namespace NativeManagerDemo
{
    /// <summary>单个命令行工具的独立检测状态。</summary>
    public sealed class ToolDetectionResult
    {
        private string _toolName;
        private string _executablePath = "";
        private bool _isInstalled;
        private bool _versionProbeSucceeded;
        private string _version = "";
        private string _errorMessage = "";
        private bool _clusterChecked;
        private bool? _clusterReachable;
        private string _clusterMessage = "";

        internal ToolDetectionResult(string toolName)
        {
            _toolName = toolName;
        }

        public string ToolName
        {
            get { return _toolName; }
            internal set { _toolName = value; }
        }

        public string ExecutablePath
        {
            get { return _executablePath; }
            internal set { _executablePath = value == null ? "" : value; }
        }

        /// <summary>只表示已定位到可执行文件，不要求版本或集群探测成功。</summary>
        public bool IsInstalled
        {
            get { return _isInstalled; }
            internal set { _isInstalled = value; }
        }

        public bool VersionProbeSucceeded
        {
            get { return _versionProbeSucceeded; }
            internal set { _versionProbeSucceeded = value; }
        }

        public bool IsAvailable
        {
            get { return _isInstalled && _versionProbeSucceeded; }
        }

        public string Version
        {
            get { return _version; }
            internal set { _version = value == null ? "" : value; }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            internal set { _errorMessage = value == null ? "" : value; }
        }

        /// <summary>仅 kubectl 会进行集群连通检查。</summary>
        public bool ClusterChecked
        {
            get { return _clusterChecked; }
            internal set { _clusterChecked = value; }
        }

        /// <summary>未检查时为空，否则表示集群是否可达。</summary>
        public bool? ClusterReachable
        {
            get { return _clusterReachable; }
            internal set { _clusterReachable = value; }
        }

        public string ClusterMessage
        {
            get { return _clusterMessage; }
            internal set { _clusterMessage = value == null ? "" : value; }
        }
    }

    /// <summary>四种工具的完整检测结果。</summary>
    public sealed class ToolchainDetectionResult
    {
        private readonly List<ToolDetectionResult> _tools;
        private readonly ToolDetectionResult _kubectl;
        private readonly ToolDetectionResult _helm;
        private readonly ToolDetectionResult _istioCtl;
        private readonly ToolDetectionResult _yq;

        internal ToolchainDetectionResult(ToolDetectionResult kubectl, ToolDetectionResult helm,
            ToolDetectionResult istioCtl, ToolDetectionResult yq)
        {
            _kubectl = kubectl;
            _helm = helm;
            _istioCtl = istioCtl;
            _yq = yq;
            _tools = new List<ToolDetectionResult>();
            _tools.Add(kubectl);
            _tools.Add(helm);
            _tools.Add(istioCtl);
            _tools.Add(yq);
        }

        public IList<ToolDetectionResult> Tools
        {
            get { return new List<ToolDetectionResult>(_tools); }
        }

        public ToolDetectionResult Kubectl
        {
            get { return _kubectl; }
        }

        public ToolDetectionResult Helm
        {
            get { return _helm; }
        }

        public ToolDetectionResult IstioCtl
        {
            get { return _istioCtl; }
        }

        public ToolDetectionResult Yq
        {
            get { return _yq; }
        }
    }

    /// <summary>分别定位并探测 kubectl、helm、istioctl 和 yq，不依赖任何 UI。</summary>
    public sealed class ToolchainDetector
    {
        private readonly IProcessRunner _processRunner;
        private int _versionTimeoutMilliseconds = 5000;
        private int _clusterTimeoutMilliseconds = 5000;

        public ToolchainDetector()
            : this(new ProcessRunner())
        {
        }

        public ToolchainDetector(IProcessRunner processRunner)
        {
            if (processRunner == null) throw new ArgumentNullException("processRunner");
            _processRunner = processRunner;
        }

        public int VersionTimeoutMilliseconds
        {
            get { return _versionTimeoutMilliseconds; }
            set { _versionTimeoutMilliseconds = value; }
        }

        public int ClusterTimeoutMilliseconds
        {
            get { return _clusterTimeoutMilliseconds; }
            set { _clusterTimeoutMilliseconds = value; }
        }

        public ToolchainDetectionResult DetectAll()
        {
            // Detect 自身返回错误状态，确保一个工具失败不会中断后续工具。
            return new ToolchainDetectionResult(Detect("kubectl"), Detect("helm"),
                Detect("istioctl"), Detect("yq"));
        }

        public ToolDetectionResult Detect(string toolName)
        {
            string normalized = NormalizeToolName(toolName);
            ToolDetectionResult status = new ToolDetectionResult(normalized);

            if (!IsSupportedTool(normalized))
            {
                status.ErrorMessage = "不支持检测工具: " + (toolName == null ? "(空)" : toolName);
                return status;
            }

            try
            {
                string executable = FindExecutable(normalized);
                if (string.IsNullOrEmpty(executable))
                {
                    status.ErrorMessage = "未在 PATH 或常见安装目录中找到 " + normalized + "。";
                    return status;
                }

                status.ExecutablePath = executable;
                status.IsInstalled = true;

                ExternalCommandResult versionResult = _processRunner.Execute(
                    CreateVersionCommand(normalized, executable));
                status.VersionProbeSucceeded = versionResult.Success;
                status.Version = FirstNonEmptyLine(versionResult.StandardOutput);
                if (!versionResult.Success)
                {
                    status.ErrorMessage = BuildFailureMessage("版本探测", versionResult);
                    return status;
                }

                if (normalized == "kubectl") CheckCluster(status, executable);
            }
            catch (Exception ex)
            {
                status.VersionProbeSucceeded = false;
                status.ErrorMessage = "检测 " + normalized + " 时发生异常: " + ex.Message;
            }

            return status;
        }

        /// <summary>按 PATH 和平台常见安装目录定位工具；未找到时返回 null。</summary>
        public static string FindExecutable(string toolName)
        {
            if (string.IsNullOrEmpty(toolName) || toolName.Trim().Length == 0) return null;
            toolName = toolName.Trim();

            bool windows = IsWindows();
            if (ContainsDirectorySeparator(toolName))
                return FindInDirectoryOrPath(toolName, windows);

            List<string> directories = new List<string>();
            string path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                string[] pathParts = path.Split(new char[] { Path.PathSeparator });
                for (int i = 0; i < pathParts.Length; i++) AddDirectory(directories, pathParts[i]);
            }
            AddCommonDirectories(directories, windows);

            for (int i = 0; i < directories.Count; i++)
            {
                string found = FindInDirectory(directories[i], toolName, windows);
                if (found != null) return found;
            }
            return null;
        }

        private void CheckCluster(ToolDetectionResult status, string executable)
        {
            status.ClusterChecked = true;
            try
            {
                ExternalCommand command = new ExternalCommand(executable);
                command.Arguments.Add("cluster-info");
                command.Arguments.Add("--request-timeout=5s");
                command.TimeoutMilliseconds = _clusterTimeoutMilliseconds;
                command.MaxOutputCharacters = 16384;

                ExternalCommandResult clusterResult = _processRunner.Execute(command);
                status.ClusterReachable = clusterResult.Success;
                status.ClusterMessage = clusterResult.Success
                    ? "集群连接正常。"
                    : BuildFailureMessage("集群连通检查", clusterResult);
            }
            catch (Exception ex)
            {
                status.ClusterReachable = false;
                status.ClusterMessage = "集群连通检查发生异常: " + ex.Message;
            }
        }

        private ExternalCommand CreateVersionCommand(string toolName, string executable)
        {
            ExternalCommand command = new ExternalCommand(executable);
            switch (toolName)
            {
                case "kubectl":
                    command.Arguments.Add("version");
                    command.Arguments.Add("--client");
                    break;
                case "helm":
                    command.Arguments.Add("version");
                    command.Arguments.Add("--short");
                    break;
                case "istioctl":
                    command.Arguments.Add("version");
                    command.Arguments.Add("--remote=false");
                    break;
                case "yq":
                    command.Arguments.Add("--version");
                    break;
            }
            command.TimeoutMilliseconds = _versionTimeoutMilliseconds;
            command.MaxOutputCharacters = 16384;
            return command;
        }

        private static string FindInDirectoryOrPath(string path, bool windows)
        {
            string directory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(directory)) directory = Environment.CurrentDirectory;
            return FindInDirectory(directory, fileName, windows);
        }

        private static string FindInDirectory(string directory, string toolName, bool windows)
        {
            if (string.IsNullOrEmpty(directory)) return null;
            directory = directory.Trim().Trim('"');
            if (directory.Length == 0) return null;

            try
            {
                string direct = Path.Combine(directory, toolName);
                if (File.Exists(direct)) return Path.GetFullPath(direct);
                if (windows && !direct.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    string executable = direct + ".exe";
                    if (File.Exists(executable)) return Path.GetFullPath(executable);
                }
            }
            catch
            {
                // PATH 中可能存在空项、无效路径或已无权限的目录，继续检查其他目录。
            }
            return null;
        }

        private static void AddCommonDirectories(List<string> directories, bool windows)
        {
            if (windows)
            {
                string programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
                string programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                string programData = Environment.GetEnvironmentVariable("ProgramData");
                string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                string userProfile = Environment.GetEnvironmentVariable("USERPROFILE");

                AddToolDirectories(directories, programFiles);
                AddToolDirectories(directories, programFilesX86);
                AddDirectory(directories, Combine(programData, "chocolatey\\bin"));
                AddDirectory(directories, Combine(userProfile, "scoop\\shims"));
                AddDirectory(directories, Combine(userProfile, "bin"));
                AddDirectory(directories, Combine(localAppData, "Microsoft\\WinGet\\Links"));
                AddDirectory(directories, "C:\\tools");
            }
            else
            {
                AddDirectory(directories, "/usr/local/bin");
                AddDirectory(directories, "/usr/bin");
                AddDirectory(directories, "/bin");
                AddDirectory(directories, "/opt/homebrew/bin");
                AddDirectory(directories, "/snap/bin");
                AddDirectory(directories, "/opt/kubernetes/bin");
                AddDirectory(directories, "/opt/istio/bin");
                string home = Environment.GetEnvironmentVariable("HOME");
                AddDirectory(directories, Combine(home, ".local/bin"));
                AddDirectory(directories, Combine(home, "bin"));
            }
        }

        private static void AddToolDirectories(List<string> directories, string root)
        {
            AddDirectory(directories, Combine(root, "Kubernetes"));
            AddDirectory(directories, Combine(root, "Kubernetes\\bin"));
            AddDirectory(directories, Combine(root, "Helm"));
            AddDirectory(directories, Combine(root, "Helm\\bin"));
            AddDirectory(directories, Combine(root, "Istio"));
            AddDirectory(directories, Combine(root, "Istio\\bin"));
            AddDirectory(directories, Combine(root, "yq"));
            AddDirectory(directories, Combine(root, "yq\\bin"));
        }

        private static string Combine(string left, string right)
        {
            if (string.IsNullOrEmpty(left)) return null;
            try { return Path.Combine(left, right); }
            catch { return null; }
        }

        private static void AddDirectory(List<string> directories, string directory)
        {
            if (string.IsNullOrEmpty(directory)) return;
            directory = directory.Trim().Trim('"');
            if (directory.Length == 0 || directories.Contains(directory)) return;
            directories.Add(directory);
        }

        private static string BuildFailureMessage(string operation, ExternalCommandResult result)
        {
            string detail = FirstNonEmptyLine(result.StandardError);
            if (detail.Length == 0) detail = FirstNonEmptyLine(result.ErrorMessage);
            if (detail.Length == 0 && result.ExitCode != -1)
                detail = "退出码 " + result.ExitCode;
            return operation + "失败" + (detail.Length == 0 ? "。" : ": " + detail);
        }

        private static string FirstNonEmptyLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            using (StringReader reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length > 0) return line;
                }
            }
            return "";
        }

        private static string NormalizeToolName(string toolName)
        {
            return string.IsNullOrEmpty(toolName) ? "" : toolName.Trim().ToLowerInvariant();
        }

        private static bool IsSupportedTool(string toolName)
        {
            return toolName == "kubectl" || toolName == "helm" || toolName == "istioctl" || toolName == "yq";
        }

        private static bool ContainsDirectorySeparator(string path)
        {
            return path.IndexOf('/') >= 0 || path.IndexOf('\\') >= 0;
        }

        private static bool IsWindows()
        {
            return Path.DirectorySeparatorChar == '\\';
        }
    }
}
