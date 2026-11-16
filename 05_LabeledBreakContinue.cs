// C# 15: Labeled break and continue
// Run: dotnet run 05_LabeledBreakContinue.cs
//
// Theme: searching the dungeon.
//
// A plain `break` or `continue` always targets the innermost loop (or switch).
// C# 15 lets you name the loop you mean. Java, JavaScript, Kotlin, Rust and Go
// have had this for years; C# used flags or goto.

using static System.Console;

string[] map =
[
	"#########",
	"#@..#..$#",
	"#.#.#.#.#",
	"#T#...#D#",
	"#########",
];

WriteLine("=== 1. break out of the outer loop ===");

(int Row, int Column)? treasure = null;

// NEW: a label placed directly on a loop. The syntax is the same as a goto label
// (`name:`), but it must sit on the loop or switch statement it names.
search: for (var row = 0; row < map.Length; row++)
{
	for (var column = 0; column < map[row].Length; column++)
	{
		if (map[row][column] is '$')
		{
			treasure = (row, column);
			break search;   // NEW: leaves BOTH loops in one step
		}
	}
}

WriteLine($"  treasure at {treasure}");

WriteLine();
WriteLine("=== 2. continue the outer loop ===");

// A row is safe when it holds no trap (T) and no dragon (D)
rows: foreach (var line in map)
{
	foreach (var tile in line)
	{
		// `tile is 'T' or 'D'` is a pattern with `or` (C# 9): same as tile == 'T' || tile == 'D'
		if (tile is 'T' or 'D')
			continue rows;  // NEW: skip the rest of THIS row and move to the next line
	}

	// Only reached when the inner loop finished without finding a hazard
	WriteLine($"  safe: {line}");
}

WriteLine();
WriteLine("=== 3. break a loop from inside a switch ===");

// The classic trap: inside a switch statement, a plain `break` only leaves the SWITCH,
// not the loop around it. Getting out of the loop used to need a flag or a goto.
var steps = 0;

walk: foreach (var command in "EESSXEE")
{
	switch (command)
	{
		case 'E' or 'S':
			steps++;
			break;          // plain break: ends this case, the loop keeps going
		case 'X':
			WriteLine($"  hit the exit after {steps} steps");
			break walk;     // NEW: labeled break: ends the loop itself
	}
}

WriteLine();
WriteLine("=== 4. What it replaces ===");

// C# 14, option A: a flag, checked in the outer loop's condition.
// Easy to get wrong: forget `&& !found` and the outer loop carries on.
var found = false;
for (var row = 0; row < map.Length && !found; row++)
	for (var column = 0; column < map[row].Length; column++)
		if (map[row][column] is 'D')
		{
			found = true;
			break;
		}

// C# 14, option B: goto past the loops. Works, but jumps to a label far from the loops.
for (var row = 0; row < map.Length; row++)
	for (var column = 0; column < map[row].Length; column++)
		if (map[row][column] is 'D')
			goto done;
done:

WriteLine($"  flag version found the dragon: {found}");

// NOTE: the IDE0410 code-style rule flags both patterns above and offers to rewrite
// them as a labeled break. Without a label, break and continue behave exactly as before,
// so existing code is unaffected.
WriteLine("  IDE0410 offers to rewrite both patterns as a labeled break");
