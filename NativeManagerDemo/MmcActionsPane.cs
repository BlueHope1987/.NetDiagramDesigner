using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    public class VerbInvokedEventArgs : EventArgs
    {
        public ManagedEntity Entity;
        public VerbDescriptor Verb;
        public string CommandText;
        public int Replicas;

        public VerbInvokedEventArgs() { Replicas = -1; }
    }

    /// <summary>MMC 风格操作窗格；只收集意图，不自行确认或启动进程。</summary>
    public class MmcActionsPane : UserControl
    {
        private TopologyProjector _projector;
        private ManagedEntity _current;
        private Panel _header;
        private Label _nameLabel;
        private Label _kindLabel;
        private Label _nsLabel;
        private Label _sourceLabel;
        private Label _statusBadge;
        private Panel _verbArea;
        private readonly ToolTip _toolTip;
        private ManagerMode _mode;

        public event EventHandler<VerbInvokedEventArgs> VerbInvoked;

        public MmcActionsPane()
        {
            _toolTip = new ToolTip();
            _mode = ManagerMode.Checking;
            BuildUi();
        }

        public ManagerMode Mode
        {
            get { return _mode; }
            set { _mode = value; RefreshFromSelection(); }
        }

        public void Attach(TopologyProjector projector) { _projector = projector; }

        private void BuildUi()
        {
            this.BackColor = Color.FromArgb(245, 248, 252);
            _header = new Panel();
            _header.Dock = DockStyle.Top;
            _header.Height = 142;
            _header.BackColor = Color.FromArgb(230, 238, 248);
            _header.Padding = new Padding(10, 8, 10, 8);

            Label title = new Label();
            title.Text = "对象操作（与命令行同一策略）";
            title.Dock = DockStyle.Top;
            title.Height = 24;
            title.Font = new Font(this.Font, FontStyle.Bold);
            _header.Controls.Add(title);

            _nameLabel = MakeLabel(22, true);
            _nameLabel.Text = "(未选择)";
            _header.Controls.Add(_nameLabel);
            _kindLabel = MakeLabel(19, false);
            _header.Controls.Add(_kindLabel);
            _nsLabel = MakeLabel(19, false);
            _header.Controls.Add(_nsLabel);
            _sourceLabel = MakeLabel(19, false);
            _header.Controls.Add(_sourceLabel);
            _statusBadge = MakeLabel(24, true);
            _statusBadge.TextAlign = ContentAlignment.MiddleCenter;
            _header.Controls.Add(_statusBadge);

            _verbArea = new Panel();
            _verbArea.Dock = DockStyle.Fill;
            _verbArea.AutoScroll = true;
            _verbArea.BackColor = this.BackColor;
            this.Controls.Add(_verbArea);
            this.Controls.Add(_header);
        }

        private Label MakeLabel(int height, bool bold)
        {
            Label label = new Label();
            label.Dock = DockStyle.Top;
            label.Height = height;
            label.ForeColor = Color.FromArgb(75, 85, 99);
            if (bold) label.Font = new Font(this.Font, FontStyle.Bold);
            return label;
        }

        public void RefreshFromSelection()
        {
            ManagedEntity primary = _projector == null ? null : _projector.GetPrimarySelected();
            _current = primary;
            RenderHeader(primary);
            RenderVerbs(primary);
        }

        private void RenderHeader(ManagedEntity entity)
        {
            if (entity == null)
            {
                _nameLabel.Text = "(未选择对象)";
                _kindLabel.Text = "从画布选择资源，或输入 select <名称>";
                _nsLabel.Text = "";
                _sourceLabel.Text = "";
                _statusBadge.Text = "";
                _statusBadge.BackColor = Color.Transparent;
                return;
            }
            _nameLabel.Text = entity.Name;
            _kindLabel.Text = "类型: " + entity.Kind + " / " + VerbMatrix.KindName(entity.Kind);
            _nsLabel.Text = "命名空间: " + (string.IsNullOrEmpty(entity.Namespace) ? "（集群级）" : entity.Namespace);
            string id = string.IsNullOrEmpty(entity.Uid) ? entity.Id : entity.Uid;
            _sourceLabel.Text = (_mode == ManagerMode.Online ? "在线资源 · " : "本地设计 · ") + ShortId(id);
            _statusBadge.Text = "状态: " + ResourceVisualPalette.StatusText(entity.Status);
            _statusBadge.BackColor = ResourceVisualPalette.FillFor(entity.Kind);
            _statusBadge.ForeColor = ResourceVisualPalette.BorderFor(entity.Status);
        }

        private void RenderVerbs(ManagedEntity entity)
        {
            _verbArea.Controls.Clear();
            if (entity == null) return;
            IList<VerbDescriptor> verbs = VerbMatrix.GetVerbs(entity.Kind, entity.Status);
            List<string> order = new List<string>();
            order.Add(VerbGroup.Common);
            order.Add(VerbGroup.Workload);
            order.Add(VerbGroup.Network);
            order.Add(VerbGroup.Storage);
            order.Add(VerbGroup.Config);
            order.Add(VerbGroup.Helm);
            order.Add(VerbGroup.Danger);
            Dictionary<string, List<VerbDescriptor>> groups = new Dictionary<string, List<VerbDescriptor>>();
            int i;
            for (i = 0; i < verbs.Count; i++)
            {
                List<VerbDescriptor> list;
                if (!groups.TryGetValue(verbs[i].Group, out list))
                {
                    list = new List<VerbDescriptor>();
                    groups[verbs[i].Group] = list;
                }
                list.Add(verbs[i]);
            }
            int y = 8;
            for (i = 0; i < order.Count; i++)
            {
                List<VerbDescriptor> list;
                if (!groups.TryGetValue(order[i], out list)) continue;
                Label group = new Label();
                group.Text = order[i];
                group.Font = new Font(this.Font, FontStyle.Bold);
                group.ForeColor = Color.FromArgb(74, 111, 165);
                group.Location = new Point(10, y);
                group.AutoSize = true;
                _verbArea.Controls.Add(group);
                y += 24;
                int j;
                for (j = 0; j < list.Count; j++)
                {
                    VerbDescriptor verb = list[j];
                    Button button = new Button();
                    button.Text = verb.Name + (verb.Risk == RiskLevel.High ? "  [需确认]" : "");
                    button.Tag = verb;
                    button.Location = new Point(16, y);
                    button.Size = new Size(Math.Max(160, this.ClientSize.Width - 36), 32);
                    button.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    button.FlatStyle = FlatStyle.Flat;
                    string reason = DisabledReason(verb);
                    button.Enabled = reason.Length == 0;
                    _toolTip.SetToolTip(button, button.Enabled ? verb.Description : reason);
                    if (verb.Risk == RiskLevel.High) button.ForeColor = Color.FromArgb(183, 28, 28);
                    button.Click += new EventHandler(OnVerbClick);
                    _verbArea.Controls.Add(button);
                    y += 37;
                }
                y += 9;
            }
        }

        private string DisabledReason(VerbDescriptor verb)
        {
            if (!verb.Enabled) return string.IsNullOrEmpty(verb.DisabledReason) ? "当前状态不支持该操作" : verb.DisabledReason;
            if (_mode == ManagerMode.Checking) return "正在检查命令行工具，请稍候";
            if (_mode == ManagerMode.DesignDebug && verb.RequiresOnline && !verb.SupportsDesign)
                return "该操作需要在线集群；设计模式仅支持本地新增、删除、换父、连线和副本设计";
            if (_mode == ManagerMode.Online && !verb.RequiresOnline && !verb.SupportsDesign) return "该操作仅在设计模式可用";
            return "";
        }

        private void OnVerbClick(object sender, EventArgs e)
        {
            if (_current == null) return;
            Button button = sender as Button;
            if (button == null) return;
            VerbDescriptor verb = button.Tag as VerbDescriptor;
            if (verb == null || DisabledReason(verb).Length > 0) return;
            int replicas = -1;
            if (verb.Verb == "scale")
            {
                int initial = _current.Replicas >= 0 ? _current.Replicas : 1;
                if (!PromptReplicas(initial, out replicas)) return;
            }
            string command = VerbMatrix.RenderCommand(verb, _current);
            if (replicas >= 0) command = command.Replace("{replicas}", replicas.ToString());
            VerbInvokedEventArgs args = new VerbInvokedEventArgs();
            args.Entity = _current;
            args.Verb = verb;
            args.CommandText = command;
            args.Replicas = replicas;
            if (VerbInvoked != null) VerbInvoked(this, args);
        }

        private static bool PromptReplicas(int initial, out int replicas)
        {
            replicas = initial;
            using (Form form = new Form())
            {
                form.Text = "伸缩副本";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(320, 116);
                Label label = new Label();
                label.Text = "新的副本数（0 会停止全部副本并需要确认）";
                label.SetBounds(12, 12, 296, 24);
                form.Controls.Add(label);
                NumericUpDown number = new NumericUpDown();
                number.Minimum = 0;
                number.Maximum = 10000;
                number.Value = initial;
                number.SetBounds(12, 42, 130, 26);
                form.Controls.Add(number);
                Button ok = new Button();
                ok.Text = "确定";
                ok.DialogResult = DialogResult.OK;
                ok.SetBounds(152, 76, 72, 28);
                form.Controls.Add(ok);
                Button cancel = new Button();
                cancel.Text = "取消";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(236, 76, 72, 28);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                if (form.ShowDialog() != DialogResult.OK) return false;
                replicas = (int)number.Value;
                return true;
            }
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            return id.Length <= 18 ? id : id.Substring(0, 18) + "…";
        }
    }
}
