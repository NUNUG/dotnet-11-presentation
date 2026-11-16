// C# 15 PREVIEW: the updated memory safety model
// Run: dotnet run 06_MemorySafetyPreview.cs
//
// The one C# 15 feature that still needs LangVersion=preview.
//
// Today, `unsafe` means "pointers appear here". Merely declaring a pointer needs it,
// even though declaring a pointer cannot corrupt memory. The new model moves `unsafe`
// to the operations that actually read or write through a pointer, which is where
// memory bugs come from, so a reviewer can find the dangerous lines by searching for it.
// This is step one of a plan that spans several releases.

// File-based app directives: the same as these MSBuild properties in a .csproj.
//   LangVersion=preview                        the new syntax and the pointer relaxations
//   AllowUnsafeBlocks=true                     still required for any unsafe code
//   Features=updated-memory-safety-rules       the stricter caller rules in section 3
// NOTE: a #: directive line cannot carry a trailing comment; it becomes part of the value.
#:property LangVersion=preview
#:property AllowUnsafeBlocks=true
#:property Features=updated-memory-safety-rules

using System.Runtime.InteropServices;
using static System.Console;

WriteLine("=== 1. Pointer relaxations: these no longer need an unsafe context ===");

// None of the next five lines is inside an unsafe block. All are errors in C# 14.
var number = 42;
int* pointer = &number;                    // NEW: declare a pointer, take an address
int[] scores = [10, 20, 30];
int* scratch = stackalloc int[4];          // NEW: stackalloc converted to a pointer
WriteLine($"  sizeof(Tile) = {sizeof(Tile)} bytes");   // NEW: sizeof on any unmanaged type

fixed (int* first = scores)                // NEW: pinning
{
	// Reading THROUGH a pointer is the dangerous part, so that still needs unsafe:
	// *pointer (indirection), first[2] (element access), p->field (member access).
	unsafe
	{
		WriteLine($"  *pointer = {*pointer}, first[2] = {first[2]}");
	}
}

WriteLine();
WriteLine("=== 2. unsafe(...) expressions ===");

// NEW: an unsafe context for exactly ONE expression, rather than a whole block.
// Keeps the unsafe region as small as possible.
var viaExpression = unsafe(*pointer) + 1;
WriteLine($"  unsafe(*pointer) + 1 = {viaExpression}");

// It also works where a block cannot go: field initializers, constructor
// initializers (`: base(...)`), and catch filters. See Header.Signature below.
WriteLine($"  Header.Signature = 0x{Header.Signature:X}");

WriteLine();
WriteLine("=== 3. unsafe on a member is now a contract for the caller ===");

Span<byte> buffer = stackalloc byte[4];

// Dungeon.Poke is marked `unsafe`. Under the updated rules that means "requires unsafe":
// whoever CALLS it must also be in an unsafe context, acknowledging the obligation.
// LIVE EDIT: uncomment the next line to show
//   error CS9362: 'Dungeon.Poke(...)' must be used in an unsafe context because it is marked as 'unsafe'
// Dungeon.Poke(buffer, 1, 0xFF);
unsafe
{
	Dungeon.Poke(buffer, 1, 0xFF);
}

WriteLine($"  buffer = [{string.Join(", ", buffer.ToArray())}]");

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

[StructLayout(LayoutKind.Sequential)]
public struct Tile
{
	public byte Kind;
	public short Light;     // 1 byte + 1 padding byte + 2 bytes = sizeof 4
}

public static class Header
{
	// Read() is marked unsafe (requires-unsafe, see section 3), so calling it needs an unsafe context.
	// A field initializer cannot contain an unsafe BLOCK, but it can contain an unsafe EXPRESSION.
	public static readonly int Signature = unsafe(Read());

	static unsafe int Read()
	{
		var raw = 0x1138;
		int* p = &raw;          // fine without an unsafe context now
		return unsafe(*p);      // only the dereference is marked (the `unsafe` on Read does not cover it)
	}
}

public static class Dungeon
{
	// "requires-unsafe": the audit obligation flows to every caller.
	// Document what the caller must guarantee, here: 0 <= index < buffer.Length
	public static unsafe void Poke(Span<byte> buffer, int index, byte value)
	{
		fixed (byte* start = buffer)
		{
			// NEW and surprising: `unsafe` on the signature no longer makes the BODY unsafe.
			// It is now a statement about callers. The dangerous operation is still marked
			// where it happens.
			// LIVE EDIT: remove this unsafe block to show
			//   error CS9360: This operation may only be used in an unsafe context
			unsafe
			{
				start[index] = value;
			}
		}
	}
}
