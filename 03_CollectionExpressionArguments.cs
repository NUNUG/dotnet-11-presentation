// C# 15: Collection expression arguments
// Run: dotnet run 03_CollectionExpressionArguments.cs
//
// Theme: the adventurer's pack.
//
// Collection expressions arrived in C# 12: `List<int> x = [1, 2, 3];`
// Their gap: no way to pass constructor arguments, so the moment you needed a comparer
// or a capacity you were back to `new HashSet<string>(items, comparer)`.
// C# 15 fills the gap with a `with(...)` element, which must come first.

using static System.Console;

string[] looted = ["Rope", "TORCH", "torch", "rope", "Lockpick"];

WriteLine("=== 1. Comparers ===");

// `..` is the spread element: copy every item from `looted` into the new collection
HashSet<string> exact = [.. looted];

// NEW: with(...) passes its arguments to the HashSet<string> constructor,
// exactly as if you had written new HashSet<string>(StringComparer.OrdinalIgnoreCase)
HashSet<string> pack = [with(StringComparer.OrdinalIgnoreCase), .. looted];

WriteLine($"  default comparer:    {exact.Count} items  [{string.Join(", ", exact)}]");
WriteLine($"  OrdinalIgnoreCase:   {pack.Count} items  [{string.Join(", ", pack)}]");

WriteLine();
WriteLine("=== 2. Capacity ===");

// NOTE: when the compiler can count the elements, it presizes for you. A spread over
// a LINQ query has no known length, so here an explicit capacity is worth passing.
// Named arguments work inside with(...), which makes the intent obvious.
List<string> sized = [with(capacity: 64), .. looted.Where(item => item.Length > 4)];
WriteLine($"  Count {sized.Count}, Capacity {sized.Capacity}");

WriteLine();
WriteLine("=== 3. Any constructor argument ===");

// Not just comparers and capacity: whatever the constructor takes.
// Here, a descending comparer so the most valuable bounty sorts first.
SortedSet<int> bounties = [with(Comparer<int>.Create((left, right) => right.CompareTo(left))), 40, 250, 5, 900];
WriteLine($"  bounties: {string.Join(" > ", bounties)}");

// NOTE: there is still no dictionary syntax. This does NOT compile in C# 15:
//   Dictionary<string, int> prices = [with(StringComparer.OrdinalIgnoreCase), new("Rope", 2)];
//   error CS9215: ... must have an instance or extension method 'Add' that can be called with a single argument.

WriteLine();
WriteLine("=== 4. Before and after ===");

// C# 14: the comparer forced you back to the constructor
var before = new HashSet<string>(looted, StringComparer.OrdinalIgnoreCase);

// C# 15: stays a collection expression
HashSet<string> after = [with(StringComparer.OrdinalIgnoreCase), .. looted];

WriteLine($"  same contents: {before.SetEquals(after)}");

// NOTE: collection expressions are target-typed. The compiler picks the collection type
// from where the expression is going. Here that is the parameter type of CountDistinct,
// so with(...) is resolved against HashSet<string>'s constructors. The call site never
// names the collection type at all.
WriteLine($"  as an argument: {CountDistinct([with(StringComparer.OrdinalIgnoreCase), "Gem", "GEM", "gem"])} distinct");

static int CountDistinct(HashSet<string> items) => items.Count;
