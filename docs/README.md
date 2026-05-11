# Lux Language Documentation

Lux is a typed superset of Lua. It adds optional type annotations, an ES-style module system, classes, interfaces, generics, pattern matching, async/await, annotations, and a package manager &mdash; without changing how the compiled Lua looks. Every Lux feature lowers to plain Lua 5.1 / 5.2 / 5.3 / 5.4 / LuaJIT at compile time. Type information lives in the compiler, not in the output.

## Table of Contents

### Language reference

 1. [Getting Started](01-getting-started.md) — Project setup, `lux.toml`, target versions, `lux build` / `lux run`
 2. [Type System](02-types.md) — Primitives, nullable, union, array, map, struct, function, tuple, generic types
 3. [Variables & Constants](03-variables.md) — `local`, `local mut`, immutability rules, deep freeze
 4. [Functions](04-functions.md) — Named, local, anonymous, default params, varargs, overloads, generics
 5. [Control Flow](05-control-flow.md) — `if`/`while`/`for`/`do` blocks, `break N`, `continue`, `goto`, labels
 6. [Operators](06-operators.md) — Arithmetic, comparison, logical, bitwise, increment/decrement, nil-coalescing (`??`), optional chaining (`?.`, `?()`), alt-bool operators (`&&`, `||`, `!`)
 7. [Strings](07-strings.md) — Literal forms, escapes, interpolation, configurable concat operator
 8. [Enums](08-enums.md) — Named constants with auto-numbering or explicit string/number values
 9. [Classes](09-classes.md) — Constructors, inheritance, `abstract`, `override`, `protected`, `static`, getters/setters, operator overloading, `instanceof` / `typeof`
10. [Interfaces](10-interfaces.md) — Type contracts, interface inheritance, `implements`
11. [Modules](11-modules.md) — `import` / `export`, named / default / namespace / side-effect imports
12. [Pattern Matching](12-pattern-matching.md) — Match statements & expressions, value / type / wildcard patterns, `when` guards, exhaustiveness
13. [Async / Await](13-async-await.md) — Coroutine-based async functions, `__done` callbacks
14. [Nilability & Optionals](14-nilability.md) — Strict-nil mode, `??`, `!`, `?.`, narrowing
15. [Declaration Files](15-declarations.md) — `.d.lux` files for typing external Lua code
16. [Tables](16-tables.md) — Constructors, type annotations, configurable index base

### Toolchain & ecosystem

17. [CLI Reference](17-cli-reference.md) — Every command, every flag
18. [Package Manager](18-package-manager.md) — Dependency specs, install pipeline, registry, lifecycle scripts
19. [Annotations](19-annotations.md) — Compile-time IR rewriting (`@deprecated`, `@inline`, custom annotations)
20. [Doc Comments](20-doc-comments.md) — LuaCATS-style comments, generating doc sites via `lux docs`
21. [Testing](21-testing.md) — Writing tests with the built-in `lux:test` framework, running via `lux test`
22. [REPL](22-repl.md) — Interactive Lux sessions
23. [Standalone Binaries](23-compiling.md) — Bundling a project into a single native executable via `lux compile`
