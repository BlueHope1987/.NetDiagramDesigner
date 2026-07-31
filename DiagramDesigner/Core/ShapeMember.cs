using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DiagramDesigner.Core
{
    public enum MemberType
    {
        Property,
        Method,
        Event,
        Constraint,
        Field
    }

    public enum Visibility
    {
        Public,
        Private,
        Protected,
        Internal
    }

    [Serializable]
    public class ShapeMember
    {
        private MemberType _memberType = MemberType.Property;
        private string _name = "";
        private string _typeName = "";
        private Visibility _visibility = Visibility.Public;
        private bool _isStatic = false;
        private bool _isAbstract = false;
        private string _defaultValue = "";
        private List<ShapeMemberParameter> _parameters = new List<ShapeMemberParameter>();

        [Category("成员")]
        [DisplayName("类型")]
        public MemberType MemberType
        {
            get { return _memberType; }
            set { _memberType = value; }
        }

        [Category("成员")]
        [DisplayName("名称")]
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        [Category("成员")]
        [DisplayName("数据类型")]
        public string TypeName
        {
            get { return _typeName; }
            set { _typeName = value; }
        }

        [Category("成员")]
        [DisplayName("可见性")]
        public Visibility Visibility
        {
            get { return _visibility; }
            set { _visibility = value; }
        }

        [Category("成员")]
        [DisplayName("静态")]
        public bool IsStatic
        {
            get { return _isStatic; }
            set { _isStatic = value; }
        }

        [Category("成员")]
        [DisplayName("抽象")]
        public bool IsAbstract
        {
            get { return _isAbstract; }
            set { _isAbstract = value; }
        }

        [Category("成员")]
        [DisplayName("默认值")]
        public string DefaultValue
        {
            get { return _defaultValue; }
            set { _defaultValue = value; }
        }

        [Browsable(false)]
        public List<ShapeMemberParameter> Parameters
        {
            get { return _parameters; }
            set { _parameters = value; }
        }

        public string GetSignature()
        {
            string vis = GetVisibilitySymbol();
            string modifier = "";
            if (_isStatic)
                modifier += " static";
            if (_isAbstract)
                modifier += " abstract";

            switch (_memberType)
            {
                case MemberType.Property:
                    return string.Format("{0}{1} {2} {3}", vis, modifier, _typeName, _name);
                case MemberType.Field:
                    return string.Format("{0}{1} {2} {3}", vis, modifier, _typeName, _name);
                case MemberType.Method:
                    return string.Format("{0}{1} {2} {3}({4})", vis, modifier, _typeName, _name, GetParametersText());
                case MemberType.Event:
                    return string.Format("{0}{1} event {2} {3}", vis, modifier, _typeName, _name);
                case MemberType.Constraint:
                    return string.Format("[{0}]", _name);
                default:
                    return _name;
            }
        }

        private string GetVisibilitySymbol()
        {
            switch (_visibility)
            {
                case Visibility.Public: return "+";
                case Visibility.Private: return "-";
                case Visibility.Protected: return "#";
                case Visibility.Internal: return "~";
                default: return "+";
            }
        }

        private string GetParametersText()
        {
            if (_parameters == null || _parameters.Count == 0)
                return "";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < _parameters.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(_parameters[i].ToString());
            }
            return sb.ToString();
        }
    }
}
