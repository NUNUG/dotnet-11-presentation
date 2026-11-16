# .NET 11 and C# 15 Explainer

Nov 12, 2026 · @Nate

## How to use this sheet

Every feature gets the same five parts: the one-sentence version, the problem it solves, a before-and-after in code, how to say it out loud, and the questions people are likely to ask. Read the table first. If you can say each row's pitch in your own words, you can carry the talk.

| Feature | Say it in one sentence | Status | Demo |
| --- | --- | --- | --- |
| Union types | A value that is exactly one of a list of types, and the compiler makes you handle every one. | C# 15 | 01 |
| Closed hierarchies | A base class only your own project can subclass, so the compiler knows the complete list of subclasses. | C# 15 | 02 |
| Collection expression arguments | You can pass constructor arguments, like a comparer, inside the `[...]` syntax. | C# 15 | 03 |
| Extension indexers | You can add square-bracket access, `x[...]`, to a type you do not own. | C# 15 | 04 |
| Labeled break and continue | You can name which loop a `break` or `continue` applies to. | C# 15 | 05 |
| Static interface members on older runtimes | Simple static helpers on interfaces now compile for old targets like .NET Framework. | C# 15 | slide |
| Memory safety | `unsafe` moves from "pointers exist here" to "memory is read or written here." | Preview | 06 |
| Runtime Async | The runtime, not the compiler, now runs `async` methods: cleaner stacks, fewer allocations. | Preview | 08 |
| Library highlights | Small built-in APIs that remove boilerplate: Process, FullJoin, JSON, Zstandard. | .NET 11 | 07 |
| Behavior changes | Code that compiles the same but gives different answers. | .NET 11 | 10 |

"Preview" means it ships in .NET 11 but needs an opt-in flag and may change. Everything else is supported for production.

## 1. Union types (demo 01)

**In one sentence:** a union is a value that is exactly one of a fixed list of types, and the compiler checks that you handled every possibility.

**The problem it solves.** Methods often have several possible outcomes that are not related to each other. Opening a door can give you a room, a locked door, or a trap. Until now you had three bad options. You could return `object`, and nothing would stop a caller from forgetting a case. You could make all three classes inherit from a shared base class, which forces a relationship that does not really exist. Or you could throw an exception for the "unusual" cases, which the caller discovers only at run time.

**The idea.** Declare the list once:

```csharp
public record Room(string Name);
public record Locked(string KeyName);
public record Trap(int Damage);

public union DoorResult(Room, Locked, Trap);
```

Three things now happen automatically:

1. **Implicit conversion.** You can return a `Room` where a `DoorResult` is expected. No wrapping.
2. **Look-through matching.** A `switch` on a `DoorResult` tests what is inside it. `Room r` matches when the door result holds a room.
3. **Exhaustiveness.** If your `switch` forgets `Trap`, you get warning CS8509 naming the missing case. Add a fourth type to the union later, and every switch that does not handle it lights up.

**Before and after.**

```csharp
// Before: object, and hope
object result = OpenDoor("iron");
var message = result switch
{
    Room r => $"entered the {r.Name}",
    Locked l => $"needs the {l.KeyName} key",
    _ => "???",              // forced to add this, and it hides a forgotten Trap
};

// After: the type tells you every outcome, and the compiler checks you
DoorResult result = OpenDoor("iron");
var message = result switch
{
    Room r => $"entered the {r.Name}",
    Locked l => $"needs the {l.KeyName} key",
    Trap t => $"took {t.Damage} damage",
};
```

**What it really is.** The compiler turns a `union` into a small struct with one property, `Value`, that holds the actual object. That explains the three sharp edges in demo 01:

- `default(DoorResult)` is empty: its `Value` is `null`. An array of 10 unions starts as 10 empty unions.
- A value type like `int` gets boxed, because `Value` is an object reference.
- A `null` pattern checks whether the contents are null, not the union itself.

**How to say it.** "A union is a closed list of types. Return any of them, and the compiler forces every caller to handle every one. It is the result type people have built with libraries like OneOf for years, now in the language."

**Likely questions.**

- *Is this like a TypeScript union?* Close. You list the types with a name, and switching on it is checked by the compiler.
- *Is this like an F# discriminated union?* Similar purpose. The difference is that C# unions combine types that already exist, instead of declaring new cases inline.
- *Can the case types be anything?* Yes: records, classes, structs, `int`, `string`, even other unions.
- *Does it work with JSON?* Yes. See section 9.

## 2. Closed hierarchies (demo 02)

**In one sentence:** a `closed` base class can only be subclassed inside your own project, so the compiler knows the complete list of subclasses and can check a `switch` for you.

**The problem it solves.** You write `abstract class Monster` with three subclasses. You switch on a `Monster` and handle all three. The compiler still warns that the switch is incomplete, because anyone, anywhere, could add a fourth subclass later. So you add `_ => throw ...` to quiet it. That catch-all arm then also hides the warning you actually wanted on the day a teammate adds a `Slime`.

**The idea.** Add one word:

```csharp
public closed record Monster(string Name, int HitPoints);
public record Goblin(int PackSize) : Monster("Goblin", PackSize * 7);
public record Skeleton(bool HasShield) : Monster("Skeleton", 30);
public record Dragon(string Title) : Monster(Title, 250);
```

Now a `switch` that handles Goblin, Skeleton and Dragon needs no catch-all. Add `Slime` and every incomplete switch warns.

**`closed` vs `internal` vs `sealed`.** These sound alike but answer different questions.

| Keyword | The question it answers | Effect |
| --- | --- | --- |
| `internal` | Who can see and use this type? | Only code in the same project (assembly) |
| `sealed` | Can anyone subclass this? | No one, not even your own project |
| `closed` | Who can subclass it, and is the list final? | Only your own project, and the compiler treats that list as complete |

The useful combination is `public closed`. Other projects can use a `Monster`, accept one as a parameter and switch on it. They cannot invent a new kind of monster.

Two details to know:

- **Closed means abstract.** You cannot create a plain `Monster`, only its subclasses.
- **Closed is not inherited.** If `Dragon` is a normal class, anyone can still subclass `Dragon`. That is safe for your switch, because an `ElderDragon` is still a `Dragon`. To check a deeper level, mark the middle class `closed` too.

**Union or closed: which do I use?** Both give you a checked `switch`.

| Use a union when | Use a closed class when |
| --- | --- |
| The types have nothing in common | The types share data or behavior |
| You do not own the types (`int`, `string`, a library type) | You own the base class |
| You want "one of these" for a single method's result | You are modeling a family of related things |

**How to say it.** "`sealed` says nobody can extend this. `closed` says only I can, and the compiler takes my word that the list is complete. That is what turns a switch into a checked switch."

**Likely questions.**

- *Couldn't I do this before with an internal constructor?* That blocked outside subclasses, but the compiler did not know it, so it still demanded a catch-all arm.
- *Does JSON know about it?* Yes. System.Text.Json can now discover all subclasses itself (section 9).

## 3. Collection expression arguments (demo 03)

**In one sentence:** inside the square-bracket collection syntax you can now write `with(...)` first, and those arguments go to the collection's constructor.

**Background you need.** C# 12 added *collection expressions*, the square-bracket syntax: `List<string> names = ["Ann", "Bob"];`. The `..` inside means "spread": copy in every item from another collection, as in `[.. otherList]`. The compiler works out which collection to build from the variable's type.

**The problem it solves.** The brackets had no place for constructor arguments. The moment you needed a case-insensitive comparer, or a starting capacity, you had to drop the nice syntax and go back to `new`.

**Before and after.**

```csharp
// Before: the comparer forces you back to the constructor
var pack = new HashSet<string>(looted, StringComparer.OrdinalIgnoreCase);

// After: with(...) comes first, the items follow
HashSet<string> pack = [with(StringComparer.OrdinalIgnoreCase), .. looted];
```

Both lines do exactly the same thing. `with(StringComparer.OrdinalIgnoreCase)` is passed to the `HashSet<string>` constructor, then the items are added.

Other uses from demo 03:

- **Capacity:** `[with(capacity: 64), .. query]` presizes a list when the compiler cannot count the items in advance.
- **Any constructor argument:** a `SortedSet<int>` with a descending comparer, so the largest number comes first.

**One limitation to mention.** There is still no dictionary version. `Dictionary<string, int> d = [with(comparer), new("a", 1)];` does not compile (error CS9215).

**How to say it.** "Collection expressions were great until you needed a comparer. Now the comparer goes in the brackets too."

**Likely questions.**

- *Is this the same `with` as records?* No. Records use `with` to copy an object with changes (`hero with { Column = 2 }`). Here `with(...)` is only allowed as the first element in brackets.
- *Is it faster?* No. It compiles to the same constructor call. It is about readable code.

## 4. Extension indexers (demo 04)

**In one sentence:** you can add square-bracket access, `thing[...]`, to a type you did not write, the same way extension methods add `.Method()`.

**Background you need.** An *indexer* is what makes `list[3]` or `dictionary["key"]` work. Normally only the type's author can add one. *Extension methods* have long let you add methods to types you do not own. C# 14 replaced the old `this` parameter style with `extension` blocks, which can also add properties and operators. Indexers were the one member kind still missing.

**Before and after.** The map in demo 04 is a `List<string>`, one string per row. To read the tile at row 1, column 7:

```csharp
// Before: a helper method, or nested indexing every time
char tile = map[1][7];
char tile = MapHelpers.TileAt(map, 1, 7);

// After: it reads like a grid
char tile = map[1, 7];
```

The declaration:

```csharp
public static class MapExtensions
{
    extension(IReadOnlyList<string> map)    // "map" is the object being extended
    {
        public char this[int row, int column] => map[row][column];
    }
}
```

Read the `extension(...)` line as: "for any `IReadOnlyList<string>`, which I will call `map`, add these members." Inside it, the indexer is written exactly as it would be inside a class.

**Two things worth showing.**

- **The index can be any type.** Demo 04 also has `map[hero]`, where `hero` is a `Point`. And a dictionary indexer with a fallback: `inventory["potion", orElse: 0]`.
- **Arrays never use them.** `string[]` has built-in indexing, so the compiler never looks for extensions on it, and you get error CS0022. The workaround is to declare the extension on an interface the array implements, such as `IReadOnlyList<string>`, and use that interface.

**How to say it.** "Extension methods let you add verbs to a type you do not own. Extension indexers let you add square brackets."

**Likely questions.**

- *What if the type already has an indexer with the same arguments?* The type's own indexer wins. Extensions are only considered when nothing built in fits.
- *Can they set values?* Yes, an extension indexer can have a `set` as well as a `get`.

## 5. Labeled break and continue (demo 05)

**In one sentence:** you can give a loop a name and then say `break thatName;` or `continue thatName;` from inside a nested loop.

**The problem it solves.** A plain `break` always leaves the innermost loop. When you search a grid with two nested loops and find what you want, `break` only leaves the inner loop and the outer one keeps going. The usual fixes were a `found` flag checked at every level, or a `goto`. There is an even sneakier case: inside a `switch`, `break` only ends the switch, not the loop around it.

**Before and after.**

```csharp
// Before: a flag, checked in the outer loop's condition
var found = false;
for (var row = 0; row < map.Length && !found; row++)
    for (var col = 0; col < map[row].Length; col++)
        if (map[row][col] == '$') { found = true; break; }

// After: name the outer loop, break it directly
search: for (var row = 0; row < map.Length; row++)
    for (var col = 0; col < map[row].Length; col++)
        if (map[row][col] == '$') break search;
```

`search:` is the label. It must sit directly on the loop (or switch) it names. `continue rows;` works the same way: it skips to the next pass of the named loop.

**How to say it.** "Java and JavaScript have had this for decades. C# finally lets you say which loop you mean, so the flag variables and the `goto` can go."

**Likely questions.**

- *Does it change existing code?* No. Without a label, `break` and `continue` behave exactly as before.
- *Is this just `goto` in disguise?* It is safer. A label on a loop can only be used to leave or restart that loop, and the IDE (rule IDE0410) offers to convert old flag and goto patterns for you.

## 6. Static interface members on older runtimes (no demo)

**In one sentence:** a simple static helper method on an interface now compiles even when you target an old runtime like .NET Framework or netstandard2.0.

**Background you need.** Since C# 8, interfaces can contain code, such as default method bodies and static members. That needed support from the .NET runtime itself, so it never worked when targeting .NET Framework. Library authors who build for several targets at once could not use any of it.

**What changed.** A plain static method, one that is not `virtual` or `abstract`, does not actually need anything special from the runtime. C# 15 now allows it on any target:

```csharp
public interface IMeasurement
{
    public static double ToMeters(double centimeters) => centimeters / 100;
}

var meters = IMeasurement.ToMeters(250);
```

Default method bodies, `static abstract` and `static virtual` members still need the newer runtime.

**How to say it.** "If you ship a library that still targets .NET Framework, you can now put helper methods on your interfaces."

**Why there is no demo.** On .NET 11 this already worked, so there is nothing new to see. It only shows up when you target an old framework.

## 7. Memory safety (demo 06, preview)

**In one sentence:** `unsafe` is being redefined from "pointers appear here" to "memory is actually read or written here," so the dangerous lines are easy to find.

**Background you need.** A *pointer* is a raw memory address. C# allows pointers only inside code marked `unsafe`, because writing through a bad pointer can corrupt memory. Today, even *declaring* a pointer requires `unsafe`, though declaring one cannot hurt anything. The result is large unsafe blocks where only one or two lines are truly risky.

**What changes, in three steps.**

1. **Harmless pointer operations no longer need `unsafe`.** Declaring a pointer, taking an address with `&`, pinning with `fixed`, `stackalloc` and `sizeof` are all allowed in normal code. *Using* the pointer to read or write memory (`*p`, `p[i]`, `p->field`) still needs `unsafe`.
2. **`unsafe(...)` for one expression.** `var x = unsafe(*pointer) + 1;` marks just the risky part. It also works where a block cannot go, such as a field initializer.
3. **`unsafe` on a method becomes a warning label for callers.** If `Poke` is marked `unsafe`, every caller must also be in an unsafe context (error CS9362 otherwise). The method's own body is no longer automatically unsafe; its risky line needs marking too (error CS9360).

```csharp
public static unsafe void Poke(Span<byte> buffer, int index, byte value)
{
    fixed (byte* start = buffer)        // fine without unsafe now
    {
        unsafe { start[index] = value; } // the actual write: marked
    }
}

unsafe { Dungeon.Poke(buffer, 1, 0xFF); } // the caller acknowledges the risk
```

**How to say it.** "Today `unsafe` marks where pointers live. The new rules mark where memory is touched, and make every caller of a risky method sign for it. A security review becomes a search for one keyword."

**Status.** Preview only. It needs `LangVersion=preview` plus a feature flag, and Microsoft describes it as the first step of a plan spanning several releases. Most application developers never write pointer code, so pitch it as "where the language is heading."

## 8. Runtime Async (demo 08, preview)

**In one sentence:** `async` methods are now run by the .NET runtime itself instead of being rewritten by the compiler, which gives cleaner call stacks and fewer memory allocations.

**Background you need.** Since C# 5 (2012), the compiler has secretly rewritten every `async` method into a hidden class called a *state machine*. It remembers where the method was when it hit `await`, so it can resume there later. It works, but it adds hidden methods and objects that show up in debuggers and profilers, and it allocates memory.

**What changes.** With Runtime Async the runtime knows how to pause and resume a method directly. No hidden class is generated. The .NET 11 libraries themselves are already built this way. Your own code opts in with one project setting:

```xml
<Features>runtime-async=on</Features>
```

**Running demo 08.** Run the same file twice. The first command uses the old compiler state machines; the second turns Runtime Async on, so you can compare the output side by side:

```
dotnet run 08_RuntimeAsync.cs
dotnet run 08_RuntimeAsync.cs -p:Features=runtime-async=on
```

The demo has three parts, all covered below: the live call stacks, the memory cost of `async` without `await`, and where exceptions appear.

**What you can see (demo 08).**

| Measurement | Compiler state machine | Runtime Async |
| --- | --- | --- |
| Stack frames for 3 nested async calls | 13 | 5 |
| Memory per call: `async` method with no `await`, awaited by another async method | 104 bytes | 0 bytes |
| Memory per call: same method, read with `.Result` | 104 bytes | 72 bytes |

The stack-frame row is about *live* stacks: what you see in the debugger's Call Stack window or a profiler. Exception stack traces were already cleaned up, so they look the same either way.

**Your question: is `async` without `await` free now?** Mostly, and the warning is gone. Compiler warning CS1998 ("this async method lacks await") was removed in the .NET 10 SDK, so nothing flags it anymore. The cost dropped too, as the table shows. One real difference remains: **where exceptions appear**.

```csharp
static async Task<int> A() => throw new Exception();  // async
static Task<int> B() => throw new Exception();        // not async

var t = A();   // no exception here; it is stored inside the Task
var u = B();   // throws right here, at the call
```

That matters if a caller starts several tasks and awaits them later. Choose `async` or not on purpose, for that reason, not for speed.

**How to say it.** "For 14 years the compiler faked `async` with generated classes. Now the runtime does it for real. You get readable call stacks and fewer allocations, and the framework already runs this way."

**Likely questions.**

- *Is everything faster?* Microsoft reports lower overhead, and the allocation numbers above are real, but it is a preview for your code. Measure your own app before claiming a number.
- *Do I need to change my code?* No. Same source; only the project setting differs.

## 9. Library highlights (demo 07)

**In one sentence:** a handful of built-in APIs that replace code everyone has been writing by hand.

**Run a program and capture its output.** Before, you set up a `ProcessStartInfo`, redirected output, read it, waited, and hoped stderr did not fill up and deadlock. Now it is one line:

```csharp
var result = await Process.RunAndCaptureTextAsync("git", ["status"]);
Console.WriteLine(result.StandardOutput);
```

**FullJoin.** A LINQ version of SQL's FULL OUTER JOIN: keep every row from both lists, matched where possible. Perfect for reconciling two lists, such as a ledger against a shelf count. It returns pairs, and the side with no match is empty (`null` or `0`). .NET 10 added `LeftJoin` and `RightJoin`; this completes the set.

**JSON and unions.** System.Text.Json writes whichever case a union currently holds, with no wrapper:

| Union value | JSON |
| --- | --- |
| `Reward` holding `250` | `250` |
| `Reward` holding `"Vorpal Blade"` | `"Vorpal Blade"` |
| `ProductResult` holding a `Product` | `{"Id":42,"Name":"Keyboard"}` |

Three rules worth knowing:

1. **Each case uses its own JSON settings.** `[JsonIgnore]` on `Product.CostPrice` keeps it out of the output, and also stops a client from setting it when sending JSON in.
2. **Reading back, the JSON shape picks the case.** A number can only be the `int` case. When two cases are both JSON objects, the serializer cannot tell them apart. You get build warning SYSLIB1227, then an error at run time.
3. **The fix is a classifier.** `[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]` picks the case by property names: JSON with `MissingId` becomes a `NotFound`.

**JSON and closed classes.** For a closed hierarchy, add `[JsonPolymorphic(InferClosedTypePolymorphism = true)]` and the serializer finds every subclass itself. It writes a `"$type"` field so the right type comes back. Before, you listed every subclass by hand in attributes, and the list went stale.

So: a union writes clean JSON with no type tag, which suits API responses. A closed hierarchy writes a type tag, which suits data you save and reload.

**JSON Lines.** `topLevelValues: true` writes one JSON object per line instead of one big array. Readers can process each line as it arrives. Common for logs and data exports.

**Zstandard compression.** A modern compression format, now built in next to GZip and Brotli. In the demo it was about 4 times faster than GZip with somewhat larger output, which is why it is popular for logs and caches.

**Stream adapters.** Many APIs want a `Stream`. Before, turning a string into one meant copying it into a byte array first. `new StringStream(text, Encoding.UTF8)` skips that copy. `ReadOnlySequenceStream` does the same for data that arrives in several pieces, as it does in high-performance network code.

**Small conveniences.**

- `EqualityComparer<T>.Create(x => x.Name)`: "equal if the names match," in one line instead of a whole class.
- `Random.Shared.NextInteger<byte>(1, 21)`: random numbers of any integer type without casting.
- String methods that accept a `Rune`, so you can search for emoji and other characters that do not fit in a single `char`.

**Why the demo uses `LedgerJson`.** File-based apps default to settings that disable reflection-based JSON, so the demo uses the JSON *source generator*: a small class that tells the compiler which types to prepare code for. In a normal project you can call `JsonSerializer.Serialize(value)` as usual.

## 10. Behavior changes (demo 10)

**In one sentence:** these compile exactly as before but give different results, so they are the ones that bite during an upgrade.

**Decimal conversions.** A `double` stores numbers in binary, and 1.23 has no exact binary form. The double really holds 1.229999999999999982236431606. The old cast to `decimal` rounded that to about 15 digits, which happened to give a clean `1.23`. The new cast keeps the true stored value.

| Code | .NET 10 | .NET 11 |
| --- | --- | --- |
| `(decimal)1.23` from a double | 1.23 | 1.2299999999999999822364316060 |
| `(decimal)1.23 == 1.23m` | True | False |

Neither answer is a bug: .NET 11 is more accurate, but it exposes code that does money math in `double`. The fixes, best first: keep money in `decimal` from the start (`1.23m`); round to the precision you mean, `Math.Round((decimal)d, 2)`; or parse the double's printed form, `decimal.Parse(d.ToString())`.

**Saturating casts.** This is about converting a `double` to a small integer type when the number is too big to fit. Before, the number wrapped around like a car odometer: `(byte)300.0` gave 44, because 300 minus 256 is 44. Now it stops at the limit, called *saturating*: `(byte)300.0` gives 255, the largest byte. Negative numbers become 0, and NaN becomes 0. This mostly matters in image, audio and sensor code, and usually clamping is what people wanted anyway.

**Empty GZip output.** Compressing nothing used to produce 0 bytes, which is not a valid GZip file. Now it produces a proper empty file of 20 bytes. Code that checked `length == 0` to mean "nothing to send" now behaves differently.

**CPU baseline.** .NET 11 refuses to start on x64 processors older than roughly 2013 that lack the SSE4.2 instruction set. The error is "The current CPU is missing one or more of the baseline instruction sets." Check old kiosks, industrial PCs, and virtual machines configured to hide CPU features.

**How to say it.** "Most upgrades break at compile time, which is easy. These four break at run time: money math in double, wraparound casts, empty compressed streams, and very old CPUs."

## Glossary

| Term | Plain meaning |
| --- | --- |
| Assembly | A compiled project: one .dll or .exe. `internal` and `closed` both stop at its boundary. |
| Boxing | Wrapping a value type like `int` in an object so it can be stored as `object`. Costs a small allocation. |
| Case type | One of the types listed in a union, such as `Room` in `union DoorResult(Room, Locked, Trap)`. |
| Collection expression | The `[a, b, .. others]` syntax for building lists, arrays and sets. |
| Exhaustive switch | A `switch` that handles every possible value, so it needs no `_ =>` catch-all arm. |
| Extension block | C# 14 syntax, `extension(Type name) { ... }`, for adding members to a type you do not own. |
| File-based app | A single `.cs` file run with `dotnet run file.cs`, no project file needed. |
| Indexer | The member that makes `x[...]` work. |
| Native AOT | Compiling an app ahead of time into machine code, for fast startup. It disallows some reflection. |
| Pattern | A test inside `is` or `switch`, such as `Weapon { Damage: > 10 }`: is it a Weapon, and is its Damage over 10? |
| Preview | Ships in the box but needs an opt-in setting and may change before it is final. |
| Source generator | A compiler plug-in that writes code at build time, instead of using reflection at run time. |
| Spread (`..`) | Inside a collection expression, copy every item from another collection. |
| State machine | The hidden class the compiler generates for each `async` method today. Runtime Async removes it. |
| STS / LTS | Standard Term Support (2 years) vs Long Term Support (3 years). .NET 10 and 11 both end November 14, 2028. |
