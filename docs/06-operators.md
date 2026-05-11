# Operators

## Standard Lua Operators

### Arithmetic

| Operator | Description      |
|----------|------------------|
| `+`      | Addition         |
| `-`      | Subtraction      |
| `*`      | Multiplication   |
| `/`      | Division         |
| `//`     | Floor division   |
| `%`      | Modulo           |
| `^`      | Exponentiation   |
| `-x`     | Unary negation   |

### Comparison

| Operator | Description      |
|----------|------------------|
| `==`     | Equal            |
| `~=`     | Not equal        |
| `<`      | Less than        |
| `>`      | Greater than     |
| `<=`     | Less or equal    |
| `>=`     | Greater or equal |

### Logical

| Operator | Description |
|----------|-------------|
| `and`    | Logical AND |
| `or`     | Logical OR  |
| `not`    | Logical NOT |

### String & Other

| Operator | Description       |
|----------|-------------------|
| `..`     | Concatenation     |
| `#`      | Length            |

### Bitwise (Lua 5.3+)

| Operator | Description  |
|----------|-------------|
| `&`      | Bitwise AND |
| `\|`     | Bitwise OR  |
| `~`      | Bitwise XOR |
| `~x`     | Bitwise NOT |
| `<<`     | Left shift  |
| `>>`     | Right shift |

## Alternative Boolean Operators

When `alt_boolean_operators = true` in config:

```lux
a && b        -- same as: a and b
a || b        -- same as: a or b
!a            -- same as: not a
a != b        -- same as: a ~= b
```

These compile directly to their Lua equivalents.

## Increment / Decrement

Lux adds C-style increment and decrement operators. Because `--` is a Lua comment, decrement uses `~~` instead:

```lux
x++           -- post-increment (returns old value)
++x           -- pre-increment (returns new value)
x~~           -- post-decrement (returns old value)
~~x           -- pre-decrement (returns new value)
```

Works on any l-value:

```lux
arr[i]++
obj.count~~
++matrix[x][y]
```

Compiles to runtime helper functions:

```lua
-- Post-increment example
local __old = x; x = __old + 1  -- returns __old
-- Pre-increment example
x = x + 1                        -- returns x
```

## Nil Coalescing (`??`)

Returns the left operand if it is not nil, otherwise the right operand:

```lux
local name = user?.name ?? "Anonymous"
local port = config.port ?? 8080
```

Compiles to: `(function() local __v = left; if __v ~= nil then return __v end return right end)()`

## Non-Nil Assertion (`!`)

Asserts that a value is not nil. The type checker treats the result as non-nullable:

```lux
local name: string? = getName()
print(name!)                   -- type: string (not string?)
```

The `!` postfix operator is a compile-time assertion. No runtime code is emitted.

## Optional Chaining (`?.`)

Safely access fields on a potentially nil value:

```lux
local value = obj?.field       -- nil if obj is nil
local nested = a?.b?.c         -- chains safely
```

## Optional Call (`?()`)

Safely call a potentially nil function:

```lux
local result = callback?()     -- nil if callback is nil
```

## Type Check (`is`)

Runtime type check:

```lux
if x is number then
    print(x + 1)
end
```

See [Types](02-types.md) for details.

## Type Cast (`as`)

Compile-time type assertion (no runtime effect):

```lux
local n = value as number
```

## Operator Precedence (low to high)

1. `or`, `||`
2. `??`
3. `and`, `&&`
4. Comparison: `<`, `>`, `<=`, `>=`, `~=`, `==`, `!=`
5. `is`, `as`
6. `|` (bitwise OR)
7. `~` (bitwise XOR)
8. `&` (bitwise AND)
9. `<<`, `>>` (shifts)
10. `..` (concatenation, right-associative)
11. `+`, `-`
12. `*`, `/`, `//`, `%`
13. Postfix: `!`, `++`, `~~`
14. Unary: `not`, `!`, `#`, `-`, `~`, `await`, `++`, `~~`
15. `^` (exponentiation, right-associative)

## Configurable Concat Operator

With `concat_operator = "+"` in config, the `+` operator can be used for string concatenation:

```lux
local msg = "Hello" + " " + "World"  -- compiles to .. in Lua
```

## Runtime Type Introspection

### `typeof`

Returns a string describing the runtime type of a value. For classes and enums the returned string is the declared name; for primitives it matches Lua's `type()` result.

```lux
typeof 42            -- "number"
typeof "hi"          -- "string"
typeof myDog         -- "Dog"
typeof Colors.Red    -- "Colors" (when static type is known as enum)
```

When the static type is known at compile time (class or enum), the name is inlined as a string literal. Otherwise a runtime helper walks the metatable chain for a `__name` marker and falls back to `type()`.

### `instanceof`

Runtime check whether a value is an instance of the given class (including subclasses).

```lux
if pet instanceof Dog then
    pet:bark()
end
```

Walks the metatable chain so both direct and inherited class matches return true. Returns `false` for non-table values or unknown classes.
