# Strings

## String Literals

Lux supports all Lua string forms:

```lux
local a = "double quoted"
local b = 'single quoted'
local c = [[long bracket string
spans multiple lines]]
local d = [==[nested brackets]==]
```

## String Interpolation

When `string_interpolation = true` in config, backtick strings support embedded expressions:

```lux
local name = "Alice"
local age = 30
local msg = `Hello, {name}! You are {age} years old.`
```

Compiles to:

```lua
local msg = "Hello, " .. tostring(name) .. "! You are " .. tostring(age) .. " years old."
```

### Expression Interpolation

Any expression can be embedded:

```lux
local result = `2 + 2 = {2 + 2}`
local info = `Length: {#items}, First: {items[1]}`
```

### Escaping

```lux
local escaped = `Use \{braces\} literally`
local backtick = `Use \` for backticks`
```

## String Concat Operator

The `..` operator always works for concatenation:

```lux
local greeting = "Hello" .. " " .. "World"
```

With `concat_operator = "+"` in config, `+` also works:

```lux
local greeting = "Hello" + " " + "World"
```

This compiles to a helper function that handles both string and number addition.
