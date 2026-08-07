using System;
using System.Collections.Generic;

namespace DiagramDesigner.Core
{
    public class ShapeTypeRegistry
    {
        private static ShapeTypeRegistry _instance = new ShapeTypeRegistry();
        public static ShapeTypeRegistry Instance { get { return _instance; } }

        private Dictionary<string, ShapeType> _types = new Dictionary<string, ShapeType>();

        private ShapeTypeRegistry() { }

        /// <summary>
        /// 注册图形类型。自动调用 EnsureDefaultZones 确保每个图形
        /// 至少拥有一个默认标题 Zone。
        /// </summary>
        public void Register(ShapeType shapeType)
        {
            if (shapeType == null || shapeType.Name.Length == 0)
                return;
            shapeType.EnsureDefaultZones();
            _types[shapeType.Name] = shapeType;
        }

        public void Unregister(string name)
        {
            if (_types.ContainsKey(name))
                _types.Remove(name);
        }

        public ShapeType GetShapeType(string name)
        {
            if (_types.ContainsKey(name))
                return _types[name];
            return null;
        }

        public List<ShapeType> GetAllTypes()
        {
            return new List<ShapeType>(_types.Values);
        }

        public List<string> GetCategories()
        {
            List<string> categories = new List<string>();
            foreach (ShapeType type in _types.Values)
            {
                if (!categories.Contains(type.Category))
                    categories.Add(type.Category);
            }
            return categories;
        }

        public List<ShapeType> GetTypesByCategory(string category)
        {
            List<ShapeType> result = new List<ShapeType>();
            foreach (ShapeType type in _types.Values)
            {
                if (type.Category == category)
                    result.Add(type);
            }
            return result;
        }

        public void Clear()
        {
            _types.Clear();
        }

        public bool Contains(string name)
        {
            return _types.ContainsKey(name);
        }
    }
}
