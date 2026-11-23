using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace NativeManagerDemo
{
    /// <summary>便于后台服务或无头测试替换的同步进程执行器。</summary>
    public interface IProcessRunner
    {
        ExternalCommandResult Execute(ExternalCommand command);
        ExternalCommandResult Execute(ExternalCommand command, CommandCancellation cancellation);
    }

    /// <summary>.NET 2.0 环境下供单次同步执行使用的取消信号。</summary>
    public sealed class CommandCancellation : IDisposable
    {
        private readonly ManualResetEvent _cancelled = new ManualResetEvent(false);

        public bool IsCancellationRequested
        {
            get { return _cancelled.WaitOne(0, false); }
        }

        public void Cancel()
        {
            _cancelled.Set();
        }

        public void Dispose()
        {
            _cancelled.Close();
        }
    }

    /// <summary>
    /// 直接启动固定可执行文件，不创建交互终端，也不经过 cmd.exe、powershell 或 sh。
    /// Execute 会阻塞当前调用线程，UI 应由调用方放到后台线程中执行。
    /// </summary>
    public sealed class ProcessRunner : IProcessRunner
    {
        public ExternalCommandResult Execute(ExternalCommand command)
        {
            return Execute(command, null);
        }

        public ExternalCommandResult Execute(ExternalCommand command, CommandCancellation cancellation)
        {
            if (command == null) throw new ArgumentNullException("command");

            ExternalCommandResult result = new ExternalCommandResult();
            result.CommandText = command.DisplayText;
            Stopwatch duration = Stopwatch.StartNew();

            if (cancellation != null && cancellation.IsCancellationRequested)
            {
                result.Cancelled = true;
                result.ErrorMessage = "执行已取消，进程未启动。";
                result.Duration = duration.Elapsed;
                return result;
            }

            Process process = null;
            OutputReader standardOutputReader = null;
            OutputReader standardErrorReader = null;
            bool outputCaptured = false;

            try
            {
                if (string.IsNullOrEmpty(command.Executable) || command.Executable.Trim().Length == 0)
                {
                    result.ErrorMessage = "未指定可执行文件。";
                    return result;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = command.Executable;
                startInfo.Arguments = EncodeArguments(command.Arguments);
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.ErrorDialog = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardInput = true;
                if (!string.IsNullOrEmpty(command.WorkingDirectory))
                    startInfo.WorkingDirectory = command.WorkingDirectory;

                process = new Process();
                process.StartInfo = startInfo;
                if (!process.Start())
                {
                    result.ErrorMessage = "进程启动失败。";
                    return result;
                }

                result.Started = true;

                // 向子进程提供 EOF，避免命令等待交互输入。
                process.StandardInput.Close();

                standardOutputReader = new OutputReader(process.StandardOutput, command.MaxOutputCharacters);
                standardErrorReader = new OutputReader(process.StandardError, command.MaxOutputCharacters);
                standardOutputReader.Start();
                standardErrorReader.Start();

                WaitForProcess(process, command.TimeoutMilliseconds, cancellation, result);
                CaptureOutput(process, result, standardOutputReader, standardErrorReader);
                outputCaptured = true;

                if (process.HasExited) result.ExitCode = process.ExitCode;
                result.Success = result.Started && !result.TimedOut && !result.Cancelled && result.ExitCode == 0;

                if (!result.TimedOut && !result.Cancelled)
                {
                    if (standardOutputReader.Error != null)
                        AppendError(result, "读取标准输出失败: " + standardOutputReader.Error.Message);
                    if (standardErrorReader.Error != null)
                        AppendError(result, "读取标准错误失败: " + standardErrorReader.Error.Message);
                    if (standardOutputReader.Error != null || standardErrorReader.Error != null)
                        result.Success = false;
                    else if (result.Started && result.ExitCode != 0)
                        result.ErrorMessage = "进程退出码为 " + result.ExitCode + "。";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                AppendError(result, ex.Message);

                if (process != null && result.Started)
                {
                    string killError = TryKill(process);
                    if (killError != null) AppendError(result, "终止进程失败: " + killError);
                }

                if (!outputCaptured && process != null && SafeHasExited(process))
                {
                    CaptureOutput(process, result, standardOutputReader, standardErrorReader);
                    outputCaptured = true;
                }
            }
            finally
            {
                if (!outputCaptured && process != null && SafeHasExited(process))
                    CaptureOutput(process, result, standardOutputReader, standardErrorReader);

                if (process != null) process.Dispose();
                result.Duration = duration.Elapsed;
            }

            return result;
        }

        /// <summary>
        /// .NET 2.0 尚无 ArgumentList，这里按 Windows 进程参数规则逐参数编码。
        /// 编码只用于进程 API 的参数字段，不会引入 shell。
        /// </summary>
        internal static string EncodeArguments(IList<string> arguments)
        {
            if (arguments == null || arguments.Count == 0) return "";
            StringBuilder encoded = new StringBuilder();
            for (int i = 0; i < arguments.Count; i++)
            {
                if (i > 0) encoded.Append(' ');
                EncodeArgument(encoded, arguments[i] == null ? "" : arguments[i]);
            }
            return encoded.ToString();
        }

        private static void EncodeArgument(StringBuilder encoded, string argument)
        {
            bool needsQuotes = argument.Length == 0;
            for (int i = 0; i < argument.Length; i++)
            {
                if (char.IsWhiteSpace(argument[i]) || argument[i] == '"' || argument[i] == '\\')
                {
                    needsQuotes = true;
                    break;
                }
            }

            if (!needsQuotes)
            {
                encoded.Append(argument);
                return;
            }

            encoded.Append('"');
            int backslashes = 0;
            for (int i = 0; i < argument.Length; i++)
            {
                char ch = argument[i];
                if (ch == '\\')
                {
                    backslashes++;
                }
                else if (ch == '"')
                {
                    encoded.Append('\\', backslashes * 2 + 1);
                    encoded.Append('"');
                    backslashes = 0;
                }
                else
                {
                    encoded.Append('\\', backslashes);
                    encoded.Append(ch);
                    backslashes = 0;
                }
            }
            encoded.Append('\\', backslashes * 2);
            encoded.Append('"');
        }

        private static void WaitForProcess(Process process, int timeoutMilliseconds,
            CommandCancellation cancellation, ExternalCommandResult result)
        {
            if (cancellation == null && timeoutMilliseconds <= 0)
            {
                process.WaitForExit();
                return;
            }

            Stopwatch wait = Stopwatch.StartNew();
            while (!process.WaitForExit(50))
            {
                if (cancellation != null && cancellation.IsCancellationRequested)
                {
                    result.Cancelled = true;
                    result.ErrorMessage = "命令已被取消。";
                    KillAndWait(process, result);
                    return;
                }

                if (timeoutMilliseconds > 0 && wait.ElapsedMilliseconds >= timeoutMilliseconds)
                {
                    result.TimedOut = true;
                    result.ErrorMessage = "命令执行超过 " + timeoutMilliseconds + " 毫秒，已请求终止。";
                    KillAndWait(process, result);
                    return;
                }
            }
        }

        private static void KillAndWait(Process process, ExternalCommandResult result)
        {
            string error = TryKill(process);
            if (error != null)
            {
                AppendError(result, "终止进程失败: " + error);
                return;
            }

            try { process.WaitForExit(); }
            catch (Exception ex) { AppendError(result, "等待进程退出失败: " + ex.Message); }
        }

        private static string TryKill(Process process)
        {
            try
            {
                if (!process.HasExited) process.Kill();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static bool SafeHasExited(Process process)
        {
            try { return process.HasExited; }
            catch { return false; }
        }

        private static void CaptureOutput(Process process, ExternalCommandResult result,
            OutputReader standardOutputReader, OutputReader standardErrorReader)
        {
            // 若终止失败且进程仍在运行，不等待管道 EOF，避免错误路径再次阻塞。
            if (!SafeHasExited(process)) return;

            if (standardOutputReader != null)
            {
                standardOutputReader.Join();
                result.StandardOutput = standardOutputReader.Text;
                result.StandardOutputTruncated = standardOutputReader.Truncated;
            }
            if (standardErrorReader != null)
            {
                standardErrorReader.Join();
                result.StandardError = standardErrorReader.Text;
                result.StandardErrorTruncated = standardErrorReader.Truncated;
            }
        }

        private static void AppendError(ExternalCommandResult result, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (result.ErrorMessage.Length == 0) result.ErrorMessage = message;
            else result.ErrorMessage = result.ErrorMessage + Environment.NewLine + message;
        }

        /// <summary>持续读取并排空单个管道，即使保留长度已经达到上限也不停止读取。</summary>
        private sealed class OutputReader
        {
            private readonly StreamReader _reader;
            private readonly int _limit;
            private readonly StringBuilder _text;
            private readonly Thread _thread;
            private bool _truncated;
            private Exception _error;

            public OutputReader(StreamReader reader, int limit)
            {
                _reader = reader;
                _limit = Math.Max(0, limit);
                _text = new StringBuilder();
                _thread = new Thread(new ThreadStart(ReadAll));
                _thread.IsBackground = true;
                _thread.Name = "外部命令输出读取";
            }

            public Exception Error
            {
                get { return _error; }
            }

            public string Text
            {
                get { return _text.ToString(); }
            }

            public bool Truncated
            {
                get { return _truncated; }
            }

            public void Start()
            {
                _thread.Start();
            }

            public void Join()
            {
                if (_thread.IsAlive) _thread.Join();
            }

            private void ReadAll()
            {
                try
                {
                    char[] buffer = new char[4096];
                    int count;
                    while ((count = _reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        int remaining = _limit - _text.Length;
                        int retained = Math.Min(count, Math.Max(0, remaining));
                        if (retained > 0) _text.Append(buffer, 0, retained);
                        if (retained < count) _truncated = true;
                    }
                }
                catch (Exception ex)
                {
                    _error = ex;
                }
            }
        }
    }
}
