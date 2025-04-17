#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Guru.SDK.Framework.Utils.Extensions
{
    public static class DictionaryExtensions
    {
        public static Dictionary<string, object> FilterOutNulls(
            this Dictionary<string, object?> parameters,
            Func<string, object, object>? converter = null)
        {
            var filtered = new Dictionary<string, object>();

            foreach (var kvp in parameters)
            {
                if (kvp.Value != null)
                {
                    filtered[kvp.Key] = converter?.Invoke(kvp.Key, kvp.Value) ?? kvp.Value;
                }
            }

            return filtered;
        }
        
        public static string ToFullString(this Dictionary<string, object> dictionary)
        {
            var result = dictionary.Aggregate("{", (current, kvp) => current + $"{kvp.Key}: {kvp.Value}, ");
            result += "}";
            return result;
        }
    }
}