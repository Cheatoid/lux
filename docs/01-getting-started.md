# Getting Started

Lux is a typed Lua transpiler. It takes Lux source files and compiles them to plain Lua (5.1 - 5.4, LuaJIT). All type annotations are compile-time only and stripped from the output.

## Build & Run

```bash
dotnet build Lux/Lux.csproj
dotnet run --project Lux/Lux.csproj
```

## Project Configuration (lux.toml)

Every Lux project uses a `lux.toml` file in its root directory:

```toml
name = "my-project"
target = "lua54"              # lua51, lua52, lua53, lua54, luajit
source = "src"
output = "out"
entry = "src/main.lux"
minify = false
generate_declarations = true  # generate .d.lux type definitions
```

### Config Inheritance & Presets

```toml
extends = ["./base.toml"]
preset = "strict"             # "strict" or "relaxed"
globals = ["lib/std.d.lux"]   # global declaration files
```

### Code Options

```toml
[code]
index_base = 0                # 0-based indexing (adjusted to Lua 1-based)
concat_operator = "+"         # use + for string concat (.. always works)
string_interpolation = true   # enable backtick interpolated strings
alt_boolean_operators = true  # enable &&, ||, !, !=
semicolons = "optional"       # optional, required, forbidden
import_statement = "require(%s)"
strip_unused = true
```

### Rules

```toml
[rules]
allow_any = true              # allow the `any` type
strict_nil = false            # require explicit nilability
immutable_default = false     # variables immutable by default
deep_freeze = false           # freeze immutable tables at runtime
exhaustive_match = "none"     # none, relaxed, explicit
```

### Minification / Mangling

```toml
[mangle]
enabled = false
mangle_locals = true
mangle_params = true
mangle_top_level = false
keep_function_names = true
```

### Build Scripts

```toml
[scripts]
pre_build = ["echo Building..."]
post_build = ["echo Done!"]
```

## Lua Target Versions

| Feature       | Lua 5.1 | Lua 5.2 | Lua 5.3 | Lua 5.4 | LuaJIT |
|---------------|---------|---------|---------|---------|--------|
| goto          | -       | yes     | yes     | yes     | yes    |
| Floor div `//`| -       | -       | yes     | yes     | -      |
| Bitwise ops   | -       | -       | yes     | yes     | bitlib |
| `<const>`     | -       | -       | -       | yes     | -      |
| `<close>`     | -       | -       | -       | yes     | -      |

When targeting older Lua versions, Lux automatically emits polyfill helpers (e.g. `math.floor(a/b)` for `//` on Lua 5.1).

## Running Lux Code

In addition to transpiling, Lux ships with an embedded Lua 5.4 interpreter
(KeraLua) so you can compile and execute Lux code in one step:

```bash
lux run                 # compile the current project and run its entry file
lux run src/main.lux    # compile and run a single file (compiled output is temporary)
lux run -- foo bar      # forward arguments to the running script (via the global `arg` table)
```

In project mode, `lux run` uses the `entry` field from `lux.toml`. If `entry`
is not set, it looks for `main.lua`, `index.lua`, or `init.lua` in the output
directory. Imports are resolved through Lua's normal `require` mechanism —
`lux run` prepends the compiled output directory to `package.path` before
executing the entry script.

In single-file mode, output is written to a temporary directory and cleaned up
after the run. Use `lux build` if you want the compiled Lua to persist.
