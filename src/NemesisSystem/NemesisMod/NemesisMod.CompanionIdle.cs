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
	private sealed class IdleState
	{
		public int Stage;

		public int Point;

		public Vector3 At;

		public Vector3 Seat;

		public int Interior;

		public int CheckAt;

		public int NextLine;

		public int Reseats;

		public int LookAgainAt;

		public int Tries;

		public int AwaySince = -1;

		public bool Spoke;

		public bool Settled;

		public bool NearTried;

		public HashSet<int> Tried = new HashSet<int>();
	}

	private int _nextBanter;

	private int _banterBusyUntil;

	private (Companion who, string line, int at)? _banterReply;

	private static readonly Dictionary<string, (string a, string b)[]> PairLines = new Dictionary<string, (string, string)[]>
	{
		["karen|sean"] = new(string, string)[2]
		{
			("Sean, you owe me a drink.", "I owe everyone a drink, Karen. That's the beauty of it."),
			("You talk too much, Irish.", "And you love every word of it.")
		},
		["charles|lenny"] = new(string, string)[2]
		{
			("You keep up all right, Lenny.", "Learned from the best, Charles."),
			("Tracks are fresh. Stay sharp.", "Always am.")
		},
		["john|micah"] = new(string, string)[2]
		{
			("Keep your eyes to yourself, Micah.", "Ooh, touchy, Marston."),
			("Something about you I don't trust.", "Feeling's mutual, cowpoke.")
		},
		["javier|micah"] = new(string, string)[1] { ("Dutch trusts you, Micah. I don't.", "Lucky I don't need your trust, amigo.") },
		["bill|micah"] = new(string, string)[1] { ("You think you're smarter'n me, Bell?", "I know I am, Bill.") },
		["hosea|john"] = new(string, string)[1] { ("You should spend more time with your son, John.", "I know, Hosea. I know.") },
		["kieran|sean"] = new(string, string)[1] { ("I-I never meant no harm, Sean.", "Ah, you're grand, Kieran. Mostly.") },
		["lenny|sean"] = new(string, string)[1] { ("Sean, you ever shut up?", "Not if I can help it, Lenny boy.") },
		["charles|javier"] = new(string, string)[1] { ("You play beautifully, Javier.", "Gracias, Charles. You should sing with me sometime.") },
		["dutch|hosea"] = new(string, string)[1] { ("We need a plan, Hosea.", "We always need a plan, Dutch. We need a good one.") }
	};

	private static readonly (string a, string b)[] Warm = new(string, string)[5]
	{
		("Good to be out of that camp.", "Damn right it is."),
		("You keeping all right?", "Better out here."),
		("Remember that job in Blackwater?", "Let's not."),
		("Arthur rides like he's got somewhere to be.", "He always does."),
		("Weather's holding.", "For now.")
	};

	private static readonly (string a, string b)[] Cold = new(string, string)[3]
	{
		("Ride somewhere else, would you?", "Free country."),
		("Don't talk to me.", "Wasn't planning on it."),
		("You slow us down.", "Funny, I was gonna say the same.")
	};

	private static readonly (string a, string b)[] Plain = new(string, string)[3]
	{
		("How far you reckon we're going?", "Ask Arthur."),
		("Hungry yet?", "Always."),
		("Quiet out here.", "Too quiet.")
	};

	private const uint NeckwearCat = 1606587013u;

	private static readonly string[] MaskItemNames = new string[2] { "KIT_BANDANA", "CLOTHING_ITEM_PZ_NECKERCHIEF_001" };

	private int _nextMaskSync;

	private static readonly Dictionary<uint, int> _maskPreset = new Dictionary<uint, int>();

	private const uint MasksCat = 1963323202u;

	private const uint MasksLargeCat = 1249071452u;

	private static readonly uint BandanaCat = Joaat.Of("BANDANAS");

	private static readonly uint MasksMpCat = Joaat.Of("MASKS_MP");

	private static Dictionary<uint, string> _catNames;

	private readonly Dictionary<Companion, IdleState> _compIdle = new Dictionary<Companion, IdleState>();

	private Vector3 _stillAt;

	private int _stillSince = -1;

	private int _insideOf;

	private int _insideSince;

	private static readonly string[] IdleOutdoors = new string[10] { "Nice to sit a minute.", "Quiet out here. Too quiet, maybe.", "You ever think about just... stopping? Somewhere like this?", "My feet are killin' me.", "Take your time, Arthur. I ain't in a hurry.", "Smell that? Rain's coming.", "Don't see country like this back east.", "We waitin' on something, or just waitin'?", "Wake me when you're done brooding.", "Horses could use the rest too." };

	private static readonly string[] IdleIndoors = new string[6] { "Good to be out of the weather.", "Keep an eye on that door.", "Folks are lookin' at us, Arthur.", "Whose place is this?", "Don't touch nothing you can't pay for.", "Smells like old coffee in here." };

	private static readonly string[] IdleSaloon = new string[8] { "Buy a man a drink, Arthur?", "This whiskey's watered. Again.", "That fella at the table's been cheating all night.", "One drink. Then we go.", "I'd sit in on a hand if I had any money.", "Piano player's drunker than me.", "Keep your voice down - half this room's wanted somewhere.", "Barkeep's eyeing us." };

	private static readonly Dictionary<string, string[]> IdleOwn = new Dictionary<string, string[]>
	{
		["sean"] = new string[3] { "Me ma would've loved a place like this. Or hated it.", "If I had a dollar for every time we stopped...", "Arthur, you ever been to Ireland? You'd hate it." },
		["lenny"] = new string[3] { "I've been reading again, Arthur.", "You alright? You look tired.", "Pop always said sit when you can." },
		["charles"] = new string[3] { "Good country for hunting.", "You can hear everything, if you listen.", "I'll keep watch." },
		["javier"] = new string[3] { "I could play something, if I had my guitar.", "Back home we'd be eating by now.", "You think Dutch knows what he's doing?" },
		["john"] = new string[3] { "Abigail'd have my hide if she saw me sittin' around.", "Don't start, Arthur.", "We gonna do something or not?" },
		["hosea"] = new string[3] { "Sit with an old man a while.", "When you're my age, every chair's a blessing.", "Dutch used to be different, you know." },
		["bill"] = new string[3] { "I ain't stupid, Arthur. I know what you're thinking.", "Could use a drink.", "This is boring." },
		["micah"] = new string[3] { "Sittin' around like old women.", "You and me, cowpoke. We think alike.", "Hurry it up." },
		["sadie"] = new string[3] { "Don't get comfortable, Arthur.", "Jake liked places like this.", "I ain't one for sitting." },
		["kieran"] = new string[3] { "Mr. Morgan... thank you. For bringing me.", "I don't mind waitin'.", "The horses like it here." },
		["uncle"] = new string[3] { "Finally, a rest. My lumbago, Arthur.", "Wake me when we leave.", "I was a cowboy once, you know." }
	};

	private void Banter(Ped player)
	{
		if (_banterReply.HasValue && Game.GameTime >= _banterReply.Value.at)
		{
			(Companion, string, int) value = _banterReply.Value;
			_banterReply = null;
			if (value.Item1.State == 1 && value.Item1.Ped != null && value.Item1.Ped.Exists())
			{
				ShowSubtitle(value.Item1.Name.Split(' ')[0] + ": \"" + value.Item2 + "\"");
			}
		}
		List<Companion> list = _comps.Where((Companion c) => c.State == 1 && c.Ped != null && c.Ped.Exists() && c.Ped.Position.DistanceTo(player.Position) < 30f).ToList();
		if (list.Count < 2 || player.IsInCombat || Game.GameTime < _nextBanter)
		{
			return;
		}
		if (_nextBanter == 0)
		{
			_nextBanter = Game.GameTime + 40000;
			return;
		}
		_nextBanter = Game.GameTime + 80000 + _rng.Next(70000);
		Companion a = list[_rng.Next(list.Count)];
		Companion companion = (from _ in list
			where _ != a && _.Ped.Position.DistanceTo(a.Ped.Position) < 20f
			orderby _rng.Next()
			select _).FirstOrDefault();
		if (companion == null)
		{
			return;
		}
		string text = ((string.CompareOrdinal(a.Key, companion.Key) < 0) ? (a.Key + "|" + companion.Key) : (companion.Key + "|" + a.Key));
		int num = TieBetween(a.Key, companion.Key);
		(string, string) tuple;
		if (PairLines.TryGetValue(text, out (string, string)[] value2) && _rng.Next(100) < 60)
		{
			tuple = value2[_rng.Next(value2.Length)];
			if (text.StartsWith(companion.Key + "|"))
			{
				Companion companion2 = a;
				a = companion;
				companion = companion2;
			}
		}
		else
		{
			tuple = ((num <= -25) ? Cold[_rng.Next(Cold.Length)] : ((num >= 30) ? Warm[_rng.Next(Warm.Length)] : Plain[_rng.Next(Plain.Length)]));
		}
		ShowSubtitle(a.Name.Split(' ')[0] + ": \"" + tuple.Item1 + "\"");
		TASK.TASK_LOOK_AT_ENTITY(a.Ped.Handle, companion.Ped.Handle, 3000, 0, 0, 0);
		TASK.TASK_LOOK_AT_ENTITY(companion.Ped.Handle, a.Ped.Handle, 4000, 0, 0, 0);
		_banterReply = (companion, tuple.Item2, Game.GameTime + 3500);
		_banterBusyUntil = Game.GameTime + 8000;
		GrudgeLog($"COMPANION banter {a.Name} -> {companion.Name} (tie {num})");
	}

	private static string CatName(uint h)
	{
		if (_catNames == null)
		{
			_catNames = new Dictionary<uint, string>();
			string[] array = new string[49]
			{
				"hats", "masks", "MASKS_LARGE", "neckwear", "neckties", "BANDANAS", "coats", "coats_closed", "vests", "shirts_full",
				"pants", "boots", "gloves", "belts", "gunbelts", "holsters_left", "satchels", "suspenders", "chaps", "spurs",
				"spats", "eyewear", "jewelry_rings_right", "jewelry_rings_left", "ponchos", "cloaks", "loadouts", "hair", "heads", "BODIES_UPPER",
				"BODIES_LOWER", "teeth", "eyes", "beards_complete", "beards_mustache", "beards_chin", "boot_accessories", "badges", "accessories", "gauntlets",
				"neckwear_bandana", "MASKS_MP", "armor", "dresses", "skirts", "hair_accessories", "talisman_belt", "talisman_wrist", "aprons"
			};
			foreach (string text in array)
			{
				_catNames[MISC.GET_HASH_KEY(text)] = text;
			}
		}
		if (!_catNames.TryGetValue(h, out var value))
		{
			return $"0x{h:X8}";
		}
		return value;
	}

	private void MaskFollow(Ped player)
	{
		foreach (Companion comp in _comps)
		{
			if (comp.PullUpAt > 0 && Game.GameTime >= comp.PullUpAt && comp.Ped != null && comp.Ped.Exists())
			{
				PullUp(comp);
			}
		}
		if (Game.GameTime < _nextMaskSync || _comps.Count == 0)
		{
			return;
		}
		_nextMaskSync = Game.GameTime + 1000;
		bool flag = PlayerMasked(player);
		foreach (Companion comp2 in _comps)
		{
			if (comp2.State != 1 || comp2.Ped == null || !comp2.Ped.Exists())
			{
				continue;
			}
			if (comp2.MaskCheckAt > 0 && Game.GameTime >= comp2.MaskCheckAt)
			{
				comp2.MaskCheckAt = 0;
				bool flag2 = comp2.MaskAdded != 0 && WearsComponent(comp2.Ped.Handle, comp2.MaskAdded);
				GrudgeLog("COMPANION " + comp2.Name + ": mask " + (comp2.MaskOn ? "on" : "off") + " - " + ((comp2.MaskAdded == 0) ? "his own neckwear pulled up" : (flag2 ? "the bandana took" : "the bandana did NOT take")));
				if (comp2.MaskOn && comp2.MaskAdded != 0 && !flag2 && comp2.MaskTry + 1 < MaskItemNames.Length)
				{
					comp2.MaskTry++;
					SetMaskItem(comp2, on: true);
				}
			}
			if (flag != comp2.MaskOn)
			{
				comp2.MaskTry = 0;
				SetMask(comp2, flag);
			}
		}
	}

	private void RealOutfitBack(Companion c)
	{
		if (!(c.Ped == null) && c.Ped.Exists() && c.RealOutfit != 0)
		{
			Crumbs.Native("Companion outfit back");
			PED._EQUIP_META_PED_OUTFIT(c.Ped.Handle, c.RealOutfit);
			Crumbs.Native("Companion ped variation (outfit back)");
			PED._UPDATE_PED_VARIATION(c.Ped.Handle, p1: false, p2: true, p3: true, p4: true, p5: false);
			c.MaskAdded = 0u;
			c.PullUpAt = 0;
		}
	}

	private void SetMask(Companion c, bool on)
	{
		if (c.Ped == null || !c.Ped.Exists() || RealHandsOff(c, c.Ped))
		{
			return;
		}
		int handle = c.Ped.Handle;
		_ = c.Ped.Model.Hash;
		c.MaskOn = on;
		if (on)
		{
			c.MaskTry = 0;
			SetMaskItem(c, on: true);
			ShowSubtitle(c.Name.Split(' ')[0] + ": \"" + ((_rng.Next(2) == 0) ? "Masks on." : "Right behind you.") + "\"");
			return;
		}
		c.Scan = -1;
		if (c.InMaskOutfit && c.SavedOutfit != 0)
		{
			Function.Call(1802219197667468668uL, handle, c.SavedOutfit);
			Function.Call(14739335899491263887uL, handle, false, true, true, true, false);
		}
		c.InMaskOutfit = false;
		if (c.MaskAdded != 0)
		{
			SetMaskItem(c, on: false);
		}
	}

	private void PullUp(Companion c)
	{
		int handle = c.Ped.Handle;
		uint maskAdded = c.MaskAdded;
		if (!c.MaskOn || maskAdded == 0)
		{
			c.PullUpAt = 0;
			return;
		}
		if (_maskUp == 0)
		{
			_maskUp = MISC.GET_HASH_KEY("mask_up");
		}
		if (c.PullUps == 0 && !c.Ped.IsOnMount)
		{
			Function.Call(12570364454928820833uL, handle, 0, MISC.GET_HASH_KEY("BANDANA_ON_RIGHT_HAND"), 1, 0, -1f);
		}
		Crumbs.Native("Companion wearable state");
		Function.Call(7402043853486337707uL, handle, maskAdded, _maskUp, 0, c.PullUps % 2 == 1, 1);
		Crumbs.Native("Companion ped variation");
		PED._UPDATE_PED_VARIATION(handle, p1: false, p2: true, p3: true, p4: true, p5: false);
		c.PullUps++;
		c.PullUpAt = ((c.PullUps < 3) ? (Game.GameTime + 600) : 0);
		if (c.PullUps == 3)
		{
			GrudgeLog("COMPANION " + c.Name + ": bandana pulled up (3 tries" + (c.Ped.IsOnMount ? ", in the saddle - no hand move" : "") + ") - tell me if it's still round his neck");
		}
	}

	private unsafe void SetMaskItem(Companion c, bool on)
	{
		int handle = c.Ped.Handle;
		if (_maskUp == 0)
		{
			_maskUp = MISC.GET_HASH_KEY("mask_up");
		}
		uint maskUp = _maskUp;
		uint num = MISC.GET_HASH_KEY("base");
		c.MaskOn = on;
		try
		{
			uint num2 = 0u;
			int num3 = Function.Call<int>(10394376663872179841uL, new InputArgument[1] { handle });
			ulong* ptr = stackalloc ulong[1];
			ulong* ptr2 = stackalloc ulong[1];
			for (int i = 0; i < num3 && i < 64; i++)
			{
				if (num2 != 0)
				{
					break;
				}
				*ptr = 0uL;
				*ptr2 = 0uL;
				uint num4 = Function.Call<uint>(8627268931001909819uL, new InputArgument[5]
				{
					handle,
					i,
					true,
					new InputArgument((ulong)ptr),
					new InputArgument((ulong)ptr2)
				});
				if (num4 != 0 && Function.Call<uint>(11209604758464182439uL, new InputArgument[3] { handle, i, 0 }) == 1606587013)
				{
					num2 = num4;
				}
			}
			if (num2 != 0 && c.MaskAdded == 0)
			{
				Function.Call(7402043853486337707uL, handle, num2, on ? maskUp : num, 0, true, 1);
			}
			else if (on)
			{
				uint num5 = MISC.GET_HASH_KEY(MaskItemNames[Math.Min(c.MaskTry, MaskItemNames.Length - 1)]);
				Crumbs.Native("Companion shop item");
				if (c.MaskAdded != num5)
				{
					PED._APPLY_SHOP_ITEM_TO_PED(handle, num5, immediately: true, isMp: false, p4: false);
				}
				c.MaskAdded = num5;
				c.PullUpAt = Game.GameTime + 400;
				c.PullUps = 0;
			}
			else if (c.MaskAdded != 0 && c.IsReal && c.RealOutfit != 0)
			{
				Crumbs.Native("Companion item interaction");
				if (!c.Ped.IsOnMount)
				{
					Function.Call(12570364454928820833uL, handle, 0, MISC.GET_HASH_KEY("BANDANA_OFF_RIGHT_HAND"), 1, 0, -1f);
				}
				RealOutfitBack(c);
			}
			else if (c.MaskAdded != 0)
			{
				Crumbs.Native("Companion item interaction");
				if (!c.Ped.IsOnMount)
				{
					Function.Call(12570364454928820833uL, handle, 0, MISC.GET_HASH_KEY("BANDANA_OFF_RIGHT_HAND"), 1, 0, -1f);
				}
				Crumbs.Native("Companion meta tag remove");
				Function.Call(15497067739283637561uL, handle, 1606587013u, 0);
				c.MaskAdded = 0u;
				c.PullUpAt = 0;
			}
			Crumbs.Native("Companion ped variation");
			PED._UPDATE_PED_VARIATION(handle, p1: false, p2: true, p3: true, p4: true, p5: false);
			c.MaskCheckAt = 0;
		}
		catch (Exception ex)
		{
			GrudgeLog("COMPANION " + c.Name + ": mask failed - " + ex.Message);
		}
	}

	private bool PlayerIdleFor(Ped player, int ms)
	{
		if (player.IsOnMount || player.IsInVehicle || player.IsInCombat || PLAYER.IS_PLAYER_FREE_AIMING(Game.Player.Handle) || !(ENTITY.GET_ENTITY_SPEED(player.Handle) < 0.6f) || PED.IS_PED_RAGDOLL(player.Handle) || (_stillSince >= 0 && player.Position.DistanceTo(_stillAt) > 4f))
		{
			_stillSince = -1;
			return false;
		}
		if (_stillSince < 0)
		{
			_stillSince = Game.GameTime;
			_stillAt = player.Position;
		}
		return Game.GameTime - _stillSince >= ms;
	}

	private void CompanionIdle(Companion c, Ped player)
	{
		Ped ped = c.Ped;
		if (ped == null || !ped.Exists() || ped.IsDead)
		{
			_compIdle.Remove(c);
			return;
		}
		int num = INTERIOR.GET_INTERIOR_FROM_ENTITY(player.Handle);
		if (num != 0)
		{
			if (_insideOf != num)
			{
				_insideOf = num;
				_insideSince = Game.GameTime;
			}
		}
		else
		{
			_insideOf = 0;
		}
		bool flag = (num != 0 && Game.GameTime - _insideSince >= 1500 && !player.IsInCombat && !PLAYER.IS_PLAYER_FREE_AIMING(Game.Player.Handle)) || PlayerIdleFor(player, 10000);
		_compIdle.TryGetValue(c, out var value);
		bool flag2 = ped.IsInCombat || ped.IsOnMount || player.IsInCombat || _convo != null;
		if (value != null && value.Stage > 0 && !flag2)
		{
			float num2 = player.Position.DistanceTo((value.Seat == Vector3.Zero) ? value.At : value.Seat);
			int interior = value.Interior;
			bool flag3 = ((interior != 0) ? (num != interior) : (num != 0));
			if (!flag3)
			{
				value.AwaySince = -1;
			}
			else if (value.AwaySince < 0)
			{
				value.AwaySince = Game.GameTime;
			}
			if (!player.IsOnMount && !player.IsInVehicle && !PLAYER.IS_PLAYER_FREE_AIMING(Game.Player.Handle) && (!flag3 || Game.GameTime - value.AwaySince <= 3000) && !(num2 > ((interior != 0) ? 25f : 22f)))
			{
				flag = true;
			}
		}
		if (!flag || flag2)
		{
			bool flag4 = ped.IsInCombat || player.IsInCombat;
			if (value != null && value.Stage > 0 && flag4)
			{
				if (value.Point != 0)
				{
					_seatTaken.Remove(value.Point);
				}
				GrudgeLog("COMPANION " + c.Name + ": a fight - stops hanging about");
			}
			else if (value != null && value.Stage > 0)
			{
				TASK.CLEAR_PED_TASKS(ped.Handle, p1: false, p2: false);
				TASK.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(ped.Handle, player.Handle, 1.2f, -1.5f, 0f, 1.6f, -1, 1.5f, persistFollowing: true, p9: false, walkOnly: false, p11: false, p12: false, p13: false);
				if (value.Point != 0)
				{
					_seatTaken.Remove(value.Point);
				}
				GrudgeLog(Pron.Her(string.Format("COMPANION {0}: you left ({1:0} m{2}) - he gets up and follows", c.Name, player.Position.DistanceTo((value.Seat == Vector3.Zero) ? value.At : value.Seat), (num != value.Interior) ? ", another building / outside" : ""), !PED.IS_PED_MALE(ped.Handle)));
			}
			_compIdle.Remove(c);
			return;
		}
		if (value == null)
		{
			if (!(ped.Position.DistanceTo(player.Position) > 20f))
			{
				value = new IdleState
				{
					Stage = 1,
					At = player.Position,
					Interior = num,
					NextLine = Game.GameTime + 8000 + _rng.Next(10000)
				};
				_compIdle[c] = value;
				int num3 = CompanionPoint(c, player, num);
				if (num3 != 0)
				{
					value.Point = num3;
					_seatTaken[num3] = Game.GameTime;
					Crumbs.Native("seat task: companion seat");
					NoteSeatTask();
					TASK.TASK_USE_SCENARIO_POINT(ped.Handle, num3, "", -1, p4: true, p5: false, 0u, p7: false, -1f, p9: true);
					value.CheckAt = Game.GameTime + 12000;
					value.Tried.Add(num3);
					value.Tries = 1;
					Vector3 vector = (value.Seat = TASK._GET_SCENARIO_POINT_COORDS(num3, p1: true));
					GrudgeLog(Pron.Her(string.Format("COMPANION {0}: you've stopped - he takes a spot {1:0.0} m from you{2}", c.Name, vector.DistanceTo(player.Position), (num != 0) ? " (indoors)" : ""), !PED.IS_PED_MALE(ped.Handle)));
				}
				else
				{
					IdleInPlaceFor(c, ped, player);
					value.Stage = 2;
				}
			}
			return;
		}
		if (value.Stage == 1 && PED.IS_PED_USING_ANY_SCENARIO(ped.Handle))
		{
			value.Settled = true;
			value.Stage = 2;
			GrudgeLog("COMPANION " + c.Name + ": settled in");
		}
		else if (value.Stage == 1 && Game.GameTime >= value.CheckAt)
		{
			Vector3 position = ((value.Point != 0) ? TASK._GET_SCENARIO_POINT_COORDS(value.Point, p1: true) : Vector3.Zero);
			float num4 = ((value.Point != 0) ? ped.Position.DistanceTo(position) : 0f);
			if (value.Point != 0 && num4 > 1.5f && ENTITY.GET_ENTITY_SPEED(ped.Handle) > 0.3f && Game.GameTime - value.CheckAt < 6000)
			{
				return;
			}
			if (value.Point != 0 && num4 < 2.5f && !value.NearTried)
			{
				value.NearTried = true;
				Crumbs.Native("seat task: companion nearest seat");
				NoteSeatTask();
				TASK.TASK_USE_NEAREST_SCENARIO_TO_COORD_WARP(ped.Handle, position.X, position.Y, position.Z, 2f, -1, p6: true, p7: false, p8: false, p9: false);
				value.CheckAt = Game.GameTime + 6000;
				GrudgeLog(Pron.Her("COMPANION " + c.Name + ": next to the spot but not in it - the nearest one there instead", !PED.IS_PED_MALE(ped.Handle)));
				return;
			}
			value.NearTried = false;
			if (value.Point != 0)
			{
				_seatTaken.Remove(value.Point);
			}
			int num5 = ((value.Tries < 3) ? CompanionPoint(c, player, num, value.Tried) : 0);
			if (num5 != 0)
			{
				GrudgeLog($"COMPANION {c.Name}: that spot didn't take ({num4:0.0} m from it) - another");
				value.Point = num5;
				value.Tried.Add(num5);
				value.Tries++;
				_seatTaken[num5] = Game.GameTime;
				value.Seat = TASK._GET_SCENARIO_POINT_COORDS(num5, p1: true);
				Crumbs.Native("seat task: companion next seat");
				NoteSeatTask();
				TASK.TASK_USE_SCENARIO_POINT(ped.Handle, num5, "", -1, p4: true, p5: false, 0u, p7: false, -1f, p9: true);
				value.CheckAt = Game.GameTime + 12000;
			}
			else
			{
				value.Point = 0;
				IdleInPlaceFor(c, ped, player);
				value.Stage = 2;
			}
		}
		if (value.Stage == 2 && value.Settled && value.Point != 0 && Game.GameTime >= value.LookAgainAt && !PED.IS_PED_USING_ANY_SCENARIO(ped.Handle))
		{
			value.LookAgainAt = Game.GameTime + 3000;
			if (value.Reseats++ < 2 && TASK._GET_PED_USING_SCENARIO_POINT(value.Point) == 0)
			{
				Crumbs.Native("seat task: companion reseat");
				NoteSeatTask();
				TASK.TASK_USE_SCENARIO_POINT(ped.Handle, value.Point, "", -1, p4: true, p5: false, 0u, p7: false, -1f, p9: true);
				GrudgeLog($"COMPANION {c.Name}: got up by himself ({ped.Position.DistanceTo(player.Position):0} m from you) - back to his spot");
			}
		}
		if (value.Stage == 2 && Game.GameTime >= value.NextLine && Game.GameTime >= _banterBusyUntil && ped.Position.DistanceTo(player.Position) < 14f)
		{
			value.NextLine = Game.GameTime + 45000 + _rng.Next(45000);
			TASK.TASK_LOOK_AT_ENTITY(ped.Handle, player.Handle, 4000, 0, 51, 0);
			ShowSubtitle(c.Name.Split(' ')[0] + ": \"" + IdleLine(c, player, num) + "\"");
			if (!value.Spoke)
			{
				value.Spoke = true;
				NoteRegardTalk(c);
			}
		}
	}

	private unsafe int CompanionPoint(Companion c, Ped player, int interior, HashSet<int> skip = null)
	{
		ulong* ptr = stackalloc ulong[112];
		for (int i = 0; i < 112; i++)
		{
			ptr[i] = 0uL;
		}
		*ptr = 96uL;
		Vector3 me = player.Position;
		float range = ((interior != 0) ? 14f : 12f);
		Crumbs.Native("CompanionIdle scenario points");
		int num = TASK.GET_SCENARIO_POINTS_IN_AREA(me.X, me.Y, me.Z, range, ptr, 96);
		List<(int, float)> list = new List<(int, float)>();
		HashSet<int> hashSet = new HashSet<int>(from s in _compIdle.Values
			select s.Point into p
			where p != 0
			select p);
		List<Vector3> source = (from p in PedsAround(player)
			where p != null && p.Exists() && !p.IsDead && p.Handle != player.Handle && p.Handle != c.Ped.Handle && p.Position.DistanceTo(me) < range + 2f
			select p.Position).ToList();
		for (int num2 = 1; num2 <= Math.Min(num + 1, 96); num2++)
		{
			int num3 = (int)(ptr[num2] & 0xFFFFFFFFu);
			if (num3 != 0 && !hashSet.Contains(num3) && (skip == null || !skip.Contains(num3)) && TASK.DOES_SCENARIO_POINT_EXIST(num3) && TASK._GET_PED_USING_SCENARIO_POINT(num3) == 0 && (!_seatTaken.TryGetValue(num3, out var value) || Game.GameTime - value >= 120000))
			{
				Vector3 pc = TASK._GET_SCENARIO_POINT_COORDS(num3, p1: true);
				float num4 = pc.DistanceTo(me);
				if (!(num4 < 1.8f) && !(num4 > range) && !(Math.Abs(pc.Z - me.Z) > 2.2f) && !source.Any((Vector3 q) => q.DistanceTo(pc) < 1f) && INTERIOR.GET_INTERIOR_AT_COORDS(pc.X, pc.Y, pc.Z) == interior)
				{
					list.Add((num3, num4));
				}
			}
		}
		if (list.Count == 0)
		{
			return 0;
		}
		List<(int, float)> list2 = list.OrderBy(((int pt, float d) x) => x.d).Take(4).ToList();
		return list2[_rng.Next(list2.Count)].Item1;
	}

	private void IdleInPlaceFor(Companion c, Ped ped, Ped player)
	{
		NemesisRecord rec = c.Rec;
		object obj;
		if (rec == null || rec.Personality != Personality.Hothead)
		{
			NemesisRecord rec2 = c.Rec;
			if (rec2 == null || rec2.Personality != Personality.Drifter)
			{
				obj = new string[4] { "WORLD_HUMAN_SMOKE", "WORLD_HUMAN_GUARD_LAZY", "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_GUARD_LAZY" };
				goto IL_008e;
			}
		}
		obj = new string[3] { "WORLD_HUMAN_SMOKE", "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_GUARD_LAZY" };
		goto IL_008e;
		IL_008e:
		string[] array = (string[])obj;
		string text = array[_rng.Next(array.Length)];
		TASK.TASK_TURN_PED_TO_FACE_ENTITY(ped.Handle, player.Handle, 1200, 0f, 0f, 0f);
		TASK.TASK_START_SCENARIO_IN_PLACE_HASH(ped.Handle, MISC.GET_HASH_KEY(text), -1, playEnterAnim: true, 0u, -1f, p6: false);
		GrudgeLog("COMPANION " + c.Name + ": no spot near you - " + text.Replace("WORLD_HUMAN_", "").ToLowerInvariant() + " where he stands");
	}

	private bool InSaloon(Ped player, int interior)
	{
		if (interior == 0)
		{
			return false;
		}
		string text = TownAt(player.Position);
		if (text != null && TownSpots.TryGetValue(text, out (Vector3?, Vector3?) value))
		{
			Vector3? item = value.Item2;
			if (item.HasValue)
			{
				return item.GetValueOrDefault().DistanceTo(player.Position) < 30f;
			}
		}
		return false;
	}

	private string IdleLine(Companion c, Ped player, int interior)
	{
		bool flag = InSaloon(player, interior);
		string[] value;
		string[] array = (IdleOwn.TryGetValue(c.Key, out value) ? value : null);
		if (array != null && _rng.Next(100) < 40)
		{
			return array[_rng.Next(array.Length)];
		}
		string[] array2 = (flag ? IdleSaloon : ((interior != 0) ? IdleIndoors : IdleOutdoors));
		return array2[_rng.Next(array2.Length)];
	}

	private void NoteRegardTalk(Companion c)
	{
		if (IsRecruit(c))
		{
			return;
		}
		try
		{
			AddRegard(c.Key, 1, "a quiet word while you rested");
		}
		catch
		{
		}
	}
}
