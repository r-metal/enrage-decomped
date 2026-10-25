using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Newtonsoft.Json;
using RDR2;
using RDR2.Math;
using RDR2.Native;

namespace NemesisSystem.BulletImpact;

public sealed class BulletImpactModule
{
	private const int OverlayLines = 6;

	private readonly BulletImpactSettings _cfg;

	private readonly ImpactDetector _detector = new ImpactDetector();

	private readonly string _logPath;

	private readonly XRayLog _xray;

	private readonly XRayViewer _viewer;

	private readonly Func<Vector3, string> _zoneOf;

	private readonly Func<int, NemesisTag> _nemesisOf;

	private readonly CrippleTracker _cripple;

	private readonly Action<string, CrippleLevel> _nemesisCrippled;

	private int _errorCount;

	private readonly List<(XRayEntry entry, int handle, int until)> _pendingDeaths = new List<(XRayEntry, int, int)>();

	private const int WatchForDeathMs = 30000;

	private readonly Dictionary<int, int> _victimIds = new Dictionary<int, int>();

	private int _nextVictimId;

	public Func<int, bool> HandsOff;

	private const int AnimalAimLingerMs = 1500;

	private int _animalAimAt = -1073741824;

	private int _damageNoteAt = -1073741824;

	private float _damageSet = -1f;

	private bool _damageFailed;

	private static readonly string[] KillKeys = new string[2] { "brain", "heart" };

	private const int AnimalLungsMs = 8000;

	private const ulong GetCleanlinessFn = 9867385503644060490uL;

	private const ulong SetCleanlinessFn = 8442122850154317733uL;

	private const ulong GetQualityFn = 8920611098360557866uL;

	private const ulong SetQualityFn = 14874130914120974523uL;

	private readonly List<(XRayEntry entry, int handle, int bone, int at, uint model)> _animalBleeds = new List<(XRayEntry, int, int, int, uint)>();

	public Func<int, string> AnimalHandsOff;

	private static readonly Dictionary<string, string> AnimalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["bearblack"] = "Black Bear",
		["bear"] = "Bear",
		["buck"] = "Buck",
		["deer"] = "Deer",
		["elk"] = "Elk",
		["moose"] = "Moose",
		["bighornram"] = "Bighorn Ram",
		["buffalo"] = "Bison",
		["bison"] = "Bison",
		["pronghorn"] = "Pronghorn",
		["wolf"] = "Wolf",
		["wolf_medium"] = "Wolf",
		["wolf_small"] = "Wolf",
		["coyote"] = "Coyote",
		["fox"] = "Fox",
		["cougar"] = "Cougar",
		["panther"] = "Panther",
		["boar"] = "Boar",
		["boarlegendary"] = "Legendary Boar",
		["pig"] = "Pig",
		["cow"] = "Cow",
		["bull"] = "Bull",
		["ox"] = "Ox",
		["sheep"] = "Sheep",
		["goat"] = "Goat",
		["rabbit"] = "Rabbit",
		["possum"] = "Possum",
		["raccoon"] = "Raccoon",
		["skunk"] = "Skunk",
		["badger"] = "Badger",
		["beaver"] = "Beaver",
		["armadillo"] = "Armadillo",
		["alligator"] = "Alligator",
		["donkey"] = "Donkey",
		["mule"] = "Mule",
		["cat"] = "Cat",
		["javelina"] = "Peccary",
		["muskrat"] = "Muskrat",
		["squirrel"] = "Squirrel"
	};

	private readonly Dictionary<int, int> _killShotAt = new Dictionary<int, int>();

	private const int BeatingGapMs = 20000;

	private readonly Dictionary<(int ped, BlowKind kind), (XRayEntry entry, int at)> _beatings = new Dictionary<(int, BlowKind), (XRayEntry, int)>();

	private readonly Dictionary<int, int> _shotAt = new Dictionary<int, int>();

	private readonly StringBuilder _logBuf = new StringBuilder();

	private int _nextLogFlush;

	public bool ViewerOpen => _viewer.IsOpen;

	public event Action SwitchToHierarchy;

	public BulletImpactModule(string scriptsDir, string dataDir, Func<Vector3, string> zoneOf, Func<int, NemesisTag> nemesisOf, Func<string, NemesisFate> fateOf, Action<string, CrippleLevel> nemesisCrippled)
	{
		_nemesisCrippled = nemesisCrippled;
		_cripple = new CrippleTracker(delegate(string line)
		{
			Log(line);
		});
		_cfg = new BulletImpactSettings(Path.Combine(scriptsDir, "BulletImpact.ini"));
		_logPath = Path.Combine(dataDir, "BulletImpact.log");
		_zoneOf = zoneOf;
		_nemesisOf = nemesisOf;
		try
		{
			AnimalAnatomy.TailIds = (from BoneID b in Enum.GetValues(typeof(BoneID))
				select (b: b, m: Regex.Match(b.ToString(), "^ID_SKEL_TAIL(\\d+)$", RegexOptions.IgnoreCase)) into x
				where x.m.Success
				orderby int.Parse(x.m.Groups[1].Value)
				select (int)x.b).Distinct().ToArray();
		}
		catch
		{
		}
		_xray = new XRayLog(Path.Combine(dataDir, "xray_log.json"), _cfg.XRayMaxEntries);
		DropKillShotEchoes();
		_nextVictimId = _xray.Entries.Select((XRayEntry e) => e.VictimId).DefaultIfEmpty(0).Max() + 1;
		XRayViewer.GlyphCost = _cfg.XRayGlyphCost;
		XRayViewer.Diag = delegate(string line)
		{
			Log(line);
		};
		ImpactDetector.AmmoNote = delegate(string line)
		{
			Log(line);
		};
		XRayRenderer.Diag = delegate(string line)
		{
			Log(line);
		};
		_viewer = new XRayViewer(_xray, _cfg.XRayQuality, _cfg.XRayRectLimit, fateOf, delegate(int n)
		{
			_cfg.XRayRectLimit = n;
			_cfg.Save();
		}, delegate(XRayQuality q)
		{
			_cfg.XRayQuality = q;
			_cfg.Save();
		});
		_viewer.SwitchToHierarchy += delegate
		{
			this.SwitchToHierarchy?.Invoke();
		};
		_detector.Impact += OnImpact;
		_detector.KilledOutCold += OnKilledOutCold;
		_detector.Diagnostic += delegate(string d)
		{
			if (_cfg.LogHits)
			{
				Log("DIAG " + d);
			}
		};
	}

	private bool Spared(int handle)
	{
		try
		{
			return HandsOff != null && HandsOff(handle);
		}
		catch
		{
			return true;
		}
	}

	internal static float DamageFor(bool mission, bool animalAim, float playerDamage)
	{
		if (!(mission || animalAim))
		{
			return playerDamage;
		}
		return 1f;
	}

	private unsafe bool AimingAtAnimal()
	{
		int handle = Game.Player.Handle;
		int num = 0;
		if (((PLAYER.GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(handle, &num) && num != 0) || (PLAYER.GET_PLAYER_TARGET_ENTITY(handle, &num) && num != 0)) && ENTITY.IS_ENTITY_A_PED(num) && !new Ped(num).IsHuman)
		{
			_animalAimAt = Game.GameTime;
		}
		return Game.GameTime - _animalAimAt < 1500;
	}

	private void SetPlayerDamage()
	{
		bool mISSION_FLAG = MISC.GET_MISSION_FLAG();
		bool flag = !mISSION_FLAG && AimingAtAnimal();
		float num = DamageFor(mISSION_FLAG, flag, _cfg.PlayerDamage);
		if (num != _damageSet || Game.FrameCount % 120 == 0)
		{
			PLAYER.SET_PLAYER_WEAPON_DAMAGE_MODIFIER(Game.Player.Handle, num);
			if (num != _damageSet && flag && Game.GameTime - _damageNoteAt > 60000)
			{
				_damageNoteAt = Game.GameTime;
				Log($"DAMAGE the game's own ({num:0.##}) - aiming at an animal (people: {_cfg.PlayerDamage:0.##})");
			}
			_damageSet = num;
		}
	}

	public void Tick(Ped player)
	{
		FlushLog();
		if (!_cfg.Enabled)
		{
			return;
		}
		try
		{
			try
			{
				SetPlayerDamage();
			}
			catch (Exception ex)
			{
				if (!_damageFailed)
				{
					_damageFailed = true;
					LogError(ex);
				}
			}
			_detector.Poll(player);
			if (_cfg.Crippling)
			{
				_cripple.Tick(player);
			}
			UpdatePendingDeaths();
			UpdateAnimalBleeds();
			_xray.SaveIfDirty();
			if (Game.FrameCount % 600 == 0)
			{
				PruneVictimIds();
			}
		}
		catch (Exception ex2)
		{
			LogError(ex2);
		}
	}

	public void Draw()
	{
		if (!_cfg.Enabled)
		{
			return;
		}
		try
		{
			if (_viewer.IsOpen)
			{
				Game.DisableAllControlsThisFrame();
				_viewer.Draw();
			}
		}
		catch (Exception ex)
		{
			LogError(ex);
		}
	}

	private void LogError(Exception ex)
	{
		if (++_errorCount <= 20)
		{
			Log("ERROR " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
		}
	}

	public void OpenViewer(string nemesisId = null)
	{
		if (_cfg.Enabled)
		{
			_viewer.Open(nemesisId);
		}
	}

	public void OpenViewerAt(int entryId)
	{
		if (_cfg.Enabled)
		{
			_viewer.OpenAt(entryId);
		}
	}

	public List<(int entryId, string victim, int shots, bool animal, bool killed)> SearchXRays()
	{
		if (!_cfg.Enabled)
		{
			return new List<(int, string, int, bool, bool)>();
		}
		return _viewer.SearchRows();
	}

	public void ClearXRayLog()
	{
		_xray.Clear();
		_xray.SaveIfDirty(force: true);
	}

	public int ShiftGameMinutes(long delta)
	{
		int num = 0;
		foreach (XRayEntry entry in _xray.Entries)
		{
			if (entry.GameMin > 0)
			{
				entry.GameMin += delta;
				num++;
			}
		}
		if (num > 0)
		{
			_xray.MarkDirty();
		}
		return num;
	}

	public void SaveXRayNow()
	{
		_xray.SaveIfDirty(force: true);
	}

	public void SaveXRayLater()
	{
		_xray.SaveInBackground();
	}

	public void ReloadXRayLog()
	{
		_viewer.Close();
		_xray.Reload();
		_animalBleeds.Clear();
		DropKillShotEchoes();
		_victimIds.Clear();
		_pendingDeaths.Clear();
		_nextVictimId = Math.Max(_nextVictimId, _xray.Entries.Select((XRayEntry e) => e.VictimId).DefaultIfEmpty(0).Max() + 1);
	}

	public int MoveForeignAside(Func<string, bool> onRoster, string toPath, out int men)
	{
		men = 0;
		try
		{
			IEnumerable<IGrouping<int, XRayEntry>> enumerable = from e in _xray.Entries
				where e.GameMin == 0
				group e by e.ShootoutId;
			List<XRayEntry> list = new List<XRayEntry>();
			foreach (IGrouping<int, XRayEntry> item in enumerable)
			{
				if (!item.Any((XRayEntry e) => e.NemesisId != null && onRoster(e.NemesisId)))
				{
					list.AddRange(item);
				}
			}
			if (list.Count == 0)
			{
				return 0;
			}
			men = list.Select((XRayEntry e) => (e.VictimId == 0) ? e.Victim : e.VictimId.ToString()).Distinct().Count();
			SafeFile.Write(toPath, JsonConvert.SerializeObject(list));
			HashSet<XRayEntry> hashSet = new HashSet<XRayEntry>(list);
			_xray.Entries.RemoveAll(hashSet.Contains);
			_xray.MarkDirty();
			_xray.SaveIfDirty(force: true);
			Log($"XRAY {list.Count} shots on {men} men from another playthrough moved to {Path.GetFileName(toPath)}");
			return list.Count;
		}
		catch (Exception ex)
		{
			Log("XRAY move-aside failed: " + ex.Message);
			return 0;
		}
	}

	private void DropKillShotEchoes()
	{
		try
		{
			List<XRayEntry> kills = _xray.Entries.Where((XRayEntry e) => e.Effect != null && e.Effect.StartsWith("Killed outright")).ToList();
			int num = _xray.Entries.RemoveAll((XRayEntry e) => !e.ImpactOnVictim && e.Effect == null && kills.Any((XRayEntry k) => k != e && ((k.VictimId != 0) ? (k.VictimId == e.VictimId) : (k.PedHandle == e.PedHandle)) && (e.Time - k.Time).TotalMilliseconds >= 0.0 && (e.Time - k.Time).TotalMilliseconds < 1000.0));
			if (num > 0)
			{
				_xray.MarkDirty();
				Log($"XRAY {num} echo entries of kill shots removed (the kill shot's own damage, logged as a second shot)");
			}
		}
		catch (Exception ex)
		{
			Log("XRAY echo clean-up failed: " + ex.Message);
		}
	}

	public void CloseViewer(bool now = false)
	{
		_viewer.Close();
		if (now)
		{
			_xray.SaveIfDirty(force: true);
		}
		else
		{
			_xray.SaveInBackground();
		}
	}

	public bool HandleKey(Keys key, bool allowOpen)
	{
		if (_viewer.IsOpen)
		{
			_viewer.HandleKey(key);
			if (!_viewer.IsOpen)
			{
				_xray.SaveInBackground();
			}
			return true;
		}
		return false;
	}

	public void Orbit(float dYaw, float dPitch)
	{
		if (_viewer.IsOpen)
		{
			_viewer.Orbit(dYaw, dPitch);
		}
	}

	public void HandleKeyUp(Keys key)
	{
		if (_viewer.IsOpen)
		{
			_viewer.HandleKeyUp(key);
		}
	}

	public WoundReport LatestWound(int pedHandle)
	{
		XRayEntry xRayEntry = _xray.Entries.LastOrDefault((XRayEntry x) => x.PedHandle == pedHandle && x.WeaponClass != WeaponClass.Blunt && (DateTime.Now - x.Time).TotalSeconds < 120.0);
		if (xRayEntry == null)
		{
			return null;
		}
		if (xRayEntry.Analysis == null)
		{
			xRayEntry.Analysis = XRayAnalysis.Build(xRayEntry);
		}
		WoundReport woundReport = new WoundReport
		{
			WeaponName = XRayViewer.PrettyWeapon(xRayEntry.WeaponName),
			Distance = xRayEntry.Distance
		};
		if (xRayEntry.Analysis.Main != null)
		{
			woundReport.EntryText = xRayEntry.Analysis.Main.EntryText;
		}
		woundReport.Damage.AddRange(xRayEntry.Analysis.AllDamage);
		return woundReport;
	}

	public CrippleLevel CrippleLevelOf(int handle)
	{
		return _cripple.LevelOf(handle);
	}

	public void ForgetCripples()
	{
		_cripple.Clear();
	}

	public void ApplyCondition(Ped ped, CrippleLevel level)
	{
		if (_cfg.Crippling)
		{
			_cripple.Apply(ped, level);
		}
	}

	public int ShotsOnNemesis(string nemesisId)
	{
		return _xray.Entries.Count((XRayEntry e) => e.NemesisId == nemesisId);
	}

	private void UpdatePendingDeaths()
	{
		for (int num = _pendingDeaths.Count - 1; num >= 0; num--)
		{
			(XRayEntry, int, int) tuple = _pendingDeaths[num];
			int handle = tuple.Item2;
			int item = tuple.Item3;
			Ped ped = Entity.FromHandle(handle) as Ped;
			bool flag = ped != null && ped.Exists() && ped.IsDead;
			int at = 0;
			XRayEntry xRayEntry = (flag ? LatestBeating(handle, out at) : null);
			if (xRayEntry != null && (!_shotAt.TryGetValue(handle, out var value) || value < at) && !Downed.IsFinished(handle))
			{
				if (!xRayEntry.KnockedOut)
				{
					xRayEntry.KnockedOut = true;
					xRayEntry.Analysis = null;
					_xray.MarkDirty();
					Log($"KNOCKOUT ped={handle} - out cold, not dead");
				}
				if (Game.GameTime > item || !ped.Exists())
				{
					_pendingDeaths.RemoveAt(num);
				}
			}
			else
			{
				if (flag)
				{
					foreach (XRayEntry item2 in _xray.Entries.Where((XRayEntry x) => x.PedHandle == handle))
					{
						item2.Killed = true;
					}
					XRayEntry xRayEntry2 = _xray.Entries.LastOrDefault((XRayEntry x) => x.PedHandle == handle && x.LeftForDead) ?? _xray.Entries.LastOrDefault((XRayEntry x) => x.PedHandle == handle);
					if (xRayEntry2 != null)
					{
						xRayEntry2.FatalShot = true;
					}
					_xray.MarkDirty();
				}
				if (flag || ped == null || !ped.Exists() || Game.GameTime > item)
				{
					_pendingDeaths.RemoveAt(num);
				}
			}
		}
	}

	private void Cripple(XRayEntry entry, int handle)
	{
		Ped ped = Entity.FromHandle(handle) as Ped;
		if (ped == null || !ped.Exists() || ped.IsDead || Kids.Protected(ped) || Spared(handle))
		{
			return;
		}
		eRelationshipType relationshipWithPed = ped.GetRelationshipWithPed(Game.Player.Character);
		if (relationshipWithPed == eRelationshipType.Like || relationshipWithPed == eRelationshipType.Respect)
		{
			return;
		}
		if (entry.Analysis == null)
		{
			entry.Analysis = XRayAnalysis.Build(entry);
		}
		string effect;
		CrippleLevel crippleLevel = CrippleRules.Assess(entry.Analysis.AllDamage, out effect);
		if (crippleLevel == CrippleLevel.None)
		{
			return;
		}
		CrippleLevel crippleLevel2 = _cripple.LevelOf(handle);
		CrippleLevel crippleLevel3 = _cripple.Apply(ped, crippleLevel);
		if (crippleLevel3 > crippleLevel2)
		{
			entry.Effect = effect;
			NemesisTag nemesisTag = _nemesisOf(handle);
			if (nemesisTag != null && !nemesisTag.IsFollower)
			{
				_nemesisCrippled?.Invoke(nemesisTag.Id, crippleLevel3);
			}
		}
	}

	private bool KillShot(XRayEntry entry, ImpactRecord rec)
	{
		if (entry.Analysis == null)
		{
			entry.Analysis = XRayAnalysis.Build(entry);
		}
		DamageItem damageItem = entry.Analysis.AllDamage.FirstOrDefault((DamageItem d) => !d.Chipped && KillKeys.Contains(d.Key));
		if (damageItem == null)
		{
			return false;
		}
		Ped ped = Entity.FromHandle(rec.PedHandle) as Ped;
		if (ped == null || !ped.Exists() || ped.IsDead || Kids.Protected(ped) || Spared(ped.Handle))
		{
			return false;
		}
		eRelationshipType relationshipWithPed = ped.GetRelationshipWithPed(Game.Player.Character);
		if (relationshipWithPed == eRelationshipType.Like || relationshipWithPed == eRelationshipType.Respect)
		{
			return false;
		}
		int handle = Game.Player.Character.Handle;
		_killShotAt[ped.Handle] = Game.GameTime;
		PED.APPLY_DAMAGE_TO_PED(ped.Handle, ped.Health + 500, 0, rec.BoneId, handle);
		if (!ped.IsDead)
		{
			ped.Health = 0;
		}
		entry.Effect = ((damageItem.Key == "brain") ? "Killed outright - brain destroyed" : "Killed outright - heart pierced");
		entry.Killed = (entry.FatalShot = true);
		Log($"KILLSHOT ped={rec.PedHandle} {damageItem.Name}");
		return true;
	}

	private string AnimalOff(int handle)
	{
		try
		{
			return AnimalHandsOff?.Invoke(handle);
		}
		catch
		{
			return "left alone";
		}
	}

	private void AnimalKillShot(XRayEntry entry, ImpactRecord rec)
	{
		if (entry.Analysis == null)
		{
			entry.Analysis = XRayAnalysis.Build(entry);
		}
		XRayAnalysis analysis = entry.Analysis;
		if (analysis.Main == null || analysis.Main.Guessed)
		{
			return;
		}
		string text = AnimalAnatomy.Drop(new PathResult[1] { analysis.Main }.Concat(analysis.Pellets).SelectMany((PathResult p) => p.Damage));
		if (text == null)
		{
			return;
		}
		string text2 = AnimalOff(rec.PedHandle);
		if (text2 != null)
		{
			entry.Effect = "Kept going - " + text2;
			Log($"KILLSHOT animal ped={rec.PedHandle} {text} - not dropped: {text2}");
		}
		else if (text == "lungs")
		{
			if (!_animalBleeds.Any(((XRayEntry entry, int handle, int bone, int at, uint model) b) => b.handle == rec.PedHandle))
			{
				_animalBleeds.Add((entry, rec.PedHandle, rec.BoneId, Game.GameTime + 8000, ENTITY.GET_ENTITY_MODEL(rec.PedHandle)));
			}
			Log($"KILLSHOT animal ped={rec.PedHandle} both lungs - down within {8} s");
		}
		else
		{
			DropAnimal(entry, rec.PedHandle, rec.BoneId, text);
		}
	}

	private void UpdateAnimalBleeds()
	{
		for (int num = _animalBleeds.Count - 1; num >= 0; num--)
		{
			var (entry, num2, bone, num3, num4) = _animalBleeds[num];
			if (Game.GameTime >= num3)
			{
				_animalBleeds.RemoveAt(num);
				if (ENTITY.DOES_ENTITY_EXIST(num2) && !ENTITY.IS_ENTITY_DEAD(num2) && AnimalOff(num2) == null && ENTITY.GET_ENTITY_MODEL(num2) == num4 && !PED.IS_PED_HUMAN(num2))
				{
					DropAnimal(entry, num2, bone, "lungs");
				}
			}
		}
	}

	private void DropAnimal(XRayEntry entry, int handle, int bone, string drop)
	{
		Ped ped = Entity.FromHandle(handle) as Ped;
		if (ped == null || !ped.Exists() || ped.IsDead || ped.IsHuman)
		{
			return;
		}
		int num = -1;
		int num2 = -1;
		int num3 = -1;
		try
		{
			num = Function.Call<int>(9867385503644060490uL, new InputArgument[1] { handle });
			num2 = Function.Call<int>(8920611098360557866uL, new InputArgument[1] { handle });
		}
		catch
		{
		}
		_killShotAt[handle] = Game.GameTime;
		PED.APPLY_DAMAGE_TO_PED(handle, ped.Health + 500, 0, bone, Game.Player.Character.Handle);
		if (!ped.IsDead)
		{
			ped.Health = 0;
		}
		try
		{
			num3 = Function.Call<int>(9867385503644060490uL, new InputArgument[1] { handle });
			if (num >= 0 && num3 != num)
			{
				Function.Call(8442122850154317733uL, handle, num);
			}
			if (num2 >= 0 && Function.Call<int>(8920611098360557866uL, new InputArgument[1] { handle }) != num2)
			{
				Function.Call(14874130914120974523uL, handle, num2);
			}
		}
		catch
		{
		}
		entry.Effect = drop switch
		{
			"spine" => "Dropped where it stood - spine severed", 
			"neck" => "Dropped where it stood - neck broken", 
			"heart" => "Dropped - heart shot", 
			"brain" => "Dropped on the spot - brain destroyed", 
			_ => "Down in seconds - both lungs", 
		};
		entry.Killed = true;
		if (drop != "lungs")
		{
			entry.FatalShot = true;
			entry.StillUp = false;
		}
		_xray.MarkDirty();
		Log(string.Format("KILLSHOT animal ped={0} {1} - dropped (pelt cleanliness {2}{3}, quality {4})", handle, drop, num, (num3 != num) ? $", {num3} after - put back" : "", num2));
	}

	public void TagVictim(int pedHandle, string nemesisId, string name)
	{
		if (!_victimIds.TryGetValue(pedHandle, out var vid))
		{
			return;
		}
		bool flag = false;
		foreach (XRayEntry item in _xray.Entries.Where((XRayEntry x) => x.VictimId == vid && x.NemesisId == null && x.FollowerOf == null))
		{
			item.NemesisId = nemesisId;
			item.NemesisName = name;
			flag = true;
		}
		if (flag)
		{
			_xray.MarkDirty();
		}
	}

	public int RenameVictims(Func<string, uint, string> rename)
	{
		int num = 0;
		foreach (XRayEntry entry in _xray.Entries)
		{
			if (entry != null && entry.NemesisId == null)
			{
				string text = rename(entry.Victim, entry.VictimModel);
				if (text != entry.Victim)
				{
					entry.Victim = text;
					num++;
				}
			}
		}
		if (num > 0)
		{
			_xray.MarkDirty();
			_xray.SaveIfDirty(force: true);
		}
		return num;
	}

	public void NameVictim(int pedHandle, string name)
	{
		if (string.IsNullOrEmpty(name) || !_victimIds.TryGetValue(pedHandle, out var vid))
		{
			return;
		}
		bool flag = false;
		foreach (XRayEntry item in _xray.Entries.Where((XRayEntry x) => x.VictimId == vid && x.Victim != name))
		{
			item.Victim = name;
			flag = true;
		}
		if (flag)
		{
			_xray.MarkDirty();
		}
	}

	private int VictimIdFor(int handle)
	{
		if (!_victimIds.TryGetValue(handle, out var value))
		{
			value = (_victimIds[handle] = _nextVictimId++);
		}
		return value;
	}

	private void PruneVictimIds()
	{
		foreach (int item in _victimIds.Keys.ToList())
		{
			Ped ped = Entity.FromHandle(item) as Ped;
			if (ped == null || !ped.Exists())
			{
				_victimIds.Remove(item);
			}
		}
	}

	internal static string PrettyVictim(uint model)
	{
		Gang gang = Gangs.ForModel(model);
		if (gang == null)
		{
			return "Stranger";
		}
		return (gang.Name.StartsWith("The ") ? gang.Name.Substring(4) : gang.Name) + " man";
	}

	internal static string AnimalName(uint model)
	{
		if (!Enum.IsDefined(typeof(PedHash), model))
		{
			return "Animal";
		}
		PedHash pedHash = (PedHash)model;
		string text = pedHash.ToString().ToLowerInvariant();
		if (text.StartsWith("a_c_"))
		{
			text = text.Substring(4);
		}
		if (text.StartsWith("horse") || text.StartsWith("horsemule"))
		{
			if (!text.Contains("mule"))
			{
				return "Horse";
			}
			return "Mule";
		}
		if (text.StartsWith("dog"))
		{
			return "Dog";
		}
		string key = string.Join("_", from t in text.Split('_')
			where t.Length > 0 && !t.All(char.IsDigit)
			select t);
		if (AnimalNames.TryGetValue(key, out var value))
		{
			return value;
		}
		string text2 = text.Split('_')[0];
		if (AnimalNames.TryGetValue(text2, out value))
		{
			return value;
		}
		if (text2.Length <= 0)
		{
			return "Animal";
		}
		return char.ToUpperInvariant(text2[0]) + text2.Substring(1);
	}

	internal static string ModelLabel(uint model)
	{
		if (!Enum.IsDefined(typeof(PedHash), model))
		{
			return "Stranger";
		}
		PedHash pedHash = (PedHash)model;
		List<string> list = (from p in pedHash.ToString().Split('_')
			where p.Length > 0 && !p.All(char.IsDigit)
			select p).ToList();
		IEnumerable<string> source = ((list.Count >= 4 && list[0].Length <= 2) ? list.Skip(3) : list.Skip(1));
		string text = string.Join(" ", source.Select((string w) => char.ToUpperInvariant(w[0]) + w.Substring(1)));
		if (text.Length <= 0)
		{
			return "Stranger";
		}
		return text;
	}

	private void OnImpact(ImpactRecord rec)
	{
		if (_killShotAt.TryGetValue(rec.PedHandle, out var value))
		{
			if (Game.GameTime - value < 600)
			{
				Log($"HIT ped={rec.PedHandle} dmg={rec.Damage:0.#} - the kill shot's own damage, not a shot (skipped)");
				return;
			}
			_killShotAt.Remove(rec.PedHandle);
		}
		if (rec.Class == WeaponClass.Blunt)
		{
			OnBlow(rec);
			return;
		}
		_shotAt[rec.PedHandle] = Game.GameTime;
		if (_shotAt.Count > 64)
		{
			foreach (int item in (from kv in _shotAt
				where Game.GameTime - kv.Value > 60000
				select kv.Key).ToList())
			{
				_shotAt.Remove(item);
			}
		}
		if (rec.BonesLocal.Count > 0)
		{
			XRayEntry xRayEntry = NewEntry(rec);
			_xray.Add(xRayEntry);
			if (!rec.VictimDeadAfterHit)
			{
				_pendingDeaths.Add((xRayEntry, rec.PedHandle, Game.GameTime + 30000));
			}
			bool flag = rec.EventIndex == -1 || rec.Animal;
			if ((flag || rec.VictimDeadAfterHit || !_cfg.KillShots || !KillShot(xRayEntry, rec)) && !flag && _cfg.Crippling && !rec.VictimDeadAfterHit)
			{
				Cripple(xRayEntry, rec.PedHandle);
			}
			if (rec.Animal && rec.EventIndex != -1 && !rec.VictimDeadAfterHit && _cfg.KillShots)
			{
				AnimalKillShot(xRayEntry, rec);
			}
		}
		string text;
		if (!Enum.IsDefined(typeof(PedHash), rec.PedModel))
		{
			text = $"0x{rec.PedModel:X8}";
		}
		else
		{
			PedHash pedModel = (PedHash)rec.PedModel;
			text = pedModel.ToString();
		}
		string text2 = text;
		Log($"HIT shot={rec.ShotId} ev={rec.EventIndex} pellets={rec.PelletsOnVictim} ped={rec.PedHandle} model={text2} bone={rec.BoneId} name={rec.BoneName} index={rec.BoneIndex} " + $"weapon={rec.WeaponName} dmg={rec.Damage:0.#} dist={rec.Distance:0.0}m dead={rec.VictimDeadAfterHit} " + "impact=" + (rec.HasImpact ? Fmt(rec.Impact) : "none") + " muzzle=" + Fmt(rec.Muzzle) + " via " + rec.MuzzleSource);
	}

	private XRayEntry LatestBeating(int ped, out int at)
	{
		at = int.MinValue;
		XRayEntry result = null;
		foreach (KeyValuePair<(int, BlowKind), (XRayEntry, int)> beating in _beatings)
		{
			if (beating.Key.Item1 == ped && beating.Value.Item2 > at && _xray.Entries.Contains(beating.Value.Item1))
			{
				at = beating.Value.Item2;
				result = beating.Value.Item1;
			}
		}
		return result;
	}

	private void OnBlow(ImpactRecord rec)
	{
		if (rec.BonesLocal.Count == 0)
		{
			return;
		}
		Blow blow = new Blow
		{
			Bone = rec.BlowBone,
			Dir = rec.DirLocal,
			Butt = rec.Butt,
			Kick = rec.Kick,
			Choke = rec.Choke,
			HeldMs = rec.HeldMs,
			Stomp = rec.Stomp,
			Down = rec.Down
		};
		(int, BlowKind) key = (rec.PedHandle, blow.Kind);
		XRayEntry xRayEntry;
		if (_beatings.TryGetValue(key, out (XRayEntry, int) value) && Game.GameTime - value.Item2 < 20000 && !value.Item1.Killed && _xray.Entries.Contains(value.Item1))
		{
			xRayEntry = value.Item1.Copy();
			xRayEntry.Blows = new List<Blow>(value.Item1.Blows ?? new List<Blow>());
			Blow blow2 = ((xRayEntry.Blows.Count > 0) ? xRayEntry.Blows[xRayEntry.Blows.Count - 1] : null);
			if (blow.Choke && blow2 != null && blow2.Choke)
			{
				xRayEntry.Blows[xRayEntry.Blows.Count - 1] = new Blow
				{
					Bone = blow2.Bone,
					Dir = blow2.Dir,
					Choke = true,
					HeldMs = blow2.HeldMs + blow.HeldMs
				};
			}
			else
			{
				xRayEntry.Blows.Add(blow);
			}
			_xray.Replace(value.Item1, xRayEntry);
		}
		else
		{
			xRayEntry = NewEntry(rec);
			xRayEntry.Blows = new List<Blow> { blow };
			_xray.Add(xRayEntry);
		}
		xRayEntry.Pellets = xRayEntry.Blows.Count;
		xRayEntry.Killed = (xRayEntry.FatalShot = false);
		if (ImpactDetector.KilledByGroundBlow(rec.VictimDeadAfterHit, rec.Down, rec.Butt, rec.Choke, rec.Stomp, rec.Damage) && !Downed.IsFinished(rec.PedHandle) && !Spared(rec.PedHandle))
		{
			xRayEntry.LeftForDead = true;
			Downed.KickedToDeath(rec.PedHandle);
			Log($"STOMPED TO DEATH ped={rec.PedHandle} - your stomp took his last health ({rec.Damage:0.#}) lying there: the game killed him, not out cold");
		}
		else if (ImpactDetector.KilledByFightChoke(rec.VictimDeadAfterHit, rec.Choke, rec.FightChoke) && !Downed.IsFinished(rec.PedHandle) && !Spared(rec.PedHandle))
		{
			Downed.ChokedToDeath(rec.PedHandle);
			Log($"CHOKED TO DEATH ped={rec.PedHandle} - choked limp in a fight (held {(float)rec.HeldMs / 1000f:0.0} s): presumed dead, not out cold");
		}
		else if (rec.VictimDeadAfterHit && !xRayEntry.KnockedOut)
		{
			xRayEntry.KnockedOut = true;
		}
		_beatings[key] = (xRayEntry, Game.GameTime);
		_pendingDeaths.RemoveAll(((XRayEntry entry, int handle, int until) p) => p.handle == rec.PedHandle);
		_pendingDeaths.Add((xRayEntry, rec.PedHandle, Game.GameTime + 30000));
		if (_beatings.Count > 32)
		{
			foreach (var item in (from kv in _beatings
				where Game.GameTime - kv.Value.at > 20000
				select kv.Key).ToList())
			{
				_beatings.Remove(item);
			}
		}
		Log(string.Format("BLOW ped={0} {1} game bone={2} ({3}) -> landed on {4} ({5}) dmg={6:0.#} via {7} - {8} {9} on him{10}", rec.PedHandle, rec.Butt ? ("gun butt " + rec.WeaponName) : (rec.Choke ? "choke" : (rec.Stomp ? "stomp" : (rec.Kick ? "kick" : "punch"))), rec.BoneId, rec.BoneName, (BoneID)rec.BlowBone, Anatomy.RegionOfBone(rec.BlowBone), rec.Damage, rec.MuzzleSource, (blow.Kind == BlowKind.Kick) ? "kick" : ((blow.Kind == BlowKind.Gun) ? "gun blow" : ((blow.Kind == BlowKind.Choke) ? "choke" : "punch")), xRayEntry.Blows.Count, xRayEntry.KnockedOut ? ", out cold" : ""));
	}

	private void OnKilledOutCold(int handle, bool byYou)
	{
		if (!Downed.IsFinished(handle) && !Spared(handle))
		{
			int at;
			XRayEntry xRayEntry = LatestBeating(handle, out at);
			_pendingDeaths.RemoveAll(((XRayEntry entry, int handle, int until) p) => p.handle == handle);
			if (byYou && xRayEntry != null)
			{
				xRayEntry.LeftForDead = true;
				Downed.KickedToDeath(handle);
				_pendingDeaths.Add((xRayEntry, handle, Game.GameTime + 30000));
			}
			else
			{
				Downed.DiedOutCold(handle);
			}
			_xray.MarkDirty();
		}
	}

	private XRayEntry NewEntry(ImpactRecord rec)
	{
		NemesisTag nemesisTag = _nemesisOf(rec.PedHandle);
		Ped ped = Entity.FromHandle(rec.PedHandle) as Ped;
		int num = ((ped != null && ped.Exists()) ? ped.MaxHealth : 0);
		return new XRayEntry
		{
			Time = DateTime.Now,
			GameMin = (NemesisRecord.GameClock?.Invoke() ?? 0),
			Location = _zoneOf(rec.Muzzle),
			Victim = (rec.Animal ? AnimalName(rec.PedModel) : (nemesisTag?.PersonName ?? PrettyVictim(rec.PedModel))),
			Animal = rec.Animal,
			VictimModel = rec.PedModel,
			VictimId = VictimIdFor(rec.PedHandle),
			NemesisId = ((nemesisTag != null && !nemesisTag.IsFollower) ? nemesisTag.Id : null),
			NemesisName = ((nemesisTag != null && !nemesisTag.IsFollower && nemesisTag.Id != null) ? nemesisTag.Name : null),
			FollowerOf = ((nemesisTag != null && nemesisTag.IsFollower) ? nemesisTag.Name : null),
			WeaponHash = rec.WeaponHash,
			WeaponName = rec.WeaponName,
			WeaponClass = rec.Class,
			Ammo = rec.Ammo,
			Pellets = rec.PelletsOnVictim,
			Distance = rec.Distance,
			RecordedBoneId = rec.BoneId,
			ImpactOnVictim = rec.ImpactOnVictim,
			Entry = rec.EntryLocal,
			Dir = rec.DirLocal,
			Muzzle = rec.MuzzleLocal,
			Bones = rec.BonesLocal,
			PedHandle = rec.PedHandle,
			Killed = rec.VictimDeadAfterHit,
			FatalShot = rec.VictimDeadAfterHit,
			StillUp = (rec.Animal && !rec.VictimDeadAfterHit),
			HitShare = ((num > 0) ? (Math.Max(0f, rec.Damage) / (float)num) : 0f)
		};
	}

	public void FlushLog(bool force = false)
	{
		if (_logBuf.Length != 0 && (force || _logBuf.Length >= 16000 || Environment.TickCount >= _nextLogFlush))
		{
			_nextLogFlush = Environment.TickCount + 1000;
			try
			{
				SafeFile.Append(_logPath, _logBuf.ToString());
			}
			catch
			{
			}
			_logBuf.Clear();
		}
	}

	private static string Fmt(Vector3 v)
	{
		return $"({v.X:0.00},{v.Y:0.00},{v.Z:0.00})";
	}

	private void Log(string line)
	{
		_logBuf.Append($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
		if (_logBuf.Length > 16000)
		{
			FlushLog();
		}
	}
}
