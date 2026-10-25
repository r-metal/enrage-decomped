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
	private string _weather = "clear";

	private int _nextWeather;

	private uint _weatherHash;

	private static readonly Dictionary<uint, string> WeatherKinds = new Dictionary<uint, string>();

	private const ulong SetPedMotivation = 491572505205262965uL;

	private const ulong SetPedCombatStyle = 10001374397760965212uL;

	private const ulong SetPedDrunkness = 4642313281418361555uL;

	private const int FearState = 1;

	private const int AngryState = 2;

	private const int BraveState = 7;

	private int _mindAiLogged;

	private const ulong PlayEmote = 12905671096162301951uL;

	private const ulong FacialMood = 10032737698875811003uL;

	private const int EmoteReact = 0;

	private const int EmoteTaunt = 2;

	private const int EmoteUpperBody = 0;

	private static readonly Dictionary<Personality, uint[]> TauntEmotes = new Dictionary<Personality, uint[]>
	{
		[Personality.Hothead] = new uint[4] { 987239450u, 1897736735u, 1533402397u, 1256841324u },
		[Personality.Proud] = new uint[3] { 2190245723u, 1582069421u, 4271158938u },
		[Personality.Cold] = new uint[2] { 3741543167u, 1256841324u },
		[Personality.Greedy] = new uint[2] { 3499601683u, 1533402397u },
		[Personality.Loyal] = new uint[2] { 2190245723u, 3741543167u },
		[Personality.Drifter] = new uint[2] { 354512356u, 4128443908u },
		[Personality.Coward] = new uint[1] { 3125692224u }
	};

	private static readonly uint MoodPanic = 3729996742u;

	private static readonly uint MoodScared = 3716590166u;

	private static readonly uint MoodNervous = 3652126712u;

	private static readonly uint MoodAngry = 137506481u;

	private static readonly uint MoodCocky = 3070686211u;

	private static readonly uint MoodSmug = 3347446607u;

	private const ulong PrepareMusic = 2184674316064724362uL;

	private const ulong TriggerMusic = 8101227722246563600uL;

	private string _music;

	private int _musicStopAt = -1;

	private const ulong BlipAddModifier = 7362600660482055727uL;

	private const ulong FowReveal = 8301405158725569390uL;

	private const ulong RequestAnimDict = 12133439210843509940uL;

	internal string WeatherNow => _weather;

	private static string WeatherKindOf(uint h)
	{
		if (WeatherKinds.Count == 0)
		{
			K("fog", new string[3] { "FOG", "MISTY", "WHITEOUT" });
			K("storm", new string[6] { "THUNDERSTORM", "HURRICANE", "THUNDER", "BLIZZARD", "GROUNDBLIZZARD", "SANDSTORM" });
			K("rain", new string[5] { "RAIN", "DRIZZLE", "SHOWER", "SLEET", "HAIL" });
			K("snow", new string[2] { "SNOW", "SNOWLIGHT" });
			K("clear", new string[6] { "SUNNY", "CLOUDS", "OVERCAST", "OVERCASTDARK", "HIGHPRESSURE", "SNOWCLEARING" });
		}
		if (!WeatherKinds.TryGetValue(h, out var value))
		{
			return "clear";
		}
		return value;
		static void K(string kind, string[] names)
		{
			foreach (string text in names)
			{
				WeatherKinds[MISC.GET_HASH_KEY(text)] = kind;
			}
		}
	}

	private unsafe void UpdateWeather()
	{
		if (Game.GameTime < _nextWeather)
		{
			return;
		}
		_nextWeather = Game.GameTime + 5000;
		uint num = 0u;
		uint num2 = 0u;
		float num3 = 0f;
		MISC.GET_CURR_WEATHER_STATE(&num, &num2, &num3);
		uint num4 = ((num3 >= 0.5f && num2 != 0) ? num2 : num);
		if (num4 != _weatherHash)
		{
			_weatherHash = num4;
			string weather = _weather;
			_weather = WeatherKindOf(num4);
			if (weather != _weather)
			{
				GrudgeLog($"GAMESYS weather {weather} -> {_weather} (0x{num4:X8})");
			}
		}
	}

	internal float WeatherSightMult()
	{
		if (!Config.GsWeather)
		{
			return 1f;
		}
		switch (_weather)
		{
		case "fog":
			return 0.55f;
		case "storm":
			return 0.65f;
		case "rain":
		case "snow":
			return 0.8f;
		default:
			return 1f;
		}
	}

	internal float WeatherTensionMult()
	{
		if (!Config.GsWeather || !(_weather == "storm"))
		{
			return 1f;
		}
		return 0.7f;
	}

	internal void ApplyMindAi(Ped ped, NemesisRecord rec)
	{
		if (!Config.GsMinds || ped == null || !ped.Exists() || rec == null)
		{
			return;
		}
		try
		{
			Ped character = Game.Player.Character;
			float num = Math.Min(1f, (float)EffectiveFear(rec) / 100f);
			float num2 = Math.Min(1f, (float)rec.Hate / 100f);
			float num3 = (float)Mind.F(rec, "Bravery") / 100f;
			Function.Call(491572505205262965uL, ped.Handle, 1, num, 0);
			Function.Call(491572505205262965uL, ped.Handle, 2, num2, character.Handle);
			Function.Call(491572505205262965uL, ped.Handle, 7, num3, 0);
			string text = null;
			if (rec.Personality == Personality.Hothead || (rec.TraitsRolled && rec.Aggression >= 75))
			{
				text = "ChargeAttack";
			}
			else if (rec.Personality == Personality.Coward && Loadout.BandOf(rec) == Loadout.Band.Gun)
			{
				text = "StrafeOnly_Low";
			}
			if (text != null)
			{
				Function.Call(10001374397760965212uL, ped.Handle, MISC.GET_HASH_KEY(text), 1, -1f);
			}
			if (Quirks.Has(rec, "drunk"))
			{
				int cLOCK_HOURS = CLOCK.GET_CLOCK_HOURS();
				Function.Call(4642313281418361555uL, ped.Handle, true, (cLOCK_HOURS >= 20 || cLOCK_HOURS < 4) ? 0.5f : 0.25f);
			}
			if (_mindAiLogged++ < 20)
			{
				GrudgeLog(string.Format("GAMESYS mind AI {0}: fear {1:0.00}, anger {2:0.00}, brave {3:0.00}{4}{5}", rec.Name, num, num2, num3, (text != null) ? (", style " + text) : "", Quirks.Has(rec, "drunk") ? ", drunk" : ""));
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("GAMESYS mind AI failed: " + ex.Message);
		}
	}

	internal void TauntEmote(Ped ped, NemesisRecord rec)
	{
		if (Config.GsBody && !(ped == null) && ped.Exists() && !ped.IsOnMount)
		{
			uint[] value;
			uint[] array = ((rec != null && rec.Personality.HasValue && TauntEmotes.TryGetValue(rec.Personality.Value, out value)) ? value : TauntEmotes[Personality.Proud]);
			uint num = array[_rng.Next(array.Length)];
			Function.Call(12905671096162301951uL, ped.Handle, 2, 0, num, false, true, false, false, false);
		}
	}

	internal void BackDownEmote(Ped ped)
	{
		if (Config.GsBody && !(ped == null) && ped.Exists() && !ped.IsOnMount)
		{
			uint num = ((_rng.Next(2) == 0) ? 772373429u : 3472337526u);
			Function.Call(12905671096162301951uL, ped.Handle, 0, 0, num, false, true, false, false, false);
		}
	}

	private void InterroFace()
	{
		if (Config.GsBody && _convo?.It != null)
		{
			Interrogation it = _convo.It;
			if (!(it.Ped == null) && it.Ped.Exists() && !it.Ped.IsDead)
			{
				uint num = ((it.Terror >= 75f) ? MoodPanic : ((it.Terror >= 50f) ? MoodScared : ((it.Terror >= 25f) ? MoodNervous : ((it.Temper == Personality.Proud) ? MoodSmug : ((it.Temper == Personality.Hothead || it.Temper == Personality.Greedy) ? MoodCocky : MoodAngry)))));
				Function.Call(10032737698875811003uL, it.Ped.Handle, num, 6);
			}
		}
	}

	private void Music(string cue, string why)
	{
		if (Config.GsMusic && cue != null && !(cue == _music) && (!MISC.GET_MISSION_FLAG() || !(cue != "MC_MUSIC_STOP")))
		{
			try
			{
				Crumbs.Mark("music " + cue);
				FlushGrudgeLog(force: true);
				Function.Call<bool>(2184674316064724362uL, new InputArgument[1] { UiDraw.CString(cue) });
				bool flag = Function.Call<bool>(8101227722246563600uL, new InputArgument[1] { UiDraw.CString(cue) });
				GrudgeLog("GAMESYS music " + cue + " (" + why + ")" + (flag ? "" : " - the game didn't take it"));
			}
			catch (Exception ex)
			{
				GrudgeLog("GAMESYS music failed: " + ex.Message);
			}
			_music = ((cue == "MC_MUSIC_STOP" || cue.EndsWith("_STOP") || cue.EndsWith("_END_OS")) ? null : cue);
			_musicStopAt = -1;
		}
	}

	private void MusicStopSoon(int ms)
	{
		if (_music != null && _musicStopAt < 0)
		{
			_musicStopAt = Game.GameTime + ms;
		}
	}

	internal void MusicStandoff()
	{
		Music("DUEL_GENERAL_START", "standoff");
	}

	internal void MusicStandoffBroke()
	{
		if (_music == "DUEL_GENERAL_START")
		{
			Music("DUEL_GENERAL_DRAW", "the standoff broke");
		}
	}

	internal void MusicStandoffOver()
	{
		if (_music != null && _music.StartsWith("DUEL_GENERAL"))
		{
			Music("DUEL_GENERAL_STOP", "he backed down");
		}
	}

	private void UpdateMusic(Ped player)
	{
		if (!Config.GsMusic)
		{
			return;
		}
		if (MISC.GET_MISSION_FLAG() || player.IsDead)
		{
			if (_music != null)
			{
				Music("MC_MUSIC_STOP", "mission / death");
			}
			return;
		}
		if (_musicStopAt >= 0 && Game.GameTime >= _musicStopAt)
		{
			Music("MC_MUSIC_STOP", "cue over");
			return;
		}
		if (_music != null && _music.StartsWith("DUEL_GENERAL") && _enc == null && _musicStopAt < 0)
		{
			Music("DUEL_GENERAL_END_OS", "the encounter is over");
			return;
		}
		Vector3 pos = player.Position;
		LiveCamp liveCamp = (from lc in _liveCamps.Values
			where lc.Boss != null && lc.Boss.CampKnown
			orderby SitePos(lc.Site).DistanceTo(pos)
			select lc).FirstOrDefault();
		float num = ((liveCamp != null) ? SitePos(liveCamp.Site).DistanceTo(pos) : 9999f);
		if (liveCamp != null && liveCamp.Engaged && liveCamp.Peds.Any((Ped p) => p.Exists() && !p.IsDead) && num < 200f)
		{
			Music("SP_HIDEOUTS_GENERAL_ACTION", liveCamp.Boss.Name + "'s camp is fighting");
		}
		else if (liveCamp != null && liveCamp.Cleared && _music != null && _music.StartsWith("SP_HIDEOUTS"))
		{
			MusicStopSoon(6000);
		}
		else if (liveCamp != null && !liveCamp.Engaged && num < 90f && (_music == null || _music.StartsWith("SP_HIDEOUTS")))
		{
			Music("SP_HIDEOUTS_GENERAL_IDLE", "creeping up on " + liveCamp.Boss.Name + "'s camp");
		}
		else if (_music != null && _music.StartsWith("SP_HIDEOUTS") && num > 160f)
		{
			MusicStopSoon(6000);
		}
	}

	internal void StopModMusic()
	{
		if (_music != null)
		{
			Music("MC_MUSIC_STOP", "stopped");
		}
	}

	internal void FlashCampBlip(string campId)
	{
		if (!Config.GsMap || campId == null || !_campBlips.TryGetValue(campId, out var value) || value == 0)
		{
			return;
		}
		try
		{
			Function.Call<bool>(7362600660482055727uL, new InputArgument[2]
			{
				value,
				MISC.GET_HASH_KEY("BLIP_MODIFIER_FLASH_LONG")
			});
		}
		catch
		{
		}
	}

	private void RevealKnownCampsOnMap()
	{
		if (!Config.GsMap)
		{
			return;
		}
		int num = 0;
		foreach (string item in (from x in Roster
			where x.CampKnown && x.CampId != null && x.IsAlive
			select x.CampId).Distinct())
		{
			CampSite campSite = Camps.ById(item);
			if (campSite != null)
			{
				Vector3 vector = SitePos(campSite);
				Function.Call(8301405158725569390uL, vector.X, vector.Y, vector.Z, 0u);
				if (++num >= 8)
				{
					break;
				}
			}
		}
	}

	private void UpdateGameSystems(Ped player)
	{
		UpdateWeather();
		UpdateMusic(player);
		InterroFace();
		RevealKnownCampsOnMap();
	}

	internal string GameSystemsDebugLine()
	{
		return string.Format("game systems: weather {0} (sight x{1:0.##}){2}", _weather, WeatherSightMult(), (_music != null) ? (", music " + _music) : "") + (Config.GsMinds ? "" : ", minds off") + (Config.GsBody ? "" : ", body off") + (Config.GsMusic ? "" : ", music off") + (Config.GsMap ? "" : ", map off");
	}
}
