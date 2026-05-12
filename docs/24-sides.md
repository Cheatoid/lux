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

`@side` works on top-level decls **and** on individual class / interface
members — methods, fields, constructors, getters/setters, operators. Both
regular and `declare` forms are supported.

```lux
declare class Player
    Name: string

    @side(server) function Kick(reason: string): nil
    @side(server) function Ban(reason: string): nil

    @side(client) function ShowToast(msg: string): nil

    @side(shared) function GetID(): number

    -- Unannotated members default to "any" — reachable from every file.
    function GetName(): string
end

@side(client)
declare class Camera
    @side(client) function SetFov(fov: number): nil
end

@side(server) declare function BroadcastChat(msg: string): nil
@side(client) declare function ShowToast(msg: string): nil
```

The `Player` example above shows the typical pattern: the class **type** is
reachable from any side (the user can hold a `Player` reference everywhere),
but individual methods carry their own side mask. A `client` file that calls
`player:Kick(...)` gets a compile error; calling `player:ShowToast(...)`
works.

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

Two compiler passes split the work:

1. **`CheckSides`** runs right after type-ref resolution. It walks every
   resolved top-level `NameRef` in the file's body — variable uses, function
   calls, type annotations, `new T(...)`, `instanceof`, `extends` /
   `implements` clauses — and validates the symbol's `@side` mask against the
   file's accepted mask.
2. **`CheckMemberSides`** runs after type inference (when receiver types are
   known) and walks every `DotAccessExpr` / `MethodCallExpr` / `NewExpr`. It
   resolves the receiver's class or interface type, locates the member, and
   checks the member's `@side`. Member side annotations inherit through the
   base class / interface chain — annotating a method on the base class
   covers all child classes that don't shadow it.

When a mismatch is found:

```
Semantic#ErrSymbolWrongSide:
Symbol 'Player.Kick' is server-side only and cannot be used in
this client+shared-side file
```

Errors stay normal compile errors, so your editor underlines the offending
reference and your CI fails the build.

## Notes & limits

- `@side` works on whole declarations **and** on individual class /
  interface members (methods, fields, constructors, getters/setters,
  operators). A class type without `@side` on the type itself but with
  `@side` on its methods is the typical pattern for shared types with
  side-scoped behaviour.
- A child class that re-declares a parent member without an explicit
  `@side` is treated as **unrestricted** — the override intentionally
  drops the parent's restriction. Annotate the override too if you want
  the same scoping.
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
