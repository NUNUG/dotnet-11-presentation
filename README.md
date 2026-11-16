# .NET 11 at NUNUG: demo run sheet

Every demo is a single-file app. All were built and run against the released SDK, **11.0.100**.

```
dotnet --version        # needs 11.0.100 or later
dotnet run 01_Unions.cs
```

The first run of each file compiles it; later runs are fast. Run each one once before the meeting so nothing compiles on stage.

## Reading the comments

The comments use three markers so you can find the talking points quickly:

- `NEW:` syntax or an API that did not exist before .NET 11 / C# 15
- `NOTE:` a behavior worth pointing out, often a surprise or a sharp edge
- `LIVE EDIT:` a change to make on stage, with the exact diagnostic it produces

## Order

| # | File | Feature | Status |
|---|------|---------|--------|
| 1 | `01_Unions.cs` | Union types | C# 15 |
| 2 | `02_ClosedHierarchies.cs` | `closed` classes, and how they differ from `internal` and `sealed` | C# 15 |
| 3 | `03_CollectionExpressionArguments.cs` | `with(...)` in collection expressions | C# 15 |
| 4 | `04_ExtensionIndexers.cs` | Indexers in `extension` blocks | C# 15 |
| 5 | `05_LabeledBreakContinue.cs` | `break label;` / `continue label;` | C# 15 |
| 6 | `06_MemorySafetyPreview.cs` | Updated `unsafe` model | C# 15 **preview** |
| 7 | `07_LibraryHighlights.cs` | Process, FullJoin, JSON for unions and closed types, Zstandard, stream adapters | .NET 11 libraries |
| 8 | `08_RuntimeAsync.cs` | Live stacks, the cost of `async` without `await`, exception timing | .NET 11 **preview** |
| 9 | `09_FileBasedApps/Tavern.cs` | `#:include`, `dotnet run -e` | .NET 11 SDK |
| 10 | `10_BreakingChanges.cs` | decimal conversion, saturating casts, empty gzip | behavior changes |

The seventh C# 15 feature, non-virtual static interface members on older runtimes, only shows up when you target a runtime without default interface method support, so it is on a slide instead.

## Live edits (all verified on 11.0.100)

- **01:** delete the `Potion` arm from `Describe` → `warning CS8509 ... the pattern 'Potion' is not covered`.
- **02:** add `public record Slime() : Monster("Slime", 4);` at the bottom → `Attack` reports CS8509 for `Slime`.
- **04, section 4:** uncomment `raw[1, 1]` → `error CS0022`. Arrays never use extension indexers.
- **06, section 3:** uncomment the bare `Dungeon.Poke(...)` → `error CS9362`, the caller must be in an unsafe context.
- **06, `Poke`:** remove the inner `unsafe { }` → `error CS9360`. `unsafe` on a signature no longer makes the body unsafe.
- **08:** run it twice and compare:

      dotnet run 08_RuntimeAsync.cs
      dotnet run 08_RuntimeAsync.cs -p:Features=runtime-async=on

  | | state machine | runtime async |
  |---|---|---|
  | Frames before the first suspension | 13 | 5 |
  | `async` without `await`, awaited by an async caller | 104 B/call | 0 B/call |

- **09:** run from inside `09_FileBasedApps`, then again with `-e TAVERN_NAME="The Gilded Griffin"`.

## Notes from building these

- There are no dictionary collection expressions in C# 15: `Dictionary<string,int> d = [with(comparer), new("a", 1)]` fails with CS9215.
- File-based apps default to Native AOT settings, so reflection-based `JsonSerializer` calls throw at run time. Demo 07 uses a `JsonSerializerContext`.
- A `#:property` directive cannot carry a trailing `//` comment; the comment becomes part of the value.
- Warning CS1998 ("async method lacks await") was removed from the compiler in the .NET 10 SDK. Nothing warns about `async` without `await` any more.
- The Zstandard timings in demo 07 vary by machine. Expect the shape (somewhat larger, several times faster than gzip), not the exact milliseconds.
