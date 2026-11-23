using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NativeManagerDemo
{
    public enum LogKind { Info, Ok, Error, Echo, Hint }

    /// <summary>命令输出与安全命令入口；不支持 shell 管道或交互终端。</summary>
    public class CommandConsoleControl : UserControl
    {
        private RichTextBox _output;
        private TextBox _input;
        private Panel _inputPanel;
        private Label _prompt;
        private Button _cancel;
        private readonly List<string> _history = new List<string>();
        private int _historyIndex;
        private CommandCoordinator _coordinator;
        private bool _busy;

        public event EventHandler CommandExecuted;
        public event EventHandler CancelRequested;

        public CommandConsoleControl()
        {
            _busy = false;
            BuildUi();
        }

        private void BuildUi()
        {
            this.BackColor = Color.FromArgb(30, 33, 40);
            _output = new RichTextBox();
            _output.Dock = DockStyle.Fill;
            _output.ReadOnly = true;
            _output.BackColor = Color.FromArgb(24, 27, 33);
            _output.ForeColor = Color.Gainsboro;
            _output.Font = new Font("Consolas", 9.5f);
            _output.BorderStyle = BorderStyle.None;
            _output.WordWrap = false;
            _output.ScrollBars = RichTextBoxScrollBars.Both;

            _inputPanel = new Panel();
            _inputPanel.Dock = DockStyle.Bottom;
            _inputPanel.Height = 36;
            _inputPanel.Padding = new Padding(8, 5, 8, 5);
            _inputPanel.BackColor = Color.FromArgb(43, 48, 59);
            _inputPanel.BorderStyle = BorderStyle.FixedSingle;

            _prompt = new Label();
            _prompt.Text = ">";
            _prompt.Font = new Font("Consolas", 10.0f);
            _prompt.ForeColor = Color.FromArgb(125, 211, 252);
            _prompt.BackColor = Color.FromArgb(31, 36, 46);
            _prompt.TextAlign = ContentAlignment.MiddleCenter;
            _prompt.Location = new Point(9, 6);
            _prompt.Size = new Size(25, 23);
            _prompt.Margin = Padding.Empty;

            _input = new TextBox();
            _input.BorderStyle = BorderStyle.FixedSingle;
            _input.BackColor = Color.FromArgb(31, 36, 46);
            _input.ForeColor = Color.White;
            _input.Font = new Font("Consolas", 10.0f);
            _input.Location = new Point(34, 6);
            _input.Height = _prompt.Height;
            _input.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _input.KeyDown += new KeyEventHandler(OnInputKeyDown);
            _input.Enter += new EventHandler(OnInputFocus);
            _input.Leave += new EventHandler(OnInputFocus);

            _cancel = new Button();
            _cancel.Text = "取消";
            _cancel.Font = new Font(this.Font.FontFamily, 8.0f);
            _cancel.Size = new Size(52, 24);
            _cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _cancel.FlatStyle = FlatStyle.Flat;
            _cancel.FlatAppearance.BorderColor = Color.FromArgb(100, 116, 139);
            _cancel.ForeColor = Color.Gainsboro;
            _cancel.BackColor = Color.FromArgb(51, 65, 85);
            _cancel.Visible = false;
            _cancel.Click += new EventHandler(OnCancelClick);

            _inputPanel.Controls.Add(_input);
            _inputPanel.Controls.Add(_prompt);
            _inputPanel.Controls.Add(_cancel);
            this.Controls.Add(_output);
            this.Controls.Add(_inputPanel);
            LayoutInput();
        }

        public void Attach(CommandCoordinator coordinator) { _coordinator = coordinator; }

        public void SetBusy(bool busy)
        {
            _busy = busy;
            _cancel.Visible = busy;
            _input.BackColor = busy ? Color.FromArgb(40, 44, 53) : Color.FromArgb(31, 36, 46);
            LayoutInput();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutInput();
        }

        private void LayoutInput()
        {
            if (_input == null) return;
            int right = _inputPanel.ClientSize.Width - 9;
            if (_cancel.Visible) right -= _cancel.Width + 7;
            _input.Width = Math.Max(80, right - _input.Left);
            _input.Height = _prompt.Height;
            _cancel.Location = new Point(_inputPanel.ClientSize.Width - _cancel.Width - 9, 5);
        }

        public void WriteLine(string text, LogKind kind)
        {
            if (text == null) return;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int i;
            for (i = 0; i < lines.Length; i++) Append(lines[i], ColorOf(kind));
        }

        public void WriteResult(CommandResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.Text)) return;
            WriteLine(result.Text, result.Success ? LogKind.Ok : LogKind.Error);
        }

        public void WriteBanner(string title, string subtitle)
        {
            Append("", ColorOf(LogKind.Info));
            Append("  " + title, Color.FromArgb(120, 200, 255));
            Append("  " + subtitle, ColorOf(LogKind.Hint));
            Append("", ColorOf(LogKind.Info));
        }

        public void ClearOutput() { _output.Clear(); }
        public void FocusInput() { _input.Focus(); }

        private static Color ColorOf(LogKind kind)
        {
            switch (kind)
            {
                case LogKind.Ok: return Color.FromArgb(126, 231, 160);
                case LogKind.Error: return Color.FromArgb(248, 113, 113);
                case LogKind.Echo: return Color.FromArgb(125, 211, 252);
                case LogKind.Hint: return Color.FromArgb(148, 163, 184);
                default: return Color.FromArgb(229, 231, 235);
            }
        }

        private void Append(string text, Color color)
        {
            _output.SelectionStart = _output.TextLength;
            _output.SelectionLength = 0;
            _output.SelectionColor = color;
            _output.AppendText(text + Environment.NewLine);
            _output.SelectionStart = _output.TextLength;
            _output.ScrollToCaret();
        }

        private void OnInputFocus(object sender, EventArgs e)
        {
            _inputPanel.BackColor = _input.Focused ? Color.FromArgb(51, 65, 85) : Color.FromArgb(43, 48, 59);
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            if (CancelRequested != null) CancelRequested(this, EventArgs.Empty);
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                Submit(_input.Text);
            }
            else if (e.KeyCode == Keys.Up)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                NavigateHistory(-1);
            }
            else if (e.KeyCode == Keys.Down)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                NavigateHistory(1);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                _input.Clear();
            }
            else if (e.Control && e.KeyCode == Keys.C && _busy)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                OnCancelClick(this, EventArgs.Empty);
            }
        }

        private void NavigateHistory(int direction)
        {
            if (_history.Count == 0) return;
            _historyIndex += direction;
            if (_historyIndex < 0) _historyIndex = 0;
            if (_historyIndex >= _history.Count)
            {
                _historyIndex = _history.Count;
                _input.Clear();
                return;
            }
            _input.Text = _history[_historyIndex];
            _input.SelectionStart = _input.TextLength;
        }

        private void Submit(string raw)
        {
            string command = raw == null ? "" : raw.Trim();
            WriteLine("> " + command, LogKind.Echo);
            _input.Clear();
            if (command.Length == 0) return;
            _history.Add(command);
            _historyIndex = _history.Count;
            if (command == "cls" || command == "clear")
            {
                ClearOutput();
                return;
            }
            if (_coordinator == null)
            {
                WriteLine("命令协调器未就绪", LogKind.Error);
                return;
            }
            CommandResult result = _coordinator.Execute(command);
            WriteResult(result);
            if (CommandExecuted != null) CommandExecuted(this, EventArgs.Empty);
            FocusInput();
        }
    }
}
