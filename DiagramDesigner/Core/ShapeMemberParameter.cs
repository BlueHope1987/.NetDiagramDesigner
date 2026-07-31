using System;

namespace DiagramDesigner.Core
{
    [Serializable]
    public class ShapeMemberParameter
    {
        private string _name = "";
        private string _typeName = "";
        private string _defaultValue = "";

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public string TypeName
        {
            get { return _typeName; }
            set { _typeName = value; }
        }

        public string DefaultValue
        {
            get { return _defaultValue; }
            set { _defaultValue = value; }
        }

        public override string ToString()
        {
            if (_defaultValue.Length > 0)
                return _typeName + " " + _name + " = " + _defaultValue;
            return _typeName + " " + _name;
        }
    }
}
