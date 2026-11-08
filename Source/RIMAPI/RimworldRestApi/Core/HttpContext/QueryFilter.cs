using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RIMAPI.Core
{
    /// <summary>
    /// Provides generic filtering logic for collections based on query parameters,
    /// supporting snake_case JSON property names.
    /// </summary>
    public static class QueryFilter
    {
        /// <summary>
        /// Applies filters from the query string to the provided collection.
        /// </summary>
        public static IEnumerable<T> Apply<T>(IEnumerable<T> items, NameValueCollection query)
        {
            return Apply(items, query, null, null);
        }

        /// <summary>
        /// Applies the query's field filters to the items. Keys in <paramref name="skip"/> (the route's own
        /// parameters) are not filters. A key naming no field of the item type is reported in
        /// <paramref name="warnings"/> and not applied: it used to drop every item, which looked like "no results".
        /// </summary>
        public static IEnumerable<T> Apply<T>(IEnumerable<T> items, NameValueCollection query,
            ICollection<string> skip, List<string> warnings)
        {
            if (items == null || query == null || query.Count == 0)
                return items;

            var filteredItems = items;

            foreach (string key in query.AllKeys)
            {
                if (string.IsNullOrEmpty(key) || IsReservedKey(key) || (skip != null && skip.Contains(key)))
                    continue;
                if (!TypeHasJsonField(typeof(T), key))
                {
                    warnings?.Add($"Query key '{key}' is not a field of {typeof(T).Name}; it was not applied as a filter");
                    continue;
                }

                var value = query[key];
                filteredItems = filteredItems.Where(item => Matches(item, key, value)).ToList();
            }

            return filteredItems;
        }

        /// <summary>
        /// Applies filters to properties of an object if those properties are collections.
        /// Supports snake_case targeting (e.g. ?things_defs.category=Item).
        /// </summary>
        public static object ApplyToResult(object result, NameValueCollection query)
        {
            return ApplyToResult(result, query, null, null);
        }

        public static object ApplyToResult(object result, NameValueCollection query,
            ICollection<string> skip, List<string> warnings)
        {
            if (result == null || query == null || query.Count == 0)
                return result;

            // If the result itself is a collection, filter it directly
            if (result is IEnumerable enumerable && !(result is string))
            {
                var itemType = GetEnumerableItemType(result.GetType());
                if (itemType != null)
                {
                    return InvokeApply(itemType, result, query, skip, warnings);
                }
                return result;
            }

            var properties = result.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
                {
                    var propValue = prop.GetValue(result);
                    if (propValue == null) continue;

                    var jsonName = ConvertToSnakeCase(prop.Name);
                    var subQuery = new NameValueCollection();
                    bool hasSpecificFilter = false;

                    // 1. Check for specific filters (e.g., things_defs.category=Item)
                    var prefix = jsonName + ".";
                    foreach (string key in query.AllKeys)
                    {
                        if (key != null && key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            subQuery.Add(key.Substring(prefix.Length), query[key]);
                            hasSpecificFilter = true;
                        }
                    }

                    // 2. Fallback: If this is "things_defs", apply all non-prefixed filters to it
                    if (!hasSpecificFilter && jsonName.Equals("things_defs", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (string key in query.AllKeys)
                        {
                            if (key != null && !key.Contains(".") && !IsReservedKey(key) && (skip == null || !skip.Contains(key)))
                            {
                                subQuery.Add(key, query[key]);
                                hasSpecificFilter = true;
                            }
                        }
                    }

                    if (hasSpecificFilter)
                    {
                        var itemType = GetEnumerableItemType(prop.PropertyType);
                        if (itemType != null)
                        {
                            var filteredList = InvokeApply(itemType, propValue, subQuery, skip, warnings);
                            
                            if (prop.CanWrite)
                            {
                                if (prop.PropertyType.IsAssignableFrom(filteredList.GetType()))
                                {
                                    prop.SetValue(result, filteredList);
                                }
                                else
                                {
                                    var listType = typeof(List<>).MakeGenericType(itemType);
                                    var list = Activator.CreateInstance(listType, filteredList);
                                    prop.SetValue(result, list);
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static object InvokeApply(Type itemType, object items, NameValueCollection query,
            ICollection<string> skip, List<string> warnings)
        {
            var method = typeof(QueryFilter).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "Apply" && m.GetParameters().Length == 4);
            return method.MakeGenericMethod(itemType).Invoke(null, new object[] { items, query, skip, warnings });
        }

        /// <summary>True when the first segment of a (possibly dotted) filter key names a property of the type.</summary>
        private static bool TypeHasJsonField(Type type, string key)
        {
            if (type == typeof(object))
                return true; // untyped items: cannot tell, keep the old per-item behaviour
            string first = key.Split('.')[0];
            string loose = first.Replace("_", "");
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(p =>
                ConvertToSnakeCase(p.Name).Equals(first, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals(loose, StringComparison.OrdinalIgnoreCase));
        }

        private static bool Matches(object item, string key, string value)
        {
            if (item == null) return false;

            // Handle nested fields (e.g., stat_base.market_value)
            if (key.Contains('.'))
            {
                var parts = key.Split(new[] { '.' }, 2);
                var currentKey = parts[0];
                var remainingKey = parts[1];

                var prop = GetPropertyByJsonName(item, currentKey);
                if (prop != null)
                {
                    var propValue = prop.GetValue(item);
                    
                    // Handle Dictionary access (common for stat_base)
                    if (propValue is IDictionary dict)
                    {
                        string dictKey = remainingKey;
                        string rest = null;
                        if (remainingKey.Contains('.'))
                        {
                            var subParts = remainingKey.Split(new[] { '.' }, 2);
                            dictKey = subParts[0];
                            rest = subParts[1];
                        }

                        foreach (var k in dict.Keys)
                        {
                            // Stats in stat_base can be snake_case or PascalCase keys
                            var kStr = k.ToString();
                            if (kStr.Equals(dictKey, StringComparison.OrdinalIgnoreCase) || 
                                ConvertToSnakeCase(kStr).Equals(dictKey, StringComparison.OrdinalIgnoreCase))
                            {
                                var foundValue = dict[k];
                                if (rest != null) return Matches(foundValue, rest, value);
                                return CompareValues(foundValue, value);
                            }
                        }
                        return false;
                    }

                    return Matches(propValue, remainingKey, value);
                }
                return false;
            }

            // Simple field match
            var property = GetPropertyByJsonName(item, key);
            if (property == null) return false;

            return CompareValues(property.GetValue(item), value);
        }

        private static PropertyInfo GetPropertyByJsonName(object item, string jsonName)
        {
            var properties = item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            // Try exact snake_case match first
            foreach (var prop in properties)
            {
                if (ConvertToSnakeCase(prop.Name).Equals(jsonName, StringComparison.OrdinalIgnoreCase))
                    return prop;
            }

            // Fallback: try removing underscores for a loose match
            var normalizedJsonName = jsonName.Replace("_", "");
            foreach (var prop in properties)
            {
                if (prop.Name.Equals(normalizedJsonName, StringComparison.OrdinalIgnoreCase))
                    return prop;
            }

            return null;
        }

        private static bool CompareValues(object actualValue, string expectedValue)
        {
            if (actualValue == null) 
                return expectedValue.Equals("null", StringComparison.OrdinalIgnoreCase);

            var type = actualValue.GetType();

            if (type == typeof(bool))
            {
                if (bool.TryParse(expectedValue, out bool b))
                    return (bool)actualValue == b;
                
                var lower = expectedValue.ToLower();
                if (lower == "1" || lower == "true" || lower == "yes") return (bool)actualValue == true;
                if (lower == "0" || lower == "false" || lower == "no") return (bool)actualValue == false;
                return false;
            }

            if (IsNumericType(type))
            {
                if (double.TryParse(expectedValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double exp) &&
                    double.TryParse(actualValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double act))
                {
                    return Math.Abs(act - exp) < 0.0001;
                }
            }

            return actualValue.ToString().Equals(expectedValue, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNumericType(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte: case TypeCode.SByte: case TypeCode.UInt16: case TypeCode.UInt32: 
                case TypeCode.UInt64: case TypeCode.Int16: case TypeCode.Int32: case TypeCode.Int64: 
                case TypeCode.Decimal: case TypeCode.Double: case TypeCode.Single:
                    return true;
                default: return false;
            }
        }

        private static string ConvertToSnakeCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var result = new StringBuilder();
            result.Append(char.ToLower(input[0]));
            for (int i = 1; i < input.Length; i++)
            {
                if (char.IsUpper(input[i]))
                {
                    result.Append('_');
                    result.Append(char.ToLower(input[i]));
                }
                else
                {
                    result.Append(input[i]);
                }
            }
            return result.ToString();
        }

        private static Type GetEnumerableItemType(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            var iface = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return iface?.GetGenericArguments()[0];
        }

        private static bool IsReservedKey(string key)
        {
            var reserved = new[] { "format", "map_id", "token", "api_key" };
            return reserved.Contains(key.ToLower());
        }
    }
}
