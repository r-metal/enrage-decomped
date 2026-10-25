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
	private sealed class Smoke
	{
		public Ped B;

		public int Since;

		public int LitAt;
	}

	private const ulong HasAnimDictLoaded = 2882145328773677258uL;

	private const ulong PlayAnimAdvanced = 9497441865609983755uL;

	private const string PayDict = "script_common@handover@generic@player_handover@money";

	private const string PaidDict = "script_common@handover@generic@ped_handover@money";

	private const string ShakeDict = "script_mp@emotes@handshake@male@unarmed@full";

	private readonly List<Gesture> _gestures = new List<Gesture>();

	private static readonly (string dict, string clip, bool upper)[] ShakeVariants = new(string, string, bool)[6]
	{
		("script_mp@emotes@handshake@male@unarmed@upper", "action", true),
		("script_mp@emotes@handshake@male@unarmed@upper", "action_alt1", true),
		("script_mp@emotes@handshake@male@unarmed@upper", "action_alt2", true),
		("script_mp@emotes@handshake@male@unarmed@upper", "intro", true),
		("script_mp@emotes@handshake@male@unarmed@full", "fullbody", false),
		("script_mp@emotes@handshake@male@unarmed@full", "fullbody_alt1", false)
	};

	private static readonly uint MoneyProp = 0u;

	private readonly List<Gesture> _gestureRuns = new List<Gesture>();

	private const string MoneyModel = "p_foldedbillssmall01x";

	private const string HandBone = "PH_R_Hand";

	private const string HandBoneFallback = "SKEL_R_Hand";

	private readonly List<Smoke> _smokes = new List<Smoke>();

	private readonly Dictionary<int, (int n, int at)> _antagonized = new Dictionary<int, (int, int)>();

	private readonly List<(int at, Ped ped, string what)> _reactions = new List<(int, Ped, string)>();

	private static readonly string[] HotheadBack = new string[3] { "You wanna say that again? Go on.", "Watch your mouth, or I'll shut it for you.", "Keep talkin'. See what happens." };

	private static readonly string[] ProudBack = new string[3] { "Do you know who you're talkin' to?", "How dare you.", "I've buried better men than you for less." };

	private static readonly string[] ColdBack = new string[3] { "Not worth my time.", "Run along.", "...Hm." };

	private static readonly string[] CowardBack = new string[3] { "I don't want no trouble, mister!", "Alright, alright - I'm goin'!", "P-please, leave me be." };

	private static readonly string[] DrifterBack = new string[3] { "Whatever you say, friend.", "Ain't my problem.", "Have a nice day, then." };

	private static readonly string[] GreedyBack = new string[2] { "Talk's cheap. Come back when you're payin'.", "You're wastin' my time and my time costs money." };

	private static readonly string[] LoyalBack = new string[3] { "Careful now. I got friends.", "You don't want to do this, mister.", "Last warning." };

	private const float DeedSize = 5f;

	private const float GoodWordCap = 30f;

	private static bool CanGesture(Ped p)
	{
		if (p != null && p.Exists() && !p.IsDead && !p.IsOnMount && !p.IsInVehicle && !p.IsHogtied && !PED.IS_PED_RAGDOLL(p.Handle) && !ENTITY.IS_ENTITY_ATTACHED(p.Handle))
		{
			return !GameEventPed(p);
		}
		return false;
	}

	internal bool GesturePay(Ped him, bool youPay)
	{
		return StartGesture(him, youPay ? "script_common@handover@generic@player_handover@money" : "script_common@handover@generic@ped_handover@money", youPay ? "handover_money_plyr" : "ped_handover_money_plyr", youPay ? "handover_money_male" : "ped_handover_money_male", youPay ? "you pay" : "he pays you", shake: false, money: true, youPay);
	}

	internal bool GestureShake(Ped him)
	{
		(string, string, bool) tuple = ShakeVariants[Math.Max(0, Math.Min(ShakeVariants.Length - 1, Config.HandshakeVariant))];
		return StartGesture(him, tuple.Item1, tuple.Item2, tuple.Item2, "a handshake (" + tuple.Item2 + ")", shake: true, money: false, youGive: false, tuple.Item3);
	}

	internal bool GestureTipHat(Ped him)
	{
		return StartGesture(him, "script_common@handover@generic@ped_handover@upper_body", "tip_hat_rhand", null, "a tip of the hat", shake: true, money: false, youGive: false, upper: true);
	}

	private bool Busy(Ped him, Ped me)
	{
		if (him != null && him.Exists())
		{
			if (InterroFor(him.Handle) == null)
			{
				if (me != null && me.Exists())
				{
					return GrappledBy(him, me);
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private bool StartGesture(Ped him, string dict, string clipA, string clipB, string what, bool shake, bool money = false, bool youGive = false, bool upper = false)
	{
		if (!Config.GsGestures)
		{
			GrudgeLog("GESTURE " + what + ": skipped - Gestures is off");
			return false;
		}
		Ped character = Game.Player.Character;
		string text = ((!CanGesture(character)) ? "you can't (mounted / in a vehicle / ragdoll)" : ((!CanGesture(him)) ? "he can't (mounted / tied / ragdoll / attached)" : (character.IsInCombat ? "you're in a fight" : (PED.IS_PED_IN_COMBAT(him.Handle, 0) ? "he's in a fight" : (Busy(him, character) ? "he's being questioned / held" : ((character.Position.DistanceTo(him.Position) > 7f) ? $"too far ({character.Position.DistanceTo(him.Position):0.0} m)" : ((_gestures.Any((Gesture g) => g.B == him) || _gestureRuns.Any((Gesture g) => g.B == him)) ? "already doing one" : null)))))));
		if (text != null)
		{
			GrudgeLog($"GESTURE {what} with ped {him.Handle}: skipped - {text}");
			return false;
		}
		_gestures.Add(new Gesture
		{
			A = character,
			B = him,
			Dict = dict,
			ClipA = clipA,
			ClipB = clipB,
			What = what,
			Since = Game.GameTime,
			Shake = shake,
			Money = money,
			YouGive = youGive,
			Upper = upper
		});
		if (money)
		{
			STREAMING.REQUEST_MODEL(MISC.GET_HASH_KEY("p_foldedbillssmall01x"), p1: false);
		}
		return true;
	}

	private void UpdateGestures()
	{
		for (int num = _gestures.Count - 1; num >= 0; num--)
		{
			Gesture gesture = _gestures[num];
			if (!CanGesture(gesture.A) || !CanGesture(gesture.B) || Busy(gesture.B, gesture.A))
			{
				if (gesture.WalkSince != 0 && gesture.B != null && gesture.B.Exists())
				{
					gesture.B.BlockPermanentEvents = gesture.OrigBlock;
				}
				_gestures.RemoveAt(num);
			}
			else if (!gesture.Requested)
			{
				gesture.Requested = true;
				Crumbs.Mark("gesture exists? " + gesture.Dict);
				FlushGrudgeLog(force: true);
				if (!STREAMING.DOES_ANIM_DICT_EXIST(gesture.Dict))
				{
					_gestures.RemoveAt(num);
					GrudgeLog("GESTURE " + gesture.What + ": the game has no " + gesture.Dict);
				}
				else
				{
					Crumbs.Mark("gesture request " + gesture.Dict);
					FlushGrudgeLog(force: true);
					Crumbs.Native("Gestures anim request");
					STREAMING.REQUEST_ANIM_DICT(gesture.Dict);
					GrudgeLog("GESTURE " + gesture.What + ": " + gesture.Dict + " requested");
				}
			}
			else
			{
				Crumbs.Mark("gesture loaded? " + gesture.Dict);
				if (!STREAMING.HAS_ANIM_DICT_LOADED(gesture.Dict))
				{
					if (Game.GameTime - gesture.Since > 2500)
					{
						_gestures.RemoveAt(num);
						GrudgeLog("GESTURE " + gesture.What + ": " + gesture.Dict + " never loaded");
					}
				}
				else
				{
					float num2 = gesture.A.Position.DistanceTo(gesture.B.Position);
					if (gesture.WalkSince == 0)
					{
						gesture.WalkSince = Game.GameTime;
						gesture.StartGap = num2;
						gesture.OrigBlock = StaysBlocked(gesture.B);
						gesture.B.BlockPermanentEvents = true;
						if (num2 > 1.15f)
						{
							TASK.TASK_GO_TO_ENTITY(gesture.B.Handle, gesture.A.Handle, -1, 0.9f, 1.4f, 0f, 0);
						}
						else
						{
							TASK.TASK_TURN_PED_TO_FACE_ENTITY(gesture.B.Handle, gesture.A.Handle, 1500, 0f, 0f, 0f);
						}
						TASK.TASK_TURN_PED_TO_FACE_ENTITY(gesture.A.Handle, gesture.B.Handle, 1500, 0f, 0f, 0f);
						GrudgeLog($"GESTURE {gesture.What}: ped {gesture.B.Handle} walks over ({num2:0.0} m)");
					}
					else
					{
						bool flag = num2 <= 1.2f && Game.GameTime - gesture.WalkSince > 600;
						long num3 = Game.GameTime - gesture.WalkSince;
						if (!flag && !gesture.YouWalk && (num3 > 1500 || gesture.StartGap > 4f) && gesture.A == Game.Player.Character && !gesture.A.IsOnMount)
						{
							gesture.YouWalk = true;
							TASK.TASK_GO_TO_ENTITY(gesture.A.Handle, gesture.B.Handle, 3000, 1f, 1f, 0f, 0);
							GrudgeLog($"GESTURE {gesture.What}: ped {gesture.B.Handle} still {num2:0.0} m off - you step over to him");
						}
						if (flag || num3 >= 3000)
						{
							if (!flag && num2 > 1.4f)
							{
								_gestures.RemoveAt(num);
								OneSidedHandover(gesture, num2);
							}
							else
							{
								if (!flag)
								{
									GrudgeLog($"GESTURE {gesture.What}: ped {gesture.B.Handle} close enough ({num2:0.0} m after 3 s) - the hand-off");
								}
								_gestures.RemoveAt(num);
								Vector3 position = gesture.A.Position;
								float heading = gesture.A.Heading;
								position.Z = Ground(position).Z;
								heading = HeadingTo(position, gesture.B.Position);
								double num4 = (double)heading * Math.PI / 180.0;
								Vector3 vector = position + new Vector3((float)(0.0 - Math.Sin(num4)) * 0.9f, (float)Math.Cos(num4) * 0.9f, 0f);
								vector.Z = Ground(vector).Z;
								int flags = (gesture.Upper ? 48 : 0);
								Play(gesture.A, gesture.Dict, gesture.ClipA, position, heading, flags);
								if (gesture.ClipB != null)
								{
									Play(gesture.B, gesture.Dict, gesture.ClipB, vector, heading + 180f, flags);
								}
								else
								{
									ENTITY.SET_ENTITY_HEADING(gesture.B.Handle, heading + 180f);
									Function.Call(12905671096162301951uL, gesture.B.Handle, 0, 0, 3472337526u, false, true, false, false, false);
								}
								gesture.Started = Game.GameTime;
								float num5 = 0f;
								try
								{
									num5 = ENTITY.GET_ANIM_DURATION(gesture.Dict, gesture.ClipA);
								}
								catch
								{
								}
								gesture.DurMs = ((num5 > 0.3f) ? ((int)(num5 * 1000f)) : 3000);
								if (gesture.Money)
								{
									GiveProp(gesture);
								}
								_gestureRuns.Add(gesture);
								GrudgeLog(string.Format("GESTURE {0} with ped {1} ({2}, {3} ms{4})", gesture.What, gesture.B.Handle, gesture.Dict, gesture.DurMs, (gesture.Prop != 0) ? ", money in hand" : (gesture.Money ? ", no money prop (model not loaded)" : "")));
							}
						}
					}
				}
			}
		}
	}

	private void OneSidedHandover(Gesture g, float gap)
	{
		if (g.A != null && g.A.Exists())
		{
			TASK.CLEAR_PED_TASKS(g.A.Handle, p1: true, p2: false);
			TASK.TASK_TURN_PED_TO_FACE_ENTITY(g.A.Handle, g.B.Handle, 1000, 0f, 0f, 0f);
		}
		Crumbs.Native("Gestures one-sided");
		if (g.A != null && g.A.Exists() && g.ClipA != null)
		{
			TASK.TASK_PLAY_ANIM(g.A.Handle, g.Dict, g.ClipA, 4f, -4f, 2000, 48, 0f, p8: false, 0, p10: false, "", p12: false);
		}
		if (g.B != null && g.B.Exists())
		{
			TASK.TASK_TURN_PED_TO_FACE_ENTITY(g.B.Handle, g.A.Handle, 1500, 0f, 0f, 0f);
			Function.Call(12905671096162301951uL, g.B.Handle, 0, 0, 3472337526u, false, true, false, false, false);
			g.B.BlockPermanentEvents = g.OrigBlock;
		}
		GrudgeLog($"GESTURE {g.What}: ped {g.B?.Handle} still {gap:0.0} m off after 3 s - Arthur holds the money out, he nods (one-sided)");
	}

	private void GiveProp(Gesture g)
	{
		uint num = MISC.GET_HASH_KEY("p_foldedbillssmall01x");
		if (STREAMING.HAS_MODEL_LOADED(num))
		{
			Ped ped = (g.YouGive ? g.A : g.B);
			Crumbs.Mark("gesture prop");
			FlushGrudgeLog(force: true);
			Vector3 position = ped.Position;
			g.Prop = OBJECT.CREATE_OBJECT(num, position.X, position.Y, position.Z + 1f, isNetwork: false, bScriptHostObj: false, dynamic: false, p7: false, p8: true);
			STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED(num);
			if (g.Prop != 0)
			{
				AttachToHand(g.Prop, ped);
			}
		}
	}

	private static void AttachToHand(int prop, Ped p)
	{
		int num = ENTITY.GET_ENTITY_BONE_INDEX_BY_NAME(p.Handle, "PH_R_Hand");
		bool num2 = num >= 0;
		if (!num2)
		{
			num = ENTITY.GET_ENTITY_BONE_INDEX_BY_NAME(p.Handle, "SKEL_R_Hand");
		}
		Crumbs.Native("Gestures attach");
		if (num2)
		{
			ENTITY.ATTACH_ENTITY_TO_ENTITY(prop, p.Handle, num, 0f, 0f, 0f, 0f, 0f, 0f, p9: false, useSoftPinning: false, collision: false, isPed: false, 2, fixedRot: true, p15: false, p16: false);
		}
		else
		{
			ENTITY.ATTACH_ENTITY_TO_ENTITY(prop, p.Handle, num, 0.07f, 0.01f, -0.015f, 0f, 0f, 0f, p9: false, useSoftPinning: false, collision: false, isPed: false, 2, fixedRot: true, p15: false, p16: false);
		}
	}

	private unsafe void UpdateGestureRuns()
	{
		for (int num = _gestureRuns.Count - 1; num >= 0; num--)
		{
			Gesture gesture = _gestureRuns[num];
			int num2 = Game.GameTime - gesture.Started;
			bool flag = gesture.B == null || !gesture.B.Exists() || gesture.B.IsDead;
			if (!gesture.Checked && num2 > 500 && !flag)
			{
				gesture.Checked = true;
				bool flag2 = ENTITY.IS_ENTITY_PLAYING_ANIM(gesture.A.Handle, gesture.Dict, gesture.ClipA, 3);
				bool flag3 = gesture.ClipB != null && ENTITY.IS_ENTITY_PLAYING_ANIM(gesture.B.Handle, gesture.Dict, gesture.ClipB, 3);
				GrudgeLog("GESTURE " + gesture.What + ": playing - you " + (flag2 ? "yes" : "NO") + ", him " + (flag3 ? "yes" : "NO"));
			}
			if (gesture.Prop != 0 && !gesture.Swapped && (float)num2 > (float)gesture.DurMs * 0.5f && !flag)
			{
				gesture.Swapped = true;
				ENTITY.DETACH_ENTITY(gesture.Prop, p1: false, collision: false);
				AttachToHand(gesture.Prop, gesture.YouGive ? gesture.B : gesture.A);
			}
			if (num2 >= gesture.DurMs + 400 || flag)
			{
				_gestureRuns.RemoveAt(num);
				if (!flag && gesture.Upper)
				{
					TASK.CLEAR_PED_TASKS(gesture.B.Handle, p1: false, p2: false);
				}
				if (gesture.Prop != 0)
				{
					int prop = gesture.Prop;
					if (ENTITY.DOES_ENTITY_EXIST(prop))
					{
						ENTITY.DETACH_ENTITY(prop, p1: false, collision: false);
						OBJECT.DELETE_OBJECT(&prop);
					}
					gesture.Prop = 0;
				}
				if (!flag)
				{
					gesture.B.BlockPermanentEvents = gesture.OrigBlock;
					TASK.TASK_TURN_PED_TO_FACE_ENTITY(gesture.B.Handle, gesture.A.Handle, 1500, 0f, 0f, 0f);
				}
			}
		}
	}

	internal void ShareSmoke(Ped him)
	{
		Ped character = Game.Player.Character;
		if (!CanGesture(character) || !CanGesture(him) || character.IsInCombat || _smokes.Any((Smoke s) => s.B == him))
		{
			GrudgeLog($"SMOKE with ped {him?.Handle}: skipped");
			return;
		}
		him.BlockPermanentEvents = true;
		if (him.Position.DistanceTo(character.Position) > 1.6f)
		{
			TASK.TASK_GO_TO_ENTITY(him.Handle, character.Handle, -1, 1.2f, 1f, 0f, 0);
		}
		TASK.TASK_TURN_PED_TO_FACE_ENTITY(character.Handle, him.Handle, 1500, 0f, 0f, 0f);
		_smokes.Add(new Smoke
		{
			B = him,
			Since = Game.GameTime
		});
		GrudgeLog($"SMOKE with ped {him.Handle}: he comes over ({him.Position.DistanceTo(character.Position):0.0} m)");
	}

	private void UpdateSmokes(Ped player)
	{
		uint scenarioHash = MISC.GET_HASH_KEY("WORLD_HUMAN_SMOKE");
		for (int num = _smokes.Count - 1; num >= 0; num--)
		{
			Smoke smoke = _smokes[num];
			if (smoke.B == null || !smoke.B.Exists() || smoke.B.IsDead)
			{
				_smokes.RemoveAt(num);
			}
			else if (smoke.LitAt == 0)
			{
				float num2 = smoke.B.Position.DistanceTo(player.Position);
				if (!(num2 > 1.7f) || Game.GameTime - smoke.Since >= 6000)
				{
					smoke.LitAt = Game.GameTime;
					TASK.TASK_TURN_PED_TO_FACE_ENTITY(smoke.B.Handle, player.Handle, 1000, 0f, 0f, 0f);
					ENTITY.SET_ENTITY_HEADING(smoke.B.Handle, HeadingTo(smoke.B.Position, player.Position));
					ENTITY.SET_ENTITY_HEADING(player.Handle, HeadingTo(player.Position, smoke.B.Position));
					TASK.TASK_START_SCENARIO_IN_PLACE_HASH(smoke.B.Handle, scenarioHash, 20000, playEnterAnim: true, 0u, -1f, p6: false);
					if (CanGesture(player) && !player.IsInCombat)
					{
						TASK.TASK_START_SCENARIO_IN_PLACE_HASH(player.Handle, scenarioHash, 14000, playEnterAnim: true, 0u, -1f, p6: false);
					}
					GrudgeLog($"SMOKE with ped {smoke.B.Handle}: lit ({num2:0.0} m apart)");
				}
			}
			else if (Game.GameTime - smoke.LitAt >= 20000 || !(smoke.B.Position.DistanceTo(player.Position) < 25f))
			{
				_smokes.RemoveAt(num);
				smoke.B.BlockPermanentEvents = StaysBlocked(smoke.B);
				TASK.CLEAR_PED_TASKS(smoke.B.Handle, p1: false, p2: false);
			}
		}
	}

	internal unsafe void CleanupGestures()
	{
		foreach (Gesture item in _gestureRuns.Concat(_gestures))
		{
			if (item.Prop != 0)
			{
				int prop = item.Prop;
				if (ENTITY.DOES_ENTITY_EXIST(prop))
				{
					OBJECT.DELETE_OBJECT(&prop);
				}
			}
			if (item.B != null && item.B.Exists() && item.WalkSince != 0)
			{
				item.B.BlockPermanentEvents = item.OrigBlock;
			}
		}
		foreach (Smoke smoke in _smokes)
		{
			if (smoke.B != null && smoke.B.Exists())
			{
				smoke.B.BlockPermanentEvents = StaysBlocked(smoke.B);
			}
		}
		_gestureRuns.Clear();
		_gestures.Clear();
		_smokes.Clear();
	}

	private void Play(Ped p, string dict, string clip, Vector3 at, float heading, int flags = 0)
	{
		Crumbs.Mark($"gesture play {dict} / {clip} on ped {p.Handle}");
		FlushGrudgeLog(force: true);
		TASK.CLEAR_PED_TASKS(p.Handle, p1: true, p2: false);
		ENTITY.SET_ENTITY_HEADING(p.Handle, heading);
		if ((flags & 0x20) != 0 && p != Game.Player.Character)
		{
			TASK.TASK_STAND_STILL(p.Handle, -1);
		}
		Crumbs.Native("Gestures anim play");
		TASK.TASK_PLAY_ANIM(p.Handle, dict, clip, 4f, -4f, -1, flags, 0f, p8: false, 0, p10: false, "", p12: false);
	}

	private string RefusesHand(Personality? t, NemesisRecord traits, float grudge)
	{
		if (t == Personality.Hothead && grudge >= 30f)
		{
			return "Keep your hand. I ain't done with you.";
		}
		if (t == Personality.Proud && grudge >= 30f)
		{
			return "You think a handshake fixes it? It don't.";
		}
		if (t == Personality.Cold && grudge >= 50f)
		{
			return "...No.";
		}
		if (traits != null && traits.TraitsRolled && traits.Disposition == Disposition.Surly && grudge >= 15f && _rng.Next(100) < 40)
		{
			return "I don't shake hands with men like you.";
		}
		return null;
	}

	internal void ReactByTemper(int target, bool insult)
	{
		if (!(Entity.FromHandle(target) is Ped ped) || !ped.Exists() || ped.IsDead || !ped.IsHuman || RecordAt(target) != null || IsOurs(target) || IsStoryPed(ped) || GameOwned(ped) || GameEventPed(ped) || IsLawman(ped) || Kids.IsChild(ped) || ped.IsInVehicle || ped.IsOnMount)
		{
			return;
		}
		Ped character = Game.Player.Character;
		if (PED.IS_PED_IN_COMBAT(ped.Handle, character.Handle) || !Config.TownsfolkReactions)
		{
			return;
		}
		NemesisRecord nemesisRecord = ShadowOf(ped);
		Personality personality = nemesisRecord?.Personality ?? Personality.Cold;
		Named value;
		string text = (_named.TryGetValue(target, out value) ? value.Name : "The stranger");
		if (!insult)
		{
			string text2 = (PED.IS_PED_USING_ANY_SCENARIO(target) ? "busy" : (AtTableGame(target) ? "a card player" : null));
			if (text2 != null)
			{
				GrudgeLog($"REACT {text} ({personality}) greeted -> nothing of ours ({text2}: the game's own reply only)");
				return;
			}
			Acquaintance value2;
			float num = (_acqOf.TryGetValue(target, out value2) ? ((float)value2.Fear) : 0f);
			int num2;
			if (!(num >= 30f))
			{
				if (nemesisRecord == null || !nemesisRecord.TraitsRolled || nemesisRecord.Disposition != Disposition.Surly)
				{
					switch (personality)
					{
					default:
						num2 = ((_rng.Next(2) == 0) ? (-1457020913) : 901097731);
						break;
					case Personality.Cold:
						num2 = -822629770;
						break;
					case Personality.Hothead:
					case Personality.Proud:
						num2 = 1879954891;
						break;
					}
				}
				else
				{
					num2 = 0;
				}
			}
			else
			{
				num2 = -1312563584;
			}
			uint num3 = (uint)num2;
			string text3 = ((num3 == 0) ? "nothing (surly)" : ((!Config.GsBody) ? "nothing (body language off)" : GreetEmoteName(num3)));
			if (num3 != 0 && Config.GsBody)
			{
				_reactions.Add((Game.GameTime + 600, ped, "greet:" + num3));
			}
			GrudgeLog(string.Format("REACT {0} ({1}{2}) greeted -> {3}", text, personality, (num >= 30f) ? $", fear {num:0}" : "", text3));
		}
		else
		{
			int num4;
			if (_antagonized.TryGetValue(target, out (int, int) value3) && Game.GameTime - value3.Item2 < 90000)
			{
				(num4, _) = value3;
			}
			else
			{
				num4 = 0;
			}
			num4++;
			_antagonized[target] = (num4, Game.GameTime);
			string text4 = personality switch
			{
				Personality.Hothead => (num4 >= 2) ? "fight" : "back", 
				Personality.Proud => (num4 >= 3) ? "fight" : "stand", 
				Personality.Loyal => (num4 >= 3) ? "fight" : "warn", 
				Personality.Coward => "flee", 
				Personality.Drifter => "shrug", 
				Personality.Greedy => "leave_greedy", 
				_ => "leave_cold", 
			};
			ShowSubtitle(text + ": \"" + text4 switch
			{
				"leave_cold" => ColdBack[_rng.Next(ColdBack.Length)], 
				"leave_greedy" => GreedyBack[_rng.Next(GreedyBack.Length)], 
				"shrug" => DrifterBack[_rng.Next(DrifterBack.Length)], 
				"flee" => CowardBack[_rng.Next(CowardBack.Length)], 
				"warn" => LoyalBack[_rng.Next(LoyalBack.Length)], 
				"stand" => ProudBack[_rng.Next(ProudBack.Length)], 
				"back" => HotheadBack[_rng.Next(HotheadBack.Length)], 
				_ => (personality == Personality.Hothead) ? "That's it!" : "Alright. You asked for it.", 
			} + "\"");
			_reactions.Add((Game.GameTime + 400, ped, text4));
			GrudgeLog($"REACT {text} ({personality}) antagonized {num4}x -> {text4}");
		}
	}

	private static string GreetEmoteName(uint e)
	{
		return e switch
		{
			901097731u => "a wave", 
			2837946383u => "a hat tip", 
			3472337526u => "a nod", 
			1879954891u => "the tough greet", 
			2982403712u => "a scared flinch", 
			_ => $"emote 0x{e:X8}", 
		};
	}

	private void UpdateReactions(Ped player)
	{
		for (int num = _reactions.Count - 1; num >= 0; num--)
		{
			var (num2, ped, text) = _reactions[num];
			if (Game.GameTime < num2)
			{
				continue;
			}
			_reactions.RemoveAt(num);
			if (ped == null || !ped.Exists() || ped.IsDead || (PED.IS_PED_IN_COMBAT(ped.Handle, player.Handle) && text != "fight"))
			{
				continue;
			}
			Crumbs.Native("Gestures emote");
			if (text.StartsWith("greet:"))
			{
				uint num3 = uint.Parse(text.Substring(6));
				if (PED.IS_PED_USING_ANY_SCENARIO(ped.Handle) || AtTableGame(ped.Handle))
				{
					GrudgeLog("REACT " + WhoIs(ped.Handle) + ": answer (" + GreetEmoteName(num3) + ") skipped - busy by then");
				}
				else
				{
					Function.Call(12905671096162301951uL, ped.Handle, (num3 != 2982403712u && num3 != 3472337526u) ? 3 : 0, 0, num3, false, true, false, false, false);
				}
				continue;
			}
			if (text == null)
			{
				continue;
			}
			int length = text.Length;
			if (length <= 5)
			{
				if (length != 4)
				{
					if (length != 5)
					{
						continue;
					}
					char c = text[1];
					if (c == 'h')
					{
						if (text == "shrug")
						{
							TASK.CLEAR_PED_TASKS(ped.Handle, p1: true, p2: false);
							Function.Call(12905671096162301951uL, ped.Handle, 0, 0, 772373429u, false, true, false, false, false);
							_reactions.Add((Game.GameTime + 2500, ped, "walk"));
						}
						continue;
					}
					if (c == 'i')
					{
						if (text == "fight")
						{
							WEAPON.SET_CURRENT_PED_WEAPON(ped.Handle, 2725352035u, equipNow: true, 0, p4: false, p5: false);
							TASK.TASK_COMBAT_PED(ped.Handle, player.Handle, 0, 16);
							Say(ped, "CHALLENGE_THREATEN", player);
						}
						continue;
					}
					if (c != 't' || !(text == "stand"))
					{
						continue;
					}
				}
				else
				{
					char c = text[2];
					if ((uint)c <= 101u)
					{
						switch (c)
						{
						case 'c':
							if (text == "back")
							{
								TASK.CLEAR_PED_TASKS(ped.Handle, p1: true, p2: false);
								TASK.TASK_TURN_PED_TO_FACE_ENTITY(ped.Handle, player.Handle, 2500, 0f, 0f, 0f);
								Function.Call(12905671096162301951uL, ped.Handle, 2, 0, 1533402397u, false, true, false, false, false);
							}
							break;
						case 'e':
							if (text == "flee")
							{
								TASK.CLEAR_PED_TASKS(ped.Handle, p1: true, p2: false);
								Flee(ped.Handle, player.Handle, 30f, -1, (_rng.Next(2) == 0) ? 3f : 2f);
							}
							break;
						}
						continue;
					}
					if (c == 'l')
					{
						if (!(text == "walk"))
						{
							continue;
						}
						goto IL_057f;
					}
					if (c != 'r' || !(text == "warn"))
					{
						continue;
					}
				}
				TASK.CLEAR_PED_TASKS(ped.Handle, p1: true, p2: false);
				TASK.TASK_TURN_PED_TO_FACE_ENTITY(ped.Handle, player.Handle, 4000, 0f, 0f, 0f);
				TASK.TASK_LOOK_AT_ENTITY(ped.Handle, player.Handle, 6000, 0, 51, 0);
				Function.Call(12905671096162301951uL, ped.Handle, 0, 0, (text == "stand") ? 1802342943u : 4282765570u, false, true, false, false, false);
				continue;
			}
			if (length != 10)
			{
				if (length != 12 || !(text == "leave_greedy"))
				{
					continue;
				}
			}
			else if (!(text == "leave_cold"))
			{
				continue;
			}
			goto IL_057f;
			IL_057f:
			TASK.CLEAR_PED_TASKS(ped.Handle, p1: true, p2: false);
			Flee(ped.Handle, player.Handle, 20f, -1, 1f);
		}
	}

	internal void UpdateGestureSystems(Ped player)
	{
		UpdateGestures();
		UpdateGestureRuns();
		UpdateSmokes(player);
		UpdateReactions(player);
		foreach (int item in (from kv in _antagonized
			where Game.GameTime - kv.Value.at > 120000
			select kv.Key).ToList())
		{
			_antagonized.Remove(item);
		}
	}
}
