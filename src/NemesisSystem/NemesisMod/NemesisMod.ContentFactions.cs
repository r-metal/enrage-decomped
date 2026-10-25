using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NemesisSystem.BulletImpact;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RDR2;
using RDR2.Math;
using RDR2.Native;
using RDR2.UI;

namespace NemesisSystem;

public partial class NemesisMod
{
	private int LoadFactions()
	{
		JObject[] built = Gangs.BuiltInExport;
		return LoadOrWrite(Path.Combine(ContentDir, "factions.json"), () => new FactionsContent
		{
			Factions = built.Select((JObject o) => (JObject)o.DeepClone()).ToList()
		}, delegate(FactionsContent c)
		{
			if (c?.Factions == null || c.Factions.Count == 0)
			{
				return 0;
			}
			List<Gang> list = Gangs.Defined.ToList();
			foreach (JObject faction in c.Factions)
			{
				string id = (string?)faction["Id"];
				if (string.IsNullOrWhiteSpace(id) || id == "vdl")
				{
					GrudgeLog("CONTENT factions.json: an entry with no usable Id skipped");
				}
				else
				{
					Gang gang = list.FirstOrDefault((Gang x) => x.Id == id);
					bool num = gang == null;
					if (num)
					{
						gang = new Gang
						{
							Id = id,
							Name = id
						};
					}
					(string, int, int)[] tiers = gang.Tiers;
					(int, int, PedHash[])[] seed = gang.Seed;
					PedHash[] troopModels = gang.TroopModels;
					string text = FactionContent.Apply(gang, faction);
					if (!num)
					{
						if ((gang.Tiers == null || gang.Tiers.Length == 0) && tiers != null)
						{
							gang.Tiers = tiers;
						}
						if (gang.Seed == null && seed != null)
						{
							gang.Seed = seed;
						}
						if (gang.TroopModels == null)
						{
							gang.TroopModels = troopModels ?? new PedHash[0];
						}
					}
					if (text != null)
					{
						GrudgeLog("CONTENT factions.json " + id + ": " + text);
					}
					if (num)
					{
						FactionContent.FillNew(gang);
						list.Add(gang);
						GrudgeLog($"CONTENT factions.json: NEW faction {gang.Name} ({id}) - {gang.Tiers.Sum(((string Label, int MinRank, int Seats) t) => t.Seats)} seats");
					}
				}
			}
			Gangs.SetAll(list.ToArray());
			return list.Count;
		});
	}

	private static PlacesContent ExportPlaces()
	{
		return new PlacesContent
		{
			Districts = BuiltInDistricts.Select((District d) => new DistrictRow
			{
				Key = d.Key,
				Name = d.Name,
				X = d.X,
				Y = d.Y,
				Hashes = d.Hashes.Select(FactionContent.Hex).ToList(),
				Lore = new Dictionary<string, int>(d.Lore1899),
				Lore1907 = ((d.Lore1907 != null) ? new Dictionary<string, int>(d.Lore1907) : null),
				Region = d.Region
			}).ToList(),
			Towns = BuiltInTownSpots.Select(delegate(KeyValuePair<string, (Vector3? Doctor, Vector3? Saloon)> kv)
			{
				TownRow townRow = new TownRow
				{
					Name = kv.Key
				};
				Vector3? item = kv.Value.Doctor;
				object doctor;
				if (item.HasValue)
				{
					Vector3 valueOrDefault = item.GetValueOrDefault();
					doctor = new float[3] { valueOrDefault.X, valueOrDefault.Y, valueOrDefault.Z };
				}
				else
				{
					doctor = null;
				}
				townRow.Doctor = (float[])doctor;
				item = kv.Value.Saloon;
				object saloon;
				if (item.HasValue)
				{
					Vector3 valueOrDefault2 = item.GetValueOrDefault();
					saloon = new float[3] { valueOrDefault2.X, valueOrDefault2.Y, valueOrDefault2.Z };
				}
				else
				{
					saloon = null;
				}
				townRow.Saloon = (float[])saloon;
				townRow.DoctorOpen = (kv.Value.Doctor.HasValue ? new bool?(BuiltInOpenDoctors.Contains(kv.Key)) : ((bool?)null));
				townRow.CoreRadius = (BuiltInTownCore.TryGetValue(kv.Key, out var value) ? value : 110f);
				townRow.Centre = ((!BuiltInTownCentre.TryGetValue(kv.Key, out var value2)) ? null : new float[3] { value2.X, value2.Y, value2.Z });
				return townRow;
			}).ToList()
		};
	}

	private int ImportPlaces(PlacesContent c)
	{
		if (c == null)
		{
			return 0;
		}
		int num = 0;
		if (c.Districts != null && c.Districts.Count > 0)
		{
			List<District> ds = new List<District>();
			foreach (DistrictRow r in c.Districts.Where((DistrictRow districtRow) => !string.IsNullOrWhiteSpace(districtRow.Key)))
			{
				try
				{
					District district = BuiltInDistricts.FirstOrDefault((District b) => b.Key == r.Key);
					ds.Add(new District
					{
						Key = r.Key,
						Name = (r.Name ?? r.Key),
						X = r.X,
						Y = r.Y,
						Hashes = (r.Hashes ?? new List<string>()).Select(FactionContent.Unhex).ToArray(),
						Lore1899 = (r.Lore ?? new Dictionary<string, int>()),
						Lore1907 = (r.Lore1907 ?? district?.Lore1907),
						Region = (r.Region ?? district?.Region)
					});
				}
				catch (Exception ex)
				{
					GrudgeLog("CONTENT places.json district " + r.Key + ": " + ex.Message);
				}
			}
			foreach (District item in BuiltInDistricts.Where((District b) => b.Lore1899.Count == 0 && !ds.Any((District d) => d.Key == b.Key)))
			{
				ds.Add(item);
				GrudgeLog("CONTENT places.json: " + item.Name + " isn't in the file - the built-in one is used");
			}
			if (ds.Count > 0)
			{
				Districts = ds.ToArray();
				num += ds.Count;
			}
		}
		if (c.Towns != null && c.Towns.Count > 0)
		{
			List<TownRow> list = c.Towns.Where((TownRow t) => !string.IsNullOrWhiteSpace(t.Name) && (V(t.Doctor).HasValue || V(t.Saloon).HasValue)).ToList();
			if (list.Count > 0)
			{
				TownSpots.Clear();
				TownCoreRadius.Clear();
				TownCentre.Clear();
				OpenDoctors.Clear();
				foreach (TownRow item2 in list)
				{
					TownSpots[item2.Name] = (V(item2.Doctor), V(item2.Saloon));
					TownCoreRadius[item2.Name] = ((item2.CoreRadius > 0f) ? item2.CoreRadius : 110f);
					if (V(item2.Doctor).HasValue && (item2.DoctorOpen ?? BuiltInOpenDoctors.Contains(item2.Name)))
					{
						OpenDoctors.Add(item2.Name);
					}
					Vector3? vector = V(item2.Centre);
					if (vector.HasValue)
					{
						Vector3 valueOrDefault = vector.GetValueOrDefault();
						TownCentre[item2.Name] = valueOrDefault;
					}
				}
				num += list.Count;
			}
		}
		return num;
		static Vector3? V(float[] a)
		{
			if (a == null || a.Length < 3)
			{
				return null;
			}
			return new Vector3(a[0], a[1], a[2]);
		}
	}

	private static WildcardsContent ExportWildcards()
	{
		return new WildcardsContent
		{
			Weight = 10,
			Kinds = new Dictionary<string, int>(BuiltInTurmoilKinds),
			BadBlood = BuiltInBadBlood.ToList(),
			Deals = BuiltInDeals.ToList()
		};
	}

	private static int ImportWildcards(WildcardsContent c)
	{
		if (c == null)
		{
			return 0;
		}
		TurmoilWeight = Math.Max(0, c.Weight);
		if (c.Kinds != null && c.Kinds.Count > 0)
		{
			TurmoilKinds = c.Kinds.Where((KeyValuePair<string, int> kv) => BuiltInTurmoilKinds.ContainsKey(kv.Key.ToUpperInvariant())).ToDictionary((KeyValuePair<string, int> kv) => kv.Key.ToUpperInvariant(), (KeyValuePair<string, int> kv) => Math.Max(0, kv.Value));
		}
		if (TurmoilKinds.TryGetValue("CRACKDOWN", out var value) && value == 15)
		{
			TurmoilKinds["CRACKDOWN"] = BuiltInTurmoilKinds["CRACKDOWN"];
		}
		if (c.BadBlood != null && c.BadBlood.Count > 0)
		{
			BadBlood = c.BadBlood.ToArray();
		}
		if (c.Deals != null && c.Deals.Count > 0)
		{
			Deals = c.Deals.ToArray();
		}
		return TurmoilKinds.Count + BadBlood.Length + Deals.Length;
	}
}
