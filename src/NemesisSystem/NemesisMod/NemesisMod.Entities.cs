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
	internal int HandlesReused;

	internal int HandlesGone;

	private static readonly Dictionary<string, (string to, int w)[]> RetiredHeirs = new Dictionary<string, (string, int)[]>
	{
		["odriscoll"] = new(string, int)[3]
		{
			("laramie", 70),
			("lemoyneraiders", 20),
			("delobo", 10)
		},
		["bronte"] = new(string, int)[2]
		{
			("lemoyneraiders", 70),
			("laramie", 30)
		}
	};

	private static readonly Dictionary<string, string[]> FatePlaces = new Dictionary<string, string[]>
	{
		["odriscoll"] = new string[5] { "Valentine", "Strawberry", "the Heartlands", "Big Valley", "Annesburg" },
		["lemoyneraiders"] = new string[4] { "Rhodes", "Scarlett Meadows", "Saint Denis", "Bluewater Marsh" },
		["klan"] = new string[3] { "Rhodes", "Scarlett Meadows", "Bluewater Marsh" },
		["bronte"] = new string[2] { "Saint Denis", "the Saint Denis docks" },
		["army"] = new string[3] { "Fort Wallace", "Cumberland Forest", "the Grizzlies" },
		["murfree"] = new string[3] { "Roanoke Ridge", "Annesburg", "Van Horn" },
		["skinners"] = new string[3] { "Tall Trees", "Blackwater", "the Great Plains" },
		["wapiti"] = new string[2] { "the Grizzlies", "Ambarino" },
		[""] = new string[5] { "Valentine", "Rhodes", "Blackwater", "Strawberry", "Saint Denis" }
	};

	internal int EntitiesTracked => _modelOf.Count;

	internal void NoteModel(Ped p)
	{
		if (p != null && p.Exists())
		{
			_modelOf[p.Handle] = (uint)p.Model.Hash;
		}
	}

	private IEnumerable<(string name, ICollection<int> keys, Action<int> remove)> BookkeepingMaps()
	{
		yield return (name: "dismount", keys: _dismountOrderedAt.Keys, remove: delegate(int h)
		{
			_dismountOrderedAt.Remove(h);
		});
		yield return (name: "beatCounted", keys: _beatCounted.Keys, remove: delegate(int h)
		{
			_beatCounted.Remove(h);
		});
		yield return (name: "groupcache", keys: _groupCache.Keys, remove: delegate(int h)
		{
			_groupCache.Remove(h);
		});
		yield return (name: "together", keys: _togetherAt.Keys, remove: delegate(int h)
		{
			_togetherAt.Remove(h);
		});
		yield return (name: "hitbyyou", keys: _hitByYouAt.Keys, remove: delegate(int h)
		{
			_hitByYouAt.Remove(h);
		});
		yield return (name: "antagAt", keys: _antagAt.Keys, remove: delegate(int h)
		{
			_antagAt.Remove(h);
		});
		yield return (name: "peaceWaiting", keys: _peaceWaiting.Keys, remove: delegate(int h)
		{
			_peaceWaiting.Remove(h);
		});
		yield return (name: "lawTook", keys: _lawTook, remove: delegate(int h)
		{
			_lawTook.Remove(h);
		});
		yield return (name: "selfDefence", keys: _selfDefence, remove: delegate(int h)
		{
			_selfDefence.Remove(h);
		});
		yield return (name: "cameAtYou", keys: _cameAtYou, remove: delegate(int h)
		{
			_cameAtYou.Remove(h);
		});
		yield return (name: "remountTries", keys: _remountTries.Keys, remove: delegate(int h)
		{
			_remountTries.Remove(h);
		});
		yield return (name: "recentFoes", keys: _recentFoes.Keys, remove: delegate(int h)
		{
			_recentFoes.Remove(h);
		});
		yield return (name: "runnerRolled", keys: _runnerRolled, remove: delegate(int h)
		{
			_runnerRolled.Remove(h);
		});
		yield return (name: "letGoRunners", keys: _letGoRunners, remove: delegate(int h)
		{
			_letGoRunners.Remove(h);
		});
		yield return (name: "sparedBound", keys: _sparedBound.Keys, remove: delegate(int h)
		{
			_sparedBound.Remove(h);
		});
		yield return (name: "walkOffs", keys: _walkOffs.Keys, remove: delegate(int h)
		{
			_walkOffs.Remove(h);
		});
		yield return (name: "tableGame", keys: _tableGame.Keys, remove: delegate(int h)
		{
			_tableGame.Remove(h);
		});
		yield return (name: "wentForYouAt", keys: _wentForYouAt.Keys, remove: delegate(int h)
		{
			_wentForYouAt.Remove(h);
		});
		yield return (name: "gunOn", keys: _gunOnAt.Keys, remove: delegate(int h)
		{
			_gunOnAt.Remove(h);
		});
		yield return (name: "grouplinked", keys: _groupLinked, remove: delegate(int h)
		{
			_groupLinked.Remove(h);
		});
		yield return (name: "restrained", keys: _restrainedAt.Keys, remove: delegate(int h)
		{
			_restrainedAt.Remove(h);
		});
		yield return (name: "tied", keys: _tiedCounted, remove: delegate(int h)
		{
			_tiedCounted.Remove(h);
		});
		yield return (name: "sidedPeds", keys: _sidedPeds, remove: delegate(int h)
		{
			_sidedPeds.Remove(h);
		});
		yield return (name: "patrolOpen", keys: _patrolOpen, remove: delegate(int h)
		{
			_patrolOpen.Remove(h);
		});
		yield return (name: "patrolGroupWas", keys: _patrolGroupWas.Keys, remove: delegate(int h)
		{
			_patrolGroupWas.Remove(h);
		});
		yield return (name: "shookAt", keys: _shookAt.Keys, remove: delegate(int h)
		{
			_shookAt.Remove(h);
		});
		yield return (name: "tippedAt", keys: _tippedAt.Keys, remove: delegate(int h)
		{
			_tippedAt.Remove(h);
		});
		yield return (name: "drinkAt", keys: _drinkAt.Keys, remove: delegate(int h)
		{
			_drinkAt.Remove(h);
		});
		yield return (name: "gaveAt", keys: _gaveAt.Keys, remove: delegate(int h)
		{
			_gaveAt.Remove(h);
		});
		yield return (name: "antagonized", keys: _antagonized.Keys, remove: delegate(int h)
		{
			_antagonized.Remove(h);
		});
		yield return (name: "outlawSwitched", keys: _outlawSwitched.Keys, remove: delegate(int h)
		{
			_outlawSwitched.Remove(h);
		});
		if (_hisPeople != null)
		{
			yield return (name: "hisPeople", keys: _hisPeople.Men.Select((Ped p) => p.Handle).ToList(), remove: delegate(int h)
			{
				_hisPeople?.Men.RemoveAll((Ped p) => p.Handle == h);
			});
		}
		yield return (name: "horseRiders", keys: _horseRiders.Keys, remove: delegate(int h)
		{
			_horseRiders.Remove(h);
			_horseHurtAt.Remove(h);
		});
		yield return (name: "rescued", keys: _rescuedAt.Keys, remove: delegate(int h)
		{
			_rescuedAt.Remove(h);
		});
		yield return (name: "toldToFlee", keys: _toldToFlee.Keys, remove: delegate(int h)
		{
			_toldToFlee.Remove(h);
		});
		yield return (name: "maskedSeen", keys: _maskedSeen.Keys, remove: delegate(int h)
		{
			_maskedSeen.Remove(h);
		});
		yield return (name: "uneaseFear", keys: _uneaseFearAt.Keys, remove: delegate(int h)
		{
			_uneaseFearAt.Remove(h);
		});
		yield return (name: "robbed", keys: _robbedAt.Keys, remove: delegate(int h)
		{
			_robbedAt.Remove(h);
		});
		yield return (name: "combatants", keys: _combatants.Keys, remove: delegate(int h)
		{
			_combatants.Remove(h);
		});
		yield return (name: "countedDeaths", keys: _countedDeaths, remove: delegate(int h)
		{
			_countedDeaths.Remove(h);
		});
		yield return (name: "shotAt", keys: _shotAtAt.Keys, remove: delegate(int h)
		{
			_shotAtAt.Remove(h);
		});
		yield return (name: "comradeScored", keys: _comradeScored, remove: delegate(int h)
		{
			_comradeScored.Remove(h);
		});
		yield return (name: "killNoted", keys: _killNoted, remove: delegate(int h)
		{
			_killNoted.Remove(h);
		});
		yield return (name: "knockedOut", keys: _knockedOutWorld, remove: delegate(int h)
		{
			_knockedOutWorld.Remove(h);
		});
		yield return (name: "patrolMoved", keys: _patrolMoved.Keys, remove: delegate(int h)
		{
			_patrolMoved.Remove(h);
		});
		yield return (name: "cards", keys: _cards.Keys, remove: delegate(int h)
		{
			_cards.Remove(h);
			_cardHeadZ.Remove(h);
			_cardPrevRaw.Remove(h);
			_cardVel.Remove(h);
			_cardBlocked.Remove(h);
			_cardHead.Remove(h);
			_cardFirstSeen.Remove(h);
		});
		yield return (name: "factionHits", keys: _factionHits.Keys, remove: delegate(int h)
		{
			_factionHits.Remove(h);
		});
		yield return (name: "vehicleDrivers", keys: _vehicleDrivers.Keys, remove: delegate(int h)
		{
			_vehicleDrivers.Remove(h);
		});
		yield return (name: "looks", keys: _fpOf.Keys, remove: delegate(int h)
		{
			_fpOf.Remove(h);
		});
		yield return (name: "roadside", keys: _reNearSince.Keys, remove: delegate(int h)
		{
			_reNearSince.Remove(h);
		});
		yield return (name: "witnessShadow", keys: _witnessShadow.Keys, remove: delegate(int h)
		{
			_witnessShadow.Remove(h);
		});
		yield return (name: "gotDown", keys: _gotDown, remove: delegate(int h)
		{
			_gotDown.Remove(h);
		});
		yield return (name: "settleFrom", keys: _settleFrom.Keys, remove: delegate(int h)
		{
			_settleFrom.Remove(h);
		});
		yield return (name: "retarget", keys: _retargetAt.Keys, remove: delegate(int h)
		{
			_retargetAt.Remove(h);
		});
		yield return (name: "recognised", keys: _recognisedAt.Keys, remove: delegate(int h)
		{
			_recognisedAt.Remove(h);
		});
		yield return (name: "fighters", keys: _fighters.Keys, remove: delegate(int h)
		{
			_fighters.Remove(h);
		});
		yield return (name: "likeGreet", keys: _likeGreetAt.Keys, remove: delegate(int h)
		{
			_likeGreetAt.Remove(h);
		});
		yield return (name: "unease", keys: _unease.Keys, remove: delegate(int h)
		{
			_unease.Remove(h);
			_walkedOff.Remove(h);
		});
		yield return (name: "roadsideMet", keys: _reMet, remove: delegate(int h)
		{
			_reMet.Remove(h);
		});
		yield return (name: "roadsideRode", keys: _reRode, remove: delegate(int h)
		{
			_reRode.Remove(h);
		});
	}

	private IEnumerable<(string name, ICollection<int> keys, Action<int> remove)> LifecycleMaps()
	{
		yield return (name: "provoked", keys: _provoked.Keys, remove: delegate(int h)
		{
			_provoked.Remove(h);
		});
		yield return (name: "worldNemeses", keys: _worldNemeses.Keys, remove: delegate(int h)
		{
			_worldNemeses.Remove(h);
		});
		yield return (name: "followerOf", keys: _followerOf.Keys, remove: delegate(int h)
		{
			_followerOf.Remove(h);
		});
		yield return (name: "skirmishNamed", keys: _skirmishNamed.Keys, remove: delegate(int h)
		{
			_skirmishNamed.Remove(h);
		});
		yield return (name: "named", keys: _named.Keys, remove: delegate(int h)
		{
			_named.Remove(h);
		});
		yield return (name: "kids", keys: _kids.Keys, remove: delegate(int h)
		{
			_kids.Remove(h);
		});
		yield return (name: "acquaintances", keys: _acqOf.Keys, remove: delegate(int h)
		{
			_acqOf.Remove(h);
		});
		yield return (name: "interroMemory", keys: _interroMemory.Keys, remove: delegate(int h)
		{
			_interroMemory.Remove(h);
		});
		yield return (name: "nemesisBodies", keys: _nemesisBodies.Keys, remove: delegate(int h)
		{
			_nemesisBodies.Remove(h);
		});
	}

	private void PruneEntities()
	{
		if (Game.GameTime < _nextEntityPrune)
		{
			return;
		}
		_nextEntityPrune = Game.GameTime + 1000;
		List<(string name, ICollection<int> keys, Action<int> remove)> list = BookkeepingMaps().ToList();
		List<(string name, ICollection<int> keys, Action<int> remove)> list2 = LifecycleMaps().ToList();
		HashSet<int> all = new HashSet<int>();
		foreach (var item2 in list.Concat(list2))
		{
			foreach (int item3 in item2.Item2)
			{
				all.Add(item3);
			}
		}
		foreach (var item4 in OurPeds())
		{
			Ped item = item4.p;
			if (item != null)
			{
				all.Add(item.Handle);
			}
		}
		foreach (Ped item5 in _encHorses.Concat(_hunterHorses).Concat(_warHorses).Concat(_supplyWagonHorses)
			.Concat(_raids.SelectMany((Raid r) => r.Horses)))
		{
			if (item5 != null)
			{
				all.Add(item5.Handle);
			}
		}
		CalibrateOwnCheck();
		PruneOwn();
		_affilTold.RemoveWhere(((int, string) t) => !ENTITY.DOES_ENTITY_EXIST(t.Item1));
		foreach (int h in all)
		{
			if (!ENTITY.DOES_ENTITY_EXIST(h))
			{
				foreach (var item6 in list)
				{
					if (item6.Item2.Contains(h))
					{
						item6.Item3(h);
					}
				}
				GunMelee.Forget(h);
				Downed.Forget(h);
				if (!list2.Any(((string name, ICollection<int> keys, Action<int> remove) m) => m.keys.Contains(h)))
				{
					_modelOf.Remove(h);
					HandlesGone++;
				}
				continue;
			}
			uint num = ENTITY.GET_ENTITY_MODEL(h);
			if (!_modelOf.TryGetValue(h, out var value))
			{
				_modelOf[h] = num;
			}
			else
			{
				if (value == num)
				{
					continue;
				}
				List<string> values = (from m in list.Concat(list2)
					where m.keys.Contains(h)
					select m.name).ToList();
				foreach (var item7 in list.Concat(list2))
				{
					if (item7.Item2.Contains(h))
					{
						item7.Item3(h);
					}
				}
				GunMelee.Forget(h);
				Downed.Forget(h);
				_modelOf[h] = num;
				HandlesReused++;
				GrudgeLog(string.Format("ENTITY handle {0} reused (model 0x{1:X8} -> 0x{2:X8}): dropped from {3}", h, value, num, string.Join(", ", values)));
			}
		}
		foreach (int item8 in _modelOf.Keys.Where((int k) => !all.Contains(k)).ToList())
		{
			_modelOf.Remove(item8);
		}
	}

	internal string EntityDebugLine()
	{
		return $"entities: {EntitiesTracked} handles held - {HandlesReused} reused, {HandlesGone} gone (pruned in one place)";
	}
}
