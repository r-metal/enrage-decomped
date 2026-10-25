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
	private int _nightEcho;

	private Vector3 _nightEchoNear;

	private readonly List<(int at, Vector3 near, Ped owner, string what)> _followBooms = new List<(int, Vector3, Ped, string)>();

	private static int _modBoomAt = -100000;

	private static Vector3 _modBoomPos;

	private readonly Dictionary<int, OurRole> _roleOf = new Dictionary<int, OurRole>();

	private int _roleFrame = -1;

	private WarRecord WarNearYou(Vector3 me, float reach = 1800f)
	{
		District here = CurrentDistrict;
		return (from w in Wars
			where w.Result == null && !w.Paused && !TruceBetween(w.A, w.B)
			select (w: w, d: DistrictByKey(w.District)) into t
			where t.d != null && (t.d == here || new Vector3(t.d.X - me.X, t.d.Y - me.Y, 0f).Length() < reach)
			orderby t.d == here descending, new Vector3(t.d.X - me.X, t.d.Y - me.Y, 0f).Length()
			select t.w).FirstOrDefault();
	}

	private void QueueVolley(Vector3 pos, Ped owner, string what)
	{
		if (_rng.Next(100) < 45)
		{
			int num = ((_rng.Next(100) >= 40) ? 1 : 2);
			int num2 = Game.GameTime;
			for (int i = 0; i < num; i++)
			{
				num2 += 1000 + _rng.Next(2001);
				_followBooms.Add((num2, pos, owner, what));
			}
		}
	}

	private void UpdateVolleys(Ped player)
	{
		for (int num = _followBooms.Count - 1; num >= 0; num--)
		{
			(int, Vector3, Ped, string) tuple = _followBooms[num];
			if (Game.GameTime >= tuple.Item1)
			{
				_followBooms.RemoveAt(num);
				if (!MISC.GET_MISSION_FLAG() && !WorldBusy(player))
				{
					double num2 = _rng.NextDouble() * Math.PI * 2.0;
					float num3 = 8f + (float)_rng.NextDouble() * 17f;
					Vector3 vector = Ground(tuple.Item2 + new Vector3((float)Math.Cos(num2) * num3, (float)Math.Sin(num2) * num3, 0f));
					if (!(vector == Vector3.Zero) && !(player.Position.DistanceTo(vector) < 100f) && ModExplosion(vector, 0f, (tuple.Item3 != null && tuple.Item3.Exists() && !tuple.Item3.IsDead) ? tuple.Item3 : null, tuple.Item4 + " (volley)"))
					{
						GrudgeLog($"BOOM volley: another {tuple.Item4} {player.Position.DistanceTo(vector):0} m off");
					}
				}
			}
		}
	}

	private void UpdateNightShelling(Ped player)
	{
		UpdateVolleys(player);
		if (_nightEcho > 0 && Game.GameTime >= _nightEcho)
		{
			_nightEcho = 0;
			if (!MISC.GET_MISSION_FLAG() && !WorldBusy(player))
			{
				NightBlast(player, Ground(_nightEchoNear + new Vector3((float)(_rng.NextDouble() * 30.0 - 15.0), (float)(_rng.NextDouble() * 30.0 - 15.0), 0f)), "the second");
			}
		}
		if (Game.GameTime < _nextNightBlastRoll)
		{
			return;
		}
		_nextNightBlastRoll = Game.GameTime + 20000;
		if (!WarNight || !Config.DistantBattles || Game.GameTime < _nightBlastAllowed || SkirmishDistant || WorldBusy(player) || MISC.GET_MISSION_FLAG() || !MapSettled())
		{
			return;
		}
		WarRecord warRecord = WarNearYou(player.Position);
		if (warRecord == null)
		{
			return;
		}
		InitHeat(warRecord);
		double num = (double)warRecord.Heat * 0.4;
		if (_rng.NextDouble() * 100.0 >= num)
		{
			return;
		}
		Vector3 vector = DistantSpot(player);
		if (!(vector == Vector3.Zero))
		{
			NightBlast(player, vector, $"{WarName(warRecord)}, heat {warRecord.Heat} ({num:0}%)");
			_nightBlastAllowed = Game.GameTime + 25000;
			if (warRecord.Heat >= 60 && _rng.Next(100) < 35)
			{
				_nightEcho = Game.GameTime + 2000 + _rng.Next(4000);
				_nightEchoNear = vector;
			}
		}
	}

	private void NightBlast(Ped player, Vector3 at, string why)
	{
		if (!(player.Position.DistanceTo(at) < 120f) && ModExplosion(at, 0f, null, "night blast"))
		{
			GrudgeLog("WAR night blast " + Away(player.Position, at) + " - " + why);
			if (why != "the second")
			{
				QueueVolley(at, null, "night blast");
			}
		}
	}

	private bool ModExplosion(Vector3 pos, float shake, Ped owner, string what)
	{
		if (owner != null && owner.Exists() && !owner.IsDead)
		{
			Crumbs.Native(what + " (owned)");
			FIRE.ADD_OWNED_EXPLOSION(owner.Handle, pos.X, pos.Y, pos.Z, DistantExplosionTag, 1f, isAudible: true, isInvisible: false, shake);
		}
		else
		{
			bool flag = false;
			try
			{
				flag = World.GetAllPeds().Any((Ped p) => p != null && p.Exists() && !p.IsDead && !p.IsPlayer && p.Position.DistanceTo(pos) < 20f);
			}
			catch
			{
				flag = true;
			}
			if (flag)
			{
				GrudgeLog("BOOM " + what + ": someone within 20 m and no fighter to own it - not set off");
				return false;
			}
			Crumbs.Native(what);
			FIRE.ADD_EXPLOSION(pos.X, pos.Y, pos.Z, DistantExplosionTag, 1f, isAudible: true, isInvisible: false, shake);
		}
		_modBoomAt = Game.GameTime;
		_modBoomPos = pos;
		return true;
	}

	private Ped BoomOwner(IEnumerable<Ped> men, Vector3 pos)
	{
		return (from p in men?.Where((Ped p) => p != null && p.Exists() && !p.IsDead && !p.IsInVehicle)
			orderby p.Position.DistanceTo(pos) descending
			select p).FirstOrDefault();
	}

	private void DebugWarHeat(Ped player, int by)
	{
		WarRecord warRecord = WarNearYou(player.Position, 1E+09f) ?? Wars.FirstOrDefault((WarRecord x) => x.Result == null);
		if (warRecord == null)
		{
			SpawnerSays("no war going on - use 'Start a war here' first.");
			return;
		}
		InitHeat(warRecord);
		int heat = warRecord.Heat;
		warRecord.Heat = Math.Max(0, Math.Min(100, warRecord.Heat + by));
		SaveWorld();
		GrudgeLog($"WAR heat (spawner) {WarName(warRecord)}: {heat} -> {warRecord.Heat}");
		SpawnerSays(string.Format("{0}: heat {1} -> {2}{3}.", WarName(warRecord), heat, warRecord.Heat, warRecord.Paused ? " (a truce has it paused)" : ""));
	}
}
