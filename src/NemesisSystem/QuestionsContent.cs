using System.Collections.Generic;

namespace NemesisSystem;

public class QuestionsContent
{
	public string About { get; set; } = "Interrogation questions (Questions.cs). cat: What / When / Where / Why (three each is what the menu is made for). need: fear over his temper's line before he'll answer. odds: + / - on his chance. lies: can he lie (a lie is still shown as ? intel, sometimes a trap). requires: gang named boss officer camps kin (joined by &). answer: orders wants bossnature catch news holedup stores people past worstgrudge bargain (and the older single ones: ride alone habit funeral shakeup camp hideout treasury supplies). requires 'news': his outfit has a burying or a shake-up heir to tell of; 'funeral': a burying. Built-in rows missing from this file are added by Id unless listed in Removed.";

	public int Version { get; set; }

	public List<QuestionRow> Rows { get; set; } = new List<QuestionRow>();

	public List<string> Removed { get; set; } = new List<string>();
}
