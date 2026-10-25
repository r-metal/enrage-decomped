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
	private sealed class HoldingsWorld : IHoldingsWorld
	{
		private readonly NemesisMod _m;

		public WorldState State => _m.Chronicle;

		public long Now => _m.GameMinutesNow();

		public IEnumerable<Gang> Outfits => Gangs.All;

		public IEnumerable<Vector3> OfficerCamps => (from n in _m.Roster
			where n.CampId != null && IsOfficer(n)
			select Camps.ById(n.CampId) into s
			where s != null
			select s).Select(SitePos);

		internal HoldingsWorld(NemesisMod m)
		{
			_m = m;
		}

		public bool Destroyed(Gang g)
		{
			return _m.IsDestroyed(g);
		}

		public CampSite Site(string id)
		{
			return Camps.ById(id);
		}

		public IEnumerable<CampSite> SitesHeldBy(string gangId)
		{
			return _m.SitesHeldBy(gangId);
		}

		public string Owner(CampSite s)
		{
			return _m.SiteOwner(s);
		}

		public bool TakeoverPending(CampSite s)
		{
			return _m.TakeoverPending(s);
		}

		public bool Cleared(CampSite s)
		{
			return _m.IsCleared(s);
		}

		public bool Wet(CampSite s)
		{
			return _m.WetSite(s);
		}

		public bool InTown(CampSite s)
		{
			return _m.SiteInTown(s);
		}

		public NemesisRecord Holder(CampSite s)
		{
			return _m.HolderOf(s);
		}

		public bool AnyoneCampsAt(string siteId)
		{
			return _m.Roster.Any((NemesisRecord n) => n.CampId == siteId);
		}

		public bool LiveSupplies(string siteId)
		{
			return _m._liveSupplies.ContainsKey(siteId);
		}

		public bool LiveTreasury(string siteId)
		{
			return _m._liveTreasuries.ContainsKey(siteId);
		}

		public NemesisRecord LiveBoss(string siteId)
		{
			if (!_m._liveCamps.TryGetValue(siteId, out var value))
			{
				return null;
			}
			return value.Boss;
		}

		public string LiveCampOf(NemesisRecord n)
		{
			return _m._liveCamps.Values.FirstOrDefault((LiveCamp c) => c.Boss == n)?.Site.Id;
		}

		public IEnumerable<NemesisRecord> FreeMen(Gang g)
		{
			return _m.Roster.Where((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && n.CanHunt && !n.PresumedDead && !n.Paralyzed && n.CampId == null && n != _m.ActiveRecord && !_m._worldNemeses.ContainsValue(n));
		}

		public int Purse(Gang g)
		{
			return _m.PurseOf(g);
		}

		public bool HasPurse(Gang g)
		{
			return _m.HasPurse(g);
		}

		public int Roll(int n)
		{
			return _m._rng.Next(n);
		}
	}

	internal Holdings Hold => _hold ?? (_hold = new Holdings(new HoldingsWorld(this)));

	private void HoldingsPass()
	{
		bool changed;
		List<HoldingsEvent> ev = Hold.Pass(out changed);
		ApplyHoldings(ev);
		if (changed)
		{
			SaveWorld();
		}
	}

	private void ApplyHoldings(List<HoldingsEvent> ev)
	{
		bool flag = false;
		bool flag2 = false;
		foreach (HoldingsEvent item in ev)
		{
			switch (item.Act)
			{
			case HoldingsAct.Log:
				GrudgeLog(item.Text);
				break;
			case HoldingsAct.RedoCampBlip:
				RedoCampBlip(item.SiteId);
				break;
			case HoldingsAct.StashBlips:
				flag = true;
				break;
			case HoldingsAct.CacheBlips:
				flag2 = true;
				break;
			case HoldingsAct.RevealCamp:
				if (item.Who != null)
				{
					RevealCamp(item.Who, found: false);
				}
				break;
			case HoldingsAct.Notice:
				ReplyNotice(item.Text);
				break;
			case HoldingsAct.WorldNews:
				GrudgeLog("HOLDINGS news" + (item.Big ? " (big)" : "") + ": " + item.Text);
				if (Config.WorldNews >= (item.Big ? 2 : 3))
				{
					ShowNotification(item.Text);
				}
				break;
			case HoldingsAct.PurseTaken:
				AddPurse(item.From, -item.Amount, "the " + item.To.Name + " took their " + (item.Stash ? "stash" : "treasury") + " at " + item.Place);
				AddPurse(item.To, item.Amount, "they took the " + OutfitPoss(item.From) + " " + (item.Stash ? "stash" : "treasury") + " at " + item.Place);
				break;
			}
		}
		if (flag)
		{
			RefreshStashBlips();
		}
		if (flag2)
		{
			RefreshCacheBlips();
		}
	}

	private IReadOnlyDictionary<string, string> SupplyCamps => Hold.SupplyCamps;

	private IReadOnlyDictionary<string, long> SupplyCampHit => Hold.SupplyCampHit;

	private IReadOnlyDictionary<string, long> SupplyCaches => Hold.SupplyCaches;

	private IReadOnlyDictionary<string, string> SupplyKnownAt => Hold.SupplyKnownAt;

	private IReadOnlyDictionary<string, string> Treasuries => Hold.Treasuries;

	private IReadOnlyCollection<string> TreasuryKnownList => Hold.TreasuryKnownList;

	private IReadOnlyDictionary<string, long> TreasuryRobbed => Hold.TreasuryRobbed;

	private IReadOnlyDictionary<string, string> TreasuryRobbedSite => Hold.TreasuryRobbedSite;

	private IReadOnlyCollection<string> StashList => Hold.StashList;

	internal CampSite SupplyCampOf(Gang g)
	{
		return Hold.SupplyCampOf(g);
	}

	internal bool IsSupplyCamp(CampSite s)
	{
		return Hold.IsSupplyCamp(s);
	}

	private Gang SupplyGangOf(CampSite s)
	{
		return Hold.SupplyGangOf(s);
	}

	private bool SupplyRaidedRecently(Gang g)
	{
		return Hold.SupplyRaidedRecently(g);
	}

	internal bool IsSupplyCache(Gang g)
	{
		return Hold.IsSupplyCache(g);
	}

	internal bool SupplyCacheKnown(Gang g)
	{
		return Hold.SupplyCacheKnown(g);
	}

	private bool CacheGuardsDue(Gang g)
	{
		return Hold.CacheGuardsDue(g);
	}

	internal void KeeperLeft(NemesisRecord r, string how)
	{
		Gang gang = Gangs.Of(r);
		if (r == null || gang == null)
		{
			return;
		}
		string value;
		bool flag = Hold.SupplyRaidedRecently(gang) && Hold.SupplyCampHitSite.TryGetValue(gang.Id, out value) && Hold.SupplyCampOf(gang)?.Id == value && (value == r.CampId || Hold.SupplyKeeper(gang) == r);
		string value2;
		bool flag2 = Hold.TreasuryRobbedNow(gang) && Hold.TreasuryRobbedSite.TryGetValue(gang.Id, out value2) && Hold.TreasuryOf(gang)?.Id == value2 && (value2 == r.CampId || (Hold.IsStash(gang) && Hold.StashKeeper(gang) == r));
		if (flag || flag2)
		{
			if (flag)
			{
				Hold.MarkKeeperGone(gang, treasury: false);
			}
			if (flag2)
			{
				Hold.MarkKeeperGone(gang, treasury: true);
			}
			SaveWorld();
			GrudgeLog("HOLDINGS " + r.Name + " " + how + " - he kept the " + OutfitPoss(gang) + " " + ((flag && flag2) ? "supplies and money" : (flag ? "supplies" : "money")) + " you hit: they leave once you've gone");
		}
	}

	private NemesisRecord SupplyKeeper(Gang g)
	{
		return Hold.SupplyKeeper(g);
	}

	internal CampSite TreasuryOf(Gang g)
	{
		return Hold.TreasuryOf(g);
	}

	internal bool IsTreasury(CampSite s)
	{
		return Hold.IsTreasury(s);
	}

	private Gang TreasuryGangOf(CampSite s)
	{
		return Hold.TreasuryGangOf(s);
	}

	internal bool TreasuryKnown(Gang g)
	{
		return Hold.TreasuryKnown(g);
	}

	private bool TreasuryRobbedNow(Gang g)
	{
		return Hold.TreasuryRobbedNow(g);
	}

	internal bool IsStash(Gang g)
	{
		return Hold.IsStash(g);
	}

	internal bool IsStashSite(CampSite s)
	{
		return Hold.IsStashSite(s);
	}

	private NemesisRecord StashKeeper(Gang g)
	{
		return Hold.StashKeeper(g);
	}
}
