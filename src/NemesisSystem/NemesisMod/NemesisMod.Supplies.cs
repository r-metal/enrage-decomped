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
	private sealed class SupplyCrate
	{
		public int Obj;

		public Vector3 At;

		public bool Gone;

		public bool Big;

		public int Start = -1;

		public int Last = -1;

		public int Hits;

		public int FireTicks;
	}

	private sealed class LiveSupply
	{
		public string SiteId;

		public Gang Gang;

		public readonly List<SupplyCrate> Crates = new List<SupplyCrate>();

		public readonly List<int> Barrels = new List<int>();

		public Vehicle Wagon;

		public Vector3 WagonAt;

		public bool WagonCounted;

		public readonly List<(Ped horse, Vector3 at)> Horses = new List<(Ped, Vector3)>();

		public readonly HashSet<int> HorsesCounted = new HashSet<int>();

		public bool Burned;

		public bool Hit;

		public bool Town;

		public int Lost;

		public readonly List<(Vector3 at, int t)> FireSpots = new List<(Vector3, int)>();

		public readonly List<int> Fires = new List<int>();

		public bool CapSaid;

		public readonly List<int> DebugBlips = new List<int>();

		public readonly List<Ped> Men = new List<Ped>();

		public bool Destroyed;
	}

	private sealed class TelegraphPanel
	{
		public Ped Clerk;

		public string Town;

		public string Page = "main";

		public int Sel;

		public int Top;

		public List<TgInfo> Info = new List<TgInfo>();

		public (string id, bool nemesis, string name) To;

		public Telegram Reading;

		public string ReadFrom;

		public string ReplyTo;

		public string Line;

		public List<(string key, string text, bool enabled, string note)> Rows = new List<(string, string, bool, string)>();
	}

	private sealed class TgInfo
	{
		public string A;

		public string B;

		public string C;

		public string D;

		public string Detail;

		public (int r, int g, int b) Bc = Theme.Text;

		public (int r, int g, int b) Cc = Theme.Dim;

		public (int r, int g, int b) Dc = Theme.Dim;

		public bool Heading;

		public bool Wait;

		public bool New;

		public bool Right;
	}

	private int _nextCacheSpawn;

	private int _nextCacheBlips;

	private readonly HashSet<string> _cacheNoKeeperSaid = new HashSet<string>();

	private readonly Dictionary<string, (int blip, string label)> _cacheBlips = new Dictionary<string, (int, string)>();

	private static readonly string[] LooseCrateModels = new string[6] { "p_crate03x", "p_crate01x", "p_crate02x", "p_crate04x", "p_crate14x", "p_cratestack01x" };

	private readonly Dictionary<string, LiveSupply> _liveSupplies = new Dictionary<string, LiveSupply>();

	private static readonly string[] SupplyWagonModels = new string[8] { "supplywagon", "wagon05x", "wagon06x", "wagonwork01x", "utilliwag", "chuckwagon000x", "chuckwagon002x", "cart06" };

	private int _nextSupplyCheck;

	private int _pCrate;

	private bool _crateArmed;

	private SupplyCrate _crateOpening;

	private LiveSupply _crateOpeningIn;

	private int _crateOpenUntil;

	private const int RaidCap = 40;

	private const int SupplyGroupId = 8280594;

	private Dictionary<string, int> SupplyMap => Chronicle.Supplies ?? (Chronicle.Supplies = new Dictionary<string, int>());

	internal int SuppliesOf(Gang g)
	{
		if (g == null || g.StoryOnly)
		{
			return 100;
		}
		if (!SupplyMap.TryGetValue(g.Id, out var value))
		{
			if (!g.OrdersNotFeelings)
			{
				return 70;
			}
			return 80;
		}
		return value;
	}

	internal void AddSupplies(Gang g, int delta, string why)
	{
		if (g != null && !g.StoryOnly && delta != 0)
		{
			int num = SuppliesOf(g);
			int num2 = Math.Max(0, Math.Min(100, num + delta));
			if (num2 != num)
			{
				SupplyMap[g.Id] = num2;
				GrudgeLog($"SUPPLIES {g.Name}: {num} -> {num2} ({why})");
				SaveWorld();
			}
		}
	}

	private void AddSuppliesFrac(Gang g, double delta, string why)
	{
		if (g != null && !g.StoryOnly)
		{
			_supplyFrac.TryGetValue(g.Id, out var value);
			value += delta;
			int num = (int)Math.Truncate(value);
			_supplyFrac[g.Id] = value - (double)num;
			if (num != 0)
			{
				AddSupplies(g, num, why);
			}
		}
	}

	internal static string SupplyState(int v)
	{
		if (v > 0)
		{
			if (v >= 25)
			{
				if (v >= 50)
				{
					if (v >= 80)
					{
						return "Plenty";
					}
					return "Stocked";
				}
				return "Short";
			}
			return "Low";
		}
		return "Out";
	}

	internal static (int r, int g, int b) SupplyColor(int v)
	{
		if (v >= 25)
		{
			if (v >= 50)
			{
				return Theme.Good;
			}
			return Theme.Warn;
		}
		return Theme.Blood;
	}

	internal static string SupplyWord(Gang g)
	{
		return g?.Id switch
		{
			"odriscoll" => "stolen goods", 
			"lemoyneraiders" => "the cause's stores", 
			"army" => "the depot", 
			"klan" => "the lodge's stores", 
			"wapiti" => "the herd and the hunt", 
			"bronte" => "the family's warehouses", 
			"murfree" => "what they've hoarded", 
			"skinners" => "hides and powder", 
			_ => "supplies", 
		};
	}

	internal bool ShortOfArms(Gang g)
	{
		if (g != null && !g.StoryOnly)
		{
			return SuppliesOf(g) < 50;
		}
		return false;
	}

	internal bool LowSupplies(Gang g)
	{
		if (g != null && !g.StoryOnly)
		{
			return SuppliesOf(g) < 25;
		}
		return false;
	}

	private void DailySupplies(int days)
	{
		int num = Math.Min(days, 30);
		foreach (Gang item in Gangs.All.Where((Gang x) => !x.StoryOnly && !IsDestroyed(x)))
		{
			double num2 = 2.0 + 6.0 * Math.Min(1.2, TerritoryShare(item));
			if (item.OrdersNotFeelings)
			{
				num2 += 5.0;
			}
			if (item == Gangs.Wapiti)
			{
				num2 += 3.0;
			}
			double num3 = 4.0 * Math.Min(1.5, MenShare(item)) + (double)(3 * WarsOf(item));
			if (SuppliesOf(item) < 10)
			{
				num3 = Math.Min(num3, num2 - 0.5);
			}
			_supplyFrac.TryGetValue("day:" + item.Id, out var value);
			double num4 = (num2 - num3) * (double)num + value;
			int num5 = (int)Math.Truncate(num4);
			_supplyFrac["day:" + item.Id] = num4 - (double)num5;
			int num6 = 0;
			if (HasPurse(item) && !item.OrdersNotFeelings && SuppliesOf(item) + num5 < 60 && PurseOf(item) > 600)
			{
				int num7 = Math.Min(num, Math.Min(3, (PurseOf(item) - 600) / 40));
				if (num7 > 0)
				{
					AddPurse(item, -40 * num7, $"bought supplies ({num7} day(s))");
					num6 = 8 * num7;
				}
			}
			AddSupplies(item, num5 + num6, string.Format("{0} day(s): +{1:0.#}/day from its ground, -{2:0.#}/day for its men{3}{4}", num, num2, num3, (WarsOf(item) > 0) ? " and wars" : "", (num6 > 0) ? $", bought {num6}" : ""));
		}
	}

	private void SuppliesForLosses(Gang g, int men)
	{
		if (men > 0)
		{
			AddSuppliesFrac(g, -100.0 * (double)men / (double)Math.Max(1, MenStart(g)), "fighting");
		}
	}

	private Gang TopHolderHere(Ped p)
	{
		District d = DistrictAt(p.Position);
		if (d == null)
		{
			return null;
		}
		return (from g in Gangs.All
			where !g.StoryOnly && !IsDestroyed(g) && HoldOf(d, g.Id) > 0
			orderby HoldOf(d, g.Id) descending
			select g).FirstOrDefault();
	}

	private void DebugSupplies(Ped p, int by)
	{
		Gang gang = TopHolderHere(p);
		if (gang == null)
		{
			SpawnerSays("no outfit holds ground here.");
			return;
		}
		int num = SuppliesOf(gang);
		AddSupplies(gang, by, "the spawner (debug)");
		SpawnerSays($"the {OutfitPoss(gang)} supplies: {num} -> {SuppliesOf(gang)} ({SupplyState(SuppliesOf(gang))}).");
	}

	internal string SupplyLine()
	{
		return "supplies: " + string.Join(", ", from g in Gangs.All
			where !g.StoryOnly && !IsDestroyed(g)
			select $"{g.Id} {SuppliesOf(g)}");
	}

	private void UpdateSupplyCaches(Ped player)
	{
		if (Game.GameTime < _nextCacheSpawn)
		{
			return;
		}
		_nextCacheSpawn = Game.GameTime + 1000;
		if (Game.GameTime >= _nextCacheBlips)
		{
			_nextCacheBlips = Game.GameTime + 15000;
			RefreshCacheBlips();
		}
		Vector3 position = player.Position;
		foreach (LiveSupply item in _liveSupplies.Values.ToList())
		{
			if (!item.Town && item.Gang != null)
			{
				CampSite campSite = Camps.ById(item.SiteId);
				if (campSite != null && !(position.DistanceTo(SitePos(campSite)) > 60f) && (!Hold.SupplyKnownAt.TryGetValue(item.Gang.Id, out var value) || !(value == campSite.Id)))
				{
					Hold.KnowSupplies(item.Gang, campSite);
					SaveWorld();
					GrudgeLog("SUPPLY " + item.Gang.Name + ": you found their supplies - " + campSite.Label);
					RedoCampBlip(campSite.Id);
					RefreshCacheBlips();
				}
			}
		}
		foreach (string item2 in _unguardedCaches.ToList())
		{
			CampSite campSite2 = Camps.ById(item2);
			if (campSite2 == null || position.DistanceTo(SitePos(campSite2)) > 260f || !IsSupplyCamp(campSite2))
			{
				DespawnUnguardedCache(item2, "you rode off");
			}
		}
		if (SupplyCaches.Count == 0 || player.IsDead || NoSpawnReason(player, place: true) != null)
		{
			return;
		}
		foreach (string item3 in SupplyCaches.Keys.ToList())
		{
			Gang gang = Gangs.ById(item3);
			CampSite campSite3 = SupplyCampOf(gang);
			if (campSite3 == null)
			{
				continue;
			}
			float num = position.DistanceTo(SitePos(campSite3));
			if (_liveSupplies.ContainsKey(campSite3.Id))
			{
				if (num < 60f && !SupplyCacheKnown(gang))
				{
					FoundSupplyCache(gang, campSite3);
				}
			}
			else if (!_liveCamps.ContainsKey(campSite3.Id) && !(num > Config.CampSpawnRange) && !(num < 60f) && HiddenFromView(player, SitePos(campSite3) + new Vector3(0f, 0f, 1.2f)) && TownSiteReady(campSite3))
			{
				NemesisRecord nemesisRecord = ((CacheGuardsDue(gang) && !IsCleared(campSite3)) ? SupplyKeeper(gang) : null);
				if (nemesisRecord != null)
				{
					GrudgeLog($"SUPPLY {gang.Name}: their cache at {campSite3.Label} ({num:0} m) - kept by {nemesisRecord.Name}");
					SpawnCamp(campSite3, nemesisRecord, player);
					break;
				}
				if (CacheGuardsDue(gang) && !IsCleared(campSite3) && _cacheNoKeeperSaid.Add(campSite3.Id))
				{
					GrudgeLog("SUPPLY " + gang.Name + ": none of theirs free to keep the cache at " + campSite3.Label);
				}
				GrudgeLog($"SUPPLY {gang.Name}: their cache at {campSite3.Label} ({num:0} m) - unguarded");
				try
				{
					Kit.Spawn(campSite3, gang.Id, supply: true);
				}
				catch (Exception ex)
				{
					GrudgeLog("CAMPKIT failed: " + ex.Message);
				}
				_unguardedCaches.Add(campSite3.Id);
				try
				{
					SpawnSupplies(campSite3);
					break;
				}
				catch (Exception ex2)
				{
					GrudgeLog("SUPPLY cache stores failed: " + ex2.Message);
					break;
				}
			}
		}
	}

	private void DespawnUnguardedCache(string siteId, string why)
	{
		if (_unguardedCaches.Remove(siteId))
		{
			try
			{
				DespawnSupplies(siteId);
			}
			catch (Exception ex)
			{
				GrudgeLog("SUPPLY cache delete failed: " + ex.Message);
			}
			try
			{
				DespawnCampKit(siteId);
			}
			catch (Exception ex2)
			{
				GrudgeLog("CAMPKIT delete failed: " + ex2.Message);
			}
			GrudgeLog("SUPPLY cache at " + (Camps.ById(siteId)?.Label ?? siteId) + ": " + why + " - taken down");
		}
	}

	private void DespawnSupplyCaches()
	{
		foreach (string item in _unguardedCaches.ToList())
		{
			DespawnUnguardedCache(item, "cleanup");
		}
	}

	private void FoundSupplyCache(Gang g, CampSite site)
	{
		Hold.KnowSupplies(g, site);
		SaveWorld();
		RefreshCacheBlips();
		GrudgeLog("SUPPLY " + g.Name + ": you found their cache - " + site.Label);
		ReplyNotice("You've found the " + OutfitPoss(g) + " supplies. Marked on your map.");
	}

	private void RevealSupplyCache(Gang g, CampSite site, string by)
	{
		Hold.KnowSupplies(g, site);
		SaveWorld();
		RefreshCacheBlips();
		GrudgeLog("SUPPLY " + g.Name + ": you know the cache now - " + site.Label + " (from " + by + ")");
	}

	private unsafe void RefreshCacheBlips()
	{
		if (!MapSettled())
		{
			_mapPending.Add(RefreshCacheBlips);
			return;
		}
		if (_mapHidden)
		{
			CleanupCacheBlips();
			return;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (string key in SupplyCaches.Keys)
		{
			Gang g = Gangs.ById(key);
			CampSite campSite = SupplyCampOf(g);
			if (campSite != null && SupplyCacheKnown(g))
			{
				bool flag = (CacheGuardsDue(g) && !IsCleared(campSite) && SupplyKeeper(g) != null) || _liveCamps.ContainsKey(campSite.Id);
				dictionary[campSite.Id] = MemberWord(g) + " Supplies - cache" + (flag ? "" : " (unguarded)") + (SupplyRaidedRecently(g) ? " (raided)" : "");
			}
		}
		foreach (string item2 in _cacheBlips.Keys.ToList())
		{
			if (!dictionary.TryGetValue(item2, out var value) || !(value == _cacheBlips[item2].label))
			{
				int item = _cacheBlips[item2].blip;
				Crumbs.Native("supply cache blip remove");
				MAP.REMOVE_BLIP(&item);
				_cacheBlips.Remove(item2);
			}
		}
		foreach (KeyValuePair<string, string> item3 in dictionary)
		{
			if (!_cacheBlips.ContainsKey(item3.Key))
			{
				CampSite campSite2 = Camps.ById(item3.Key);
				Crumbs.Native("supply cache blip add");
				bool flag2 = item3.Value.Contains("(unguarded)");
				int num = MAP.BLIP_ADD_FOR_COORDS(flag2 ? 2545348716u : 2339406903u, campSite2.X, campSite2.Y, campSite2.Z);
				if (num != 0)
				{
					MAP.SET_BLIP_SPRITE(num, MISC.GET_HASH_KEY("blip_chest"), p2: true);
					Function.Call(11290983183885202434uL, num, new InputArgument(UiDraw.ForeverVarString(item3.Value)));
					_cacheBlips[item3.Key] = (num, item3.Value);
					GrudgeLog("SUPPLY cache marker: \"" + item3.Value + "\" at " + campSite2.Label + " (" + (flag2 ? "white" : "red") + ")");
				}
			}
		}
	}

	private unsafe void CleanupCacheBlips()
	{
		foreach (string item2 in _cacheBlips.Keys.ToList())
		{
			int item = _cacheBlips[item2].blip;
			MAP.REMOVE_BLIP(&item);
		}
		_cacheBlips.Clear();
	}

	private void UpdateSupplyCamps(Ped player)
	{
		if (!Config.CampsEnabled)
		{
			return;
		}
		try
		{
			UpdateSupplyCaches(player);
		}
		catch (Exception ex)
		{
			GrudgeLog("SUPPLY cache failed: " + ex.Message);
		}
		UpdateSupplyHits(player);
		try
		{
			UpdateCampObjectives(player);
		}
		catch (Exception ex2)
		{
			GrudgeLog("OBJECTIVE failed: " + ex2.Message);
		}
	}

	private void SetCrateAlight(LiveSupply ls, Vector3 at, bool big)
	{
		try
		{
			Crumbs.Native("supply crate fire");
			int num = FIRE.START_SCRIPT_FIRE(at.X, at.Y, at.Z + 0.2f, big ? 12 : 6, big ? 2f : 1f, p5: false, "", 0f, 0);
			if (num != 0)
			{
				ls.Fires.Add(num);
			}
			ls.FireSpots.Add((at, Game.GameTime));
		}
		catch (Exception ex)
		{
			GrudgeLog("SUPPLY crate fire failed: " + ex.Message);
		}
	}

	private void PutOutCrateFires(LiveSupply ls)
	{
		foreach (int fire in ls.Fires)
		{
			try
			{
				Crumbs.Native("supply fire out");
				Function.Call(8719291874861989993uL, fire);
			}
			catch
			{
			}
		}
		ls.Fires.Clear();
	}

	private void AddLooseCrate(LiveSupply ls, Vector3 at)
	{
		_propModel.TryGetValue("loosecrate", out var value);
		int num = MakeCheckpointProp(LooseCrateModels, at, _rng.Next(360), ref value, freeze: false);
		if (value != null)
		{
			_propModel["loosecrate"] = value;
		}
		if (num != 0)
		{
			int num2 = ENTITY.GET_ENTITY_HEALTH(num);
			ls.Crates.Add(new SupplyCrate
			{
				Obj = num,
				At = at,
				Start = num2,
				Last = num2
			});
		}
	}

	internal static bool OpenGround(Vector3 at, float reach)
	{
		Vector3 vector = at + new Vector3(0f, 0f, 0.6f);
		if (Ray.Blocked(vector, vector + new Vector3(0f, 0f, 5f), 17, 0))
		{
			return false;
		}
		Vector3[] array = new Vector3[4]
		{
			new Vector3(reach, 0f, 0f),
			new Vector3(0f - reach, 0f, 0f),
			new Vector3(0f, reach, 0f),
			new Vector3(0f, 0f - reach, 0f)
		};
		foreach (Vector3 vector2 in array)
		{
			if (Ray.Blocked(vector, vector + vector2, 17, 0))
			{
				return false;
			}
		}
		return true;
	}

	private void SpawnSupplies(CampSite site)
	{
		Gang gang = SupplyGangOf(site);
		if (gang == null || _liveSupplies.ContainsKey(site.Id))
		{
			return;
		}
		LiveSupply liveSupply = new LiveSupply
		{
			SiteId = site.Id,
			Gang = gang
		};
		_liveSupplies[site.Id] = liveSupply;
		bool flag = SupplyRaidedRecently(gang);
		Vector3 c = Kit.Centre(site);
		int num = (flag ? 2 : 4);
		double num2 = _rng.NextDouble() * Math.PI * 2.0;
		int moved = 0;
		int dropped = 0;
		for (int i = 0; i < num; i++)
		{
			Vector3 vector = Edge(num2 + (double)i * 0.55, 7.5f + (float)_rng.NextDouble() * 2f);
			if (vector == Vector3.Zero || !Dry(vector))
			{
				continue;
			}
			if (i < 2)
			{
				_propModel.TryGetValue("crate", out var value);
				int num3 = MakeCheckpointProp(BigCrateModels, vector, _rng.Next(360), ref value);
				if (value != null)
				{
					_propModel["crate"] = value;
				}
				if (num3 != 0)
				{
					liveSupply.Crates.Add(new SupplyCrate
					{
						Obj = num3,
						At = vector,
						Big = true
					});
				}
				continue;
			}
			for (int j = 0; j < 2; j++)
			{
				Vector3 vector2 = ((j == 0) ? vector : GroundOnSpot(vector + new Vector3(1.1f, 0.6f, 0f)));
				if (!(vector2 == Vector3.Zero) && (j <= 0 || OpenGround(vector2, 0.7f)))
				{
					AddLooseCrate(liveSupply, vector2);
				}
			}
		}
		for (int k = 0; k < 2; k++)
		{
			Vector3 vector3 = Edge(num2 + (double)num * 0.55 + (double)k * 0.4, 8f);
			if (!(vector3 == Vector3.Zero))
			{
				_propModel.TryGetValue("barrel", out var value2);
				int num4 = MakeCheckpointProp(BarrelModels, vector3, _rng.Next(360), ref value2);
				if (value2 != null)
				{
					_propModel["barrel"] = value2;
				}
				if (num4 != 0)
				{
					liveSupply.Barrels.Add(num4);
				}
			}
		}
		Vector3 vector4 = Pick(num2 + Math.PI, 11f, 3.2f);
		string text = null;
		if (!flag && vector4 != Vector3.Zero && Dry(vector4))
		{
			foreach (string item in SupplyWagonModels.OrderBy((string _) => _rng.Next()).Concat(new string[2] { "wagon04x", "cart03" }))
			{
				uint model = MISC.GET_HASH_KEY(item);
				if (STREAMING.IS_MODEL_IN_CDIMAGE(model) && STREAMING.IS_MODEL_A_VEHICLE(model))
				{
					Crumbs.Native("supply wagon");
					Vehicle vehicle = SpawnVehicle((VehicleHash)model, vector4 + new Vector3(0f, 0f, 0.3f), _rng.Next(360));
					if (!(vehicle == null))
					{
						MakeOurs(vehicle.Handle);
						VEHICLE.SET_VEHICLE_ON_GROUND_PROPERLY(vehicle.Handle, p1: false);
						liveSupply.Wagon = vehicle;
						liveSupply.WagonAt = vehicle.Position;
						text = item;
						break;
					}
				}
			}
		}
		int num5 = (flag ? 1 : (2 + _rng.Next(2)));
		for (int num6 = 0; num6 < num5; num6++)
		{
			Vector3 vector5 = Pick(num2 + Math.PI + 0.45 + (double)num6 * 0.22, 13f, 1.6f);
			if (vector5 == Vector3.Zero)
			{
				continue;
			}
			Crumbs.Native("supply horse");
			Ped ped = null;
			for (int num7 = 0; num7 < 3; num7++)
			{
				if (!(ped == null))
				{
					break;
				}
				ped = SpawnPed(RiderHorses[_rng.Next(RiderHorses.Length)], vector5, _rng.Next(360));
			}
			if (!(ped == null))
			{
				MakeOurs(ped.Handle);
				TASK.TASK_STAND_STILL(ped.Handle, -1);
				liveSupply.Horses.Add((ped, vector5));
			}
		}
		GrudgeLog(string.Format("SUPPLY {0}: {1} - {2} crate stacks, {3}, {4} horses{5}; supplies {6}; open ground: {7} moved, {8} dropped", gang.Name, site.Label, liveSupply.Crates.Count, (liveSupply.Wagon != null) ? ("a wagon (" + text + ")") : "no wagon", liveSupply.Horses.Count, flag ? " (raided - not restocked yet)" : "", SuppliesOf(gang), moved, dropped));
		Vector3 Edge(double a, float r)
		{
			return Pick(a, r, 1.1f);
		}
		Vector3 Pick(double a, float r, float reach)
		{
			for (int l = 0; l < 10; l++)
			{
				Vector3 vector6 = Raw((l == 0) ? a : (a + (_rng.NextDouble() - 0.5) * 1.0), (l == 0) ? r : Math.Max(5f, r + ((float)_rng.NextDouble() - 0.5f) * 5f));
				if (!(vector6 == Vector3.Zero) && OpenGround(vector6, reach) && Kit.ClearOf(site.Id, vector6, reach))
				{
					if (l > 0)
					{
						moved++;
					}
					return vector6;
				}
			}
			dropped++;
			return Vector3.Zero;
		}
		Vector3 Raw(double a, float r)
		{
			return GroundOnSpot(c + new Vector3((float)Math.Cos(a) * r, (float)Math.Sin(a) * r, 0f));
		}
	}

	private unsafe void DespawnSupplies(string siteId)
	{
		ClearObjectives(siteId);
		if (siteId == null || !_liveSupplies.TryGetValue(siteId, out var value))
		{
			return;
		}
		foreach (int debugBlip in value.DebugBlips)
		{
			int current = debugBlip;
			Crumbs.Native("town crate debug blip remove");
			if (MAP.DOES_BLIP_EXIST(current))
			{
				MAP.REMOVE_BLIP(&current);
			}
		}
		value.DebugBlips.Clear();
		PutOutCrateFires(value);
		_liveSupplies.Remove(siteId);
		foreach (Ped man in value.Men)
		{
			if (man != null && man.Exists())
			{
				if (!man.IsDead && SafeToDelete(man))
				{
					man.Delete();
					continue;
				}
				int handle = man.Handle;
				ReleaseIfOurs(&handle);
			}
		}
		value.Men.Clear();
		if (_crateOpening != null && value.Crates.Contains(_crateOpening))
		{
			_crateOpening = null;
		}
		Ped character = Game.Player.Character;
		DropProps((from x in value.Crates
			where x.Obj != 0
			select x.Obj).Concat(value.Barrels).ToList(), character, wipe: false);
		if (value.Wagon != null && value.Wagon.Exists())
		{
			if (!PED.IS_PED_IN_VEHICLE(character.Handle, value.Wagon.Handle, atGetIn: false) && SafeToDeleteVehicle(value.Wagon))
			{
				value.Wagon.Delete();
			}
			else
			{
				int handle2 = value.Wagon.Handle;
				ReleaseIfOurs(&handle2);
			}
		}
		foreach (var horse in value.Horses)
		{
			Ped item = horse.horse;
			if (item != null && item.Exists())
			{
				if (!item.IsDead && SafeToDelete(item))
				{
					item.Delete();
					continue;
				}
				int handle3 = item.Handle;
				ReleaseIfOurs(&handle3);
			}
		}
	}

	internal bool SafeToDeleteVehicle(Vehicle v)
	{
		Ped character = Game.Player.Character;
		if (v != null && v.Exists() && BelongsToUs(v.Handle))
		{
			if (!(character.Position.DistanceTo(v.Position) > 80f))
			{
				return HiddenFromView(character, v.Position);
			}
			return true;
		}
		return false;
	}

	private unsafe void UpdateSupplyHits(Ped player)
	{
		if (_liveSupplies.Count == 0 || Game.GameTime < _nextSupplyCheck)
		{
			return;
		}
		_nextSupplyCheck = Game.GameTime + 300;
		foreach (LiveSupply item in _liveSupplies.Values.ToList())
		{
			Gang gang = item.Gang;
			foreach (SupplyCrate cr in item.Crates)
			{
				if (cr.Gone)
				{
					continue;
				}
				bool flag = cr.Obj != 0 && ENTITY.DOES_ENTITY_EXIST(cr.Obj);
				bool flag2 = FIRE.IS_EXPLOSION_IN_SPHERE(-1, cr.At.X, cr.At.Y, cr.At.Z, cr.Big ? 5f : 4f);
				bool flag3 = (flag && FIRE.IS_ENTITY_ON_FIRE(cr.Obj)) || FIRE.GET_NUMBER_OF_FIRES_IN_RANGE(cr.At.X, cr.At.Y, cr.At.Z, cr.Big ? 2.4f : 1.6f) > 0;
				if (flag3 && item.FireSpots.Any(((Vector3 at, int t) s) => Game.GameTime - s.t < 20000 && s.at.DistanceTo2D(cr.At) < 3.5f))
				{
					flag3 = false;
				}
				cr.FireTicks = (flag3 ? (cr.FireTicks + 1) : 0);
				bool flag4 = cr.FireTicks >= 5;
				bool flag5 = false;
				if (!cr.Big)
				{
					int num = (flag ? ENTITY.GET_ENTITY_HEALTH(cr.Obj) : 0);
					if (flag && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(cr.Obj, player.Handle, p2: true, p3: true))
					{
						cr.Hits++;
						try
						{
							ENTITY.CLEAR_ENTITY_LAST_DAMAGE_ENTITY(cr.Obj);
						}
						catch
						{
						}
					}
					if (num != cr.Last)
					{
						cr.Last = num;
					}
					flag5 = !flag || num <= 0 || cr.Hits >= 2 || (cr.Start > 0 && (double)num <= (double)cr.Start * 0.85);
				}
				if (flag2 || flag5 || flag4)
				{
					cr.Gone = true;
					Vector3 at = (flag ? ENTITY.GET_ENTITY_COORDS(cr.Obj, alive: true, realCoords: true) : cr.At);
					SetCrateAlight(item, at, cr.Big);
					SupplyHit(item, player, cr.Big ? 12 : 5, (!cr.Big) ? (flag2 ? "you blew their crates apart" : (flag4 ? "you set their crates burning" : "you shot their crates to pieces")) : ((flag4 && !flag2) ? "you burned their stores" : "you blew up their stores"));
				}
			}
			if (item.Town && !item.Destroyed && item.Crates.Count > 0 && item.Crates.All((SupplyCrate x) => x.Gone))
			{
				item.Destroyed = true;
				string text = TownNear(item.Crates[0].At);
				ShowNotification("You destroyed the " + ShortOutfit(gang) + " stash" + ((text != null) ? (" in " + text) : "") + ".");
				foreach (int debugBlip in item.DebugBlips)
				{
					int current2 = debugBlip;
					if (MAP.DOES_BLIP_EXIST(current2))
					{
						MAP.REMOVE_BLIP(&current2);
					}
				}
				item.DebugBlips.Clear();
				GrudgeLog("SUPPLY " + (text ?? item.SiteId) + ": the " + OutfitPoss(gang) + " stash is destroyed - every crate gone");
			}
			if (!item.WagonCounted && item.Wagon != null)
			{
				bool flag6 = !item.Wagon.Exists() || VEHICLE.IS_VEHICLE_WRECKED(item.Wagon.Handle) || ENTITY.GET_ENTITY_HEALTH(item.Wagon.Handle) <= 0;
				bool flag7 = item.Wagon.Exists() && item.Wagon.Position.DistanceTo(item.WagonAt) > 120f;
				if (flag6 || flag7)
				{
					item.WagonCounted = true;
					SupplyHit(item, player, 15, flag6 ? "you wrecked their supply wagon" : "you made off with their supply wagon", flag6 ? ("You destroyed the " + ShortOutfit(item.Gang) + " supply wagon.") : ("You made off with the " + ShortOutfit(item.Gang) + " supply wagon."));
				}
			}
			foreach (var (ped, position) in item.Horses)
			{
				if (!(ped == null) && !item.HorsesCounted.Contains(ped.Handle))
				{
					if (!ped.Exists() || ped.IsDead)
					{
						item.HorsesCounted.Add(ped.Handle);
						SupplyHit(item, player, 4, "you killed one of their horses");
					}
					else if (ped.Position.DistanceTo(position) > 60f)
					{
						item.HorsesCounted.Add(ped.Handle);
						SupplyHit(item, player, 3, "you ran off one of their horses");
					}
				}
			}
		}
	}

	private void SupplyHit(LiveSupply ls, Ped player, int amount, string why, string say = null)
	{
		Gang gang = ls.Gang;
		Vector3 obj;
		if (ls.Town)
		{
			obj = ((ls.Crates.Count > 0) ? ls.Crates[0].At : player.Position);
		}
		else
		{
			CampSite campSite = Camps.ById(ls.SiteId);
			obj = ((campSite != null) ? SitePos(campSite) : player.Position);
		}
		Vector3 position = obj;
		int mount = PED.GET_MOUNT(player.Handle);
		if (!(player.Position.DistanceTo(position) < 90f) && (!_liveCamps.TryGetValue(ls.SiteId, out var value) || !value.Engaged) && (!(ls.Wagon != null) || !ls.Wagon.Exists() || !PED.IS_PED_IN_VEHICLE(player.Handle, ls.Wagon.Handle, atGetIn: false)) && (mount == 0 || !ls.Horses.Any(((Ped horse, Vector3 at) h) => h.horse != null && h.horse.Handle == mount)))
		{
			int val = (ls.Town ? amount : Math.Max(0, 40 - ls.Lost));
			int num = Math.Min(amount, val);
			if (!ls.Town)
			{
				ls.Lost += num;
			}
			if (num > 0)
			{
				AddSupplies(gang, -num, why.Replace("you ", "someone ") + " (not you)");
			}
			GrudgeLog($"SUPPLY {gang.Name}: -{num} at {ls.SiteId} with you {player.Position.DistanceTo(position):0} m off - not put on you");
			return;
		}
		if (!ls.Town)
		{
			int num2 = Math.Max(0, 40 - ls.Lost);
			if (amount > num2)
			{
				GrudgeLog($"SUPPLY raid cap: {why} -{amount} cut to -{num2} ({ls.Lost} of {40} taken this raid)");
			}
			amount = Math.Min(amount, num2);
		}
		int num3 = SuppliesOf(gang);
		if (amount > 0 && num3 > 0)
		{
			AddSupplies(gang, -amount, why);
			amount = num3 - SuppliesOf(gang);
			if (!ls.Town)
			{
				ls.Lost += amount;
			}
			string text = $"The {OutfitPoss(gang)} supplies: {SuppliesOf(gang)} - {SupplyState(SuppliesOf(gang)).ToLowerInvariant()}.";
			if (say != null)
			{
				ShowNotification(say);
			}
			else
			{
				GrudgeNotice(text);
			}
			Feed($"SUPPLIES {gang.Name}: -{amount} ({why})", gang.Accent);
		}
		else if (say != null)
		{
			ShowNotification(say);
		}
		else if (!ls.CapSaid)
		{
			ls.CapSaid = true;
			GrudgeNotice("The " + gang.Name + " have lost all they can here - the rest is just wreckage.");
		}
		if (ls.Hit)
		{
			return;
		}
		ls.Hit = true;
		foreach (Ped item in ls.Men.Where((Ped m) => m != null && m.Exists() && !m.IsDead))
		{
			item.BlockPermanentEvents = false;
			TASK.CLEAR_PED_TASKS(item.Handle, p1: true, p2: true);
			TASK.TASK_COMBAT_PED(item.Handle, player.Handle, 0, 16);
		}
		if (ls.Town)
		{
			AddNotoriety(gang, 1, "you broke into their crates in town");
			Vector3 vector = ((ls.Crates.Count > 0) ? ls.Crates[0].At : player.Position);
			string text2 = TownNear(vector);
			try
			{
				ChronicleDeed("supplies", "You broke into the " + OutfitPoss(gang) + " crates" + ((text2 != null) ? (" in " + text2) : ""), (text2 != null) ? ("in " + text2) : ("near " + GetZoneName(vector)), vector, gang.Id);
				return;
			}
			catch
			{
				return;
			}
		}
		Hold.RecordRaid(gang, ls.SiteId);
		CampSite campSite2 = Camps.ById(ls.SiteId);
		if (campSite2 != null)
		{
			Hold.KnowSupplies(gang, campSite2);
		}
		SaveWorld();
		AddNotoriety(gang, 3, "you hit their supplies");
		CampSite campSite3 = Camps.ById(ls.SiteId);
		LiveCamp value2;
		NemesisRecord nemesisRecord = ((campSite3 != null) ? (HolderOf(campSite3) ?? (_liveCamps.TryGetValue(campSite3.Id, out value2) ? value2.Boss : null)) : null);
		if (nemesisRecord != null)
		{
			nemesisRecord.AddHate(10);
			History(nemesisRecord, "RAIDED: you hit the supplies he kept at " + campSite3.Label, Theme.Blood);
			SaveRoster();
		}
		if (IsSupplyCache(gang))
		{
			RefreshCacheBlips();
		}
		try
		{
			ChronicleDeed("supplies", "You hit the " + OutfitPoss(gang) + " supplies", (campSite3 != null) ? ("at " + campSite3.Label) : "", (campSite3 != null) ? new Vector3?(SitePos(campSite3)) : ((Vector3?)null), gang.Id);
		}
		catch
		{
		}
		if (campSite3 != null)
		{
			RedoCampBlip(campSite3.Id);
		}
	}

	private unsafe void CrateBreaking(Ped player)
	{
		if (_crateOpening != null)
		{
			if (player.IsDead || player.Position.DistanceTo(_crateOpening.At) > 2.8f || !_liveSupplies.ContainsValue(_crateOpeningIn))
			{
				_crateOpening = null;
			}
			else
			{
				if (Game.GameTime < _crateOpenUntil)
				{
					return;
				}
				SupplyCrate crateOpening = _crateOpening;
				LiveSupply crateOpeningIn = _crateOpeningIn;
				_crateOpening = null;
				try
				{
					TASK.CLEAR_PED_TASKS(player.Handle, p1: false, p2: false);
				}
				catch
				{
				}
				if (!crateOpening.Gone)
				{
					crateOpening.Gone = true;
					int num = 2 + _rng.Next(5);
					PayPlayer(num * 100);
					if (crateOpening.Obj != 0 && ENTITY.DOES_ENTITY_EXIST(crateOpening.Obj))
					{
						int obj2 = crateOpening.Obj;
						OBJECT.DELETE_OBJECT(&obj2);
						crateOpening.Obj = 0;
					}
					SupplyHit(crateOpeningIn, player, 6, "you broke open their crates");
					GrudgeNotice($"You broke open a crate - ${num} and their stores spoiled.");
				}
			}
			return;
		}
		if (player.IsDead || player.IsInVehicle || player.IsOnMount || _menu.IsOpen || _convo != null || _tg != null || _interro != null || _boxOpening != null)
		{
			HideCratePrompt();
			return;
		}
		SupplyCrate near = null;
		LiveSupply liveSupply = null;
		float num2 = 2f;
		foreach (LiveSupply value2 in _liveSupplies.Values)
		{
			foreach (SupplyCrate crate in value2.Crates)
			{
				if (!crate.Gone)
				{
					float num3 = player.Position.DistanceTo(crate.At);
					if (num3 < num2)
					{
						num2 = num3;
						near = crate;
						liveSupply = value2;
					}
				}
			}
		}
		if (near == null)
		{
			HideCratePrompt();
			return;
		}
		if (_pCrate == 0 || !PromptValid(_pCrate))
		{
			_pCrate = MakePrompt(eInputType.InteractOption1, "Break It Open", hold: true, 8280594);
			_crateArmed = false;
		}
		HUD._UI_PROMPT_SET_VISIBLE(_pCrate, toggle: true);
		HUD._UI_PROMPT_SET_ENABLED(_pCrate, toggle: true);
		Function.Call(14292813145410643495uL, 8280594u, new InputArgument(UiDraw.VarString("Supplies")), 0, 0, 0, 0);
		if (!HUD._UI_PROMPT_HAS_HOLD_MODE_COMPLETED(_pCrate))
		{
			_crateArmed = true;
		}
		else if (_crateArmed)
		{
			_crateArmed = false;
			HideCratePrompt();
			ENTITY.SET_ENTITY_HEADING(player.Handle, HeadingTo(player.Position, near.At));
			Crumbs.Native("supply crate: crouch");
			TASK.TASK_START_SCENARIO_IN_PLACE_HASH(player.Handle, MISC.GET_HASH_KEY("WORLD_HUMAN_CROUCH_INSPECT"), 0, playEnterAnim: true, 0u, -1f, p6: false);
			_crateOpening = near;
			_crateOpeningIn = liveSupply;
			_crateOpenUntil = Game.GameTime + 2000;
			if (_liveCamps.TryGetValue(liveSupply.SiteId, out var value) && !value.Engaged && value.Peds.Any((Ped p) => p.Exists() && !p.IsDead && !LooksKnockedOut(p) && p.Position.DistanceTo(near.At) < 35f))
			{
				GrudgeLog("SUPPLY " + liveSupply.Gang.Name + ": they heard you at the crates - the camp turns on you");
				EngageCamp(value, player);
			}
		}
	}

	private void HideCratePrompt()
	{
		if (_pCrate != 0 && PromptValid(_pCrate))
		{
			HUD._UI_PROMPT_SET_VISIBLE(_pCrate, toggle: false);
			HUD._UI_PROMPT_SET_ENABLED(_pCrate, toggle: false);
		}
	}

	private void DeleteCratePrompt()
	{
		if (PromptValid(_pCrate))
		{
			HUD._UI_PROMPT_DELETE(_pCrate);
		}
		_pCrate = 0;
	}

	private void DebugSupplyCampHere(Ped p)
	{
		CampSite campSite = (from s in Camps.AllSites
			where HoldsCamps(Gangs.ById(SiteOwner(s))) && HolderOf(s) != null && !IsTreasury(s)
			orderby SitePos(s).DistanceTo(p.Position)
			select s).FirstOrDefault();
		if (campSite == null)
		{
			SpawnerSays("no manned outfit camp (that isn't a treasury) anywhere.");
			return;
		}
		Gang g = Gangs.ById(SiteOwner(campSite));
		CampSite campSite2 = SupplyCampOf(g);
		Hold.SetSupplyCamp(g, campSite);
		if (Cleared.Remove(campSite.Id))
		{
			Config.ClearedCamps = string.Join(",", Cleared.Select((KeyValuePair<string, int> kv) => $"{kv.Key}:{kv.Value}"));
			Config.Save();
		}
		NemesisRecord nemesisRecord = HolderOf(campSite);
		if (nemesisRecord != null && !nemesisRecord.CampKnown)
		{
			RevealCamp(nemesisRecord, found: false);
		}
		if (_liveCamps.TryGetValue(campSite.Id, out var value))
		{
			DespawnCamp(value, delete: true);
		}
		_campHeldOff.Remove(campSite.Id);
		if (campSite2 != null)
		{
			RedoCampBlip(campSite2.Id);
		}
		RedoCampBlip(campSite.Id);
		SaveWorld();
		GrudgeLog($"SUPPLY debug: {campSite.Label} holds the {OutfitPoss(g)} supplies ({SitePos(campSite).DistanceTo(p.Position):0} m off)");
		SpawnerSays($"the {OutfitPoss(g)} supplies: {campSite.Label}, {SitePos(campSite).DistanceTo(p.Position):0} m - marked on your map.");
	}

	private void DebugSupplyRestock(Ped p)
	{
		(Gang, CampSite) tuple = (from g in Gangs.All.Where(HoldsCamps)
			select (g: g, s: SupplyCampOf(g)) into t
			where t.s != null
			orderby SitePos(t.s).DistanceTo(p.Position)
			select t).FirstOrDefault();
		if (tuple.Item2 == null)
		{
			SpawnerSays("no outfit has a supply camp yet.");
			return;
		}
		Hold.ForgetRaid(tuple.Item1);
		if (_liveCamps.TryGetValue(tuple.Item2.Id, out var value))
		{
			DespawnCamp(value, delete: true);
		}
		SaveWorld();
		RedoCampBlip(tuple.Item2.Id);
		SpawnerSays("the " + OutfitPoss(tuple.Item1) + " supply camp is restocked - " + tuple.Item2.Label + ".");
	}
}
