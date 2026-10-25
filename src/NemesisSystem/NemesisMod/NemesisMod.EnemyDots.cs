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
	private int _nextEnemyDots;

	private readonly Dictionary<int, uint> _modelOf = new Dictionary<int, uint>();

	private int _nextEntityPrune;

	private void UpdateEnemyDots(Ped player)
	{
		if (Game.GameTime < _nextEnemyDots)
		{
			return;
		}
		_nextEnemyDots = Game.GameTime + 500;
		HashSet<int> hashSet = new HashSet<int>();
		IEnumerable<(Ped, OurRole)> second = from h in _followerOf.Keys.Concat(_worldNemeses.Keys).Distinct().ToList()
			select (Entity.FromHandle(h) as Ped, WorldNemesis: OurRole.WorldNemesis);
		foreach (var (ped, ourRole) in OurPeds().Concat<(Ped, OurRole)>(second))
		{
			if ((ped != null && IsCompanion(ped.Handle)) || ourRole == OurRole.Companion || ourRole == OurRole.Regular || ped == null || !ped.Exists() || ped.IsDead || !ped.IsHuman)
			{
				continue;
			}
			int handle = ped.Handle;
			if (!hashSet.Add(handle))
			{
				continue;
			}
			bool flag = PED.IS_PED_IN_COMBAT(handle, player.Handle);
			if (_enemyDots.TryGetValue(handle, out (Blip, int) value))
			{
				if (flag)
				{
					_enemyDots[handle] = (value.Item1, Game.GameTime);
				}
			}
			else if (flag && MAP.GET_BLIP_FROM_ENTITY(handle) == 0)
			{
				Crumbs.Native("EnemyDots blip add (V2)");
				Blip blip = ped.AddBlip(BlipType.BLIP_STYLE_ENEMY);
				if (blip != null)
				{
					_enemyDots[handle] = (blip, Game.GameTime);
				}
			}
		}
		foreach (int item in _enemyDots.Keys.ToList())
		{
			var (blip2, num) = _enemyDots[item];
			if (!ENTITY.DOES_ENTITY_EXIST(item) || ENTITY.IS_ENTITY_DEAD(item) || !hashSet.Contains(item) || Game.GameTime - num > 8000)
			{
				if (blip2 != null && blip2.Exists())
				{
					blip2.Delete();
				}
				_enemyDots.Remove(item);
			}
		}
	}

	private void ClearEnemyDots()
	{
		foreach (var value in _enemyDots.Values)
		{
			Blip item = value.blip;
			if (item != null && item.Exists())
			{
				item.Delete();
			}
		}
		_enemyDots.Clear();
	}
}
