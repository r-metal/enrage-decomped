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
	private sealed class LiveCamp
	{
		public CampSite Site;

		public NemesisRecord Boss;

		public readonly List<Ped> Peds = new List<Ped>();

		public readonly List<Blip> Blips = new List<Blip>();

		public bool OwnImap;

		public bool Engaged;

		public bool Cleared;

		public float Suspicion;

		public bool Warned;

		public string Sense;

		public readonly HashSet<int> BodiesSeen = new HashSet<int>();

		public int IndoorPending;

		public bool BossAway;
	}

	private const float CampNearest = 120f;

	private const int CampNearPercent = 10;

	private Dictionary<string, int> _cleared;

	private bool _knownSitesSeeded;

	private bool _waypointWas;

	private bool _routeRestored;

	private List<CampCandidateRow> _campCandidates = new List<CampCandidateRow>();

	private int _nextCampLearn;

	internal string CampSiteNote = "";

	private int _nextCampAware;

	private int _campShotSeen = -100000;

	internal string CampAwareNote = "";

	private static readonly string[] CallOuts = new string[5] { "Hey... who's there?", "You hear something?", "Somebody out there?", "Show yourself!", "Thought I saw something..." };

	private bool AssignChain(Gang gang)
	{
		if (gang?.Tiers == null || gang.Tiers.Length == 0)
		{
			return false;
		}
		bool result = false;
		string succ = SuccessorId(gang);
		List<NemesisRecord> living = Roster.Where((NemesisRecord n) => n.Gang == gang.Id && n.IsAlive && !n.PresumedDead && n.Id != succ).ToList();
		List<NemesisRecord>[] byTier = (from i in Enumerable.Range(0, gang.Tiers.Length)
			select (from n in living
				where TierOf(n) == i
				orderby n.Id
				select n).ToList()).ToArray();
		int t;
		for (t = 0; t < byTier.Length; t++)
		{
			List<NemesisRecord> list = null;
			int num = t - 1;
			while (num >= 0 && list == null)
			{
				if (byTier[num].Count > 0)
				{
					list = byTier[num];
				}
				num--;
			}
			foreach (NemesisRecord m in byTier[t])
			{
				if (list == null)
				{
					if (m.BossId != null)
					{
						m.BossId = null;
						result = true;
					}
				}
				else
				{
					if (m.BossId != null && list.Any((NemesisRecord b) => b.Id == m.BossId))
					{
						continue;
					}
					NemesisRecord nemesisRecord = (from b in list
						orderby byTier[t].Count((NemesisRecord x) => x.BossId == b.Id), b.Id
						select b).First();
					m.BossId = nemesisRecord.Id;
					result = true;
				}
			}
		}
		return result;
		int TierOf(NemesisRecord n)
		{
			for (int i = 0; i < gang.Tiers.Length; i++)
			{
				if (n.Rank >= gang.Tiers[i].MinRank)
				{
					return i;
				}
			}
			return gang.Tiers.Length - 1;
		}
	}

	private void UpdateCamps(Ped player)
	{
		if (Config.CampsEnabled)
		{
			UpdateCampAwareness(player);
		}
		LearnCampSites(player);
		if (!Config.CampsEnabled || Game.GameTime < _nextCampCheck)
		{
			return;
		}
		_nextCampCheck = Game.GameTime + 1000;
		foreach (LiveCamp item in _liveCamps.Values.ToList())
		{
			try
			{
				CheckHideoutIndoors(item);
			}
			catch (Exception ex)
			{
				GrudgeLog("CAMP indoors failed: " + ex.Message);
			}
		}
		if (Game.GameTime >= _nextCampAssign)
		{
			_nextCampAssign = Game.GameTime + 15000;
			try
			{
				AuditWetSites();
			}
			catch (Exception ex2)
			{
				GrudgeLog("CAMP water check failed: " + ex2.Message);
			}
			AssignCamps();
			try
			{
				HoldingsPass();
			}
			catch (Exception ex3)
			{
				GrudgeLog("HOLDINGS pass failed: " + ex3.Message);
			}
			CoolTempers();
			RollFriends();
			RefreshCampBlips();
		}
		Vector3 position = player.Position;
		CheckCampRoute(player, position);
		try
		{
			TidyTestKits(position);
		}
		catch (Exception ex4)
		{
			GrudgeLog("CAMPLOOK tidy failed: " + ex4.Message);
		}
		foreach (LiveCamp lc in _liveCamps.Values.ToList())
		{
			float num = position.DistanceTo(SitePos(lc.Site));
			bool flag = lc.Peds.Any((Ped p) => p.Exists() && !p.IsDead && !LooksKnockedOut(p));
			if (!flag && !lc.Cleared && lc.Peds.Count > 0 && num < Config.CampSpawnRange + 20f && IsTestSite(lc.Site.Id))
			{
				lc.Cleared = true;
				GrudgeLog("CAMPLOOK test camp " + lc.Site.Id + " wiped out - nothing real changes");
			}
			if (!flag && !lc.Cleared && lc.Peds.Count > 0 && num < Config.CampSpawnRange + 20f)
			{
				lc.Cleared = true;
				if (!IsTreasury(lc.Site))
				{
					try
					{
						PurseCampCleared(Gangs.ById(lc.Boss?.Gang ?? lc.Site.Gang), lc.Site.Label);
					}
					catch
					{
					}
				}
				MarkCampCleared(lc.Site);
				Feed(string.Format("CAMP {0}'s {1} cleared - empty for {2} day{3}", lc.Boss.Name, CampWord(lc), Config.CampClearDays, (Config.CampClearDays == 1) ? "" : "s"), Theme.Good, lc.Boss);
				if (!_raids.Any((Raid r) => r.CampId == lc.Site.Id && r.Arrived && r.ArrivedFirst))
				{
					WorldPart worldPart = PartFor(lc.Boss, (!lc.Boss.IsAlive || lc.Boss.PresumedDead) ? "KILLED" : ((CampWord(lc) == "stash") ? "STASH LOST" : "CAMP LOST"), lc.Boss.Title);
					worldPart.Effects.Add(string.Format("site empty {0} day{1}", Config.CampClearDays, (Config.CampClearDays == 1) ? "" : "s"));
					ChronicleDeed("camp", "You wiped out " + lc.Boss.Name + "'s " + CampWord(lc), "at " + lc.Site.Label, SitePos(lc.Site), lc.Boss.Gang, worldPart);
				}
				GrudgeNotice(lc.Site.Label + " is cleared. Nobody will hold it for a while.");
			}
			if (num > 260f || (!flag && num > 120f))
			{
				DespawnCamp(lc, delete: true);
				continue;
			}
			if (!lc.Boss.CampKnown && lc.Boss.CampId == lc.Site.Id && (lc.Site.Hideout ? lc.Engaged : (num < 90f)))
			{
				RevealCamp(lc.Boss, found: true);
				continue;
			}
			if (!IsStashSite(lc.Site))
			{
				continue;
			}
			bool engaged;
			if (!lc.Site.Hideout)
			{
				if (num < 90f)
				{
					goto IL_060f;
				}
				engaged = lc.Engaged;
			}
			else
			{
				engaged = lc.Engaged;
			}
			if (!engaged)
			{
				continue;
			}
			goto IL_060f;
			IL_060f:
			FoundStash(lc.Site);
		}
		if (player.IsDead || NoSpawnReason(player, place: true) != null)
		{
			return;
		}
		foreach (NemesisRecord item2 in Roster.Where((NemesisRecord n) => n.CampId != null && n.Gang != null && Mans(n)))
		{
			if (_liveCamps.ContainsKey(item2.CampId) || CampHeldOff(item2.CampId))
			{
				continue;
			}
			bool flag2 = item2 == ActiveRecord || _worldNemeses.ContainsValue(item2);
			if (!flag2 || FieldCap(Gangs.ById(item2.Gang)) > 0)
			{
				CampSite campSite = Camps.ById(item2.CampId);
				if (campSite != null && !IsCleared(campSite) && !(position.DistanceTo(SitePos(campSite)) > Config.CampSpawnRange) && (!(position.DistanceTo(SitePos(campSite)) > 110f) || HiddenFromView(player, SitePos(campSite) + new Vector3(0f, 0f, 1.2f))) && TownSiteReady(campSite))
				{
					SpawnCamp(campSite, item2, player, flag2);
					break;
				}
			}
		}
	}

	private Dictionary<string, int> Cleared
	{
		get
		{
			if (_cleared != null)
			{
				return _cleared;
			}
			_cleared = new Dictionary<string, int>();
			string[] array = (Config.ClearedCamps ?? "").Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(':');
				if (array2.Length == 2 && int.TryParse(array2[1], out var result))
				{
					_cleared[array2[0]] = result;
				}
			}
			return _cleared;
		}
	}

	private static DateTime DayOf(int key)
	{
		return GameDate.FromDayKey(key);
	}

	internal bool IsCleared(CampSite site)
	{
		if (TakeoverPending(site))
		{
			return true;
		}
		if (site == null || !Cleared.TryGetValue(site.Id, out var value))
		{
			return false;
		}
		try
		{
			return (DayOf(GameDayKey()) - DayOf(value)).TotalDays <= (double)Config.CampClearDays;
		}
		catch
		{
			return false;
		}
	}

	private void ClearCampsTheMissionCleared(Ped player)
	{
		List<Vector3> source = new List<Vector3>(_missionTrail) { player.Position, _missionStartPos };
		foreach (CampSite item in Camps.AllSites.ToList())
		{
			if (item == null || IsCleared(item))
			{
				continue;
			}
			Vector3 sp = new Vector3(item.X, item.Y, item.Z);
			if (source.Any((Vector3 t) => t.DistanceTo2D(sp) < 250f))
			{
				if (_liveCamps.TryGetValue(item.Id, out var value))
				{
					DespawnCamp(value, delete: true);
				}
				MarkCampCleared(item, story: true);
				GrudgeLog(string.Format("CAMP {0}: a story mission went through it - cleared for {1} day{2}", item.Label, Config.CampClearDays, (Config.CampClearDays == 1) ? "" : "s"));
			}
		}
	}

	private void MarkCampCleared(CampSite site, bool story = false)
	{
		if (!story)
		{
			try
			{
				LiveCamp value;
				Gang g = Gangs.ById((Roster.FirstOrDefault((NemesisRecord n) => n.CampId == site.Id && n.Gang != null) ?? (_liveCamps.TryGetValue(site.Id, out value) ? value.Boss : null))?.Gang ?? site.Gang ?? Gangs.ODriscolls.Id);
				ShiftControl(DistrictAt(new Vector3(site.X, site.Y, site.Z)), g, -6, "lost " + site.Label);
			}
			catch
			{
			}
		}
		Cleared[site.Id] = GameDayKey();
		foreach (string item in Cleared.Keys.ToList())
		{
			if (!IsCleared(Camps.ById(item)))
			{
				Cleared.Remove(item);
			}
		}
		Config.ClearedCamps = string.Join(",", Cleared.Select((KeyValuePair<string, int> kv) => $"{kv.Key}:{kv.Value}"));
		Config.Save();
		foreach (NemesisRecord item2 in Roster.Where((NemesisRecord n) => n.CampId == site.Id))
		{
			item2.CampId = null;
			item2.CampKnown = false;
		}
		SaveRoster();
		RefreshCampBlips();
	}

	private void SpawnCamp(CampSite site, NemesisRecord boss, Ped player, bool bossAway = false)
	{
		if (!TownSiteReady(site))
		{
			return;
		}
		if (_unguardedCaches.Contains(site.Id))
		{
			DespawnUnguardedCache(site.Id, "an officer took the site - the camp's own stores come");
		}
		LiveCamp lc = new LiveCamp
		{
			Site = site,
			Boss = boss,
			BossAway = bossAway
		};
		_liveCamps[site.Id] = lc;
		if (site.Imap != 0 && !STREAMING.IS_IPL_ACTIVE_HASH(site.Imap))
		{
			Crumbs.Native("Camps map chunk in");
			STREAMING.REQUEST_IPL_HASH(site.Imap);
			lc.OwnImap = true;
		}
		Gang gang = Gangs.ById(boss.Gang);
		if (!_liveCamps.Values.Any((LiveCamp c) => c.Engaged && CampGroupOf(c) == CampGroupOf(lc)))
		{
			World.SetRelationshipBetweenGroups((gang != null && gang.NeedsReason && !ReasonMet(gang)) ? eRelationshipType.Ignore : eRelationshipType.Dislike, CampGroupOf(lc), player.RelationshipGroup);
		}
		Vector3 center = Kit.Centre(site);
		if (site.Hideout && site.Imap == 0)
		{
			try
			{
				UnlockHideout(site);
			}
			catch (Exception ex)
			{
				GrudgeLog("CAMPDOORS unlock failed: " + ex.Message);
			}
			try
			{
				FindHideoutPoints(site);
			}
			catch (Exception ex2)
			{
				GrudgeLog("CAMP hideout points failed: " + ex2.Message);
			}
		}
		if (SiteInTown(site))
		{
			GrudgeLog("CAMPKIT " + site.Id + ": in Saint Denis - just the men, no camp laid out");
		}
		else
		{
			try
			{
				Kit.Spawn(site, boss.Gang ?? site.Gang, IsSupplyCamp(site));
			}
			catch (Exception ex3)
			{
				GrudgeLog("CAMPKIT failed: " + ex3.Message);
			}
		}
		int num = 0;
		if (bossAway)
		{
			GrudgeLog("CAMP " + site.Id + ": " + boss.Name + " is out" + ((boss == ActiveRecord) ? " (an encounter of his is on)" : " (in the world)") + " - his men hold it");
		}
		else
		{
			SpawnCampPed(lc, boss, center, num++, named: true);
		}
		int num2 = _rng.Next(100);
		int count = ((num2 >= Balance.CampExtraNamed0) ? ((num2 < Balance.CampExtraNamed0 + Balance.CampExtraNamed1) ? 1 : 2) : 0);
		foreach (NemesisRecord item in (from _ in Roster
			where _.BossId == boss.Id && !IsOfficer(_) && _.CanHunt && !_.PresumedDead && _ != ActiveRecord && !_worldNemeses.ContainsValue(_)
			orderby _rng.Next()
			select _).Take(count))
		{
			SpawnCampPed(lc, item, center, num++, named: true);
		}
		bool flag = IsTreasury(site);
		int num3 = Math.Max(0, Math.Min(FollowerCount(boss.Rank) + 2 + PurseCrewShift(Gangs.ById(boss.Gang)) + (flag ? 3 : 0), (flag ? 12 : 9) - lc.Peds.Count));
		Gang gang2 = Gangs.ById(boss.Gang);
		int num4 = FieldCap(gang2);
		if (num3 > num4)
		{
			GrudgeLog($"CAMP {site.Id}: the {gang2?.Name} have {MenOf(gang2)} men left - {num4} hired guns, not {num3}");
			num3 = num4;
		}
		lc.IndoorPending = ((site.Hideout && site.Imap == 0) ? HoldBackForIndoors(site, num3) : 0);
		if (bossAway && lc.Peds.Count == 0 && num3 > 0 && lc.IndoorPending >= num3)
		{
			lc.IndoorPending = num3 - 1;
		}
		GrudgeLog(string.Format("CAMP {0}: {1} + {2} of his men named, {3} hired guns{4}", site.Id, boss.Name, Math.Max(0, lc.Peds.Count - 1), num3, (lc.IndoorPending > 0) ? $" ({lc.IndoorPending} wait to go inside when the house is loaded)" : ""));
		for (int num5 = 0; num5 < num3 - lc.IndoorPending; num5++)
		{
			SpawnCampPed(lc, boss, center, num++, named: false);
		}
		if (bossAway && lc.Peds.Count == 0 && lc.IndoorPending == 0)
		{
			GrudgeLog("CAMP " + site.Id + ": " + boss.Name + " is out and no man of his to hold it - not put up");
			DespawnCamp(lc, delete: true);
			_campHeldOff[site.Id] = Game.GameTime + 60000;
			return;
		}
		if (flag)
		{
			try
			{
				SpawnStrongboxes(site);
			}
			catch (Exception ex4)
			{
				GrudgeLog("TREASURY boxes failed: " + ex4.Message);
			}
		}
		if (IsSupplyCamp(site))
		{
			try
			{
				SpawnSupplies(site);
			}
			catch (Exception ex5)
			{
				GrudgeLog("SUPPLY stores failed: " + ex5.Message);
			}
		}
		if (!IsTestSite(site.Id))
		{
			MaybeRaid(lc);
		}
		try
		{
			CampSighting(site, gang2);
		}
		catch (Exception ex6)
		{
			GrudgeLog("SIGHTING camp failed: " + ex6.Message);
		}
	}

	private unsafe void SpawnCampPed(LiveCamp lc, NemesisRecord rec, Vector3 center, int slot, bool named)
	{
		double num = (double)slot * 2.39996 + _rng.NextDouble() * 0.5;
		float num2 = ((slot == 0) ? 2f : (3f + (float)(slot % 3) * 2.5f + (float)_rng.NextDouble()));
		Vector3 vector = center + new Vector3((float)Math.Cos(num) * num2, (float)Math.Sin(num) * num2, 0f);
		for (int i = 0; i < 6; i++)
		{
			if (Kit.ClearOf(lc.Site.Id, vector, 0.6f))
			{
				break;
			}
			num += 0.45;
			num2 += 0.4f;
			vector = center + new Vector3((float)Math.Cos(num) * num2, (float)Math.Sin(num) * num2, 0f);
		}
		bool flag = lc.Site.Hideout && lc.Site.Imap == 0;
		bool flag2 = flag && slot > 0 && !_forceIndoor && _rng.Next(100) < 30;
		(Vector3, float, int)? tuple = ((flag && !flag2) ? NextHideoutPlace(lc.Site) : (((Vector3, float, int)?)null));
		if (tuple.HasValue)
		{
			vector = StandIn(tuple.Value.Item1, tuple.Value.Item2);
		}
		else
		{
			Vector3 safeCoordForPed = World.GetSafeCoordForPed(vector, onGround: true, 0);
			if ((safeCoordForPed.X != 0f || safeCoordForPed.Y != 0f) && safeCoordForPed.DistanceTo(vector) < 12f && safeCoordForPed.Z <= center.Z + 2f && (flag || Outdoors(safeCoordForPed)))
			{
				vector = safeCoordForPed;
			}
			else
			{
				float z = default(float);
				if (MISC.GET_GROUND_Z_FOR_3D_COORD(vector.X, vector.Y, center.Z + 3f, &z, p4: false))
				{
					vector.Z = z;
				}
				else
				{
					Vector3 vector2 = GroundOnSpot(vector);
					if (vector2 != Vector3.Zero && vector2.Z <= center.Z + 8f && (flag || Outdoors(vector2)))
					{
						vector = vector2;
					}
				}
				if (!flag && !Outdoors(vector))
				{
					vector = center + new Vector3((float)Math.Cos(num) * 2.5f, (float)Math.Sin(num) * 2.5f, 0f);
				}
				else if (!flag && SiteInAnyTown(lc.Site) && (Roofed(vector) || Math.Abs(vector.Z - center.Z) > 2.5f))
				{
					vector = center + new Vector3((float)Math.Cos(num) * 1.5f, (float)Math.Sin(num) * 1.5f, 0f);
				}
			}
		}
		uint hash = (named ? rec.ModelHash : HiredGunModel(rec));
		Model model = new Model(hash);
		if (!model.IsValid || !model.IsPed)
		{
			model = new Model(PedHash.g_m_m_unibanditos_01);
		}
		float heading = (float)(Math.Atan2(0f - (center.X - vector.X), center.Y - vector.Y) * 180.0 / Math.PI);
		Ped ped = SpawnPed((PedHash)model.Hash, vector, heading);
		STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)model.Hash);
		if (ped == null)
		{
			return;
		}
		MakeOurs(ped.Handle);
		ped.RelationshipGroup = CampGroupOf(lc);
		if (named)
		{
			ArmForHate(ped, rec, drawNow: false);
			ApplyRankStats(ped, rec, fullHeal: true);
			ApplyScars(ped, rec);
			if (rec.Limp)
			{
				_bulletImpact.ApplyCondition(ped, CrippleLevel.Limping);
			}
			foreach (int item in (from kv in _worldNemeses
				where kv.Value == rec
				select kv.Key).ToList())
			{
				_worldNemeses.Remove(item);
			}
			_worldNemeses[ped.Handle] = rec;
		}
		else
		{
			Gang gang = Gangs.ById(lc.Boss?.Gang);
			if (gang != null && gang.Id != Gangs.ODriscolls.Id)
			{
				ArmTroop(ped, gang);
			}
			else
			{
				ped.GiveWeapon((_rng.Next(2) == 0 && !ShortOfArms(gang ?? Gangs.ODriscolls)) ? eWeapon.RepeaterCarbine : eWeapon.RevolverCattleman, 60, eWeaponAttachPoint.PistolR, bForceInHand: false, bForceInHolster: true);
			}
			ped.Accuracy = 20;
			_followerOf[ped.Handle] = lc.Boss;
		}
		if (tuple.HasValue)
		{
			SitAt(ped, tuple.Value.Item3, tuple.Value.Item1, tuple.Value.Item2);
		}
		else if (flag2)
		{
			Function.Call(16164602603745899285uL, ped.Handle, center.X, center.Y, center.Z, 35f, 3f, 12f, 0);
		}
		else
		{
			string text = ((slot == 0 && (!lc.Site.Hideout || lc.Site.Imap != 0)) ? "WORLD_HUMAN_FIRE_STAND" : CampScenarios[_rng.Next(CampScenarios.Length)]);
			TASK.TASK_START_SCENARIO_IN_PLACE_HASH(ped.Handle, MISC.GET_HASH_KEY(text), -1, playEnterAnim: true, 0u, -1f, p6: false);
		}
		lc.Peds.Add(ped);
	}

	private uint HiredGunModel(NemesisRecord boss)
	{
		PedHash[] array = Gangs.ById(boss.Gang)?.Seed?.Where<(int, int, PedHash[])>(((int Rank, int Count, PedHash[] Models) t) => t.Rank <= 3).SelectMany<(int, int, PedHash[]), PedHash>(((int Rank, int Count, PedHash[] Models) t) => t.Models).ToArray();
		if (array == null || array.Length == 0)
		{
			return boss.ModelHash;
		}
		return (uint)array[_rng.Next(array.Length)];
	}

	private void EngageCamp(LiveCamp lc, Ped player)
	{
		lc.Engaged = true;
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, CampGroupOf(lc), player.RelationshipGroup);
		if (!lc.Boss.CampKnown && lc.Boss.CampId == lc.Site.Id)
		{
			RevealCamp(lc.Boss, found: true);
		}
		else if (IsStashSite(lc.Site))
		{
			FoundStash(lc.Site);
		}
		int num = 0;
		foreach (Ped item in lc.Peds.Where((Ped p) => p.Exists() && !p.IsDead))
		{
			if (_interro == null || !(_interro.Ped == item))
			{
				Crumbs.Native("Camps blip add (V2)");
				lc.Blips.Add(item.AddBlip(BlipType.BLIP_STYLE_ENEMY));
				if (PED.IS_PED_USING_ANY_SCENARIO(item.Handle))
				{
					Crumbs.Native("Camps out of scenario");
					TASK.CLEAR_PED_TASKS(item.Handle, p1: false, p2: false);
					QueueFight(item, player, (item.Position.DistanceTo(player.Position) < 15f) ? 150 : 400);
					num++;
				}
				else
				{
					TASK.TASK_COMBAT_PED(item.Handle, player.Handle, 0, 16);
					PED.SET_PED_KEEP_TASK(item.Handle, toggle: true);
				}
			}
		}
		if (num > 0)
		{
			GrudgeLog($"CAMP engage: {num} getting up out of what they were doing to fight");
		}
		Ped ped = lc.Peds.FirstOrDefault();
		object obj;
		if (!lc.BossAway)
		{
			obj = lc.Boss.Name;
		}
		else
		{
			Gang gang = Gangs.ById(lc.Boss.Gang);
			obj = ((gang != null) ? (OutfitWord(gang) + " man") : "A man");
		}
		string text = (string)obj;
		if (ped != null && ped.Exists() && !ped.IsDead)
		{
			ShowSubtitle(text + ": \"" + Taunts.CampAlarm(lc.Boss, _rng) + "\"");
		}
	}

	private unsafe void DespawnCamp(LiveCamp lc, bool delete)
	{
		try
		{
			RelockHideout(lc.Site);
		}
		catch (Exception ex)
		{
			GrudgeLog("CAMPDOORS relock failed: " + ex.Message);
		}
		try
		{
			DespawnStrongboxes(lc.Site.Id);
		}
		catch (Exception ex2)
		{
			GrudgeLog("TREASURY boxes delete failed: " + ex2.Message);
		}
		try
		{
			DespawnSupplies(lc.Site.Id);
		}
		catch (Exception ex3)
		{
			GrudgeLog("SUPPLY stores delete failed: " + ex3.Message);
		}
		try
		{
			DespawnCampKit(lc.Site.Id);
		}
		catch (Exception ex4)
		{
			GrudgeLog("CAMPKIT delete failed: " + ex4.Message);
		}
		_hideoutPoints.Remove(lc.Site.Id);
		foreach (Blip blip in lc.Blips)
		{
			if (blip != null && blip.Exists())
			{
				blip.Delete();
			}
		}
		foreach (Ped ped in lc.Peds)
		{
			if (ped.Exists())
			{
				int handle = ped.Handle;
				if (delete && !ped.IsDead && SafeToDelete(ped))
				{
					_worldNemeses.Remove(handle);
					_followerOf.Remove(handle);
					_knockedOutWorld.Remove(handle);
					ped.Delete();
				}
				else
				{
					ReleaseIfOurs(&handle);
				}
			}
		}
		Crumbs.Native("Camps map chunk out");
		if (lc.OwnImap)
		{
			STREAMING.REMOVE_IPL_HASH(lc.Site.Imap);
		}
		_liveCamps.Remove(lc.Site.Id);
		if (lc.Site.Id.StartsWith("look_") && _campHeldOff.TryGetValue(lc.Site.Id.Substring("look_".Length), out var value) && value == int.MaxValue)
		{
			_campHeldOff[lc.Site.Id.Substring("look_".Length)] = Game.GameTime + 180000;
		}
	}

	internal void RevealCamp(NemesisRecord boss, bool found)
	{
		if (found && boss.CampKnown && boss.CampUnsure)
		{
			boss.CampUnsure = false;
			SaveRoster();
			RedoCampBlip(boss.CampId);
			string[] obj = new string[5] { "It's true - ", boss.Name, "'s ", null, null };
			CampSite campSite = CampOf(boss);
			obj[3] = ((campSite != null && campSite.Hideout) ? "hideout" : "camp");
			obj[4] = " is right where you were told.";
			ReplyNotice(string.Concat(obj));
		}
		else if (!boss.CampKnown && boss.CampId != null)
		{
			boss.CampKnown = true;
			KnowSite(boss.CampId);
			SaveRoster();
			RefreshCampBlips();
			FlashCampBlip(boss.CampId);
			CampSite campSite2 = Camps.ById(boss.CampId);
			if (found)
			{
				ReplyNotice("You've found " + OfficerTitle(boss) + " " + boss.FullName + "'s " + (campSite2.Hideout ? "hideout" : "camp") + ": " + campSite2.Label + ".");
			}
		}
	}

	private List<string> KnownSites => Chronicle.KnownSites ?? (Chronicle.KnownSites = new List<string>());

	internal bool SiteKnown(string siteId)
	{
		if (siteId != null)
		{
			return KnownSites.Contains(siteId);
		}
		return false;
	}

	private void KnowSite(string siteId)
	{
		if (siteId != null && !KnownSites.Contains(siteId))
		{
			KnownSites.Add(siteId);
			SaveWorld();
		}
	}

	internal static string OfficerTitle(NemesisRecord n)
	{
		Gang gang = Gangs.Of(n);
		if (gang != null && gang.RankInName)
		{
			return "";
		}
		if (gang?.Tiers != null && gang.Tiers.Length != 0)
		{
			for (int i = 0; i < gang.Tiers.Length; i++)
			{
				if (n.Rank >= gang.Tiers[i].MinRank)
				{
					if (i != gang.Tiers.Length - 1)
					{
						return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Backstory.Singular(gang.Tiers[i].Label).ToLowerInvariant());
					}
					return "";
				}
			}
			return "";
		}
		if (n.Rank < 4)
		{
			if (n.Rank < 2)
			{
				return "";
			}
			return "Lieutenant";
		}
		return "Captain";
	}

	internal static string TierWord(NemesisRecord n, bool plural)
	{
		Gang gang = Gangs.Of(n);
		string text = null;
		if (gang?.Tiers != null)
		{
			(string, int, int)[] tiers = gang.Tiers;
			for (int i = 0; i < tiers.Length; i++)
			{
				(string, int, int) tuple = tiers[i];
				if (n.Rank >= tuple.Item2)
				{
					(text, _, _) = tuple;
					break;
				}
			}
		}
		text = (text ?? ((n.Rank >= 4) ? "CAPTAINS" : "LIEUTENANTS")).ToLowerInvariant();
		if (!plural)
		{
			return Backstory.Singular(text);
		}
		return text;
	}

	private unsafe void RefreshCampBlips()
	{
		if (!MapSettled())
		{
			_mapPending.Add(RefreshCampBlips);
			return;
		}
		if (_mapHidden)
		{
			foreach (string item in _campBlips.Keys.ToList())
			{
				int num = _campBlips[item];
				MAP.REMOVE_BLIP(&num);
			}
			_campBlips.Clear();
			return;
		}
		Dictionary<string, NemesisRecord> want = (from n in Roster
			where n.CampId != null && n.CampKnown && Mans(n)
			group n by n.CampId).ToDictionary((IGrouping<string, NemesisRecord> g) => g.Key, (IGrouping<string, NemesisRecord> g) => g.First());
		foreach (string item2 in _campBlips.Keys.Where((string k) => !want.ContainsKey(k)).ToList())
		{
			int num2 = _campBlips[item2];
			Crumbs.Native("Camps blip remove");
			MAP.REMOVE_BLIP(&num2);
			_campBlips.Remove(item2);
		}
		foreach (KeyValuePair<string, NemesisRecord> item3 in want)
		{
			CampSite campSite = Camps.ById(item3.Key);
			if (campSite == null)
			{
				continue;
			}
			Gang gang = TreasuryGangOf(campSite);
			bool flag = gang != null && TreasuryKnown(gang);
			Gang gang2 = SupplyGangOf(campSite);
			string value;
			Gang gang3 = ((gang2 != null && Hold.SupplyKnownAt.TryGetValue(gang2.Id, out value) && value == campSite.Id) ? gang2 : null);
			string text = (flag ? "blip_robbery_home" : ((gang3 != null) ? "blip_chest" : "blip_region_hideout"));
			Gang gang4 = Gangs.Of(item3.Value);
			string text2 = ((gang4 != null && item3.Value.Id == SuccessorId(gang4)) ? (item3.Value.Name + " (leader)") : (OfficerTitle(item3.Value) + " " + item3.Value.Name).Trim());
			string text3 = (flag ? "Treasury" : ((gang3 != null) ? "Supplies" : (campSite.Hideout ? "Hideout" : "Camp")));
			string text4 = ((gang4 != null) ? (MemberWord(gang4) + " " + text3 + " - " + text2) : (text2 + "'s " + text3));
			if (flag && TreasuryRobbedNow(gang))
			{
				text4 += " (robbed)";
			}
			else if (gang3 != null && SupplyRaidedRecently(gang3))
			{
				text4 += ((Hold.SupplyCampHitSite.TryGetValue(gang3.Id, out var value2) && value2 != campSite.Id) ? " (restocking)" : " (raided)");
			}
			text4 += (item3.Value.CampUnsure ? " (?)" : "");
			string text5 = text + "|" + text4;
			if (_campBlips.ContainsKey(item3.Key))
			{
				if (_campBlipSigs.TryGetValue(item3.Key, out var value3) && value3 == text5)
				{
					continue;
				}
				GrudgeLog("CAMPS marker " + campSite.Label + ": \"" + (value3 ?? "?").Split('|').Last() + "\" -> \"" + text4 + "\"");
				int num3 = _campBlips[item3.Key];
				Crumbs.Native("Camps blip remove (changed)");
				MAP.REMOVE_BLIP(&num3);
				_campBlips.Remove(item3.Key);
			}
			Crumbs.Native("Camps blip add");
			int num4 = MAP.BLIP_ADD_FOR_COORDS(2339406903u, campSite.X, campSite.Y, campSite.Z);
			Crumbs.Native("Camps blip sprite");
			MAP.SET_BLIP_SPRITE(num4, MISC.GET_HASH_KEY(text), p2: true);
			Function.Call(11290983183885202434uL, num4, new InputArgument(UiDraw.ForeverVarString(text4)));
			_campBlips[item3.Key] = num4;
			_campBlipSigs[item3.Key] = text5;
		}
	}

	internal unsafe void RedoCampBlip(string campId)
	{
		Crumbs.Native("Camps blip remove");
		if (campId != null && _campBlips.TryGetValue(campId, out var value))
		{
			MAP.REMOVE_BLIP(&value);
			_campBlips.Remove(campId);
		}
		RefreshCampBlips();
	}

	internal void TrackCamp(NemesisRecord boss)
	{
		CampSite campSite = Camps.ById(boss?.CampId);
		if (campSite != null && boss.CampKnown)
		{
			MAP.CLEAR_GPS_MULTI_ROUTE();
			MAP.START_GPS_MULTI_ROUTE(MISC.GET_HASH_KEY("COLOR_RED"), onFoot: true, inVehicle: true);
			MAP.ADD_POINT_TO_GPS_MULTI_ROUTE(campSite.X, campSite.Y, campSite.Z, p3: false);
			MAP.SET_GPS_MULTI_ROUTE_RENDER(toggle: true);
			_gpsCamp = campSite.Id;
			Chronicle.GpsCamp = campSite.Id;
			SaveWorld();
			_waypointWas = MAP.IS_WAYPOINT_ACTIVE();
			ShowNotification("Route set: " + OfficerTitle(boss) + " " + boss.Name + "'s " + (campSite.Hideout ? "hideout" : "camp") + ", " + campSite.Label + ".");
		}
	}

	private void ClearCampRoute()
	{
		MAP.SET_GPS_MULTI_ROUTE_RENDER(toggle: false);
		MAP.CLEAR_GPS_MULTI_ROUTE();
		_gpsCamp = null;
		if (Chronicle.GpsCamp != null)
		{
			Chronicle.GpsCamp = null;
			SaveWorld();
		}
	}

	private void CheckCampRoute(Ped player, Vector3 pos)
	{
		if (!_routeRestored)
		{
			_routeRestored = true;
			_waypointWas = MAP.IS_WAYPOINT_ACTIVE();
			if (Chronicle.GpsCamp != null)
			{
				_gpsCamp = Chronicle.GpsCamp;
			}
			else
			{
				MAP.SET_GPS_MULTI_ROUTE_RENDER(toggle: false);
				MAP.CLEAR_GPS_MULTI_ROUTE();
			}
		}
		bool flag = MAP.IS_WAYPOINT_ACTIVE();
		bool flag2 = flag && !_waypointWas;
		_waypointWas = flag;
		if (_gpsCamp == null)
		{
			return;
		}
		CampSite g = Camps.ById(_gpsCamp);
		string text = ((g == null) ? "the camp is gone" : ((pos.DistanceTo(SitePos(g)) < 80f) ? "you're there" : (IsCleared(g) ? "the camp was cleared" : ((!Roster.Any((NemesisRecord n) => n.CampId == g.Id && n.IsAlive && !n.PresumedDead && n.CampKnown)) ? "nobody you know holds it now" : (flag2 ? "you set your own waypoint" : null)))));
		if (text != null)
		{
			GrudgeLog("CAMP route to " + (g?.Label ?? _gpsCamp) + " cleared - " + text);
			if (text != "you're there")
			{
				ShowNotification("Route to " + (g?.Label ?? "the camp") + " cleared - " + text + ".");
			}
			ClearCampRoute();
		}
	}

	private unsafe void CleanupCamps()
	{
		foreach (LiveCamp item in _liveCamps.Values.ToList())
		{
			DespawnCamp(item, delete: true);
		}
		try
		{
			DespawnSupplyCaches();
		}
		catch (Exception ex)
		{
			GrudgeLog("SUPPLY cache cleanup failed: " + ex.Message);
		}
		try
		{
			foreach (string item2 in Kit.Keys.ToList())
			{
				DespawnCampKit(item2);
			}
			foreach (string item3 in _testCampBlips.Keys.ToList())
			{
				RemoveTestCampBlip(item3);
			}
		}
		catch (Exception ex2)
		{
			GrudgeLog("CAMPKIT cleanup failed: " + ex2.Message);
		}
		Crumbs.Native("Camps blip remove");
		foreach (string item4 in _campBlips.Keys.ToList())
		{
			int num = _campBlips[item4];
			MAP.REMOVE_BLIP(&num);
		}
		_campBlips.Clear();
		CleanupStashBlips();
		CleanupCacheBlips();
		HideMapIcons();
		if (_gpsCamp != null)
		{
			ClearCampRoute();
		}
	}

	private string CampsPath => Path.Combine(ContentDir, "camps.json");

	private void LoadCampSites()
	{
		try
		{
			if (!File.Exists(CampsPath))
			{
				SaveCampSites();
				return;
			}
			CampsContent campsContent = JsonConvert.DeserializeObject<CampsContent>(File.ReadAllText(CampsPath));
			Camps.Extra = (from r in campsContent?.Sites ?? new List<CampRow>()
				where r.Id != null && Gangs.ById(r.Gang) != null
				select new CampSite(r.Id, 0u, r.X, r.Y, r.Z, r.Region ?? "the wilds", r.Hideout, r.Name, r.Gang)).ToList();
			HashSet<string> builtIn = new HashSet<string>(from s in Camps.ODriscoll.Concat(Camps.GameGangCamps)
				select s.Id);
			int num = Camps.Extra.RemoveAll((CampSite s) => builtIn.Contains(s.Id));
			if (num > 0)
			{
				GrudgeLog($"CAMPSITE {num} site(s) in camps.json are built into the mod now - taken out of the file");
				SaveCampSites();
			}
			HashSet<string> removed = new HashSet<string>(Camps.Removed.Select((CampSite s) => s.Id));
			int num2 = Camps.Extra.RemoveAll((CampSite s) => removed.Contains(s.Id));
			if (num2 > 0)
			{
				GrudgeLog($"CAMPSITE {num2} site(s) in camps.json are taken out of the mod - out of the file too");
				SaveCampSites();
			}
			int num3 = Camps.Extra.RemoveAll((CampSite s) => AtTheFort(new Vector3(s.X, s.Y, s.Z)));
			if (num3 > 0)
			{
				GrudgeLog($"CAMPSITE dropped {num3} learned site(s) at Fort Wallace - the fort is its own hideout");
				SaveCampSites();
			}
			_campCandidates = (campsContent?.Candidates ?? new List<CampCandidateRow>()).Where((CampCandidateRow x) => x.Gang != null && (DateTime.UtcNow - x.FirstSeen).TotalDays < 14.0).ToList();
			GrudgeLog(string.Format("CONTENT camps.json: {0} sites{1}", Camps.Extra.Count, (Camps.Extra.Count > 0) ? (" (" + string.Join(", ", from s in Camps.Extra
				group s by s.Gang into g
				select $"{g.Key} {g.Count()}") + ")") : ""));
		}
		catch (Exception ex)
		{
			KeepBadFile(CampsPath, ex);
		}
	}

	private void SaveCampSites()
	{
		try
		{
			CampsContent value = new CampsContent
			{
				Sites = Camps.Extra.Select((CampSite s) => new CampRow
				{
					Id = s.Id,
					Gang = s.Gang,
					Name = s.Name,
					Region = s.Region,
					X = s.X,
					Y = s.Y,
					Z = s.Z,
					Hideout = s.Hideout
				}).ToList(),
				Candidates = _campCandidates.ToList()
			};
			SafeFile.Write(CampsPath, JsonConvert.SerializeObject(value, Formatting.Indented));
		}
		catch (Exception ex)
		{
			GrudgeLog("CAMPSITE save failed: " + ex.Message);
		}
	}

	internal bool AddCampSite(Gang g, Vector3 pos, string how)
	{
		if (g == null || g.Id == Gangs.ODriscolls.Id)
		{
			return false;
		}
		if (AtTheFort(pos))
		{
			return false;
		}
		if (Camps.Extra.Count((CampSite s) => s.Gang == g.Id) >= 6)
		{
			return false;
		}
		if (Camps.ForGang(g.Id).Any((CampSite s) => new Vector3(s.X, s.Y, s.Z).DistanceTo2D(pos) < 400f))
		{
			return false;
		}
		string zoneName = GetZoneName(pos);
		bool flag = Camps.Extra.Count((CampSite s) => s.Gang == g.Id) == 0;
		CampSite campSite = new CampSite(g.Id + "_" + NewId(6), 0u, pos.X, pos.Y, pos.Z, zoneName, flag, flag ? (g.Name + " stronghold in " + zoneName) : null, g.Id);
		Camps.Extra.Add(campSite);
		SaveCampSites();
		GrudgeLog($"CAMPSITE {how}: {campSite.Label} at {pos.X:0},{pos.Y:0},{pos.Z:0}");
		Feed("CAMP SITE " + g.Name + ": " + campSite.Label + " (" + how + ")", g.Accent);
		_nextCampAssign = 0;
		return true;
	}

	private static bool AtTheFort(Vector3 p)
	{
		return p.DistanceTo2D(FortGate) < 450f;
	}

	internal void AddCampHere(Gang g)
	{
		Ped character = Game.Player.Character;
		ShowNotification(AddCampSite(g, character.Position, "added by you") ? ("Camp site added for the " + g.Name + " here.") : "No: O'Driscolls use the game's camps, max 6 per faction, 400 m apart.");
	}

	private void LearnCampSites(Ped player)
	{
		if (Game.GameTime < _nextCampLearn)
		{
			return;
		}
		_nextCampLearn = Game.GameTime + 10000;
		foreach (IGrouping<string, (Ped, Gang)> item in from x in (from p in PedsAround(player)
				where p != null && p.Exists() && !p.IsDead && p.IsHuman && !p.IsPlayer && !IsOurs(p.Handle) && !WasOurs.Contains(p.Handle) && ENTITY.GET_ENTITY_SPEED(p.Handle) < 1.5f && p.Position.DistanceTo(player.Position) < 150f
				select (p: p, g: Gangs.ForModel((uint)p.Model.Hash)) into x
				where x.g != null && x.g.Id != Gangs.ODriscolls.Id
				select x).ToList()
			group x by x.g.Id)
		{
			Gang g = item.First().Item2;
			List<Vector3> ps = item.Select<(Ped, Gang), Vector3>(((Ped p, Gang g) x) => x.p.Position).ToList();
			Vector3 centre = ps.FirstOrDefault((Vector3 c) => ps.Count((Vector3 o) => o.DistanceTo(c) < 25f) >= 3);
			if (!(centre == Vector3.Zero) && !InsideAnyTown(centre) && TownAt(centre) == null && !Camps.ForGang(g.Id).Any((CampSite s) => new Vector3(s.X, s.Y, s.Z).DistanceTo2D(centre) < 400f) && Camps.RetiredNear(centre, 400f) == null)
			{
				CampCandidateRow campCandidateRow = _campCandidates.FirstOrDefault((CampCandidateRow c) => c.Gang == g.Id && new Vector3(c.X - centre.X, c.Y - centre.Y, 0f).Length() < 60f);
				if (campCandidateRow == null)
				{
					_campCandidates.Add(new CampCandidateRow
					{
						Gang = g.Id,
						X = centre.X,
						Y = centre.Y,
						Z = centre.Z,
						FirstSeen = DateTime.UtcNow
					});
					SaveCampSites();
					CampSiteNote = $"{g.Name} gathered at {centre.X:0},{centre.Y:0} - a camp if they're there again in 10 minutes";
					GrudgeLog("CAMPSITE candidate: " + CampSiteNote);
				}
				else if (!((DateTime.UtcNow - campCandidateRow.FirstSeen).TotalMinutes < 10.0))
				{
					_campCandidates.Remove(campCandidateRow);
					GrudgeLog($"CAMPSITE {g.Name} gathered at {centre.X:0},{centre.Y:0} again, {(DateTime.UtcNow - campCandidateRow.FirstSeen).TotalMinutes:0} min after first - learned");
					AddCampSite(g, centre, "learned - his men gather here");
				}
			}
		}
	}

	private void UpdateCampAwareness(Ped player)
	{
		if (_liveCamps.Count == 0 || Game.GameTime < _nextCampAware)
		{
			if (_liveCamps.Count == 0)
			{
				CampAwareNote = "";
			}
			return;
		}
		int num = Game.GameTime - (_nextCampAware - 200);
		_nextCampAware = Game.GameTime + 200;
		float num2 = Math.Max(0.05f, Math.Min(0.5f, (float)num / 1000f));
		Sight s = SightFor(player, 30f);
		float range = s.Range;
		bool flag = (GunMelee.PlayerFiredRecently || PED.IS_PED_SHOOTING(player.Handle)) && !GameEventGunfire;
		bool flag2 = false;
		if (flag)
		{
			uint num3 = WEAPON.GET_WEAPONTYPE_GROUP(WEAPON._GET_PED_CURRENT_HELD_WEAPON(player.Handle));
			flag2 = num3 == 3053283277u || num3 == 1548507267;
		}
		int lastShotAt = GunMelee.LastShotAt;
		bool flag3 = lastShotAt != _campShotSeen && lastShotAt - _campShotSeen > 400;
		_campShotSeen = lastShotAt;
		foreach (LiveCamp lc in _liveCamps.Values.ToList())
		{
			if (lc.Engaged)
			{
				continue;
			}
			List<Ped> list = lc.Peds.Where((Ped p) => p.Exists() && !p.IsDead).ToList();
			if (list.Count == 0)
			{
				continue;
			}
			float num4 = player.Position.DistanceTo(SitePos(lc.Site));
			string text = null;
			if (list.Any((Ped p) => PED.IS_PED_IN_COMBAT(p.Handle, player.Handle)))
			{
				text = "a fight broke out";
			}
			else
			{
				if (flag && !flag2 && num4 < 120f)
				{
					Gang gang = Gangs.ById(lc.Boss?.Gang);
					if (gang == null || !gang.NeedsReason || ReasonMet(gang))
					{
						text = "heard gunfire";
						goto IL_0296;
					}
				}
				if (flag && flag2 && flag3 && num4 < 15f)
				{
					lc.Suspicion += 40f;
					lc.Sense = "heard something";
				}
			}
			goto IL_0296;
			IL_0296:
			foreach (Ped dead in lc.Peds.Where((Ped p) => p.Exists() && p.IsDead && !lc.BodiesSeen.Contains(p.Handle)).ToList())
			{
				if (PED.GET_PED_SOURCE_OF_DEATH(dead.Handle) != player.Handle)
				{
					lc.BodiesSeen.Add(dead.Handle);
					continue;
				}
				Ped ped = list.FirstOrDefault((Ped w) => w.Position.DistanceTo(dead.Position) < 15f && ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY(w.Handle, dead.Handle, 17));
				if (!(ped == null))
				{
					lc.BodiesSeen.Add(dead.Handle);
					lc.Suspicion += 60f;
					lc.Sense = WhoIs(ped.Handle) + " found a body";
					ShowSubtitle(WhoIs(ped.Handle) + ": \"" + ((_rng.Next(2) == 0) ? "Oh God - he's dead! Someone's here!" : "Wake up... WAKE UP! We got company!") + "\"");
				}
			}
			Ped ped2 = null;
			float num5 = float.MaxValue;
			foreach (Ped item in list)
			{
				if (!(item.Position.DistanceTo(player.Position) >= num5) && Sees(item, player, s, out var d))
				{
					ped2 = item;
					num5 = d;
					if (d < 3f)
					{
						break;
					}
				}
			}
			if (ped2 != null)
			{
				float num6 = ((num5 < 3f) ? 1000f : (15f + 60f * (1f - num5 / range)));
				lc.Suspicion += num6 * num2;
				lc.Sense = $"{WhoIs(ped2.Handle)} sees you at {num5:0} m ({s.Stance}, sight {range:0} m)";
				if (lc.Suspicion >= 45f && !lc.Warned)
				{
					lc.Warned = true;
					TASK.TASK_TURN_PED_TO_FACE_ENTITY(ped2.Handle, player.Handle, 2500, 0f, 0f, 0f);
					ShowSubtitle(WhoIs(ped2.Handle) + ": \"" + CallOuts[_rng.Next(CallOuts.Length)] + "\"");
					Feed("CAMP " + lc.Boss.Name + "'s camp is suspicious (" + lc.Sense + ")", Theme.Warn);
				}
			}
			else
			{
				lc.Suspicion = Math.Max(0f, lc.Suspicion - 10f * num2);
				if (lc.Suspicion < 20f)
				{
					lc.Warned = false;
				}
			}
			Gang gang2 = Gangs.ById(lc.Boss?.Gang);
			if (lc.Suspicion >= 100f && text == null && (gang2 == null || !gang2.NeedsReason || ReasonMet(gang2)))
			{
				text = lc.Sense ?? "spotted you";
			}
			CampAwareNote = string.Format("{0}'s camp: suspicion {1:0}  ({2})  sight {3:0} m ({4})", lc.Boss.Name, Math.Min(100f, lc.Suspicion), (ped2 != null) ? lc.Sense : "unseen", range, s.Stance);
			if (text != null)
			{
				Feed("CAMP " + lc.Boss.Name + "'s camp alerted: " + text, Theme.Blood);
				GrudgeLog($"CAMP alerted ({text}) at {num4:0} m");
				EngageCamp(lc, player);
			}
		}
	}
}
