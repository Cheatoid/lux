# REPL

`lux repl` is an interactive prompt that compiles each input on the fly and runs it in a persistent Lua state. Use it for scratch experiments, exploring a new library, debugging a tricky expression, or learning the language.

```text
$ lux repl
Lux REPL 0.X.Y (target Lua54). Type :help for commands, :quit to exit.
Note: top-level `local` declarations don't persist between inputs — use `name = ...` for globals.
lux> 2 + 2
4
lux> function greet(n: string): string
...>     return "hi " .. n
...> end
lux> greet("world")
hi world
```

---

## How it works

Each non-blank line (or multi-line chunk) goes through:

1. **Token-balance check** &mdash; the lexer counts `function/do/if/for/while/match/class/interface/enum/module/repeat` against `end/until`, plus `()`/`[]`/`{}`. If the totals don't balance, the REPL prompts again (`...>`) instead of evaluating.
2. **Dual compile attempt** &mdash;
    1. First, the input is wrapped as `return <input>` and run through the full compiler. If it compiles, the chunk is run as an expression and its return value is printed (unless it's `nil`).
    2. If the expression form fails to parse, the raw input is compiled as a statement list and run. Statement output (e.g. `print` calls) flows through.
3. **Eval** &mdash; the resulting Lua chunk is executed via `RunChunk` on the persistent `LuxRuntime`. Globals (and `function foo() ... end`-style declarations) persist across inputs.

So a bare `2 + 2` prints `4` (expression path), and `function greet(n) ... end` defines a Lua global that's reachable from the next input (statement path).

---

## What persists, what doesn't

Lua semantics decide this, not the REPL:

| Form                                      | Persists between inputs? |
|-------------------------------------------|--------------------------|
| `local x = 5`                              | ❌ chunk-scoped local     |
| `x = 5` (no `local`)                       | ✅ global                  |
| `function foo() … end`                     | ✅ global function         |
| `local function foo() … end`               | ❌ chunk-scoped            |
| `class Foo … end`                          | ✅ (codegen emits a global table) |
| `enum E { A, B }`                          | ✅                         |
| `interface I … end`                        | ✅ (compile-time only, no runtime artifact) |
| `import { x } from "..."`                  | ❌ creates a chunk-local; re-import per input |

If you want a value to survive, drop the `local`. The REPL's header reminds you of this.

In strict-mode projects, `x = 5` would normally fail a static check (undeclared symbol). Inside the REPL, the compiler flips to **REPL mode**: undeclared names are auto-treated as `any`-typed globals so you can iterate without fighting the type checker. The Lua runtime then resolves them naturally via globals.

---

## REPL commands

Lines that start with `:` are interpreted by the REPL, not the compiler:

| Command          | Effect                                                 |
|------------------|--------------------------------------------------------|
| `:help`, `:h`    | Show inline help with commands and tips                |
| `:quit`, `:q`    | Exit the REPL (also `Ctrl+D` / EOF on stdin)           |
| `:clear`         | Clear the screen                                       |
| `:reset`         | Drop all globals; create a fresh `LuxRuntime`          |
| `:load <path>`   | Read a `.lux` file and evaluate it in this session    |

`:load` is the quickest way to bring a bunch of definitions into the session: write them in a file, then iterate at the prompt. Example:

```text
lux> :load examples/scratch.lux
loaded
lux> Vec2(3, 4):length()
5.0
```

---

## Multi-line input

The token-counter detects unbalanced openers and keeps reading until they balance. Continuation lines use a different prompt (`...>`) so it's obvious you're still in the same chunk.

```text
lux> if x > 0 then
...>     print("positive")
...> else
...>     print("non-positive")
...> end
positive
```

To abort a multi-line input, press `Ctrl+C` (the buffer is dropped; you can also type `:reset` to clear it explicitly).

---

## Tips

- **Print intermediate values** by typing the variable name: `lux> x` runs `return x` and prints the result.
- **Define a function once, iterate at call sites**: write the function with `function foo() … end` (global), then bounce values at it.
- **Skip the runtime cost of repeated compilation** by collecting definitions in a `.lux` file and using `:load`. The REPL re-compiles each input from scratch (no incremental cache yet) &mdash; about 50-200ms per input depending on project size.
- **Don't expect inline `import { x }` to survive**: imports use `local`, which doesn't persist. Either `:load` a file that does the imports + uses them, or call `require("mod").x` directly.
- **CI smoke**: pipe a heredoc into `lux repl` for a quick "does this still work" check:
  ```bash
  printf '2 + 2\n:quit\n' | lux repl
  ```

---

## Known limitations (v1)

- No history or line editing. `Console.ReadLine` is plain line-based input &mdash; arrow keys are passed verbatim.
- No tab completion.
- Each input goes through the full compiler pipeline. Acceptable for interactive work, but you'll feel the latency on a slow machine with a large project.
- Multi-line input cannot be aborted with a single keystroke other than `Ctrl+C` (which terminates the process). A planned `:abort` command will clear just the buffer.

These rough edges are tracked in [`compiler/TODO.md`](../compiler/TODO.md) &mdash; contributions welcome.
