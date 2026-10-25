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
	private sealed class Companion
	{
		public string Key;

		public string Name;

		public string Model;

		public NemesisRecord Rec;

		public Ped Ped;

		public Ped Horse;

		public Ped Real;

		public bool IsReal;

		public bool LeftCamp;

		public int FollowAt;

		public int MountAt;

		public int OffHorseSince = -1;

		public int RideAt;

		public int NudgeAt;

		public int Slot;

		public int DismountOrderedAt = -100000;

		public bool WasRiding;

		public bool MaskOn;

		public uint MaskAdded;

		public int MaskCheckAt;

		public int MaskTry;

		public int FightAt;

		public int LastTarget;

		public int Swaps;

		public bool WasFighting;

		public int PullUpAt;

		public int PullUps;

		public uint SavedOutfit;

		public int Scan = -1;

		public int ScanN;

		public int ScanStep;

		public bool InMaskOutfit;

		public Blip Blip;

		public Vector3 Camp;

		public int State;

		public int AskedAt;

		public int NextChat;

		public int LeaveAt;

		public int SpawnFails;

		public long Since;

		public uint RealOutfit;

		public int SavedRel;

		public bool[] SavedFlags;

		public int SavedMaxHp;

		public bool Saved;

		public bool SavedDamageable;

		public bool SavedAttr5;

		public bool SavedAttr46;

		public int SavedAccuracy;

		public List<uint> AddedWeapons = new List<uint>();
	}

	private static readonly int[] SavedFlagIds = new int[3] { 130, 297, 301 };

	private static readonly Dictionary<string, (string model, int willing)> CampPeople = new Dictionary<string, (string, int)>
	{
		["charles"] = ("cs_charlessmith", 90),
		["lenny"] = ("cs_lenny", 90),
		["javier"] = ("cs_javierescuella", 85),
		["john"] = ("cs_johnmarston", 80),
		["sean"] = ("cs_sean", 90),
		["bill"] = ("cs_billwilliamson", 70),
		["sadie"] = ("cs_mrsadler", 85),
		["hosea"] = ("cs_hoseamatthews", 75),
		["karen"] = ("cs_karen", 70),
		["micah"] = ("cs_micahbell", 60),
		["kieran"] = ("cs_kieran", 50),
		["dutch"] = ("cs_dutch", 25),
		["uncle"] = ("cs_uncle", 30),
		["trelawny"] = ("cs_josiahtrelawny", 40),
		["swanson"] = ("cs_revswanson", 20),
		["pearson"] = ("cs_mrpearson", 15),
		["grimshaw"] = ("cs_susangrimshaw", 5),
		["strauss"] = ("cs_leostrauss", 5),
		["abigail"] = ("cs_abigailroberts", 10),
		["tilly"] = ("cs_tilly", 25),
		["marybeth"] = ("cs_marybeth", 25),
		["molly"] = ("cs_mollyoshea", 5),
		["jack"] = ("cs_jackmarston", 0)
	};

	private static readonly Dictionary<string, string[]> Yes = new Dictionary<string, string[]>
	{
		["charles"] = new string[3] { "Sure. I could use the air.", "Alright. Lead the way.", "I'll get my horse." },
		["lenny"] = new string[3] { "Hell yes, Arthur. Let's go!", "Thought you'd never ask.", "Let me grab my hat." },
		["javier"] = new string[3] { "Vámonos, amigo.", "Why not. Dutch won't miss me for a while.", "I'm with you." },
		["john"] = new string[3] { "Fine. Beats sittin' here.", "Alright, Arthur. Don't get us killed.", "Let's go, then." },
		["sean"] = new string[3] { "Now you're talking! Me and you, Arthur - legends!", "Grand. I'll bring the charm.", "About time somebody asked!" },
		["bill"] = new string[3] { "Yeah, alright. Somebody's gotta watch your back.", "Fine. But I ain't cleaning nothing.", "Let's go shoot somethin'." },
		["sadie"] = new string[3] { "Thank God. One more pot of stew and I'd scream.", "Let's ride.", "I'll keep up. Don't you worry." },
		["hosea"] = new string[3] { "An old man's company? Very well.", "Why not. My bones could use the road.", "Lead on, my boy." },
		["karen"] = new string[3] { "Is there whiskey where we're going?", "Sure, sugar. Let's raise some hell.", "Finally, some fun." },
		["micah"] = new string[3] { "Well now, Black Lung wants company. Sure.", "Fine, cowpoke. Try and keep up.", "Why not. Could be profitable." },
		["kieran"] = new string[3] { "M-me? Alright... alright, I'll come.", "If you'll have me, Mister Morgan.", "I won't let you down." },
		["dutch"] = new string[2] { "Alright, Arthur. Let's see what this country holds.", "For you, son - I'll make the time." },
		["uncle"] = new string[2] { "I'll come... but I ain't doing no lifting.", "Fine, fine. My lumbago and me." },
		[""] = new string[3] { "Alright.", "Sure, let's go.", "Lead the way." }
	};

	private static readonly Dictionary<string, string[]> No = new Dictionary<string, string[]>
	{
		["dutch"] = new string[2] { "Not now, Arthur. I've got plans to make.", "Later, son. I need to think." },
		["uncle"] = new string[2] { "My lumbago, Arthur. You know how it is.", "I'd love to... but I just sat down." },
		["micah"] = new string[2] { "Ride with you? Pass.", "I got my own business, Morgan." },
		["grimshaw"] = new string[2] { "Somebody has to run this camp, Mister Morgan.", "Don't be ridiculous." },
		["pearson"] = new string[2] { "And who'll feed this lot? No, sir.", "I've a stew on, Arthur." },
		["strauss"] = new string[2] { "I have accounts to settle, Mister Morgan.", "No, no. Business." },
		["abigail"] = new string[2] { "I got Jack to look after.", "Ask John." },
		["molly"] = new string[2] { "With you? Don't be absurd.", "I'm waiting for Dutch." },
		["jack"] = new string[1] { "Ma says I can't." },
		[""] = new string[3] { "Not today, Arthur.", "Maybe another time.", "I got work to do here." }
	};

	private static readonly Dictionary<string, string[]> Chat = new Dictionary<string, string[]>
	{
		["charles"] = new string[4] { "Tracks here. Deer, a day old.", "Quiet out here. I like it.", "Weather's turning.", "You ever think about leaving all this?" },
		["lenny"] = new string[4] { "You think Dutch's got a plan? A real one?", "I read about this country in a book once.", "Race you to the next ridge!", "This is the life, Arthur." },
		["javier"] = new string[3] { "Reminds me of home, a little.", "I'd play the guitar if I'd brought it.", "Dutch will see us through. He always does." },
		["john"] = new string[3] { "Abigail's on at me again.", "Don't look at me like that.", "Some days I think about runnin'." },
		["sean"] = new string[3] { "Did I ever tell you about me da?", "We should rob a train. A big one.", "Ireland's got nothing on these hills. Well, maybe a bit." },
		["bill"] = new string[3] { "I could eat a whole horse.", "You think I'm stupid, don't you.", "Army taught me one thing - keep your powder dry." },
		["sadie"] = new string[3] { "Jake would've liked this view.", "I can shoot as well as any of you.", "Don't treat me soft, Arthur." },
		["hosea"] = new string[3] { "Dutch and I rode roads like this, years ago.", "Mind the man, not the gun.", "You're a better man than you think, Arthur." },
		["karen"] = new string[3] { "Somebody's gotta have a drink.", "I'm bored. Let's rob somebody.", "You're handsome when you scowl." },
		["micah"] = new string[3] { "This land's ripe for the picking.", "You and me, Morgan - we ain't so different.", "Smell that? Opportunity." },
		["kieran"] = new string[3] { "I'm grateful, Mister Morgan. Truly.", "The O'Driscolls'd skin me if they saw me.", "I like horses better than people." },
		["dutch"] = new string[3] { "We just need a little more money.", "Have faith, Arthur.", "This country is changing, son." },
		[""] = new string[3] { "Fine day.", "Quiet out here.", "Long way from camp." }
	};

	private static readonly string[] AgreeVoice = new string[5] { "GREET_GENERAL_FAMILIAR", "GREET_GENERAL", "GENERIC_AGREE", "HOWS_IT_GOING", "GENERIC_THANKS" };

	private static readonly string[] RefuseVoice = new string[8] { "GENERIC_INSULT_MED_NEUTRAL", "GENERIC_INSULT_HIGH_NEUTRAL", "GENERIC_INSULT_LOW_NEUTRAL", "INSULT_RESPONSE", "GENERIC_ANTAGONIZE", "ANTAGONIZE", "GENERIC_DISMISS", "GENERIC_NO" };

	private static readonly HashSet<string> LoreRiders = new HashSet<string>
	{
		"sadie", "john", "charles", "kieran", "javier", "hosea", "bill", "micah", "sean", "uncle",
		"lenny"
	};

	private static readonly HashSet<string> NoRidePrompt = new HashSet<string> { "jack" };

	private bool _kieranSeenFree;

	private static readonly string[] FightLines = new string[5] { "Get down!", "Behind you, Arthur!", "I got this one!", "They're everywhere!", "Keep shootin'!" };

	private static readonly string[] HurtLines = new string[3] { "I'm hit! I'm out, Arthur!", "Argh - I'm done, I'm done! I'll see you at camp!", "They got me! I'm pullin' back!" };

	private static readonly string[] HomeLines = new string[3] { "I'll see you back at camp.", "Good riding with you, Arthur.", "Same time next week." };

	private const int MaxCompanions = 3;

	private readonly List<Companion> _comps = new List<Companion>();

	private readonly Dictionary<string, int> _saidNo = new Dictionary<string, int>();

	private int _pTalk;

	private int _pTalkFor;

	private int _pTalkGroup;

	private bool _talkArmed;

	private int _talkAt;

	private int _pAsk;

	private int _pAskFor;

	private int _pAskGroup;

	private int _nextCompScan;

	private bool _askArmed;

	private Ped _askPed;

	private string _askKey;

	private Companion _askComp;

	private int _youOffSince = -1;

	internal const bool CompanionsAvailable = false;

	private bool _companionsOffDone;

	private static Dictionary<uint, string> _campKeyOfModel;

	private int _nextGiverScan;

	private static readonly string[] GiverLines = new string[4] { "Looks like {0}'s got something for you. I'll head back.", "That's {0} - gang business. I'll see you at camp.", "{0}'s waiting on you. I'll get out of the way.", "You go see {0}. I'll ride on back." };

	private static readonly (float x, float y)[] RideSlots = new(float, float)[3]
	{
		(2.5f, -3.5f),
		(-2.5f, -3.5f),
		(0f, -6.5f)
	};

	private int _threatScanAt;

	private bool _threatNear;

	internal bool IsCompanionReal(int h)
	{
		return _comps.Any((Companion c) => c.IsReal && c.Ped != null && c.Ped.Exists() && c.Ped.Handle == h);
	}

	private HashSet<string> NeverRide => Chronicle.CompanionNever ?? (Chronicle.CompanionNever = new HashSet<string>());

	private bool KieranTied
	{
		get
		{
			if (Chronicle.StoryChapter < 3)
			{
				return !_kieranSeenFree;
			}
			return false;
		}
	}

	private bool IsCompPed(Companion c, int h)
	{
		if (!(c.Ped != null) || !c.Ped.Exists() || c.Ped.Handle != h)
		{
			if (c.Real != null && c.Real.Exists())
			{
				return c.Real.Handle == h;
			}
			return false;
		}
		return true;
	}

	internal bool IsCompanion(int h)
	{
		return _comps.Any((Companion c) => c.State != 2 && IsCompPed(c, h));
	}

	internal string CompanionName(int h)
	{
		return _comps.FirstOrDefault((Companion c) => IsCompPed(c, h))?.Name;
	}

	internal bool IsCompanionKiller(int src)
	{
		if (src != 0)
		{
			return _comps.Any((Companion c) => c.State == 1 && c.Ped != null && c.Ped.Exists() && c.Ped.Handle == src);
		}
		return false;
	}

	internal IEnumerable<Ped> CompanionPeds()
	{
		foreach (Companion c in _comps)
		{
			if (c.Ped != null)
			{
				yield return c.Ped;
			}
			if (c.Horse != null)
			{
				yield return c.Horse;
			}
		}
	}

	private Dictionary<string, long> CompHurt => Chronicle.CompanionHurt ?? (Chronicle.CompanionHurt = new Dictionary<string, long>());

	private void UpdateCompanion(Ped player)
	{
		if (_companionsOffDone)
		{
			return;
		}
		_companionsOffDone = true;
		try
		{
			if (_comps.Count > 0)
			{
				LetCompanionsGo("riding together is off");
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("COMPANION off: send-home failed: " + ex.Message);
		}
		try
		{
			HideAskPrompt();
		}
		catch
		{
		}
		GrudgeLog("COMPANION riding together is off in this build (Companion.cs CompanionsAvailable)");
	}

	private static Dictionary<uint, string> CampKeyOfModel
	{
		get
		{
			if (_campKeyOfModel != null)
			{
				return _campKeyOfModel;
			}
			Dictionary<uint, string> dictionary = new Dictionary<uint, string>();
			foreach (KeyValuePair<string, (string, int)> campPerson in CampPeople)
			{
				dictionary[Joaat.Of(campPerson.Value.Item1)] = campPerson.Key;
			}
			return _campKeyOfModel = dictionary;
		}
	}

	private void MissionGiverNear(Ped player)
	{
		if (Game.GameTime < _nextGiverScan || !_comps.Any((Companion c) => c.State == 1))
		{
			return;
		}
		_nextGiverScan = Game.GameTime + 1000;
		if (AtGangCamp(player))
		{
			return;
		}
		HashSet<int> hashSet = new HashSet<int>(from c in _comps
			where c.Ped != null && c.Ped.Exists()
			select c.Ped.Handle);
		Vector3 position = player.Position;
		Ped ped = null;
		string who = null;
		foreach (var (ped2, vector) in PedSnapshot(player))
		{
			if (!(ped2 == null) && !((vector - position).Length() >= 45f) && !hashSet.Contains(ped2.Handle) && ped2.Exists() && !ped2.IsDead && CampKeyOfModel.TryGetValue((uint)ped2.Model.Hash, out who) && !IsOurs(ped2.Handle))
			{
				ped = ped2;
				break;
			}
		}
		if (ped == null)
		{
			return;
		}
		who = who ?? "someone";
		string text = StoryRoster.FirstOrDefault((NemesisRecord r) => r.Id == "vdl_" + who)?.Name ?? who;
		GrudgeLog($"COMPANION mission giver near: {text} ({ped.Position.DistanceTo(player.Position):0} m) - everyone riding with you heads back to camp");
		bool flag = true;
		foreach (Companion item in _comps.Where((Companion c) => c.State == 1).ToList())
		{
			if (flag && item.Ped != null && item.Ped.Exists())
			{
				ShowSubtitle(item.Name.Split(' ')[0] + ": \"" + string.Format(GiverLines[_rng.Next(GiverLines.Length)], text.Split(' ')[0]) + "\"");
			}
			flag = false;
			SendHome(item, text + " has work for you");
		}
	}

	private void UpdateAsk(Ped player)
	{
		if (_menu.IsOpen || _convo != null || _tg != null || player.IsInCombat)
		{
			HideAskPrompt();
			return;
		}
		if (Game.GameTime >= _nextCompScan)
		{
			_nextCompScan = Game.GameTime + 400;
			_askPed = null;
			_askKey = null;
			_askComp = null;
			if (!player.IsOnMount)
			{
				foreach (Companion comp in _comps)
				{
					if (comp.State == 1 && comp.Ped != null && comp.Ped.Exists() && comp.Ped.Position.DistanceTo(player.Position) < 3.2f)
					{
						_askPed = comp.Ped;
						_askKey = comp.Key;
						_askComp = comp;
						break;
					}
				}
			}
			if (_askPed == null && _comps.Count((Companion c) => c.State != 2) < 3 && AtGangCamp(player))
			{
				Vector3 position = player.Position;
				Dictionary<string, Ped> dictionary = new Dictionary<string, Ped>();
				Ped ped = null;
				foreach (var (ped2, vector) in PedSnapshot(player))
				{
					if (!(ped2 == null) && CampKeyOfModel.TryGetValue((uint)ped2.Model.Hash, out var value))
					{
						if (value == "kieran" && ped == null && ped2.Exists() && !ped2.IsDead)
						{
							ped = ped2;
						}
						if ((vector - position).Length() < 3.4f && ped2.Exists() && !ped2.IsDead && !dictionary.ContainsKey(value))
						{
							dictionary[value] = ped2;
						}
					}
				}
				foreach (KeyValuePair<string, (string, int)> kv in CampPeople)
				{
					if (_comps.Any((Companion c) => c.Key == kv.Key) || StoryCompanionBlock(kv.Key) != null || NeverRide.Contains(kv.Key) || NoRidePrompt.Contains(kv.Key))
					{
						continue;
					}
					if (kv.Key == "kieran" && !_kieranSeenFree)
					{
						Ped ped3 = ped;
						if (ped3 != null && ENTITY.GET_ENTITY_VELOCITY(ped3.Handle, 0).Length() > 0.8f && !ped3.IsHogtied)
						{
							_kieranSeenFree = true;
							GrudgeLog("COMPANION Kieran is walking about camp - free to ride");
						}
					}
					if ((!(kv.Key == "kieran") || !KieranTied) && dictionary.TryGetValue(kv.Key, out var value2) && !(value2.Position.DistanceTo(player.Position) >= 3f) && !IsOurs(value2.Handle))
					{
						_askPed = value2;
						_askKey = kv.Key;
						break;
					}
				}
			}
		}
		Ped askPed = _askPed;
		if (askPed == null || !askPed.Exists() || askPed.Position.DistanceTo(player.Position) > 3.5f)
		{
			HideAskPrompt();
			return;
		}
		bool flag = _askComp != null;
		int num = HUD._UI_PROMPT_GET_GROUP_ID_FOR_TARGET_ENTITY(askPed.Handle);
		if (_pAsk == 0 || !PromptValid(_pAsk) || _pAskFor != askPed.Handle || _pAskGroup != num)
		{
			Crumbs.Native("Companion prompt delete");
			if (PromptValid(_pAsk))
			{
				HUD._UI_PROMPT_DELETE(_pAsk);
			}
			_pAsk = MakePrompt(eInputType.InteractOption1, flag ? "Send Home" : "Ride With Me", hold: true, num);
			_pAskFor = askPed.Handle;
			_pAskGroup = num;
		}
		HUD._UI_PROMPT_SET_VISIBLE(_pAsk, toggle: true);
		HUD._UI_PROMPT_SET_ENABLED(_pAsk, toggle: true);
		HideTalkPrompt();
		if (!HUD._UI_PROMPT_HAS_HOLD_MODE_COMPLETED(_pAsk))
		{
			_askArmed = true;
		}
		else if (_askArmed)
		{
			_askArmed = false;
			Companion askComp = _askComp;
			HideAskPrompt();
			if (flag)
			{
				SendHome(askComp, "you sent him home");
			}
			else
			{
				Ask(askPed, _askKey, player);
			}
		}
	}

	private void HideTalkPrompt()
	{
		if (_pTalk != 0)
		{
			Crumbs.Native("Companion prompt delete");
			if (PromptValid(_pTalk))
			{
				HUD._UI_PROMPT_DELETE(_pTalk);
			}
			_pTalk = 0;
			_pTalkFor = 0;
		}
	}

	private void HideAskPrompt()
	{
		HideTalkPrompt();
		if (_pAsk != 0)
		{
			Crumbs.Native("Companion prompt delete");
			if (PromptValid(_pAsk))
			{
				HUD._UI_PROMPT_DELETE(_pAsk);
			}
			_pAsk = 0;
			_pAskFor = 0;
		}
	}

	private static string Line(Dictionary<string, string[]> pool, string key, Random rng)
	{
		string[] value;
		string[] array = (pool.TryGetValue(key ?? "", out value) ? value : pool[""]);
		return array[rng.Next(array.Length)];
	}

	private void Ask(Ped ped, string key, Ped player)
	{
		NemesisRecord nemesisRecord = StoryRoster.FirstOrDefault((NemesisRecord r) => r.Id == "vdl_" + key);
		string text = nemesisRecord?.Name ?? StoryCast.People.Values.FirstOrDefault((StoryCast.Person p) => p.Name != null && p.Name.ToLowerInvariant().Contains(key))?.Name ?? key;
		string text2 = text.Split(' ')[0];
		TASK.TASK_TURN_PED_TO_FACE_ENTITY(ped.Handle, player.Handle, 1500, 0f, 0f, 0f);
		long num = GameMinutesNow();
		if (CompHurt.TryGetValue(key, out var value) && value > num)
		{
			ShowSubtitle(text2 + ": \"Still healing up from last time, Arthur. Give me a day.\"");
			GrudgeLog($"COMPANION {text} is laid up ({(value - num) / 60}h left)");
			return;
		}
		if (_saidNo.TryGetValue(key, out var value2) && Game.GameTime - value2 < 240000)
		{
			ShowSubtitle(text2 + ": \"" + ((RegardOf(key) < 0) ? "I said no." : "I told you, Arthur - not now.") + "\"");
			return;
		}
		List<Companion> list = _comps.Where((Companion c) => c.State != 2).ToList();
		bool flag = LoreRiders.Contains(key);
		int num2 = (flag ? ((key == "uncle") ? 50 : 100) : 0);
		bool flag2 = _rng.Next(100) < num2;
		if (!flag)
		{
			NeverRide.Add(key);
			SaveWorld();
		}
		string text3 = ((list.Count > 0 && flag2 && TieBetween(key, list[0].Key) <= -30) ? (" ...With " + list[0].Name.Split(' ')[0] + "? Fine. Whatever.") : "");
		ShowSubtitle(text2 + ": \"" + Line(flag2 ? Yes : No, key, _rng) + text3 + "\"");
		SayAny(ped, flag2 ? "agree2" : "refuse2", flag2 ? AgreeVoice : RefuseVoice, player);
		GrudgeLog(string.Format("COMPANION asked {0}: {1} ({2}%{3}, {4} already with you)", text, flag2 ? "yes" : "no", num2, flag ? "" : ", doesn't ride - no prompt from now on", list.Count));
		if (!flag2)
		{
			_saidNo[key] = Game.GameTime;
			return;
		}
		Companion companion = new Companion
		{
			Key = key,
			Name = text,
			Model = CampPeople[key].model,
			Rec = nemesisRecord,
			Camp = player.Position,
			State = 0,
			AskedAt = Game.GameTime,
			Real = ped,
			Slot = _comps.Count
		};
		_comps.Add(companion);
		int groupId = PED.GET_PED_GROUP_INDEX(player.Handle);
		PED.SET_PED_AS_GROUP_MEMBER(ped.Handle, groupId);
		PED.SET_GROUP_SEPARATION_RANGE(groupId, 400f);
		TASK.CLEAR_PED_TASKS(ped.Handle, p1: true, p2: false);
		TASK.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(ped.Handle, player.Handle, 1.2f, -1.5f, 0f, 1.6f, -1, 1.5f, persistFollowing: true, p9: false, walkOnly: false, p11: false, p12: false, p13: false);
		GrudgeLog($"COMPANION {text}: the real one follows you from camp (group member: {PED.IS_PED_GROUP_MEMBER(ped.Handle, groupId, p2: false)})");
		Adopt(companion, ped, player);
	}

	private int TieBetween(string a, string b)
	{
		return (StoryRoster.FirstOrDefault((NemesisRecord r) => r.Id == "vdl_" + a)?.Ties?.FirstOrDefault((Tie x) => x.Id == "vdl_" + b))?.Strength ?? 0;
	}

	private void WaitToJoin(Companion c, Ped player)
	{
		Ped real = c.Real;
		bool flag = real != null && real.Exists() && !real.IsDead;
		if (flag && Game.GameTime >= c.NudgeAt)
		{
			c.NudgeAt = Game.GameTime + 2500;
			float num = real.Position.DistanceTo(player.Position);
			int groupId = PED.GET_PED_GROUP_INDEX(player.Handle);
			if (!PED.IS_PED_GROUP_MEMBER(real.Handle, groupId, p2: false))
			{
				PED.SET_PED_AS_GROUP_MEMBER(real.Handle, groupId);
			}
			if (num > 4f && !TASK.IS_PED_RUNNING(real.Handle) && !TASK.IS_PED_WALKING(real.Handle))
			{
				TASK.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(real.Handle, player.Handle, 1.2f, -1.5f, 0f, (num > 20f) ? 3f : 1.6f, -1, 1.5f, persistFollowing: true, p9: false, walkOnly: false, p11: false, p12: false, p13: false);
			}
			if (player.Position.DistanceTo(c.Camp) > 110f && num < 60f && !WorldBusy(player))
			{
				Adopt(c, real, player);
				return;
			}
		}
		if (Game.GameTime - c.AskedAt > 600000)
		{
			GrudgeLog("COMPANION " + c.Name + " gave up waiting");
			ReleaseReal(c);
			_comps.Remove(c);
		}
		else if (!(player.Position.DistanceTo(c.Camp) < 150f) && !WorldBusy(player))
		{
			if (flag)
			{
				GrudgeLog($"COMPANION {c.Name}: the real one stayed {real.Position.DistanceTo(player.Position):0} m back - his copy rides out");
				ReleaseReal(c);
			}
			if (!SpawnCompanion(c, player) && ++c.SpawnFails > 20)
			{
				GrudgeLog("COMPANION " + c.Name + " couldn't be placed - stays in camp");
				_comps.Remove(c);
			}
		}
	}

	private void Adopt(Companion c, Ped ped, Ped player)
	{
		MakeOurs(ped.Handle);
		c.SavedRel = (int)PED.GET_PED_RELATIONSHIP_GROUP_HASH(ped.Handle);
		c.SavedFlags = SavedFlagIds.Select((int f) => Function.Call<bool>(9143768600959694099uL, new InputArgument[3] { ped.Handle, f, true })).ToArray();
		c.SavedMaxHp = ped.MaxHealth;
		c.SavedDamageable = ENTITY._GET_ENTITY_CAN_BE_DAMAGED(ped.Handle);
		c.SavedAccuracy = PED.GET_PED_ACCURACY(ped.Handle);
		c.SavedAttr5 = PED._GET_PED_COMBAT_ATTRIBUTE(ped.Handle, 5);
		c.SavedAttr46 = PED._GET_PED_COMBAT_ATTRIBUTE(ped.Handle, 46);
		try
		{
			Crumbs.Native("Companion outfit read");
			c.RealOutfit = PED._GET_PED_META_OUTFIT_HASH(ped.Handle);
		}
		catch
		{
			c.RealOutfit = 0u;
		}
		c.Saved = true;
		ENTITY.SET_ENTITY_INVINCIBLE(ped.Handle, toggle: true);
		c.Ped = ped;
		c.IsReal = true;
		Setup(c, ped, player);
		GrudgeLog("COMPANION " + c.Name + " rides with you (the real one)");
	}

	private bool RealHandsOff(Companion c, Ped p)
	{
		if (!c.IsReal || p == null || !p.Exists())
		{
			return false;
		}
		bool flag = false;
		try
		{
			flag = MISC.GET_MISSION_FLAG();
		}
		catch
		{
		}
		if (!flag)
		{
			return !BelongsToUs(p.Handle);
		}
		return true;
	}

	private void ReleaseReal(Companion c)
	{
		Ped real = c.Real;
		if (!(real == null) && real.Exists())
		{
			bool flag = false;
			try
			{
				flag = MISC.GET_MISSION_FLAG();
			}
			catch
			{
			}
			if (flag || (c.IsReal && !BelongsToUs(real.Handle)))
			{
				c.Real = null;
				return;
			}
			PED.REMOVE_PED_FROM_GROUP(real.Handle);
			TASK.CLEAR_PED_TASKS(real.Handle, p1: true, p2: false);
			c.Real = null;
		}
	}

	private unsafe void Setup(Companion c, Ped ped, Ped player)
	{
		int groupId = PED.GET_PED_GROUP_INDEX(player.Handle);
		ped.RelationshipGroup = player.RelationshipGroup;
		PED.SET_PED_CONFIG_FLAG(ped.Handle, 130, value: false);
		PED.SET_PED_CONFIG_FLAG(ped.Handle, 297, value: true);
		PED.SET_PED_CONFIG_FLAG(ped.Handle, 301, value: false);
		if (!PED.IS_PED_GROUP_MEMBER(ped.Handle, groupId, p2: false))
		{
			PED.SET_PED_AS_GROUP_MEMBER(ped.Handle, groupId);
		}
		PED.SET_PED_CAN_TELEPORT_TO_GROUP_LEADER(ped.Handle, groupId, toggle: false);
		PED.SET_GROUP_SEPARATION_RANGE(groupId, 400f);
		if (ped.MaxHealth < 400)
		{
			ped.MaxHealth = 400;
			ped.Health = 400;
		}
		PED.SET_PED_ACCURACY(ped.Handle, 55);
		PED.SET_PED_COMBAT_ATTRIBUTES(ped.Handle, 5, enabled: true);
		PED.SET_PED_COMBAT_ATTRIBUTES(ped.Handle, 46, enabled: true);
		Give(eWeapon.RepeaterCarbine, 120, eWeaponAttachPoint.Rifle, equip: false, left: true);
		Give(eWeapon.RevolverCattleman, 60, eWeaponAttachPoint.PistolR, equip: true, left: false);
		if (c.Key == "charles")
		{
			Give(eWeapon.Bow, 20, eWeaponAttachPoint.Bow, equip: false, left: true);
		}
		if (c.Horse == null || !c.Horse.Exists())
		{
			Vector3 pos = ped.Position + new Vector3((float)Math.Sin((double)ped.Heading * Math.PI / 180.0) * -3.5f, (float)Math.Cos((double)ped.Heading * Math.PI / 180.0) * -3.5f, 0f);
			int num = PED.GET_MOUNT(player.Handle);
			if (num == 0)
			{
				num = PED._GET_LAST_MOUNT(player.Handle);
			}
			if (num != 0 && ENTITY.DOES_ENTITY_EXIST(num) && !ENTITY.IS_ENTITY_DEAD(num) && ENTITY.GET_ENTITY_COORDS(num, alive: true, realCoords: true).DistanceTo(player.Position) < 120f)
			{
				Vector3 vector = ENTITY.GET_ENTITY_COORDS(num, alive: true, realCoords: true);
				float num2 = ENTITY.GET_ENTITY_HEADING(num) * (float)Math.PI / 180f;
				Vector3 vector2 = new Vector3((float)Math.Cos(num2), (float)Math.Sin(num2), 0f);
				Vector3 vector3 = new Vector3((float)Math.Sin(num2), 0f - (float)Math.Cos(num2), 0f);
				pos = vector + vector2 * (2.2f + 1.8f * (float)c.Slot) + vector3 * (0.8f * (float)c.Slot);
			}
			float z = 0f;
			if (MISC.GET_GROUND_Z_FOR_3D_COORD(pos.X, pos.Y, pos.Z + 2f, &z, p4: false))
			{
				pos.Z = z;
			}
			Ped ped2 = SpawnPed(RiderHorses[_rng.Next(RiderHorses.Length)], pos, ped.Heading);
			if (ped2 != null)
			{
				MakeOurs(ped2.Handle);
				TackUp(ped2);
				c.Horse = ped2;
				Crumbs.Native("Companion onto mount");
				if (player.IsOnMount && !c.IsReal)
				{
					PED.SET_PED_ONTO_MOUNT(ped.Handle, ped2.Handle, -1, p3: true);
				}
				GrudgeLog($"COMPANION {c.Name}: his horse is ready ({pos.DistanceTo(ped.Position):0.0} m from him, {pos.DistanceTo(player.Position):0} m from you)");
			}
		}
		if (c.Blip == null || !c.Blip.Exists())
		{
			Crumbs.Native("Companion blip add (V2)");
			c.Blip = ped.AddBlip(BlipType.BLIP_STYLE_FRIENDLY);
			if (c.Blip != null)
			{
				Function.Call(11290983183885202434uL, c.Blip.Handle, new InputArgument(UiDraw.ForeverVarString(c.Name)));
			}
		}
		c.State = 1;
		c.Since = GameMinutesNow();
		c.NextChat = Game.GameTime + 40000 + _rng.Next(40000);
		c.MaskOn = false;
		c.MaskAdded = 0u;
		Feed("COMPANION " + c.Name + " rides with you", Theme.Good);
		void Give(eWeapon w, int ammo, eWeaponAttachPoint at, bool equip, bool left)
		{
			if (!c.IsReal || !Function.Call<bool>(10226742572059207868uL, new InputArgument[4]
			{
				ped.Handle,
				(uint)w,
				0,
				false
			}))
			{
				ped.GiveWeapon(w, ammo, at, equip, left);
				if (c.IsReal)
				{
					c.AddedWeapons.Add((uint)w);
				}
			}
		}
	}

	private bool SpawnCompanion(Companion c, Ped player)
	{
		Vector3 vector = OutOfSightSpot(player, 35f, 60f, ahead: false, allowSeen: true);
		if (vector == Vector3.Zero)
		{
			return false;
		}
		Model m = new Model(MISC.GET_HASH_KEY(c.Model));
		if (!m.IsValid || !m.IsPed)
		{
			GrudgeLog("COMPANION model " + c.Model + " not found");
			_comps.Remove(c);
			return false;
		}
		if (!LoadModelSafe(m, 3000))
		{
			return false;
		}
		Ped ped = SpawnPed((PedHash)m.Hash, vector, player.Heading);
		STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)m.Hash);
		if (ped == null)
		{
			return false;
		}
		NoteModel(ped);
		MakeOurs(ped.Handle);
		Crumbs.Native("Companion prompt name");
		Function.Call(5352729231319872684uL, ped.Handle, UiDraw.ForeverString(c.Name));
		c.Ped = ped;
		c.IsReal = false;
		if (c.Horse != null && c.Horse.Exists() && !c.Horse.IsDead)
		{
			c.Horse.Position = vector + new Vector3(2.5f, 0f, 0f);
		}
		Setup(c, ped, player);
		Crumbs.Native("Companion onto mount");
		if (player.IsOnMount && c.Horse != null && c.Horse.Exists())
		{
			PED.SET_PED_ONTO_MOUNT(ped.Handle, c.Horse.Handle, -1, p3: true);
		}
		GrudgeLog(string.Format("COMPANION {0} joined you at {1:0},{2:0} ({3}, {4:0} m back)", c.Name, vector.X, vector.Y, player.IsOnMount ? "mounted" : "on foot", vector.DistanceTo(player.Position)));
		return true;
	}

	private unsafe void Ride(Companion c, Ped player)
	{
		Ped ped = c.Ped;
		if (ped == null || !ped.Exists())
		{
			if (c.Swaps < 3 && !WorldBusy(player))
			{
				c.Swaps++;
				c.Ped = null;
				c.IsReal = false;
				if (SpawnCompanion(c, player))
				{
					GrudgeLog("COMPANION " + c.Name + ": the game took the real one - his copy rides on");
					return;
				}
			}
			GrudgeLog("COMPANION " + c.Name + "'s ped is gone");
			Cleanup(c);
			_comps.Remove(c);
			return;
		}
		float num = player.Position.DistanceTo(c.Camp);
		if (!c.LeftCamp && num > 130f)
		{
			c.LeftCamp = true;
			GrudgeLog("COMPANION " + c.Name + " left camp with you");
		}
		if (c.LeftCamp && num < 90f)
		{
			SendHome(c, "back at camp");
			return;
		}
		if (ped.IsDead || (float)ped.Health < (float)ped.MaxHealth * 0.3f)
		{
			ENTITY.SET_ENTITY_INVINCIBLE(ped.Handle, toggle: true);
			ShowSubtitle(c.Name.Split(' ')[0] + ": \"" + HurtLines[_rng.Next(HurtLines.Length)] + "\"");
			CompHurt[c.Key] = GameMinutesNow() + 1440;
			PED.REMOVE_PED_FROM_GROUP(ped.Handle);
			if (c.IsReal)
			{
				HeadHome(c, 3f);
			}
			else
			{
				Flee(ped, player, 200f, -1, 3f);
			}
			c.State = 2;
			c.LeaveAt = Game.GameTime;
			if (c.Blip != null && c.Blip.Exists())
			{
				c.Blip.Delete();
			}
			GrudgeLog("COMPANION " + c.Name + " was hurt - laid up for a day");
			Feed("COMPANION " + c.Name + " was hurt and rode back to camp", Theme.Warn);
			Chronicle_Ride(c, hurt: true);
			return;
		}
		Crumbs.Mark("companion " + c.Key + " fight");
		CompanionFight(c, player);
		Crumbs.Mark("companion " + c.Key + " ride");
		if (Game.GameTime >= c.RideAt)
		{
			c.RideAt = Game.GameTime + 1500;
			float num2 = ped.Position.DistanceTo(player.Position);
			bool flag = ped.IsInCombat || player.IsInCombat;
			if (c.Horse != null && c.Horse.Exists() && !c.Horse.IsDead && !flag)
			{
				bool flag2 = !ped.IsOnMount && PED.GET_MOUNT(ped.Handle) == 0;
				if (flag2 && c.WasRiding && Game.GameTime - c.DismountOrderedAt > 4000)
				{
					GrudgeLog(Pron.Her(string.Format("COMPANION {0}: came off his horse - not our order ({1}, you {2}, {3:0} m, horse {4})", c.Name, ped.IsRagdoll ? "ragdoll: a fall" : "no ragdoll", player.IsOnMount ? "riding" : "on foot", ped.Position.DistanceTo(player.Position), c.Horse.IsDead ? "dead" : $"{c.Horse.Position.DistanceTo(ped.Position):0} m from him"), !PED.IS_PED_MALE(ped.Handle)));
				}
				c.WasRiding = !flag2;
				if (!flag2)
				{
					c.OffHorseSince = -1;
				}
				else if (c.OffHorseSince < 0)
				{
					c.OffHorseSince = Game.GameTime;
				}
				if (player.IsOnMount && flag2 && Game.GameTime - c.OffHorseSince > 2000)
				{
					if (Game.GameTime >= c.MountAt)
					{
						c.MountAt = Game.GameTime + 8000;
						float num3 = c.Horse.Position.DistanceTo(ped.Position);
						if (num3 > 40f)
						{
							Vector3 vector = ped.Position + new Vector3((float)Math.Sin((double)ped.Heading * Math.PI / 180.0) * -4f, (float)Math.Cos((double)ped.Heading * Math.PI / 180.0) * -4f, 0f);
							float z = 0f;
							if (MISC.GET_GROUND_Z_FOR_3D_COORD(vector.X, vector.Y, vector.Z + 2f, &z, p4: false))
							{
								vector.Z = z;
							}
							if (!ScreenSees(vector))
							{
								c.Horse.Position = vector;
							}
							else
							{
								TASK.TASK_GO_TO_ENTITY(c.Horse.Handle, ped.Handle, -1, 3f, 2.5f, 0f, 0);
							}
							GrudgeLog($"COMPANION {c.Name}: his horse was {num3:0} m off - it comes to him");
						}
						TASK.TASK_MOUNT_ANIMAL(ped.Handle, c.Horse.Handle, 20000, -1, 2f, 1, 0uL, 0uL);
						GrudgeLog($"COMPANION {c.Name}: you mounted - he goes for his horse ({c.Horse.Position.DistanceTo(ped.Position):0} m)");
					}
				}
				else if (!player.IsOnMount && ped.IsOnMount && num2 < 15f && _youOffSince > 0 && Game.GameTime - _youOffSince > 2500)
				{
					TASK.TASK_DISMOUNT_ANIMAL(ped.Handle, 0, 0uL, 0uL, 0uL, 0);
					c.DismountOrderedAt = Game.GameTime;
					GrudgeLog($"COMPANION {c.Name}: you got down - he gets down too ({num2:0} m)");
				}
				if (player.IsOnMount && ped.IsOnMount && num2 > 25f && Game.GameTime >= c.FollowAt)
				{
					c.FollowAt = Game.GameTime + 10000;
					(float, float) tuple = RideSlots[Math.Min(RideSlots.Length - 1, Math.Max(0, c.Slot))];
					TASK.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(ped.Handle, player.Handle, tuple.Item1, tuple.Item2, 0f, (num2 > 40f) ? 4f : 2.5f, -1, 3f, persistFollowing: true, p9: false, walkOnly: false, p11: false, p12: false, p13: false);
				}
			}
			if (num2 > 180f && !flag)
			{
				Vector3 vector2 = OutOfSightSpot(player, 35f, 60f, ahead: false, allowSeen: true);
				if (vector2 != Vector3.Zero)
				{
					if (ped.IsOnMount && c.Horse != null && c.Horse.Exists())
					{
						c.Horse.Position = vector2;
						ped.Position = vector2 + new Vector3(0f, 0f, 1f);
						Crumbs.Native("Companion onto mount");
						PED.SET_PED_ONTO_MOUNT(ped.Handle, c.Horse.Handle, -1, p3: true);
					}
					else
					{
						ped.Position = vector2;
					}
					GrudgeLog($"COMPANION {c.Name} caught up (was {num2:0} m behind)");
				}
			}
		}
		if (Game.GameTime >= c.NextChat)
		{
			bool flag3 = player.IsInCombat || ped.IsInCombat;
			c.NextChat = Game.GameTime + (flag3 ? (12000 + _rng.Next(10000)) : (70000 + _rng.Next(70000)));
			if (ped.Position.DistanceTo(player.Position) < 25f && Game.GameTime >= _banterBusyUntil)
			{
				ShowSubtitle(c.Name.Split(' ')[0] + ": \"" + (flag3 ? FightLines[_rng.Next(FightLines.Length)] : Line(Chat, c.Key, _rng)) + "\"");
			}
		}
		Crumbs.Mark("companion " + c.Key + " idle");
		CompanionIdle(c, player);
	}

	private unsafe void CompanionFight(Companion c, Ped player)
	{
		if (Game.GameTime < c.FightAt)
		{
			return;
		}
		c.FightAt = Game.GameTime + 800;
		Ped ped = c.Ped;
		if (Game.GameTime >= _threatScanAt)
		{
			_threatScanAt = Game.GameTime + 800;
			_threatNear = player.IsInCombat || PedsAround(player).Any((Ped p) => p != null && p.Exists() && !p.IsDead && p.IsHuman && !IsOurs(p.Handle) && p.Position.DistanceTo(player.Position) < 100f && PED.IS_PED_IN_COMBAT(p.Handle, player.Handle));
		}
		int num = 0;
		if (GunMelee.PlayerFiredRecently && PLAYER.GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(Game.Player.Handle, &num) && num != 0 && num != c.LastTarget && ENTITY.IS_ENTITY_A_PED(num) && !ENTITY.IS_ENTITY_DEAD(num) && !IsOurs(num) && PED.IS_PED_HUMAN(num) && (!ENTITY.IS_ENTITY_A_MISSION_ENTITY(num) || BelongsToUs(num)) && !GameNamed(Models.Name(ENTITY.GET_ENTITY_MODEL(num))))
		{
			c.LastTarget = num;
			TASK.TASK_COMBAT_PED(ped.Handle, num, 0, 16);
			GrudgeLog($"COMPANION {c.Name}: takes your target (ped {num})");
		}
		else if (_threatNear && !PED.IS_PED_IN_COMBAT(ped.Handle, 0))
		{
			PED.REGISTER_HATED_TARGETS_AROUND_PED(ped.Handle, 120f);
			TASK.TASK_COMBAT_HATED_TARGETS_AROUND_PED(ped.Handle, 120f, 0, 0uL);
			if (!c.WasFighting)
			{
				GrudgeLog("COMPANION " + c.Name + ": into the fight");
			}
			c.WasFighting = true;
		}
		else if (!_threatNear)
		{
			c.WasFighting = false;
			c.LastTarget = 0;
		}
	}

	private unsafe bool ScreenSees(Vector3 p)
	{
		float num = 0f;
		float num2 = 0f;
		if (GRAPHICS.GET_SCREEN_COORD_FROM_WORLD_COORD(p.X, p.Y, p.Z + 1f, &num, &num2) && num > 0f && num < 1f && num2 > 0f && num2 < 1f)
		{
			return Game.Player.Character.Position.DistanceTo(p) < 120f;
		}
		return false;
	}

	private void GoingHome(Companion c, Ped player)
	{
		int num;
		int num2;
		if (!(c.Ped == null))
		{
			num = ((!c.Ped.Exists()) ? 1 : 0);
			if (num == 0)
			{
				num2 = ((c.Ped.Position.DistanceTo(player.Position) > 120f) ? 1 : 0);
				goto IL_0045;
			}
		}
		else
		{
			num = 1;
		}
		num2 = 0;
		goto IL_0045;
		IL_0045:
		bool flag = (byte)num2 != 0;
		if (((uint)num | (flag ? 1u : 0u)) != 0 || Game.GameTime - c.LeaveAt > 20000)
		{
			Cleanup(c);
			_comps.Remove(c);
		}
	}

	internal void LetCompanionsGo(string why)
	{
		if (_comps.Count == 0)
		{
			HideAskPrompt();
			return;
		}
		Crumbs.Mark("companion " + why + ": letting go");
		FlushGrudgeLog(force: true);
		foreach (Companion item in _comps.ToList())
		{
			SendHome(item, (item.State == 1) ? why : null, quiet: true);
		}
		HideAskPrompt();
	}

	private void SendHome(Companion c, string why, bool quiet = false)
	{
		if (c == null)
		{
			return;
		}
		if (c.State == 2)
		{
			Cleanup(c);
			_comps.Remove(c);
			return;
		}
		if (c.State != 1)
		{
			ReleaseReal(c);
			_comps.Remove(c);
			return;
		}
		if (!quiet && c.Ped != null && c.Ped.Exists())
		{
			ShowSubtitle(c.Name.Split(' ')[0] + ": \"" + HomeLines[_rng.Next(HomeLines.Length)] + "\"");
		}
		GrudgeLog("COMPANION " + c.Name + " goes home (" + why + ")");
		Chronicle_Ride(c, hurt: false);
		if (c.Ped != null && c.Ped.Exists() && c.IsReal && !BelongsToUs(c.Ped.Handle))
		{
			Cleanup(c);
			_comps.Remove(c);
			return;
		}
		if (c.Ped != null && c.Ped.Exists())
		{
			if (c.MaskOn)
			{
				SetMask(c, on: false);
			}
			PED.REMOVE_PED_FROM_GROUP(c.Ped.Handle);
			if (why == "back at camp" || quiet)
			{
				Cleanup(c);
				_comps.Remove(c);
				return;
			}
			if (c.Horse != null && c.Horse.Exists() && !c.Ped.IsOnMount)
			{
				TASK.TASK_MOUNT_ANIMAL(c.Ped.Handle, c.Horse.Handle, 20000, -1, 2f, 1, 0uL, 0uL);
			}
			if (c.IsReal)
			{
				HeadHome(c, 1.5f);
			}
			else
			{
				Flee(c.Ped, Game.Player.Character, 200f, -1, 1.5f);
			}
		}
		if (c.Blip != null && c.Blip.Exists())
		{
			c.Blip.Delete();
		}
		c.State = 2;
		c.LeaveAt = Game.GameTime;
	}

	private unsafe void Cleanup(Companion c)
	{
		if (_compIdle.TryGetValue(c, out var value))
		{
			if (value.Point != 0)
			{
				_seatTaken.Remove(value.Point);
			}
			_compIdle.Remove(c);
		}
		if (c.Blip != null && c.Blip.Exists())
		{
			c.Blip.Delete();
		}
		if (c.Ped != null && c.Ped.Exists() && RealHandsOff(c, c.Ped))
		{
			GrudgeLog("COMPANION " + c.Name + ": the game / a mission has him now - left alone");
			c.Ped = null;
			c.Real = null;
		}
		if (c.Ped != null && c.Ped.Exists())
		{
			if (!c.IsReal)
			{
				ENTITY.SET_ENTITY_INVINCIBLE(c.Ped.Handle, toggle: false);
			}
			if (c.IsReal)
			{
				if (c.MaskOn)
				{
					SetMask(c, on: false);
				}
				PED.REMOVE_PED_FROM_GROUP(c.Ped.Handle);
				TASK.CLEAR_PED_TASKS(c.Ped.Handle, p1: true, p2: false);
				RestoreReal(c);
				int handle = c.Ped.Handle;
				ReleaseIfOurs(&handle);
			}
			else
			{
				c.Ped.Delete();
			}
		}
		if (c.Horse != null && c.Horse.Exists() && BelongsToUs(c.Horse.Handle) && !ENTITY.IS_ENTITY_ATTACHED(c.Horse.Handle) && PED.GET_MOUNT(Game.Player.Character.Handle) != c.Horse.Handle)
		{
			c.Horse.Delete();
		}
		ReleaseReal(c);
	}

	private void RestoreReal(Companion c)
	{
		Ped ped = c.Ped;
		if (ped == null || !ped.Exists() || RealHandsOff(c, ped))
		{
			return;
		}
		try
		{
			if (c.SavedRel != 0)
			{
				PED.SET_PED_RELATIONSHIP_GROUP_HASH(ped.Handle, (uint)c.SavedRel);
			}
			if (c.SavedFlags != null)
			{
				for (int i = 0; i < SavedFlagIds.Length; i++)
				{
					PED.SET_PED_CONFIG_FLAG(ped.Handle, SavedFlagIds[i], c.SavedFlags[i]);
				}
			}
			if (c.SavedMaxHp > 0 && ped.MaxHealth != c.SavedMaxHp)
			{
				ped.MaxHealth = c.SavedMaxHp;
				if (ped.Health > c.SavedMaxHp)
				{
					ped.Health = c.SavedMaxHp;
				}
			}
			if (c.Saved)
			{
				ENTITY.SET_ENTITY_INVINCIBLE(ped.Handle, !c.SavedDamageable);
				PED.SET_PED_ACCURACY(ped.Handle, c.SavedAccuracy);
				PED.SET_PED_COMBAT_ATTRIBUTES(ped.Handle, 5, c.SavedAttr5);
				PED.SET_PED_COMBAT_ATTRIBUTES(ped.Handle, 46, c.SavedAttr46);
			}
			if (c.RealOutfit != 0 && c.MaskAdded != 0)
			{
				RealOutfitBack(c);
			}
			foreach (uint addedWeapon in c.AddedWeapons)
			{
				Function.Call(5231435679784720824uL, ped.Handle, addedWeapon, true, MISC.GET_HASH_KEY("REMOVE_REASON_DEFAULT"));
			}
			c.AddedWeapons.Clear();
			GrudgeLog("COMPANION " + c.Name + ": the real one put back as he was (group, flags, health, weapons)");
		}
		catch (Exception ex)
		{
			GrudgeLog("COMPANION " + c.Name + ": restore failed - " + ex.Message);
		}
	}

	private void HeadHome(Companion c, float speed)
	{
		if (!(c.Ped == null) && c.Ped.Exists())
		{
			if (c.Horse != null && c.Horse.Exists() && !c.Ped.IsOnMount && c.Ped.Position.DistanceTo(c.Horse.Position) < 30f)
			{
				TASK.TASK_MOUNT_ANIMAL(c.Ped.Handle, c.Horse.Handle, 20000, -1, 2f, 1, 0uL, 0uL);
			}
			else
			{
				TASK.TASK_GO_TO_COORD_ANY_MEANS(c.Ped.Handle, c.Camp.X, c.Camp.Y, c.Camp.Z, (!c.Ped.IsOnMount) ? speed : ((speed > 2f) ? 20f : 6f), 0, p6: false, 786603, 0f);
			}
		}
	}

	private void Chronicle_Ride(Companion c, bool hurt)
	{
		if (c.Since > 0 && c.LeftCamp)
		{
			long num = Math.Max(1L, GameMinutesNow() - c.Since);
			AddRegard(c.Key, hurt ? (-1) : Math.Min(8, 3 + (int)(num / 60)), hurt ? "got hurt riding with you" : ("rode out with you (" + ((num >= 120) ? (num / 60 + " h") : (num + " min")) + ")"));
			string text = ((num >= 120) ? $"{num / 60} hours" : $"{num} minutes");
			List<string> list = (from o in _comps
				where o != c && o.State == 1
				select o.Name).ToList();
			Vector3 position = Game.Player.Character.Position;
			ChronicleDeed("ride", "You rode out with " + c.Name + ((list.Count > 0) ? (" and " + string.Join(", ", list)) : ""), "near " + GetZoneName(position), position, "vdl", new WorldPart
			{
				Name = c.Name,
				Gang = "vdl",
				Outcome = (hurt ? "WOUNDED" : "UNHURT"),
				Note = (hurt ? ("hurt after " + text + " - laid up a day") : (text + " on the road"))
			});
			c.Since = 0L;
		}
	}

	private void WipeCompanion()
	{
		foreach (Companion item in _comps.ToList())
		{
			Cleanup(item);
		}
		_compIdle.Clear();
		_stillSince = -1;
		_comps.Clear();
		HideAskPrompt();
	}
}
