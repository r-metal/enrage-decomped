using System;

namespace NemesisSystem;

internal sealed class CampSite
{
	public readonly string Id;

	public readonly uint Imap;

	public readonly float X;

	public readonly float Y;

	public readonly float Z;

	public readonly string Region;

	public readonly bool Hideout;

	public readonly string Name;

	public readonly string Gang;

	public static Func<CampSite, string> OwnerOf;

	public string Label => Name ?? ((Gangs.ById(OwnerOf?.Invoke(this) ?? Gang)?.Name ?? "Outlaw") + " camp in " + Region);

	public CampSite(string id, uint imap, float x, float y, float z, string region, bool hideout = false, string name = null, string gang = "odriscoll")
	{
		Id = id;
		Imap = imap;
		X = x;
		Y = y;
		Z = z;
		Region = region;
		Hideout = hideout;
		Name = name;
		Gang = gang;
	}
}
