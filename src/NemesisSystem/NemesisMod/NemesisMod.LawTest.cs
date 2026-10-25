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
	private sealed class Placement
	{
		public string Name;

		public int NotShownSince = -1;

		public bool LoggedShowing = true;
	}

	private sealed class LawTestMan
	{
		public Ped P;

		public int N;

		public string What;

		public Blip Blip;

		public int KilledAt;

		public int BountyBefore;

		public bool WantedBefore;

		public int Said;
	}

	private int _nextLawTest;

	private bool _lawTestWanted;

	private uint _lawTestHateGroup;

	private const string LeaderCopyPrefix = "lore_leader_";

	private readonly Dictionary<string, NemesisRecord> _leaderPages = new Dictionary<string, NemesisRecord>();

	internal bool LawTestOn => Game.GameTime < _lawTestUntil;

	private void DebugLawTest(Ped player)
	{
		LawTestEnd("a new test");
		Gang gang = Gangs.ById("lemoyneraiders");
		if (gang == null || !FactionModels.TryGetValue(gang.Id, out var value) || value.Length == 0)
		{
			SpawnerSays("law test: no Raider models.");
			return;
		}
		if (_lawTestHateGroup == 0)
		{
			_lawTestHateGroup = World.AddRelationshipGroup("NEMESIS_LAWTEST_HATE");
		}
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _lawTestHateGroup, player.RelationshipGroup);
		(string, uint)[] array = new(string, uint)[4]
		{
			("the mod's own group (as now)", GroupFor(gang)),
			("the game's Raiders group", MISC.GET_HASH_KEY("rel_gang_lemoyne_raiders")),
			("the mod's group, hostile to you", _lawTestHateGroup),
			("the game's criminals group", MISC.GET_HASH_KEY("rel_criminals"))
		};
		float heading = player.Heading;
		double num = (double)heading * Math.PI / 180.0;
		Vector3 vector = new Vector3(0f - (float)Math.Sin(num), (float)Math.Cos(num), 0f);
		Vector3 vector2 = new Vector3((float)Math.Cos(num), (float)Math.Sin(num), 0f);
		Vector3 vector3 = player.Position + vector * 8f;
		for (int i = 0; i < array.Length; i++)
		{
			Vector3 pos = Ground(vector3 + vector2 * (((float)i - 1.5f) * 2.5f));
			Ped ped = SpawnPed(value[_rng.Next(value.Length)], pos, heading + 180f);
			if (ped == null)
			{
				GrudgeLog($"LAWTEST {i + 1}: the model didn't load - not made");
				continue;
			}
			MakeOurs(ped.Handle);
			ApplyFactionStyle(ped, gang.Id);
			WEAPON.REMOVE_ALL_PED_WEAPONS(ped.Handle, p1: true, p2: true);
			ped.RelationshipGroup = array[i].Item2;
			ped.BlockPermanentEvents = true;
			TASK.TASK_STAND_STILL(ped.Handle, -1);
			LawTestMan lawTestMan = new LawTestMan
			{
				P = ped,
				N = i + 1,
				What = array[i].Item1
			};
			try
			{
				lawTestMan.Blip = ped.AddBlip(BlipType.BLIP_STYLE_NEUTRAL);
				if (lawTestMan.Blip != null)
				{
					Function.Call(11290983183885202434uL, lawTestMan.Blip.Handle, new InputArgument(UiDraw.ForeverVarString($"Law test {i + 1} - {array[i].Item1}")));
				}
			}
			catch
			{
			}
			_lawTest.Add(lawTestMan);
		}
		_lawTestUntil = Game.GameTime + 300000;
		_fairUntil = 0;
		try
		{
			_lawTestBounty = LAW.GET_BOUNTY(Game.Player.Handle);
			_lawTestWanted = PLAYER.GET_PLAYER_WANTED_LEVEL(Game.Player.Handle) > 0;
		}
		catch
		{
		}
		GrudgeLog(string.Format("LAWTEST started: {0} Raiders, left to right - {1} - bounty ${2}, wanted {3}; fair game off for 5 minutes", _lawTest.Count, string.Join("; ", _lawTest.Select((LawTestMan m) => $"{m.N} {m.What}")), _lawTestBounty / 100, _lawTestWanted));
		SpawnerSays("law test: left to right - 1 the mod's group, 2 the game's Raiders, 3 the mod's group (hostile), 4 the game's criminals. Kill one, wait 30 s for its LAWTEST line, then the next - 1 last.");
	}

	private void LawTestTick(Ped player)
	{
		if (_lawTest.Count == 0 || Game.GameTime < _nextLawTest)
		{
			return;
		}
		_nextLawTest = Game.GameTime + 500;
		int handle = Game.Player.Handle;
		int num = _lawTestBounty;
		bool flag = _lawTestWanted;
		try
		{
			num = LAW.GET_BOUNTY(handle);
			flag = PLAYER.GET_PLAYER_WANTED_LEVEL(handle) > 0;
		}
		catch
		{
		}
		foreach (LawTestMan item in _lawTest)
		{
			if (item.KilledAt == 0 && item.P != null && item.P.Exists() && item.P.IsDead)
			{
				item.KilledAt = Game.GameTime;
				item.BountyBefore = _lawTestBounty;
				item.WantedBefore = _lawTestWanted;
				bool flag2 = PED.GET_PED_SOURCE_OF_DEATH(item.P.Handle) == player.Handle;
				GrudgeLog(string.Format("LAWTEST {0} ({1}) killed{2} - bounty before ${3}, wanted {4}", item.N, item.What, flag2 ? " by you" : " - not by you", item.BountyBefore / 100, item.WantedBefore));
				if (item.Blip != null && item.Blip.Exists())
				{
					item.Blip.Delete();
				}
			}
			if (item.KilledAt == 0)
			{
				continue;
			}
			int[] array = new int[3] { 3000, 10000, 30000 };
			while (item.Said < array.Length && Game.GameTime - item.KilledAt >= array[item.Said])
			{
				bool flag3 = false;
				bool flag4 = false;
				bool flag5 = false;
				try
				{
					flag3 = Function.Call<bool>(7629521592523355275uL, new InputArgument[1] { handle });
					flag4 = Function.Call<bool>(844106609231005222uL, new InputArgument[1] { handle });
					flag5 = LAW.IS_LAW_INCIDENT_ACTIVE(handle);
				}
				catch
				{
				}
				GrudgeLog(string.Format("LAWTEST {0} ({1}) +{2} s: bounty ${3} -> ${4}, wanted {5}, witnesses {6}{7}, law incident {8}", item.N, item.What, array[item.Said] / 1000, item.BountyBefore / 100, num / 100, flag, flag3 ? "active" : "none active", flag4 ? " / pending" : "", flag5 ? "on" : "off"));
				item.Said++;
			}
		}
		if (_lawTest.All((LawTestMan m) => m.KilledAt == 0 || m.Said >= 3))
		{
			_lawTestBounty = num;
			_lawTestWanted = flag;
		}
		if (_lawTest.All((LawTestMan m) => m.KilledAt > 0 && m.Said >= 3))
		{
			LawTestEnd("all four done");
		}
		else if (!LawTestOn)
		{
			LawTestEnd("5 minutes up");
		}
	}

	private void LawTestEnd(string why)
	{
		if (_lawTest.Count == 0)
		{
			return;
		}
		foreach (LawTestMan item in _lawTest)
		{
			if (item.Blip != null && item.Blip.Exists())
			{
				item.Blip.Delete();
			}
			if (item.P != null && item.P.Exists() && !item.P.IsDead)
			{
				item.P.Delete();
			}
		}
		_lawTest.Clear();
		_lawTestUntil = 0;
		GrudgeLog("LAWTEST over (" + why + ") - fair game back to normal");
	}
}
