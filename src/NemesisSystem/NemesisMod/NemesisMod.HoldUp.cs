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
	private void HoldDpadUp()
	{
		if (ShownNow(_pSocial) || ShownNow(_pTelegraph) || ShownNow(_pAsk))
		{
			_dpadOursAt = Game.GameTime;
		}
		if (Game.GameTime - _dpadOursAt <= 400)
		{
			eInputType[] dpadUpGameActions = DpadUpGameActions;
			for (int i = 0; i < dpadUpGameActions.Length; i++)
			{
				Game.DisableControlThisFrame(dpadUpGameActions[i]);
			}
		}
	}

	private void LetGoAccounting(Interrogation it)
	{
		NemesisRecord rec = it.Rec;
		if (rec != null && !it.Released)
		{
			rec.AddFear(5);
			rec.TimesSpared++;
			Remember(rec, "Joy", 35, "you let him go");
			SparedHim(rec, "let go after questioning");
		}
		else if (rec == null && !it.Released)
		{
			NoteMercy(it.Gang, Speaker(it), "h" + it.Ped?.Handle);
		}
		it.Released = true;
		it.LetGo = true;
		if (it.Ped != null && _fighters.TryGetValue(it.Ped.Handle, out var value) && !value.Done)
		{
			FinishFighter(value, it.Ped);
		}
	}

	private void UpdateSparedBound(Ped player)
	{
		foreach (KeyValuePair<int, int> item in _sparedBound.ToList())
		{
			int key = item.Key;
			if (!ENTITY.DOES_ENTITY_EXIST(key))
			{
				_sparedBound.Remove(key);
				continue;
			}
			Ped ped = new Ped(key);
			if (ped.IsDead)
			{
				_sparedBound.Remove(key);
			}
			else
			{
				if (_interro?.Ped != null && _interro.Ped.Handle == key)
				{
					continue;
				}
				if (CantWalk(key))
				{
					_sparedBound.Remove(key);
					GrudgeLog($"INTERRO ped {key} let go but can't walk - left where he lies");
					continue;
				}
				if (Game.GameTime - item.Value > 1200000)
				{
					ped.BlockPermanentEvents = StaysBlocked(ped);
					_sparedBound.Remove(key);
					continue;
				}
				if (ped.IsHogtied || ped.IsRagdoll || ENTITY.IS_ENTITY_ATTACHED(key) || PED.GET_PED_IS_BEING_GRAPPLED(key) || TASK.IS_PED_IN_WRITHE(key))
				{
					ped.BlockPermanentEvents = true;
					continue;
				}
				_sparedBound.Remove(key);
				if (_surrenderGroup != 0)
				{
					ped.RelationshipGroup = _surrenderGroup;
				}
				TASK.CLEAR_PED_TASKS(key, p1: true, p2: true);
				if (_interroMemory.TryGetValue(key, out var value) && InterroPatience.After(value.Terror, value.Temper, value.Calm, letGo: true, value.Warned) != AfterTalk.Run)
				{
					WalkOff(key, player, deaf: true);
					GrudgeLog($"INTERRO ped {key} is free - keeps his word and walks off (fear {value.Terror:0})");
					continue;
				}
				StartRunning(key, player);
				_recentFoes[key] = Game.GameTime;
				_runnerRolled.Remove(key);
				_letGoRunners.Add(key);
				GrudgeLog($"INTERRO ped {key} is free - keeps his word and runs");
			}
		}
	}

	private void WalkOff(int h, Ped player, bool deaf = false, int ms = 15000)
	{
		Ped ped = new Ped(h);
		Vector3 position = player.Position;
		Vector3 position2 = ped.Position;
		float num = position2.X - position.X;
		float num2 = position2.Y - position.Y;
		float num3 = (float)Math.Sqrt(num * num + num2 * num2);
		if (num3 < 0.5f)
		{
			Vector3 forwardVector = ped.ForwardVector;
			num = forwardVector.X;
			num2 = forwardVector.Y;
			num3 = Math.Max(0.01f, (float)Math.Sqrt(num * num + num2 * num2));
		}
		Vector3 vector = new Vector3(position2.X + num / num3 * 22f, position2.Y + num2 / num3 * 22f, position2.Z);
		if (deaf)
		{
			ped.BlockPermanentEvents = true;
		}
		TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(h, vector.X, vector.Y, vector.Z, 1f, -1, 1.5f, 0, 0f);
		_walkOffs[h] = new Walker
		{
			Since = Game.GameTime,
			Until = Game.GameTime + ms,
			Deaf = deaf
		};
	}

	private void UpdateWalkOffs(Ped player)
	{
		foreach (KeyValuePair<int, Walker> item in _walkOffs.ToList())
		{
			int key = item.Key;
			Walker value = item.Value;
			if (!ENTITY.DOES_ENTITY_EXIST(key) || ENTITY.IS_ENTITY_DEAD(key))
			{
				_walkOffs.Remove(key);
				continue;
			}
			Ped ped = new Ped(key);
			if ((_hitByYouAt.TryGetValue(key, out var value2) && value2 > value.Since) || (_interro?.Ped != null && _interro.Ped.Handle == key) || ped.IsHogtied || ENTITY.IS_ENTITY_ATTACHED(key) || PED.IS_PED_IN_COMBAT(key, player.Handle))
			{
				if (value.Deaf)
				{
					ped.BlockPermanentEvents = StaysBlocked(ped);
				}
				_walkOffs.Remove(key);
			}
			else if (Game.GameTime >= value.Until || !(ped.Position.DistanceTo(player.Position) < 20f))
			{
				_walkOffs.Remove(key);
				if (value.Deaf)
				{
					ped.BlockPermanentEvents = StaysBlocked(ped);
				}
				TASK.TASK_WANDER_STANDARD(key, 10f, 10);
			}
		}
	}
}
