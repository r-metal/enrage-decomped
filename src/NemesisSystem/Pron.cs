using System.Text.RegularExpressions;
using RDR2.Native;

namespace NemesisSystem;

public static class Pron
{
	private static readonly Regex Words = new Regex("\\b(he|him|his|himself|he's|he'll|he'd)\\b", RegexOptions.IgnoreCase);

	public static string Her(string s, bool female)
	{
		if (!female || string.IsNullOrEmpty(s))
		{
			return s;
		}
		return Words.Replace(s, delegate(Match m)
		{
			string value = m.Value;
			string text = value.ToLowerInvariant() switch
			{
				"he'll" => "she'll", 
				"he's" => "she's", 
				"himself" => "herself", 
				"his" => "her", 
				"him" => "her", 
				"he" => "she", 
				_ => "she'd", 
			};
			if (value.ToUpperInvariant() == value)
			{
				return text.ToUpperInvariant();
			}
			return char.IsUpper(value[0]) ? (char.ToUpperInvariant(text[0]) + text.Substring(1)) : text;
		});
	}

	public static bool FemaleAt(int handle)
	{
		if (handle != 0 && ENTITY.DOES_ENTITY_EXIST(handle) && ENTITY.IS_ENTITY_A_PED(handle))
		{
			return !PED.IS_PED_MALE(handle);
		}
		return false;
	}
}
