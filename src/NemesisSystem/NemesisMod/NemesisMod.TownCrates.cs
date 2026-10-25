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
	private const float TownStashGap = 150f;

	private const int TownStashMax = 2;

	private void UpdateTownCrates(Ped player)
	{
		string currentTown = _currentTown;
		if (_townCratesFor != null && currentTown != _townCratesFor)
		{
			DespawnTownStashes(_townCratesFor);
			_townCratesFor = null;
		}
		if (currentTown != null && !(_townCratesFor == currentTown) && !MISC.GET_MISSION_FLAG() && !WorldBusy(player) && InTownForArrivals(player, currentTown))
		{
			_townCratesFor = currentTown;
			if (_rng.Next(100) >= 50)
			{
				GrudgeLog("SUPPLY " + currentTown + ": no outfit's crates on the street this visit");
			}
			else if (SpawnTownCrates(player, currentTown) && _rng.Next(100) < 30)
			{
				SpawnTownCrates(player, currentTown);
			}
		}
	}

	private IEnumerable<string> TownStashKeys(string town)
	{
		return _liveSupplies.Keys.Where((string k) => k == "town:" + town || k.StartsWith("town:" + town + "#")).ToList();
	}

	private void DespawnTownStashes(string town)
	{
		foreach (string item in TownStashKeys(town))
		{
			DespawnSupplies(item);
		}
	}

	private string FreeTownStashKey(string town)
	{
		for (int i = 1; i <= 2; i++)
		{
			string text = ((i == 1) ? ("town:" + town) : $"town:{town}#{i}");
			if (!_liveSupplies.ContainsKey(text))
			{
				return text;
			}
		}
		return null;
	}

	private bool SpawnTownCrates(Ped player, string town)
	{
		Vector3 centre;
		float radius;
		District d = DistrictAt(TownCore(town, out centre, out radius) ? centre : player.Position);
		District d2;
		Gang gang = TownHolder(town, out d2) ?? ((d == null) ? null : (from x in Gangs.All
			where !x.StoryOnly && !IsDestroyed(x) && HoldOf(d, x.Id) > 0
			orderby HoldOf(d, x.Id) descending
			select x).FirstOrDefault());
		if (gang == null)
		{
			return false;
		}
		string text = FreeTownStashKey(town);
		if (text == null)
		{
			return false;
		}
		List<Vector3> list = (from k in TownStashKeys(town)
			select _liveSupplies[k] into o
			where o.Crates.Count > 0
			select o.Crates[0].At).ToList();
		Vector3 position = player.Position;
		List<StashSpot> list2 = StashSpots.Where((StashSpot x) => x.Town == town).ToList();
		if (list2.Count == 0)
		{
			GrudgeLog("SUPPLY " + town + ": no stash spots placed here - no stash");
			return false;
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		StashSpot stashSpot = null;
		foreach (StashSpot sp in list2.OrderBy((StashSpot _) => _rng.Next()))
		{
			if (sp.Pos.DistanceTo(position) < 30f)
			{
				num2++;
				continue;
			}
			if (list.Any((Vector3 o) => o.DistanceTo2D(sp.Pos) < 150f))
			{
				num3++;
				continue;
			}
			if (!HiddenFromView(player, sp.Pos + new Vector3(0f, 0f, 1f)))
			{
				num++;
				continue;
			}
			stashSpot = sp;
			break;
		}
		if (stashSpot == null)
		{
			GrudgeLog($"SUPPLY {town}: no placed spot fits the {OutfitPoss(gang)} stash ({list2.Count} here: {num} in view, {num2} too near you, {num3} too near the other stash)");
			return false;
		}
		LiveSupply liveSupply = new LiveSupply
		{
			SiteId = text,
			Gang = gang,
			Town = true
		};
		_liveSupplies[liveSupply.SiteId] = liveSupply;
		Vector3 best = stashSpot.Pos;
		double num4 = (double)stashSpot.H * Math.PI / 180.0;
		Vector3 vector = new Vector3(0f - (float)Math.Sin(num4), (float)Math.Cos(num4), 0f);
		Vector3 vector2 = new Vector3(0f - vector.Y, vector.X, 0f);
		if (_rng.Next(100) < 60)
		{
			Vector3 vector3 = GroundOnSpot(best - vector * 1f);
			if (vector3 != Vector3.Zero && Math.Abs(vector3.Z - best.Z) < 0.8f)
			{
				_propModel.TryGetValue("crate", out var value);
				int num5 = MakeCheckpointProp(BigCrateModels, vector3, stashSpot.H + (float)_rng.Next(-8, 9), ref value);
				if (value != null)
				{
					_propModel["crate"] = value;
				}
				if (num5 != 0)
				{
					liveSupply.Crates.Add(new SupplyCrate
					{
						Obj = num5,
						At = vector3,
						Big = true
					});
				}
			}
		}
		(float, float)[] obj = new(float, float)[7]
		{
			(0f, 0.4f),
			(0.9f, 0.3f),
			(-0.9f, 0.3f),
			(0.45f, 1.2f),
			(-0.45f, 1.2f),
			(1.6f, -0.4f),
			(-1.6f, -0.4f)
		};
		int num6 = 3 + _rng.Next(3);
		int num7 = 0;
		(float, float)[] array = obj;
		for (int num8 = 0; num8 < array.Length; num8++)
		{
			var (num9, num10) = array[num8];
			if (num7 >= num6)
			{
				break;
			}
			Vector3 vector4 = GroundOnSpot(best + vector2 * num9 + vector * num10);
			if (!(vector4 == Vector3.Zero) && !(Math.Abs(vector4.Z - best.Z) > 0.6f))
			{
				AddLooseCrate(liveSupply, vector4);
				num7++;
			}
		}
		if (liveSupply.Crates.Count == 0)
		{
			DespawnSupplies(liveSupply.SiteId);
			GrudgeLog("SUPPLY " + town + ": no room at the placed spot for the " + OutfitPoss(gang) + " stash");
			return false;
		}
		string text2 = null;
		if (_rng.Next(100) < 40 && FactionModels.ContainsKey(gang.Id))
		{
			Vector3 vector5 = GroundOnSpot(best - vector2 * 2.2f + vector * 1.2f);
			if (vector5 != Vector3.Zero && Math.Abs(vector5.Z - best.Z) < 1f)
			{
				Ped ped = CheckpointMan(gang, vector5, HeadingTo(vector5, best + vector * 4f), (_rng.Next(2) == 0) ? "WORLD_HUMAN_LEAN_WALL_LEFT" : "WORLD_HUMAN_SMOKE");
				if (ped != null)
				{
					liveSupply.Men.Add(ped);
					text2 = "a lookout";
				}
			}
		}
		if (Config.DebugEnabled && !_mapHidden)
		{
			Crumbs.Native("town crate debug blip");
			int num11 = MAP.BLIP_ADD_FOR_COORDS(2545348716u, best.X, best.Y, best.Z);
			if (num11 != 0)
			{
				MAP.SET_BLIP_SPRITE(num11, MISC.GET_HASH_KEY("blip_chest"), p2: true);
				Function.Call(11290983183885202434uL, num11, new InputArgument(UiDraw.ForeverVarString(ShortOutfit(gang) + " stash (debug)")));
				liveSupply.DebugBlips.Add(num11);
			}
		}
		int num12 = liveSupply.Crates.Count((SupplyCrate x) => x.Big);
		GrudgeLog(string.Format("SUPPLY {0}: the {1} stash at a placed spot {2:0},{3:0}{4} - {5} crates{6}{7}", town, OutfitPoss(gang), best.X, best.Y, (list.Count > 0) ? $" ({list.Min((Vector3 o) => o.DistanceTo2D(best)):0} m from the other)" : "", liveSupply.Crates.Count - num12, (num12 > 0) ? ", a big crate" : "", (text2 != null) ? ", a lookout" : ""));
		return true;
	}

	private void DebugSupplyWagon(Ped p)
	{
		if (_supplyWagon != null)
		{
			SpawnerSays("a supply wagon is already on the road.");
			return;
		}
		Gang gang = TopHolderHere(p);
		if (gang == null || !FactionModels.ContainsKey(gang.Id))
		{
			gang = Gangs.All.FirstOrDefault((Gang x) => !x.StoryOnly && !IsDestroyed(x) && FactionModels.ContainsKey(x.Id));
		}
		if (gang == null)
		{
			SpawnerSays("no outfit with men to send.");
		}
		else
		{
			SpawnerSays(SpawnSupplyWagon(p, gang) ? ("the " + OutfitPoss(gang) + " supply wagon is coming up the road ahead.") : "no road out of your view ahead - face down a road and try again.");
		}
	}

	private void DebugTownCrates(Ped p)
	{
		string text = SpawnerTown(p);
		if (text != null)
		{
			if (FreeTownStashKey(text) == null)
			{
				DespawnTownStashes(text);
			}
			_townCratesFor = text;
			int num = TownStashKeys(text).Count();
			bool flag = SpawnTownCrates(p, text);
			SpawnerSays(flag ? $"a stash is down somewhere in town ({num + 1} of {2} - debug markers on the map, grudges.log says where)." : "no placed stash spot fits right now (in view, too near you, or near the other stash - grudges.log says) - place more with 'Place a town stash spot here'.");
		}
	}
}
