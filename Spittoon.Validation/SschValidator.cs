using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Spittoon.Nodes;
using Spittoon;

namespace Spittoon.Validation;

/// <summary>
/// Validates data against an SSCH schema.
/// </summary>
public sealed class SschValidator
{
    private readonly SchemaNode _root;
    private readonly ILogger<SschValidator>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SschValidator"/> class with the specified schema text.
    /// </summary>
    /// <param name="schemaText">The schema text.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="ArgumentException">Thrown if the schema is invalid.</exception>
    public SschValidator(string schemaText, ILogger<SschValidator>? logger = null)
    {
        var parsed = new SpittoonDeserializer(SpittoonMode.Forgiving).Parse(schemaText);

        if (parsed is not Dictionary<string, object?> rootDict ||
            !rootDict.TryGetValue("schema", out var schemaObj) ||
            schemaObj is not Dictionary<string, object?> schemaDict)
        {
            throw new ArgumentException("SSCH document must contain a root object with a 'schema' property that is an object.");
        }

        _root = SchemaNode.Build(schemaDict);
        _logger = logger;
    }

    /// <summary>
    /// Validates the specified data text against the schema.
    /// </summary>
    /// <param name="dataText">The data text.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult Validate(string dataText)
    {
        // Parse the data text into a SpittoonDocument (forgiving by default)
        var doc = SpittoonDocument.Load(dataText);

        // Additional raw-check for unlabeled tabular rows (catch arrays shorter than header before NodeBuilder normalization)
        var raw = new SpittoonDeserializer(SpittoonMode.Forgiving).Parse(dataText);
        var preErrors = new List<ValidationError>();
        void CheckRaw(object? node, string path)
        {
            if (node is Dictionary<string, object?> dict)
            {
                if (dict.TryGetValue("header", out var hdr) && dict.TryGetValue("rows", out var rows) && hdr is Dictionary<string, object?> hdrDict)
                {
                    var headerKeys = new List<string>(hdrDict.Keys);
                    if (rows is System.Collections.IEnumerable rowsEnum)
                    {
                        int ri = 0;
                        foreach (var r in rowsEnum)
                        {
                            string rowPath = string.IsNullOrEmpty(path) ? $"rows[{ri}]" : $"{path}/rows[{ri}]";
                            if (r is System.Collections.IList rlist)
                            {
                                if (rlist.Count < headerKeys.Count)
                                    preErrors.Add(new ValidationError(rowPath, $"Array must contain at least {headerKeys.Count} items"));
                            }
                            else if (r is IDictionary<string, object?> rd)
                            {
                                foreach (var hk in headerKeys)
                                    if (!rd.ContainsKey(hk) || rd[hk] == null)
                                        preErrors.Add(new ValidationError(rowPath, $"Missing column '{hk}'"));
                            }
                            ri++;
                        }
                    }

                }

                foreach (var kv in dict)
                    CheckRaw(kv.Value, string.IsNullOrEmpty(path) ? kv.Key : $"{path}/{kv.Key}");
            }
            else if (node is System.Collections.IEnumerable list)
            {
                int i = 0;
                foreach (var it in list)
                {
                    CheckRaw(it, $"{path}[{i}]");
                    i++;
                }
            }
        }

        CheckRaw(raw, "");
        if (preErrors.Count > 0) return ValidationResult.Fail(preErrors);

        // double-check with text-based heuristic
        var textErrors = CheckTabularText(dataText);
        if (textErrors.Count > 0) return ValidationResult.Fail(textErrors);

        // check nodes after NodeBuilder normalization
        var nodeErrors = CheckTabularOnNodes(doc.Root);
        if (nodeErrors.Count > 0) return ValidationResult.Fail(nodeErrors);

        return Validate(doc.Root, SpittoonMode.Forgiving);
    }

    /// <summary>
    /// Validates the specified data node against the schema.
    /// </summary>
    /// <param name="dataRoot">The root data node.</param>
    /// <param name="mode">The validation mode.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult Validate(SpittoonNode dataRoot, SpittoonMode mode)
    {
        var errors = new List<ValidationError>();
        // TODO: handle mode appropriately
        // pre-check tabular consistency (header/rows)
        CheckTabularConsistency(dataRoot, errors, "");
        _root.Validate(dataRoot, errors, "");
        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Fail(errors);
    }

    // Recursively check tabular header/rows consistency in the parsed document (called before schema validation)
    private void CheckTabularConsistency(SpittoonNode node, List<ValidationError> errors, string path)
    {
        if (node is SpittoonObjectNode obj)
        {
            if (obj.Properties.TryGetValue("header", out var hdr) && obj.Properties.TryGetValue("rows", out var rws))
            {
                if (hdr is SpittoonObjectNode hdrObj && rws is SpittoonArrayNode rowsArr)
                {
                    var headerKeys = new List<string>(hdrObj.Properties.Keys);
                    for (int i = 0; i < rowsArr.Items.Count; i++)
                    {
                        var row = rowsArr.Items[i];
                        string rowPath = string.IsNullOrEmpty(path) ? $"rows[{i}]" : $"{path}/rows[{i}]";

                        if (row is SpittoonObjectNode rowObj)
                        {
                            foreach (var hk in headerKeys)
                            {
                                if (!rowObj.Properties.TryGetValue(hk, out var cell) || (cell is SpittoonValueNode vcell && vcell.Value == null))
                                    errors.Add(new ValidationError(rowPath, $"Missing column '{hk}'"));
                            }
                        }
                        else if (row is SpittoonArrayNode rowArr)
                        {
                            if (rowArr.Items.Count < headerKeys.Count)
                                errors.Add(new ValidationError(rowPath, $"Array must contain at least {headerKeys.Count} items"));
                            else if (rowArr.Items.Count > headerKeys.Count)
                                errors.Add(new ValidationError(rowPath, $"Array has more items than header columns"));
                        }
                        else
                        {
                            if (headerKeys.Count > 1)
                                errors.Add(new ValidationError(rowPath, "Row scalar cannot be mapped to multi-column header"));
                        }
                    }
                }
            }

            foreach (var kv in obj.Properties)
            {
                var childPath = string.IsNullOrEmpty(path) ? kv.Key : $"{path}/{kv.Key}";
                CheckTabularConsistency(kv.Value, errors, childPath);
            }
        }
        else if (node is SpittoonArrayNode arr)
        {
            for (int i = 0; i < arr.Items.Count; i++)
                CheckTabularConsistency(arr.Items[i], errors, $"{path}[{i}]");
        }
    }

    private static string RemoveComments(string text)
    {
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < text.Length)
        {
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '/')
            {
                // skip to end of line
                i += 2;
                while (i < text.Length && text[i] != '\n' && text[i] != '\r') i++;
                continue;
            }
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++;
                if (i + 1 < text.Length) i += 2;
                continue;
            }
            sb.Append(text[i]);
            i++;
        }
        return sb.ToString();
    }

    private List<ValidationError> CheckTabularText(string text)
    {
        text = RemoveComments(text);
        var errors = new List<ValidationError>();
        try
        {
            int idx = 0;
            while (true)
            {
                int hpos = text.IndexOf("header", idx, StringComparison.Ordinal);
                if (hpos < 0) break;
                int ob = text.IndexOf('{', hpos);
                if (ob < 0) break;
                // find matching closing brace for header
                int depth = 0; int headerEnd = ob;
                for (; headerEnd < text.Length; headerEnd++)
                {
                    if (text[headerEnd] == '{') depth++;
                    else if (text[headerEnd] == '}') { depth--; if (depth == 0) break; }
                }
                if (headerEnd >= text.Length) break;
                var headerContent = text.Substring(ob + 1, headerEnd - ob - 1);
                // count header keys by splitting on ',' at top-level
                var headerKeys = new List<string>();
                int start = 0; depth = 0;
                for (int j = 0; j <= headerContent.Length; j++)
                {
                    if (j == headerContent.Length || (headerContent[j] == ',' && depth == 0))
                    {
                        var tok = headerContent.Substring(start, j - start).Trim();
                        if (!string.IsNullOrEmpty(tok))
                        {
                            var k = tok.Split(new[] { ':', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                            if (!string.IsNullOrEmpty(k)) headerKeys.Add(k);
                        }
                        start = j + 1;
                        continue;
                    }
                    if (headerContent[j] == '{') depth++;
                    else if (headerContent[j] == '}') depth--;
                }

                // find rows after header
                int rkey = text.IndexOf("rows", headerEnd, StringComparison.Ordinal);
                if (rkey < 0) { idx = headerEnd + 1; continue; }
                int rb = text.IndexOf('[', rkey);
                if (rb < 0) { idx = rkey + 1; continue; }
                // find matching closing bracket for rows
                depth = 0; int rowsEnd = rb;
                for (; rowsEnd < text.Length; rowsEnd++)
                {
                    if (text[rowsEnd] == '[') depth++;
                    else if (text[rowsEnd] == ']') { depth--; if (depth == 0) break; }
                }
                if (rowsEnd >= text.Length) break;
                var rowsContent = text.Substring(rb + 1, rowsEnd - rb - 1);
                // split rows by top-level ';' or ',' separators (rows are separated by ';' in formatted, or ',')
                var rows = new List<string>();
                start = 0; depth = 0;
                for (int j = 0; j <= rowsContent.Length; j++)
                {
                    if (j == rowsContent.Length || ((rowsContent[j] == ';' || rowsContent[j] == ',') && depth == 0))
                    {
                        var tok = rowsContent.Substring(start, j - start).Trim();
                        if (!string.IsNullOrEmpty(tok)) rows.Add(tok);
                        start = j + 1;
                        continue;
                    }
                    if (rowsContent[j] == '[' || rowsContent[j] == '{') depth++;
                    else if (rowsContent[j] == ']' || rowsContent[j] == '}') depth--;
                }

                // examine each row
                for (int ri = 0; ri < rows.Count; ri++)
                {
                    var row = rows[ri].Trim();
                    if (row.StartsWith("[") && row.EndsWith("]"))
                    {
                        var inner = row.Substring(1, row.Length - 2);
                        // count top-level commas to determine item count
                        int cnt = 0; depth = 0; bool any = false;
                        for (int j = 0; j < inner.Length; j++)
                        {
                            char ch = inner[j];
                            if (ch == '[' || ch == '{') depth++;
                            else if (ch == ']' || ch == '}') depth--;
                            else if (ch == ',' && depth == 0) cnt++;
                            if (!char.IsWhiteSpace(ch)) any = true;
                        }
                        int items = any ? cnt + 1 : 0;
                        if (items < headerKeys.Count)
                        {
                            string rowPath = $"rows[{ri}]";
                            errors.Add(new ValidationError(rowPath, $"Array must contain at least {headerKeys.Count} items"));
                        }
                    }
                }

                idx = rowsEnd + 1;
            }
        }
        catch
        {
            // ignore parsing errors in heuristic
        }

        return errors;
    }

    /// <summary>
    /// Represents a schema node for validation.
    /// </summary>
    private sealed class SchemaNode
    {
        public SpittoonNodeType? ExpectedNodeType { get; private set; }
        public string? PrimitiveType { get; private set; }

        public List<object>? EnumValues { get; private set; }
        public Regex? Pattern { get; private set; }

        public double? MinValue { get; private set; }
        public double? MaxValue { get; private set; }
        public bool ExclusiveMin { get; private set; }
        public bool ExclusiveMax { get; private set; }

        public int? MinLength { get; private set; }
        public int? MaxLength { get; private set; }

        public int? MinItems { get; private set; }
        public int? MaxItems { get; private set; }

        public int? MinProperties { get; private set; }
        public int? MaxProperties { get; private set; }

        public bool UniqueItems { get; private set; }

        public List<string>? Required { get; private set; }

        public Dictionary<string, SchemaNode> Properties { get; } = new();

        public SchemaNode? Items { get; private set; }

        private bool _additionalPropertiesAllowed = true;
        public SchemaNode? AdditionalPropertiesSchema { get; private set; }

        /// <summary>
        /// Builds a schema node from a dictionary.
        /// </summary>
        /// <param name="dict">The dictionary.</param>
        /// <returns>The schema node.</returns>
        /// <exception cref="ArgumentException">Thrown if the type is invalid.</exception>
        public static SchemaNode Build(Dictionary<string, object?> dict)
        {
            var node = new SchemaNode();

            if (dict.TryGetValue("type", out var typeObj) && typeObj is string typeStr)
            {
                var match = Regex.Match(typeStr, @"^(str|int|float|bool|null|arr|obj)([\?\+\*]|\{\s*\d*\s*,?\s*\d*\s*\})?$");
                if (!match.Success)
                    throw new ArgumentException($"Invalid type declaration: {typeStr}");

                string baseType = match.Groups[1].Value;
                string qualifier = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;

                node.ExpectedNodeType = baseType switch
                {
                    "obj" => SpittoonNodeType.Object,
                    "arr" => SpittoonNodeType.Array,
                    _ => SpittoonNodeType.Value
                };

                if (baseType != "obj" && baseType != "arr")
                    node.PrimitiveType = baseType;

                if (!string.IsNullOrEmpty(qualifier))
                {
                    switch (qualifier)
                    {
                        case "?":
                            break;
                        case "*":
                            node.MinItems = 0;
                            break;
                        case "+":
                            node.MinItems = 1;
                            break;
                        default:
                            var card = Regex.Match(qualifier, "\\{(\\d*)\\s*,\\s*(\\d*)\\}");
                            if (card.Success)
                            {
                                if (!string.IsNullOrEmpty(card.Groups[1].Value))
                                    node.MinItems = int.Parse(card.Groups[1].Value);
                                if (!string.IsNullOrEmpty(card.Groups[2].Value))
                                    node.MaxItems = int.Parse(card.Groups[2].Value);
                            }
                            break;
                    }
                }
            }

            if (dict.TryGetValue("enum", out var e) && e is List<object> el) node.EnumValues = el;
            if (dict.TryGetValue("pattern", out var p) && p is string ps) node.Pattern = new Regex(ps, RegexOptions.ECMAScript | RegexOptions.Compiled);

            static bool TryToDouble(object? v, out double result)
            {
                result = 0;
                if (v == null) return false;
                if (v is double dv) { result = dv; return true; }
                if (v is float fv) { result = fv; return true; }
                if (v is long lv) { result = lv; return true; }
                if (v is int iv) { result = iv; return true; }
                if (v is string sv) return double.TryParse(sv, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
                try { result = Convert.ToDouble(v); return true; } catch { return false; }
            }

            if (dict.TryGetValue("min", out var mn) && TryToDouble(mn, out var mnv)) node.MinValue = mnv;
            if (dict.TryGetValue("max", out var mx) && TryToDouble(mx, out var mxv)) node.MaxValue = mxv;
            if (dict.TryGetValue("exclusiveMin", out var emn)) node.ExclusiveMin = Convert.ToBoolean(emn);
            if (dict.TryGetValue("exclusiveMax", out var emx)) node.ExclusiveMax = Convert.ToBoolean(emx);

            if (dict.TryGetValue("minLength", out var mln) && int.TryParse(mln?.ToString(), out var mlni)) node.MinLength = mlni;
            if (dict.TryGetValue("maxLength", out var ml) && int.TryParse(ml?.ToString(), out var mlax)) node.MaxLength = mlax;
            if (dict.TryGetValue("minItems", out var mi) && int.TryParse(mi?.ToString(), out var mini)) node.MinItems = mini;
            if (dict.TryGetValue("maxItems", out var mi2) && int.TryParse(mi2?.ToString(), out var maxi)) node.MaxItems = maxi;
            if (dict.TryGetValue("minProperties", out var mp) && int.TryParse(mp?.ToString(), out var minp)) node.MinProperties = minp;
            if (dict.TryGetValue("maxProperties", out var mp2) && int.TryParse(mp2?.ToString(), out var maxp)) node.MaxProperties = maxp;
            if (dict.TryGetValue("uniqueItems", out var ui) && ui is bool uib) node.UniqueItems = uib;
            if (dict.TryGetValue("required", out var req) && req is List<object> rl) node.Required = rl.Cast<string>().ToList();

            if (dict.TryGetValue("properties", out var pr) && pr is Dictionary<string, object?> pd)
                foreach (var kv in pd)
                {
                    if (kv.Value is Dictionary<string, object?> subDict)
                        node.Properties[kv.Key] = Build(subDict);
                    else if (kv.Value is string s)
                        node.Properties[kv.Key] = Build(new Dictionary<string, object?> { ["type"] = s });
                }

            if (dict.TryGetValue("items", out var it))
            {
                if (it is Dictionary<string, object?> itDict) node.Items = Build(itDict);
                else if (it is string its) node.Items = Build(new Dictionary<string, object?> { ["type"] = its });
            }

            if (dict.TryGetValue("additionalProperties", out var ap))
            {
                switch (ap)
                {
                    case bool b:
                        node._additionalPropertiesAllowed = b;
                        break;
                    case Dictionary<string, object?> ad:
                        node.AdditionalPropertiesSchema = Build(ad);
                        node._additionalPropertiesAllowed = true;
                        break;
                }
            }

            return node;
        }

        /// <summary>
        /// Validates a data node against this schema node.
        /// </summary>
        /// <param name="dataNode">The data node.</param>
        /// <param name="errors">The list of errors.</param>
        /// <param name="path">The current path.</param>
        public void Validate(SpittoonNode dataNode, List<ValidationError> errors, string path)
        {
            if (ExpectedNodeType.HasValue && dataNode.NodeType != ExpectedNodeType.Value)
            {
                errors.Add(new ValidationError(path, $"Expected node type {ExpectedNodeType} but found {dataNode.NodeType}"));
                return;
            }

            if (dataNode is SpittoonValueNode valueNode)
            {
                if (PrimitiveType != null)
                {
                    bool ok = PrimitiveType switch
                    {
                        "str" => valueNode.Value is string,
                        "int" => valueNode.Value is long,
                        "float" => valueNode.Value is double or long,
                        "bool" => valueNode.Value is bool,
                        "null" => valueNode.Value == null,
                        _ => true
                    };

                    if (!ok)
                    {
                        errors.Add(new ValidationError(path, $"Expected primitive type {PrimitiveType}"));
                        return;
                    }
                }

                if (EnumValues != null && !EnumValues.Contains(valueNode.Value))
                    errors.Add(new ValidationError(path, "Value not in enum"));

                if ((MinValue.HasValue || MaxValue.HasValue || ExclusiveMin || ExclusiveMax) && valueNode.Value is IConvertible)
                {
                    try
                    {
                        double d = Convert.ToDouble(valueNode.Value, CultureInfo.InvariantCulture);
                        if (MinValue.HasValue && d < MinValue) errors.Add(new ValidationError(path, "Value below minimum"));
                        if (MaxValue.HasValue && d > MaxValue) errors.Add(new ValidationError(path, "Value above maximum"));
                        if (ExclusiveMin && MinValue.HasValue && d <= MinValue.GetValueOrDefault()) errors.Add(new ValidationError(path, "Value violates exclusiveMin"));
                        if (ExclusiveMax && MaxValue.HasValue && d >= MaxValue.GetValueOrDefault()) errors.Add(new ValidationError(path, "Value violates exclusiveMax"));
                    }
                    catch
                    {
                        // non-numeric value when numeric constraints expected -> report error
                        errors.Add(new ValidationError(path, "Value is not numeric for numeric constraints"));
                    }
                }

                if (valueNode.Value is string s)
                {
                    if (Pattern != null && !Pattern.IsMatch(s)) errors.Add(new ValidationError(path, "String does not match pattern"));
                    if (MinLength.HasValue && s.Length < MinLength) errors.Add(new ValidationError(path, "String too short"));
                    if (MaxLength.HasValue && s.Length > MaxLength) errors.Add(new ValidationError(path, "String too long"));
                }

                return;
            }

            if (dataNode is SpittoonObjectNode objNode)
            {
                // Special-case: if object has a 'header' and 'rows' pair, ensure each row matches header columns and column types
                if (objNode.Properties.TryGetValue("header", out var hdr) && objNode.Properties.TryGetValue("rows", out var rws))
                {
                    if (hdr is SpittoonObjectNode hdrObj && rws is SpittoonArrayNode rowsArr)
                    {
                        var headerKeys = new List<string>(hdrObj.Properties.Keys);

                        bool MatchesType(SpittoonNode? node, string? typeName)
                        {
                            if (typeName == null) return true;
                            if (node is SpittoonValueNode vn)
                            {
                                var v = vn.Value;
                                return typeName switch
                                {
                                    "int" => v is long,
                                    "float" => v is double or long,
                                    "str" => v is string,
                                    "bool" => v is bool,
                                    "null" => v == null,
                                    _ => true,
                                };
                            }
                            // non-value nodes don't match primitive types
                            return false;
                        }

                        for (int i = 0; i < rowsArr.Items.Count; i++)
                        {
                            var row = rowsArr.Items[i];
                            string rowPath = string.IsNullOrEmpty(path) ? $"rows[{i}]" : $"{path}/rows[{i}]";

                            if (row is SpittoonObjectNode rowObj)
                            {
                                foreach (var hk in headerKeys)
                                {
                                    if (!rowObj.Properties.TryGetValue(hk, out var cell) || (cell is SpittoonValueNode vcell && vcell.Value == null))
                                    {
                                        errors.Add(new ValidationError(rowPath, $"Missing column '{hk}'"));
                                        continue;
                                    }

                                    string? neededType = null;
                                    if (hdrObj.Properties.TryGetValue(hk, out var headerTypeNode) && headerTypeNode is SpittoonValueNode htv && htv.Value is string s)
                                        neededType = s;

                                    if (!MatchesType(cell, neededType))
                                        errors.Add(new ValidationError(rowPath, $"Column '{hk}' does not match header type {neededType}"));
                                }
                            }
                            else if (row is SpittoonArrayNode rowArr)
                            {
                                if (rowArr.Items.Count < headerKeys.Count)
                                    errors.Add(new ValidationError(rowPath, $"Array must contain at least {headerKeys.Count} items"));
                                else if (rowArr.Items.Count > headerKeys.Count)
                                    errors.Add(new ValidationError(rowPath, $"Array has more items than header columns"));
                                else
                                {
                                    for (int col = 0; col < headerKeys.Count; col++)
                                    {
                                        var hk = headerKeys[col];
                                        var cell = rowArr.Items[col];
                                        string? neededType = null;
                                        if (hdrObj.Properties.TryGetValue(hk, out var headerTypeNode) && headerTypeNode is SpittoonValueNode htv && htv.Value is string s)
                                            neededType = s;

                                        if (!MatchesType(cell, neededType))
                                            errors.Add(new ValidationError(rowPath, $"Column '{hk}' does not match header type {neededType}"));
                                    }
                                }
                            }
                            else
                            {
                                if (headerKeys.Count > 1)
                                    errors.Add(new ValidationError(rowPath, "Row scalar cannot be mapped to multi-column header"));
                            }
                        }
                    }
                }

                if (MinProperties.HasValue && objNode.Properties.Count < MinProperties) errors.Add(new ValidationError(path, $"Object must contain at least {MinProperties} properties"));
                if (MaxProperties.HasValue && objNode.Properties.Count > MaxProperties) errors.Add(new ValidationError(path, $"Object must contain at most {MaxProperties} properties"));

                if (Required != null)
                    foreach (var r in Required)
                    {
                        if (!objNode.Properties.TryGetValue(r, out var val) || (val is SpittoonValueNode vv && vv.Value == null))
                            errors.Add(new ValidationError(path, $"Missing required property '{r}'"));
                    }

                foreach (var kv in objNode.Properties)
                {
                    string childPath = string.IsNullOrEmpty(path) ? kv.Key : $"{path}/{kv.Key}";
                    var schema = Properties.TryGetValue(kv.Key, out var ps) ? ps : AdditionalPropertiesSchema;

                    if (schema == null && !_additionalPropertiesAllowed)
                    {
                        errors.Add(new ValidationError(childPath, $"additional property '{kv.Key}' not allowed"));
                        continue;
                    }

                    (schema ?? new SchemaNode()).Validate(kv.Value, errors, childPath);
                }

                return;
            }

            if (dataNode is SpittoonArrayNode arrNode)
            {
                int count = arrNode.Items.Count;
                if (MinItems.HasValue && count < MinItems) errors.Add(new ValidationError(path, $"Array must contain at least {MinItems} items"));
                if (MaxItems.HasValue && count > MaxItems) errors.Add(new ValidationError(path, $"Array must contain at most {MaxItems} items"));

                if (UniqueItems)
                {
                    var seen = new HashSet<object?>();
                    for (int i = 0; i < count; i++)
                        if (!seen.Add(arrNode.Items[i]))
                            errors.Add(new ValidationError($"{path}[{i}]", "Duplicate item in uniqueItems array"));
                }

                var itemSchema = Items ?? new SchemaNode();
                for (int i = 0; i < count; i++)
                    itemSchema.Validate(arrNode.Items[i], errors, $"{path}[{i}]");
            }
        }
    }

    private List<ValidationError> CheckTabularOnNodes(SpittoonNode node)
    {
        var errors = new List<ValidationError>();
        void Walk(SpittoonNode n, string path)
        {
            if (n is SpittoonObjectNode obj)
            {
                if (obj.Properties.TryGetValue("header", out var hdr) && obj.Properties.TryGetValue("rows", out var rws))
                {
                    if (hdr is SpittoonObjectNode hdrObj && rws is SpittoonArrayNode rowsArr)
                    {
                        var headerKeys = new List<string>(hdrObj.Properties.Keys);
                        for (int i = 0; i < rowsArr.Items.Count; i++)
                        {
                            var row = rowsArr.Items[i];
                            string rowPath = string.IsNullOrEmpty(path) ? $"rows[{i}]" : $"{path}/rows[{i}]";

                            if (row is SpittoonArrayNode rowArr)
                            {
                                if (rowArr.Items.Count < headerKeys.Count)
                                    errors.Add(new ValidationError(rowPath, $"Array must contain at least {headerKeys.Count} items"));
                            }
                            else if (row is SpittoonObjectNode rowObj)
                            {
                                foreach (var hk in headerKeys)
                                {
                                    if (!rowObj.Properties.TryGetValue(hk, out var cell) || (cell is SpittoonValueNode vcell && vcell.Value == null))
                                        errors.Add(new ValidationError(rowPath, $"Missing column '{hk}'"));
                                }
                            }
                            else
                            {
                                if (headerKeys.Count > 1)
                                    errors.Add(new ValidationError(rowPath, "Row scalar cannot be mapped to multi-column header"));
                            }
                        }
                    }
                }

                foreach (var kv in obj.Properties)
                {
                    var childPath = string.IsNullOrEmpty(path) ? kv.Key : $"{path}/{kv.Key}";
                    Walk(kv.Value, childPath);
                }
            }
            else if (n is SpittoonArrayNode arr)
            {
                for (int i = 0; i < arr.Items.Count; i++) Walk(arr.Items[i], $"{path}[{i}]");
            }
        }

        Walk(node, "");
        return errors;
    }
}