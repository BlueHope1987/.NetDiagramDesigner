using System;
using System.Collections.Generic;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    /// <summary>Namespace 分组布局：容器内网格排列子对象，集群级对象放在左侧独立区域。</summary>
    public class LayeredTopologyLayout : ITopologyLayout
    {
        private const float ChildGapX = 160.0f;
        private const float ChildGapY = 120.0f;

        public IList<ViewNode> Arrange(TopologySnapshot snapshot)
        {
            List<ViewNode> result = new List<ViewNode>();
            if (snapshot == null || snapshot.Entities == null) return result;

            Dictionary<string, ManagedEntity> namespacesById = new Dictionary<string, ManagedEntity>();
            Dictionary<string, ManagedEntity> namespacesByName = new Dictionary<string, ManagedEntity>();
            Dictionary<string, List<ManagedEntity>> children = new Dictionary<string, List<ManagedEntity>>();
            List<ManagedEntity> roots = new List<ManagedEntity>();
            List<ManagedEntity> namespaces = new List<ManagedEntity>();
            List<ManagedEntity> unassigned = new List<ManagedEntity>();
            int i;
            for (i = 0; i < snapshot.Entities.Count; i++)
            {
                ManagedEntity entity = snapshot.Entities[i];
                if (entity.Kind == EntityKind.Namespace)
                {
                    namespaces.Add(entity);
                    namespacesById[entity.Id] = entity;
                    if (!namespacesByName.ContainsKey(entity.Name)) namespacesByName[entity.Name] = entity;
                }
            }
            for (i = 0; i < snapshot.Entities.Count; i++)
            {
                ManagedEntity entity = snapshot.Entities[i];
                if (entity.Kind == EntityKind.Namespace) continue;
                ManagedEntity parent = null;
                if (!string.IsNullOrEmpty(entity.ParentId)) namespacesById.TryGetValue(entity.ParentId, out parent);
                if (parent == null && DesignTopologyStore.IsNamespaced(entity.Kind) && !string.IsNullOrEmpty(entity.Namespace))
                    namespacesByName.TryGetValue(entity.Namespace, out parent);
                if (parent != null)
                {
                    List<ManagedEntity> items;
                    if (!children.TryGetValue(parent.Id, out items))
                    {
                        items = new List<ManagedEntity>();
                        children[parent.Id] = items;
                    }
                    items.Add(entity);
                }
                else if (!DesignTopologyStore.IsNamespaced(entity.Kind)) roots.Add(entity);
                else unassigned.Add(entity);
            }

            float rootY = 40.0f;
            for (i = 0; i < roots.Count; i++)
            {
                ManagedEntity entity = roots[i];
                ViewNode node = Node(entity, 40.0f, rootY);
                result.Add(node);
                rootY += Math.Max(node.Height + 28.0f, 105.0f);
            }

            float namespaceY = 40.0f;
            for (i = 0; i < namespaces.Count; i++)
            {
                ManagedEntity ns = namespaces[i];
                List<ManagedEntity> items;
                if (!children.TryGetValue(ns.Id, out items)) items = new List<ManagedEntity>();
                int columns = items.Count <= 1 ? 1 : (items.Count < 6 ? 2 : 3);
                int rows = items.Count == 0 ? 1 : (items.Count + columns - 1) / columns;
                float width = Math.Max(330.0f, columns * ChildGapX + 36.0f);
                float height = Math.Max(200.0f, rows * ChildGapY + 76.0f);
                ViewNode container = Node(ns, 340.0f, namespaceY);
                container.Width = width;
                container.Height = height;
                result.Add(container);
                int childIndex;
                for (childIndex = 0; childIndex < items.Count; childIndex++)
                {
                    int column = childIndex % columns;
                    int row = childIndex / columns;
                    ViewNode child = Node(items[childIndex],
                        container.X + 18.0f + column * ChildGapX,
                        container.Y + 48.0f + row * ChildGapY);
                    result.Add(child);
                }
                namespaceY += height + 42.0f;
            }

            for (i = 0; i < unassigned.Count; i++)
            {
                result.Add(Node(unassigned[i], 340.0f + (i % 3) * ChildGapX, namespaceY + (i / 3) * ChildGapY));
            }
            return result;
        }

        private static ViewNode Node(ManagedEntity entity, float x, float y)
        {
            ViewNode node = new ViewNode();
            node.RefId = entity.Id;
            node.Label = entity.Name;
            node.IconKey = VerbMatrix.KindName(entity.Kind);
            node.VisualState = entity.Status.ToString();
            node.X = x;
            node.Y = y;
            node.Width = 138.0f;
            node.Height = 92.0f;
            if (entity.Kind == EntityKind.Cluster) { node.Width = 170.0f; node.Height = 118.0f; }
            else if (entity.Kind == EntityKind.Node) { node.Width = 100.0f; node.Height = 138.0f; }
            else if (entity.Kind == EntityKind.Namespace) { node.Width = 480.0f; node.Height = 240.0f; }
            else if (entity.Kind == EntityKind.YamlDocument) { node.Width = 150.0f; node.Height = 105.0f; }
            return node;
        }
    }
}
