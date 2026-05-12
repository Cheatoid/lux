# Sides (Client / Server / Shared)

Lux can split your type universe by **execution side** — the multiplayer
sandbox pattern made famous by Garry's Mod, FiveM, and nanos-world. A symbol
declared as server-only can't be referenced from a client-side file, and vice
versa. Errors are caught at compile time before they reach the runtime.

> Sides are **opt-in per project**. Without a `[sides]` block in `lux.toml`,
> every file accepts every symbol — exactly the pre-sides default.

## Mental model

Two pieces fit together:

1. **Annotations on declarations** — each `declare class`, `declare function`,
   `class`, `interface`, etc. can carry `@side(client)`, `@side(server)`, or
   `@side(shared)`. The annotation says where the symbol is **available**.
2. **Globs in `lux.toml`** — a `[sides]` block maps folder globs to the side
   bits a file is **allowed to reach**. A file in `src/Server/**` typically
   accepts `["server", "shared"]`.

A reference type-checks if every bit in the symbol's side mask is contained in
the file's accepted mask. Unannotated symbols carry an implicit "any" side and
are reachable from any file (so existing code keeps working).

## Annotating declarations

`@side` works on top-level decls — classes, interfaces, functions, variables,
modules, enums — both regular and `declare` forms.

```lux
@side(server)
declare class Player
    Name: string
    function Kick(reason: string): nil
end

@side(client)
declare class Camera
    function SetFov(fov: number): nil
end

@side(shared)
declare class Vector
    x: number
    y: number
end

@side(server) declare function BroadcastChat(msg: string): nil
@side(client) declare function ShowToast(msg: string): nil
```

You can list multiple sides if a symbol is genuinely available on more than
one (`@side(client, server)`). Writing `@side(client, server, shared)` is the
same as not annotating at all — it means "reachable from anywhere".

## Configuring file scopes

```toml
# lux.toml
[sides]
"src/Client/**" = ["client", "shared"]
"src/Server/**" = ["server", "shared"]
"src/Shared/**" = ["shared"]
```

Each key is a glob (supports `*`, `**`, `?`) matched against the source path
relative to the project root. Each value is the list of side bits the file is
allowed to use.

- A **server** file (`src/Server/**`) accepts `server` and `shared` symbols.
- A **client** file (`src/Client/**`) accepts `client` and `shared` symbols.
- A **shared** file (`src/Shared/**`) accepts only `shared` symbols — code in
  here will run on both sides, so it must not depend on anything that's only
  available on one of them.

Files that don't match any glob default to "any" — unrestricted. Build
incrementally: start with one folder under `[sides]`, expand from there.

## What gets checked

The check runs as a dedicated `CheckSides` pass right after type-ref
resolution (and before type inference, so the better errors come first). For
each resolved name reference in the file's body — variable uses, function
calls, type annotations, `new T(...)`, `instanceof`, `extends`/`implements`
clauses — the compiler looks up the symbol's `@side` mask and validates it
against the file's accepted mask.

When a mismatch is found:

```
Semantic#ErrSymbolWrongSide:
Symbol 'BroadcastChat' is server-side only and cannot be used in
this client+shared-side file
```

The error stays a normal compile error, so your editor underlines the
offending reference and your CI fails the build.

## Notes & limits

- Sides are checked on **whole declarations** for now — you cannot put
  `@side(server)` on a single class method or interface field. If you need
  finer granularity, split the class.
- `@side` is a **builtin compiler annotation** — it does not go through the
  user-script annotation pipeline, so you cannot redefine or rewrite it from
  a `.lux` annotation file.
- Imported symbols inherit the side of the original declaration; you don't
  need to re-annotate at the import site.
- Type parameters (`<T>`) are never side-restricted — the side check skips
  symbols of kind `TypeParam`.
- The default for unannotated symbols is "any" (wildcard), so introducing
  `[sides]` to an existing project will not silently restrict anything until
  you start adding `@side` annotations.
