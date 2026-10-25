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
	private WorldEvent _aftermathLive;

	private int _nextAftermathCheck;

	internal static readonly (string name, uint style, string sprite)[] MarkerLooks = new(string, uint, string)[15]
	{
		("NEUTRAL_SECRET", 1631143573u, "blip_ambient_secret"),
		("POI_SPRITE", 2641016523u, "blip_poi"),
		("OBJ_POI", 203020899u, "blip_poi"),
		("NEUTRAL_POI", 1631143573u, "blip_poi"),
		("CAMP_POI", 1560611276u, "blip_poi"),
		("SECRET", 2641016523u, "blip_ambient_secret"),
		("SEARCH", 1631143573u, "blip_npc_search"),
		("MISSION_RC", 1631143573u, "blip_mission_area_rc"),
		("OBJ_MISSION_RC", 203020899u, "blip_mission_area_rc"),
		("CLUE_RC", 1538154804u, "blip_rc"),
		("OBJ_RC", 203020899u, "blip_rc"),
		("NEUTRAL_RC", 1631143573u, "blip_rc"),
		("POI", 2641016523u, "blip_rc"),
		("RCM", 2957021944u, null),
		("CAMP", 1560611276u, "blip_rc")
	};

	private uint _aftermathImap;

	internal bool ToggleMark(WorldEvent e)
	{
		if (e == null || !e.HasPos)
		{
			return false;
		}
		e.Marked = !e.Marked;
		_nextAftermathCheck = 0;
		SaveWorld();
		ShowNotification(e.Marked ? ("Marked on your map: " + e.Title) : "Mark removed.");
		return true;
	}

	private unsafe void UpdateAftermath(Ped player)
	{
		if (Game.GameTime < _nextAftermathCheck)
		{
			return;
		}
		_nextAftermathCheck = Game.GameTime + 1000;
		long now = GameMinutesNow();
		Vector3 me = player.Position;
		List<WorldEvent> list = Chronicle.Events.Where((WorldEvent e) => e.HasPos && e.AftermathUntil > now && !e.Visited && (e.Heard || Config.WorldKnowAll)).ToList();
		foreach (WorldEvent item in Chronicle.Events.Where((WorldEvent e) => e.HasPos && !e.Reached && e.AftermathUntil > 0 && new Vector3(e.X, e.Y, me.Z).DistanceTo(me) < 25f).ToList())
		{
			item.Reached = true;
			item.Marked = false;
			SaveWorld();
		}
		foreach (WorldEvent item2 in Chronicle.Events.Where((WorldEvent e) => e.Marked && e.HasPos && new Vector3(e.X, e.Y, me.Z).DistanceTo(me) < 25f).ToList())
		{
			item2.Marked = false;
			SaveWorld();
		}
		List<WorldEvent> show = Chronicle.Events.Where((WorldEvent e) => e.HasPos && (e.Marked || (e.AftermathUntil > now && !e.Reached && (e.Heard || Config.WorldKnowAll) && new Vector3(e.X, e.Y, me.Z).DistanceTo(me) < Config.WorldMarkerRadius))).ToList();
		if (!MapSettled())
		{
			show = null;
		}
		else if (_mapHidden)
		{
			show = new List<WorldEvent>();
		}
		if (show != null)
		{
			foreach (string item3 in _aftermathBlips.Keys.Where((string k) => !show.Any((WorldEvent e) => e.Id == k)).ToList())
			{
				int num = _aftermathBlips[item3];
				Crumbs.Native("WorldSim blip remove");
				MAP.REMOVE_BLIP(&num);
				_aftermathBlips.Remove(item3);
			}
		}
		if (show != null)
		{
			foreach (WorldEvent item4 in show)
			{
				if (!_aftermathBlips.ContainsKey(item4.Id))
				{
					int value = AddMarkerBlip(item4.X, item4.Y, item4.Z, (item4.AftermathUntil > now && !item4.Visited) ? ("Aftermath: " + item4.Title) : item4.Title);
					_aftermathBlips[item4.Id] = value;
				}
			}
		}
		foreach (WorldEvent item5 in list)
		{
			float num2 = new Vector3(item5.X, item5.Y, me.Z).DistanceTo(me);
			if (_aftermathLive == null && !MISC.GET_MISSION_FLAG() && num2 < 140f && (num2 < 90f || HiddenFromView(player, new Vector3(item5.X, item5.Y, me.Z + 1f))))
			{
				SpawnAftermath(item5, player);
			}
		}
		if (_aftermathLive != null && new Vector3(_aftermathLive.X, _aftermathLive.Y, player.Position.Z).DistanceTo(player.Position) > 300f)
		{
			DespawnAftermath();
		}
		UpdateSurvivors(player);
	}

	private (string name, uint style, string sprite) MarkerLook
	{
		get
		{
			if (MarkerLooks.FirstOrDefault(((string name, uint style, string sprite) m) => m.name == Config.WorldMarkerStyle).name == null)
			{
				return MarkerLooks[0];
			}
			return MarkerLooks.First(((string name, uint style, string sprite) m) => m.name == Config.WorldMarkerStyle);
		}
	}

	internal int AddMarkerBlip(float x, float y, float z, string name)
	{
		(string, uint, string) markerLook = MarkerLook;
		Crumbs.Native("WorldSim blip add");
		int num = MAP.BLIP_ADD_FOR_COORDS(markerLook.Item2, x, y, z);
		Crumbs.Native("WorldSim blip sprite");
		if (markerLook.Item3 != null)
		{
			MAP.SET_BLIP_SPRITE(num, MISC.GET_HASH_KEY(markerLook.Item3), p2: true);
		}
		Function.Call(11290983183885202434uL, num, new InputArgument(UiDraw.ForeverVarString(name)));
		return num;
	}

	internal void CycleMarkerLook()
	{
		int num = Array.FindIndex(MarkerLooks, ((string name, uint style, string sprite) m) => m.name == MarkerLook.name);
		Config.WorldMarkerStyle = MarkerLooks[(num + 1) % MarkerLooks.Length].name;
		Config.Save();
		ClearAftermathBlips();
		ClearPlanBlips();
		_nextAftermathCheck = 0;
		_nextPlanCheck = 0;
		ShowNotification($"Map ? marker: {Config.WorldMarkerStyle} ({Array.FindIndex(MarkerLooks, ((string name, uint style, string sprite) m) => m.name == Config.WorldMarkerStyle) + 1}/{MarkerLooks.Length}) - K for the next");
		GrudgeLog("MARKER look " + Config.WorldMarkerStyle);
	}

	private unsafe void SpawnAftermath(WorldEvent e, Ped player)
	{
		_aftermathLive = e;
		e.Visited = true;
		float z = default(float);
		if (MISC.GET_GROUND_Z_FOR_3D_COORD(e.X, e.Y, Math.Max(e.Z, player.Position.Z) + 40f, &z, p4: false))
		{
			e.Z = z;
			SaveWorld();
		}
		Vector3 center = new Vector3(e.X, e.Y, e.Z);
		CampSite campSite = (from c in Camps.ODriscoll
			where c.Imap != 0
			orderby new Vector3(c.X, c.Y, c.Z).DistanceTo(center)
			select c).FirstOrDefault();
		if (campSite != null && new Vector3(campSite.X, campSite.Y, campSite.Z).DistanceTo(center) < 60f && !STREAMING.IS_IPL_ACTIVE_HASH(campSite.Imap))
		{
			Crumbs.Native("WorldSim map chunk in");
			STREAMING.REQUEST_IPL_HASH(campSite.Imap);
			_aftermathImap = campSite.Imap;
			GrudgeLog("AFTERMATH " + e.Title + ": the camp (" + campSite.Id + ") streamed in for it, empty");
		}
		List<(uint, NemesisRecord, string)> list = new List<(uint, NemesisRecord, string)>();
		foreach (WorldPart p in e.People.Where((WorldPart worldPart) => worldPart.Outcome == "KILLED"))
		{
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord r) => r.Id == p.Id);
			if (nemesisRecord != null && (!nemesisRecord.IsAlive || nemesisRecord.PresumedDead))
			{
				list.Add((nemesisRecord.ModelHash, nemesisRecord, nemesisRecord.Gang));
			}
		}
		foreach (KeyValuePair<string, int> item4 in e.UnnamedDead)
		{
			if (FactionModels.TryGetValue(item4.Key, out var value))
			{
				for (int num = 0; num < item4.Value; num++)
				{
					list.Add(((uint)value[_rng.Next(value.Length)], null, item4.Key));
				}
			}
		}
		int num2 = 0;
		float num5 = default(float);
		foreach (var item5 in list.Take(8))
		{
			uint item = item5.Item1;
			NemesisRecord item2 = item5.Item2;
			string item3 = item5.Item3;
			double num3 = _rng.NextDouble() * Math.PI * 2.0;
			float num4 = 2f + (float)_rng.NextDouble() * 14f;
			Vector3 pos = center + new Vector3((float)Math.Cos(num3) * num4, (float)Math.Sin(num3) * num4, 0f);
			if (MISC.GET_GROUND_Z_FOR_3D_COORD(pos.X, pos.Y, center.Z + 25f, &num5, p4: false))
			{
				pos.Z = num5 + 0.5f;
			}
			Model m = new Model(item);
			if (!m.IsValid || !m.IsPed)
			{
				m = new Model(PedHash.g_m_m_unibanditos_01);
			}
			if (!LoadModelSafe(m, 1500))
			{
				break;
			}
			Ped ped = SpawnPed((PedHash)m.Hash, pos, _rng.Next(360));
			STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)m.Hash);
			if (!(ped == null))
			{
				MakeOurs(ped.Handle);
				if (item2 != null)
				{
					ApplyScars(ped, item2);
				}
				KlanBody(ped, item3);
				PED.APPLY_DAMAGE_TO_PED(ped.Handle, ped.Health + 500, 0, 0, 0);
				if (item2 != null)
				{
					_nemesisBodies[ped.Handle] = item2;
				}
				_aftermathPeds.Add(ped);
				num2++;
			}
		}
		SaveWorld();
		MaybeSpawnSurvivors(e, player, center);
		GrudgeLog($"AFTERMATH {e.Title} {e.Place}: {num2} bodies");
		Feed($"AFTERMATH {e.Title} {e.Place}: {num2} bodies", KindColor(e.Kind));
		GrudgeNotice("The aftermath of it: " + e.Title.ToLowerInvariant() + " " + e.Place + ".");
	}

	private void DespawnAftermath()
	{
		if (_aftermathImap != 0)
		{
			uint im = _aftermathImap;
			_aftermathImap = 0u;
			Crumbs.Native("WorldSim map chunk out");
			if (!_liveCamps.Values.Any((LiveCamp c) => c.Site.Imap == im))
			{
				STREAMING.REMOVE_IPL_HASH(im);
			}
		}
		foreach (Ped aftermathPed in _aftermathPeds)
		{
			if (aftermathPed != null && aftermathPed.Exists())
			{
				_nemesisBodies.Remove(aftermathPed.Handle);
				DeleteOrRelease(aftermathPed);
			}
		}
		_aftermathPeds.Clear();
		_aftermathLive = null;
		DespawnSurvivors();
	}

	private unsafe void ClearAftermathBlips()
	{
		Crumbs.Native("WorldSim blip remove");
		foreach (string item in _aftermathBlips.Keys.ToList())
		{
			int num = _aftermathBlips[item];
			MAP.REMOVE_BLIP(&num);
		}
		_aftermathBlips.Clear();
	}

	internal static void Flee(int ped, int from, float distance, int ms, float speed)
	{
		TASK.TASK_SMART_FLEE_PED(ped, from, distance, ms, 0, speed, 0);
	}
}
