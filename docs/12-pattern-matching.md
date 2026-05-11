# Pattern Matching

Lux provides `match` for pattern matching on values.

## Match Statement

```lux
match status
case "ok" then
    print("Success")
case "error" then
    print("Failure")
case _ then
    print("Unknown")
end
```

Compiles to an if/elseif/else chain.

## Value Patterns

Match against specific values using `==`:

```lux
match x
case 1 then
    print("one")
case 2 then
    print("two")
case 3 then
    print("three")
case _ then
    print("other")
end
```

## Wildcard Pattern

`_` matches anything (catch-all):

```lux
match value
case _ then
    print("always matches")
end
```

## Type Binding Pattern

Bind a name to the matched value with a type check:

```lux
match value
case s: string then
    print("String: " .. s)
case n: number then
    print("Number: " .. tostring(n))
case _ then
    print("Something else")
end
```

This checks `type(value) == "string"` and binds `s = value` in the arm body.

## Guard Expressions

Add an additional condition with `when`:

```lux
match x
case n: number when n > 0 then
    print("Positive: " .. n)
case n: number when n < 0 then
    print("Negative: " .. n)
case n: number then
    print("Zero")
case _ then
    print("Not a number")
end
```

## Match Expression

`match` can be used as an expression that returns a value:

```lux
local label = match code
    case 200 then "OK"
    case 404 then "Not Found"
    case 500 then "Server Error"
    case _ then "Unknown"
end
```

Match expressions also support guards and type bindings.

## Exhaustive Matching

Configure exhaustive matching in `lux.toml`:

```toml
[rules]
exhaustive_match = "explicit"  # none, relaxed, explicit
```

- `none` -- no exhaustiveness check
- `relaxed` -- warn if wildcard `_` is missing
- `explicit` -- error if not all enum variants are covered (when matching on an enum)

## Enum Matching

```lux
enum Direction
    North
    South
    East
    West
end

match dir
case Direction.North then print("Going north")
case Direction.South then print("Going south")
case Direction.East then print("Going east")
case Direction.West then print("Going west")
end
```

With `exhaustive_match = "explicit"`, omitting a variant is a compile error.
