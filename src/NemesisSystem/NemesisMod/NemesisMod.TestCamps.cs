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
	private const string LookPrefix = "look_";

	private readonly Dictionary<string, int> _testCampBlips = new Dictionary<string, int>();

	private bool _lookPropsOnly = true;

	private string _visitTown;

	private int _visitAggressors;

	internal CampKit Kit => _kit ?? (_kit = new CampKit(this));

	private void DespawnCampKit(string siteId)
	{
		RemoveTestCampBlip(siteId);
		Kit.Despawn(siteId);
	}

	internal void DebugCampKitHere(Ped player)
	{
		if (Kit.Has("debug_kit"))
		{
			DespawnCampKit("debug_kit");
			ShowNotification("enRAGE: camp kit taken away.");
			return;
		}
		Vector3 vector = player.Position + player.ForwardVector * 8f;
		int num = Kit.LayAt("debug_kit", vector, Environment.TickCount, OutfitHere()?.Id);
		AddTestCampBlip(new CampSite("debug_kit", 0u, vector.X, vector.Y, vector.Z, "here", hideout: false, null, OutfitHere()?.Id), OutfitHere(), null, vector);
		ShowNotification(string.Format("{0}: camp kit, {1} props, 8 m ahead. Again to take it away. (Log: CAMPKIT lines - which models loaded)", "enRAGE", num));
	}

	private Gang OutfitHere()
	{
		District currentDistrict = CurrentDistrict;
		return ((currentDistrict != null) ? (from kv in ControlMap[currentDistrict.Key]
			orderby kv.Value descending
			select Gangs.ById(kv.Key)).FirstOrDefault((Gang x) => x != null && Gangs.InEra(x) && x.HasCamps && !x.OrdersNotFeelings) : null) ?? Gangs.All.FirstOrDefault((Gang x) => x.HasCamps && !x.OrdersNotFeelings && !x.StoryOnly);
	}

	private CampSite LastPlaced => Camps.Extra.LastOrDefault((CampSite s) => s.Id.StartsWith("placed_"));

	internal void DebugPlaceCamp(Ped player)
	{
		Gang gang = OutfitHere();
		if (gang == null)
		{
			ShowNotification("enRAGE: no outfit here holds camps.");
			return;
		}
		Vector3 position = player.Position;
		string zoneName = GetZoneName(position);
		CampSite campSite = new CampSite("placed_" + gang.Id + "_" + NewId(6), 0u, position.X, position.Y, position.Z, zoneName, hideout: false, null, gang.Id);
		Camps.Extra.Add(campSite);
		SaveCampSites();
		_nextCampAssign = 0;
		GrudgeLog($"CAMPSITE placed (test): {campSite.Id} for the {gang.Name} at {position.X:0},{position.Y:0},{position.Z:0} ({zoneName})");
		ShowNotification("enRAGE: a " + gang.Name + " camp site placed here. 'Spawn the placed camp' to see it, 'Revert' to take it away.");
	}

	internal void DebugSpawnPlaced(Ped player)
	{
		CampSite site = LastPlaced;
		if (site == null)
		{
			ShowNotification("enRAGE: no placed camp site - place one first.");
			return;
		}
		if (_liveCamps.TryGetValue(site.Id, out var value))
		{
			DespawnCamp(value, delete: true);
		}
		_liveCamps.Remove(site.Id);
		Kit.ForgetCentre(site.Id);
		_streetCentres.Remove(site.Id);
		_noStreetUntil.Remove(site.Id);
		_streetWaitSince.Remove(site.Id);
		if (!TownSiteReady(site))
		{
			ShowNotification("enRAGE: no street-level ground at the placed site" + (_noStreetUntil.ContainsKey(site.Id) ? "" : " yet (its ground is loading - try again)") + ". Stand on the street and place it again.");
			return;
		}
		Gang gang = Gangs.ById(site.Gang);
		NemesisRecord nemesisRecord = (from n in Roster
			where n.Gang == site.Gang && n.IsAlive && !n.PresumedDead && n.Rank >= 2 && (n.CampId == site.Id || OffStage(n))
			orderby n.CampId == site.Id descending, n.Rank descending
			select n).FirstOrDefault();
		if (nemesisRecord == null)
		{
			ShowNotification("enRAGE: no officer of the " + gang?.Name + " free to hold it.");
			return;
		}
		SpawnCamp(site, nemesisRecord, player);
		AddTestCampBlip(site, gang, nemesisRecord);
		GrudgeLog("CAMPSITE spawned (test): " + site.Id + " under " + nemesisRecord.Name);
		ShowNotification("enRAGE: the " + gang?.Name + " camp at the placed site, under " + nemesisRecord.Name + ".");
	}

	internal void DebugRevertPlaced(Ped player)
	{
		CampSite site = LastPlaced;
		if (site == null)
		{
			ShowNotification("enRAGE: no placed camp site to revert.");
			return;
		}
		if (_liveCamps.TryGetValue(site.Id, out var value))
		{
			DespawnCamp(value, delete: true);
		}
		_liveCamps.Remove(site.Id);
		Kit.ForgetCentre(site.Id);
		Camps.Extra.Remove(site);
		SaveCampSites();
		foreach (NemesisRecord item in Roster.Where((NemesisRecord n) => n.CampId == site.Id))
		{
			item.CampId = null;
			item.CampKnown = false;
		}
		CampOwners.Remove(site.Id);
		Hold.ForgetSite(site.Id);
		SaveRoster();
		SaveWorld();
		_nextCampAssign = 0;
		GrudgeLog("CAMPSITE reverted (test): " + site.Id + " removed from camps.json");
		ShowNotification("enRAGE: the placed camp site is gone." + ((LastPlaced != null) ? " (Revert again for the one before.)" : ""));
	}

	internal void DebugCampHere(Ped player)
	{
		District currentDistrict = CurrentDistrict;
		Gang g = ((currentDistrict != null) ? (from kv in ControlMap[currentDistrict.Key]
			orderby kv.Value descending
			select Gangs.ById(kv.Key)).FirstOrDefault((Gang x) => x != null && Gangs.InEra(x) && x.HasCamps && !x.OrdersNotFeelings) : null) ?? Gangs.All.FirstOrDefault((Gang x) => x.HasCamps && !x.OrdersNotFeelings && !x.StoryOnly);
		NemesisRecord nemesisRecord = ((g == null) ? null : (from n in Roster
			where n.Gang == g.Id && n.IsAlive && !n.PresumedDead && n.Rank >= 2 && OffStage(n)
			orderby n.Rank descending
			select n).FirstOrDefault());
		if (nemesisRecord == null)
		{
			ShowNotification("enRAGE: no officer of the " + (g?.Name ?? "outfits") + " free to hold a camp here.");
			return;
		}
		Vector3 vector = player.Position + player.ForwardVector * 14f;
		CampSite site = new CampSite("debug_camp_" + Game.GameTime, 0u, vector.X, vector.Y, vector.Z, currentDistrict?.Name ?? "here", hideout: false, null, g.Id);
		SpawnCamp(site, nemesisRecord, player);
		AddTestCampBlip(site, g, nemesisRecord);
		GrudgeLog("CAMPKIT test camp: the " + g.Name + " under " + nemesisRecord.Name + ", 14 m ahead");
		ShowNotification("enRAGE: a " + g.Name + " camp under " + nemesisRecord.Name + ", 14 m ahead. It goes when you ride off.");
	}

	internal bool CampHeldOff(string siteId)
	{
		if (siteId != null && _campHeldOff.TryGetValue(siteId, out var value))
		{
			return Game.GameTime < value;
		}
		return false;
	}

	internal static bool IsTestSite(string id)
	{
		if (id != null)
		{
			if (!id.StartsWith("look_") && !id.StartsWith("debug_camp_"))
			{
				return id == "debug_kit";
			}
			return true;
		}
		return false;
	}

	private void AddTestCampBlip(CampSite site, Gang g, NemesisRecord boss, Vector3? at = null)
	{
		RemoveTestCampBlip(site.Id);
		Vector3 vector = at ?? Kit.Centre(site);
		Crumbs.Native("test camp blip add");
		int num = MAP.BLIP_ADD_FOR_COORDS(2339406903u, vector.X, vector.Y, vector.Z);
		if (num != 0)
		{
			MAP.SET_BLIP_SPRITE(num, MISC.GET_HASH_KEY("blip_region_hideout"), p2: true);
			string text = ((g != null) ? (MemberWord(g) + " ") : "") + "Camp" + ((boss != null) ? (" - " + (OfficerTitle(boss) + " " + boss.Name).Trim()) : "") + " (test)";
			Function.Call(11290983183885202434uL, num, new InputArgument(UiDraw.ForeverVarString(text)));
			_testCampBlips[site.Id] = num;
		}
	}

	internal unsafe void RemoveTestCampBlip(string siteId)
	{
		if (siteId != null && _testCampBlips.TryGetValue(siteId, out var value))
		{
			_testCampBlips.Remove(siteId);
			Crumbs.Native("test camp blip remove");
			if (MAP.DOES_BLIP_EXIST(value))
			{
				MAP.REMOVE_BLIP(&value);
			}
		}
	}

	internal void DebugCampLookAtNearest(Ped player, Gang g)
	{
		Vector3 pos = player.Position;
		CampSite campSite = (from x in Camps.AllSites
			where !x.Hideout
			orderby pos.DistanceTo2D(new Vector3(x.X, x.Y, x.Z))
			select x).FirstOrDefault();
		float num = ((campSite == null) ? float.MaxValue : pos.DistanceTo2D(new Vector3(campSite.X, campSite.Y, campSite.Z)));
		DespawnTestCamps();
		if (num > 250f)
		{
			Vector3 vector = pos + player.ForwardVector * 20f;
			campSite = new CampSite("here_" + Game.GameTime, 0u, vector.X, vector.Y, vector.Z, CurrentDistrict?.Name ?? "here", hideout: false, null, g.Id);
			GrudgeLog("CAMPLOOK no camp site within 250 m" + ((num < float.MaxValue) ? $" (nearest {num:0} m)" : "") + " - the " + g.Name + " camp goes 20 m ahead of you");
			num = 20f;
		}
		if (_liveCamps.TryGetValue(campSite.Id, out var value))
		{
			DespawnCamp(value, delete: true);
		}
		if (!campSite.Id.StartsWith("here_"))
		{
			_campHeldOff[campSite.Id] = int.MaxValue;
		}
		CampSite campSite2 = new CampSite("look_" + campSite.Id, 0u, campSite.X, campSite.Y, campSite.Z, campSite.Region, hideout: false, null, g.Id);
		Kit.ForgetCentre(campSite2.Id);
		NemesisRecord nemesisRecord = (_lookPropsOnly ? null : (from n in Roster
			where n.Gang == g.Id && n.IsAlive && !n.PresumedDead && n.Rank >= 2 && OffStage(n)
			orderby n.Rank descending
			select n).FirstOrDefault());
		if (nemesisRecord == null)
		{
			Kit.Spawn(campSite2, g.Id, IsSupplyCamp(campSite2));
			AddTestCampBlip(campSite2, g, null);
			string text = (_lookPropsOnly ? "props only" : "props only - no officer of theirs free");
			GrudgeLog("CAMPLOOK " + campSite.Id + ": the " + g.Name + " look, " + text);
			SpawnerSays($"the {g.Name} camp at {campSite.Region} ({num:0} m) - {text}, marked on your map. 'Despawn' takes it away.");
		}
		else
		{
			SpawnCamp(campSite2, nemesisRecord, player);
			AddTestCampBlip(campSite2, g, nemesisRecord);
			GrudgeLog("CAMPLOOK " + campSite.Id + ": the " + g.Name + " under " + nemesisRecord.Name);
			SpawnerSays($"a {g.Name} camp under {nemesisRecord.Name} at {campSite.Region} ({num:0} m), marked on your map. 'Despawn' takes it away.");
		}
	}

	private int DespawnTestCamps()
	{
		int num = 0;
		foreach (LiveCamp item in _liveCamps.Values.Where((LiveCamp c) => c.Site.Id.StartsWith("look_") || c.Site.Id.StartsWith("debug_camp_")).ToList())
		{
			DespawnCamp(item, delete: true);
			num++;
		}
		foreach (string item2 in Kit.Keys.Where((string k) => k.StartsWith("look_") || k == "debug_kit").ToList())
		{
			DespawnCampKit(item2);
			num++;
		}
		foreach (string item3 in _campHeldOff.Keys.ToList())
		{
			if (_campHeldOff[item3] == int.MaxValue)
			{
				_campHeldOff[item3] = Game.GameTime + 180000;
			}
		}
		return num;
	}

	private void TidyTestKits(Vector3 me)
	{
		foreach (string item in Kit.Keys.Where((string k) => IsTestSite(k) && !_liveCamps.ContainsKey(k)).ToList())
		{
			Vector3? vector = Kit.FirstSpot(item);
			if (!vector.HasValue || vector.GetValueOrDefault().DistanceTo2D(me) <= 260f)
			{
				continue;
			}
			DespawnCampKit(item);
			if (item.StartsWith("look_"))
			{
				string key = item.Substring("look_".Length);
				if (_campHeldOff.TryGetValue(key, out var value) && value == int.MaxValue)
				{
					_campHeldOff[key] = Game.GameTime + 180000;
				}
			}
			GrudgeLog("CAMPLOOK " + item + ": you rode off - the test camp is gone");
		}
	}

	internal void DebugDespawnCamp(Ped player)
	{
		int num = DespawnTestCamps();
		if (num > 0)
		{
			GrudgeLog($"CAMPLOOK despawned {num} test camp(s)");
			ShowNotification("enRAGE: test camp taken away.");
			return;
		}
		Vector3 pos = player.Position;
		LiveCamp liveCamp = _liveCamps.Values.OrderBy((LiveCamp c) => pos.DistanceTo(SitePos(c.Site))).FirstOrDefault();
		if (liveCamp == null)
		{
			ShowNotification("enRAGE: no camp is up near you.");
			return;
		}
		string id = liveCamp.Site.Id;
		DespawnCamp(liveCamp, delete: true);
		_campHeldOff[id] = Game.GameTime + 180000;
		GrudgeLog("CAMPLOOK despawned " + id + " (held off 3 minutes)");
		ShowNotification("enRAGE: " + liveCamp.Site.Label + " taken away - it won't come back for 3 minutes.");
	}
}
