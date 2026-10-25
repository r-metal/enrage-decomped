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
	private Dictionary<string, Agenda> Agendas => Chronicle.Agendas ?? (Chronicle.Agendas = new Dictionary<string, Agenda>());

	internal Agenda AgendaOf(Gang g)
	{
		if (g == null || g.StoryOnly || IsDestroyed(g))
		{
			return null;
		}
		if (!LeaderTemper(g).HasValue)
		{
			return null;
		}
		if (!Agendas.TryGetValue(g.Id, out var value))
		{
			return null;
		}
		return value;
	}

	internal string AgendaText(Gang g, Agenda a = null)
	{
		a = a ?? AgendaOf(g);
		if (a == null)
		{
			return null;
		}
		return a.Kind switch
		{
			"expand" => "take " + (Districts.FirstOrDefault((District d) => d.Key == a.Target)?.Name ?? "more ground"), 
			"revenge" => "break the " + (Gangs.ById(a.Target)?.Name ?? "enemy"), 
			"rich" => "get rich", 
			"huntyou" => "see you dead", 
			_ => "hold what they have", 
		};
	}

	internal string LeaderKey(Gang g)
	{
		LeaderSeat leaderSeat = SeatOf(g);
		if (leaderSeat == null || leaderSeat.Fallen == null)
		{
			return "lore:" + g.Id;
		}
		if (!string.IsNullOrEmpty(leaderSeat.Successor))
		{
			return leaderSeat.Successor;
		}
		return null;
	}

	private bool RevengeFits(Gang g, Gang e)
	{
		if (e != null && e != g && !e.StoryOnly && !IsDestroyed(e) && g.TurfWars && e.TurfWars)
		{
			return Districts.Any((District d) => HoldOf(d, g.Id) >= 5 && HoldOf(d, e.Id) >= 5);
		}
		return false;
	}

	private Agenda PickAgenda(Gang g, Agenda old = null)
	{
		Personality? personality = LeaderTemper(g);
		string text = g.LeaderName ?? "their leader";
		Agenda agenda = new Agenda
		{
			By = text,
			ById = LeaderKey(g),
			SetAt = GameMinutesNow(),
			ReviewAt = GameMinutesNow() + (long)(6 + _rng.Next(4)) * 1440L
		};
		if (SeatOf(g)?.Fallen == null && LeaderLore.ContainsKey(g.Id))
		{
			switch (g.Id)
			{
			case "odriscoll":
				agenda.Kind = "huntyou";
				return agenda;
			case "bronte":
				agenda.Kind = "rich";
				return agenda;
			case "army":
				agenda.Kind = "hold";
				return agenda;
			case "wapiti":
				agenda.Kind = "hold";
				return agenda;
			}
		}
		int num = NotorietyOf(g);
		switch (personality)
		{
		case Personality.Hothead:
			if (num >= 25 && _rng.Next(2) == 0)
			{
				agenda.Kind = "huntyou";
				break;
			}
			agenda.Target = WorstEnemy();
			agenda.Kind = ((agenda.Target != null) ? "revenge" : ((num >= 10) ? "huntyou" : "hold"));
			break;
		case Personality.Proud:
			agenda.Target = ExpandTarget();
			agenda.Kind = ((agenda.Target != null) ? "expand" : null);
			if (agenda.Kind == null)
			{
				agenda.Target = WorstEnemy();
				agenda.Kind = ((agenda.Target != null) ? "revenge" : "hold");
			}
			break;
		case Personality.Greedy:
		case Personality.Drifter:
			agenda.Kind = "rich";
			break;
		case Personality.Cold:
			agenda.Target = ExpandTarget();
			agenda.Kind = ((agenda.Target != null) ? "expand" : "hold");
			break;
		case Personality.Loyal:
			agenda.Target = WorstEnemy();
			agenda.Kind = ((agenda.Target != null && Grievances.Any((KeyValuePair<string, int> kv) => kv.Key.Contains(g.Id) && kv.Value >= 20)) ? "revenge" : "hold");
			if (agenda.Kind == "hold")
			{
				agenda.Target = null;
			}
			break;
		default:
			agenda.Kind = "hold";
			break;
		}
		if (old != null && agenda.Kind == old.Kind && agenda.Target == old.Target)
		{
			if (agenda.Kind != "rich")
			{
				agenda.Kind = "rich";
				agenda.Target = null;
			}
			else
			{
				agenda.Kind = "hold";
				agenda.Target = null;
			}
		}
		return agenda;
		string ExpandTarget()
		{
			return (from x in Districts
				select (d: x, h: HoldOf(x, g.Id), top: (from kv in ControlMap[x.Key]
					where kv.Key != g.Id
					select kv.Value).DefaultIfEmpty(0).Max()) into x
				where x.h >= 6 && x.top >= x.h
				orderby x.h descending
				select x).FirstOrDefault().d?.Key;
		}
		string WorstEnemy()
		{
			Gang gang = (from o in Gangs.All
				where RevengeFits(g, o)
				orderby Relation(g.Id, o.Id) - GrievanceOf(g.Id, o.Id)
				select o).FirstOrDefault();
			if (gang == null || Relation(g.Id, gang.Id) > -25)
			{
				return null;
			}
			return gang.Id;
		}
	}

	private bool AgendaMet(Gang g, Agenda a)
	{
		string kind = a.Kind;
		if (!(kind == "expand"))
		{
			if (kind == "revenge")
			{
				Gang gang = Gangs.ById(a.Target);
				if (gang == null || IsDestroyed(gang) || Relation(g.Id, gang.Id) > -10 || !RevengeFits(g, gang) || a.Wins >= 2)
				{
					return true;
				}
				LeaderSeat leaderSeat = SeatOf(gang);
				if (leaderSeat != null && leaderSeat.Fallen != null)
				{
					return leaderSeat.At > a.SetAt;
				}
				return false;
			}
			return false;
		}
		District district = Districts.FirstOrDefault((District x) => x.Key == a.Target);
		if (district == null)
		{
			return true;
		}
		int num = HoldOf(district, g.Id);
		int num2 = (from kv in ControlMap[district.Key]
			where kv.Key != g.Id
			select kv.Value).DefaultIfEmpty(0).Max();
		return num > num2;
	}

	private void DailyAgendas()
	{
		foreach (Gang item in Gangs.All.Where((Gang x) => !x.StoryOnly && !IsDestroyed(x)))
		{
			if (!LeaderTemper(item).HasValue)
			{
				continue;
			}
			string text = item.LeaderName ?? "their leader";
			string text2 = LeaderKey(item);
			Agendas.TryGetValue(item.Id, out var value);
			if (value != null && value.ById == null)
			{
				value.ById = text2;
				value.By = text;
			}
			if (value == null)
			{
				Agenda agenda = PickAgenda(item);
				Agendas[item.Id] = agenda;
				GrudgeLog("AGENDA " + item.Name + ": " + text + " means to " + AgendaText(item, agenda));
				SaveWorld();
				continue;
			}
			if (value.ById != text2)
			{
				Personality? personality = LeaderTemper(item);
				bool flag = personality == Personality.Loyal || (personality != Personality.Proud && personality != Personality.Hothead && _rng.Next(2) == 0);
				string text3 = AgendaText(item, value);
				string text4 = value.By;
				if (flag && value.Kind == "revenge" && !RevengeFits(item, Gangs.ById(value.Target)))
				{
					flag = false;
				}
				Agenda agenda2 = (flag ? new Agenda
				{
					Kind = value.Kind,
					Target = value.Target,
					By = text,
					ById = text2,
					SetAt = GameMinutesNow(),
					ReviewAt = GameMinutesNow() + (long)(6 + _rng.Next(4)) * 1440L
				} : PickAgenda(item, value));
				Agendas[item.Id] = agenda2;
				AgendaEvent(item, flag ? (text + " takes up " + text4 + "'s fight") : (text + " turns the " + item.Name + " from " + text4 + "'s aim"), flag ? ("He means to " + text3 + ", as " + text4 + " did.") : (text4 + " meant to " + text3 + ". " + text + " means to " + AgendaText(item, agenda2) + "."));
				continue;
			}
			long num = GameMinutesNow();
			bool flag2 = AgendaMet(item, value);
			if (flag2 || num >= value.ReviewAt || value.ReviewAt - num > 43200)
			{
				string text5 = AgendaText(item, value);
				Agenda agenda3 = PickAgenda(item, flag2 ? value : null);
				Agendas[item.Id] = agenda3;
				if (agenda3.Kind != value.Kind || agenda3.Target != value.Target)
				{
					AgendaEvent(item, flag2 ? ("The " + item.Name + " have what they wanted") : (text + " has new plans for the " + item.Name), (flag2 ? (text + " meant to " + text5 + " - done. ") : ("No more trying to " + text5 + ". ")) + "Now he means to " + AgendaText(item, agenda3) + ".");
				}
				else
				{
					SaveWorld();
				}
			}
		}
	}

	private void AgendaEvent(Gang g, string title, string line)
	{
		WorldEvent worldEvent = NewEvent("TURMOIL", GameMinutesNow());
		worldEvent.Gangs.Add(g.Id);
		worldEvent.Title = title;
		worldEvent.Lines.Add(line);
		worldEvent.Heard = true;
		Chronicle.Events.Add(worldEvent);
		GrudgeLog("AGENDA " + title + " - " + line);
		SaveWorld();
	}

	internal double AgendaWarMult(Gang a, Gang b, District d)
	{
		Agenda agenda = AgendaOf(a);
		if (agenda == null)
		{
			return 1.0;
		}
		if (agenda.Kind == "revenge" && agenda.Target == b.Id)
		{
			return 2.0;
		}
		if (agenda.Kind == "expand" && agenda.Target == d.Key)
		{
			return 1.8;
		}
		if (agenda.Kind == "hold")
		{
			return 0.5;
		}
		if (agenda.Kind == "rich")
		{
			return 0.7;
		}
		return 0.85;
	}

	internal double AgendaPairMult(Gang a, Gang b, District d)
	{
		return Math.Max(0.3, Math.Min(2.5, AgendaWarMult(a, b, d) * AgendaWarMult(b, a, d)));
	}

	internal int AgendaLean(Gang a, Gang b, District d)
	{
		return Math.Sign(AgendaWarMult(a, b, d) - AgendaWarMult(b, a, d));
	}

	internal void AgendaClashWon(Gang winner, Gang loser)
	{
		Agenda agenda = AgendaOf(winner);
		if (agenda != null && agenda.Kind == "revenge" && loser != null && agenda.Target == loser.Id)
		{
			agenda.Wins++;
		}
	}

	internal double AgendaAmbushMult(Gang g)
	{
		if (!(AgendaOf(g)?.Kind == "huntyou"))
		{
			return 1.0;
		}
		return 1.4;
	}

	internal bool GrievanceHeld(string a, string b)
	{
		Agenda agenda = AgendaOf(Gangs.ById(a));
		if (agenda == null || !(agenda.Kind == "revenge") || !(agenda.Target == b))
		{
			Agenda agenda2 = AgendaOf(Gangs.ById(b));
			if (agenda2 != null && agenda2.Kind == "revenge")
			{
				return agenda2.Target == a;
			}
			return false;
		}
		return true;
	}

	internal string AgendaLine()
	{
		return "aims: " + string.Join(", ", from g in Gangs.All
			where !g.StoryOnly && !IsDestroyed(g)
			select g.Id + " " + (AgendaText(g) ?? "none (seat open)"));
	}
}
