using System.Collections.Generic;

namespace NemesisSystem;

public class LifeContent
{
	public string About { get; set; } = "Everyday events of the men on your roster (WorldSim LIFE, WorldLife.cs). who: any / gang / loner / army / a faction id. Tokens {n} him, {t} a town, {g} his gang, {f} a mate, {b} his boss, {l} his leader. weight: + good / - bad mood strength. hours on his mind. when: conditions joined by & (and) and | (or): married single children notarmy knows poor rich dim sharp pet nopet loyal coward hothead proud cold greedy drifter friendly surly devout godless notgodless catholic a quirk (drunk gambler braggart traitor honest superstitious sadist) rank>=N rank<=N hate>=N hate<N (N or vengeful / murderous) fear>=N fear<N aggr<N aggr>=N. odds: how often (10 = usual). effects: rest:H fear:N hate:N child marry single widow scar:beat|blade|claw friend sour pet nopet. Ids keep built-in rows from being added twice; list an Id in Removed to drop a built-in row for good.";

	public int Version { get; set; }

	public List<LifeContentRow> Rows { get; set; } = new List<LifeContentRow>();

	public List<string> Removed { get; set; } = new List<string>();
}
