# Interfaces

Interfaces define type contracts. They are compile-time only and emit **no Lua code**.

> ⚠️ **Interface methods do NOT have implicit `self`.**
> Unlike [class instance methods](./09-classes.md#methods), interface methods are
> stored as-written — the type checker treats their declared parameter list as
> the full contract. This is intentional: `interface` doubles as the shape syntax
> for module-style declarations like the stdlib's `declare interface StringLib`
> + `declare string: StringLib`, where members are called as `string.upper(s)`
> with no receiver.
>
> Practical consequence for OOP-style interfaces: when a class `implements` an
> interface, calling `(value: Interface):method(args)` on an interface-typed
> value type-checks the args **as written** (no receiver injected). At Lua
> runtime the colon still passes the receiver as `self`, and the implementing
> class's method receives it — this works because class instance methods do
> have implicit self.
>
> tl;dr — for now, treat interface method declarations as plain function fields.
> The dot-vs-colon decision is on you at the call site. A future Lux version may
> add an explicit `method` keyword to disambiguate "module function" from
> "instance contract".

## Defining an Interface

```lux
interface Printable
    function toString(): string
end

interface Drawable
    function draw(): void
    function hide(): void
    visible: boolean
end
```

## Interface Inheritance

Interfaces can extend other interfaces:

```lux
interface Serializable
    function toJson(): string
end

interface Storable extends Serializable
    function save(): void
    function load(): void
end
```

## Implementing Interfaces

Classes declare which interfaces they implement:

```lux
class Document implements Printable, Serializable
    content: string

    constructor(content: string)
        self.content = content
    end

    override function toString(): string
        return self.content
    end

    override function toJson(): string
        return '{"content":"' .. self.content .. '"}'
    end
end
```

The compiler checks that all interface methods and fields are present. Missing members produce an error:

```
Error: Class 'Document' does not implement interface member 'save' from 'Storable'
```

## Multiple Interfaces

```lux
class Widget implements Drawable, Printable, Serializable
    -- must implement all methods from all three interfaces
end
```

## Exported Interfaces

```lux
export interface Plugin
    function init(): void
    function destroy(): void
    name: string
end
```
