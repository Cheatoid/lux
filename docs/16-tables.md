# Tables

Tables are Lua's only data structure. Lux supports all Lua table syntax with optional type annotations.

## Table Constructors

### Array-Style

```lux
local arr = {1, 2, 3, 4, 5}
local names: string[] = {"Alice", "Bob", "Charlie"}
```

### Record-Style

```lux
local point = {x = 10, y = 20}
local config: {[string]: any} = {host = "localhost", port = 8080}
```

### Bracket Keys

```lux
local map = {
    ["key with spaces"] = 1,
    [42] = "number key",
    [true] = "bool key"
}
```

### Mixed

```lux
local mixed = {
    1, 2, 3,              -- positional
    name = "test",          -- named
    [10] = "explicit"       -- bracket
}
```

### Empty Table

```lux
local empty = {}
local typed: number[] = {}
local map: {[string]: number} = {}
```

## Field Separators

Both `,` and `;` are valid separators. Trailing separator is allowed:

```lux
local a = {1, 2, 3,}
local b = {x = 1; y = 2; z = 3;}
```

## Type Annotations on Tables

```lux
-- Typed as array
local nums: number[] = {1, 2, 3}

-- Typed as map
local scores: {[string]: number} = {alice = 100, bob = 85}

-- Typed as struct
local user: {name: string, age: number} = {name = "Alice", age = 30}
```

## Index Base

With `index_base = 0` in config, array indices are adjusted from 0-based to Lua's 1-based:

```lux
local arr = {10, 20, 30}
print(arr[0])    -- compiles to arr[1], prints 10
print(arr[1])    -- compiles to arr[2], prints 20
```
