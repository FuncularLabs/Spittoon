using Microsoft.Extensions.Logging;
using Spittoon.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Spittoon
{
    public sealed class SpittoonSerializer
    {
        private readonly SpittoonMode _mode;

        public SpittoonSerializer(SpittoonMode mode = SpittoonMode.Strict) => _mode = mode;

        public string Serialize(object? value, Formatting formatting = Formatting.Indented)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, formatting, 0);
            return sb.ToString();
        }

        private void WriteValue(StringBuilder sb, object? value, Formatting formatting, int depth)
        {
            if (value == null) { sb.Append("null"); return; }
            if (value is string s) { WriteString(sb, s); return; }
            if (value is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (value is IFormattable f) { sb.Append(f.ToString(null, CultureInfo.InvariantCulture)); return; }

            // If value is a dictionary and appears to be a tabular (header+rows) and we are in Indented formatting, special-case it
            if (value is IDictionary<string, object?> dictValue && formatting == Formatting.Indented &&
                dictValue.TryGetValue("header", out var headerObj) && headerObj is IDictionary<string, object?> headerDict &&
                dictValue.TryGetValue("rows", out var rowsObj) && rowsObj is IEnumerable rowsEnum)
            {
                // Special-case tabular: write header then rows as unlabeled arrays
                sb.Append('{');
                sb.Append('\n');
                sb.Append(new string(' ', (depth + 1) * 2));
                WriteString(sb, "header");
                sb.Append(':');
                WriteValue(sb, headerDict, Formatting.Indented, depth + 1);

                sb.Append(',');
                sb.Append('\n');
                sb.Append(new string(' ', (depth + 1) * 2));
                WriteString(sb, "rows");
                sb.Append(':');
                // rows array
                sb.Append("[\n");

                var headerKeys = headerDict.Keys.ToList();
                int rowIndex = 0;
                foreach (var r in rowsEnum)
                {
                    if (rowIndex > 0) sb.Append(";\n");
                    sb.Append(new string(' ', (depth + 2) * 2));
                    if (r is IDictionary<string, object?> rowDict)
                    {
                        sb.Append('[');
                        for (int i = 0; i < headerKeys.Count; i++)
                        {
                            if (i > 0) sb.Append("; ");
                            var key = headerKeys[i];
                            rowDict.TryGetValue(key, out var rv);
                            WriteValue(sb, rv, Formatting.Compact, depth + 2);
                        }
                        sb.Append(']');
                    }
                    else
                    {
                        // fallback
                        sb.Append('[');
                        WriteValue(sb, r, Formatting.Compact, depth + 2);
                        sb.Append(']');
                    }

                    rowIndex++;
                }

                if (rowIndex > 0) sb.Append('\n').Append(new string(' ', (depth + 1) * 2));
                sb.Append(']');

                sb.Append('\n').Append(new string(' ', depth * 2));
                sb.Append('}');
                return;
            }

            // If value is a dictionary, serialize its entries as object members
            if (value is IDictionary<string, object?> map)
            {
                sb.Append('{');
                bool firstEntry = true;
                foreach (var kvp in map)
                {
                    if (!firstEntry) sb.Append(',');
                    sb.Append('"').Append(kvp.Key).Append('"').Append(':');
                    WriteValue(sb, kvp.Value, formatting, depth + 1);
                    firstEntry = false;
                }
                sb.Append('}');
                return;
            }

            if (value is IEnumerable en && value is not string)
            {
                sb.Append('[');
                bool first = true;
                string sep = formatting == Formatting.Indented ? "; " : ", ";
                foreach (var item in en)
                {
                    if (!first) sb.Append(sep);
                    WriteValue(sb, item, formatting, depth + 1);
                    first = false;
                }
                sb.Append(']');
                return;
            }

            // Objects
            var type = value.GetType();

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetCustomAttribute<SpittoonIgnoreAttribute>() == null)
                .Select(p => new { Name = p.GetCustomAttribute<SpittoonNameAttribute>()?.Name ?? p.Name, Value = p.GetValue(value) })
                .ToList();

            sb.Append('{');
            bool firstProp = true;
            foreach (var kv in props)
            {
                if (!firstProp) sb.Append(',');
                sb.Append('"').Append(kv.Name).Append('"').Append(':');
                WriteValue(sb, kv.Value, formatting, depth + 1);
                firstProp = false;
            }
            sb.Append('}');
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                if (c == '"') sb.Append('\"'); else sb.Append(c);
            }
            sb.Append('"');
        }
    }
}
