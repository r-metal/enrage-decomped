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
	private enum IconKind
	{
		Camps,
		Supplies,
		Funerals,
		Treasuries,
		LaidUp,
		Bars,
		Habits,
		Plans,
		People,
		Calm
	}

	private sealed class MapIcon
	{
		public Vector3 At;

		public string Sprite;

		public string Name;

		public string Tint;

		public float Radius;
	}

	private const int MapIconCap = 250;

	private const ulong BlipAddForRadiusFn = 5039874870525413504uL;

	private int _nextPeopleIcons;

	private const string Blue = "BLIP_MODIFIER_DEBUG_BLUE";

	private const string Green = "BLIP_MODIFIER_DEBUG_GREEN";

	private const string Red = "BLIP_MODIFIER_DEBUG_RED";

	private const string Yellow = "BLIP_MODIFIER_DEBUG_YELLOW";

	private long _lastMindHour = -1L;

	private long _lastMindDay = -1L;

	private void DebugIcons(IconKind k)
	{
		if (_iconBlips.ContainsKey(k))
		{
			HideIcons(k);
			SpawnerSays("map icons: " + IconKindNames[(int)k] + " off.");
		}
		else
		{
			var (num, num2) = ShowIcons(k);
			SpawnerSays(string.Format("map icons: {0} {1}{2}. Again to hide.", num, IconKindNames[(int)k], (num2 > 0) ? $" ({num2} more left out - the map's limit; the nearest are shown)" : ""));
		}
	}

	private void DebugIconsAll()
	{
		if (_iconBlips.Count > 0)
		{
			HideMapIcons();
			SpawnerSays("map icons: all off.");
			return;
		}
		int num = 0;
		int num2 = 0;
		foreach (IconKind value in Enum.GetValues(typeof(IconKind)))
		{
			(int, int) tuple = ShowIcons(value);
			num += tuple.Item1;
			num2 += tuple.Item2;
		}
		SpawnerSays(string.Format("map icons: {0} on the map{1}. Again to hide them all.", num, (num2 > 0) ? $" ({num2} more left out - the map's limit; the nearest are shown)" : ""));
	}

	private (int shown, int left) ShowIcons(IconKind k, bool quiet = false)
	{
		HideIcons(k);
		Vector3 me = Game.Player.Character.Position;
		List<MapIcon> list = (from i in IconsOf(k)
			where i.At != Vector3.Zero
			orderby i.At.DistanceTo2D(me)
			select i).ToList();
		int num = Math.Max(0, 250 - _iconBlips.Values.Sum((List<int> v) => v.Count));
		List<int> list2 = new List<int>();
		foreach (MapIcon item in list.Take(num))
		{
			int num2 = AddMapIcon(item);
			if (num2 != 0)
			{
				list2.Add(num2);
			}
		}
		_iconBlips[k] = list2;
		if (k == IconKind.People)
		{
			_nextPeopleIcons = Game.GameTime + 3000;
		}
		if (!quiet)
		{
			GrudgeLog($"MAPICONS {IconKindNames[(int)k]}: {list2.Count} shown of {list.Count}");
		}
		return (shown: list2.Count, left: Math.Max(0, list.Count - num));
	}

	private unsafe void HideIcons(IconKind k)
	{
		if (!_iconBlips.TryGetValue(k, out var value))
		{
			return;
		}
		foreach (int item in value)
		{
			int current = item;
			if (MAP.DOES_BLIP_EXIST(current))
			{
				MAP.REMOVE_BLIP(&current);
			}
		}
		_iconBlips.Remove(k);
	}

	internal void HideMapIcons()
	{
		foreach (IconKind item in _iconBlips.Keys.ToList())
		{
			HideIcons(item);
		}
	}

	private int AddMapIcon(MapIcon ic)
	{
		Crumbs.Native("map icon");
		int num = ((ic.Radius > 0f) ? Function.Call<int>(5039874870525413504uL, new InputArgument[5]
		{
			Joaat.Of("BLIP_STYLE_AREA"),
			ic.At.X,
			ic.At.Y,
			ic.At.Z,
			ic.Radius
		}) : MAP.BLIP_ADD_FOR_COORDS(2545348716u, ic.At.X, ic.At.Y, ic.At.Z));
		if (num == 0)
		{
			return 0;
		}
		if (ic.Sprite != null)
		{
			MAP.SET_BLIP_SPRITE(num, MISC.GET_HASH_KEY(ic.Sprite), p2: true);
		}
		if (ic.Tint != null)
		{
			try
			{
				Function.Call<bool>(7362600660482055727uL, new InputArgument[2]
				{
					num,
					MISC.GET_HASH_KEY(ic.Tint)
				});
			}
			catch
			{
			}
		}
		Function.Call(11290983183885202434uL, num, new InputArgument(UiDraw.ForeverVarString(ic.Name)));
		return num;
	}

	private void MapIconsTick()
	{
		if (_iconBlips.ContainsKey(IconKind.People) && Game.GameTime >= _nextPeopleIcons)
		{
			ShowIcons(IconKind.People, quiet: true);
		}
	}

	private static MapIcon Icon(Vector3 at, string sprite, string name, string tint = null, float radius = 0f)
	{
		return new MapIcon
		{
			At = at,
			Sprite = sprite,
			Name = name,
			Tint = tint,
			Radius = radius
		};
	}

	private IEnumerable<MapIcon> IconsOf(IconKind k)
	{
		switch (k)
		{
		case IconKind.Camps:
		{
			foreach (CampSite allSite in Camps.AllSites)
			{
				bool flag = allSite.Id.StartsWith("placed_");
				bool flag2 = !flag && Camps.Extra.Contains(allSite);
				string text = Gangs.ById(SiteOwner(allSite))?.Name ?? "nobody holds it";
				yield return Icon(SitePos(allSite), allSite.Hideout ? "blip_region_hideout" : "blip_camp_tent", "Camp " + (allSite.Hideout ? "hideout" : "site") + ": " + text + " - " + allSite.Region + " (" + (flag ? "placed" : (flag2 ? "learned" : "the game's")) + ")", flag ? "BLIP_MODIFIER_DEBUG_BLUE" : (flag2 ? "BLIP_MODIFIER_DEBUG_GREEN" : null));
			}
			CampSite[] retired = Camps.Retired;
			foreach (CampSite campSite2 in retired)
			{
				yield return Icon(new Vector3(campSite2.X, campSite2.Y, campSite2.Z), "blip_region_hideout", "Retired camp: " + campSite2.Name + " - nobody camps here", "BLIP_MODIFIER_DEBUG_RED");
			}
			break;
		}
		case IconKind.Supplies:
			foreach (Gang item3 in Gangs.All.Where(HoldsCamps))
			{
				CampSite campSite3 = SupplyCampOf(item3);
				if (campSite3 != null)
				{
					yield return Icon(SitePos(campSite3), "blip_chest", "Supplies: the " + item3.Name + "'s supply camp - " + campSite3.Region, "BLIP_MODIFIER_DEBUG_YELLOW");
				}
			}
			foreach (StashSpot stashSpot in StashSpots)
			{
				yield return Icon(new Vector3(stashSpot.X, stashSpot.Y, stashSpot.Z), "blip_chest", "Stash spot: " + stashSpot.Town + (stashSpot.BuiltIn ? " (built in)" : " (placed)"));
			}
			break;
		case IconKind.Funerals:
			foreach (var item4 in AllCemeteries())
			{
				yield return Icon(item4.at, "blip_ambient_death", "Funerals: " + CemeteryLabel(item4.at));
			}
			foreach (Vector3 plot in Plots)
			{
				yield return Icon(plot, "blip_deadeye_cross", "Funeral plot (marked)");
			}
			break;
		case IconKind.Treasuries:
			foreach (Gang item5 in Gangs.All.Where(HoldsCamps))
			{
				CampSite campSite = TreasuryOf(item5);
				if (campSite != null)
				{
					yield return Icon(SitePos(campSite), "blip_gang_savings", "Treasury: the " + item5.Name + "'s - " + campSite.Region + (TreasuryKnownList.Contains(item5.Id) ? " (you know it)" : ""));
				}
			}
			break;
		case IconKind.LaidUp:
			foreach (KeyValuePair<string, (Vector3?, Vector3?)> townSpot in TownSpots)
			{
				Vector3? item = townSpot.Value.Item1;
				if (item.HasValue)
				{
					Vector3 valueOrDefault2 = item.GetValueOrDefault();
					if (OpenDoctors.Contains(townSpot.Key))
					{
						yield return Icon(valueOrDefault2, "blip_shop_doctor", "Laid-up men: the " + townSpot.Key + " doctor's");
					}
				}
			}
			foreach (var hotel in Hotels)
			{
				yield return Icon(hotel.at, "blip_hotel_bed", "Laid-up men: a hotel bed in " + hotel.town + " (marked)");
			}
			break;
		case IconKind.Bars:
			foreach (KeyValuePair<string, (Vector3?, Vector3?)> townSpot2 in TownSpots)
			{
				Vector3? item = townSpot2.Value.Item2;
				if (item.HasValue)
				{
					Vector3 valueOrDefault = item.GetValueOrDefault();
					yield return Icon(valueOrDefault, "blip_saloon", "Saloon: " + townSpot2.Key);
				}
			}
			foreach (Bar b in Bars)
			{
				if (!TownSpots.Any(delegate(KeyValuePair<string, (Vector3? Doctor, Vector3? Saloon)> t)
				{
					Vector3? item2 = t.Value.Saloon;
					return item2.HasValue && item2.GetValueOrDefault().DistanceTo(b.At) < 5f;
				}))
				{
					yield return Icon(b.At, "blip_saloon", "Saloon fight bar: " + b.Town, "BLIP_MODIFIER_DEBUG_YELLOW");
				}
			}
			break;
		case IconKind.Habits:
			foreach (NemesisRecord r in Roster.Where((NemesisRecord n) => n.IsAlive && !n.PresumedDead && n.Habits != null))
			{
				foreach (Habit habit in r.Habits)
				{
					if (habit.X != 0f || habit.Y != 0f)
					{
						yield return Icon(new Vector3(habit.X, habit.Y, habit.Z), "blip_ambient_gang_leader", "Habit: " + r.Name + " " + HabitText(habit) + (habit.Known ? " (you know it)" : ""));
					}
				}
			}
			break;
		case IconKind.Plans:
			foreach (Plan item6 in Plans.Where((Plan x) => !x.Done))
			{
				Vector3 at = ((item6.Kind == "funeral" && item6.PlotSet) ? new Vector3(item6.PlotX, item6.PlotY, item6.PlotZ) : new Vector3(item6.X, item6.Y, item6.Z));
				string text2 = Gangs.ById(item6.Gang)?.Name;
				yield return Icon(at, "blip_attention", "Plan (" + item6.Kind + (item6.Lie ? ", a lie" : "") + ((text2 != null) ? (", " + text2) : "") + "): " + item6.Title + " - " + WhenText(item6.DueAt) + (item6.Known ? " (known)" : ""));
			}
			break;
		case IconKind.People:
		{
			List<KeyValuePair<int, NemesisRecord>> list = _worldNemeses.ToList();
			if (_activeNemesisPed != null && ActiveRecord != null && !_worldNemeses.ContainsKey(_activeNemesisPed.Handle))
			{
				list.Add(new KeyValuePair<int, NemesisRecord>(_activeNemesisPed.Handle, ActiveRecord));
			}
			foreach (KeyValuePair<int, NemesisRecord> item7 in list)
			{
				if (item7.Value != null && ENTITY.DOES_ENTITY_EXIST(item7.Key) && !ENTITY.IS_ENTITY_DEAD(item7.Key))
				{
					yield return Icon(ENTITY.GET_ENTITY_COORDS(item7.Key, alive: true, realCoords: true), "blip_ambient_npc", string.Format("{0} ({1}, rank {2})", item7.Value.Name, Gangs.ById(item7.Value.Gang)?.Name ?? "no outfit", item7.Value.Rank));
				}
			}
			break;
		}
		case IconKind.Calm:
		{
			CampSite[] retired = Camps.Retired;
			foreach (CampSite s in retired)
			{
				Vector3 c = new Vector3(s.X, s.Y, s.Z);
				yield return Icon(c, null, $"Calm zone: {s.Name} - no fights within {300f:0} m", null, 300f);
				yield return Icon(c, "blip_region_hideout", "Calm zone: " + s.Name + " (its middle)", "BLIP_MODIFIER_DEBUG_GREEN");
			}
			break;
		}
		}
	}
}
