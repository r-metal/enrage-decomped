using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NemesisSystem;

public class Acquaintance
{
	public long LastScaredMin { get; set; }

	public string Id { get; set; }

	public string Name { get; set; }

	public string Title { get; set; }

	public int LifeRules { get; set; }

	[JsonIgnore]
	public string FullName
	{
		get
		{
			if (!string.IsNullOrEmpty(Title))
			{
				return Name + " " + Title;
			}
			return Name;
		}
	}

	public uint Model { get; set; }

	public string Town { get; set; }

	public bool Met { get; set; }

	public string Temper { get; set; }

	public int Grudge { get; set; }

	public bool GrudgeSettled { get; set; }

	public bool? SawFace { get; set; }

	public int TimesSeen { get; set; }

	public DateTime FirstSeen { get; set; }

	public DateTime LastSeen { get; set; }

	public DateTime LastDealt { get; set; }

	public DateTime LastSpawned { get; set; }

	public bool Dead { get; set; }

	public string Note { get; set; }

	public int Favours { get; set; }

	public int Fear { get; set; }

	public List<string> Witnessed { get; set; }

	public int Like { get; set; }

	public bool Reported { get; set; }

	public bool KeptQuiet { get; set; }

	public List<string> FavourNotes { get; set; }

	public string Affil { get; set; }

	public long LastGameMin { get; set; }

	public bool Lookout { get; set; }

	public int LastGreetDay { get; set; }

	public bool LikeSpread { get; set; }

	public List<string> Circle { get; set; }
}
