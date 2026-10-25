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
	internal static string LeaderCopyId(Gang g)
	{
		if (g != null)
		{
			return "lore_leader_" + g.Id;
		}
		return null;
	}

	internal static bool IsLeaderCopy(NemesisRecord r)
	{
		if (r != null && r.Id != null)
		{
			return r.Id.StartsWith("lore_leader_");
		}
		return false;
	}

	internal static string FamilyNameOf(NemesisRecord r)
	{
		if (r != null && r.Name != null)
		{
			if (!IsLeaderCopy(r))
			{
				return NameBank.Surname(r.Name);
			}
			return r.Name.Substring(r.Name.LastIndexOf(' ') + 1);
		}
		return "";
	}

	private List<NemesisRecord> BenchedLeaders => Chronicle.BenchedLeaders ?? (Chronicle.BenchedLeaders = new List<NemesisRecord>());

	private bool LeaderCopyFits(Gang g, bool making = false)
	{
		if (g != null && !g.StoryOnly && g != Gangs.Army && !g.OrdersNotFeelings && !IsDestroyed(g) && !string.IsNullOrEmpty(g.LoreLeader ?? g.LeaderName))
		{
			if (making)
			{
				return Roster.Any((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && !IsLeaderCopy(n));
			}
			return true;
		}
		return false;
	}

	private void SyncLeaderCopies()
	{
		bool flag = false;
		Gang[] all = Gangs.All;
		foreach (Gang gang in all)
		{
			string id = LeaderCopyId(gang);
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == id);
			if (nemesisRecord != null && RepairLeaderCopy(gang, nemesisRecord))
			{
				flag = true;
			}
			LeaderSeat leaderSeat = SeatOf(gang);
			bool flag2 = leaderSeat == null || leaderSeat.Fallen == null;
			if (nemesisRecord != null && (!nemesisRecord.IsAlive || nemesisRecord.PresumedDead))
			{
				if (nemesisRecord.PresumedDead)
				{
					nemesisRecord.IsAlive = false;
					nemesisRecord.PresumedDead = false;
				}
				if (flag2 && leaderSeat != null && leaderSeat.Successor == nemesisRecord.Id)
				{
					string text = ((nemesisRecord.Fate == "Arrested") ? "arrested" : "killed");
					if (nemesisRecord.SeatFell)
					{
						Seats[gang.Id] = new LeaderSeat
						{
							Fallen = text,
							OldName = nemesisRecord.FullName,
							At = GameMinutesNow()
						};
						GrudgeLog("LEADER " + gang.Name + ": " + nemesisRecord.FullName + "'s fall was already counted (the world file missed it) - the seat is open again, quietly");
					}
					else
					{
						nemesisRecord.SeatFell = true;
						Fall(gang, nemesisRecord.FullName, text, nemesisRecord.KilledByPlayer, nemesisRecord);
					}
					flag = true;
				}
			}
			else if (nemesisRecord != null)
			{
				string text2 = ((!flag2) ? ("his seat fell (" + leaderSeat.Fallen + ")") : ((!Config.FindLeaders) ? "lore leaders locked" : ((!LeaderCopyFits(gang)) ? "his outfit's out of the picture" : null)));
				if (text2 != null)
				{
					if (OffStage(nemesisRecord))
					{
						BenchLeaderCopy(nemesisRecord, text2);
						flag = true;
					}
					continue;
				}
				if (leaderSeat == null)
				{
					leaderSeat = (Seats[gang.Id] = new LeaderSeat());
				}
				if (leaderSeat.Successor != nemesisRecord.Id)
				{
					leaderSeat.Successor = nemesisRecord.Id;
					flag = true;
				}
			}
			else if (Config.FindLeaders && flag2 && LeaderCopyFits(gang, making: true))
			{
				if (MakeLeaderCopy(gang))
				{
					flag = true;
				}
			}
			else if (leaderSeat != null && leaderSeat.Fallen == null && leaderSeat.Successor != null && leaderSeat.Successor.StartsWith("lore_leader_"))
			{
				leaderSeat.Successor = null;
				flag = true;
			}
		}
		if (flag)
		{
			ApplyLeaders();
			SaveRoster();
			SaveWorld();
		}
	}

	private uint LeaderFace(Gang g)
	{
		foreach (string item in from x in (g.LeaderModel ?? "").Split('|')
			select x.Trim() into x
			where x.Length > 0
			select x)
		{
			uint num = MISC.GET_HASH_KEY(item);
			if (STREAMING.IS_MODEL_IN_CDIMAGE(num))
			{
				return num;
			}
		}
		if (g.Seed != null)
		{
			foreach (var item2 in g.Seed.OrderByDescending(((int Rank, int Count, PedHash[] Models) s) => s.Rank))
			{
				PedHash[] array = item2.Models ?? new PedHash[0];
				foreach (PedHash pedHash in array)
				{
					if (!BountyModel(Models.Name((uint)pedHash)))
					{
						return (uint)pedHash;
					}
				}
			}
		}
		if (g.TroopModels == null || g.TroopModels.Length == 0)
		{
			return 424175505u;
		}
		return (uint)g.TroopModels[0];
	}

	private bool MakeLeaderCopy(Gang g)
	{
		string id = LeaderCopyId(g);
		string text = g.LoreLeader ?? g.LeaderName;
		if (Roster.Any((NemesisRecord n) => n.Id == id))
		{
			return false;
		}
		NemesisRecord nemesisRecord = BenchedLeaders.FirstOrDefault((NemesisRecord b) => b?.Id == id);
		if (nemesisRecord != null)
		{
			BenchedLeaders.Remove(nemesisRecord);
			GrudgeLog("LEADER " + text + ": back on the roster (lore leaders unlocked)");
		}
		else
		{
			nemesisRecord = JsonConvert.DeserializeObject<NemesisRecord>(JsonConvert.SerializeObject(LorePage(g)));
			nemesisRecord.Id = id;
			nemesisRecord.Name = text;
			nemesisRecord.Title = "";
			nemesisRecord.Origin = GrudgeOrigin.GangMember;
			nemesisRecord.SeedMember = true;
			nemesisRecord.CardLife = true;
			nemesisRecord.ModelHash = LeaderFace(g);
			nemesisRecord.OutfitHash = 0u;
			nemesisRecord.KinId = null;
			nemesisRecord.KinRelation = null;
			nemesisRecord.PersonalityKnown = true;
			nemesisRecord.Hate = (g.SeedHate.HasValue ? ((g.SeedHate.Value.min + g.SeedHate.Value.max) / 2) : 75);
			GrudgeLog($"LEADER {text}: out in the world (lore leaders unlocked) - {Models.Name(nemesisRecord.ModelHash)}, rank {nemesisRecord.Rank}");
		}
		nemesisRecord.IsAlive = true;
		nemesisRecord.PresumedDead = false;
		nemesisRecord.Rank = 5;
		nemesisRecord.Gang = g.Id;
		nemesisRecord.BossId = null;
		nemesisRecord.CampId = null;
		nemesisRecord.CampKnown = false;
		nemesisRecord.CampUnsure = false;
		Roster.Add(nemesisRecord);
		LeaderSeat leaderSeat = SeatOf(g);
		if (leaderSeat == null)
		{
			leaderSeat = (Seats[g.Id] = new LeaderSeat());
		}
		leaderSeat.Successor = nemesisRecord.Id;
		_nextCampAssign = 0;
		return true;
	}

	private bool RepairLeaderCopy(Gang g, NemesisRecord copy)
	{
		bool result = false;
		if (!copy.CardLife)
		{
			NemesisRecord nemesisRecord = LorePage(g);
			if (nemesisRecord != null)
			{
				copy.Past = ((nemesisRecord.Past == null) ? null : JsonConvert.DeserializeObject<List<PastEntry>>(JsonConvert.SerializeObject(nemesisRecord.Past)));
				copy.PastRolled = nemesisRecord.PastRolled;
				copy.BornYear = nemesisRecord.BornYear;
				copy.Birthplace = nemesisRecord.Birthplace;
				copy.LifeRules = nemesisRecord.LifeRules;
				copy.Personality = nemesisRecord.Personality;
				copy.PersonalityKnown = true;
				copy.KinId = null;
				copy.KinRelation = null;
				copy.CardLife = true;
				result = true;
				GrudgeLog($"LEADER {copy.Name}: his card's life (born {copy.BornYear}, {copy.Personality})");
			}
		}
		uint num = LeaderFace(g);
		if (copy.ModelHash != num && (!copy.IsAlive || OffStage(copy)))
		{
			GrudgeLog("LEADER " + copy.Name + ": his face " + Models.Name(copy.ModelHash) + " -> " + Models.Name(num));
			copy.ModelHash = num;
			copy.OutfitHash = 0u;
			result = true;
		}
		return result;
	}

	private void BenchLeaderCopy(NemesisRecord copy, string why)
	{
		BenchedLeaders.RemoveAll((NemesisRecord b) => b?.Id == copy.Id);
		BenchedLeaders.Add(copy);
		RemoveRecord(copy, "the leader benched - " + why);
		GrudgeLog("LEADER " + copy.Name + ": off the roster (" + why + ") - kept in world.json");
	}

	private void BenchLeaderCopies(string why)
	{
		foreach (NemesisRecord item in Roster.Where((NemesisRecord n) => IsLeaderCopy(n) && n.IsAlive).ToList())
		{
			BenchLeaderCopy(item, why);
		}
	}
}
