using System.Collections.Generic;
using RDR2.Math;

namespace NemesisSystem;

internal interface IHoldingsWorld
{
	WorldState State { get; }

	long Now { get; }

	IEnumerable<Gang> Outfits { get; }

	IEnumerable<Vector3> OfficerCamps { get; }

	bool Destroyed(Gang g);

	CampSite Site(string id);

	IEnumerable<CampSite> SitesHeldBy(string gangId);

	string Owner(CampSite s);

	bool TakeoverPending(CampSite s);

	bool Cleared(CampSite s);

	bool Wet(CampSite s);

	bool InTown(CampSite s);

	NemesisRecord Holder(CampSite s);

	bool AnyoneCampsAt(string siteId);

	bool LiveSupplies(string siteId);

	bool LiveTreasury(string siteId);

	NemesisRecord LiveBoss(string siteId);

	string LiveCampOf(NemesisRecord n);

	IEnumerable<NemesisRecord> FreeMen(Gang g);

	int Purse(Gang g);

	bool HasPurse(Gang g);

	int Roll(int n);
}
