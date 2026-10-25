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
	private const int MenuHoldMs = 800;

	private const int DoubleTapMs = 350;

	private int _lastRightTap = -100000;

	private int _padHoldSince = -1;

	private int _padHoldSeen = -100000;

	private bool _padHoldFired;

	private int _viewDownAt;

	private const int StickRepeatMs = 180;

	private int _dpadUpSince = -1;

	private int _nextStickMove;

	private readonly HashSet<Keys> _padHeld = new HashSet<Keys>();

	private bool _ltWas;

	private bool _rtWas;

	private static readonly string[] CatalogApps = new string[14]
	{
		"shop_browsing", "shop_menu", "catalog", "catalog_menu", "shop_catalog", "store_catalog", "wardrobe", "crafting", "crafting_menu", "cooking",
		"cooking_menu", "camp_crafting", "satchel", "inventory"
	};

	private void PollGamepad()
	{
		if (XPad.Log == null)
		{
			XPad.Log = GrudgeLog;
		}
		XPad.Poll();
		if (!XPad.Connected)
		{
			ReleasePadHeld();
			return;
		}
		if (_search != null)
		{
			_viewDownAt = 0;
			Keys? keys = null;
			if (XPad.Pressed(1))
			{
				keys = Keys.Up;
			}
			else if (XPad.Pressed(2))
			{
				keys = Keys.Down;
			}
			else if (XPad.Pressed(4))
			{
				keys = Keys.Left;
			}
			else if (XPad.Pressed(8))
			{
				keys = Keys.Right;
			}
			else if (XPad.Pressed(4096))
			{
				keys = Keys.Return;
			}
			else if (XPad.Pressed(8192))
			{
				keys = Keys.Escape;
			}
			else if (XPad.Pressed(16384))
			{
				keys = Keys.Back;
			}
			else if (XPad.Pressed(32768))
			{
				keys = Keys.Space;
			}
			else if (XPad.Pressed(512))
			{
				keys = Keys.Tab;
			}
			else if (XPad.Pressed(256))
			{
				keys = Keys.Q;
			}
			if (Math.Max(Math.Abs(XPad.LX), Math.Abs(XPad.LY)) > 0.6f)
			{
				if (!keys.HasValue && UiDraw.RealMs >= _nextStickMove)
				{
					_nextStickMove = UiDraw.RealMs + 180;
					keys = ((!(Math.Abs(XPad.LX) > Math.Abs(XPad.LY))) ? ((XPad.LY > 0f) ? Keys.Up : Keys.Down) : ((XPad.LX > 0f) ? Keys.Right : Keys.Left));
				}
			}
			else
			{
				_nextStickMove = 0;
			}
			if (keys.HasValue)
			{
				PadSearchKey(keys.Value);
			}
			return;
		}
		bool isOpen = _menu.IsOpen;
		bool viewerOpen = _bulletImpact.ViewerOpen;
		if (_iniPanel.IsOpen)
		{
			bool shift = XPad.Held(16384);
			if (XPad.Pressed(1))
			{
				_iniPanel.HandleKey(Keys.Up, shift);
			}
			if (XPad.Pressed(2))
			{
				_iniPanel.HandleKey(Keys.Down, shift);
			}
			if (XPad.Pressed(4))
			{
				_iniPanel.HandleKey(Keys.Left, shift);
			}
			if (XPad.Pressed(8))
			{
				_iniPanel.HandleKey(Keys.Right, shift);
			}
			if (XPad.Pressed(256))
			{
				_iniPanel.HandleKey(Keys.Q, shift);
			}
			if (XPad.Pressed(512))
			{
				_iniPanel.HandleKey(Keys.E, shift);
			}
			if (XPad.Pressed(8192))
			{
				_iniPanel.HandleKey(Keys.Back, shift);
			}
		}
		else if (_spawnMenuOpen)
		{
			if (XPad.Pressed(1))
			{
				SpawnMenuKey(Keys.Up);
			}
			if (XPad.Pressed(2))
			{
				SpawnMenuKey(Keys.Down);
			}
			if (XPad.Pressed(4096))
			{
				SpawnMenuKey(Keys.Return);
			}
			if (XPad.Pressed(8192))
			{
				SpawnMenuKey(Keys.Back);
			}
		}
		else if (_paperShown != null)
		{
			if (XPad.Pressed(4096) || XPad.Pressed(8192))
			{
				ClosePaper();
			}
		}
		else if (_welcome.InSetup)
		{
			if (XPad.Pressed(1))
			{
				_welcome.HandleKey(Keys.Up);
			}
			if (XPad.Pressed(2))
			{
				_welcome.HandleKey(Keys.Down);
			}
			if (XPad.Pressed(4))
			{
				_welcome.HandleKey(Keys.Left);
			}
			if (XPad.Pressed(8))
			{
				_welcome.HandleKey(Keys.Right);
			}
			if (XPad.Pressed(4096))
			{
				_welcome.HandleKey(Keys.Return);
			}
			if (XPad.Pressed(8192))
			{
				_welcome.HandleKey(Keys.Back);
			}
		}
		else if (_welcome.IsOpen)
		{
			if (XPad.Pressed(4) || XPad.Pressed(256))
			{
				_welcome.HandleKey(Keys.Left);
			}
			if (XPad.Pressed(8) || XPad.Pressed(512))
			{
				_welcome.HandleKey(Keys.Right);
			}
			if (XPad.Pressed(4096))
			{
				_welcome.HandleKey(Keys.Return);
			}
			if (XPad.Pressed(8192))
			{
				_welcome.HandleKey(Keys.Back);
			}
		}
		else if (_tg != null)
		{
			if (XPad.Pressed(1))
			{
				Dispatch(Keys.Up);
			}
			if (XPad.Pressed(2))
			{
				Dispatch(Keys.Down);
			}
			if (_tg.Page == "read" && XPad.Pressed(4))
			{
				Dispatch(Keys.Left);
			}
			if (_tg.Page == "read" && XPad.Pressed(8))
			{
				Dispatch(Keys.Right);
			}
			if (XPad.Pressed(4096))
			{
				Dispatch(Keys.Return);
			}
			if (XPad.Pressed(8192))
			{
				Dispatch(Keys.Back);
			}
		}
		else if (_convo != null)
		{
			_ = _convo;
			if (XPad.Pressed(1))
			{
				Dispatch(Keys.Up);
			}
			if (XPad.Pressed(2))
			{
				Dispatch(Keys.Down);
			}
			if (XPad.Pressed(4))
			{
				Dispatch(Keys.Left);
			}
			if (XPad.Pressed(8))
			{
				Dispatch(Keys.Right);
			}
			if (XPad.Pressed(4096))
			{
				Dispatch(Keys.Return);
			}
			if (XPad.Pressed(8192) && _convo != null)
			{
				Dispatch(Keys.Back);
			}
		}
		else if (!isOpen && !viewerOpen)
		{
			ReleasePadHeld();
			int padMenuOpen = Config.PadMenuOpen;
			if (padMenuOpen == 3)
			{
				if (Game.GameTime - _padHoldSeen > 250)
				{
					_padHoldSince = -1;
					_padHoldFired = XPad.Held(8);
				}
				_padHoldSeen = Game.GameTime;
				if (!XPad.Held(8))
				{
					_padHoldSince = -1;
					_padHoldFired = false;
				}
				else if (_padHoldSince < 0)
				{
					_padHoldSince = Game.GameTime;
				}
				else if (!_padHoldFired && Game.GameTime - _padHoldSince >= 800)
				{
					_padHoldFired = true;
					PadOpen();
				}
			}
			else if (padMenuOpen < 4 && XPad.Pressed(padMenuOpen switch
			{
				2 => 2, 
				1 => 4, 
				_ => 8, 
			}))
			{
				if (Game.GameTime - _lastRightTap <= 350)
				{
					_lastRightTap = -100000;
					PadOpen();
				}
				else
				{
					_lastRightTap = Game.GameTime;
				}
			}
		}
		else
		{
			if (XPad.Held(1) && _dpadUpSince == int.MaxValue)
			{
				return;
			}
			_dpadUpSince = -1;
			if (XPad.Pressed(1))
			{
				Dispatch(Keys.Up);
			}
			if (XPad.Pressed(2))
			{
				Dispatch(Keys.Down);
			}
			if (!viewerOpen && XPad.Pressed(4))
			{
				Dispatch(Keys.Left);
			}
			if (!viewerOpen && XPad.Pressed(8))
			{
				Dispatch(Keys.Right);
			}
			if (Math.Max(Math.Abs(XPad.LX), Math.Abs(XPad.LY)) > 0.6f && (!viewerOpen || !(Math.Abs(XPad.LX) > Math.Abs(XPad.LY))))
			{
				if (UiDraw.RealMs >= _nextStickMove)
				{
					_nextStickMove = UiDraw.RealMs + 180;
					Dispatch((!(Math.Abs(XPad.LX) > Math.Abs(XPad.LY))) ? ((XPad.LY > 0f) ? Keys.Up : Keys.Down) : ((XPad.LX > 0f) ? Keys.Right : Keys.Left));
				}
			}
			else
			{
				_nextStickMove = 0;
			}
			if (XPad.Pressed(4096))
			{
				Dispatch(Keys.Return);
			}
			if (XPad.Pressed(8192))
			{
				Dispatch(Keys.Back);
			}
			if (XPad.Pressed(32))
			{
				_viewDownAt = Environment.TickCount | 1;
			}
			if (_viewDownAt != 0)
			{
				if (!XPad.Held(32))
				{
					_viewDownAt = 0;
					Dispatch(Keys.Tab);
				}
				else if (Environment.TickCount - _viewDownAt > 600)
				{
					_viewDownAt = 0;
					OpenSearch(pad: true);
					return;
				}
			}
			if (XPad.Pressed(16) && isOpen && !viewerOpen)
			{
				OpenSetupScreen();
				return;
			}
			if (viewerOpen)
			{
				if (XPad.Pressed(16384))
				{
					Dispatch(Keys.Home);
				}
				if (XPad.Pressed(256))
				{
					Dispatch(Keys.Q);
				}
				if (XPad.Pressed(512))
				{
					Dispatch(Keys.E);
				}
				if (XPad.Pressed(32768))
				{
					Dispatch(Keys.Space);
				}
				if (XPad.Pressed(128))
				{
					Dispatch(Keys.R);
				}
				PadHold(XPad.RT > 0.4f, Keys.Z);
				PadHold(XPad.LT > 0.4f, Keys.X);
				if (XPad.RX != 0f || XPad.RY != 0f)
				{
					_bulletImpact.Orbit(XPad.RX * 140f * UiDraw.RealDt, XPad.RY * 90f * UiDraw.RealDt);
				}
				return;
			}
			ReleasePadHeld();
			if (XPad.Pressed(16384))
			{
				Dispatch(Keys.Home);
			}
			if (XPad.Pressed(32768))
			{
				Dispatch(Keys.R);
			}
			if (XPad.Pressed(256))
			{
				Dispatch(Keys.Q);
			}
			if (XPad.Pressed(512))
			{
				Dispatch(Keys.E);
			}
			if (XPad.Pressed(128))
			{
				Dispatch(Keys.C);
			}
			if (XPad.Pressed(64))
			{
				Dispatch(Keys.N);
			}
			TriggerTap(XPad.LT, ref _ltWas, Keys.OemMinus);
			TriggerTap(XPad.RT, ref _rtWas, Keys.Oemplus);
		}
		void PadOpen()
		{
			string text = PadMenuBlocked();
			if (text == null)
			{
				OpenLastTab();
			}
			else
			{
				GrudgeLog("MENU pad open blocked: " + text);
			}
		}
	}

	internal string PadMenuBlocked()
	{
		bool soft;
		return MenuBlocked(out soft);
	}

	internal string MenuBlocked(out bool soft)
	{
		soft = false;
		try
		{
			if (HUD.IS_PAUSE_MENU_ACTIVE())
			{
				return "pause menu";
			}
			Ped character = Game.Player.Character;
			if (character != null && character.Exists() && WorldBusy(character))
			{
				return "the game is busy (" + QuietWhy + ")";
			}
			if (!PLAYER.IS_PLAYER_CONTROL_ON(Game.Player.Handle))
			{
				return "no player control (catalog / cutscene)";
			}
			string[] catalogApps = CatalogApps;
			foreach (string text in catalogApps)
			{
				if (UIAPPS.IS_UIAPP_RUNNING(text))
				{
					return "the catalog is open (" + text + ")";
				}
			}
			if (UIAPPS.IS_ANY_UIAPP_RUNNING())
			{
				soft = true;
				return "a game screen is open (crafting / cooking / ...)";
			}
			if (INTERIOR.GET_INTERIOR_FROM_ENTITY(Game.Player.Character.Handle) != 0)
			{
				GrudgeLog($"MENU pad open indoors: any ui app active {UIAPPS.IS_ANY_UIAPP_ACTIVE()} running {UIAPPS.IS_ANY_UIAPP_RUNNING()}");
			}
		}
		catch
		{
		}
		return null;
	}

	private void TriggerTap(float value, ref bool was, Keys key)
	{
		bool flag = value > 0.5f;
		if (flag && !was)
		{
			Dispatch(key);
		}
		was = flag;
	}

	private void PadHold(bool down, Keys key)
	{
		if (down && _padHeld.Add(key))
		{
			Dispatch(key);
		}
		else if (!down && _padHeld.Remove(key))
		{
			_bulletImpact.HandleKeyUp(key);
		}
	}

	private void ReleasePadHeld()
	{
		foreach (Keys item in _padHeld)
		{
			_bulletImpact.HandleKeyUp(item);
		}
		_padHeld.Clear();
	}
}
