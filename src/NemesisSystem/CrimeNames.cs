using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

internal static class CrimeNames
{
	private static readonly (string id, string name)[] Known = new(string, string)[40]
	{
		("MURDER", "murder"),
		("ASSAULT", "assault"),
		("UNARMED_ASSAULT", "unarmed assault"),
		("THREATEN", "threatening"),
		("KIDNAPPING", "kidnapping"),
		("DISTURBANCE", "disturbing the peace"),
		("LOOTING", "looting"),
		("THEFT", "theft"),
		("HORSE_THEFT", "horse theft"),
		("LIVESTOCK_THEFT", "livestock theft"),
		("STOLEN_GOODS", "stolen goods"),
		("ROBBERY", "robbery"),
		("BANK_ROBBERY", "bank robbery"),
		("TRAIN_ROBBERY", "train robbery"),
		("STAGECOACH_ROBBERY", "stagecoach robbery"),
		("BURGLARY", "burglary"),
		("TRESPASSING", "trespassing"),
		("GRAVE_ROBBERY", "grave robbing"),
		("ARSON", "arson"),
		("EXPLOSION", "an explosion"),
		("PROPERTY_DESTRUCTION", "property destruction"),
		("VANDALISM", "vandalism"),
		("VEHICLE_DESTRUCTION", "wrecking a vehicle"),
		("HIJACK_VEHICLE", "hijacking"),
		("JAIL_BREAK", "a jail break"),
		("LASSO_ASSAULT", "lasso assault"),
		("HOGTIE", "hogtying"),
		("INTIMIDATION", "intimidation"),
		("ACCOMPLICE", "an accomplice"),
		("CHEATING", "cheating"),
		("ASSAULT_LAW", "assaulting a lawman"),
		("MURDER_LAW", "murdering a lawman"),
		("THREATEN_LAW", "threatening a lawman"),
		("ASSAULT_HORSE", "assaulting a horse"),
		("MURDER_HORSE", "killing a horse"),
		("ASSAULT_LIVESTOCK", "assaulting livestock"),
		("MURDER_LIVESTOCK", "killing livestock"),
		("ASSAULT_ANIMAL", "assaulting an animal"),
		("MURDER_ANIMAL", "killing an animal"),
		("ASSAULT_CORPSE", "desecrating a body")
	};

	private static Dictionary<uint, string> _byHash;

	internal static uint Joaat(string s)
	{
		uint num = 0u;
		string text = s.ToLowerInvariant();
		foreach (char c in text)
		{
			num += (byte)c;
			num += num << 10;
			num ^= num >> 6;
		}
		num += num << 3;
		num ^= num >> 11;
		return num + (num << 15);
	}

	internal static string Name(uint hash)
	{
		if (hash == 0)
		{
			return null;
		}
		if (_byHash == null)
		{
			_byHash = Known.ToDictionary(((string id, string name) k) => Joaat("CRIME_" + k.id), ((string id, string name) k) => k.name);
		}
		if (!_byHash.TryGetValue(hash, out var value))
		{
			return $"0x{hash:X8}";
		}
		return value;
	}
}
