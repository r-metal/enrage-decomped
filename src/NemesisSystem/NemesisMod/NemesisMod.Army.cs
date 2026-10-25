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
	private static readonly PedHash[] Troopers = new PedHash[2]
	{
		PedHash.s_m_y_army_01,
		PedHash.s_m_m_army_01
	};

	private static readonly (string abbr, string title)[] OfficerGrades = new(string, string)[3]
	{
		("Maj.", "Major"),
		("Capt.", "Captain"),
		("Lt.", "Lieutenant")
	};

	private static readonly string[] Prefixes = new string[6] { "Pvt. ", "Sgt. ", "Maj. ", "Capt. ", "Lt. ", "Col. " };

	private static bool IsArmy(NemesisRecord rec)
	{
		return Gangs.Squads(rec);
	}

	private void RefreshArmyNames()
	{
		List<NemesisRecord> army = Roster.Where(delegate(NemesisRecord nemesisRecord)
		{
			Gang gang2 = Gangs.Of(nemesisRecord);
			return gang2 != null && gang2.RankInName && nemesisRecord.Name != null;
		}).ToList();
		if (army.Count == 0)
		{
			return;
		}
		HashSet<string> used = new HashSet<string>();
		Gang[] all = Gangs.All;
		foreach (Gang gang in all)
		{
			string[] array = new string[1] { gang.LoreLeader };
			foreach (string text in array)
			{
				if (!string.IsNullOrWhiteSpace(text) && text.Contains(' '))
				{
					used.Add(text.Substring(text.LastIndexOf(' ') + 1));
				}
			}
		}
		foreach (NemesisRecord n in from x in army
			where x.IsAlive
			orderby x.KilledPlayerAt, x.Id
			select x)
		{
			string text2 = Surname(n);
			if (used.Add(text2))
			{
				continue;
			}
			string text3 = (from l in Gangs.Of(n)?.LastNames ?? NameBank.AmericanLast
				where !used.Contains(l) && !army.Any((NemesisRecord a) => Surname(a) == l)
				orderby Seeds.Stable(n.Id + l)
				select l).FirstOrDefault();
			if (text3 != null)
			{
				n.Name = n.Name.Substring(0, n.Name.Length - text2.Length) + text3;
				used.Add(text3);
				GrudgeLog("NAMING army: two of them were " + text2 + " - this one is " + text3 + " now (kin keep their own surnames)");
			}
		}
		HashSet<string> commanders = new HashSet<string>(from id in Gangs.All.Where((Gang x) => x.RankInName).Select(SuccessorId)
			where id != null
			select id);
		List<NemesisRecord> list = (from nemesisRecord in army
			where nemesisRecord.IsAlive && nemesisRecord.Rank >= 4 && !commanders.Contains(nemesisRecord.Id)
			orderby nemesisRecord.Rank descending, nemesisRecord.Id
			select nemesisRecord).ToList();
		foreach (NemesisRecord item in army)
		{
			int num3 = list.IndexOf(item);
			(string, string) tuple = ((item.IsAlive && commanders.Contains(item.Id)) ? ("Col.", "Colonel") : ((num3 >= 0) ? OfficerGrades[Math.Min(num3, OfficerGrades.Length - 1)] : ((item.Rank >= 4) ? (item.Name.StartsWith("Maj.") ? OfficerGrades[0] : (item.Name.StartsWith("Capt.") ? OfficerGrades[1] : OfficerGrades[2])) : ((item.Rank < 2) ? ("Pvt.", "Private") : ("Sgt.", "Sergeant")))));
			item.Title = "";
			if (item.IsAlive || !item.Name.Contains("."))
			{
				item.Name = tuple.Item1 + " " + Surname(item);
			}
		}
		static string Surname(NemesisRecord nemesisRecord)
		{
			string text4 = nemesisRecord.Name;
			string[] prefixes = Prefixes;
			foreach (string text5 in prefixes)
			{
				if (text4.StartsWith(text5))
				{
					text4 = text4.Substring(text5.Length);
					break;
				}
			}
			if (!text4.Contains(' '))
			{
				return text4;
			}
			return text4.Substring(text4.LastIndexOf(' ') + 1);
		}
	}

	private void SpawnArmyCrew(NemesisRecord rec, Vector3 leaderPos, float heading)
	{
		Gang gang = Gangs.Of(rec) ?? Gangs.Army;
		List<NemesisRecord> source = Roster.Where((NemesisRecord n) => n.Gang == rec.Gang && n != rec && n.CanHunt && !n.PresumedDead && !Resting(n) && !_worldNemeses.ContainsValue(n) && n != ActiveRecord).ToList();
		List<NemesisRecord> list = new List<NemesisRecord>();
		int num;
		if (rec.Rank >= 4)
		{
			num = 3 + _rng.Next(2) + CrewBonus(gang);
		}
		else if (rec.Rank >= 2)
		{
			int num2 = 3 + _rng.Next(2);
			if (_rng.Next(100) < Config.CrewRosterChance)
			{
				list.AddRange((from _ in source
					where _.Rank == 1
					orderby _rng.Next()
					select _).Take(1));
			}
			num = num2 - list.Count + CrewBonus(gang);
		}
		else
		{
			int num3 = 2 + _rng.Next(2);
			NemesisRecord nemesisRecord = null;
			NemesisRecord nemesisRecord2 = null;
			if (_rng.Next(100) < Config.CrewRosterChance)
			{
				nemesisRecord = (from _ in source
					where _.Rank >= 2 && _.Rank <= 3
					orderby _rng.Next()
					select _).FirstOrDefault();
				if (nemesisRecord != null)
				{
					list.Add(nemesisRecord);
				}
				else if ((nemesisRecord2 = (from _ in source
					where _.Rank == 1
					orderby _rng.Next()
					select _).FirstOrDefault()) != null)
				{
					list.Add(nemesisRecord2);
				}
			}
			num = ((nemesisRecord == null) ? 1 : 0) + (num3 - ((nemesisRecord2 != null) ? 1 : 0)) + CrewBonus(gang);
		}
		int num4 = PurseCrewShift(gang);
		if (num4 != 0)
		{
			num = Math.Max(1, num + num4);
			GrudgeLog("CREW " + rec.Name + " (Army): " + ((num4 > 0) ? "the Army is rich - one more trooper" : "the Army is broke - one fewer"));
		}
		int i = 0;
		foreach (NemesisRecord item in list)
		{
			Model model;
			Ped ped = SpawnNemesisPed(item, Slot(), heading, NemesisGroup(), out model, (uint)((gang.TroopModels.Length != 0) ? gang.TroopModels[0] : PedHash.s_m_y_army_01));
			if (!(ped == null))
			{
				_followers.Add(ped);
				_worldNemeses[ped.Handle] = item;
				item.TimesAmbushed++;
				item.Touch();
			}
		}
		for (int num5 = 0; num5 < num; num5++)
		{
			PedHash[] array = ((gang.TroopModels.Length != 0) ? gang.TroopModels : Troopers);
			Ped ped2 = SpawnPed(array[_rng.Next(array.Length)], Slot(), heading);
			if (!(ped2 == null))
			{
				MakeOurs(ped2.Handle);
				ArmTroop(ped2, gang, gang.UniformKit ? new eWeapon?(gang.UniformLong) : ((eWeapon?)null));
				ped2.Accuracy = gang.TroopAccuracy - 5;
				ped2.BlockPermanentEvents = true;
				ped2.RelationshipGroup = _nemesisGroup;
				ApplyFactionStyle(ped2, gang.Id);
				_followers.Add(ped2);
				_followerOf[ped2.Handle] = rec;
			}
		}
		GrudgeLog($"{gang.Id.ToUpperInvariant()} crew for {rec.Name} (rank {rec.Rank}): {list.Count} named, {num} unnamed");
		Vector3 Slot()
		{
			double num6 = (double)heading * Math.PI / 180.0 + Math.PI + ((i % 2 == 0) ? 0.5 : (-0.5));
			float num7 = 2.5f + (float)i * 1.3f;
			i++;
			return Ground(leaderPos + new Vector3((0f - (float)Math.Sin(num6)) * num7, (float)Math.Cos(num6) * num7, 0f));
		}
	}

	private unsafe static int LastDamageBone(int h)
	{
		int result = 0;
		if (!PED.GET_PED_LAST_DAMAGE_BONE(h, &result))
		{
			return 0;
		}
		return result;
	}

	private static bool HeadBone(int b)
	{
		if (b != 21030)
		{
			if (b >= 14283)
			{
				return b <= 14288;
			}
			return false;
		}
		return true;
	}

	private bool InFortWallace(Vector3 p)
	{
		return ZONE._GET_MAP_ZONE_AT_COORDS(p.X, p.Y, p.Z, 11) == 4195085991u;
	}
}
