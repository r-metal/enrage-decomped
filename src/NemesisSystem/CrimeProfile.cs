using System.Collections.Generic;

namespace NemesisSystem;

internal sealed class CrimeProfile
{
	public bool You;

	public string BannerLabel;

	public string BannerValue;

	public string BannerWord;

	public (int r, int g, int b) BannerCol;

	public string Side;

	public (int r, int g, int b) SideCol;

	public bool SideFilled;

	public int Rep = int.MinValue;

	public string CrimesTitle;

	public string CrimesNote;

	public List<(string when, string text, string badge)> Crimes = new List<(string, string, string)>();

	public List<(string kind, (int r, int g, int b) kindCol, string title, string outcome, (int r, int g, int b) outCol)> Events = new List<(string, (int, int, int), string, string, (int, int, int))>();

	public string EventsNote;

	public string TilesTitle;

	public List<(string num, string label, (int r, int g, int b) col)> Tiles = new List<(string, string, (int, int, int))>();

	public List<string> Dead = new List<string>();

	public int DeadMore;

	public List<(string num, string label, (int r, int g, int b) col)> Witnesses = new List<(string, string, (int, int, int))>();

	public List<(string town, int rep)> Towns = new List<(string, int)>();

	public List<(string name, int value, (int r, int g, int b) col)> Factions = new List<(string, int, (int, int, int))>();
}
