using System;
using System.Collections.Generic;
using System.Drawing;

namespace DiagramDesigner.Core
{
    // 注：BooleanOperation 枚举现统一定义在 RenderCommand.cs 中
    // （成员：None, Union, Subtract, Intersect, Xor），此处不再重复定义。

    /// <summary>
    /// 图形组合器。支持将多个内置或自定义图形组合为新的复合图形。
    /// 注：当前实现为视觉层叠加组合；真正的几何布尔运算需要多边形裁剪算法支持。
    /// </summary>
    public static class ShapeComposer
    {
        /// <summary>
        /// 将多个图形叠加组合为一个新图形（并集效果）
        /// </summary>
        public static ShapeType Union(string name, string category, params ShapeType[] shapes)
        {
            return Combine(name, category, BooleanOperation.Union, shapes);
        }

        /// <summary>
        /// 将基础图形与裁剪图形组合（差集效果：基础图形减去裁剪图形）
        /// </summary>
        public static ShapeType Subtract(string name, string category, ShapeType baseShape, ShapeType cutShape)
        {
            return Combine(name, category, BooleanOperation.Subtract, baseShape, cutShape);
        }

        /// <summary>
        /// 取多个图形的交集（视觉模拟：后续图形作为前景叠加）
        /// </summary>
        public static ShapeType Intersect(string name, string category, params ShapeType[] shapes)
        {
            return Combine(name, category, BooleanOperation.Intersect, shapes);
        }

        /// <summary>
        /// 通用组合方法
        /// </summary>
        public static ShapeType Combine(string name, string category, BooleanOperation operation, params ShapeType[] shapes)
        {
            if (shapes == null || shapes.Length == 0)
                throw new ArgumentException("至少需要一个图形", nameof(shapes));

            ShapeType result = new ShapeType();
            result.Name = name;
            result.Category = category;
            result.Description = string.Format("组合图形 ({0})", operation);

            // 继承第一个图形的尺寸和颜色作为基准
            ShapeType first = shapes[0];
            result.DefaultWidth = first.DefaultWidth;
            result.DefaultHeight = first.DefaultHeight;
            result.DefaultFillColor = first.DefaultFillColor;
            result.DefaultBorderColor = first.DefaultBorderColor;
            result.DefaultTextColor = first.DefaultTextColor;
            result.SupportsMembers = first.SupportsMembers;
            result.IsContainer = first.IsContainer;

            // 复制第一个图形的渲染命令
            foreach (RenderCommand cmd in first.RenderCommands)
            {
                result.RenderCommands.Add(CloneCommand(cmd));
            }

            // 处理后续图形
            for (int i = 1; i < shapes.Length; i++)
            {
                ShapeType overlay = shapes[i];
                foreach (RenderCommand cmd in overlay.RenderCommands)
                {
                    RenderCommand cloned = CloneCommand(cmd);
                    ApplyOperation(cloned, operation, i);
                    result.RenderCommands.Add(cloned);
                }
            }

            return result;
        }

        /// <summary>
        /// 将多个图形水平并排组合为一个新图形
        /// </summary>
        public static ShapeType HorizontalGroup(string name, string category, params ShapeType[] shapes)
        {
            if (shapes == null || shapes.Length == 0)
                return null;

            ShapeType result = new ShapeType();
            result.Name = name;
            result.Category = category;
            result.Description = "水平组合";

            float totalWidth = 0;
            float maxHeight = 0;
            foreach (ShapeType s in shapes)
            {
                totalWidth += s.DefaultWidth;
                maxHeight = Math.Max(maxHeight, s.DefaultHeight);
            }
            result.DefaultWidth = totalWidth;
            result.DefaultHeight = maxHeight;

            float offsetX = 0;
            foreach (ShapeType s in shapes)
            {
                float scaleX = s.DefaultWidth / totalWidth;
                float scaleY = s.DefaultHeight / maxHeight;
                float xRatio = offsetX / totalWidth;

                foreach (RenderCommand cmd in s.RenderCommands)
                {
                    RenderCommand cloned = CloneCommand(cmd);
                    cloned.X = xRatio + cmd.X * scaleX;
                    cloned.Width = cmd.Width * scaleX;
                    cloned.Y = cmd.Y * scaleY;
                    cloned.Height = cmd.Height * scaleY;
                    result.RenderCommands.Add(cloned);
                }
                offsetX += s.DefaultWidth;
            }

            return result;
        }

        /// <summary>
        /// 将多个图形垂直堆叠组合为一个新图形
        /// </summary>
        public static ShapeType VerticalGroup(string name, string category, params ShapeType[] shapes)
        {
            if (shapes == null || shapes.Length == 0)
                return null;

            ShapeType result = new ShapeType();
            result.Name = name;
            result.Category = category;
            result.Description = "垂直组合";

            float maxWidth = 0;
            float totalHeight = 0;
            foreach (ShapeType s in shapes)
            {
                maxWidth = Math.Max(maxWidth, s.DefaultWidth);
                totalHeight += s.DefaultHeight;
            }
            result.DefaultWidth = maxWidth;
            result.DefaultHeight = totalHeight;

            float offsetY = 0;
            foreach (ShapeType s in shapes)
            {
                float scaleX = s.DefaultWidth / maxWidth;
                float scaleY = s.DefaultHeight / totalHeight;
                float yRatio = offsetY / totalHeight;

                foreach (RenderCommand cmd in s.RenderCommands)
                {
                    RenderCommand cloned = CloneCommand(cmd);
                    cloned.X = cmd.X * scaleX;
                    cloned.Width = cmd.Width * scaleX;
                    cloned.Y = yRatio + cmd.Y * scaleY;
                    cloned.Height = cmd.Height * scaleY;
                    result.RenderCommands.Add(cloned);
                }
                offsetY += s.DefaultHeight;
            }

            return result;
        }

        /// <summary>
        /// 为现有图形添加装饰元素（如边框、角标、内部图标等）
        /// </summary>
        public static ShapeType Decorate(string name, ShapeType baseShape, params RenderCommand[] decorations)
        {
            ShapeType result = new ShapeType();
            result.Name = name;
            result.Category = baseShape.Category;
            result.Description = baseShape.Description;
            result.DefaultWidth = baseShape.DefaultWidth;
            result.DefaultHeight = baseShape.DefaultHeight;
            result.DefaultFillColor = baseShape.DefaultFillColor;
            result.DefaultBorderColor = baseShape.DefaultBorderColor;
            result.DefaultTextColor = baseShape.DefaultTextColor;
            result.SupportsMembers = baseShape.SupportsMembers;
            result.IsContainer = baseShape.IsContainer;

            foreach (RenderCommand cmd in baseShape.RenderCommands)
                result.RenderCommands.Add(CloneCommand(cmd));

            if (decorations != null)
            {
                foreach (RenderCommand dec in decorations)
                    result.RenderCommands.Add(CloneCommand(dec));
            }

            return result;
        }

        #region 内部辅助

        private static RenderCommand CloneCommand(RenderCommand source)
        {
            return source.Clone();
        }

        private static void ApplyOperation(RenderCommand cmd, BooleanOperation op, int layerIndex)
        {
            switch (op)
            {
                case BooleanOperation.Subtract:
                    // 差集：裁剪图形以半透明方式显示，便于区分
                    if (layerIndex > 0)
                    {
                        cmd.UseShapeColors = false;
                        Color original = cmd.FillColor.ToColor();
                        cmd.FillColor = new XmlColor(Color.FromArgb(80, original));
                        cmd.Stroke = true;
                    }
                    break;

                case BooleanOperation.Intersect:
                    // 交集：非首层图形仅描边不填充，模拟轮廓交叠
                    if (layerIndex > 0)
                    {
                        cmd.Fill = false;
                        cmd.Stroke = true;
                        cmd.StrokeWidth = 2f;
                    }
                    break;

                case BooleanOperation.Xor:
                    // 异或：交替填充/描边
                    if (layerIndex % 2 == 1)
                    {
                        cmd.Fill = !cmd.Fill;
                    }
                    break;

                case BooleanOperation.Union:
                default:
                    // 并集：默认叠加，不做特殊处理
                    break;
            }
        }

        #endregion
    }
}
