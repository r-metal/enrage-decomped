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
	private sealed class Band
	{
		public string What;

		public Gang Gang;

		public readonly List<Ped> Men = new List<Ped>();

		public readonly HashSet<int> CameBefore = new HashSet<int>();

		public int Since;

		public int Until;

		public bool Turned;

		public readonly Dictionary<int, uint> Models = new Dictionary<int, uint>();

		public bool Has(Ped m)
		{
			if (m != null && m.Exists() && Models.TryGetValue(m.Handle, out var value))
			{
				return ENTITY.GET_ENTITY_MODEL(m.Handle) == value;
			}
			return false;
		}
	}

	private const int FortTellPercent = 60;

	private const int BandWatchMs = 180000;

	private const float BandFireNearM = 15f;

	private readonly List<Band> _bands = new List<Band>();

	private readonly Dictionary<int, int> _bandAimAt = new Dictionary<int, int>();

	private int _nextBandCheck;

	private void RegisterBand(IEnumerable<Ped> men, Gang gang, string what)
	{
		Vector3 position = Game.Player.Character.Position;
		Band band = new Band
		{
			What = what,
			Gang = gang,
			Since = Game.GameTime,
			Until = Game.GameTime + 180000
		};
		foreach (Ped man in men)
		{
			if (man == null || !man.Exists() || man.IsDead || !man.IsHuman || man.Position.DistanceTo(position) > 250f)
			{
				continue;
			}
			int h = man.Handle;
			if (!band.Men.Any((Ped x) => x.Handle == h) && !_bands.Any((Band x) => x.Men.Any((Ped m) => m.Handle == h)))
			{
				band.Men.Add(man);
				band.Models[h] = ENTITY.GET_ENTITY_MODEL(h);
				if (_cameAtYou.Contains(h) || _selfDefence.Contains(h))
				{
					band.CameBefore.Add(h);
				}
			}
		}
		if (band.Men.Count >= 2)
		{
			_bands.Add(band);
			GrudgeLog($"BAND {what}: {band.Men.Count} men let go near you - watched {180} s (they turn on you if you go for one of them)");
		}
	}

	private void BandFromEncounter()
	{
		NemesisRecord activeRecord = ActiveRecord;
		List<Ped> men = (from p in new Ped[1] { _activeNemesisPed }.Concat(_followers)
			where p != null && p.Exists() && !p.IsDead
			select p).ToList();
		RegisterBand(men, Gangs.ById(activeRecord?.Gang), (activeRecord != null) ? (activeRecord.Name + "'s men") : "the encounter's men");
	}

	private Band BandOf(int h)
	{
		return _bands.FirstOrDefault((Band b) => b.Men.Any((Ped m) => m.Handle == h && b.Has(m)));
	}

	private unsafe void UpdateBands(Ped player)
	{
		if (_bands.Count == 0 || Game.GameTime < _nextBandCheck)
		{
			return;
		}
		_nextBandCheck = Game.GameTime + 300;
		int now = Game.GameTime;
		int handle = Game.Player.Handle;
		int num = 0;
		Vector3 at = player.Position;
		if (PLAYER.GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(handle, &num) && num != 0)
		{
			_bandAimAt[num] = now;
		}
		num = 0;
		if (PLAYER.GET_PLAYER_TARGET_ENTITY(handle, &num) && num != 0)
		{
			_bandAimAt[num] = now;
		}
		bool firedInAnger = FiredInAnger;
		foreach (Band b in _bands.ToList())
		{
			List<Ped> list = b.Men.Where((Ped p) => b.Has(p) && !p.IsDead).ToList();
			if (list.Count == 0 || (!b.Turned && now > b.Until) || list.All((Ped p) => p.Position.DistanceTo(at) > 250f))
			{
				_bands.Remove(b);
				if (b.Turned)
				{
					foreach (Ped item in list)
					{
						item.BlockPermanentEvents = StaysBlocked(item);
					}
				}
				GrudgeLog("BAND " + b.What + ": " + ((list.Count == 0) ? "all down or gone" : ((now > b.Until && !b.Turned) ? "time up" : "250 m off")) + " - left to the game");
			}
			else
			{
				if (b.Turned)
				{
					continue;
				}
				string text = null;
				foreach (Ped man in b.Men)
				{
					if (!b.Has(man))
					{
						continue;
					}
					int handle2 = man.Handle;
					if (_hitByYouAt.TryGetValue(handle2, out var value) && value > b.Since)
					{
						text = "you " + (man.IsDead ? "killed" : "hurt") + " " + WhoIs(handle2);
						break;
					}
					if (!man.IsDead)
					{
						if ((_cameAtYou.Contains(handle2) || _selfDefence.Contains(handle2)) && !b.CameBefore.Contains(handle2))
						{
							text = WhoIs(handle2) + " went for you";
							break;
						}
						if (firedInAnger && now - b.Since > 2000 && ((_bandAimAt.TryGetValue(handle2, out var value2) && now - value2 < 1500) || man.Position.DistanceTo(at) < 15f))
						{
							text = "you shot at them";
							break;
						}
					}
				}
				if (text != null)
				{
					TurnBand(b, player, text);
				}
			}
		}
		if (_bandAimAt.Count <= 64)
		{
			return;
		}
		foreach (int item2 in (from kv in _bandAimAt
			where now - kv.Value > 5000
			select kv.Key).ToList())
		{
			_bandAimAt.Remove(item2);
		}
	}

	private void TurnBand(Band b, Ped player, string why)
	{
		b.Turned = true;
		Vector3 position = player.Position;
		int num = 0;
		foreach (Ped item in b.Men.Where((Ped p) => b.Has(p) && !p.IsDead))
		{
			int handle = item.Handle;
			if (!(item.Position.DistanceTo(position) > 120f) && !NerveOut(handle) && !item.IsHogtied && (!(_interro?.Ped != null) || _interro.Ped.Handle != handle))
			{
				item.BlockPermanentEvents = true;
				Crumbs.Native("band turns on you");
				TASK.TASK_COMBAT_PED(handle, player.Handle, 0, 16);
				num++;
			}
		}
		GrudgeLog($"BAND {b.What} turn on you: {why} ({num} men)");
		if (num > 0)
		{
			Feed("FIGHT " + b.What + " turn on you (" + why + ")", Theme.Blood);
		}
	}

	private void Grace(int seconds, string why)
	{
		int num = Game.GameTime + seconds * 1000;
		if (num > _graceUntil)
		{
			_graceUntil = num;
			_graceWhy = why;
			GrudgeLog($"CALM grace {seconds}s - {why}");
		}
	}

	private void MapIconsForMission()
	{
		Guard("map camps", RefreshCampBlips);
		Guard("map stashes", RefreshStashBlips);
		Guard("map caches", RefreshCacheBlips);
		Guard("map plans", RefreshPlanBlips);
		Guard("map wars", RefreshWarMarkers);
		Guard("map fort", RefreshFortBlip);
		_nextTownRepBlips = 0;
		if (_mapHidden)
		{
			Guard("map town rep", ClearTownRepBlips);
			Guard("map aftermath", ClearAftermathBlips);
			Guard("map supply wagon", DropSupplyWagonBlip);
		}
	}
}
