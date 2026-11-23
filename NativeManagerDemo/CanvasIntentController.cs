using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DiagramDesigner.Controls;
using DiagramDesigner.Core;
using DiagramDesigner.Shapes;
using NativeManagerDemo.Contract;

namespace NativeManagerDemo
{
    public enum ManagerMode { Checking, DesignDebug, Online }

    public class CanvasIntentEventArgs : EventArgs
    {
        public bool Success;
        public string Message;
    }

    /// <summary>把画布手势解释为设计意图；在线模式下的语义编辑会被还原。</summary>
    public class CanvasIntentController
    {
        private readonly DiagramEditor _editor;
        private readonly EditorTopologyHost _host;
        private readonly TopologyProjector _projector;
        private readonly DesignTopologyStore _design;
        private bool _renderPending;
        private string _selectAfterRender;
        private ManagerMode _mode;

        public event EventHandler<CanvasIntentEventArgs> IntentHandled;
        public event EventHandler Changed;

        public CanvasIntentController(DiagramEditor editor, EditorTopologyHost host,
            TopologyProjector projector, DesignTopologyStore design)
        {
            if (editor == null) throw new ArgumentNullException("editor");
            if (host == null) throw new ArgumentNullException("host");
            if (projector == null) throw new ArgumentNullException("projector");
            if (design == null) throw new ArgumentNullException("design");
            _editor = editor;
            _host = host;
            _projector = projector;
            _design = design;
            _mode = ManagerMode.Checking;
            _selectAfterRender = "";
            _editor.ShapeAdded += new EventHandler<ShapeEventArgs>(OnShapeAdded);
            _editor.ShapeDeleted += new EventHandler<ShapeEventArgs>(OnShapeDeleted);
            _editor.ConnectionAdded += new EventHandler<ConnectionEventArgs>(OnConnectionAdded);
            _editor.ConnectionDeleted += new EventHandler<ConnectionEventArgs>(OnConnectionDeleted);
            _editor.DocumentCommitted += new EventHandler<ChangeSet>(OnDocumentCommitted);
            _editor.ShapeChanged += new EventHandler<ChangeSet>(OnShapeChanged);
        }

        public ManagerMode Mode
        {
            get { return _mode; }
            set { _mode = value; }
        }

        private bool IgnoreEvents
        {
            get { return _projector.IsRendering || _editor.IsApplyingExternalChange; }
        }

        private void OnShapeAdded(object sender, ShapeEventArgs args)
        {
            if (IgnoreEvents || args.Shape == null) return;
            if (_mode != ManagerMode.DesignDebug)
            {
                Reject("在线模式只允许选择与布局；新增对象请切换到设计·调试模式");
                return;
            }
            EntityKind kind = KindOf(args.Shape);
            if (kind == EntityKind.Unknown || kind == EntityKind.Cluster)
            {
                Reject("该工具不是可管理的云原生资源");
                return;
            }
            string namespaceName = "";
            ContainerShape parent = args.Shape.Parent as ContainerShape;
            if (parent != null)
            {
                ManagedEntity ns = _projector.GetEntityForShape(parent.Id);
                if (ns != null && ns.Kind == EntityKind.Namespace) namespaceName = ns.Name;
            }
            string name = UniqueName(kind);
            DesignEditResult result = _design.AddEntity(kind, name, namespaceName);
            if (result.Success) _selectAfterRender = result.EntityId;
            Notify(result);
            ScheduleRender();
        }

        private void OnShapeDeleted(object sender, ShapeEventArgs args)
        {
            if (IgnoreEvents || args.Shape == null) return;
            if (_mode != ManagerMode.DesignDebug)
            {
                Reject("在线模式不会删除集群资源；删除请使用右侧操作并确认");
                return;
            }
            string entityId = _projector.GetEntityIdForShape(args.Shape.Id);
            if (entityId == null) { ScheduleRender(); return; }
            DesignEditResult result = _design.DeleteEntity(entityId);
            Notify(result);
            ScheduleRender();
        }

        private void OnConnectionAdded(object sender, ConnectionEventArgs args)
        {
            if (IgnoreEvents || args.Connection == null) return;
            if (_mode != ManagerMode.DesignDebug)
            {
                Reject("在线模式不允许新建语义连线");
                return;
            }
            ManagedEntity from = _projector.GetEntityForShape(args.Connection.FromShape.Id);
            ManagedEntity to = _projector.GetEntityForShape(args.Connection.ToShape.Id);
            RelationKind kind = InferRelation(from, to);
            DesignEditResult result = _design.AddRelation(from == null ? "" : from.Id, to == null ? "" : to.Id, kind);
            Notify(result);
            ScheduleRender();
        }

        private void OnConnectionDeleted(object sender, ConnectionEventArgs args)
        {
            if (IgnoreEvents || args.Connection == null) return;
            if (_mode != ManagerMode.DesignDebug)
            {
                Reject("在线模式不允许删除语义连线");
                return;
            }
            string relationId = _projector.GetRelationIdForConnection(args.Connection.Id);
            DesignEditResult result = relationId == null
                ? new DesignEditResult(false, "未找到对应的设计关系")
                : _design.RemoveRelation(relationId);
            Notify(result);
            ScheduleRender();
        }

        private void OnShapeChanged(object sender, ChangeSet args)
        {
            if (IgnoreEvents || args == null || args.Kind != ChangeKind.ShapePropertyChanged) return;
            if (args.PropertyName != "Name") return;
            string entityId = _projector.GetEntityIdForShape(args.ShapeId);
            if (entityId == null) return;
            if (_mode != ManagerMode.DesignDebug)
            {
                Reject("在线模式不能直接重命名集群资源");
                return;
            }
            DesignEditResult result = _design.RenameEntity(entityId, Convert.ToString(args.NewValue));
            Notify(result);
            ScheduleRender();
        }

        private void OnDocumentCommitted(object sender, ChangeSet args)
        {
            if (IgnoreEvents) return;
            bool semanticChange = false;
            bool rejected = false;
            IList<ShapeInfo> shapes = _host.ListShapes();
            int i;
            for (i = 0; i < shapes.Count; i++)
            {
                ShapeInfo shape = shapes[i];
                ManagedEntity entity = _projector.GetEntityForShape(shape.Id);
                if (entity == null) continue;
                Guid containerShape = _host.GetContainer(shape.Id);
                string actualParentId = "";
                if (containerShape != Guid.Empty)
                {
                    ManagedEntity parent = _projector.GetEntityForShape(containerShape);
                    if (parent != null && parent.Kind == EntityKind.Namespace) actualParentId = parent.Id;
                }
                string expectedParentId = entity.ParentId == null ? "" : entity.ParentId;
                if (actualParentId == expectedParentId) continue;
                if (_mode != ManagerMode.DesignDebug)
                {
                    rejected = true;
                    continue;
                }
                if (actualParentId.Length == 0 || !DesignTopologyStore.IsNamespaced(entity.Kind))
                {
                    rejected = true;
                    continue;
                }
                DesignEditResult result = _design.MoveEntity(entity.Id, actualParentId);
                if (result.Success) semanticChange = true;
                else rejected = true;
                Notify(result);
            }
            if (rejected)
            {
                Notify(new DesignEditResult(false, "非法容器归属已还原；namespaced 对象必须完整拖入另一个 Namespace"));
                ScheduleRender();
            }
            else if (semanticChange) ScheduleRender();
            else
            {
                try { _projector.SaveLayout(); }
                catch (Exception ex) { Notify(new DesignEditResult(false, "布局保存失败: " + ex.Message)); }
            }
        }

        public static RelationKind InferRelation(ManagedEntity from, ManagedEntity to)
        {
            if (from == null || to == null) return RelationKind.Unknown;
            if (from.Kind == EntityKind.Cluster && to.Kind == EntityKind.Namespace) return RelationKind.Owns;
            if (from.Kind == EntityKind.Service && to.Kind == EntityKind.Pod) return RelationKind.Selects;
            if (from.Kind == EntityKind.Ingress && to.Kind == EntityKind.Service) return RelationKind.Routes;
            if (from.Kind == EntityKind.VirtualService && to.Kind == EntityKind.Service) return RelationKind.Routes;
            if (from.Kind == EntityKind.VirtualService || from.Kind == EntityKind.DestinationRule) return RelationKind.Depends;
            if (from.Kind == EntityKind.HelmRelease) return RelationKind.Manages;
            if (to.Kind == EntityKind.ConfigMap || to.Kind == EntityKind.Secret || to.Kind == EntityKind.PersistentVolumeClaim)
                return RelationKind.Reads;
            if (to.Kind == EntityKind.Pod) return RelationKind.Owns;
            return RelationKind.Depends;
        }

        private EntityKind KindOf(ShapeBase shape)
        {
            if (shape is ContainerShape) return EntityKind.Namespace;
            GenericShape generic = shape as GenericShape;
            if (generic == null) return EntityKind.Unknown;
            string name = generic.ShapeTypeName;
            if (name == K8sShapes.Node) return EntityKind.Node;
            if (name == K8sShapes.Deployment) return EntityKind.Deployment;
            if (name == K8sShapes.ReplicaSet) return EntityKind.ReplicaSet;
            if (name == K8sShapes.StatefulSet) return EntityKind.StatefulSet;
            if (name == K8sShapes.DaemonSet) return EntityKind.DaemonSet;
            if (name == K8sShapes.Pod) return EntityKind.Pod;
            if (name == K8sShapes.Service) return EntityKind.Service;
            if (name == K8sShapes.Ingress) return EntityKind.Ingress;
            if (name == K8sShapes.ConfigMap) return EntityKind.ConfigMap;
            if (name == K8sShapes.Secret) return EntityKind.Secret;
            if (name == K8sShapes.PersistentVolume) return EntityKind.PersistentVolume;
            if (name == K8sShapes.PersistentVolumeClaim) return EntityKind.PersistentVolumeClaim;
            if (name == K8sShapes.Job) return EntityKind.Job;
            if (name == K8sShapes.CronJob) return EntityKind.CronJob;
            if (name == K8sShapes.Etcd) return EntityKind.Etcd;
            if (name == K8sShapes.HelmRelease) return EntityKind.HelmRelease;
            if (name == K8sShapes.HelmChart) return EntityKind.HelmChart;
            if (name == K8sShapes.IstioGateway) return EntityKind.IstioGateway;
            if (name == K8sShapes.VirtualService) return EntityKind.VirtualService;
            if (name == K8sShapes.DestinationRule) return EntityKind.DestinationRule;
            if (name == K8sShapes.YamlDocument) return EntityKind.YamlDocument;
            return EntityKind.Unknown;
        }

        private string UniqueName(EntityKind kind)
        {
            string baseName = VerbMatrix.KindName(kind).ToLowerInvariant();
            if (baseName == "unknown") baseName = "resource";
            TopologySnapshot snapshot = _design.QueryTopology();
            int suffix = snapshot.Entities.Count + 1;
            string candidate = baseName + "-" + suffix.ToString();
            bool exists = true;
            while (exists)
            {
                exists = false;
                int i;
                for (i = 0; i < snapshot.Entities.Count; i++)
                    if (snapshot.Entities[i].Name == candidate) { exists = true; break; }
                if (exists) { suffix++; candidate = baseName + "-" + suffix.ToString(); }
            }
            return candidate;
        }

        private void Reject(string message)
        {
            Notify(new DesignEditResult(false, message));
            ScheduleRender();
        }

        private void Notify(DesignEditResult result)
        {
            if (IntentHandled == null) return;
            CanvasIntentEventArgs args = new CanvasIntentEventArgs();
            args.Success = result.Success;
            args.Message = result.Message;
            IntentHandled(this, args);
        }

        private void ScheduleRender()
        {
            if (_renderPending) return;
            _renderPending = true;
            try { _editor.BeginInvoke(new MethodInvoker(RenderScheduled)); }
            catch
            {
                _renderPending = false;
                RenderScheduled();
            }
        }

        private void RenderScheduled()
        {
            _renderPending = false;
            TopologySnapshot snapshot = _mode == ManagerMode.DesignDebug ? _design.QueryTopology() : _projector.Current;
            if (snapshot != null) _projector.Render(snapshot);
            if (_selectAfterRender.Length > 0)
            {
                _projector.SelectEntity(_selectAfterRender);
                _selectAfterRender = "";
            }
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
    }
}
