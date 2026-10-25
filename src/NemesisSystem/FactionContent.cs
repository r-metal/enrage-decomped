using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using RDR2;

namespace NemesisSystem;

internal static class FactionContent
{
	private static readonly JsonSerializer Ser = JsonSerializer.Create(new JsonSerializerSettings
	{
		Converters = { (JsonConverter)new StringEnumConverter() },
		NullValueHandling = NullValueHandling.Ignore
	});

	private static readonly HashSet<string> Special = new HashSet<string>
	{
		"Seed", "Tiers", "Accent", "ExtraSidearms", "ExtraLongGuns", "SeedHate", "OnlyDistricts", "DistrictWeights", "Jobs", "FirstNames",
		"LastNames", "Titles", "ExcludedNames", "LeaderName", "LeaderBlurb", "LoreLeader", "LoreBlurb"
	};

	private static readonly string[] Banks = new string[4] { "FirstNames", "LastNames", "Titles", "ExcludedNames" };

	private static readonly FieldInfo[] Fields = typeof(Gang).GetFields(BindingFlags.Instance | BindingFlags.Public);

	private static readonly Gang Defaults = new Gang();

	private static string BankName(string[] arr)
	{
		if (arr != null)
		{
			return typeof(NameBank).GetFields(BindingFlags.Static | BindingFlags.Public).FirstOrDefault((FieldInfo f) => f.FieldType == typeof(string[]) && f.GetValue(null) == arr)?.Name;
		}
		return null;
	}

	private static string[] Bank(JToken t)
	{
		if (t == null || t.Type == JTokenType.Null)
		{
			return null;
		}
		if (t.Type == JTokenType.String)
		{
			return typeof(NameBank).GetField(t.Value<string>(), BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as string[];
		}
		return t.ToObject<string[]>();
	}

	internal static string Hex(uint h)
	{
		return "0x" + h.ToString("X8");
	}

	internal static uint Unhex(string s)
	{
		s = (s ?? "").Trim();
		if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			return Convert.ToUInt32(s.Substring(2), 16);
		}
		if (!uint.TryParse(s, out var result))
		{
			return Joaat.Of(s);
		}
		return result;
	}

	internal static JObject Export(Gang g)
	{
		JObject jObject = new JObject();
		FieldInfo[] fields = Fields;
		foreach (FieldInfo fieldInfo in fields)
		{
			if (!Special.Contains(fieldInfo.Name))
			{
				object value = fieldInfo.GetValue(g);
				object value2 = fieldInfo.GetValue(Defaults);
				if (fieldInfo.Name == "Id" || fieldInfo.Name == "Name")
				{
					jObject[fieldInfo.Name] = value as string;
				}
				else if (value != null && !Same(value, value2))
				{
					jObject[fieldInfo.Name] = JToken.FromObject(value, Ser);
				}
			}
		}
		jObject["LeaderName"] = g.LoreLeader ?? g.LeaderName;
		jObject["LeaderBlurb"] = g.LoreBlurb ?? g.LeaderBlurb;
		string[] banks = Banks;
		foreach (string text in banks)
		{
			string[] array = (string[])typeof(Gang).GetField(text).GetValue(g);
			if (array != null)
			{
				string text2 = BankName(array);
				jObject[text] = ((text2 != null) ? ((JToken)text2) : JArray.FromObject(array));
			}
		}
		jObject["Accent"] = new JArray(g.Accent.r, g.Accent.g, g.Accent.b);
		if (g.Tiers != null)
		{
			jObject["Tiers"] = new JArray(g.Tiers.Select(((string Label, int MinRank, int Seats) t) => new JObject
			{
				["Label"] = t.Label,
				["MinRank"] = t.MinRank,
				["Seats"] = t.Seats
			}));
		}
		if (g.Seed != null)
		{
			jObject["Seed"] = new JArray(g.Seed.Select(((int Rank, int Count, PedHash[] Models) s) => new JObject
			{
				["Rank"] = s.Rank,
				["Count"] = s.Count,
				["Models"] = JArray.FromObject(s.Models.Select((PedHash m) => m.ToString()))
			}));
		}
		if (g.ExtraSidearms.Length != 0)
		{
			jObject["ExtraSidearms"] = new JArray(g.ExtraSidearms.Select(((eWeapon w, string name, int weight, int minRank) w) => new JObject
			{
				["Weapon"] = w.w.ToString(),
				["Name"] = w.name,
				["Weight"] = w.weight,
				["MinRank"] = w.minRank
			}));
		}
		if (g.ExtraLongGuns.Length != 0)
		{
			jObject["ExtraLongGuns"] = new JArray(g.ExtraLongGuns.Select(((eWeapon w, string name, int weight) w) => new JObject
			{
				["Weapon"] = w.w.ToString(),
				["Name"] = w.name,
				["Weight"] = w.weight
			}));
		}
		if (g.SeedHate.HasValue)
		{
			jObject["SeedHate"] = new JArray(g.SeedHate.Value.min, g.SeedHate.Value.max);
		}
		if (g.OnlyDistricts != null)
		{
			jObject["OnlyDistricts"] = JArray.FromObject(g.OnlyDistricts.Select(Hex));
		}
		if (g.DistrictWeights != null)
		{
			jObject["DistrictWeights"] = JObject.FromObject(g.DistrictWeights.ToDictionary((KeyValuePair<uint, double> kv) => Hex(kv.Key), (KeyValuePair<uint, double> kv) => kv.Value));
		}
		if (g.Jobs != null && g.Jobs.Length != 0)
		{
			jObject["Jobs"] = new JArray(g.Jobs.Select(((string title, string place, int crew) j) => new JObject
			{
				["Title"] = j.title,
				["Place"] = j.place,
				["Crew"] = j.crew
			}));
		}
		return jObject;
	}

	private static bool Same(object a, object b)
	{
		if (a == null || b == null)
		{
			return a == b;
		}
		if (a is Array array && b is Array array2)
		{
			if (array.Length == 0)
			{
				return array2.Length == 0;
			}
			return false;
		}
		if (a is IDictionary dictionary && b is IDictionary dictionary2)
		{
			if (dictionary.Count == 0)
			{
				return dictionary2.Count == 0;
			}
			return false;
		}
		return a.Equals(b);
	}

	internal static string Apply(Gang g, JObject o)
	{
		List<string> bad = new List<string>();
		foreach (JProperty item in o.Properties())
		{
			try
			{
				switch (item.Name)
				{
				case "FirstNames":
				case "Titles":
				case "ExcludedNames":
				case "LastNames":
				{
					string[] array = Bank(item.Value);
					if (array == null && item.Value.Type == JTokenType.String)
					{
						bad.Add($"{item.Name}: no name bank '{item.Value}'");
					}
					else
					{
						typeof(Gang).GetField(item.Name).SetValue(g, array);
					}
					break;
				}
				case "LeaderName":
					g.LeaderName = (g.LoreLeader = item.Value.Value<string>());
					break;
				case "LeaderBlurb":
					g.LeaderBlurb = (g.LoreBlurb = item.Value.Value<string>());
					break;
				case "Accent":
				{
					int[] array2 = item.Value.ToObject<int[]>();
					if (array2.Length >= 3)
					{
						g.Accent = (r: array2[0], g: array2[1], b: array2[2]);
					}
					break;
				}
				case "Tiers":
					g.Tiers = item.Value.Select((JToken t) => ((string?)t["Label"], (int)t["MinRank"], (int)t["Seats"])).ToArray();
					break;
				case "Seed":
					g.Seed = item.Value.Select((JToken s) => ((int)s["Rank"], (int)s["Count"], Models(s["Models"], bad))).ToArray();
					break;
				case "ExtraSidearms":
					g.ExtraSidearms = item.Value.Select((JToken w) => (Weapon(w["Weapon"]), (string?)w["Name"], (int)w["Weight"], ((int?)w["MinRank"]) ?? 1)).ToArray();
					break;
				case "ExtraLongGuns":
					g.ExtraLongGuns = item.Value.Select((JToken w) => (Weapon(w["Weapon"]), (string?)w["Name"], (int)w["Weight"])).ToArray();
					break;
				case "SeedHate":
				{
					int[] array3 = item.Value.ToObject<int[]>();
					g.SeedHate = ((array3.Length >= 2) ? new(int, int)?((array3[0], array3[1])) : (((int, int)?)null));
					break;
				}
				case "OnlyDistricts":
					g.OnlyDistricts = ((item.Value.Type == JTokenType.Null) ? null : new HashSet<uint>(item.Value.Select((JToken t) => Unhex((string?)t))));
					break;
				case "DistrictWeights":
					g.DistrictWeights = ((item.Value.Type == JTokenType.Null) ? null : ((JObject)item.Value).Properties().ToDictionary((JProperty x) => Unhex(x.Name), (JProperty x) => (double)x.Value));
					break;
				case "Jobs":
					g.Jobs = item.Value.Select((JToken j) => ((string?)j["Title"], ((string?)j["Place"]) ?? "the road by {t}", ((int?)j["Crew"]) ?? 3)).ToArray();
					break;
				case "TroopModels":
					g.TroopModels = Models(item.Value, bad);
					break;
				default:
				{
					FieldInfo field = typeof(Gang).GetField(item.Name, BindingFlags.Instance | BindingFlags.Public);
					if (field == null)
					{
						bad.Add("unknown field '" + item.Name + "'");
					}
					else
					{
						field.SetValue(g, (item.Value.Type == JTokenType.Null) ? null : item.Value.ToObject(field.FieldType, Ser));
					}
					break;
				}
				}
			}
			catch (Exception ex)
			{
				bad.Add(item.Name + ": " + ex.Message);
			}
		}
		if (bad.Count != 0)
		{
			return string.Join("; ", bad);
		}
		return null;
	}

	private static PedHash[] Models(JToken t, List<string> bad)
	{
		List<PedHash> list = new List<PedHash>();
		foreach (JToken item in (IEnumerable<JToken>)(t ?? new JArray()))
		{
			string text = (string?)item;
			if (Enum.TryParse<PedHash>(text, ignoreCase: true, out var result))
			{
				list.Add(result);
			}
			else
			{
				bad.Add("no ped model '" + text + "'");
			}
		}
		return list.ToArray();
	}

	private static eWeapon Weapon(JToken t)
	{
		return (eWeapon)Enum.Parse(typeof(eWeapon), (string?)t, ignoreCase: true);
	}

	internal static void FillNew(Gang g)
	{
		g.ModelTokens = g.ModelTokens ?? new string[0];
		g.FirstNames = g.FirstNames ?? NameBank.AmericanFirst;
		g.LastNames = g.LastNames ?? NameBank.AmericanLast;
		g.Titles = g.Titles ?? NameBank.AmericanTitles;
		g.LeaderName = g.LeaderName ?? ("the head of the " + g.Name);
		g.LoreLeader = g.LoreLeader ?? g.LeaderName;
		g.LeaderBlurb = g.LeaderBlurb ?? "";
		g.LoreBlurb = g.LoreBlurb ?? g.LeaderBlurb;
		if (g.Tiers == null || g.Tiers.Length == 0)
		{
			g.Tiers = new(string, int, int)[3]
			{
				("OFFICERS", 4, 2),
				("HANDS", 2, 4),
				("MEN", 1, 8)
			};
		}
		if (g.Seed == null || g.Seed.Length == 0)
		{
			PedHash[] m = ((g.TroopModels != null && g.TroopModels.Length != 0) ? g.TroopModels : new PedHash[1] { PedHash.g_m_m_uniduster_01 });
			g.Seed = g.Tiers.Select(((string Label, int MinRank, int Seats) t) => (MinRank: t.MinRank, Seats: t.Seats, m: m)).ToArray();
		}
	}
}
