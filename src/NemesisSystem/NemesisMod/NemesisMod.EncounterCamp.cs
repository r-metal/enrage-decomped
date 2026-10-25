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
	private int _encFire;

	private string _fireModel;

	private bool _fireLogged;

	private bool _squaredUp;

	private bool _leadWalking;

	private Encounter _enc;

	private readonly List<Ped> _encHorses = new List<Ped>();

	private bool CampAroundFire(Ped lead, List<Ped> crew)
	{
		Vector3 vector = GroundOnSpot(lead.Position + new Vector3(1.2f, 0.8f, 0f));
		if (vector == Vector3.Zero)
		{
			return false;
		}
		int num = MakeFire(vector);
		if (num == 0)
		{
			return false;
		}
		_encFire = num;
		List<Ped> list = new List<Ped> { lead }.Concat(crew.Where((Ped c) => c != null && c.Exists())).ToList();
		double num2 = _rng.NextDouble() * Math.PI * 2.0;
		for (int num3 = 0; num3 < list.Count; num3++)
		{
			Ped ped = list[num3];
			double num4 = num2 + (double)num3 * (Math.PI * 2.0 / (double)list.Count) + (_rng.NextDouble() - 0.5) * 0.35;
			float num5 = 2.1f + (float)_rng.NextDouble() * 0.5f;
			Vector3 vector2 = GroundOnSpot(vector + new Vector3((float)Math.Cos(num4) * num5, (float)Math.Sin(num4) * num5, 0f));
			if (!(vector2 == Vector3.Zero))
			{
				Crumbs.Native("encounter camp: place man");
				ENTITY.SET_ENTITY_COORDS(ped.Handle, vector2.X, vector2.Y, vector2.Z, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
				ENTITY.SET_ENTITY_HEADING(ped.Handle, HeadingTo(vector2, vector));
				string text = ((ped == lead) ? "WORLD_HUMAN_FIRE_SIT" : FireCrew[_rng.Next(FireCrew.Length)]);
				Crumbs.Native("encounter camp: scenario");
				NoteSeatTask();
				TASK.TASK_START_SCENARIO_IN_PLACE_HASH(ped.Handle, MISC.GET_HASH_KEY(text), -1, playEnterAnim: true, 0u, -1f, p6: false);
			}
		}
		GrudgeLog($"ENCOUNTER camp: a fire ({_fireModel}) at {vector.X:0},{vector.Y:0} - {list.Count} men round it");
		return true;
	}

	internal int MakeFire(Vector3 at)
	{
		string[] array = ((_fireModel == null) ? FireModels : new string[1] { _fireModel });
		foreach (string text in array)
		{
			Model m = new Model(text);
			if (m.IsValid && m.IsInCdImage && LoadModelSafe(m, 1500))
			{
				Crumbs.Native("encounter camp: create fire");
				int num = OBJECT.CREATE_OBJECT((uint)m.Hash, at.X, at.Y, at.Z, isNetwork: false, bScriptHostObj: false, dynamic: false, p7: false, p8: true);
				STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)m.Hash);
				if (num != 0)
				{
					OBJECT.PLACE_OBJECT_ON_GROUND_PROPERLY(num, p1: false);
					ENTITY.FREEZE_ENTITY_POSITION(num, toggle: true);
					ENTITY.SET_ENTITY_AS_MISSION_ENTITY(num, p1: true, p2: true);
					_fireModel = text;
					return num;
				}
			}
		}
		if (!_fireLogged)
		{
			_fireLogged = true;
			GrudgeLog("ENCOUNTER camp: none of the campfire models could be made - they stand about instead");
		}
		return 0;
	}

	private unsafe void DropCampfire(bool force = false)
	{
		if (_encFire == 0)
		{
			return;
		}
		int encFire = _encFire;
		_encFire = 0;
		if (ENTITY.DOES_ENTITY_EXIST(encFire))
		{
			Ped character = Game.Player.Character;
			Vector3 vector = ENTITY.GET_ENTITY_COORDS(encFire, alive: false, realCoords: false);
			Crumbs.Native("encounter camp: drop fire");
			if (force || character == null || !character.Exists() || HiddenFromView(character, vector) || character.Position.DistanceTo(vector) > 80f)
			{
				OBJECT.DELETE_OBJECT(&encFire);
			}
			else
			{
				ENTITY.SET_ENTITY_AS_NO_LONGER_NEEDED(&encFire);
			}
		}
	}

	private void SquareUp(Ped player, Ped lead, float d)
	{
		List<Ped> list = new List<Ped> { lead }.Concat(_followers.Where((Ped f) => f != null && f.Exists() && !f.IsDead)).ToList();
		if (!_squaredUp)
		{
			_squaredUp = true;
			foreach (Ped item in list)
			{
				if (PED.IS_PED_USING_ANY_SCENARIO(item.Handle))
				{
					TASK.CLEAR_PED_TASKS(item.Handle, p1: false, p2: false);
				}
				TASK.TASK_LOOK_AT_ENTITY(item.Handle, player.Handle, -1, 0, 0, 0);
				TASK.TASK_TURN_PED_TO_FACE_ENTITY(item.Handle, player.Handle, 2500, 0f, 0f, 0f);
			}
			_leadWalking = d > 14f;
			if (_leadWalking)
			{
				TASK.TASK_GO_TO_ENTITY(lead.Handle, player.Handle, -1, 9f, 1.1f, 0f, 0);
			}
			return;
		}
		foreach (Ped item2 in list)
		{
			if (item2 == lead && _leadWalking)
			{
				if (d > 10.5f)
				{
					continue;
				}
				_leadWalking = false;
			}
			TASK.TASK_TURN_PED_TO_FACE_ENTITY(item2.Handle, player.Handle, 2500, 0f, 0f, 0f);
		}
	}

	private bool EncounterContextOk(Ped player, out bool town, out bool mounted)
	{
		town = TownAt(player.Position) != null;
		mounted = PED.IS_PED_ON_MOUNT(player.Handle);
		if (player.IsDead)
		{
			return false;
		}
		if (INTERIOR.GET_INTERIOR_FROM_ENTITY(player.Handle) != 0)
		{
			return false;
		}
		if (MISC.GET_MISSION_FLAG())
		{
			return false;
		}
		if (!PLAYER.IS_PLAYER_CONTROL_ON(Game.Player.Handle))
		{
			return false;
		}
		return true;
	}
}
