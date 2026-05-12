# CLI Reference

The `lux` binary is the only tool you need: compiler, runner, REPL, test runner, package manager, language server, docs generator, and standalone-binary bundler &mdash; all behind subcommands.

```
lux <command> [args]
```

Run `lux help` for a one-screen summary. The sections below are the complete reference.

---

## `lux init`

Scaffolds a new Lux project in the current directory. Creates:

- `lux.toml` with sensible defaults (`target = "lua54"`, `source = "src"`, `output = "out"`)
- `src/` and `out/` directories
- `.gitignore` with `out/` and `lux_modules/` pre-listed

Refuses to overwrite an existing `lux.toml`.

```bash
mkdir my-app && cd my-app
lux init
```

---

## `lux create <spec> [dir]`

Scaffolds a project from a template. The spec can be:

- A git URL: `https://github.com/owner/template.git`, `git@github.com:owner/template.git`
- A GitHub shortcut: `gh:owner/template`, `github:owner/template@v1`
- A direct URL to a `setup.lux` file that handles prompting and file generation

| Flag           | Description                                              |
|----------------|----------------------------------------------------------|
| `--skip-setup` | Clone the template but don't run its `setup.lux`         |
| `--offline`    | Use only the cached registry; don't fetch from network    |
| `--no-cache`   | Bypass the local git cache for this run                  |

```bash
lux create gh:DasDarki/lux-app-template my-game
lux create https://example.com/setup.lux
```

---

## `lux build [files...]`

Compiles the project (no args) or specific `.lux` files (with args).

**Project mode** &mdash; reads `lux.toml`, scans `<source>/**/*.lux`, writes `<output>/**/*.lua`. The configured `entry`, `target`, code section, mangle section, and rules section all apply.

**File mode** &mdash; compile the given files plus an optional `lux.toml` (auto-detected). Output lands in the project's `<output>` dir (or `out/` if no config). Useful for one-off compilation.

```bash
lux build                          # project mode
lux build src/foo.lux src/bar.lux  # file mode
lux build lux.toml src/main.lux    # explicit config
```

Pre/post-build scripts from `[scripts]` run before/after compilation.

---

## `lux run [files...] [-- args...]`

Compiles **and** executes via the embedded Lua 5.4 interpreter (KeraLua). State on the target machine is irrelevant &mdash; the binary brings its own Lua.

Same file vs project mode as `lux build`. Everything after `--` is passed to the running Lua script as the global `arg` table.

```bash
lux run                       # project mode, entry from lux.toml
lux run -- --verbose foo bar  # forward args to script

lux run src/script.lux        # one-off file
```

The `package.path` is augmented with the compiled output dir and `lux_modules/`, so cross-module requires Just Work. Exit code mirrors the Lua chunk's success.

---

## `lux test [filter]`

Discovers and runs unit / integration tests.

**Discovery**: a file is a test iff
- It lives under any directory listed in `[test].dirs` (default: `tests/`, `test/`), recursively; or
- Its filename ends with one of `[test].patterns` (default: `_test.lux`, `.test.lux`).

The optional `[filter]` argument matches against the full test name (substring; case-sensitive). Only matching tests run; the others are skipped silently.

| Flag             | Description                                  |
|------------------|----------------------------------------------|
| `--quiet`, `-q`  | Suppress per-test tick output; show summary only |

```bash
lux test                  # run every discovered test
lux test "match"          # only tests whose name contains "match"
lux test --quiet
```

Exit code is 0 on all-pass, 1 on any failure or compile error. See [Testing](21-testing.md) for the assertion API and `describe`/`test` semantics.

---

## `lux repl`

Starts an interactive Lux session.

```text
lux> 2 + 2
4
lux> function greet(n: string): string
...>     return "hi " .. n
...> end
lux> greet("world")
hi world
```

The runtime persists across inputs &mdash; globals and top-level `function`s survive. Top-level `local`s do not (Lua semantics; use `name = ...` without `local` to get a global).

REPL commands (start with `:`):

| Command          | Effect                                        |
|------------------|-----------------------------------------------|
| `:help`, `:h`    | Show inline help                              |
| `:quit`, `:q`    | Exit (also `Ctrl+D`)                         |
| `:clear`         | Clear the screen                              |
| `:reset`         | Drop all globals (fresh runtime)              |
| `:load <path>`   | Read a `.lux` file and evaluate it in-session |

Multi-line input is detected automatically &mdash; the REPL keeps prompting (`...>`) until brackets and blocks balance. See [REPL](22-repl.md) for details.

---

## `lux compile`

Bundles the project into a **single self-contained native executable**. Embeds all compiled Lua, the Lua 5.4 interpreter, and the Lux stdlib bindings. The result runs on a machine with no Lua and no .NET.

| Flag             | Default                              | Description                                              |
|------------------|--------------------------------------|----------------------------------------------------------|
| `--out <path>`   | `./<name>` (`.exe` on Windows)       | Output binary path                                       |
| `--name <s>`     | `config.Name` or entry basename       | Application name (also the AssemblyName)                 |
| `--target <rid>` | current RID                          | .NET RID, e.g. `linux-arm64`, `win-x64`, `osx-arm64`     |
| `--aot`          | off                                  | Use Native AOT (experimental — smaller binary, faster start; needs clang/lld on Linux, MSVC on Windows) |
| `--keep-build`   | off                                  | Don't delete the temporary launcher project on success    |

Bundle process (5 steps, all logged to stdout):

1. Compile the project (same pipeline as `lux build`).
2. Scan `lux_modules/` for native libraries (`.so`, `.dylib`, `.dll`) &mdash; fail if any are found (current Lux can't ship them inside the binary).
3. Generate a temporary `Launcher.csproj` that references `Lux.Runtime.dll` (the embedded Lua wrapper), with every compiled `.lua` plus every pre-built `.lua` from `lux_modules/` as embedded resources.
4. Run `dotnet publish -c Release -r <rid>` with single-file self-contained settings.
5. Copy the published binary to `--out`, chmod +x on Unix.

```bash
lux compile                    # → ./my-app (~70 MB)
lux compile --out dist/server  # custom path
lux compile --aot              # smaller binary (~15 MB), needs native toolchain
```

See [Standalone Binaries](23-compiling.md) for tuning and troubleshooting.

---

## `lux docs [args]`

Generates documentation for the project's exported APIs from inline doc comments.

| Flag             | Default | Description                                          |
|------------------|---------|------------------------------------------------------|
| `--out <dir>`    | `docs`  | Output directory for generated files                 |
| `--no-html`      | off     | Skip the HTML site, keep only Markdown               |
| `--no-md`        | off     | Skip Markdown, keep only the HTML site               |

The generator reads LuaCATS-style triple-dash comments (`--- summary`, `---@param`, `---@return`) on exported declarations and produces one Markdown file per module + an HTML site index. See [Doc Comments](20-doc-comments.md) for the comment syntax.

```bash
lux docs                       # → docs/index.html + per-module .md
lux docs --out public/api      # custom dir
lux docs --no-html             # plain Markdown only
```

---

## `lux install`

Installs declared dependencies from `lux.toml` into `lux_modules/`. Reads `lux.lock` if present, otherwise resolves fresh from git.

| Flag                                        | Description                                                |
|---------------------------------------------|------------------------------------------------------------|
| `--frozen`                                  | Require an up-to-date `lux.lock`; error on drift           |
| `--offline`                                 | Only use the local git cache; never fetch                  |
| `--no-dev`, `--production`                  | Skip `[dev-dependencies]`                                  |
| `--no-cache`                                | Bypass the registry cache for alias resolution             |
| `--allow-scripts`                           | Allow `[scripts] install` / `postinstall` to run           |
| `--allow-scripts=pkg1,pkg2`                 | Allow scripts only for the named packages                  |

```bash
lux install                              # install everything
lux install --frozen --no-dev            # CI mode
lux install --allow-scripts=lua-protobuf  # allow specific package's scripts
```

---

## `lux add <spec>`

Adds a dependency to `lux.toml` (preserving formatting + comments via TOML round-trip) and installs it.

Spec formats:

- `name@version` (alias registry): `lux-strings@1.2.0`
- `github:owner/repo[@ref]`: `github:DasDarki/lux-strings@v1.2.0`
- `gh:owner/repo[@ref]`: shorthand for the above
- `https://...`: any git URL, optional `#ref` suffix
- `file:../local-path`: link a local checkout

| Flag                                        | Description                                                |
|---------------------------------------------|------------------------------------------------------------|
| `--dev`                                     | Add to `[dev-dependencies]` instead of `[dependencies]`    |
| `--peer`                                    | Add to `[peer-dependencies]`                               |
| `--no-cache`                                | Bypass the registry cache                                  |
| `--allow-scripts[=names]`                   | Allow lifecycle scripts (all, or comma-separated names)    |

```bash
lux add github:owner/cool-lib@v1
lux add lux-strings@1.2.0 --dev
lux add file:../my-utils
```

---

## `lux remove <name>`

Removes a dependency from `lux.toml` and deletes it from `lux_modules/`. Also unlinks transitive deps that no other package references.

```bash
lux remove cool-lib
```

---

## `lux pm prune [<spec>]`

Wipe the on-disk package caches so the next `install` / `create` re-fetches from origin. Use when a package was republished under the same ref (force-pushed tag, mutable branch) and the cached snapshot is now stale.

```bash
lux pm prune                                        # wipe ALL caches
lux pm prune github:LuaLux/nanos-world-types        # wipe one repo's bare clone + snapshots
```

`lux pm prune` (no args) removes `~/.lux/cache/git`, `~/.lux/store` and `~/.lux/tmp`. With a git spec it removes only that repo's bare clone and every commit snapshot of it.

## `lux pm update [<name>]`

Re-resolves dependencies against origin and rewrites the lockfile. The bare clone is `git fetch`'d on every invocation, so any floating ref (default branch, semver range, mutable tag) picks up new commits. Pinned exact-commit specs end up at the same SHA.

```bash
lux pm update                          # re-resolve every dep
lux pm update nanos-world-types        # re-resolve just one
```

`pm update` (no args) wipes the entire lockfile and re-resolves; `pm update <name>` drops only the named entry. Either way the install step that follows re-extracts snapshots and re-links `lux_modules/`. For *cache-corruption* scenarios (mis-named ref, force-pushed branch overwriting the bare clone), reach for `lux pm prune` instead.

## `lux pm refresh-registry`

Re-downloads the alias registry index so name → git-URL lookups (`lux add cool-lib@v1`) see the latest published packages.

```bash
lux pm refresh-registry
```

`lux registry refresh` still works as a deprecated alias and prints a one-line migration notice.

---

## `lux lps`

Starts the Language Server Protocol server over stdio. Editors invoke this transparently; you should not need to call it directly.

```bash
lux lps          # blocks reading from stdin / writing to stdout
```

The server provides diagnostics, hover, go-to-definition, completion, signature help, rename, find-references, document symbols, semantic tokens, and code actions ("Implement interface", "Auto-import", "Compile current file"). It reuses the compiler's `CheckPipeline` (full pipeline minus mangle/codegen) for fast incremental feedback.

---

## `lux version`

Prints the Lux compiler version (semver, three components).

```bash
lux version       # → lux 0.X.Y
```

---

## `lux help`

Prints a one-screen summary of every command + the most common flags. Detailed flags live in this document.

---

## Exit codes

| Code | Meaning                                              |
|------|------------------------------------------------------|
| 0    | Success                                              |
| 1    | Any failure: compile error, test failure, missing file, install failure, etc. |

For `lux run` and `lux compile`'d binaries, the exit code propagates from the user's Lua script (`os.exit(N)` works as expected).
