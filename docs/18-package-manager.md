# Package Manager

Lux ships with a built-in package manager modeled after npm / cargo, with git as the source of truth. Dependencies are declared in `lux.toml`, fetched from git, and linked into a local `lux_modules/` folder. There is no central registry server &mdash; just an optional **alias registry** (a JSON file in this repo) that maps short names to git URLs.

---

## Declaring dependencies

`lux.toml` has three dependency tables:

```toml
[dependencies]
# String spec
lux-strings = "github:DasDarki/lux-strings@v1.2.0"

# Inline table — same fields as a [packages.<name>] block
lua-math = { git = "https://example.com/lua-math.git", tag = "v0.5.0" }

# Local checkout for development
my-utils = "file:../my-utils"

[dev-dependencies]
# Only installed when --no-dev is not set
lux-bench = "github:DasDarki/lux-bench@v0.3.0"

[peer-dependencies]
# Required by libraries, not auto-installed
lux-core = ">=1.0.0"
```

---

## Specifier formats

| Format                                | Resolves to                                          |
|---------------------------------------|------------------------------------------------------|
| `name@version`                         | Registry alias → git URL + ref `vX.Y.Z`             |
| `github:owner/repo`                    | `https://github.com/owner/repo.git`                  |
| `github:owner/repo@v1`                 | `https://github.com/owner/repo.git#v1`               |
| `gh:owner/repo[@ref]`                  | Shorthand for `github:...`                           |
| `https://example.com/foo.git`          | Direct git URL, default branch                       |
| `https://example.com/foo.git#v1.0`     | Direct git URL, specific ref                         |
| `file:../local-path`                   | Symlink / copy of a local directory                  |
| `{ git = "...", tag = "v1" }`          | Inline table form, same fields as the spec strings   |
| `{ git = "...", branch = "main" }`     | Branch instead of tag                                |
| `{ git = "...", rev = "abc1234" }`     | Specific commit SHA                                  |

A ref can be a branch, a tag, or a commit SHA. Tags are preferred for reproducibility.

---

## Adding / removing

```bash
lux add github:DasDarki/lux-strings@v1.2.0
lux add lua-math@0.5.0 --dev
lux remove lua-math
```

`lux add` mutates `lux.toml` via a roundtrip TOML parser (Tomlyn `DocumentSyntax`) &mdash; **your formatting and comments are preserved**. If the resulting spec is ambiguous with an inline-table form already present, the inline table is upgraded; otherwise a string form is appended.

After mutation, the installer runs &mdash; you get the new package in `lux_modules/` immediately.

---

## The install pipeline

`lux install` (or `lux add`'s implicit install) runs five stages:

1. **Resolve** &mdash; every spec is normalized into a `(name, gitUrl, ref)` triple. Registry aliases are looked up against the alias registry (cached at `~/.lux/registry/index.json`, refresh with `lux registry refresh`).
2. **Fetch** &mdash; each unique `(gitUrl, ref)` is fetched into a bare-clone cache at `~/.lux/cache/git/<hash>/`. Subsequent fetches reuse the cache. Use `--no-cache` to force a fresh clone.
3. **Materialize** &mdash; each package is checked out into the **store** at `~/.lux/store/<name>@<resolved-ref>/`.
4. **Link** &mdash; the store directory is linked into `<project>/lux_modules/<name>/`. On Linux/macOS this is a symlink; on Windows it's a junction (or a copy on filesystems that don't support junctions).
5. **Transitive resolve** &mdash; the freshly installed package's own `lux.toml` is read; its `[dependencies]` are processed the same way. Versions are de-duplicated by `(name, resolved-ref)`.

Output:

```
$ lux install
  resolved lux-strings -> github:DasDarki/lux-strings@v1.2.0
  resolved lua-math    -> file:../lua-math@
Installed 2 package(s) into lux_modules/.
```

---

## The lockfile (`lux.lock`)

After every successful install, Lux writes `lux.lock` next to `lux.toml`. It pins every transitively-resolved `(name, git, ref, commit, integrity)` tuple. Commit it to source control &mdash; `lux install --frozen` (CI mode) refuses to drift from the lockfile.

```toml
# lux.lock v1
version = 1

[[package]]
name = "lux-strings"
git = "https://github.com/DasDarki/lux-strings.git"
ref = "v1.2.0"
commit = "abcdef1234..."
integrity = "sha256-..."

[[package]]
name = "lua-math"
git = "file:/path/to/lua-math"
```

---

## Conflict resolution + name aliases

When two packages depend on the same name at incompatible refs, the **alias registry** is consulted. If the registry has a canonical entry for the conflicting name, the dependency tree may be rewritten to use `@scope/name` form to disambiguate:

```toml
# Original
lux-fancy = "github:fork-a/lux-fancy@v2.0.0"
# But a transitive dep wants github:fork-b/lux-fancy@v1 — registry maps both to a scope:
# Lux rewrites:
"@a/lux-fancy" = "github:fork-a/lux-fancy@v2.0.0"
"@b/lux-fancy" = "github:fork-b/lux-fancy@v1.0.0"
```

If no scope mapping exists, the install fails with a clear message and a suggestion to manually use `@scope/name` syntax.

---

## How dependencies are seen by the compiler

After install, the compiler discovery (Phase 4) walks `lux_modules/`:

- Every `<pkg>/init.lux` or `<pkg>/init.lua` becomes the entry for `import { x } from "<pkg>"`.
- Every `<pkg>/<sub>.lux` (or `.lua`) becomes addressable as `import { x } from "<pkg>/<sub>"`.
- `.d.lux` files in any installed package are loaded as type-only declarations &mdash; their types become available to the type checker, but no Lua code is generated for them.
- Each package's own `[scripts]` annotations are loaded (provided you used `--allow-scripts`).

This means a pure-Lua package can ship without any Lux source &mdash; just a `.lua` entry point and an `.d.lux` next to it (the [`examples/lua-math/`](../examples/lua-math/) example shows the pattern).

---

## Lifecycle scripts

A dependency can declare scripts in its own `lux.toml`:

```toml
# In lux_modules/foo/lux.toml
[scripts]
install = ["echo 'building native helpers...'", "make native"]
postinstall = ["./scripts/copy-assets.sh"]
```

These are **disabled by default** &mdash; Lux does not run arbitrary code from dependencies unless you opt in:

```bash
lux install --allow-scripts            # allow all
lux install --allow-scripts=foo,bar    # allow only specific packages
```

This mirrors npm's `--ignore-scripts` default-flipped to safe-by-default. Trusted internal mirrors can set `[install].allow_scripts = true` in the project's own `lux.toml` to suppress the per-invocation flag.

---

## Publishing your own package

There is no central package server. To publish a Lux library, just push a git repo:

1. Have a `lux.toml` at the root with `name`, `version`, optional `[dependencies]`.
2. Either ship `.lux` sources (run through the consumer's compiler) or pre-built `.lua` + `.d.lux` (faster install, no consumer needs to recompile yours).
3. Tag releases as `vX.Y.Z`.
4. Users add it with `lux add github:you/your-lib@vX.Y.Z`.

If you want a friendly short name, open a PR against the alias registry to map `your-lib` to `github:you/your-lib`. After that, users can `lux add your-lib@1.2.0` without typing the org.

---

## Caches & file locations

| Path                                  | Purpose                                              |
|---------------------------------------|------------------------------------------------------|
| `~/.lux/cache/git/<hash>/`            | Bare-clone cache, deduplicated per git URL           |
| `~/.lux/store/<name>@<ref>/`          | Materialized checkout, shared across projects        |
| `~/.lux/registry/index.json`          | Alias registry cache (refreshed by `lux registry refresh`) |
| `<project>/lux_modules/<name>/`       | Link to the store; what the compiler reads from      |
| `<project>/lux.lock`                  | Pinned resolution; commit this                       |

Delete `~/.lux/cache/` to fully reset the fetch cache; `~/.lux/store/` to also reset materialized checkouts.
