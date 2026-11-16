// Pulled in by `#:include Menu.cs` in Tavern.cs.
// No top-level statements here: only the entry-point file may have those.

public record Dish(string Name, int Price);
public record SoldOut(string BackOn);
public record NotOnMenu;

// Three outcomes, and the caller has to handle all of them
public union OrderResult(Dish, SoldOut, NotOnMenu);

public static class Menu
{
	public static OrderResult Order(string item) => item switch
	{
		"ale" => new Dish("Ale", 2),
		"stew" => new SoldOut("Tuesday"),
		_ => new NotOnMenu(),
	};
}
