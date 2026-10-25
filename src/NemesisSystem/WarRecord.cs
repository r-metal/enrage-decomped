using System.Collections.Generic;

namespace NemesisSystem;

internal class WarRecord
{
	public string District;

	public string A;

	public string B;

	public long StartMin;

	public int HoldA0;

	public int HoldB0;

	public int Clashes;

	public int Dead;

	public int LastReportDay;

	public int DeadAtReport;

	public int MenA0;

	public int MenB0;

	public int GiveUpDay;

	public string Cause;

	public string Goal;

	public string GoalSite;

	public string GoalText;

	public string Result;

	public int Heat;

	public bool HeatSet;

	public bool Paused;

	public List<string> GoalLog = new List<string>();

	public long LastBattleAt;

	public int BattleDay;

	public int BattlesToday;

	public int YouKilledA;

	public int YouKilledB;

	public int OfficersA;

	public int OfficersB;

	public bool LeaderFellA;

	public bool LeaderFellB;

	public bool FavourSaid;

	public long EndMin;

	public string EndTitle;

	public string EndHow;

	public string EndPurseA;

	public string EndPurseB;

	public string EndMenA;

	public string EndMenB;

	public string EndLeaderA;

	public string EndLeaderB;

	public string EndAimA;

	public string EndAimB;

	public int EndHoldA = -1;

	public int EndHoldB = -1;

	public string Name;
}
