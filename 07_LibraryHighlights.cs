// .NET 11 libraries: the demoable highlights
// Run: dotnet run 07_LibraryHighlights.cs
//
// Theme: the guild ledger. No NuGet packages: everything here is in the box.
//
// NOTE: JSON in this file goes through the System.Text.Json source generator
// (the LedgerJson class at the bottom). File-based apps default to Native AOT-friendly
// settings, which switch off reflection-based serialization, so a plain
// JsonSerializer.Serialize(value) call compiles with trimming warnings and then THROWS:
//   InvalidOperationException: Reflection-based serialization has been disabled for this application.

using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using static System.Console;

WriteLine("=== 1. Process: run and capture in one call ===");

// NEW: one call starts the process, reads stdout and stderr, and waits for exit.
// The old way: ProcessStartInfo with RedirectStandardOutput, UseShellExecute = false,
// Start, ReadToEndAsync, WaitForExitAsync, and care to drain stderr so it cannot deadlock.
// The arguments are a collection, so there is no quoting or escaping to get wrong.
var sdk = await Process.RunAndCaptureTextAsync("dotnet", ["--version"]);

// ExitStatus is new too: it can tell a normal exit apart from being killed by a signal
WriteLine($"  dotnet --version -> {sdk.StandardOutput.Trim()} (exit code {sdk.ExitStatus.ExitCode})");

WriteLine();
WriteLine("=== 2. LINQ: FullJoin, and joins that return tuples ===");

(string Item, int Count)[] ledger = [("Rope", 4), ("Torch", 9), ("Lockpick", 2)];
(string Item, int Count)[] shelf = [("Torch", 9), ("Lockpick", 1), ("Potion", 6)];

// NEW: FullJoin keeps unmatched rows from BOTH sides (a SQL FULL OUTER JOIN).
// .NET 10 added LeftJoin and RightJoin; .NET 11 completes the set.
// NOTE: no result selector. The join returns (outer, inner) tuples, deconstructed
// straight into `book` and `actual` here. A side with no match is `default`,
// which for these tuples means Item == null and Count == 0.
foreach (var (book, actual) in ledger.FullJoin(shelf, book => book.Item, actual => actual.Item))
{
	// A tuple pattern: switch on both Item values at once
	var verdict = (book.Item, actual.Item) switch
	{
		(null, _) => "on the shelf, missing from the ledger",
		(_, null) => "in the ledger, missing from the shelf",
		_ when book.Count != actual.Count => $"count mismatch ({book.Count} vs {actual.Count})",
		_ => "ok",
	};
	WriteLine($"  {book.Item ?? actual.Item,-9} {verdict}");
}

WriteLine();
WriteLine("=== 3. System.Text.Json understands C# 15 unions ===");

// The serializer writes the ACTIVE CASE directly: no wrapper object, no $type discriminator.
// An int case becomes a JSON number, a string case a JSON string.
Reward[] rewards = [250, "Vorpal Blade"];
var json = JsonSerializer.Serialize(rewards, LedgerJson.Default.RewardArray);
WriteLine($"  serialized:   {json}");

// Reading back: with no discriminator, the case is chosen from the JSON token kind.
// A number can only be the int case, a string only the string case.
var roundTripped = JsonSerializer.Deserialize(json, LedgerJson.Default.RewardArray)!;
WriteLine($"  deserialized: {string.Join(", ", roundTripped.Select(reward => reward switch
{
	int coins => $"{coins} coins",
	string item => $"item '{item}'",
}))}");

WriteLine();
WriteLine("=== 4. Unions of objects: each case keeps its own JSON contract ===");

// The union adds no serialization rules of its own. The active case is serialized
// with ITS type's contract, so attributes on the case types still apply.
// Product.CostPrice is [JsonIgnore], so it never leaves the server.
ProductResult found = new Product(42, "Keyboard", CostPrice: 31.50m);
ProductResult missing = new NotFound(99);
WriteLine($"  Product:  {JsonSerializer.Serialize(found, LedgerJson.Default.ProductResult)}");
WriteLine($"  NotFound: {JsonSerializer.Serialize(missing, LedgerJson.Default.ProductResult)}");

// The catch: both cases are JSON objects, so the token kind cannot pick one.
// Without help, the source generator warns (SYSLIB1227) and deserializing throws JsonException.
// ProductResult opts into the structural classifier (see its attribute at the bottom),
// which picks the case by the ROOT-LEVEL PROPERTY NAMES in the payload.
var fromClient = JsonSerializer.Deserialize("""{"Id":7,"Name":"Mouse","CostPrice":9}""", LedgerJson.Default.ProductResult);
WriteLine($"  read {{Id, Name, CostPrice}}: {fromClient.Value}");
WriteLine("    CostPrice is 0: [JsonIgnore] also stops a client from SETTING it");

var notFound = JsonSerializer.Deserialize("""{"MissingId":7}""", LedgerJson.Default.ProductResult);
WriteLine($"  read {{MissingId}}:           {notFound.Value}");

// NOTE: the structural classifier only looks at property names, never values or nested data.
// If two cases share a shape, you need a custom JsonTypeClassifierFactory, or a closed
// hierarchy (next section) which writes a $type discriminator instead.

WriteLine();
WriteLine("=== 5. System.Text.Json infers polymorphism from a closed hierarchy ===");

// NEW: InferClosedTypePolymorphism. Before this you listed every subtype by hand with
// [JsonDerivedType(typeof(Fetch), "Fetch")] and so on, and the list drifted out of date.
// Because Quest is closed, the compiler already knows the complete list.
// Unlike a union, this DOES write a $type discriminator, so similar shapes round-trip safely.
Quest[] quests = [new Fetch("Golden Chalice"), new Slay("Dragon", 1)];
var questJson = JsonSerializer.Serialize(quests, LedgerJson.Default.QuestArray);
WriteLine($"  {questJson}");
WriteLine($"  back again: {string.Join(", ", JsonSerializer.Deserialize(questJson, LedgerJson.Default.QuestArray)!.Select(quest => quest.GetType().Name))}");

WriteLine();
WriteLine("=== 6. JSON Lines from an IAsyncEnumerable ===");

// NEW: topLevelValues: true writes JSON Lines (one compact JSON value per line)
// instead of one big array. Handy for logs, exports and streaming APIs, because the
// reader can process each line as it arrives without waiting for a closing bracket.
using var lines = new MemoryStream();
await JsonSerializer.SerializeAsyncEnumerable(lines, Patrol(), LedgerJson.Default.Sighting, topLevelValues: true);
foreach (var line in Encoding.UTF8.GetString(lines.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries))
	WriteLine($"  {line}");

static async IAsyncEnumerable<Sighting> Patrol()
{
	yield return new("Goblin", 3);
	await Task.Yield();
	yield return new("Dragon", 1);
}

WriteLine();
WriteLine("=== 7. Zstandard in the box ===");

// NEW: ZstandardStream sits in System.IO.Compression next to GZipStream and BrotliStream.
// Same Stream-wrapping shape, so switching is a one-word change.
// ~3 MB of log-shaped text, seeded so every run compresses the same bytes.
var dice = new Random(11);
string[] monsters = ["goblin", "skeleton", "ogre", "lich", "dragon"];
var chronicle = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, 60_000).Select(turn =>
	$"turn={turn} room={dice.Next(400)} monster={monsters[dice.Next(monsters.Length)]} damage={dice.Next(1, 40)} gold={dice.Next(1000)}\n")));

WriteLine($"  original  {chronicle.Length,9:N0} bytes");
Report("gzip", chronicle, stream => new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true));
Report("brotli", chronicle, stream => new BrotliStream(stream, CompressionLevel.Optimal, leaveOpen: true));
Report("zstandard", chronicle, stream => new ZstandardStream(stream, CompressionLevel.Optimal, leaveOpen: true));

// NOTE: times vary by machine. The point to make is the shape: Zstandard is several times
// faster than gzip at a somewhat larger output, which is why it is popular for logs and caches.
static void Report(string name, byte[] data, Func<Stream, Stream> wrap)
{
	using var output = new MemoryStream();
	var started = Stopwatch.GetTimestamp();
	using (var compressor = wrap(output))   // disposing the compressor flushes the final block
		compressor.Write(data);
	WriteLine($"  {name,-9} {output.Length,9:N0} bytes  {Stopwatch.GetElapsedTime(started).TotalMilliseconds,6:N0} ms");
}

WriteLine();
WriteLine("=== 8. Stream adapters: no copy into a MemoryStream ===");

// NEW: StringStream reads a string as a Stream, encoding as it goes.
// The old way was new MemoryStream(Encoding.UTF8.GetBytes(text)): a whole extra byte[] copy.
using var scroll = new StringStream("""{"Monster":"Lich","Count":1}""", Encoding.UTF8);
WriteLine($"  from StringStream:           {JsonSerializer.Deserialize(scroll, LedgerJson.Default.Sighting)}");

// NEW: ReadOnlySequenceStream reads a segmented ReadOnlySequence<byte> as a Stream.
// Pipelines (System.IO.Pipelines, Kestrel) hand you data as a chain of buffers. Previously
// you called .ToArray() to flatten it before passing it to a Stream-based API.
// Here the JSON is deliberately split mid-word across two segments: {"Monster":"Og | re","Count":2}
var first = new Segment("""{"Monster":"Og"""u8.ToArray());
var last = first.Append("""re","Count":2}"""u8.ToArray());
using var segmented = new ReadOnlySequenceStream(new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length));
WriteLine($"  from ReadOnlySequenceStream: {JsonSerializer.Deserialize(segmented, LedgerJson.Default.Sighting)}");

// The other two adapters: ReadOnlyMemoryStream wraps a ReadOnlyMemory<byte>,
// WritableMemoryStream wraps a fixed-size Memory<byte>.

WriteLine();
WriteLine("=== 9. Small things you will use ===");

// NEW: EqualityComparer<T>.Create(keySelector). "Equal if the names match" in one line,
// instead of a whole class implementing IEqualityComparer<T>.
// Combined with C# 15's with(...) to hand the comparer to the HashSet.
HashSet<(string Name, int Level)> party = [with(EqualityComparer<(string Name, int Level)>.Create(member => member.Name)),
	("Aria", 5), ("Brom", 4), ("Aria", 9)];
WriteLine($"  EqualityComparer.Create:  {party.Count} unique names");

// NEW: generic random numbers for any integer type, no casting from int.
// The upper bound is exclusive, as with Random.Next, so 1..21 is a d20.
WriteLine($"  Random.NextInteger<byte>: d20 roll = {Random.Shared.NextInteger<byte>(1, 21)}");

// NEW: string methods that take a Rune (a full Unicode scalar value).
// 🐉 is outside the 16-bit range, so it cannot be passed as a single `char`.
var banner = "Here be 🐉🐉 dragons";
WriteLine($"  string.Contains(Rune):    {banner.Contains(new Rune(0x1F409))}");

// NEW: find the exact byte offset of bad UTF-8, for a precise error message.
// 0xC3 starts a 2-byte sequence, but 0x28 '(' is not a valid continuation byte.
ReadOnlySpan<byte> corrupt = [(byte)'o', (byte)'k', 0xC3, 0x28];
WriteLine($"  Utf8.IndexOfInvalidSubsequence: {Utf8.IndexOfInvalidSubsequence(corrupt)}");

// NEW: hexadecimal floating-point text ("X" format, HexFloat parsing). Every bit is preserved,
// which decimal text cannot promise. Matches C's printf("%a"), useful for golden-file tests.
var hex = Math.PI.ToString("X");
WriteLine($"  Math.PI as hex float:     {hex}  round-trips: {double.Parse(hex, System.Globalization.NumberStyles.HexFloat) == Math.PI}");

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

// Primitive cases: the JSON token kind alone picks the case
public union Reward(int, string);

// Object cases: [JsonIgnore] on a case type is honored, on write AND on read
public record Product(int Id, string Name, [property: JsonIgnore] decimal CostPrice = 0);
public record NotFound(int MissingId);

// NEW: [JsonUnion] with the built-in structural classifier. Remove the attribute and the
// build warns SYSLIB1227: both cases serialize as JSON objects, so reading is ambiguous.
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union ProductResult(Product, NotFound);

// NEW: InferClosedTypePolymorphism. No [JsonDerivedType] list to maintain.
[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record Quest;
public record Fetch(string Item) : Quest;
public record Slay(string Monster, int Count) : Quest;

public record Sighting(string Monster, int Count);

// Source-generated JSON metadata. Each [JsonSerializable] produces a property on
// LedgerJson.Default (RewardArray, ProductResult, QuestArray, Sighting).
[JsonSerializable(typeof(Reward[]))]
[JsonSerializable(typeof(ProductResult))]
[JsonSerializable(typeof(Quest[]))]
[JsonSerializable(typeof(Sighting))]
partial class LedgerJson : JsonSerializerContext;

// A minimal linked buffer segment, to build a multi-segment ReadOnlySequence<byte> by hand.
// In real code the pipeline creates these for you.
sealed class Segment : ReadOnlySequenceSegment<byte>
{
	public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

	public Segment Append(ReadOnlyMemory<byte> memory)
	{
		var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
		Next = next;
		return next;
	}
}
