// C# 15: Extension indexers
// Run: dotnet run 04_ExtensionIndexers.cs
//
// Theme: the dungeon map.
//
// C# 14 introduced `extension` blocks, which went beyond extension methods to
// extension properties and operators. Indexers were the missing member kind.
// C# 15 adds them, so you can bolt `x[...]` onto a type you do not own.

using static System.Console;

List<string> map =
[
	"#########",
	"#@..#..$#",
	"#.#.#.#.#",
	"#.#...#D#",
	"#########",
];

WriteLine("=== 1. Index a type you do not own ===");

// List<string> only has map[int]. The two-argument map[row, column] comes from the
// extension indexer declared at the bottom of this file.
// The compiler tries the type's own indexers first; when none fit, it looks for extensions.
WriteLine($"  map[1, 1] = '{map[1, 1]}'  (the hero)");
WriteLine($"  map[1, 7] = '{map[1, 7]}'  (the treasure)");
WriteLine($"  map[3, 7] = '{map[3, 7]}'  (the dragon)");

WriteLine();
WriteLine("=== 2. Any parameter type, including your own ===");

// The index does not have to be an int. Here it is our own Point struct.
var hero = new Point(Row: 1, Column: 1);
WriteLine($"  map[hero] = '{map[hero]}'");

// `hero with { Column = 2 }` is a record copy with one property changed (C# 9 syntax).
// Describe() is a C# 14 extension method on char, defined in the same extension class.
WriteLine($"  east of hero: {map[hero with { Column = 2 }].Describe()}");

WriteLine();
WriteLine("=== 3. Indexers on interfaces, generic too ===");

// IReadOnlyDictionary has no "or default" indexer. This one works for every
// IReadOnlyDictionary<TKey, TValue>, because the extension block itself is generic.
IReadOnlyDictionary<string, int> inventory = new Dictionary<string, int> { ["torch"] = 3, ["rope"] = 1 };

// NOTE: named arguments work in indexers: `orElse:` reads better than a bare 0
WriteLine($"  torches: {inventory["torch", orElse: 0]}");
WriteLine($"  potions: {inventory["potion", orElse: 0]}");

WriteLine();
WriteLine("=== 4. Sharp edge: arrays ===");

// Arrays use built-in element access, which never looks for extension indexers.
// LIVE EDIT: uncomment the next line to show
//   error CS0022: Wrong number of indices inside []; expected 1
string[] raw = [.. map];
// WriteLine(raw[1, 1]);

// Workaround: view the array through an interface that the extension targets.
// That is why the extension below is declared on IReadOnlyList<string> rather than List<string>.
IReadOnlyList<string> viaInterface = raw;
WriteLine($"  through IReadOnlyList<string>: '{viaInterface[1, 1]}'");

// ---------------------------------------------------------------------------
// Types and extensions
// ---------------------------------------------------------------------------

public record struct Point(int Row, int Column);

public static class MapExtensions
{
	// An extension block: `map` is the receiver, i.e. the object the members act on.
	// NOTE: indexers are always instance members, so the receiver MUST be named.
	// (A static-only extension block could write `extension(IReadOnlyList<string>)`.)
	extension(IReadOnlyList<string> map)
	{
		// NEW: indexers in an extension block. Ordinary indexer syntax, nothing extra.
		// Both are get-only here; a `set` accessor is allowed as well.
		public char this[int row, int column] => map[row][column];
		public char this[Point point] => map[point.Row][point.Column];
	}

	// A C# 14 extension method, used in section 2
	extension(char tile)
	{
		public string Describe() => tile switch
		{
			'#' => "wall",
			'.' => "open floor",
			'$' => "treasure",
			'D' => "dragon",
			'@' => "hero",
			_ => "unknown",
		};
	}

	// A generic extension block: TKey and TValue are inferred from the receiver
	extension<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> source)
	{
		public TValue this[TKey key, TValue orElse] => source.TryGetValue(key, out var value) ? value : orElse;
	}
}
