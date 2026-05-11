# Classes & OOP

Lux provides TypeScript-like classes that compile to idiomatic Lua metatable patterns.

## Basic Class

```lux
class Animal
    name: string
    age: number = 0

    constructor(name: string, age: number)
        self.name = name
        self.age = age
    end

    function speak(): string
        return self.name .. " makes a sound"
    end

    static function create(name: string): Animal
        return new Animal(name, 0)
    end
end
```

### Instantiation

```lux
local a = new Animal("Rex", 5)
a:speak()                         -- instance method (colon syntax)
Animal.create("Buddy")            -- static method (dot syntax)
```

`new Animal(args)` compiles to `Animal.new(args)`.

### Lua Output

```lua
local Animal = {}
Animal.__index = Animal

function Animal.new(name, age)
    local self = setmetatable({}, Animal)
    self.name = name
    self.age = age
    return self
end

function Animal:speak()
    return self.name .. " makes a sound"
end

function Animal.create(name)
    return Animal.new(name, 0)
end
```

## Fields

```lux
class Config
    -- Instance field (on every instance)
    timeout: number = 30

    -- Static field (on the class table itself)
    static version: string = "1.0"

    -- Local field (file-scoped, not on the class table)
    local internalCounter: number = 0

    -- Protected field (on the class table, accessible in subclasses)
    protected debugMode: boolean = false
end
```

## Methods

```lux
class Service
    -- Instance method (uses colon syntax)
    function process(data: string): string
        return data
    end

    -- Static method (uses dot syntax)
    static function initialize(): void
        print("init")
    end

    -- Async method
    async function fetchData(url: string): string
        return await httpGet(url)
    end

    -- Local method (file-scoped helper, not on class)
    local function helper(): void
        print("internal")
    end
end
```

## Inheritance

```lux
class Dog extends Animal
    breed: string

    constructor(name: string, age: number, breed: string)
        super(name, age)         -- must call super in derived constructor
        self.breed = breed
    end

    override function speak(): string
        return "Woof!"
    end
end
```

### Lua Output (Inheritance)

```lua
local Dog = setmetatable({}, { __index = Animal })
Dog.__index = Dog

function Dog.new(name, age, breed)
    local self = Animal.new(name, age)
    setmetatable(self, Dog)
    self.breed = breed
    return self
end

function Dog:speak()
    return "Woof!"
end
```

### Super Calls

`super(args)` can only be used inside a derived class constructor. It must be the first statement.

```lux
class Cat extends Animal
    constructor(name: string)
        super(name, 0)
    end
end
```

## Override

Mark methods that override a parent method with `override`:

```lux
class Circle extends Shape
    override function area(): number
        return 3.14 * self.radius ^ 2
    end
end
```

If you define a method that exists in a parent without `override`, the compiler emits a **warning** about shadowing. If you use `override` but no matching parent method exists, it's an **error**.

## Abstract Classes

Abstract classes cannot be instantiated directly. They may contain abstract methods (signature only, no body):

```lux
abstract class Shape
    abstract function area(): number
    abstract function perimeter(): number

    function describe(): string
        return `Area: {self:area()}, Perimeter: {self:perimeter()}`
    end
end

-- ERROR: Cannot instantiate abstract class
-- local s = new Shape()

class Rectangle extends Shape
    width: number
    height: number

    constructor(w: number, h: number)
        self.width = w
        self.height = h
    end

    override function area(): number
        return self.width * self.height
    end

    override function perimeter(): number
        return 2 * (self.width + self.height)
    end
end

local r = new Rectangle(10, 5)  -- OK
```

Abstract methods compile to error stubs:

```lua
function Shape:area()
    error("Abstract method 'area' must be implemented")
end
```

Non-abstract subclasses **must** implement all abstract methods, or the compiler reports an error.

## Protected Members

Protected fields and methods are accessible within the class and its subclasses:

```lux
class Base
    protected secret: string = "hidden"

    protected function validate(): boolean
        return true
    end
end

class Child extends Base
    function check(): boolean
        return self:validate()     -- OK: accessing protected from subclass
    end
end
```

Protected members are compiled as regular members on the class table (no runtime enforcement, compile-time checks only).

## Getters & Setters

```lux
class Person
    _age: number = 0

    get age(): number
        return self._age
    end

    set age(value: number)
        if value >= 0 then
            self._age = value
        end
    end
end

local p = new Person()
p.age = 25           -- calls setter
print(p.age)         -- calls getter
```

### Lua Output (Accessors)

When a class has getters/setters, Lux uses a proxy metatable:

```lua
local function __lux_class_proxy(cls, parent)
    return {
        __index = function(t, k)
            local g = cls["__get_" .. k]
            if g then return g(t) end
            local v = cls[k]
            if v ~= nil then return v end
            if parent then
                g = parent["__get_" .. k]
                if g then return g(t) end
                return parent[k]
            end
        end,
        __newindex = function(t, k, v)
            local s = cls["__set_" .. k]
            if s then s(t, v) return end
            if cls["__get_" .. k] then
                error("Cannot set readonly property '" .. k .. "'")
            end
            rawset(t, k, v)
        end
    }
end
```

Read-only properties (getter without setter) throw an error on write.

## Implementing Interfaces

```lux
class Sprite implements Drawable, Serializable
    override function draw(): void
        print("drawing")
    end

    override function serialize(): string
        return "{}"
    end
end
```

See [Interfaces](10-interfaces.md).

## Operator Overloading

Classes can define how built-in operators behave on their instances using the
`operator` keyword. Each overload compiles to a Lua metamethod on the class
table, so operator dispatch happens through Lua's normal metatable mechanism.

```lux
class Vector2
    x: number
    y: number

    constructor(x: number, y: number)
        self.x = x
        self.y = y
    end

    operator +(other: Vector2): Vector2
        return new Vector2(self.x + other.x, self.y + other.y)
    end

    operator -(other: Vector2): Vector2
        return new Vector2(self.x - other.x, self.y - other.y)
    end

    operator -(): Vector2
        return new Vector2(-self.x, -self.y)
    end

    operator ==(other: Vector2): boolean
        return self.x == other.x and self.y == other.y
    end
end

local a = new Vector2(1, 2)
local b = new Vector2(3, 4)
local sum = a + b      -- calls Vector2:__add
local neg = -a         -- calls Vector2:__unm
local eq  = a == b     -- calls Vector2:__eq
```

### Supported Operators

| Operator | Arity  | Metamethod  |
|----------|--------|-------------|
| `+`      | binary | `__add`     |
| `-`      | binary | `__sub`     |
| `-`      | unary  | `__unm`     |
| `*`      | binary | `__mul`     |
| `/`      | binary | `__div`     |
| `//`     | binary | `__idiv`    |
| `%`      | binary | `__mod`     |
| `^`      | binary | `__pow`     |
| `..`     | binary | `__concat`  |
| `==`     | binary | `__eq`      |
| `<`      | binary | `__lt`      |
| `<=`     | binary | `__le`      |
| `#`      | unary  | `__len`     |

Unary vs. binary `-` is disambiguated by parameter count: zero parameters means
unary negation (`__unm`); one parameter means subtraction (`__sub`).

Inside an operator body, `self` refers to the left-hand operand (or the sole
operand for unary overloads). Binary overloads receive the right-hand operand
as the single parameter.

### Inheritance

Operator overloads are inherited: a subclass automatically picks up its base
class's operators unless it defines its own. Re-declaring an operator in a
subclass overrides the inherited one.

## Exported Classes

```lux
export class User
    name: string
    email: string

    constructor(name: string, email: string)
        self.name = name
        self.email = email
    end
end
```
