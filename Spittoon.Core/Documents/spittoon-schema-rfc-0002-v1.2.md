## SPIT-RFC 0001: SPITTOON — v1.2
### Semicolon-Punctuated, Interoperable, Tersely-Typed Open Object Notation 

![Spittoon Logo Small](spittoon-logo-small.jpg)

**Status:** Proposed  
**Author:** Grok (with a nod to the prompt's architects)  
**Date:** November 17, 2025  
**Abstract:**  
SPITTOON (Semicolon-Punctuated, Interoperable, Tersely-Typed Open Object Notation) is a lightweight, human-readable serialization format designed for the efficient exchange of structured data. It balances compactness with unambiguity, drawing inspiration from JSON's hierarchy, CSV's tabular brevity, and TOON's unquoted terseness—while sidestepping their pitfalls, like JSON's verbosity or CSV's relational rigidity. SPITTOON excels in scenarios demanding quick parsing of mixed-type collections, such as configuration files, API payloads, or log aggregations, where every byte counts but so does the sanity of the engineer squinting at it in a dimly lit server room.

Think of SPITTOON as the mullet of data formats: business in the front (structured and professional), party in the back (compact enough to fit more in your pocket). This v1.1 refines punctuation for sleeker flow—semicolons optional inside containers, collections harmoniously enclosed where it counts—ensuring deterministic behavior across implementations. More examples herein, because nothing polishes a spec like a parade of payloads.

#### 1. Core Principles
- **Human Readability:** Whitespace-insensitive where possible, but encourages indentation for nested structures.
- **Unambiguity:** Strict tokenization rules prevent parsing collisions.
- **Compactness:** No quotes on unreserved labels or simple values; optional semicolons inside containers.
- **Capability:** Supports primitives, complex objects, homogeneous/heterogeneous collections.
- **Terseness:** Omit redundant syntax (e.g., no array length indicators) without sacrificing clarity.

#### 2. Lexical Elements
SPITTOON is tokenized into values, labels, and delimiters. Tokens are separated by whitespace (spaces, tabs, newlines) or punctuation. Leading/trailing whitespace is ignored except within quoted strings.

##### 2.1 Primitives
Primitives are unquoted unless they require escaping (see Section 4).

| Type      | Representation                  | Examples                  |
|-----------|---------------------------------|---------------------------|
| Null      | `null`                          | `status:null`             |
| Boolean   | `true` / `false`                | `active:true`             |
| Integer   | Sequence of digits, optional `-` prefix | `count:42`, `offset:-5`   |
| Float     | Integer with optional `.` and trailing digits, optional `-` prefix | `pi:3.14159`, `gravity:-9.8` |
| String    | Unquoted if no special chars; quoted with `"` if needed | `name:Alice`, `greeting:"Hello, World!"` |

- Integers/floats must not have leading zeros unless zero itself (`0` or `0.0`).
- Strings without spaces, quotes, or delimiters can be unquoted for brevity.
- Examples in context (nested in objects below).

##### 2.2 Complex Types
Complex types (objects) are enclosed in `{}` and consist of key-value pairs separated by `:` and commas.

- **Syntax:** `{ label1:value1, label2:value2, ... }`
- Keys (labels) are unquoted identifiers: alphanumeric + `_` + `-`, starting with alphanumeric or `_`.
- Values can be primitives, nested complexes, or collections.
- Commas or semicolons separate pairs (interchangeable); trailing comma or semicolon is ignored (closing `}` implies termination).
- Examples:
```json
// Simple object with primitives
person:{ name:Alice, age:30, active:true }
// Nested complex with optional semicolon
address:{ street:"123 Main St", city:Anytown, zip:12345; }  // Semicolon optional here
// Mixed with collections (see 2.3)
profile:{ user:Alice, scores:[95, 87], tags:["dev", "music"] }
```

##### 2.3 Collections
Collections denote arrays/lists, enclosed in `[]`, with comma-separated elements.

- **Syntax:** `[ value1, value2, ... ]`
- Homogeneous: All elements same type (inferred, not enforced at syntax level).
- Heterogeneous: Mixed types allowed.
- No count in brackets (unlike TOON); length inferred from elements.
- Commas or semicolons separate elements (interchangeable); trailing comma or semicolon is ignored (closing `]` implies termination).
- Nested collections permitted; semicolon optional after last element (closing `]` implies termination).
- Examples:
```json
// Homogeneous integers
scores:[ 95, 87, 92 ]
// Heterogeneous mix
mixed:[ "apple", { color:red, ripe:true }, 3.14, null ]
// Nested collections
nested:[ [1, 2], [3, 4, 5] ]
// Empty collection
empty:[ ]
// With optional semicolon (forgiving parse ignores)
trailing:[ "first", "last"; ]  // Equivalent to [ "first", "last" ]
```

##### 2.4 Tabular Mode (Header-Declared Rows)
For repeated structures (like CSV), declare a named collection with a header complex type, followed by a colon, then enclosed comma-separated rows matching that structure.

- **Syntax:** `collectionName:{ header:{ label1:type1, label2:type2, ... }; }: [ row1, row2, ... ]`
- Types in header are optional but recommended for schema alignment (e.g., `int`, `str`, `bool`, `float`, `obj`, `arr`).
- Rows SHOULD omit repeating labels to keep tabular data compact; implementations will serialize rows without labels and deserializers will accept both unlabeled rows (preferred) and labeled rows (tolerated for compatibility).
- Commas or semicolons separate rows (interchangeable); trailing comma or semicolon is ignored (closing `]` implies termination).

Examples:

```json
// Preferred (rows omit labels after header)
employees:{ header:{ id:int, name:str, salary:float }; }: [
  [1, Bob, 75000.0],
  [2, Carol, 82000.0]
]

// Nested objects in rows (values can be arrays/objects) — labels omitted in rows
projects:{ header:{ name:str, tasks:arr, priority:int }; }: [
  [Spittoon, [RFC, Impl, Test], 10],
  [SSCH, [Schema, Validate], 5]
]

// One-time example showing labeled rows are allowed (tolerated), but not required — parsers accept this form
legacy_allowed_labeled:{ header:{ status:str }; }: [
  { status:ok }, // labeled row — allowed but not expected
  { status:deprecated }
]

// Empty tabular
legacy:{ header:{ status:str }; }: [ ]
```

(End of Tabular Mode section — other examples in this document use the compact, unlabeled row form after the header.)

#### 6. Full Examples
A configuration file with mixed elements, comments, and variety:
```json
/* Spittoon Config - Because JSON was too chatty, and XML too verbose. v1.2: Tabular rows are compact by default. */
server:{ host:localhost, port:8080, secure:false };
// Unnamed heterogeneous collection of endpoints
endpoints:[
  { path:"/api/users", method:GET, auth:true },
  { path:"/api/logs", method:POST, auth:{ role:admin, level:1 } }
];

// Tabular users with header and unlabeled rows (preferred)
users:{ header:{ id:int, name:str, active:bool, roles:arr }; }: [
  [1, Alice, true, [user, editor]],
  [2, Bob, false, [viewer]],
  [3, Carol, true, [admin]]
];

// Nested object with empty collection
cache:{ enabled:true, ttl:3600, invalidations:[ ] }  // Empty array for no ops

// Root-level multi-statement (semicolon separated)
version:1.2; status:ready
```

```json
schema: {
  version: 1.0;
  id: "urn:spittoon:contest";
  title: "Spittoon Entry Schema";
  description: "For judging the arc and accuracy of expectorations.";
  type: arr{1,100};
  items: {
    type: obj;
    properties: {
      contestant: {
        type: str;
        pattern: /^[A-Z][a-z]+ [A-Z][a-z]+$/;
        minLength: 5;
        description: "Full name, because anonymity is for cowards.";
      };
      distance: {
        type: float;
        min: 0;
        max: 50.0;
        exclusiveMax: true;
        description: "Meters from cuspidor; closer is no fun.";
      };
      style: {
        type: str;
        enum: [arc, straight, spin];
        description: "Delivery technique—spin for extra points.";
      };
      witnesses: {
        type: arr{2,5};
        items: { type: str; pattern: /^Witness \d+$/; };
      };
    };
    required: [contestant, distance];
    additionalProperties: false;
  };
};
/* Note: No 'polish' property—spit don't shine. */

