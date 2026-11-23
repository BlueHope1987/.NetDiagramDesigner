using System;
using System.Collections.Generic;
using System.Drawing;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    /// <summary>RunStatus → 状态键；颜色统一由 ResourceVisualPalette 决定。</summary>
    public class StatusVisualMap : IStatusVisualMap
    {
        public string IconKeyOf(ManagedEntity entity) { return VerbMatrix.KindName(entity.Kind); }
        public string VisualStateOf(ManagedEntity entity) { return entity.Status.ToString(); }
    }

    /// <summary>把唯一事实源 TopologySnapshot 全量投影为图形，并维护稳定 Id 双向映射。</summary>
    public class TopologyProjector
    {
        private readonly ITopologyHost _host;
        private readonly ITopologyLayout _layout;
        private readonly IStatusVisualMap _visualMap;
        private readonly Dictionary<Guid, string> _shapeToEntity = new Dictionary<Guid, string>();
        private readonly Dictionary<string, Guid> _entityToShape = new Dictionary<string, Guid>(StringComparer.Ordinal);
        private readonly Dictionary<Guid, string> _connectionToRelation = new Dictionary<Guid, string>();
        private readonly Dictionary<string, ManagedEntity> _entitiesById = new Dictionary<string, ManagedEntity>(StringComparer.Ordinal);
        private TopologySnapshot _current;
        private TopologyLayoutStore _layoutStore;
        private bool _rendering;
        private bool _hasRendered;

        public TopologyProjector(ITopologyHost host)
            : this(host, new LayeredTopologyLayout(), new StatusVisualMap()) { }

        public TopologyProjector(ITopologyHost host, ITopologyLayout layout, IStatusVisualMap visualMap)
        {
            if (host == null) throw new ArgumentNullException("host");
            if (layout == null) throw new ArgumentNullException("layout");
            if (visualMap == null) throw new ArgumentNullException("visualMap");
            _host = host;
            _layout = layout;
            _visualMap = visualMap;
        }

        public TopologySnapshot Current { get { return _current; } }
        public bool IsRendering { get { return _rendering; } }

        public void AttachLayoutStore(TopologyLayoutStore store)
        {
            _layoutStore = store;
        }

        public void Render(TopologySnapshot snapshot)
        {
            if (snapshot == null) return;
            string selectedEntity = null;
            ManagedEntity selected = GetPrimarySelected();
            if (selected != null) selectedEntity = selected.Id;
            float oldZoom = _host.GetZoom();
            PointF oldOffset = _host.GetViewportOffset();
            if (_current != null) CaptureLayout();

            _rendering = true;
            try
            {
                using (_host.SuppressEvents())
                using (_host.BeginApplyExternalChange())
                {
                    _current = snapshot;
                    _shapeToEntity.Clear();
                    _entityToShape.Clear();
                    _connectionToRelation.Clear();
                    _entitiesById.Clear();
                    int i;
                    for (i = 0; i < snapshot.Entities.Count; i++) _entitiesById[snapshot.Entities[i].Id] = snapshot.Entities[i];
                    _host.NewDocument();
                    IList<ViewNode> nodes = _layout.Arrange(snapshot);
                    for (i = 0; i < nodes.Count; i++)
                    {
                        ViewNode node = nodes[i];
                        ManagedEntity entity;
                        if (!_entitiesById.TryGetValue(node.RefId, out entity)) continue;
                        node.IconKey = _visualMap.IconKeyOf(entity);
                        node.VisualState = _visualMap.VisualStateOf(entity);
                        TopologyLayoutEntry saved;
                        if (_layoutStore != null && _layoutStore.TryGet(entity.Id, out saved))
                        {
                            node.X = saved.X;
                            node.Y = saved.Y;
                            if (saved.Width > 0.0f) node.Width = saved.Width;
                            if (saved.Height > 0.0f) node.Height = saved.Height;
                        }
                        ShapeInfo shape = _host.AddShape(K8sShapes.ShapeNameOf(entity.Kind), entity.Name, node.X, node.Y);
                        if (shape == null) continue;
                        _host.SetShapeBounds(shape.Id, new RectangleF(node.X, node.Y, node.Width, node.Height));
                        _host.ConfigureResourceStates(shape.Id, entity.Kind, entity.Status.ToString());
                        _shapeToEntity[shape.Id] = entity.Id;
                        _entityToShape[entity.Id] = shape.Id;
                    }

                    for (i = 0; i < snapshot.Entities.Count; i++)
                    {
                        ManagedEntity entity = snapshot.Entities[i];
                        if (string.IsNullOrEmpty(entity.ParentId)) continue;
                        Guid childShape, parentShape;
                        if (_entityToShape.TryGetValue(entity.Id, out childShape) &&
                            _entityToShape.TryGetValue(entity.ParentId, out parentShape))
                            _host.SetContainer(childShape, parentShape);
                    }

                    if (snapshot.Relations != null)
                    {
                        for (i = 0; i < snapshot.Relations.Count; i++)
                        {
                            Relation relation = snapshot.Relations[i];
                            if (relation == null) continue;
                            Guid fromShape, toShape;
                            if (!_entityToShape.TryGetValue(relation.FromId, out fromShape)) continue;
                            if (!_entityToShape.TryGetValue(relation.ToId, out toShape)) continue;
                            string label = string.IsNullOrEmpty(relation.Protocol) ? RelationLabel(relation.Kind)
                                : RelationLabel(relation.Kind) + " / " + relation.Protocol;
                            Guid connection = _host.ConnectById(fromShape, toShape, label);
                            if (connection != Guid.Empty) _connectionToRelation[connection] = relation.Id;
                        }
                    }
                }
            }
            finally
            {
                _rendering = false;
            }

            if (!string.IsNullOrEmpty(selectedEntity))
            {
                Guid shape;
                if (_entityToShape.TryGetValue(selectedEntity, out shape)) _host.SelectById(shape);
            }
            if (_hasRendered) _host.SetViewport(oldZoom, oldOffset);
            else if (_layoutStore != null && _layoutStore.Count > 0)
                _host.SetViewport(_layoutStore.Zoom, new PointF(_layoutStore.ViewportX, _layoutStore.ViewportY));
            else _host.FitView();
            _hasRendered = true;
            CaptureLayout();
        }

        public void CaptureLayout()
        {
            if (_layoutStore == null) return;
            IList<ShapeInfo> shapes = _host.ListShapes();
            int i;
            for (i = 0; i < shapes.Count; i++)
            {
                string entityId;
                if (_shapeToEntity.TryGetValue(shapes[i].Id, out entityId))
                    _layoutStore.Set(entityId, shapes[i].X, shapes[i].Y, shapes[i].Width, shapes[i].Height);
            }
            _layoutStore.Zoom = _host.GetZoom();
            PointF offset = _host.GetViewportOffset();
            _layoutStore.ViewportX = offset.X;
            _layoutStore.ViewportY = offset.Y;
        }

        public void SaveLayout()
        {
            CaptureLayout();
            if (_layoutStore != null) _layoutStore.Save();
        }

        public bool SelectEntity(string entityId)
        {
            Guid shape;
            return _entityToShape.TryGetValue(entityId, out shape) && _host.SelectById(shape);
        }

        public Guid GetShapeForEntity(string entityId)
        {
            Guid shape;
            return _entityToShape.TryGetValue(entityId, out shape) ? shape : Guid.Empty;
        }

        public string GetEntityIdForShape(Guid shapeId)
        {
            string entityId;
            return _shapeToEntity.TryGetValue(shapeId, out entityId) ? entityId : null;
        }

        public string GetRelationIdForConnection(Guid connectionId)
        {
            string relationId;
            return _connectionToRelation.TryGetValue(connectionId, out relationId) ? relationId : null;
        }

        public ManagedEntity GetEntityForShape(Guid shapeId)
        {
            string entityId = GetEntityIdForShape(shapeId);
            ManagedEntity entity;
            if (entityId != null && _entitiesById.TryGetValue(entityId, out entity)) return entity;
            return null;
        }

        public IList<ManagedEntity> GetSelectedEntities()
        {
            List<ManagedEntity> list = new List<ManagedEntity>();
            IList<ShapeInfo> selection = _host.ListSelection();
            int i;
            for (i = 0; i < selection.Count; i++)
            {
                ManagedEntity entity = GetEntityForShape(selection[i].Id);
                if (entity != null) list.Add(entity);
            }
            return list;
        }

        public ManagedEntity GetPrimarySelected()
        {
            IList<ManagedEntity> selection = GetSelectedEntities();
            return selection.Count > 0 ? selection[0] : null;
        }

        private static string RelationLabel(RelationKind kind)
        {
            switch (kind)
            {
                case RelationKind.Calls: return "调用";
                case RelationKind.Reads: return "读取";
                case RelationKind.Writes: return "写入";
                case RelationKind.Publishes: return "发布";
                case RelationKind.Subscribes: return "订阅";
                case RelationKind.Depends: return "依赖";
                case RelationKind.Owns: return "拥有";
                case RelationKind.Selects: return "选择";
                case RelationKind.Routes: return "路由";
                case RelationKind.Manages: return "管理";
                default: return "关系";
            }
        }
    }
}
