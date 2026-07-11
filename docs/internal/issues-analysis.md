# Issue-Analyse: Typsystem & Rückgabemodell

> Interne Design-Notiz. Grundlage: empirische Reproduktion am frisch gebauten Compiler
> (`compiler/bin/Debug/net10.0/Lux`) plus statische Analyse der Passes. Stand: 2026-07-11.

## Big Picture

Vier der sieben gemeldeten Punkte — **1, 5, 6, 7** — sind im Kern *dasselbe* Thema:
das **Rückgabetyp-/Signatur-Modell ist unterbaut**. Konkret:

- `FunctionType.ReturnType` ([Type.cs:224](../../compiler/IR/Base/Type.cs#L224)) ist ein *einzelner*
  `Type`. Es gibt keine Return-**Arity** (0 / 1 / N / variadic).
- Es gibt **kein** `void`/`none` (Primitives sind nur `nil, any, number, boolean, string`).
- Es gibt **keine** „gibt der Body überhaupt einen Wert zurück"-Prüfung (kein all-paths-return).
- Methoden ohne Return-Annotation defaulten hart auf `nil` und erben **nicht** von der Basis
  (im Gegensatz zu freien Funktionen, die aus dem Body inferieren).

Empfehlung: **1/5/6/7 als einen zusammenhängenden „Return-/Signatur-Modell"-Umbau** angehen,
statt vier Einzelfixes. Umgesetzt wird zuerst Cluster **1/5/6** (Korrektheits-Bugs).

---

## 1. `void` — anerkennen, als Alias für `nil`

**Befund (verifiziert):** `function foo(): void end` → `Symbol 'void' is not declared in this scope`.
`void` fällt in [IRVisitor.TypeRefs.cs](../../compiler/IR/Visitor/IRVisitor.TypeRefs.cs) auf `NamedTypeRef`
durch (nur `string/number/boolean/any` sind gemappt), [ResolveTypeRefsPass.cs:661](../../compiler/Compiler/Passes/ResolveTypeRefsPass.cs#L661)
findet kein Symbol → `ErrUndeclaredSymbol`, Fallback `any`. Gleichzeitig ist `void` in
[docs/02-types.md:14](../02-types.md) als *Primitivtyp* dokumentiert und in zig Interface-/
Declaration-Beispielen benutzt. Reiner Doku-vs-Implementierung-Gap.

**nil vs none:** Der Reporter hat Lua-semantisch recht — `return` (0 Werte) ≠ `return nil` (1 Wert),
beobachtbar via `select("#", ...)`. Aber das Typsystem modelliert heute keine Arity. Echtes `none`
(0-Werte) korrekt einzuführen = Return-Arity modellieren = Issue 7.

**Entscheidung:** `void` als anerkanntes Keyword → resolved zu `PrimitiveNil`. Pragmatisch, deckt Docs
+ Nutzer ab, komponiert perfekt mit Issue 6 (nil-Return ist nilbar → braucht kein `return`). Die echte
none-vs-nil-Unterscheidung ist als Folgearbeit an Issue 7 gehängt, **nicht** jetzt gebaut.
**Status: umgesetzt.**

---

## 5. Abstract-/Override-Rückgabetyp — realer Bug, enger als gemeldet

**Befund (verifiziert):**
- Mit **explizitem** `override function IsOpen(): boolean` löst der Return *korrekt* zu `boolean` auf
  (konkret, über Basistyp, über 3-Ebenen-Kette). Der gemeldete „nil" reproduziert in der Kern-Inferenz
  **nicht** in diesem Fall.
- **Ohne** Return-Annotation am Override (`override function IsOpen()`) wird der Return `nil`:
  Methoden defaulten in [InferTypesPass.cs:530](../../compiler/Compiler/Passes/InferTypesPass.cs#L530)
  hart auf `PrimNil` und inferieren — anders als freie Funktionen — **nicht** aus dem Body und **erben
  nicht** von der überschriebenen Basismethode.
- `override function IsOpen(): number` über `abstract IsOpen(): boolean` kompiliert **fehlerfrei** —
  Override-Signaturkompatibilität wird nicht geprüft (bewusst nicht Teil dieses Fixes, s. Folgearbeit).
- **Zweiter, separater Fehlerpfad (wahrscheinlich das, was der Reporter im Editor/`.d.lux` sah):**
  [DeclGenPass.EmitReturnType](../../compiler/Compiler/Passes/DeclGenPass.cs#L409) liest den Return
  **nicht** aus `method.ReturnType`, sondern über das Symbol des Methodennamens — Klassen-Methodennamen
  werden in `BindDeclarePass` aber **nicht als Symbole deklariert** → `Sym == Invalid` → Return fällt
  bei der Declaration-Generierung weg.

**Entscheidung / umgesetzt:**
1. Methode ohne Return-Annotation erbt den Return von der überschriebenen Basismethode bzw. vom
   implementierten Interface (deckt den Override-Fall exakt ab). Fällt keine Vererbung an → `nil`.
2. `DeclGenPass.EmitReturnType` liest den Return direkt aus dem AST-`TypeRef` der Methode/Accessors.

Folgearbeit (nicht in diesem Cluster): Override-Signaturkompatibilität (kovarianter Return) erzwingen;
Methoden-Return aus dem Body inferieren (erfordert Reordering der Methodenauflösung).

---

## 6. Return-Enforcement fehlt komplett

**Befund (verifiziert):**
- `function foo(): string end` (leerer Body) kompiliert **fehlerfrei**. Kein all-paths-return-Check
  existiert repo-weit.
- **Methoden prüfen Rückgabewerte gar nicht** gegen den deklarierten Typ:
  `class C function foo(): string return 42 end end` kompiliert — die freie Funktion mit demselben
  Fehler erfordert. Nur `ResolveFunctionLike` (freie/lokale Funktionen) macht den `EnsureAssignable`-Loop
  ([InferTypesPass.cs:827](../../compiler/Compiler/Passes/InferTypesPass.cs#L827)); die Methodenschleife
  in `ResolveClassDecl` nicht.
- Der Inferenz-Vorwurf aus dem Report reproduziert **nicht**: `local a = foo()` inferiert `a` korrekt
  als `string`.

**Entscheidung / umgesetzt:**
1. Neue Diagnose `ErrMissingReturn`. Funktions-artige (freie/lokale Funktionen, Methoden, Getter) mit
   deklariertem, **nicht-nilbarem und nicht-`any`** Return müssen auf allen Pfaden einen Wert liefern.
2. Konservative Kontrollfluss-Analyse (`FunctionBodyAlwaysReturns`): erkennt als terminierend
   `return`, `error(...)`/`os.exit(...)`, `if/elseif/else` mit vollständig terminierenden Zweigen,
   `do`-Blöcke, `while true`/`repeat until false` ohne entkommenden `break`, sowie `match` mit
   durchweg terminierenden Armen. Im Zweifel wird „terminiert" angenommen → **Bias zu False-Negatives,
   nie False-Positives.**
3. Methoden bekommen zusätzlich den Return-Wert-Typcheck (Parität mit freien Funktionen).

`void`/`nil`/`T?`/`any`-Returns sind vom Missing-Return-Check ausgenommen (Fall-through liefert `nil`,
was zulässig/erwünscht ist).

**Umgesetzter Scope:** freie Funktionen, lokale Funktionen, Klassen-/Instanzmethoden.
**Bewusste Folgearbeit (nicht in diesem Cluster):** anonyme Funktionsausdrücke (Lambdas, eigener Pfad
`InferFunctionDef`) und Accessor-Getter erhalten den Missing-Return-Check noch nicht.

---

## 7. Variadic Returns — fehlt; Fixed-Arity ist bereits vollständig

**Befund (verifiziert):**
- Fixed-Arity funktioniert komplett: `function multi(): (string, number, boolean)` parst + type-checkt;
  `local s, n, b = multi()` destrukturiert und inferiert `s` als `string`; `() -> (number, string)` und
  `local r: (number, string)` parsen. Intern als **einzelner `TupleType`** modelliert.
- **Variadic fehlt:** `(any...)`, `...any`, `any...` parsen alle nicht. `...` (`ELLIPSIS`) existiert nur
  in Parameterposition.
- Tiefer als nur Syntax: `return ...` wird als `any` (Einzelwert) typisiert, `return table.unpack(arr)`
  als der eine deklarierte Return von `unpack`. Ein *trailing* Call/Vararg wird **nicht** in mehrere
  Rückgabewerte propagiert ([InferTypesPass.cs:2305](../../compiler/Compiler/Passes/InferTypesPass.cs#L2305)
  `ComputeReturnType`).

**Entscheidung / umgesetzt (#15):** Variadic-Return-Syntax `...T` eingeführt.
- Grammatik: `typeAtom : ... | ELLIPSIS typeSingle # VariadicType` (Parser regeneriert).
- Typ-Modell: neuer `VariadicType(elementType)` (`TypeKind.Variadic`), nutzbar als Return-Typ und als
  Tuple-Tail (`(string, ...number)`).
- Inferenz: Assignability für Variadic-Ziele (Tuple/Single/Variadic-Quelle, Zero-Values via `nil`);
  Tuple-mit-Variadic-Tail-Assignability; Call-Site-Expansion (`local a, b, c = f()` füllt alle Slots);
  Collapse zu `T` bei Einzelbindung; Ausnahme vom Missing-Return-Check (kann 0 Werte liefern).
- stdlib: `unpack`/`table.unpack` liefern jetzt `...any` → `return table.unpack(arr)` funktioniert.
- Anzeige: `...T` in DeclGen und LSP-Hover.
- End-to-End verifiziert: erzeugt sauberes natives Lua-Multi-Return; `select("#", ...)` korrekt.

**Bewusste Folgearbeit (nicht in #15):**
- `...T` ist syntaktisch überall erlaubt, aber nur in Return-Position sinnvoll; Verwendung in
  Variablen-/Parameter-Position wird lenient behandelt (collapse), nicht per Diagnose verboten.
- `return ...` / `return call()` wird über `any` akzeptiert; präzises Tracking des Vararg-Element-Typs
  (sodass `return ...` bei `...: string` nur `...string` erfüllt) fehlt noch.
- Variadic + Generics (`unpack<T>(a: T[]): ...T`) ist nicht durch `TypeTable.Substitute` verdrahtet.
- Spreizen eines Variadic-Calls als Funktions-**Argument** (`f(forward(arr))`) wird nicht expandiert —
  nur Assignment-/Return-Kontexte.
- Die echte `none`-vs-`nil`-Arity aus Issue 1 könnte hierauf aufbauen (0-Werte-Marker), ist aber offen.

---

## 2. Constructor-Optimierung — ja, als konservatives Peephole (offen)

**Befund (verifiziert):** Lowering in [CodegenPass.cs:305-616](../../compiler/Compiler/Passes/CodegenPass.cs#L305)
(`EmitClassDecl` / `EmitClassConstructorBody` / `EmitInstanceFieldDefaults`). Constructor-Body ist eine
**beliebige** Statement-Liste; `super(...)` → `local self = Base.new(args)`; Getter/Setter → Proxy-Metatable.

**Empfehlung:** `return setmetatable({ ... }, Class)` nur emittieren, wenn: keine Basisklasse (bzw. super
sauber), keine Accessors/Proxy, und Body+Field-Defaults eine reine Geradeauslinie aus
`self.<literalKey> = <expr>` ohne `self`-Reads/Control-Flow/Escape/Feld-Interdependenz sind. Sonst heutiges
Codegen. Reiner Codegen-Change, verhaltenserhaltend, moderater Gewinn (v.a. LuaJIT). Prio unter den
Korrektheits-Bugs.

---

## 3. Compile-time Reflection — groß, heute nicht erreichbar (offen)

**Befund (verifiziert):** Annotations laufen an Pipeline-Position 3, **vor** `BindDeclare`, auf untypisiertem/
ungebundenem IR. `apply(target, args)` bekommt via [IRLuaCodec](../../compiler/Compiler/Annotations/IRLuaCodec.cs)
nur die *eine* dekorierte Deklaration (`Type`/`Sym`/`ResolvedType` sind rausgestrippt). Kein Zugriff auf
Geschwister/andere Files/Programm-Registry. `TypeTable`/`SymbolArena`/`ScopeGraph` existieren, werden aber
erst nach den Annotations befüllt und nie in die Sandbox gebrückt.

**Empfehlung:** „Alle Typen/Klassen/Methoden einsammeln" braucht neue Plumbing (Reflection-Phase *nach*
`InferTypes` + Host-API in die Sandbox). Erst konkrete Use-Cases scopen (Serialisierung? DI? ORM?
Test-Discovery?), dann designen. Nicht jetzt.

---

## 4. Traits — Sorge berechtigt, bremsen (offen)

Es gibt bereits Interfaces, Klassen-Vererbung, Generics mit Bounds, Operator-Overloading, Nilability,
Immutability. Full-Traits (assoziierte Typen, Trait-Bounds, Coherence/Orphan-Rules) = genau der Bloat.
Overlap mit Interfaces (Interface mit Default-Methoden ≈ Trait).

**Gegenvorschlag — ~80 % des Nutzens, Bruchteil der Komplexität:**
- **Interface Default-Methoden** → „Verhalten teilen/wiederverwenden", kleine Erweiterung des Bestehenden.
- **Extension Methods** (Methoden retroaktiv an `string`/`number`/Fremdtypen) → Expression Problem, die
  Hauptmotivation des Reporters.

Full Traits ablehnen, bis ein Use-Case auftaucht, den diese beiden nicht abdecken.

**Entscheidung / umgesetzt (statt Full Traits):**
- **Interface Default-Methoden** (umgesetzt): Interface-Methode *mit* Body = Default; implementierende
  Klassen erben sie, `override` opt-out. `self` ist die Instanz; Defaults dürfen andere
  (abstrakte/Default-)Methoden aufrufen und Interface-Felder lesen; transitiv über `extends` vererbt.
  Codegen kopiert den Body zur Compile-Zeit auf jede nicht-überschreibende Klasse (keine Runtime-Library).
  Berührt: Grammatik (`InterfaceDefaultMethodMember`, Parser-Regen), IR (`InterfaceMethodNode.Body`),
  BindDeclare/ResolveNames/ResolveTypeRefs (Bodies + `self`), InferTypes (Body-Check, `DefaultMethods`,
  Impl-Check-Lockerung + Injektion mit self-Param, `override` auch gegen Interfaces), Codegen (`DefaultsToEmit`).
- **Extension Methods** (in Umsetzung): `extend Type ... end`; Aufruf lowert compile-time zu einem
  normalen Funktionsaufruf → funktioniert auch auf `number`/`boolean`.

---

## Priorisierung

| Prio | Issue | Status |
|---|---|---|
| 1 | **6** Return-Enforcement (+ **5** Override) | in Umsetzung (Cluster 1/5/6) |
| 2 | **1** `void` | in Umsetzung |
| 3 | **2** Constructor-Opt | offen |
| 4 | **7** Variadic Returns | umgesetzt (#15) |
| 5 | **4** Traits | offen — auf Default-Methods + Extensions eindampfen |
| 6 | **3** Reflection | offen — erst Use-Cases scopen |
