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
	private static readonly Queue<(int id, int at)> _uiSoundIds = new Queue<(int, int)>();

	internal static Action<string> SoundLog;

	private static readonly HashSet<string> _soundSetMissLogged = new HashSet<string>();

	private int _menuDrawFails;

	private int _nextIniLook;

	private bool _settingsEdited;

	private string _keysNotice;

	private const int MenuAgainMs = 4000;

	private bool _menuHeld;

	private int _menuHeldAt;

	private int _menuKeyNotes;

	private void DrainKeys()
	{
		int num = 0;
		while (_keyQueue.Count > 0 && num++ < 32)
		{
			var (flag, e) = _keyQueue.Dequeue();
			if (flag)
			{
				Guard("key " + e.KeyCode, delegate
				{
					OnKeyDown(null, e);
				});
				continue;
			}
			try
			{
				_bulletImpact.HandleKeyUp(e.KeyCode);
			}
			catch (Exception ex)
			{
				GrudgeLog("ERROR in key up: " + ex.Message);
			}
		}
	}

	private void OnKeyDown(object sender, KeyEventArgs e)
	{
		XPad.NoteKeyboard();
		UiMouse.NoteKey(e.KeyCode);
		if (_iniPanel.Capturing)
		{
			_iniPanel.Capture(e.KeyCode, e.Modifiers & Keys.Modifiers);
			return;
		}
		if (_welcome.Capturing)
		{
			_welcome.Capture(e.KeyCode, e.Modifiers & Keys.Modifiers);
			return;
		}
		Keys rosterMenuKey = Config.RosterMenuKey;
		(Keys, Keys, Keys) tuple = KeyCombo.Extras(rosterMenuKey);
		if (tuple.Item1 != Keys.None && KeyCombo.Is(e, rosterMenuKey, tuple.Item1))
		{
			if (!_iniPanel.IsOpen && (_menu.IsOpen || _bulletImpact.ViewerOpen || _convo != null || _tg != null))
			{
				ShowNotification("enRAGE: close the menu first to open the settings.");
			}
			else
			{
				_iniPanel.Toggle();
			}
		}
		else
		{
			if (_iniPanel.IsOpen && _iniPanel.HandleKey(e.KeyCode, e.Shift))
			{
				return;
			}
			if (_welcome.IsOpen)
			{
				_welcome.HandleKey(e.KeyCode);
			}
			else
			{
				if (PaperKey(e.KeyCode))
				{
					return;
				}
				if (e.Control && !e.Alt && !e.Shift && e.KeyCode == Config.ForceAmbushKey)
				{
					if (_debugOn || _spawnMenuOpen)
					{
						ToggleSpawnMenu();
					}
				}
				else if (_spawnMenuOpen)
				{
					SpawnMenuKey(e.KeyCode);
				}
				else if (_search != null)
				{
					if (!e.Control && !e.Alt)
					{
						if (!UiMouse.Sending)
						{
							_search.Pad = false;
							_search.OnKeys = false;
						}
						SearchKey(e.KeyCode, e.Shift);
					}
				}
				else if (e.Control && e.KeyCode == Keys.F && (_menu.IsOpen || _bulletImpact.ViewerOpen))
				{
					OpenSearch(pad: false);
				}
				else if (e.KeyCode == Keys.Tab && !e.Control && !e.Alt && !UiMouse.Sending && (_menu.IsOpen || _bulletImpact.ViewerOpen))
				{
					if (_tabDownAt == 0)
					{
						_tabDownAt = Environment.TickCount | 1;
					}
				}
				else if (tuple.Item3 != Keys.None && KeyCombo.Is(e, rosterMenuKey, tuple.Item3))
				{
					if (Config.DebugEnabled || _debugOn)
					{
						ToggleDebug();
					}
				}
				else if (tuple.Item2 != Keys.None && KeyCombo.Is(e, rosterMenuKey, tuple.Item2))
				{
					UndoRestore();
				}
				else if (KeyCombo.Is(e, rosterMenuKey))
				{
					Dispatch(rosterMenuKey);
				}
				else
				{
					if (e.KeyCode == KeyCombo.Base(rosterMenuKey))
					{
						MenuKeyNote("got " + KeyCombo.Name(e.KeyCode | (e.Modifiers & Keys.Modifiers)) + " - not the menu key (the modifiers differ)", routine: true);
					}
					if ((KeyCombo.Mods(rosterMenuKey) != Keys.None || e.KeyCode != KeyCombo.Base(rosterMenuKey)) && e.KeyCode != Config.ForceAmbushKey)
					{
						Dispatch(e.KeyCode);
					}
				}
			}
		}
	}

	internal static void UiSound(string name)
	{
		try
		{
			string text;
			string text2;
			if (UiSounds.TryGetValue(name, out (string, string) value))
			{
				(text, text2) = value;
			}
			else
			{
				text2 = "HUD_PLAYER_MENU";
				text = name;
			}
			Crumbs.Native("cs sound release");
			while (_uiSoundIds.Count > 0 && (_uiSoundIds.Count > 12 || UiDraw.RealMs - _uiSoundIds.Peek().at > 2000))
			{
				AUDIO.RELEASE_SOUND_ID(_uiSoundIds.Dequeue().id);
			}
			if (text2 != "HUD_PLAYER_MENU")
			{
				Crumbs.Native("cs soundset prepare");
				if (!AUDIO.PREPARE_SOUNDSET(text2, p1: false) && _soundSetMissLogged.Add(text2))
				{
					SoundLog?.Invoke("SOUND set " + text2 + " not loaded yet (asked for it) - '" + text + "' may be silent");
				}
			}
			int sOUND_ID = AUDIO.GET_SOUND_ID();
			if (sOUND_ID < 0)
			{
				if (_soundSetMissLogged.Add("#id"))
				{
					SoundLog?.Invoke($"SOUND no sound id free ({_uiSoundIds.Count} held) - '{text}' skipped");
				}
				return;
			}
			Crumbs.Native("cs sound");
			Function.Call(14870059130450844401uL, sOUND_ID, text, text2, true);
			_uiSoundIds.Enqueue((sOUND_ID, UiDraw.RealMs));
		}
		catch
		{
		}
	}

	internal static void ReleaseUiSounds()
	{
		Crumbs.Native("cs sound release all");
		while (_uiSoundIds.Count > 0)
		{
			try
			{
				AUDIO.RELEASE_SOUND_ID(_uiSoundIds.Dequeue().id);
			}
			catch
			{
			}
		}
	}

	private void MenuKeySound(Keys key)
	{
		if (!_menu.IsOpen && !_bulletImpact.ViewerOpen && _convo == null && _tg == null)
		{
			return;
		}
		switch (key)
		{
		case Keys.Up:
			UiSound("NAV_UP");
			return;
		case Keys.Down:
			UiSound("NAV_DOWN");
			return;
		case Keys.Left:
			UiSound("NAV_LEFT");
			return;
		case Keys.Right:
			UiSound("NAV_RIGHT");
			return;
		case Keys.Return:
			UiSound("SELECT");
			return;
		case Keys.Back:
		case Keys.Escape:
			UiSound("BACK");
			return;
		case Keys.Prior:
		case Keys.Q:
			UiSound("BUMPER_LEFT");
			return;
		case Keys.Tab:
		case Keys.Next:
		case Keys.E:
			UiSound("BUMPER_RIGHT");
			return;
		}
		if (key >= Keys.D1 && key <= Keys.D9 && (_convo != null || _tg != null))
		{
			UiSound("SELECT");
		}
	}

	private void Dispatch(Keys key)
	{
		if (key == Keys.Escape && (_menu.IsOpen || _bulletImpact.ViewerOpen || _convo != null || _tg != null))
		{
			key = Keys.Back;
		}
		if (key == Keys.O && (_menu.IsOpen || _bulletImpact.ViewerOpen) && _convo == null && _tg == null)
		{
			_bulletImpact.CloseViewer();
			OpenSetupScreen();
			return;
		}
		MenuKeySound(key);
		if ((_convo != null && HandleConvoKey(key)) || (_tg != null && HandleTelegraphKey(key)) || (key == Config.TelegraphKey && !_menu.IsOpen && !_bulletImpact.ViewerOpen && OnTelegraphKey()))
		{
			return;
		}
		if (_bulletImpact.ViewerOpen)
		{
			if (key == Config.RosterMenuKey)
			{
				_bulletImpact.CloseViewer();
				_menu.Open();
			}
			else if (key == Keys.Home)
			{
				_bulletImpact.CloseViewer();
				_menu.Open();
				_menu.ShowArthur();
				UiSound("SELECT");
			}
			else
			{
				_bulletImpact.HandleKey(key, allowOpen: false);
			}
		}
		else if (_menu.IsOpen && key == Keys.Tab)
		{
			if (!_menu.InWorld)
			{
				_menu.ShowWorld();
				return;
			}
			_menu.Toggle();
			_bulletImpact.OpenViewer();
		}
		else
		{
			if (_menu.IsOpen && _menu.HandleKey(key))
			{
				return;
			}
			bool flag = _convo != null || _tg != null;
			bool allowOpen = !flag && !_menu.IsOpen && !WorldBusy(Game.Player.Character);
			if (_bulletImpact.HandleKey(key, allowOpen))
			{
				return;
			}
			if (key == Config.InterrogateKey && !_menu.IsOpen)
			{
				OnInterrogateKey();
			}
			else
			{
				if (key != Config.RosterMenuKey)
				{
					return;
				}
				bool soft = false;
				string text = (_menu.IsOpen ? null : (flag ? "a panel is open" : MenuBlocked(out soft)));
				if (text != null && soft && _menuHeld && Environment.TickCount - _menuHeldAt < 4000)
				{
					_menuHeld = false;
					MenuKeyNote("opened anyway (second press) - " + text);
					text = null;
				}
				if (text != null)
				{
					MenuKeyNote("blocked - " + text);
					if (soft)
					{
						_menuHeld = true;
						_menuHeldAt = Environment.TickCount;
						ShowNotification("enRAGE: a game screen seems to be open, so the menu waited. Press " + KeyCombo.MenuText + " again to open it anyway.");
					}
					else
					{
						ShowNotification("enRAGE: the menu can't open while " + MenuBlockedText(text) + ".");
					}
					return;
				}
				if (!_menu.IsOpen)
				{
					try
					{
						OpenLastTab();
						MenuKeyNote("opened", routine: true);
						return;
					}
					catch (Exception ex)
					{
						GrudgeLog($"ERROR opening the menu: {ex}");
						MenuKeyNote("error - " + ex.Message);
						ShowNotification("enRAGE: the menu hit an error and didn't open. It's in grudges.log (Documents, Rockstar Games, Red Dead Redemption 2, enRAGE).");
						return;
					}
				}
				_menu.Toggle();
				MenuKeyNote("closed", routine: true);
			}
		}
	}

	private void DrawMenu()
	{
		try
		{
			_menu.Draw();
			_menuDrawFails = 0;
		}
		catch
		{
			if (++_menuDrawFails >= 3 && _menu.IsOpen)
			{
				_menuDrawFails = 0;
				_menu.Toggle();
				MenuKeyNote("closed - it failed to draw 3 frames running");
				ShowNotification("enRAGE: the menu hit an error and closed. It's in grudges.log (Documents, Rockstar Games, Red Dead Redemption 2, enRAGE).");
			}
			throw;
		}
	}

	private void WatchSettingsFile()
	{
		if (!_settingsEdited && !_iniPanel.IsOpen && Config.EditedOutside())
		{
			Config.ReadEdited();
		}
		if (_settingsEdited)
		{
			_settingsEdited = false;
			GrudgeLog("SETTINGS NemesisMod.ini was changed outside the game - read in (menu key " + KeyCombo.Name(Config.RosterMenuKey) + ")");
			if (Config.KeysNote != null)
			{
				GrudgeLog("KEYS " + Config.KeysNote);
			}
			ShowNotification("enRAGE: NemesisMod.ini changed, your settings are read in. Menu key: " + KeyCombo.MenuText + "." + ((Config.KeysNote != null) ? (" " + Config.KeysNote) : ""));
		}
	}

	private static string MenuBlockedText(string why)
	{
		if (!why.StartsWith("pause menu"))
		{
			if (!why.Contains("dead"))
			{
				if (!why.Contains("arrest"))
				{
					if (!why.Contains("fade"))
					{
						if (!why.Contains("control"))
						{
							if (!why.StartsWith("the catalog is open"))
							{
								if (!(why == "a panel is open"))
								{
									return why;
								}
								return "a conversation or the telegraph is open";
							}
							return "a game screen is open" + why.Substring("the catalog is open".Length);
						}
						return "the game has your controls (a shop, a cutscene, a scripted moment or another mod)";
					}
					return "the screen is fading";
				}
				return "you're being arrested";
			}
			return "you're dead";
		}
		return "the pause menu is up";
	}

	private void MenuKeyNote(string what, bool routine = false)
	{
		if (!routine || _menuKeyNotes < 12)
		{
			if (routine)
			{
				_menuKeyNotes++;
			}
			GrudgeLog("MENU key " + KeyCombo.MenuText + ": " + what);
		}
	}
}
