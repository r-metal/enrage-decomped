using System.Collections.Generic;

namespace NemesisSystem;

internal sealed class Newspaper
{
	public string Name;

	public string Town;

	public string Date;

	public string Banner = "LATEST NEWS";

	public int Day;

	public Article Lead;

	public readonly List<Article> Stories = new List<Article>();

	public readonly List<string> About = new List<string>();

	public readonly List<string> Wanted = new List<string>();

	public readonly List<string> Wars = new List<string>();

	public readonly List<string> Further = new List<string>();

	public readonly List<(string Name, string Of, string When, string Where, string Until)> Funerals = new List<(string, string, string, string, string)>();

	public readonly List<Plan> FuneralPlans = new List<Plan>();

	public int Layout;

	public string Ad;
}
