# lua-math

A **pure Lua** library that ships a `.d.lux` declaration alongside its
`init.lua` so Lux consumers can call it with full type information.

## Layout

```
lua-math/
├-- lux.toml      -- still useful for `lux install` to pick up the package name
├-- init.lua      -- the actual implementation (`lerp`, `clamp`, `sum`, …)
└-- init.d.lux    -- declare module "lua-math" with the matching Lux types
```