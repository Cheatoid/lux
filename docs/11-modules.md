# Modules (Import / Export)

Lux uses an ES-style module system that compiles to Lua's `require()`.

## Importing

### Named Import

```lux
import { Vector2, Rect } from "engine/math"

local v = Vector2.new(1, 2)
```

Compiles to:

```lua
local __mod = require("engine/math")
local Vector2 = __mod.Vector2
local Rect = __mod.Rect
```

### Aliased Import

```lux
import { Vector2 as Vec2, Rect as Box } from "engine/math"
```

### Default Import

```lux
import Player from "entities/player"
```

### Namespace Import

```lux
import * as utils from "lib/utils"

utils.debug()
utils.format("hello")
```

### Side-Effect Import

Runs the module without binding any names:

```lux
import "polyfill"
```

## Exporting

### Export Function

```lux
export function calculate(x: number): number
    return x * 2
end
```

### Export Local Function

```lux
export local function helper(): string
    return "help"
end
```

### Export Variable

```lux
export local PI: number = 3.14159
export local mut counter: number = 0
```

### Export Enum

```lux
export enum LogLevel
    Debug
    Info
    Warn
    Error
end
```

### Export Class

```lux
export class Connection
    host: string
    port: number

    constructor(host: string, port: number)
        self.host = host
        self.port = port
    end
end
```

### Export Interface

```lux
export interface Handler
    function handle(data: string): void
end
```

## Module Return

Exports compile to a return table at the end of the file:

```lua
-- Generated Lua
local function calculate(x)
    return x * 2
end

return {
    calculate = calculate
}
```

## Import Path Resolution

The import path is passed to the `import_statement` config option (default: `require(%s)`). Paths use forward slashes and no file extension:

```lux
import { Foo } from "lib/foo"        -- require("lib/foo")
import Bar from "src/components/bar"  -- require("src/components/bar")
```
