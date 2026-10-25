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
	private readonly HashSet<int> _missionPedsLogged = new HashSet<int>();

	private int _nextMissionPedScan;

	private static readonly string[] MistakenNemeses = new string[1] { "7c5ae91d316c" };

	private int _nextLoreScan;

	private const int LeaderNotoriety = 40;

	internal const string WoffordDead = "killed by you on his bounty";

	internal string LoreNameAt(string modelLower, Vector3 at)
	{
		Gang[] all = Gangs.All;
		foreach (Gang gang in all)
		{
			if (!string.IsNullOrEmpty(gang.LeaderModel) && gang.LeaderModel.ToLowerInvariant().Split('|').Contains(modelLower) && AtLeaderSpot(gang, at))
			{
				return gang.LoreLeader ?? gang.LeaderName;
			}
		}
		return Roster.FirstOrDefault((NemesisRecord n) => n.LoreModel != null && n.LoreModel.ToLowerInvariant() == modelLower)?.FullName;
	}

	private void NoteLoreModels()
	{
		Gang[] all = Gangs.All;
		foreach (Gang gang in all)
		{
			if (string.IsNullOrEmpty(gang.LeaderModel))
			{
				continue;
			}
			string[] array = gang.LeaderModel.ToLowerInvariant().Split('|');
			foreach (string text in array)
			{
				if (text.StartsWith("u_"))
				{
					LoreModels.Add(text.Trim());
				}
			}
		}
		foreach (NemesisRecord item in Roster)
		{
			if (item.LoreModel != null)
			{
				LoreModels.Add(item.LoreModel.ToLowerInvariant());
			}
		}
	}

	private void LeaderWatch(Gang g, Ped him, Ped player)
	{
		string text = ((him == null) ? "not around" : (him.IsDead ? (LooksKnockedOut(him) ? "knocked out" : "dead") : (ENTITY.IS_ENTITY_ATTACHED_TO_ENTITY(him.Handle, player.Handle) ? "carried by you" : (ENTITY.IS_ENTITY_ATTACHED(him.Handle) ? "on a horse / carried" : ((!him.IsHogtied) ? "free" : "hogtied")))));
		_leaderState.TryGetValue(g.Id, out var value);
		if (!(text == value))
		{
			_leaderState[g.Id] = text;
			if (value != null || !(text == "not around"))
			{
				string text2 = ((him != null) ? $" - {him.Position.DistanceTo(player.Position):0} m from you, model {Models.Name((uint)him.Model.Hash)}, ped {him.Handle}" : "");
				GrudgeLog("LEADER " + (g.LoreLeader ?? g.LeaderName) + " (" + g.Name + "): " + (value ?? "first seen") + " -> " + text + text2 + (MISC.GET_MISSION_FLAG() ? " [mission running]" : ""));
			}
		}
	}

	private void LogMissionPeds(Ped player)
	{
		if (Game.GameTime < _nextMissionPedScan)
		{
			return;
		}
		_nextMissionPedScan = Game.GameTime + 2000;
		if (!MISC.GET_MISSION_FLAG())
		{
			if (_missionPedsLogged.Count > 0)
			{
				_missionPedsLogged.Clear();
			}
		}
		else
		{
			if (_missionPedsLogged.Count > 60)
			{
				return;
			}
			foreach (Ped item in PedsAround(player))
			{
				if (!(item == null) && item.Exists() && !item.IsPlayer && item.IsHuman && !_missionPedsLogged.Contains(item.Handle) && ENTITY.IS_ENTITY_A_MISSION_ENTITY(item.Handle) && !BelongsToUs(item.Handle))
				{
					float num = item.Position.DistanceTo(player.Position);
					if (!(num > 80f))
					{
						_missionPedsLogged.Add(item.Handle);
						GrudgeLog(string.Format("MISSION ped {0} (ped {1}) {2:0} m - {3}", Models.Name((uint)item.Model.Hash), item.Handle, num, item.IsDead ? "dead" : (item.IsHogtied ? "hogtied" : "alive")));
					}
				}
			}
		}
	}

	private void EnsureLoreMen()
	{
		Gang raiders = Gangs.LemoyneRaiders;
		if (Roster.Any((NemesisRecord n) => n.Gang == raiders.Id) && !Roster.Any((NemesisRecord n) => n.Id == "lore_millemont"))
		{
			NemesisRecord nemesisRecord = NewRecord(3939581308u, "Scarlett Meadows", raiders, 4);
			nemesisRecord.Id = "lore_millemont";
			nemesisRecord.Name = "Camille de Millemont";
			nemesisRecord.Title = "";
			nemesisRecord.LoreModel = "u_m_m_uniexconfedsbounty_01";
			nemesisRecord.Gang = raiders.Id;
			nemesisRecord.Rank = 4;
			nemesisRecord.Origin = GrudgeOrigin.GangMember;
			nemesisRecord.SeedMember = true;
			nemesisRecord.Personality = Personality.Proud;
			nemesisRecord.Hate = 72;
			nemesisRecord.KillLocation = "Rhodes";
			nemesisRecord.Past = nemesisRecord.Past ?? new List<PastEntry>();
			nemesisRecord.Past.Add(new PastEntry
			{
				Year = 1898,
				Month = 0,
				Kind = "crime",
				Text = "Robbed the mail and killed a postal carrier - he calls it rebellion",
				Public = true,
				Known = true
			});
			nemesisRecord.Past.Add(new PastEntry
			{
				Year = 1899,
				Month = 0,
				Kind = "crime",
				Text = "Wanted in Rhodes: $55, dead or alive - theft of state property, crimes against officers of the state",
				Public = true,
				Known = true
			});
			Roster.Add(nemesisRecord);
			SaveRoster();
			GrudgeLog("LEADER added Camille de Millemont (lore) to the Lemoyne Raiders as a captain");
		}
	}

	private void RetireMistakenNemeses()
	{
		foreach (NemesisRecord item in Roster.Where((NemesisRecord x) => x.IsAlive && x.Id != null && MistakenNemeses.Contains(x.Id)).ToList())
		{
			GrudgeLog("ROSTER " + item.Name + ": made a nemesis by mistake - he quits the life");
			QuitTheLife(item, GameMinutesNow());
		}
	}

	internal static bool BountyModel(string m)
	{
		if (!string.IsNullOrEmpty(m))
		{
			if (!m.Contains("_bht_"))
			{
				if (m.Contains("bounty"))
				{
					return !m.Contains("bountyhunter");
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private void BountyModelsStayInTheirMissions()
	{
		int num = 0;
		try
		{
			foreach (NemesisRecord r in Roster.Where((NemesisRecord x) => x.IsAlive && x.Gang != null && (x.SeedMember || (x.Id ?? "").StartsWith("lore_")) && !IsLeaderCopy(x) && BountyModel(Models.Name(x.ModelHash))).ToList())
			{
				Gang gang = Gangs.ById(r.Gang);
				uint num2 = 0u;
				if (gang?.Seed != null)
				{
					foreach (var item in gang.Seed.OrderBy(((int Rank, int Count, PedHash[] Models) s) => Math.Abs(s.Rank - r.Rank)))
					{
						PedHash[] array = item.Models ?? new PedHash[0];
						foreach (PedHash pedHash in array)
						{
							if (!BountyModel(Models.Name((uint)pedHash)))
							{
								num2 = (uint)pedHash;
								break;
							}
						}
						if (num2 != 0)
						{
							break;
						}
					}
				}
				if (num2 == 0 && gang?.TroopModels != null && gang.TroopModels.Length != 0)
				{
					num2 = (uint)gang.TroopModels[0];
				}
				if (num2 != 0)
				{
					GrudgeLog("MODEL " + r.Name + ": " + Models.Name(r.ModelHash) + " is a bounty mission's - now " + Models.Name(num2) + ", new clothes");
					r.ModelHash = num2;
					r.OutfitHash = 0u;
					num++;
				}
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("MODEL bounty-model check failed: " + ex.Message);
		}
		if (num > 0)
		{
			SaveRoster();
		}
	}

	private void LoreMenSeen(Ped player)
	{
		if (!Chronicle.MillemontFix)
		{
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == "lore_millemont");
			if (nemesisRecord != null && !nemesisRecord.IsAlive && nemesisRecord.History != null && nemesisRecord.History.Any((HistoryEntry h) => h.Text != null && h.Text.Contains("bounty on him was settled")) && nemesisRecord.TimesCheatedDeath > 0)
			{
				Chronicle.MillemontFix = true;
				nemesisRecord.IsAlive = true;
				nemesisRecord.PresumedDead = true;
				nemesisRecord.History.RemoveAll((HistoryEntry h) => h.Text != null && h.Text.Contains("bounty on him was settled"));
				GrudgeLog("LEADER Camille de Millemont brought back - his ped from a mod skirmish isn't the game's bounty man (presumed dead, as his roll said)");
				SaveRoster();
				SaveWorld();
				goto IL_0103;
			}
		}
		if (!Chronicle.MillemontFix)
		{
			Chronicle.MillemontFix = true;
		}
		goto IL_0103;
		IL_0103:
		if (Game.GameTime < _nextLoreScan)
		{
			return;
		}
		_nextLoreScan = Game.GameTime + 1000;
		foreach (NemesisRecord item in Roster.Where((NemesisRecord n) => n.LoreModel != null && n.IsAlive))
		{
			uint model = MISC.GET_HASH_KEY(item.LoreModel);
			Ped ped = PedsAround(player).FirstOrDefault((Ped p) => p != null && p.Exists() && p.Model.Hash == (int)model && !IsOurs(p.Handle) && !WasOurs.Contains(p.Handle) && !KnownBody(p.Handle, model) && !Gangs.All.Any((Gang g) => g.LeaderSpot != null && AtLeaderSpot(g, p.Position)));
			if (!(ped == null) && ped.IsDead && !LooksKnockedOut(ped))
			{
				item.IsAlive = false;
				item.KilledByPlayer = ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(ped.Handle, player.Handle, p2: true, p3: true);
				History(item, "DIED - the game's own bounty on him was settled", Theme.Blood);
				Feed("LEADER " + item.FullName + " is dead (the Rhodes bounty)", Theme.Blood, item);
				DeathNotice(item.FullName + " is dead.");
				GrudgeLog("LEADER " + item.FullName + " (lore man) died - seen as the game's " + item.LoreModel);
				SaveRoster();
			}
		}
	}

	private void TrackNamedPeds()
	{
		foreach (KeyValuePair<int, Provocation> item in _provoked.ToList())
		{
			NemesisRecord nemesisRecord = item.Value?.Record;
			if (nemesisRecord != null && nemesisRecord.IsAlive && !_worldNemeses.ContainsKey(item.Key) && Roster.Contains(nemesisRecord) && (!(_activeNemesisPed != null) || item.Key != _activeNemesisPed.Handle) && ENTITY.DOES_ENTITY_EXIST(item.Key) && ENTITY.IS_ENTITY_A_PED(item.Key) && !_worldNemeses.ContainsValue(nemesisRecord))
			{
				_worldNemeses[item.Key] = nemesisRecord;
				GrudgeLog($"TRACK ped {item.Key} is {nemesisRecord.FullName} now - his death counts as his");
			}
		}
		if (_interro?.Ped != null && _interro.Rec != null && Roster.Contains(_interro.Rec) && _interro.Rec.IsAlive && !_worldNemeses.ContainsKey(_interro.Ped.Handle) && !_worldNemeses.ContainsValue(_interro.Rec) && _interro.Ped.Exists() && (_activeNemesisPed == null || _interro.Ped.Handle != _activeNemesisPed.Handle))
		{
			_worldNemeses[_interro.Ped.Handle] = _interro.Rec;
			GrudgeLog($"TRACK questioned ped {_interro.Ped.Handle} is {_interro.Rec.FullName} - his death counts as his");
		}
	}
}
