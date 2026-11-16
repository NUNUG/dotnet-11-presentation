// C# 15: Union types
// Run: dotnet run 01_Unions.cs
//
// Theme: the dungeon. Every chest holds exactly one kind of loot.
//
// A union says "this value is exactly one of these types" and the compiler holds
// you to it. Before C# 15 the usual options were `object` (no checking at all),
// a marker interface or base class (open to anyone, so no exhaustiveness), or
// a library such as OneOf.

using static System.Console;

WriteLine("=== 1. Declare, convert, match ===");

// NEW: no `new Loot(...)` needed. Each case type converts implicitly to the union.
// The collection expression is typed Loot[], so each element is converted on the way in.
Loot[] chest = [new Gold(250), new Weapon("Vorpal Blade", 12), new Potion("Healing", 3)];

foreach (var loot in chest)
	WriteLine($"  {Describe(loot)}");

// NEW: exhaustiveness. There is no `_ =>` discard arm, and none is needed.
// LIVE EDIT: delete the Potion arm and you get
//   warning CS8509: ... the pattern 'Potion' is not covered.
// The same warning appears at every switch when you add a fourth case type to Loot.
//
// NOTE: the patterns test what is INSIDE the union. `loot` is a Loot, not a Gold,
// yet `Gold g` matches and binds the Gold directly. No `.Value` needed.
static string Describe(Loot loot) => loot switch
{
	Gold g => $"{g.Coins} gold coins",
	Weapon w => $"{w.Name} (+{w.Damage} damage)",
	Potion p => $"Potion of {p.Effect} x{p.Doses}",
};

WriteLine();
WriteLine("=== 2. Unions can have members ===");

foreach (var loot in chest)
{
	// `Value` is the one property every union has. It holds the contents, typed object?.
	// The `!` tells the nullable checker "not null here": every chest slot was assigned.
	var kind = loot.Value!.GetType().Name;

	// Interpolation alignment, {expression,width}:
	//   ,-7  pads to 7 characters, left-aligned  ("Gold   ")
	//   ,4   pads to 4 characters, right-aligned (" 250"), so the numbers line up
	WriteLine($"  {kind,-7} sells for {loot.SellPrice,4} gp");
}

WriteLine();
WriteLine("=== 3. Result-or-error without exceptions or base classes ===");

foreach (var door in (string[])["oak", "iron", "mimic"])
{
	// The return type documents every outcome, and the switch must handle all three.
	// Compare with throwing an exception for "locked": callers find out at run time.
	var message = OpenDoor(door) switch
	{
		Room room => $"entered the {room.Name}",
		Locked locked => $"locked, needs the {locked.KeyName} key",
		Trap trap => $"trap! took {trap.Damage} damage",
	};
	WriteLine($"  {door,-5} door: {message}");
}

// Every `return` here is an implicit conversion from Room, Locked or Trap to DoorResult.
static DoorResult OpenDoor(string door) => door switch
{
	"oak" => new Room("Armory"),
	"iron" => new Locked("skeleton"),
	_ => new Trap(7),
};

WriteLine();
WriteLine("=== 4. Generic unions, and primitives as case types ===");

// Unions can be generic. Option<T> is the classic "maybe a value" type.
Option<int> found = new Some<int>(42);
Option<int> missing = new None();
WriteLine($"  found:   {Show(found)}");
WriteLine($"  missing: {Show(missing)}");

static string Show<T>(Option<T> option) => option switch
{
	Some<T> some => $"Some({some.Value})",
	None => "None",
};

// NOTE: case types do not have to be records you wrote. Here they are plain int and string,
// which have no common base type other than object. You cannot do this with inheritance.
IntOrString id = 1138;
WriteLine($"  id: {DescribeId(id)}");
id = "THX";
WriteLine($"  id: {DescribeId(id)}");

static string DescribeId(IntOrString id) => id switch
{
	int number => $"the number {number}",
	string text => $"the text \"{text}\"",
};

WriteLine();
WriteLine("=== 5. The sharp edges ===");

// A `union` declaration compiles to a struct holding a single object? reference.
// Three consequences worth knowing:

// (a) default(Loot) is an empty union: Value is null.
//     Watch for this in arrays and uninitialized fields: `new Loot[10]` is ten empty unions.
Loot empty = default;
WriteLine($"  default(Loot).Value is null: {empty.Value is null}");

// The `null` pattern also looks inside: it is true when the CONTENTS are null.
// Add a `null =>` arm to switches that can see default values (Loot.SellPrice below does).
WriteLine($"  empty is null:               {empty is null}");

// (b) value-type cases are boxed, because the storage is an object reference.
//     Fine for most code; in a hot loop over ints, write a custom union with typed fields instead.
IntOrString boxed = 7;
WriteLine($"  IntOrString holding 7 stores a boxed {boxed.Value!.GetType().Name}");

// (c) a pattern on a union looks through to the contents, including nested patterns.
//     `Weapon { Damage: > 10 }` reads as: the contents are a Weapon, AND its Damage is over 10.
//     That is a type pattern, then a property pattern, then a relational pattern.
var isStrongWeapon = chest[1] is Weapon { Damage: > 10 };
WriteLine($"  chest[1] holds a weapon with more than 10 damage: {isStrongWeapon}");

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

public record Gold(int Coins);
public record Weapon(string Name, int Damage);
public record Potion(string Effect, int Doses);

// NEW: the `union` keyword. The parentheses list the case types.
// A union can have a body with methods and computed properties, but not instance fields
// or auto-properties: the contents live in Value and nowhere else.
public union Loot(Gold, Weapon, Potion)
{
	// `this switch` matches on the union's own contents
	public int SellPrice => this switch
	{
		Gold g => g.Coins,
		Weapon w => w.Damage * 40,
		Potion p => p.Doses * 25,
		null => 0,              // the empty default(Loot) case from section 5
	};
}

public record Room(string Name);
public record Locked(string KeyName);
public record Trap(int Damage);
public union DoorResult(Room, Locked, Trap);

public record None;
public record Some<T>(T Value);
public union Option<T>(None, Some<T>);

// Primitive case types: no wrapper records needed
public union IntOrString(int, string);
