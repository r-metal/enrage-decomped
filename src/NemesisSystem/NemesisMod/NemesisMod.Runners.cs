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
	private sealed class Runner
	{
		public int H;

		public int Since;

		public int NextOrder;

		public uint Side;
	}

	private int _nextRunnerCheck;

	internal void StartRunning(int h, Ped player, uint side = 0u)
	{
		if (!ENTITY.DOES_ENTITY_EXIST(h))
		{
			return;
		}
		Ped ped = new Ped(h);
		if (!ped.IsDead)
		{
			if (ped.IsHogtied || ENTITY.IS_ENTITY_ATTACHED(h))
			{
				ped.BlockPermanentEvents = true;
				_sparedBound[h] = Game.GameTime;
			}
			else if (!CantWalk(h))
			{
				ped.BlockPermanentEvents = true;
				Flee(h, player.Handle, 400f, -1, 3f);
				_runners[h] = new Runner
				{
					H = h,
					Since = Game.GameTime,
					NextOrder = Game.GameTime + 3000,
					Side = side
				};
			}
		}
	}

	private void UpdateRunners(Ped player)
	{
		if (_runners.Count == 0 || Game.GameTime < _nextRunnerCheck)
		{
			return;
		}
		_nextRunnerCheck = Game.GameTime + 1000;
		Vector3 position = player.Position;
		foreach (Runner item in _runners.Values.ToList())
		{
			int h = item.H;
			if (!ENTITY.DOES_ENTITY_EXIST(h))
			{
				_runners.Remove(h);
				continue;
			}
			Ped ped = new Ped(h);
			if (ped.IsDead)
			{
				_runners.Remove(h);
				continue;
			}
			if (ped.IsHogtied || ENTITY.IS_ENTITY_ATTACHED(h) || InterroFor(h) != null || UnderArrest(h))
			{
				ped.BlockPermanentEvents = StaysBlocked(ped);
				if (item.Side != 0)
				{
					ped.RelationshipGroup = item.Side;
				}
				_runners.Remove(h);
				continue;
			}
			if (_hitByYouAt.TryGetValue(h, out var value) && value > item.Since + 500)
			{
				ped.BlockPermanentEvents = StaysBlocked(ped);
				if (item.Side != 0)
				{
					ped.RelationshipGroup = item.Side;
				}
				TASK.TASK_COMBAT_PED(h, player.Handle, 0, 16);
				GrudgeLog("RUNNER " + WhoIs(h) + ": you hit him while he ran - he turns and fights");
				_runners.Remove(h);
				continue;
			}
			float num = ped.Position.DistanceTo(position);
			Vector3 direction = GameplayCamera.Direction;
			Vector3 vector = ped.Position - position;
			float num2 = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y) * (float)Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
			bool flag = num2 > 0.001f && (direction.X * vector.X + direction.Y * vector.Y) / num2 < -0.2f;
			if ((num >= 200f || num >= 110f || (num >= 60f && flag)) && !IsActiveMan(h) && HiddenFromView(player, ped.Position))
			{
				bool flag2 = BelongsToUs(h);
				GrudgeLog(string.Format("RUNNER {0}: {1:0} m off and out of sight after {2} s - {3}", WhoIs(h), num, (Game.GameTime - item.Since) / 1000, flag2 ? "gone" : "left to the game"));
				_runners.Remove(h);
				_worldNemeses.Remove(h);
				if (flag2)
				{
					DeleteOrRelease(ped);
					continue;
				}
				ped.BlockPermanentEvents = false;
				if (item.Side != 0)
				{
					ped.RelationshipGroup = item.Side;
				}
			}
			else if (Game.GameTime >= item.NextOrder && !ped.IsRagdoll && (ENTITY.GET_ENTITY_SPEED(h) < 1f || !PED.IS_PED_FLEEING(h)))
			{
				item.NextOrder = Game.GameTime + 3000;
				ped.BlockPermanentEvents = true;
				if (PED.IS_PED_IN_ANY_VEHICLE(h, atGetIn: false) && ENTITY.GET_ENTITY_SPEED(h) < 1f)
				{
					TASK.TASK_LEAVE_ANY_VEHICLE(h, 0, 0);
					GrudgeLog("RUNNER " + WhoIs(h) + ": stuck on a stopped vehicle - gets off and runs");
				}
				else
				{
					Flee(h, player.Handle, 400f, -1, 3f);
				}
			}
		}
	}
}
