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
	private static readonly HashSet<string> BuiltInOpenDoctors = new HashSet<string> { "Valentine", "Saint Denis" };

	private static readonly HashSet<string> OpenDoctors = new HashSet<string>(BuiltInOpenDoctors);

	private const float SightingDespawnDistance = 150f;

	private string _currentTown;

	private int _nextTownCheck;

	private Ped _sightingPed;

	private NemesisRecord _sightingRec;

	private bool _sightingSpoke;

	private Blip _sightingBlip;

	private int _sightingCrippleAt;

	private const float SightingSpawnDistance = 45f;

	private NemesisRecord _pendingRec;

	private Vector3 _pendingSpot;

	private int _pendingBlip;

	private string _pendingTown;

	private List<(string town, Vector3 at)> _hotels;

	private int _pendingBlipAt;

	private string _pendingNote;

	private bool _pendingWaitLogged;

	private static readonly string[] FellNotKilled = new string[1] { "52a10487fba9" };

	internal Sight SightFor(Ped player, float walkRange)
	{
		bool playerFiredRecently = GunMelee.PlayerFiredRecently;
		bool isOnMount = player.IsOnMount;
		bool flag = !isOnMount && !playerFiredRecently && PED.GET_PED_STEALTH_MOVEMENT(player.Handle);
		bool flag2 = !isOnMount && (TASK.IS_PED_RUNNING(player.Handle) || TASK.IS_PED_SPRINTING(player.Handle));
		float num = walkRange;
		string text = "walking";
		if (flag)
		{
			num = Math.Min(walkRange * 0.45f, 16f);
			text = "crouched";
		}
		else if (isOnMount || playerFiredRecently)
		{
			num = walkRange * 1.5f;
			text = (isOnMount ? "mounted" : "fired");
		}
		else if (flag2)
		{
			num = walkRange * 1.33f;
			text = "running";
		}
		int cLOCK_HOURS = CLOCK.GET_CLOCK_HOURS();
		if (cLOCK_HOURS >= 21 || cLOCK_HOURS < 5)
		{
			num *= 0.6f;
			text += ", night";
		}
		float num2 = WeatherSightMult();
		if (num2 < 1f)
		{
			num *= num2;
			text = text + ", " + WeatherNow;
		}
		return new Sight
		{
			Range = num,
			Close = (flag ? 3.5f : 8f),
			Crouched = flag,
			Stance = text
		};
	}

	internal unsafe static bool Dry(Vector3 p)
	{
		float num = 0f;
		if (WATER.GET_WATER_HEIGHT(p.X, p.Y, p.Z + 2f, &num))
		{
			return num < p.Z + 0.3f;
		}
		return true;
	}

	internal Vector3 GoodSpot(Ped player, Func<Vector3> pick, int tries = 10, bool mustHide = true)
	{
		Vector3 vector = Vector3.Zero;
		for (int i = 0; i < tries; i++)
		{
			Vector3 vector2 = pick();
			if (Dry(vector2) && (!(vector2.DistanceTo(player.Position) > 100f) || LoadedGround(vector2)))
			{
				if (HiddenFromView(player, vector2))
				{
					return vector2;
				}
				if (vector == Vector3.Zero)
				{
					vector = vector2;
				}
			}
		}
		if (!mustHide)
		{
			return vector;
		}
		return Vector3.Zero;
	}

	internal bool Sees(Ped watcher, Ped player, Sight s, out float d)
	{
		d = watcher.Position.DistanceTo(player.Position);
		if (d < s.Close)
		{
			return true;
		}
		if (d >= s.Range)
		{
			return false;
		}
		if (!ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY_IN_FRONT(watcher.Handle, player.Handle, 17))
		{
			return false;
		}
		if (s.Crouched)
		{
			return HeadSeesHead(watcher, player);
		}
		return true;
	}

	internal static IEnumerable<string> TownNames => TownSpots.Keys;

	private void UpdateTownSightings(Ped player)
	{
		if (_sightingCrippleAt > 0 && Game.GameTime >= _sightingCrippleAt && _sightingPed != null && _sightingPed.Exists() && !_sightingPed.IsDead && ENTITY.HAS_COLLISION_LOADED_AROUND_ENTITY(_sightingPed.Handle))
		{
			_sightingCrippleAt = 0;
			_bulletImpact.ApplyCondition(_sightingPed, CrippleLevel.SpineBroken);
		}
		if (Game.GameTime >= _nextTownCheck)
		{
			_nextTownCheck = Game.GameTime + 1500;
			string text = TownAt(player.Position);
			if (text != _currentTown)
			{
				_currentTown = text;
				if (text != null && !player.IsDead)
				{
					OnEnterTown(text);
					OnEnterTownPresence(text, player);
				}
			}
		}
		UpdatePendingSighting(player);
		UpdateSightingPed(player);
		UpdateTownPresence(player);
	}

	private string TownAt(Vector3 pos)
	{
		uint key = ZONE._GET_MAP_ZONE_AT_COORDS(pos.X, pos.Y, pos.Z, 1);
		if (!ZoneNames.TryGetValue(key, out var value) || !TownSpots.ContainsKey(value))
		{
			return null;
		}
		return value;
	}

	private void OnEnterTown(string town)
	{
		SightingFor(town, force: false);
	}

	private string HotelsPath => Path.Combine(_saveDir, "hotels.txt");

	private List<(string town, Vector3 at)> Hotels
	{
		get
		{
			if (_hotels != null)
			{
				return _hotels;
			}
			_hotels = new List<(string, Vector3)>();
			try
			{
				if (File.Exists(HotelsPath))
				{
					string[] array = File.ReadAllLines(HotelsPath);
					foreach (string text in array)
					{
						string[] q = text.Split('|');
						if (q.Length >= 4)
						{
							_hotels.Add((q[0], new Vector3(F(1), F(2), F(3))));
						}
						float F(int num)
						{
							if (!float.TryParse(q[num], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
							{
								return 0f;
							}
							return result;
						}
					}
				}
			}
			catch (Exception ex)
			{
				GrudgeLog("SIGHTING hotels.txt unreadable: " + ex.Message);
			}
			return _hotels;
		}
	}

	private IEnumerable<Vector3> HotelsIn(string town)
	{
		return from h in Hotels
			where h.town == town
			select h.at;
	}

	internal void DebugMarkHotel(Ped player)
	{
		string town = _currentTown ?? TownAt(player.Position);
		if (town == null)
		{
			SpawnerSays("stand by a bed in a town's hotel first.");
			return;
		}
		Vector3 at = player.Position;
		if (Hotels.Any(((string town, Vector3 at) h) => h.town == town && h.at.DistanceTo(at) < 2f))
		{
			SpawnerSays("that bed in " + town + " is already marked.");
			return;
		}
		Hotels.Add((town, at));
		try
		{
			SafeFile.Write(HotelsPath, string.Join("\r\n", Hotels.Select(((string town, Vector3 at) h) => string.Format(CultureInfo.InvariantCulture, "{0}|{1:0.0}|{2:0.0}|{3:0.0}", h.town, h.at.X, h.at.Y, h.at.Z))));
		}
		catch (Exception ex)
		{
			GrudgeLog("SIGHTING hotels.txt not saved: " + ex.Message);
		}
		SpawnerSays($"hotel bed marked in {town} ({HotelsIn(town).Count()} there) - laid-up men can be found here now.");
	}

	private bool SightingFor(string town, bool force)
	{
		if (_sightingPed != null || _pendingRec != null)
		{
			return false;
		}
		List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.IsAlive && n.Paralyzed && !n.PresumedDead).ToList();
		if (list.Count == 0 || (!force && _rng.Next(100) >= Config.ParalyzedSightingChance))
		{
			return false;
		}
		if (!TownSpots.ContainsKey(town))
		{
			return false;
		}
		NemesisRecord nemesisRecord = list[_rng.Next(list.Count)];
		Vector3? item = TownSpots[town].Doctor;
		List<Vector3> list2 = HotelsIn(town).ToList();
		List<(string, Vector3, int)> list3 = new List<(string, Vector3, int)>();
		if (item.HasValue && OpenDoctors.Contains(town))
		{
			list3.Add(("at the doctor's", item.Value, 65));
		}
		if (list2.Count > 0)
		{
			list3.Add(("in a room at the hotel", list2[_rng.Next(list2.Count)], 35));
		}
		if (list3.Count == 0)
		{
			return false;
		}
		int num = _rng.Next(list3.Sum<(string, Vector3, int)>(((string where, Vector3 at, int w) o) => o.w));
		(string, Vector3, int) tuple = list3.Last();
		foreach (var item2 in list3)
		{
			if ((num -= item2.Item3) < 0)
			{
				tuple = item2;
				break;
			}
		}
		_pendingSpot = tuple.Item2;
		_pendingRec = nemesisRecord;
		_pendingTown = town;
		_pendingBlipAt = Game.GameTime + 6000;
		_pendingNote = "Word in " + town + ": a crippled man is laid up " + tuple.Item1 + ". Marked on your map.";
		GrudgeLog("SIGHTING " + nemesisRecord.Name + " (crippled) - laid up " + tuple.Item1);
		return true;
	}

	private void CampSighting(CampSite site, Gang g)
	{
		if (site != null && g != null && !(_sightingPed != null) && _pendingRec == null)
		{
			List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.IsAlive && n.Paralyzed && !n.PresumedDead && n.Gang == g.Id).ToList();
			if (list.Count != 0 && _rng.Next(100) < Config.ParalyzedSightingChance)
			{
				NemesisRecord nemesisRecord = list[_rng.Next(list.Count)];
				_pendingSpot = SitePos(site) + new Vector3(3f, 2f, 0f);
				_pendingRec = nemesisRecord;
				_pendingTown = null;
				_pendingBlipAt = Game.GameTime + 6000;
				_pendingNote = "A crippled man is laid up at " + site.Label + ". Marked on your map.";
				GrudgeLog("SIGHTING " + nemesisRecord.Name + " (crippled) - laid up at " + site.Label + " (the camp spawned near you)");
			}
		}
	}

	private void UpdatePendingSighting(Ped player)
	{
		if (_pendingRec == null)
		{
			return;
		}
		float num = player.Position.DistanceTo(_pendingSpot);
		if (Roster.Contains(_pendingRec) && _pendingRec.IsAlive)
		{
			bool num2;
			if (_pendingTown == null)
			{
				num2 = num > 2500f;
			}
			else
			{
				if (!(_currentTown != _pendingTown))
				{
					goto IL_00bb;
				}
				num2 = num > 400f;
			}
			if (!num2)
			{
				goto IL_00bb;
			}
		}
		if (_pendingBlip != 0 && Roster.Contains(_pendingRec) && !_pendingRec.IsAlive)
		{
			ReplyNotice(_pendingRec.Name + " won't be found laid up any more - he's dead.");
		}
		ClearPendingSighting();
		return;
		IL_00bb:
		if (_pendingBlip == 0 && _pendingBlipAt > 0 && Game.GameTime >= _pendingBlipAt && MapSettled() && !WorldBusy(player))
		{
			_pendingBlipAt = 0;
			Crumbs.Native("Sightings blip add");
			_pendingBlip = MAP.BLIP_ADD_FOR_COORDS(2545348716u, _pendingSpot.X, _pendingSpot.Y, _pendingSpot.Z);
			Crumbs.Native("Sightings blip name");
			if (_pendingBlip != 0)
			{
				Function.Call(11290983183885202434uL, _pendingBlip, new InputArgument(UiDraw.ForeverVarString(_pendingRec.FullName + " (crippled)")));
			}
			if (_pendingNote != null)
			{
				GrudgeNotice(_pendingNote);
			}
			_pendingNote = null;
		}
		if (num > 45f || NoSpawnReason(player, place: true) != null)
		{
			return;
		}
		STREAMING.REQUEST_COLLISION_AT_COORD(_pendingSpot.X, _pendingSpot.Y, _pendingSpot.Z);
		int num3 = INTERIOR.GET_INTERIOR_AT_COORDS(_pendingSpot.X, _pendingSpot.Y, _pendingSpot.Z);
		if (num3 != 0 && !INTERIOR.IS_INTERIOR_READY(num3))
		{
			if (!_pendingWaitLogged)
			{
				_pendingWaitLogged = true;
				GrudgeLog($"SIGHTING {_pendingRec.Name}: waiting for the building at {_pendingSpot.X:0},{_pendingSpot.Y:0} to load");
			}
		}
		else
		{
			_pendingWaitLogged = false;
			NemesisRecord pendingRec = _pendingRec;
			Vector3 pendingSpot = _pendingSpot;
			ClearPendingSighting();
			SpawnSighting(pendingRec, pendingSpot);
		}
	}

	private unsafe void ClearPendingSighting()
	{
		Crumbs.Native("Sightings blip remove");
		if (_pendingBlip != 0)
		{
			int pendingBlip = _pendingBlip;
			MAP.REMOVE_BLIP(&pendingBlip);
		}
		_pendingBlip = 0;
		_pendingBlipAt = 0;
		_pendingNote = null;
		_pendingRec = null;
		_pendingTown = null;
	}

	private void SpawnSighting(NemesisRecord rec, Vector3 spot)
	{
		Vector3 vector = World.GetSafeCoordForPed(spot, onGround: true, 0);
		if (vector.DistanceTo(spot) > 6f || (vector.X == 0f && vector.Y == 0f))
		{
			vector = spot;
		}
		Model model = new Model(rec.ModelHash);
		if (!model.IsValid || !model.IsPed)
		{
			model = new Model(PedHash.g_m_m_unibanditos_01);
		}
		Ped ped = SpawnPed((PedHash)model.Hash, vector + new Vector3(0f, 0f, 0.1f), _rng.Next(360));
		STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)model.Hash);
		if (!(ped == null))
		{
			MakeOurs(ped.Handle);
			WEAPON.REMOVE_ALL_PED_WEAPONS(ped.Handle, p1: true, p2: true);
			ped.BlockPermanentEvents = true;
			ApplyScars(ped, rec);
			_sightingCrippleAt = Game.GameTime + 2500;
			_sightingPed = ped;
			_sightingRec = rec;
			_sightingSpoke = false;
			Crumbs.Native("Sightings blip add (V2)");
			_sightingBlip = ped.AddBlip(BlipType.BLIP_STYLE_NEUTRAL);
			if (_sightingBlip != null)
			{
				Function.Call(11290983183885202434uL, _sightingBlip.Handle, new InputArgument(UiDraw.ForeverVarString(rec.FullName + " (crippled)")));
			}
		}
	}

	private void UpdateSightingPed(Ped player)
	{
		if (_sightingPed == null)
		{
			return;
		}
		NemesisRecord sightingRec = _sightingRec;
		if (!_sightingPed.Exists() || !Roster.Contains(sightingRec))
		{
			ClearSighting(delete: false);
		}
		else if (_sightingPed.IsDead)
		{
			int num = PED.GET_PED_SOURCE_OF_DEATH(_sightingPed.Handle);
			bool flag = ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(_sightingPed.Handle, player.Handle, p2: true, p3: true);
			bool flag2 = num == player.Handle || (num == 0 && flag);
			if (!flag2 && !flag && (num == 0 || num == _sightingPed.Handle))
			{
				GrudgeLog("SIGHTING " + sightingRec.Name + " died with nobody's hand on it (a fall) - not counted, he's still laid up");
				ClearSighting(delete: true);
				return;
			}
			try
			{
				ResolveNemesisDeath(_sightingPed, sightingRec, executed: true);
			}
			catch (Exception ex)
			{
				GrudgeLog("SIGHTING death handling failed: " + ex.Message);
			}
			sightingRec.IsAlive = false;
			sightingRec.PresumedDead = false;
			SaveRoster();
			DeathNotice(flag2 ? ("You finished " + sightingRec.FullName + ". He never stood a chance.") : (sightingRec.FullName + " is dead - he never stood a chance."));
			ClearSighting(delete: false);
		}
		else
		{
			float num2 = _sightingPed.Position.DistanceTo(player.Position);
			if (!_sightingSpoke && num2 < 4f)
			{
				_sightingSpoke = true;
				ShowSubtitle(sightingRec.FullName + ": \"" + Taunts.Paralyzed(sightingRec, _rng) + "\"");
			}
			if (num2 > 150f)
			{
				ClearSighting(delete: true);
			}
		}
	}

	internal void ReviveFellMen()
	{
		string[] fellNotKilled = FellNotKilled;
		foreach (string id in fellNotKilled)
		{
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == id);
			if (nemesisRecord == null || nemesisRecord.IsAlive)
			{
				continue;
			}
			nemesisRecord.IsAlive = true;
			nemesisRecord.PresumedDead = false;
			nemesisRecord.KilledByPlayer = false;
			nemesisRecord.History?.RemoveAll((HistoryEntry h) => h.Text != null && h.Text.StartsWith("KILLED BY") && h.Text.Contains("Saint Denis"));
			Chronicle.DeathNews?.RemoveAll((DeathNews n) => n.DeadId == id);
			Chronicle.PeopleNews?.RemoveAll((DeathNews n) => n.DeadId == id);
			Chronicle.RetiredNames?.Remove(nemesisRecord.Name);
			foreach (WorldEvent item in Chronicle.Events.Where((WorldEvent e) => e.People != null && e.People.Any((WorldPart p) => p.Id == id && p.Outcome == "KILLED")).ToList())
			{
				item.People.RemoveAll((WorldPart p) => p.Id == id);
				if (item.People.Count == 0)
				{
					Chronicle.Events.Remove(item);
				}
			}
			SaveRoster();
			SaveWorld();
			GrudgeLog("SIGHTING repair: " + nemesisRecord.Name + " didn't die - he fell through the doctor's floor before it had loaded. He's back, still laid up.");
		}
	}

	private unsafe void ClearSighting(bool delete)
	{
		ClearPendingSighting();
		if (_sightingBlip != null && _sightingBlip.Exists())
		{
			_sightingBlip.Delete();
		}
		_sightingBlip = null;
		if (_sightingPed != null && _sightingPed.Exists())
		{
			if (delete && SafeToDelete(_sightingPed))
			{
				_sightingPed.Delete();
			}
			else
			{
				int handle = _sightingPed.Handle;
				ReleaseIfOurs(&handle);
			}
		}
		_sightingPed = null;
		_sightingRec = null;
	}
}
