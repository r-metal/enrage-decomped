using System;
using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

internal static class WorldLife
{
	public static readonly LifeEvent[] BuiltIn = new LifeEvent[270]
	{
		new LifeEvent("any", "{n} came down with a fever", "laid up with a fever", "Sick", -3, 48, null, 10, "rest:36"),
		new LifeEvent("any", "{n} caught a cough in the rain he can't shake", "a cough that won't quit", "Sick", -2, 48, null, 10, "rest:12"),
		new LifeEvent("any", "{n} broke two fingers falling off a fence", "two splinted fingers", "Hurting", -2, 72, null, 10),
		new LifeEvent("any", "{n} was thrown from his horse", "a back that aches from the fall", "Hurting", -2, 48, null, 10, "rest:12"),
		new LifeEvent("any", "{n} was bitten by a rattlesnake", "a swollen leg from a snakebite", "Hurting", -3, 48, null, 8, "rest:24"),
		new LifeEvent("any", "{n} nearly drowned crossing a river", "nearly drowned crossing a river", "Shaken", -2, 48, null, 10),
		new LifeEvent("any", "{n} tangled with a cougar and lived", "the cougar's claws", "Shaken", -3, 96, null, 6, "scar:claw", "rest:24"),
		new LifeEvent("any", "{n} got lost in a blizzard up in the mountains", "the cold of the mountains", "Shaken", -2, 48, null, 8, "rest:12"),
		new LifeEvent("any", "{n} got sunstroke on the trail", "a sunstroke headache", "Sick", -2, 24, null, 8, "rest:12"),
		new LifeEvent("any", "{n} had a rotten tooth pulled in {t}", "a sore jaw", "Hurting", -1, 24, null, 10),
		new LifeEvent("any", "{n} ate bad meat and spent two days sick", "a gut that's still not right", "Sick", -2, 48, null, 10, "rest:24"),
		new LifeEvent("any", "{n} lost a tooth in a tavern scrap in {t}", "a gap in his teeth", "Humiliated", -1, 48, null, 8, "scar:beat"),
		new LifeEvent("any", "{n} took a beating from three drunks in {t}", "the beating in {t}", "Humiliated", -3, 72, null, 6, "scar:beat"),
		new LifeEvent("any", "{n} cut his hand open skinning a deer", "a bandaged hand", "Hurting", -1, 36, null, 10),
		new LifeEvent("any", "{n} had nightmares about the men he's killed", "bad dreams", "Shaken", -2, 48, "aggr<60", 10),
		new LifeEvent("any", "{n} brought down a big buck", "the biggest buck he's ever shot", "Proud", 2, 48, null, 10),
		new LifeEvent("any", "{n} drove off a wolf pack alone", "the wolves he drove off", "Proud", 2, 48, null, 10),
		new LifeEvent("any", "{n} spent a night lost in the woods", "a night lost in the woods", "Shaken", -1, 24, null, 10),
		new LifeEvent("any", "{n} saw a ghost on the road at night - or says he did", "something he saw on the road at night", "Shaken", -2, 48, null, 10),
		new LifeEvent("any", "{n} found a gold nugget in a creek", "a gold nugget in his pocket", "Flush", 3, 72, null, 5),
		new LifeEvent("any", "{n} was robbed on the road", "being robbed on the road", "Bitter", -3, 72, null, 10),
		new LifeEvent("any", "{n} watched a man die of fever", "a dying man's face", "Shaken", -1, 48, null, 10),
		new LifeEvent("any", "{n} buried a stranger by the road", "a grave by the road", "Steady", 0, 24, null, 10),
		new LifeEvent("any", "{n} helped a family whose wagon broke down", "helping a stranded family", "Proud", 1, 48, "friendly", 10),
		new LifeEvent("any", "{n} found a stray dog that follows him now", "the stray dog that follows him", "Joyful", 2, 96, "nopet", 6, "pet"),
		new LifeEvent("any", "{n}'s dog was killed by a wolf", "his dog, dead", "Grieving", -3, 96, "pet", 10, "nopet"),
		new LifeEvent("any", "{n} slept rough under the stars", "a cold night outdoors", "Steady", 0, 24, null, 10),
		new LifeEvent("any", "{n} saw a hanging in {t}", "the hanging in {t}", "Shaken", -2, 48, null, 10, "fear:2"),
		new LifeEvent("any", "{n} won a horse race outside {t}", "winning the horse race", "Proud", 3, 48, null, 10),
		new LifeEvent("any", "{n} bought a new horse", "his new horse", "Proud", 2, 48, null, 10),
		new LifeEvent("any", "{n}'s horse went lame and had to be shot", "having to shoot his horse", "Bitter", -2, 72, null, 10),
		new LifeEvent("any", "{n} bought a new hat in {t}", "a fine new hat", "Proud", 1, 36, null, 10),
		new LifeEvent("any", "{n} got a shave and a hot bath in {t}", "a hot bath", "Joyful", 1, 24, null, 10),
		new LifeEvent("any", "{n} had his photograph taken in {t}", "his photograph", "Proud", 1, 48, null, 10),
		new LifeEvent("any", "{n} drank {t} dry", "a long night in {t}", "Drunk", 1, 18, null, 10),
		new LifeEvent("any", "{n} drank alone in {t} for three days", "the bottle", "Drunk", -1, 48, null, 10),
		new LifeEvent("any", "{n} swore off drink", "staying sober", "Steady", 1, 72, null, 10),
		new LifeEvent("any", "{n} fell off the wagon - drinking again", "the bottle again", "Drunk", -1, 48, null, 10),
		new LifeEvent("any", "{n} won big at poker in {t}", "a fat poker pot", "Flush", 2, 48, null, 10),
		new LifeEvent("any", "{n} lost his money at cards in {t}", "losing at cards", "Bitter", -2, 48, null, 10),
		new LifeEvent("any", "{n} lost his horse in a card game in {t}", "losing his horse at cards", "Bitter", -3, 72, "notarmy", 6),
		new LifeEvent("any", "{n} won an arm-wrestling match in {t}", "beating the big man at arm-wrestling", "Proud", 2, 36, null, 10),
		new LifeEvent("any", "{n} won a shooting contest in {t}", "the shooting contest he won", "Proud", 3, 48, null, 10),
		new LifeEvent("any", "{n} lost a shooting contest in {t}", "losing the shooting contest", "Humiliated", -2, 36, null, 10),
		new LifeEvent("any", "{n} got into a shouting match in {t}", "the shouting match in {t}", "Bitter", -1, 24, null, 10),
		new LifeEvent("any", "{n} was run out of the {t} saloon", "being run out of the saloon", "Humiliated", -2, 36, null, 10),
		new LifeEvent("any", "{n} spent a night in the {t} jail", "a night in the cells", "Jailed", -2, 24, "notarmy", 10, "rest:24"),
		new LifeEvent("any", "{n} was fined by the {t} sheriff", "the sheriff's fine", "Bitter", -1, 24, "notarmy", 10),
		new LifeEvent("any", "{n} talked his way out of an arrest in {t}", "fooling the sheriff", "Proud", 2, 36, "notarmy", 10),
		new LifeEvent("any", "{n} was cheated by a merchant in {t}", "the crooked merchant", "Bitter", -2, 48, null, 10),
		new LifeEvent("any", "{n} had his gun stolen in {t}", "his stolen gun", "Bitter", -2, 48, null, 10),
		new LifeEvent("any", "{n} bought a fine new revolver in {t}", "his new revolver", "Proud", 2, 48, null, 10),
		new LifeEvent("any", "{n} paid off his debts", "being square with everyone", "Joyful", 2, 72, null, 10),
		new LifeEvent("any", "{n} owes money to the wrong people", "the men he owes", "Shaken", -3, 96, "notarmy", 10),
		new LifeEvent("any", "{n} lent money to a man who skipped town", "the money he'll never see again", "Bitter", -2, 72, null, 10),
		new LifeEvent("any", "{n} had his fortune read in {t} - it went badly", "what the fortune teller said", "Shaken", -1, 48, null, 10),
		new LifeEvent("any", "{n} saw a travelling show in {t}", "the travelling show", "Joyful", 1, 24, null, 10),
		new LifeEvent("any", "{n} celebrated his birthday in {t}", "his birthday", "Joyful", 2, 24, null, 10),
		new LifeEvent("any", "{n} took up the harmonica", "learning the harmonica", "Joyful", 1, 48, null, 10),
		new LifeEvent("any", "{n} read a dime novel about outlaws", "a dime novel", "Joyful", 1, 24, null, 10),
		new LifeEvent("any", "{n} learned to write his name", "writing his name", "Proud", 1, 72, "dim", 10),
		new LifeEvent("any", "{n} went to church in {t}", "the sermon in {t}", "Steady", 1, 48, "notgodless", 10),
		new LifeEvent("any", "{n} prayed all night", "praying for his soul", "Steady", 1, 24, "devout", 10),
		new LifeEvent("any", "{n} cursed God after a bad week", "a God who doesn't listen", "Bitter", -1, 48, "godless", 10),
		new LifeEvent("any", "{n} confessed his sins to a priest in {t}", "the confession", "Steady", 2, 48, "catholic&notgodless", 10),
		new LifeEvent("any", "{n} got married in {t}", "his new wife", "Joyful", 4, 120, "single", 6, "marry"),
		new LifeEvent("any", "{n} fell for a saloon girl in {t}", "a girl in {t}", "Joyful", 2, 72, "single", 10),
		new LifeEvent("any", "{n} was jilted by a woman in {t}", "the woman who left him", "Bitter", -3, 96, "single", 10),
		new LifeEvent("any", "{n} became a father again", "a new baby at home", "Joyful", 4, 96, "married", 8, "child"),
		new LifeEvent("any", "{n}'s child fell sick", "his sick child", "Homesick", -3, 96, "married&children", 10),
		new LifeEvent("any", "{n}'s wife left him", "his wife walking out", "Bitter", -4, 120, "married", 4, "single"),
		new LifeEvent("any", "{n}'s wife died of consumption", "his dead wife", "Grieving", -5, 168, "married", 3, "widow"),
		new LifeEvent("any", "{n} went home to see his family", "seeing his family", "Joyful", 3, 72, "married", 10),
		new LifeEvent("any", "{n} sent money home to his family", "the money he sent home", "Steady", 1, 48, "married", 10),
		new LifeEvent("any", "{n}'s wife fell ill", "his sick wife, far away", "Homesick", -3, 96, "married", 10),
		new LifeEvent("any", "{n} got a letter from home - good news", "good news from home", "Joyful", 2, 72, null, 10),
		new LifeEvent("any", "{n} got a letter from home - bad news", "bad news from home", "Homesick", -2, 72, null, 10),
		new LifeEvent("any", "{n} learned his brother back east had died", "his dead brother", "Grieving", -3, 120, null, 10),
		new LifeEvent("any", "{n} got word his father had died", "his father's death", "Grieving", -3, 120, null, 10),
		new LifeEvent("any", "{n} visited his mother's grave", "his mother's grave", "Grieving", -1, 36, null, 10),
		new LifeEvent("any", "{n} talked about leaving the territory for good", "leaving all this behind", "Homesick", -1, 72, null, 10),
		new LifeEvent("any", "{n} heard tales of what you've done", "the stories about you", "Shaken", -2, 72, "knows", 10, "fear:5"),
		new LifeEvent("any", "{n} swore in front of witnesses he'd kill you", "killing you", "Bitter", -1, 72, "hate>=40", 10, "hate:4"),
		new LifeEvent("any", "{n} told everyone in {t} how he'd beat you", "his boasts about you", "Proud", 1, 48, "proud|hothead", 10, "hate:2"),
		new LifeEvent("any", "{n} had nightmares about you", "your face", "Shaken", -2, 48, "fear>=30", 10, "fear:4"),
		new LifeEvent("any", "{n} cleaned and oiled his guns all evening", "the day he finds you", "Steady", 1, 24, "hate>=murderous", 10),
		new LifeEvent("any", "{n} sharpened his knife thinking of you", "your throat", "Bitter", 0, 48, "hate>=vengeful&hate<murderous", 10, "hate:2"),
		new LifeEvent("any", "{n} asked around {t} about you", "where you ride", "Steady", 0, 48, "hate>=vengeful&knows", 10),
		new LifeEvent("any", "{n} decided you weren't worth dying for", "letting it go", "Steady", 1, 72, "coward|drifter", 6, "hate:-6"),
		new LifeEvent("any", "{n} saw your wanted poster in {t}", "your face on the poster", "Steady", 0, 48, "knows", 8, "fear:2"),
		new LifeEvent("gang", "{n} rode on a stagecoach job that paid well", "his cut from the stagecoach", "Flush", 2, 72, null, 10),
		new LifeEvent("gang", "{n} rode on a job that went bad", "the job that went bad", "Shaken", -2, 72, null, 10),
		new LifeEvent("gang", "{n} robbed a homestead with the boys", "the homestead job", "Flush", 2, 48, null, 10),
		new LifeEvent("gang", "{n} helped hold up a train", "the train job", "Flush", 3, 72, "rank>=2", 6),
		new LifeEvent("gang", "{n} was nearly caught by lawmen", "the law on his heels", "Shaken", -2, 48, null, 10),
		new LifeEvent("gang", "{n} was passed over", "the seat he thinks he's owed", "Bitter", -3, 120, "rank<=3", 10),
		new LifeEvent("gang", "{n} was praised by {b}", "{b}'s praise", "Proud", 2, 72, null, 10),
		new LifeEvent("gang", "{n} was dressed down by {b} in front of the others", "{b}'s tongue-lashing", "Humiliated", -2, 72, null, 10),
		new LifeEvent("gang", "{n} stood watch three nights running", "too many nights on watch", "Bitter", -1, 48, null, 10),
		new LifeEvent("gang", "{n} and {f} became friends", "his new friend {f}", "Joyful", 1, 48, null, 10, "friend"),
		new LifeEvent("gang", "{n} accused {f} of skimming from the take", "{f} and the missing money", "Bitter", -2, 72, null, 10, "sour"),
		new LifeEvent("gang", "{n} taught a new man how to shoot", "the new man he's teaching", "Proud", 1, 48, "rank>=2", 10),
		new LifeEvent("gang", "{n} won the camp card game", "the pot he took off the boys", "Flush", 1, 24, null, 10),
		new LifeEvent("gang", "{n} lost his share at cards to {f}", "{f} and his luck", "Bitter", -2, 48, null, 10),
		new LifeEvent("gang", "{n} swore loyalty to {l} again", "loyalty to {l}", "Steady", 1, 72, "loyal", 10),
		new LifeEvent("gang", "{n} thought about leaving the {g}", "getting out", "Bitter", -2, 96, "fear>=40|coward", 10),
		new LifeEvent("gang", "{n} drank to the gang's dead", "the boys who didn't make it", "Grieving", -1, 36, null, 10),
		new LifeEvent("gang", "{n} got the camp's best bedroll", "a good bedroll", "Joyful", 1, 24, null, 10),
		new LifeEvent("odriscoll", "{n} sang Irish songs by the fire all night", "the old songs", "Joyful", 1, 24, null, 10),
		new LifeEvent("odriscoll", "{n} heard {l} wants results", "what {l} expects", "Shaken", -1, 48, null, 10),
		new LifeEvent("odriscoll", "{n} stole a wagon of whiskey", "the stolen whiskey", "Flush", 2, 48, null, 10),
		new LifeEvent("raider", "{n} raised a toast to the old cause", "the war that never ended", "Proud", 1, 36, null, 10),
		new LifeEvent("raider", "{n} drilled with the boys like it was '63", "the old drill", "Steady", 1, 24, null, 10),
		new LifeEvent("raider", "{n} cursed the Army over a bottle", "the bluecoats", "Bitter", -1, 36, null, 10),
		new LifeEvent("klan", "{n} hid his robe from his wife", "keeping his secret", "Shaken", -1, 48, null, 10),
		new LifeEvent("klan", "{n} was shunned in {t}", "the looks he gets in {t}", "Humiliated", -2, 48, null, 10),
		new LifeEvent("wapiti", "{n} brought an elk back for the families", "the elk hunt", "Proud", 2, 48, null, 10),
		new LifeEvent("wapiti", "{n} went without when the rations came short again", "the short rations", "Bitter", -2, 48, null, 10),
		new LifeEvent("wapiti", "{n} broke a wild horse", "the new horse", "Joyful", 1, 36, null, 10),
		new LifeEvent("wapiti", "{n} was turned away at the trading post in Valentine", "the trading post", "Humiliated", -2, 48, null, 10),
		new LifeEvent("wapiti", "{n} watched soldiers ride past the reservation", "the soldiers", "Shaken", -1, 24, null, 10),
		new LifeEvent("wapiti", "{n} sat with the elders through the night", "the elders' words", "Steady", 1, 48, null, 10),
		new LifeEvent("wapiti", "{n} argued with {f} about fighting the Army", "the argument with {f}", "Bitter", -1, 36, null, 10),
		new LifeEvent("wapiti", "{n} lost a horse in the river crossing", "the lost horse", "Grieving", -1, 48, null, 10),
		new LifeEvent("wapiti", "{n} spent the night alone in the mountains", "the night in the mountains", "Steady", 1, 36, null, 10),
		new LifeEvent("wapiti", "{n} fell sick with the fever going round the camp", "the fever", "Sick", -2, 48, null, 10, "rest:36"),
		new LifeEvent("wapiti", "{n} won a horse race against {f}", "the race", "Proud", 2, 36, null, 10),
		new LifeEvent("wapiti", "{n} made new arrows through the evening", "the new arrows", "Steady", 0, 24, null, 10),
		new LifeEvent("nightfolk", "{n} hung a lantern in the cypress and waited for something to come to it", "the lantern in the cypress", "Steady", 1, 36, null, 10),
		new LifeEvent("nightfolk", "{n} heard his name called from the water three nights running", "the voice in the water", "Shaken", -2, 72, null, 10),
		new LifeEvent("nightfolk", "{n} came back from the swamp mud to the chest and won't say where he'd been", "where he was", "Steady", 0, 48, null, 10),
		new LifeEvent("nightfolk", "{n} buried something under the floor of his shack", "what's under the floor", "Steady", 1, 72, null, 10),
		new LifeEvent("nightfolk", "{n} lost two toes to a gator in the shallows", "the gator", "Hurting", -3, 72, null, 6, "rest:24", "scar:claw"),
		new LifeEvent("nightfolk", "{n} sat up all night with {l}, and neither said a word", "the night with {l}", "Steady", 1, 48, "rank>=2", 10),
		new LifeEvent("nightfolk", "{n} found a Saint Denis man's pocket watch in a heron's nest", "the pocket watch", "Flush", 1, 36, null, 10),
		new LifeEvent("nightfolk", "{n} carved a face into a cypress stump and talks to it", "the face in the stump", "Joyful", 1, 48, null, 10),
		new LifeEvent("nightfolk", "{n} followed a light out past Lagras until dawn", "the light past Lagras", "Shaken", -1, 48, null, 10),
		new LifeEvent("nightfolk", "{n} hasn't spoken in nine days", "the silence", "Steady", 0, 72, null, 10),
		new LifeEvent("nightfolk", "{n} traded a jar of teeth for a shotgun", "the trade", "Proud", 1, 48, null, 10),
		new LifeEvent("nightfolk", "{n} was seen in daylight, and the others won't look at him", "being seen in daylight", "Humiliated", -2, 72, null, 10),
		new LifeEvent("nightfolk", "{n} came down with the swamp fever", "the swamp fever", "Sick", -2, 48, null, 10, "rest:36"),
		new LifeEvent("nightfolk", "{n} sang to the dead in the reeds until the frogs went quiet", "the singing", "Steady", 1, 36, null, 10),
		new LifeEvent("nightfolk", "{n} and {f} rowed out to the old plantation and came back one oar short", "the night at the plantation", "Shaken", -1, 48, null, 10),
		new LifeEvent("nightfolk", "{n} hung a Raider's hat in the trees by the road", "the hat in the trees", "Proud", 2, 48, null, 10),
		new LifeEvent("nightfolk", "{n} fell out with {f} over which of them the bayou favours", "{f}", "Bitter", -2, 48, null, 8, "sour"),
		new LifeEvent("nightfolk", "{n} pulled a drowned man out of the water and went through his pockets", "the drowned man's pockets", "Flush", 1, 36, null, 10),
		new LifeEvent("nightfolk", "{n} won't go past the old church at Shady Belle any more", "the old church", "Shaken", -1, 48, "superstitious|coward", 10),
		new LifeEvent("nightfolk", "{n} whistled in the dark until something whistled back", "what whistled back", "Shaken", -2, 48, null, 10),
		new LifeEvent("nightfolk", "{n} took in a stray dog and named it after a dead man", "the dog", "Joyful", 2, 48, "nopet", 6, "pet"),
		new LifeEvent("nightfolk", "{n} stood outside a house in Saint Denis all night, until the lamps went out", "the house in Saint Denis", "Steady", 0, 48, null, 10),
		new LifeEvent("nightfolk", "{n} keeps a heron's skull by his bed now, for luck", "the heron's skull", "Steady", 1, 48, null, 10),
		new LifeEvent("nightfolk", "{n} was marked by {l} for something he swears he didn't do", "{l}'s mark", "Bitter", -3, 72, "rank<=2", 10),
		new LifeEvent("nightfolk", "{n} and {f} watched a riverboat go by and counted the lights", "the riverboat with {f}", "Joyful", 1, 36, null, 8, "friend"),
		new LifeEvent("laramie", "{n} changed the brand on a stolen steer so well the owner bought it back", "the steer", "Proud", 2, 48, null, 10),
		new LifeEvent("laramie", "{n} got kicked by a horse in the corral", "the kick", "Hurting", -2, 48, null, 8, "rest:24"),
		new LifeEvent("laramie", "{n} rode night herd alone in a storm", "the storm", "Steady", 1, 36, null, 10),
		new LifeEvent("laramie", "{n} lost his share at cards in Blackwater", "the lost share", "Bitter", -2, 48, "gambler|greedy", 10),
		new LifeEvent("laramie", "{n} sold a stolen horse in Armadillo for twice what it was worth", "the sale", "Flush", 2, 48, null, 10),
		new LifeEvent("laramie", "{n} roped a wild mustang on the Great Plains", "the mustang", "Joyful", 2, 48, null, 10),
		new LifeEvent("laramie", "{n} found his own face on a cattlemen's association poster", "the poster", "Shaken", -1, 72, null, 10),
		new LifeEvent("laramie", "{n} and {f} drove a stolen herd through the night to Hennigan's Stead", "the drive with {f}", "Proud", 1, 48, null, 8, "friend"),
		new LifeEvent("laramie", "{n} was shot at by a rancher who caught him at the fence line", "the rancher's shotgun", "Shaken", -2, 48, null, 10),
		new LifeEvent("laramie", "{n} branded his hand by mistake", "the burn", "Hurting", -1, 48, null, 10),
		new LifeEvent("laramie", "{n} heard {l} wants him to lead his own crew", "{l}'s word", "Proud", 2, 72, "rank<=2", 10),
		new LifeEvent("laramie", "{n} argued with {f} about who lost the last herd", "{f}", "Bitter", -2, 48, null, 8, "sour"),
		new LifeEvent("delobo", "{n} crossed the river to see his mother", "his mother across the river", "Homesick", -1, 72, null, 10),
		new LifeEvent("delobo", "{n} lit a candle at the mission for the men they lost", "the candle for the dead", "Grieving", -1, 48, "notgodless", 10),
		new LifeEvent("delobo", "{n} won a horse race in Tumbleweed against an American", "the race", "Proud", 2, 48, null, 10),
		new LifeEvent("delobo", "{n} spent his share of the train money in one night in Armadillo", "the night in Armadillo", "Joyful", 1, 36, null, 10),
		new LifeEvent("delobo", "{n} was nearly bitten by a rattlesnake under his bedroll", "the rattlesnake", "Shaken", -1, 24, null, 10),
		new LifeEvent("delobo", "{n} went three days without water in Gaptooth Ridge", "the dry days", "Sick", -2, 48, null, 6, "rest:24"),
		new LifeEvent("delobo", "{n} ran guns across the river by moonlight", "the guns", "Flush", 2, 48, null, 10),
		new LifeEvent("delobo", "{n} played the guitar by the fire until the jefe told him to stop", "the guitar", "Joyful", 1, 24, null, 10),
		new LifeEvent("delobo", "{n} heard the rurales are asking about him in Nuevo Paraiso", "the rurales", "Shaken", -2, 72, null, 10),
		new LifeEvent("delobo", "{n} and {f} hid from an Army patrol in a dry wash all day", "the day in the wash with {f}", "Steady", 1, 48, null, 8, "friend"),
		new LifeEvent("delobo", "{n} was cheated by a horse trader in Armadillo", "the horse trader", "Bitter", -2, 48, null, 10),
		new LifeEvent("delobo", "{n} wrote a letter home he never sent", "the letter", "Homesick", -1, 48, "married|children", 10),
		new LifeEvent("army", "{n} drew extra duty at Fort Wallace", "extra duty", "Bitter", -1, 48, null, 10),
		new LifeEvent("army", "{n} got his pay", "payday", "Flush", 1, 48, null, 10),
		new LifeEvent("army", "{n} was drilled hard by his sergeant", "the sergeant's drill", "Bitter", -1, 24, null, 10),
		new LifeEvent("army", "{n} was commended by his officer", "the commendation", "Proud", 2, 72, null, 10),
		new LifeEvent("army", "{n} was put on report for drinking", "being put on report", "Humiliated", -2, 72, null, 10),
		new LifeEvent("army", "{n} stood guard all night in the cold", "the cold night on guard", "Sick", -1, 24, null, 10),
		new LifeEvent("army", "{n} wrote a letter to his mother", "home", "Homesick", -1, 48, null, 10),
		new LifeEvent("army", "{n} lost money betting on a boxing match at the fort", "the boxing bet", "Bitter", -1, 24, null, 10),
		new LifeEvent("army", "{n} rode dispatch to Valentine and back", "the dispatch ride", "Steady", 1, 24, null, 10),
		new LifeEvent("army", "{n} got new boots from the quartermaster", "new boots", "Joyful", 1, 24, null, 10),
		new LifeEvent("army", "{n} was disciplined for losing his rifle", "the lost rifle", "Humiliated", -2, 72, null, 10),
		new LifeEvent("army", "{n} chased deserters through the hills", "the deserters", "Steady", 0, 36, null, 10),
		new LifeEvent("loner", "{n} drifted to {t} looking for work", "finding work", "Steady", 0, 48, null, 10),
		new LifeEvent("loner", "{n} took a job breaking horses near {t}", "breaking horses", "Steady", 1, 48, null, 10),
		new LifeEvent("loner", "{n} worked a week on a ranch near {t}", "ranch work", "Steady", 1, 72, null, 10),
		new LifeEvent("loner", "{n} quit his job in {t}", "the job he quit", "Bitter", -1, 48, null, 10),
		new LifeEvent("loner", "{n} sold furs in {t}", "a good price for furs", "Flush", 1, 36, null, 10),
		new LifeEvent("loner", "{n} played cards with strangers in {t} and won", "the strangers' money", "Flush", 2, 36, null, 10),
		new LifeEvent("loner", "{n} robbed a traveller on the road", "the traveller he robbed", "Flush", 2, 48, "poor", 10),
		new LifeEvent("loner", "{n} was hired as a guard for a wagon train", "the wagon train", "Steady", 1, 72, null, 10),
		new LifeEvent("loner", "{n} made a friend in {t}", "a friend in {t}", "Joyful", 2, 48, null, 10),
		new LifeEvent("any", "{n} lost his hat in the wind on the trail", "his good hat, gone", "Bitter", -1, 24, null, 10),
		new LifeEvent("any", "{n} found a pocket watch in the mud outside {t}", "the found watch", "Joyful", 1, 36, null, 8),
		new LifeEvent("any", "{n} got into a shoving match over a woman in {t}", "the woman in {t}", "Bitter", -1, 48, "single", 8, "scar:beat"),
		new LifeEvent("any", "{n} paid a dentist in {t} to pull a rotten tooth", "the pulled tooth", "Hurting", -2, 48, null, 10),
		new LifeEvent("any", "{n} had his boots stolen while he slept", "his stolen boots", "Bitter", -2, 48, null, 10),
		new LifeEvent("any", "{n} won a shooting contest outside {t}", "winning the shooting contest", "Proud", 2, 72, "aggr>=50", 6),
		new LifeEvent("any", "{n} lost a shooting contest outside {t}", "losing the shooting contest", "Humiliated", -2, 48, null, 6),
		new LifeEvent("any", "{n} drank too much and slept in a ditch", "the ditch he woke in", "Humiliated", -1, 24, "drunk", 14),
		new LifeEvent("any", "{n} got thrown out of a saloon in {t}", "the saloon in {t}", "Humiliated", -2, 48, "drunk|hothead", 10),
		new LifeEvent("any", "{n} lost a week's money at faro in {t}", "the faro table", "Bitter", -2, 72, "gambler", 16, "hate:1"),
		new LifeEvent("any", "{n} cleaned out a card table in {t}", "the poker pot", "Flush", 3, 48, "gambler", 14),
		new LifeEvent("any", "{n} bought a new rifle from the gunsmith in {t}", "the new rifle", "Proud", 1, 48, "notarmy", 10),
		new LifeEvent("any", "{n} had his horse throw a shoe miles from anywhere", "the long walk", "Bitter", -1, 24, null, 10),
		new LifeEvent("any", "{n} watched a hanging in {t}", "the hanging", "Shaken", -2, 72, null, 10),
		new LifeEvent("any", "{n} helped pull a wagon out of the mud", "a good turn", "Steady", 1, 24, "friendly", 10),
		new LifeEvent("any", "{n} was kicked by a mule", "the mule's kick", "Hurting", -2, 48, null, 6, "rest:12"),
		new LifeEvent("any", "{n} ate bad meat and was sick for two days", "the bad meat", "Sick", -2, 48, null, 8, "rest:24"),
		new LifeEvent("any", "{n} saw a ghost by the old church, he swears it", "what he saw by the church", "Shaken", -2, 96, "superstitious", 18, "fear:3"),
		new LifeEvent("any", "{n} lit a candle for his mother in {t}", "his mother", "Homesick", -1, 48, "devout|catholic", 8),
		new LifeEvent("any", "{n} got a letter saying his father died", "his father, gone", "Grieving", -4, 120, null, 4),
		new LifeEvent("any", "{n} read about you in the newspaper", "what the paper said about you", "Shaken", -1, 48, "knows", 8, "fear:2"),
		new LifeEvent("any", "{n} bragged about you in a saloon and got laughed at", "the laughing", "Humiliated", -2, 48, "braggart&knows", 14, "hate:3"),
		new LifeEvent("any", "{n} sat up all night with a bottle, thinking about you", "you", "Bitter", -1, 48, "hate>=vengeful&drunk", 14, "hate:2"),
		new LifeEvent("any", "{n} told a stranger he'd seen you, for a dollar", "the dollar", "Flush", 1, 24, "traitor|greedy", 8),
		new LifeEvent("any", "{n} took a bath and a shave in {t}", "a clean shave", "Joyful", 1, 24, null, 10),
		new LifeEvent("any", "{n} got his fortune told in {t}", "what the fortune teller said", "Shaken", -1, 72, "superstitious", 14),
		new LifeEvent("any", "{n} hurt a man badly in a fight over nothing", "the man he hurt", "Steady", 0, 48, "sadist", 16, "scar:beat"),
		new LifeEvent("any", "{n} carved his initials into a tree on the trail", "home", "Steady", 0, 24, null, 10),
		new LifeEvent("any", "{n} sold his old horse and bought a better one", "the new horse", "Joyful", 2, 48, "rich|greedy", 8),
		new LifeEvent("any", "{n} lost his rifle crossing a river", "the lost rifle", "Bitter", -2, 48, null, 6),
		new LifeEvent("any", "{n} caught a thief in his camp and let him go", "the thief he let go", "Steady", 1, 24, "friendly", 8),
		new LifeEvent("any", "{n} caught a thief in his camp and beat him bloody", "the thief he beat", "Proud", 1, 24, "aggr>=60", 8),
		new LifeEvent("any", "{n} spent a night in a cave waiting out a storm", "the storm", "Shaken", -1, 24, null, 10),
		new LifeEvent("any", "{n} stayed awake after a bad dream about the war", "the war", "Shaken", -2, 72, "rank>=1", 4),
		new LifeEvent("any", "{n} wrote a will, just in case", "dying", "Shaken", -1, 72, "fear>=40", 8),
		new LifeEvent("any", "{n}'s youngest got sick back home", "his sick child", "Grieving", -3, 96, "married&children", 10, "fear:2"),
		new LifeEvent("any", "{n} sent his wages home to his wife", "his family", "Steady", 1, 48, "married", 10),
		new LifeEvent("any", "{n}'s wife wrote, asking him to come home", "his wife's letter", "Homesick", -2, 96, "married", 10),
		new LifeEvent("any", "{n} got a dog from a farmer near {t}", "the new dog", "Joyful", 2, 72, "nopet", 4, "pet"),
		new LifeEvent("any", "{n} fell asleep on watch and was caught", "falling asleep on watch", "Humiliated", -2, 48, "notarmy", 6),
		new LifeEvent("gang", "{n} and {f} rode out to rob a lone rider", "the lone rider", "Flush", 1, 36, null, 10),
		new LifeEvent("gang", "{n} was sent to scout the roads near {t}", "scouting", "Steady", 0, 24, null, 10),
		new LifeEvent("gang", "{n} argued with {b} about the split from a job", "the split", "Bitter", -2, 72, null, 8, "hate:1"),
		new LifeEvent("gang", "{n} won {b}'s favour on a job", "{b}'s favour", "Proud", 2, 72, null, 10),
		new LifeEvent("gang", "{n} buried a man of the {g} in the hills", "the man he buried", "Grieving", -2, 96, null, 6),
		new LifeEvent("gang", "{n} stood guard all night while the {g} slept", "the long watch", "Steady", 0, 24, null, 10),
		new LifeEvent("gang", "{n} stole a crate of whiskey off a wagon", "the whiskey", "Joyful", 2, 48, "drunk", 10),
		new LifeEvent("gang", "{n} got a new tattoo in {t}", "the new tattoo", "Proud", 1, 48, null, 10),
		new LifeEvent("gang", "{n} taught a younger man of the {g} to shoot", "the young one", "Proud", 1, 48, "rank>=2", 10),
		new LifeEvent("gang", "{n} was caught skimming from the {g}'s take", "being caught skimming", "Humiliated", -3, 96, "greedy|traitor", 8, "scar:beat"),
		new LifeEvent("odriscoll", "{n} drank to Colm's health in {t}", "Colm O'Driscoll", "Steady", 1, 24, null, 10),
		new LifeEvent("odriscoll", "{n} heard Colm was looking to settle with Dutch", "the old feud", "Bitter", -1, 48, null, 8, "hate:1"),
		new LifeEvent("odriscoll", "{n} sang Irish songs in camp till dawn", "the old songs", "Homesick", 1, 24, null, 10),
		new LifeEvent("raider", "{n} rode in a parade of old veterans outside {t}", "the old cause", "Proud", 2, 48, null, 10),
		new LifeEvent("raider", "{n} burned a Union flag in camp", "the old war", "Bitter", 0, 48, null, 10),
		new LifeEvent("raider", "{n} argued with a Braithwaite man over money", "the Braithwaites", "Bitter", -1, 48, null, 10),
		new LifeEvent("klan", "{n} hid his robes from his wife", "his wife finding out", "Shaken", -1, 48, "married", 10),
		new LifeEvent("klan", "{n} was laughed at in {t} - folks know what he is", "being known", "Humiliated", -2, 72, null, 10),
		new LifeEvent("army", "{n} stood drill in the rain all morning", "the drill", "Bitter", -1, 24, null, 10),
		new LifeEvent("army", "{n} was promised leave and then refused it", "the leave he lost", "Bitter", -2, 72, null, 10),
		new LifeEvent("army", "{n} shot a rattlesnake on patrol", "the snake", "Steady", 0, 24, null, 10),
		new LifeEvent("army", "{n} lost a bet on the regiment's horse race", "the horse race", "Bitter", -1, 24, null, 10),
		new LifeEvent("army", "{n} was praised by Colonel Favours", "the colonel's praise", "Proud", 2, 72, null, 6),
		new LifeEvent("army", "{n} guarded prisoners at the fort all night", "the prisoners", "Steady", 0, 24, null, 10),
		new LifeEvent("loner", "{n} did a day's work at the stables in {t}", "the stables", "Steady", 1, 24, null, 10),
		new LifeEvent("loner", "{n} slept rough outside {t}", "the cold ground", "Bitter", -1, 24, "poor", 10),
		new LifeEvent("loner", "{n} bought a round for strangers in {t}", "the strangers", "Joyful", 1, 24, "rich|friendly", 10),
		new LifeEvent("loner", "{n} was cheated at cards in {t}", "the cheat", "Bitter", -2, 48, "gambler|dim", 10),
		new LifeEvent("loner", "{n} hunted a deer and sold the hide in {t}", "the hide", "Flush", 1, 24, null, 10),
		new LifeEvent("wapiti", "{n} saw soldiers cross the river near the reservation", "the soldiers", "Bitter", -2, 48, null, 10),
		new LifeEvent("wapiti", "{n} told the old stories to the children", "the old stories", "Steady", 1, 48, null, 10),
		new LifeEvent("wapiti", "{n} brought back a good elk", "the elk", "Proud", 2, 48, null, 10)
	};

	public static LifeEvent[] All = BuiltIn;

	public const int ContentVersion = 2;

	public static int AddedFromBuiltIn;

	public static readonly List<string> Recent = new List<string>();

	public static LifeContent Export()
	{
		return new LifeContent
		{
			Version = 2,
			Rows = BuiltIn.Select((LifeEvent e) => new LifeContentRow
			{
				Id = e.Id,
				Who = e.Who,
				Title = e.Title,
				Thought = e.Thought,
				Mood = e.Mood,
				Weight = e.Weight,
				Hours = e.Hours,
				When = e.When,
				Odds = e.Odds,
				Effects = e.Effects.ToList()
			}).ToList()
		};
	}

	public static int Import(LifeContent c)
	{
		if (c?.Rows == null || c.Rows.Count == 0)
		{
			return 0;
		}
		List<LifeEvent> list = (from x in c.Rows
			where !string.IsNullOrEmpty(x.Title)
			select new LifeEvent(x.Id, x.Who ?? "any", x.Title, x.Thought ?? x.Title, x.Mood ?? "Steady", x.Weight, Math.Max(1, x.Hours), x.When, Math.Max(0, (x.Odds == 0) ? 10 : x.Odds), (x.Effects ?? new List<string>()).ToArray())).ToList();
		HashSet<string> have = new HashSet<string>(list.Select((LifeEvent e) => e.Id));
		HashSet<string> removed = new HashSet<string>(c.Removed ?? new List<string>());
		List<LifeEvent> list2 = BuiltIn.Where((LifeEvent e) => !have.Contains(e.Id) && !removed.Contains(e.Id)).ToList();
		AddedFromBuiltIn = list2.Count;
		list.AddRange(list2);
		All = list.ToArray();
		return All.Length;
	}

	public static LifeEvent Pick(NemesisRecord r, Random rng)
	{
		HashSet<string> mine = new HashSet<string>(from h in (r.History ?? new List<HistoryEntry>()).Skip(Math.Max(0, (r.History?.Count ?? 0) - 30))
			select h.Text ?? "");
		List<LifeEvent> list = All.Where((LifeEvent e) => e.Fits(r) && !Recent.Contains(e.Id) && (Key(e).Length < 6 || !mine.Any((string t) => t.Contains(Key(e))))).ToList();
		if (list.Count == 0)
		{
			list = All.Where((LifeEvent e) => e.Fits(r)).ToList();
		}
		if (list.Count == 0)
		{
			return null;
		}
		int maxValue = list.Sum((LifeEvent e) => e.Odds);
		int num = rng.Next(maxValue);
		foreach (LifeEvent item in list)
		{
			num -= item.Odds;
			if (num < 0)
			{
				return item;
			}
		}
		return list[list.Count - 1];
		static string Key(LifeEvent e)
		{
			return e.Title.Replace("{n}'s ", "").Replace("{n} ", "").Replace("{n}", "")
				.Split('{')[0].Trim();
		}
	}
}
