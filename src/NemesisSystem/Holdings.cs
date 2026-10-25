using System;
using System.Collections.Generic;
using System.Linq;
using RDR2.Math;

namespace NemesisSystem;

internal sealed class Holdings
{
	internal const long SupplyRestock = 2880L;

	internal const long TreasuryMoveAfter = 1440L;

	internal const long CacheGuardDelay = 360L;

	internal const long SuppliesMoveAfter = 1440L;

	internal const double TreasuryShare = 0.4;

	internal const int StashBelowCamps = 4;

	internal const int StashBackAtCamps = 5;

	private readonly IHoldingsWorld _w;

	private WorldState S => _w.State;

	internal Dictionary<string, string> SupplyCamps => S.SupplyCamps ?? (S.SupplyCamps = new Dictionary<string, string>());

	internal Dictionary<string, long> SupplyCaches => S.SupplyCaches ?? (S.SupplyCaches = new Dictionary<string, long>());

	internal Dictionary<string, string> SupplyKnownAt => S.SupplyKnownAt ?? (S.SupplyKnownAt = new Dictionary<string, string>());

	internal Dictionary<string, long> SupplyCampHit => S.SupplyCampHitAt ?? (S.SupplyCampHitAt = new Dictionary<string, long>());

	internal Dictionary<string, string> SupplyCampHitSite => S.SupplyCampHitSite ?? (S.SupplyCampHitSite = new Dictionary<string, string>());

	internal Dictionary<string, string> Treasuries => S.Treasuries ?? (S.Treasuries = new Dictionary<string, string>());

	internal List<string> TreasuryKnownList => S.TreasuryKnown ?? (S.TreasuryKnown = new List<string>());

	internal Dictionary<string, long> TreasuryRobbed => S.TreasuryRobbedAt ?? (S.TreasuryRobbedAt = new Dictionary<string, long>());

	internal Dictionary<string, string> TreasuryRobbedSite => S.TreasuryRobbedSite ?? (S.TreasuryRobbedSite = new Dictionary<string, string>());

	internal List<string> StashList => S.TreasuryStash ?? (S.TreasuryStash = new List<string>());

	internal List<string> KeeperGoneList => S.KeeperGone ?? (S.KeeperGone = new List<string>());

	internal Holdings(IHoldingsWorld world)
	{
		_w = world;
	}

	internal static bool HoldsCamps(Gang g)
	{
		if (g != null && !g.OrdersNotFeelings)
		{
			return !g.StoryOnly;
		}
		return false;
	}

	internal CampSite SupplyCampOf(Gang g)
	{
		if (g == null || !HoldsCamps(g) || !SupplyCamps.TryGetValue(g.Id, out var value))
		{
			return null;
		}
		return _w.Site(value);
	}

	internal bool IsSupplyCamp(CampSite s)
	{
		if (s != null)
		{
			return SupplyCamps.Values.Contains(s.Id);
		}
		return false;
	}

	internal Gang SupplyGangOf(CampSite s)
	{
		if (s != null)
		{
			return Gangs.ById(SupplyCamps.FirstOrDefault((KeyValuePair<string, string> kv) => kv.Value == s.Id).Key);
		}
		return null;
	}

	internal bool SupplyRaidedRecently(Gang g)
	{
		if (g != null && SupplyCampHit.TryGetValue(g.Id, out var value) && _w.Now >= value)
		{
			return _w.Now - value < 2880;
		}
		return false;
	}

	internal bool IsSupplyCache(Gang g)
	{
		if (g != null)
		{
			return SupplyCaches.ContainsKey(g.Id);
		}
		return false;
	}

	internal bool SupplyCacheKnown(Gang g)
	{
		if (g != null)
		{
			CampSite campSite = SupplyCampOf(g);
			if (campSite != null && SupplyKnownAt.TryGetValue(g.Id, out var value))
			{
				return value == campSite.Id;
			}
		}
		return false;
	}

	internal bool CacheGuardsDue(Gang g)
	{
		if (g != null && SupplyCaches.TryGetValue(g.Id, out var value))
		{
			if (_w.Now - value < 360)
			{
				return _w.Now < value;
			}
			return true;
		}
		return false;
	}

	internal void KnowSupplies(Gang g, CampSite s)
	{
		if (g != null && s != null)
		{
			SupplyKnownAt[g.Id] = s.Id;
		}
	}

	internal CampSite TreasuryOf(Gang g)
	{
		if (g == null || !HoldsCamps(g) || !Treasuries.TryGetValue(g.Id, out var value))
		{
			return null;
		}
		return _w.Site(value);
	}

	internal bool IsTreasury(CampSite s)
	{
		if (s != null)
		{
			return Treasuries.Values.Contains(s.Id);
		}
		return false;
	}

	internal Gang TreasuryGangOf(CampSite s)
	{
		if (s != null)
		{
			return Gangs.ById(Treasuries.FirstOrDefault((KeyValuePair<string, string> kv) => kv.Value == s.Id).Key);
		}
		return null;
	}

	internal bool TreasuryKnown(Gang g)
	{
		if (g != null)
		{
			return TreasuryKnownList.Contains(g.Id);
		}
		return false;
	}

	internal bool TreasuryRobbedNow(Gang g)
	{
		if (g != null)
		{
			return TreasuryRobbed.ContainsKey(g.Id);
		}
		return false;
	}

	internal bool IsStash(Gang g)
	{
		if (g != null)
		{
			return StashList.Contains(g.Id);
		}
		return false;
	}

	internal bool IsStashSite(CampSite s)
	{
		if (s != null)
		{
			return StashList.Any((string id) => TreasuryOf(Gangs.ById(id))?.Id == s.Id);
		}
		return false;
	}

	internal int MannedCamps(Gang g)
	{
		return _w.SitesHeldBy(g.Id).Count((CampSite s) => _w.Holder(s) != null && !_w.Cleared(s));
	}

	internal NemesisRecord StashKeeper(Gang g)
	{
		return Keeper(g, TreasuryOf(g)?.Id, (!IsSupplyCache(g)) ? null : SupplyCampOf(g)?.Id, null);
	}

	internal NemesisRecord SupplyKeeper(Gang g)
	{
		return Keeper(g, SupplyCampOf(g)?.Id, (!IsStash(g)) ? null : TreasuryOf(g)?.Id, IsStash(g) ? StashKeeperPlain(g) : null);
	}

	private NemesisRecord StashKeeperPlain(Gang g)
	{
		return Keeper(g, TreasuryOf(g)?.Id, null, null);
	}

	private NemesisRecord Keeper(Gang g, string ownSite, string otherSite, NemesisRecord not)
	{
		NemesisRecord otherBoss = ((otherSite != null) ? _w.LiveBoss(otherSite) : null);
		return (from n in _w.FreeMen(g)
			where n != not && n != otherBoss && !BossElsewhere(n, ownSite)
			orderby n.Rank descending, n.Id
			select n).FirstOrDefault();
	}

	private bool BossElsewhere(NemesisRecord n, string ownSite)
	{
		string text = _w.LiveCampOf(n);
		if (text != null)
		{
			return text != ownSite;
		}
		return false;
	}

	internal CampSite PickStash(Gang g, CampSite except)
	{
		List<Vector3> mannedAll = _w.OfficerCamps.ToList();
		List<Vector3> own = (from s in _w.SitesHeldBy(g.Id)
			where _w.Holder(s) != null
			select s).Select(Pos).ToList();
		List<CampSite> source = (from s in _w.SitesHeldBy(g.Id)
			where (except == null || s.Id != except.Id) && !_w.Cleared(s) && !_w.TakeoverPending(s) && _w.Holder(s) == null && !_w.AnyoneCampsAt(s.Id) && _w.LiveBoss(s.Id) == null && !IsSupplyCamp(s)
			select s).ToList();
		float[] array = new float[2] { 400f, 250f };
		foreach (float gap in array)
		{
			CampSite campSite = (from s in source
				where mannedAll.All((Vector3 m) => m.DistanceTo2D(Pos(s)) >= gap)
				orderby s.Hideout descending, (own.Count != 0) ? own.Min((Vector3 o) => o.DistanceTo2D(Pos(s))) : 0f
				select s).FirstOrDefault();
			if (campSite != null)
			{
				return campSite;
			}
		}
		return null;
	}

	private static Vector3 Pos(CampSite s)
	{
		return new Vector3(s.X, s.Y, s.Z);
	}

	internal void RecordRaid(Gang g, string siteId)
	{
		SupplyCampHit[g.Id] = _w.Now;
		SupplyCampHitSite[g.Id] = siteId;
		KeeperGoneList.Remove("sup:" + g.Id);
	}

	internal void ForgetRaid(Gang g)
	{
		SupplyCampHit.Remove(g.Id);
		SupplyCampHitSite.Remove(g.Id);
		KeeperGoneList.Remove("sup:" + g.Id);
	}

	internal bool KeeperGone(Gang g, bool treasury)
	{
		if (g != null)
		{
			return KeeperGoneList.Contains((treasury ? "tre:" : "sup:") + g.Id);
		}
		return false;
	}

	internal void MarkKeeperGone(Gang g, bool treasury)
	{
		string item = (treasury ? "tre:" : "sup:") + g?.Id;
		if (g != null && !KeeperGoneList.Contains(item))
		{
			KeeperGoneList.Add(item);
		}
	}

	internal void RecordRobbery(Gang g, string siteId)
	{
		TreasuryRobbed[g.Id] = _w.Now;
		TreasuryRobbedSite[g.Id] = siteId;
		KnowTreasury(g);
		KeeperGoneList.Remove("tre:" + g.Id);
	}

	internal void ForgetRobbery(Gang g)
	{
		TreasuryRobbed.Remove(g.Id);
		TreasuryRobbedSite.Remove(g.Id);
	}

	internal bool KnowTreasury(Gang g)
	{
		if (g == null || TreasuryKnownList.Contains(g.Id))
		{
			return false;
		}
		TreasuryKnownList.Add(g.Id);
		return true;
	}

	internal void ForgetTreasuryKnown(Gang g)
	{
		if (g != null)
		{
			TreasuryKnownList.Remove(g.Id);
		}
	}

	internal void SetSupplyCamp(Gang g, CampSite s)
	{
		SupplyCamps[g.Id] = s.Id;
		SupplyCaches.Remove(g.Id);
		ForgetRaid(g);
	}

	internal void SetTreasury(Gang g, CampSite s)
	{
		Treasuries[g.Id] = s.Id;
		StashList.Remove(g.Id);
		ForgetRobbery(g);
		TreasuryKnownList.Remove(g.Id);
	}

	internal void ForgetSite(string siteId)
	{
		foreach (string item in (from kv in Treasuries
			where kv.Value == siteId
			select kv.Key).ToList())
		{
			Treasuries.Remove(item);
			StashList.Remove(item);
			TreasuryKnownList.Remove(item);
			TreasuryRobbed.Remove(item);
			TreasuryRobbedSite.Remove(item);
		}
		foreach (string item2 in (from kv in SupplyCamps
			where kv.Value == siteId
			select kv.Key).ToList())
		{
			SupplyCamps.Remove(item2);
			SupplyCaches.Remove(item2);
			SupplyKnownAt.Remove(item2);
		}
	}

	internal void Check(Action<string, string> fault)
	{
		foreach (Gang item in _w.Outfits.Where(HoldsCamps))
		{
			if (SupplyCamps.TryGetValue(item.Id, out var value))
			{
				CampSite campSite = _w.Site(value);
				if (campSite == null)
				{
					fault("supplies:" + item.Id, "the " + item.Name + " supplies are at a site that doesn't exist (" + value + ")");
				}
				else if (_w.Holder(campSite) == null && !IsSupplyCache(item) && !_w.LiveSupplies(campSite.Id) && _w.LiveBoss(campSite.Id) == null)
				{
					fault("supplies:" + item.Id, "the " + item.Name + " supplies are at " + campSite.Label + ", which nobody holds, and it isn't a cache");
				}
				else if (campSite != null && TreasuryOf(item)?.Id == campSite.Id)
				{
					fault("supplies-on-treasury:" + item.Id, "the " + item.Name + " supplies and money are both at " + campSite.Label);
				}
				else if (campSite != null && _w.InTown(campSite))
				{
					fault("supplies-in-town:" + item.Id, "the " + item.Name + " supplies are at " + campSite.Label + ", in town");
				}
			}
			if (Treasuries.TryGetValue(item.Id, out var value2))
			{
				CampSite campSite2 = _w.Site(value2);
				if (campSite2 == null)
				{
					fault("treasury:" + item.Id, "the " + item.Name + " money is at a site that doesn't exist (" + value2 + ")");
				}
				else if (_w.Holder(campSite2) == null && !IsStash(item) && !_w.LiveTreasury(campSite2.Id) && _w.LiveBoss(campSite2.Id) == null)
				{
					fault("treasury:" + item.Id, "the " + item.Name + " money is at " + campSite2.Label + ", which nobody holds, and it isn't a stash");
				}
			}
			if (IsStash(item) && IsSupplyCache(item) && StashKeeper(item) != null && StashKeeper(item) == SupplyKeeper(item))
			{
				fault("keepers:" + item.Id, "the " + item.Name + " stash and cache have the same keeper");
			}
		}
		foreach (string key in SupplyCaches.Keys)
		{
			if (SupplyCampOf(Gangs.ById(key)) == null)
			{
				fault("cache:" + key, "a cache for the " + (Gangs.ById(key)?.Name ?? key) + " with no supply site");
			}
		}
		foreach (string stash in StashList)
		{
			if (TreasuryOf(Gangs.ById(stash)) == null)
			{
				fault("stash:" + stash, "a stash for the " + (Gangs.ById(stash)?.Name ?? stash) + " with no treasury site");
			}
		}
		foreach (string key2 in SupplyKnownAt.Keys)
		{
			Gang gang = Gangs.ById(key2);
			if (gang != null && !HoldsCamps(gang))
			{
				fault("known:" + key2, "supplies known for the " + gang.Name + ", who hold no camps");
			}
		}
		foreach (KeyValuePair<string, string> item2 in SupplyKnownAt)
		{
			Gang gang2 = Gangs.ById(item2.Key);
			if (gang2 != null)
			{
				CampSite campSite3 = SupplyCampOf(gang2);
				if (campSite3 != null && campSite3.Id != item2.Value)
				{
					fault("known-moved:" + item2.Key, "the " + gang2.Name + " supplies moved to " + campSite3.Label + " but you still 'know' them at " + item2.Value);
				}
			}
		}
	}

	internal List<HoldingsEvent> Pass(out bool changed)
	{
		List<HoldingsEvent> list = new List<HoldingsEvent>();
		bool changed2 = false;
		PlaceTreasuries(list, ref changed2);
		PlaceSupplies(list, ref changed2);
		foreach (string item in SupplyCampHit.Keys.ToList())
		{
			if (!SupplyRaidedRecently(Gangs.ById(item)) || Gangs.ById(item) == null)
			{
				SupplyCampHit.Remove(item);
				SupplyCampHitSite.Remove(item);
				KeeperGoneList.Remove("sup:" + item);
				changed2 = true;
			}
		}
		foreach (string item2 in KeeperGoneList.Where((string k) => k.StartsWith("tre:") && !TreasuryRobbed.ContainsKey(k.Substring(4))).ToList())
		{
			KeeperGoneList.Remove(item2);
			changed2 = true;
		}
		changed = changed2;
		return list;
	}

	private static void Log(List<HoldingsEvent> ev, string text)
	{
		ev.Add(new HoldingsEvent
		{
			Act = HoldingsAct.Log,
			Text = text
		});
	}

	private static void Blip(List<HoldingsEvent> ev, string siteId)
	{
		if (siteId != null)
		{
			ev.Add(new HoldingsEvent
			{
				Act = HoldingsAct.RedoCampBlip,
				SiteId = siteId
			});
		}
	}

	private static void StashBlips(List<HoldingsEvent> ev)
	{
		ev.Add(new HoldingsEvent
		{
			Act = HoldingsAct.StashBlips
		});
	}

	private static void CacheBlips(List<HoldingsEvent> ev)
	{
		ev.Add(new HoldingsEvent
		{
			Act = HoldingsAct.CacheBlips
		});
	}

	private static void Notice(List<HoldingsEvent> ev, string text)
	{
		ev.Add(new HoldingsEvent
		{
			Act = HoldingsAct.Notice,
			Text = text
		});
	}

	private static void News(List<HoldingsEvent> ev, string text, bool big)
	{
		ev.Add(new HoldingsEvent
		{
			Act = HoldingsAct.WorldNews,
			Text = text,
			Big = big
		});
	}

	private static string The(Gang g)
	{
		if (!g.Name.StartsWith("The "))
		{
			return "the " + g.Name;
		}
		return "the " + g.Name.Substring(4);
	}

	private string From(Gang g, CampSite s)
	{
		return s.Name ?? (((_w.Owner(s) == g.Id) ? "their" : "the") + " camp in " + s.Region);
	}

	private static void Reveal(List<HoldingsEvent> ev, NemesisRecord who)
	{
		ev.Add(new HoldingsEvent
		{
			Act = HoldingsAct.RevealCamp,
			Who = who
		});
	}

	private static void Taken(List<HoldingsEvent> ev, Gang from, Gang to, int amount, string place, bool stash)
	{
		ev.Add(new HoldingsEvent
		{
			Act = HoldingsAct.PurseTaken,
			From = from,
			To = to,
			Amount = amount,
			Place = place,
			Stash = stash
		});
	}

	private bool Forget(Gang g)
	{
		return Treasuries.Remove(g.Id) | TreasuryKnownList.Remove(g.Id) | TreasuryRobbed.Remove(g.Id) | TreasuryRobbedSite.Remove(g.Id) | StashList.Remove(g.Id) | SupplyCamps.Remove(g.Id) | SupplyCaches.Remove(g.Id) | SupplyKnownAt.Remove(g.Id) | SupplyCampHit.Remove(g.Id) | SupplyCampHitSite.Remove(g.Id) | KeeperGoneList.Remove("sup:" + g.Id) | KeeperGoneList.Remove("tre:" + g.Id);
	}

	private void PlaceTreasuries(List<HoldingsEvent> ev, ref bool changed)
	{
		long now = _w.Now;
		foreach (Gang item in _w.Outfits.Where(HoldsCamps))
		{
			if (_w.Destroyed(item))
			{
				if (Forget(item))
				{
					changed = true;
				}
				continue;
			}
			CampSite site = TreasuryOf(item);
			if ((site != null && _w.LiveTreasury(site.Id)) || PlaceStash(item, site, now, ev, ref changed))
			{
				continue;
			}
			long value;
			bool flag = TreasuryRobbed.TryGetValue(item.Id, out value) && (now - value >= 1440 || now < value);
			if (flag && site != null && TreasuryRobbedSite.TryGetValue(item.Id, out var value2) && value2 != site.Id)
			{
				TreasuryRobbed.Remove(item.Id);
				TreasuryRobbedSite.Remove(item.Id);
				changed = true;
				Log(ev, "TREASURY " + item.Name + ": refilled at " + site.Label);
				flag = false;
			}
			string text = ((site != null) ? _w.Owner(site) : null);
			bool flag2 = site != null && text != item.Id;
			bool num = site != null && !flag2 && _w.Holder(site) != null && !_w.Cleared(site);
			string value3;
			bool flag3 = KeeperGone(item, treasury: true) && TreasuryRobbedSite.TryGetValue(item.Id, out value3) && site != null && value3 == site.Id;
			if (num && !flag && !flag3)
			{
				continue;
			}
			string value4;
			bool flag4 = TreasuryRobbedSite.TryGetValue(item.Id, out value4) && site != null && value4 == site.Id && TreasuryRobbed.ContainsKey(item.Id);
			if (site != null && !flag2 && !flag && !flag4 && !flag3 && TreasuryKnown(item) && !_w.Cleared(site))
			{
				if (!StashList.Contains(item.Id))
				{
					StashList.Add(item.Id);
				}
				changed = true;
				Log(ev, "TREASURY " + item.Name + ": nobody holds " + site.Label + " - the money you know of stays there, a stash now");
				Blip(ev, site.Id);
				StashBlips(ev);
				continue;
			}
			if (flag2)
			{
				Gang gang = Gangs.ById(text);
				int num2 = ((!TreasuryRobbedNow(item)) ? ((int)((double)_w.Purse(item) * 0.4)) : 0);
				if (gang != null && _w.HasPurse(gang) && num2 > 0)
				{
					Taken(ev, item, gang, num2, site.Label, stash: false);
				}
				Log(ev, "TREASURY " + item.Name + ": " + site.Label + " lost to the " + (gang?.Name ?? text) + ((num2 > 0) ? $" - they took ${num2}" : ""));
			}
			CampSite campSite = (from s in _w.SitesHeldBy(item.Id)
				where (site == null || s.Id != site.Id) && !_w.TakeoverPending(s) && _w.Holder(s) != null
				orderby IsSupplyCamp(s) ? 1 : 0, s.Hideout descending, _w.Holder(s).Rank descending
				select s).ThenBy((CampSite _) => _w.Roll(1048576)).FirstOrDefault();
			if (campSite == null)
			{
				if (site != null && (flag2 || _w.Holder(site) == null))
				{
					News(ev, "Word is " + The(item) + " have moved their money out of " + From(item, site) + ". Nobody knows where it went.", TreasuryKnown(item) || TreasuryRobbed.ContainsKey(item.Id) || _w.Cleared(site));
					Treasuries.Remove(item.Id);
					TreasuryKnownList.Remove(item.Id);
					TreasuryRobbed.Remove(item.Id);
					TreasuryRobbedSite.Remove(item.Id);
					StashList.Remove(item.Id);
					KeeperGoneList.Remove("tre:" + item.Id);
					changed = true;
					Log(ev, "TREASURY " + item.Name + ": no manned camp to keep their money in - none for now");
					Blip(ev, site.Id);
				}
				continue;
			}
			string text2 = ((site == null) ? "where they keep it" : (flag2 ? "moved - the old camp was lost" : (flag ? "moved after you robbed it" : "moved - nobody held the old camp")));
			if (site != null)
			{
				News(ev, "Word is " + The(item) + " have moved their money out of " + From(item, site) + ".", TreasuryKnown(item) || flag || flag4 || _w.Cleared(site));
			}
			Treasuries[item.Id] = campSite.Id;
			TreasuryKnownList.Remove(item.Id);
			KeeperGoneList.Remove("tre:" + item.Id);
			changed = true;
			if (flag || !TreasuryRobbed.ContainsKey(item.Id))
			{
				TreasuryRobbed.Remove(item.Id);
				TreasuryRobbedSite.Remove(item.Id);
			}
			else
			{
				text2 += " - still empty from your robbery";
			}
			string[] obj = new string[8] { "TREASURY ", item.Name, ": ", campSite.Label, " (", text2, ")", null };
			NemesisRecord nemesisRecord = _w.Holder(campSite);
			obj[7] = ((nemesisRecord != null) ? (", held by " + nemesisRecord.Name) : "");
			Log(ev, string.Concat(obj));
			Blip(ev, site?.Id);
		}
	}

	private bool PlaceStash(Gang g, CampSite site, long now, List<HoldingsEvent> ev, ref bool changed)
	{
		int num = MannedCamps(g);
		if (site != null && IsStash(g))
		{
			NemesisRecord nemesisRecord = _w.Holder(site);
			if (nemesisRecord != null)
			{
				StashList.Remove(g.Id);
				changed = true;
				if (TreasuryKnown(g) && !nemesisRecord.CampKnown)
				{
					Reveal(ev, nemesisRecord);
				}
				Log(ev, "TREASURY " + g.Name + ": their stash at " + site.Label + " is " + nemesisRecord.Name + "'s camp now - the money stays");
				StashBlips(ev);
				Blip(ev, site.Id);
				return true;
			}
			bool flag = TreasuryKnown(g);
			if (num >= 5 && !flag)
			{
				News(ev, "Word is " + The(g) + " have moved their money out of " + From(g, site) + ".", big: false);
				Treasuries.Remove(g.Id);
				StashList.Remove(g.Id);
				changed = true;
				Log(ev, $"TREASURY {g.Name}: back on their feet ({num} camps) - the money leaves the stash at {site.Label} for a camp");
				StashBlips(ev);
				return true;
			}
			long value;
			bool flag2 = TreasuryRobbed.TryGetValue(g.Id, out value);
			bool flag3 = flag2 && (now - value >= 1440 || now < value);
			if (flag3 && TreasuryRobbedSite.TryGetValue(g.Id, out var value2) && value2 != site.Id)
			{
				TreasuryRobbed.Remove(g.Id);
				TreasuryRobbedSite.Remove(g.Id);
				changed = true;
				Log(ev, "TREASURY " + g.Name + ": the stash at " + site.Label + " refilled");
				StashBlips(ev);
				return true;
			}
			string text = _w.Owner(site);
			bool flag4 = text != g.Id || _w.Cleared(site);
			string value3;
			bool flag5 = flag2 && TreasuryRobbedSite.TryGetValue(g.Id, out value3) && value3 == site.Id && (KeeperGone(g, treasury: true) || StashKeeper(g) == null);
			if (!flag4 && !flag3 && !flag5)
			{
				return true;
			}
			if (flag4 && text != null && text != g.Id)
			{
				Gang gang = Gangs.ById(text);
				int num2 = ((!flag2) ? ((int)((double)_w.Purse(g) * 0.4)) : 0);
				if (gang != null && _w.HasPurse(gang) && num2 > 0)
				{
					Taken(ev, g, gang, num2, site.Label, stash: true);
				}
			}
			CampSite campSite = PickStash(g, site);
			News(ev, "Word is " + The(g) + " have moved their money out of " + From(g, site) + ".", flag || flag2 || _w.Cleared(site));
			if (flag3)
			{
				TreasuryRobbed.Remove(g.Id);
				TreasuryRobbedSite.Remove(g.Id);
			}
			TreasuryKnownList.Remove(g.Id);
			KeeperGoneList.Remove("tre:" + g.Id);
			changed = true;
			if (campSite == null)
			{
				Treasuries.Remove(g.Id);
				StashList.Remove(g.Id);
				Log(ev, "TREASURY " + g.Name + ": the stash at " + site.Label + " " + (flag4 ? "is gone" : "was robbed") + " - nowhere out of the way for another; a camp will do");
				StashBlips(ev);
				return true;
			}
			Treasuries[g.Id] = campSite.Id;
			Log(ev, "TREASURY " + g.Name + ": the stash moved from " + site.Label + " to " + campSite.Label + " (" + (flag4 ? "the old one was lost" : "after you robbed it") + ")");
			StashBlips(ev);
			return true;
		}
		if (num >= 4)
		{
			return false;
		}
		if (site != null && (TreasuryRobbedNow(g) || TreasuryKnown(g)))
		{
			return false;
		}
		CampSite campSite2 = PickStash(g, site);
		if (campSite2 == null)
		{
			return false;
		}
		if (site != null)
		{
			News(ev, "Word is " + The(g) + " have moved their money out of " + From(g, site) + ".", big: false);
		}
		Treasuries[g.Id] = campSite2.Id;
		if (!StashList.Contains(g.Id))
		{
			StashList.Add(g.Id);
		}
		TreasuryKnownList.Remove(g.Id);
		changed = true;
		Log(ev, string.Format("TREASURY {0}: only {1} manned camp{2} left - their money's moved out to a stash at {3}{4}", g.Name, num, (num == 1) ? "" : "s", campSite2.Label, (site != null) ? (" (was " + site.Label + ")") : ""));
		Blip(ev, site?.Id);
		StashBlips(ev);
		return true;
	}

	private void PlaceSupplies(List<HoldingsEvent> ev, ref bool changed)
	{
		foreach (Gang item in _w.Outfits.Where(HoldsCamps))
		{
			if (_w.Destroyed(item))
			{
				if (Forget(item))
				{
					changed = true;
				}
				continue;
			}
			CampSite site = SupplyCampOf(item);
			if (site != null && _w.LiveSupplies(site.Id))
			{
				continue;
			}
			if (site != null && _w.InTown(site))
			{
				News(ev, "Word is " + The(item) + " have moved their supplies out of " + From(item, site) + ".", SupplyKnownAt.TryGetValue(item.Id, out var value) && value == site.Id);
				SupplyCamps.Remove(item.Id);
				SupplyCaches.Remove(item.Id);
				SupplyKnownAt.Remove(item.Id);
				changed = true;
				Log(ev, "SUPPLY " + item.Name + ": " + site.Label + " is in town - their supplies move out of it");
				Blip(ev, site.Id);
				site = null;
			}
			string value2;
			string hit = ((SupplyRaidedRecently(item) && SupplyCampHitSite.TryGetValue(item.Id, out value2)) ? value2 : null);
			long value3;
			bool flag = SupplyCampHit.TryGetValue(item.Id, out value3) && _w.Now - value3 >= 1440;
			bool flag2 = site != null && (_w.Holder(site) == null || _w.Cleared(site) || KeeperGone(item, treasury: false) || SupplyCaches.ContainsKey(item.Id));
			if (site != null && hit == site.Id && (flag || flag2))
			{
				if (SupplyCacheKnown(item))
				{
					_ = 1;
				}
				else
					_ = _w.Holder(site)?.CampKnown ?? false;
				SupplyCamps.Remove(item.Id);
				SupplyCaches.Remove(item.Id);
				SupplyKnownAt.Remove(item.Id);
				KeeperGoneList.Remove("sup:" + item.Id);
				changed = true;
				Log(ev, "SUPPLY " + item.Name + ": you wrecked their supplies at " + site.Label + " - what's left goes to another camp" + (flag ? "" : " (now: its keeper's gone / it was a cache / wiped out)"));
				News(ev, "Word is " + The(item) + " have moved what's left of their supplies out of " + From(item, site) + ".", big: true);
				Blip(ev, site.Id);
				CacheBlips(ev);
				site = null;
			}
			string wiped = null;
			if (site != null && _w.Cleared(site) && hit != site.Id)
			{
				string value4;
				bool flag3 = SupplyCacheKnown(item) || (SupplyKnownAt.TryGetValue(item.Id, out value4) && value4 == site.Id);
				SupplyCamps.Remove(item.Id);
				SupplyCaches.Remove(item.Id);
				SupplyKnownAt.Remove(item.Id);
				changed = true;
				Log(ev, "SUPPLY " + item.Name + ": " + (_w.TakeoverPending(site) ? "another outfit is taking" : "you wiped out") + " " + site.Label + " - their supplies go to another camp");
				News(ev, "Word is " + The(item) + " have moved their supplies out of " + From(item, site) + ".", flag3 || !_w.TakeoverPending(site));
				Blip(ev, site.Id);
				CacheBlips(ev);
				wiped = site.Id;
				site = null;
			}
			CampSite treasury = TreasuryOf(item);
			bool noCamp = !_w.SitesHeldBy(item.Id).Any((CampSite s) => (treasury == null || s.Id != treasury.Id) && !_w.TakeoverPending(s) && _w.Holder(s) != null && !_w.Wet(s));
			if (PlaceSupplyCache(item, site, noCamp, ev, ref changed) || (site != null && _w.Owner(site) == item.Id && _w.Holder(site) != null && !IsTreasury(site)))
			{
				continue;
			}
			CampSite campSite = (from s in _w.SitesHeldBy(item.Id)
				where (site == null || s.Id != site.Id) && s.Id != hit && s.Id != wiped && (treasury == null || s.Id != treasury.Id) && !_w.TakeoverPending(s) && _w.Holder(s) != null && !_w.Wet(s) && !_w.InTown(s)
				orderby s.Hideout
				select s).ThenBy((CampSite _) => _w.Roll(1048576)).FirstOrDefault();
			if (campSite == null)
			{
				if (site != null)
				{
					News(ev, "Word is " + The(item) + " have pulled their supplies out of " + From(item, site) + ".", SupplyKnownAt.TryGetValue(item.Id, out var value5) && value5 == site.Id);
					SupplyCamps.Remove(item.Id);
					changed = true;
					Log(ev, "SUPPLY " + item.Name + ": no manned camp for their supplies - none for now");
					Blip(ev, site.Id);
				}
				continue;
			}
			if (site != null)
			{
				News(ev, "Word is " + The(item) + " have moved their supplies out of " + From(item, site) + ".", SupplyKnownAt.TryGetValue(item.Id, out var value6) && value6 == site.Id);
			}
			SupplyCamps[item.Id] = campSite.Id;
			changed = true;
			if (SupplyKnownAt.TryGetValue(item.Id, out var value7) && value7 != campSite.Id)
			{
				SupplyKnownAt.Remove(item.Id);
			}
			string[] obj = new string[6]
			{
				"SUPPLY ",
				item.Name,
				": their supplies at ",
				campSite.Label,
				(site != null) ? " (moved)" : "",
				null
			};
			NemesisRecord nemesisRecord = _w.Holder(campSite);
			obj[5] = ((nemesisRecord != null) ? (", held by " + nemesisRecord.Name) : "");
			Log(ev, string.Concat(obj));
			Blip(ev, site?.Id);
			Blip(ev, campSite.Id);
		}
	}

	private bool PlaceSupplyCache(Gang g, CampSite site, bool noCamp, List<HoldingsEvent> ev, ref bool changed)
	{
		if (site == null)
		{
			return false;
		}
		if (!SupplyCaches.ContainsKey(g.Id))
		{
			if (_w.Holder(site) != null || (!noCamp && !SupplyCacheKnown(g)) || _w.Owner(site) != g.Id || _w.TakeoverPending(site))
			{
				return false;
			}
			SupplyCaches[g.Id] = _w.Now;
			changed = true;
			Log(ev, string.Format("SUPPLY {0}: no manned camp left - their supplies stay at {1}, a cache (nobody keeps it for {2} hours){3}", g.Name, site.Label, 6L, SupplyCacheKnown(g) ? " - you know it" : ""));
			Blip(ev, site.Id);
			CacheBlips(ev);
			return true;
		}
		NemesisRecord nemesisRecord = _w.Holder(site);
		string value;
		bool flag = SupplyRaidedRecently(g) && SupplyCampHitSite.TryGetValue(g.Id, out value) && value == site.Id;
		if (nemesisRecord != null && !flag)
		{
			SupplyCaches.Remove(g.Id);
			changed = true;
			if (SupplyCacheKnown(g) && !nemesisRecord.CampKnown)
			{
				Reveal(ev, nemesisRecord);
			}
			Log(ev, "SUPPLY " + g.Name + ": the cache at " + site.Label + " is " + nemesisRecord.Name + "'s camp now - a supply camp again");
			CacheBlips(ev);
			Blip(ev, site.Id);
			return true;
		}
		if (_w.Owner(site) != g.Id || _w.TakeoverPending(site))
		{
			SupplyCaches.Remove(g.Id);
			SupplyCamps.Remove(g.Id);
			changed = true;
			Log(ev, "SUPPLY " + g.Name + ": the cache at " + site.Label + " is lost with the site");
			CacheBlips(ev);
			return true;
		}
		if (_w.LiveSupplies(site.Id))
		{
			return true;
		}
		if (!noCamp && !SupplyCacheKnown(g))
		{
			SupplyCaches.Remove(g.Id);
			SupplyCamps.Remove(g.Id);
			changed = true;
			Log(ev, "SUPPLY " + g.Name + ": a manned camp again - the supplies leave the cache at " + site.Label);
			CacheBlips(ev);
		}
		return true;
	}
}
