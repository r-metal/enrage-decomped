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
	private sealed class MaskedSighting
	{
		public string Crime;

		public string Text;

		public int Fear;

		public int At;

		public Vector3 Where;
	}

	private readonly Dictionary<int, MaskedSighting> _maskedSeen = new Dictionary<int, MaskedSighting>();

	private int _nextUnmaskCheck;

	internal const string VdlId = "vdl";

	internal static readonly (int r, int g, int b) VdlAccent = (r: 170, g: 140, b: 110);

	internal void NoteMaskedWitness(Ped p, string crime, string text, int fear, Vector3 at)
	{
		if (!(p == null) && p.Exists() && (!_maskedSeen.TryGetValue(p.Handle, out var value) || value.Fear < fear || Game.GameTime - value.At >= 600000))
		{
			_maskedSeen[p.Handle] = new MaskedSighting
			{
				Crime = crime,
				Text = text,
				Fear = fear,
				At = Game.GameTime,
				Where = at
			};
		}
	}

	private void UpdateUnmasking(Ped player)
	{
		if (Game.GameTime < _nextUnmaskCheck)
		{
			return;
		}
		_nextUnmaskCheck = Game.GameTime + 500;
		foreach (int item in (from kv in _maskedSeen
			where Game.GameTime - kv.Value.At > 600000
			select kv.Key).ToList())
		{
			_maskedSeen.Remove(item);
		}
		bool flag;
		try
		{
			flag = PlayerMasked(player);
		}
		catch
		{
			return;
		}
		if (flag)
		{
			return;
		}
		_ = player.Position;
		foreach (KeyValuePair<int, MaskedSighting> item2 in _maskedSeen.ToList())
		{
			Ped ped = Entity.FromHandle(item2.Key) as Ped;
			if (ped == null || !ped.Exists() || ped.IsDead)
			{
				_maskedSeen.Remove(item2.Key);
			}
			else if (SeesYourFace(ped, player))
			{
				_maskedSeen.Remove(item2.Key);
				Recognised(ped, player, item2.Value);
			}
		}
		foreach (KeyValuePair<int, Provocation> item3 in _provoked.ToList())
		{
			Provocation value = item3.Value;
			if (value.SawFace || !value.SeenMasked || value.Record != null || Game.GameTime - value.LastActionAt > 600000)
			{
				continue;
			}
			Ped ped2 = Entity.FromHandle(item3.Key) as Ped;
			if (!(ped2 == null) && ped2.Exists() && !ped2.IsDead && SeesYourFace(ped2, player))
			{
				value.SawFace = true;
				if (_acqOf.TryGetValue(item3.Key, out var value2))
				{
					value2.SawFace = true;
					_peopleDirty = true;
				}
				bool settled = value.Settled;
				if (settled)
				{
					value.Settled = false;
					value.LastActionAt = Game.GameTime - Balance.GsSettleSeconds * 1000;
				}
				GrudgeLog(string.Format("UNMASKED {0} saw your face - the masked man was you (grudge {1:0}{2})", WhoIs(item3.Key), value.Score, settled ? ", decides again" : ""));
				Feed(Pron.Her($"UNMASKED {WhoIs(item3.Key)} saw your face - he knows it was you (grudge {value.Score:0})", Pron.FemaleAt(item3.Key)), Theme.Warn);
				OutfitLearnsYourName(ped2, WhoIs(item3.Key));
			}
		}
	}

	private static bool SeesYourFace(Ped p, Ped player)
	{
		float num = p.Position.DistanceTo(player.Position);
		if (num > 20f)
		{
			return false;
		}
		if (!(num < 4f))
		{
			return ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY_IN_FRONT(p.Handle, player.Handle, 17);
		}
		return true;
	}

	private void Recognised(Ped p, Ped player, MaskedSighting s)
	{
		NemesisRecord nemesisRecord = RecordAt(p.Handle);
		if (nemesisRecord != null)
		{
			int fear = nemesisRecord.Fear;
			nemesisRecord.AddFear(Math.Max(1, s.Fear / 3));
			History(nemesisRecord, "SAW YOUR FACE - the masked man who " + s.Text.Replace("saw you ", "") + " was you", Theme.Warn);
			GrudgeLog($"UNMASKED {nemesisRecord.Name} (nemesis) saw your face - fear {fear} -> {nemesisRecord.Fear}");
			Feed($"UNMASKED {nemesisRecord.Name} saw your face - fear {fear} -> {nemesisRecord.Fear}", Theme.Warn, nemesisRecord);
			OutfitLearnsYourName(p, nemesisRecord.Name);
			return;
		}
		if (!_named.ContainsKey(p.Handle) && LookReadable(p))
		{
			_named[p.Handle] = NameByLook(p);
			_namesGiven++;
		}
		_rememberingWitness = true;
		try
		{
			RememberPerson(p.Handle, met: false);
		}
		finally
		{
			_rememberingWitness = false;
		}
		if (_acqOf.TryGetValue(p.Handle, out var value))
		{
			float num = ShadowOf(p).FearGainMult * Balance.FearGainScale;
			int fear2 = value.Fear;
			value.Fear = Math.Min(100, value.Fear + Math.Max(1, (int)Math.Round((float)s.Fear * num)));
			value.LastScaredMin = GameMinutesNow();
			value.SawFace = true;
			(value.Witnessed ?? (value.Witnessed = new List<string>())).Add(s.Text + " (masked) - then saw your face - " + GetZoneName(s.Where) + ", " + GameDateShort());
			TrimWitnessed(value);
			_peopleDirty = true;
			MarkNamed(p.Handle, 3);
			NoteWitness(p.Handle, value, s.Text);
			GrudgeLog(string.Format("UNMASKED {0} saw your face - the masked man who {1} was you: fear {2} -> {3}", value.Name, s.Text.Replace("saw you ", ""), fear2, value.Fear));
			Feed(Pron.Her($"UNMASKED {value.Name} saw your face - he knows it was you (fear {fear2} -> {value.Fear})", Pron.FemaleAt(p.Handle)), Theme.Warn);
			OutfitLearnsYourName(p, value.Name);
		}
	}

	private void OutfitLearnsYourName(Ped p, string who)
	{
		Gang gang = Gangs.ById(RecordAt(p.Handle)?.Gang) ?? Gangs.ForModel((uint)p.Model.Hash);
		if (gang != null && Chronicle.MaskedWithheld != null && Chronicle.MaskedWithheld.TryGetValue(gang.Id, out var value) && !(value < 0.5f))
		{
			Chronicle.MaskedWithheld[gang.Id] = 0f;
			SaveWorld();
			AddNotoriety(gang, (int)Math.Round(value), who + " knows the masked man was you");
		}
	}

	internal void FaceSeenInQuestioning(Ped p, Ped player)
	{
		if (p == null || !p.Exists())
		{
			return;
		}
		bool flag;
		try
		{
			flag = PlayerMasked(player);
		}
		catch
		{
			return;
		}
		if (flag)
		{
			return;
		}
		_maskedSeen.TryGetValue(p.Handle, out var value);
		if (value != null)
		{
			_maskedSeen.Remove(p.Handle);
			Recognised(p, player, value);
		}
		Provocation provocation = ((RecordAt(p.Handle) == null) ? RememberStranger(p.Handle) : null);
		if (provocation != null && !provocation.SawFace)
		{
			provocation.SawFace = true;
			if (provocation.Settled && provocation.Score > 0f)
			{
				provocation.Settled = false;
				provocation.LastActionAt = Game.GameTime - Balance.GsSettleSeconds * 1000;
			}
			GrudgeLog($"UNMASKED {WhoIs(p.Handle)} has seen your face - you questioned him barefaced (grudge {provocation.Score:0})");
			if (provocation.SeenMasked)
			{
				OutfitLearnsYourName(p, WhoIs(p.Handle));
			}
		}
		if (_acqOf.TryGetValue(p.Handle, out var value2) && value2.SawFace != true)
		{
			value2.SawFace = true;
			_peopleDirty = true;
		}
	}
}
