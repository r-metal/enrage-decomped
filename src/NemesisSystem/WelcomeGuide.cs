using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace NemesisSystem;

internal sealed class WelcomeGuide
{
	private sealed class Page
	{
		public string Title;

		public string Kicker;

		public (string head, string body)[] Left;

		public (string head, string body)[] Right;
	}

	internal sealed class SetupRow
	{
		public string Section;

		public string Label;

		public string Help;

		public Func<string> Value;

		public Action<int> Change;

		public Action Run;

		public bool IsKey;

		public bool IsStart;

		public Func<int, string> Confirm;

		public Func<string> Locked;
	}

	private int _page;

	private readonly Action _onClose;

	private bool _readSomething;

	private (string title, string sub, string[] body, string path, string[] steps)? _warning;

	private bool _whatsNew;

	private bool _thenWhatsNew;

	private bool _guideAfterNews;

	private static readonly Page WhatsNewPage = new Page
	{
		Title = "What's new in 0.9.5.3-decomped.1",
		Kicker = "Unofficial: men who ride with you",
		Left = new(string, string)[5]
		{
			("Ride With Me", "At camp, stand by one of the gang and hold Ride With Me. Charles, Sadie, John, Lenny and most riders say yes; the rest never ride."),
			("Three at most", "Up to three ride with you, and the men you recruit count. On foot, hold Send Home by one of them and he heads home."),
			("On the road", "They mount and get down when you do, and catch up if left behind. They take on the man you shoot at and join any fight near you."),
			("Hurt", "Below 30% health a man pulls out and is laid up for a game day. Ask or send for him before then and he tells you he's still healing."),
			("Home again", "Ride back to camp and the gang goes home. Come across one of them out in the world - he has work for you - and they all head back.")
		},
		Right = new(string, string)[5]
		{
			("Recruits", "Spare a man - he gives up, you let him go after questioning, or his nerve breaks - and he may offer to ride with you for good."),
			("His offer", "He stops and waits a minute - get close and hold Ride With Me. Cowards and drifters offer most, proud men hardly ever, leaders never."),
			("His old gang", "He leaves their ranks and they hear he turned. If three already ride with you, he goes his way and waits till you send for him."),
			("Your Men", "F11, Hierarchy, then Q / E (pad: LB / RB) past Unaffiliated. Send For Him, Send Home, or Let Him Go - press it twice; it's for good."),
			("Settings", "[Companions] and [Recruits] Enabled switch them off. MaxKept (6) caps the men you keep, ChancePercent (100) scales their odds.")
		}
	};

	public Func<List<SetupRow>> SetupRows;

	public Action<Keys> SetMenuKey;

	public Action OnSetupDone;

	public Action<string> Log;

	private List<SetupRow> _rows = new List<SetupRow>();

	private int _row;

	private bool _setup;

	private bool _thenGuide;

	private bool _capturing;

	private string _setupNote = "";

	private SetupRow _askRow;

	private int _askDir;

	private string _askText;

	private bool _askYes;

	private static readonly Page[] Pages = new Page[12]
	{
		new Page
		{
			Title = "Welcome to enRAGE",
			Kicker = "A living world that remembers what you did - and who you did it to",
			Left = new(string, string)[2]
			{
				("What this is", "Everyone you cross in the West can become your NEMESIS. Rob a man, beat him, insult him, shoot his horse - he may not forget. He gets a name, a temper, a past and a grudge, and one day he comes looking for you."),
				("A world that moves without you", "Eight outfits - the O'Driscolls, the Lemoyne Raiders, the U.S. Army, the Wapiti, the Bronte Family, the Klan, the Murfree Brood and the Skinner Brothers - have their own ranks, camps, leaders and wars. They fight, feud, promote their own and die while you ride. In the epilogue it's eight years later: the O'Driscolls, the Bronte Family and the Wapiti are gone, the Laramie Gang, the Del Lobos and the Night Folk ride, and the World tab tells what became of everyone.")
			},
			Right = new(string, string)[2]
			{
				("This guide", "A few pages on every system and how to use it. Read it now or later - it's always in the menu.\n- Next page: Right / E / Enter (pad: RB / A)\n- Back a page: Left / Q (pad: LB)\n- Close: Backspace (pad: B)\n- Open it again: F11, then G (pad: Y)"),
				("Story mode only", "enRAGE never runs in Online. During story missions it stays out of the way: nothing spawns, nothing interrupts.")
			}
		},
		new Page
		{
			Title = "The Menu",
			Kicker = "F11 on the keyboard, or double-tap Right on the D-pad",
			Left = new(string, string)[2]
			{
				("Three tabs", "Tab (pad: View) switches between them.\n- HIERARCHY: every outfit's ranks, from leader to foot soldier, with the fallen at the bottom. Loners are on the Unaffiliated list.\n- WORLD: everything that has happened - fights, feuds, deaths, wars, your own deeds.\n- X-RAY LOG: every man you've shot or beaten, wound by wound and blow by blow.\nSEARCH: hold Tab (pad: hold View) on any tab and type a name - nemeses, people, leaders, the gang, wars, world events, X-rays. Pick one to jump straight to it."),
				("Getting around", "- Q / E (pad: LB / RB): next outfit, or filter\n- Arrows / D-pad: move, Enter / A: open a man's page\n- On his page, R (pad: Y): his history, his mind, his crimes\n- A or Home (pad: X): your own page (Arthur, or John in the epilogue), from anywhere\n- Backspace / B: back, F11: close\n- The mouse works too: hover, click, right-click to go back")
			},
			Right = new(string, string)[2]
			{
				("Quick settings", "- N (pad: LS): Messages - what pops up on screen\n- C (pad: RS): Name Cards over people's heads\n- O (pad: Start): Settings - the common ones on one card\n- Alt+F11: All settings, every one, changed live"),
				("A man's page", "Rank and standing, his hate and fear of you, his temper and traits, his kin and friends, the scars you gave him, and a history of everything between you - plus a life story that reaches back years before he met you. His life made him: a man who ran from his first fight scares easier, one who took to the bottle drinks, one raised hard is quicker to anger.")
			}
		},
		new Page
		{
			Title = "Making Enemies",
			Kicker = "Anything you do to a stranger counts - and so does anything kind",
			Left = new(string, string)[2]
			{
				("The grudge", "Every stranger you deal with keeps a quiet score. Insults, stick-ups, punches, robbery, hogtying, stealing his horse, shooting at him, killing his friends - each adds to it. Greeting, defusing and kindness bring it down - but not right after you've hurt him.\nPast the line, he becomes your nemesis on the spot. Below it, he decides when he walks away - the higher the score, the likelier he is to hold on."),
				("Wear a mask", "A bandana pulled up, or a mask (a hood or a skull mask too): if he never saw your face, he can't hold a grudge against you. Masked kills count for only a quarter of an outfit's notoriety, and a masked man scares people faster. But take the mask off in front of someone who saw what you did - or question a man barefaced - and he knows it was you. The law and any witnesses still saw a masked man do it.")
			},
			Right = new(string, string)[2]
			{
				("Hate", "- Resentful: he'll come at you with his fists\n- Vengeful: a knife, maybe a gun\n- Murderous: he wants you dead\nEach man has his own breaking points - a hothead reaches for a gun sooner, a drifter later. Hate fades with time for some, and festers for others."),
				("Cheating death", "Shoot a nemesis and he may survive - scarred, and angrier. A shot to the brain or heart, or finishing him while he's down, makes sure. Kill a stranger and he's gone for good.")
			}
		},
		new Page
		{
			Title = "Who They Are",
			Kicker = "Every nemesis is a person, not a stat",
			Left = new(string, string)[2]
			{
				("Temper", "- Hothead: quick to fight, slow to forgive\n- Proud: never backs down, hates being shamed\n- Cold: patient, hard to scare, hard to read\n- Coward: runs, surrenders, hires others to do it\n- Greedy: money moves him, both ways\n- Loyal: dies for his outfit, never talks\n- Drifter: might just let it go"),
				("Fear", "Scars, crippling wounds, watching you kill their friends and your bloody reputation all frighten men. Frightened men hunt you less, break in fights, surrender and talk.")
			},
			Right = new(string, string)[2]
			{
				("Minds and memories", "Men remember what you did - the shame of a beating, the grief of a dead brother. Some want revenge, some want to rise in their outfit, some just want out. Their needs, stress and goals change how they act."),
				("Family and friends", "Men have brothers, cousins, sons and friends - in their own outfit and beyond. Kill one and the others hear of it within hours or days - then they grieve, or come for blood.")
			}
		},
		new Page
		{
			Title = "Outfits and Ranks",
			Kicker = "Every gang is a hierarchy - and you can take it apart",
			Left = new(string, string)[2]
			{
				("Ranks", "Each outfit has a leader and three tiers below him. Every few game days each outfit has its own SHAKE-UP: empty seats are filled, the ambitious rise and survivors who earned it are promoted. New men want paying - an outfit short of money leaves seats empty. An outfit that's lost its officers shakes up sooner, and fills most of the empty seats - not always all - with the men who've earned it. Kill a captain and someone takes his place - maybe someone worse."),
				("Leaders", "The game's own leaders - Colm O'Driscoll, Lindsey Wofford, Colonel Favours, Angelo Bronte, Rains Fall - sit at the top. Select a leader's card for his page: his life, what he's after, his money, men and wars. Kill or capture one and his seat opens - within one to four days his outfit picks a new leader. Turning Wofford in for his bounty counts too.")
			},
			Right = new(string, string)[2]
			{
				("Notoriety", "Kill an outfit's men and they learn your name: Unknown, Noticed, Wanted, Hunted, Blood Feud. The higher it climbs, the more often they come, the more men they bring and the better they fight. It fades with time - faster if you let their men live, kill their enemies, or send their leader a gift or an apology by wire."),
				("Some need a reason", "The Army, the Bronte Family and the Wapiti won't come after you unprovoked. Kill their men and they will - and a big enough bounty on your head turns the Army against you too. Out on the roads you'll mostly meet the lower ranks; officers come rarely.")
			}
		},
		new Page
		{
			Title = "Encounters",
			Kicker = "Trouble builds quietly - then it finds you",
			Left = new(string, string)[2]
			{
				("In the wild", "Tension rises as you ride - faster at night and the more notorious you are - and when it peaks, someone turns up: a man and his crew camped by the road, riders on your tail, a wagon ahead, a sniper on a ridge. After a quiet day or two out of the wild, the first one finds you sooner. A big fight with an encounter or a camp takes the tension right down; a scrap with anyone else only halves it. Nothing spawns while you're on a mission (a stranger's too), indoors or resting."),
				("Standoffs", "Many encounters start with words. Greet him and he may back down, depending on his temper and his fear of you. Antagonize him, draw on him or shoot, and it starts - though a man without the nerve for it may back down at the words and walk off.")
			},
			Right = new(string, string)[2]
			{
				("In towns", "People you've met turn up again. Men with grudges drink at the bar, sit on porches and walk the streets - most hold their grudge and won't start anything unless you push, but now and then one walks over to have it out. At most one man comes for you each visit, and the others keep out of a fight that isn't theirs unless you hit or draw on them. But if one of his own outfit, a friend or kin sees you fighting him, a loyal or brave man comes to back him up. Ordinary townsfolk mostly keep out of it - now and then one tough one joins in. Officers keep habits - a saloon on certain nights, a road at dusk: find one at it twice, or make his men talk, and you'll know where to wait for him. Hurt him there and he stops going. If someone goes for you with fists or a knife, a lawman who sees it marches him off to the cells - if he pulls a gun, they shoot. Swing first where a lawman can see, or keep hitting a man who's beaten, and the law comes for you."),
				("The world around you", "Out in the country, patrols answer gunfire - if it's over when they get there, they look the scene over and ride on. Towns have their own law. Rival outfits clash in skirmishes you can join or watch - shoot at one side, or fire close to them, and that side turns its guns on you. In a saloon, soldiers may be drinking when Raiders come through the door.")
			}
		},
		new Page
		{
			Title = "Interrogation",
			Kicker = "Fear makes men talk",
			Left = new(string, string)[2]
			{
				("Getting him to talk", "A gang man at your mercy - hogtied, in your grip, surrendered, wounded on the ground, or at gunpoint - can be questioned. Look for INTERROGATE on D-pad Up in his prompts (I on the keyboard)."),
				("Scaring him", "Fear is everything. Hit him, cut him, shoot near him, keep your gun on him, threaten him, or let him watch his friends die. Each temper breaks differently - a coward folds, a loyal man may never talk. B (Backspace) closes the questions so you can hit him; D-pad Up asks again.")
			},
			Right = new(string, string)[2]
			{
				("What to ask", "Questions are grouped What / When / Where / Why: his outfit's next job, when you can catch his boss, what's coming in the outfit, where his boss is holed up, where the money and supplies are kept, his family, why he rides.\n- The first question is the hardest - unless he's truly terrified\n- Scared out of his wits, he'll most likely talk\n- Each question shows how likely he is to answer - the more scared he is, the more he gives (alone beats a habit beats a ride)\n- Each man gives up only one or two big secrets - ask for more and he tells you why not\n- He may lie - and you'll find out"),
				("What you get", "Camps and hideouts marked on your map, plans you can ambush, an officer's habits, a burying to watch, kin and friends, his own past. Then let him go - a scared man may leave his outfit, and take kin or a friend with him - or finish it.")
			}
		},
		new Page
		{
			Title = "Camps and Hideouts",
			Kicker = "Find them, sneak up, clear them out",
			Left = new(string, string)[4]
			{
				("Finding them", "Officers keep camps and hideouts - the game's gang camps, Beaver Hollow, the Bronte mansion and the Saint Denis docks, the Wapiti reservation. Ride close to a camp and you find it; a hideout only goes on your map when someone gives it up or you fight there."),
				("Getting close", "Camps start wary, not hostile. Crouch, come at night or in fog, and keep out of sight - suspicion builds while they can see you. A gunshot or a body they find gives you away."),
				("Treasuries", "Every outfit but the Army keeps its money at one camp - more guards, and strongboxes holding a big share of its purse. Few of their men know where it is - the leader's top men most likely - but you can ask, or someone who likes you may wire a tip-off. Hold to break a box open - the camp hears it. They move it after a robbery. An outfit down to its last few camps stashes its money away from them."),
				("Supplies", "Every outfit but the Army keeps its supplies at one camp too - crates, big stores, a wagon, horses. Shoot their crates, blow up the stores, wreck or take the wagon, run off the horses, and the outfit runs short. More of their men know where the supplies are. Loaded supply wagons with riders run the roads - one you spot shows on your map - and towns they hold may have their crates on the street.")
			},
			Right = new(string, string)[3]
			{
				("Clearing them", "Kill or drive off every man and the camp stays empty for a few days. The outfit loses ground. Rival outfits may move in, and the Army raids outlaw camps near its own ground. Outlaws in their camps, crews and patrols are fair game - no bounty for killing them; soldiers and the Wapiti are another matter."),
				("Reading the map", "A camp's label names its outfit and officer. (?) - you were told where it is but haven't seen it. A chest marks a supply camp, a house a treasury; (raided) or (robbed) - you hit it; (restocking) - they moved their stores. A white ? is a battlefield; a pulsing marker is a fight going on (gunfire far off has none - follow the sound)."),
				("Fort Wallace", "The Army's headquarters. Only an officer knows the commander's habits, and even he may not give them up. Kill the commander there and the fort stands empty until the Army picks a new one.")
			}
		},
		new Page
		{
			Title = "The Living World",
			Kicker = "The WORLD tab keeps every story the papers missed",
			Left = new(string, string)[3]
			{
				("Every hour", "Outfits clash over camps and ground, men feud over cards and women, marry, fall sick, get drunk, go to jail, die in duels. All of it lands in the WORLD tab with the people involved and what it did to them."),
				("Territory and wars", "Each district is held by outfits. Win fights and the ground shifts. Old grudges, flashpoints and each leader's aim (shown on his hierarchy page) start wars. A war runs until one side gets what it wants, runs out of men, makes peace or cools off - it can last weeks, with truces in between. Kill a side's officers and its war falters; help one side and they'll remember it."),
				("Strength", "Men, money, supplies and ground make an outfit's strength, shown on the hierarchy - with the districts they hold under it. Jobs and big scores fill the purse; robbing their men and clearing their camps empty it. Every death takes a fighting man off - an outfit with few men left fields smaller camps, crews and patrols - and money hires them back; when it's short, local men may throw in for a share. Supplies come from its ground and run down with every man and every fight: an outfit running short arms its men with revolvers, and one low on supplies can't start a war or hire hands.")
			},
			Right = new(string, string)[3]
			{
				("Wars up close", "War districts are marked on the map and see far more fighting - the hotter the war, the more often. You may hear a battle in the distance and ride over to find what's left, and at night you'll hear blasts. The side that holds a town puts roadblocks on the roads in and men on its streets - they let you by unless they're after you, and in a hot war they may turn you back. Their enemies stay out, and when a war burns hot they hold roads out in the country too. Hold D-pad Down to see who's at war; the WORLD tab's Wars page has every war in full."),
				("Battlefields", "After a big fight, a ? marks the spot for a while. Ride out to find the dead - and sometimes survivors, still there. The outfits bury their own: a day after an officer dies, his kin and his men gather at the graveside for a few hours (a man of no outfit is buried by whoever will mourn him). The newspaper prints when and where. Watch, pay your respects on foot with empty hands, or shoot it up - they'll remember which."),
				("Your save is its world", "Every game save keeps its own world. Switch between your saves and the nemeses, the world and its history switch with them. A save enRAGE has never seen gets a world of its own. Shift+F11 undoes a switch for two minutes.")
			}
		},
		new Page
		{
			Title = "People and Reputation",
			Kicker = "Everyone has a name - and a memory",
			Left = new(string, string)[3]
			{
				("Names", "Strangers are strangers until you greet them. Greet a group and you meet them all. The people you meet are remembered, and turn up again. A name card shows \"Stranger\" until you greet him, then his temper; anyone with a grudge or hate shows his temper and how angry he is, and an outfit's men show their rank above the name. Children have names too: greet one and you learn a little about them. They never hold a grudge."),
				("Witnesses", "Commit a crime in front of people and they remember - some fear you, some tell the law, some can be intimidated into keeping quiet. Word spreads through a town. Fighting off someone who came at you - with a fist or a gun - scares them far less."),
				("Reputation", "Every town you've dealt with likes or hates you, shown on the map. Good deeds raise it where they happen - helping or rescuing strangers on the road, seeing someone safely on their way, charity, mercy, driving outlaws out of town - and the good word fades over a week or so. People are warier where you're infamous - though good deeds soften what newcomers hear, people who like you are slower to flee, and fear fades faster in a town that likes you. Men who turned nemesis in a town weigh on its opinion of you, and grudges people let go fade with time. If someone swings first, the law takes your side.")
			},
			Right = new(string, string)[2]
			{
				("Kindness", "On foot, give someone a few dollars (D-pad Up) - he'll remember. Once you have, you can still tip your hat to him. At a bar, buy a man a drink ($2). Wearing a hat, tip it to people you meet - on foot or from the saddle (it counts once a day per person). Say hello each day: the first greeting counts most, and people who like you speak well of you to their friends."),
				("Telegrams", "Visit any station clerk (D-pad Up or T) to write to your nemeses - make peace, threaten, pay them off, ask about their boss - or to people you've met: regards, a gift, condolences, a favour. Each message says what it's likely to do before you send it. Answers come in days and tell you what it did - and whether he'll still come for you; Recently sent lists what you've wired and what came of it, and the dead don't answer. Write back to keep it going: friendly people become pen pals who send their news, but write too often - or to someone who can't stand you - and they stop answering for a while. The clerk sells the local newspaper too (5 cents): the region's news written up as the papers do - your own deeds among it, though your name only appears on a WANTED notice - the wars or your bounty, and the funerals coming.")
			}
		},
		new Page
		{
			Title = "The X-Ray",
			Kicker = "Every bullet, every bone",
			Left = new(string, string)[2]
			{
				("Wound by wound", "Every man you shoot is recorded: the bullet's path through his body, the bones it broke, the organs it hit, entry and exit. Open the X-RAY LOG tab, pick a man and a shot, and turn the skeleton. Animals you shoot (deer, wolves, bears, birds...) have their own list with their own skeleton - and a hunter's verdict on the shot."),
				("Ammunition matters", "Split-point rounds break apart, high-velocity rounds punch through, express hits harder, explosive and incendiary rounds do what they say.")
			},
			Right = new(string, string)[3]
			{
				("Wounds that last", "A shot to the spine or neck can cripple a man for good - nemeses remember their limp, their scar, the arm that never set right. Shoot to wound, or shoot to kill."),
				("Fists and boots", "Punches, kicks, stomps and chokes are recorded too, blow by blow, from a black eye to a cracked skull. A man you knock out or choke out reads as out cold, not dead."),
				("Controls", "- Mouse drag or right stick: turn the skeleton\n- Wheel or LT / RT: zoom\n- Q / E or LB / RB: Humans / Animals")
			}
		},
		new Page
		{
			Title = "You're Ready",
			Kicker = "Ride out. Make some enemies.",
			Left = new(string, string)[1] { ("A few tips", "- Your first nemesis is one bad decision away\n- Masks keep your name clean - most of the time\n- Interrogate officers, not foot soldiers, for the big secrets\n- Check the WORLD tab: the outfits are busy without you\n- A downed nemesis isn't always dead - shoot again to be sure") },
			Right = new(string, string)[2]
			{
				("Make it yours", "O in the menu (pad: Start) opens Settings: the common ones on one card. Its All settings row, or Alt+F11, opens every setting (WASD or the arrows to move). Another mod on the same key? The Settings card's Menu key row: one key, or a combination like Shift+F9. Turn pop-up messages and name cards up or down from the menu (N and C). World News decides which world events pop up. Encounters sets how often someone comes for you: Fewer, Normal, More or Lots. Other keys: All settings, [Keys] - pick one, press Enter, then press the new key."),
				("This guide", "F11, then G (pad: Y) brings it back any time.")
			}
		}
	};

	public bool IsOpen { get; private set; }

	public bool InWarning
	{
		get
		{
			if (IsOpen)
			{
				return _warning.HasValue;
			}
			return false;
		}
	}

	public bool InWhatsNew
	{
		get
		{
			if (IsOpen)
			{
				return _whatsNew;
			}
			return false;
		}
	}

	public bool InSetup
	{
		get
		{
			if (IsOpen)
			{
				return _setup;
			}
			return false;
		}
	}

	public bool Capturing
	{
		get
		{
			if (IsOpen && _setup)
			{
				return _capturing;
			}
			return false;
		}
	}

	public WelcomeGuide(Action onClose)
	{
		_onClose = onClose;
	}

	public void Open(int page = 0)
	{
		IsOpen = true;
		_setup = false;
		_whatsNew = false;
		_readSomething = true;
		_page = Math.Max(0, Math.Min(Pages.Length - 1, page));
	}

	public void Close()
	{
		if (!IsOpen)
		{
			return;
		}
		if (_warning.HasValue)
		{
			_warning = null;
			IsOpen = false;
			return;
		}
		if (_whatsNew && _guideAfterNews)
		{
			_guideAfterNews = false;
			_whatsNew = false;
			_page = 0;
			_readSomething = true;
			NemesisMod.UiSound("SELECT");
			return;
		}
		bool num = _readSomething || _whatsNew;
		IsOpen = false;
		_setup = false;
		_whatsNew = false;
		_capturing = false;
		_readSomething = false;
		if (num)
		{
			_onClose?.Invoke();
		}
	}

	public void OpenWarning(string title, string sub, string[] body, string path, string[] steps)
	{
		_warning = (title, sub, body, path, steps);
		IsOpen = true;
		_setup = false;
		_whatsNew = false;
	}

	private static List<string> WrapPath(string p, float scale, float width)
	{
		List<string> list = new List<string>();
		string text = "";
		string[] array = p.Split('\\');
		foreach (string text2 in array)
		{
			string text3 = ((text.Length > 0) ? (text + "\\") : "") + text2;
			if (text.Length > 0 && Theme.MixedWidth(text3, scale, 0.0222f) > width)
			{
				list.Add(text + "\\");
				text = text2;
			}
			else
			{
				text = text3;
			}
		}
		if (text.Length > 0)
		{
			list.Add(text);
		}
		return list;
	}

	private void DrawWarning()
	{
		(string, string, string[], string, string[]) value = _warning.Value;
		float tw = 0.44f;
		List<string> list = value.Item3.SelectMany((string b) => Wrap(b, 0.27f, tw).Concat(new string[1] { "" })).ToList();
		List<string> list2 = value.Item5.Select((string s, int i) => $"{i + 1}.  {s}").SelectMany((string s) => Wrap(s, 0.27f, tw - 0.01f)).ToList();
		List<string> list3 = (from f in (value.Item4 ?? "").Split('\n')
			where f.Length > 0
			select f).SelectMany((string f) => WrapPath(f, 0.25f, tw - 0.02f).Concat(new string[1] { "" })).ToList();
		if (list3.Count > 0)
		{
			list3.RemoveAt(list3.Count - 1);
		}
		float num = 0.15f + (float)list.Count * 0.0245f + (float)list3.Count * 0.024f + 0.04f + (float)list2.Count * 0.0245f + 0.12f;
		float num2 = 0.25f;
		float num3 = Math.Max(0.05f, 0.5f - num / 2f);
		Theme.InkBehind(num2, num3, 0.5f, num);
		UiDraw.Rect(num2, num3, 0.5f, num, Theme.Panel.r, Theme.Panel.g, Theme.Panel.b, 240);
		(int, int, int) nativeRed = Theme.NativeRed;
		Theme.Hairline(num2, num3, 0.5f, num, nativeRed.Item1, nativeRed.Item2, nativeRed.Item3, 255);
		Theme.Hairline(num2 + 0.003f, num3 + 0.003f * UiDraw.Aspect, 0.494f, num - 0.006f * UiDraw.Aspect, nativeRed.Item1, nativeRed.Item2, nativeRed.Item3, 200);
		Theme.Corners(num2, num3, 0.5f, num);
		UiDraw.Rect(num2, num3, 0.5f, 0.006f, nativeRed.Item1, nativeRed.Item2, nativeRed.Item3, 255);
		float num4 = num3 + 0.022f;
		string text = "enRAGE".ToUpperInvariant();
		UiDraw.TextFont(text, UiDraw.TitleFont, 0.5f - Theme.CapsWidth(text, 0.21f) / 2f, num4, 0.21f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		num4 += 0.022f;
		string text2 = value.Item1.ToUpperInvariant();
		float num5 = 0.48f;
		while (num5 > 0.32f && Theme.CapsWidth(text2, num5) > tw)
		{
			num5 -= 0.02f;
		}
		float num6 = 0.5f - Theme.CapsWidth(text2, num5) / 2f;
		UiDraw.TextFont(text2, UiDraw.TitleFont, num6, num4, num5, nativeRed.Item1, nativeRed.Item2, nativeRed.Item3);
		UiDraw.TextFont(text2, UiDraw.TitleFont, num6 + 0.00052083336f, num4, num5, nativeRed.Item1, nativeRed.Item2, nativeRed.Item3);
		num4 += 0.05f;
		UiDraw.Text(value.Item2, 0.5f - Theme.MixedWidth(value.Item2, 0.25f, 0.0222f) / 2f, num4, 0.25f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		num4 += 0.03f;
		Theme.CenterRule(num2, num4, 0.5f, 0.05f, 150);
		num4 += 0.018f;
		float num7 = num2 + 0.03f;
		foreach (string item in list)
		{
			if (UiDraw.TextSlotsLeft < 8)
			{
				break;
			}
			if (item.Length > 0)
			{
				UiDraw.Text(item, num7, num4, 0.27f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
			}
			num4 += ((item.Length > 0) ? 0.0245f : 0.01225f);
		}
		if (list3.Count > 0 && list3[0].Length > 0)
		{
			float h = (float)list3.Count * 0.024f + 0.012f;
			UiDraw.Rect(num7 - 0.006f, num4 - 0.004f, tw + 0.012f, h, 12, 12, 11, 210);
			Theme.Hairline(num7 - 0.006f, num4 - 0.004f, tw + 0.012f, h, Theme.Warn.r, Theme.Warn.g, Theme.Warn.b, 200);
			foreach (string item2 in list3)
			{
				if (item2.Length > 0)
				{
					UiDraw.Text(item2, num7 + 0.004f, num4 + 0.002f, 0.25f, Theme.Warn.r, Theme.Warn.g, Theme.Warn.b);
				}
				num4 += ((item2.Length > 0) ? 0.024f : 0.01f);
			}
			num4 += 0.022f;
		}
		Theme.Section("WHAT TO DO", num7 - 0.004f, num4, num7 + tw);
		num4 += 0.03f;
		foreach (string item3 in list2)
		{
			if (UiDraw.TextSlotsLeft < 8)
			{
				break;
			}
			UiDraw.Text(item3, num7 + 0.006f, num4, 0.27f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
			num4 += 0.0245f;
		}
		float num8 = 0.17f;
		float num9 = 0.038f;
		float x = 0.5f - num8 / 2f;
		float num10 = num3 + num - 0.05f - num9;
		UiMouse.Region(x, num10, num8, num9, null, delegate
		{
			NemesisMod.UiSound("SELECT");
			Close();
		});
		Theme.SelectBox(x, num10, num8, num9);
		UiDraw.TextFont("UNDERSTOOD", UiDraw.TitleFont, 0.5f - Theme.CapsWidth("UNDERSTOOD", 0.24f) / 2f, num10 + num9 / 2f - 0.011f, 0.24f, Theme.SelectedText.r, Theme.SelectedText.g, Theme.SelectedText.b);
		List<(string, string)> hints = (XPad.RecentlyUsed ? new List<(string, string)> { ("A", "Understood") } : new List<(string, string)> { ("Enter", "Understood") });
		Theme.CenterRule(num2, num3 + num - 0.04f, 0.5f, 0.012f, 150);
		Theme.HintRow(num7, num7 + tw, num3 + num - 0.032f, hints, 0.21f, Theme.Text);
	}

	public void OpenWhatsNew()
	{
		IsOpen = true;
		_setup = false;
		_whatsNew = true;
		_page = 0;
	}

	public void ShowWhatsNewFromSetup()
	{
		_guideAfterNews = _thenGuide;
		_setup = false;
		_capturing = false;
		_thenGuide = false;
		_thenWhatsNew = false;
		OnSetupDone?.Invoke();
		_whatsNew = true;
		NemesisMod.UiSound("SELECT");
	}

	public void OpenSetup(bool thenGuide, bool thenWhatsNew = false)
	{
		_rows = SetupRows?.Invoke() ?? new List<SetupRow>();
		_askRow = null;
		_setup = true;
		_whatsNew = false;
		_readSomething = false;
		_thenGuide = thenGuide;
		_thenWhatsNew = thenWhatsNew && !thenGuide;
		_capturing = false;
		_row = 0;
		_setupNote = "";
		_page = 0;
		IsOpen = true;
	}

	public void CloseSetup()
	{
		bool num = _thenGuide || _thenWhatsNew;
		_thenGuide = false;
		_thenWhatsNew = false;
		_setup = false;
		_capturing = false;
		_askRow = null;
		OnSetupDone?.Invoke();
		if (num)
		{
			IsOpen = false;
		}
		else
		{
			Close();
		}
	}

	private void FinishSetup()
	{
		_setup = false;
		_capturing = false;
		_askRow = null;
		OnSetupDone?.Invoke();
		if (_thenGuide)
		{
			_page = 0;
			_readSomething = true;
			NemesisMod.UiSound("SELECT");
		}
		else if (_thenWhatsNew)
		{
			_thenWhatsNew = false;
			_whatsNew = true;
			NemesisMod.UiSound("SELECT");
		}
		else
		{
			Close();
		}
	}

	public void Capture(Keys code, Keys mods)
	{
		if (!_capturing)
		{
			return;
		}
		switch (code)
		{
		case Keys.ShiftKey:
		case Keys.ControlKey:
		case Keys.Menu:
		case Keys.LShiftKey:
		case Keys.RShiftKey:
		case Keys.LControlKey:
		case Keys.RControlKey:
		case Keys.LMenu:
		case Keys.RMenu:
			return;
		case Keys.Back:
		case Keys.Escape:
			_capturing = false;
			_setupNote = "Menu key unchanged";
			return;
		}
		string text = KeyCombo.Problem("RosterMenu", code | mods);
		if (text != null)
		{
			_setupNote = text;
			return;
		}
		_capturing = false;
		SetMenuKey?.Invoke(code | mods);
		_setupNote = "Menu key: " + KeyCombo.Name(code | mods);
		NemesisMod.UiSound("SELECT");
	}

	private bool SetupKey(Keys key)
	{
		if (_askRow != null)
		{
			return AskKey(key);
		}
		if (_rows.Count == 0)
		{
			FinishSetup();
			return true;
		}
		if (_capturing)
		{
			_capturing = false;
			_setupNote = "Menu key unchanged";
			return true;
		}
		SetupRow setupRow = _rows[Math.Min(_row, _rows.Count - 1)];
		switch (key)
		{
		case Keys.Up:
		case Keys.W:
			_row = (_row + _rows.Count - 1) % _rows.Count;
			NemesisMod.UiSound("NAV_UP");
			_setupNote = "";
			break;
		case Keys.Down:
		case Keys.S:
			_row = (_row + 1) % _rows.Count;
			NemesisMod.UiSound("NAV_DOWN");
			_setupNote = "";
			break;
		case Keys.Left:
		case Keys.A:
			ChangeRow(setupRow, -1);
			break;
		case Keys.Right:
		case Keys.D:
			ChangeRow(setupRow, 1);
			break;
		case Keys.Return:
		case Keys.Space:
			if (setupRow.IsStart)
			{
				FinishSetup();
			}
			else if (setupRow.Run != null)
			{
				NemesisMod.UiSound("SELECT");
				setupRow.Run();
			}
			else if (setupRow.IsKey)
			{
				_capturing = true;
				_setupNote = (XPad.RecentlyUsed ? "Press a keyboard key (or Escape) - the controller has its own row below" : "Press the new key - hold Shift / Ctrl / Alt for a combination (Escape cancels)");
			}
			else
			{
				ChangeRow(setupRow, 1);
			}
			break;
		case Keys.Back:
		case Keys.Escape:
			NemesisMod.UiSound("BACK");
			FinishSetup();
			break;
		}
		return true;
	}

	private void ChangeRow(SetupRow r, int dir)
	{
		if (r.Change == null)
		{
			return;
		}
		string text = r.Locked?.Invoke();
		if (text != null)
		{
			_setupNote = text;
			NemesisMod.UiSound("BACK");
			Log?.Invoke("SETUP " + r.Label + " can't change: " + text);
			return;
		}
		string text2 = r.Confirm?.Invoke(dir);
		if (text2 != null)
		{
			_askRow = r;
			_askDir = dir;
			_askText = text2;
			_askYes = false;
			NemesisMod.UiSound("SELECT");
			Log?.Invoke("SETUP asked: " + r.Label + " (" + r.Value?.Invoke() + ") - " + text2);
		}
		else
		{
			r.Change(dir);
			NemesisMod.UiSound((dir < 0) ? "NAV_LEFT" : "NAV_RIGHT");
		}
	}

	private bool AskKey(Keys key)
	{
		switch (key)
		{
		case Keys.Left:
		case Keys.Up:
		case Keys.Right:
		case Keys.Down:
		case Keys.A:
		case Keys.D:
		case Keys.S:
		case Keys.W:
			_askYes = !_askYes;
			NemesisMod.UiSound(_askYes ? "NAV_LEFT" : "NAV_RIGHT");
			break;
		case Keys.Return:
		case Keys.Space:
			Answer(_askYes);
			break;
		case Keys.Back:
		case Keys.Escape:
			Answer(yes: false);
			break;
		}
		return true;
	}

	private void Answer(bool yes)
	{
		SetupRow askRow = _askRow;
		int askDir = _askDir;
		_askRow = null;
		_askText = null;
		if (askRow != null)
		{
			Log?.Invoke("SETUP answered " + (yes ? "Yes" : "No") + ": " + askRow.Label);
			if (yes)
			{
				askRow.Change(askDir);
				_setupNote = askRow.Label + ": " + askRow.Value?.Invoke();
				NemesisMod.UiSound("SELECT");
			}
			else
			{
				_setupNote = askRow.Label + ": unchanged";
				NemesisMod.UiSound("BACK");
			}
		}
	}

	private void DrawSetup()
	{
		if (_askRow != null)
		{
			DrawAsk();
			return;
		}
		int num = (from r in _rows
			where !r.IsStart
			select r.Section).Distinct().Count();
		int num2 = _rows.Count((SetupRow r) => !r.IsStart);
		float num3 = 0.098f + (float)num * 0.032f + (float)num2 * 0.034f + 0.016f + 0.052f + 0.05f + 0.044f;
		float num4 = 0.32999998f;
		float num5 = Math.Max(0.06f, 0.5f - num3 / 2f);
		Theme.InkBehind(num4, num5, 0.34f, num3);
		UiDraw.Rect(num4, num5, 0.34f, num3, Theme.Panel.r, Theme.Panel.g, Theme.Panel.b, 232);
		Theme.Hairline(num4, num5, 0.34f, num3, Theme.Border.r, Theme.Border.g, Theme.Border.b, 230);
		Theme.Corners(num4, num5, 0.34f, num3);
		float num6 = num5 + 0.016f;
		string text = "enRAGE".ToUpperInvariant();
		UiDraw.TextFont(text, UiDraw.TitleFont, 0.5f - Theme.CapsWidth(text, 0.2f) / 2f, num6, 0.2f, Theme.NativeRed.r, Theme.NativeRed.g, Theme.NativeRed.b);
		num6 += 0.02f;
		UiDraw.TextFont("SETTINGS", UiDraw.TitleFont, 0.5f - Theme.CapsWidth("SETTINGS", 0.4f) / 2f, num6, 0.4f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		UiDraw.TextFont("SETTINGS", UiDraw.TitleFont, 0.5f - Theme.CapsWidth("SETTINGS", 0.4f) / 2f + 0.00052083336f, num6, 0.4f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		num6 += 0.04f;
		UiDraw.Text("Change any of it later in the settings.", 0.5f - Theme.MixedWidth("Change any of it later in the settings.", 0.21f, 0.0222f) / 2f, num6, 0.21f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		num6 += 0.022f;
		string text2 = null;
		float num7 = num4 + 0.018f;
		float num8 = 0.30400002f;
		for (int num9 = 0; num9 < _rows.Count; num9++)
		{
			SetupRow setupRow = _rows[num9];
			if (setupRow.IsStart)
			{
				continue;
			}
			if (setupRow.Section != text2)
			{
				text2 = setupRow.Section;
				Theme.Section((setupRow.Section ?? "").ToUpperInvariant(), num7 - 0.004f, num6 + 0.006f, num7 + num8);
				num6 += 0.032f;
			}
			bool flag = num9 == _row;
			int ri = num9;
			UiMouse.Region(num7, num6, num8, 0.030000001f, delegate
			{
				if (_row == ri)
				{
					return false;
				}
				_row = ri;
				return true;
			}, delegate
			{
				_row = ri;
				SetupKey(Keys.Return);
			});
			if (flag)
			{
				Theme.SelectBox(num7, num6, num8, 0.030000001f);
			}
			(int, int, int) tuple = (flag ? Theme.SelectedText : Theme.Text);
			float y = num6 + 0.015000001f - 0.011f;
			UiDraw.Text(setupRow.Label, num7 + 0.01f, y, 0.25f, tuple.Item1, tuple.Item2, tuple.Item3);
			string text3 = ((_capturing && flag) ? "press a key..." : (setupRow.Value?.Invoke() ?? ""));
			(int, int, int) tuple2 = (flag ? Theme.SelectedText : ((text3 == "Off") ? Theme.Dim : Theme.Gold));
			float num10 = Theme.MixedWidth(text3, 0.25f, 0.0222f);
			float num11 = num7 + num8 - 0.01f - num10;
			if (!setupRow.IsKey && setupRow.Run == null && !(_capturing && flag))
			{
				(int, int, int) c = (flag ? Theme.SelectedText : Theme.Faint);
				float num12 = 0.014f;
				float num13 = num12 / UiDraw.Aspect;
				float cy = num6 + 0.015000001f;
				float num14 = num7 + num8 - 0.01f - num13 / 2f;
				Theme.MenuArrow(Theme.ArrowDir.Right, num14, cy, num12, c);
				num11 = num14 - num13 / 2f - 0.006f - num10;
				Theme.MenuArrow(Theme.ArrowDir.Left, num11 - 0.006f - num13 / 2f, cy, num12, c);
			}
			UiDraw.Text(text3, num11, y, 0.25f, tuple2.Item1, tuple2.Item2, tuple2.Item3);
			num6 += 0.034f;
		}
		num6 += 0.01f;
		Theme.HRule(num7, num6, num8, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 160);
		num6 += 0.008f;
		SetupRow setupRow2 = ((_rows.Count > 0) ? _rows[Math.Min(_row, _rows.Count - 1)] : null);
		string s = ((_setupNote != "") ? _setupNote : KeyCombo.Keyed(setupRow2?.Help ?? ""));
		(int, int, int) tuple3 = ((_setupNote != "") ? Theme.Warn : Theme.Dim);
		foreach (string item in Wrap(s, 0.21f, num8).Take(2))
		{
			UiDraw.Text(item, num7, num6, 0.21f, tuple3.Item1, tuple3.Item2, tuple3.Item3);
			num6 += 0.019f;
		}
		num6 = Math.Max(num6, num5 + num3 - 0.094f);
		int si = _rows.FindIndex((SetupRow r) => r.IsStart);
		if (si >= 0)
		{
			bool flag2 = _row == si;
			float num15 = 0.16f;
			float num16 = 0.034f;
			float x = 0.5f - num15 / 2f;
			UiMouse.Region(x, num6, num15, num16, delegate
			{
				if (_row == si)
				{
					return false;
				}
				_row = si;
				return true;
			}, delegate
			{
				_row = si;
				SetupKey(Keys.Return);
			});
			if (flag2)
			{
				Theme.SelectBox(x, num6, num15, num16);
			}
			else
			{
				Theme.Hairline(x, num6, num15, num16, Theme.Border.r, Theme.Border.g, Theme.Border.b, 230);
			}
			string text4 = (_thenGuide ? "CONFIRM - ON TO THE GUIDE" : "CONFIRM");
			float num17 = 0.23f;
			while (num17 > 0.17f && Theme.CapsWidth(text4, num17) > num15 - 0.012f)
			{
				num17 -= 0.01f;
			}
			(int, int, int) tuple4 = (flag2 ? Theme.SelectedText : Theme.Title);
			UiDraw.TextFont(text4, UiDraw.TitleFont, 0.5f - Theme.CapsWidth(text4, num17) / 2f, num6 + num16 / 2f - 0.01f, num17, tuple4.Item1, tuple4.Item2, tuple4.Item3);
		}
		List<(string, string)> hints = (XPad.RecentlyUsed ? new List<(string, string)>
		{
			("D-pad", "Choose"),
			("A", "Select"),
			("B", "Confirm & Back")
		} : new List<(string, string)>
		{
			("Up/Down", "Choose"),
			("Left/Right", "Change"),
			("Enter", "Select"),
			("Backspace", "Confirm & Back")
		});
		Theme.CenterRule(num4, num5 + num3 - 0.04f, 0.34f, 0.012f, 150);
		Theme.HintRow(num7, num7 + num8, num5 + num3 - 0.032f, hints, 0.21f, Theme.Text);
	}

	private void DrawAsk()
	{
		float num = 0.296f;
		List<string> list = Wrap(_askText ?? "", 0.22f, num).Take(6).ToList();
		float num2 = 0.086f + (float)list.Count * 0.021f + 0.018f + 0.034f + 0.014f + 0.046f;
		float num3 = 0.32999998f;
		float num4 = 0.5f - num2 / 2f;
		float num5 = num3 + 0.022f;
		Theme.InkBehind(num3, num4, 0.34f, num2);
		UiDraw.Rect(num3, num4, 0.34f, num2, Theme.Panel.r, Theme.Panel.g, Theme.Panel.b, 232);
		Theme.Hairline(num3, num4, 0.34f, num2, Theme.Border.r, Theme.Border.g, Theme.Border.b, 230);
		Theme.Corners(num3, num4, 0.34f, num2);
		UiMouse.Region(0f, 0f, 1f, 1f, () => false);
		float num6 = num4 + 0.016f;
		string text = "enRAGE".ToUpperInvariant();
		UiDraw.TextFont(text, UiDraw.TitleFont, 0.5f - Theme.CapsWidth(text, 0.2f) / 2f, num6, 0.2f, Theme.NativeRed.r, Theme.NativeRed.g, Theme.NativeRed.b);
		num6 += 0.02f;
		string text2 = (_askRow?.Label ?? "").ToUpperInvariant();
		float num7 = 0.34f;
		while (num7 > 0.24f && Theme.CapsWidth(text2, num7) > num)
		{
			num7 -= 0.02f;
		}
		float num8 = 0.5f - Theme.CapsWidth(text2, num7) / 2f;
		UiDraw.TextFont(text2, UiDraw.TitleFont, num8, num6, num7, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		UiDraw.TextFont(text2, UiDraw.TitleFont, num8 + 0.00052083336f, num6, num7, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		num6 += 0.038f;
		Theme.HRule(num5, num6, num, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 160);
		num6 += 0.012f;
		foreach (string item in list)
		{
			UiDraw.Text(item, num5, num6, 0.22f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
			num6 += 0.021f;
		}
		num6 += 0.018f;
		float num9 = 0.372f;
		for (int num10 = 0; num10 < 2; num10++)
		{
			bool yes = num10 == 0;
			bool num11 = yes == _askYes;
			float num12 = num9 + (float)num10 * 0.13599999f;
			UiMouse.Region(num12, num6, 0.12f, 0.034f, delegate
			{
				if (_askYes == yes)
				{
					return false;
				}
				_askYes = yes;
				return true;
			}, delegate
			{
				Answer(yes);
			});
			if (num11)
			{
				Theme.SelectBox(num12, num6, 0.12f, 0.034f);
			}
			else
			{
				Theme.Hairline(num12, num6, 0.12f, 0.034f, Theme.Border.r, Theme.Border.g, Theme.Border.b, 230);
			}
			string text3 = (yes ? "YES" : "NO");
			(int, int, int) tuple = (num11 ? Theme.SelectedText : Theme.Title);
			UiDraw.TextFont(text3, UiDraw.TitleFont, num12 + 0.06f - Theme.CapsWidth(text3, 0.23f) / 2f, num6 + 0.017f - 0.01f, 0.23f, tuple.Item1, tuple.Item2, tuple.Item3);
		}
		List<(string, string)> hints = (XPad.RecentlyUsed ? new List<(string, string)>
		{
			("D-pad", "Choose"),
			("A", "Select"),
			("B", "No")
		} : new List<(string, string)>
		{
			("Left/Right", "Choose"),
			("Enter", "Select"),
			("Backspace", "No")
		});
		Theme.CenterRule(num3, num4 + num2 - 0.04f, 0.34f, 0.012f, 150);
		Theme.HintRow(num5, num5 + num, num4 + num2 - 0.032f, hints, 0.21f, Theme.Text);
	}

	private void DrawWhatsNew()
	{
		Page whatsNewPage = WhatsNewPage;
		float colW = 0.3f;
		float num = Math.Max(ColH(whatsNewPage.Left), ColH(whatsNewPage.Right));
		float num2 = 0.112f + num + 0.06f + 0.046f;
		float num3 = Math.Max(0.05f, 0.5f - num2 / 2f);
		Theme.InkBehind(0.13999999f, num3, 0.72f, num2);
		UiDraw.Rect(0.13999999f, num3, 0.72f, num2, Theme.Panel.r, Theme.Panel.g, Theme.Panel.b, 232);
		Theme.Hairline(0.13999999f, num3, 0.72f, num2, Theme.Border.r, Theme.Border.g, Theme.Border.b, 230);
		Theme.Corners(0.13999999f, num3, 0.72f, num2);
		float cy = num3 + 0.018f;
		string text = "enRAGE".ToUpperInvariant();
		UiDraw.TextFont(text, UiDraw.TitleFont, 0.5f - Theme.CapsWidth(text, 0.21f) / 2f, cy, 0.21f, Theme.NativeRed.r, Theme.NativeRed.g, Theme.NativeRed.b);
		cy += 0.021f;
		float num4 = 0.5f - Theme.CapsWidth("WHAT'S NEW", 0.44f) / 2f;
		UiDraw.TextFont("WHAT'S NEW", UiDraw.TitleFont, num4, cy, 0.44f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		UiDraw.TextFont("WHAT'S NEW", UiDraw.TitleFont, num4 + 0.00052083336f, cy, 0.44f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		cy += 0.044f;
		string text2 = "Version " + Theme.ModVersion + "  -  " + whatsNewPage.Kicker;
		UiDraw.Text(text2, 0.5f - Theme.MixedWidth(text2, 0.23f, 0.0222f) / 2f, cy, 0.23f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		cy += 0.026f;
		Theme.CenterRule(0.13999999f, cy, 0.72f, 0.05f, 150);
		cy += 0.016f;
		float num5 = 0.13999999f + 0.036f;
		float cx = num5 + colW + 0.048f;
		Col(whatsNewPage.Left, num5);
		Col(whatsNewPage.Right, cx);
		Theme.VRule(num5 + colW + 0.024f, cy, num - 0.026f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 120);
		float num6 = 0.15f;
		float num7 = 0.036f;
		float x = 0.5f - num6 / 2f;
		float num8 = num3 + num2 - 0.046f - num7 - 0.012f;
		UiMouse.Region(x, num8, num6, num7, null, delegate
		{
			NemesisMod.UiSound("SELECT");
			Close();
		});
		Theme.SelectBox(x, num8, num6, num7);
		UiDraw.TextFont("CONTINUE", UiDraw.TitleFont, 0.5f - Theme.CapsWidth("CONTINUE", 0.24f) / 2f, num8 + num7 / 2f - 0.011f, 0.24f, Theme.SelectedText.r, Theme.SelectedText.g, Theme.SelectedText.b);
		List<(string, string)> hints = (XPad.RecentlyUsed ? new List<(string, string)>
		{
			("A", "Continue"),
			("B", "Close")
		} : new List<(string, string)>
		{
			("Enter", "Continue"),
			("Backspace", "Close")
		});
		Theme.CenterRule(0.13999999f, num3 + num2 - 0.042f, 0.72f, 0.012f, 150);
		Theme.HintRow(num5, num5 + 2f * colW + 0.048f, num3 + num2 - 0.034f, hints, 0.21f, Theme.Text);
		void Col((string head, string body)[] secs, float num10)
		{
			float num9 = cy;
			for (int i = 0; i < secs.Length; i++)
			{
				var (text3, s) = secs[i];
				if (UiDraw.TextSlotsLeft < 8)
				{
					break;
				}
				Theme.Section(text3.ToUpperInvariant(), num10 - 0.004f, num9, num10 + colW);
				num9 += 0.034f;
				foreach (string item in Wrap(KeyCombo.Keyed(s), 0.285f, colW))
				{
					UiDraw.Text(item, num10, num9, 0.285f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
					num9 += 0.029f;
				}
				num9 += 0.026f;
			}
		}
		float ColH((string head, string body)[] secs)
		{
			return secs.Sum(((string head, string body) s) => 0.034f + (float)Wrap(KeyCombo.Keyed(s.body), 0.285f, colW).Count * 0.029f + 0.026f);
		}
	}

	public bool HandleKey(Keys key)
	{
		if (!IsOpen)
		{
			return false;
		}
		if (_warning.HasValue)
		{
			if (key == Keys.Return || key == Keys.Space || key == Keys.Back || key == Keys.Escape)
			{
				NemesisMod.UiSound("SELECT");
				Close();
			}
			return true;
		}
		if (_setup)
		{
			return SetupKey(key);
		}
		if (_whatsNew)
		{
			switch (key)
			{
			case Keys.Return:
			case Keys.Space:
				NemesisMod.UiSound("SELECT");
				Close();
				break;
			case Keys.Back:
			case Keys.Escape:
				NemesisMod.UiSound("BACK");
				Close();
				break;
			}
			return true;
		}
		switch (key)
		{
		case Keys.Prior:
		case Keys.Left:
		case Keys.A:
		case Keys.Q:
			if (_page > 0)
			{
				_page--;
				NemesisMod.UiSound("NAV_LEFT");
			}
			break;
		case Keys.Next:
		case Keys.Right:
		case Keys.D:
		case Keys.E:
			if (_page < Pages.Length - 1)
			{
				_page++;
				NemesisMod.UiSound("NAV_RIGHT");
			}
			break;
		case Keys.Return:
		case Keys.Space:
			if (_page < Pages.Length - 1)
			{
				_page++;
				NemesisMod.UiSound("NAV_RIGHT");
			}
			else
			{
				NemesisMod.UiSound("SELECT");
				Close();
			}
			break;
		case Keys.Home:
			_page = 0;
			break;
		case Keys.End:
			_page = Pages.Length - 1;
			break;
		case Keys.Back:
		case Keys.Escape:
			NemesisMod.UiSound("BACK");
			Close();
			break;
		}
		return true;
	}

	public void Draw()
	{
		if (!IsOpen)
		{
			return;
		}
		if (_warning.HasValue)
		{
			DrawWarning();
			return;
		}
		if (_setup)
		{
			DrawSetup();
			return;
		}
		if (_whatsNew)
		{
			DrawWhatsNew();
			return;
		}
		Page page = (_whatsNew ? WhatsNewPage : Pages[_page]);
		float num = 0.07f;
		float num2 = 0.86f;
		float num3 = 0.04f;
		float num4 = 0.90199995f;
		if (!Theme.Window(num3, num4))
		{
			Theme.PanelRect(num, num3, num2, num4 - num3, 236);
		}
		string text = page.Title.ToUpperInvariant();
		float num5 = 0.5f;
		while (num5 > 0.34f && Theme.CapsWidth(text, num5) > num2 * 0.7f)
		{
			num5 -= 0.02f;
		}
		float num6 = 0.5f - Theme.CapsWidth(text, num5) / 2f;
		UiDraw.TextFont(text, UiDraw.TitleFont, num6, 0.058f, num5, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		UiDraw.TextFont(text, UiDraw.TitleFont, num6 + 0.00052083336f, 0.058f, num5, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		string text2 = KeyCombo.Keyed(page.Kicker);
		UiDraw.Text(text2, 0.5f - Theme.MixedWidth(text2, 0.25f, 0.0222f) / 2f, 0.1f, 0.25f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		float num7 = 0.0105f;
		float num8 = 0.0075f;
		float num9 = (float)Pages.Length * num7 + (float)(Pages.Length - 1) * num8;
		float num10 = 0.5f - num9 / 2f;
		float num11 = 0.132f;
		for (int i = 0; i < Pages.Length; i++)
		{
			if (_whatsNew)
			{
				break;
			}
			float num12 = num10 + (float)i * (num7 + num8);
			(int, int, int) tuple = ((i == _page) ? Theme.NativeRed : ((i < _page) ? Theme.Text : Theme.Faint));
			UiDraw.Rect(num12, num11, num7, num7 * UiDraw.Aspect, tuple.Item1, tuple.Item2, tuple.Item3, (i == _page) ? 255 : 200);
			int pi = i;
			UiMouse.Region(num12 - 0.002f, num11 - 0.004f, num7 + 0.004f, num7 * UiDraw.Aspect + 0.008f, null, delegate
			{
				_page = pi;
				NemesisMod.UiSound("NAV_RIGHT");
			});
		}
		Theme.CenterRule(num, 0.16f, num2, 0.05f, 150);
		float x = num + 0.045f;
		float w = num2 - 0.09f;
		float num13 = 0.176f;
		float num14 = 0.15f;
		if (!_whatsNew)
		{
			UiDraw.Rect(x, num13, w, num14, 12, 12, 11, 170);
			Theme.Hairline(x, num13, w, num14, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 160);
			try
			{
				WelcomePics.Draw(_page, x, num13, w, num14);
			}
			catch
			{
			}
		}
		float y = (_whatsNew ? 0.185f : (num13 + num14 + 0.024f));
		float num15 = 0.04f;
		float num16 = (num2 - 0.09f - num15) / 2f;
		float num17 = num + 0.045f;
		float x2 = num17 + num16 + num15;
		Column(page.Left, num17, y, num16);
		Column(page.Right, x2, y, num16);
		bool recentlyUsed = XPad.RecentlyUsed;
		bool flag = _page == Pages.Length - 1;
		if (_whatsNew)
		{
			Theme.Footer(0.868f, recentlyUsed ? "A  Start Playing     B  Close" : "Enter  Start Playing     Backspace  Close");
			return;
		}
		string help = (recentlyUsed ? ("LB/RB  Page     A  " + (flag ? "Start Playing" : "Next") + "     B  Close") : ("Left/Right  Page     Enter  " + (flag ? "Start Playing" : "Next") + "     Backspace  Close"));
		Theme.Footer(0.868f, help);
	}

	private static void Column((string head, string body)[] sections, float x, float y, float width)
	{
		if (sections == null)
		{
			return;
		}
		for (int i = 0; i < sections.Length; i++)
		{
			var (text, s) = sections[i];
			if (UiDraw.TextSlotsLeft < 12 || y > 0.80599993f)
			{
				break;
			}
			Theme.Section(text.ToUpperInvariant(), x - 0.004f, y, x + width);
			y += 0.03f;
			string[] array = KeyCombo.Keyed(s).Split('\n');
			foreach (string text2 in array)
			{
				bool flag = text2.StartsWith("- ");
				string s2 = (flag ? text2.Substring(2) : text2);
				float x2 = (flag ? (x + 0.014f) : x);
				float width2 = (flag ? (width - 0.014f) : width);
				List<string> list = Wrap(s2, 0.245f, width2);
				for (int k = 0; k < list.Count; k++)
				{
					if (UiDraw.TextSlotsLeft < 8 || y > 0.8331999f)
					{
						return;
					}
					if (flag && k == 0)
					{
						float num = 0.0042f;
						float num2 = num * UiDraw.Aspect;
						UiDraw.Rect(x + 0.004f, y + 0.009187501f - num2 / 2f, num, num2, Theme.NativeRed.r, Theme.NativeRed.g, Theme.NativeRed.b, 230);
					}
					UiDraw.Text(list[k], x2, y, 0.245f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
					y += 0.0228f;
				}
				y += 0.006f;
			}
			y += 0.014f;
		}
	}

	private static List<string> Wrap(string s, float scale, float width)
	{
		List<string> list = new List<string>();
		string text = "";
		string[] array = (s ?? "").Split(' ');
		foreach (string text2 in array)
		{
			if (text2.Length != 0)
			{
				string text3 = ((text.Length == 0) ? text2 : (text + " " + text2));
				if (text.Length > 0 && Theme.MixedWidth(text3, scale, 0.0222f) > width)
				{
					list.Add(text);
					text = text2;
				}
				else
				{
					text = text3;
				}
			}
		}
		if (text.Length > 0)
		{
			list.Add(text);
		}
		return list;
	}
}
