# lux-strings

A tiny **Lux-only** library that exposes a handful of string utilities. The
implementation is fully written in Lux; the generated Lua sits under `out/`
once you run `lux build`.

## Layout

```
lux-strings/
├-- lux.toml      -- package manifest (source = ".")
├-- init.lux      -- main entry: trim / repeatN / padLeft / padRight / ...
└-- case.lux      -- sub-module: capitalize / lower / upper
```

Because the consumer's `lux_modules/lux-strings/` link points straight at this
directory, `init.lux` becomes the default entry and `case.lux` is reachable as
`lux-strings/case`.

## Build / inspect

```bash
cd examples/lux-strings
lux build
```

`out/` ends up with the transpiled Lua and an `init.d.lux` declaration mirror.