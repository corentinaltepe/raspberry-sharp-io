using System;
using System.Collections.Generic;
using System.Text;

namespace Raspberry
{
    public static class DictionaryExtensionsMethod
    {
        public static string DictToString<T, V>(this IEnumerable<KeyValuePair<T, V>> items, string format = null)
        {
            format = String.IsNullOrEmpty(format) ? "{0}='{1}' " : format;

            StringBuilder itemString = new StringBuilder();
            foreach (var item in items)
                itemString.AppendFormat(format, item.Key, item.Value);

            return itemString.ToString();
        }

    }
}
