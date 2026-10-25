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
	private int _graceUntil = -1;

	private string _graceWhy = "";

	private Vector3 _calmLastPos;

	private int _calmLastAt = -1;

	private bool _wasOnTrain;

	private bool _wasFaded;

	private int _nextCalmCheck;

	private string _calmReason;

	private string _calmPlaceReason;

	private int _nextGangScan;

	private bool _atGangCamp;

	private List<Vector3> _gangCamps;

	private HashSet<uint> _gangHashes;

	private bool _wasOnMission;

	private bool _mapHidden;

	private readonly List<(Vector3 pos, int at)> _campSeen = new List<(Vector3, int)>();

	private const ulong IsControlEnabledFn = 2083596516048037337uL;

	private bool _rhodesGunsBlocked;

	private int _rhodesBlockedSince = -1;

	private int _rhodesFreeSince = -1;

	private int _nextRhodesCheck;

	private int _rhodesProbeAt;

	private string _rhodesProbeWas;

	private int _gameEventNearAt = -100000;

	private int _nextGameEventCheck;

	private string _gameEventWho;

	private readonly Dictionary<int, int> _gameEventFirstSeen = new Dictionary<int, int>();

	private List<int> _sceneCandidates = new List<int>();

	private int _sceneQuietAt = -1;

	private Vector3 _sceneQuietPos;

	private List<int> _scenePeds = new List<int>();

	private string _sceneWho;

	private int _sceneUntil;

	private readonly List<Vector3> _scenePos = new List<Vector3>();

	private int _scenePosAt = -100000;

	private void UpdateCalm(Ped player)
	{
		if (_graceUntil < 0)
		{
			Grace(Config.CalmLoadGraceSeconds, "just loaded");
		}
		Vector3 position = player.Position;
		float num = ((_calmLastAt >= 0) ? position.DistanceTo(_calmLastPos) : 0f);
		float num2 = Math.Max(0.05f, (float)(Game.GameTime - _calmLastAt) / 1000f);
		if (_calmLastAt >= 0 && num > 200f && num / num2 > 120f && num2 < 600f)
		{
			Grace(Config.CalmGraceSeconds, "fast travel / a long jump");
			Guard("fast travel wipe", WipeWorldAfterJump);
		}
		_calmLastPos = position;
		_calmLastAt = Game.GameTime;
		bool mISSION_FLAG = MISC.GET_MISSION_FLAG();
		if (mISSION_FLAG && !_wasOnMission)
		{
			GrudgeLog("CALM a mission started - everything the mod had out is cleared");
			Guard("companions go home", delegate
			{
				LetCompanionsGo("a mission");
			});
			Guard("hideout doors", delegate
			{
				RelockAllHideouts(missionStart: true);
			});
			Guard("mission wipe", delegate
			{
				WipeWorldAfterRespawn(respawn: false, "MISSION");
			});
		}
		if (mISSION_FLAG != _mapHidden)
		{
			_mapHidden = mISSION_FLAG;
			GrudgeLog(mISSION_FLAG ? "MAP the mod's icons hidden for the mission" : "MAP the mission's over - the mod's icons back");
			Guard("map icons", MapIconsForMission);
		}
		_wasOnMission = mISSION_FLAG;
		bool flag = PED.IS_PED_IN_ANY_TRAIN(player.Handle);
		if (_wasOnTrain && !flag)
		{
			Grace(Config.CalmGraceSeconds, "off the train");
		}
		_wasOnTrain = flag;
		bool flag2 = CAM.IS_SCREEN_FADED_OUT();
		if (_wasFaded && !flag2)
		{
			Grace(Config.CalmGraceSeconds / 2, "after a fade (sleep, load)");
		}
		_wasFaded = flag2;
	}

	internal void ForgetSafeCamps()
	{
		Config.GangCamps = "";
		_gangCamps = null;
		_campSeen.Clear();
		Config.Save();
		GrudgeLog("CALM safe camps forgotten");
	}

	private List<Vector3> GangCamps
	{
		get
		{
			if (_gangCamps != null)
			{
				return _gangCamps;
			}
			_gangCamps = new List<Vector3>();
			string[] array = (Config.GangCamps ?? "").Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(',');
				if (array2.Length >= 2 && float.TryParse(array2[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && float.TryParse(array2[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
				{
					Vector3 vector = new Vector3(result, result2, 0f);
					if (InsideAnyTown(vector))
					{
						GrudgeLog($"CALM dropped a gang camp inside a town at {result:0},{result2:0} (Arthur's gang walking around town isn't a camp)");
					}
					else
					{
						_gangCamps.Add(vector);
					}
				}
			}
			string text = string.Join(";", _gangCamps.Select((Vector3 c) => c.X.ToString("0", CultureInfo.InvariantCulture) + "," + c.Y.ToString("0", CultureInfo.InvariantCulture)));
			if (text != (Config.GangCamps ?? ""))
			{
				Config.GangCamps = text;
				Config.Save();
			}
			return _gangCamps;
		}
	}

	internal string StoryBansWeapons(Vector3 pos)
	{
		if (!(Models.Name(Game.Player.Character) == "player_three") && !(TownAt(pos) != "Rhodes"))
		{
			if (!_rhodesGunsBlocked)
			{
				if (StoryChapter != 3)
				{
					return null;
				}
				return "in Rhodes (chapter 3: the story has your guns put away there)";
			}
			return "in Rhodes (the story has your guns put away there)";
		}
		return null;
	}

	private void RhodesBanTick(Ped player)
	{
		if (Game.GameTime < _nextRhodesCheck)
		{
			return;
		}
		_nextRhodesCheck = Game.GameTime + 250;
		if (TownAt(player.Position) != "Rhodes" || Models.Name(player) == "player_three")
		{
			_rhodesBlockedSince = (_rhodesFreeSince = -1);
			_rhodesProbeWas = null;
			return;
		}
		if (_convo != null || _tg != null || _menu.IsOpen || _bulletImpact.ViewerOpen || _iniPanel.IsOpen || _welcome.IsOpen || Game.GameTime - _convoClosedAt < 1500 || Game.GameTime - _panelClosedAt < 1500 || player.IsDead || MISC.GET_MISSION_FLAG() || !PLAYER.IS_PLAYER_CONTROL_ON(Game.Player.Handle) || INTERIOR.GET_INTERIOR_FROM_ENTITY(player.Handle) != 0)
		{
			_rhodesBlockedSince = (_rhodesFreeSince = -1);
			return;
		}
		bool flag = On(eInputType.Aim);
		bool flag2 = On(eInputType.Attack);
		bool flag3 = On(eInputType.OpenWheelMenu);
		bool flag4 = On(eInputType.ToggleHolster);
		bool flag5 = WEAPON._GET_PED_CURRENT_HELD_WEAPON(player.Handle) != 2725352035u;
		string text = "aim " + (flag ? "on" : "OFF") + ", shoot " + (flag2 ? "on" : "OFF") + ", weapon wheel " + (flag3 ? "on" : "OFF") + ", holster " + (flag4 ? "on" : "OFF") + ", " + (flag5 ? "a gun in hand" : "empty-handed");
		if (text != _rhodesProbeWas && Game.GameTime - _rhodesProbeAt > 2000)
		{
			_rhodesProbeWas = text;
			_rhodesProbeAt = Game.GameTime;
			GrudgeLog("SAFETOWN probe in Rhodes: " + text + (_rhodesGunsBlocked ? " (the ban is on)" : ""));
		}
		if (!flag3 || (!flag && !flag2))
		{
			_rhodesFreeSince = -1;
			if (_rhodesBlockedSince < 0)
			{
				_rhodesBlockedSince = Game.GameTime;
			}
			if (!_rhodesGunsBlocked && Game.GameTime - _rhodesBlockedSince >= 2000)
			{
				_rhodesGunsBlocked = true;
				GrudgeLog("SAFETOWN the game is keeping your guns away in Rhodes (" + text + ") - nothing new starts here");
			}
		}
		else if (flag && flag2 && flag3 && flag4)
		{
			_rhodesBlockedSince = -1;
			if (_rhodesFreeSince < 0)
			{
				_rhodesFreeSince = Game.GameTime;
			}
			if (_rhodesGunsBlocked && Game.GameTime - _rhodesFreeSince >= 10000)
			{
				_rhodesGunsBlocked = false;
				GrudgeLog("SAFETOWN the game gives you your guns back in Rhodes - the ban is lifted");
			}
		}
		else
		{
			_rhodesBlockedSince = (_rhodesFreeSince = -1);
		}
		static bool On(eInputType c)
		{
			try
			{
				return Function.Call<bool>(2083596516048037337uL, new InputArgument[2]
				{
					0,
					(uint)c
				});
			}
			catch
			{
				return true;
			}
		}
	}

	private bool InsideAnyTown(Vector3 p)
	{
		foreach (string key in TownSpots.Keys)
		{
			if (TownCore(key, out var centre, out var radius) && new Vector3(p.X - centre.X, p.Y - centre.Y, 0f).Length() < radius + 40f)
			{
				return true;
			}
		}
		return false;
	}

	private bool AtGangCamp(Ped player)
	{
		Vector3 pos = player.Position;
		foreach (Vector3 gangCamp in GangCamps)
		{
			if (new Vector3(pos.X - gangCamp.X, pos.Y - gangCamp.Y, 0f).Length() < Config.CalmCampRadius)
			{
				return true;
			}
		}
		if (Game.GameTime < _nextGangScan)
		{
			return _atGangCamp;
		}
		_nextGangScan = Game.GameTime + 2000;
		if (_gangHashes == null)
		{
			_gangHashes = new HashSet<uint>(CampKeyOfModel.Keys);
		}
		int num = 0;
		foreach (Ped item2 in PedsAround(player))
		{
			if (!(item2 == null) && item2.Exists() && !item2.IsDead && !item2.IsPlayer && _gangHashes.Contains((uint)item2.Model.Hash) && !IsOurs(item2.Handle) && item2.Position.DistanceTo(pos) < 70f)
			{
				num++;
			}
		}
		_atGangCamp = num >= 2 && !InsideAnyTown(pos);
		if (_atGangCamp && !MISC.GET_MISSION_FLAG() && !GangCamps.Any((Vector3 c) => new Vector3(pos.X - c.X, pos.Y - c.Y, 0f).Length() < 300f))
		{
			(Vector3, int) item = _campSeen.FirstOrDefault(((Vector3 pos, int at) c) => new Vector3(pos.X - c.pos.X, pos.Y - c.pos.Y, 0f).Length() < 300f);
			if (item.Item2 == 0)
			{
				_campSeen.Add((new Vector3(pos.X, pos.Y, 0f), Game.GameTime));
				GrudgeLog($"CALM gang seen at {pos.X:0},{pos.Y:0} - a camp if they're here again later");
				return _atGangCamp;
			}
			if (Game.GameTime - item.Item2 < 600000)
			{
				return _atGangCamp;
			}
			_campSeen.Remove(item);
			if (GangCamps.Count >= 8)
			{
				GangCamps.RemoveAt(0);
			}
			GangCamps.Add(new Vector3(pos.X, pos.Y, 0f));
			Config.GangCamps = string.Join(";", GangCamps.Select((Vector3 c) => c.X.ToString("0", CultureInfo.InvariantCulture) + "," + c.Y.ToString("0", CultureInfo.InvariantCulture)));
			Config.Save();
			GrudgeLog($"CALM gang camp remembered at {pos.X:0},{pos.Y:0} ({GangCamps.Count} known)");
		}
		return _atGangCamp;
	}

	private bool LawAfterYou()
	{
		int handle = Game.Player.Handle;
		try
		{
			return LAW.IS_LAW_INCIDENT_ACTIVE(handle) || PLAYER.GET_PLAYER_WANTED_LEVEL(handle) > 0;
		}
		catch
		{
			return false;
		}
	}

	internal bool GameEventGunfire => Game.GameTime - _gameEventNearAt < 4000;

	internal bool FiredInAnger
	{
		get
		{
			if (GunMelee.PlayerFiredRecently)
			{
				return !GameEventGunfire;
			}
			return false;
		}
	}

	private void CheckGameEventNear(Ped player)
	{
		if (Game.GameTime < _nextGameEventCheck)
		{
			return;
		}
		_nextGameEventCheck = Game.GameTime + 500;
		Vector3 position = player.Position;
		foreach (var (ped, vector) in PedSnapshot(player))
		{
			if (!(ped == null) && !(vector.DistanceTo(position) > 100f) && GameEventPed(ped) && !ped.IsDead)
			{
				int handle = ped.Handle;
				if (!_gameEventFirstSeen.TryGetValue(handle, out var value))
				{
					value = (_gameEventFirstSeen[handle] = Game.GameTime);
					GrudgeLog($"GAMEEVENT {NameOf(handle)} ({Models.Name(ped)}) {vector.DistanceTo(position):0} m off - no encounters of ours, and your shots aren't taken as an attack, while he's near (5 min at most)");
				}
				if (Game.GameTime - value <= 300000)
				{
					_gameEventNearAt = Game.GameTime;
					_gameEventWho = NameOf(handle);
				}
			}
		}
		if (_gameEventFirstSeen.Count <= 50)
		{
			return;
		}
		foreach (int item in (from kv in _gameEventFirstSeen
			where Game.GameTime - kv.Value > 600000
			select kv.Key).ToList())
		{
			_gameEventFirstSeen.Remove(item);
		}
	}

	internal string NoSpawnReason(Ped player, bool place = false)
	{
		try
		{
			CheckGameEventNear(player);
		}
		catch (Exception ex)
		{
			GrudgeLog("GAMEEVENT check failed: " + ex.Message);
		}
		if (!Config.CalmEnabled)
		{
			return null;
		}
		if (Game.GameTime >= _nextCalmCheck)
		{
			_nextCalmCheck = Game.GameTime + 500;
			string text = null;
			if (MISC.GET_MISSION_FLAG())
			{
				text = "on a mission";
			}
			else
			{
				string text2 = StoryScene(player);
				if (text2 != null)
				{
					text = text2;
				}
				else if (Game.GameTime < _graceUntil)
				{
					text = $"settling in ({(_graceUntil - Game.GameTime) / 1000}s, {_graceWhy})";
				}
				else if (PED.IS_PED_IN_ANY_TRAIN(player.Handle))
				{
					text = "on a train";
				}
				else if (AtGangCamp(player))
				{
					text = "at the gang's camp";
				}
				else
				{
					CampSite campSite = Camps.RetiredNear(player.Position, 300f);
					if (campSite != null)
					{
						text = "on " + campSite.Name + " (the game puts your guns away there)";
					}
					else
					{
						string text3 = StoryBansWeapons(player.Position);
						if (text3 != null)
						{
							text = text3;
						}
					}
				}
			}
			_calmPlaceReason = text;
			if (text == null)
			{
				if (INTERIOR.GET_INTERIOR_FROM_ENTITY(player.Handle) != 0)
				{
					text = "indoors";
				}
				else if (PED.IS_PED_USING_ANY_SCENARIO(player.Handle))
				{
					text = "resting (sitting / at a fire)";
				}
				else if (Config.CalmWhileWanted && LawAfterYou())
				{
					text = "the law is after you";
				}
				else if (Game.GameTime - _gameEventNearAt < 3000)
				{
					text = "one of the game's own events nearby (" + _gameEventWho + ")";
				}
			}
			_calmReason = text;
		}
		if (!place)
		{
			return _calmReason;
		}
		return _calmPlaceReason;
	}

	internal void NoteQuietForScene(Ped player, bool quiet)
	{
		try
		{
			if (quiet)
			{
				_sceneCandidates.Clear();
				_sceneQuietAt = Game.GameTime;
				_sceneQuietPos = player.Position;
				{
					foreach (var (ped, vector) in PedSnapshot(player))
					{
						if (!(ped == null) && ped.Exists() && ped.Handle != player.Handle && !(vector.DistanceTo(player.Position) > 30f) && Models.Name(ped).StartsWith("cs_", StringComparison.OrdinalIgnoreCase) && ENTITY.IS_ENTITY_A_MISSION_ENTITY(ped.Handle) && !BelongsToUs(ped.Handle) && !IsCompanion(ped.Handle))
						{
							_sceneCandidates.Add(ped.Handle);
						}
					}
					return;
				}
			}
			if (_sceneQuietAt >= 0)
			{
				int num = Game.GameTime - _sceneQuietAt;
				_sceneQuietAt = -1;
				if (num >= 6000 && _sceneCandidates.Count != 0 && !(player.Position.DistanceTo(_sceneQuietPos) > 100f))
				{
					_scenePeds = _sceneCandidates.ToList();
					Ped ped2 = Entity.FromHandle(_scenePeds[0]) as Ped;
					_sceneWho = ((!(ped2 != null)) ? "a story character" : (_named.TryGetValue(ped2.Handle, out var value) ? value.Name : Models.Name(ped2)));
					_sceneUntil = Game.GameTime + 900000;
					GrudgeLog($"CALM a story scene with {_sceneWho} ({num / 1000} s cutscene, no mission flag - a stranger mission?) - nothing new comes while he's about");
				}
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("CALM story scene check failed: " + ex.Message);
		}
	}

	internal bool InStoryScene(Vector3 at)
	{
		if (_scenePeds.Count == 0 || Game.GameTime >= _sceneUntil)
		{
			return false;
		}
		if (Game.GameTime - _scenePosAt > 500)
		{
			_scenePosAt = Game.GameTime;
			_scenePos.Clear();
			foreach (int scenePed in _scenePeds)
			{
				Entity entity = Entity.FromHandle(scenePed);
				if ((object)entity != null && entity.Exists() && !entity.IsDead)
				{
					_scenePos.Add(entity.Position);
				}
			}
		}
		return _scenePos.Any((Vector3 s) => s.DistanceTo(at) < 150f);
	}

	private string StoryScene(Ped player)
	{
		if (_scenePeds.Count == 0)
		{
			return null;
		}
		if (Game.GameTime >= _sceneUntil || !_scenePeds.Any(delegate(int h)
		{
			Entity entity = Entity.FromHandle(h);
			return entity != null && entity.Exists() && !entity.IsDead && entity.Position.DistanceTo(player.Position) < 200f;
		}))
		{
			GrudgeLog("CALM the story scene with " + _sceneWho + " is over");
			_scenePeds.Clear();
			return null;
		}
		return "a story scene (" + _sceneWho + ")";
	}

	internal bool CalmIsWanted => _calmReason == "the law is after you";

	internal string CalmDebugLine(Ped player)
	{
		string text = NoSpawnReason(player);
		if (text != null)
		{
			return $"HELD: {text}  ({GangCamps.Count} gang camps known)";
		}
		return $"calm: nothing holding them back ({GangCamps.Count} gang camps known)";
	}
}
