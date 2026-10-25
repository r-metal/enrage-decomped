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
	private readonly Dictionary<int, float> _cardHeadZ = new Dictionary<int, float>();

	private readonly Dictionary<int, (Vector3 head, int frame)> _cardHead = new Dictionary<int, (Vector3, int)>();

	private readonly Dictionary<int, int> _cardFirstSeen = new Dictionary<int, int>();

	private string _cardTown;

	private int _cardTownAt = -100000;

	private int _cardTownLeftAt = -100000;

	private const int CardBoneReadsPerFrame = 6;

	private const int CardRaysPerFrame = 3;

	private const int CardNewPedMs = 1000;

	private const int CardHoldMs = 4000;

	private const int CardJumpMs = 12000;

	private int _cardFrame;

	private readonly Dictionary<int, (float x, float y)> _cardPrevRaw = new Dictionary<int, (float, float)>();

	private readonly Dictionary<int, (float x, float y)> _cardVel = new Dictionary<int, (float, float)>();

	private readonly List<(int h, bool known)> _cardCand = new List<(int, bool)>();

	private int _cardCandAt = -100000;

	private int _cardCandMode = -1;

	private int _cardCandTarget;

	private const int NameCardLingerMs = 500;

	private int _cardTargetSeen;

	private int _cardTargetAt;

	private const float PlanLiveRange = 220f;

	private readonly Dictionary<string, int> _funeralSeenAt = new Dictionary<string, int>();

	private static (int r, int g, int b) GrudgeColor(float score)
	{
		if (!(score >= (float)Balance.GsThreshold))
		{
			if (!(score >= (float)Balance.GsFloor))
			{
				if (!(score > 0f))
				{
					return (r: 190, g: 188, b: 180);
				}
				return (r: 200, g: 190, b: 130);
			}
			return Theme.Warn;
		}
		return Theme.Blood;
	}

	private static float OneEuroAlpha(float cutoff, float dt)
	{
		float num = 1f / ((float)Math.PI * 2f * Math.Max(0.01f, cutoff));
		return 1f / (1f + num / dt);
	}

	private unsafe int CardTarget()
	{
		int handle = Game.Player.Handle;
		int num = 0;
		if (((PLAYER.GET_PLAYER_INTERACTION_TARGET_ENTITY(handle, &num, p2: false, p3: false) && num != 0) || (PLAYER.GET_PLAYER_TARGET_ENTITY(handle, &num) && num != 0) || (PLAYER.GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(handle, &num) && num != 0)) && ENTITY.IS_ENTITY_A_PED(num))
		{
			_cardTargetSeen = num;
			_cardTargetAt = UiDraw.RealMs;
			return num;
		}
		if (_cardTargetSeen == 0 || UiDraw.RealMs - _cardTargetAt >= 500)
		{
			return 0;
		}
		return _cardTargetSeen;
	}

	private bool HasNameCard(int h)
	{
		if ((!(_activeNemesisPed != null) || _activeNemesisPed.Handle != h) && !_worldNemeses.ContainsKey(h) && !_named.ContainsKey(h))
		{
			return _kids.ContainsKey(h);
		}
		return true;
	}

	private static string GrudgeWord(float score)
	{
		if (!(score < 25f))
		{
			if (!(score < 45f))
			{
				return "Seething";
			}
			return "Bitter";
		}
		return "Annoyed";
	}

	private string CardRankWord(NemesisRecord rec, Gang g)
	{
		string text = MemberWord(g);
		string text2 = ((rec.Id != null && rec.Id == SuccessorId(g)) ? "Leader" : null);
		if (text2 == null && g.Tiers != null)
		{
			(string, int, int)[] tiers = g.Tiers;
			for (int i = 0; i < tiers.Length; i++)
			{
				(string, int, int) tuple = tiers[i];
				if (rec.Rank >= tuple.Item2)
				{
					text2 = Backstory.Singular(tuple.Item1);
					break;
				}
			}
		}
		if (string.IsNullOrEmpty(text2))
		{
			return text.ToUpperInvariant();
		}
		if (text.EndsWith(text2, StringComparison.OrdinalIgnoreCase))
		{
			return text.ToUpperInvariant();
		}
		return (text + " " + text2).ToUpperInvariant();
	}

	private void DrawNameCards(Ped player)
	{
		int nameCards = Config.NameCards;
		bool flag = Game.GameTime - _lastJumpAt < 12000 || Game.GameTime - _loadScreenAt < 12000;
		if (_currentTown != _cardTown)
		{
			if (_currentTown != null && Game.GameTime - _cardTownLeftAt >= 30000)
			{
				_cardTownAt = Game.GameTime;
			}
			if (_currentTown == null)
			{
				_cardTownLeftAt = Game.GameTime;
			}
			_cardTown = _currentTown;
		}
		bool flag2 = Game.GameTime - _cardTownAt < 4000 || Game.GameTime - _lastSaveSeenAt < 4000;
		if (nameCards <= 0 || flag || _menu.IsOpen || _bulletImpact.ViewerOpen || _convo != null || _tg != null || _iniPanel.IsOpen)
		{
			_cards.Clear();
			_cardHeadZ.Clear();
			_cardPrevRaw.Clear();
			_cardVel.Clear();
			_cardBlocked.Clear();
			_cardHead.Clear();
			_cardFirstSeen.Clear();
			return;
		}
		float knownRange = Config.NameCardKnownRange;
		float strangerRange = (_debugOn ? Config.NamingRadius : Config.NameCardRange);
		Vector3 me = player.Position;
		int num = ((nameCards == 3) ? CardTarget() : 0);
		HashSet<int> once;
		if (UiDraw.RealMs - _cardCandAt >= 250 || nameCards != _cardCandMode || num != _cardCandTarget)
		{
			_cardCandAt = UiDraw.RealMs;
			_cardCandMode = nameCards;
			_cardCandTarget = num;
			_cardCand.Clear();
			once = new HashSet<int>();
			if (nameCards == 3)
			{
				if (num != 0 && HasNameCard(num))
				{
					Cand(num, known: true);
				}
			}
			else
			{
				if (_activeNemesisPed != null)
				{
					Cand(_activeNemesisPed.Handle, known: true);
				}
				foreach (KeyValuePair<int, NemesisRecord> worldNemese in _worldNemeses)
				{
					TownMan townMan = HiddenTownMan(worldNemese.Key);
					if (townMan == null)
					{
						Cand(worldNemese.Key, known: true);
					}
					else if (townMan.Rec.PlainGreets > 0 || nameCards >= 2)
					{
						Cand(worldNemese.Key, townMan.Rec.PlainGreets > 0);
					}
				}
				foreach (KeyValuePair<int, Named> item in _named)
				{
					Provocation value;
					bool flag3 = item.Value.Met || _acqOf.ContainsKey(item.Key) || (_provoked.TryGetValue(item.Key, out value) && value.Score > 0f);
					if (flag3 || nameCards >= 2)
					{
						Cand(item.Key, flag3);
					}
				}
				foreach (KeyValuePair<int, Kid> kid in _kids)
				{
					if (kid.Value.Met || nameCards >= 2)
					{
						Cand(kid.Key, kid.Value.Met);
					}
				}
			}
		}
		List<(Ped, float, float, bool)> list = new List<(Ped, float, float, bool)>();
		HashSet<int> hashSet = new HashSet<int>();
		foreach (var (num2, flag4) in _cardCand)
		{
			if (!ENTITY.DOES_ENTITY_EXIST(num2))
			{
				continue;
			}
			Ped ped = new Ped(num2);
			if (!ped.IsDead)
			{
				float num3 = (flag4 ? knownRange : strangerRange);
				float num4 = ped.Position.DistanceTo(me);
				if (num4 <= num3)
				{
					list.Add((ped, num4, num3, false));
					hashSet.Add(num2);
				}
			}
		}
		foreach (KeyValuePair<int, (float, float, float, int)> card in _cards)
		{
			if (card.Value.Item4 == _cardFrame && !(card.Value.Item1 <= 0f) && !hashSet.Contains(card.Key) && ENTITY.DOES_ENTITY_EXIST(card.Key))
			{
				Ped ped2 = new Ped(card.Key);
				if (!ped2.IsDead)
				{
					list.Add((ped2, ped2.Position.DistanceTo(me), knownRange, true));
				}
			}
		}
		_cardFrame++;
		float realDt = UiDraw.RealDt;
		int num5 = 0;
		int num6 = 0;
		foreach (var (ped3, num7, num8, flag5) in list.OrderBy<(Ped, float, float, bool), float>(((Ped ped, float d, float range, bool leaving) c) => c.d).Take((_debugOn && DebugMode != 6) ? 8 : 14))
		{
			if (!ENTITY.DOES_ENTITY_EXIST(ped3.Handle) || !ENTITY.IS_ENTITY_A_PED(ped3.Handle))
			{
				continue;
			}
			Crumbs.Native("NameCards ready to render");
			if (!PED.IS_PED_READY_TO_RENDER(ped3.Handle))
			{
				continue;
			}
			if (!_cardFirstSeen.TryGetValue(ped3.Handle, out var value2))
			{
				if (flag2)
				{
					continue;
				}
				Named value3;
				int num9 = ((_named.TryGetValue(ped3.Handle, out value3) && value3.NamedAt > 0 && value3.NamedAt <= Game.GameTime) ? value3.NamedAt : Game.GameTime);
				value2 = (_cardFirstSeen[ped3.Handle] = num9);
				if (Game.GameTime - value2 < 1000)
				{
					continue;
				}
			}
			if (Game.GameTime - value2 < 1000)
			{
				continue;
			}
			Vector3 position = ped3.Position;
			(Vector3, int) value4;
			bool flag6 = _cardHead.TryGetValue(ped3.Handle, out value4);
			if (flag2 && !flag6)
			{
				continue;
			}
			Vector3 vector;
			if (!flag2 && (!flag6 || _cardFrame - value4.Item2 >= 3) && num6 < 6)
			{
				num6++;
				Crumbs.Native("NameCards head bone");
				vector = PED.GET_PED_BONE_COORDS(ped3.Handle, 21030, 0f, 0f, 0f);
				if (vector.X != 0f || vector.Y != 0f)
				{
					_cardHead[ped3.Handle] = (vector - position, _cardFrame);
				}
			}
			else
			{
				if (!flag6)
				{
					continue;
				}
				vector = position + value4.Item1;
			}
			float num11 = ((vector.X == 0f && vector.Y == 0f) ? (position.Z + 0.75f) : vector.Z);
			if (_cardHeadZ.TryGetValue(ped3.Handle, out var value5) && Math.Abs(value5 - num11) < 1.5f)
			{
				num11 = value5 + (num11 - value5) * Math.Min(1f, realDt * 6f);
			}
			_cardHeadZ[ped3.Handle] = num11;
			float x = ((vector.X == 0f && vector.Y == 0f) ? position.X : (position.X + (vector.X - position.X) * 0.6f));
			float y = ((vector.X == 0f && vector.Y == 0f) ? position.Y : (position.Y + (vector.Y - position.Y) * 0.6f));
			if (!Projection.ToScreen(new Vector3(x, y, num11 + 0.32f), out var x2, out var y2))
			{
				_cards.Remove(ped3.Handle);
				continue;
			}
			float num12 = Math.Max(0.004f, Math.Min(0.1f, realDt));
			if (_cards.TryGetValue(ped3.Handle, out (float, float, float, int) value6) && _cardFrame - value6.Item4 == 1 && _cardPrevRaw.TryGetValue(ped3.Handle, out (float, float) value7))
			{
				_cardVel.TryGetValue(ped3.Handle, out (float, float) value8);
				float num13 = (x2 - value7.Item1) / num12;
				float num14 = (y2 - value7.Item2) / num12;
				if (Math.Abs(x2 - value7.Item1) > 0.08f || Math.Abs(y2 - value7.Item2) > 0.08f)
				{
					num13 = (num14 = 0f);
					value8 = (0f, 0f);
					value7 = (x2, y2);
				}
				float num15 = OneEuroAlpha(1f, num12);
				value8 = (value8.Item1 + (num13 - value8.Item1) * num15, value8.Item2 + (num14 - value8.Item2) * num15);
				float num16 = (float)Math.Sqrt(value8.Item1 * value8.Item1 + value8.Item2 * value8.Item2);
				float num17 = OneEuroAlpha(Config.NameCardSteady + 18f * num16, num12);
				float num18 = value7.Item1 + (x2 - value7.Item1) * num17;
				float num19 = value7.Item2 + (y2 - value7.Item2) * num17;
				_cardPrevRaw[ped3.Handle] = (num18, num19);
				_cardVel[ped3.Handle] = value8;
				x2 = num18 + value8.Item1 * num12 * Config.NameCardLead;
				y2 = num19 + value8.Item2 * num12 * Config.NameCardLead;
			}
			else
			{
				_cardPrevRaw[ped3.Handle] = (x2, y2);
				_cardVel[ped3.Handle] = (0f, 0f);
			}
			_cardBlocked.TryGetValue(ped3.Handle, out (bool, int) value9);
			if (!flag2 && (value9.Item2 == 0 || Game.GameTime - value9.Item2 > 150) && num5 < 3)
			{
				num5++;
				value9 = (CardOccluded(new Vector3(position.X, position.Y, num11 + 0.05f)), Game.GameTime);
				_cardBlocked[ped3.Handle] = value9;
			}
			bool flag7 = value9.Item2 == 0 || value9.Item1;
			if (!flag7 && ENTITY.DOES_ENTITY_EXIST(ped3.Handle))
			{
				Crumbs.Native("NameCards interior");
				int num20 = INTERIOR.GET_INTERIOR_FROM_ENTITY(ped3.Handle);
				if (num20 != 0 && num20 != INTERIOR.GET_INTERIOR_FROM_ENTITY(player.Handle))
				{
					flag7 = true;
				}
			}
			float val = Math.Max(0f, Math.Min(1f, (num8 - num7) / 12f));
			_cards.TryGetValue(ped3.Handle, out (float, float, float, int) value10);
			float num21;
			if (value10.Item4 != 0 && _cardFrame - value10.Item4 <= 3)
			{
				(num21, _, _, _) = value10;
			}
			else
			{
				num21 = 0f;
			}
			float num22 = num21;
			num22 = (flag5 ? Math.Max(0f, num22 - realDt * 4f) : (flag7 ? Math.Max(0f, num22 - realDt * 6f) : Math.Min(val, num22 + realDt * 4f)));
			float num23 = UiDraw.FromScreenX(x2);
			float num24 = UiDraw.FromScreenY(y2);
			_cards[ped3.Handle] = (num22, num23, num24, _cardFrame);
			int num25 = (int)(255f * num22);
			if (num25 < 8)
			{
				continue;
			}
			NemesisRecord nemesisRecord = RecordAt(ped3.Handle);
			string text = null;
			(int, int, int) tuple4 = Theme.Dim;
			TownMan townMan2 = ((nemesisRecord != null) ? HiddenTownMan(ped3.Handle) : null);
			string text2;
			(int, int, int) tuple5;
			(int, int, int) tuple6;
			Named value11;
			string text3;
			if (IsCompanion(ped3.Handle))
			{
				text2 = CompanionName(ped3.Handle);
				tuple5 = (110, 200, 120);
				text3 = "rides with you";
				tuple6 = (110, 200, 120);
			}
			else if (townMan2 != null)
			{
				bool num26 = nemesisRecord.PlainGreets > 0;
				text2 = nemesisRecord.Name;
				tuple5 = (num26 ? Theme.Title : (170, 168, 160));
				text3 = ((!num26) ? "Stranger" : (nemesisRecord.Personality?.ToString() ?? "")) + (_debugOn ? $" - {Gangs.ById(nemesisRecord.Gang)?.Name} in disguise, greets {nemesisRecord.PlainGreets}/3" : "");
				tuple6 = Theme.Dim;
			}
			else if (nemesisRecord != null)
			{
				tuple5 = UiDraw.HateColor(nemesisRecord.HateTier);
				text2 = nemesisRecord.Name;
				text3 = (nemesisRecord.Personality.HasValue ? $"{nemesisRecord.Personality} - " : "") + nemesisRecord.HateTier.ToString() + (_debugOn ? $"  (fear {EffectiveFear(nemesisRecord)}, {Traits.Short(IntentOf(nemesisRecord))})" : "");
				tuple6 = tuple5;
				Gang gang = Gangs.Of(nemesisRecord);
				if (gang != null && !gang.StoryOnly)
				{
					text = CardRankWord(nemesisRecord, gang);
					tuple4 = tuple5;
				}
			}
			else if (_named.TryGetValue(ped3.Handle, out value11))
			{
				_provoked.TryGetValue(ped3.Handle, out var value12);
				_acqOf.TryGetValue(ped3.Handle, out var value13);
				float num27 = value12?.Score ?? ((float)(value13?.Grudge ?? 0));
				string text4 = ((value12 != null) ? value12.Temper.ToString() : value13?.Temper);
				if (text4 == null && value13 != null)
				{
					try
					{
						string text5 = (value13.Temper = TemperOf(ped3));
						text4 = text5;
						_peopleDirty = true;
					}
					catch
					{
					}
				}
				text2 = value11.Name;
				tuple5 = (value11.Met ? Theme.Title : (170, 168, 160));
				bool flag8 = value13?.Witnessed != null && value13.Witnessed.Any((string w) => w.StartsWith("saw "));
				bool num28 = num27 >= 5f && (value13 == null || !value13.GrudgeSettled || value12 != null);
				string text7 = ((text4 != null) ? (char.ToUpperInvariant(text4[0]) + text4.Substring(1)) : null);
				text3 = (num28 ? (((text7 != null) ? (text7 + " - ") : "") + GrudgeWord(num27)) : (value11.Met ? (text7 + (flag8 ? (((text7 != null) ? " - " : "") + "Witness") : "")) : (flag8 ? "Stranger - Witness" : "Stranger")));
				if (_debugOn)
				{
					text3 = text3 + ((num27 > 0f) ? $"  (grudge {num27:0})" : "") + ((value11.Affil != null) ? ("  (" + (Gangs.ById(value11.Affil)?.Name ?? value11.Affil) + " friend)") : "") + ((value13 != null && (value13.Fear > 0 || value13.Like > 0)) ? $"  (fear {value13.Fear} like {value13.Like})" : "");
				}
				if (value11.Story)
				{
					text3 = value11.StoryTemper + ((value11.StoryNote != null) ? (((value11.StoryTemper != null) ? " - " : "") + value11.StoryNote) : "");
				}
				text3 = text3.Trim();
				if (!value11.Story)
				{
					Gang gang2 = Gangs.ForModel(ENTITY.GET_ENTITY_MODEL(ped3.Handle));
					if (gang2 != null && !gang2.StoryOnly)
					{
						text = MemberWord(gang2).ToUpperInvariant();
						tuple4 = tuple5;
					}
				}
				tuple6 = GrudgeColor(num27);
			}
			else
			{
				if (!_kids.TryGetValue(ped3.Handle, out var value14))
				{
					continue;
				}
				text2 = value14.Name;
				tuple5 = (value14.Met ? Theme.Title : (170, 168, 160));
				text3 = (value14.Boy ? "Boy" : "Girl") + (value14.Met ? (" - " + value14.Temper) : "");
				tuple6 = Theme.Dim;
			}
			float num29 = Math.Max(0f, Math.Min(1f, (num7 - 6f) / Math.Max(1f, num8 - 6f)));
			float num30 = 0.25f - 0.06f * num29;
			float num31 = 0.175f - 0.03f * num29;
			float num32 = 0.1f * num30;
			float num33 = 0.094f * num31;
			float fear = 0f;
			float like = 0f;
			float hate = 0f;
			bool nemesis = nemesisRecord != null;
			string why = null;
			if (_debugOn)
			{
				_acqOf.TryGetValue(ped3.Handle, out var value15);
				_provoked.TryGetValue(ped3.Handle, out var value16);
				fear = ((nemesisRecord != null) ? ((float)EffectiveFear(nemesisRecord)) : Math.Max(value15?.Fear ?? 0, UneaseOf(ped3.Handle)));
				like = ((nemesisRecord != null) ? 0f : ((float)(value15?.Like ?? 0)));
				hate = ((nemesisRecord != null) ? ((float)nemesisRecord.Hate) : Math.Max(value16?.Score ?? 0f, value15?.Grudge ?? 0));
				why = HateBarWhy(nemesisRecord, value16);
				num24 -= CardBarsHeight(num30, fear, like, hate);
			}
			UiDraw.TextFont(text2, UiDraw.TitleFont, num23 - Theme.TitleWidth(text2, num30) / 2f, num24 - num32 - num33 + 0.002f, num30, tuple5.Item1, tuple5.Item2, tuple5.Item3, num25);
			if (text != null)
			{
				float num34 = num31 * 0.92f;
				float num35 = 0.094f * num34;
				UiDraw.TextFont(text, UiDraw.TitleFont, num23 - Theme.TitleWidth(text, num34) / 2f, num24 - num32 - num33 - num35 + 0.004f, num34, tuple4.Item1, tuple4.Item2, tuple4.Item3, (int)((float)num25 * 0.85f));
			}
			if (text3.Length > 0)
			{
				UiDraw.TextFont(text3, UiDraw.TitleFont, num23 - Theme.TitleWidth(text3, num31) / 2f, num24 - num33 - 0.0018518518f, num31, tuple6.Item1, tuple6.Item2, tuple6.Item3, (int)((float)num25 * 0.88f));
			}
			if (_debugOn)
			{
				DrawCardBars(num23, num24, num30, num25, fear, like, hate, nemesis, why);
			}
		}
		if (_cardFrame % 120 != 0)
		{
			return;
		}
		foreach (int item2 in (from kv in _cards
			where _cardFrame - kv.Value.seenFrame > 60
			select kv.Key).ToList())
		{
			_cards.Remove(item2);
		}
		foreach (int item3 in (from k in _cardHeadZ.Keys.Concat(_cardBlocked.Keys)
			where !_cards.ContainsKey(k)
			select k).Distinct().ToList())
		{
			_cardHeadZ.Remove(item3);
			_cardPrevRaw.Remove(item3);
			_cardVel.Remove(item3);
			_cardBlocked.Remove(item3);
		}
		void Cand(int h, bool known)
		{
			if (once.Add(h) && ENTITY.DOES_ENTITY_EXIST(h))
			{
				float num36 = (known ? knownRange : strangerRange) + 20f;
				if (!((ENTITY.GET_ENTITY_COORDS(h, alive: true, realCoords: true) - me).Length() > num36))
				{
					_cardCand.Add((h, known));
				}
			}
		}
	}
}
