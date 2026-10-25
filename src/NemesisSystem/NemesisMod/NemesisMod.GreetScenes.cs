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
	private sealed class GreetScene
	{
		public string Name;

		public bool Busy;

		public bool Checked;

		public bool Left;

		public bool Restored;

		public bool Female;

		public int Point;

		public int Start;

		public int RestoreAt;

		public Vector3 At;

		public bool Still;

		public bool Table;

		public string Script;
	}

	private readonly Dictionary<string, float> _enemyGoodwill = new Dictionary<string, float>();

	private readonly Dictionary<int, GreetScene> _greetScenes = new Dictionary<int, GreetScene>();

	private const int GreetBackOnHisOwnMs = 15000;

	private const int GreetSceneGiveUpMs = 60000;

	private const int GreetSceneMaxMs = 75000;

	private int _nextGreetScenes;

	private void NoteEnemyHurt(Gang victim, int rank)
	{
		if (victim == null || victim.StoryOnly)
		{
			return;
		}
		float num = ((rank >= 4) ? 1f : 0.25f);
		foreach (Gang item in Gangs.All.Where((Gang x) => x != victim && !x.StoryOnly && !IsDestroyed(x) && NotorietyOf(x) > 0))
		{
			if (Relation(item.Id, victim.Id) <= Config.ClashLine)
			{
				_enemyGoodwill.TryGetValue(item.Id, out var value);
				value += num;
				int num2 = (int)value;
				_enemyGoodwill[item.Id] = value - (float)num2;
				if (num2 > 0)
				{
					LowerNotoriety(item, num2, "you've been killing men of the " + victim.Name, "enemies", 5);
				}
			}
		}
	}

	internal void NoteGreetScene(int handle)
	{
		if (_greetScenes.ContainsKey(handle) || !ENTITY.DOES_ENTITY_EXIST(handle) || ENTITY.IS_ENTITY_DEAD(handle) || IsOurs(handle))
		{
			return;
		}
		Ped ped = new Ped(handle);
		if (!ped.IsHuman || ped.IsPlayer)
		{
			return;
		}
		bool flag = PED.IS_PED_USING_ANY_SCENARIO(handle);
		int num = 0;
		if (flag)
		{
			try
			{
				num = TASK._GET_SCENARIO_POINT_PED_IS_USING(handle, p1: true);
			}
			catch
			{
			}
		}
		Named value;
		string text = ((_named.TryGetValue(handle, out value) && !string.IsNullOrEmpty(value.Name)) ? value.Name : $"ped {handle}");
		string text2 = null;
		try
		{
			text2 = ScriptOf(handle);
		}
		catch
		{
		}
		bool flag2 = AtTableGame(handle);
		bool flag3 = !flag && ENTITY.GET_ENTITY_SPEED(handle) < 0.3f;
		_greetScenes[handle] = new GreetScene
		{
			Name = text,
			Female = !PED.IS_PED_MALE(handle),
			Busy = flag,
			Point = num,
			Start = Game.GameTime,
			At = ped.Position,
			Still = flag3,
			Script = text2,
			Table = flag2
		};
		string text3 = ((text2 != null) ? (" - run by the game's script '" + text2 + "'" + (flag2 ? ", a table game" : "")) : " - no game script runs him");
		if (flag)
		{
			GrudgeLog(Pron.Her("GREETSCENE " + text + ": greeted " + ((num != 0) ? "in an activity on a spot" : "in an activity where he stood") + text3 + " [" + BusyNote(handle) + "]", !PED.IS_PED_MALE(handle)));
		}
		else if (flag3)
		{
			GrudgeLog(Pron.Her("GREETSCENE " + text + ": greeted standing still or seated, NOT in an activity" + text3 + " - watching 8 s", !PED.IS_PED_MALE(handle)));
		}
	}

	private void UpdateGreetScenes(Ped player)
	{
		if (_greetScenes.Count == 0 || Game.GameTime < _nextGreetScenes)
		{
			return;
		}
		_nextGreetScenes = Game.GameTime + 500;
		foreach (KeyValuePair<int, GreetScene> item in _greetScenes.ToList())
		{
			int key = item.Key;
			GreetScene value = item.Value;
			int num = Game.GameTime - value.Start;
			if (!ENTITY.DOES_ENTITY_EXIST(key) || ENTITY.IS_ENTITY_DEAD(key) || num > 75000)
			{
				_greetScenes.Remove(key);
				continue;
			}
			Ped ped = new Ped(key);
			if (!value.Busy)
			{
				if (!value.Still)
				{
					_greetScenes.Remove(key);
					continue;
				}
				Vector3 position = ped.Position;
				float num2 = position.Z - value.At.Z;
				float num3 = new Vector3(position.X - value.At.X, position.Y - value.At.Y, 0f).Length();
				if (num2 > 0.25f || num3 > 1f)
				{
					GrudgeLog(Pron.Her(string.Format("GREETSCENE {0}: got up {1:0.0} s after the greeting (rose {2:0.00} m, {3:0.0} m from his place{4}{5}{6})", value.Name, (double)num / 1000.0, num2, num3, (value.Script != null) ? (", script '" + value.Script + "'") : "", PED.IS_PED_FLEEING(key) ? ", fleeing" : "", PED.IS_PED_USING_ANY_SCENARIO(key) ? ", into an activity" : ""), value.Female));
					_greetScenes.Remove(key);
				}
				else if (num > 8000)
				{
					GrudgeLog(Pron.Her("GREETSCENE " + value.Name + ": stayed put 8 s on (not in an activity)", value.Female));
					_greetScenes.Remove(key);
				}
				continue;
			}
			bool flag = PED.IS_PED_USING_ANY_SCENARIO(key);
			if (!value.Left)
			{
				if (!flag)
				{
					value.Left = true;
					value.RestoreAt = Game.GameTime + 15000;
					GrudgeLog(Pron.Her($"GREETSCENE {value.Name}: left his activity {(double)num / 1000.0:0.0} s after the greeting", value.Female));
					continue;
				}
				if (!value.Checked && num >= 3000)
				{
					value.Checked = true;
					GrudgeLog(Pron.Her("GREETSCENE " + value.Name + ": still in his activity 3 s on", value.Female));
				}
				if (num > 8000)
				{
					_greetScenes.Remove(key);
				}
			}
			else if (!value.Restored)
			{
				if (Game.GameTime < value.RestoreAt)
				{
					continue;
				}
				string text = (flag ? "went back to it by himself" : ((!Config.TownsfolkReactions) ? "townsfolk reactions are off" : ((value.Table || AtTableGame(key)) ? "a card player - the table's script has him" : (PED.IS_PED_FLEEING(key) ? "he's fleeing" : (PED.IS_PED_IN_COMBAT(key, 0) ? "he's fighting" : ((ped.IsInVehicle || ped.IsOnMount) ? "he's riding" : ((ENTITY.GET_ENTITY_SPEED(key) > 2.2f) ? "he's running" : ((ped.Position.DistanceTo(value.At) > 15f) ? "he's gone on his way" : (IsOurs(key) ? "the mod has him now" : null)))))))));
				if (text != null)
				{
					GrudgeLog(Pron.Her("GREETSCENE " + value.Name + ": not put back - " + text, value.Female));
					_greetScenes.Remove(key);
					continue;
				}
				if (Function.Call<bool>(7006499128492128287uL, new InputArgument[1] { key }))
				{
					if (num > 60000)
					{
						GrudgeLog(Pron.Her("GREETSCENE " + value.Name + ": not put back - always in your sight", value.Female));
						_greetScenes.Remove(key);
					}
					continue;
				}
				Crumbs.Native("greet scene: back to it");
				NoteSeatTask();
				if (value.Point != 0)
				{
					TASK.TASK_USE_SCENARIO_POINT(key, value.Point, "", -1, p4: true, p5: false, 0u, p7: false, -1f, p9: true);
				}
				else
				{
					TASK.TASK_USE_NEAREST_SCENARIO_TO_COORD_WARP(key, value.At.X, value.At.Y, value.At.Z, 2f, -1, p6: true, p7: false, p8: false, p9: false);
				}
				value.Restored = true;
				value.RestoreAt = Game.GameTime + 8000;
				GrudgeLog(Pron.Her(string.Format("GREETSCENE {0}: put back {1} ({2:0.0} m from it)", value.Name, (value.Point != 0) ? "on his spot" : "into the nearest activity where he was", ped.Position.DistanceTo(value.At)), value.Female));
			}
			else if (flag || Game.GameTime >= value.RestoreAt)
			{
				GrudgeLog(Pron.Her("GREETSCENE " + value.Name + ": " + (flag ? "back in his activity" : "didn't take - left as he is"), value.Female));
				_greetScenes.Remove(key);
			}
		}
	}
}
