using System;
using System.Collections.Generic;
using System.Text;

namespace NativeManagerDemo
{
    /// <summary>一个不使用 shell 的外部进程调用及其执行选项。</summary>
    public sealed class ExternalCommand
    {
        private string _executable;
        private readonly List<string> _arguments;
        private string _displayText;
        private string _workingDirectory;
        private int _timeoutMilliseconds;
        private int _maxOutputCharacters;

        public ExternalCommand()
        {
            _arguments = new List<string>();
            _executable = "";
            _workingDirectory = "";
            _timeoutMilliseconds = 30000;
            _maxOutputCharacters = 65536;
        }

        public ExternalCommand(string executable)
            : this()
        {
            _executable = executable;
        }

        public ExternalCommand(string executable, IList<string> arguments)
            : this(executable)
        {
            if (arguments == null) return;
            for (int i = 0; i < arguments.Count; i++)
                _arguments.Add(arguments[i] == null ? "" : arguments[i]);
        }

        /// <summary>固定的可执行文件名或完整路径，不接受 shell 命令文本。</summary>
        public string Executable
        {
            get { return _executable; }
            set { _executable = value; }
        }

        /// <summary>逐个传递给可执行文件的参数，调用前可修改此列表。</summary>
        public IList<string> Arguments
        {
            get { return _arguments; }
        }

        /// <summary>用于界面回显和审计的文本；未显式设置时根据参数生成。</summary>
        public string DisplayText
        {
            get
            {
                if (_displayText != null) return _displayText;
                return BuildDisplayText(_executable, _arguments);
            }
            set { _displayText = value; }
        }

        public string WorkingDirectory
        {
            get { return _workingDirectory; }
            set { _workingDirectory = value; }
        }

        /// <summary>执行超时；小于等于零表示不限时。</summary>
        public int TimeoutMilliseconds
        {
            get { return _timeoutMilliseconds; }
            set { _timeoutMilliseconds = value; }
        }

        /// <summary>stdout 和 stderr 各自保留的最大字符数；零表示只排空、不保留。</summary>
        public int MaxOutputCharacters
        {
            get { return _maxOutputCharacters; }
            set { _maxOutputCharacters = value; }
        }

        public static ExternalCommand FromCommandLine(string commandLine)
        {
            List<string> tokens = CommandLineTokenizer.Parse(commandLine);
            if (tokens.Count == 0)
                throw new CommandLineParseException("命令不能为空。", 0);

            ExternalCommand command = new ExternalCommand(tokens[0]);
            for (int i = 1; i < tokens.Count; i++) command.Arguments.Add(tokens[i]);
            command.DisplayText = commandLine;
            return command;
        }

        public override string ToString()
        {
            return DisplayText;
        }

        private static string BuildDisplayText(string executable, IList<string> arguments)
        {
            StringBuilder text = new StringBuilder();
            AppendDisplayArgument(text, executable);
            if (arguments != null)
            {
                for (int i = 0; i < arguments.Count; i++)
                {
                    text.Append(' ');
                    AppendDisplayArgument(text, arguments[i]);
                }
            }
            return text.ToString();
        }

        private static void AppendDisplayArgument(StringBuilder text, string argument)
        {
            if (argument == null) argument = "";
            bool quote = argument.Length == 0;
            for (int i = 0; i < argument.Length; i++)
            {
                if (char.IsWhiteSpace(argument[i]) || argument[i] == '"')
                {
                    quote = true;
                    break;
                }
            }

            if (!quote)
            {
                text.Append(argument);
                return;
            }

            text.Append('"');
            for (int i = 0; i < argument.Length; i++)
            {
                if (argument[i] == '"' || argument[i] == '\\') text.Append('\\');
                text.Append(argument[i]);
            }
            text.Append('"');
        }
    }

    /// <summary>外部进程执行结果；启动失败、超时和取消也通过此对象返回。</summary>
    public sealed class ExternalCommandResult
    {
        private string _commandText = "";
        private bool _started;
        private int _exitCode = -1;
        private string _standardOutput = "";
        private string _standardError = "";
        private bool _standardOutputTruncated;
        private bool _standardErrorTruncated;
        private bool _timedOut;
        private bool _cancelled;
        private bool _success;
        private string _errorMessage = "";
        private TimeSpan _duration;

        public string CommandText
        {
            get { return _commandText; }
            set { _commandText = value == null ? "" : value; }
        }

        public bool Started
        {
            get { return _started; }
            set { _started = value; }
        }

        /// <summary>进程未启动或未能取得退出码时为 -1。</summary>
        public int ExitCode
        {
            get { return _exitCode; }
            set { _exitCode = value; }
        }

        public string StandardOutput
        {
            get { return _standardOutput; }
            set { _standardOutput = value == null ? "" : value; }
        }

        public string StandardError
        {
            get { return _standardError; }
            set { _standardError = value == null ? "" : value; }
        }

        public bool StandardOutputTruncated
        {
            get { return _standardOutputTruncated; }
            set { _standardOutputTruncated = value; }
        }

        public bool StandardErrorTruncated
        {
            get { return _standardErrorTruncated; }
            set { _standardErrorTruncated = value; }
        }

        public bool OutputTruncated
        {
            get { return _standardOutputTruncated || _standardErrorTruncated; }
        }

        public bool TimedOut
        {
            get { return _timedOut; }
            set { _timedOut = value; }
        }

        public bool Cancelled
        {
            get { return _cancelled; }
            set { _cancelled = value; }
        }

        public bool Success
        {
            get { return _success; }
            set { _success = value; }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { _errorMessage = value == null ? "" : value; }
        }

        public TimeSpan Duration
        {
            get { return _duration; }
            set { _duration = value; }
        }
    }
}
