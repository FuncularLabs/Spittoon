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
    public sealed class SpittoonDeserializer
    {
        private readonly SpittoonMode _mode;
        private readonly ILogger<SpittoonDeserializer>? _logger;

        public SpittoonDeserializer(SpittoonMode mode = SpittoonMode.Strict, ILogger<SpittoonDeserializer>? logger = null)
        {
            _mode = mode;
            _logger = logger;
        }

        public object? Parse(string text) => new Parser(text, _mode, _logger).ParseRoot();

        public T Deserialize<T>(string text) where T : new()
        {
            var result = Parse(text);

            if (result is not Dictionary<string, object?> dict)
                throw new SpittoonValidationException("Root must be an object for strongly-typed deserialization.");

            var instance = new T();
            MapToObject(dict, instance);
            return instance;
        }

        public dynamic? DeserializeDynamic(string text)
        {
            var result = Parse(text);

            if (result is IDictionary<string, object?> d)
            {
                var exp = new ExpandoObject();
                var target = (IDictionary<string, object?>)exp;
                foreach (var kv in d)
                    target[kv.Key] = kv.Value;
                return exp;
            }

            return result is List<object?> list ? list : result;
        }

        private void MapToObject(Dictionary<string, object?> source, object target)
        {
            var type = target.GetType();

            var propMap = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                              .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
                              .ToDictionary(
                                  p => p.GetCustomAttribute<SpittoonNameAttribute>()?.Name ?? p.Name,
                                  p => p,
                                  StringComparer.OrdinalIgnoreCase);

            foreach (var kv in source)
            {
                if (propMap.TryGetValue(kv.Key, out var prop))
                {
                    try
                    {
                        var converted = ConvertValue(kv.Value, prop.PropertyType);
                        prop.SetValue(target, converted);
                    }
                    catch (Exception ex) when (_mode == SpittoonMode.Forgiving)
                    {
                        _logger?.LogWarning(ex, "Forgiving mode: could not set property {Property}", kv.Key);
                    }
                }
                else if (_mode == SpittoonMode.Strict)
                {
                    throw new SpittoonValidationException($"Unknown property '{kv.Key}'");
                }
            }

            if (_mode == SpittoonMode.Strict)
            {
                foreach (var prop in type.GetProperties()
                                         .Where(p => p.GetCustomAttribute<SpittoonRequiredAttribute>() != null))
                {
                    var val = prop.GetValue(target);
                    if (val == null || (prop.PropertyType.IsValueType && val.Equals(Activator.CreateInstance(prop.PropertyType))))
                    {
                        var msg = prop.GetCustomAttribute<SpittoonRequiredAttribute>()?.ErrorMessage ?? $"Required property '{prop.Name}' is missing";
                        throw new SpittoonValidationException(msg);
                    }
                }
            }
        }

        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value == null) return null;

            if (targetType.IsInstanceOfType(value)) return value;

            if (targetType == typeof(string)) return value.ToString();

            if (targetType.IsEnum)
                return Enum.Parse(targetType, value.ToString()!, ignoreCase: true);

            if (typeof(IEnumerable).IsAssignableFrom(targetType) && value is List<object?> list)
            {
                var elementType = targetType.IsArray
                    ? targetType.GetElementType()
                    : targetType.IsGenericType
                        ? targetType.GetGenericArguments()[0]
                        : typeof(object);

                if (elementType == null) elementType = typeof(object);

                var typedListObj = Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                if (typedListObj is not IList typedList)
                    throw new SpittoonValidationException("Could not create typed list for deserialization");

                foreach (var item in list)
                {
                    var convertedItem = ConvertValue(item, elementType);
                    typedList.Add(convertedItem);
                }

                if (targetType.IsArray)
                {
                    var array = Array.CreateInstance(elementType, typedList.Count);
                    typedList.CopyTo(array, 0);
                    return array;
                }

                return typedList;
            }

            if (value is Dictionary<string, object?> dict)
            {
                var obj = Activator.CreateInstance(targetType) ?? throw new SpittoonValidationException($"Could not create instance of type {targetType}");
                new SpittoonDeserializer().MapToObject(dict, obj);
                return obj;
            }

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _pos;
            private readonly SpittoonMode _mode;
            private readonly ILogger? _logger;

            public Parser(string text, SpittoonMode mode, ILogger? logger)
            {
                _text = text ?? throw new ArgumentNullException(nameof(text));
                _mode = mode;
                _logger = logger;
            }

            public object? ParseRoot()
            {
                Skip();
                if (_pos >= _text.Length) return null;

                if (_text[_pos] == '{') return ParseObject();
                if (_text[_pos] == '[') return ParseArray();

                if (IsUnbracedRoot())
                {
                    if (_mode == SpittoonMode.Forgiving)
                        return ParseUnbracedRoot();
                    throw new SpittoonSyntaxException("Unbraced root not allowed in strict mode");
                }

                object? single = ParseValue();
                Skip();
                if (_pos < _text.Length && _mode == SpittoonMode.Strict)
                    throw new SpittoonSyntaxException("Unexpected trailing characters after root value");

                return single;
            }

            private bool IsUnbracedRoot()
            {
                int p = _pos;
                while (p < _text.Length && char.IsWhiteSpace(_text[p])) p++;
                int q = p;
                while (q < _text.Length && !char.IsWhiteSpace(_text[q]) && _text[q] != ':' && _text[q] != '{' && _text[q] != '[') q++;
                return q < _text.Length && _text[q] == ':';
            }

            private object ParseUnbracedRoot()
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                while (_pos < _text.Length)
                {
                    Skip();
                    if (_pos >= _text.Length) break;

                    string key = ParseKey();

                    Skip();
                    if (_pos >= _text.Length || _text[_pos] != ':')
                        throw new SpittoonSyntaxException($"Expected ':' after key '{key}'");

                    _pos++; // consume ':'
                    Skip();

                    object? value = ParseValue();

                    Skip();
                    if (_pos < _text.Length && _text[_pos] == ':')
                    {
                        int save = _pos;
                        _pos++; // consume ':'
                        Skip();
                        if (_pos < _text.Length && _text[_pos] == '[')
                        {
                            var rows = ParseArray();

                            // value may be either the header object itself, or a wrapper object containing a 'header' property
                            Dictionary<string, object?>? headerDict = null;
                            if (value is Dictionary<string, object?> vdict)
                            {
                                // If the value itself contains a 'header' property, use that inner object
                                if (vdict.TryGetValue("header", out var inner) && inner is Dictionary<string, object?> innerDict)
                                {
                                    headerDict = innerDict;
                                }
                                else
                                {
                                    // value might itself be the header dict
                                    headerDict = vdict;
                                }
                            }

                            if (headerDict != null)
                            {
                                var headerKeys = new List<string>(headerDict.Keys);
                                var normalized = new List<object?>();

                                foreach (var row in rows)
                                {
                                    if (row is Dictionary<string, object?> rdict)
                                    {
                                        normalized.Add(rdict);
                                        continue;
                                    }

                                    if (row is List<object?> rlist)
                                    {
                                        var mapped = new Dictionary<string, object?>(StringComparer.Ordinal);
                                        int n = Math.Min(headerKeys.Count, rlist.Count);
                                        for (int i = 0; i < n; i++) mapped[headerKeys[i]] = rlist[i];

                                        if (rlist.Count < headerKeys.Count)
                                        {
                                            for (int i = rlist.Count; i < headerKeys.Count; i++) mapped[headerKeys[i]] = null;
                                            if (_mode == SpittoonMode.Strict)
                                                throw new SpittoonValidationException("Row has fewer values than header columns");
                                        }
                                        else if (rlist.Count > headerKeys.Count)
                                        {
                                            if (_mode == SpittoonMode.Strict)
                                                throw new SpittoonValidationException("Row has more values than header columns");
                                            // forgiving: ignore extras
                                        }

                                        normalized.Add(mapped);
                                        continue;
                                    }

                                    if (row != null)
                                    {
                                        if (headerKeys.Count == 1)
                                        {
                                            normalized.Add(new Dictionary<string, object?>(StringComparer.Ordinal) { [headerKeys[0]] = row });
                                            continue;
                                        }

                                        if (_mode == SpittoonMode.Strict)
                                            throw new SpittoonValidationException("Row scalar cannot be mapped to multi-column header");

                                        var m = new Dictionary<string, object?>(StringComparer.Ordinal) { [headerKeys[0]] = row };
                                        for (int i = 1; i < headerKeys.Count; i++) m[headerKeys[i]] = null;
                                        normalized.Add(m);
                                        continue;
                                    }

                                    var nullMapped = new Dictionary<string, object?>(StringComparer.Ordinal);
                                    foreach (var hk in headerKeys) nullMapped[hk] = null;
                                    normalized.Add(nullMapped);
                                }

                                var wrapper = new Dictionary<string, object?>(StringComparer.Ordinal)
                                {
                                    ["header"] = headerDict,
                                    ["rows"] = normalized
                                };

                                result[key] = wrapper;
                            }
                            else
                            {
                                throw new SpittoonSyntaxException("Tabular header must be an object");
                            }

                            Skip();
                            if (_pos < _text.Length && (_text[_pos] == ',' || _text[_pos] == ';')) _pos++;
                            continue;
                        }

                        _pos = save;
                    }

                    result[key] = value;

                    Skip();
                    if (_pos < _text.Length && (_text[_pos] == ',' || _text[_pos] == ';')) _pos++;
                }

                return result;
            }

            private object? ParseValue()
            {
                Skip();

                if (_pos >= _text.Length)
                    throw new SpittoonSyntaxException("Unexpected end of input");

                char c = _text[_pos];

                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return ParseQuotedString();
                if (c == '/') return ParseRegexLiteral();

                if (TryConsume("null")) return null;
                if (TryConsume("true")) return true;
                if (TryConsume("false")) return false;

                if (c == '-' || char.IsDigit(c)) return ParseNumber();

                return ParseUnquotedString();
            }

            private string ParseRegexLiteral()
            {
                // consume opening '/'
                Consume('/');
                var sb = new StringBuilder();
                bool escaped = false;
                while (_pos < _text.Length)
                {
                    char ch = _text[_pos++];
                    if (!escaped && ch == '/')
                    {
                        // end of regex literal
                        return sb.ToString();
                    }
                    if (ch == '\\' && !escaped)
                    {
                        escaped = true;
                        continue;
                    }
                    sb.Append(ch);
                    escaped = false;
                }

                if (_mode == SpittoonMode.Forgiving) return sb.ToString();
                throw new SpittoonSyntaxException("Unterminated regex literal");
            }

            private string ParseQuotedString()
            {
                Consume('"');
                var sb = new StringBuilder();

                while (_pos < _text.Length)
                {
                    char c = _text[_pos++];
                    if (c == '\\')
                    {
                        if (_pos >= _text.Length)
                        {
                            sb.Append('\\');
                            break;
                        }
                        c = _text[_pos++];
                    }
                    else if (c == '"')
                    {
                        return sb.ToString();
                    }

                    sb.Append(c);
                }

                if (_mode == SpittoonMode.Forgiving)
                {
                    return sb.ToString();
                }

                throw new SpittoonSyntaxException("Unterminated string");
            }

            private string ParseUnquotedString()
            {
                int start = _pos;
                if (_mode == SpittoonMode.Forgiving)
                {
                    int braceDepth = 0;
                    while (_pos < _text.Length)
                    {
                        char ch = _text[_pos];
                        if (ch == '{') { braceDepth++; _pos++; continue; }
                        if (ch == '}')
                        {
                            if (braceDepth > 0) { braceDepth--; _pos++; continue; }
                            // treat as delimiter if not in a brace group
                            break;
                        }

                        if ((ch == ',' || ch == ';' || ch == ']') && braceDepth == 0) break;
                        _pos++;
                    }

                    // trim whitespace from ends
                    int s = start, e = _pos - 1;
                    while (s <= e && char.IsWhiteSpace(_text[s])) s++;
                    while (e >= s && char.IsWhiteSpace(_text[e])) e--;
                    if (s > e) throw new SpittoonSyntaxException("Expected value");
                    return _text.Substring(s, e - s + 1);
                }

                // In strict mode allow braces inside unquoted tokens (e.g., type qualifiers like arr{1,100})
                while (_pos < _text.Length && !char.IsWhiteSpace(_text[_pos]) && ",;:[]\"".IndexOf(_text[_pos]) == -1)
                    _pos++;

                if (start == _pos) throw new SpittoonSyntaxException("Expected value");
                return _text.Substring(start, _pos - start);
            }

            private object ParseNumber()
            {
                int start = _pos;
                if (_text[_pos] == '-') _pos++;

                bool hasDot = false;
                while (_pos < _text.Length)
                {
                    char c = _text[_pos];
                    if (c == '.')
                    {
                        if (hasDot) throw new SpittoonSyntaxException("Multiple decimal points");
                        hasDot = true;
                    }
                    else if (!char.IsDigit(c))
                        break;
                    _pos++;
                }

                string str = _text.Substring(start, _pos - start);
                return hasDot ? (object)double.Parse(str, CultureInfo.InvariantCulture) : long.Parse(str);
            }

            private string ParseKey()
            {
                if (_pos >= _text.Length) throw new SpittoonSyntaxException("Unexpected end of input while parsing key");
                if (_text[_pos] == '"') return ParseQuotedString();

                int start = _pos;
                while (_pos < _text.Length && !char.IsWhiteSpace(_text[_pos]) && ":,;{}[]\"".IndexOf(_text[_pos]) == -1 && _text[_pos] != ':')
                    _pos++;

                if (start == _pos) throw new SpittoonSyntaxException("Expected key");
                return _text.Substring(start, _pos - start);
            }

            private void Skip()
            {
                while (_pos < _text.Length)
                {
                    if (char.IsWhiteSpace(_text[_pos]))
                    {
                        _pos++;
                        continue;
                    }

                    if (_text[_pos] == '/' && _pos + 1 < _text.Length)
                    {
                        if (_text[_pos + 1] == '/')
                        {
                            _pos += 2;
                            while (_pos < _text.Length && _text[_pos] != '\n' && _pos != '\r') _pos++;
                            continue;
                        }

                        if (_text[_pos + 1] == '*')
                        {
                            _pos += 2;
                            while (_pos + 1 < _text.Length && !(_text[_pos] == '*' && _text[_pos + 1] == '/'))
                                _pos++;
                            if (_pos + 1 >= _text.Length) throw new SpittoonSyntaxException("Unterminated block comment");
                            _pos += 2;
                            continue;
                        }
                    }

                    break;
                }
            }

            private void Consume(char expected)
            {
                Skip();
                if (_pos >= _text.Length || _text[_pos] != expected)
                    throw new SpittoonSyntaxException($"Expected '{expected}'");
                _pos++;
            }

            private bool TryConsume(char expected)
            {
                Skip();
                if (_pos < _text.Length && _text[_pos] == expected)
                {
                    _pos++;
                    return true;
                }
                return false;
            }

            private bool TryConsume(string literal)
            {
                Skip();
                if (_text.Length - _pos >= literal.Length && _text.Substring(_pos, literal.Length) == literal)
                {
                    _pos += literal.Length;
                    return true;
                }
                return false;
            }

            private Dictionary<string, object?> ParseObject()
            {
                Consume('{');
                var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

                while (true)
                {
                    Skip();
                    if (TryConsume('}')) break;

                    string key = ParseKey();

                    Skip();
                    Consume(':');
                    Skip();

                    dict[key] = ParseValue();

                    Skip();
                    if (TryConsume(',') || TryConsume(';')) continue;
                    if (TryConsume('}')) break;

                    throw new SpittoonSyntaxException("Expected , ; or }");
                }

                // Normalize tabular header+rows if present (accept IDictionary and IEnumerable)
                if (dict.TryGetValue("header", out var headerVal) && headerVal is IDictionary<string, object?> headerDict
                    && dict.TryGetValue("rows", out var rowsVal) && rowsVal is IEnumerable rowsEnum)
                {
                    var headerKeys = new List<string>(headerDict.Keys);
                    var normalized = new List<object?>();

                    foreach (var row in rowsEnum)
                    {
                        if (row is IDictionary<string, object?> rdict)
                        {
                            normalized.Add(new Dictionary<string, object?>(rdict, StringComparer.Ordinal));
                            continue;
                        }

                        if (row is IEnumerable rlist && !(row is string))
                        {
                            var values = new List<object?>();
                            foreach (var it in rlist) values.Add(it);

                            var mapped = new Dictionary<string, object?>(StringComparer.Ordinal);
                            int n = Math.Min(headerKeys.Count, values.Count);
                            for (int i = 0; i < n; i++) mapped[headerKeys[i]] = values[i];

                            if (values.Count < headerKeys.Count)
                            {
                                for (int i = values.Count; i < headerKeys.Count; i++) mapped[headerKeys[i]] = null;
                                if (_mode == SpittoonMode.Strict)
                                    throw new SpittoonValidationException("Row has fewer values than header columns");
                            }
                            else if (values.Count > headerKeys.Count)
                            {
                                if (_mode == SpittoonMode.Strict)
                                    throw new SpittoonValidationException("Row has more values than header columns");
                                // forgiving: ignore extras
                            }

                            normalized.Add(mapped);
                            continue;
                        }

                        // scalar or null
                        if (row != null)
                        {
                            if (headerKeys.Count == 1)
                            {
                                normalized.Add(new Dictionary<string, object?>(StringComparer.Ordinal) { [headerKeys[0]] = row });
                                continue;
                            }

                            if (_mode == SpittoonMode.Strict)
                                throw new SpittoonValidationException("Row scalar cannot be mapped to multi-column header");

                            var m = new Dictionary<string, object?>(StringComparer.Ordinal) { [headerKeys[0]] = row };
                            for (int i = 1; i < headerKeys.Count; i++) m[headerKeys[i]] = null;
                            normalized.Add(m);
                            continue;
                        }

                        var nullMapped = new Dictionary<string, object?>(StringComparer.Ordinal);
                        foreach (var hk in headerKeys) nullMapped[hk] = null;
                        normalized.Add(nullMapped);
                    }

                    dict["rows"] = normalized;
                }

                return dict;
            }

            private List<object?> ParseArray()
            {
                Consume('[');
                var list = new List<object?>();

                while (true)
                {
                    Skip();
                    if (TryConsume(']')) break;

                    list.Add(ParseValue());

                    Skip();
                    if (TryConsume(',') || TryConsume(';')) continue;
                    if (TryConsume(']')) break;

                    throw new SpittoonSyntaxException("Expected , ; or ]");
                }

                return list;
            }
        }
    }
}
