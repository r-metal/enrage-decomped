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
	private sealed class MaskedTrainJob
	{
		public Vector3 At;

		public int When;

		public string Town;

		public string Place;

		public WorldEvent Deed;
	}

	private const ulong BlipAddModifierHash = 7362600660482055727uL;

	private int _trainBountyWas = -1;

	private int _nextTrainCheck;

	private int _nextTrainScan;

	private readonly List<int> _trainsNear = new List<int>();

	private readonly Dictionary<int, int> _trainsRobbed = new Dictionary<int, int>();

	private MaskedTrainJob _maskedTrain;

	private Vector3 _lastTrainRobAt;

	private int _lastTrainRobWhen = -1000000;

	private int _trainNearSince = -1;

	private int _trainsAtStation;

	private static readonly Dictionary<string, int> StartPurse = new Dictionary<string, int>
	{
		["odriscoll"] = 350,
		["lemoyneraiders"] = 300,
		["bronte"] = 900,
		["klan"] = 250,
		["wapiti"] = 120,
		["murfree"] = 120,
		["skinners"] = 160,
		["army"] = 6000,
		["laramie"] = 300,
		["delobo"] = 350,
		["nightfolk"] = 80
	};

	internal const int PurseBroke = 60;

	private void UpdateTrainRobbery(Ped player)
	{
		if (Game.GameTime < _nextTrainCheck)
		{
			return;
		}
		_nextTrainCheck = Game.GameTime + 1000;
		try
		{
			CheckTrainUnmasked(player);
		}
		catch (Exception ex)
		{
			GrudgeLog("TRAIN unmask check failed: " + ex.Message);
		}
		int num;
		try
		{
			num = LAW.GET_BOUNTY(Game.Player.Handle);
		}
		catch
		{
			return;
		}
		int trainBountyWas = _trainBountyWas;
		_trainBountyWas = num;
		if (Game.GameTime >= _nextTrainScan)
		{
			_nextTrainScan = Game.GameTime + 2000;
			_trainsNear.Clear();
			_trainsAtStation = 0;
			Vector3 position = player.Position;
			bool flag = false;
			try
			{
				flag = PED.IS_PED_IN_ANY_TRAIN(player.Handle);
			}
			catch
			{
			}
			try
			{
				Vehicle[] allVehicles = World.GetAllVehicles();
				foreach (Vehicle vehicle in allVehicles)
				{
					if (vehicle != null && vehicle.Exists() && Function.Call<bool>(18160986468102566260uL, new InputArgument[1] { ENTITY.GET_ENTITY_MODEL(vehicle.Handle) }) && vehicle.Position.DistanceTo(position) < 35f)
					{
						bool flag2 = false;
						try
						{
							flag2 = Function.Call<bool>(16755568960454759414uL, new InputArgument[1] { vehicle.Handle });
						}
						catch
						{
						}
						if (!flag2 || flag)
						{
							_trainsNear.Add(vehicle.Handle);
						}
						else
						{
							_trainsAtStation++;
						}
					}
				}
			}
			catch
			{
			}
		}
		if (_trainsNear.Count == 0)
		{
			_trainNearSince = -1;
		}
		else if (_trainNearSince < 0)
		{
			_trainNearSince = Game.GameTime;
		}
		if (trainBountyWas < 0 || num <= trainBountyWas)
		{
			return;
		}
		string text = TrainNotCounted(player);
		if (text != null)
		{
			if (_trainsNear.Count > 0 || _trainsAtStation > 0)
			{
				GrudgeLog($"TRAIN bounty +${(num - trainBountyWas) / 100} by a train - not a robbery: {text}");
			}
		}
		else
		{
			TrainRobberyCounted(player, num, trainBountyWas);
		}
	}

	private string TrainNotCounted(Ped player)
	{
		if (Game.GameTime - _lastFadeAt < 17000 || Game.GameTime - _lastJumpAt < 30000)
		{
			return "just after a fade / load / fast travel";
		}
		if (_trainsNear.Count == 0)
		{
			return "the only train near stands at its station (and you're not aboard)";
		}
		if (Game.GameTime - _trainNearSince < 15000)
		{
			return $"near the train only {(Game.GameTime - _trainNearSince) / 1000} s (15 s)";
		}
		if (Game.GameTime - _lastTrainRobWhen < 900000 && player.Position.DistanceTo(_lastTrainRobAt) < 300f)
		{
			return "the same robbery (15 min, 300 m)";
		}
		bool flag = false;
		try
		{
			flag = MISC.GET_MISSION_FLAG();
		}
		catch
		{
		}
		if (flag)
		{
			return "a mission";
		}
		foreach (int item in (from kv in _trainsRobbed
			where Game.GameTime - kv.Value > 900000
			select kv.Key).ToList())
		{
			_trainsRobbed.Remove(item);
		}
		if (_trainsNear.Any(_trainsRobbed.ContainsKey))
		{
			return "this train was robbed already";
		}
		return null;
	}

	private void TrainRobberyCounted(Ped player, int bounty, int was)
	{
		foreach (int item in _trainsNear.ToList())
		{
			_trainsRobbed[item] = Game.GameTime;
		}
		_lastTrainRobAt = player.Position;
		_lastTrainRobWhen = Game.GameTime;
		TrainRobbed(player, (bounty - was) / 100);
	}

	private void TrainRobbed(Ped player, int bountyUp)
	{
		Vector3 pos = player.Position;
		bool flag = false;
		try
		{
			flag = PlayerMasked(player);
		}
		catch
		{
		}
		string text = TownNear(pos) ?? NearestRepTown(pos);
		string zoneName = GetZoneName(pos);
		GrudgeLog(string.Format("TRAIN robbed near {0}{1} - bounty +${2}{3}", zoneName, (text != null) ? (" (" + text + ")") : "", bountyUp, flag ? ", masked" : ""));
		WorldEvent deed = null;
		try
		{
			deed = ChronicleDeed("train", flag ? ("A masked man robbed the train near " + zoneName) : ("You robbed the train near " + zoneName), "near " + zoneName, pos, null);
		}
		catch (Exception ex)
		{
			GrudgeLog("TRAIN deed failed: " + ex.Message);
		}
		if (flag)
		{
			_maskedTrain = new MaskedTrainJob
			{
				At = pos,
				When = Game.GameTime,
				Town = text,
				Place = zoneName,
				Deed = deed
			};
		}
		if (text != null)
		{
			Dictionary<string, TownTalk> dictionary = Chronicle.TownWord ?? (Chronicle.TownWord = new Dictionary<string, TownTalk>());
			if (!dictionary.TryGetValue(text, out var value))
			{
				value = (dictionary[text] = new TownTalk());
			}
			float word = value.Word;
			float num = (flag ? 6f : 24f);
			value.Word = Math.Min(100f, value.Word + num * (1f - value.Word / 100f));
			value.LastWordDay = GameDayKey();
			_townWordCache.Remove(text);
			_townRepCache.Remove(text);
			GrudgeLog(string.Format("TRAIN {0} talks of it: word {1:0} -> {2:0}{3}", text, word, value.Word, flag ? " (a masked man - they don't know it was you)" : ""));
			RepNote($"{text}: the train robbery - word {word:0} -> {value.Word:0}");
		}
		foreach (Gang item in Gangs.All.Where((Gang x) => !x.StoryOnly && !x.OrdersNotFeelings && !IsDestroyed(x) && Gangs.InEra(x) && NotorietyOf(x) > 0))
		{
			LowerNotoriety(item, 1, "you robbed a train", "train", 2);
		}
		Gang army = Gangs.Army;
		if (army != null && Gangs.InEra(army) && !IsDestroyed(army))
		{
			AddNotoriety(army, 3, "you robbed a train");
		}
		int num2 = 0;
		try
		{
			foreach (Ped item2 in (from p in PedsAround(player)
				where p != null && p.Exists() && !p.IsDead && p.IsHuman && !p.IsPlayer && !Kids.Protected(p) && p.Position.DistanceTo(pos) < 25f
				orderby p.Position.DistanceTo(pos)
				select p).Take(10).ToList())
			{
				bool flag2 = false;
				try
				{
					flag2 = PED.IS_PED_IN_ANY_TRAIN(item2.Handle);
				}
				catch
				{
				}
				if (flag2 || !(item2.Position.DistanceTo(pos) > 12f))
				{
					OnRobAttempt(item2.Handle);
					num2++;
				}
			}
		}
		catch (Exception ex2)
		{
			GrudgeLog("TRAIN passengers failed: " + ex2.Message);
		}
		GrudgeLog($"TRAIN {num2} aboard held up - a stick-up each");
		SaveWorld();
	}

	private void CheckTrainUnmasked(Ped player)
	{
		MaskedTrainJob maskedTrain = _maskedTrain;
		if (maskedTrain == null)
		{
			return;
		}
		if (Game.GameTime - maskedTrain.When > 600000)
		{
			_maskedTrain = null;
			return;
		}
		bool flag = true;
		try
		{
			flag = PlayerMasked(player);
		}
		catch
		{
		}
		if (flag || player.Position.DistanceTo(maskedTrain.At) > 50f)
		{
			return;
		}
		Vector3 me = player.Position;
		Ped ped = PedsAround(player).FirstOrDefault((Ped p) => p != null && p.Exists() && !p.IsDead && p.IsHuman && !p.IsPlayer && p.Position.DistanceTo(me) < 20f && ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY(p.Handle, player.Handle, 17));
		if (!(ped == null))
		{
			_maskedTrain = null;
			if (maskedTrain.Deed != null)
			{
				maskedTrain.Deed.Title = "You robbed the train near " + maskedTrain.Place;
				maskedTrain.Deed.Lines.Add("A masked man - until he took the mask off in front of them. They saw your face.");
			}
			if (maskedTrain.Town != null && Chronicle.TownWord != null && Chronicle.TownWord.TryGetValue(maskedTrain.Town, out var value))
			{
				float word = value.Word;
				value.Word = Math.Min(100f, value.Word + 18f * (1f - value.Word / 100f));
				_townWordCache.Remove(maskedTrain.Town);
				_townRepCache.Remove(maskedTrain.Town);
				GrudgeLog($"TRAIN {maskedTrain.Town}: they know it was you - word {word:0} -> {value.Word:0}");
			}
			Gang army = Gangs.Army;
			if (army != null && Gangs.InEra(army) && !IsDestroyed(army))
			{
				AddNotoriety(army, 2, "they saw your face after the train robbery");
			}
			SaveWorld();
			GrudgeLog($"TRAIN unmasked: someone saw your face {ped.Position.DistanceTo(me):0} m off, {player.Position.DistanceTo(maskedTrain.At):0} m from the robbery - the World tab says it was you");
			ReplyNotice("They saw your face. Word is out that you robbed that train.");
		}
	}

	private string NearestRepTown(Vector3 p)
	{
		string result = null;
		float num = float.MaxValue;
		foreach (string repTown in RepTowns)
		{
			if (TownCore(repTown, out var centre, out var _))
			{
				float num2 = new Vector3(p.X - centre.X, p.Y - centre.Y, 0f).Length();
				if (num2 < num)
				{
					num = num2;
					result = repTown;
				}
			}
		}
		if (!(num < 2500f))
		{
			return null;
		}
		return result;
	}

	private static int CapOf(Gang g)
	{
		if (g == null || !g.OrdersNotFeelings)
		{
			return 5000;
		}
		return 12000;
	}

	private double TaperOf(Gang g)
	{
		return Math.Max(0.2, 1.0 - (double)PurseOf(g) / ((g != null && g.OrdersNotFeelings) ? 10000.0 : 2000.0));
	}
}
