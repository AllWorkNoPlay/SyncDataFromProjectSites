using System.Globalization;

namespace ContentTypesSyncSPToDB.Model
{
    public class Property
    {
        private string _value;

        public enum TypeEnum { String, Number, Currency, DateTime };

        public string Name { get; set; }

        public string Value

        {
            get
            {
                switch (Type)
                {
                    case TypeEnum.Number:
                    case TypeEnum.Currency:
                        return string.IsNullOrEmpty(_value) ? "0" : double.Parse(_value.ToString()).ToString(new CultureInfo("en-IE"));
                    default:
                        return _value ?? string.Empty;
                }
            }
            set { _value = value; }
        }

        public TypeEnum Type { get; set; }
    }
}
