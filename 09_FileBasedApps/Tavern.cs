#!/usr/bin/env dotnet
// .NET 11 SDK: file-based apps can span files
// Run (from this folder):
//   dotnet run Tavern.cs
//   dotnet run Tavern.cs -e TAVERN_NAME="The Gilded Griffin"
//
// .NET 10 introduced file-based apps: `dotnet run app.cs` with no .csproj.
// Their limit was one file. .NET 11 adds #:include, so a demo or script can grow
// a second file without growing a project.

// The #! line above (a "shebang") lets Linux and macOS run the file directly
// after chmod +x. NOTE: with several .cs files in play, analyzer CA2266 asks for it
// on the entry-point file, to make clear which file is the program.

// NEW: #:include pulls Menu.cs into this compilation, like a <Compile Include> item.
// It also accepts .dll files, which become references.
#:include Menu.cs

using static System.Console;

// NEW: `dotnet run -e NAME=VALUE` sets an environment variable for this run only,
// with no shell export and no launchSettings.json.
var tavern = Environment.GetEnvironmentVariable("TAVERN_NAME") ?? "The Rusty Flagon";

WriteLine($"Welcome to {tavern}!");
WriteLine();

foreach (var order in (string[])["ale", "stew", "dragon steak"])
{
	// OrderResult is a C# 15 union declared in Menu.cs, so the switch is exhaustive
	var reply = Menu.Order(order) switch
	{
		Dish dish => $"{dish.Price} gp, coming right up",
		SoldOut soldOut => $"sold out until {soldOut.BackOn}",
		NotOnMenu => "we do not serve that here",
	};
	WriteLine($"  {order,-13} {reply}");
}
