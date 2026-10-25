using System.Collections.Generic;
using RDR2;
using RDR2.Native;

namespace NemesisSystem;

internal static class Kids
{
	public static readonly HashSet<int> StoryProtected = new HashSet<int>();

	private static readonly Dictionary<uint, bool> ByModel = new Dictionary<uint, bool>();

	public static bool Protected(Ped p)
	{
		if (p != null)
		{
			return StoryProtected.Contains(p.Handle);
		}
		return false;
	}

	public static bool IsChild(Ped p)
	{
		if (p == null || !p.Exists())
		{
			return false;
		}
		if (PED._IS_PED_CHILD(p.Handle))
		{
			return true;
		}
		uint hash = (uint)p.Model.Hash;
		if (ByModel.TryGetValue(hash, out var value))
		{
			return value;
		}
		value = false;
		string text = Models.Name(hash);
		value = text.Contains("kid") || text.Contains("child");
		ByModel[hash] = value;
		return value;
	}
}
