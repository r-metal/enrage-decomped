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
	private const int DecorTypeInt = 3;

	private int _honorDecor = -1;

	private int _nextKlanHonor;

	private int _klanHonorSet;

	private readonly HashSet<int> _honorMarked = new HashSet<int>();

	private readonly HashSet<int> _klanBodies = new HashSet<int>();

	private static readonly uint[] KlanRallyModels = new uint[2] { 138961043u, 969427509u };

	private readonly List<LawTestMan> _lawTest = new List<LawTestMan>();

	private int _lawTestUntil;

	private int _lawTestBounty;

	private void KlanHonorTick()
	{
		if (Game.GameTime < _nextKlanHonor)
		{
			return;
		}
		_nextKlanHonor = Game.GameTime + 500;
		if (_honorDecor < 0)
		{
			AskHonorDecor();
		}
		if (_honorDecor == 0)
		{
			return;
		}
		foreach (int item in OurHandles())
		{
			if (!_honorMarked.Contains(item) && ENTITY.DOES_ENTITY_EXIST(item) && KlanMan(item))
			{
				MarkHonor(item);
			}
		}
		if (_honorMarked.Count > 256)
		{
			_honorMarked.RemoveWhere((int h) => !ENTITY.DOES_ENTITY_EXIST(h));
		}
		if (_klanBodies.Count > 64)
		{
			_klanBodies.RemoveWhere((int h) => !ENTITY.DOES_ENTITY_EXIST(h));
		}
	}

	private void MarkHonor(int h)
	{
		if (_honorDecor < 0)
		{
			AskHonorDecor();
		}
		if (_honorDecor == 1 && _honorMarked.Add(h))
		{
			Crumbs.Native("klan honor decor");
			bool flag = Function.Call<bool>(16757697947711703311uL, new InputArgument[3]
			{
				h,
				new InputArgument(UiDraw.CString("honor_override")),
				0
			});
			if (_klanHonorSet++ == 0)
			{
				GrudgeLog(string.Format("KLANHONOR first Klansman marked (ped {0}{1}): {2}", h, ENTITY.IS_ENTITY_DEAD(h) ? ", a body" : "", flag ? "set" : "the game refused it"));
			}
		}
	}

	internal void KlanBody(Ped body, string gangId)
	{
		if (!(body == null) && body.Exists() && !(gangId != Gangs.Klan.Id))
		{
			_klanBodies.Add(body.Handle);
			MarkHonor(body.Handle);
		}
	}

	private void AskHonorDecor()
	{
		List<int> list = new List<int>();
		for (int i = 1; i <= 7; i++)
		{
			if (Function.Call<bool>(8229574572667171442uL, new InputArgument[2]
			{
				new InputArgument(UiDraw.CString("honor_override")),
				i
			}))
			{
				list.Add(i);
			}
		}
		_honorDecor = (list.Contains(3) ? 1 : 0);
		GrudgeLog((_honorDecor == 1) ? "KLANHONOR the game knows \"honor_override\" (an int decorator) - every Klansman of ours is marked: killing him costs no honor" : ((list.Count > 0) ? ("KLANHONOR \"honor_override\" is a decorator of type " + string.Join(", ", list) + ", not an int - Klansmen aren't marked") : "KLANHONOR the game has no \"honor_override\" decorator - Klansmen aren't marked (killing one may still cost honor)"));
	}

	private IEnumerable<int> OurHandles()
	{
		foreach (var item2 in OurPeds())
		{
			Ped item = item2.p;
			if (item != null)
			{
				yield return item.Handle;
			}
		}
		foreach (int item3 in _followerOf.Keys.ToList())
		{
			yield return item3;
		}
		foreach (int item4 in _worldNemeses.Keys.ToList())
		{
			yield return item4;
		}
		foreach (int item5 in _nemesisBodies.Keys.ToList())
		{
			yield return item5;
		}
		foreach (int item6 in _klanBodies.ToList())
		{
			yield return item6;
		}
	}

	private bool KlanMan(int h)
	{
		uint value = ENTITY.GET_ENTITY_MODEL(h);
		if (KlanRallyModels.Contains(value))
		{
			return true;
		}
		if (_klanBodies.Contains(h))
		{
			return true;
		}
		if (_nemesisBodies.TryGetValue(h, out var value2) && value2?.Gang == Gangs.Klan.Id)
		{
			return true;
		}
		if (_worldNemeses.TryGetValue(h, out var value3) && value3?.Gang == Gangs.Klan.Id)
		{
			return true;
		}
		if (_followerOf.TryGetValue(h, out var value4) && value4?.Gang == Gangs.Klan.Id)
		{
			return true;
		}
		if (_activeNemesisPed != null && _activeNemesisPed.Handle == h && ActiveRecord?.Gang == Gangs.Klan.Id)
		{
			return true;
		}
		if (_townMen.Any((TownMan t) => t.Ped != null && t.Ped.Handle == h && t.Rec?.Gang == Gangs.Klan.Id))
		{
			return true;
		}
		return _liveCamps.Values.Any((LiveCamp c) => c.Boss?.Gang == Gangs.Klan.Id && c.Peds.Any((Ped p) => p != null && p.Handle == h));
	}
}
