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
	private struct ScarOption
	{
		public readonly string Pack;

		public readonly string Description;

		public ScarOption(string pack, string description)
		{
			Pack = pack;
			Description = description;
		}
	}

	private const ulong PedWasKilledByHeadshot = 502877896016690276uL;

	private static readonly ScarOption[] BladeScars = new ScarOption[2]
	{
		new ScarOption("PD_Right_cut_cheek", "a knife scar across his cheek"),
		new ScarOption("PD_ODR6_Duster_Slash_Belly", "a stitched slash across his gut")
	};

	private static readonly ScarOption[] BeatingScars = new ScarOption[2]
	{
		new ScarOption("PD_Right_bruise_eye_A", "an eye that never healed right"),
		new ScarOption("PD_Nose_cut", "a nose broken crooked")
	};

	private static readonly ScarOption[] ClawScars = new ScarOption[1]
	{
		new ScarOption("PD_Animal_attack_left_face1_Enemy", "claw marks down his face")
	};

	private static readonly ScarOption[] ArrowScars = new ScarOption[1]
	{
		new ScarOption("PD_Victim_Dead_Arrow_Wounds1", "puckered arrowhead scars")
	};

	private static readonly ScarOption[] BurnScars = new ScarOption[1]
	{
		new ScarOption("PD_ODR3_Arthur_Wound_Shoulder_L_Cauterized", "burn scars")
	};

	private static readonly ScarOption[] NeckScars = new ScarOption[1]
	{
		new ScarOption("PD_Ped_Throat_slash", "a ragged scar across his throat")
	};

	private static readonly ScarOption[] GunshotScars = new ScarOption[2]
	{
		new ScarOption("PD_ODR3_Arthur_Wound_Shoulder_L", "a bullet scar in his shoulder"),
		new ScarOption("PD_Right_cut_cheek", "a bullet graze across his cheek")
	};

	private readonly Dictionary<int, NemesisRecord> _nemesisBodies = new Dictionary<int, NemesisRecord>();

	private const int HeldKillMs = 30000;

	private readonly Dictionary<int, int> _heldAt = new Dictionary<int, int>();

	private static readonly HashSet<uint> HandGroups = new HashSet<uint> { 416676503u, 3193669993u, 3700405225u, 970310034u, 3082541095u, 860033945u, 3053283277u, 2725924767u, 3566412244u, 1548507267u };

	private static readonly HashSet<string> FinalWounds = new HashSet<string> { "brain", "heart", "cervical" };

	private static readonly HashSet<string> LegWounds = new HashSet<string> { "femur", "knee", "tibia", "foot", "pelvis", "spine" };

	private void NoteHeld(Ped p)
	{
		if (p == null || !p.Exists() || p.IsDead)
		{
			return;
		}
		int handle = p.Handle;
		int handle2 = Game.Player.Character.Handle;
		bool flag = !PED.IS_PED_ON_MOUNT(handle) && !PED.IS_PED_IN_ANY_VEHICLE(handle, atGetIn: false) && (ENTITY.IS_ENTITY_ATTACHED_TO_ENTITY(handle, handle2) || PED._GET_CARRIER_AS_PED(handle) == handle2 || PED._GET_CARRIER_AS_MOUNT(handle) != 0);
		if ((p.IsHogtied || flag) && !LawTook(handle) && !UnderArrest(handle))
		{
			_heldAt[handle] = Game.GameTime;
		}
		if (_heldAt.Count <= 100)
		{
			return;
		}
		foreach (int item in (from kv in _heldAt
			where Game.GameTime - kv.Value > 30000
			select kv.Key).ToList())
		{
			_heldAt.Remove(item);
		}
	}

	private static bool NoOtherHand(int src, int me, int victim)
	{
		if (src == 0 || src == me || !ENTITY.DOES_ENTITY_EXIST(src) || !ENTITY.IS_ENTITY_A_PED(src) || !PED.IS_PED_HUMAN(src))
		{
			return true;
		}
		if (!PED.IS_PED_IN_ANY_VEHICLE(src, atGetIn: false))
		{
			return false;
		}
		return !HandGroups.Contains(WEAPON.GET_WEAPONTYPE_GROUP(PED.GET_PED_CAUSE_OF_DEATH(victim)));
	}

	private void ResolveNemesisDeath(Ped ped, NemesisRecord rec, bool executed = false)
	{
		rec.Touch();
		int num = PED.GET_PED_SOURCE_OF_DEATH(ped.Handle);
		Ped character = Game.Player.Character;
		bool flag = num == character.Handle || IsCompanionKiller(num) || (num == 0 && (executed || ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(ped.Handle, character.Handle, p2: true, p3: true)));
		int value = 0;
		bool flag2 = !flag && _heldAt.TryGetValue(ped.Handle, out value) && Game.GameTime - value < 30000 && NoOtherHand(num, character.Handle, ped.Handle);
		if (flag2)
		{
			flag = true;
			GrudgeLog(string.Format("DEATH {0}: tied up / carried {1} s before, killed by {2} - your kill", rec.Name, (Game.GameTime - value) / 1000, (num == 0) ? "nobody named" : ((!ENTITY.DOES_ENTITY_EXIST(num) || !ENTITY.IS_ENTITY_A_PED(num)) ? "a vehicle / a thing" : ((!PED.IS_PED_HUMAN(num)) ? "an animal" : "a man driving"))));
		}
		executed = executed && flag;
		string text = (flag ? "you" : KillerDesc(num));
		bool flag3 = !_nemesisBodies.ContainsKey(ped.Handle);
		if (flag3)
		{
			if (flag)
			{
				AddNotoriety(Gangs.ById(rec.Gang), Notoriety.ForKill(rec.Rank), $"killed {rec.Name}, rank {rec.Rank}");
			}
			if (flag && rec.Gang != null)
			{
				try
				{
					ControlAfterKill(Gangs.ById(rec.Gang), rec.Rank, ped.Position);
				}
				catch
				{
				}
			}
			if (flag && rec.Gang != null)
			{
				try
				{
					NoteFairKill(Gangs.ById(rec.Gang), ped);
				}
				catch
				{
				}
			}
			if (flag)
			{
				try
				{
					MercyTakenBack(rec.Id);
					MercyTakenBack("h" + ped.Handle);
				}
				catch
				{
				}
			}
			else if (rec.Gang != null)
			{
				try
				{
					LoseMen(Gangs.ById(rec.Gang), NamedWorth(rec.Rank), rec.Name + " killed");
				}
				catch
				{
				}
			}
			NoteKill(ped, rec.Gang);
			QueueDeathNews(rec, flag, text, null, ped.Position);
		}
		_nemesisBodies[ped.Handle] = rec;
		string zoneName = GetZoneName(ped.Position);
		bool flag4 = Function.Call<bool>(502877896016690276uL, new InputArgument[1] { ped.Handle });
		WoundReport woundReport = _bulletImpact.LatestWound(ped.Handle);
		bool flag5 = Config.FatalWoundsFinal && woundReport != null && woundReport.Damage.Any((DamageItem d) => !d.Chipped && FinalWounds.Contains(d.Key));
		int num2 = Math.Min(Config.CheatDeathMaxChance, Config.CheatDeathBaseChance + Config.CheatDeathPerRank * (rec.Rank - 1));
		bool flag6 = !executed && !flag4 && !flag5 && !IsLeaderCopy(rec) && (Config.CheatDeathAlways || _rng.Next(100) < num2);
		if (flag6)
		{
			ScarOption scarOption = PickScar(ped, rec);
			(string, string, string)? tuple = ScarFromWound(woundReport);
			rec.Scars.Add(new Scar
			{
				DamagePack = (tuple?.Item2 ?? scarOption.Pack),
				Description = (tuple?.Item3 ?? scarOption.Description),
				Wound = tuple?.Item1,
				Location = zoneName,
				ReceivedAt = DateTime.Now,
				FromYou = flag
			});
			NoteOutcome(rec, "cheated death");
			rec.TimesCheatedDeath++;
			rec.PresumedDeadAt = GameMinutesNow();
			if (woundReport != null && woundReport.Damage.Any((DamageItem d) => !d.Chipped && WoundKeys.Base(d.Key) == "spine"))
			{
				rec.Paralyzed = true;
			}
			else if (woundReport != null && woundReport.Damage.Any((DamageItem d) => !d.Chipped && LegWounds.Contains(WoundKeys.Base(d.Key))))
			{
				rec.Limp = true;
			}
			if (flag)
			{
				GainRankXp(rec, 3, "cheated death - carries your scar");
				Remember(rec, (Mind.F(rec, "Bravery") < 50) ? "Fear" : "Anger", 80, "you nearly killed him - " + (rec.Scars.LastOrDefault()?.Description ?? "a scar to remember it by"));
				rec.AddHate(25);
				rec.AddFear(Balance.FearPerScar);
				rec.AddYourNotoriety(15);
			}
			History(rec, "KILLED BY " + (flag ? "YOU" : text.ToUpperInvariant()) + " near " + zoneName, Theme.Blood);
			if (rec.History.Count > 0)
			{
				rec.History[rec.History.Count - 1].Cover = true;
			}
			rec.PresumedDead = true;
			rec.LastDefeatLocation = zoneName;
			History(rec, flag ? ("YOU LEFT HIM FOR DEAD near " + zoneName + " - he lived: " + rec.Scars.Last().Description) : ("LEFT FOR DEAD by " + text + " near " + zoneName + " - he lived: " + rec.Scars.Last().Description), Theme.Blood);
		}
		else
		{
			rec.IsAlive = false;
			rec.PresumedDead = false;
			rec.KilledByPlayer = flag;
			History(rec, "KILLED BY " + (flag ? "YOU" : text.ToUpperInvariant()) + " near " + zoneName + (flag4 ? " (headshot)" : (flag5 ? " (fatal wound)" : (executed ? " (finished off)" : (flag2 ? " (tied up)" : "")))), Theme.Blood);
		}
		if (flag3 && flag)
		{
			WorldPart worldPart = new WorldPart
			{
				Id = rec.Id,
				Name = rec.Name,
				Gang = rec.Gang,
				Outcome = "KILLED",
				SecretlyLived = flag6,
				Note = (flag4 ? "headshot" : (flag5 ? "a fatal wound" : (executed ? "finished off" : (flag2 ? "tied up" : rec.Title))))
			};
			if (rec.Gang != null)
			{
				worldPart.Effects.Add($"{Gangs.ById(rec.Gang)?.Name} notoriety +{Notoriety.ForKill(rec.Rank)}");
			}
			ChronicleDeed("kill", "You killed " + rec.Name, "near " + zoneName, ped.Position, rec.Gang, worldPart);
		}
		SaveRoster();
		DeathNotice(rec.FullName + " is dead.");
	}

	private string KillerDesc(int src)
	{
		if (src == 0)
		{
			return "someone";
		}
		if (_worldNemeses.TryGetValue(src, out var value))
		{
			return value.Name;
		}
		Raid raid = _raids.FirstOrDefault((Raid x) => x.Men.Any((Ped m) => m != null && m.Exists() && m.Handle == src));
		if (raid != null)
		{
			return "the " + raid.Gang.Name;
		}
		if (_skirmish.Any((Ped p) => p != null && p.Exists() && p.Handle == src))
		{
			return "the " + (Gangs.ById((ModGroupOf(src) == _sideA) ? _skirmishGangA : _skirmishGangB)?.Name ?? "other side");
		}
		if (_liveCamps.Values.Any((LiveCamp c) => c.Peds.Any((Ped p) => p != null && p.Exists() && p.Handle == src)))
		{
			return "the O'Driscoll Boys";
		}
		return "someone";
	}

	private void KinReact(NemesisRecord dead, string onlyId = null)
	{
		foreach (var item3 in (from n in Roster
			where n.IsAlive && n != dead && (onlyId == null || n.Id == onlyId) && (n.KinId == dead.Id || dead.KinId == n.Id)
			select (n: n, (n.KinId == dead.Id) ? Relative(n.KinRelation, toThem: true) : (dead.KinRelation ?? "kin"))).Concat<(NemesisRecord, string)>(from f in FriendsOf(dead)
			where f.IsAlive && (onlyId == null || f.Id == onlyId) && f.KinId != dead.Id && dead.KinId != f.Id
			select (f: f, "friend")).ToList())
		{
			NemesisRecord item = item3.Item1;
			string item2 = item3.Item2;
			int hate = item.Hate;
			int fear = item.Fear;
			(int hate, int fear, string why) tuple = GriefReaction(item, item2);
			int num = tuple.hate;
			int num2 = tuple.fear;
			string text = tuple.why;
			Tie tie = Mind.TieTo(item, dead.Id);
			if (tie != null)
			{
				float num3 = Math.Max(0.3f, Math.Min(1.5f, (float)(tie.Strength + 30) / 100f));
				num = (int)Math.Round((float)num * num3);
				num2 = (int)Math.Round((float)num2 * num3);
				text += $", tie {tie.Strength}";
			}
			Remember(item, "Grief", Math.Max(40, Math.Min(95, 60 + (tie?.Strength ?? 50) / 3)), "you killed his " + item2 + ", " + dead.Name);
			if (Mind.F(item, "Anger") >= 45 || item.Personality == Personality.Loyal)
			{
				Mind.SetGoal(item, new Goal
				{
					Type = "Avenge",
					Target = "you",
					Text = "avenge his " + item2 + " " + dead.Name,
					Since = GameMinutesNow()
				});
			}
			item.AddYourNotoriety(15);
			item.Hate = Math.Max(0, Math.Min(100, item.Hate + (int)Math.Round((float)num * Balance.HateGainScale)));
			item.Fear = Math.Max(0, Math.Min(100, item.Fear + (int)Math.Round((float)num2 * Balance.FearGainScale)));
			item.Touch();
			Feed(((item2 == "friend") ? "FRIEND" : "KIN") + " " + item.Name + " heard his " + item2 + " " + dead.Name + " died: hate " + Delta(hate, item.Hate) + "  fear " + Delta(fear, item.Fear) + "  (" + text + ")", (item.Fear - fear > item.Hate - hate) ? (150, 170, 210) : Theme.Blood, item);
			GrudgeNotice((item.Fear - fear > item.Hate - hate) ? (item.FullName + " heard you killed his " + item2 + ", " + dead.Name + ". He's afraid he's next.") : (item.FullName + " heard you killed his " + item2 + ", " + dead.Name + ". He's coming for blood."));
		}
	}

	private (int hate, int fear, string why) GriefReaction(NemesisRecord k, string rel)
	{
		float num = Traits.KinWeight(rel);
		float h = 25f * num;
		float f = 6f * num;
		List<string> why = new List<string> { rel };
		switch (k.Personality)
		{
		case Personality.Loyal:
			H(1.4f, "loyal");
			break;
		case Personality.Proud:
			H(1.2f, "proud");
			break;
		case Personality.Hothead:
			H(1.2f, "hothead");
			break;
		case Personality.Cold:
			H(0.6f, "cold");
			break;
		case Personality.Drifter:
			H(0.5f, "drifter");
			break;
		case Personality.Coward:
			H(0.4f, "coward");
			F(3f, "scared");
			break;
		}
		if (k.TraitsRolled)
		{
			H(0.8f + (float)k.Aggression / 250f, Traits.Aggr(k.Aggression).ToLowerInvariant());
			if (k.Disposition == Disposition.Friendly)
			{
				H(0.85f, "friendly");
			}
			if (k.Devotion == Devotion.Devout)
			{
				H(0.85f, "devout");
			}
			if (k.Family == "married" && k.Children > 0)
			{
				F(1.4f, "family man");
			}
			else if (k.Family == "single")
			{
				H(1.1f, "nothing to lose");
			}
			if (k.Smarts == Smarts.Sharp)
			{
				F(1.2f, "sharp");
			}
		}
		return (hate: (int)Math.Round(h), fear: (int)Math.Round(f), why: string.Join(", ", why));
		void F(float m, string tag)
		{
			if (Math.Abs(m - 1f) > 0.01f)
			{
				f *= m;
				why.Add(tag);
			}
		}
		void H(float m, string tag)
		{
			if (Math.Abs(m - 1f) > 0.01f)
			{
				h *= m;
				why.Add(tag);
			}
		}
	}

	private static string Relative(string what, bool toThem)
	{
		return Traits.Inverse(what);
	}

	private static (string key, string pack, string description)? ScarFromWound(WoundReport w)
	{
		DamageItem damageItem = w?.Damage.FirstOrDefault((DamageItem x) => !FinalWounds.Contains(x.Key));
		if (damageItem == null)
		{
			return null;
		}
		if (damageItem.Key.StartsWith("burn:"))
		{
			return (damageItem.Key, "PD_ODR3_Arthur_Wound_Shoulder_L_Cauterized", "burn scars over his " + damageItem.Key.Substring(5));
		}
		if (damageItem.Key.StartsWith("shrapnel:"))
		{
			return (damageItem.Key, "PD_ODR3_Arthur_Wound_Shoulder_L", "shrapnel scars in his " + damageItem.Key.Substring(9));
		}
		if (damageItem.Key == "concussion")
		{
			return (damageItem.Key, "PD_Right_cut_cheek", "ears that still ring from the blast");
		}
		string text = WoundKeys.Side(damageItem.Key);
		switch (WoundKeys.Base(damageItem.Key))
		{
		case "lung":
			return (damageItem.Key, "PD_ODR3_Arthur_Wound_Shoulder_L", "a bullet scar over his " + text + " lung");
		case "liver":
		case "stomach":
		case "intestines":
			return (damageItem.Key, "PD_ODR6_Duster_Slash_Belly", "a puckered bullet scar in his gut");
		case "kidney":
			return (damageItem.Key, "PD_ODR6_Duster_Slash_Belly", "a bullet scar low on his back");
		case "ribs":
		case "sternum":
		case "scapula":
			return (damageItem.Key, "PD_ODR3_Arthur_Wound_Shoulder_L", "ribs that knitted back crooked");
		case "carotid":
		case "windpipe":
			return (damageItem.Key, "PD_Ped_Throat_slash", "a ragged bullet scar across his throat");
		case "jaw":
			return (damageItem.Key, "PD_Right_cut_cheek", "a jaw wired back together");
		case "skull":
			return (damageItem.Key, "PD_Right_cut_cheek", "a dent in his skull where your bullet glanced off");
		case "bladder":
		case "pelvis":
			return (damageItem.Key, "PD_ODR6_Duster_Slash_Belly", "a hip that grinds with every step");
		case "foot":
		case "knee":
		case "femur":
		case "tibia":
		case "femoral":
			return (damageItem.Key, "PD_ODR3_Arthur_Wound_Shoulder_L", ("a " + text + " leg that healed crooked").Replace("  ", " "));
		case "hand":
		case "humerus":
		case "forearm":
		case "clavicle":
			return (damageItem.Key, "PD_ODR3_Arthur_Wound_Shoulder_L", ("a " + text + " arm that never set right").Replace("  ", " "));
		case "spine":
			return (damageItem.Key, "PD_ODR3_Arthur_Wound_Shoulder_L", "a back that's never been straight since");
		default:
			return (damageItem.Key, "PD_ODR3_Arthur_Wound_Shoulder_L", "a bullet scar");
		}
	}

	private unsafe ScarOption PickScar(Ped ped, NemesisRecord rec)
	{
		uint num = PED.GET_PED_CAUSE_OF_DEATH(ped.Handle);
		uint num2 = ((num != 0) ? WEAPON.GET_WEAPONTYPE_GROUP(num) : 0u);
		int num3 = PED.GET_PED_SOURCE_OF_DEATH(ped.Handle);
		Entity entity = ((num3 != 0) ? Entity.FromHandle(num3) : null);
		int num4 = 0;
		PED.GET_PED_LAST_DAMAGE_BONE(ped.Handle, &num4);
		BoneID boneID = (BoneID)num4;
		string text = boneID.ToString();
		ScarOption[] array = ((entity != null && entity.IsAnimal) ? ClawScars : (num2 switch
		{
			3566412244u => BladeScars, 
			2685387236u => BeatingScars, 
			3053283277u => ArrowScars, 
			1548507267u => BurnScars, 
			_ => (!text.Contains("NECK") && !text.Contains("JAW")) ? GunshotScars : NeckScars, 
		}));
		ScarOption[] array2 = array.Where((ScarOption o) => rec.Scars.All((Scar s) => s.DamagePack != o.Pack)).ToArray();
		ScarOption[] array3 = ((array2.Length != 0) ? array2 : array);
		return array3[_rng.Next(array3.Length)];
	}

	private static void ApplyScars(Ped ped, NemesisRecord rec)
	{
		foreach (Scar scar in rec.Scars)
		{
			PED.APPLY_PED_DAMAGE_PACK(ped.Handle, scar.DamagePack, 0f, 1f);
		}
	}
}
