# Type System

Lux adds an optional, gradual type system to Lua. All type annotations are compile-time only and stripped from the generated Lua output.

## Primitive Types

| Type      | Description            |
|-----------|------------------------|
| `number`  | Any number (int/float) |
| `string`  | Text                   |
| `boolean` | `true` or `false`      |
| `nil`     | The nil value          |
| `any`     | Opt out of type checks |
| `void`    | No return value        |

```lux
local count: number = 42
local name: string = "Lux"
local active: boolean = true
```

## Nullable Types

Append `?` to make a type nullable (equivalent to `T | nil`):

```lux
local name: string? = nil
local age: number? = 25
```

## Union Types

Use `|` to allow multiple types:

```lux
local id: string | number = "abc"
local result: boolean | nil = true
```

## Array Types

Append `[]` for arrays:

```lux
local nums: number[] = {1, 2, 3}
local matrix: number[][] = {{1, 2}, {3, 4}}
local names: string?[] = {"a", nil, "c"}  -- array of nullable strings
local data: number[]? = nil               -- nullable array
```

## Map Types

```lux
local config: { [string]: number } = { timeout = 30, retries = 3 }
local lookup: { [number]: string } = { [1] = "one", [2] = "two" }
```

## Struct Types

Named fields with types:

```lux
local point: { x: number, y: number } = { x = 10, y = 20 }
local user: { name: string, age: number, active: boolean }
```

### Meta Fields

```lux
local mt: { meta __index: (any) -> any, value: number }
```

## Function Types

```lux
local callback: (number, string) -> boolean
local handler: (string) -> void
local factory: () -> (number, string)       -- multi-return
```

## Tuple Types

For multi-return values:

```lux
local result: (number, string) = getResult()
function multi(): (string, number, boolean)
    return "ok", 42, true
end
```

## Type Check Expression (`is`)

Check a value's type at runtime:

```lux
if x is number then
    print(x + 1)
end

if value is string then
    print(#value)
end
```

Compiles to `type(x) == "number"`.

## Type Cast Expression (`as`)

Assert a type at compile-time (no runtime check):

```lux
local x: any = 42
local n: number = x as number
```

## Grouping

Use parentheses to group complex types:

```lux
local arr: (string | number)[] = {"hello", 42}
```
