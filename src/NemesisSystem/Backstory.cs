using System;
using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

public static class Backstory
{
	public class Ctx
	{
		public int Year;

		public int Month;

		public int Age;

		public int JoinMonth;

		public string Born;

		public string Home;

		public bool Irish;

		public bool IrishBorn;

		public bool South;

		public bool North;

		public Random Rng;

		public NemesisRecord R;
	}

	internal static string[] IrishCounties = new string[10] { "County Cork", "County Kerry", "County Galway", "County Mayo", "County Clare", "County Donegal", "County Limerick", "County Sligo", "County Wexford", "County Tipperary" };

	internal static string[] IrishAmerican = new string[3] { "Five Points, New York, to Irish parents", "Boston, to Irish parents", "Saint Denis, to Irish dock hands" };

	internal static string[] Southern = new string[10] { "Scarlett Meadows, Lemoyne", "Bluewater Marsh, Lemoyne", "Bayou Nwa, Lemoyne", "Rhodes, Lemoyne", "Saint Denis, Lemoyne", "Georgia", "Alabama", "Mississippi", "the Carolinas", "Tennessee" };

	internal static string[] Northern = new string[11]
	{
		"Ohio", "Pennsylvania", "New York", "Massachusetts", "Indiana", "Illinois", "Michigan", "Kentucky", "Blackwater, West Elizabeth", "Valentine, New Hanover",
		"Bavaria - came over as a boy"
	};

	internal static string[] Frontier = new string[18]
	{
		"Valentine, New Hanover", "Annesburg, New Hanover", "Emerald Ranch, New Hanover", "Van Horn, New Hanover", "Blackwater, West Elizabeth", "Strawberry, West Elizabeth", "Big Valley, West Elizabeth", "Rhodes, Lemoyne", "Saint Denis, Lemoyne", "Armadillo, New Austin",
		"Tumbleweed, New Austin", "the Ambarino high country", "New York", "Boston", "Philadelphia", "Missouri", "Kansas", "Ohio"
	};

	internal static string[] Towns = new string[8] { "Valentine", "Rhodes", "Saint Denis", "Strawberry", "Blackwater", "Van Horn", "Annesburg", "Armadillo" };

	internal static string[] IrishWives = new string[8] { "Mary", "Bridget", "Kathleen", "Nora", "Margaret", "Eileen", "Siobhan", "Honora" };

	internal static string[] IrishHusbands = new string[8] { "Patrick", "Michael", "Sean", "Thomas", "Daniel", "Cornelius", "Dermot", "Owen" };

	internal static string[] Husbands = new string[10] { "John", "William", "Samuel", "Henry", "Thomas", "Charles", "Jacob", "Walter", "Amos", "Eli" };

	internal static string[] Wives = new string[12]
	{
		"Sarah", "Martha", "Elizabeth", "Anna", "Clara", "Emma", "Lucy", "Ruth", "Hattie", "Ida",
		"Mae", "Josephine"
	};

	internal static (string battle, int year)[] GreyBattles = new(string, int)[8]
	{
		("Shiloh", 1862),
		("Antietam", 1862),
		("Chickamauga", 1863),
		("Gettysburg", 1863),
		("the Wilderness", 1864),
		("Franklin", 1864),
		("Atlanta", 1864),
		("Petersburg", 1864)
	};

	internal static (string battle, int year)[] BlueBattles = new(string, int)[7]
	{
		("Shiloh", 1862),
		("Antietam", 1862),
		("Vicksburg", 1863),
		("Gettysburg", 1863),
		("the Wilderness", 1864),
		("Atlanta", 1864),
		("Petersburg", 1865)
	};

	internal static LifeT[] Boyhood = new LifeT[25]
	{
		L("youth", "any", 6, 14, 10, "Raised on a hardscrabble farm outside {home}", "greedy:1 cold:1"),
		L("youth", "man", 6, 14, 8, "His father drank and used his fists", "hothead:3 coward:2", "surly aggr:+4"),
		L("youth", "woman", 6, 14, 8, "Her father drank, and beat whoever was nearest", "hothead:3 coward:2", "surly aggr:+4"),
		L("youth", "any", 4, 12, 6, "Orphaned young - an uncle worked him like a mule", "cold:3 drifter:2 loyal:2"),
		L("youth", "any", 6, 14, 8, "Raised in the church by a God-fearing mother", "coward:1", "devout friendly"),
		L("youth", "any", 8, 14, 8, "Ran wild with the older boys in {home}", "hothead:2 proud:1", "aggr:+3"),
		L("youth", "poor", 6, 14, 7, "Went hungry most winters", "greedy:3 cold:1"),
		L("youth", "man", 6, 14, 6, "A sickly boy, bullied by his brothers", "coward:3 proud:2"),
		L("youth", "woman", 6, 14, 6, "A sickly girl the other children made fun of", "coward:3 proud:2"),
		L("youth", "any", 8, 14, 6, "Could shoot a rabbit at fifty paces before he could read", "proud:1", "aggr:+2"),
		L("youth", "any", 7, 14, 5, "A schoolmarm beat his letters into him - he reads better than most", "cold:1", "sharp"),
		L("youth", "irish", 6, 14, 8, "Grew up on stories of the Famine and the landlords", "proud:2 loyal:2", "surly"),
		L("youth", "south", 5, 14, 9, "Watched Union troops burn his family's barn", "hothead:3 cold:2", "surly aggr:+5", pub: false, 1849, 1859),
		L("youth", "north|man", 6, 14, 6, "Son of a Union veteran who never spoke of the war", "cold:2 loyal:2"),
		L("youth", "north|woman", 6, 14, 6, "Daughter of a Union veteran who never spoke of the war", "cold:2 loyal:2"),
		L("youth", "any", 3, 10, 6, "Lost his mother when he was small", "cold:2 drifter:2"),
		L("youth", "rich", 6, 14, 10, "Born into money - and never let anyone forget it", "proud:4 greedy:2"),
		L("youth", "any", 6, 14, 5, "His family lost everything in a fire", "greedy:2 coward:1"),
		L("youth", "wapiti", 5, 13, 10, "Followed the herds with his family across the northern plains", "drifter:2 proud:1", "", pub: false, 0, 1882),
		L("youth", "wapiti", 4, 12, 9, "Rode before he could walk far", "proud:2", "aggr:+2"),
		L("youth", "wapiti", 8, 14, 9, "Learned to hunt elk in the Grizzlies from his uncle", "cold:1 loyal:1"),
		L("youth", "wapiti", 6, 14, 9, "Grew up hungry, the herds thinning every year", "cold:2 greedy:1", "surly", pub: false, 1872),
		L("youth", "wapiti", 4, 13, 7, "Lost his father to the soldiers when he was a boy", "hothead:3 cold:2", "surly aggr:+4", pub: false, 0, 1886),
		L("youth", "wapiti", 6, 14, 8, "Sat with the old men by the fire for their stories every night", "loyal:3", "friendly"),
		L("youth", "wapiti", 8, 14, 5, "Was sent east to a government school - and ran away home", "proud:3 hothead:1", "surly", pub: false, 1872)
	};

	internal static LifeT[] Youth = new LifeT[25]
	{
		L("youth", "any", 14, 19, 8, "Left home young and never wrote back", "drifter:3 cold:2"),
		L("youth", "man", 16, 21, 6, "Rode drag on a cattle drive into Valentine's stockyards", "drifter:1", "aggr:+2"),
		L("youth", "man", 15, 19, 6, "Won his first knife fight before he was twenty", "hothead:2 proud:3", "aggr:+5"),
		L("youth", "man", 15, 20, 5, "Ran from his first real fight - he's never lived it down", "coward:4 proud:2", "fear:+4"),
		L("youth", "man", 14, 18, 5, "Apprenticed to a blacksmith in {home}", "cold:1"),
		L("youth", "man", 16, 21, 5, "Lost the girl he loved to a richer man", "proud:2 hothead:1", "surly"),
		L("youth", "any", 13, 17, 5, "Caught stealing chickens; the beating taught him nothing", "greedy:2 hothead:1"),
		L("youth", "irish", 13, 19, 10, "Ran with a street gang in the Five Points", "loyal:2 hothead:2", "aggr:+4", pub: false, 0, 0, "New York"),
		L("youth", "army", 15, 18, 10, "Lied about his age to join up", "proud:2 loyal:2"),
		L("youth", "south", 8, 16, 8, "Grew up on his grandfather's tales of the Lost Cause", "proud:3", "surly", pub: false, 1858),
		L("youth", "trade:Gambler", 16, 21, 15, "Won his first stake at faro on a Saint Denis riverboat", "greedy:3 proud:1"),
		L("youth", "trade:Miner", 12, 15, 15, "Went down the mine at fourteen", "cold:2", "surly"),
		L("youth", "any", 16, 21, 4, "Fell in with a travelling medicine show", "drifter:2 greedy:1", "sharp"),
		L("youth", "wapiti", 14, 18, 9, "Broke his first wild horse at fifteen", "proud:2", "aggr:+2"),
		L("youth", "wapiti", 14, 20, 8, "Went hungry the winter the government's rations never came", "cold:2 hothead:1", "surly"),
		L("youth", "wapiti", 15, 21, 7, "Traded furs at a post on the Dakota River", "greedy:1"),
		L("youth", "wapiti", 15, 21, 7, "Saw a friend shot by soldiers", "hothead:3 cold:1", "aggr:+4"),
		L("youth", "wapiti", 13, 18, 7, "Fasted alone in the mountains to find his path", "proud:1 cold:1", "devout"),
		L("youth", "wapiti", 15, 20, 5, "Ran from his first fight with soldiers, and has never forgiven himself", "coward:3 proud:2", "fear:+4"),
		L("youth", "woman", 12, 18, 8, "Kept house for her father and brothers after her mother died", "cold:2 loyal:2"),
		L("youth", "woman", 12, 17, 6, "Learned to shoot from her father - better than any of her brothers", "proud:3", "aggr:+3"),
		L("youth", "woman", 15, 19, 5, "Ran off with a travelling theatre company at seventeen", "drifter:3 proud:1"),
		L("youth", "woman", 13, 17, 5, "Went to work in a Saint Denis textile mill at thirteen", "cold:2", "surly"),
		L("youth", "woman", 10, 16, 4, "Taught herself to read from a stolen Bible", "proud:2", "sharp"),
		L("youth", "woman", 16, 21, 5, "Turned down a rich man's proposal - her family never forgave her", "proud:3 hothead:1")
	};

	internal static LifeT[] Work = new LifeT[72]
	{
		L("work", "trade:Miner", 18, 50, 12, "Worked the Annesburg coal mine, year after year", "cold:2", "surly"),
		L("work", "trade:Miner", 20, 50, 8, "Lived through a cave-in that buried six men", "coward:2 cold:2", "fear:+5"),
		L("work", "trade:Farmer", 18, 50, 10, "Sharecropped land he'd never own", "greedy:2", "surly"),
		L("work", "trade:Farmer", 20, 55, 6, "Lost a whole harvest to drought", "greedy:1 cold:1"),
		L("work", "trade:Rancher", 22, 55, 10, "Built a small spread outside {home}", "proud:2"),
		L("work", "trade:Rancher", 22, 55, 7, "Shot a rustler off his land", "hothead:2 proud:2", "aggr:+4"),
		L("work", "trade:Cowhand", 18, 45, 12, "Drove herds into Valentine for the Heartlands outfits", "drifter:2"),
		L("work", "trade:Railroad man", 18, 50, 12, "Laid track for Cornwall's railroad, for pennies", "greedy:2", "surly"),
		L("work", "trade:Railroad man", 20, 50, 6, "Lost two fingers coupling cars", "coward:1 cold:1"),
		L("work", "trade:Dockworker", 18, 50, 12, "Hauled crates on the Saint Denis docks under Bronte's men", "cold:1 greedy:1 coward:1"),
		L("work", "trade:Fisherman", 18, 55, 12, "Fished the Lannahechee for a living", "drifter:1"),
		L("work", "trade:Lumberjack", 18, 50, 12, "Felled timber in the Roanoke Ridge logging camps", "cold:1", "aggr:+2"),
		L("work", "trade:Hunter|trade:Trapper", 18, 55, 12, "Trapped beaver and fox up in the Grizzlies", "cold:2 drifter:2"),
		L("work", "trade:Hunter|trade:Trapper", 20, 55, 6, "Mauled by a bear - he still carries the marks", "proud:2", "fear:+4"),
		L("work", "trade:Gambler", 20, 55, 12, "Ran card games on the Saint Denis riverboats", "greedy:3"),
		L("work", "trade:Gambler", 22, 55, 8, "Owes money to the wrong people in Saint Denis", "greedy:2 coward:2", "fear:+6"),
		L("work", "trade:Gentleman", 22, 60, 12, "Made his money in Lemoyne cotton", "proud:3 greedy:2"),
		L("work", "trade:Gentleman", 22, 60, 6, "Lost a fortune at the card tables and won half of it back", "greedy:2 proud:1"),
		L("work", "trade:Preacher", 20, 60, 14, "Preached hellfire at tent revivals across the territories", "proud:2", "devout"),
		L("work", "trade:Doctor|man", 22, 60, 14, "Learned his medicine sawing off legs in field hospitals", "cold:2", "sharp", pub: false, 0, 1845),
		L("work", "trade:Doctor", 22, 60, 10, "Studied medicine back East; the frontier wasn't what he pictured", "cold:1 proud:1", "sharp"),
		L("work", "trade:Townsman", 20, 60, 10, "Kept a dry goods store in {home}", "greedy:2 coward:1"),
		L("work", "trade:Townsman", 20, 60, 7, "Clerked at the bank in {town}", "cold:1", "sharp"),
		L("work", "trade:Vagrant", 20, 60, 12, "Lost his trade to the bottle", "drifter:3 coward:1", "dim"),
		L("work", "trade:Vagrant", 18, 60, 10, "Rode the boxcars from Blackwater to Annesburg and back", "drifter:3"),
		L("work", "trade:Labourer", 18, 55, 10, "Dug ditches and hauled stone wherever there was pay", "drifter:1"),
		L("work", "trade:Outlaw|trade:Drifter", 18, 45, 8, "Rode with a crew of road agents down in New Austin", "hothead:1 cold:1", "aggr:+5"),
		L("work", "trade:Drifter", 18, 50, 8, "Drifted from town to town, never more than a season", "drifter:4"),
		L("work", "trade:Blacksmith", 18, 60, 12, "Kept the forge in {home} and shod half the horses in the county", "proud:1 cold:1", "aggr:+2"),
		L("work", "trade:Blacksmith", 22, 60, 8, "Took over the smithy when the old smith's hands gave out", "loyal:2"),
		L("work", "trade:Blacksmith", 20, 60, 6, "Mended wagon wheels and plough shares for every farm around {home}", "cold:1"),
		L("youth", "trade:Blacksmith|woman", 12, 17, 15, "Learned the forge at her father's side - she could shoe a horse before she could sew", "proud:2", "aggr:+2"),
		L("work", "trade:Blacksmith|woman", 20, 60, 8, "Worked the hammer in {home} until the men who laughed at her brought her their horses", "proud:3 hothead:1", "aggr:+2"),
		L("work", "trade:Tailor", 18, 60, 12, "Cut suits for the gentlemen of Saint Denis in a shop off the avenue", "greedy:1 proud:1", "sharp"),
		L("work", "trade:Tailor", 16, 60, 8, "Mended coats and trousers in {home} for whoever could pay", "cold:1 greedy:1"),
		L("work", "trade:Doctor|woman", 22, 60, 10, "Studied at the Woman's Medical College in Philadelphia, then came west where a doctor is a doctor", "proud:2", "sharp"),
		L("work", "trade:Seamstress", 16, 60, 12, "Took in sewing in {home} to keep a roof over her head", "cold:1 greedy:1"),
		L("work", "trade:Seamstress", 18, 60, 8, "Sewed gowns for the society ladies of Saint Denis in a shop off the avenue", "greedy:1 proud:1", "sharp"),
		L("work", "trade:Laundress", 14, 60, 12, "Washed the miners' clothes in Annesburg for a nickel a load", "cold:2", "surly"),
		L("work", "trade:Laundress", 16, 60, 8, "Scrubbed sheets in a hotel laundry in {town} from dawn to dark", "coward:1 cold:1"),
		L("work", "trade:Schoolteacher", 18, 50, 12, "Taught the one-room schoolhouse outside {home}", "loyal:2", "sharp friendly"),
		L("work", "trade:Schoolteacher", 18, 45, 8, "Came west from back East to teach school, and stayed", "proud:1 drifter:1", "sharp"),
		L("work", "trade:Shopkeeper|woman", 22, 65, 12, "Kept the general store in {town}, and kept the books better than the men", "greedy:2 proud:1", "sharp"),
		L("work", "trade:Shopkeeper", 25, 65, 8, "Ran the family store in {home} after the old man's heart gave out", "loyal:2 cold:1"),
		L("work", "trade:Cook", 16, 60, 12, "Cooked at a boarding house in {town} for the railroad men", "cold:1"),
		L("work", "trade:Cook|trade:Camp cook", 18, 60, 10, "Cooked for a cattle outfit on the trail up from Texas", "drifter:2", "aggr:+2"),
		L("work", "trade:Camp cook", 18, 60, 10, "Fed the loggers in the Roanoke Ridge camps out of one iron pot", "cold:1 proud:1"),
		L("work", "trade:Housemaid", 13, 50, 12, "Went into service in a big house in Saint Denis at thirteen", "coward:1 loyal:1"),
		L("work", "trade:Housemaid", 16, 50, 7, "Was let go without a reference for a broken vase she never touched", "hothead:2 cold:1", "surly"),
		L("work", "trade:Boarding-house keeper", 25, 65, 12, "Kept a boarding house in {town} - drummers, drifters and railroad men", "greedy:2 cold:1", "sharp"),
		L("work", "trade:Milliner", 18, 60, 12, "Trimmed hats for the ladies of {town}, feathers and all", "proud:2"),
		L("work", "trade:Telegraph operator", 17, 45, 12, "Worked the telegraph key at the {town} station - he hears everything first", "cold:1", "sharp"),
		L("work", "trade:Nurse", 18, 60, 12, "Nursed fever patients at the Saint Denis hospital", "loyal:2 cold:1", "friendly"),
		L("work", "trade:Nurse", 20, 60, 8, "Delivered half the babies born in {home}", "loyal:2", "friendly"),
		L("work", "trade:Nun", 17, 70, 14, "Took her vows at the convent in Saint Denis", "loyal:2 coward:1", "devout"),
		L("work", "trade:Nun", 20, 70, 8, "Runs the soup line for the poor of Saint Denis", "loyal:3", "devout friendly"),
		L("work", "trade:Preacher's wife", 20, 65, 12, "Followed her husband's ministry from one tent revival to the next", "loyal:2", "devout"),
		L("work", "trade:Lady", 18, 65, 12, "Married into Lemoyne cotton money", "proud:3 greedy:2"),
		L("work", "trade:Lady", 20, 65, 8, "Raised money for the Saint Denis orphans' home at her own charity balls", "proud:2", "friendly"),
		L("work", "trade:Lady|trade:Schoolteacher|trade:Seamstress|woman", 20, 65, 6, "Marched for the vote with the suffragists in Saint Denis", "proud:3 hothead:1", "sharp"),
		L("work", "trade:Saloon girl", 16, 40, 12, "Danced for drinks at a Valentine saloon", "cold:2 greedy:1", "surly"),
		L("work", "trade:Saloon girl", 17, 40, 8, "Worked upstairs at a saloon in {town}, and saved every coin she could hide", "cold:2 greedy:2"),
		L("work", "trade:Showgirl", 16, 45, 12, "Sang on the stage of a Saint Denis theatre", "proud:3", "friendly"),
		L("work", "trade:Showgirl", 17, 45, 8, "Toured the river towns with a travelling show", "drifter:2 proud:1"),
		L("work", "trade:Homesteader", 20, 65, 12, "Proved up a homestead claim near {home} with her own two hands", "proud:2 cold:1", "aggr:+2"),
		L("work", "trade:Homesteader|trade:Farm wife", 20, 65, 10, "Sold eggs and butter in {town} on market days", "greedy:1"),
		L("work", "trade:Farm wife", 18, 65, 12, "Worked her husband's land from first light, then cooked for the hands", "cold:2 loyal:1"),
		L("work", "trade:Farm wife|trade:Homesteader", 22, 65, 6, "Lost a whole harvest to drought, and planted again", "proud:1 cold:1"),
		L("work", "trade:Market seller", 16, 65, 12, "Sold oysters and pralines at the Saint Denis market", "greedy:1", "friendly"),
		L("work", "trade:Market seller", 18, 65, 8, "Sold fish off the dock at Lagras before dawn", "cold:1"),
		L("work", "trade:Traveller", 20, 65, 12, "Came west by train to start over where nobody knew her name", "drifter:2 cold:1"),
		L("work", "trade:Traveller", 20, 65, 8, "Rode the line from Saint Denis to Blackwater to see her sister", "loyal:2")
	};

	internal static LifeT[] Crime = new LifeT[8]
	{
		L("crime", "any", 18, 50, 10, "Did {n} years in Sisika Penitentiary for robbery", "hothead:2 cold:2", "surly", pub: true, 0, 0, null, "CRIME", "{name} sent to Sisika Penitentiary", mirrorHeard: true),
		L("crime", "any", 18, 50, 8, "Shot a man dead over cards in Van Horn", "hothead:3", "aggr:+6", pub: false, 0, 0, null, "CRIME", "A card game in Van Horn ends in a killing"),
		L("crime", "any", 18, 45, 6, "Broke out of the Strawberry jail", "proud:2 hothead:1", "", pub: true, 0, 0, null, "CRIME", "Jailbreak in Strawberry - {name} gets away", mirrorHeard: true),
		L("crime", "any", 18, 45, 6, "His face is on an old wanted poster in New Austin", "cold:1 proud:1", "", pub: true),
		L("crime", "south", 18, 50, 8, "Ran moonshine through the bayou for a bootlegger", "greedy:2"),
		L("crime", "any", 18, 45, 7, "Rustled horses across the Heartlands", "greedy:2 drifter:1"),
		L("crime", "any", 20, 50, 6, "Got away with a payroll - and drank the lot", "greedy:2 hothead:1"),
		L("crime", "any", 18, 45, 7, "Held up a stagecoach on the road outside Strawberry", "greedy:2 cold:1", "", pub: false, 0, 0, null, "CRIME", "Stagecoach held up outside Strawberry", mirrorHeard: true)
	};

	internal static LifeT[] GangLife = new LifeT[43]
	{
		L("gang", "odriscoll", 18, 50, 8, "Swore himself to Colm after the Van der Linde feud cost him a friend", "loyal:3 hothead:1"),
		L("gang", "odriscoll", 18, 50, 7, "Owes his life to Colm O'Driscoll - and Colm never lets him forget it", "loyal:4"),
		L("gang", "odriscoll", 18, 50, 6, "Kept watch through a winter up at Six Point Cabin", "cold:1 loyal:1"),
		L("gang", "odriscoll", 18, 50, 6, "Fences stolen horses in Valentine for the gang", "greedy:2"),
		L("gang", "lemoyneraiders", 18, 60, 8, "Burned out a carpetbagger's store in Rhodes", "hothead:2 proud:2", "aggr:+4"),
		L("gang", "lemoyneraiders", 18, 60, 7, "Guards moonshine stills in Bluewater Marsh", "cold:1 greedy:1"),
		L("gang", "lemoyneraiders", 18, 60, 7, "Swears he'll never salute the Union flag", "proud:3"),
		L("gang", "army", 18, 55, 10, "Posted to Fort Wallace under Colonel Favours", "loyal:2 cold:1", "", pub: true),
		L("gang", "army", 20, 40, 7, "Rode with the cavalry on the plains campaigns", "cold:2 loyal:2", "aggr:+4", pub: false, 0, 1870),
		L("gang", "army", 18, 50, 5, "Disciplined for drinking on watch", "hothead:1 coward:1"),
		L("gang", "army", 18, 50, 5, "Escorted a Cornwall payroll through Roanoke Ridge", "loyal:1"),
		L("gang", "army", 18, 50, 6, "Saw his sergeant shot by Raiders near Rhodes", "hothead:2 cold:2", "aggr:+4"),
		L("gang", "klan", 25, 60, 8, "Lost his land to the bank and blamed everyone but himself", "greedy:2 coward:2", "surly"),
		L("gang", "klan", 20, 60, 8, "Wears the hood because his father did", "coward:2 loyal:2"),
		L("gang", "laramie", 17, 50, 9, "Was a cowhand on a Hennigan's Stead ranch until the owner cheated him of a season's pay", "greedy:2 hothead:1"),
		L("gang", "laramie", 18, 50, 8, "Can change a brand with a running iron faster than any man on the plains", "proud:2 cold:1", "", pub: true),
		L("gang", "laramie", 18, 55, 7, "Drove cattle up the trail from Texas and never went back", "drifter:2"),
		L("gang", "laramie", 18, 50, 7, "Rode with the rustlers who ran off the MacFarlane herd", "greedy:1 proud:1", "", pub: true),
		L("gang", "laramie", 20, 55, 6, "Lost his own small herd to a hard winter, and took other men's after", "cold:2 greedy:1"),
		L("gang", "laramie", 17, 45, 6, "Followed his older brother into the Laramie boys", "loyal:3"),
		L("gang", "laramie", 20, 55, 5, "Was nearly hanged by a cattlemen's posse in the Great Plains", "coward:1 cold:1", "", pub: true),
		L("gang", "delobo", 17, 50, 9, "Rode with bandits on both sides of the river since he was fifteen", "hothead:1 proud:1"),
		L("gang", "delobo", 18, 55, 8, "Left Nuevo Paraiso one step ahead of the rurales", "coward:1 cold:1"),
		L("gang", "delobo", 18, 50, 7, "Robbed his first train near Rio Bravo", "greedy:2", "", pub: true),
		L("gang", "delobo", 18, 55, 7, "Fought in the troubles across the border before the Del Lobos took him in", "cold:2 loyal:1"),
		L("gang", "delobo", 20, 55, 6, "Smuggles guns across the river for the jefe", "greedy:1 loyal:1"),
		L("gang", "delobo", 17, 45, 6, "Swore himself to the jefe after the jefe saved his life in Gaptooth Ridge", "loyal:3"),
		L("gang", "delobo", 20, 55, 6, "His face is on wanted posters in Armadillo and in Chuparosa both", "proud:2", "", pub: true),
		L("gang", "delobo", 25, 60, 5, "Lost his farm across the river to a rich man's lawyers", "hothead:2 greedy:1"),
		L("gang", "nightfolk", 17, 60, 9, "Was born on a houseboat in Bayou Nwa and has never slept on dry land", "cold:1 loyal:1"),
		L("gang", "nightfolk", 17, 50, 8, "Was taken in by the Night Folk as a boy, after the fever took his family", "loyal:3"),
		L("gang", "nightfolk", 20, 60, 7, "Stopped going to church in Saint Denis the year the lanterns first came", "cold:2"),
		L("gang", "nightfolk", 18, 60, 7, "Swore himself to the elder in the dark, and has never seen his face", "loyal:2 coward:1"),
		L("gang", "nightfolk", 18, 50, 7, "Lost a brother to the Lemoyne Raiders on the bayou road", "hothead:2", "aggr:+4"),
		L("gang", "nightfolk", 20, 60, 6, "Knows every channel from Lagras to Shady Belle by the sound of the water", "cold:1 proud:1"),
		L("gang", "nightfolk", 20, 55, 6, "Left a warning in the cypress that Saint Denis still talks about", "proud:2", "", pub: true),
		L("gang", "nightfolk", 25, 60, 5, "Hasn't spoken a full sentence since the night of the big flood", "cold:2"),
		L("gang", "wapiti", 17, 45, 9, "Rides with the young men who are done waiting on the Army's promises", "hothead:2 proud:2", "aggr:+4", pub: true),
		L("gang", "wapiti", 18, 55, 8, "Keeps watch on Fort Wallace from the ridges above it", "cold:2 loyal:1"),
		L("gang", "wapiti", 17, 45, 7, "Took back horses from the Army's corrals", "proud:2 greedy:1", "", pub: true),
		L("gang", "wapiti", 18, 60, 8, "Listens to Rains Fall, though his heart wants to fight", "loyal:2 coward:1"),
		L("gang", "wapiti", 18, 60, 8, "Hunts for the families when the rations run short", "loyal:3", "friendly"),
		L("gang", "wapiti", 18, 55, 6, "Followed the oil men's surveyors across Wapiti land", "cold:2")
	};

	internal static LifeT[] Adult = new LifeT[30]
	{
		L("faith", "any", 20, 60, 6, "Found God at a tent revival after a bad year", "coward:1", "devout"),
		L("faith", "any", 20, 60, 6, "Stopped going to church after his mother's funeral", "cold:2", "godless"),
		L("adult", "any", 18, 50, 6, "Won a shooting contest at a county fair", "proud:3", "aggr:+2"),
		L("adult", "man", 18, 50, 6, "Took a bullet in a saloon brawl in {town}", "hothead:2", "aggr:+3"),
		L("adult", "any", 18, 60, 6, "Lost his savings to a crooked card sharp", "greedy:3", "surly"),
		L("adult", "any", 18, 60, 4, "Pulled a drowning boy out of the river", "proud:1", "friendly"),
		L("loss", "any", 15, 60, 6, "Lost a brother to the fever", "cold:2 coward:1"),
		L("loss", "any", 18, 55, 6, "Buried his best friend after a gunfight", "cold:2 hothead:2", "aggr:+4"),
		L("faith", "wapiti", 18, 60, 8, "Keeps to the old ways, whatever the missionaries say", "proud:2", "devout"),
		L("faith", "wapiti", 18, 60, 5, "Stopped praying the winter his mother died", "cold:2", "godless"),
		L("loss", "wapiti", 15, 60, 8, "Lost a brother to the coughing sickness", "cold:2 hothead:1"),
		L("adult", "wapiti", 18, 50, 7, "Won a horse race against riders from three bands", "proud:3"),
		L("adult", "wapiti", 18, 50, 6, "Was jailed in Valentine for a fight he didn't start", "hothead:2 proud:1", "surly"),
		L("loss", "wapiti", 18, 60, 6, "Buried a friend the soldiers killed", "hothead:2 cold:2", "aggr:+4"),
		L("adult", "wapiti", 25, 60, 5, "Taught his nephews to track the way his uncle taught him", "loyal:2", "friendly"),
		L("adult", "any", 18, 60, 5, "Snakebit out in the scrub - nearly died", "coward:2", "fear:+4"),
		L("adult", "any", 22, 60, 5, "Started drinking hard and never stopped", "hothead:1 drifter:1", "dim"),
		L("adult", "man", 18, 45, 6, "Beat a bigger man bare-knuckle in {town}", "proud:3", "aggr:+3", pub: false, 0, 0, null, "BRAWL", "Bare-knuckle fight in {town} - {name} wins"),
		L("adult", "man", 18, 50, 5, "Backed down from a duel in Armadillo", "coward:4", "fear:+3"),
		L("adult", "any", 20, 60, 5, "Saves every cent he makes", "greedy:2 cold:1"),
		L("adult", "any", 18, 50, 5, "Sends money home to his mother every month", "loyal:2", "friendly"),
		L("adult", "any", 18, 55, 4, "Shot his own horse when it broke a leg, and wept", "cold:-1 coward:1", "friendly"),
		L("adult", "any", 20, 60, 4, "Sat out a blizzard alone in the Grizzlies for nine days", "cold:3 drifter:1"),
		L("adult", "woman", 20, 60, 6, "Nursed half of {town} through a diphtheria winter", "loyal:2", "friendly"),
		L("faith", "woman", 22, 60, 5, "Joined the temperance women and took an axe to a saloon's bottles", "proud:2 hothead:2", "devout"),
		L("adult", "woman", 20, 60, 5, "Keeps a pistol in her apron since a man broke into her kitchen", "cold:2 coward:1", "aggr:+3"),
		L("adult", "woman", 22, 60, 5, "Ran the house on what her husband didn't drink away", "cold:2 greedy:1", "surly"),
		L("adult", "woman", 18, 50, 4, "Won the pie contest at the {town} fair three years running", "proud:2", "friendly"),
		L("loss", "woman", 20, 50, 6, "Lost a baby to the fever, and never speaks of it", "cold:3 coward:1"),
		L("adult", "woman", 20, 55, 4, "Waited three years for a sweetheart who never came back from the gold fields", "loyal:2 cold:1")
	};

	private static readonly (string name, Func<LifeT[]> get, Action<LifeT[]> set)[] PoolSlots = new(string, Func<LifeT[]>, Action<LifeT[]>)[6]
	{
		("boyhood", () => Boyhood, delegate(LifeT[] v)
		{
			Boyhood = v;
		}),
		("youth", () => Youth, delegate(LifeT[] v)
		{
			Youth = v;
		}),
		("work", () => Work, delegate(LifeT[] v)
		{
			Work = v;
		}),
		("crime", () => Crime, delegate(LifeT[] v)
		{
			Crime = v;
		}),
		("gang", () => GangLife, delegate(LifeT[] v)
		{
			GangLife = v;
		}),
		("adult", () => Adult, delegate(LifeT[] v)
		{
			Adult = v;
		})
	};

	private static readonly (string name, Func<string[]> get, Action<string[]> set)[] PlaceSlots = new(string, Func<string[]>, Action<string[]>)[8]
	{
		("irishCounties", () => IrishCounties, delegate(string[] v)
		{
			IrishCounties = v;
		}),
		("irishAmerican", () => IrishAmerican, delegate(string[] v)
		{
			IrishAmerican = v;
		}),
		("southern", () => Southern, delegate(string[] v)
		{
			Southern = v;
		}),
		("northern", () => Northern, delegate(string[] v)
		{
			Northern = v;
		}),
		("frontier", () => Frontier, delegate(string[] v)
		{
			Frontier = v;
		}),
		("towns", () => Towns, delegate(string[] v)
		{
			Towns = v;
		}),
		("irishWives", () => IrishWives, delegate(string[] v)
		{
			IrishWives = v;
		}),
		("wives", () => Wives, delegate(string[] v)
		{
			Wives = v;
		})
	};

	public static int AddedFromBuiltIn;

	private static LifeT L(string kind, string who, int minAge, int maxAge, int w, string text, string tilt = "", string fx = "", bool pub = false, int bornFrom = 0, int bornTo = 0, string bornIn = null, string mirrorKind = null, string mirrorTitle = null, bool mirrorHeard = false)
	{
		LifeT lifeT = new LifeT
		{
			Kind = kind,
			Who = who,
			MinAge = minAge,
			MaxAge = maxAge,
			W = w,
			Text = text,
			Fx = fx,
			Public = pub,
			TiltText = tilt,
			BornFrom = bornFrom,
			BornTo = bornTo,
			BornIn = bornIn,
			MirrorKind = mirrorKind,
			MirrorTitle = mirrorTitle,
			MirrorHeard = mirrorHeard
		};
		string[] array = (tilt ?? "").Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(':');
			if (array2.Length == 2 && Enum.TryParse<Personality>(array2[0], ignoreCase: true, out var result) && int.TryParse(array2[1], out var result2))
			{
				lifeT.Tilt[result] = result2;
			}
		}
		return lifeT;
	}

	public static LivesContent Export()
	{
		LivesContent livesContent = new LivesContent();
		(string, Func<string[]>, Action<string[]>)[] placeSlots = PlaceSlots;
		for (int i = 0; i < placeSlots.Length; i++)
		{
			(string, Func<string[]>, Action<string[]>) tuple = placeSlots[i];
			livesContent.Places[tuple.Item1] = tuple.Item2().ToList();
		}
		livesContent.GreyBattles = GreyBattles.Select(delegate((string battle, int year) b)
		{
			Battle battle = new Battle();
			(battle.Name, battle.Year) = b;
			return battle;
		}).ToList();
		livesContent.BlueBattles = BlueBattles.Select(delegate((string battle, int year) b)
		{
			Battle battle = new Battle();
			(battle.Name, battle.Year) = b;
			return battle;
		}).ToList();
		(string, Func<LifeT[]>, Action<LifeT[]>)[] poolSlots = PoolSlots;
		for (int i = 0; i < poolSlots.Length; i++)
		{
			(string, Func<LifeT[]>, Action<LifeT[]>) tuple2 = poolSlots[i];
			livesContent.Pools[tuple2.Item1] = (from t in tuple2.Item2()
				select new LifeRow
				{
					Id = LifeCond.Slug(t.Kind + " " + t.Text),
					Kind = t.Kind,
					Who = t.Who,
					MinAge = t.MinAge,
					MaxAge = t.MaxAge,
					Weight = t.W,
					Text = t.Text,
					Tilt = t.TiltText,
					Fx = t.Fx,
					Public = t.Public,
					BornFrom = t.BornFrom,
					BornTo = t.BornTo,
					BornIn = t.BornIn,
					MirrorKind = t.MirrorKind,
					MirrorTitle = t.MirrorTitle,
					MirrorHeard = t.MirrorHeard
				}).ToList();
		}
		return livesContent;
	}

	public static int Import(LivesContent c)
	{
		int num = 0;
		if (c == null)
		{
			return 0;
		}
		(string, Func<string[]>, Action<string[]>)[] placeSlots = PlaceSlots;
		for (int i = 0; i < placeSlots.Length; i++)
		{
			(string, Func<string[]>, Action<string[]>) tuple = placeSlots[i];
			if (c.Places != null && c.Places.TryGetValue(tuple.Item1, out var value) && value != null && value.Count > 0)
			{
				tuple.Item3(value.ToArray());
			}
		}
		List<Battle> greyBattles = c.GreyBattles;
		if (greyBattles != null && greyBattles.Count > 0)
		{
			GreyBattles = c.GreyBattles.Select((Battle b) => (Name: b.Name, Year: b.Year)).ToArray();
		}
		List<Battle> blueBattles = c.BlueBattles;
		if (blueBattles != null && blueBattles.Count > 0)
		{
			BlueBattles = c.BlueBattles.Select((Battle b) => (Name: b.Name, Year: b.Year)).ToArray();
		}
		(string, Func<LifeT[]>, Action<LifeT[]>)[] poolSlots = PoolSlots;
		for (int i = 0; i < poolSlots.Length; i++)
		{
			(string name, Func<LifeT[]> get, Action<LifeT[]> set) p = poolSlots[i];
			if (c.Pools != null && c.Pools.TryGetValue(p.name, out var value2) && value2 != null && value2.Count != 0)
			{
				List<LifeT> list = (from x in value2
					where !string.IsNullOrEmpty(x.Text)
					select L(x.Kind ?? p.name, x.Who ?? "any", x.MinAge, Math.Max(x.MinAge, x.MaxAge), Math.Max(0, x.Weight), x.Text, x.Tilt ?? "", x.Fx ?? "", x.Public, x.BornFrom, x.BornTo, x.BornIn, x.MirrorKind, x.MirrorTitle, x.MirrorHeard)).ToList();
				HashSet<string> ids = new HashSet<string>(value2.Select((LifeRow x) => (!string.IsNullOrEmpty(x.Id)) ? x.Id : LifeCond.Slug((x.Kind ?? p.name) + " " + x.Text)));
				HashSet<string> removed = new HashSet<string>(c.Removed ?? new List<string>());
				List<LifeT> list2 = p.get().Where(delegate(LifeT t)
				{
					string item = LifeCond.Slug(t.Kind + " " + t.Text);
					return !ids.Contains(item) && !removed.Contains(item);
				}).ToList();
				if (list2.Count > 0)
				{
					list.AddRange(list2);
					AddedFromBuiltIn += list2.Count;
				}
				p.set(list.ToArray());
				num += value2.Count;
			}
		}
		return num;
	}

	public static bool KinAgeFits(NemesisRecord r, NemesisRecord kin)
	{
		if (kin == null || kin.BornYear <= 0 || r.BornYear <= 0)
		{
			return true;
		}
		int num = r.BornYear - kin.BornYear;
		switch (r.KinRelation)
		{
		case "son":
			if (num >= 16)
			{
				return num <= 40;
			}
			return false;
		case "father":
			if (num <= -16)
			{
				return num >= -40;
			}
			return false;
		case "nephew":
			if (num >= 12)
			{
				return num <= 32;
			}
			return false;
		case "uncle":
			if (num <= -12)
			{
				return num >= -32;
			}
			return false;
		case "brother":
			return Math.Abs(num) <= 12;
		default:
			return Math.Abs(num) <= 16;
		}
	}

	public static string RelationForAge(NemesisRecord r, NemesisRecord kin)
	{
		if (kin == null || kin.BornYear <= 0 || r.BornYear <= 0)
		{
			return r.KinRelation;
		}
		if (KinAgeFits(r, kin))
		{
			return r.KinRelation;
		}
		int num = r.BornYear - kin.BornYear;
		if (Math.Abs(num) <= 12)
		{
			return "brother";
		}
		if (num >= 16 && num <= 40)
		{
			return "son";
		}
		if (num <= -16 && num >= -40)
		{
			return "father";
		}
		if (Math.Abs(num) <= 16)
		{
			return "cousin";
		}
		return null;
	}

	public static bool FitKinAge(NemesisRecord r, NemesisRecord kin, Random rng, int year)
	{
		if (kin == null || kin.BornYear <= 0)
		{
			return false;
		}
		int kb = kin.BornYear;
		int num = Born(r.KinRelation ?? "brother");
		if (!Fits(num))
		{
			r.KinRelation = "brother";
			num = Born("brother");
		}
		if (!Fits(num))
		{
			num = Math.Max(year - 74, Math.Min(year - 16, num));
		}
		if ((r.KinRelation == "brother" || r.KinRelation == "son" || r.KinRelation == "father") && kin.Birthplace != null && rng.Next(100) < 75)
		{
			r.Birthplace = kin.Birthplace;
		}
		bool result = r.BornYear != num;
		r.BornYear = num;
		return result;
		int Born(string rel)
		{
			return rel switch
			{
				"son" => kb + 18 + rng.Next(20), 
				"father" => kb - (20 + rng.Next(18)), 
				"nephew" => kb + 14 + rng.Next(16), 
				"uncle" => kb - (14 + rng.Next(16)), 
				"brother" => kb + rng.Next(-10, 11), 
				_ => kb + rng.Next(-14, 15), 
			};
		}
		bool Fits(int b)
		{
			int num2 = year - b;
			if (num2 >= 16)
			{
				return num2 <= 74;
			}
			return false;
		}
	}

	public static BackstoryResult Roll(NemesisRecord r, Random rng, int year, int month, bool derive, bool keepBorn = false)
	{
		BackstoryResult backstoryResult = new BackstoryResult();
		r.Past = new List<PastEntry>();
		int num = ((r.BornYear > 0 && (!derive || keepBorn)) ? (year - r.BornYear) : AgeFor(r, rng));
		r.BornYear = year - num;
		int month2 = 1 + rng.Next(12);
		Ctx c = new Ctx
		{
			Year = year,
			Month = month,
			Age = num,
			Rng = rng,
			R = r
		};
		c.Irish = r.Gang == "odriscoll" || (r.Name != null && NameBank.IrishLast.Any((string l) => r.Name.EndsWith(" " + l)));
		c.Born = r.Birthplace ?? BirthplaceFor(r, c, rng);
		r.Birthplace = c.Born;
		c.IrishBorn = c.Born.Contains("Ireland") || c.Born.StartsWith("County");
		if (c.IrishBorn && !c.Born.Contains("Ireland"))
		{
			c.Born += ", Ireland";
			r.Birthplace = c.Born;
		}
		c.South = r.Gang == "lemoyneraiders" || r.Gang == "klan" || Southern.Any((string s) => c.Born.StartsWith(s.Split(',')[0]));
		c.North = !c.South && !c.IrishBorn && (r.Gang == "army" || Northern.Any((string s) => c.Born.StartsWith(s.Split(',')[0])));
		c.Home = (c.IrishBorn ? c.Born.Split(',')[0] : c.Born.Split(',')[0]);
		Add(r, backstoryResult, null, r.BornYear, month2, "Born in " + c.Born, "born", null, null, pub: true);
		Pick(r, backstoryResult, c, Boyhood, derive);
		if (c.IrishBorn || c.Born.StartsWith("Bavaria"))
		{
			int year2 = Math.Min(year, r.BornYear + 9 + rng.Next(12));
			Add(r, backstoryResult, null, year2, 1 + rng.Next(12), c.IrishBorn ? "Crossed from Queenstown to New York in steerage" : "Crossed from Hamburg to New York with his family", "youth", "loyal:1 cold:1", null, pub: true, derive);
		}
		Pick(r, backstoryResult, c, Youth, derive);
		War(r, backstoryResult, c, rng);
		if (r.Gang == null || r.Gang == "klan")
		{
			Pick(r, backstoryResult, c, Work, derive);
		}
		bool flag = r.Gang == "odriscoll" || r.Gang == "lemoyneraiders" || r.Trade == "Outlaw" || r.Trade == "Drifter" || r.Trade == "Vagrant";
		if (r.Gang != "army" && r.Gang != "wapiti" && r.Gang != "nightfolk" && r.Gang != "delobo" && rng.Next(100) < (flag ? 55 : 12))
		{
			Pick(r, backstoryResult, c, Crime, derive);
		}
		if (rng.Next(100) < 30 && num >= 40 && (r.Trade == "Farmer" || r.Trade == "Rancher" || r.Trade == "Townsman" || r.Trade == "Gentleman" || r.Trade == "Lady" || r.Trade == "Shopkeeper" || r.Trade == "Homesteader" || r.Trade == "Farm wife" || r.Gang == "klan") && year >= 1893 && r.BornYear <= 1873)
		{
			Add(r, backstoryResult, null, 1893, 5 + rng.Next(6), (r.Trade == "Gentleman") ? "Lost half his fortune when the banks failed in the Panic of '93" : "Lost everything when the banks failed in the Panic of '93", "loss", "greedy:3 cold:1", "surly", pub: false, derive);
		}
		int minYear = JoinUp(r, backstoryResult, c, rng);
		if (r.Gang != null)
		{
			Pick(r, backstoryResult, c, GangLife, derive, minYear);
		}
		Family(r, backstoryResult, c, rng, derive);
		int num2 = ((num < 40) ? 1 : 2);
		for (int num3 = 0; num3 < num2; num3++)
		{
			Pick(r, backstoryResult, c, Adult, derive);
		}
		foreach (PastEntry item in r.Past)
		{
			if (item.Year >= year)
			{
				item.Year = year;
				if (item.Month > month)
				{
					item.Month = month;
				}
			}
		}
		r.Past = (from e in r.Past
			orderby e.Year, e.Month
			select e).ToList();
		r.PastRolled = true;
		return backstoryResult;
	}

	private static int AgeFor(NemesisRecord r, Random rng)
	{
		switch (r.Gang)
		{
		case "odriscoll":
			if (r.Rank < 4)
			{
				if (r.Rank < 2)
				{
					return Between(17, 30);
				}
				return Between(24, 40);
			}
			return Between(30, 48);
		case "lemoyneraiders":
			if (r.Rank < 4)
			{
				if (r.Rank < 2)
				{
					if (rng.Next(2) != 0)
					{
						return Between(46, 60);
					}
					return Between(18, 34);
				}
				return Between(34, 58);
			}
			return Between(45, 62);
		case "army":
			if (r.Rank < 4)
			{
				if (r.Rank < 2)
				{
					return Between(18, 28);
				}
				return Between(26, 42);
			}
			return Between(32, 55);
		case "klan":
			return Between(25, 60);
		case "wapiti":
			if (r.Rank < 4)
			{
				if (r.Rank < 2)
				{
					return Between(17, 30);
				}
				return Between(24, 44);
			}
			return Between(36, 58);
		default:
			switch (r.Trade)
			{
			case "Vagrant":
				return Between(28, 65);
			case "Gentleman":
			case "Doctor":
			case "Preacher":
				return Between(30, 62);
			case "Cowhand":
			case "Labourer":
				return Between(18, 40);
			default:
				return Between(19, 58);
			}
		}
		int Between(int a, int b)
		{
			return a + rng.Next(b - a + 1);
		}
	}

	private static string BirthplaceFor(NemesisRecord r, Ctx c, Random rng)
	{
		if (r.Gang == "wapiti")
		{
			return (new string[4] { "the northern plains", "the northern plains", "the Grizzlies, Ambarino", "the Dakota River country" })[rng.Next(4)];
		}
		if (c.Irish)
		{
			if (rng.Next(100) >= 60)
			{
				return IrishAmerican[rng.Next(IrishAmerican.Length)];
			}
			return IrishCounties[rng.Next(IrishCounties.Length)] + ", Ireland";
		}
		if (r.Gang == "lemoyneraiders" || r.Gang == "klan")
		{
			return Southern[rng.Next(Southern.Length)];
		}
		if (r.Gang == "army")
		{
			if (rng.Next(100) >= 12)
			{
				return Northern[rng.Next(Northern.Length)];
			}
			return IrishCounties[rng.Next(IrishCounties.Length)] + ", Ireland";
		}
		string text = Models.Name(r.ModelHash);
		if (text.Contains("rhd") || text.Contains("sd") || text.Contains("bayou") || text.Contains("lem"))
		{
			return Southern[rng.Next(Southern.Length)];
		}
		return Frontier[rng.Next(Frontier.Length)];
	}

	private static void War(NemesisRecord r, BackstoryResult res, Ctx c, Random rng)
	{
		if (r.Gang == "wapiti")
		{
			if (r.BornYear <= 1862 && rng.Next(100) < 45)
			{
				Add(r, res, null, Math.Min(c.Year, r.BornYear + 17 + rng.Next(6)), 1 + rng.Next(12), "Fought the soldiers on the plains as a young man", "war", "cold:2 proud:2", "aggr:+5", pub: true);
			}
			if (r.BornYear < 1891)
			{
				Add(r, res, null, Math.Min(c.Year, 1891 + rng.Next(4)), 1 + rng.Next(12), "Came onto the reservation in Ambarino with his people", "loss", "cold:2 hothead:1 loyal:1", "surly", pub: true);
			}
		}
		else
		{
			if (r.BornYear > 1848)
			{
				return;
			}
			bool south = c.South;
			if (Woman(r))
			{
				if (rng.Next(100) < 55)
				{
					string[] array = ((!south) ? new string[4] { "Kept the farm going while the men were away at the War", "Nursed Union wounded in a field hospital", "Sewed uniforms for the regiment in '62", "Lost two brothers to the War" } : new string[4] { "Kept the farm going while the men were away at the War", "Nursed Confederate wounded in a church turned hospital", "Watched Union soldiers burn the barn in '64", "Lost two brothers to the War" });
					Add(r, res, null, 1862 + rng.Next(3), 1 + rng.Next(12), array[rng.Next(array.Length)], "war", "cold:1 loyal:1");
					if (south && rng.Next(100) < 30)
					{
						Add(r, res, null, 1865, 4, "Saw the farm burned and her people scattered", "loss", "hothead:1 cold:2", "surly");
					}
				}
				return;
			}
			int num = ((r.Gang == "lemoyneraiders") ? 90 : ((r.Gang == "klan") ? 70 : ((r.Gang == "army") ? 75 : 40)));
			if (rng.Next(100) >= num)
			{
				return;
			}
			(string, int)[] array2 = (south ? GreyBattles : BlueBattles).Where(((string battle, int year) b) => b.year - r.BornYear >= 15).ToArray();
			if (array2.Length != 0)
			{
				(string, int) tuple = array2[rng.Next(array2.Length)];
				Add(r, res, null, tuple.Item2, 3 + rng.Next(8), "Fought for the " + (south ? "Confederacy" : "Union") + " at " + tuple.Item1, "war", "cold:2 proud:2", "aggr:+5", pub: true);
				int num2 = rng.Next(100);
				if (num2 < 35)
				{
					(string, int) tuple2 = array2[rng.Next(array2.Length)];
					Add(r, res, null, Math.Max(tuple.Item2, tuple2.Item2), 2 + rng.Next(9), "Wounded at " + tuple2.Item1, "war", "coward:1 hothead:1", "fear:+3");
				}
				else if (num2 < 50 && south)
				{
					Add(r, res, null, 1864, 1 + rng.Next(12), "Captured and held in a Union prison camp", "war", "hothead:2 cold:1", "surly");
				}
				else if (num2 < 62)
				{
					Add(r, res, null, 1864 + rng.Next(2), 1 + rng.Next(4), "Deserted before the end - and never told a soul", "war", "coward:3 drifter:2");
				}
				else if (num2 < 75 && !south && tuple.Item2 <= 1864)
				{
					Add(r, res, null, 1864, 11, "Marched with Sherman to the sea", "war", "cold:2");
				}
				if (south && rng.Next(100) < 40)
				{
					Add(r, res, null, 1865, 4, "Came home to find the farm burned and his people scattered", "loss", "hothead:2 cold:2", "surly");
				}
			}
		}
	}

	private static int JoinUp(NemesisRecord r, BackstoryResult res, Ctx c, Random rng)
	{
		Gang gang = Gangs.ById(r.Gang);
		if (gang == null)
		{
			return 0;
		}
		int num = ((r.Rank >= 4) ? (5 + rng.Next(8)) : ((r.Rank >= 2) ? (2 + rng.Next(7)) : rng.Next(5)));
		int num2 = Math.Max(r.BornYear + 16, c.Year - num);
		if (r.Gang == "wapiti")
		{
			num2 = r.BornYear + 16 + rng.Next(4);
		}
		if (num2 > c.Year)
		{
			num2 = c.Year;
		}
		int month = (c.JoinMonth = ((num2 == c.Year) ? (1 + rng.Next(Math.Max(1, c.Month))) : (1 + rng.Next(12))));
		string text = ((r.Gang == "odriscoll") ? ("Fell in with " + gang.LeaderName + "'s boys") : ((r.Gang == "lemoyneraiders") ? ("Took up with the Lemoyne Raiders under " + gang.LeaderName) : ((r.Gang == "army") ? ("Enlisted in the U.S. Army at " + (new string[4] { "Blackwater", "Saint Denis", "Fort Wallace", "a recruiting office back East" })[rng.Next(4)]) : ((r.Gang == "klan") ? "Put on the hood for the first time" : ((r.Gang == "wapiti") ? "Rode out with the warriors for the first time" : ("Joined the " + gang.Name))))));
		Add(r, res, null, num2, month, text, "gang", (r.Gang == "odriscoll") ? "loyal:1" : "", null, r.Gang != "klan");
		(string, int, int)[] tiers = gang.Tiers;
		List<string> list = new List<string>();
		for (int num3 = tiers.Length - 2; num3 >= 0; num3--)
		{
			if (r.Rank >= tiers[num3].Item2)
			{
				list.Add(Singular(tiers[num3].Item1));
			}
		}
		for (int i = 0; i < list.Count; i++)
		{
			int num4 = Math.Min(c.Year, num2 + (int)Math.Round((double)((c.Year - num2) * (i + 1)) / (double)(list.Count + 1)));
			string text2 = ((r.Gang == "army") ? ((list[i] == "Officer") ? "Commissioned an officer" : ("Made " + list[i].ToLowerInvariant())) : ((r.Gang == "wapiti") ? ("Trusted as a " + list[i].ToLowerInvariant()) : ("Made " + list[i])));
			Add(r, res, null, Math.Max(num2, num4), (num4 == c.Year) ? (1 + rng.Next(Math.Max(1, c.Month))) : (1 + rng.Next(12)), text2, "rank");
		}
		return num2;
	}

	private static void Family(NemesisRecord r, BackstoryResult res, Ctx c, Random rng, bool derive)
	{
		string text;
		int num2;
		if (derive)
		{
			int num = ((c.Age < 20) ? 5 : ((c.Age < 26) ? 30 : ((c.Age < 36) ? 55 : 68)));
			if (r.Gang == "odriscoll" && r.Rank == 1)
			{
				num -= 20;
			}
			if (r.Gang == "army" && r.Rank == 1)
			{
				num -= 15;
			}
			if (r.Trade == "Drifter" || r.Trade == "Vagrant")
			{
				num -= 20;
			}
			text = ((rng.Next(100) < num) ? ((rng.Next(100) < ((c.Age >= 40) ? 25 : 12)) ? "widower" : "married") : "single");
			num2 = ((!(text == "single")) ? ((rng.Next(100) >= 25) ? (1 + rng.Next(Math.Min(5, Math.Max(1, (c.Age - 18) / 5)))) : 0) : 0);
			res.Family = text;
			res.Children = num2;
		}
		else
		{
			text = r.Family ?? "single";
			num2 = r.Children;
		}
		if (text == "single")
		{
			return;
		}
		int num3 = 19 + rng.Next(Math.Max(1, Math.Min(12, c.Age - 19)));
		int num4 = Math.Min(c.Year, r.BornYear + num3);
		bool flag = Woman(r);
		string text2 = ((!flag) ? ((r.Gang == "wapiti") ? "a woman of the Wapiti" : (c.Irish ? IrishWives : Wives)[rng.Next((c.Irish ? IrishWives : Wives).Length)]) : ((r.Gang == "wapiti") ? "a man of the Wapiti" : (c.Irish ? IrishHusbands : Husbands)[rng.Next((c.Irish ? IrishHusbands : Husbands).Length)]));
		Add(r, res, null, num4, 1 + rng.Next(12), "Married " + text2, "family", "loyal:1", null, pub: false, derive);
		int num5 = int.MaxValue;
		string text3 = null;
		if (text == "widower")
		{
			int num6 = Math.Min(c.Year, Math.Max(num4 + 1, num4 + rng.Next(Math.Max(2, c.Year - num4 + 1))));
			text3 = (new string[5] { "the fever", "childbirth", "a winter cough", "cholera", "a fall from a wagon" })[rng.Next(5)];
			num5 = num6 * 12 + rng.Next(12);
		}
		int num7 = num4 * 12 + 9 + rng.Next(12);
		for (int i = 0; i < num2; i++)
		{
			if (num7 > num5)
			{
				if (derive)
				{
					res.Children = i;
				}
				break;
			}
			int year = Math.Min(c.Year, num7 / 12);
			int month = num7 % 12 + 1;
			Add(r, res, null, year, month, (rng.Next(2) == 0) ? "A son born" : "A daughter born", "family", null, null, pub: false, derive);
			if (text3 == "childbirth" && i == num2 - 1)
			{
				num5 = Math.Max(num5, num7);
			}
			num7 += 12 + rng.Next(30);
		}
		if (text == "widower")
		{
			if (text3 == "childbirth" && num2 == 0)
			{
				text3 = "the fever";
			}
			int year2 = Math.Min(c.Year, num5 / 12);
			int month2 = num5 % 12 + 1;
			Add(r, res, null, year2, month2, "Lost " + ((!(r.Gang == "wapiti")) ? text2 : (flag ? "her husband" : "his wife")) + " to " + ((flag && text3 == "childbirth") ? "the fever" : text3), "loss", "cold:3 hothead:1", (rng.Next(100) < 35) ? "godless" : "", pub: false, derive);
		}
	}

	private static void Pick(NemesisRecord r, BackstoryResult res, Ctx c, LifeT[] pool, bool derive, int minYear = 0)
	{
		Personality? personality = r.Personality;
		List<(LifeT, double)> list = new List<(LifeT, double)>();
		foreach (LifeT t in pool)
		{
			if (!WhoOk(t.Who, r, c) || (Woman(r) && t.Text != null && (t.Text.Contains("wife") || t.Text.Contains("widow") || t.Text.Contains("enlisted") || t.Text.Contains("Enlisted"))) || t.MinAge > c.Age || !t.Allows(r, c) || r.Past.Any((PastEntry e) => e.Text != null && e.Text.StartsWith(t.Text.Split('{')[0])))
			{
				continue;
			}
			double num = t.W;
			if (!derive)
			{
				if (Contradicts(t.Fx, r))
				{
					continue;
				}
				if (personality.HasValue)
				{
					num *= Math.Max(0.15, Math.Min(3.0, 1.0 + 0.35 * (double)(t.Tilt.TryGetValue(personality.Value, out var value) ? value : 0)));
				}
			}
			list.Add((t, num));
		}
		if (list.Count == 0)
		{
			return;
		}
		double num2 = c.Rng.NextDouble() * list.Sum<(LifeT, double)>(((LifeT t, double w) x) => x.w);
		LifeT lifeT = list[list.Count - 1].Item1;
		foreach (var item in list)
		{
			num2 -= item.Item2;
			if (num2 <= 0.0)
			{
				(lifeT, _) = item;
				break;
			}
		}
		int num3 = lifeT.MinAge + c.Rng.Next(Math.Max(1, Math.Min(lifeT.MaxAge, c.Age) - lifeT.MinAge + 1));
		int num4 = Math.Min(c.Year, Math.Max(minYear, r.BornYear + num3));
		int num5 = ((num4 == c.Year) ? (1 + c.Rng.Next(Math.Max(1, c.Month))) : (1 + c.Rng.Next(12)));
		if (minYear > 0 && num4 == minYear && num5 < c.JoinMonth)
		{
			num5 = Math.Min((num4 == c.Year) ? Math.Max(1, c.Month) : 12, c.JoinMonth + c.Rng.Next(3));
		}
		string text = Towns[c.Rng.Next(Towns.Length)];
		string text2 = Fill(lifeT.Text, c, text);
		PastEntry pastEntry = Add(r, res, null, num4, num5, text2, lifeT.Kind, null, derive ? lifeT.Fx : null, lifeT.Public, derive);
		foreach (KeyValuePair<Personality, int> item2 in lifeT.Tilt)
		{
			res.Tilt[item2.Key] = (res.Tilt.TryGetValue(item2.Key, out var value2) ? value2 : 0) + item2.Value;
		}
		if (lifeT.MirrorKind != null)
		{
			pastEntry.MirrorKind = lifeT.MirrorKind;
			pastEntry.MirrorTitle = Fill(lifeT.MirrorTitle, c, text);
			pastEntry.MirrorPlace = ((lifeT.MirrorTitle.Contains("{town}") || lifeT.Text.Contains("{town}")) ? text : (lifeT.Text.Contains("Van Horn") ? "Van Horn" : (lifeT.Text.Contains("Strawberry") ? "Strawberry" : (lifeT.Text.Contains("Sisika") ? "Sisika Penitentiary" : ""))));
			pastEntry.MirrorHeard = lifeT.MirrorHeard;
		}
	}

	private static string Fill(string s, Ctx c, string town)
	{
		return (s ?? "").Replace("{home}", c.Home).Replace("{town}", town).Replace("{n}", (2 + c.Rng.Next(5)).ToString());
	}

	private static PastEntry Add(NemesisRecord r, BackstoryResult res, string worldId, int year, int month, string text, string kind, string tilt = null, string fx = null, bool pub = false, bool derive = true)
	{
		if (Woman(r))
		{
			text = Pron.Her(text, female: true);
		}
		PastEntry pastEntry = new PastEntry
		{
			Year = year,
			Month = Math.Max(0, Math.Min(12, month)),
			Text = text,
			Kind = kind,
			WorldId = worldId,
			Public = pub
		};
		r.Past.Add(pastEntry);
		if (tilt != null)
		{
			string[] array = tilt.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(':');
				if (array2.Length == 2 && Enum.TryParse<Personality>(array2[0], ignoreCase: true, out var result) && int.TryParse(array2[1], out var result2))
				{
					res.Tilt[result] = (res.Tilt.TryGetValue(result, out var value) ? value : 0) + result2;
				}
			}
		}
		if (derive && !string.IsNullOrEmpty(fx))
		{
			res.Fx.AddRange(fx.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
		}
		return pastEntry;
	}

	internal static bool Woman(NemesisRecord r)
	{
		if (r != null)
		{
			return Models.Female(r.ModelHash);
		}
		return false;
	}

	private static bool WhoOk(string who, NemesisRecord r, Ctx c)
	{
		Gang gang = Gangs.ById(r.Gang);
		if (gang != null && gang.OwnLives)
		{
			return who.Split('|').Contains(r.Gang);
		}
		string[] array = who.Split('|');
		if (array.Contains("man") && Woman(r))
		{
			return false;
		}
		if (array.Contains("woman") && !Woman(r))
		{
			return false;
		}
		if (array.All((string x) => x == "man" || x == "woman"))
		{
			return true;
		}
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (text == "any")
			{
				return true;
			}
			if (text == "gang" && r.Gang != null)
			{
				return true;
			}
			if (text == "loner" && r.Gang == null)
			{
				return true;
			}
			if (text == r.Gang)
			{
				return true;
			}
			if (text == "irish" && c.Irish)
			{
				return true;
			}
			if (text == "irishborn" && c.IrishBorn)
			{
				return true;
			}
			if (text == "south" && c.South)
			{
				return true;
			}
			if (text == "north" && c.North)
			{
				return true;
			}
			if (text == "rich" && r.Wealth == Wealth.Rich)
			{
				return true;
			}
			if (text == "poor" && (r.Wealth == Wealth.Poor || r.Wealth == Wealth.Working))
			{
				return true;
			}
			if (text.StartsWith("trade:") && r.Trade == text.Substring(6))
			{
				return true;
			}
		}
		return false;
	}

	private static bool Contradicts(string fx, NemesisRecord r)
	{
		if (string.IsNullOrEmpty(fx) || !r.TraitsRolled)
		{
			return false;
		}
		string[] array = fx.Split(' ');
		foreach (string text in array)
		{
			if (text == "devout" && (r.Devotion == Devotion.Godless || r.Devotion == Devotion.Lapsed))
			{
				return true;
			}
			if (text == "godless" && (r.Devotion == Devotion.Devout || r.Devotion == Devotion.Churchgoer))
			{
				return true;
			}
			if (text == "friendly" && r.Disposition == Disposition.Surly)
			{
				return true;
			}
			if (text == "surly" && r.Disposition == Disposition.Friendly)
			{
				return true;
			}
			if (text == "sharp" && r.Smarts == Smarts.Dim)
			{
				return true;
			}
			if (text == "dim" && r.Smarts == Smarts.Sharp)
			{
				return true;
			}
			if (text.StartsWith("aggr:+") && r.Aggression < 30)
			{
				return true;
			}
		}
		return false;
	}

	public static Personality PickTemper(BackstoryResult res, bool gang, Random rng)
	{
		int value;
		return Personalities.PickWeighted(rng, gang, (Personality p) => Math.Max(0.15, 1.0 + 0.15 * (double)(res.Tilt.TryGetValue(p, out value) ? value : 0)));
	}

	public static void Apply(NemesisRecord r, BackstoryResult res)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		foreach (string item in res.Fx)
		{
			if (item.StartsWith("aggr:") && int.TryParse(item.Substring(5), out var result))
			{
				num += result;
				continue;
			}
			if (item.StartsWith("fear:") && int.TryParse(item.Substring(5), out var result2))
			{
				num5 += result2;
				continue;
			}
			switch (item)
			{
			case "friendly":
				num2++;
				break;
			case "surly":
				num2--;
				break;
			case "devout":
				num3++;
				break;
			case "godless":
				num3--;
				break;
			case "sharp":
				num4++;
				break;
			case "dim":
				num4--;
				break;
			}
		}
		r.Aggression = Math.Max(0, Math.Min(100, r.Aggression + Math.Max(-20, Math.Min(20, num))));
		if (num2 > 0)
		{
			r.Disposition = ((r.Disposition == Disposition.Surly) ? Disposition.Neutral : Disposition.Friendly);
		}
		if (num2 < 0)
		{
			r.Disposition = ((r.Disposition == Disposition.Friendly) ? Disposition.Neutral : Disposition.Surly);
		}
		if (num3 == 0 && res.Fx.Any((string f) => f == "devout" || f == "godless"))
		{
			r.Devotion = Devotion.Lapsed;
		}
		if (num3 > 0)
		{
			r.Devotion = Devotion.Devout;
			if (r.Faith == "none")
			{
				r.Faith = ((r.Gang == "odriscoll") ? "Catholic" : "Methodist");
			}
		}
		if (num3 < 0)
		{
			r.Devotion = Devotion.Godless;
			r.Faith = "none";
		}
		if (num4 > 0)
		{
			r.Smarts = Smarts.Sharp;
		}
		if (num4 < 0)
		{
			r.Smarts = Smarts.Dim;
		}
		if (num5 > 0)
		{
			r.Fear = Math.Min(30, r.Fear + num5);
		}
		if (res.Family != null)
		{
			r.Family = res.Family;
			r.Children = res.Children;
		}
	}

	internal static string Singular(string label)
	{
		string text = ((label.Length > 1 && label.EndsWith("S")) ? label.Substring(0, label.Length - 1) : label);
		if (text.Length != 0)
		{
			return text[0] + text.Substring(1).ToLowerInvariant();
		}
		return text;
	}

	public static (int r, int g, int b) KindColor(string kind)
	{
		switch (kind)
		{
		case "war":
			return (r: 176, g: 150, b: 116);
		case "crime":
			return (r: 214, g: 150, b: 80);
		case "gang":
		case "rank":
			return (r: 150, g: 176, b: 140);
		case "family":
			return (r: 150, g: 172, b: 196);
		case "loss":
			return (r: 160, g: 126, b: 160);
		case "world":
			return (r: 196, g: 170, b: 120);
		case "kin":
		case "friend":
			return (r: 140, g: 180, b: 196);
		default:
			return (r: 176, g: 164, b: 138);
		}
	}
}
