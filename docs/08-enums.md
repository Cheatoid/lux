# Enums

Enums define a set of named constants.

## Basic Enum

Values are auto-numbered starting from 0:

```lux
enum Color
    Red
    Green
    Blue
end
```

Compiles to:

```lua
local Color = {Red = 0, Green = 1, Blue = 2}
```

## Explicit Values

```lux
enum HttpStatus
    Ok = 200
    NotFound = 404
    ServerError = 500
end
```

## String Enums

```lux
enum Direction
    Up = "up"
    Down = "down"
    Left = "left"
    Right = "right"
end
```

String enums compile to a bidirectional lookup table:

```lua
local Direction = {Up = "up", Down = "down", Left = "left", Right = "right"}
Direction["up"] = "Up"
Direction["down"] = "Down"
-- ...
```

## Usage

```lux
local c = Color.Red
if c == Color.Green then
    print("Green!")
end
```

## Enums in Match

```lux
match status
case HttpStatus.Ok then
    print("Success")
case HttpStatus.NotFound then
    print("Not found")
case _ then
    print("Other")
end
```

## Iteration

Enums are iterable:

```lux
for name, value in pairs(Color) do
    print(name, value)
end
```

## Exported Enums

```lux
export enum Severity
    Low
    Medium
    High
    Critical
end
```
