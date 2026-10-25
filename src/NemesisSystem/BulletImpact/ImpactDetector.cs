using System;
using System.Collections.Generic;
using System.Linq;
using RDR2;
using RDR2.Math;
using RDR2.Native;

namespace NemesisSystem.BulletImpact;

internal sealed class ImpactDetector
{
	private sealed class Hold
	{
		public int Since;

		public int Hp0;

		public int HpLow;

		public int Struck;

		public int Ticks;

		public int Other;

		public int LastPunchAt = -1073741824;

		public bool Squeezed;

		public bool KilledByOther;

		public bool Grappled;
	}

	private static readonly string[] MuzzleBoneNames = new string[3] { "gun_muzzle", "muzzle", "Muzzle" };

	private const int WeaponAttachHandPrimary = 0;

	private const int HeadBoneId = 21030;

	private const int ShotWindowMs = 150;

	private const ulong GetNumberOfBulletsImpactedEntity = 10881604826085319960uL;

	private int _shotId;

	private int _lastShotTime = -100000;

	private const int RightHandBone = 22798;

	private const int ChestBone = 14413;

	private readonly Queue<(int t, Vector3 p)> _blade = new Queue<(int, Vector3)>();

	private uint _lastShotWeapon;

	private static readonly Dictionary<uint, bool> _fourLegged = new Dictionary<uint, bool>();

	private static readonly string[] Birds = new string[40]
	{
		"chicken", "rooster", "turkey", "duck", "goose", "swan", "eagle", "hawk", "owl", "crow",
		"raven", "pigeon", "dove", "songbird", "sparrow", "robin", "cardinal", "bluejay", "oriole", "finch",
		"waxwing", "warbler", "kingfisher", "woodpecker", "heron", "egret", "pelican", "spoonbill", "crane", "loon",
		"gull", "cormorant", "vulture", "condor", "parrot", "parakeet", "quail", "pheasant", "grouse", "bird"
	};

	private static readonly string[] NotAnimals = new string[16]
	{
		"fish", "trout", "salmon", "muskie", "sturgeon", "snake", "turtle", "toad", "frog", "lizard",
		"iguana", "gilamonster", "lobster", "crawfish", "squirrel", "chipmunk"
	};

	private readonly Dictionary<int, int> _damagedAt = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _areaAt = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _blowAt = new Dictionary<int, int>();

	private const int LeftHandBone = 34606;

	private readonly Queue<(int t, Vector3 l, Vector3 r, Vector3 w, Vector3 lf, Vector3 rf)> _fists = new Queue<(int, Vector3, Vector3, Vector3, Vector3, Vector3)>();

	private static readonly int[] StrikeBones = Anatomy.SpineChain.Concat(Anatomy.LimbChains.SelectMany((int[] c) => c)).Distinct().ToArray();

	private readonly Dictionary<int, int> _grabbedSince = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _lyingAt = new Dictionary<int, int>();

	private const int DownChainMs = 4000;

	private readonly Dictionary<int, int> _struckAt = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _stompAt = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _stompNoteAt = new Dictionary<int, int>();

	private const float StompSpeed = 1.5f;

	private const float StompLift = 0.2f;

	private const float StompReach = 0.35f;

	private const int StompGapMs = 450;

	private const ulong GetPedTimeOfDeathFn = 2204654383702665610uL;

	private readonly Dictionary<int, string> _downState = new Dictionary<int, string>();

	internal const float GroundBlowMax = 4.5f;

	private const int KillStompMs = 3000;

	private const int KillGraceMs = 1500;

	private const int StillOutMs = 20000;

	private readonly Dictionary<int, int> _koDiedAt = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _todMovedAt = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _koStomps = new Dictionary<int, int>();

	private readonly HashSet<int> _stillOutNoted = new HashSet<int>();

	private const float SaddleKickReach = 0.9f;

	private const float SaddleKickSpeed = 4.5f;

	private const float ChokeTickDamage = 1.5f;

	private const int ChokeTicks = 3;

	private const float ChokeHands = 0.3f;

	private const float TakedownGrabDamage = 6f;

	private const float TakedownDamage = 30f;

	private const float TakedownHands = 0.45f;

	private readonly Dictionary<int, Hold> _holds = new Dictionary<int, Hold>();

	private const int HoldChokeMinDrop = 15;

	private readonly Dictionary<int, (int hp, int last)> _beaten = new Dictionary<int, (int, int)>();

	private readonly HashSet<uint> _unknownLogged = new HashSet<uint>();

	private static uint _hExplosion;

	private static uint _hFire;

	private static uint _hMolotov;

	private static uint _hDynamite;

	private readonly Dictionary<int, (int at, uint weapon)> _pendingImpact = new Dictionary<int, (int, uint)>();

	private int _playerShotAt = -100000;

	private uint _playerShotWeapon;

	private const float ExecutionShare = 0.6f;

	private const float ExecutionReach = 0.25f;

	private const int ExecutionLookMs = 500;

	private static string _lastAmmoKey;

	internal static Action<string> AmmoNote;

	private const ulong IsEntityAVehicleFn = 14112428505153989708uL;

	private const ulong VehiclePedIsUsingFn = 6959377544440096893uL;

	private const ulong CurrentVehicleWeaponFn = 1159492374221042396uL;

	private const ulong TurretSeatFn = 18399335019481672262uL;

	private static readonly string[] GunMuzzleBones = new string[7] { "gun_muzzle", "muzzle", "Muzzle", "turret_1barrel", "turret_barrel", "gun_barrel", "barrel" };

	private int _gunOthers;

	private readonly HashSet<string> _gunNoted = new HashSet<string>();

	public event Action<ImpactRecord> Impact;

	public event Action<string> Diagnostic;

	public event Action<int, bool> KilledOutCold;

	public unsafe void Poll(Ped player)
	{
		TrackBlade(player);
		TrackFists(player);
		if (PED.IS_PED_SHOOTING(player.Handle))
		{
			uint playerShotWeapon = 0u;
			if (WEAPON.GET_CURRENT_PED_WEAPON(player.Handle, &playerShotWeapon, p2: true, 0, p4: true))
			{
				_playerShotAt = Game.GameTime;
				_playerShotWeapon = playerShotWeapon;
			}
		}
		int num = SCRIPTS.GET_NUMBER_OF_EVENTS(0);
		ulong* ptr = stackalloc ulong[9];
		int num2 = 0;
		List<int> list = new List<int>();
		int num3 = -1;
		uint weapon = 0u;
		for (int i = 0; i < num; i++)
		{
			switch (SCRIPTS.GET_EVENT_AT_INDEX(0, i))
			{
			case 2787876538u:
				*ptr = 0uL;
				if (SCRIPTS.GET_EVENT_DATA(0, i, ptr, 1))
				{
					num2++;
					list.Add((int)(*ptr));
				}
				break;
			case 402722103u:
			{
				for (int j = 0; j < 9; j++)
				{
					ptr[j] = 0uL;
				}
				if (!SCRIPTS.GET_EVENT_DATA(0, i, ptr, 9))
				{
					break;
				}
				if (num3 < 0)
				{
					num3 = MountedGun(player, out weapon);
				}
				bool flag = num3 > 0 && ((int)ptr[1] == num3 || (int)ptr[1] == player.Handle);
				if ((int)ptr[1] != player.Handle && !flag)
				{
					if (num3 > 0 && (uint)ptr[1] != 0 && _gunOthers < 3 && !ENTITY.IS_ENTITY_A_PED((int)ptr[1]) && ENTITY.IS_ENTITY_A_PED((int)(*ptr)))
					{
						_gunOthers++;
						AmmoNote?.Invoke($"MOUNTED GUN {ModelName(num3)}: ped={(int)(*ptr)} hit by entity {(int)ptr[1]} ({ModelName((int)ptr[1])}) weapon {WeaponText((uint)ptr[2])} - not yours, not counted");
					}
					if ((uint)ptr[1] != 0 && (uint)ptr[1] != (uint)(*ptr))
					{
						StrayHit((int)(*ptr), (int)ptr[1], (uint)ptr[2], *(float*)(ptr + 4));
					}
					break;
				}
				uint num4 = (uint)ptr[2];
				if (num4 == 0)
				{
					break;
				}
				bool flag2 = flag && (int)ptr[1] == num3;
				WeaponClass? weaponClass = BladeOf(num4);
				WeaponClass? weaponClass2 = ((!weaponClass.HasValue) ? AreaOf(num4) : ((WeaponClass?)null));
				bool flag3 = !flag2 && !weaponClass.HasValue && !weaponClass2.HasValue && WEAPON.GET_WEAPONTYPE_GROUP(num4) == 2685387236u;
				if (!weaponClass.HasValue && !weaponClass2.HasValue && !flag3 && !IsProjectileWeapon(num4))
				{
					if (_unknownLogged.Add(num4))
					{
						Action<string> action = this.Diagnostic;
						if (action != null)
						{
							object arg = num4;
							object arg2;
							if (!Enum.IsDefined(typeof(eWeapon), num4))
							{
								arg2 = "?";
							}
							else
							{
								eWeapon eWeapon = (eWeapon)num4;
								arg2 = eWeapon.ToString();
							}
							action($"DAMAGE by you with weapon {arg:X8} ({arg2}) - not X-rayed");
						}
					}
					if (flag && _gunNoted.Add($"unknown {num4:X8}"))
					{
						AmmoNote?.Invoke("MOUNTED GUN " + ModelName(num3) + ": a hit with weapon " + WeaponText(num4) + " (the gun's " + WeaponText(weapon) + ") - not X-rayed, the X-ray doesn't know it");
					}
					break;
				}
				bool flag4 = !flag2 && !weaponClass.HasValue && !weaponClass2.HasValue && !flag3 && GunMelee.IsGunMeleeHit(player, (int)(*ptr), num4);
				if (flag && _gunNoted.Add(string.Format("{0}/{1:X8}/{2}", ENTITY.GET_ENTITY_MODEL(num3), num4, ((int)ptr[1] == num3) ? "gun" : "you")))
				{
					AmmoNote?.Invoke("MOUNTED GUN " + ModelName(num3) + ": your hit - the game says it came from " + (((int)ptr[1] == num3) ? "the gun" : "you") + ", weapon " + WeaponText(num4) + (weaponClass2.HasValue ? $" ({weaponClass2})" : "") + " - X-rayed");
				}
				if (!flag3 && !flag4)
				{
					EndHold((int)(*ptr), weaponClass.HasValue ? "cut" : (weaponClass2.HasValue ? "burned / blown up" : "shot"));
				}
				Ped ped = Entity.FromHandle((int)(*ptr)) as Ped;
				bool flag5 = ped != null && !ped.IsHuman && !ped.IsPlayer;
				if (flag5)
				{
					AnimalBones.Probe(ped, AmmoNote);
					if (!FourLegged(ped))
					{
						break;
					}
				}
				else if (ped == null || !ped.IsHuman || ped.IsPlayer || Kids.IsChild(ped))
				{
					break;
				}
				if (flag3 || flag4)
				{
					if (flag5 || (_blowAt.TryGetValue(ped.Handle, out var value) && Game.GameTime - value < 250))
					{
						break;
					}
					(int bone, float gap, bool foot) tuple = Reach(ped);
					int item = tuple.bone;
					float item2 = tuple.gap;
					bool item3 = tuple.foot;
					float num5 = *(float*)(ptr + 4);
					bool flag6 = false;
					if (flag3 && PED.IS_PED_ON_MOUNT(player.Handle))
					{
						int num6 = PED.GET_MOUNT(player.Handle);
						float num7 = ((num6 != 0) ? ENTITY.GET_ENTITY_SPEED(num6) : 0f);
						flag6 = item3 && item2 < 0.9f && num7 < 4.5f;
						AmmoNote?.Invoke(string.Format("MOUNTED HIT ped={0} dmg={1:0.#} boot={2} gap={3:0.00} horse {4:0.0} m/s melee={5} - {6}", ped.Handle, num5, item3, item2, num7, PED.IS_PED_IN_MELEE_COMBAT(player.Handle), flag6 ? "a kick from the saddle" : "not a kick (ridden into / no boot near)"));
						if (!flag6)
						{
							_blowAt[ped.Handle] = Game.GameTime;
							break;
						}
					}
					else if (flag3 && item2 > 0.35f && Bumped(player))
					{
						break;
					}
					string text = NotABlow(ped, num5);
					if (text == null && flag3 && !_holds.ContainsKey(ped.Handle) && PED.GET_PED_IS_BEING_GRAPPLED(ped.Handle))
					{
						_holds[ped.Handle] = new Hold
						{
							Since = Game.GameTime,
							Hp0 = ped.Health + (int)num5,
							HpLow = ped.Health
						};
					}
					if (text == null && flag3 && (!item3 || !(item2 < 0.35f)) && !Held(ped.Handle) && (num5 < 6f || (ped.IsDead && num5 >= 30f)))
					{
						(bool behind, float hands) tuple2 = Grip(player, ped);
						bool item4 = tuple2.behind;
						float item5 = tuple2.hands;
						bool flag7 = (item4 && item5 < 0.45f) || item5 < 0.3f;
						if (flag7)
						{
							bool num8 = ped.IsDead && num5 >= 30f;
							if (!_holds.TryGetValue(ped.Handle, out var value2))
							{
								Dictionary<int, Hold> holds = _holds;
								int handle = ped.Handle;
								Hold obj = new Hold
								{
									Since = Game.GameTime,
									Hp0 = ped.Health + (int)num5,
									HpLow = ped.Health
								};
								value2 = obj;
								holds[handle] = obj;
							}
							if (num8)
							{
								value2.Squeezed = true;
							}
							text = (num8 ? "the choke (a takedown)" : "the grab (from behind)");
						}
						AmmoNote?.Invoke(string.Format("TAKEDOWN? ped={0} dmg={1:0.#} behind={2} hands {3:0.00} m from his neck out={4} - {5}", ped.Handle, num5, item4, item5, ped.IsDead, flag7 ? text : "a punch"));
					}
					if (text == null && flag3 && Held(ped.Handle))
					{
						Hold hold = _holds[ped.Handle];
						(bool behind, float hands) tuple3 = Grip(player, ped);
						bool item6 = tuple3.behind;
						float item7 = tuple3.hands;
						string text2 = HoldHitKind(num5, ped.IsDead, hold.Ticks);
						if (text2 == "a squeeze" && ++hold.Ticks >= 3)
						{
							hold.Squeezed = true;
						}
						AmmoNote?.Invoke($"HOLD HIT ped={ped.Handle} dmg={num5:0.#} behind={item6} hands {item7:0.00} m from his neck - {text2}");
						if (text2 != "a punch")
						{
							text = "part of the hold (the choke)";
						}
					}
					if (text != null)
					{
						AmmoNote?.Invoke($"NOT A BLOW ped={ped.Handle} dmg={num5:0.#} - {text}");
						_blowAt[ped.Handle] = Game.GameTime;
						if (text == "the grab" && !_holds.ContainsKey(ped.Handle))
						{
							_holds[ped.Handle] = new Hold
							{
								Since = Game.GameTime,
								Hp0 = ped.Health + (int)num5,
								HpLow = ped.Health
							};
						}
						break;
					}
					_blowAt[ped.Handle] = Game.GameTime;
					if (_holds.TryGetValue(ped.Handle, out var value3))
					{
						value3.Struck += (int)num5;
						value3.LastPunchAt = Game.GameTime;
					}
					ImpactRecord impactRecord = CaptureBlow(player, ped, num4, flag4, num5, (item2 < 0.35f) ? item : (-1), "event", flag6 || (flag3 && item3 && item2 < 0.35f), flag6);
					impactRecord.ShotId = ++_shotId;
					impactRecord.EventIndex = i;
					this.Impact?.Invoke(impactRecord);
				}
				else if (weaponClass2.HasValue)
				{
					int num9 = ((weaponClass2 == WeaponClass.Burn) ? 6000 : 1500);
					if (!_areaAt.TryGetValue(ped.Handle, out var value4) || Game.GameTime - value4 >= num9)
					{
						_areaAt[ped.Handle] = Game.GameTime;
						ImpactRecord impactRecord2 = CaptureArea(at: new Vector3(*(float*)(ptr + 6), *(float*)(ptr + 7), *(float*)(ptr + 8)), player: player, victim: ped, weapon: num4, kind: weaponClass2.Value, damage: *(float*)(ptr + 4));
						impactRecord2.Animal = flag5;
						impactRecord2.ShotId = ++_shotId;
						impactRecord2.EventIndex = i;
						this.Diagnostic?.Invoke(string.Format("{0} ped={1} weapon {2}, {3} points, {4:0.0} m from it", (weaponClass2 == WeaponClass.Burn) ? "BURN" : "BLAST", ped.Handle, impactRecord2.WeaponName, impactRecord2.PelletsOnVictim, impactRecord2.Distance));
						this.Impact?.Invoke(impactRecord2);
					}
				}
				else if (weaponClass.HasValue)
				{
					ImpactRecord impactRecord3 = CaptureStrike(player, ped, num4, weaponClass.Value, *(float*)(ptr + 4));
					impactRecord3.Animal = flag5;
					impactRecord3.ShotId = ++_shotId;
					impactRecord3.EventIndex = i;
					this.Impact?.Invoke(impactRecord3);
				}
				else
				{
					if (num4 != _lastShotWeapon || Game.GameTime - _lastShotTime > 150)
					{
						_shotId++;
					}
					_lastShotWeapon = num4;
					_lastShotTime = Game.GameTime;
					ImpactRecord impactRecord4 = Capture(player, ped, num4, *(float*)(ptr + 4), flag ? num3 : 0);
					if (flag && impactRecord4.VictimDeadAfterHit && _gunNoted.Add($"kill {ENTITY.GET_ENTITY_MODEL(num3)}"))
					{
						int num10 = PED.GET_PED_SOURCE_OF_DEATH(ped.Handle);
						AmmoNote?.Invoke("MOUNTED GUN " + ModelName(num3) + ": a kill (muzzle via " + impactRecord4.MuzzleSource + ", impact " + (impactRecord4.HasImpact ? "on him" : "none") + ") - the game names " + ((num10 == player.Handle) ? "you" : ((num10 == num3) ? "the gun" : ((num10 == 0) ? "nobody" : $"entity {num10}"))) + " as his killer");
					}
					impactRecord4.Animal = flag5;
					impactRecord4.ShotId = _shotId;
					impactRecord4.EventIndex = i;
					impactRecord4.EventCoord = new Vector3(*(float*)(ptr + 6), *(float*)(ptr + 7), *(float*)(ptr + 8));
					_damagedAt[ped.Handle] = Game.GameTime;
					this.Impact?.Invoke(impactRecord4);
				}
				break;
			}
			}
		}
		WatchBeaten(player);
		WatchHolds(player);
		WatchStomps(player);
		if (num2 > 0)
		{
			this.Diagnostic?.Invoke(string.Format("EVENT_SHOT_FIRED_BULLET_IMPACT x{0} this frame, entities [{1}]", num2, string.Join(",", list)));
		}
		if (num2 > 0 && Game.GameTime - _playerShotAt < 600)
		{
			foreach (int item8 in list)
			{
				if (item8 != 0 && !_pendingImpact.ContainsKey(item8) && ENTITY.DOES_ENTITY_EXIST(item8) && ENTITY.IS_ENTITY_A_PED(item8) && !ENTITY.IS_ENTITY_DEAD(item8))
				{
					_pendingImpact[item8] = (Game.GameTime, _playerShotWeapon);
				}
			}
		}
		foreach (KeyValuePair<int, (int, uint)> item9 in _pendingImpact.ToList())
		{
			if (_damagedAt.TryGetValue(item9.Key, out var value5) && value5 >= item9.Value.Item1 - 50)
			{
				_pendingImpact.Remove(item9.Key);
			}
			else
			{
				if (Game.GameTime - item9.Value.Item1 < 350)
				{
					continue;
				}
				_pendingImpact.Remove(item9.Key);
				Ped ped2 = Entity.FromHandle(item9.Key) as Ped;
				if (!(ped2 == null) && ped2.Exists() && ped2.IsHuman && !ped2.IsPlayer && !Kids.IsChild(ped2) && item9.Value.Item2 != 0 && IsProjectileWeapon(item9.Value.Item2))
				{
					if (item9.Value.Item2 != _lastShotWeapon || Game.GameTime - _lastShotTime > 150)
					{
						_shotId++;
					}
					_lastShotWeapon = item9.Value.Item2;
					_lastShotTime = Game.GameTime;
					ImpactRecord impactRecord5 = Capture(player, ped2, item9.Value.Item2, 0f);
					if (impactRecord5.ImpactOnVictim)
					{
						impactRecord5.ShotId = _shotId;
						impactRecord5.EventIndex = -1;
						this.Diagnostic?.Invoke($"FALLBACK capture ped={item9.Key}: bullet impact without a damage event (weapon {item9.Value.Item2:X8})");
						this.Impact?.Invoke(impactRecord5);
					}
				}
			}
		}
		if (_damagedAt.Count > 64)
		{
			foreach (int item10 in (from kv in _damagedAt
				where Game.GameTime - kv.Value > 5000
				select kv.Key).ToList())
			{
				_damagedAt.Remove(item10);
			}
		}
		if (_areaAt.Count > 64)
		{
			foreach (int item11 in (from kv in _areaAt
				where Game.GameTime - kv.Value > 10000
				select kv.Key).ToList())
			{
				_areaAt.Remove(item11);
			}
		}
		if (_blowAt.Count <= 64)
		{
			return;
		}
		foreach (int item12 in (from kv in _blowAt
			where Game.GameTime - kv.Value > 5000
			select kv.Key).ToList())
		{
			_blowAt.Remove(item12);
		}
	}

	private static bool FourLegged(Ped p)
	{
		uint hash = (uint)p.Model.Hash;
		if (_fourLegged.TryGetValue(hash, out var value))
		{
			return value;
		}
		List<int> list = Anatomy.AllBones.Where((int id) => PED.GET_PED_BONE_INDEX(p.Handle, id) != -1).ToList();
		value = AnimalAnatomy.LooksFourLegged(list);
		object obj;
		if (!Enum.IsDefined(typeof(PedHash), hash))
		{
			obj = "";
		}
		else
		{
			PedHash pedHash = (PedHash)hash;
			obj = pedHash.ToString().ToLowerInvariant();
		}
		string mn = (string)obj;
		bool flag = Birds.Any((string k) => mn.Contains(k));
		if (NotAnimals.Any((string k) => mn.Contains(k)))
		{
			value = false;
		}
		else if (flag)
		{
			value = list.Contains(56200) && list.Contains(21030);
		}
		if (value && !flag && list.Contains(56200) && list.Contains(21030))
		{
			Vector3 vector = PED.GET_PED_BONE_COORDS(p.Handle, 56200, 0f, 0f, 0f);
			Vector3 position = PED.GET_PED_BONE_COORDS(p.Handle, 21030, 0f, 0f, 0f);
			if (vector.DistanceTo(position) < 0.25f)
			{
				value = false;
			}
		}
		_fourLegged[hash] = value;
		Action<string> ammoNote = AmmoNote;
		if (ammoNote != null)
		{
			string text;
			if (!Enum.IsDefined(typeof(PedHash), hash))
			{
				text = $"0x{hash:X8}";
			}
			else
			{
				PedHash pedHash = (PedHash)hash;
				text = pedHash.ToString();
			}
			ammoNote("ANIMAL X-RAY " + text + ": " + ((!value) ? "not a four-legged skeleton - skipped" : (flag ? "a bird - X-rayed" : "four-legged - X-rayed")));
		}
		return value;
	}

	private static bool Bumped(Ped player)
	{
		if (PED.IS_PED_IN_MELEE_COMBAT(player.Handle))
		{
			return false;
		}
		if (!PED.IS_PED_ON_MOUNT(player.Handle))
		{
			return ENTITY.GET_ENTITY_SPEED(player.Handle) > 2.6f;
		}
		return true;
	}

	private void TrackFists(Ped player)
	{
		Vector3 item = PED.GET_PED_BONE_COORDS(player.Handle, 34606, 0f, 0f, 0f);
		Vector3 vector = PED.GET_PED_BONE_COORDS(player.Handle, 22798, 0f, 0f, 0f);
		int num = WEAPON.GET_CURRENT_PED_WEAPON_ENTITY_INDEX(player.Handle, 0);
		Vector3 item2 = PED.GET_PED_BONE_COORDS(player.Handle, 45454, 0f, 0f, 0f);
		Vector3 item3 = PED.GET_PED_BONE_COORDS(player.Handle, 33646, 0f, 0f, 0f);
		_fists.Enqueue((Game.GameTime, item, vector, (num != 0) ? ENTITY.GET_ENTITY_COORDS(num, alive: true, realCoords: true) : vector, item2, item3));
		while (_fists.Count > 48)
		{
			_fists.Dequeue();
		}
	}

	private static float BoneThickness(int id)
	{
		switch (id)
		{
		default:
			return 0.04f;
		case 14410:
		case 14411:
		case 56200:
			return 0.12f;
		case 14412:
		case 14413:
		case 14414:
		case 14415:
			return 0.13f;
		case 21030:
			return 0.1f;
		}
	}

	private (int bone, float gap, bool foot) Reach(Ped victim)
	{
		List<(int, Vector3)> list = new List<(int, Vector3)>();
		int[] strikeBones = StrikeBones;
		foreach (int num in strikeBones)
		{
			if (PED.GET_PED_BONE_INDEX(victim.Handle, num) != -1)
			{
				list.Add((num, PED.GET_PED_BONE_COORDS(victim.Handle, num, 0f, 0f, 0f)));
			}
		}
		int item = -1;
		float num2 = 99f;
		bool item2 = false;
		foreach (var fist in _fists)
		{
			if (Game.GameTime - fist.t > 300)
			{
				continue;
			}
			foreach (var item6 in list)
			{
				int item3 = item6.Item1;
				Vector3 item4 = item6.Item2;
				float num3 = BoneThickness(item3);
				Vector3 item5 = fist.l;
				float val = item5.DistanceTo(item4);
				item5 = fist.r;
				float val2 = item5.DistanceTo(item4);
				item5 = fist.w;
				float num4 = Math.Min(val, Math.Min(val2, item5.DistanceTo(item4))) - num3;
				item5 = fist.lf;
				float val3 = item5.DistanceTo(item4);
				item5 = fist.rf;
				float num5 = Math.Min(val3, item5.DistanceTo(item4)) - num3;
				if (num4 < num2)
				{
					num2 = num4;
					item = item3;
					item2 = false;
				}
				if (num5 < num2 - 0.05f)
				{
					num2 = num5;
					item = item3;
					item2 = true;
				}
			}
		}
		return (bone: item, gap: num2, foot: item2);
	}

	private void NoteDownState(Ped ped, int died)
	{
		int handle = ped.Handle;
		string text = $"fatal={PED.IS_PED_FATALLY_INJURED(handle)} died@={died} cause={PED.GET_PED_CAUSE_OF_DEATH(handle):X8} source={PED.GET_PED_SOURCE_OF_DEATH(handle)} ragdoll={PED.IS_PED_RAGDOLL(handle)} health={ped.Health}/{ped.MaxHealth}";
		if (!_downState.TryGetValue(handle, out var value) || !(value == text))
		{
			_downState[handle] = text;
			if (_downState.Count > 32)
			{
				_downState.Clear();
			}
			AmmoNote?.Invoke(string.Format("DOWNSTATE ped={0} {1}{2}", handle, text, (value == null) ? " (first)" : (" (was " + value + ")")));
		}
	}

	internal static bool KilledByFightChoke(bool deadAfter, bool choke, bool fight)
	{
		return deadAfter && choke && fight;
	}

	internal static bool KilledByGroundBlow(bool deadAfter, bool down, bool butt, bool choke, bool stomp, float damage)
	{
		if (deadAfter && down && !butt && !choke && !stomp && damage > 0f)
		{
			return damage <= 4.5f;
		}
		return false;
	}

	private void Unwatch(int h)
	{
		_struckAt.Remove(h);
		_stompAt.Remove(h);
		_stompNoteAt.Remove(h);
		_koDiedAt.Remove(h);
		_todMovedAt.Remove(h);
		_koStomps.Remove(h);
		_stillOutNoted.Remove(h);
	}

	private void WatchStomps(Ped player)
	{
		if (_struckAt.Count == 0)
		{
			return;
		}
		bool flag = !PED.IS_PED_ON_MOUNT(player.Handle) && !PED.IS_PED_IN_ANY_VEHICLE(player.Handle, atGetIn: false);
		foreach (int item in _struckAt.Keys.ToList())
		{
			int num = Game.GameTime - _struckAt[item];
			Ped ped = Entity.FromHandle(item) as Ped;
			if (ped == null || !ped.Exists() || num > 300000 || Downed.IsFinished(item))
			{
				Unwatch(item);
				continue;
			}
			if (!ped.IsDead)
			{
				_koDiedAt.Remove(item);
				_todMovedAt.Remove(item);
				_koStomps.Remove(item);
				_stillOutNoted.Remove(item);
				if (num > 30000)
				{
					_struckAt.Remove(item);
				}
				continue;
			}
			if (!NemesisMod.LooksKnockedOut(ped))
			{
				if (PED.GET_PED_CAUSE_OF_DEATH(item) != 0)
				{
					Unwatch(item);
					AmmoNote?.Invoke($"STOMP watch off ped={item} - killed, not out cold");
				}
				continue;
			}
			int num2 = Function.Call<int>(2204654383702665610uL, new InputArgument[1] { item });
			if (!_koDiedAt.TryGetValue(item, out var value))
			{
				value = (_koDiedAt[item] = num2);
			}
			NoteDownState(ped, num2);
			if (num2 != value && !_todMovedAt.ContainsKey(item))
			{
				_todMovedAt[item] = Game.GameTime;
			}
			int value2;
			int num4 = (_koStomps.TryGetValue(item, out value2) ? value2 : 0);
			int value5;
			if (_todMovedAt.TryGetValue(item, out var value3))
			{
				int value4;
				bool flag2 = _stompAt.TryGetValue(item, out value4) && value4 >= value3 - 3000;
				if (flag2 || Game.GameTime - value3 > 1500)
				{
					AmmoNote?.Invoke(string.Format("KILLED OUT COLD ped={0} - the game killed him (his time of death {1} -> {2}){3}", item, value, num2, flag2 ? $" on stomp {num4} on him out cold" : ", no stomp of yours near it"));
					Unwatch(item);
					this.KilledOutCold?.Invoke(item, flag2);
					continue;
				}
			}
			else if (num4 >= 3 && !_stillOutNoted.Contains(item) && _stompAt.TryGetValue(item, out value5) && Game.GameTime - value5 > 20000)
			{
				_stillOutNoted.Add(item);
				AmmoNote?.Invoke($"STILL OUT ped={item} - {num4} stomps on him out cold and his time of death hasn't moved: the game hasn't killed him");
			}
			if (!flag || ENTITY.GET_ENTITY_SPEED(player.Handle) > 1.5f || ped.Position.DistanceTo(player.Position) > 2.5f || (_stompAt.TryGetValue(item, out var value6) && Game.GameTime - value6 < 450) || (_blowAt.TryGetValue(item, out var value7) && Game.GameTime - value7 < 450))
			{
				continue;
			}
			var (num5, num6, num7, num8) = BootDown(ped);
			if (num5 < 0 || num6 > 0.8f || num8 < 0.90000004f)
			{
				continue;
			}
			bool flag3 = num6 < 0.35f && num8 >= 1.5f && num7 >= 0.2f;
			if (flag3 || !_stompNoteAt.TryGetValue(item, out var value8) || Game.GameTime - value8 > 1000)
			{
				if (!flag3)
				{
					_stompNoteAt[item] = Game.GameTime;
				}
				AmmoNote?.Invoke(string.Format("STOMP{0} ped={1} boot {2:0.00} m off {3}, came down {4:0.0} m/s from {5:0.00} m up{6}", flag3 ? "" : "?", item, num6, (BoneID)num5, num8, num7, flag3 ? $" - {num4 + 1} on him out cold" : " - not counted"));
			}
			if (flag3)
			{
				_stompAt[item] = Game.GameTime;
				_koStomps[item] = num4 + 1;
				_blowAt[item] = Game.GameTime;
				ImpactRecord impactRecord = CaptureBlow(player, ped, 2725352035u, butt: false, 0f, num5, "stomp", kick: false, saddle: false, 0, stomp: true);
				impactRecord.ShotId = ++_shotId;
				impactRecord.EventIndex = -2;
				this.Impact?.Invoke(impactRecord);
			}
		}
	}

	private (int bone, float gap, float lift, float speed) BootDown(Ped victim)
	{
		if (_fists.Count < 3)
		{
			return (bone: -1, gap: 99f, lift: 0f, speed: 0f);
		}
		(int, Vector3, Vector3, Vector3, Vector3, Vector3)[] array = _fists.ToArray();
		(int, Vector3, Vector3, Vector3, Vector3, Vector3) tuple = array[array.Length - 1];
		int num = -1;
		float num2 = 99f;
		bool flag = true;
		Vector3 vector = Vector3.Zero;
		int[] strikeBones = StrikeBones;
		foreach (int num3 in strikeBones)
		{
			if (PED.GET_PED_BONE_INDEX(victim.Handle, num3) != -1)
			{
				Vector3 vector2 = PED.GET_PED_BONE_COORDS(victim.Handle, num3, 0f, 0f, 0f);
				float num4 = BoneThickness(num3);
				float num5 = tuple.Item5.DistanceTo(vector2) - num4;
				float num6 = tuple.Item6.DistanceTo(vector2) - num4;
				if (num5 < num2)
				{
					num2 = num5;
					num = num3;
					flag = true;
					vector = vector2;
				}
				if (num6 < num2)
				{
					num2 = num6;
					num = num3;
					flag = false;
					vector = vector2;
				}
			}
		}
		if (num < 0)
		{
			return (bone: -1, gap: 99f, lift: 0f, speed: 0f);
		}
		float num7 = 0f;
		float num8 = 0f;
		(int, Vector3, Vector3, Vector3, Vector3, Vector3)[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(int, Vector3, Vector3, Vector3, Vector3, Vector3) tuple2 = array2[i];
			if (tuple.Item1 - tuple2.Item1 <= 400)
			{
				Vector3 vector3 = (flag ? tuple2.Item5 : tuple2.Item6);
				num7 = Math.Max(num7, vector3.Z - vector.Z);
				int num9 = tuple.Item1 - tuple2.Item1;
				if (num9 >= 100 && num9 <= 200)
				{
					float val = num8;
					float z = vector3.Z;
					Vector3 obj = (flag ? tuple.Item5 : tuple.Item6);
					num8 = Math.Max(val, (z - obj.Z) / ((float)num9 / 1000f));
				}
			}
		}
		return (bone: num, gap: num2, lift: num7, speed: num8);
	}

	internal static string HoldHitKind(float dmg, bool outCold, int ticksBefore)
	{
		if (!(dmg < 1.5f))
		{
			if (!outCold || ticksBefore < 3)
			{
				return "a punch";
			}
			return "the choke's end (out cold)";
		}
		return "a squeeze";
	}

	private static (bool behind, float hands) Grip(Ped player, Ped victim)
	{
		Vector3 vector = ENTITY.GET_ENTITY_FORWARD_VECTOR(victim.Handle);
		Vector3 vector2 = player.Position - victim.Position;
		vector2.Z = 0f;
		bool item = vector2.Length() > 0.05f && (vector.X * vector2.X + vector.Y * vector2.Y) / vector2.Length() < -0.3f;
		Vector3 position = PED.GET_PED_BONE_COORDS(victim.Handle, 14285, 0f, 0f, 0f);
		float val = PED.GET_PED_BONE_COORDS(player.Handle, 34606, 0f, 0f, 0f).DistanceTo(position);
		float val2 = PED.GET_PED_BONE_COORDS(player.Handle, 22798, 0f, 0f, 0f).DistanceTo(position);
		return (behind: item, hands: Math.Max(val, val2));
	}

	private bool Held(int h)
	{
		if (_holds.ContainsKey(h))
		{
			return PED.GET_PED_IS_BEING_GRAPPLED(h);
		}
		return false;
	}

	private void EndHold(int h, string why)
	{
		if (_holds.Remove(h))
		{
			AmmoNote?.Invoke($"HOLD ped={h} ended - {why}: not a choke");
		}
	}

	private void StrayHit(int h, int by, uint weapon, float dmg)
	{
		if (!_holds.TryGetValue(h, out var value))
		{
			return;
		}
		string text = string.Format("{0}{1}, {2}, {3:0.#} damage", ModelName(by), ENTITY.IS_ENTITY_A_PED(by) ? "" : " (not a person)", WeaponText(weapon), dmg);
		if (!value.Squeezed)
		{
			EndHold(h, "hurt by someone else (" + text + ")");
			return;
		}
		value.Other += (int)Math.Ceiling(dmg);
		bool flag = ENTITY.IS_ENTITY_DEAD(h) && weapon != 0 && WEAPON.GET_WEAPONTYPE_GROUP(weapon) != 2685387236u;
		if (flag)
		{
			value.KilledByOther = true;
		}
		AmmoNote?.Invoke(string.Format("HOLD ped={0} hit by someone else in the choke ({1}) - {2}", h, text, flag ? "it killed him: the choke so far counts, not as what put him down" : "the choke goes on, that damage isn't its"));
	}

	private void WatchHolds(Ped player)
	{
		if (_holds.Count == 0)
		{
			return;
		}
		foreach (int item in _holds.Keys.ToList())
		{
			Hold hold = _holds[item];
			Ped ped = Entity.FromHandle(item) as Ped;
			if (ped == null || !ped.Exists())
			{
				_holds.Remove(item);
				continue;
			}
			bool isDead = ped.IsDead;
			bool flag = PED.GET_PED_IS_BEING_GRAPPLED(item);
			if (flag)
			{
				hold.Grappled = true;
			}
			if (flag || isDead)
			{
				hold.HpLow = Math.Min(hold.HpLow, (!isDead) ? ped.Health : 0);
			}
			int num = Game.GameTime - hold.Since;
			if (!isDead && (flag || num < 4000) && num < 60000)
			{
				continue;
			}
			_holds.Remove(item);
			int num2 = Math.Max(0, hold.Hp0 - hold.HpLow - hold.Struck - hold.Other);
			bool flag2 = hold.Squeezed && (num2 >= 15 || (isDead && Game.GameTime - hold.LastPunchAt > 1500));
			AmmoNote?.Invoke(string.Format("HOLD ped={0} held {1:0.0} s, health {2} -> {3} (punches {4}, squeezes {5}{6}){7}{8} - {9}", item, (float)num / 1000f, hold.Hp0, hold.HpLow, hold.Struck, hold.Ticks, (hold.Other > 0) ? $", others' hits {hold.Other}" : "", (!isDead) ? "" : (hold.KilledByOther ? ", killed by someone else" : ", went limp"), hold.Squeezed ? ", squeezed" : "", flag2 ? "a choke" : "not a choke"));
			if (flag2)
			{
				_blowAt[item] = Game.GameTime;
				ImpactRecord impactRecord = CaptureBlow(player, ped, 2725352035u, butt: false, num2, 14285, "hold", kick: false, saddle: false, Math.Max(1, num));
				if (hold.KilledByOther)
				{
					impactRecord.VictimDeadAfterHit = false;
				}
				impactRecord.FightChoke = hold.Grappled;
				impactRecord.ShotId = ++_shotId;
				impactRecord.EventIndex = -2;
				this.Impact?.Invoke(impactRecord);
			}
		}
	}

	private string NotABlow(Ped v, float dmg)
	{
		int handle = v.Handle;
		int num = Function.Call<int>(7030473103003024859uL, new InputArgument[1] { handle });
		if (num == 1)
		{
			return "being hogtied";
		}
		if (num >= 4)
		{
			return "being carried / put down / cut free";
		}
		if (PED.GET_PED_IS_BEING_GRAPPLED(handle))
		{
			if (!_grabbedSince.TryGetValue(handle, out var value))
			{
				value = (_grabbedSince[handle] = Game.GameTime);
			}
			if (Game.GameTime - value < 1500 && dmg < 6f)
			{
				return "the grab";
			}
		}
		else
		{
			_grabbedSince.Remove(handle);
		}
		if (_grabbedSince.Count > 32)
		{
			_grabbedSince.Clear();
		}
		return null;
	}

	private static string HowHeWas(Ped v)
	{
		int num = Function.Call<int>(7030473103003024859uL, new InputArgument[1] { v.Handle });
		if (!v.IsHogtied)
		{
			if (!PED.GET_PED_IS_BEING_GRAPPLED(v.Handle))
			{
				if (!v.IsRagdoll && num != 2 && num != 3)
				{
					return "";
				}
				return ", he was down";
			}
			return ", you held him";
		}
		return ", he was tied";
	}

	private unsafe void WatchBeaten(Ped player)
	{
		if (_beaten.Count == 0)
		{
			return;
		}
		foreach (int item in _beaten.Keys.ToList())
		{
			(int, int) tuple = _beaten[item];
			Ped ped = Entity.FromHandle(item) as Ped;
			if (ped == null || !ped.Exists() || ped.IsDead || Game.GameTime - tuple.Item2 > 20000)
			{
				_beaten.Remove(item);
				continue;
			}
			int health = ped.Health;
			_beaten[item] = (health, tuple.Item2);
			if (health > tuple.Item1 - 2 || (_blowAt.TryGetValue(item, out var value) && Game.GameTime - value < 250))
			{
				continue;
			}
			uint num = 0u;
			if (!WEAPON.GET_CURRENT_PED_WEAPON(player.Handle, &num, p2: true, 0, p4: true))
			{
				continue;
			}
			bool flag = WEAPON.GET_WEAPONTYPE_GROUP(num) == 2685387236u;
			bool flag2 = !flag && GunMelee.IsGun(num);
			if ((flag || flag2) && PED.IS_PED_IN_MELEE_COMBAT(player.Handle) && (!flag2 || !GunMelee.PlayerFiredRecently) && NotABlow(ped, tuple.Item1 - health) == null && !Held(item))
			{
				var (struck, num2, flag3) = Reach(ped);
				if (!(num2 > 0.3f))
				{
					_blowAt[item] = Game.GameTime;
					ImpactRecord impactRecord = CaptureBlow(player, ped, num, flag2, tuple.Item1 - health, struck, "health", flag && flag3);
					impactRecord.ShotId = ++_shotId;
					impactRecord.EventIndex = -2;
					this.Impact?.Invoke(impactRecord);
				}
			}
		}
	}

	private unsafe ImpactRecord CaptureBlow(Ped player, Ped victim, uint weapon, bool butt, float damage, int struck, string via, bool kick = false, bool saddle = false, int heldMs = 0, bool stomp = false)
	{
		bool flag = heldMs > 0;
		_struckAt[victim.Handle] = Game.GameTime;
		ImpactRecord obj = new ImpactRecord
		{
			PedHandle = victim.Handle,
			PedModel = (uint)victim.Model.Hash,
			VictimDeadAfterHit = victim.IsDead,
			WeaponHash = weapon
		};
		object weaponName;
		if (!flag)
		{
			if (!butt || !Enum.IsDefined(typeof(eWeapon), weapon))
			{
				weaponName = (((kick || stomp) && !butt) ? "Boots" : "Fists");
			}
			else
			{
				eWeapon eWeapon = (eWeapon)weapon;
				weaponName = eWeapon.ToString();
			}
		}
		else
		{
			weaponName = "Bare hands";
		}
		obj.WeaponName = (string)weaponName;
		obj.Damage = damage;
		obj.GameTime = Game.GameTime;
		obj.Butt = butt && !flag;
		obj.Kick = (kick || stomp) && !butt && !flag;
		obj.Choke = flag;
		obj.HeldMs = heldMs;
		obj.Stomp = stomp;
		ImpactRecord impactRecord = obj;
		int boneId = default(int);
		if (PED.GET_PED_LAST_DAMAGE_BONE(victim.Handle, &boneId))
		{
			impactRecord.BoneId = boneId;
			impactRecord.BoneIndex = PED.GET_PED_BONE_INDEX(victim.Handle, boneId);
		}
		object boneName;
		if (impactRecord.BoneId >= 0)
		{
			BoneID boneId2 = (BoneID)impactRecord.BoneId;
			boneName = boneId2.ToString();
		}
		else
		{
			boneName = "none";
		}
		impactRecord.BoneName = (string)boneName;
		if (via == "event" && impactRecord.BoneId >= 0 && impactRecord.BoneId != 56200 && PED.GET_PED_BONE_INDEX(victim.Handle, impactRecord.BoneId) != -1)
		{
			struck = -1;
		}
		Vector3 vector;
		if (struck >= 0)
		{
			impactRecord.BlowBone = struck;
			vector = PED.GET_PED_BONE_COORDS(victim.Handle, struck, 0f, 0f, 0f);
		}
		else
		{
			vector = ((impactRecord.BoneId >= 0) ? PED.GET_PED_BONE_COORDS(victim.Handle, impactRecord.BoneId, 0f, 0f, 0f) : (victim.Position + new Vector3(0f, 0f, 0.5f)));
			float num = float.MaxValue;
			int[] strikeBones = StrikeBones;
			foreach (int num2 in strikeBones)
			{
				if (PED.GET_PED_BONE_INDEX(victim.Handle, num2) != -1)
				{
					float num3 = PED.GET_PED_BONE_COORDS(victim.Handle, num2, 0f, 0f, 0f).DistanceTo(vector);
					if (num3 < num)
					{
						num = num3;
						impactRecord.BlowBone = num2;
					}
				}
			}
		}
		if (via == "event" || !_beaten.ContainsKey(victim.Handle))
		{
			_beaten[victim.Handle] = (victim.Health, Game.GameTime);
		}
		else
		{
			_beaten[victim.Handle] = (victim.Health, _beaten[victim.Handle].last);
		}
		Vector3 vector2 = PED.GET_PED_BONE_COORDS(player.Handle, 14413, 0f, 0f, 0f);
		Vector3 vector3 = Norm(vector - vector2, victim.Position - player.Position);
		impactRecord.HasImpact = true;
		impactRecord.Impact = vector;
		impactRecord.Muzzle = vector - vector3 * 0.6f;
		int num4 = Function.Call<int>(7030473103003024859uL, new InputArgument[1] { victim.Handle });
		bool isHogtied = victim.IsHogtied;
		bool flag2 = victim.IsRagdoll || victim.IsDead || num4 == 2 || num4 == 3;
		int value;
		bool flag3 = _lyingAt.TryGetValue(victim.Handle, out value) && Game.GameTime - value < 4000;
		impactRecord.Down = stomp || isHogtied || (flag2 && flag3);
		if (stomp || isHogtied || flag2)
		{
			_lyingAt[victim.Handle] = Game.GameTime;
		}
		else
		{
			_lyingAt.Remove(victim.Handle);
		}
		if (_lyingAt.Count > 64)
		{
			foreach (int item in (from kv in _lyingAt
				where Game.GameTime - kv.Value > 4000
				select kv.Key).ToList())
			{
				_lyingAt.Remove(item);
			}
		}
		impactRecord.MuzzleSource = (flag ? $"choke, held {(float)heldMs / 1000f:0.0} s" : ((butt ? "gun butt" : (stomp ? "stomp" : ((!impactRecord.Kick) ? "fist" : (saddle ? "kick from the saddle" : "kick")))) + ", " + via + ((struck >= 0) ? "" : ", game's bone") + HowHeWas(victim)));
		impactRecord.Distance = player.Position.DistanceTo(victim.Position);
		impactRecord.Class = WeaponClass.Blunt;
		SnapshotSkeleton(victim, impactRecord);
		return impactRecord;
	}

	private static WeaponClass? AreaOf(uint weapon)
	{
		if (_hExplosion == 0)
		{
			_hExplosion = MISC.GET_HASH_KEY("WEAPON_EXPLOSION");
			_hFire = MISC.GET_HASH_KEY("WEAPON_FIRE");
			_hMolotov = MISC.GET_HASH_KEY("WEAPON_THROWN_MOLOTOV");
			_hDynamite = MISC.GET_HASH_KEY("WEAPON_THROWN_DYNAMITE");
		}
		if (weapon == _hExplosion || weapon == _hDynamite)
		{
			return WeaponClass.Blast;
		}
		if (weapon == _hFire || weapon == _hMolotov)
		{
			return WeaponClass.Burn;
		}
		object obj;
		if (!Enum.IsDefined(typeof(eWeapon), weapon))
		{
			obj = "";
		}
		else
		{
			eWeapon eWeapon = (eWeapon)weapon;
			obj = eWeapon.ToString();
		}
		string text = (string)obj;
		if (text.Contains("Dynamite") || text.Contains("Explos") || text.Contains("Volatile"))
		{
			return WeaponClass.Blast;
		}
		if (text.StartsWith("Turret") && text.Contains("Cannon"))
		{
			return WeaponClass.Blast;
		}
		if (text.Contains("Molotov") || text.Contains("FireBottle") || (text.Contains("Fire") && !text.Contains("Firework")))
		{
			return WeaponClass.Burn;
		}
		return null;
	}

	private unsafe ImpactRecord CaptureArea(Ped player, Ped victim, uint weapon, WeaponClass kind, float damage, Vector3 at)
	{
		ImpactRecord obj = new ImpactRecord
		{
			PedHandle = victim.Handle,
			PedModel = (uint)victim.Model.Hash,
			VictimDeadAfterHit = victim.IsDead,
			WeaponHash = weapon
		};
		object weaponName;
		if (!Enum.IsDefined(typeof(eWeapon), weapon))
		{
			weaponName = ((kind == WeaponClass.Blast) ? "Explosion" : "Fire");
		}
		else
		{
			eWeapon eWeapon = (eWeapon)weapon;
			weaponName = eWeapon.ToString();
		}
		obj.WeaponName = (string)weaponName;
		obj.Damage = damage;
		obj.GameTime = Game.GameTime;
		ImpactRecord impactRecord = obj;
		int boneId = default(int);
		if (PED.GET_PED_LAST_DAMAGE_BONE(victim.Handle, &boneId))
		{
			impactRecord.BoneId = boneId;
			impactRecord.BoneIndex = PED.GET_PED_BONE_INDEX(victim.Handle, boneId);
		}
		object boneName;
		if (impactRecord.BoneId >= 0)
		{
			BoneID boneId2 = (BoneID)impactRecord.BoneId;
			boneName = boneId2.ToString();
		}
		else
		{
			boneName = "none";
		}
		impactRecord.BoneName = (string)boneName;
		Vector3 vector = ((impactRecord.BoneId >= 0) ? PED.GET_PED_BONE_COORDS(victim.Handle, impactRecord.BoneId, 0f, 0f, 0f) : (victim.Position + new Vector3(0f, 0f, 0.4f)));
		Vector3 vector2;
		if (at != Vector3.Zero && at.DistanceTo(victim.Position) < 6f && at.DistanceTo(vector) > 0.3f)
		{
			vector2 = at;
		}
		else
		{
			Vector3 vector3 = Norm(player.Position - victim.Position, new Vector3(0f, 1f, 0f));
			vector2 = victim.Position + new Vector3(vector3.X, vector3.Y, 0f) * 1.5f + new Vector3(0f, 0f, (kind == WeaponClass.Burn) ? 0.4f : 0.1f);
		}
		impactRecord.HasImpact = true;
		impactRecord.Impact = vector;
		impactRecord.Muzzle = vector2;
		impactRecord.MuzzleSource = ((at != Vector3.Zero && vector2 == at) ? "event coords" : "your side of him");
		impactRecord.Distance = vector2.DistanceTo(vector);
		impactRecord.Class = kind;
		impactRecord.PelletsOnVictim = ((kind == WeaponClass.Burn) ? 10 : Math.Max(6, Math.Min(16, 6 + (int)(damage / 15f))));
		SnapshotSkeleton(victim, impactRecord);
		return impactRecord;
	}

	private void TrackBlade(Ped player)
	{
		int num = WEAPON.GET_CURRENT_PED_WEAPON_ENTITY_INDEX(player.Handle, 0);
		Vector3 item = ((num != 0) ? ENTITY.GET_ENTITY_COORDS(num, alive: true, realCoords: true) : PED.GET_PED_BONE_COORDS(player.Handle, 22798, 0f, 0f, 0f));
		_blade.Enqueue((Game.GameTime, item));
		while (_blade.Count > 12)
		{
			_blade.Dequeue();
		}
	}

	private Vector3 BladeMotion()
	{
		if (_blade.Count < 2)
		{
			return Vector3.Zero;
		}
		(int t, Vector3 p)[] array = _blade.ToArray();
		(int, Vector3) tuple = array[array.Length - 1];
		(int, Vector3) tuple2 = array[0];
		(int, Vector3)[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(int, Vector3) tuple3 = array2[i];
			if (tuple.Item1 - tuple3.Item1 <= 150)
			{
				tuple2 = tuple3;
				break;
			}
		}
		return tuple.Item2 - tuple2.Item2;
	}

	private static WeaponClass? BladeOf(uint weapon)
	{
		if (!Enum.IsDefined(typeof(eWeapon), weapon))
		{
			return null;
		}
		eWeapon eWeapon = (eWeapon)weapon;
		string text = eWeapon.ToString();
		if (!text.StartsWith("Melee") && !text.StartsWith("Thrown"))
		{
			return null;
		}
		if (text.Contains("Knife") || text.Contains("Knives"))
		{
			return WeaponClass.Stab;
		}
		if (text.Contains("Machete") || text.Contains("Hatchet") || text.Contains("Cleaver") || text.Contains("Tomahawk") || text.Contains("Sword"))
		{
			return WeaponClass.Chop;
		}
		return null;
	}

	private unsafe ImpactRecord CaptureStrike(Ped player, Ped victim, uint weapon, WeaponClass kind, float damage)
	{
		ImpactRecord obj = new ImpactRecord
		{
			PedHandle = victim.Handle,
			PedModel = (uint)victim.Model.Hash,
			VictimDeadAfterHit = victim.IsDead,
			WeaponHash = weapon
		};
		eWeapon eWeapon = (eWeapon)weapon;
		obj.WeaponName = eWeapon.ToString();
		obj.Ammo = AmmoOf(player, weapon);
		obj.Damage = damage;
		obj.GameTime = Game.GameTime;
		ImpactRecord impactRecord = obj;
		int boneId = default(int);
		if (PED.GET_PED_LAST_DAMAGE_BONE(victim.Handle, &boneId))
		{
			impactRecord.BoneId = boneId;
			impactRecord.BoneIndex = PED.GET_PED_BONE_INDEX(victim.Handle, boneId);
		}
		object boneName;
		if (impactRecord.BoneId >= 0)
		{
			BoneID boneId2 = (BoneID)impactRecord.BoneId;
			boneName = boneId2.ToString();
		}
		else
		{
			boneName = "none";
		}
		impactRecord.BoneName = (string)boneName;
		Vector3 vector = ((impactRecord.BoneId >= 0) ? PED.GET_PED_BONE_COORDS(victim.Handle, impactRecord.BoneId, 0f, 0f, 0f) : (victim.Position + new Vector3(0f, 0f, 0.4f)));
		Vector3 vector2 = PED.GET_PED_BONE_COORDS(player.Handle, 14413, 0f, 0f, 0f);
		Vector3 vector3 = Norm(vector - vector2, victim.Position - player.Position);
		bool flag = impactRecord.WeaponName.StartsWith("Thrown");
		Vector3 vector4 = (flag ? Vector3.Zero : BladeMotion());
		if (!flag && victim.IsHuman && impactRecord.BoneId == 21030 && victim.IsDead && damage >= (float)victim.MaxHealth * 0.6f)
		{
			(int bone, float gap, Vector3 knife, int ago) tuple = KnifeNearest(victim);
			int item = tuple.bone;
			float item2 = tuple.gap;
			Vector3 item3 = tuple.knife;
			int item4 = tuple.ago;
			string boneName2 = impactRecord.BoneName;
			bool flag2 = item >= 0 && item2 <= 0.25f;
			if (flag2)
			{
				impactRecord.BoneId = item;
				impactRecord.BoneIndex = PED.GET_PED_BONE_INDEX(victim.Handle, item);
				BoneID boneId2 = (BoneID)item;
				impactRecord.BoneName = boneId2.ToString();
				vector = PED.GET_PED_BONE_COORDS(victim.Handle, item, 0f, 0f, 0f);
				vector3 = Norm(vector - item3, vector3);
			}
			if (vector4.Length() < 0.15f)
			{
				vector4 = Vector3.Zero;
			}
			Action<string> ammoNote = AmmoNote;
			if (ammoNote != null)
			{
				object[] obj2 = new object[7] { victim.Handle, damage, boneName2, null, null, null, null };
				object obj3;
				if (item < 0)
				{
					obj3 = "nothing";
				}
				else
				{
					BoneID boneId2 = (BoneID)item;
					obj3 = boneId2.ToString();
				}
				obj2[3] = obj3;
				obj2[4] = item2;
				obj2[5] = item4;
				obj2[6] = (flag2 ? "read as that" : "kept the game's");
				ammoNote(string.Format("EXECUTION ped={0} dmg={1:0.#} - the game says {2}; your knife came nearest {3} ({4:0.00} m off it, {5} ms before) - {6}", obj2));
			}
		}
		bool flag3 = vector4.Length() > 0.05f;
		Vector3 vector5 = new Vector3(0f, 0f, 1f);
		float num = Vector3.Dot(vector4, vector3);
		Vector3 vector6 = vector4 - vector3 * num;
		if (kind == WeaponClass.Stab && !flag)
		{
			bool num2;
			if (!flag3)
			{
				if (impactRecord.BoneName.Contains("Spine"))
				{
					goto IL_0381;
				}
				num2 = impactRecord.BoneName.Contains("Pelvis");
			}
			else
			{
				num2 = num >= vector6.Length() * 0.8f;
			}
			if (!num2)
			{
				kind = WeaponClass.Slash;
			}
		}
		goto IL_0381;
		IL_0381:
		Vector3 vector7 = kind switch
		{
			WeaponClass.Stab => (flag3 && num > 0f) ? Norm(vector3 + Norm(vector4, vector3) * 0.4f, vector3) : vector3, 
			WeaponClass.Slash => Norm(Norm((vector6.Length() > 0.03f) ? vector6 : Vector3.Cross(vector3, vector5), vector3) * 0.85f + vector3 * 0.5f, vector3), 
			_ => flag3 ? Norm(Norm(vector4, vector3) * 0.7f + vector3 * 0.5f, vector3) : Norm(vector3 - vector5 * 0.5f, vector3), 
		};
		switch (kind)
		{
		case WeaponClass.Slash:
			vector -= vector3 * 0.045f;
			break;
		case WeaponClass.Chop:
			vector -= vector3 * 0.02f;
			break;
		}
		impactRecord.HasImpact = true;
		impactRecord.Impact = vector;
		impactRecord.Muzzle = vector - vector7 * 0.6f;
		impactRecord.MuzzleSource = (flag3 ? $"blade motion {vector4.Length():0.00} m" : "approach only");
		impactRecord.Distance = player.Position.DistanceTo(victim.Position);
		impactRecord.Class = kind;
		SnapshotSkeleton(victim, impactRecord);
		return impactRecord;
	}

	private (int bone, float gap, Vector3 knife, int ago) KnifeNearest(Ped victim)
	{
		List<(int, Vector3)> list = new List<(int, Vector3)>();
		int[] spineChain = Anatomy.SpineChain;
		foreach (int num in spineChain)
		{
			if (PED.GET_PED_BONE_INDEX(victim.Handle, num) != -1)
			{
				list.Add((num, PED.GET_PED_BONE_COORDS(victim.Handle, num, 0f, 0f, 0f)));
			}
		}
		int item = -1;
		int item2 = 0;
		float num2 = 99f;
		Vector3 item3 = Vector3.Zero;
		foreach (var fist in _fists)
		{
			int num3 = Game.GameTime - fist.t;
			if (num3 > 500)
			{
				continue;
			}
			foreach (var item7 in list)
			{
				int item4 = item7.Item1;
				Vector3 item5 = item7.Item2;
				float num4 = BoneThickness(item4);
				Vector3 item6 = fist.w;
				float num5 = item6.DistanceTo(item5) - num4;
				item6 = fist.r;
				float num6 = item6.DistanceTo(item5) - num4;
				if (num5 < num2)
				{
					num2 = num5;
					item = item4;
					item3 = fist.w;
					item2 = num3;
				}
				if (num6 < num2)
				{
					num2 = num6;
					item = item4;
					item3 = fist.r;
					item2 = num3;
				}
			}
		}
		return (bone: item, gap: num2, knife: item3, ago: item2);
	}

	private static Vector3 Norm(Vector3 v, Vector3 fallback)
	{
		float num = v.Length();
		if (num > 0.0001f)
		{
			return v * (1f / num);
		}
		num = fallback.Length();
		if (!(num > 0.0001f))
		{
			return new Vector3(0f, 1f, 0f);
		}
		return fallback * (1f / num);
	}

	internal static AmmoKind AmmoOf(Ped player, uint weapon)
	{
		uint num = 0u;
		uint num2 = 0u;
		try
		{
			int num3 = WEAPON.GET_CURRENT_PED_WEAPON_ENTITY_INDEX(player.Handle, 0);
			if (num3 != 0)
			{
				num = Function.Call<uint>(9113906463975140883uL, new InputArgument[2] { player.Handle, num3 });
			}
		}
		catch
		{
		}
		try
		{
			num2 = WEAPON.GET_PED_AMMO_TYPE_FROM_WEAPON(player.Handle, weapon);
		}
		catch
		{
		}
		AmmoKind ammoKind = Ammo.FromHash(num);
		if (ammoKind == AmmoKind.Regular)
		{
			ammoKind = Ammo.FromHash(num2);
		}
		string text = $"{num:X8}/{num2:X8}";
		if (text != _lastAmmoKey)
		{
			_lastAmmoKey = text;
			AmmoNote?.Invoke($"AMMO read: weapon object {Name(num)}, weapon {Name(num2)} -> {ammoKind}");
		}
		return ammoKind;
	}

	private static string Name(uint h)
	{
		if (h != 0)
		{
			if (!Enum.IsDefined(typeof(eAmmoType), h))
			{
				return $"0x{h:X8}";
			}
			eAmmoType eAmmoType = (eAmmoType)h;
			return eAmmoType.ToString();
		}
		return "none";
	}

	private static bool IsProjectileWeapon(uint weapon)
	{
		uint num = WEAPON.GET_WEAPONTYPE_GROUP(weapon);
		if (num != 416676503 && num != 3193669993u && num != 3700405225u && num != 970310034 && num != 3082541095u && num != 860033945 && num != 3053283277u)
		{
			return IsTurretGun(weapon);
		}
		return true;
	}

	private static bool IsTurretGun(uint w)
	{
		object obj;
		if (!Enum.IsDefined(typeof(eWeapon), w))
		{
			obj = "";
		}
		else
		{
			eWeapon eWeapon = (eWeapon)w;
			obj = eWeapon.ToString();
		}
		string text = (string)obj;
		if (text.StartsWith("Turret"))
		{
			return !text.Contains("Cannon");
		}
		return false;
	}

	private unsafe static int MountedGun(Ped player, out uint weapon)
	{
		weapon = 0u;
		int num = PED.GET_VEHICLE_PED_IS_IN(player.Handle, lastVehicle: false);
		if (num == 0)
		{
			num = Function.Call<int>(6959377544440096893uL, new InputArgument[1] { player.Handle });
		}
		if (num == 0 || !Function.Call<bool>(14112428505153989708uL, new InputArgument[1] { num }))
		{
			return 0;
		}
		ulong* ptr = stackalloc ulong[1];
		*ptr = 0uL;
		if (Function.Call<bool>(1159492374221042396uL, new InputArgument[2]
		{
			player.Handle,
			new InputArgument((ulong)ptr)
		}) && (uint)(*ptr) != 0)
		{
			weapon = (uint)(*ptr);
			return num;
		}
		ulong* ptr2 = stackalloc ulong[1];
		*ptr2 = 0uL;
		if (!Function.Call<bool>(18399335019481672262uL, new InputArgument[2]
		{
			num,
			new InputArgument((ulong)ptr2)
		}) || VEHICLE.GET_PED_IN_VEHICLE_SEAT(num, (int)(*ptr2)) != player.Handle)
		{
			return 0;
		}
		return num;
	}

	private static string ModelName(int e)
	{
		uint num = ENTITY.GET_ENTITY_MODEL(e);
		if (!Enum.IsDefined(typeof(VehicleHash), num))
		{
			if (!Enum.IsDefined(typeof(PedHash), num))
			{
				return $"0x{num:X8}";
			}
			PedHash pedHash = (PedHash)num;
			return pedHash.ToString();
		}
		VehicleHash vehicleHash = (VehicleHash)num;
		return vehicleHash.ToString();
	}

	private static string WeaponText(uint w)
	{
		if (w != 0)
		{
			if (!Enum.IsDefined(typeof(eWeapon), w))
			{
				return $"0x{w:X8}";
			}
			eWeapon eWeapon = (eWeapon)w;
			return eWeapon.ToString();
		}
		return "none";
	}

	private unsafe static ImpactRecord Capture(Ped player, Ped victim, uint weapon, float damage, int gun = 0)
	{
		ImpactRecord impactRecord = new ImpactRecord();
		impactRecord.PedHandle = victim.Handle;
		impactRecord.PedModel = (uint)victim.Model.Hash;
		impactRecord.VictimDeadAfterHit = victim.IsDead;
		impactRecord.WeaponHash = weapon;
		string weaponName;
		if (!Enum.IsDefined(typeof(eWeapon), weapon))
		{
			weaponName = $"0x{weapon:X8}";
		}
		else
		{
			eWeapon eWeapon = (eWeapon)weapon;
			weaponName = eWeapon.ToString();
		}
		impactRecord.WeaponName = weaponName;
		impactRecord.IsShotgun = WEAPON.GET_WEAPONTYPE_GROUP(weapon) == 860033945;
		impactRecord.Damage = damage;
		impactRecord.GameTime = Game.GameTime;
		impactRecord.PelletsOnVictim = Function.Call<int>(10881604826085319960uL, new InputArgument[3] { victim.Handle, false, false });
		impactRecord.Ammo = ((gun == 0) ? AmmoOf(player, weapon) : (IsTurretGun(weapon) ? AmmoKind.MachineGun : AmmoKind.Regular));
		ImpactRecord impactRecord2 = impactRecord;
		int boneId = default(int);
		if (PED.GET_PED_LAST_DAMAGE_BONE(victim.Handle, &boneId))
		{
			impactRecord2.BoneId = boneId;
			impactRecord2.BoneIndex = PED.GET_PED_BONE_INDEX(victim.Handle, boneId);
		}
		object boneName;
		if (impactRecord2.BoneId >= 0)
		{
			BoneID boneId2 = (BoneID)impactRecord2.BoneId;
			boneName = boneId2.ToString();
		}
		else
		{
			boneName = "none";
		}
		impactRecord2.BoneName = (string)boneName;
		ulong* ptr = stackalloc ulong[3];
		impactRecord2.HasImpact = WEAPON.GET_PED_LAST_WEAPON_IMPACT_COORD(player.Handle, (Vector3*)ptr);
		impactRecord2.Impact = ReadScrVector(ptr);
		if (gun != 0 && impactRecord2.HasImpact && impactRecord2.Impact.DistanceTo(victim.Position) > 3f)
		{
			impactRecord2.HasImpact = false;
		}
		if (gun != 0)
		{
			GunMuzzle(player, gun, out impactRecord2.Muzzle, out impactRecord2.MuzzleSource);
		}
		else
		{
			ResolveMuzzle(player, out impactRecord2.Muzzle, out impactRecord2.MuzzleSource);
		}
		Vector3 position = (impactRecord2.HasImpact ? impactRecord2.Impact : victim.Position);
		impactRecord2.Distance = impactRecord2.Muzzle.DistanceTo(position);
		SnapshotSkeleton(victim, impactRecord2);
		return impactRecord2;
	}

	private static void SnapshotSkeleton(Ped victim, ImpactRecord rec)
	{
		if (!Anatomy.IsBlade(rec.Class) && !Anatomy.IsArea(rec.Class) && !Anatomy.IsBlunt(rec.Class))
		{
			rec.Class = ClassOf(rec.WeaponHash);
		}
		Vector3 origin = victim.Position;
		double num = (double)victim.Heading * Math.PI / 180.0;
		V3 fwd = new V3((float)(0.0 - Math.Sin(num)), (float)Math.Cos(num), 0f);
		V3 right = new V3((float)Math.Cos(num), (float)Math.Sin(num), 0f);
		float num2 = float.MaxValue;
		IEnumerable<int> enumerable;
		if (victim.IsHuman)
		{
			IEnumerable<int> allBones = Anatomy.AllBones;
			enumerable = allBones;
		}
		else
		{
			enumerable = Anatomy.AllBones.Concat(AnimalAnatomy.TailIds);
		}
		foreach (int item in enumerable)
		{
			if (PED.GET_PED_BONE_INDEX(victim.Handle, item) != -1)
			{
				Vector3 w = PED.GET_PED_BONE_COORDS(victim.Handle, item, 0f, 0f, 0f);
				rec.BonesLocal.Add(new BonePoint
				{
					Id = item,
					P = ToLocal(w)
				});
				if (rec.HasImpact)
				{
					num2 = Math.Min(num2, w.DistanceTo(rec.Impact));
				}
			}
		}
		rec.MuzzleLocal = ToLocal(rec.Muzzle);
		Vector3 vector = (rec.HasImpact ? rec.Impact : victim.Position);
		rec.DirLocal = DirToLocal(vector - rec.Muzzle);
		rec.EntryLocal = ToLocal(vector);
		rec.ImpactOnVictim = rec.HasImpact && num2 < 0.45f;
		V3 DirToLocal(Vector3 vector2)
		{
			V3 a = new V3(vector2.X, vector2.Y, vector2.Z);
			return new V3(V3.Dot(a, right), V3.Dot(a, fwd), a.Z).Normalized();
		}
		V3 ToLocal(Vector3 vector2)
		{
			V3 a = new V3(vector2.X - origin.X, vector2.Y - origin.Y, vector2.Z - origin.Z);
			return new V3(V3.Dot(a, right), V3.Dot(a, fwd), a.Z);
		}
	}

	private static WeaponClass ClassOf(uint weapon)
	{
		return WEAPON.GET_WEAPONTYPE_GROUP(weapon) switch
		{
			416676503u => WeaponClass.Pistol, 
			3193669993u => WeaponClass.Revolver, 
			3700405225u => WeaponClass.Repeater, 
			3082541095u => WeaponClass.Sniper, 
			860033945u => WeaponClass.Shotgun, 
			3053283277u => WeaponClass.Bow, 
			_ => WeaponClass.Rifle, 
		};
	}

	private unsafe static Vector3 ReadScrVector(ulong* v)
	{
		return new Vector3(*(float*)v, *(float*)(v + 1), *(float*)(v + 2));
	}

	private static void ResolveMuzzle(Ped player, out Vector3 pos, out string source)
	{
		int num = WEAPON.GET_CURRENT_PED_WEAPON_ENTITY_INDEX(player.Handle, 0);
		if (num != 0)
		{
			string[] muzzleBoneNames = MuzzleBoneNames;
			foreach (string text in muzzleBoneNames)
			{
				int num2 = ENTITY.GET_ENTITY_BONE_INDEX_BY_NAME(num, text);
				if (num2 != -1)
				{
					pos = ENTITY.GET_WORLD_POSITION_OF_ENTITY_BONE(num, num2);
					source = "bone '" + text + "'";
					return;
				}
			}
			pos = ENTITY.GET_ENTITY_COORDS(num, alive: true, realCoords: true);
			source = "weapon entity";
		}
		else
		{
			pos = PED.GET_PED_BONE_COORDS(player.Handle, 21030, 0f, 0f, 0f);
			source = "player head";
		}
	}

	private static void GunMuzzle(Ped player, int gun, out Vector3 pos, out string source)
	{
		string[] gunMuzzleBones = GunMuzzleBones;
		foreach (string text in gunMuzzleBones)
		{
			int num = ENTITY.GET_ENTITY_BONE_INDEX_BY_NAME(gun, text);
			if (num != -1)
			{
				pos = ENTITY.GET_WORLD_POSITION_OF_ENTITY_BONE(gun, num);
				source = "the mounted gun's bone '" + text + "'";
				return;
			}
		}
		pos = PED.GET_PED_BONE_COORDS(player.Handle, 21030, 0f, 0f, 0f);
		source = "player head (on a mounted gun)";
	}
}
