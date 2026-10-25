using System.Collections.Generic;

namespace NemesisSystem;

public class PlacesContent
{
	public string About { get; set; } = "Districts: Key, Name, X / Y (its middle), Hashes (the zone hashes the game gives for it, hex), Lore (faction id -> starting hold 0-100; a faction's encounter weight there = hold / 18). Towns: Name (the game's zone name), Doctor / Saloon [x, y, z] (either may be missing), DoctorOpen (true: the doctor's office is open in the game, so a laid-up man can be found there), CoreRadius (the busy part, m).";

	public List<DistrictRow> Districts { get; set; } = new List<DistrictRow>();

	public List<TownRow> Towns { get; set; } = new List<TownRow>();
}
