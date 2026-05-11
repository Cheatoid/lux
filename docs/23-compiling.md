# Standalone Binaries

`lux compile` bundles a Lux project into a **single, self-contained native executable** that runs on a machine with neither Lua nor .NET installed. The bundle includes:

- All of your project's compiled `.lua` files
- All `.lua` and `.lux` files from installed dependencies (`lux_modules/`)
- The embedded Lua 5.4 interpreter (via KeraLua)
- The Lux stdlib bindings (HTTP, JSON, FS, Console, Project)
- A tiny C# launcher that wires everything together

```bash
lux compile                    # → ./<project-name>
lux compile --out dist/server  # custom path
lux compile --target linux-arm64
lux compile --aot              # experimental: smaller, faster, more brittle
```

---

## Requirements

The build host (where you run `lux compile`) needs:

- **The .NET 10 SDK** &mdash; the launcher is compiled through `dotnet publish`. Install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download).
- **Network access on first invocation** &mdash; KeraLua is pulled from NuGet. Cached afterwards; subsequent compiles are offline.

The **target machine** (where the produced binary runs) needs **nothing**. The first launch self-extracts native libraries into `%TEMP%/.net/<app>/<hash>/`; subsequent launches reuse the cache. Disk footprint of the cache: ~1-2 MB.

---

## How it works

```
[1/5] Compiling Lux source            (same pipeline as `lux build`)
[2/5] Bundle target: <name> for <rid>
[3/5] Bundling N module(s) and publishing
[4/5] Copying binary to output path
[5/5] Done — <name> (<size> MB) → <out>
```

Step 3 is where the magic happens:

1. A temp directory is created (`/tmp/lux-compile-<rand>/`).
2. Every compiled `.lua` from your project and every `.lua` / `.lux` (compiled) from `lux_modules/` is written into `temp/resources/<flattened-name>.lua`.
3. A `Launcher.csproj` is generated that:
   - Targets `net10.0` with `SelfContained=true` and `PublishSingleFile=true`.
   - References `Lux.Runtime.dll` (the embedded Lua wrapper, located via the running `lux` binary's path).
   - Marks every `.lua` file as an `<EmbeddedResource>` with a `LogicalName` matching the Lua module name (so `require("foo/bar")` works at runtime).
4. A `Program.cs` is generated that:
   - Constructs a `LuxRuntime`.
   - Calls `RegisterEmbeddedModule(name, asm, resourceName)` for every bundled module &mdash; this populates `package.preload[name]` from the embedded resource at runtime.
   - Forwards `args` to the Lua `arg` table.
   - Calls `require(<entry-module>)` to kick off execution.
5. `dotnet publish` is invoked with the correct `-r <rid>` flag.

After publish, the resulting executable is copied to `--out` (default: `./<name>` in the project root). On Unix, `chmod +x` is applied.

---

## Module discovery

Your bundle includes:

- Every `.lux` file under `<source>/**` (compiled by the same pipeline that handles `lux build`).
- Every `.lua` and `.lux` file under `lux_modules/<pkg>/` &mdash; addressed by the module name `<pkg>` (for `init.lua`/`init.lux`) or `<pkg>/<sub-path>`.

The module name used at runtime mirrors what `require(...)` would search for under Lua's normal `package.path`. So `import { x } from "lua-math"` compiles to `require("lua-math")` which finds `package.preload["lua-math"]`, which the launcher set up from the embedded resource for `lua_modules/lua-math/init.lua`.

---

## Native dependencies

`lux compile` **fails fast** if it finds a native shared library (`.so`, `.dylib`, `.dll`) inside `lux_modules/`:

```
error: standalone binary cannot bundle native modules:
  • /path/to/lux_modules/lua-protobuf/libpb.so
Remove the offending package or use 'lux run' instead.
```

The reason is correctness, not policy &mdash; bundling a `.so` would require us to know the target RID at fetch time, ship matching binaries, and arrange dlopen paths inside the self-extract cache. Future versions may relax this for vetted packages; for now, anything with C deps stays out.

If you need a native package, use `lux run` (which keeps `package.cpath` pointing at the on-disk `lux_modules/`) or vendor the C build into the launcher manually.

---

## Cross-compilation

`--target <rid>` accepts any .NET RID:

| RID            | Platform                          |
|----------------|-----------------------------------|
| `linux-x64`    | Linux on amd64                    |
| `linux-arm64`  | Linux on aarch64 (Raspberry Pi 4+, Graviton, Apple Silicon under Linux VM) |
| `osx-x64`      | Intel Mac                         |
| `osx-arm64`    | Apple Silicon Mac (M1, M2, M3, …) |
| `win-x64`      | 64-bit Windows                    |
| `win-arm64`    | ARM64 Windows (Surface Pro X, …)  |

```bash
lux compile --target linux-arm64
lux compile --target win-x64
```

The .NET runtime packs for non-host RIDs may not be installed by default. `dotnet publish` will tell you what to install (typically `dotnet workload install ...`). The single-file path works for every RID; the `--aot` path may require additional native toolchains on the host.

---

## Output sizes

| Mode          | Linux x64 | Linux arm64 | macOS arm64 | Windows x64 |
|---------------|-----------|-------------|-------------|-------------|
| SingleFile    | ~70 MB    | ~70 MB      | ~70 MB      | ~70 MB      |
| `--aot`       | ~10-20 MB | ~10-20 MB   | ~10-20 MB   | ~10-20 MB   |

SingleFile mode bundles the entire .NET runtime and all native libs into the executable. AOT compiles to native code with much smaller footprint but needs a working AOT toolchain on the build host (clang/lld on Linux, MSVC on Windows). AOT is **experimental** in Lux &mdash; reflection-heavy code paths in `Lux.Runtime` may need additional `DynamicallyAccessedMembers` annotations to survive trimming.

---

## Flags

| Flag             | Default                              | Purpose                                              |
|------------------|--------------------------------------|------------------------------------------------------|
| `--out <path>`   | `./<name>` (`.exe` on Windows)        | Output binary path                                   |
| `--name <s>`     | `config.Name` or entry basename       | Application name (AssemblyName + filename)           |
| `--target <rid>` | Current RID                          | .NET RID; see table above                            |
| `--aot`          | off                                  | Native AOT compilation (experimental)                |
| `--keep-build`   | off                                  | Don't delete the temp launcher dir after success    |

---

## Troubleshooting

**`error: dotnet SDK not found in PATH`**
Install the .NET 10 SDK and ensure `dotnet --version` runs.

**`error: cannot locate Lux.Runtime.dll next to the running lux binary`**
You're running an in-source `lux` build that hasn't been built yet (or has a corrupted `bin/`). Re-run `dotnet build Lux.sln` and retry.

**`error: standalone binary cannot bundle native modules`**
Your dependency tree includes `.so`/`.dylib`/`.dll` files. Remove the offending package or use `lux run`. See [Native dependencies](#native-dependencies).

**`error: dotnet publish failed`**
The temp launcher project failed to build. Pass `--keep-build` and inspect the temp directory printed in the error output. Common cause: missing runtime pack for a cross-RID target.

**Binary works on dev machine, crashes on target with "lua54 library not found"**
The first-launch self-extract failed. Most likely the user's `%TEMP%` is non-writable. Workaround: set `DOTNET_BUNDLE_EXTRACT_BASE_DIR` to a writable path before invoking the binary.

**Binary works but starts up slow**
SingleFile mode self-extracts to a temp cache on first run. Subsequent runs are instant (cache hits). For consistently fast startup, use `--aot`.

---

## Comparison

| Tool          | Output                            | Pros                                     | Cons                              |
|---------------|-----------------------------------|------------------------------------------|-----------------------------------|
| `lux build`   | A folder of `.lua` files          | Smallest output; debuggable              | Needs Lua on the target           |
| `lux run`     | Nothing (runs in-process)         | Fastest iteration                        | Dev-time only                     |
| `lux compile` | A single native binary             | Ships anywhere; zero deps                | 70 MB; first-launch extract       |
| `lux compile --aot` | A native binary (smaller)    | Smallest; instant startup                | Needs native toolchain on build host; AOT-trim edge cases |
