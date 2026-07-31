using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
                    sd.States.Add(stateData);
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
                    g.States.Add(s);
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
    }
}
