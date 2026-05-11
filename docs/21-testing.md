# Testing

Lux ships with a built-in test framework as the module `lux:test`, plus the `lux test` CLI command for discovery, execution, and reporting. No external testing library required.

```bash
lux test                  # run every discovered test
lux test "math"           # only tests whose full name contains "math"
lux test --quiet           # suppress per-test output
```

---

## Writing tests

A test file is any `.lux` source that calls `test(...)` (and optionally `describe(...)`) at the top level. The framework is imported from the magic module `"lux:test"`:

```lua
-- tests/math_test.lux
import { describe, test, expect } from "lux:test"

describe("Math operations", function(): nil
    test("addition", function(): nil
        expect(2 + 2):toBe(4)
    end)

    test("near floats", function(): nil
        expect(0.1 + 0.2):toBeNear(0.3, 1e-9)
    end)

    test("string contains", function(): nil
        expect("hello world"):toContain("world")
    end)
end)

test("standalone test", function(): nil
    expect({1, 2, 3}):toHaveLength(3)
end)
```

The `lux:test` module is resolved by the runtime &mdash; the consuming code doesn't need any `.lua` files on disk for it. At runtime, `lux test` injects the implementation into `package.preload["lux:test"]` before each test file is loaded.

---

## API

### Test registration

```lua
test(name: string, fn: () -> nil): nil          -- alias: it(...)
describe(name: string, fn: () -> nil): nil      -- group tests with a prefix
beforeEach(fn: () -> nil): nil                  -- run before each test in scope
afterEach(fn: () -> nil): nil                   -- run after each test in scope
skip(name: string, fn: () -> nil): nil          -- marker — fn is not invoked
```

Tests register **and run eagerly** at module load time &mdash; there is no "collect first, run later" phase. This means errors at module level surface immediately, and a `describe` group runs its tests in declaration order.

### Assertion API (`expect`)

```lua
expect(value):
    toBe(other)                       -- ==
    toEqual(other)                    -- deep equality (recurses into tables)
    toBeNear(other, eps?)             -- numeric tolerance (default eps = 1e-9)
    toBeTruthy()
    toBeFalsy()
    toBeNil()
    toBeDefined()                     -- value ~= nil
    toContain(needle)                 -- string substring or table member
    toMatch(pattern)                  -- Lua string pattern
    toHaveLength(n)                   -- #value == n
    toThrow(message?)                 -- value must be a callable that raises;
                                       -- if message given, the error string
                                       -- must contain it

    -- Negated forms:
    toNotBe(other)
    toNotEqual(other)
    toNotContain(needle)
```

A failed assertion raises an error that the runner catches per-test; subsequent assertions in the same test do not run.

---

## Discovery rules

`lux test` discovers test files using two complementary mechanisms (both configurable in `lux.toml`):

```toml
[test]
dirs = ["tests", "test"]       # walked recursively
patterns = ["_test.lux", ".test.lux"]
quiet = false
```

- Any `.lux` file inside one of `[test].dirs` is a test file.
- Any `.lux` file (anywhere under `[source]`) whose filename ends with one of `[test].patterns` is a test file.

```
src/
├-- main.lux                ← regular source
├-- math.lux                ← regular source
└-- math_test.lux           ← test (suffix match)
tests/
├-- integration_test.lux    ← test (in tests/ dir)
└-- api/v2_test.lux         ← test (recursive in tests/)
```

`.d.lux` files are always excluded.

---

## Filtering

The positional argument to `lux test` filters tests by their **full name** (substring match):

```bash
lux test "Math > addition"      # exact dot-separated path
lux test "Math"                 # any test inside a Math describe block
lux test addition               # any test whose name contains "addition"
```

`describe` groups contribute their name as a prefix; nested groups join with ` > `. So `describe("Math operations", function() test("addition", ...) end)` produces the full name `Math operations > addition`.

The filter applies after discovery &mdash; tests that don't match are silently skipped (not counted as failures or skips). Test output and summary reflect only matched tests.

---

## Output

Default output mirrors the structure of the source:

```
-- /tmp/lux-test-abc123/math_test.lua --
Math operations
  ✓ addition
  ✓ near floats
  ✓ string contains
✓ standalone test

-----------------------------------------
✓ all 4 test(s) passed
```

Failure output includes the assertion message and a stack trace:

```
Math operations
  ✓ addition
  ✗ subtraction
      expected 2 to be 3
      stack traceback:
          [C]: in function 'error'
          [string "lux:test"]:142: in method 'toBe'
          .../math_test.lua:9: in function <.../math_test.lua:7>

-----------------------------------------
✗ 1 failed, 3 passed (4 total)

  • Math operations > subtraction
    /tmp/lux-test-abc123/math_test.lua
```

`--quiet` removes the per-test ticks; the summary at the bottom is always shown.

---

## Exit codes

| Code | Meaning                                |
|------|----------------------------------------|
| 0    | All discovered tests passed            |
| 1    | One or more failures, or compile error |

So `lux test` is CI-friendly without further configuration.

---

## Hooks (`beforeEach` / `afterEach`)

Hooks defined inside a `describe` block apply only to tests in that block (and its nested blocks). Hooks defined at the file top level apply to every test in the file.

```lua
describe("Database tests", function(): nil
    local conn: any

    beforeEach(function(): nil
        conn = connect()
    end)

    afterEach(function(): nil
        conn:close()
    end)

    test("inserts a row", function(): nil
        expect(conn:insert("foo")):toBeTruthy()
    end)
end)
```

A failure in a hook is reported as the test's failure, not a separate event.

---

## Integration with the package manager

Test-only dependencies belong in `[dev-dependencies]`:

```toml
[dev-dependencies]
test-helpers = "github:DasDarki/lux-test-helpers@v0.3.0"
```

`lux install --no-dev` (used in production CI before a binary build) skips them. `lux install` (default) and `lux test` (which respects `--no-dev`'s absence) include them.

---

## Comparison with the legacy adapter

The repository's [`test/`](../test/) project (164 tests covering the language itself) uses a thin adapter (`test/src/_runner.lux`) that re-exports `section / expect / expectEq / ...` on top of `lux:test`. This was kept for source compatibility while the legacy `run()`-per-file structure was migrated. New code should use `lux:test` directly.
