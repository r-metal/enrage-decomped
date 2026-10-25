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
	private int _fairUntil;

	private string _fairWho;

	internal int EncounterIntervalMs()
	{
		int num = Gangs.All.Max((Gang g) => Notoriety.Level(NotorietyOf(g)));
		return (int)((double)(Config.AmbushIntervalSeconds * 1000) * (1.0 - 0.08 * (double)num));
	}

	internal static void ApplyFactionStyle(Ped ped, string gangId)
	{
		if (ped == null || !ped.Exists() || gangId == null)
		{
			return;
		}
		Gang gang = Gangs.ById(gangId);
		if (gang != null)
		{
			if (gang.CombatMovement.HasValue)
			{
				PED.SET_PED_COMBAT_MOVEMENT(ped.Handle, gang.CombatMovement.Value);
			}
			if (gang.CombatAbility.HasValue)
			{
				PED.SET_PED_COMBAT_ABILITY(ped.Handle, gang.CombatAbility.Value);
			}
			if (gang.CombatRange.HasValue)
			{
				PED.SET_PED_COMBAT_RANGE(ped.Handle, gang.CombatRange.Value);
			}
			if (gang.MayFlee)
			{
				PED.SET_PED_COMBAT_ATTRIBUTES(ped.Handle, 58, enabled: false);
			}
			if (gang.AccuracyMin > 0)
			{
				ped.Accuracy = Math.Max(ped.Accuracy, gang.AccuracyMin);
			}
			if (gang.AccuracyMax < 100)
			{
				ped.Accuracy = Math.Min(ped.Accuracy, gang.AccuracyMax);
			}
		}
	}

	private void RaiderInfighting()
	{
		Gang g = Gangs.LemoyneRaiders;
		if (_rng.Next(100) >= Balance.RaiderShakeupChance)
		{
			return;
		}
		string succ = SuccessorId(g);
		List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && !n.PresumedDead && !_worldNemeses.ContainsValue(n) && n != ActiveRecord && n.Id != succ && !(n.Id?.StartsWith("lore_") ?? false)).ToList();
		if (list.Count < 3)
		{
			return;
		}
		NemesisRecord upstart = (from _ in list
			where _.Rank < 4
			orderby _rng.Next()
			select _).FirstOrDefault();
		NemesisRecord nemesisRecord = ((upstart == null) ? null : (from n in list
			where n.Rank > upstart.Rank
			orderby n.Rank
			select n).ThenBy((NemesisRecord _) => _rng.Next()).FirstOrDefault());
		if (upstart != null && nemesisRecord != null)
		{
			int num = _rng.Next(100);
			WorldEvent worldEvent = NewEvent("FEUD", GameMinutesNow());
			worldEvent.Gangs.Add(g.Id);
			worldEvent.Place = "in the Raiders' camp";
			WorldPart worldPart = new WorldPart
			{
				Id = upstart.Id,
				Name = upstart.Name,
				Gang = g.Id
			};
			WorldPart worldPart2 = new WorldPart
			{
				Id = nemesisRecord.Id,
				Name = nemesisRecord.Name,
				Gang = g.Id
			};
			worldEvent.People.Add(worldPart);
			worldEvent.People.Add(worldPart2);
			if (num < 45)
			{
				int rank = nemesisRecord.Rank;
				worldEvent.Title = upstart.Name + " shot " + nemesisRecord.Name + " for his place";
				OffscreenDeath(nemesisRecord, worldEvent, worldPart2, upstart.Name, gun: true);
				upstart.Rank = rank;
				worldPart.Outcome = "PROMOTED";
				worldPart.Note = "took his place as " + What(upstart);
				History(upstart, "SHOT " + nemesisRecord.Name.ToUpperInvariant() + " and took his place as " + What(upstart), g.Accent);
				GrudgeNotice("Lemoyne Raiders: " + upstart.FullName + " shot " + nemesisRecord.Name + " and took his place as " + What(upstart) + ".");
			}
			else if (num < 80)
			{
				NemesisRecord nemesisRecord2 = upstart;
				NemesisRecord nemesisRecord3 = nemesisRecord;
				int rank2 = nemesisRecord.Rank;
				int rank3 = upstart.Rank;
				nemesisRecord2.Rank = rank2;
				nemesisRecord3.Rank = rank3;
				worldEvent.Title = upstart.Name + " pushed " + nemesisRecord.Name + " aside";
				worldPart.Outcome = "PROMOTED";
				worldPart.Note = What(upstart) + " now";
				worldPart2.Outcome = "DEMOTED";
				worldPart2.Note = What(nemesisRecord) + " now";
				History(upstart, "PUSHED " + nemesisRecord.Name.ToUpperInvariant() + " ASIDE - " + What(upstart) + " now", g.Accent);
				History(nemesisRecord, "PUSHED ASIDE by " + upstart.Name + " - " + What(nemesisRecord) + " now", Theme.Warn);
				Remember(nemesisRecord, "Shame", 60, "pushed aside by " + upstart.Name, upstart.Id);
				GrudgeNotice("Lemoyne Raiders: " + upstart.FullName + " pushed " + nemesisRecord.Name + " aside - he's " + What(upstart) + " now.");
			}
			else
			{
				worldEvent.Title = upstart.Name + " drew on " + nemesisRecord.Name + " and lost";
				OffscreenDeath(upstart, worldEvent, worldPart, nemesisRecord.Name, gun: true);
				worldPart2.Outcome = "WON";
				worldPart2.Note = "put down an upstart";
				GrudgeNotice("Lemoyne Raiders: " + upstart.FullName + " drew on " + nemesisRecord.Name + ". He lost.");
			}
			worldEvent.Heard = WordReachesYou(worldEvent, Game.Player.Character);
			Chronicle.Events.Add(worldEvent);
			SaveWorld();
			GrudgeLog($"RAIDERS shakeup roll={num} upstart={upstart.Name} above={nemesisRecord.Name}");
			SaveRoster();
		}
		static string What(NemesisRecord r)
		{
			if (r.Rank < 4)
			{
				if (r.Rank < 2)
				{
					return "Raider";
				}
				return "Sergeant";
			}
			return "Captain";
		}
	}

	private unsafe bool DrewOnYou(int h)
	{
		uint num = 0u;
		if (!WEAPON.GET_CURRENT_PED_WEAPON(h, &num, p2: true, 0, p4: true) || num == 0)
		{
			return false;
		}
		uint num2 = WEAPON.GET_WEAPONTYPE_GROUP(num);
		if (num2 != 2685387236u)
		{
			return num2 != 308416707;
		}
		return false;
	}

	private void NoteFairKill(Gang g, Ped victim)
	{
		if (g == null || victim == null || g.OrdersNotFeelings || g.StoryOnly || g == Gangs.Wapiti)
		{
			return;
		}
		int handle = victim.Handle;
		OurRole ourRole = RoleOf(handle);
		if (ourRole != OurRole.Camp && ourRole != OurRole.Active && ourRole != OurRole.Crew && ourRole != OurRole.HiredGun && ourRole != OurRole.Patrol && ourRole != OurRole.Skirmish && ourRole != OurRole.Hunter && ourRole != OurRole.Rival && (!_combatants.TryGetValue(handle, out var value) || Game.GameTime - value >= 15000) && !CameAtYou(handle))
		{
			GrudgeLog(_outlawSwitched.ContainsKey(handle) ? ("FAIRGAME " + AttackerLabel(handle) + " killed - not fighting you, but an outlaw to the game (its " + GroupName(_outlawSwitched[handle].game) + "): no crime") : ("FAIRGAME " + AttackerLabel(handle) + " killed - he wasn't fighting you: the law may count it"));
			return;
		}
		bool num = Game.GameTime > _fairUntil;
		OpenFair(g, kill: true);
		if (num)
		{
			GrudgeLog("FAIRGAME your kill of " + AttackerLabel(handle) + " (" + ShortOutfit(g) + ") - a fight with outlaws: its disturbance crimes wait until it's over");
		}
	}

	private static bool FieldRole(OurRole role)
	{
		if (role != OurRole.Camp && role != OurRole.Active && role != OurRole.Crew && role != OurRole.HiredGun && role != OurRole.Patrol && role != OurRole.Skirmish && role != OurRole.Hunter)
		{
			return role == OurRole.Rival;
		}
		return true;
	}

	private static bool FairOutfit(Gang g)
	{
		if (g != null && !g.OrdersNotFeelings && !g.StoryOnly)
		{
			return g != Gangs.Wapiti;
		}
		return false;
	}
}
