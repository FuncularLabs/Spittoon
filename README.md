### SPITTOON–Semicolon-Punctuated, Interoperable, Tersely-Typed Open Object Notation

**Version:** 0.1.5

![SPITTOON logo](https://raw.githubusercontent.com/FuncularLabs/Spittoon/main/spittoon-logo-small.jpg)

[![NuGet](https://img.shields.io/nuget/v/Spittoon.Core.svg)](https://www.nuget.org/packages/Spittoon.Core)
[![Downloads](https://img.shields.io/nuget/dt/Spittoon.Core.svg)](https://www.nuget.org/packages/Spittoon.Core)
[![CI status](https://img.shields.io/github/actions/workflow/status/FuncularLabs/Spittoon/ci.yml?branch=main&label=CI)](https://github.com/FuncularLabs/Spittoon/actions)
[![Tests](https://img.shields.io/github/actions/workflow/status/FuncularLabs/Spittoon/ci.yml?branch=main&label=Tests)](https://github.com/FuncularLabs/Spittoon/actions)

## Reserved keywords

SPITTOON uses a small set of well-known member names for special data shapes. The two primary reserved keywords are:

- `header` — declares a typed column header for tabular sections
- `rows` — contains the rows for a tabular section (arrays or objects normalized to rows)

If your object model naturally contains properties named `header` or `rows`, you have several options:

- Rename the property in source and use `[SpittoonName("header")]` (or `rows`) on the property to explicitly map the name.
- Keep the property but quote the key in the `.spit` file so the parser treats it as a normal object member (e.g. `"header": { ... }`).
- Nest your model under a container object so the top-level `header`/`rows` pair is not confused with your domain property.

Reserved keywords are only treated specially when they form a `header`+`rows` pair with the appropriate shapes; otherwise they behave like normal members.

## Recommended schema extension

We recommend using the extension `.spitsd` for SPITTOON schema files (Spittoon Schema Definition).

Because JSON had too much baggage, CSV was too flat, TOON too quirky in the brackets, and we needed something with actual spit. Terse, human-readable, hierarchical + tabular-ready serialization: comma-or-semicolon separators, optional SSCH schema validation, and a mullet that’s strictly business in the front, pure party in the back.

> First there was CSV. Then XML arrived wearing a tuxedo nobody asked for. Then JSON showed up, drunk on quotes and braces. Then TOON tried to fix everything and mostly just confused people. And now, finally, the format to end the format wars: **SPITTOON**.

## Why SPITTOON exists

- JSON is quote-heavy and verbose — delightful for machines, exhausting for humans who must edit configuration at 02:13.
- XML is a war crime against readability; it insists every opening tag bring a plus-one.
- CSV can’t represent hierarchy without melting into a puddle of ambiguity.
- TOON’s bracketed counts and mandatory semicolons feel like a tax on comprehension.
- Real-world config files and log payloads are getting bigger and more nested, yet humans still need to read and tweak them.

SPITTOON aims to be the smallest set of sensible rules that solves these problems. It is:

- More compact than JSON (fewer quotes, terser separators).
- Strictly unambiguous (no `is this key quoted or not?` arguments at 3 a.m.).
- Actually pleasant to hand-edit (line-oriented, optional semicolons for cleanliness).
- Supporting proper typed tabular sections (think CSV with structure and nesting).

## Comparison (short and rude)

| Format | Readability | Typical Size | Feature completeness |
|---|---:|---:|---|
| CSV | Fast to scan if flat; hopeless with nesting | Small | Flat only — collapses under pressure |
| XML | Machine-robust, human-hostile | Large | Complete, but did you enjoy reading it? |
| JSON | Familiar, verbose, quote-hungry | Medium | Great all-rounder; heavy on punctuation |
| TOON | Ambitious, fiddly | Medium | Adds complexity that rarely pays for itself |
| SPITTOON | Designed for humans who still care | Small → Medium | Compact, unambiguous, tabular + nested — actually useful |

## Hello World

Simple object (valid SPITTOON — strings with spaces are quoted):

```csharp
/* hello.spit */
{
  message: "Hello, world",
  count: 3,
  active: true
}
```

A real-world before/after — typical appsettings.json (abridged)

```csharp
// appsettings.json (typical, quote soup)
{
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft": "Warning" },
    "Console": { "IncludeScopes": false }
  },
  "ConnectionStrings": { "DefaultConnection": "Server=.;Database=App;Trusted_Connection=True;" },
  "FeatureFlags": { "NewSearch": true }
}
```

Same thing in SPITTOON (explicit, balanced punctuation, connection string quoted because of delimiters):

```csharp
/* appsettings.spit */
{
  Logging: {
    LogLevel: { Default: Information, Microsoft: Warning },
    Console: { IncludeScopes: false }
  },
  ConnectionStrings: { DefaultConnection: "Server=.;Database=App;Trusted_Connection=True;" },
  FeatureFlags: { NewSearch: true }
}
```

## Usage (C#)

Serializing a POCO to a `.spit` file:

```csharp
// create and write .spit file
var poco = new AppSettings { /* ... */ };
var serializer = new SpittoonSerializer();
string text = serializer.Serialize(poco, Formatting.Indented);
File.WriteAllText("appsettings.spit", text); // yes, really that simple
```

Deserializing to a strongly-typed object:

```csharp
// read and map to strongly-typed object
string raw = File.ReadAllText("appsettings.spit");
var deserializer = new SpittoonDeserializer();
var settings = deserializer.Deserialize<AppSettings>(raw);
// properties are mapped by name; use [SpittoonName("custom")] to rename
```

### Forgiving vs Strict mode

```csharp
// Forgiving: acceptable for human-written configs (allows small syntax relaxations)
var forgiving = new SpittoonDeserializer(SpittoonMode.Forgiving);
var dyn = forgiving.DeserializeDynamic(File.ReadAllText("loose.spit"));

// Strict: used for validation pipelines and CI gates — rejects ambiguities
var strict = new SpittoonDeserializer(SpittoonMode.Strict);
var typed = strict.Deserialize<MyConfig>(File.ReadAllText("clean.spit"));
```

### Simple syntax validation

```csharp
bool ok = Spittoon.Validation.SpittoonValidator.IsValid(text, SpittoonMode.Forgiving);
var result = Spittoon.Validation.SpittoonValidator.ValidateSyntax(text);
// result contains detailed error info when syntax is broken
```

### Full SSCH schema validation

```csharp
// load schema (SSCH is a compact schema language for SPITTOON)
var ssch = File.ReadAllText("my-schema.spit");
var validator = new Spittoon.Validation.SschValidator(ssch);
var validation = validator.Validate(File.ReadAllText("candidate.spit"), SpittoonMode.Strict);
if (!validation.IsValid) foreach (var e in validation.Errors) Console.WriteLine(e);
```

## File extension

We strongly recommend `*.spit`. If you are terminally verbose, `*.spittoon` is also allowed but please seek help.

## Tabular examples — because real data repeats

When you have lots of repeated rows (user lists, metrics, logs), JSON becomes a parade of identical keys, CSV loses structure, and both invite typos. SPITTOON lets you declare a header once and express rows tersely — with optional column types — so the same information is clearer and smaller.

JSON (verbose, repetitive):

```json
[
  { "id": 1, "name": "Alice", "active": true },
  { "id": 2, "name": "Bob",   "active": false },
  { "id": 3, "name": "Carol", "active": true }
]
```

SPITTOON tabular form (clear header, compact rows). Note: header types are recommended; rows omit labels for brevity:

```csharp
users: {
  header: { id:int, name:str, active:bool },
  rows: [
    [1, Alice, true],
    [2, Bob, false],
    [3, Carol, true]
  ]
}
```

If you prefer the rows as objects (more explicit, still compact):

```csharp
users: {
  header: { id:int, name:str, active:bool },
  rows: [
    { id: 1, name: Alice, active: true },
    { id: 2, name: Bob,   active: false },
    { id: 3, name: Carol, active: true }
  ]
}
```

Why this helps:

- The header documents column names and types once — less noise when skimming.
- Rows are short and line-oriented; commas (or semicolons) make row boundaries obvious in formatted output.
- Parsers normalize arrays-of-arrays into arrays-of-objects for convenient access (`header` + `rows` → list of dictionaries).
- Compared to the JSON form above you typically shave both characters and cognitive load.

A slightly more realistic example — log lines with metadata. Per RFC, timestamps and messages containing colons or spaces must be quoted:

```csharp
logs: {
  header: { ts:str, lvl:str, msg:str, meta:obj },
  rows: [
    ["2025-01-01T12:00:00Z", INFO, "Started", { pid: 123 }],
    ["2025-01-01T12:01:00Z", WARN, "Slow query", { ms: 512 }]
  ]
}
```

The serializer and validator already understand this `header`+`rows` shape and will normalize rows for you — meaning short, readable source and convenient programmatic access.

## Badges

[![NuGet](https://img.shields.io/nuget/v/Spittoon.Core.svg)](https://www.nuget.org/packages/Spittoon.Core)
[![Downloads](https://img.shields.io/nuget/dt/Spittoon.Core.svg)](https://www.nuget.org/packages/Spittoon.Core)
[![CI status](https://img.shields.io/github/actions/workflow/status/FuncularLabs/Spittoon/ci.yml?branch=main&label=CI)](https://github.com/FuncularLabs/Spittoon/actions)
[![Tests](https://img.shields.io/github/actions/workflow/status/FuncularLabs/Spittoon/ci.yml?branch=main&label=Tests)](https://github.com/FuncularLabs/Spittoon/actions)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Go forth. Expectoration of clean data is now in your hands.