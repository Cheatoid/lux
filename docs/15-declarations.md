# Declaration Files (.d.lux)

Declaration files provide type information for existing Lua code without any runtime output. They use the `.d.lux` extension.

## Declaring Functions

```lux
declare function print(msg: string): void
declare function type(v: any): string
declare function tostring(v: any): string
declare function tonumber(s: string): number?
```

## Declaring Variables

```lux
declare pi: number
declare version: string
declare config: { [string]: any }
```

## Declaring Modules

Group declarations for a Lua module:

```lux
declare module "math"
    function abs(x: number): number
    function floor(x: number): number
    function ceil(x: number): number
    function sqrt(x: number): number
    function sin(x: number): number
    function cos(x: number): number
    pi: number
    huge: number
    maxinteger: number
end
```

## Declaring Enums

```lux
declare enum ErrorCode
    Success: number
    NotFound: number
    Timeout: number
end
```

Declared enums have typed members but no values (type-only).

## Declaring Classes

```lux
declare class Vector2
    x: number
    y: number

    constructor(x: number, y: number)
    function magnitude(): number
    function normalize(): Vector2
    static function zero(): Vector2
    static function one(): Vector2
end

declare abstract class Shape
    abstract function area(): number
    protected function validate(): boolean
end
```

Declared classes support all modifiers: `abstract`, `static`, `protected`, `override`.

## Declaring Interfaces

```lux
declare interface Iterator
    function next(): any?
    function reset(): void
end
```

## Module Declarations with Classes

```lux
declare module "game/entities"
    class Entity
        id: number
        name: string
        constructor(name: string)
        function update(dt: number): void
    end

    interface Renderable
        function render(): void
    end

    enum EntityType
        Player: number
        Enemy: number
        NPC: number
    end
end
```

## Auto-Generation

When `generate_declarations = true` in config, Lux automatically generates `.d.lux` files from exported symbols:

```toml
generate_declarations = true
```

For a file with:

```lux
export class Player
    name: string
    constructor(name: string)
    function attack(): void
end

export function createPlayer(name: string): Player
    return new Player(name)
end
```

Lux generates:

```lux
declare module "path/to/file"
    class Player
        name: string
        constructor(name: string)
        function attack(): void
    end
    declare function createPlayer(name: string): Player
end
```

## Loading Global Declarations

Point to declaration files in config:

```toml
globals = ["lib/std.d.lux", "lib/"]
```

- Individual `.d.lux` files are loaded directly
- Directories are scanned recursively for `.d.lux` files
- All declared symbols become available globally across the project
