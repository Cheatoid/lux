# zoo-app

The consumer in the `examples/` triple. Pulls in
[`lux-strings`](../lux-strings) and [`lua-math`](../lua-math) through the
package manager (file dependencies) and exercises the import / module / type
system end to end.

## What it demonstrates

| | |
|-|-|
| **Lux → Lux import** | `import { trim, padLeft, startsWith } from "lux-strings"` resolves to the linked Lux source and compiles to `require("lux-strings")`. |
| **Sub-module import** | `import { capitalize } from "lux-strings/case"` resolves to `lux_modules/lux-strings/case.lua`. |
| **Lua → Lux import via .d.lux** | `import { lerp, clamp, vec2 } from "lua-math"` finds the `declare module "lua-math"` block in `init.d.lux`; the type-checker uses it while the generated code falls through to the real `init.lua`. |
| **Cross-package types** | `Vec2` (declared in lua-math) flows through `length2` and stays type-safe in zoo-app. |

## Run it

```bash
# 1. Build the Lux library so its .lua files exist alongside the sources.
#    (lua-math already ships its init.lua, no build needed.)
cd ../lux-strings && lux build

# 2. Wire the file deps into lux_modules/.
cd ../zoo-app && lux install

# 3. Compile + execute via the embedded runtime.
lux run
```

Expected output:

```
  > Welcome, Whiskers!
loud (95 dB)
quiet (120 dB)
|origin| = 5.0
lerp(0,10,0.25) = 2.5
sum(1..5) = 15
```