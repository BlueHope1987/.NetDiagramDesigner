using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using DiagramDesigner.Config;
using DiagramDesigner.Controls;
using DiagramDesigner.Core;
using DiagramDesigner.Serialization;
using DiagramDesigner.Shapes;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    /// <summary>
    /// ITopologyHost 的编辑器实现。
    /// 把 DiagramEditor 的公开 API 映射为宿主可用的操作集，
    /// 全程只使用控件的公开契约（Document / Canvas / UpdateShapeProperty / 序列化器），
    /// 不触碰其任何内部实现。
    /// </summary>
    public class EditorTopologyHost : ITopologyHost
    {
        private readonly DiagramEditor _editor;

        public EditorTopologyHost(DiagramEditor editor)
        {
            if (editor == null) throw new ArgumentNullException("editor");
            _editor = editor;
        }

        public DiagramEditor Editor { get { return _editor; } }

        private DrawingDocument Doc { get { return _editor.GetDocument(); } }

        // ===== 查询 =====

        public IList<ShapeInfo> ListShapes()
        {
            List<ShapeInfo> list = new List<ShapeInfo>();
            foreach (ShapeBase s in Doc.Shapes) list.Add(ToInfo(s));
            return list;
        }

        public IList<ShapeInfo> ListSelection()
        {
            List<ShapeInfo> list = new List<ShapeInfo>();
            foreach (ShapeBase s in Doc.Shapes)
                if (s.Selected) list.Add(ToInfo(s));
            return list;
        }

        public int ShapeCount { get { return Doc.Shapes.Count; } }
        public int ConnectionCount { get { return Doc.Connections.Count; } }

        public IList<string> ListTypeNames()
        {
            List<string> names = new List<string>();
            foreach (ShapeType t in ShapeTypeRegistry.Instance.GetAllTypes())
                names.Add(t.Name);
            return names;
        }

        public string DescribeConnections()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Connection c in Doc.Connections)
            {
                string from = c.FromShape != null ? c.FromShape.Name : "?";
                string to = c.ToShape != null ? c.ToShape.Name : "?";
                sb.AppendLine(string.Format("  {0} -> {1}{2}", from, to,
                    string.IsNullOrEmpty(c.Label) ? "" : "  [" + c.Label + "]"));
            }
            if (sb.Length == 0) return "(无连线)";
            return sb.ToString();
        }

        // ===== 图形 =====

        public ShapeInfo AddShape(string typeName, string name, float x, float y)
        {
            ShapeType type = ShapeTypeRegistry.Instance.GetShapeType(typeName);
            if (type == null) return null;

            ShapeBase shape = type.CreateInstance();
            if (!string.IsNullOrEmpty(name)) shape.Name = name;
            shape.Description = type.Name;
            ContainerShape container = shape as ContainerShape;
            if (container != null) container.HeaderText = shape.Name;
            shape.X = x;
            shape.Y = y;
            Doc.AddShape(shape);
            Doc.Reindex();
            _editor.Canvas.Invalidate();
            return ToInfo(shape);
        }

        public int RemoveSelection()
        {
            int n = 0;
            foreach (ShapeBase s in Doc.Shapes)
                if (s.Selected) n++;
            _editor.DeleteSelected();
            return n;
        }

        public void SelectAll()
        {
            foreach (ShapeBase s in Doc.Shapes) s.Selected = true;
            _editor.Canvas.Invalidate();
            _editor.Canvas.OnSelectionChanged();
        }

        public void ClearSelection()
        {
            foreach (ShapeBase s in Doc.Shapes) s.Selected = false;
            _editor.Canvas.Invalidate();
            _editor.Canvas.OnSelectionChanged();
        }

        public bool SelectByName(string name)
        {
            bool found = false;
            foreach (ShapeBase s in Doc.Shapes)
            {
                bool match = string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase);
                s.Selected = match;
                if (match) found = true;
            }
            _editor.Canvas.Invalidate();
            _editor.Canvas.OnSelectionChanged();
            return found;
        }

        public bool SelectById(Guid id)
        {
            bool found = false;
            foreach (ShapeBase s in Doc.Shapes)
            {
                s.Selected = s.Id == id;
                if (s.Selected) found = true;
            }
            _editor.Canvas.Invalidate();
            _editor.Canvas.OnSelectionChanged();
            return found;
        }

        public bool RenameSelection(string newName)
        {
            bool any = false;
            foreach (ShapeBase s in Doc.Shapes)
            {
                if (!s.Selected) continue;
                _editor.UpdateShapeProperty(s.Id, "Name", newName);
                any = true;
            }
            _editor.Canvas.Invalidate();
            return any;
        }

        public bool SetProperty(string prop, string value)
        {
            bool any = false;
            List<ShapeBase> targets = new List<ShapeBase>();
            foreach (ShapeBase s in Doc.Shapes)
                if (s.Selected) targets.Add(s);

            foreach (ShapeBase s in targets)
            {
                any = true;
                SetShapeProperty(s.Id, prop, value);
            }
            return any;
        }

        public bool SetShapeProperty(Guid id, string prop, string value)
        {
            ShapeBase s = FindShape(id);
            if (s == null) return false;

            string p = CanonicalProp(prop);
            if (p == null) return false;
            if (p == "State") return SetShapeState(id, value);

            object typed;
            if (!TryCoerce(p, value, out typed)) return false;

            bool ok = _editor.UpdateShapeProperty(id, p, typed);
            if (p == "X" || p == "Y" || p == "Width" || p == "Height")
                Doc.Reindex();
            _editor.Canvas.Invalidate();
            return ok;
        }

        public bool SetShapeState(Guid id, string stateName)
        {
            GenericShape gs = FindShape(id) as GenericShape;
            if (gs == null) return false;

            bool has = false;
            foreach (ShapeState st in gs.States)
                if (string.Equals(st.Name, stateName, StringComparison.OrdinalIgnoreCase)) { has = true; break; }
            if (!has) return false;

            gs.CurrentStateName = stateName;
            _editor.Canvas.Invalidate();
            return true;
        }

        public bool ConfigureStates(Guid id, string[] stateNames, string initial)
        {
            GenericShape gs = FindShape(id) as GenericShape;
            if (gs == null || stateNames == null) return false;

            gs.States.Clear();
            for (int i = 0; i < stateNames.Length; i++)
            {
                ShapeState st = new ShapeState();
                st.Name = stateNames[i];
                Color fill, border, text;
                StatusColorsFor(stateNames[i], out fill, out border, out text);
                st.FillColor = new XmlColor(fill);
                st.BorderColor = new XmlColor(border);
                st.TextColor = new XmlColor(text);
                gs.States.Add(st);
            }
            if (!string.IsNullOrEmpty(initial)) gs.CurrentStateName = initial;
            _editor.Canvas.Invalidate();
            return true;
        }

        public bool ConfigureResourceStates(Guid id, EntityKind kind, string initial)
        {
            ShapeBase shape = FindShape(id);
            if (shape == null) return false;
            Color fill = ResourceVisualPalette.FillFor(kind);
            Color text = ResourceVisualPalette.TextFor(kind);
            string[] names = new string[] { "Unknown", "Pending", "Running", "Stopped", "Warning", "Failed" };
            GenericShape generic = shape as GenericShape;
            if (generic != null)
            {
                generic.States.Clear();
                int i;
                for (i = 0; i < names.Length; i++)
                {
                    ShapeState state = new ShapeState();
                    state.Name = names[i];
                    state.FillColor = new XmlColor(fill);
                    state.BorderColor = new XmlColor(ResourceVisualPalette.BorderFor((RunStatus)Enum.Parse(typeof(RunStatus), names[i])));
                    state.TextColor = new XmlColor(text);
                    generic.States.Add(state);
                }
                generic.CurrentStateName = string.IsNullOrEmpty(initial) ? "Unknown" : initial;
            }
            ContainerShape container = shape as ContainerShape;
            if (container != null)
            {
                RunStatus status = RunStatus.Unknown;
                try { status = (RunStatus)Enum.Parse(typeof(RunStatus), initial); }
                catch { }
                container.FillColor = fill;
                container.BorderColor = ResourceVisualPalette.BorderFor(status);
                container.HeaderColor = ResourceVisualPalette.HeaderFor(kind);
                container.TextColor = text;
                container.HeaderText = shape.Name;
                container.BorderWidth = 2.0f;
            }
            _editor.Canvas.Invalidate();
            return true;
        }

        public bool SetShapeBounds(Guid id, RectangleF bounds)
        {
            ShapeBase shape = FindShape(id);
            if (shape == null) return false;
            bool result = _editor.UpdateShapeProperty(id, "Bounds", bounds);
            Doc.Reindex();
            _editor.Canvas.Invalidate();
            return result;
        }

        public bool SetContainer(Guid childId, Guid containerId)
        {
            ShapeBase child = FindShape(childId);
            if (child == null) return false;
            ShapeBase oldParentShape = child.Parent as ShapeBase;
            ContainerShape oldParent = oldParentShape as ContainerShape;
            if (oldParent != null && oldParent.Id != containerId) oldParent.RemoveChild(child);
            if (containerId == Guid.Empty)
            {
                if (oldParent != null) oldParent.RemoveChild(child);
                return true;
            }
            ContainerShape parent = FindShape(containerId) as ContainerShape;
            if (parent == null || parent == child) return false;
            parent.AddChild(child);
            return true;
        }

        public Guid GetContainer(Guid childId)
        {
            ShapeBase child = FindShape(childId);
            ShapeBase parent = child == null ? null : child.Parent as ShapeBase;
            return parent == null ? Guid.Empty : parent.Id;
        }

        // ===== 连线 =====

        public bool Connect(string fromName, string toName, string label)
        {
            ShapeBase f = FindShapeByName(fromName);
            ShapeBase t = FindShapeByName(toName);
            if (f == null || t == null) return false;

            Connection c = new Connection();
            c.FromShape = f;
            c.ToShape = t;
            c.Mode = GlobalConfig.Instance.DefaultConnectionMode;
            c.Label = label;
            Doc.AddConnection(c);
            _editor.Canvas.Invalidate();
            return true;
        }

        public Guid ConnectById(Guid fromId, Guid toId, string label)
        {
            ShapeBase from = FindShape(fromId);
            ShapeBase to = FindShape(toId);
            if (from == null || to == null) return Guid.Empty;
            Connection connection = new Connection();
            connection.FromShape = from;
            connection.ToShape = to;
            connection.Mode = GlobalConfig.Instance.DefaultConnectionMode;
            connection.Label = label;
            Doc.AddConnection(connection);
            _editor.Canvas.Invalidate();
            return connection.Id;
        }

        public void BringToFrontSelection() { _editor.BringToFront(); }
        public void SendToBackSelection() { _editor.SendToBack(); }

        // ===== 视图 =====

        public void Zoom(float factor) { _editor.Canvas.Zoom = _editor.Canvas.Zoom * factor; }
        public void SetZoom(float absolute) { _editor.Canvas.Zoom = absolute; }
        public float GetZoom() { return _editor.Canvas.Zoom; }
        public PointF GetViewportOffset() { return _editor.Canvas.Offset; }
        public void SetViewport(float zoom, PointF offset)
        {
            if (zoom > 0.05f) _editor.Canvas.Zoom = zoom;
            _editor.Canvas.Offset = offset;
            _editor.Canvas.Invalidate();
        }
        public IDisposable SuppressEvents() { return _editor.SuppressEvents(); }
        public IDisposable BeginApplyExternalChange() { return _editor.BeginApplyExternalChange(); }

        public void FitView()
        {
            IList<ShapeBase> shapes = Doc.Shapes;
            if (shapes.Count == 0) return;

            RectangleF b = shapes[0].Bounds;
            for (int i = 1; i < shapes.Count; i++)
                b = RectangleF.Union(b, shapes[i].Bounds);

            if (b.Width < 1f) b = new RectangleF(b.X, b.Y, 1f, b.Height);
            if (b.Height < 1f) b = new RectangleF(b.X, b.Y, b.Width, 1f);

            float vw = _editor.Canvas.ClientSize.Width;
            float vh = _editor.Canvas.ClientSize.Height;
            if (vw <= 4f || vh <= 4f) return;

            float z = Math.Min((vw - 80f) / b.Width, (vh - 80f) / b.Height);
            if (z > 5f) z = 5f;
            if (z < 0.1f) z = 0.1f;
            _editor.Canvas.Zoom = z;

            float cx = b.X + b.Width / 2f;
            float cy = b.Y + b.Height / 2f;
            _editor.Canvas.Offset = new PointF(vw / 2f - cx * z, vh / 2f - cy * z);
        }

        public bool SetConnectionMode(string mode)
        {
            string m = (mode == null ? "" : mode.Trim().ToLowerInvariant());
            if (m == "straight" || m == "直线") { _editor.SetConnectionMode(ConnectionMode.Straight); return true; }
            if (m == "curve" || m == "曲线") { _editor.SetConnectionMode(ConnectionMode.Curve); return true; }
            if (m == "ortho" || m == "orthogonal" || m == "正交") { _editor.SetConnectionMode(ConnectionMode.Orthogonal); return true; }
            return false;
        }

        // ===== 文档 =====

        public void NewDocument()
        {
            _editor.ClearDocument();
            Doc.Reindex();
        }

        public void SaveDocument(string path)
        {
            Doc.Config = _editor.BuildCanvasConfig();
            XmlShapeSerializer.Save(path, Doc);
        }

        public void LoadDocument(string path)
        {
            DrawingDocument doc = XmlShapeSerializer.Load(path);
            _editor.SetDocument(doc);
            Doc.Reindex();
        }

        // ===== 私有辅助 =====

        private ShapeBase FindShape(Guid id)
        {
            foreach (ShapeBase s in Doc.Shapes)
                if (s.Id == id) return s;
            return null;
        }

        private ShapeBase FindShapeByName(string name)
        {
            foreach (ShapeBase s in Doc.Shapes)
                if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        private ShapeInfo ToInfo(ShapeBase s)
        {
            ShapeInfo info = new ShapeInfo();
            info.Id = s.Id;
            info.Name = s.Name;
            info.X = s.X;
            info.Y = s.Y;
            info.Width = s.Width;
            info.Height = s.Height;
            info.FillColor = s.FillColor;
            info.BorderColor = s.BorderColor;

            GenericShape gs = s as GenericShape;
            if (gs != null)
            {
                info.TypeName = gs.ShapeTypeName;
                info.StateName = gs.CurrentStateName;
                List<string> states = new List<string>();
                foreach (ShapeState st in gs.States) states.Add(st.Name);
                info.States = states.ToArray();
            }
            else if (s.IsContainer)
            {
                info.TypeName = string.IsNullOrEmpty(s.Description) ? "容器" : s.Description;
            }
            else
            {
                info.TypeName = s.GetType().Name;
            }
            return info;
        }

        private static string CanonicalProp(string prop)
        {
            string p = (prop == null ? "" : prop.Trim().ToLowerInvariant());
            switch (p)
            {
                case "name": case "名称": return "Name";
                case "x": return "X";
                case "y": return "Y";
                case "w": case "width": case "宽": case "宽度": return "Width";
                case "h": case "height": case "高": case "高度": return "Height";
                case "fill": case "fillcolor": case "填充": return "FillColor";
                case "border": case "bordercolor": case "边框": return "BorderColor";
                case "textcolor": case "文字": return "TextColor";
                case "borderwidth": case "linewidth": case "线宽": return "BorderWidth";
                case "state": case "状态": return "State";
                default: return null;
            }
        }

        private static bool TryCoerce(string canonical, string value, out object typed)
        {
            typed = null;
            switch (canonical)
            {
                case "Name":
                    typed = value;
                    return true;
                case "X": case "Y": case "Width": case "Height": case "BorderWidth":
                    float f;
                    if (!float.TryParse(value, out f)) return false;
                    typed = f;
                    return true;
                case "FillColor": case "BorderColor": case "TextColor":
                    Color c = ColorUtil.Parse(value);
                    if (c == Color.Empty) return false;
                    typed = c;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 按状态名返回对应的填充/边框/文字色。
        /// 覆盖 K8s RunStatus（Pending/Running/Stopped/Warning/Failed/Unknown）
        /// 以及旧版 Normal/Error。
        /// </summary>
        private static void StatusColorsFor(string stateName, out Color fill, out Color border, out Color text)
        {
            string s = (stateName == null ? "" : stateName.Trim().ToLowerInvariant());
            switch (s)
            {
                case "pending":
                    fill = Color.FromArgb(227, 242, 253);
                    border = Color.FromArgb(33, 150, 243);
                    text = Color.FromArgb(13, 71, 161);
                    break;
                case "running":
                case "normal":
                    fill = Color.FromArgb(232, 245, 233);
                    border = Color.FromArgb(76, 175, 80);
                    text = Color.FromArgb(27, 94, 32);
                    break;
                case "stopped":
                    fill = Color.FromArgb(238, 238, 238);
                    border = Color.FromArgb(117, 117, 117);
                    text = Color.FromArgb(66, 66, 66);
                    break;
                case "warning":
                    fill = Color.FromArgb(255, 243, 224);
                    border = Color.FromArgb(255, 152, 0);
                    text = Color.FromArgb(230, 81, 0);
                    break;
                case "failed":
                case "error":
                    fill = Color.FromArgb(255, 235, 238);
                    border = Color.FromArgb(244, 67, 54);
                    text = Color.FromArgb(183, 28, 28);
                    break;
                case "unknown":
                default:
                    fill = Color.FromArgb(245, 245, 245);
                    border = Color.FromArgb(158, 158, 158);
                    text = Color.FromArgb(97, 97, 97);
                    break;
            }
        }
    }
}
