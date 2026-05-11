# Variables & Constants

## Local Variables

```lux
local x = 42
local name: string = "hello"
local a, b, c = 1, 2, 3
local typed_a: number, typed_b: string = 1, "two"
```

## Mutability

By default, variables are mutable. With `immutable_default = true` in config, variables are immutable by default:

```lux
-- With immutable_default = false (default)
local x = 1            -- mutable
local y <const> = 2    -- immutable (Lua 5.4 attribute)

-- With immutable_default = true
local x = 1            -- immutable by default
local mut y = 2        -- explicitly mutable with `mut`
```

## Lua 5.4 Attributes

```lux
local x <const> = 42
local file <close> = io.open("data.txt")
```

## Deep Freeze

When `deep_freeze = true` in config, immutable table variables are frozen at runtime:

```lux
local data = { a = 1, b = 2 }
-- data.a = 3  -- runtime error: attempt to modify frozen table
```

Compiles to a `setmetatable` wrapper that blocks `__newindex`.

## Assignment

```lux
x = 42
a, b = b, a                      -- swap
obj.field = "value"
arr[1] = "first"
```

## Multiple Assignment

```lux
local x, y, z = 1, 2, 3
x, y = y, x                      -- swap
```
