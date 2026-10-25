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
	private sealed class Interrogation
	{
		public int IntelGiven;

		public bool Empty;

		public bool Released;

		public Ped Ped;

		public NemesisRecord Rec;

		public NemesisRecord Stand;

		public Gang Gang;

		public int Round;

		public int Stage;

		public int NextAt;

		public int Hits;

		public int Threats;

		public Personality? Temper;

		public bool HeldByUs;

		public bool Frozen;

		public bool WasGrappled;

		public int LastGamePromptAt = -100000;

		public int RegrabAt = -1;

		public int LastGameThreatAt = -100000;

		public int Regrabs;

		public bool AskedCamp;

		public bool AskedKin;

		public float Terror;

		public float StartTerror;

		public int Stabs;

		public int Shots;

		public int NearShots;

		public int NearFear;

		public int LastDamageAt = -100000;

		public int PendingShotAt = -1;

		public int LastAimTick;

		public bool WasShooting;

		public int FamilyChance;

		public string StartNote = "";

		public string Note = "";

		public readonly List<string> StartTags = new List<string>();

		public readonly List<string> ActOrder = new List<string>();

		public readonly Dictionary<string, int> ActCount = new Dictionary<string, int>();

		public int CampNeed;

		public int FamilyNeed;

		public int CampTries;

		public int KinTries;

		public float CampLockAt = -1f;

		public float KinLockAt = -1f;

		public int ThreatsUsed;

		public bool ScaredSinceThreat = true;

		public bool StartedDown;

		public bool HandsUp;

		public int LastAimedAt = -100000;

		public int StartedAt;

		public Vector3 HoldPos;

		public int LastHealth = -1;

		public int LastShotAt = -100000;

		public int LastHitKindAt = -100000;

		public int LastHitKind = -1;

		public int LastChance;

		public bool CampDone;

		public bool KinDone;

		public string OpenCat;

		public readonly Dictionary<string, int> Tries = new Dictionary<string, int>();

		public readonly Dictionary<string, float> Locks = new Dictionary<string, float>();

		public readonly HashSet<string> AnsweredNow = new HashSet<string>();

		public bool AskedAny;

		public bool Bribed;

		public bool BargainStruck;

		public bool LetGo;

		public bool GotAway;

		public int SlippedAt = -1;

		public bool Calm;

		public bool Warned;

		public bool GoNow;

		public bool Defiant;

		public float Patience;

		public float PatienceMax;

		public int NextAskAt;

		public int BribeBonus;

		public int Common;

		public float Tw = 0.5f;

		public int LastGates = -1;

		public readonly HashSet<string> Reported = new HashSet<string>();

		public bool ShameRemembered;

		public bool FearRemembered;

		public NemesisRecord Shown;

		public NemesisRecord Who => Rec ?? Stand;

		public void Act(string label)
		{
			if (!ActCount.ContainsKey(label))
			{
				ActCount[label] = 0;
				ActOrder.Add(label);
			}
			ActCount[label]++;
		}
	}

	private sealed class Walker
	{
		public int Since;

		public int Until;

		public bool Deaf;
	}

	private static readonly string[] DefiantLines = new string[3] { "INSULT_RESPONSE", "GENERIC_INSULT_HIGH_NEUTRAL", "CHALLENGE_THREATEN" };

	private static readonly string[] ArthurThreats = new string[11]
	{
		"ROB_TOUGH_GRAPPLE_THREATEN", "WITNESS_THREATEN_MALE", "INSULT_HOGTIED_PED_CONV_PART1", "ROB_THREATEN", "ROB_CALM_THREATEN", "GENERIC_THREATEN", "THREATEN", "INTIMIDATE", "WITNESS_THREATEN_MALE_ESCALATED", "ROB_ESCALATED_THREATEN",
		"GENERIC_INSULT_HIGH_NEUTRAL"
	};

	private readonly Dictionary<string, string> _voiceWorks = new Dictionary<string, string>();

	private const ulong GetBestPedWeaponFn = 9548732433391192802uL;

	private readonly Dictionary<int, int> _wentForYouAt = new Dictionary<int, int>();

	private static readonly string[] StareLines = new string[3] { "Next time you point that thing at me, you'd best use it.", "I'll remember your face.", "Go on. Ride on." };

	private static readonly string[] MutterLines = new string[3] { "...Damn fool.", "...Waste of my damn time.", "...Ought to be ashamed." };

	private static readonly string[] SafeTownDoneLines = new string[3] { "You're lucky we're in town, mister.", "Not here. But I'll be seeing you.", "Another time. Somewhere quieter." };

	private static readonly string[] ArthurDone = new string[3] { "GENERIC_BYE", "GOODBYE", "WITNESS_DISMISS" };

	private (float shock, string why) BattleShock(Ped ped, NemesisRecord rec, NemesisRecord shown, Ped player)
	{
		List<string> list = new List<string>();
		float num = 0f;
		string gang = rec?.Gang ?? shown?.Gang ?? OutfitOfPed(ped)?.Id;
		int num2;
		int num3;
		if (_bulletImpact.LatestWound(ped.Handle) == null && !ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(ped.Handle, player.Handle, p2: true, p3: true))
		{
			if (_hitByYouAt.TryGetValue(ped.Handle, out var value))
			{
				num2 = ((Game.GameTime - value < 180000) ? 1 : 0);
				if (num2 != 0)
				{
					goto IL_00d8;
				}
			}
			else
			{
				num2 = 0;
			}
			num3 = ((FoughtYouWhy(ped.Handle) != null) ? 1 : 0);
			goto IL_00d9;
		}
		num2 = 1;
		goto IL_00d8;
		IL_00d9:
		bool flag = (byte)num3 != 0;
		List<(int, Vector3, string)> list2 = RecentKills.Where(((int at, Vector3 pos, string gang) k) => Game.GameTime - k.at < 360000 && k.pos.DistanceTo(ped.Position) < 80f).ToList();
		int num4 = list2.Count<(int, Vector3, string)>(((int at, Vector3 pos, string gang) k) => gang != null && k.gang == gang);
		bool flag2 = num4 > 0 || flag;
		if (list2.Count > 0 && flag2)
		{
			float num5 = Math.Min(50f, (float)num4 * 16f + (float)(list2.Count - num4) * 10f);
			num += num5;
			list.Add(string.Format("{0} killed{1}", list2.Count, (num4 > 0) ? $", {num4} his own" : ""));
		}
		if (num2 != 0 && ped.Health < ped.MaxHealth / 2)
		{
			num += 15f;
			list.Add("badly hurt");
		}
		if (num2 != 0 && _bulletImpact.CrippleLevelOf(ped.Handle) != CrippleLevel.None)
		{
			num += 15f;
			list.Add("crippled");
		}
		if (list2.Count > 0 && flag2 && gang != null && !PedsAround(player).Any((Ped o) => o.Handle != ped.Handle && o.Exists() && !DeadNow(o) && o.Position.DistanceTo(ped.Position) < 40f && (RecordFor(o)?.Gang ?? OutfitOfPed(o)?.Id) == gang))
		{
			num += 15f;
			list.Add("last one standing");
		}
		if (num <= 0f)
		{
			return (shock: 0f, why: "");
		}
		float num6 = Math.Max(0.8f, Traits.FearGain(rec ?? shown) * Balance.FearGainScale);
		return (shock: num * num6, why: string.Join(", ", list) + $", x{num6:0.00}");
		IL_00d8:
		num3 = 1;
		goto IL_00d9;
	}

	private static (int resolve, float terror, int need) Breaking(Personality? p)
	{
		return p switch
		{
			Personality.Coward => (resolve: 10, terror: 0.7f, need: 25), 
			Personality.Drifter => (resolve: 5, terror: 0.55f, need: 35), 
			Personality.Greedy => (resolve: 3, terror: 0.5f, need: 35), 
			Personality.Hothead => (resolve: -5, terror: 0.45f, need: 40), 
			Personality.Cold => (resolve: -8, terror: 0.4f, need: 45), 
			Personality.Proud => (resolve: -8, terror: 0.35f, need: 50), 
			Personality.Loyal => (resolve: -12, terror: 0.35f, need: 55), 
			_ => (resolve: 0, terror: 0.5f, need: 40), 
		};
	}

	private int ChanceToTalk(Interrogation it, Ped player)
	{
		NemesisRecord rec = it.Rec;
		int num = ((rec == null) ? Balance.InterroBaseAmbient : ((rec.Rank >= 4) ? Balance.InterroBaseLieutenant : ((rec.Rank >= 2) ? Balance.InterroBaseCaptain : Balance.InterroBaseBoy)));
		(int resolve, float terror, int need) tuple = Breaking(it.Temper);
		int item = tuple.resolve;
		float item2 = tuple.terror;
		it.FamilyNeed = Math.Min(85, (it.CampNeed = tuple.need) + Balance.InterroFamilyFearExtra + ((it.Temper == Personality.Loyal) ? 5 : 0) + ((rec != null && rec.TraitsRolled && rec.Family == "married" && rec.Children > 0) ? 5 : 0));
		int num2 = Traits.InterrogationBias(rec);
		Ped ped = it.Ped;
		bool flag = ped.IsHogtied || TASK.IS_PED_IN_WRITHE(ped.Handle) || _bulletImpact.CrippleLevelOf(ped.Handle) >= CrippleLevel.Legless;
		if (PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, ped.Handle) || PlayerTargets(ped))
		{
			it.LastAimedAt = Game.GameTime;
		}
		bool flag2 = Game.GameTime - it.LastAimedAt < 2500;
		int num3 = ((rec != null) ? (-(rec.Hate - 50) / 5) : 0);
		int num4 = MoodTalkBias(rec);
		(int bias, string why) tuple2 = Mind.TalkBias(rec, (string id) => Roster.FirstOrDefault((NemesisRecord x) => x.Id == id));
		int item3 = tuple2.bias;
		string item4 = tuple2.why;
		int num5 = ((rec != null) ? ((Quirks.Has(rec, "drunk") ? 8 : 0) + (Quirks.Has(rec, "traitor") ? 15 : 0) - (Quirks.Has(rec, "sadist") ? 8 : 0)) : 0);
		int num6 = (it.Common = num + item + num2 + (flag ? 8 : 0) + (flag2 ? 8 : 0) + num3 + num4 + item3 + num5);
		it.Tw = item2;
		int num7 = (int)Math.Round((it.Terror - (float)it.CampNeed) * item2);
		int num8 = (int)Math.Round((it.Terror - (float)it.FamilyNeed) * item2);
		int num9 = ((!(it.Terror < (float)it.CampNeed)) ? Math.Max(3, Math.Min(90, num6 + num7)) : 0);
		it.FamilyChance = ((!(it.Terror < (float)it.FamilyNeed)) ? Math.Max(5, Math.Min(85, num6 + num8)) : 0);
		it.Note = string.Format("{0}: base {1} resolve {2:+0;-0;0} traits {3:+0;-0;0}{4}{5} hate {6:+0;-0;0}{7}{8}", it.Temper, num, item, num2, flag ? " bound+8" : "", flag2 ? " aimed+8" : "", num3, (num4 != 0) ? $" mood {num4:+0;-0;0}" : "", (item3 != 0) ? $" mind {item3:+0;-0;0} ({item4})" : "") + $" | fear {it.Terror:0}: camp needs {it.CampNeed} ({num7:+0;-0;0} past it x{item2:0.00}) -> {num9}%" + $" | family needs {it.FamilyNeed} ({num8:+0;-0;0}) -> {it.FamilyChance}%";
		if (it == _interro)
		{
			InterroNote = it.Note;
		}
		QuestionRow questionRow = Questions.All.FirstOrDefault((QuestionRow x) => x.Id == "where-camp");
		QuestionRow questionRow2 = Questions.All.FirstOrDefault((QuestionRow x) => x.Id == "where-people");
		if (questionRow != null)
		{
			num9 = RollOdds(it, questionRow);
		}
		if (questionRow2 != null)
		{
			it.FamilyChance = RollOdds(it, questionRow2);
		}
		return num9;
	}

	private void Terrify(Interrogation it, int amount, string what)
	{
		if (amount <= 0)
		{
			return;
		}
		float num = Traits.FearGain(it.Rec ?? it.Shown) * Balance.FearGainScale;
		int cLOCK_HOURS = CLOCK.GET_CLOCK_HOURS();
		if (Quirks.Has(it.Rec, "superstitious") && (cLOCK_HOURS >= 21 || cLOCK_HOURS < 5))
		{
			num *= 1.3f;
		}
		if (Quirks.Has(it.Rec, "sadist"))
		{
			switch (what)
			{
			case "HIT":
			case "STABBED":
			case "SHOT":
				num *= 0.7f;
				break;
			}
		}
		try
		{
			if (PlayerMasked(Game.Player.Character))
			{
				num *= 1.25f;
			}
		}
		catch
		{
		}
		it.Act((what == "SHOT NEAR") ? "SHOT NEAR HIM" : ((what == "SAW YOU KILL") ? "SAW A KILL" : what));
		float terror = it.Terror;
		bool flag = it.Terror >= 99.5f;
		it.Terror = Math.Min(100f, it.Terror + (float)amount * num);
		if (what != "THREATENED")
		{
			it.ScaredSinceThreat = true;
		}
		bool flag2 = false;
		if (it.CampLockAt >= 0f && it.Terror >= it.CampLockAt)
		{
			it.CampLockAt = -1f;
			it.CampTries = Balance.InterroTriesPerScare - 1;
			flag2 = true;
		}
		if (it.KinLockAt >= 0f && it.Terror >= it.KinLockAt)
		{
			it.KinLockAt = -1f;
			it.KinTries = Balance.InterroTriesPerScare - 1;
			flag2 = true;
		}
		int count = it.Locks.Count;
		ReopenQuestions(it, flag && amount > 0);
		if (it.Locks.Count < count)
		{
			flag2 = true;
		}
		if (flag2)
		{
			Feed($"INTERROGATION {Speaker(it)}: scared enough to ask again (fear {it.Terror:0})", Theme.Good, it.Rec);
		}
		int lastChance = it.LastChance;
		it.LastChance = ChanceToTalk(it, Game.Player.Character);
		Feed($"{what} {Speaker(it)}: fear {terror:0} -> {it.Terror:0} (x{num:0.00})  camp {lastChance}% -> {it.LastChance}%  family {it.FamilyChance}%", (it.Terror > terror) ? Theme.Warn : Theme.Dim, it.Rec);
		if (_convo != null && _convo.It == it)
		{
			Personality personality = it.Temper ?? Personality.Cold;
			bool flag3 = it.Terror >= 60f;
			string text = what switch
			{
				"SHOT NEAR" => "near", 
				"STABBED" => "cut", 
				"SHOT" => "shot", 
				_ => "hit", 
			};
			if (flag3 || (personality == Personality.Coward && text == "hit"))
			{
				text += "_scared";
			}
			_convo.Line = InterroLines.Pick(text, it.Gang, personality, _rng, null, null, LeaderOut(it.Gang, it.Rec)) ?? ((!(what == "SHOT")) ? "...Ugh." : (flag3 ? "AAH! Stop! Stop, I'll talk!" : "Gah! You son of a..."));
			BuildOptions(_convo);
		}
	}

	private string ConfessCamp(Interrogation it)
	{
		NemesisRecord nemesisRecord = IntelTarget(it);
		it.CampDone = true;
		if (nemesisRecord != null && CampOf(nemesisRecord) == null)
		{
			return Nothing(it, nemesisRecord.Name + "? He's on the move - no camp I know of. That's the truth!");
		}
		if (nemesisRecord != null && it.Rec != null)
		{
			it.Rec.GaveCamp = true;
			it.Rec.AddHate(10);
		}
		if (nemesisRecord == null)
		{
			if (it.Rec == null || it.Rec.Rank < 4)
			{
				if (BossOf(it.Rec) == null || !BossOf(it.Rec).CampKnown)
				{
					return "I don't know nothin' about the camps! I just ride where I'm told!";
				}
				return "You already know where " + BossOf(it.Rec).Name + " is! That's all I got!";
			}
			if (!IsLeader(it.Gang, it.Rec))
			{
				if (!LeaderOut(it.Gang, it.Rec))
				{
					return (it.Gang?.LeaderName ?? "The boss") + "? Nobody knows where he lays his head. Not even me. That's the truth!";
				}
				return "Nobody's at the top right now. There's no camp to give you.";
			}
			return "Where I sleep? You'll not get that out of me. Nobody gets that.";
		}
		CampSite campSite = CampOf(nemesisRecord);
		string text = ((it.Rec?.BossId == nemesisRecord.Id) ? ("our " + TierWord(nemesisRecord, plural: false)) : ("one of the " + TierWord(nemesisRecord, plural: true)));
		string text2 = (campSite.Hideout ? ("holed up at " + campSite.Name) : ("camped out in " + GetZoneName(SitePos(campSite))));
		nemesisRecord.AddHate(5);
		RevealPast(it.Rec, "his years in the gang", "gang", "rank", "weave", "world", "crime", "war");
		History(nemesisRecord, "GIVEN UP: " + Speaker(it) + " told you where to find him (" + campSite.Label + ")", Theme.Warn);
		RevealCamp(nemesisRecord, found: false);
		ReplyNotice("INTEL: " + OfficerTitle(nemesisRecord) + " " + nemesisRecord.FullName + " - " + campSite.Label + ". Marked on your map.");
		Feed("INTEL from " + Speaker(it) + ": " + nemesisRecord.FullName + " (" + text + ") - " + campSite.Label, Theme.Good, it.Rec);
		WorldPart worldPart = ((it.Rec != null) ? PartFor(it.Rec, "TALKED", "under questioning") : new WorldPart
		{
			Name = Speaker(it),
			Outcome = "TALKED",
			Note = "under questioning"
		});
		if (it.Rec != null)
		{
			worldPart.Effects.Add("hate +10 (a rat now)");
		}
		WorldPart worldPart2 = PartFor(nemesisRecord, "GIVEN UP", campSite.Label);
		worldPart2.Effects.Add("camp marked on your map");
		ChronicleDeed("intel", Speaker(it) + " gave up " + nemesisRecord.Name + "'s " + (campSite.Hideout ? "hideout" : "camp"), campSite.Label, null, nemesisRecord.Gang, worldPart, worldPart2);
		return "Alright, alright! " + nemesisRecord.FullName + " - " + text + " - he's " + text2 + "! Now let me be!";
	}

	private NemesisRecord StandInFor(Ped ped, Interrogation it)
	{
		_named.TryGetValue(ped.Handle, out var value);
		Provocation value2;
		NemesisRecord nemesisRecord = ((_provoked.TryGetValue(ped.Handle, out value2) && value2.Shadow != null && value2.Shadow.PastRolled) ? value2.Shadow : null);
		if (nemesisRecord == null)
		{
			try
			{
				nemesisRecord = ShadowFor(ped);
			}
			catch
			{
				nemesisRecord = new NemesisRecord();
			}
		}
		nemesisRecord.Id = $"tmp:{ped.Handle}";
		nemesisRecord.Name = value?.Name ?? ((it.Gang?.Name != null) ? (it.Gang.Name + " man") : "Stranger");
		nemesisRecord.Title = value?.Title ?? "";
		nemesisRecord.Gang = it.Gang?.Id;
		nemesisRecord.ModelHash = (uint)ped.Model.Hash;
		nemesisRecord.Personality = it.Temper;
		nemesisRecord.Hate = 30;
		nemesisRecord.Rank = 1;
		NemesisRecord value3;
		NemesisRecord nemesisRecord2 = (_followerOf.TryGetValue(ped.Handle, out value3) ? value3 : null);
		if (nemesisRecord2 == null)
		{
			nemesisRecord2 = _liveCamps.Values.FirstOrDefault((LiveCamp lc) => lc.Peds.Any((Ped p) => p != null && p.Handle == ped.Handle))?.Boss;
		}
		nemesisRecord.BossId = nemesisRecord2?.Id;
		GrudgeLog("INTERRO " + nemesisRecord.Name + ": no record - a stand-in with his own life" + ((nemesisRecord2 != null) ? (", answers to " + nemesisRecord2.Name) : ""));
		return nemesisRecord;
	}

	private string ConfessKin(Interrogation it)
	{
		it.KinDone = true;
		NemesisRecord who = it.Who;
		if (who == null)
		{
			return Nothing(it, "Family? Ain't got none worth telling you about.");
		}
		List<(NemesisRecord, string)> list = KinOf(who);
		if (list.Count > 0 && it.Rec != null)
		{
			it.Rec.GaveKin = true;
		}
		RevealKin(who, list);
		List<Goal> goals = who.Goals;
		if (goals != null && goals.Count > 0 && !who.WantsKnown)
		{
			who.WantsKnown = true;
			Feed("WANTS " + who.Name + ": " + string.Join("; ", who.Goals.Select((Goal g) => g.Text)), Theme.Gold, who);
		}
		RevealPast(who, "his family and where he came from", "born", "youth", "family", "loss", "kin", "friend", "faith");
		who.AddHate(5);
		if (list.Count == 0)
		{
			return Nothing(it, (who.Family == "married") ? ("Got a wife" + ((who.Children > 0) ? " and young'uns" : "") + " - and you leave them out of this!") : "Ain't got nobody. Nobody rides with me.");
		}
		(NemesisRecord, string) tuple = list[0];
		if (tuple.Item2 == "friend" && list.All<(NemesisRecord, string)>(((NemesisRecord who, string rel) x) => x.rel == "friend"))
		{
			if (!tuple.Item1.IsAlive)
			{
				return tuple.Item1.Name + " was my friend. You killed him.";
			}
			return tuple.Item1.Name + "... we ride together. " + (Gangs.ById(tuple.Item1.Gang)?.Name ?? "He's his own man") + "." + ((list.Count > 1) ? (" And " + list[1].Item1.Name + ".") : "") + " Leave 'em out of this.";
		}
		string text = ((list.Count > 1) ? (" And " + list[1].Item1.Name + ((list[1].Item2 == "friend") ? ", a friend" : (", my " + list[1].Item2)) + ".") : "");
		if (!tuple.Item1.IsAlive)
		{
			return "My " + tuple.Item2 + ", " + tuple.Item1.Name + ". You killed him, you son of a bitch." + text;
		}
		return "My " + tuple.Item2 + "... " + tuple.Item1.Name + ". He rides with " + (Gangs.ById(tuple.Item1.Gang)?.Name ?? "nobody") + "." + text + " Leave him be!";
	}

	private List<(NemesisRecord who, string rel)> KinOf(NemesisRecord rec)
	{
		List<(NemesisRecord, string)> list = new List<(NemesisRecord, string)>();
		if (rec == null)
		{
			return list;
		}
		NemesisRecord nemesisRecord = ((rec.KinId != null) ? Roster.FirstOrDefault((NemesisRecord r) => r.Id == rec.KinId) : null);
		if (nemesisRecord != null)
		{
			list.Add((nemesisRecord, Traits.Inverse(rec.KinRelation)));
		}
		foreach (NemesisRecord item in Roster.Where((NemesisRecord r) => r.KinId == rec.Id))
		{
			list.Add((item, item.KinRelation));
		}
		foreach (NemesisRecord item2 in FriendsOf(rec))
		{
			list.Add((item2, "friend"));
		}
		return list;
	}

	private void RevealKin(NemesisRecord rec, List<(NemesisRecord who, string rel)> kin)
	{
		string text = ((!rec.TraitsRolled) ? "" : ((rec.Family == "single") ? "single" : (((rec.Family == "married") ? "married" : "a widower") + ((rec.Children > 0) ? string.Format(", {0} {1}", rec.Children, (rec.Children == 1) ? "child" : "children") : ""))));
		if (kin.Count == 0)
		{
			Feed("KIN " + rec.Name + ": no kin or friends on your roster" + ((text != "") ? (" - " + text) : ""), Theme.Dim, rec);
			return;
		}
		Feed("KIN " + rec.Name + " gave up his family and friends: " + string.Join(";  ", kin.Select(((NemesisRecord who, string rel) k) => k.rel + " " + k.who.Name + " (" + Where(k.who) + ")")) + ((text != "") ? ("  - " + text) : ""), (210, 190, 140), rec);
		ChronicleDeed("intel", rec.Name + " gave up his family and friends", "", null, rec.Gang, new WorldPart[1] { PartFor(rec, "TALKED", "under questioning") }.Concat(kin.Select(((NemesisRecord who, string rel) k2) => PartFor(k2.who, "GIVEN UP", "his " + k2.rel))).ToArray());
		foreach (var item in kin)
		{
			History(item.who, ((item.rel == "friend") ? "FRIEND" : "KIN") + ": his " + Traits.Inverse(item.rel) + " " + rec.Name + " told you about him under questioning", (210, 190, 140));
		}
		static string Where(NemesisRecord k)
		{
			if (!k.IsAlive)
			{
				return "dead";
			}
			Gang gang = Gangs.ById(k.Gang);
			if (gang != null)
			{
				return $"{gang.Name}, rank {k.Rank}";
			}
			return "rides alone";
		}
	}

	private NemesisRecord IntelTarget(Interrogation it)
	{
		NemesisRecord rec = it.Rec;
		if (rec != null)
		{
			NemesisRecord nemesisRecord = BossOf(rec);
			if (!Unknown(nemesisRecord))
			{
				return null;
			}
			return nemesisRecord;
		}
		if (it.Gang == null)
		{
			return null;
		}
		List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.Gang == it.Gang.Id && n.Rank >= 2 && n.Rank < 4 && Unknown(n)).ToList();
		if (list.Count != 0)
		{
			return list[_rng.Next(list.Count)];
		}
		return null;
		static bool Unknown(NemesisRecord o)
		{
			if (o != null && o.CampId != null && !o.CampKnown && o.CanHunt && !o.PresumedDead)
			{
				return IsOfficer(o);
			}
			return false;
		}
	}

	private void BuildInterroOptions(Convo c)
	{
		BuildQuestionOptions(c);
	}

	private void ChooseInterro(Convo c, ConvoOption o)
	{
		FaceSeenInQuestioning(c.Ped, Game.Player.Character);
		Interrogation it = c.It;
		Ped character = Game.Player.Character;
		NemesisRecord rec = it.Rec;
		Personality personality = it.Temper ?? Personality.Cold;
		string text = Speaker(it);
		int before = rec?.Hate ?? 0;
		int before2 = rec?.Fear ?? 0;
		_ = it.LastChance;
		int num = _rng.Next(100);
		if (o.Odds < 0)
		{
			_ = 1;
		}
		else
			_ = num < o.Odds;
		_ = InterroNote;
		if (o.Key.StartsWith("cat:"))
		{
			ToggleCategory(c, o.Key.Substring(4));
			return;
		}
		if (o.Key.StartsWith("q:"))
		{
			AskQuestion(c, o);
			return;
		}
		switch (o.Key)
		{
		case "ibribe":
		{
			int num2 = BribeCost(it);
			if (CashDollars < num2)
			{
				break;
			}
			MONEY._MONEY_DECREMENT_CASH_BALANCE(num2 * 100);
			if (!it.Ped.IsHogtied && !PED.IS_PED_RAGDOLL(it.Ped.Handle))
			{
				GesturePay(it.Ped, youPay: true);
			}
			it.Bribed = true;
			bool flag = personality == Personality.Greedy || (rec != null && Mind.N(rec, "Money") >= 60);
			bool flag2 = rec != null && rec.Wealth == Wealth.Poor;
			string situation;
			if (personality == Personality.Loyal)
			{
				it.BribeBonus = -10;
				if (rec != null)
				{
					int hate = rec.Hate;
					rec.AddHate(5);
					CapScuffle(rec, hate);
				}
				situation = "bribe_loyal";
			}
			else if (rec != null && rec.Wealth == Wealth.Rich)
			{
				it.BribeBonus = 0;
				situation = "bribe_rich";
			}
			else if (flag)
			{
				it.BribeBonus = 30;
				situation = "bribe_greedy";
			}
			else if (flag2)
			{
				it.BribeBonus = 15;
				situation = "bribe_poor";
			}
			else
			{
				it.BribeBonus = 8;
				situation = "bribe";
			}
			c.Line = InterroLines.Pick(situation, it.Gang, personality, _rng, null, null, LeaderOut(it.Gang, it.Rec)) ?? "Money don't buy everything. But it helps.";
			it.Act("PAID");
			Feed(string.Format("BRIBED {0}: ${1} - talk {2:+0;-0;0}%{3}", text, num2, it.BribeBonus, flag ? ", lies less" : ""), (it.BribeBonus > 0) ? Theme.Good : Theme.Warn, rec);
			break;
		}
		case "ithreat":
		{
			it.Threats++;
			it.ThreatsUsed++;
			it.ScaredSinceThreat = false;
			Terrify(it, Balance.TerrorThreat, "THREATENED");
			SayAny(character, "threaten", ArthurThreats, it.Ped);
			int item = Personalities.Interrogation(it.Temper).threat;
			bool flag3 = Unscared(it);
			c.Line = InterroLines.Pick((item < 0 || flag3) ? "threat_defy" : ((personality == Personality.Coward || it.Terror >= 60f) ? "threat_scared" : ((_rng.Next(2) == 0) ? "threat_defy" : "threat")), it.Gang, personality, _rng, null, null, LeaderOut(it.Gang, it.Rec)) ?? ((item < 0 || flag3) ? "Go on then. Do it." : "...You wouldn't.");
			if (flag3)
			{
				if ((personality == Personality.Hothead || personality == Personality.Proud) && _rng.Next(2) == 0)
				{
					OutOfPatience(c, it, "called your bluff");
					return;
				}
				if (Strain(c, it, 2f, "an empty threat"))
				{
					return;
				}
			}
			break;
		}
		case "idone":
			SayAny(character, "done talking", ArthurDone, it.Ped);
			Feed("DONE " + text + ": you left it there (not scared of you - nothing spared)", Theme.Dim, rec);
			c.Line = ThatsAllLine(personality, IsMourner(it.Ped));
			EndConvoSoon(c, "that's all");
			return;
		case "irelease":
			LetGoAccounting(it);
			Say(character, "WITNESS_DISMISS", it.Ped);
			Feed("LET GO " + text + " after questioning" + ((rec != null) ? (": hate " + Delta(before, rec.Hate) + "  fear " + Delta(before2, rec.Fear)) : ""), Theme.Dim, rec);
			c.Line = InterroLines.Pick((it.Terror >= 55f) ? "letgo_scared" : "letgo", it.Gang, personality, _rng, null, null, LeaderOut(it.Gang, rec), it.Ped != null && it.Ped.Exists() && Models.Female((uint)it.Ped.Model.Hash)) ?? "...";
			EndConvoSoon(c, "release");
			return;
		}
		it.LastChance = ChanceToTalk(it, character);
		if (rec != null)
		{
			SaveRoster();
		}
		BuildOptions(c);
	}

	private void EndInterrogation(bool released, bool quiet = true)
	{
		if (_interro?.Rec != null)
		{
			Interrogation x = _interro;
			List<string> list = x.AnsweredNow.Where((string a) => !x.Reported.Contains(a)).ToList();
			List<string> list2 = x.ActOrder.Select((string a) => (x.ActCount[a] <= 1) ? a.ToLowerInvariant() : $"{a.ToLowerInvariant()} x{x.ActCount[a]}").ToList();
			if (x.Reported.Count == 0 || list.Count > 0)
			{
				string text = ((list.Count > 0) ? ("answered " + string.Join(", ", list.Select((string id) => Questions.All.FirstOrDefault((QuestionRow q) => q.Id == id)?.Answer ?? id))) : "told you nothing");
				History(x.Rec, string.Format("QUESTIONED{0}: {1} - fear {2:0} -> {3:0}{4}", (x.Reported.Count > 0) ? " (again)" : "", text, x.StartTerror, x.Terror, (list2.Count > 0) ? (" - " + string.Join(", ", list2)) : ""), Theme.Gold);
			}
			foreach (string item in list)
			{
				x.Reported.Add(item);
			}
			if (x.Reported.Count == 0)
			{
				x.Reported.Add("-");
			}
		}
		if (_interro != null && _interro.Rec != null && _interro.Ped.Exists() && !DeadNow(_interro.Ped))
		{
			NemesisRecord rec = _interro.Rec;
			int num = (int)Math.Round((_interro.Terror - _interro.StartTerror) / 2f);
			if (_interro.Terror >= 40f && !_interro.FearRemembered)
			{
				_interro.FearRemembered = true;
				Remember(rec, "Fear", (int)Math.Min(90f, _interro.Terror), "you questioned him");
			}
			if (_interro.CampDone && rec.GaveCamp && !_interro.ShameRemembered)
			{
				_interro.ShameRemembered = true;
				Remember(rec, "Shame", 60, "he talked - gave up his own");
			}
			if (num > 0)
			{
				int fear = rec.Fear;
				rec.Fear = Math.Min(100, rec.Fear + num);
				Feed($"FEAR {rec.Name}: questioning left its mark - fear {Delta(fear, rec.Fear)}  (interrogation fear {_interro.StartTerror:0} -> {_interro.Terror:0})", (150, 170, 210), rec);
				_interro.StartTerror = _interro.Terror;
				SaveRoster();
			}
		}
		HidePrompts();
		HideLockPrompt();
		Interrogation interro = _interro;
		_interro = null;
		_interroCandidate = null;
		ReleaseHisPeople(released && interro != null && interro.GoNow && CanGoForYou(interro.Ped, Game.Player.Character));
		if (interro?.Ped != null && interro.Ped.Exists())
		{
			ENTITY.FREEZE_ENTITY_POSITION(interro.Ped.Handle, toggle: false);
		}
		if (interro?.Ped != null && interro.Ped.Exists())
		{
			interro.Ped.BlockPermanentEvents = StaysBlocked(interro.Ped);
			TASK.TASK_CLEAR_LOOK_AT(interro.Ped.Handle);
		}
		if (released && interro != null && (interro.LetGo || interro.BargainStruck) && interro.Ped != null && interro.Ped.Exists() && !DeadNow(interro.Ped))
		{
			if (_surrenderGroup == 0)
			{
				_surrenderGroup = World.AddRelationshipGroup("NEMESIS_SURRENDERED");
			}
			interro.Ped.RelationshipGroup = _surrenderGroup;
			World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, _surrenderGroup, Game.Player.Character.RelationshipGroup);
			if (!interro.HeldByUs)
			{
				interro.Ped.BlockPermanentEvents = true;
				_sparedBound[interro.Ped.Handle] = Game.GameTime;
				GrudgeLog(string.Format("INTERRO {0}: ped {1} {2} - he runs once he's free", interro.BargainStruck ? "bargain struck" : "let go", interro.Ped.Handle, interro.Ped.IsHogtied ? "hogtied" : "down / held"));
			}
		}
		if (interro?.Ped == null || !interro.Ped.Exists() || DeadNow(interro.Ped))
		{
			return;
		}
		if (released && interro.GoNow)
		{
			interro.GoNow = false;
			if (CanGoForYou(interro.Ped, Game.Player.Character))
			{
				GoesForYou(interro, Game.Player.Character);
				return;
			}
		}
		if (released && quiet && !interro.GotAway)
		{
			TalkEndedQuietly(interro.Ped, interro.LetGo || interro.BargainStruck);
		}
		if (interro.HeldByUs)
		{
			TASK.CLEAR_PED_TASKS(interro.Ped.Handle, p1: true, p2: true);
			if (released)
			{
				AfterQuestioning(interro, Game.Player.Character);
			}
		}
	}

	private void AfterQuestioning(Interrogation it, Ped player)
	{
		int handle = it.Ped.Handle;
		AfterTalk afterTalk = InterroPatience.After(it.Terror, it.Temper, it.Calm, it.LetGo || it.BargainStruck, it.Warned);
		bool flag = afterTalk != AfterTalk.Run && !it.LetGo && !it.BargainStruck && BackToGrave(it.Ped);
		bool flag2 = !flag && afterTalk != AfterTalk.Run && !it.LetGo && !it.BargainStruck && BackToHisPatrol(handle);
		if (flag || flag2)
		{
			switch (afterTalk)
			{
			case AfterTalk.StareDown:
				ShowSubtitle(Speaker(it) + ": \"" + PickLine(StareLines) + "\"");
				break;
			case AfterTalk.Mutter:
				Say(it.Ped, DefiantLines[_rng.Next(DefiantLines.Length)], player);
				ShowSubtitle(Speaker(it) + ": \"" + PickLine(MutterLines) + "\"");
				break;
			}
		}
		else
		{
			switch (afterTalk)
			{
			case AfterTalk.Run:
				StartRunning(handle, player);
				_recentFoes[handle] = Game.GameTime;
				_runnerRolled.Remove(handle);
				_letGoRunners.Add(handle);
				break;
			case AfterTalk.WalkOff:
				WalkOff(handle, player);
				break;
			case AfterTalk.Business:
				TASK.TASK_WANDER_STANDARD(handle, 10f, 10);
				break;
			case AfterTalk.StareDown:
				TASK.TASK_TURN_PED_TO_FACE_ENTITY(handle, player.Handle, 3000, 0f, 0f, 0f);
				_walkOffs[handle] = new Walker
				{
					Since = Game.GameTime,
					Until = Game.GameTime + 3000
				};
				ShowSubtitle(Speaker(it) + ": \"" + PickLine(StareLines) + "\"");
				break;
			case AfterTalk.Mutter:
				WalkOff(handle, player);
				Say(it.Ped, DefiantLines[_rng.Next(DefiantLines.Length)], player);
				ShowSubtitle(Speaker(it) + ": \"" + PickLine(MutterLines) + "\"");
				break;
			}
		}
		GrudgeLog(string.Format("INTERRO closed on {0} (fear {1:0}{2}{3}): {4}{5}", Speaker(it), it.Terror, it.Calm ? $", patience {it.Patience:0}/{it.PatienceMax:0}" : ", from a fight", (it.LetGo || it.BargainStruck) ? ", let go" : "", afterTalk, flag ? " - back to his place at the grave" : (flag2 ? " - back to his patrol" : "")));
	}

	private static bool Unscared(Interrogation it)
	{
		if (it.Calm)
		{
			return it.Terror < (float)it.CampNeed;
		}
		return false;
	}

	private string FoughtYouWhy(int h)
	{
		if (!_fighters.ContainsKey(h))
		{
			if (!_cameAtYou.Contains(h))
			{
				if (!_selfDefence.Contains(h))
				{
					if (!_combatants.TryGetValue(h, out var value) || Game.GameTime - value >= 180000)
					{
						if (!_recentFoes.TryGetValue(h, out var value2) || Game.GameTime - value2 >= 180000)
						{
							if (!_hitByYouAt.TryGetValue(h, out var value3) || Game.GameTime - value3 >= 180000)
							{
								return null;
							}
							return $"you hit him {(Game.GameTime - value3) / 1000} s ago";
						}
						return $"fought or fled you {(Game.GameTime - value2) / 1000} s ago";
					}
					return $"in a fight with you {(Game.GameTime - value) / 1000} s ago";
				}
				return "went for you";
			}
			return "came at you";
		}
		return "fought you";
	}

	private bool Strain(Convo c, Interrogation it, float cost, string why)
	{
		float patience = it.Patience;
		it.Patience = Math.Max(0f, it.Patience - cost);
		if (it.Patience <= 0f)
		{
			OutOfPatience(c, it, why);
			return true;
		}
		if (it.Patience <= 1f && !it.Warned)
		{
			it.Warned = true;
			c.Line = (c.Line + " " + WarnLine(it.Temper ?? Personality.Cold)).Trim();
			Feed($"INTERROGATION {Speaker(it)}: his patience is about gone ({it.Patience:0} left) - one more and he's done", Theme.Warn, it.Rec);
		}
		GrudgeLog($"INTERRO {Speaker(it)} patience {patience:0} -> {it.Patience:0} ({why}, fear {it.Terror:0} under his line {it.CampNeed})");
		return false;
	}

	private static void FaceYouDown(Ped p, Ped player)
	{
		TASK.TASK_TURN_PED_TO_FACE_ENTITY(p.Handle, player.Handle, -1, 0f, 0f, 0f);
		TASK.TASK_LOOK_AT_ENTITY(p.Handle, player.Handle, -1, 0, 51, 0);
	}

	private bool CanGoForYou(Ped p, Ped player)
	{
		if (p != null && p.Exists() && !DeadNow(p) && !p.IsIncapacitated && !GrappledBy(p, player) && !p.IsHogtied && !p.IsRagdoll && !TASK.IS_PED_IN_WRITHE(p.Handle) && !CantWalk(p.Handle))
		{
			return !ENTITY.IS_ENTITY_ATTACHED(p.Handle);
		}
		return false;
	}

	private void OutOfPatience(Convo c, Interrogation it, string why)
	{
		it.Patience = 0f;
		Ped character = Game.Player.Character;
		Personality t = it.Temper ?? Personality.Cold;
		if (it.Rec != null)
		{
			SaveRoster();
		}
		string text = StoryBansWeapons(character.Position);
		if (!CanGoForYou(it.Ped, character) || text != null)
		{
			c.Line = ((text != null) ? PickLine(SafeTownDoneLines) : DoneLine(t));
			Feed("INTERROGATION " + Speaker(it) + ": out of patience (" + why + ") - he won't say another word", Theme.Warn, it.Rec);
			GrudgeLog("INTERRO " + Speaker(it) + " out of patience (" + why + ") - " + ((text != null) ? ("not here (" + text + ")") : "he can't go for you") + ": done talking");
			EndConvoSoon(c, "out of patience");
		}
		else
		{
			ShowSubtitle(Speaker(it) + ": \"" + SnapLine(t) + "\"");
			Say(it.Ped, DefiantLines[_rng.Next(DefiantLines.Length)], character);
			GrudgeLog($"INTERRO {Speaker(it)} SNAPPED ({why}; fear {it.Terror:0} under his line {it.CampNeed}) - he goes for you");
			if (it.Rec != null)
			{
				History(it.Rec, "WENT FOR YOU when you questioned him", Theme.Warn);
			}
			it.GoNow = true;
			EndConvo("he snapped");
		}
	}

	internal bool WentForYouQuestioned(int h)
	{
		if (_wentForYouAt.TryGetValue(h, out var value))
		{
			return Game.GameTime - value < 60000;
		}
		return false;
	}

	private void GoesForYou(Interrogation it, Ped player)
	{
		Ped ped = it.Ped;
		int handle = ped.Handle;
		Crumbs.Native("Interrogation snap");
		TASK.CLEAR_PED_TASKS(handle, p1: true, p2: true);
		_wentForYouAt[handle] = Game.GameTime;
		if (_cameAtYou.Count > 300)
		{
			_cameAtYou.Clear();
		}
		_cameAtYou.Add(handle);
		_selfDefence.Add(handle);
		ped.RelationshipGroup = NemesisGroup();
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, NemesisGroup(), player.RelationshipGroup);
		PED.SET_PED_COMBAT_ATTRIBUTES(handle, 5, enabled: true);
		PED.SET_PED_COMBAT_ATTRIBUTES(handle, 46, enabled: true);
		uint num = Function.Call<uint>(9548732433391192802uL, new InputArgument[3] { handle, false, false });
		uint num2 = ((num != 0) ? WEAPON.GET_WEAPONTYPE_GROUP(num) : 0u);
		bool flag = num != 0 && num2 != 2685387236u && num2 != 308416707;
		if (flag)
		{
			try
			{
				WEAPON.SET_CURRENT_PED_WEAPON(handle, num, equipNow: true, 0, p4: false, p5: false);
			}
			catch
			{
			}
		}
		TASK.TASK_COMBAT_PED(handle, player.Handle, 0, 16);
		string text = ((!flag) ? "his fists" : (num2 switch
		{
			1548507267u => "something to throw", 
			3566412244u => "a knife", 
			_ => "his gun", 
		}));
		Feed("SNAPPED " + Speaker(it) + ": out of patience - he goes for you with " + text, Theme.Warn, it.Rec);
	}

	private string PickLine(string[] l)
	{
		return l[_rng.Next(l.Length)];
	}

	private string WarnLine(Personality t)
	{
		return PickLine(t switch
		{
			Personality.Coward => new string[2] { "Please, mister... leave me be. I'm warning you.", "Don't you push me. I mean it." }, 
			Personality.Greedy => new string[2] { "Time's money, friend. And you're about out of both.", "You ain't paying for this conversation. It's about over." }, 
			Personality.Loyal => new string[2] { "I've got nothing for you. Ask again and see what happens.", "I won't tell you twice." }, 
			Personality.Cold => new string[2] { "You're wasting my time. You won't waste much more of it.", "Careful now." }, 
			Personality.Proud => new string[2] { "I've answered to you long enough.", "Nobody talks to me like this for long." }, 
			Personality.Hothead => new string[2] { "One more question. Go on. Ask me one more.", "You're pushin' me, mister. You're pushin' real hard." }, 
			_ => new string[2] { "Last question, mister. Then I'm done with you.", "That's about enough of that." }, 
		});
	}

	private string SnapLine(Personality t)
	{
		return PickLine(t switch
		{
			Personality.Cold => new string[2] { "Enough.", "You should've let it be." }, 
			Personality.Proud => new string[2] { "Nobody talks to me like that!", "I'll not be spoken to like a dog!" }, 
			Personality.Hothead => new string[2] { "That's it! To hell with you!", "I've had enough of you!" }, 
			_ => new string[2] { "I'm done talking!", "Enough of this!" }, 
		});
	}

	private string DoneLine(Personality t)
	{
		return PickLine(t switch
		{
			Personality.Cold => new string[2] { "We're finished, you and me.", "That's all the talk you'll get." }, 
			Personality.Hothead => new string[2] { "I'm done talking to you! You hear me? Done!", "Cut me loose and ask me that again. I dare you." }, 
			_ => new string[2] { "I'm done talking to you.", "Not another word. Not one." }, 
		});
	}

	private string ThatsAllLine(Personality t, bool mourner)
	{
		return PickLine((!mourner) ? (t switch
		{
			Personality.Coward => new string[2] { "...Alright. Alright, mister.", "Yes sir. Good day, sir." }, 
			Personality.Greedy => new string[2] { "Cost me nothin'. Cost you your time.", "Then we're square. For now." }, 
			Personality.Loyal => new string[2] { "Good. Go on, then.", "Then leave us be." }, 
			Personality.Cold => new string[2] { "Then go.", "We're done." }, 
			Personality.Proud => new string[2] { "Then get out of my sight.", "Don't come at me like that again." }, 
			Personality.Hothead => new string[2] { "About damn time.", "Then quit wavin' that thing at me." }, 
			_ => new string[2] { "Suits me.", "Fine by me." }, 
		}) : new string[2] { "Then leave us to bury our own.", "Then let us grieve in peace." });
	}

	private static bool NoTowns(NemesisRecord n)
	{
		return Gangs.ById(n?.Gang)?.NoTowns ?? false;
	}

	private uint PlainModelOf(NemesisRecord rec)
	{
		string[] array = Gangs.ById(rec.Gang)?.TownModels;
		if (array == null || array.Length == 0)
		{
			return 0u;
		}
		if (string.IsNullOrEmpty(rec.PlainModel) || !array.Contains(rec.PlainModel))
		{
			rec.PlainModel = array[Seeds.For(rec, "plain").Next(array.Length)];
		}
		return MISC.GET_HASH_KEY(rec.PlainModel);
	}
}
