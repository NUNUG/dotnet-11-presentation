// .NET 11: three behavior changes you can see for yourself
// Run: dotnet run 10_BreakingChanges.cs
//
// Theme: the tax collector. None of these fails to compile; all of them change output.
// The full list: https://learn.microsoft.com/dotnet/core/compatibility/11

using System.Globalization;
using System.IO.Compression;
using static System.Console;

WriteLine("=== 1. double -> decimal is now correctly rounded ===");

// Why it happens: a double is binary, and 1.23 has no exact binary form.
// The double actually stores 1.229999999999999982236431606...
//
// BEFORE: the cast rounded to about 15 significant digits, which happened to hide that: 1.23
// NOW:    the cast converts the stored value exactly, as far as decimal's 28-29 digits allow.
// There is no compatibility switch.
var rate = 1.23;
WriteLine($"  (decimal)1.23         = {(decimal)rate}");
WriteLine($"  (decimal)0.1f         = {(decimal)0.1f}");
WriteLine($"  (decimal)(0.1 + 0.2)  = {(decimal)(0.1 + 0.2)}");

// What breaks: equality checks against decimal literals, and anything that prints or
// stores the result (invoices, reports, database columns with enough precision).
WriteLine($"  (decimal)1.23 == 1.23m: {(decimal)rate == 1.23m}");

WriteLine();
WriteLine("  Fixes, best first:");

// Best: never go through double. A decimal literal (m suffix) or decimal arithmetic throughout.
WriteLine($"    stay in decimal:            {1.23m}");

// When the double comes from code you cannot change: round to the precision you actually mean
WriteLine($"    Math.Round((decimal)d, 2):  {Math.Round((decimal)rate, 2)}");

// Or go through the shortest round-trip text of the double, which is what you see when you print it.
// Closest to the old-looking results, without hard-coding a number of decimal places.
WriteLine($"    decimal.Parse(d.ToString()): {decimal.Parse(rate.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)}");

WriteLine();
WriteLine("=== 2. Float to small integer casts saturate instead of wrapping ===");

// An `unchecked` cast from double to byte, sbyte, short or ushort, when the value does not fit.
// BEFORE: truncate to int first, then keep the low bits. (byte)300.0 == 44 (300 - 256).
// NOW:    clamp to the target's range. (byte)300.0 == 255. NaN becomes 0.
// (Casts to int and long already saturated as of .NET 9; .NET 11 extends it to the small types.)
//
// Where it matters: image, audio and sensor code that turns doubles into bytes or shorts.
// Clamping is usually what you wanted. If you relied on wraparound, it is gone.
// If you want an error instead, use checked(...): it throws OverflowException.
WriteLine($"  {"value",8} {"(byte)",7} {"(short)",8}");
foreach (var value in (double[])[300.0, -5.0, 70_000.0, double.NaN])
	WriteLine($"  {value,8} {unchecked((byte)value),7} {unchecked((short)value),8}");

WriteLine();
WriteLine("=== 3. GZipStream and DeflateStream write a valid stream even when empty ===");

// BEFORE: compressing nothing produced 0 bytes, which is not a valid gzip file.
// NOW:    the header and footer are always written, so the output is a real (empty) gzip stream.
using var output = new MemoryStream();
using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
{
	// nothing written
}

WriteLine($"  empty gzip payload: {output.Length} bytes");

// What breaks: code that checks `compressed.Length == 0` to mean "nothing to send",
// and tests that compare compressed output byte for byte.
WriteLine("  anything that tested `stream.Length == 0` to mean \"nothing to send\" just changed");
