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
	private const int SelfCheckConfirmMs = 30000;

	private int _selfCheckAt = -1;

	private int _selfCheckConfirmAt = -1;

	private Dictionary<string, (string rule, string detail)> _selfCheckSuspects;

	private bool _selfCheckAsked;

	private static readonly Dictionary<string, (Vector3? Doctor, Vector3? Saloon)> BuiltInTownSpots = new Dictionary<string, (Vector3?, Vector3?)>
	{
		{
			"Valentine",
			(new Vector3(-287.55f, 812.09f, 119.25f), new Vector3(-313.51f, 805.81f, 118.98f))
		},
		{
			"Saint Denis",
			(new Vector3(2732.4f, -1233.83f, 50.7f), new Vector3(2792.37f, -1168.41f, 47.93f))
		},
		{
			"Rhodes",
			(new Vector3(1372.46f, -1305.12f, 77.75f), new Vector3(1340.17f, -1374.82f, 80.48f))
		},
		{
			"Blackwater",
			(new Vector3(-785.69f, -1306.2f, 43.81f), new Vector3(-817.59f, -1319.09f, 43.68f))
		},
		{
			"Armadillo",
			(new Vector3(-3738.99f, -2636.93f, -12.4f), new Vector3(-3699.81f, -2594.41f, -13.32f))
		},
		{
			"Tumbleweed",
			(new Vector3(-5528.01f, -2952.85f, -0.5f), new Vector3(-5518.48f, -2906.51f, -1.75f))
		},
		{
			"Strawberry",
			(new Vector3(-1808.89f, -434f, 158.72f), null)
		},
		{
			"Annesburg",
			(new Vector3(2922.97f, 1356.31f, 44.71f), null)
		},
		{
			"Van Horn",
			(null, new Vector3(2947.84f, 528.06f, 45.34f))
		}
	};

	private static readonly Dictionary<string, (Vector3? Doctor, Vector3? Saloon)> TownSpots = new Dictionary<string, (Vector3?, Vector3?)>(BuiltInTownSpots);

	private void UpdateSelfCheck(Ped player)
	{
		if (_selfCheckAt < 0)
		{
			_selfCheckAt = _loadedAt + 60000;
		}
		if (player.IsDead || WorldBusy(player) || !WorldClockSettled)
		{
			return;
		}
		if (_selfCheckConfirmAt >= 0)
		{
			if (Game.GameTime >= _selfCheckConfirmAt)
			{
				_selfCheckConfirmAt = -1;
				int checks;
				List<KeyValuePair<string, (string, string)>> faults = (from kv in RunSelfChecks(out checks)
					where _selfCheckSuspects.ContainsKey(kv.Key)
					select kv).ToList();
				ReportSelfCheck(faults, checks);
				_selfCheckSuspects = null;
			}
		}
		else if (Game.GameTime >= _selfCheckAt)
		{
			_selfCheckAt = Game.GameTime + 600000;
			_selfCheckSuspects = RunSelfChecks(out var checks2);
			if (_selfCheckSuspects.Count == 0)
			{
				ReportSelfCheck(new List<KeyValuePair<string, (string, string)>>(), checks2);
			}
			else
			{
				_selfCheckConfirmAt = Game.GameTime + 30000;
			}
		}
	}

	internal void DebugSelfCheck(Ped player)
	{
		_selfCheckAsked = true;
		_selfCheckSuspects = RunSelfChecks(out var checks);
		if (_selfCheckSuspects.Count == 0)
		{
			ReportSelfCheck(new List<KeyValuePair<string, (string, string)>>(), checks);
			return;
		}
		_selfCheckConfirmAt = Game.GameTime + 30000;
		SpawnerSays($"self-check: {_selfCheckSuspects.Count} thing(s) look wrong - checking again in 30 s.");
	}

	private void ReportSelfCheck(List<KeyValuePair<string, (string rule, string detail)>> faults, int checks)
	{
		if (faults.Count == 0)
		{
			GrudgeLog($"SELFCHECK ok - {checks} checks");
		}
		foreach (IGrouping<string, KeyValuePair<string, (string, string)>> item in from f in faults
			group f by f.Value.rule)
		{
			foreach (KeyValuePair<string, (string, string)> item2 in item.Take(3))
			{
				GrudgeLog("SELFCHECK " + item.Key + ": " + item2.Value.Item2);
			}
			if (item.Count() > 3)
			{
				GrudgeLog($"SELFCHECK {item.Key}: ...and {item.Count() - 3} more");
			}
		}
		if (_selfCheckAsked)
		{
			_selfCheckAsked = false;
			SpawnerSays((faults.Count == 0) ? $"self-check: all {checks} checks fine." : $"self-check: {faults.Count} fault(s) - see grudges.log (SELFCHECK).");
		}
	}

	private Dictionary<string, (string rule, string detail)> RunSelfChecks(out int checks)
	{
		Dictionary<string, (string, string)> bad = new Dictionary<string, (string, string)>();
		int n = 0;
		long now = GameMinutesNow();
		bool mapOn = MapSettled() && !_mapHidden;
		if (Config.CampsEnabled)
		{
			Check("holdings", delegate
			{
				Hold.Check(delegate(string key, string what)
				{
					Fault("holdings", key, what);
				});
			});
		}
		Check("known", delegate
		{
			foreach (string id in KnownSites)
			{
				CampSite campSite = Camps.ById(id);
				NemesisRecord nemesisRecord = HolderOf(campSite);
				if (nemesisRecord != null && !Roster.Any((NemesisRecord x) => x.CampId == id && x.CampKnown))
				{
					Fault("known", id, "you know " + campSite.Label + ", but its holder " + nemesisRecord.Name + " isn't marked as known");
				}
			}
		});
		Check("markers", delegate
		{
			if (!mapOn || !Config.CampsEnabled)
			{
				return;
			}
			foreach (IGrouping<string, NemesisRecord> item in from x in Roster
				where x.CampId != null && x.CampKnown && Mans(x)
				group x by x.CampId)
			{
				CampSite campSite = Camps.ById(item.Key);
				if (campSite != null)
				{
					string sig;
					if (!_campBlips.ContainsKey(item.Key))
					{
						Fault("markers", item.Key, campSite.Label + " is known (" + item.First().Name + ") but has no marker");
					}
					else if (_campBlipSigs.TryGetValue(item.Key, out sig) && !item.Any((NemesisRecord x) => sig.Contains(x.Name)))
					{
						Fault("markers", "label:" + item.Key, campSite.Label + "'s marker names someone else: \"" + sig.Split('|').Last() + "\" (held by " + item.First().Name + ")");
					}
				}
			}
			foreach (string key in SupplyCaches.Keys)
			{
				Gang gang = Gangs.ById(key);
				CampSite campSite2 = SupplyCampOf(gang);
				if (campSite2 != null && SupplyCacheKnown(gang) && !_cacheBlips.ContainsKey(campSite2.Id))
				{
					Fault("markers", "cache:" + key, "the " + gang.Name + " cache at " + campSite2.Label + " is known but has no marker");
				}
			}
			foreach (Gang item2 in Gangs.All.Where(HoldsCamps))
			{
				CampSite campSite3 = TreasuryOf(item2);
				if (campSite3 != null && TreasuryKnown(item2))
				{
					bool num;
					if (!IsStash(item2))
					{
						if (_campBlips.ContainsKey(campSite3.Id))
						{
							continue;
						}
						num = HolderOf(campSite3) == null;
					}
					else
					{
						num = _stashBlips.ContainsKey(campSite3.Id);
					}
					if (!num)
					{
						Fault("markers", "treasury:" + item2.Id, "the " + item2.Name + " treasury at " + campSite3.Label + " is known but has no marker");
					}
				}
			}
		});
		Check("plans", delegate
		{
			foreach (Plan p in Plans.Where((Plan x) => !x.Done))
			{
				if (now > p.DueAt + p.WindowMin + 120 && !p.Live)
				{
					Fault("plans", p.Id, p.Kind + " \"" + p.Title + "\" was due " + WhenText(p.DueAt) + " and never resolved");
				}
				if (p.Kind == "funeral" && !p.Live && p.LeaderId != null)
				{
					NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord r) => r.Id == p.LeaderId);
					if (nemesisRecord != null && !nemesisRecord.IsAlive)
					{
						Fault("plans", "mourner:" + p.Id, "the funeral \"" + p.Title + "\" is led by " + nemesisRecord.Name + ", who's dead");
					}
				}
			}
		});
		Check("roster", delegate
		{
			foreach (IGrouping<string, NemesisRecord> item3 in from r in Roster
				where r.IsAlive && !r.PresumedDead && !string.IsNullOrEmpty(r.Name)
				group r by r.Name into x
				where x.Count() > 1
				select x)
			{
				Fault("roster", "name:" + item3.Key, $"{item3.Count()} living men are called {item3.Key}");
			}
			Gang[] all = Gangs.All;
			foreach (Gang gang in all)
			{
				string sid = SuccessorId(gang);
				if (sid != null)
				{
					NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord r) => r.Id == sid);
					if (nemesisRecord != null && !nemesisRecord.IsAlive && !LeaderVacant(gang))
					{
						Fault("roster", "seat:" + gang.Id, "the " + gang.Name + " leader's seat is held by " + nemesisRecord.Name + ", who's dead");
					}
				}
			}
		});
		Check("entities", delegate
		{
			foreach (string item4 in Kit.Keys.Where((string k) => !_liveCamps.ContainsKey(k) && !IsTestSite(k) && !_unguardedCaches.Contains(k)))
			{
				Fault("entities", "kit:" + item4, "camp props at " + (Camps.ById(item4)?.Label ?? item4) + " with no camp");
			}
			foreach (string item5 in _liveSupplies.Keys.Where((string k) => !k.StartsWith("town:") && !_liveCamps.ContainsKey(k) && !_unguardedCaches.Contains(k)))
			{
				Fault("entities", "stores:" + item5, "camp stores at " + (Camps.ById(item5)?.Label ?? item5) + " with no camp");
			}
		});
		Check("eras", delegate
		{
			foreach (LiveCamp value in _liveCamps.Values)
			{
				Gang gang = Gangs.ById(value.Boss?.Gang);
				if (gang != null && !Gangs.InEra(gang) && !IsTestSite(value.Site.Id))
				{
					Fault("eras", value.Site.Id, "a live camp of the " + gang.Name + " (not of this era) at " + value.Site.Label);
				}
			}
		});
		checks = n;
		return bad;
		void Check(string rule, Action body)
		{
			n++;
			try
			{
				body();
			}
			catch (Exception ex)
			{
				Fault(rule, "crash", "the check itself failed: " + ex.Message);
			}
		}
		void Fault(string rule, string key, string detail)
		{
			bad[rule + "|" + key] = (rule, detail);
		}
	}
}
