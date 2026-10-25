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
	private readonly List<Ped> _survivors = new List<Ped>();

	private Gang _survivorGang;

	private bool _survivorsHostile;

	private uint _survivorGroup;

	private int _nextSurvivorCheck;

	private static readonly string[] SurvivorScenarios = new string[4] { "WORLD_HUMAN_CROUCH_INSPECT", "WORLD_HUMAN_GUARD_SCOUT", "WORLD_HUMAN_SMOKE", "WORLD_HUMAN_GUARD_LAZY" };

	private void MaybeSpawnSurvivors(WorldEvent e, Ped player, Vector3 center)
	{
		if ((!e.ForceSurvivors && _rng.Next(100) >= Config.WorldSurvivorChance) || e.Gangs == null)
		{
			return;
		}
		List<string> list = e.Gangs.Where((string g) => !string.IsNullOrEmpty(g) && Gangs.ById(g) != null).Distinct().ToList();
		if (list.Count == 0)
		{
			return;
		}
		List<string> source = list.OrderBy(Dead).ToList();
		string text = source.First();
		string text2 = source.Last();
		bool flag = list.Count == 1 || _rng.Next(2) == 0;
		Gang gang = Gangs.ById(flag ? text : text2);
		if (gang == null || gang.TroopModels.Length == 0)
		{
			return;
		}
		List<NemesisRecord> list2 = (from _ in (from p in e.People
				where p.Gang == gang.Id && (p.Outcome == "UNHURT" || p.Outcome == "WOUNDED" || p.Outcome == "DISTINGUISHED")
				select Roster.FirstOrDefault((NemesisRecord r) => r.Id == p.Id) into r
				where r != null && r.IsAlive && !r.PresumedDead && r.CanHunt && !Resting(r) && r != ActiveRecord && !_worldNemeses.ContainsValue(r)
				select r).Distinct()
			orderby _rng.Next()
			select _).Take(2).ToList();
		int num = Math.Max(list2.Count, 2 + _rng.Next(3));
		if (_survivorGroup == 0)
		{
			_survivorGroup = World.AddRelationshipGroup("NEMESIS_SURVIVORS");
		}
		_survivorGang = gang;
		_survivorsHostile = false;
		SetSurvivorRelation(player);
		List<string> list3 = new List<string>();
		if (!flag)
		{
			list3.Add("mourn");
			if (num >= 4 && _rng.Next(100) < 30)
			{
				list3.Add("mourn");
			}
		}
		else
		{
			list3.Add("search");
		}
		list3.Add("watch");
		List<string> list4 = new List<string> { "smoke", "wander", "lazy", "talk", "drink", "watch" }.OrderBy((string _) => _rng.Next()).ToList();
		while (list3.Count < num && list4.Count > 0)
		{
			list3.Add(list4[0]);
			list4.RemoveAt(0);
		}
		while (list3.Count < num)
		{
			list3.Add("wander");
		}
		list3 = (from _ in list3.Take(num)
			orderby _rng.Next()
			select _).ToList();
		HashSet<int> usedBodies = new HashSet<int>();
		for (int num2 = 0; num2 < num; num2++)
		{
			string role = list3[num2];
			double num3 = _rng.NextDouble() * Math.PI * 2.0;
			float num4 = 4f + (float)_rng.NextDouble() * 12f;
			Vector3 pos = GroundOnSpot(center + new Vector3((float)Math.Cos(num3) * num4, (float)Math.Sin(num3) * num4, 0f));
			float heading = _rng.Next(360);
			Ped ped;
			if (num2 < list2.Count)
			{
				NemesisRecord nemesisRecord = list2[num2];
				ped = SpawnNemesisPed(nemesisRecord, pos, heading, _survivorGroup, out var _, (uint)gang.TroopModels[0]);
				if (ped == null)
				{
					continue;
				}
				_worldNemeses[ped.Handle] = nemesisRecord;
				nemesisRecord.Touch();
				History(nemesisRecord, flag ? ("STILL HOLDING the field after it: " + e.Title) : ("WENT BACK for his dead after it: " + e.Title), gang.Accent);
			}
			else
			{
				ped = SpawnPed(gang.TroopModels[_rng.Next(gang.TroopModels.Length)], pos, heading);
				if (ped == null)
				{
					continue;
				}
				MakeOurs(ped.Handle);
				ArmTroop(ped, gang);
				ped.Accuracy = gang.TroopAccuracy;
				ped.RelationshipGroup = _survivorGroup;
				ApplyFactionStyle(ped, gang.Id);
			}
			ped.BlockPermanentEvents = false;
			role = SurvivorRole(ped, role, gang, center, usedBodies);
			GrudgeLog("SURVIVORS a " + MemberWord(gang) + " man: " + role);
			_survivors.Add(ped);
		}
		GrudgeLog(string.Format("SURVIVORS at {0}: {1} {2} ({3}), ", e.Title, _survivors.Count, gang.Name, flag ? "the winners still hold it" : "the losers came back") + string.Format("{0} named ({1}), after you: {2}", list2.Count, string.Join(", ", list2.Select((NemesisRecord n) => n.Name)), AfterYou(gang)));
		Feed(string.Format("AFTERMATH {0}: {1} men still at the battlefield ({2})", gang.Name, _survivors.Count, flag ? "holding it" : "came back for their dead"), gang.Accent);
		int Dead(string g)
		{
			int value;
			return e.People.Count((WorldPart p) => p.Gang == g && p.Outcome == "KILLED") + (e.UnnamedDead.TryGetValue(g, out value) ? value : 0);
		}
	}

	private string SurvivorRole(Ped p, string role, Gang gang, Vector3 center, HashSet<int> usedBodies)
	{
		switch (role)
		{
		case "mourn":
		case "search":
		{
			Ped ped2 = Body(role == "mourn");
			if (ped2 == null)
			{
				Scenario("WORLD_HUMAN_GUARD_LAZY");
				return "lazy (no body for him)";
			}
			usedBodies.Add(ped2.Handle);
			double num2 = _rng.NextDouble() * Math.PI * 2.0;
			Vector3 vector2 = ped2.Position + new Vector3((float)Math.Cos(num2) * 0.9f, (float)Math.Sin(num2) * 0.9f, 0f);
			MoveTo(vector2, HeadingTo(vector2, ped2.Position));
			Scenario((role == "mourn") ? "WORLD_HUMAN_GRAVE_MOURNING_KNEEL" : "WORLD_HUMAN_CROUCH_INSPECT");
			if (!(role == "mourn"))
			{
				return "goes through the dead's pockets";
			}
			return "kneels by his dead";
		}
		case "watch":
		{
			double num3 = _rng.NextDouble() * Math.PI * 2.0;
			Vector3 vector3 = center + new Vector3((float)Math.Cos(num3) * (12f + (float)_rng.NextDouble() * 5f), (float)Math.Sin(num3) * (12f + (float)_rng.NextDouble() * 5f), 0f);
			MoveTo(vector3, HeadingTo(center, vector3));
			Scenario("WORLD_HUMAN_GUARD_SCOUT");
			return "keeps watch";
		}
		case "wander":
			Function.Call(16164602603745899285uL, p.Handle, center.X, center.Y, center.Z, 10f, 2f, 8f, 0);
			return "walks among the bodies";
		case "talk":
		{
			Ped ped = _survivors.LastOrDefault((Ped s) => s != null && s.Exists());
			if (ped == null)
			{
				Scenario("WORLD_HUMAN_SMOKE");
				return "smokes";
			}
			double num = _rng.NextDouble() * Math.PI * 2.0;
			Vector3 vector = ped.Position + new Vector3((float)Math.Cos(num) * 1.5f, (float)Math.Sin(num) * 1.5f, 0f);
			MoveTo(vector, HeadingTo(vector, ped.Position));
			TASK.TASK_LOOK_AT_ENTITY(p.Handle, ped.Handle, -1, 0, 51, 0);
			return "talks with another man";
		}
		case "drink":
			Scenario("WORLD_HUMAN_DRINKING");
			return "drinks";
		case "smoke":
			Scenario("WORLD_HUMAN_SMOKE");
			return "smokes";
		default:
			Scenario("WORLD_HUMAN_GUARD_LAZY");
			return "leans on his rifle";
		}
		Ped Body(bool own)
		{
			return (from _ in _aftermathPeds
				where _ != null && _.Exists() && !usedBodies.Contains(_.Handle) && gang.IsMemberModel((uint)_.Model.Hash) == own
				orderby _rng.Next()
				select _).FirstOrDefault();
		}
		void MoveTo(Vector3 at, float heading)
		{
			Vector3 vector4 = GroundOnSpot(at);
			ENTITY.SET_ENTITY_COORDS(p.Handle, vector4.X, vector4.Y, vector4.Z, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
			ENTITY.SET_ENTITY_HEADING(p.Handle, heading);
		}
		void Scenario(string s)
		{
			TASK.TASK_START_SCENARIO_IN_PLACE_HASH(p.Handle, MISC.GET_HASH_KEY(s), -1, playEnterAnim: true, 0u, -1f, p6: false);
		}
	}

	private void SetSurvivorRelation(Ped player)
	{
		if (_survivorGroup != 0 && _survivorGang != null)
		{
			World.SetRelationshipBetweenGroups((_survivorsHostile || (_survivorGang.NeedsReason && ReasonMet(_survivorGang))) ? eRelationshipType.Hate : (_survivorGang.NeedsReason ? eRelationshipType.Ignore : eRelationshipType.Dislike), _survivorGroup, player.RelationshipGroup);
		}
	}

	private void UpdateSurvivors(Ped player)
	{
		if (_survivors.Count == 0 || Game.GameTime < _nextSurvivorCheck)
		{
			return;
		}
		_nextSurvivorCheck = Game.GameTime + 300;
		if (_survivorsHostile)
		{
			return;
		}
		List<Ped> list = _survivors.Where((Ped p) => p != null && p.Exists() && !p.IsDead).ToList();
		string text = null;
		Sight s = SightFor(player, Config.WorldSurvivorSpotRange);
		foreach (Ped item in list)
		{
			if (ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(item.Handle, player.Handle, p2: true, p3: true))
			{
				text = "you hurt one of them";
				break;
			}
			if (!_survivorGang.NeedsReason)
			{
				float d = item.Position.DistanceTo(player.Position);
				if (Sees(item, player, s, out d))
				{
					text = $"they saw you at {d:0} m ({s.Stance})";
					break;
				}
				if (FiredInAnger && d < 80f)
				{
					text = "you fired";
					break;
				}
			}
		}
		if (text == null && list.Count < _survivors.Count((Ped p) => p != null && p.Exists()))
		{
			text = "one of them went down";
		}
		if (text == null)
		{
			return;
		}
		_survivorsHostile = true;
		SetSurvivorRelation(player);
		foreach (Ped item2 in list)
		{
			TASK.CLEAR_PED_TASKS(item2.Handle, p1: true, p2: true);
			TASK.TASK_COMBAT_PED(item2.Handle, player.Handle, 0, 16);
		}
		GrudgeLog("SURVIVORS turned on you: " + text);
		Feed("AFTERMATH the " + _survivorGang.Name + " at the battlefield turned on you (" + text + ")", Theme.Blood);
	}

	private void DespawnSurvivors()
	{
		foreach (Ped survivor in _survivors)
		{
			if (!(survivor != null) || !survivor.Exists())
			{
				continue;
			}
			if (_worldNemeses.TryGetValue(survivor.Handle, out var value) && ReallyDead(survivor) && value.IsAlive && !value.PresumedDead && Roster.Contains(value))
			{
				try
				{
					ResolveNemesisDeath(survivor, value, Executed(survivor.Handle));
				}
				catch (Exception ex)
				{
					GrudgeLog("SURVIVORS death resolve failed: " + ex.Message);
				}
			}
			_worldNemeses.Remove(survivor.Handle);
			DeleteOrRelease(survivor);
		}
		_survivors.Clear();
		_survivorGang = null;
		_survivorsHostile = false;
	}
}
