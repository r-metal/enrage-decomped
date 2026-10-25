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
	private sealed class SpawnItem
	{
		public string Label;

		public string Note;

		public Action Go;

		public bool Header;

		public bool Stay;
	}

	private sealed class StagedJob
	{
		public Func<bool> Still;

		public Action Run;

		public Action Done;
	}

	private int _spawnSel;

	private string _spawnForce;

	private bool _spawnHunters;

	private bool _spawnSkirmish;

	private bool _spawnRidge;

	private int _spawnTop;

	private readonly Queue<StagedJob> _staged = new Queue<StagedJob>();

	private List<SpawnItem> SpawnItems()
	{
		List<SpawnItem> l = new List<SpawnItem>();
		H("A NEMESIS COMES FOR YOU");
		I("Any encounter", "whatever fits here - in town, one more man in town", delegate
		{
			_spawnForce = null;
			_forceAmbushRequested = true;
		});
		I("Waiting up ahead", "he and his crew, doing something by the road", delegate
		{
			Nemesis("idle");
		});
		I("Riders on your tail", "they come up from behind, mounted", delegate
		{
			Nemesis("riders");
		});
		I("On the road ahead", "they wait on the road you're riding", delegate
		{
			Nemesis("ahead");
		});
		I("A wagon on the road", "his crew on a wagon coming your way", delegate
		{
			Nemesis("wagon");
		});
		I("A sniper", "high ground, out of your view", delegate
		{
			Nemesis("sniper");
		});
		I("A call-out", "he walks up and calls you out (best in town)", delegate
		{
			Nemesis("callout");
		});
		I("Bounty hunters", "hired by a man who won't come himself", delegate
		{
			_spawnHunters = true;
		});
		I("A rival joins the fight", "during an encounter: a man who hates HIM turns up", delegate
		{
			Spawner("rival", DebugRival);
		});
		H("IN TOWN");
		I("A nemesis in town", "one more man waiting about town (not hunting you)", delegate
		{
			Spawner("town man", delegate(Ped p)
			{
				if (!SpawnTownHunter(p))
				{
					ShowNotification("enRAGE: you're not in a town.");
				}
			});
		});
		I("Someone you know", "a person you've met here turns up", delegate
		{
			Spawner("regular", DebugRegular);
		});
		I("A Klansman out of his robe", "in plain clothes - greet him three times", delegate
		{
			Spawner("klan", DebugKlanInTown);
		});
		I("He waits on the road out", "a murderous man waits outside this town", delegate
		{
			Spawner("road ambush", DebugRoadAmbush);
		});
		I("A laid-up man", "in a town: a paralyzed nemesis at the doctor's (Valentine, Saint Denis) or a hotel bed you've marked", delegate
		{
			Spawner("sighting", DebugSighting);
		});
		I("Mark a hotel bed here", "stand by a hotel bed: laid-up men can be found there", delegate
		{
			Spawner("hotel bed", DebugMarkHotel);
		});
		I("A war checkpoint", "men watching the street (any town, war or not)", delegate
		{
			Spawner("checkpoint", DebugCheckpoint);
		});
		I("A saloon fight", "in the next bar you walk up to or into", delegate
		{
			_forceSaloon = true;
			ShowNotification("enRAGE: a saloon fight in the next bar you walk up to or into.");
		});
		H("THE WORLD");
		I("A skirmish", "two outfits fighting ahead of you", delegate
		{
			_spawnSkirmish = true;
		});
		I("A road patrol", "riders of the outfit that patrols here", delegate
		{
			_forcePatrol = 1;
		});
		I("Riders heard your shots", "a patrol rides in to where you stand", delegate
		{
			Spawner("responding", DebugResponding);
		});
		I("A raid on the nearest camp", "a live camp within 170 m gets hit", delegate
		{
			_forcePatrol = 2;
		});
		I("Riders on a ridge", "the Wapiti watching you from high ground", delegate
		{
			_spawnRidge = true;
		});
		H("CAMPS");
		I("Place a camp site here", "a real camp site of the strongest outfit here, saved in camps.json - then spawn it, and revert it if it's bad", delegate
		{
			Spawner("place camp", DebugPlaceCamp);
		});
		I("Spawn the placed camp", "the camp at the site you placed last, now (an officer of theirs and his men, with its props)", delegate
		{
			Spawner("spawn placed camp", DebugSpawnPlaced);
		});
		I("Revert the placed camp", "the camp gone and the site out of camps.json - again for the one placed before it", delegate
		{
			Spawner("revert placed camp", DebugRevertPlaced);
		});
		I("A camp here now", "a real camp of the strongest outfit here, 14 m ahead, with its props - it goes when you ride off", delegate
		{
			Spawner("camp here", DebugCampHere);
		});
		I("Camp kit here (props only)", "the camp props in the look of the outfit here, 8 m ahead - again to take it away (log: which models loaded)", delegate
		{
			Spawner("camp kit", DebugCampKitHere);
		});
		I("Despawn camp", "test camps gone (the ones below, a camp here now, the kit) - else the nearest camp, held off 3 minutes", delegate
		{
			Spawner("despawn camp", DebugDespawnCamp);
		});
		H("CAMP LOOKS");
		I("Props only: " + (_lookPropsOnly ? "ON" : "OFF"), _lookPropsOnly ? "the rows below lay just the camp - props, wagon, horses, no men (Enter: with men)" : "the rows below bring the outfit's men too (Enter: props only)", delegate
		{
			_lookPropsOnly = !_lookPropsOnly;
			SpawnerSays("camp looks: " + (_lookPropsOnly ? "props only" : "with their men") + ".");
		});
		l[l.Count - 1].Stay = true;
		foreach (Gang item in Gangs.Defined.Where((Gang x) => x.HasCamps && !x.OrdersNotFeelings))
		{
			Gang gg = item;
			I(item.Name + " camp", "at the nearest camp site (250 m; else 20 m ahead of you) in their look" + (Gangs.InEra(item) ? "" : " - not of this era: props only"), delegate
			{
				Spawner("camp look", delegate(Ped p)
				{
					DebugCampLookAtNearest(p, gg);
				});
			});
		}
		H("ROUTINES");
		I("An officer's habit, now", "the habit nearest you is on now (3 h) - marked", delegate
		{
			Spawner("habit", DebugHabit);
		});
		I("A funeral ahead", "the last outfit man to die is buried at the nearest cemetery (1.5 km)", delegate
		{
			Spawner("funeral", DebugFuneral);
		});
		I("Voice test (nearest person)", "the nearest person tries the game's speech lines - the ones said are logged and used", delegate
		{
			Spawner("voice test", DebugVoiceProbe);
		});
		I("Voice check: every outfit model", "each outfit model made out of sight for a second: which have a voice (MODELVOICE lines)", delegate
		{
			Spawner("model voices", DebugModelVoices);
		});
		I("Mark a funeral plot here", "stand among graves: funerals are held here (anywhere - a new graveyard if none is near)", delegate
		{
			Spawner("funeral plot", DebugMarkPlot);
		});
		I("Read the newspaper", "this region's paper, as it would print today", delegate
		{
			Spawner("paper", DebugPaper);
		});
		H("EPILOGUE");
		I("An old 0.9.4 epilogue world (test)", "John's save: the 1899 outfits on the 1907 clock, like 0.9.4 made - the conversion held so you can play in it first", delegate
		{
			Spawner("old epilogue world", DebugOldEpilogueWorld);
		});
		I("Convert it now (test)", "the conversion a player updating in the epilogue gets: the outfits change, everything you did stays", delegate
		{
			Spawner("convert old world", DebugConvertOld);
		});
		I("Jump to 1907 (test)", "eight years later on this world - a save point first; Shift + the menu key within 2 minutes puts 1899 back", delegate
		{
			Spawner("eight years", DebugEightYears);
		});
		H("WAR");
		I("Start a war here", "the two biggest outfits in this district go to war", delegate
		{
			DebugStartWar(Game.Player.Character);
		});
		I("A warband", "one side rides past, the other waits for it", delegate
		{
			_forceWar = 1;
		});
		I("An ambush on the road", "men lying in wait, riders coming (40% a wagon)", delegate
		{
			_forceWar = 2;
		});
		I("An ambush on a wagon", "the riders come on a wagon - dynamite", delegate
		{
			_forceWarWagon = true;
			_forceWar = 2;
		});
		I("Just after a battle", "the dead and the ones still standing", delegate
		{
			_forceWar = 3;
		});
		I("Bodies on the road ahead", "a fight between the two just ended (war here)", delegate
		{
			Spawner("road bodies", DebugRoadBodies);
		});
		I("A war patrol", "riders of one side (war here)", delegate
		{
			Spawner("war patrol", DebugWarPatrol);
		});
		I("A roadblock on the road", "in a war district, out of town: a side holds the road ahead", delegate
		{
			Spawner("road roadblock", DebugRoadBlock);
		});
		I("A distant battle", "a fight 200-250 m off you can hear (a war near you)", delegate
		{
			DebugDistantBattle(Game.Player.Character);
		});
		I("A distant explosion", "one blast 200-235 m off, out of sight", delegate
		{
			Spawner("explosion", DebugDistantBoom);
		});
		I("War heat +20", "the war here (or the nearest) burns hotter", delegate
		{
			Spawner("heat up", delegate(Ped p)
			{
				DebugWarHeat(p, 20);
			});
		});
		I("War heat -20", "the war here (or the nearest) cools", delegate
		{
			Spawner("heat down", delegate(Ped p)
			{
				DebugWarHeat(p, -20);
			});
		});
		H("SUPPLIES");
		I("Supplies -30", "the outfit holding most ground here", delegate
		{
			Spawner("supplies down", delegate(Ped p)
			{
				DebugSupplies(p, -30);
			});
		});
		I("Supplies +30", "the outfit holding most ground here", delegate
		{
			Spawner("supplies up", delegate(Ped p)
			{
				DebugSupplies(p, 30);
			});
		});
		I("Supply camp here", "the nearest manned camp keeps its outfit's supplies", delegate
		{
			Spawner("supply camp here", DebugSupplyCampHere);
		});
		I("Supply camp restocked", "the nearest supply camp, full again", delegate
		{
			Spawner("supply restock", DebugSupplyRestock);
		});
		I("A supply wagon", "the outfit holding the ground here runs supplies past you", delegate
		{
			Spawner("supply wagon", DebugSupplyWagon);
		});
		I("Supply crates in this town", "a stash of the district holder's at a placed spot (up to 2 a town, 150 m apart)", delegate
		{
			Spawner("town crates", DebugTownCrates);
		});
		I("Place a town stash spot here", "a stash can pile up where you stand, facing the way you face (content\\stash_spots.json)", delegate
		{
			Spawner("stash spot", DebugPlaceStashSpot);
		});
		I("Remove the nearest stash spot", "the placed stash spot within 15 m", delegate
		{
			Spawner("stash spot remove", DebugRemoveStashSpot);
		});
		H("TREASURY");
		I("Treasury camp here", "the nearest manned camp becomes its outfit's treasury", delegate
		{
			Spawner("treasury here", DebugTreasuryHere);
		});
		I("Reveal all treasuries", "every outfit's treasury on the map", delegate
		{
			Spawner("treasury reveal", DebugRevealTreasuries);
		});
		I("Refill the nearest treasury", "+$300 and the robbery forgotten", delegate
		{
			Spawner("treasury refill", DebugRefillTreasury);
		});
		H("MAP ICONS");
		T("Show every kind", "all ten below on the map at once (again: hide them all)", delegate
		{
			Spawner("icons all", delegate
			{
				DebugIconsAll();
			});
		});
		T("Camp sites", "the game's, learned (green), placed by you (blue), hideouts - who holds each; retired ones (red)", delegate
		{
			Spawner("icons camps", delegate
			{
				DebugIcons(IconKind.Camps);
			});
		});
		T("Supplies", "each outfit's supply camp (yellow) and the town stash spots", delegate
		{
			Spawner("icons supplies", delegate
			{
				DebugIcons(IconKind.Supplies);
			});
		});
		T("Funerals", "every cemetery and the grave plots marked", delegate
		{
			Spawner("icons funerals", delegate
			{
				DebugIcons(IconKind.Funerals);
			});
		});
		T("Treasuries", "each outfit's treasury camp (shown only - not found)", delegate
		{
			Spawner("icons treasuries", delegate
			{
				DebugIcons(IconKind.Treasuries);
			});
		});
		T("Laid-up spots", "where a paralyzed man can be found: each town's doctor, the hotel beds marked", delegate
		{
			Spawner("icons laid up", delegate
			{
				DebugIcons(IconKind.LaidUp);
			});
		});
		T("Bars", "each town's saloon, and the bars a saloon fight can start in (yellow)", delegate
		{
			Spawner("icons bars", delegate
			{
				DebugIcons(IconKind.Bars);
			});
		});
		T("Officers' habits", "where each officer keeps his habit, and when", delegate
		{
			Spawner("icons habits", delegate
			{
				DebugIcons(IconKind.Habits);
			});
		});
		T("Everything coming", "every plan - jobs, rides, funerals, habits, lies - known or not, and when", delegate
		{
			Spawner("icons plans", delegate
			{
				DebugIcons(IconKind.Plans);
			});
		});
		T("Named men out now", "the mod's named men in the world now (moved every 3 s)", delegate
		{
			Spawner("icons people", delegate
			{
				DebugIcons(IconKind.People);
			});
		});
		T("Calm zones", "the Wapiti Reservation: no fights within 300 m", delegate
		{
			Spawner("icons calm", delegate
			{
				DebugIcons(IconKind.Calm);
			});
		});
		H("CHECKS");
		I("Law test: four Raiders", "what makes a kill a crime: 4 unarmed Raiders 8 m ahead, each in a different group - fair game off 5 min, LAWTEST lines (do it in town)", delegate
		{
			Spawner("law test", DebugLawTest);
		});
		I("Self-check now", "the mod checks its own rules against the world now (SELFCHECK lines in grudges.log; a fault is looked at again in 30 s)", delegate
		{
			Spawner("self-check", DebugSelfCheck);
		});
		return l;
		void H(string t)
		{
			l.Add(new SpawnItem
			{
				Label = t,
				Header = true
			});
		}
		void I(string label, string note, Action go)
		{
			l.Add(new SpawnItem
			{
				Label = label,
				Note = note,
				Go = go
			});
		}
		void Nemesis(string stage)
		{
			_spawnForce = stage;
			_forceAmbushRequested = true;
		}
		void T(string label, string note, Action go)
		{
			l.Add(new SpawnItem
			{
				Label = label,
				Note = note,
				Go = go,
				Stay = true
			});
		}
	}

	private void ToggleSpawnMenu()
	{
		if (!_spawnMenuOpen && (_menu.IsOpen || _bulletImpact.ViewerOpen || _convo != null || _tg != null || _iniPanel.IsOpen || _welcome.IsOpen))
		{
			ShowNotification("enRAGE: close the menu first to open the encounter spawner.");
			return;
		}
		_spawnMenuOpen = !_spawnMenuOpen;
		UiSound(_spawnMenuOpen ? "MENU_ENTER" : "MENU_CLOSE");
		if (_spawnMenuOpen)
		{
			List<SpawnItem> list = SpawnItems();
			if (_spawnSel <= 0 || _spawnSel >= list.Count || list[_spawnSel].Header)
			{
				_spawnSel = 1;
			}
		}
	}

	private void SpawnMenuKey(Keys key)
	{
		List<SpawnItem> items = SpawnItems();
		switch (key)
		{
		case Keys.Up:
		case Keys.W:
			_spawnSel = Step(_spawnSel, -1);
			UiSound("NAV_UP");
			break;
		case Keys.Down:
		case Keys.S:
			_spawnSel = Step(_spawnSel, 1);
			UiSound("NAV_DOWN");
			break;
		case Keys.Return:
		case Keys.Space:
		{
			SpawnItem spawnItem = items[Math.Max(0, Math.Min(items.Count - 1, _spawnSel))];
			if (!spawnItem.Header)
			{
				UiSound("SELECT");
				if (!spawnItem.Stay)
				{
					_spawnMenuOpen = false;
				}
				GrudgeLog("SPAWNER " + spawnItem.Label);
				spawnItem.Go();
			}
			break;
		}
		case Keys.Back:
		case Keys.Escape:
			UiSound("BACK");
			_spawnMenuOpen = false;
			break;
		}
		int Step(int from, int dir)
		{
			int num = from;
			do
			{
				num = (num + dir + items.Count) % items.Count;
			}
			while (items[num].Header);
			return num;
		}
	}

	private void RunSpawnMenuRequests(Ped player)
	{
		if (_spawnSkirmish)
		{
			_spawnSkirmish = false;
			if (_skirmish.Count > 0)
			{
				ShowNotification("A skirmish is already on.");
			}
			else if (!TrySkirmish(player, town: false, test: true))
			{
				ShowNotification("No skirmish could start here (no two outfits to fight, or no room).");
			}
		}
		if (_spawnRidge)
		{
			_spawnRidge = false;
			if (!SpawnRidgeWatch(player, Gangs.Wapiti))
			{
				ShowNotification("No high ground for riders near you.");
			}
		}
		if (_spawnHunters)
		{
			_spawnHunters = false;
			NemesisRecord nemesisRecord = (from _ in Roster
				where _.IsAlive && !_.PresumedDead && _.CanHunt && HiresHunters(_)
				orderby _rng.Next()
				select _).FirstOrDefault() ?? (from _ in Roster
				where _.IsAlive && !_.PresumedDead && _.CanHunt && _.Gang == null
				orderby _rng.Next()
				select _).FirstOrDefault();
			if (nemesisRecord == null)
			{
				ShowNotification("Nobody on your roster could send bounty hunters.");
			}
			else
			{
				SendBountyHunters(nemesisRecord, player);
			}
		}
	}

	private void DrawSpawnMenu()
	{
		if (!_spawnMenuOpen)
		{
			return;
		}
		if (!_debugOn)
		{
			_spawnMenuOpen = false;
			return;
		}
		Game.DisableAllControlsThisFrame();
		List<SpawnItem> list = SpawnItems();
		float num = 0.59000003f;
		int val = Math.Max(8, 36);
		int num2 = Math.Min(list.Count, val);
		if (_spawnSel < _spawnTop + 1)
		{
			_spawnTop = Math.Max(0, _spawnSel - 1);
		}
		if (_spawnSel >= _spawnTop + num2 - 1)
		{
			_spawnTop = Math.Min(list.Count - num2, _spawnSel - num2 + 2);
		}
		_spawnTop = Math.Max(0, Math.Min(_spawnTop, list.Count - num2));
		float num3 = 0.075f + (float)num2 * 0.0225f + 0.06f;
		float num4 = Math.Max(0.02f, 0.5f - num3 / 2f);
		Theme.PanelRect(num, num4, 0.38f, num3, 240);
		string text = "ENCOUNTER SPAWNER";
		UiDraw.TextFont(text, UiDraw.TitleFont, num + 0.19f - Theme.CapsWidth(text, 0.34f) / 2f, num4 + 0.008f, 0.34f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		Theme.CenterRule(num, num4 + 0.052f, 0.38f, 0.04f, 150);
		float num5 = num4 + 0.064f;
		if (list.Count > num2)
		{
			Theme.ScrollBar(num + 0.38f - 0.006f, num5, (float)num2 * 0.0225f, _spawnTop, num2, list.Count);
		}
		for (int i = _spawnTop; i < _spawnTop + num2; i++)
		{
			SpawnItem spawnItem = list[i];
			if (spawnItem.Header)
			{
				UiDraw.TextFont(spawnItem.Label, UiDraw.TitleFont, num + 0.014f, num5 + 0.003f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				num5 += 0.0225f;
				continue;
			}
			int idx = i;
			UiMouse.Region(num + 0.008f, num5, 0.364f, 0.0225f, delegate
			{
				if (_spawnSel == idx)
				{
					return false;
				}
				_spawnSel = idx;
				return true;
			}, delegate
			{
				UiMouse.Send(Keys.Return);
			});
			bool num6 = i == _spawnSel;
			if (num6)
			{
				Theme.SelectBox(num + 0.008f, num5, 0.364f, 0.0225f);
			}
			(int, int, int) tuple = (num6 ? Theme.SelectedText : Theme.Text);
			UiDraw.TextFont(spawnItem.Label, UiDraw.TitleFont, num + 0.022f, num5 + 0.002f, 0.205f, tuple.Item1, tuple.Item2, tuple.Item3);
			if (num6 && spawnItem.Note != null)
			{
				string note = spawnItem.Note;
				UiDraw.Text(note, num + 0.38f - 0.016f - Theme.MixedWidth(note, 0.16f, 0.0222f), num5 + 0.005f, 0.16f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			}
			num5 += 0.0225f;
		}
		List<(string, string)> hints = (XPad.RecentlyUsed ? new List<(string, string)>
		{
			("D-pad", "Navigate"),
			("A", "Spawn"),
			("B", "Close")
		} : new List<(string, string)>
		{
			("Up/Down", "Navigate"),
			("Enter", "Spawn"),
			("Backspace", "Close")
		});
		Theme.HintRow(num + 0.012f, num + 0.38f - 0.012f, num4 + num3 - 0.04f, hints, 0.22f, Theme.Text);
	}

	private void Spawner(string what, Action<Ped> go)
	{
		Guard("spawner " + what, delegate
		{
			go(Game.Player.Character);
		});
	}

	private void SpawnerSays(string text)
	{
		ShowNotification("enRAGE: " + text);
		GrudgeLog("SPAWNER says: " + text);
	}

	private string SpawnerTown(Ped player)
	{
		string obj = _currentTown ?? TownAt(player.Position);
		if (obj == null)
		{
			SpawnerSays("go into a town first.");
		}
		return obj;
	}

	private void DebugRival(Ped player)
	{
		if (_enc == null || ActiveRecord == null || _activeNemesisPed == null || !_activeNemesisPed.Exists())
		{
			SpawnerSays("start a nemesis encounter first - the rival comes for HIM.");
			return;
		}
		if (_rival != null)
		{
			SpawnerSays("a rival is already here.");
			return;
		}
		NemesisRecord target = ActiveRecord;
		NemesisRecord nemesisRecord = (from t in (target.Ties ?? new List<Tie>()).Where(IsRivalTie)
			select Roster.FirstOrDefault((NemesisRecord n) => n.Id == t.Id)).FirstOrDefault(Free) ?? (from _ in Roster
			where Free(_) && _.Gang != null && _.Gang == target.Gang
			orderby _rng.Next()
			select _).FirstOrDefault() ?? (from _ in Roster.Where(Free)
			orderby _rng.Next()
			select _).FirstOrDefault();
		if (nemesisRecord == null)
		{
			SpawnerSays("nobody free on your roster to send as his rival.");
			return;
		}
		_rivalRolled = true;
		SpawnRival(nemesisRecord, target, player);
		SpawnerSays((_rival != null) ? (nemesisRecord.Name + " turns up - he hates " + target.Name + ".") : "no hidden spot for the rival (see grudges.log).");
		bool Free(NemesisRecord n)
		{
			if (n != null && n != target && n.IsAlive && !n.PresumedDead && n.CanHunt && OffStage(n))
			{
				return !Gangs.Squads(n);
			}
			return false;
		}
	}

	private void DebugRegular(Ped player)
	{
		string text = SpawnerTown(player);
		if (text != null)
		{
			SpawnTestRegular(player, text, "asked from the spawner");
		}
	}

	private void DebugKlanInTown(Ped player)
	{
		string text = SpawnerTown(player);
		if (text != null)
		{
			NemesisRecord nemesisRecord = (from _ in Roster
				where NoTowns(_) && Gangs.ById(_.Gang)?.TownModels != null && _.IsAlive && !_.PresumedDead && _.CanHunt && !_worldNemeses.ContainsValue(_) && _ != ActiveRecord
				orderby _rng.Next()
				select _).FirstOrDefault();
			if (nemesisRecord == null)
			{
				SpawnerSays("no free Klansman on the roster.");
				return;
			}
			bool flag = SpawnTownPresence(nemesisRecord, text, player);
			SpawnerSays(flag ? (nemesisRecord.Name + " of the " + Gangs.ById(nemesisRecord.Gang)?.Name + " is about " + text + " in plain clothes (no dot).") : "no spot for him in town (see grudges.log).");
		}
	}

	private void DebugRoadAmbush(Ped player)
	{
		string text = SpawnerTown(player);
		if (text == null)
		{
			return;
		}
		if (_roadAmbush != null)
		{
			SpawnerSays(_roadAmbush.Rec.Name + " is already waiting outside " + _roadAmbush.Town + ".");
			return;
		}
		NemesisRecord nemesisRecord = (from _ in Roster
			where _.IsAlive && !_.PresumedDead && _.CanHunt && _.HateTier == HateTier.Murderous && !IsArmy(_) && HasReason(_) && !_worldNemeses.ContainsValue(_) && _ != ActiveRecord
			orderby _rng.Next()
			select _).FirstOrDefault();
		if (nemesisRecord == null)
		{
			SpawnerSays("no free murderous nemesis to wait for you.");
			return;
		}
		_roadAmbush = new RoadAmbush
		{
			Rec = nemesisRecord,
			Town = text,
			Until = Game.GameTime + Balance.RoadAmbushMinutes * 60000
		};
		GrudgeLog("TOWNCALM " + nemesisRecord.Name + " waits outside " + text + " (spawner)");
		SpawnerSays(nemesisRecord.Name + " waits on the road out of " + text + " - ride out of town.");
	}

	private void DebugSighting(Ped player)
	{
		string text = SpawnerTown(player);
		if (text != null)
		{
			if (_sightingPed != null || _pendingRec != null)
			{
				SpawnerSays("a laid-up man is already about.");
			}
			else if (!Roster.Any((NemesisRecord n) => n.IsAlive && n.Paralyzed && !n.PresumedDead))
			{
				SpawnerSays("nobody on your roster is paralyzed (break a man's spine or neck first).");
			}
			else if (!SightingFor(text, force: true))
			{
				SpawnerSays("no open doctor's or marked hotel bed in " + text + " (the doctor's is open only in Valentine and Saint Denis).");
			}
		}
	}

	private void DebugCheckpoint(Ped player)
	{
		string text = SpawnerTown(player);
		if (text == null)
		{
			return;
		}
		if (_roadblocks.Any((Roadblock r) => r.Spawner))
		{
			SpawnerSays("the spawner's roadblock is already up - ride out of town and back.");
			return;
		}
		District d = DistrictAt(player.Position);
		(Gang, Gang)? tuple = ((d != null) ? WarIn(d) : (((Gang, Gang)?)null));
		Gang gang = null;
		if (tuple.HasValue)
		{
			gang = ((HoldOf(d, tuple.Value.Item1.Id) >= HoldOf(d, tuple.Value.Item2.Id)) ? tuple.Value.Item1 : tuple.Value.Item2);
			if (!FactionModels.ContainsKey(gang.Id))
			{
				gang = ((gang == tuple.Value.Item1) ? tuple.Value.Item2 : tuple.Value.Item1);
			}
		}
		if (gang == null || !FactionModels.ContainsKey(gang.Id))
		{
			gang = (from x in Gangs.All
				where !x.StoryOnly && !IsDestroyed(x) && FactionModels.ContainsKey(x.Id)
				orderby (d != null) ? HoldOf(d, x.Id) : 0 descending
				select x).FirstOrDefault();
		}
		if (gang == null)
		{
			SpawnerSays("no outfit with men to put on the street.");
			return;
		}
		string why = null;
		string why2;
		Vector3? vector = CheckpointSpot(player, text, mustHide: true, out why2) ?? CheckpointSpot(player, text, mustHide: false, out why);
		if (!vector.HasValue)
		{
			GrudgeLog("CHECKPOINT spawner: hidden - " + why2 + "; anywhere - " + why);
			SpawnerSays("no road 30-115 m off here - try facing down the street.");
			return;
		}
		float roadHeading = (float.IsNaN(_checkpointRoad) ? HeadingTo(vector.Value, player.Position) : _checkpointRoad);
		_checkpointRoad = float.NaN;
		Roadblock roadblock = BuildRoadblock(player, gang, vector.Value, roadHeading, text, -1, spawner: true);
		if (roadblock == null)
		{
			SpawnerSays("nobody could be put there (see grudges.log).");
			return;
		}
		GrudgeLog($"CHECKPOINT {text}: the spawner's roadblock - {roadblock.Men.Count} {gang.Name} men {vector.Value.DistanceTo(player.Position):0} m ahead at {vector.Value.X:0},{vector.Value.Y:0} ({CompassTo(player.Position, vector.Value)} of you)");
		SpawnerSays($"the {gang.Name} hold the road {vector.Value.DistanceTo(player.Position):0} m away ({CompassTo(player.Position, vector.Value)}).");
	}

	private void DebugResponding(Ped player)
	{
		(Gang, Gang)? warHere = WarHere;
		Gang gang = (warHere.HasValue ? (from _ in new Gang[2]
			{
				warHere.Value.Item1,
				warHere.Value.Item2
			}
			where FactionModels.ContainsKey(_.Id)
			orderby _rng.Next()
			select _).FirstOrDefault() : null);
		gang = gang ?? PatrolFaction;
		if (gang == null)
		{
			District d = CurrentDistrict;
			gang = (from x in Gangs.All
				where !x.StoryOnly && !IsDestroyed(x) && FactionModels.ContainsKey(x.Id)
				orderby (d != null) ? HoldOf(d, x.Id) : 0 descending
				select x).FirstOrDefault() ?? Gangs.Army;
		}
		int count = _raids.Count;
		SpawnRespondingPatrol(player, player.Position, "your shots (the spawner)", gang);
		SpawnerSays((_raids.Count > count) ? ("the " + gang.Name + " heard you - riders coming.") : "no loaded ground out of your sight for them (see grudges.log).");
	}

	private void DebugRoadBodies(Ped player)
	{
		District d = CurrentDistrict;
		WarRecord warRecord = ((d == null) ? null : Wars.FirstOrDefault((WarRecord x) => x.Result == null && x.District == d.Key));
		if (warRecord == null)
		{
			SpawnerSays("no war in this district - use 'Start a war here' first.");
			return;
		}
		InitHeat(warRecord);
		SpawnerSays(RoadBodiesNow(player, warRecord, d) ? "a fight just ended up ahead - look the way you're facing." : "no hidden spot ahead / no men to put there (see grudges.log).");
	}

	private void DebugDistantBoom(Ped player)
	{
		Vector3 vector = DistantSpot(player);
		if (vector == Vector3.Zero)
		{
			SpawnerSays("no spot 200-235 m off out of your sight - try again elsewhere.");
			return;
		}
		if (!ModExplosion(vector, 0f, null, "spawner explosion"))
		{
			SpawnerSays("someone's standing where it would go - try again.");
			return;
		}
		GrudgeLog($"SKIRMISH boom (spawner) at {vector.X:0},{vector.Y:0} ({vector.DistanceTo(player.Position):0} m from you, tag {DistantExplosionTag})");
		SpawnerSays("a blast " + Away(player.Position, vector) + " - listen.");
	}

	private void DebugWarPatrol(Ped player)
	{
		(Gang, Gang)? warHere = WarHere;
		if (!warHere.HasValue)
		{
			SpawnerSays("no war here - use 'Start a war here' first.");
			return;
		}
		Gang gang = (from _ in new Gang[2]
			{
				warHere.Value.Item1,
				warHere.Value.Item2
			}
			where _ != null && FactionModels.ContainsKey(_.Id)
			orderby _rng.Next()
			select _).FirstOrDefault();
		if (gang == null)
		{
			SpawnerSays("neither side has men to send.");
		}
		else
		{
			SpawnerSays(SpawnRoadPatrol(player, force: true, gang) ? ("a " + gang.Name + " patrol rides the road.") : "no room for a patrol here.");
		}
	}

	private void Stage(Action run, Func<bool> still = null, Action done = null)
	{
		_staged.Enqueue(new StagedJob
		{
			Run = run,
			Still = still,
			Done = done
		});
	}

	private void RunStaged()
	{
		if (_staged.Count == 0)
		{
			return;
		}
		StagedJob stagedJob = _staged.Dequeue();
		try
		{
			if (stagedJob.Still == null || stagedJob.Still())
			{
				stagedJob.Run();
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("STAGED a job failed: " + ex.Message);
		}
		finally
		{
			try
			{
				stagedJob.Done?.Invoke();
			}
			catch
			{
			}
		}
	}

	private void ClearStaged()
	{
		while (_staged.Count > 0)
		{
			StagedJob stagedJob = _staged.Dequeue();
			try
			{
				stagedJob.Done?.Invoke();
			}
			catch
			{
			}
		}
	}
}
