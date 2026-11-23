using System;
using System.Collections.Generic;
using System.Text;

namespace NativeManagerDemo
{
    /// <summary>外部命令文本无法安全解析时抛出的异常。</summary>
    public class CommandLineParseException : FormatException
    {
        private readonly int _position;

        public CommandLineParseException(string message, int position)
            : base(message)
        {
            _position = position;
        }

        /// <summary>出错字符的从零开始索引；输入末尾错误时等于输入长度。</summary>
        public int Position
        {
            get { return _position; }
        }
    }

    /// <summary>
    /// 将单行命令文本解析为独立参数。解析结果只供 ProcessStartInfo 使用，不交给 shell。
    /// </summary>
    public static class CommandLineTokenizer
    {
        public static List<string> Parse(string commandLine)
        {
            List<string> arguments = new List<string>();
            if (string.IsNullOrEmpty(commandLine)) return arguments;

            string unsafeError;
            int unsafePosition;
            if (TryGetUnsafeTextError(commandLine, out unsafeError, out unsafePosition))
                throw new CommandLineParseException(unsafeError, unsafePosition);

            StringBuilder current = new StringBuilder();
            char quote = '\0';
            bool tokenStarted = false;

            for (int i = 0; i < commandLine.Length; i++)
            {
                char ch = commandLine[i];

                if (quote != '\0')
                {
                    if (ch == '\\')
                    {
                        if (i + 1 >= commandLine.Length)
                            throw new CommandLineParseException("反斜杠后缺少要转义的字符。", i);
                        current.Append(commandLine[++i]);
                        tokenStarted = true;
                    }
                    else if (ch == quote)
                    {
                        quote = '\0';
                        tokenStarted = true;
                    }
                    else
                    {
                        current.Append(ch);
                        tokenStarted = true;
                    }
                    continue;
                }

                if (ch == '\\')
                {
                    if (i + 1 >= commandLine.Length)
                        throw new CommandLineParseException("反斜杠后缺少要转义的字符。", i);
                    current.Append(commandLine[++i]);
                    tokenStarted = true;
                }
                else if (ch == '\'' || ch == '"')
                {
                    quote = ch;
                    tokenStarted = true;
                }
                else if (char.IsWhiteSpace(ch))
                {
                    if (tokenStarted)
                    {
                        arguments.Add(current.ToString());
                        current.Length = 0;
                        tokenStarted = false;
                    }
                }
                else
                {
                    current.Append(ch);
                    tokenStarted = true;
                }
            }

            if (quote != '\0')
                throw new CommandLineParseException("存在未闭合的" + (quote == '\'' ? "单引号。" : "双引号。"), commandLine.Length);

            if (tokenStarted) arguments.Add(current.ToString());
            return arguments;
        }

        /// <summary>Parse 的别名，便于调用方按“分词”语义使用。</summary>
        public static List<string> Tokenize(string commandLine)
        {
            return Parse(commandLine);
        }

        public static bool TryParse(string commandLine, out List<string> arguments, out string errorMessage)
        {
            try
            {
                arguments = Parse(commandLine);
                errorMessage = null;
                return true;
            }
            catch (CommandLineParseException ex)
            {
                arguments = new List<string>();
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>同时供结构化参数入口复用，防止绕过文本解析器。</summary>
        internal static bool TryGetUnsafeTextError(string text, out string errorMessage, out int position)
        {
            errorMessage = null;
            position = -1;
            if (text == null) return false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                position = i;

                if (ch == '\0')
                {
                    errorMessage = "命令或参数不能包含 NUL 字符。";
                    return true;
                }
                if (ch == '\r' || ch == '\n' || ch == '\u0085' || ch == '\u2028' || ch == '\u2029')
                {
                    errorMessage = "外部命令只能是单行文本，不能包含换行符。";
                    return true;
                }
                if (ch == ';' || ch == '|' || ch == '&' || ch == '>' || ch == '<' || ch == '`')
                {
                    errorMessage = "命令或参数包含已禁止的 shell 元字符 '" + ch + "'。";
                    return true;
                }
                if (ch == '$' && i + 1 < text.Length && text[i + 1] == '(')
                {
                    errorMessage = "命令或参数不能包含 shell 命令替换 $()。";
                    return true;
                }
            }

            position = -1;
            return false;
        }
    }
}
