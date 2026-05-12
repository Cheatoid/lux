# Annotations

Annotations are compile-time metadata that can **rewrite the IR**. They are Lux's answer to decorators (TypeScript / Python), attributes (C#), and macros (Rust): a way to attach behavior to declarations without changing the call sites.

```lua
@deprecated("use Vec2 instead")
function oldVec(x: number, y: number): { x: number, y: number }
    return { x = x, y = y }
end
```

When the compiler encounters `@deprecated`, it looks up the annotation's definition, hands it the target declaration as a serialized IR tree, and runs the annotation's logic in a sandboxed Lua interpreter. The annotation can mutate the declaration, emit warnings, replace it with a new node, or wrap it &mdash; whatever its implementation does.

---

## Declaring an annotation

An annotation is a regular Lux function that uses the `@annotation` meta-annotation:

```lua
-- lib/deprecated.lux
import { Diagnostic } from "lux:ir"   -- IR helpers ship in the stdlib

@annotation(targets = [AnnotationTarget.Function, AnnotationTarget.Method])
export function deprecated(target, args)
    local message: string = args.message or "deprecated"
    target.annotations.push({
        kind = "warning",
        text = "this " .. target.kind .. " is deprecated: " .. message,
    })
    return target  -- pass through unchanged; the warning shows on every call site
end
```

The `targets` array names which declaration kinds this annotation is allowed to decorate &mdash; see `stdlib/annotation.d.lux` for the full enum (`Function`, `Method`, `Class`, `Interface`, `Field`, `Parameter`, `Variable`, ...).

---

## How an annotation runs

```
@deprecated("use Vec2 instead") function oldVec(...) ... end
        │
        ▼
[ResolveAnnotations pass]   Discover annotation source files (via [annotations] in lux.toml)
        │                    Parse them, register exported `@annotation`-marked functions
        ▼
[ApplyAnnotations pass]      For each annotated declaration:
                              1. Serialize the target IR node to a Lua table tree
                              2. Spin up a sandboxed LuxRuntime
                              3. Push the table + the annotation arguments
                              4. Call the annotation function
                              5. Deserialize the return value back into IR
                              6. Replace the original declaration in-place
```

The sandboxed runtime has access to:
- Standard Lua (`string`, `table`, `math`, `pcall`, ...)
- The `ir` helper module (loaded as a global; from `runtime/ir_helpers.lua`)

It does **not** have access to: `io`, `os`, `package`, `require`, `load`, `dofile`, `debug`. Annotations cannot read your filesystem, fetch URLs, or escape the compile.

> **Important note**: Annotations run under a specific configuration which e.g. has its index base back to normal Lua behavior (1-based) regardless of the project's configured.

---

## Registering annotation files

Tell the compiler where annotation definitions live:

```toml
# lux.toml
annotations = [
    "lib/annotations",          # directory: every .lux in here is scanned
    "lib/special.lux",          # individual file
]
```

Every exported function with an `@annotation(...)` meta-annotation becomes available project-wide.

---

## Annotation arguments

Call syntax mirrors function calls. Positional and named arguments both work:

```lua
@deprecated("use Vec2")              -- positional
@deprecated(message = "use Vec2")    -- named
@stable                               -- no args
@route(path = "/health", method = "GET")
```

In the annotation handler, `args` is a `Dictionary<string, any>` &mdash; positional values are bound by parameter order from the annotation function's declared signature, so the handler can write `args.message` regardless of call style.

---

## Targets

Annotations attach to specific kinds of declarations. The `targets` array in `@annotation` is a whitelist &mdash; using `@deprecated` on a class when the annotation only declared `targets = [Function]` is an error.

| Target enum value         | Applies to                                  |
|---------------------------|---------------------------------------------|
| `Function`                | Top-level `function` / `local function`     |
| `Method`                  | Class methods                               |
| `Class`                   | `class` declarations                        |
| `Interface`               | `interface` declarations                    |
| `Field`                   | Class instance fields                       |
| `StaticField`             | Class `static` fields                       |
| `Property`                | Getter / setter accessors                   |
| `Variable`                | `local` / `local mut` declarations          |
| `Parameter`               | Function parameters                         |
| `Module`                  | `declare module "..."` blocks                |
| `Enum`                    | `enum` declarations                         |
| `EnumMember`              | Individual enum members                     |

---

## Built-in annotations

A few annotations ship with the stdlib:

- `@deprecated(message?)` &mdash; mark a symbol as deprecated; emits a compile-time warning at every call site.
- `@inline` &mdash; hint that a function should be inlined when called (codegen heuristic; not guaranteed).
- `@pure` &mdash; declare that a function has no side effects; enables more aggressive dead-code-elimination of unused return values.
- `@experimental` &mdash; emit a one-time warning per file that references the symbol.

The full list lives in `stdlib/annotation.d.lux`.

---

## Writing a non-trivial annotation

Here's `@memoize` &mdash; wraps a function so it caches results by argument tuple:

```lua
-- lib/memoize.lux
@annotation(targets = [AnnotationTarget.Function])
export function memoize(target, _args)
    local original = target.body
    local cacheVar = ir.newName("__memo_" .. target.name)
    target.body = ir.block({
        ir.localDecl(cacheVar, ir.table({})),
        ir.functionDecl(target.name, target.params, ir.block({
            -- if cache[k] ~= nil then return cache[k] end
            -- local v = (original)(...)
            -- cache[k] = v
            -- return v
            ...
        }))
    })
    return target
end
```

The `ir` helper module exposes constructors for every node kind (statements, expressions, declarations) so annotations can build syntax trees ergonomically without bookkeeping NodeIDs.

---

## When NOT to use an annotation

Annotations are powerful and run at compile time, which makes them tempting. Don't reach for them when:

- A plain function would do (e.g. `validate(myValue)` instead of `@validate myValue`).
- The behavior depends on runtime state &mdash; annotations only see compile-time IR.
- You need it just to suppress a warning &mdash; use the relevant `[rules]` knob or `--allow-X` instead.

Annotations are best for: deprecation tracking, performance hints, registering symbols (e.g. routes, event handlers), generating boilerplate, runtime type guards.
