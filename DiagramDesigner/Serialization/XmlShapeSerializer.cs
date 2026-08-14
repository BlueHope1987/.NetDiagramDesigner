using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;
using DiagramDesigner.Core;
using DiagramDesigner.Shapes;

namespace DiagramDesigner.Serialization
{
    public class XmlShapeSerializer
    {
        public static void Save(string filePath, DrawingDocument document)
        {
            DocumentData data = ConvertToData(document);
            XmlSerializer serializer = new XmlSerializer(typeof(DocumentData));
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                serializer.Serialize(writer, data);
            }
        }

        public static DrawingDocument Load(string filePath)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(DocumentData));
            DocumentData data;
            using (StreamReader reader = new StreamReader(filePath))
            {
                data = (DocumentData)serializer.Deserialize(reader);
            }
            return ConvertFromData(data);
        }

        public static DocumentData ConvertToData(DrawingDocument document)
        {
            DocumentData data = new DocumentData();
            data.Name = document.Name;
            data.PageWidth = document.PageSize.Width;
            data.PageHeight = document.PageSize.Height;
            data.Config = document.Config;

            Dictionary<ShapeBase, string> idMap = new Dictionary<ShapeBase, string>();

            foreach (ShapeBase shape in document.Shapes)
            {
                ShapeData sd = ConvertShapeToData(shape);
                idMap[shape] = sd.Id;
                data.Shapes.Add(sd);
            }

            foreach (ShapeData sd in data.Shapes)
            {
                foreach (ShapeBase shape in document.Shapes)
                {
                    if (idMap[shape] == sd.Id)
                    {
                        if (shape.Parent != null && idMap.ContainsKey(shape.Parent))
                        {
                            sd.ParentId = idMap[shape.Parent];
                        }
                        break;
                    }
                }
            }

            foreach (Connection conn in document.Connections)
            {
                ConnectionData cd = new ConnectionData();
                cd.Id = conn.Id.ToString();
                if (conn.FromShape != null && idMap.ContainsKey(conn.FromShape))
                    cd.FromShapeId = idMap[conn.FromShape];
                if (conn.ToShape != null && idMap.ContainsKey(conn.ToShape))
                    cd.ToShapeId = idMap[conn.ToShape];
                cd.Mode = conn.Mode.ToString();
                cd.ArgbLineColor = conn.LineColor.ToArgb();
                cd.LineWidth = conn.LineWidth;
                cd.DashStyle = conn.DashStyle.ToString();
                cd.ArrowAtEnd = conn.ArrowAtEnd;
                cd.Label = conn.Label;
                data.Connections.Add(cd);
            }

            return data;
        }

        public static DrawingDocument ConvertFromData(DocumentData data)
        {
            DrawingDocument document = new DrawingDocument();
            document.Name = data.Name;
            document.PageSize = new SizeF(data.PageWidth, data.PageHeight);
            if (data.Config != null)
                document.Config = data.Config;

            Dictionary<string, ShapeBase> shapeMap = new Dictionary<string, ShapeBase>();
            Dictionary<string, string> parentMap = new Dictionary<string, string>();

            foreach (ShapeData sd in data.Shapes)
            {
                ShapeBase shape = ConvertDataToShape(sd);
                if (shape != null)
                {
                    document.AddShape(shape);
                    shapeMap[sd.Id] = shape;
                    if (sd.ParentId.Length > 0)
                        parentMap[sd.Id] = sd.ParentId;
                }
            }

            foreach (string childId in parentMap.Keys)
            {
                if (shapeMap.ContainsKey(childId) && shapeMap.ContainsKey(parentMap[childId]))
                {
                    ShapeBase child = shapeMap[childId];
                    ShapeBase parent = shapeMap[parentMap[childId]];
                    if (parent is ContainerShape)
                    {
                        ((ContainerShape)parent).AddChild(child);
                    }
                }
            }

            foreach (ConnectionData cd in data.Connections)
            {
                Connection conn = new Connection();
                conn.Id = new Guid(cd.Id);
                if (shapeMap.ContainsKey(cd.FromShapeId))
                    conn.FromShape = shapeMap[cd.FromShapeId];
                if (shapeMap.ContainsKey(cd.ToShapeId))
                    conn.ToShape = shapeMap[cd.ToShapeId];

                try
                {
                    conn.Mode = (ConnectionMode)Enum.Parse(typeof(ConnectionMode), cd.Mode);
                }
                catch
                {
                    conn.Mode = ConnectionMode.Straight;
                }

                conn.LineColor = Color.FromArgb(cd.ArgbLineColor);
                conn.LineWidth = cd.LineWidth;

                try
                {
                    conn.DashStyle = (DashStyle)Enum.Parse(typeof(DashStyle), cd.DashStyle);
                }
                catch
                {
                    conn.DashStyle = DashStyle.Solid;
                }

                conn.ArrowAtEnd = cd.ArrowAtEnd;
                conn.Label = cd.Label;
                document.AddConnection(conn);
            }

            return document;
        }

        private static ShapeData ConvertShapeToData(ShapeBase shape)
        {
            ShapeData sd = new ShapeData();
            sd.Id = shape.Id.ToString();
            sd.Name = shape.Name;
            sd.Description = shape.Description;
            sd.X = shape.X;
            sd.Y = shape.Y;
            sd.Width = shape.Width;
            sd.Height = shape.Height;
            sd.ArgbFillColor = shape.FillColor.ToArgb();
            sd.ArgbBorderColor = shape.BorderColor.ToArgb();
            sd.ArgbTextColor = shape.TextColor.ToArgb();
            sd.BorderWidth = shape.BorderWidth;
            sd.ZOrder = shape.ZOrder;
            sd.Visible = shape.Visible;

            if (shape is ContainerShape)
            {
                ContainerShape c = (ContainerShape)shape;
                sd.ShapeClass = "ContainerShape";
                sd.IsContainer = true;
                sd.HeaderText = c.HeaderText;
                sd.HeaderHeight = c.HeaderHeight;
                sd.ArgbHeaderColor = c.HeaderColor.ToArgb();
            }
            else if (shape is GenericShape)
            {
                GenericShape g = (GenericShape)shape;
                sd.ShapeClass = "GenericShape";
                sd.ShapeTypeName = g.ShapeTypeName;
                sd.CurrentStateName = g.CurrentStateName;
                sd.MemberAreaTop = g.MemberAreaTop;
                sd.RefWidth = g.RefWidth;
                sd.RefHeight = g.RefHeight;

                // 序列化图形实例的 Zone 列表
                if (g.Zones != null)
                {
                    foreach (ShapeZone zone in g.Zones)
                        sd.Zones.Add(ToZoneData(zone));
                }

                foreach (ShapeMember m in g.Members)
                {
                    MemberData md = new MemberData();
                    md.MemberType = m.MemberType.ToString();
                    md.Name = m.Name;
                    md.TypeName = m.TypeName;
                    md.Visibility = m.Visibility.ToString();
                    md.IsStatic = m.IsStatic;
                    md.IsAbstract = m.IsAbstract;
                    md.DefaultValue = m.DefaultValue;
                    foreach (ShapeMemberParameter p in m.Parameters)
                    {
                        ParameterData pd = new ParameterData();
                        pd.Name = p.Name;
                        pd.TypeName = p.TypeName;
                        pd.DefaultValue = p.DefaultValue;
                        md.Parameters.Add(pd);
                    }
                    sd.Members.Add(md);
                }

                foreach (ShapeState s in g.States)
                {
                    StateData stateData = new StateData();
                    stateData.Name = s.Name;
                    stateData.ArgbFillColor = s.FillColor.Argb;
                    stateData.ArgbBorderColor = s.BorderColor.Argb;
                    stateData.ArgbTextColor = s.TextColor.Argb;
                    stateData.ArgbHeaderColor = s.HeaderColor.Argb;
                    stateData.Priority = s.Priority;
                    stateData.UseCustomRenderCommands = s.UseCustomRenderCommands;

                    // 序列化状态的自定义绘制指令
                    if (s.CustomRenderCommands != null)
                    {
                        foreach (RenderCommand rc in s.CustomRenderCommands)
                            stateData.CustomRenderCommands.Add(ToCommandData(rc));
                    }

                    // 序列化状态的自定义 Zone 列表（CustomZones）
                    if (s.CustomZones != null)
                    {
                        foreach (ShapeZone zone in s.CustomZones)
                            stateData.Zones.Add(ToZoneData(zone));
                    }

                    sd.States.Add(stateData);
                }

                // 序列化系统行为列表（GenericShape.SystemActions）
                if (g.SystemActions != null)
                {
                    foreach (ShapeAction action in g.SystemActions)
                    {
                        ActionData ad = ToActionData(action);
                        if (ad != null)
                            sd.SystemActions.Add(ad);
                    }
                }
            }

            return sd;
        }

        private static ShapeBase ConvertDataToShape(ShapeData sd)
        {
            ShapeBase shape = null;

            if (sd.ShapeClass == "ContainerShape")
            {
                ContainerShape c = new ContainerShape();
                c.HeaderText = sd.HeaderText;
                c.HeaderHeight = sd.HeaderHeight;
                c.HeaderColor = Color.FromArgb(sd.ArgbHeaderColor);
                shape = c;
            }
            else
            {
                GenericShape g = new GenericShape();
                g.ShapeTypeName = sd.ShapeTypeName;
                g.CurrentStateName = sd.CurrentStateName;
                g.MemberAreaTop = sd.MemberAreaTop;
                g.RefWidth = sd.RefWidth;
                g.RefHeight = sd.RefHeight;

                // 反序列化图形实例的 Zone 列表（向后兼容：空列表不影响加载）
                if (sd.Zones != null)
                {
                    foreach (ZoneData zd in sd.Zones)
                        g.Zones.Add(FromZoneData(zd));
                }

                foreach (MemberData md in sd.Members)
                {
                    ShapeMember m = new ShapeMember();
                    try
                    {
                        m.MemberType = (MemberType)Enum.Parse(typeof(MemberType), md.MemberType);
                    }
                    catch
                    {
                        m.MemberType = MemberType.Property;
                    }
                    m.Name = md.Name;
                    m.TypeName = md.TypeName;
                    try
                    {
                        m.Visibility = (Visibility)Enum.Parse(typeof(Visibility), md.Visibility);
                    }
                    catch
                    {
                        m.Visibility = Core.Visibility.Public;
                    }
                    m.IsStatic = md.IsStatic;
                    m.IsAbstract = md.IsAbstract;
                    m.DefaultValue = md.DefaultValue;
                    foreach (ParameterData pd in md.Parameters)
                    {
                        ShapeMemberParameter p = new ShapeMemberParameter();
                        p.Name = pd.Name;
                        p.TypeName = pd.TypeName;
                        p.DefaultValue = pd.DefaultValue;
                        m.Parameters.Add(p);
                    }
                    g.Members.Add(m);
                }

                foreach (StateData stateData in sd.States)
                {
                    ShapeState s = new ShapeState();
                    s.Name = stateData.Name;
                    s.FillColor = new XmlColor(Color.FromArgb(stateData.ArgbFillColor));
                    s.BorderColor = new XmlColor(Color.FromArgb(stateData.ArgbBorderColor));
                    s.TextColor = new XmlColor(Color.FromArgb(stateData.ArgbTextColor));
                    s.HeaderColor = new XmlColor(Color.FromArgb(stateData.ArgbHeaderColor));
                    s.Priority = stateData.Priority;
                    s.UseCustomRenderCommands = stateData.UseCustomRenderCommands;

                    // 反序列化状态的自定义绘制指令
                    if (stateData.CustomRenderCommands != null)
                    {
                        foreach (RenderCommandData rcd in stateData.CustomRenderCommands)
                            s.CustomRenderCommands.Add(FromCommandData(rcd));
                    }

                    // 反序列化状态的自定义 Zone 列表（CustomZones）
                    if (stateData.Zones != null)
                    {
                        foreach (ZoneData zd in stateData.Zones)
                            s.CustomZones.Add(FromZoneData(zd));
                    }

                    g.States.Add(s);
                }

                // 反序列化系统行为列表（向后兼容：空列表不影响加载）
                if (sd.SystemActions != null)
                {
                    foreach (ActionData ad in sd.SystemActions)
                    {
                        ShapeAction action = FromActionData(ad);
                        if (action != null)
                            g.SystemActions.Add(action);
                    }
                }

                shape = g;
            }

            shape.Id = new Guid(sd.Id);
            shape.Name = sd.Name;
            shape.Description = sd.Description;
            shape.Bounds = new RectangleF(sd.X, sd.Y, sd.Width, sd.Height);
            shape.FillColor = Color.FromArgb(sd.ArgbFillColor);
            shape.BorderColor = Color.FromArgb(sd.ArgbBorderColor);
            shape.TextColor = Color.FromArgb(sd.ArgbTextColor);
            shape.BorderWidth = sd.BorderWidth;
            shape.ZOrder = sd.ZOrder;
            shape.Visible = sd.Visible;

            return shape;
        }

        // =====================================================================
        // RenderCommand <-> RenderCommandData 转换
        // =====================================================================

        /// <summary>将 RenderCommand 转换为可序列化的 RenderCommandData。</summary>
        private static RenderCommandData ToCommandData(RenderCommand rc)
        {
            RenderCommandData rcd = new RenderCommandData();
            rcd.CommandType = rc.CommandType.ToString();
            rcd.X = rc.X;
            rcd.Y = rc.Y;
            rcd.Width = rc.Width;
            rcd.Height = rc.Height;
            rcd.CornerRadius = rc.CornerRadius;
            rcd.ArgbFillColor = rc.FillColor.Argb;
            rcd.ArgbStrokeColor = rc.StrokeColor.Argb;
            rcd.StrokeWidth = rc.StrokeWidth;
            rcd.Text = rc.Text;
            rcd.TextAlign = rc.TextAlign;
            rcd.FontSize = rc.FontSize;
            rcd.IsBold = rc.IsBold;
            rcd.PolygonPointsStr = PointsToString(rc.PolygonPoints);
            rcd.MultiPathsStr = MultiPathsToString(rc.MultiPaths);
            rcd.PathDefsStr = PathDefsToString(rc.PathDefs);
            rcd.PolyHandleTypesStr = HandleTypesToString(rc.PolyHandleTypes);
            rcd.PolyHandleInsStr = HandlePointsToString(rc.PolyHandleIns);
            rcd.PolyHandleOutsStr = HandlePointsToString(rc.PolyHandleOuts);
            rcd.BoolOp = rc.BoolOp.ToString();
            rcd.UseShapeColors = rc.UseShapeColors;
            rcd.Fill = rc.Fill;
            rcd.Stroke = rc.Stroke;
            return rcd;
        }

        /// <summary>将 RenderCommandData 还原为 RenderCommand。</summary>
        private static RenderCommand FromCommandData(RenderCommandData rcd)
        {
            RenderCommand rc = new RenderCommand();

            try
            {
                rc.CommandType = (RenderCommandType)Enum.Parse(typeof(RenderCommandType), rcd.CommandType);
            }
            catch
            {
                rc.CommandType = RenderCommandType.Rectangle;
            }

            rc.X = rcd.X;
            rc.Y = rcd.Y;
            rc.Width = rcd.Width;
            rc.Height = rcd.Height;
            rc.CornerRadius = rcd.CornerRadius;
            rc.FillColor = new XmlColor(Color.FromArgb(rcd.ArgbFillColor));
            rc.StrokeColor = new XmlColor(Color.FromArgb(rcd.ArgbStrokeColor));
            rc.StrokeWidth = rcd.StrokeWidth;
            rc.Text = rcd.Text;
            rc.TextAlign = rcd.TextAlign;
            rc.FontSize = rcd.FontSize;
            rc.IsBold = rcd.IsBold;
            rc.PolygonPoints = ParsePoints(rcd.PolygonPointsStr);
            rc.MultiPaths = ParseMultiPaths(rcd.MultiPathsStr);
            rc.PathDefs = ParsePathDefs(rcd.PathDefsStr);
            rc.PolyHandleTypes = ParseHandleTypes(rcd.PolyHandleTypesStr);
            rc.PolyHandleIns = ParseHandlePoints(rcd.PolyHandleInsStr);
            rc.PolyHandleOuts = ParseHandlePoints(rcd.PolyHandleOutsStr);

            try
            {
                rc.BoolOp = (BooleanOperation)Enum.Parse(typeof(BooleanOperation), rcd.BoolOp);
            }
            catch
            {
                rc.BoolOp = BooleanOperation.None;
            }

            rc.UseShapeColors = rcd.UseShapeColors;
            rc.Fill = rcd.Fill;
            rc.Stroke = rcd.Stroke;
            return rc;
        }

        // =====================================================================
        // ShapeZone <-> ZoneData 转换
        // =====================================================================

        /// <summary>将 ShapeZone 转换为可序列化的 ZoneData。</summary>
        private static ZoneData ToZoneData(ShapeZone zone)
        {
            ZoneData zd = new ZoneData();
            zd.Name = zone.Name;
            zd.Layout = zone.Layout.ToString();
            zd.Scaling = zone.Scaling.ToString();
            zd.X = zone.X;
            zd.Y = zone.Y;
            zd.Width = zone.Width;
            zd.Height = zone.Height;
            zd.ShowBorder = zone.ShowBorder;
            zd.ArgbBorderColor = zone.BorderColor.Argb;
            zd.Title = zone.Title;
            zd.IsTitleZone = zone.IsTitleZone;
            zd.IsMemberZone = zone.IsMemberZone;
            zd.Anchor = zone.Anchor.ToString();
            zd.ArgbFillColor = zone.FillColor.Argb;
            zd.IsClickZone = zone.IsClickZone;
            zd.IsConnectionZone = zone.IsConnectionZone;
            zd.CanStart = zone.CanStart;
            zd.CanEnd = zone.CanEnd;
            zd.AllowedLineTypes = zone.AllowedLineTypes;
            zd.AllowSelfConnect = zone.AllowSelfConnect;
            return zd;
        }

        /// <summary>将 ZoneData 还原为 ShapeZone。</summary>
        private static ShapeZone FromZoneData(ZoneData zd)
        {
            ShapeZone zone = new ShapeZone();
            zone.Name = zd.Name;

            try
            {
                zone.Layout = (ZoneLayout)Enum.Parse(typeof(ZoneLayout), zd.Layout);
            }
            catch
            {
                zone.Layout = ZoneLayout.None;
            }

            try
            {
                zone.Scaling = (ZoneScaling)Enum.Parse(typeof(ZoneScaling), zd.Scaling);
            }
            catch
            {
                zone.Scaling = ZoneScaling.None;
            }

            zone.X = zd.X;
            zone.Y = zd.Y;
            zone.Width = zd.Width;
            zone.Height = zd.Height;
            zone.ShowBorder = zd.ShowBorder;
            zone.BorderColor = new XmlColor(Color.FromArgb(zd.ArgbBorderColor));
            zone.Title = zd.Title;
            zone.IsTitleZone = zd.IsTitleZone;
            zone.IsMemberZone = zd.IsMemberZone;

            try
            {
                zone.Anchor = (ZoneAnchor)Enum.Parse(typeof(ZoneAnchor), zd.Anchor);
            }
            catch
            {
                zone.Anchor = ZoneAnchor.Absolute;
            }

            zone.FillColor = new XmlColor(Color.FromArgb(zd.ArgbFillColor));
            zone.IsClickZone = zd.IsClickZone;
            zone.IsConnectionZone = zd.IsConnectionZone;
            zone.CanStart = zd.CanStart;
            zone.CanEnd = zd.CanEnd;
            zone.AllowedLineTypes = zd.AllowedLineTypes;
            zone.AllowSelfConnect = zd.AllowSelfConnect;
            return zone;
        }

        // =====================================================================
        // ShapeAction <-> ActionData 转换（含子操作递归序列化）
        // 适用于 SystemActions 与 CustomActions，统一处理新字段
        // （IsSystemBehavior、ZoneName、SubActions）。
        // =====================================================================

        /// <summary>将 ShapeAction 转换为可序列化的 ActionData（递归处理子操作）。</summary>
        private static ActionData ToActionData(ShapeAction action)
        {
            if (action == null)
                return null;

            ActionData ad = new ActionData();
            ad.Name = action.Name;
            ad.IconName = action.IconName;
            ad.ActionType = action.ActionType.ToString();
            ad.CallbackName = action.CallbackName;
            ad.TargetState = action.TargetState;
            ad.IsSystemBehavior = action.IsSystemBehavior;
            ad.ZoneName = action.ZoneName;

            if (action.SubActions != null)
            {
                foreach (ShapeAction sub in action.SubActions)
                {
                    ActionData subAd = ToActionData(sub);
                    if (subAd != null)
                        ad.SubActions.Add(subAd);
                }
            }
            return ad;
        }

        /// <summary>将 ActionData 还原为 ShapeAction（递归还原子操作）。</summary>
        private static ShapeAction FromActionData(ActionData ad)
        {
            if (ad == null)
                return null;

            ShapeAction action = new ShapeAction();
            action.Name = ad.Name;
            action.IconName = ad.IconName;

            try
            {
                action.ActionType = (ShapeActionType)Enum.Parse(
                    typeof(ShapeActionType), ad.ActionType);
            }
            catch
            {
                action.ActionType = ShapeActionType.HostCallback;
            }

            action.CallbackName = ad.CallbackName;
            action.TargetState = ad.TargetState;
            action.IsSystemBehavior = ad.IsSystemBehavior;
            action.ZoneName = ad.ZoneName;

            if (ad.SubActions != null)
            {
                foreach (ActionData subAd in ad.SubActions)
                {
                    ShapeAction sub = FromActionData(subAd);
                    if (sub != null)
                        action.SubActions.Add(sub);
                }
            }
            return action;
        }

        // =====================================================================
        // PointF[] 与 List<PointF[]> 的字符串编解码辅助方法
        // =====================================================================

        /// <summary>
        /// 将 PointF[] 编码为 "x1,y1;x2,y2;..." 字符串。
        /// 使用 InvariantCulture 保证小数点为 "."，避免与坐标分隔符 "," 冲突。
        /// </summary>
        private static string PointsToString(PointF[] points)
        {
            if (points == null || points.Length == 0)
                return "";

            string[] parts = new string[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                parts[i] = points[i].X.ToString(CultureInfo.InvariantCulture)
                    + "," + points[i].Y.ToString(CultureInfo.InvariantCulture);
            }
            return string.Join(";", parts);
        }

        /// <summary>
        /// 将 "x1,y1;x2,y2;..." 字符串解码为 PointF[]。
        /// 解析失败时返回 null，保证向后兼容。
        /// </summary>
        private static PointF[] ParsePoints(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            string[] parts = s.Split(';');
            List<PointF> result = new List<PointF>();
            for (int i = 0; i < parts.Length; i++)
            {
                string trimmed = parts[i].Trim();
                if (trimmed.Length == 0)
                    continue;

                string[] xy = trimmed.Split(',');
                if (xy.Length >= 2)
                {
                    float x;
                    float y;
                    if (float.TryParse(xy[0].Trim(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out x)
                        && float.TryParse(xy[1].Trim(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out y))
                    {
                        result.Add(new PointF(x, y));
                    }
                }
            }

            if (result.Count == 0)
                return null;
            return result.ToArray();
        }

        /// <summary>
        /// 将 List&lt;PointF[]&gt; 编码为 "x1,y1;x2,y2 | x3,y3;x4,y4" 字符串。
        /// 路径之间以 " | " 分隔，空路径会被跳过。
        /// </summary>
        private static string MultiPathsToString(List<PointF[]> paths)
        {
            if (paths == null || paths.Count == 0)
                return "";

            List<string> pathStrs = new List<string>();
            foreach (PointF[] path in paths)
            {
                if (path == null || path.Length == 0)
                    continue;
                pathStrs.Add(PointsToString(path));
            }

            if (pathStrs.Count == 0)
                return "";
            return string.Join(" | ", pathStrs.ToArray());
        }

        /// <summary>
        /// 将 "x1,y1;x2,y2 | x3,y3;x4,y4" 字符串解码为 List&lt;PointF[]&gt;。
        /// 路径以 "|" 分隔，解析失败时返回 null，保证向后兼容。
        /// </summary>
        private static List<PointF[]> ParseMultiPaths(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            string[] pathParts = s.Split('|');
            List<PointF[]> result = new List<PointF[]>();
            for (int i = 0; i < pathParts.Length; i++)
            {
                string trimmed = pathParts[i].Trim();
                if (trimmed.Length == 0)
                    continue;

                PointF[] pts = ParsePoints(trimmed);
                if (pts != null && pts.Length > 0)
                    result.Add(pts);
            }

            if (result.Count == 0)
                return null;
            return result;
        }

        // =====================================================================
        // PathDef 列表的字符串编解码辅助方法
        // 每条路径拥有独立的 BoolOp。
        // 格式: "x1,y1|x2,y2;BoolOp|x3,y3;BoolOp2|..."
        //   - 顶点之间以 '|' 分隔
        //   - x,y 之间以 ',' 分隔
        //   - 路径的顶点与其 BoolOp 以 ';' 分隔
        //   - 路径之间以 '|' 分隔
        // =====================================================================

        /// <summary>
        /// 将 List&lt;PathDef&gt; 编码为字符串。
        /// 每条路径编码为 "x1,y1|x2,y2|...;BoolOp,IsShape#handleData"
        /// 其中 handleData 格式为 "HT,hiX,hiY,hoX,hoY#HT,..."（每个顶点的句柄）
        /// 无句柄数据时省略 # 部分。向后兼容：无逗号时 IsShape 默认 true。
        /// </summary>
        private static string PathDefsToString(List<PathDef> defs)
        {
            if (defs == null || defs.Count == 0)
                return "";

            List<string> pathStrs = new List<string>();
            foreach (PathDef def in defs)
            {
                if (def == null)
                    continue;
                string pts = PointsToPipeString(def.Points);
                string meta = def.BoolOp.ToString() + "," + def.IsShape.ToString().ToLower();

                // 编码句柄数据
                string handleStr = "";
                if (def.HandleTypes != null && def.Points != null)
                {
                    List<string> handleParts = new List<string>();
                    for (int i = 0; i < def.Points.Length; i++)
                    {
                        HandleType ht = (i < def.HandleTypes.Length) ? def.HandleTypes[i] : HandleType.None;
                        PointF hi = (i < def.HandleIns.Length) ? def.HandleIns[i] : PointF.Empty;
                        PointF ho = (i < def.HandleOuts.Length) ? def.HandleOuts[i] : PointF.Empty;
                        handleParts.Add((int)ht + ","
                            + hi.X.ToString(CultureInfo.InvariantCulture) + ","
                            + hi.Y.ToString(CultureInfo.InvariantCulture) + ","
                            + ho.X.ToString(CultureInfo.InvariantCulture) + ","
                            + ho.Y.ToString(CultureInfo.InvariantCulture));
                    }
                    handleStr = "#" + string.Join("#", handleParts.ToArray());
                }

                pathStrs.Add(pts + ";" + meta + handleStr);
            }

            if (pathStrs.Count == 0)
                return "";
            return string.Join("|", pathStrs.ToArray());
        }

        /// <summary>
        /// 将路径定义字符串解码为 List&lt;PathDef&gt;。
        /// 支持新旧两种格式：
        ///   旧格式: "x1,y1|x2,y2;BoolOp|..."（无 IsShape 和句柄数据）
        ///   新格式: "x1,y1|x2,y2;BoolOp,IsShape#handleData|..."
        /// 解析失败时返回 null，保证向后兼容。
        /// </summary>
        private static List<PathDef> ParsePathDefs(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            string[] tokens = s.Split('|');
            List<PathDef> result = new List<PathDef>();
            List<PointF> current = new List<PointF>();

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (token.Length == 0)
                    continue;

                // 分离句柄数据（# 后部分）
                string handleSection = "";
                int hashIdx = token.IndexOf('#');
                if (hashIdx >= 0)
                {
                    handleSection = token.Substring(hashIdx);
                    token = token.Substring(0, hashIdx);
                }

                string pointPart = token;
                string metaPart = "";

                int sep = token.IndexOf(';');
                if (sep >= 0)
                {
                    pointPart = token.Substring(0, sep);
                    metaPart = token.Substring(sep + 1);
                }

                PointF pt;
                if (TryParsePoint(pointPart, out pt))
                    current.Add(pt);

                // 遇到 ';' 表示该路径结束，附带其 BoolOp + IsShape
                if (sep >= 0)
                {
                    // 解析元数据：格式为 "BoolOp" 或 "BoolOp,IsShape"
                    BooleanOperation op = BooleanOperation.None;
                    bool isShape = true;

                    string[] metaParts = metaPart.Split(',');
                    if (metaParts.Length >= 1)
                    {
                        try
                        {
                            op = (BooleanOperation)Enum.Parse(
                                typeof(BooleanOperation), metaParts[0].Trim());
                        }
                        catch
                        {
                            op = BooleanOperation.None;
                        }
                    }
                    if (metaParts.Length >= 2)
                    {
                        try
                        {
                            isShape = bool.Parse(metaParts[1].Trim());
                        }
                        catch
                        {
                            isShape = true;
                        }
                    }

                    if (current.Count > 0)
                    {
                        PathDef pd = new PathDef(current.ToArray(), op);
                        pd.IsShape = isShape;

                        // 解析句柄数据
                        if (handleSection.Length > 1)
                        {
                            ParsePathDefHandles(pd, handleSection.Substring(1)); // 跳过 '#'
                        }

                        result.Add(pd);
                    }
                    current.Clear();
                }
            }

            if (result.Count == 0)
                return null;
            return result;
        }

        /// <summary>
        /// 解析路径句柄数据字符串，设置到 PathDef。
        /// 格式: "HT,hiX,hiY,hoX,hoY#HT,hiX,hiY,hoX,hoY#..."
        /// </summary>
        private static void ParsePathDefHandles(PathDef pd, string handleStr)
        {
            if (pd.Points == null || pd.Points.Length == 0)
                return;

            string[] entries = handleStr.Split('#');
            if (entries.Length == 0)
                return;

            int n = pd.Points.Length;
            HandleType[] hTypes = new HandleType[n];
            PointF[] hIns = new PointF[n];
            PointF[] hOuts = new PointF[n];
            bool hasHandles = false;

            for (int i = 0; i < n && i < entries.Length; i++)
            {
                string entry = entries[i].Trim();
                if (entry.Length == 0)
                {
                    hTypes[i] = HandleType.None;
                    hIns[i] = PointF.Empty;
                    hOuts[i] = PointF.Empty;
                    continue;
                }

                string[] parts = entry.Split(',');
                if (parts.Length >= 5)
                {
                    try
                    {
                        hTypes[i] = (HandleType)int.Parse(parts[0].Trim());
                        hIns[i] = new PointF(
                            float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture),
                            float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture));
                        hOuts[i] = new PointF(
                            float.Parse(parts[3].Trim(), CultureInfo.InvariantCulture),
                            float.Parse(parts[4].Trim(), CultureInfo.InvariantCulture));
                        if (hTypes[i] != HandleType.None)
                            hasHandles = true;
                    }
                    catch
                    {
                        hTypes[i] = HandleType.None;
                        hIns[i] = PointF.Empty;
                        hOuts[i] = PointF.Empty;
                    }
                }
            }

            if (hasHandles)
            {
                pd.HandleTypes = hTypes;
                pd.HandleIns = hIns;
                pd.HandleOuts = hOuts;
            }
        }

        /// <summary>
        /// 将 PointF[] 编码为 "x1,y1|x2,y2|..." 字符串（顶点以 '|' 分隔）。
        /// 使用 InvariantCulture 保证小数点为 "."。
        /// </summary>
        private static string PointsToPipeString(PointF[] points)
        {
            if (points == null || points.Length == 0)
                return "";

            string[] parts = new string[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                parts[i] = points[i].X.ToString(CultureInfo.InvariantCulture)
                    + "," + points[i].Y.ToString(CultureInfo.InvariantCulture);
            }
            return string.Join("|", parts);
        }

        /// <summary>
        /// 尝试将 "x,y" 解析为单个 PointF。
        /// 解析失败返回 false，保证向后兼容。
        /// </summary>
        private static bool TryParsePoint(string s, out PointF pt)
        {
            pt = PointF.Empty;
            if (s == null)
                return false;
            string trimmed = s.Trim();
            if (trimmed.Length == 0)
                return false;

            string[] xy = trimmed.Split(',');
            if (xy.Length < 2)
                return false;

            float x;
            float y;
            if (float.TryParse(xy[0].Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out x)
                && float.TryParse(xy[1].Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out y))
            {
                pt = new PointF(x, y);
                return true;
            }
            return false;
        }

        // =====================================================================
        // Polygon 类型曲线句柄数据的编解码辅助方法
        // =====================================================================

        /// <summary>
        /// 将 HandleType[] 编码为逗号分隔的整数字符串。
        /// 如 "0,1,2,0"（0=None, 1=Symmetric, 2=Asymmetric）
        /// </summary>
        private static string HandleTypesToString(HandleType[] types)
        {
            if (types == null || types.Length == 0)
                return "";
            string[] parts = new string[types.Length];
            for (int i = 0; i < types.Length; i++)
                parts[i] = ((int)types[i]).ToString();
            return string.Join(",", parts);
        }

        /// <summary>
        /// 将 PointF[] 编码为 "x,y|x,y|..." 字符串。
        /// </summary>
        private static string HandlePointsToString(PointF[] pts)
        {
            if (pts == null || pts.Length == 0)
                return "";
            return PointsToPipeString(pts);
        }

        /// <summary>
        /// 将逗号分隔的整数字符串解码为 HandleType[]。
        /// </summary>
        private static HandleType[] ParseHandleTypes(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;
            string[] parts = s.Split(',');
            List<HandleType> result = new List<HandleType>();
            foreach (string p in parts)
            {
                try
                {
                    result.Add((HandleType)int.Parse(p.Trim()));
                }
                catch
                {
                    result.Add(HandleType.None);
                }
            }
            return (result.Count > 0) ? result.ToArray() : null;
        }

        /// <summary>
        /// 将 "x,y|x,y|..." 字符串解码为 PointF[]。
        /// </summary>
        private static PointF[] ParseHandlePoints(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;
            string[] parts = s.Split('|');
            List<PointF> result = new List<PointF>();
            foreach (string p in parts)
            {
                PointF pt;
                if (TryParsePoint(p, out pt))
                    result.Add(pt);
            }
            return (result.Count > 0) ? result.ToArray() : null;
        }
    }
}
