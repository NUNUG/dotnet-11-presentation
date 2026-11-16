// .NET 11 PREVIEW: Runtime Async
// Run it twice and compare:
//
//   dotnet run 08_RuntimeAsync.cs
//   dotnet run 08_RuntimeAsync.cs -p:Features=runtime-async=on
//
// Same source both times. The first build uses compiler-generated state machines,
// as every C# version since 5 has. The second lets the runtime suspend and resume
// the methods itself. (In a .csproj: <Features>runtime-async=on</Features>.
// No EnablePreviewFeatures needed.)
//
// NOTE: the .NET 11 libraries themselves are ALREADY compiled with runtime async.
// The flag only decides how YOUR code is compiled.

using System.Diagnostics;
using static System.Console;

WriteLine("##### Part 1: live call stacks #####");
WriteLine();

await EnterDungeonAsync();

static async Task EnterDungeonAsync()
{
	await Task.CompletedTask;   // already complete, so this does not suspend
	await DescendStairsAsync();
}

static async Task DescendStairsAsync()
{
	await Task.CompletedTask;
	await OpenChestAsync();
}

static async Task OpenChestAsync()
{
	// Nothing has suspended yet, so the whole async call chain is still on the real stack.
	// Expect 13 frames without the flag (each async method shows up twice, with a
	// AsyncMethodBuilderCore.Start frame between) and 5 with it.
	Show("Before the first suspension");

	// Task.Yield forces a real suspension: the rest of the method runs later,
	// as a continuation on a thread pool thread.
	await Task.Yield();

	// The callers are gone from the stack (they are waiting on Tasks, not on the stack).
	// What remains is the thread pool plus the plumbing that resumes us.
	// Runtime async removes the ExecutionContext and state-machine-box frames.
	Show("After resuming from a real suspension");
}

// The LIVE stack: what a profiler, a debugger, or `new StackTrace()` sees right now.
// NOTE: EXCEPTION stack traces already hid this plumbing, so they look the same either way.
// The improvement is in profilers, the debugger's call stack window, and diagnostic logging.
static void Show(string title)
{
	var frames = new StackTrace(skipFrames: 1).ToString().TrimEnd().Split('\n');

	WriteLine($"=== {title}: {frames.Length} frames ===");
	foreach (var frame in frames)
		WriteLine($"  {frame.Trim()}");
	WriteLine();
}

WriteLine("##### Part 2: is `async` without `await` still a cost? #####");
WriteLine();

// The question: an interface makes you return Task<int>, but you have the answer already.
// Do you write `async` and return the value, or skip `async` and use Task.FromResult?
//
// NOTE: warning CS1998 ("async method lacks 'await' operators") no longer exists.
// It was removed from the compiler in the .NET 10 SDK, partly because people silenced it
// with `await Task.FromResult(x)`, which costs MORE. Nothing warns you either way now.

const int Calls = 100_000;

// Values from 1000 up, so the runtime cannot hand back a cached, pre-allocated Task.
// Two rounds, measuring the second, so one-time JIT allocations are not counted.
for (var round = 0; round < 2; round++)
{
	var viaResult = Measure(() => { for (var i = 0; i < Calls; i++) _ = NoAwaitAsync(i + 1000).Result; });
	var viaFromResult = Measure(() => { for (var i = 0; i < Calls; i++) _ = FromResult(i + 1000).Result; });
	var viaAwait = await MeasureAsync(async () => { for (var i = 0; i < Calls; i++) _ = await NoAwaitAsync(i + 1000); });

	if (round == 1)
	{
		WriteLine($"  async, no await, read with .Result:  {viaResult / Calls,4} bytes per call");
		WriteLine($"  Task.FromResult, read with .Result:  {viaFromResult / Calls,4} bytes per call");
		WriteLine($"  async, no await, awaited by caller:  {viaAwait / Calls,4} bytes per call");
	}
}

// Expected on 11.0.100:
//                                  state machine   runtime-async=on
//   async + .Result                    104 B            72 B   (same as Task.FromResult)
//   Task.FromResult + .Result           72 B            72 B
//   async, awaited by async caller     104 B             0 B   (no Task created at all)
//
// The last line is the interesting one: when an async method awaits another async method
// that finishes synchronously, runtime async can skip creating the Task entirely.

static async Task<int> NoAwaitAsync(int x) => x * 2;    // `async` with no `await`
static Task<int> FromResult(int x) => Task.FromResult(x * 2);

static long Measure(Action action)
{
	var before = GC.GetAllocatedBytesForCurrentThread();
	action();
	return GC.GetAllocatedBytesForCurrentThread() - before;
}

static async Task<long> MeasureAsync(Func<Task> action)
{
	// Everything completes synchronously, so this stays on one thread and the
	// per-thread allocation counter is accurate.
	var before = GC.GetAllocatedBytesForCurrentThread();
	await action();
	return GC.GetAllocatedBytesForCurrentThread() - before;
}

WriteLine();
WriteLine("##### Part 3: what `async` still changes: where exceptions appear #####");
WriteLine();

// Runtime async does not change this, and it is the real reason to choose deliberately.
// An async method never throws at the call. The exception is stored in the returned Task
// and thrown when someone awaits it.
var task = ThrowsAsync();
WriteLine($"  async version:     call returned a Task, IsFaulted = {task.IsFaulted}");

try
{
	_ = ThrowsWithoutAsync();
}
catch (InvalidOperationException)
{
	WriteLine("  non-async version: threw immediately, at the call site");
}

// That matters for argument validation: callers that start several tasks and await them
// later (Task.WhenAll) see the error at different points depending on which you chose.
static async Task<int> ThrowsAsync() => throw new InvalidOperationException("trap");
static Task<int> ThrowsWithoutAsync() => throw new InvalidOperationException("trap");
