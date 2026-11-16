// C# 15: Closed hierarchies
// Run: dotnet run 02_ClosedHierarchies.cs
//
// Theme: the bestiary. You own the base class, and you know every monster.
//
// Three keywords that sound alike and answer different questions:
//   internal  - who can SEE and USE the type?   (only this assembly)
//   sealed    - can anyone DERIVE from it?       (no one, not even this assembly)
//   closed    - who can derive, and is the list complete?
//               (only this assembly, and the compiler treats that list as final)
//
// `public closed` is the interesting combination: other assemblies can use Monster,
// take it as a parameter and pattern-match on it, but they cannot add a new monster.

using static System.Console;

WriteLine("=== 1. Exhaustive switch over a class hierarchy ===");

Monster[] wave = [new Goblin(3), new Skeleton(HasShield: true), new Dragon("Smolder", Element.Fire)];

foreach (var monster in wave)
	WriteLine($"  {monster.Name,-22} {monster.HitPoints,3} hp   {Attack(monster)}");

// NEW: no `_ =>` discard arm. Because Monster is closed, the compiler knows that
// Goblin, Skeleton and Dragon are the only direct subclasses, so this switch is complete.
//
// Before C# 15 you had to add `_ => throw new UnreachableException()` to silence CS8509.
// That arm then also swallowed the warning you WANTED when someone added a new monster.
//
// LIVE EDIT: add this line at the bottom of the file:
//   public record Slime() : Monster("Slime", 4);
// and this switch reports: warning CS8509 ... the pattern 'Slime' is not covered.
static string Attack(Monster monster) => monster switch
{
	Goblin g => $"{g.PackSize} rusty daggers",
	Skeleton { HasShield: true } => "shield bash",   // property pattern: a more specific arm first...
	Skeleton => "bone club",                          // ...then the general one, so Skeleton is fully covered
	Dragon d => $"{d.Breath} breath",
};

WriteLine();
WriteLine("=== 2. Closed is not sealed, and it is not transitive ===");

// Dragon is an ordinary (non-closed, non-sealed) record, so ElderDragon may derive from it,
// even from another assembly. The switch above is still exhaustive:
// an ElderDragon IS a Dragon, so the `Dragon d` arm catches it.
Monster boss = new ElderDragon("Vermithrax", Element.Frost, Age: 900);
WriteLine($"  {boss.Name}: {Attack(boss)}");

WriteLine();
WriteLine("=== 3. Close the intermediate level to push exhaustiveness down ===");

Encounter[] rooms = [new Fight(wave[0]), new Shop("Ye Olde Potions"), new Riddle("What has keys but no locks?")];

foreach (var room in rooms)
	WriteLine($"  {Enter(room)}");

// NOTE: this switch names Shop and Riddle, not Peaceful. That only counts as complete
// because Peaceful is ALSO closed. If Peaceful were an ordinary class, the compiler would
// have to assume some other Peaceful subclass might exist and would warn here.
static string Enter(Encounter encounter) => encounter switch
{
	Fight f => $"Roll initiative against {f.Enemy.Name}",
	Shop s => $"Browse the wares at {s.Keeper}",
	Riddle r => $"A voice asks: {r.Question}",
};

WriteLine();
WriteLine("=== 4. Closed implies abstract ===");

// A closed class is implicitly abstract: you only ever create the subclasses.
// `closed` cannot be combined with `sealed`, `static`, or an explicit `abstract`.
WriteLine($"  typeof(Monster).IsAbstract: {typeof(Monster).IsAbstract}");
// new Monster("Nobody", 0);  // error CS0144: cannot create an instance of the abstract type

// Union or closed hierarchy? (see 01_Unions.cs)
//   union   - composes types you may not own; no shared base, no shared members
//   closed  - you own the base class and want shared state/behavior on it

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

public enum Element { Fire, Frost }

// NEW: the `closed` modifier. Works on classes and records.
// Shared state (Name, HitPoints) lives on the base, which a union cannot offer.
public closed record Monster(string Name, int HitPoints);
public record Goblin(int PackSize) : Monster($"Goblin pack of {PackSize}", PackSize * 7);
public record Skeleton(bool HasShield) : Monster("Skeleton", HasShield ? 30 : 18);
public record Dragon(string Title, Element Breath) : Monster($"{Title} the Dragon", 250);

// Legal: Dragon is neither closed nor sealed. Closing is per class, not per hierarchy.
public record ElderDragon(string Title, Element Breath, int Age) : Dragon(Title, Breath);

// Two levels, both closed, so a switch can be exhaustive over the leaves
public closed record Encounter;
public record Fight(Monster Enemy) : Encounter;
public closed record Peaceful : Encounter;
public record Shop(string Keeper) : Peaceful;
public record Riddle(string Question) : Peaceful;
