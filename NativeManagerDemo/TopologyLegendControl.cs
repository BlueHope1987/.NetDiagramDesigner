using System.Drawing;
using System.Windows.Forms;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    /// <summary>位于右侧操作窗格下方的紧凑图例，解释填充色、边框色与容器语义。</summary>
    public class TopologyLegendControl : UserControl
    {
        public TopologyLegendControl()
        {
            this.BackColor = Color.FromArgb(249, 250, 252);
            this.ForeColor = Color.FromArgb(50, 61, 72);
            this.MinimumSize = new Size(260, 160);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            int width = this.ClientSize.Width;
            int columnWidth = width / 3;
            if (columnWidth < 80) columnWidth = 80;

            using (Font title = new Font("Microsoft YaHei", 9f, FontStyle.Bold))
            using (Font text = new Font("Microsoft YaHei", 8f, FontStyle.Regular))
            using (Font note = new Font("Microsoft YaHei", 7.5f, FontStyle.Regular))
            using (Brush textBrush = new SolidBrush(this.ForeColor))
            using (Brush noteBrush = new SolidBrush(Color.FromArgb(102, 113, 124)))
            using (Pen separator = new Pen(Color.FromArgb(222, 226, 231)))
            {
                g.DrawString("图例｜填充色：资源类型（同族相近色）", title, textBrush,
                    new RectangleF(9, 5, width - 18, 20));
                DrawType(g, text, textBrush, 8, 30, columnWidth, "K8s 核心", EntityKind.Cluster);
                DrawType(g, text, textBrush, 8 + columnWidth, 30, columnWidth, "工作负载", EntityKind.Deployment);
                DrawType(g, text, textBrush, 8 + columnWidth * 2, 30, columnWidth, "网络/配置", EntityKind.Service);
                DrawType(g, text, textBrush, 8, 56, columnWidth, "Helm", EntityKind.HelmRelease);
                DrawType(g, text, textBrush, 8 + columnWidth, 56, columnWidth, "Istio", EntityKind.IstioGateway);
                DrawType(g, text, textBrush, 8 + columnWidth * 2, 56, columnWidth, "YAML/设计", EntityKind.YamlDocument);

                g.DrawLine(separator, 8, 82, width - 8, 82);
                g.DrawString("边框色：运行状态", title, textBrush, new RectangleF(9, 87, width - 18, 20));
                DrawStatus(g, text, textBrush, 8, 111, columnWidth, RunStatus.Running);
                DrawStatus(g, text, textBrush, 8 + columnWidth, 111, columnWidth, RunStatus.Pending);
                DrawStatus(g, text, textBrush, 8 + columnWidth * 2, 111, columnWidth, RunStatus.Warning);
                DrawStatus(g, text, textBrush, 8, 137, columnWidth, RunStatus.Failed);
                DrawStatus(g, text, textBrush, 8 + columnWidth, 137, columnWidth, RunStatus.Stopped);
                DrawStatus(g, text, textBrush, 8 + columnWidth * 2, 137, columnWidth, RunStatus.Unknown);
                g.DrawString("Namespace 是唯一容器；连线表示拥有、选择、路由或管理关系。",
                    note, noteBrush, new RectangleF(9, 159, width - 18, 17));
            }
        }

        private static void DrawType(Graphics g, Font font, Brush textBrush, int x, int y,
            int width, string label, EntityKind kind)
        {
            using (Brush fill = new SolidBrush(ResourceVisualPalette.FillFor(kind)))
            using (Pen border = new Pen(Color.FromArgb(133, 143, 153)))
            {
                g.FillRectangle(fill, x, y + 2, 17, 13);
                g.DrawRectangle(border, x, y + 2, 17, 13);
            }
            g.DrawString(label, font, textBrush, new RectangleF(x + 22, y, width - 24, 20));
        }

        private static void DrawStatus(Graphics g, Font font, Brush textBrush, int x, int y,
            int width, RunStatus status)
        {
            using (Brush fill = new SolidBrush(Color.White))
            using (Pen border = new Pen(ResourceVisualPalette.BorderFor(status), 2f))
            {
                g.FillRectangle(fill, x, y + 2, 17, 13);
                g.DrawRectangle(border, x, y + 2, 17, 13);
            }
            g.DrawString(ResourceVisualPalette.StatusText(status), font, textBrush,
                new RectangleF(x + 22, y, width - 24, 20));
        }
    }
}
