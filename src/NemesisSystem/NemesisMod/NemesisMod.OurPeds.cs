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
	private sealed class Landing
	{
		public Ped P;

		public Vector3 At;

		public int Born;

		public int FreedAt;

		public bool Frozen;

		public int Saves;
	}

	private const ulong BelongsToThisScript = 7073775680682537778uL;

	internal static int OwnCheckState;

	private static int _ownYes;

	private static int _ownNo;

	private static readonly Dictionary<int, uint> _ownSpawned = new Dictionary<int, uint>();

	internal static Action<Ped> GiveVoiceHook;

	private static readonly Dictionary<uint, bool> _varyModel = new Dictionary<uint, bool>();

	private static readonly Random _outfitRng = new Random();

	private static readonly List<Landing> _landing = new List<Landing>();

	internal static Action<string> LandingLog;

	private static readonly List<int> _ownGone = new List<int>();

	internal static readonly HashSet<int> WasOurs = new HashSet<int>();

	private readonly HashSet<int> _animalScriptSaid = new HashSet<int>();

	internal static double ModelWaitMs;

	private IEnumerable<(Ped p, OurRole role)> OurPeds()
	{
		if (_activeNemesisPed != null)
		{
			yield return (p: _activeNemesisPed, role: OurRole.Active);
		}
		bool missionOn = false;
		try
		{
			missionOn = MISC.GET_MISSION_FLAG();
		}
		catch
		{
		}
		foreach (Ped item in CompanionPeds())
		{
			if (!missionOn || !(item != null) || !IsCompanionReal(item.Handle))
			{
				yield return (p: item, role: OurRole.Companion);
			}
		}
		foreach (Ped follower in _followers)
		{
			yield return (p: follower, role: OurRole.Crew);
		}
		foreach (TownMan townMan in _townMen)
		{
			if (townMan.Ped != null)
			{
				yield return (p: townMan.Ped, role: OurRole.Town);
			}
		}
		foreach (Ped regular in _regulars)
		{
			yield return (p: regular, role: OurRole.Regular);
		}
		foreach (LiveCamp value in _liveCamps.Values)
		{
			foreach (Ped ped in value.Peds)
			{
				yield return (p: ped, role: OurRole.Camp);
			}
		}
		foreach (Raid raid in _raids)
		{
			foreach (Ped man in raid.Men)
			{
				yield return (p: man, role: OurRole.Patrol);
			}
		}
		foreach (Ped checkpointMan in CheckpointMen)
		{
			yield return (p: checkpointMan, role: OurRole.Patrol);
		}
		foreach (Ped supplyWagonMan in SupplyWagonMen)
		{
			yield return (p: supplyWagonMan, role: OurRole.Patrol);
		}
		foreach (Ped item2 in _skirmish)
		{
			yield return (p: item2, role: OurRole.Skirmish);
		}
		foreach (Ped saloonPed in SaloonPeds)
		{
			yield return (p: saloonPed, role: OurRole.Skirmish);
		}
		foreach (Ped survivor in _survivors)
		{
			yield return (p: survivor, role: OurRole.Survivor);
		}
		foreach (Ped hunter in _hunters)
		{
			yield return (p: hunter, role: OurRole.Hunter);
		}
		if (_general != null)
		{
			yield return (p: _general, role: OurRole.General);
		}
		foreach (Ped item3 in _genEscort)
		{
			yield return (p: item3, role: OurRole.General);
		}
		if (_rival != null)
		{
			if (_rival.Ped != null)
			{
				yield return (p: _rival.Ped, role: OurRole.Rival);
			}
			foreach (Ped man2 in _rival.Men)
			{
				yield return (p: man2, role: OurRole.Rival);
			}
		}
		if (_sightingPed != null)
		{
			yield return (p: _sightingPed, role: OurRole.Sighting);
		}
		foreach (Ped aftermathPed in _aftermathPeds)
		{
			yield return (p: aftermathPed, role: OurRole.Aftermath);
		}
	}

	internal OurRole RoleOf(int handle)
	{
		if (_roleFrame != Game.FrameCount)
		{
			_roleFrame = Game.FrameCount;
			_roleOf.Clear();
			foreach (var (ped, value) in OurPeds())
			{
				if (ped != null && !_roleOf.ContainsKey(ped.Handle))
				{
					_roleOf[ped.Handle] = value;
				}
			}
			foreach (int key in _followerOf.Keys)
			{
				if (!_roleOf.ContainsKey(key))
				{
					_roleOf[key] = OurRole.HiredGun;
				}
			}
			foreach (int key2 in _worldNemeses.Keys)
			{
				if (!_roleOf.ContainsKey(key2))
				{
					_roleOf[key2] = OurRole.WorldNemesis;
				}
			}
		}
		if (!_roleOf.TryGetValue(handle, out var value2))
		{
			return OurRole.None;
		}
		return value2;
	}

	internal bool IsOurs(int handle)
	{
		return RoleOf(handle) != OurRole.None;
	}

	internal bool OutfitFighter(int handle)
	{
		OurRole ourRole = RoleOf(handle);
		if (ourRole != OurRole.Patrol && ourRole != OurRole.Camp && ourRole != OurRole.Skirmish && ourRole != OurRole.Survivor && ourRole != OurRole.Aftermath && ourRole != OurRole.HiredGun)
		{
			return ourRole == OurRole.Hunter;
		}
		return true;
	}

	internal unsafe void DeleteOrRelease(Ped p)
	{
		if (!(p == null) && p.Exists())
		{
			if (SafeToDelete(p))
			{
				p.Delete();
				return;
			}
			int handle = p.Handle;
			ReleaseIfOurs(&handle);
		}
	}

	internal static int OwnRegistered => _ownSpawned.Count;

	internal static void MakeOurs(int h)
	{
		if (h != 0 && ENTITY.DOES_ENTITY_EXIST(h))
		{
			ENTITY.SET_ENTITY_AS_MISSION_ENTITY(h, p1: true, p2: true);
			_ownSpawned[h] = ENTITY.GET_ENTITY_MODEL(h);
		}
	}

	internal static Ped SpawnPed(PedHash model, Vector3 pos, float heading)
	{
		bool flag = false;
		if (!STREAMING.HAS_MODEL_LOADED((uint)model))
		{
			Model m = new Model((int)model);
			if (!m.IsValid || !m.IsPed)
			{
				return null;
			}
			RequestModel(m, 1500);
			flag = true;
			if (!m.IsLoaded)
			{
				return null;
			}
			if (MISC.GET_MISSION_FLAG() || CAM.IS_SCREEN_FADED_OUT() || CAM.IS_SCREEN_FADING_OUT())
			{
				STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)model);
				return null;
			}
		}
		Ped ped = World.CreatePed(model, pos, heading);
		if (flag)
		{
			STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)model);
		}
		if (ped != null && ped.Exists())
		{
			_ownSpawned[ped.Handle] = (uint)ped.Model.Hash;
			Land(ped, pos);
			try
			{
				GiveVoiceHook?.Invoke(ped);
			}
			catch
			{
			}
			VaryOutfit(ped);
		}
		return ped;
	}

	private static void VaryOutfit(Ped p)
	{
		try
		{
			uint m = (uint)p.Model.Hash;
			if (!_varyModel.TryGetValue(m, out var value))
			{
				value = (_varyModel[m] = Gangs.All.Any((Gang g) => g.TroopModels != null && g.TroopModels.Length == 1 && g.TroopModels[0] == (PedHash)m));
			}
			if (value)
			{
				int num = PED.GET_NUM_META_PED_OUTFITS(p.Handle);
				if (num > 1)
				{
					PED._EQUIP_META_PED_OUTFIT_PRESET(p.Handle, _outfitRng.Next(num), p2: false);
					Function.Call(14739335899491263887uL, p.Handle, false, true, true, true, false);
				}
			}
		}
		catch
		{
		}
	}

	private static void Land(Ped p, Vector3 at)
	{
		STREAMING.REQUEST_COLLISION_AT_COORD(at.X, at.Y, at.Z);
		if (p.IsHuman)
		{
			Landing landing = new Landing
			{
				P = p,
				At = at,
				Born = Game.GameTime,
				FreedAt = Game.GameTime
			};
			if (!ENTITY.HAS_COLLISION_LOADED_AROUND_ENTITY(p.Handle))
			{
				ENTITY.FREEZE_ENTITY_POSITION(p.Handle, toggle: true);
				landing.Frozen = true;
			}
			_landing.Add(landing);
		}
	}

	internal static void ClearLanding()
	{
		foreach (Landing item in _landing)
		{
			try
			{
				if (item.Frozen && item.P != null && item.P.Exists())
				{
					ENTITY.FREEZE_ENTITY_POSITION(item.P.Handle, toggle: false);
				}
			}
			catch
			{
			}
		}
		_landing.Clear();
	}

	internal static void TickLanding()
	{
		for (int num = _landing.Count - 1; num >= 0; num--)
		{
			Landing landing = _landing[num];
			if (landing.P == null || !landing.P.Exists() || landing.P.IsDead)
			{
				_landing.RemoveAt(num);
			}
			else
			{
				int handle = landing.P.Handle;
				if (landing.Frozen)
				{
					bool flag = ENTITY.HAS_COLLISION_LOADED_AROUND_ENTITY(handle);
					int num2 = INTERIOR.GET_INTERIOR_AT_COORDS(landing.At.X, landing.At.Y, landing.At.Z);
					if (num2 != 0 && !INTERIOR.IS_INTERIOR_READY(num2))
					{
						flag = false;
					}
					if (!flag)
					{
						STREAMING.REQUEST_COLLISION_AT_COORD(landing.At.X, landing.At.Y, landing.At.Z);
					}
					if (flag || landing.P.IsOnMount || PED.IS_PED_IN_ANY_VEHICLE(handle, atGetIn: false) || Game.GameTime - landing.Born > 30000)
					{
						ENTITY.FREEZE_ENTITY_POSITION(handle, toggle: false);
						landing.Frozen = false;
						landing.FreedAt = Game.GameTime;
						if (!flag)
						{
							LandingLog?.Invoke($"LANDING ped {handle}: no ground loaded at {landing.At.X:0},{landing.At.Y:0} after 30 s - let go anyway");
						}
					}
				}
				else if (Game.GameTime - landing.FreedAt > 15000)
				{
					_landing.RemoveAt(num);
				}
				else if (!landing.P.IsOnMount && !PED.IS_PED_IN_ANY_VEHICLE(handle, atGetIn: false) && !ENTITY.IS_ENTITY_ATTACHED(handle))
				{
					Vector3 position = landing.P.Position;
					if (!(ENTITY.GET_ENTITY_VELOCITY(handle, 0).Z > -7f) && !(position.Z > landing.At.Z - 4f) && landing.Saves < 2)
					{
						landing.Saves++;
						Vector3 safeCoordForPed = World.GetSafeCoordForPed(landing.At, onGround: true, 0);
						Vector3 at = (((safeCoordForPed.X != 0f || safeCoordForPed.Y != 0f) && new Vector3(safeCoordForPed.X - landing.At.X, safeCoordForPed.Y - landing.At.Y, 0f).Length() < 15f) ? safeCoordForPed : landing.At);
						ENTITY.SET_ENTITY_COORDS(handle, at.X, at.Y, at.Z + 0.3f, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
						ENTITY.SET_ENTITY_VELOCITY(handle, 0f, 0f, 0f);
						STREAMING.REQUEST_COLLISION_AT_COORD(at.X, at.Y, at.Z);
						ENTITY.FREEZE_ENTITY_POSITION(handle, toggle: true);
						landing.Frozen = true;
						landing.At = at;
						LandingLog?.Invoke($"LANDING ped {handle} ({Models.Name((uint)landing.P.Model.Hash)}): fell through the ground at {position.X:0},{position.Y:0},{position.Z:0} - put back at {at.X:0},{at.Y:0},{at.Z:0}");
					}
				}
			}
		}
	}

	internal static Vehicle SpawnVehicle(VehicleHash model, Vector3 pos, float heading)
	{
		Stopwatch stopwatch = (STREAMING.HAS_MODEL_LOADED((uint)model) ? null : Stopwatch.StartNew());
		Vehicle vehicle = World.CreateVehicle(model, pos, heading);
		if (stopwatch != null)
		{
			ModelWaitMs += stopwatch.Elapsed.TotalMilliseconds;
		}
		if (vehicle != null && vehicle.Exists())
		{
			_ownSpawned[vehicle.Handle] = (uint)vehicle.Model.Hash;
		}
		return vehicle;
	}

	private static bool Registered(int h)
	{
		if (_ownSpawned.TryGetValue(h, out var value))
		{
			return value == ENTITY.GET_ENTITY_MODEL(h);
		}
		return false;
	}

	internal static void PruneOwn()
	{
		_ownGone.Clear();
		foreach (int key in _ownSpawned.Keys)
		{
			if (!ENTITY.DOES_ENTITY_EXIST(key))
			{
				_ownGone.Add(key);
			}
		}
		foreach (int item in _ownGone)
		{
			_ownSpawned.Remove(item);
		}
	}

	internal static bool BelongsToUs(int h)
	{
		if (h == 0 || !ENTITY.DOES_ENTITY_EXIST(h))
		{
			return false;
		}
		if (!ENTITY.IS_ENTITY_A_MISSION_ENTITY(h))
		{
			return false;
		}
		if (OwnCheckState > 0)
		{
			try
			{
				return Function.Call<bool>(7073775680682537778uL, new InputArgument[2] { h, false });
			}
			catch
			{
			}
		}
		return Registered(h);
	}

	internal unsafe static void ReleaseIfOurs(int* h)
	{
		if (h == null || *h == 0 || !ENTITY.DOES_ENTITY_EXIST(*h) || !BelongsToUs(*h))
		{
			return;
		}
		int num = *h;
		Crumbs.Native("Ours release entity");
		ENTITY.SET_ENTITY_AS_NO_LONGER_NEEDED(h);
		_ownSpawned.Remove(num);
		if (WasOurs.Count > 4000)
		{
			WasOurs.Clear();
		}
		WasOurs.Add(num);
		if (ENTITY.IS_ENTITY_A_PED(num) && !ENTITY.IS_ENTITY_DEAD(num))
		{
			if (ReleasedAt.Count > 300)
			{
				ReleasedAt.Clear();
			}
			ReleasedAt[num] = Game.GameTime;
		}
	}

	private void CalibrateOwnCheck()
	{
		if (OwnCheckState != 0)
		{
			return;
		}
		foreach (var (ped, ourRole) in OurPeds())
		{
			if (!(ped == null) && ped.Exists() && ourRole != OurRole.Companion && ENTITY.IS_ENTITY_A_MISSION_ENTITY(ped.Handle) && Registered(ped.Handle))
			{
				bool flag;
				try
				{
					flag = Function.Call<bool>(7073775680682537778uL, new InputArgument[2] { ped.Handle, false });
				}
				catch
				{
					flag = false;
				}
				if (flag)
				{
					_ownYes++;
				}
				else
				{
					_ownNo++;
				}
				if (_ownYes > 0)
				{
					OwnCheckState = 1;
					GrudgeLog($"ENTITY ownership check works ({_ownYes} of ours read as ours, {_ownNo} not)");
					break;
				}
				if (_ownNo >= 6)
				{
					OwnCheckState = -1;
					GrudgeLog("ENTITY ownership check reads FALSE for our own peds - using the mod's own spawn registry instead");
					break;
				}
			}
		}
	}

	internal bool XRayHandsOff(int h)
	{
		if (MISC.GET_MISSION_FLAG())
		{
			return true;
		}
		if (IsCompanion(h) || IsCompanionReal(h))
		{
			return true;
		}
		Ped ped = Entity.FromHandle(h) as Ped;
		if (ped == null || !ped.Exists())
		{
			return true;
		}
		if (!IsStoryPed(ped))
		{
			return GameOwned(ped);
		}
		return true;
	}

	internal string XRayAnimalHandsOff(int h)
	{
		if (XRayHandsOff(h))
		{
			return "a mission's";
		}
		int handle = Game.Player.Character.Handle;
		if (h == PED.GET_MOUNT(handle) || h == PED._GET_LAST_MOUNT(handle) || h == PLAYER._GET_SADDLE_HORSE_FOR_PLAYER(Game.Player.Handle))
		{
			return "your horse";
		}
		Ped ped = Entity.FromHandle(h) as Ped;
		if (ped == null || !ped.Exists())
		{
			return "gone";
		}
		string text = Models.Name(ped);
		if (text.Contains("legendary"))
		{
			return "a legendary animal";
		}
		string text2 = null;
		try
		{
			text2 = ScriptOf(h);
		}
		catch
		{
		}
		if (text2 != null && text2.IndexOf("legend", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "a legendary animal";
		}
		if (text2 != null && _animalScriptSaid.Add(h))
		{
			GrudgeLog($"XRAY animal {text} (ped {h}) belongs to the game script '{text2}' - its X-ray verdict comes true all the same");
		}
		return null;
	}

	internal bool GameOwned(Ped p)
	{
		if (p == null || !p.Exists())
		{
			return false;
		}
		int handle = p.Handle;
		if (IsOurs(handle))
		{
			return false;
		}
		string text = Models.Name(p);
		if (text.StartsWith("msp_") || text.StartsWith("mes_") || text.StartsWith("rcsp_") || text.StartsWith("rces_"))
		{
			return true;
		}
		if (!MISC.GET_MISSION_FLAG() && !InStoryScene(p.Position))
		{
			return false;
		}
		if (ENTITY.IS_ENTITY_A_MISSION_ENTITY(handle))
		{
			return !BelongsToUs(handle);
		}
		return false;
	}

	internal bool MissionHeld(Ped p)
	{
		if (p == null || !p.Exists() || IsOurs(p.Handle) || !MISC.GET_MISSION_FLAG())
		{
			return false;
		}
		if (ENTITY.IS_ENTITY_A_MISSION_ENTITY(p.Handle))
		{
			return !BelongsToUs(p.Handle);
		}
		return false;
	}

	internal bool MissionMemoryOk(Ped p)
	{
		if (!MissionHeld(p) || IsStoryPed(p) || GameEventPed(p))
		{
			return false;
		}
		string text = Models.Name(p);
		if (text.StartsWith("msp_") || text.StartsWith("mes_") || text.StartsWith("rcsp_") || text.StartsWith("rces_"))
		{
			return false;
		}
		return Gangs.ForModel((uint)p.Model.Hash) != null;
	}

	internal static bool RallyModel(uint m)
	{
		return Models.Name(m).StartsWith("re_rally");
	}

	internal static bool RallyManFree(Ped p)
	{
		if (p != null && p.Exists() && RallyModel((uint)p.Model.Hash))
		{
			return !ENTITY.IS_ENTITY_A_MISSION_ENTITY(p.Handle);
		}
		return false;
	}

	internal static bool GameEventPed(Ped p)
	{
		if (p != null && p.Exists() && Models.Name(p).StartsWith("re_"))
		{
			return !Registered(p.Handle);
		}
		return false;
	}

	internal static bool RequestModel(Model m, int ms)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			return m.Request(ms);
		}
		finally
		{
			ModelWaitMs += stopwatch.Elapsed.TotalMilliseconds;
		}
	}

	internal bool LoadModelSafe(Model m, int ms)
	{
		if (!m.IsValid)
		{
			return false;
		}
		if (!m.IsLoaded)
		{
			RequestModel(m, ms);
		}
		if (!m.IsLoaded)
		{
			return false;
		}
		_nextBusyCheck = 0;
		Ped character = Game.Player.Character;
		if (character == null || !character.Exists() || WorldBusy(character) || MISC.GET_MISSION_FLAG())
		{
			GrudgeLog($"SPAWN skipped - the world went busy while model 0x{(uint)m.Hash:X8} loaded");
			return false;
		}
		return true;
	}

	internal bool IsOurs(Ped p)
	{
		if (p != null)
		{
			return RoleOf(p.Handle) != OurRole.None;
		}
		return false;
	}

	internal bool IsPlaced(int handle)
	{
		OurRole ourRole = RoleOf(handle);
		if (ourRole != OurRole.None)
		{
			return ourRole != OurRole.Regular;
		}
		return false;
	}
}
