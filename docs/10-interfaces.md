# Interfaces

Interfaces define type contracts. They are compile-time only and emit **no Lua code**.

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
