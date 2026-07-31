using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using DiagramDesigner.Core;

namespace DiagramDesigner.Config
{
    public enum EditorTheme
    {
        Light,
        Dark
    }

    [Serializable]
    public class GlobalConfig
    {
        private static GlobalConfig _instance = new GlobalConfig();
        public static GlobalConfig Instance { get { return _instance; } }

        private ConnectionMode _defaultConnectionMode = ConnectionMode.Straight;
        private bool _allowConnectionIntersection = false;
        private bool _showConnectionArcOnIntersect = true;
        private Color _intersectionArcColor = Color.FromArgb(255, 80, 80);
        private float _intersectionArcRadius = 6f;
        private bool _snapToGrid = true;
        private float _gridSize = 20f;
        private bool _showGrid = true;
        private bool _antiAlias = true;
        private float _defaultZoom = 1.0f;
        private EditorTheme _theme = EditorTheme.Light;

        [Category("连线")]
        [DisplayName("默认连线模式")]
        public ConnectionMode DefaultConnectionMode
        {
            get { return _defaultConnectionMode; }
            set { _defaultConnectionMode = value; NotifyChanged(); }
        }

        [Category("连线")]
        [DisplayName("允许连线相交")]
        public bool AllowConnectionIntersection
        {
            get { return _allowConnectionIntersection; }
            set { _allowConnectionIntersection = value; NotifyChanged(); }
        }

        [Category("连线")]
        [DisplayName("相交时显示圆弧")]
        public bool ShowConnectionArcOnIntersect
        {
            get { return _showConnectionArcOnIntersect; }
            set { _showConnectionArcOnIntersect = value; NotifyChanged(); }
        }

        [Category("连线")]
        [DisplayName("相交圆弧颜色")]
        public Color IntersectionArcColor
        {
            get { return _intersectionArcColor; }
            set { _intersectionArcColor = value; NotifyChanged(); }
        }

        [Category("连线")]
        [DisplayName("相交圆弧半径")]
        public float IntersectionArcRadius
        {
            get { return _intersectionArcRadius; }
            set { _intersectionArcRadius = value; NotifyChanged(); }
        }

        [Category("网格")]
        [DisplayName("对齐网格")]
        public bool SnapToGrid
        {
            get { return _snapToGrid; }
            set { _snapToGrid = value; NotifyChanged(); }
        }

        [Category("网格")]
        [DisplayName("网格大小")]
        public float GridSize
        {
            get { return _gridSize; }
            set { _gridSize = value; NotifyChanged(); }
        }

        [Category("网格")]
        [DisplayName("显示网格")]
        public bool ShowGrid
        {
            get { return _showGrid; }
            set { _showGrid = value; NotifyChanged(); }
        }

        [Category("画布")]
        [DisplayName("抗锯齿")]
        public bool AntiAlias
        {
            get { return _antiAlias; }
            set { _antiAlias = value; NotifyChanged(); }
        }

        [Category("画布")]
        [DisplayName("主题")]
        [Description("编辑器配色主题")]
        public EditorTheme Theme
        {
            get { return _theme; }
            set
            {
                _theme = value;
                ApplyThemeColors();
                NotifyChanged();
            }
        }

        [Browsable(false)]
        public float DefaultZoom
        {
            get { return _defaultZoom; }
            set { _defaultZoom = value; }
        }

        [Browsable(false)]
        public Color CanvasBackground
        {
            get
            {
                if (_theme == EditorTheme.Dark)
                    return Color.FromArgb(25, 30, 50);
                return Color.FromArgb(235, 242, 250);
            }
        }

        [Browsable(false)]
        public Color GridColor
        {
            get
            {
                if (_theme == EditorTheme.Dark)
                    return Color.FromArgb(45, 55, 80);
                return Color.FromArgb(210, 220, 235);
            }
        }

        [Browsable(false)]
        public Color GradientCenterColor
        {
            get
            {
                if (_theme == EditorTheme.Dark)
                    return Color.FromArgb(30, 38, 65);
                return Color.FromArgb(245, 250, 255);
            }
        }

        [Browsable(false)]
        public Color SelectionColor
        {
            get
            {
                if (_theme == EditorTheme.Dark)
                    return Color.FromArgb(80, 160, 255);
                return Color.FromArgb(0, 120, 215);
            }
        }

        [Browsable(false)]
        public Color ToolPanelBackColor
        {
            get
            {
                if (_theme == EditorTheme.Dark)
                    return Color.FromArgb(35, 40, 55);
                return Color.FromArgb(245, 247, 250);
            }
        }

        [Browsable(false)]
        public Color ToolPanelCategoryColor
        {
            get
            {
                if (_theme == EditorTheme.Dark)
                    return Color.FromArgb(60, 70, 95);
                return Color.FromArgb(225, 230, 240);
            }
        }

        [Browsable(false)]
        public Color ToolPanelTextColor
        {
            get
            {
                if (_theme == EditorTheme.Dark)
                    return Color.FromArgb(200, 210, 230);
                return Color.FromArgb(50, 55, 65);
            }
        }

        [Browsable(false)]
        public Color ToolPanelBorderColor
        {
            get
            {
                if (_theme == EditorTheme.Dark)
                    return Color.FromArgb(55, 65, 85);
                return Color.FromArgb(210, 215, 225);
            }
        }

        [Browsable(false)]
        public Color RubberBandColor
        {
            get
            {
                if (_theme == EditorTheme.Dark)
                    return Color.FromArgb(80, 160, 255);
                return Color.FromArgb(0, 120, 215);
            }
        }

        public void ApplyThemeColors()
        {
        }

        // ===== 马卡龙色系调色板 =====

        public static Color[] MacaronPalette
        {
            get
            {
                return new Color[] {
                    Color.FromArgb(255, 214, 220),  // 草莓粉 Strawberry Pink
                    Color.FromArgb(255, 218, 193),  // 蜜桃杏 Peach Apricot
                    Color.FromArgb(255, 236, 179),  // 柠檬黄 Lemon Yellow
                    Color.FromArgb(221, 238, 187),  // 抹茶绿 Matcha Green
                    Color.FromArgb(178, 223, 219),  // 薄荷青 Mint Teal
                    Color.FromArgb(199, 211, 236),  // 蓝莓蓝 Blueberry Blue
                    Color.FromArgb(210, 196, 232),  // 薰衣草 Lavender
                    Color.FromArgb(235, 198, 218),  // 玫瑰粉 Rose Pink
                    Color.FromArgb(198, 224, 180),  // 青苹果 Green Apple
                    Color.FromArgb(238, 223, 197),  // 奶油白 Cream White
                    Color.FromArgb(181, 215, 230),  // 天空蓝 Sky Blue
                    Color.FromArgb(228, 209, 191),  // 焦糖棕 Caramel Brown
                };
            }
        }

        public static string[] MacaronPaletteNames
        {
            get
            {
                return new string[] {
                    "草莓粉", "蜜桃杏", "柠檬黄", "抹茶绿",
                    "薄荷青", "蓝莓蓝", "薰衣草", "玫瑰粉",
                    "青苹果", "奶油白", "天空蓝", "焦糖棕"
                };
            }
        }

        // 马卡龙色系配套边框色（较深）
        public static Color[] MacaronBorderPalette
        {
            get
            {
                return new Color[] {
                    Color.FromArgb(200, 120, 130),  // 草莓粉
                    Color.FromArgb(200, 150, 110),  // 蜜桃杏
                    Color.FromArgb(200, 170, 100),  // 柠檬黄
                    Color.FromArgb(140, 175, 120),  // 抹茶绿
                    Color.FromArgb(110, 160, 155),  // 薄荷青
                    Color.FromArgb(120, 145, 180),  // 蓝莓蓝
                    Color.FromArgb(145, 125, 175),  // 薰衣草
                    Color.FromArgb(175, 120, 145),  // 玫瑰粉
                    Color.FromArgb(130, 165, 115),  // 青苹果
                    Color.FromArgb(175, 160, 135),  // 奶油白
                    Color.FromArgb(115, 155, 175),  // 天空蓝
                    Color.FromArgb(170, 140, 120),  // 焦糖棕
                };
            }
        }

        public event EventHandler Changed;

        private void NotifyChanged()
        {
            if (Changed != null)
                Changed(this, EventArgs.Empty);
        }
    }
}
