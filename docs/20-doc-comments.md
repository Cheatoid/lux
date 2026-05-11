# Doc Comments

Lux's documentation syntax is **LuaCATS-compatible**: triple-dash line comments (`---`) and tag-prefixed lines like `---@param`, `---@return`. The same comments serve three purposes:

1. **LSP hover** — your editor shows the comment when you hover a symbol.
2. **`.d.lux` generation** — comments propagate into the generated declaration file when `generate_declarations = true`.
3. **Doc site generation** — `lux docs` reads them to produce Markdown + an HTML site for your project's public API.

```lua
--- Returns a greeting for the given name.
---
--- @param name The person to greet
--- @return A greeting string
function greet(name: string): string
    return "Hello, " .. name .. "!"
end
```

---

## Comment forms

| Form          | Effect                                                      |
|---------------|-------------------------------------------------------------|
| `--- text`    | Documentation comment (one or more lines, joined)           |
| `---@tag ...` | Tag with arguments (`@param`, `@return`, `@see`, ...)       |
| `-- text`     | Regular comment (ignored by docs)                           |
| `--[[ ... ]]` | Block comment (ignored by docs)                             |

A documentation comment must be attached to the immediately following declaration. Blank lines between the comment and the declaration are allowed; non-blank content between them detaches the comment.

```lua
--- Attached.
function attached() end

--- Not attached — blank line and a regular comment between.

local _ = 1

--- Attached again.
function attachedAgain() end
```

---

## Supported tags

| Tag                                  | Purpose                                                       |
|--------------------------------------|---------------------------------------------------------------|
| `@param <name> <description>`        | Document a parameter                                          |
| `@param <name> <type> <description>` | Type-annotated param (overrides the inline type if present)   |
| `@return <description>`              | Document the return value                                     |
| `@return <type> <description>`       | Type-annotated return                                         |
| `@throws <description>`              | Document an error condition                                   |
| `@see <ref>`                          | Cross-reference: another function, class, or URL              |
| `@since <version>`                    | When this symbol was introduced                              |
| `@deprecated [reason]`                | Mark as deprecated (also emits a warning at call sites)      |
| `@example`                            | Followed by lines of example code (until next tag or end)    |
| `@field <name> <type> <description>`  | Document a class / interface / struct field                 |
| `@generic <T> <description>`          | Document a type parameter                                    |

Unknown tags pass through verbatim into the generated docs &mdash; you can use whatever doc convention your project already follows.

---

## Documenting classes, interfaces, enums

```lua
--- A 2D point in Cartesian coordinates.
---
--- @since 1.0
class Point
    --- The x-axis position.
    x: number

    --- The y-axis position.
    y: number

    --- Constructs a new point at the given coordinates.
    ---
    --- @param x The horizontal position
    --- @param y The vertical position
    constructor(x: number, y: number)
        self.x = x
        self.y = y
    end

    --- Returns the squared distance to another point. Cheaper than `distance`
    --- because it skips the `sqrt`.
    ---
    --- @param other The point to measure to
    --- @return The squared distance
    function distanceSq(other: Point): number
        local dx = self.x - other.x
        local dy = self.y - other.y
        return dx*dx + dy*dy
    end
end
```

Fields, methods, constructors, accessors, and the class itself all carry their own doc comments. Each shows up as a separate section in the generated docs.

---

## Module-level docs

A `---` comment at the very top of a file (before any declaration) becomes the **module summary**:

```lua
--- # Math utilities
---
--- Small, fast 2D math helpers. Designed for use in tight inner loops &mdash; no
--- allocations in any function in this module.

function dot(a: { x: number, y: number }, b: { x: number, y: number }): number
    return a.x * b.x + a.y * b.y
end
```

Markdown formatting (headings, code fences, lists) is preserved in the generated HTML.

---

## Generating documentation

```bash
lux docs                      # → docs/<module>.md + docs/index.html
lux docs --out site/api       # custom output
lux docs --no-html            # markdown only
lux docs --no-md              # html only
```

The generator:

1. Compiles the project (the same pipeline as `lux build`, but with docs collected as a side effect).
2. For every exported declaration with a doc comment, emits a section in `<module>.md`.
3. For HTML: builds an index page with a sidebar (one entry per module), plus one HTML file per module. Markdown is rendered to HTML using a built-in renderer (no external Pandoc or Marked needed).

```
docs/
├-- index.html               # site index with sidebar
├-- style.css                # ships with the generator
├-- math.md
├-- math.html
├-- strings.md
└-- strings.html
```

The generated site is a static directory you can drop into GitHub Pages, Netlify, or any static host.

---

## Doc comments survive into `.d.lux`

When `generate_declarations = true` (the default), the compiler emits a `<name>.d.lux` next to your output that other Lux projects can `import` from. **Doc comments are preserved** on every declaration in the emitted file:

```lua
-- generated: my-lib/index.d.lux
declare module "my-lib"
    --- Returns the absolute value.
    ---
    --- @param x The input
    --- @return |x|
    function abs(x: number): number
end
```

So a downstream consumer's editor shows your hover text for every API call &mdash; no separate documentation step required.
