using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NativeManagerDemo
{
    /// <summary>纯视觉布局，不属于集群事实，也不会改变资源语义。</summary>
    public class TopologyLayoutEntry
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public TopologyLayoutEntry()
        {
            Width = 0.0f;
            Height = 0.0f;
        }
    }

    public class TopologyLayoutStore
    {
        private readonly Dictionary<string, TopologyLayoutEntry> _entries;
        private readonly string _path;

        public float Zoom;
        public float ViewportX;
        public float ViewportY;

        public TopologyLayoutStore(string path)
        {
            _path = path == null ? "" : path;
            _entries = new Dictionary<string, TopologyLayoutEntry>(StringComparer.Ordinal);
            Zoom = 1.0f;
        }

        public int Count { get { return _entries.Count; } }

        public bool TryGet(string entityId, out TopologyLayoutEntry entry)
        {
            return _entries.TryGetValue(entityId, out entry);
        }

        public void Set(string entityId, float x, float y, float width, float height)
        {
            if (string.IsNullOrEmpty(entityId)) return;
            TopologyLayoutEntry entry;
            if (!_entries.TryGetValue(entityId, out entry))
            {
                entry = new TopologyLayoutEntry();
                _entries[entityId] = entry;
            }
            entry.X = x;
            entry.Y = y;
            entry.Width = width;
            entry.Height = height;
        }

        public void Remove(string entityId)
        {
            _entries.Remove(entityId);
        }

        public void Clear()
        {
            _entries.Clear();
        }

        public void Load()
        {
            _entries.Clear();
            if (_path.Length == 0 || !File.Exists(_path)) return;
            object root = MiniJson.Parse(File.ReadAllText(_path, Encoding.UTF8));
            IDictionary<string, object> items = MiniJson.Object(MiniJson.Get(root, "entries"));
            if (items == null) return;
            Zoom = (float)MiniJson.Int32(root, "zoomPercent", 100) / 100.0f;
            if (Zoom <= 0.05f) Zoom = 1.0f;
            ViewportX = MiniJson.Int32(root, "viewportX", 0);
            ViewportY = MiniJson.Int32(root, "viewportY", 0);
            foreach (KeyValuePair<string, object> pair in items)
            {
                TopologyLayoutEntry entry = new TopologyLayoutEntry();
                entry.X = MiniJson.Int32(pair.Value, "x", 0);
                entry.Y = MiniJson.Int32(pair.Value, "y", 0);
                entry.Width = MiniJson.Int32(pair.Value, "width", 0);
                entry.Height = MiniJson.Int32(pair.Value, "height", 0);
                _entries[pair.Key] = entry;
            }
        }

        public void Save()
        {
            if (_path.Length == 0) return;
            string directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["schemaVersion"] = 1;
            root["zoomPercent"] = (int)(Zoom * 100.0f);
            root["viewportX"] = (int)ViewportX;
            root["viewportY"] = (int)ViewportY;
            Dictionary<string, object> items = new Dictionary<string, object>();
            foreach (KeyValuePair<string, TopologyLayoutEntry> pair in _entries)
            {
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["x"] = (int)pair.Value.X;
                item["y"] = (int)pair.Value.Y;
                item["width"] = (int)pair.Value.Width;
                item["height"] = (int)pair.Value.Height;
                items[pair.Key] = item;
            }
            root["entries"] = items;
            File.WriteAllText(_path, MiniJson.Stringify(root), new UTF8Encoding(false));
        }
    }
}
