# Reflection

Reflection lets a program inspect its own types **at runtime** &mdash; enumerate a
class's fields and methods, look a type up by name, construct an instance from
its descriptor, read the annotations attached to a declaration, and so on.

Unlike most compiled languages, Lux reflection is deliberately **runtime**, not
compile-time. Lux targets environments &mdash; game-mod sandboxes especially &mdash;
where you compile scripts but never control the Lua runtime they load into. There
is no host process to ask "what fields does this class have?". So instead of
keeping the metadata in the compiler, Lux **emits** it: the compiler writes type
descriptors into a global registry (`_G.__lux_reflect`) as part of the output Lua,
and the `reflect` library reads that registry back at runtime.

```lua
class Weapon
    name: string
    damage: number
    constructor(name: string, damage: number)
        self.name = name
        self.damage = damage
    end
end

local info = reflect.get("game::Weapon")
print(info.name)                 -- "Weapon"
print(#info.fields)              -- 2
print(info.fields[0].name)       -- "name"
print(info.fields[0].type.name)  -- "string"
```

---

## Enabling reflection

Metadata emission is controlled by the `[reflection]` section of `lux.toml`:

```toml
[reflection]
mode = "all"        # "all" (default) | "annotated" | "none"
```

| Mode | What gets a descriptor | Use for |
| --- | --- | --- |
| `all` | Every class, interface, enum, top-level function and variable | The default. Everything is reflectable. |
| `annotated` | Only declarations carrying [`@reflectable`](#opting-in-with-reflectable) | Ship metadata for your plugin API only, nothing else. |
| `none` | Nothing | Strip all reflection. Zero bytes, zero overhead. |

`all` is the default because reflection working everywhere is the least
surprising behaviour; `annotated` and `none` are opt-in size/overhead
optimizations, not the norm.

### Opting in with `@reflectable`

In `annotated` mode a declaration is only recorded if it is marked:

```lua
@reflectable
class Plugin
    -- recorded
end

class Internal
    -- NOT recorded under mode = "annotated"
end
```

### Stripping reflection

Setting `mode = "none"` removes the registry, the descriptors and the reader
library entirely. `reflect.*` queries still compile, but return `nil` / empty
tables because the registry is empty. Use this for release builds that don't need
introspection.

---

## Identifiers

Every registry entry is keyed by a stable id of the form:

```
<module>::<Name>
```

The module segment is your project's `name` (from `lux.toml`) for the root
package, and the directory leaf for each dependency &mdash; so a `Widget` class in
an imported `widget` package is `widget::Widget`, distinct from a `Widget` in your
own `game::Widget`. Ids are stable across files: a cross-module reference and the
definition itself always produce the same id, so `reflect.get(id)` resolves a type
no matter which file asked for it.

---

## The `reflect` library

`reflect` is a global; no import is required. Its full typed surface lives in
`stdlib/reflect.d.lux`.

### `reflect.Of(value)` &mdash; reflect by static type

`reflect.Of` is a **compiler intrinsic**. It resolves using the *static* type of
its argument, decided at compile time:

```lua
reflect.Of(sword)     -- sword: Weapon  -> the Weapon ClassInfo
reflect.Of(Weapon)    -- a type name    -> the Weapon ClassInfo
reflect.Of(3.14)      -- a primitive    -> { kind = "primitive", name = "number" }
reflect.Of(anyValue)  -- static type any -> runtime lookup (see typeOf)
```

Because it keys off the static type, `reflect.Of(x)` on a named type is resolved
to a constant `reflect.get("id")` at compile time &mdash; there is no runtime search.

### `reflect.typeOf(value)` / `reflect.dynamic(value)` &mdash; reflect by runtime value

Where `Of` uses the compile-time type, `typeOf` inspects the *actual* value at
runtime via its metatable stamp:

```lua
local w: any = makeSomething()
print(reflect.typeOf(w).name)   -- the real class/enum name, or the primitive kind
```

For a reflected class or enum instance it returns that entry; for anything else it
returns a `{ kind = "primitive", name = type(v) }` descriptor. `reflect.dynamic`
is an alias used as the fallback when `reflect.Of` is handed a statically-`any`
value.

### `reflect.get(id)` &mdash; look up by id

```lua
local info = reflect.get("game::Weapon")
```

Returns the registry entry for the id, or `nil` if unknown (or reflection is off).

### Collections

Each returns a typed array of the matching descriptors:

```lua
reflect.classes()     -- ClassInfo[]
reflect.interfaces()  -- InterfaceInfo[]
reflect.enums()       -- EnumInfo[]
reflect.functions()   -- FunctionInfo[]
reflect.variables()   -- VariableInfo[]
reflect.all()         -- every entry, mixed
```

### `reflect.ref` / `create` / `invoke` &mdash; the live handles

Every descriptor except an interface carries a live `ref` back to the real
runtime object. `reflect.create` and `reflect.invoke` are convenience wrappers
over it:

```lua
local info = reflect.get("game::Weapon")
local dagger = reflect.create(info, "Dagger", 7)   -- info.ref.new("Dagger", 7)
print(dagger:attack("goblin"))                     -- 7

local fn = reflect.get("game::greet")
print(reflect.invoke(fn, "world"))                 -- fn.ref("world") -> "hello world"
```

A variable's `ref` is a **getter closure** &mdash; call it to read the variable's
current value:

```lua
local v = reflect.get("game::player")
print(v.ref())   -- current value of `player`
```

### `reflect.implementorsOf(id)`

Every class whose `interfaces` list contains the given interface id:

```lua
for _, c in ipairs(reflect.implementorsOf("game::Usable")) do
    print(c.name)   -- classes that `implements Usable`
end
```

### Annotations at runtime

Annotations are compile-time, but Lux preserves them in the registry in a
simplified, runtime-readable form (`{ name, args }`):

```lua
local info = reflect.get("game::Plugin")
if reflect.hasAnnotation(info, "route") then
    local a = reflect.annotation(info, "route")
    print(a.args.path)                     -- named arg
end
for _, a in ipairs(reflect.annotationsOf(info)) do
    print(a.name)
end
```

---

## Descriptor shapes

Descriptors are plain Lua tables with a `kind` tag. The precise shapes are
declared as interfaces in `stdlib/reflect.d.lux`, so the collection queries are
fully typed:

- **`TypeDesc`** &mdash; a structural type description. `kind` selects the payload:
  `"primitive"` (`name`), `"named"` (`id`, resolve with `reflect.get`), `"array"`
  (`element`), `"map"` (`key`, `value`), `"union"` (`types`), `"variadic"`
  (`element`), `"tuple"` (`elements`), `"function"` (`params`, `returns`).
- **`ClassInfo`** &mdash; `name`, `fields`, `methods`, optional `isAbstract`, `base`
  (id), `interfaces` (ids), `annotations`, and `ref` (the class table).
- **`InterfaceInfo`** &mdash; like a class but **erased at runtime**, so no `ref`;
  `bases` holds the base-interface ids.
- **`EnumInfo`** &mdash; `members` (`{ name, value }`) plus `ref` (the enum table).
- **`FunctionInfo`** / **`VariableInfo`** &mdash; `params`/`returns` and `type`
  respectively; `ref` is the function itself, or a getter closure for a variable.
- **`FieldInfo`** / **`MethodInfo`** / **`ParamInfo`** &mdash; members of the above,
  each with a `type: TypeDesc`; methods carry `isStatic` / `isAbstract` /
  `isOverride` / `isAsync` flags where relevant.

> Reflection arrays follow Lux's [configurable index base](16-tables.md). With the
> default 0-based indexing, `info.fields[0]` is the first field.

---

## A worked example

```lua
enum Rarity
    Common
    Rare
    Legendary
end

interface Usable
    function use(): nil
end

class Weapon implements Usable
    name: string
    damage: number
    constructor(name: string, damage: number)
        self.name = name
        self.damage = damage
    end
    function attack(target: string): number
        return self.damage
    end
    function use(): nil end
end

local player: string = "hero"

function greet(who: string): string
    return "hello " .. who
end

local sword = new Weapon("Excalibur", 42)

print(reflect.Of(sword).name)                      -- Weapon
print(reflect.Of(Weapon).kind)                     -- class
print(reflect.Of(3.14).name)                       -- number
print(reflect.typeOf(sword).name)                  -- Weapon

local info = reflect.get("game::Weapon")
print(#info.fields)                                -- 2

local dagger = reflect.create(info, "Dagger", 7)
print(dagger:attack("goblin"))                     -- 7

print(reflect.invoke(reflect.get("game::greet"), "world"))  -- hello world
print(reflect.get("game::player").ref())           -- hero

local rarity = reflect.get("game::Rarity")
print(#rarity.members, rarity.members[0].name)     -- 3   Common

print(reflect.implementorsOf("game::Usable")[0].name)       -- Weapon
```

Iterating the whole program is just as direct:

```lua
for _, c in ipairs(reflect.classes()) do
    print(c.name)
    for _, f in ipairs(c.fields) do
        print("  " .. f.name .. ": " .. f.type.kind)
    end
    for _, m in ipairs(c.methods) do
        print("  " .. m.name .. "() -> " .. m.returns.kind)
    end
end
```

---

## Notes and limitations

- **`reflect.Of` is static, `typeOf` is dynamic.** `Of` is resolved at compile
  time from the declared type; use `typeOf` when you only have the value (e.g. an
  `any`) and need its real runtime type.
- **Interfaces are erased.** They have a descriptor (so you can read their shape
  and find implementors) but no `ref` &mdash; there is no runtime interface object.
- **Only top-level functions and variables** are recorded, not locals nested
  inside function bodies.
- **`mode = "none"` is safe to leave queries against.** Reflection calls still
  compile; they simply return empty results when the registry isn't emitted.
