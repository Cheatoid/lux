# Control Flow

## If / Elseif / Else

```lux
if x > 0 then
    print("positive")
elseif x < 0 then
    print("negative")
else
    print("zero")
end
```

## While Loop

```lux
while condition do
    -- body
end
```

## Repeat-Until Loop

```lux
repeat
    -- body
until condition
```

## Numeric For

```lux
for i = 1, 10 do
    print(i)
end

for i = 0, 100, 5 do  -- with step
    print(i)
end
```

## Generic For

```lux
for k, v in pairs(tbl) do
    print(k, v)
end

for i, v in ipairs(arr) do
    print(i, v)
end
```

The compiler performs iteration type checking: when iterating over typed collections, loop variables are inferred. Enums are also iterable.

## Do Block

Creates a new scope:

```lux
do
    local temp = compute()
    -- temp is only visible here
end
```

## Break

```lux
while true do
    if done then break end
end
```

## Labels & Goto

```lux
::retry::
local ok = tryOperation()
if not ok then
    goto retry
end
```

Available on Lua 5.2+ and LuaJIT targets.
