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
	private sealed class Bar
	{
		public string Town;

		public Vector3 At;

		public Vector3 Door;
	}

	private readonly List<Ped> _saloonA = new List<Ped>();

	private readonly List<Ped> _saloonB = new List<Ped>();

	private Gang _saloonGangA;

	private Gang _saloonGangB;

	private int _saloonStep;

	private int _saloonStepAt;

	private int _saloonFightAt;

	private int _saloonInSince;

	private int _saloonCooldownUntil;

	private bool _saloonSeenByYou;

	private bool _saloonHostileA;

	private bool _saloonHostileB;

	private bool _forceSaloon;

	private string _saloonTown;

	private string _saloonRolledFor;

	private Vector3 _saloonBar;

	private uint _saloonGrpA;

	private uint _saloonGrpB;

	private int _saloonLoggedInterior;

	private int _saloonInterior;

	private bool _saloonLoggedBar;

	private readonly HashSet<string> _saloonNearLogged = new HashSet<string>();

	private int _saloonApproachCheck;

	private int _saloonEnteredAt;

	private string _saloonWalkSaid;

	private int _saloonWalkSaidAt;

	private string _saloonPlaceWas;

	private List<Bar> _bars;

	private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	private static readonly Bar[] ShippedBars = new Bar[5]
	{
		new Bar
		{
			Town = "Rhodes",
			At = new Vector3(1339.2f, -1375.3f, 80.5f),
			Door = new Vector3(1352.8f, -1376.8f, 80.5f)
		},
		new Bar
		{
			Town = "Valentine",
			At = new Vector3(-313.8f, 807.6f, 119f),
			Door = new Vector3(-307.6f, 798.5f, 119f)
		},
		new Bar
		{
			Town = "Valentine",
			At = new Vector3(-238.5f, 769.1f, 118.1f),
			Door = new Vector3(-244f, 765.7f, 118.2f)
		},
		new Bar
		{
			Town = "Saint Denis",
			At = new Vector3(2793.2f, -1166.8f, 47.9f),
			Door = new Vector3(2793.7f, -1174.1f, 47.9f)
		},
		new Bar
		{
			Town = "Saint Denis",
			At = new Vector3(2639.9f, -1222.9f, 53.4f),
			Door = new Vector3(2628.6f, -1221f, 53.4f)
		}
	};

	private Vector3 _lastOutside;

	private int _lastOutsideAt;

	private string _saloonRefusedWhy;

	private Vector3 _saloonRolledAt;

	private int _saloonRetargetAt;

	private bool _saloonStaged;

	private Vector3 _saloonDoor;

	private Vector3 _saloonDoorOut;

	private readonly HashSet<int> _saloonThrough = new HashSet<int>();

	private bool _saloonStraight;

	private bool _saloonBreached;

	private int _saloonGatherAt;

	private int _saloonDrinkersAt;

	private int _saloonStormAt;

	private string BarDoorNote = "";

	private string BarPointNote = "";

	private static readonly Dictionary<string, int> BuiltInTurmoilKinds = new Dictionary<string, int>
	{
		["INCIDENT"] = 30,
		["DEFECTION"] = 25,
		["ASSASSINATION"] = 15,
		["TRUCE"] = 7,
		["ALLIANCE"] = 8,
		["CRACKDOWN"] = 7,
		["FEVER"] = 10,
		["WINDFALL"] = 15
	};

	internal static Dictionary<string, int> TurmoilKinds = BuiltInTurmoilKinds;

	internal IEnumerable<Ped> SaloonPeds => _saloonA.Concat(_saloonB);

	private Ped NearestBartender(Ped player)
	{
		return (from t in PedSnapshot(player)
			where t.p != null && t.p.Exists() && Models.Name(t.p).Contains("bartender")
			orderby (t.pos - player.Position).Length()
			select t.p).FirstOrDefault();
	}

	private Ped BartenderNear(Ped player, int interior)
	{
		foreach (var item in PedSnapshot(player))
		{
			var (ped, _) = item;
			if (!(ped == null) && ped.Exists() && !ped.IsDead && !((item.pos - player.Position).Length() > 25f) && Models.Name(ped).Contains("bartender") && INTERIOR.GET_INTERIOR_FROM_ENTITY(ped.Handle) != 0)
			{
				return ped;
			}
		}
		return null;
	}

	private bool SaloonMayRunQuiet(Ped player)
	{
		if (!MISC.GET_MISSION_FLAG() && !player.IsDead && !CAM.IS_SCREEN_FADED_OUT() && !CAM.IS_SCREEN_FADING_OUT() && !CAM.IS_SCREEN_FADING_IN() && !PLAYER.IS_PLAYER_BEING_ARRESTED(Game.Player.Handle, atArresting: false))
		{
			return player.Position.DistanceTo(_saloonBar) < 60f;
		}
		return false;
	}

	private void UpdateSaloonFight(Ped player)
	{
		if (INTERIOR.GET_INTERIOR_FROM_ENTITY(player.Handle) == 0)
		{
			_lastOutside = player.Position;
			_lastOutsideAt = Game.GameTime;
		}
		else if (Game.GameTime - _lastOutsideAt < 2000)
		{
			string pl = SaloonPlace(player);
			if (pl != null)
			{
				Bar bar = (from b in Bars
					where b.Town == pl && b.Door == Vector3.Zero
					orderby b.At.DistanceTo(player.Position)
					select b).FirstOrDefault();
				if (bar != null && bar.At.DistanceTo(player.Position) < 25f && _lastOutside.DistanceTo(player.Position) < 6f)
				{
					LearnBar(pl, bar.At, player);
				}
				else if (bar == null && _saloonStep != 0 && _saloonBar.DistanceTo(player.Position) < 25f && BarAt(pl, _saloonBar) == null)
				{
					LearnBar(pl, _saloonBar, player);
				}
			}
		}
		if (_saloonStep != 0)
		{
			RunSaloonFight(player);
			return;
		}
		int num = INTERIOR.GET_INTERIOR_FROM_ENTITY(player.Handle);
		string text = SaloonPlace(player);
		if (text != _saloonPlaceWas)
		{
			_saloonPlaceWas = text;
			_saloonRolledFor = null;
			_saloonNearLogged.Clear();
		}
		WorldState chronicle = Chronicle;
		if ((chronicle == null || chronicle.SaloonFights == 0) && _saloonRolledFor != null && _saloonRolledAt.DistanceTo(player.Position) > 80f)
		{
			_saloonRolledFor = null;
		}
		if (text != null && num == 0 && Game.GameTime >= _saloonApproachCheck)
		{
			_saloonApproachCheck = Game.GameTime + 1000;
			foreach (Vector3 item in BarsIn(text))
			{
				float num2 = item.DistanceTo(player.Position);
				if (num2 < 12f || num2 > 45f)
				{
					continue;
				}
				int num3 = INTERIOR.GET_INTERIOR_AT_COORDS(item.X, item.Y, item.Z);
				string text2 = $"{text}|{item.X:0}|{item.Y:0}";
				if (num3 == 0 || !INTERIOR.IS_INTERIOR_READY(num3))
				{
					if (_saloonNearLogged.Add(text2 + "|wait"))
					{
						GrudgeLog($"SALOON near the bar at {item.X:0},{item.Y:0} ({num2:0} m) - its building isn't loaded yet (interior {num3}); it'll roll when you walk in instead");
					}
					continue;
				}
				if ((!_forceSaloon && (_saloonRolledFor == text || Game.GameTime < _saloonCooldownUntil)) || (!_forceSaloon && MISC.GET_MISSION_FLAG()))
				{
					break;
				}
				bool forceSaloon = _forceSaloon;
				_forceSaloon = false;
				_saloonRolledFor = text;
				_saloonRolledAt = player.Position;
				if (_saloonWalkSaid != text2 || Game.GameTime - _saloonWalkSaidAt > 30000)
				{
					_saloonWalkSaid = text2;
					_saloonWalkSaidAt = Game.GameTime;
					GrudgeLog($"SALOON walking up to a bar in {text} ({num2:0} m, interior {num3})");
				}
				TryStartSaloon(player, text, item, num3, forceSaloon);
				return;
			}
		}
		if (num == 0)
		{
			_lastOutside = player.Position;
			_lastOutsideAt = Game.GameTime;
		}
		if (text == null || num == 0)
		{
			_saloonInSince = 0;
			_saloonLoggedInterior = 0;
			return;
		}
		Ped ped = BartenderNear(player, num);
		if (_saloonLoggedInterior != num || _saloonLoggedBar != (ped != null))
		{
			_saloonLoggedInterior = num;
			_saloonLoggedBar = ped != null;
			string text3;
			if (!(ped != null))
			{
				Ped ped2 = NearestBartender(player);
				text3 = "no bartender in this building" + (((object)ped2 != null) ? string.Format(" (nearest {0} {1:0} m, {2})", Models.Name(ped2), ped2.Position.DistanceTo(player.Position), (ped2.Position.DistanceTo(player.Position) <= 25f) ? "but not in here" : "too far") : "");
			}
			else
			{
				text3 = $"a bar: {Models.Name(ped)} {ped.Position.DistanceTo(player.Position):0} m";
			}
			string text4 = text3;
			string text5 = ((_saloonRolledFor == text) ? " - already rolled this visit" : ((Game.GameTime < _saloonCooldownUntil) ? $" - cooling down {(_saloonCooldownUntil - Game.GameTime) / 1000} s" : ""));
			GrudgeLog($"SALOON you're in a building in {text} (interior {num}, {player.Position.X:0},{player.Position.Y:0}) - {text4}{text5}");
		}
		if (ped == null)
		{
			_saloonInSince = 0;
			return;
		}
		if (_saloonInSince == 0)
		{
			_saloonInSince = Game.GameTime;
		}
		LearnBar(text, ped.Position, player);
		if (_forceSaloon || (!(_saloonRolledFor == text) && Game.GameTime >= _saloonCooldownUntil))
		{
			bool forceSaloon2 = _forceSaloon;
			_forceSaloon = false;
			_saloonRolledFor = text;
			_saloonRolledAt = player.Position;
			TryStartSaloon(player, text, ped.Position, num, forceSaloon2);
		}
	}

	private string SaloonPlace(Ped player)
	{
		return _currentTown ?? TownAt(player.Position) ?? DistrictAt(player.Position)?.Name ?? "the wilds";
	}

	private string BarsPath => Path.Combine(_saveDir, "bars.txt");

	private List<Bar> Bars
	{
		get
		{
			if (_bars != null)
			{
				return _bars;
			}
			_bars = new List<Bar>();
			try
			{
				if (File.Exists(BarsPath))
				{
					string[] array = File.ReadAllLines(BarsPath);
					foreach (string text in array)
					{
						string[] q = text.Split('|');
						if (q.Length >= 4)
						{
							_bars.Add(new Bar
							{
								Town = q[0],
								At = new Vector3(F(1), F(2), F(3)),
								Door = new Vector3(F(4), F(5), F(6))
							});
						}
						float F(int num)
						{
							if (num >= q.Length || !float.TryParse(q[num], NumberStyles.Float, Inv, out var result))
							{
								return 0f;
							}
							return result;
						}
					}
				}
				Bar[] shippedBars = ShippedBars;
				foreach (Bar s in shippedBars)
				{
					if (!_bars.Any((Bar b2) => b2.Town == s.Town && b2.At.DistanceTo(s.At) < 20f))
					{
						_bars.Add(new Bar
						{
							Town = s.Town,
							At = s.At,
							Door = s.Door
						});
					}
				}
				foreach (string item in Chronicle.Bars ?? new List<string>())
				{
					string[] q2 = item.Split('|');
					if (q2.Length == 4 && float.TryParse(q2[1], NumberStyles.Float, Inv, out var x) && float.TryParse(q2[2], NumberStyles.Float, Inv, out var y) && float.TryParse(q2[3], NumberStyles.Float, Inv, out var z) && !_bars.Any((Bar b2) => b2.Town == q2[0] && b2.At.DistanceTo(new Vector3(x, y, z)) < 20f))
					{
						_bars.Add(new Bar
						{
							Town = q2[0],
							At = new Vector3(x, y, z)
						});
					}
				}
			}
			catch (Exception ex)
			{
				GrudgeLog("SALOON bars.txt unreadable: " + ex.Message);
			}
			return _bars;
		}
	}

	private void SaveBars()
	{
		try
		{
			SafeFile.Write(BarsPath, string.Join("\r\n", Bars.Select((Bar b) => string.Format(Inv, "{0}|{1:0.0}|{2:0.0}|{3:0.0}|{4:0.0}|{5:0.0}|{6:0.0}", b.Town, b.At.X, b.At.Y, b.At.Z, b.Door.X, b.Door.Y, b.Door.Z))));
		}
		catch (Exception ex)
		{
			GrudgeLog("SALOON bars.txt not saved: " + ex.Message);
		}
	}

	private IEnumerable<Vector3> BarsIn(string town)
	{
		if (TownSpots.TryGetValue(town, out (Vector3?, Vector3?) value))
		{
			Vector3? item = value.Item2;
			if (item.HasValue)
			{
				Vector3 sal = item.GetValueOrDefault();
				if (!Bars.Any((Bar b) => b.Town == town && b.At.DistanceTo(sal) < 20f))
				{
					yield return sal;
				}
			}
		}
		foreach (Bar bar in Bars)
		{
			if (bar.Town == town)
			{
				yield return bar.At;
			}
		}
	}

	private Bar BarAt(string town, Vector3 at)
	{
		return Bars.FirstOrDefault((Bar b) => b.Town == town && b.At.DistanceTo(at) < 20f);
	}

	private void LearnBar(string town, Vector3 at, Ped player)
	{
		Bar bar = BarAt(town, at);
		bool flag = bar == null;
		if (flag)
		{
			bar = new Bar
			{
				Town = town,
				At = at
			};
			Bars.Add(bar);
		}
		bool flag2 = bar.Door == Vector3.Zero && Game.GameTime - _lastOutsideAt < 2000 && _lastOutside.DistanceTo(player.Position) < 6f;
		if (flag2)
		{
			bar.Door = _lastOutside;
		}
		if (flag || flag2)
		{
			SaveBars();
			GrudgeLog(string.Format("SALOON {0} in {1} at {2:0},{3:0}{4}", flag ? "learned a bar" : "bar", town, at.X, at.Y, flag2 ? $" - its door: {bar.Door.X:0},{bar.Door.Y:0} (where you came in)" : ""));
		}
	}

	private void TryStartSaloon(Ped player, string town, Vector3 bar, int interior, bool forced)
	{
		string text = StoryBansWeapons(player.Position);
		if (!forced && (MISC.GET_MISSION_FLAG() || text != null || PLAYER.GET_PLAYER_WANTED_LEVEL(Game.Player.Handle) > 0 || PED.IS_PED_IN_COMBAT(player.Handle, 0) || _enc != null || WorldBusy(player)))
		{
			string text2 = (MISC.GET_MISSION_FLAG() ? "a mission" : ((text != null) ? text : ((PLAYER.GET_PLAYER_WANTED_LEVEL(Game.Player.Handle) > 0) ? "you're wanted" : (PED.IS_PED_IN_COMBAT(player.Handle, 0) ? "you're in a fight" : ((_enc != null) ? "an encounter is on" : "the world is busy")))));
			if (_saloonRefusedWhy != town + text2)
			{
				_saloonRefusedWhy = town + text2;
				GrudgeLog("SALOON " + town + ": not now - " + text2);
			}
			_saloonRolledFor = null;
			return;
		}
		District d = DistrictAt(player.Position);
		bool war;
		(Gang, Gang)? tuple = PickSaloonPair(d, out war);
		if (!tuple.HasValue && forced)
		{
			tuple = PickSaloonPair(d, out war, anywhere: true);
		}
		if (!tuple.HasValue)
		{
			GrudgeLog("SALOON " + town + ": no two outfits who'd fight here" + (forced ? " (test)" : ""));
			return;
		}
		WorldState chronicle = Chronicle;
		int num = ((chronicle == null || chronicle.SaloonFights == 0) ? Math.Max(25, Balance.SaloonFightChance * ((!war) ? 1 : 2)) : (Balance.SaloonFightChance * ((!war) ? 1 : 2)));
		if (!forced && _rng.Next(100) >= num)
		{
			GrudgeLog($"SALOON {town}: {tuple.Value.Item1.Name} / {tuple.Value.Item2.Name} could - rolled no ({num}%)");
		}
		else
		{
			_saloonInterior = interior;
			StartSaloonFight(player, town, bar, tuple.Value.Item1, tuple.Value.Item2);
			TownHappening("a saloon fight");
		}
	}

	private (Gang a, Gang b)? PickSaloonPair(District d, out bool war, bool anywhere = false)
	{
		war = false;
		List<Gang> list = Gangs.All.Where((Gang g) => !g.StoryOnly && !IsDestroyed(g) && g.TroopModels != null && g.TroopModels.Length != 0 && g != Gangs.Wapiti && (anywhere || g.OrdersNotFeelings || HoldOf(d, g.Id) >= 3)).ToList();
		List<(Gang, Gang, double, bool)> list2 = new List<(Gang, Gang, double, bool)>();
		foreach (Gang x in list)
		{
			foreach (Gang y in list)
			{
				if (x == y)
				{
					continue;
				}
				int num = Relation(x.Id, y.Id);
				if ((anywhere || (num <= Config.ClashLine && !TruceBetween(x.Id, y.Id))) && (!anywhere || num <= -20))
				{
					bool flag = d != null && Wars.Any((WarRecord w) => w.District == d.Key && ((w.A == x.Id && w.B == y.Id) || (w.A == y.Id && w.B == x.Id)));
					double item = (double)(-num * ((!flag) ? 1 : 3)) * ((HoldOf(d, y.Id) >= HoldOf(d, x.Id)) ? 1.5 : 1.0);
					list2.Add((x, y, item, flag));
				}
			}
		}
		if (list2.Count == 0)
		{
			return null;
		}
		double num2 = _rng.NextDouble() * list2.Sum<(Gang, Gang, double, bool)>(((Gang a, Gang b, double w, bool war) c) => c.w);
		foreach (var item2 in list2)
		{
			if ((num2 -= item2.Item3) <= 0.0)
			{
				war = item2.Item4;
				return (item2.Item1, item2.Item2);
			}
		}
		(Gang, Gang, double, bool) tuple = list2.Last();
		war = tuple.Item4;
		return (tuple.Item1, tuple.Item2);
	}

	private void StartSaloonFight(Ped player, string town, Vector3 bar, Gang a, Gang b)
	{
		_saloonTown = town;
		_saloonBar = bar;
		_saloonGangA = a;
		_saloonGangB = b;
		if (Chronicle != null)
		{
			Chronicle.SaloonFights++;
			SaveWorld();
		}
		_saloonHostileA = false;
		_saloonHostileB = false;
		_saloonSeenByYou = true;
		if (_saloonGrpA == 0)
		{
			_saloonGrpA = World.AddRelationshipGroup("NEMESIS_SALOON_A");
		}
		if (_saloonGrpB == 0)
		{
			_saloonGrpB = World.AddRelationshipGroup("NEMESIS_SALOON_B");
		}
		World.SetRelationshipBetweenGroups(eRelationshipType.Respect, _saloonGrpA, _saloonGrpA);
		World.SetRelationshipBetweenGroups(eRelationshipType.Respect, _saloonGrpB, _saloonGrpB);
		World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, _saloonGrpA, _saloonGrpB);
		World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, _saloonGrpB, _saloonGrpA);
		SaloonToYou(player);
		int num = 4 + _rng.Next(2);
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		foreach (var item3 in BarPoints(player, bar))
		{
			Vector3 item = item3.pos;
			float item2 = item3.heading;
			if (_saloonA.Count >= num)
			{
				break;
			}
			Vector3 floor = FloorUnder(item);
			if (floor == Vector3.Zero)
			{
				num3++;
				continue;
			}
			if (PedSnapshot(player).Any(((Ped p, Vector3 pos) t) => t.p != null && t.p.Exists() && !t.p.IsDead && t.p.Handle != player.Handle && (t.pos - floor).Length() < 1.6f))
			{
				num4++;
				continue;
			}
			Ped ped = SpawnSaloonMan(a, floor, _saloonGrpA);
			if (!(ped == null))
			{
				ped.Heading = item2;
				_saloonA.Add(ped);
				num2++;
				Crumbs.Native("saloon drinker drinks");
				string text = (new string[3] { "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_SMOKE" })[_rng.Next(3)];
				TASK.TASK_START_SCENARIO_IN_PLACE_HASH(ped.Handle, MISC.GET_HASH_KEY(text), -1, playEnterAnim: true, 0u, -1f, p6: false);
			}
		}
		for (int num5 = _saloonA.Count; num5 < num; num5++)
		{
			Vector3 vector = OutOfSightSpot(player, 22f, 45f, ahead: false, allowSeen: true);
			if (!(vector == Vector3.Zero))
			{
				Ped ped2 = SpawnSaloonMan(a, vector, _saloonGrpA);
				if (!(ped2 == null))
				{
					_saloonA.Add(ped2);
					Vector3 vector2 = bar + new Vector3((float)(_rng.NextDouble() * 4.0 - 2.0), (float)(_rng.NextDouble() * 4.0 - 2.0), 0f);
					Crumbs.Native("saloon drinkers walk in");
					TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(ped2.Handle, vector2.X, vector2.Y, vector2.Z, 1f, -1, 1.5f, 0, 0f);
				}
			}
		}
		if (_saloonA.Count == 0)
		{
			GrudgeLog("SALOON " + town + ": no spot for the " + a.Name + " - not even outside");
			EndSaloonFight(wipe: true);
			return;
		}
		_saloonStep = ((num2 != _saloonA.Count) ? 1 : 2);
		_saloonStepAt = Game.GameTime;
		_saloonEnteredAt = ((INTERIOR.GET_INTERIOR_FROM_ENTITY(player.Handle) == _saloonInterior) ? Game.GameTime : 0);
		_saloonFightAt = ((_saloonEnteredAt != 0) ? (Game.GameTime + 15000 + _rng.Next(25000)) : int.MaxValue);
		GrudgeLog(string.Format("SALOON {0}: {1} of them already in the room, {2} walking in ({3}{4}{5})", town, num2, _saloonA.Count - num2, BarPointNote, (num3 > 0) ? $", {num3} with no floor under them" : "", (num4 > 0) ? $", {num4} next to someone" : ""));
		_saloonCooldownUntil = Game.GameTime + 1200000;
		GrudgeLog($"SALOON {town}: {_saloonA.Count} {a.Name} men come in for a drink - the {b.Name} will come for them (relation {Relation(a.Id, b.Id)})");
	}

	private unsafe List<(Vector3 pos, float heading, int pt)> BarPoints(Ped player, Vector3 bar)
	{
		List<(Vector3, float, int)> list = new List<(Vector3, float, int)>();
		ulong* ptr = stackalloc ulong[144];
		for (int i = 0; i < 144; i++)
		{
			ptr[i] = 0uL;
		}
		*ptr = 128uL;
		Crumbs.Native("saloon scenario points");
		int num = TASK.GET_SCENARIO_POINTS_IN_AREA(bar.X, bar.Y, bar.Z, 20f, ptr, 128);
		HashSet<int> hashSet = new HashSet<int>();
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		for (int j = 1; j <= Math.Min(num + 1, 128); j++)
		{
			int num8 = (int)(ptr[j] & 0xFFFFFFFFu);
			if (num8 == 0 || !hashSet.Add(num8) || !TASK.DOES_SCENARIO_POINT_EXIST(num8))
			{
				continue;
			}
			if (TASK._GET_PED_USING_SCENARIO_POINT(num8) != 0)
			{
				num2++;
				continue;
			}
			Vector3 c = TASK._GET_SCENARIO_POINT_COORDS(num8, p1: true);
			if (INTERIOR.GET_INTERIOR_AT_COORDS(c.X, c.Y, c.Z) == 0)
			{
				num3++;
			}
			else if (c.DistanceTo(player.Position) < 2f || Math.Abs(c.Z - bar.Z) > 3f)
			{
				num4++;
			}
			else if (!Enclosed(c))
			{
				num5++;
			}
			else if (!HiddenFromView(player, c))
			{
				num6++;
			}
			else if (list.Any(((Vector3, float, int) x) => x.Item1.DistanceTo(c) < 2.5f))
			{
				num7++;
			}
			else
			{
				list.Add((c, TASK._GET_SCENARIO_POINT_HEADING(num8, p1: true), num8));
			}
		}
		BarPointNote = $"{hashSet.Count} points: {list.Count} usable, {num2} in use, {num3} outdoors, {num4} upstairs / on you, {num5} not enclosed, {num6} in your view, {num7} too close together";
		return list.OrderBy(((Vector3, float, int) _) => _rng.Next()).ToList();
	}

	private bool PastDoor(Ped p)
	{
		if (p == null || !p.Exists())
		{
			return false;
		}
		if (_saloonDoor == Vector3.Zero)
		{
			if (INTERIOR.GET_INTERIOR_FROM_ENTITY(p.Handle) != 0)
			{
				return p.Position.DistanceTo(_saloonBar) < 10f;
			}
			return false;
		}
		if (p.Position.DistanceTo2D(_saloonBar) < _saloonDoor.DistanceTo2D(_saloonBar) - 1.5f)
		{
			return Math.Abs(p.Position.Z - _saloonBar.Z) < 3f;
		}
		return false;
	}

	private void PickSaloonDoor()
	{
		Bar bar = BarAt(_saloonTown, _saloonBar);
		if (bar != null && bar.Door != Vector3.Zero)
		{
			_saloonDoorOut = bar.Door;
			_saloonDoor = bar.Door + Vector3.Normalize(new Vector3(_saloonBar.X - bar.Door.X, _saloonBar.Y - bar.Door.Y, 0f)) * 1.5f;
			BarDoorNote = "the doorway you came in by";
		}
		else
		{
			_saloonDoor = BarDoor(_saloonBar, out _saloonDoorOut);
		}
	}

	private void StageShooters(Ped player)
	{
		PickSaloonDoor();
		_saloonStraight = false;
		_saloonBreached = false;
		_saloonGatherAt = 0;
		_saloonDrinkersAt = 0;
		_saloonStormAt = 0;
		Vector3 vector = ((_saloonDoor != Vector3.Zero) ? _saloonDoorOut : _saloonBar);
		int num = 4 + _rng.Next(2);
		int num2 = 0;
		int num3 = 0;
		for (int i = 0; i < num; i++)
		{
			Vector3 vector2 = Vector3.Zero;
			for (int j = 0; j < 20; j++)
			{
				if (!(vector2 == Vector3.Zero))
				{
					break;
				}
				double num4 = _rng.NextDouble() * Math.PI * 2.0;
				float num5 = 12f + (float)_rng.NextDouble() * 10f;
				Vector3 c0 = World.GetSafeCoordForPed(vector + new Vector3((float)Math.Cos(num4) * num5, (float)Math.Sin(num4) * num5, 0f), onGround: true, 0);
				if (!(c0 == Vector3.Zero) && INTERIOR.GET_INTERIOR_AT_COORDS(c0.X, c0.Y, c0.Z) == 0 && HiddenFromView(player, c0 + new Vector3(0f, 0f, 1f)) && Dry(c0) && !_saloonB.Any((Ped o) => o.Position.DistanceTo(c0) < 1.5f))
				{
					vector2 = c0;
				}
			}
			if (vector2 == Vector3.Zero)
			{
				vector2 = OutOfSightSpot(player, 25f, 45f, ahead: false, allowSeen: true);
			}
			if (vector2 == Vector3.Zero)
			{
				num2++;
				continue;
			}
			Ped ped = SpawnSaloonMan(_saloonGangB, vector2, _saloonGrpB);
			if (ped == null)
			{
				num3++;
				continue;
			}
			_saloonB.Add(ped);
			Crumbs.Native("saloon shooters wait");
			TASK.TASK_STAND_STILL(ped.Handle, -1);
		}
		if (num2 + num3 > 0)
		{
			GrudgeLog($"SALOON {_saloonTown}: {num} shooters wanted - {num2} found no hidden spot outside, {num3} didn't spawn");
		}
		GrudgeLog(string.Format("SALOON {0}: {1} {2} men waiting outside - {3} ({4}); they go in {5} s", _saloonTown, _saloonB.Count, _saloonGangB.Name, (_saloonDoor != Vector3.Zero) ? $"they'll come through the door at {_saloonDoor.X:0},{_saloonDoor.Y:0}" : "no door found - straight for the bar", BarDoorNote, Math.Max(0, (_saloonFightAt - Game.GameTime) / 1000)));
	}

	private Vector3 BarDoor(Vector3 bar, out Vector3 outside)
	{
		outside = Vector3.Zero;
		Vector3 vector = Vector3.Zero;
		float num = 999f;
		int num2 = 0;
		foreach (DoorRow allDoor in AllDoors)
		{
			Vector3 vector2 = new Vector3(allDoor.X, allDoor.Y, allDoor.Z);
			float num3 = vector2.DistanceTo2D(bar);
			if (num3 > 30f || Math.Abs(vector2.Z - bar.Z) > 3f || (allDoor.Model ?? "").Contains("gate"))
			{
				continue;
			}
			num2++;
			Vector3 vector3 = Vector3.Zero;
			float num4 = -1f;
			float[] array = new float[4] { 2.5f, 4f, 6f, 8f };
			foreach (float num5 in array)
			{
				for (int j = 0; j < 16; j++)
				{
					double num6 = (double)j * Math.PI / 8.0;
					Vector3 vector4 = GroundNear(vector2 + new Vector3((float)Math.Cos(num6) * num5, (float)Math.Sin(num6) * num5, 0f));
					if (!(vector4 == Vector3.Zero) && INTERIOR.GET_INTERIOR_AT_COORDS(vector4.X, vector4.Y, vector4.Z + 0.5f) == 0)
					{
						float num7 = vector4.DistanceTo2D(bar);
						if (num7 > num4)
						{
							num4 = num7;
							vector3 = vector4;
						}
					}
				}
			}
			if (!(vector3 == Vector3.Zero))
			{
				float num8 = num3 + ((allDoor.Model ?? "").StartsWith("door_") ? 8f : 0f);
				if (num8 < num)
				{
					num = num8;
					vector = vector2;
					outside = vector3;
				}
			}
		}
		BarDoorNote = ((vector == Vector3.Zero) ? $"{num2} doors within 30 m on its floor, none with the street outside" : $"door {vector.DistanceTo2D(bar):0} m from the bar, its outside {outside.X:0},{outside.Y:0}");
		return vector;
	}

	private unsafe static Vector3 GroundNear(Vector3 c)
	{
		float num = default(float);
		if (MISC.GET_GROUND_Z_FOR_3D_COORD(c.X, c.Y, c.Z + 1.5f, &num, p4: false) && Math.Abs(num - c.Z) < 2.5f)
		{
			return new Vector3(c.X, c.Y, num);
		}
		return Vector3.Zero;
	}

	private void SaloonTarget(Ped p)
	{
		bool flag = _saloonA.Contains(p);
		List<Ped> list = (from f in flag ? _saloonB : _saloonA
			where f != null && f.Exists() && !f.IsDead
			orderby f.Position.DistanceTo(p.Position)
			select f).ToList();
		Ped character = Game.Player.Character;
		if (list.Count == 0 && (flag ? _saloonHostileA : _saloonHostileB))
		{
			TASK.TASK_COMBAT_PED(p.Handle, character.Handle, 0, 16);
		}
		else if (list.Count != 0)
		{
			TASK.TASK_COMBAT_PED(p.Handle, list[0].Handle, 0, 16);
		}
	}

	private unsafe static Vector3 FloorUnder(Vector3 pos)
	{
		float num = default(float);
		if (MISC.GET_GROUND_Z_FOR_3D_COORD(pos.X, pos.Y, pos.Z + 0.3f, &num, p4: false) && num <= pos.Z + 0.35f && pos.Z - num < 1.3f)
		{
			return new Vector3(pos.X, pos.Y, num);
		}
		return Vector3.Zero;
	}

	private Ped SpawnSaloonMan(Gang g, Vector3 at, uint grp)
	{
		Ped ped = SpawnPed(g.TroopModels[_rng.Next(g.TroopModels.Length)], at, 0f);
		if (ped == null || !ped.Exists())
		{
			return null;
		}
		ped.RelationshipGroup = grp;
		ped.BlockPermanentEvents = true;
		try
		{
			ArmTroop(ped, g);
		}
		catch
		{
		}
		WEAPON.SET_CURRENT_PED_WEAPON(ped.Handle, 2725352035u, equipNow: true, 0, p4: false, p5: false);
		PED.SET_PED_COMBAT_ATTRIBUTES(ped.Handle, 46, enabled: true);
		NoteModel(ped);
		return ped;
	}

	private void SaloonToYou(Ped player)
	{
		uint relationshipGroup = player.RelationshipGroup;
		World.SetRelationshipBetweenGroups(_saloonHostileA ? eRelationshipType.Hate : eRelationshipType.Ignore, _saloonGrpA, relationshipGroup);
		World.SetRelationshipBetweenGroups(_saloonHostileB ? eRelationshipType.Hate : eRelationshipType.Ignore, _saloonGrpB, relationshipGroup);
	}

	private void RunSaloonFight(Ped player)
	{
		_saloonA.RemoveAll((Ped p) => p == null || !p.Exists());
		_saloonB.RemoveAll((Ped p) => p == null || !p.Exists());
		Vector3 position = player.Position;
		int num = _saloonA.Count((Ped p) => !p.IsDead);
		int num2 = _saloonB.Count((Ped p) => !p.IsDead);
		if (!_saloonHostileA && _saloonA.Any((Ped p) => ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(p.Handle, player.Handle, p2: true, p3: true)))
		{
			_saloonHostileA = true;
			SaloonToYou(player);
			GrudgeLog("SALOON you hit one of the " + _saloonGangA.Name + " - they're after you");
			foreach (Ped item in _saloonA.Where((Ped p) => !p.IsDead))
			{
				TASK.TASK_COMBAT_PED(item.Handle, player.Handle, 0, 16);
			}
		}
		if (!_saloonHostileB && _saloonB.Any((Ped p) => ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(p.Handle, player.Handle, p2: true, p3: true)))
		{
			_saloonHostileB = true;
			SaloonToYou(player);
			GrudgeLog("SALOON you hit one of the " + _saloonGangB.Name + " - they're after you");
			foreach (Ped item2 in _saloonB.Where((Ped p) => !p.IsDead))
			{
				TASK.TASK_COMBAT_PED(item2.Handle, player.Handle, 0, 16);
			}
		}
		float num3 = _saloonBar.DistanceTo(position);
		if (num3 > 150f || MISC.GET_MISSION_FLAG())
		{
			FinishSaloonFight((num3 > 150f) ? "you left" : "a mission");
			return;
		}
		switch (_saloonStep)
		{
		case 1:
			if (!_saloonA.All((Ped p) => p.IsDead || p.Position.DistanceTo(_saloonBar) < 4f) && Game.GameTime - _saloonStepAt <= 40000)
			{
				break;
			}
			foreach (Ped item3 in _saloonA.Where((Ped p) => !p.IsDead))
			{
				Crumbs.Native("saloon drinkers drink");
				TASK.TASK_START_SCENARIO_IN_PLACE_HASH(scenarioHash: MISC.GET_HASH_KEY((_rng.Next(3) == 0) ? "WORLD_HUMAN_LEAN_BARREL" : "WORLD_HUMAN_DRINKING"), ped: item3.Handle, duration: -1, playEnterAnim: true, conditionalHash: 0u, heading: -1f, p6: false);
			}
			_saloonStep = 2;
			_saloonStepAt = Game.GameTime;
			if (_saloonEnteredAt != 0)
			{
				_saloonFightAt = Math.Max(_saloonFightAt, Game.GameTime + 15000);
			}
			GrudgeLog($"SALOON {_saloonTown}: the {_saloonGangA.Name} are at the bar - the {_saloonGangB.Name} come in {(_saloonFightAt - Game.GameTime) / 1000} s");
			break;
		case 2:
		{
			if (_saloonEnteredAt == 0 && INTERIOR.GET_INTERIOR_FROM_ENTITY(player.Handle) == _saloonInterior)
			{
				_saloonEnteredAt = Game.GameTime;
				_saloonFightAt = Game.GameTime + 8000 + _rng.Next(10000);
				GrudgeLog($"SALOON {_saloonTown}: you walked in - the {_saloonGangB.Name} come in {(_saloonFightAt - Game.GameTime) / 1000} s");
			}
			if (_saloonEnteredAt == 0 && Game.GameTime - _saloonStepAt > 240000)
			{
				FinishSaloonFight("you never went in");
				break;
			}
			if (_saloonEnteredAt != 0 && _saloonB.Count == 0 && !_saloonStaged)
			{
				_saloonStaged = true;
				StageShooters(player);
			}
			if (Game.GameTime < _saloonFightAt)
			{
				break;
			}
			if (_saloonB.Count > 0)
			{
				PickSaloonDoor();
				Vector3 vector = ((_saloonDoor != Vector3.Zero) ? _saloonDoorOut : _saloonBar);
				foreach (Ped item4 in _saloonB.Where((Ped p) => !p.IsDead))
				{
					Crumbs.Native("saloon shooters go");
					TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(item4.Handle, vector.X, vector.Y, vector.Z, 2.2f, -1, 1.5f, 0, 0f);
				}
				_saloonStep = 3;
				_saloonStepAt = Game.GameTime;
				GrudgeLog("SALOON " + _saloonTown + ": the " + _saloonGangB.Name + " move on the bar - " + BarDoorNote);
				break;
			}
			int num4 = 4 + _rng.Next(2);
			PickSaloonDoor();
			_saloonStraight = false;
			_saloonBreached = false;
			_saloonGatherAt = 0;
			_saloonDrinkersAt = 0;
			_saloonStormAt = 0;
			Vector3 vector2 = ((_saloonDoor != Vector3.Zero) ? _saloonDoorOut : _saloonBar);
			for (int num5 = 0; num5 < num4; num5++)
			{
				Vector3 vector3 = Vector3.Zero;
				for (int num6 = 0; num6 < 14; num6++)
				{
					if (!(vector3 == Vector3.Zero))
					{
						break;
					}
					double num7 = _rng.NextDouble() * Math.PI * 2.0;
					float num8 = 25f + (float)_rng.NextDouble() * 20f;
					Vector3 safeCoordForPed = World.GetSafeCoordForPed(vector2 + new Vector3((float)Math.Cos(num7) * num8, (float)Math.Sin(num7) * num8, 0f), onGround: true, 0);
					if (!(safeCoordForPed == Vector3.Zero) && INTERIOR.GET_INTERIOR_AT_COORDS(safeCoordForPed.X, safeCoordForPed.Y, safeCoordForPed.Z) == 0 && HiddenFromView(player, safeCoordForPed + new Vector3(0f, 0f, 1f)) && Dry(safeCoordForPed))
					{
						vector3 = safeCoordForPed;
					}
				}
				if (vector3 == Vector3.Zero)
				{
					vector3 = OutOfSightSpot(player, 25f, 45f, ahead: false, allowSeen: true);
				}
				if (!(vector3 == Vector3.Zero))
				{
					Ped ped = SpawnSaloonMan(_saloonGangB, vector3, _saloonGrpB);
					if (!(ped == null))
					{
						_saloonB.Add(ped);
						Crumbs.Native("saloon shooters run in");
						TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(ped.Handle, vector2.X, vector2.Y, vector2.Z, 2.2f, -1, 1.5f, 0, 0f);
					}
				}
			}
			GrudgeLog("SALOON " + _saloonTown + ": " + ((_saloonDoor != Vector3.Zero) ? $"they'll come through the door at {_saloonDoor.X:0},{_saloonDoor.Y:0}" : "no door found - straight for the bar") + " (" + BarDoorNote + ")");
			if (_saloonB.Count == 0)
			{
				FinishSaloonFight("the shooters had no way in");
				break;
			}
			_saloonStep = 3;
			_saloonStepAt = Game.GameTime;
			GrudgeLog($"SALOON {_saloonTown}: {_saloonB.Count} {_saloonGangB.Name} men are coming");
			break;
		}
		case 3:
		{
			if (_saloonDoor != Vector3.Zero && !_saloonBreached)
			{
				List<Ped> list = _saloonB.Where((Ped p) => !p.IsDead).ToList();
				foreach (Ped item5 in list.Where((Ped p) => !_saloonThrough.Contains(p.Handle) && p.Position.DistanceTo(_saloonDoorOut) < 5f))
				{
					_saloonThrough.Add(item5.Handle);
					Crumbs.Native("saloon draws at the door");
					WEAPON.SET_CURRENT_PED_WEAPON(item5.Handle, WEAPON.GET_BEST_PED_WEAPON(item5.Handle, p1: false, p2: false), equipNow: true, 0, p4: false, p5: false);
					TASK.TASK_STAND_STILL(item5.Handle, -1);
					if (_saloonGatherAt == 0)
					{
						_saloonGatherAt = Game.GameTime;
					}
				}
				if (_saloonGatherAt != 0 && (list.All((Ped p) => _saloonThrough.Contains(p.Handle)) || Game.GameTime - _saloonGatherAt > 3000))
				{
					_saloonBreached = true;
					foreach (Ped item6 in list)
					{
						Crumbs.Native("saloon through the door");
						WEAPON.SET_CURRENT_PED_WEAPON(item6.Handle, WEAPON.GET_BEST_PED_WEAPON(item6.Handle, p1: false, p2: false), equipNow: true, 0, p4: false, p5: false);
						TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(item6.Handle, _saloonBar.X, _saloonBar.Y, _saloonBar.Z, 2f, -1, 3f, 0, 0f);
					}
					_saloonStormAt = Game.GameTime + 1200;
					GrudgeLog($"SALOON {_saloonTown}: {list.Count} of them through the door together, guns out");
				}
			}
			if (!_saloonStraight && Game.GameTime - _saloonStepAt > 30000)
			{
				_saloonStraight = true;
				foreach (Ped item7 in _saloonB.Where((Ped p) => !p.IsDead))
				{
					TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(item7.Handle, _saloonBar.X, _saloonBar.Y, _saloonBar.Z, 2f, -1, 3f, 0, 0f);
				}
				GrudgeLog("SALOON " + _saloonTown + ": nobody got through the door in 30 s - straight for the bar");
			}
			bool flag4 = _saloonDoor != Vector3.Zero && !_saloonStraight && !_saloonBreached;
			if ((_saloonStormAt == 0 || Game.GameTime < _saloonStormAt) && (flag4 || !_saloonB.Any((Ped p) => !p.IsDead && (PastDoor(p) || _saloonA.Any((Ped a2) => !a2.IsDead && a2.Position.DistanceTo(p.Position) < 5f)))) && Game.GameTime - _saloonStepAt <= 70000 && !_saloonA.Any((Ped p) => PED.IS_PED_SHOOTING(p.Handle)) && !_saloonB.Any((Ped p) => p.IsDead))
			{
				break;
			}
			World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _saloonGrpA, _saloonGrpB);
			World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _saloonGrpB, _saloonGrpA);
			foreach (Ped item8 in from p in _saloonA.Concat(_saloonB)
				where !p.IsDead
				select p)
			{
				if (_saloonB.Contains(item8) && !PastDoor(item8) && item8.Position.DistanceTo(_saloonDoor) > 4f)
				{
					Crumbs.Native("saloon still coming in");
					WEAPON.SET_CURRENT_PED_WEAPON(item8.Handle, WEAPON.GET_BEST_PED_WEAPON(item8.Handle, p1: false, p2: false), equipNow: true, 0, p4: false, p5: false);
					TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(item8.Handle, _saloonBar.X, _saloonBar.Y, _saloonBar.Z, 2f, -1, 3f, 0, 0f);
				}
				else if (!_saloonA.Contains(item8))
				{
					Crumbs.Native("saloon fight");
					TASK.CLEAR_PED_TASKS(item8.Handle, p1: false, p2: false);
					SaloonTarget(item8);
				}
			}
			_saloonDrinkersAt = Game.GameTime + 1500 + _rng.Next(1000);
			string[] array = new string[4]
			{
				"There they are! " + _saloonGangA.Name + " scum!",
				"Drink up, boys - it's your last!",
				"Nobody drinks in this town but us!",
				"The " + _saloonGangB.Name + " send their regards!"
			};
			if (position.DistanceTo(_saloonBar) < 40f)
			{
				ShowSubtitle(array[_rng.Next(array.Length)]);
			}
			_saloonStep = 4;
			_saloonStepAt = Game.GameTime;
			GrudgeLog("SALOON " + _saloonTown + ": the " + _saloonGangB.Name + " open fire on the " + _saloonGangA.Name);
			break;
		}
		case 4:
			if (_saloonDrinkersAt != 0 && Game.GameTime >= _saloonDrinkersAt)
			{
				_saloonDrinkersAt = 0;
				foreach (Ped item9 in _saloonA.Where((Ped p) => !p.IsDead))
				{
					Crumbs.Native("saloon drinkers react");
					TASK.CLEAR_PED_TASKS(item9.Handle, p1: false, p2: false);
					SaloonTarget(item9);
				}
			}
			if (Game.GameTime >= _saloonRetargetAt)
			{
				_saloonRetargetAt = Game.GameTime + 700;
				foreach (Ped item10 in from p in _saloonA.Concat(_saloonB)
					where !p.IsDead
					select p)
				{
					bool flag = _saloonA.Contains(item10);
					if ((flag && _saloonDrinkersAt != 0 && !PED.IS_PED_IN_COMBAT(item10.Handle, 0)) || (!flag && !PastDoor(item10) && Game.GameTime - _saloonStepAt < 12000 && !PED.IS_PED_IN_COMBAT(item10.Handle, 0)))
					{
						continue;
					}
					bool flag2 = (flag ? _saloonHostileA : _saloonHostileB);
					bool flag3 = PED.IS_PED_IN_COMBAT(item10.Handle, player.Handle);
					if ((flag3 && !flag2) || !PED.IS_PED_IN_COMBAT(item10.Handle, 0))
					{
						if (flag3 && !flag2)
						{
							GrudgeLog($"SALOON {(flag ? _saloonGangA.Name : _saloonGangB.Name)} man {item10.Handle} turned on you - sent back at the other side");
						}
						Crumbs.Native("saloon retarget");
						TASK.CLEAR_PED_TASKS(item10.Handle, p1: false, p2: false);
						SaloonTarget(item10);
					}
				}
			}
			if (num == 0 || num2 == 0 || Game.GameTime - _saloonStepAt > 240000)
			{
				FinishSaloonFight((num == 0) ? ("the " + _saloonGangA.Name + " are all down") : ((num2 == 0) ? ("the " + _saloonGangB.Name + " are all down") : "4 minutes"));
			}
			break;
		}
	}

	private void FinishSaloonFight(string why)
	{
		int num = _saloonA.Count((Ped p) => p.Exists() && p.IsDead);
		int num2 = _saloonB.Count((Ped p) => p.Exists() && p.IsDead);
		if (_saloonStep >= 4 && _saloonGangA != null && _saloonGangB != null)
		{
			Gang gang = ((num >= num2) ? _saloonGangB : _saloonGangA);
			Gang g = ((gang == _saloonGangA) ? _saloonGangB : _saloonGangA);
			ShiftRelation(_saloonGangA.Id, _saloonGangB.Id, -8, "shot up the " + _saloonTown + " saloon");
			LoseMen(_saloonGangA, num, "a saloon fight");
			LoseMen(_saloonGangB, num2, "a saloon fight");
			AddGrievance(_saloonGangA.Id, _saloonGangB.Id, 3 * (num + num2) / 2, "the " + _saloonTown + " saloon");
			District district = DistrictAt(_saloonBar);
			if (district != null)
			{
				ShiftControl(district, gang, 3, "won the fight in the " + _saloonTown + " saloon");
				ShiftControl(district, g, -LossDown(district, g, 3), "lost the fight in the " + _saloonTown + " saloon");
			}
			List<string> list = new List<string>();
			list.Add("The " + _saloonGangA.Name + " were drinking at the bar when the " + _saloonGangB.Name + " came through the doors shooting.");
			list.Add($"{num} of the {_saloonGangA.Name} and {num2} of the {_saloonGangB.Name} dead. The {gang.Name} held the room.");
			List<string> list2 = list;
			if (_saloonHostileA || _saloonHostileB)
			{
				list2.Add("You were in the middle of it.");
			}
			try
			{
				Dictionary<string, int> unnamedDead = new Dictionary<string, int>
				{
					[_saloonGangA.Id] = num,
					[_saloonGangB.Id] = num2
				};
				ChronicleWitness("The " + _saloonGangB.Name + " shot up the " + _saloonTown + " saloon", "in " + _saloonTown, _saloonBar, new string[2] { _saloonGangB.Id, _saloonGangA.Id }, new List<WorldPart>(), unnamedDead, list2);
			}
			catch (Exception ex)
			{
				GrudgeLog("SALOON chronicle failed: " + ex.Message);
			}
		}
		GrudgeLog($"SALOON {_saloonTown}: over ({why}) - {num} {_saloonGangA?.Name} / {num2} {_saloonGangB?.Name} dead");
		EndSaloonFight(wipe: false);
	}

	internal void EndSaloonFight(bool wipe)
	{
		foreach (Ped item in _saloonA.Concat(_saloonB).ToList())
		{
			if (!(item == null) && item.Exists())
			{
				if (wipe)
				{
					Wipe(item);
				}
				else
				{
					DeleteOrRelease(item);
				}
			}
		}
		_saloonA.Clear();
		_saloonB.Clear();
		_saloonThrough.Clear();
		_saloonDoor = Vector3.Zero;
		_saloonStaged = false;
		_saloonStep = 0;
		_saloonGangA = (_saloonGangB = null);
	}
}
