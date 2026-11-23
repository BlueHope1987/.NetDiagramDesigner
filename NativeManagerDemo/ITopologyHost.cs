using System;
using System.Collections.Generic;
using System.Drawing;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    /// <summary>
    /// 面向宿主的对象快照。仅携带宿主关心的字段，
    /// 使命令行与对象操作面板无需依赖控件的具体图形类型。
    /// </summary>
    public class ShapeInfo
    {
        public Guid Id;
        public string Name;
        public string TypeName;
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public Color FillColor;
        public Color BorderColor;
        public string StateName;      // 当前状态名（可能为空）
        public string[] States;       // 全部状态名（可能为空数组）

        public bool HasStates
        {
            get { return States != null && States.Length > 0; }
        }
    }

    /// <summary>
    /// 宿主与图形控件之间的集成契约。
    /// 命令行引擎与对象操作面板都只依赖此接口，
    /// 因此可脱离 WinForms/控件独立测试（见 SelfTest）。
    /// </summary>
    public interface ITopologyHost
    {
        // 查询
        IList<ShapeInfo> ListShapes();
        IList<ShapeInfo> ListSelection();
        int ShapeCount { get; }
        int ConnectionCount { get; }
        IList<string> ListTypeNames();
        string DescribeConnections();

        // 图形
        ShapeInfo AddShape(string typeName, string name, float x, float y);
        int RemoveSelection();
        void SelectAll();
        void ClearSelection();
        bool SelectByName(string name);
        bool SelectById(Guid id);
        bool RenameSelection(string newName);
        bool SetProperty(string prop, string value);                 // 作用于选中集
        bool SetShapeProperty(Guid id, string prop, string value);   // 作用于单个对象
        bool SetShapeState(Guid id, string stateName);
        bool ConfigureStates(Guid id, string[] stateNames, string initial);
        bool ConfigureResourceStates(Guid id, EntityKind kind, string initial);
        bool SetShapeBounds(Guid id, RectangleF bounds);
        bool SetContainer(Guid childId, Guid containerId);
        Guid GetContainer(Guid childId);

        // 连线
        bool Connect(string fromName, string toName, string label);
        Guid ConnectById(Guid fromId, Guid toId, string label);
        void BringToFrontSelection();
        void SendToBackSelection();

        // 视图
        void Zoom(float factor);
        void SetZoom(float absolute);
        float GetZoom();
        PointF GetViewportOffset();
        void SetViewport(float zoom, PointF offset);
        void FitView();
        bool SetConnectionMode(string mode);
        IDisposable SuppressEvents();
        IDisposable BeginApplyExternalChange();

        // 文档
        void NewDocument();
        void SaveDocument(string path);
        void LoadDocument(string path);
    }

    /// <summary>颜色解析/格式化辅助（支持 #RRGGBB、#AARRGGBB、名称、R,G,B[,A]）。</summary>
    public static class ColorUtil
    {
        public static Color Parse(string s)
        {
            if (s == null) return Color.Empty;
            s = s.Trim();
            if (s.Length == 0) return Color.Empty;

            if (s[0] == '#')
            {
                string hex = s.Substring(1);
                try
                {
                    if (hex.Length == 6)
                    {
                        int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                        int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                        int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                        return Color.FromArgb(r, g, b);
                    }
                    if (hex.Length == 8)
                    {
                        int a = Convert.ToInt32(hex.Substring(0, 2), 16);
                        int r = Convert.ToInt32(hex.Substring(2, 2), 16);
                        int g = Convert.ToInt32(hex.Substring(4, 2), 16);
                        int b = Convert.ToInt32(hex.Substring(6, 2), 16);
                        return Color.FromArgb(a, r, g, b);
                    }
                }
                catch { return Color.Empty; }
            }

            Color named = Color.FromName(s);
            if (named.IsKnownColor) return named;

            string[] parts = s.Split(',');
            if (parts.Length >= 3)
            {
                try
                {
                    int r = byte.Parse(parts[0].Trim());
                    int g = byte.Parse(parts[1].Trim());
                    int b = byte.Parse(parts[2].Trim());
                    if (parts.Length >= 4)
                    {
                        int a = byte.Parse(parts[3].Trim());
                        return Color.FromArgb(a, r, g, b);
                    }
                    return Color.FromArgb(r, g, b);
                }
                catch { return Color.Empty; }
            }
            return Color.Empty;
        }

        public static string ToHex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }
    }
}
