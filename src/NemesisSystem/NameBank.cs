using System.Linq;

namespace NemesisSystem;

internal static class NameBank
{
	public static readonly string[] AmericanFirst = new string[456]
	{
		"Silas", "Eli", "Clay", "Jeb", "Ezra", "Jasper", "Abner", "Cole", "Barnaby", "Bodie",
		"Caleb", "Calvin", "Cass", "Chester", "Clem", "Dade", "Dallas", "Delbert", "Doyle", "Elijah",
		"Elmer", "Emmett", "Enoch", "Ephraim", "Esau", "Everett", "Floyd", "Grady", "Gus", "Hank",
		"Harvey", "Hezekiah", "Homer", "Horace", "Hoyt", "Ike", "Ira", "Isaiah", "Jesse", "Jethro",
		"Lafe", "Leland", "Levi", "Lonnie", "Malachi", "Moses", "Nate", "Ned", "Orville", "Owen",
		"Percy", "Pike", "Quincy", "Rafe", "Reuben", "Sam", "Saul", "Seth", "Shelby", "Solomon",
		"Travis", "Tucker", "Ulysses", "Vernon", "Walt", "Wiley", "Zachariah", "Zeke", "Abel", "Alvin",
		"Archie", "Arlo", "Bart", "Benedict", "Bert", "Buck", "Byron", "Cecil", "Chauncey", "Clarence",
		"Clyde", "Cornelius", "Curtis", "Dewey", "Duncan", "Edgar", "Edmund", "Ellis", "Elton", "Emory",
		"Ernest", "Eugene", "Felix", "Franklin", "Fletcher", "Gilbert", "Grover", "Hamish", "Herschel", "Hugo",
		"Irving", "Jonah", "Judd", "Lyman", "Marion", "Milo", "Monroe", "Morgan", "Mose", "Nathaniel",
		"Noble", "Ollie", "Otto", "Parker", "Phineas", "Porter", "Roscoe", "Rowan", "Sherman", "Sterling",
		"Theo", "Thurston", "Tom", "Wallace", "Warren", "Wendell", "Willard", "Wilbur", "Winston", "Woodrow",
		"Wyatt", "Amon", "August", "Baxter", "Bram", "Cal", "Dell", "Dutch", "Eben", "Fenton",
		"Garland", "Hap", "Jem", "Kit", "Lon", "Mace", "Nolan", "Orrin", "Pruett", "Rance",
		"Sully", "Alonzo", "Ambrose", "Ansel", "Barrett", "Bennett", "Boyd", "Burl", "Carl", "Carson",
		"Cyrus", "Dexter", "Dudley", "Earl", "Edwin", "Elbert", "Elias", "Elisha", "Ford", "Frank",
		"Gabriel", "George", "Gideon", "Harlan", "Harold", "Harrison", "Henry", "Herbert", "Hollis", "Howard",
		"Jacob", "Jedediah", "Joel", "Joseph", "Lemuel", "Lester", "Lewis", "Lucas", "Luther", "Mack",
		"Matthias", "Nathan", "Newt", "Obed", "Oliver", "Oscar", "Otis", "Patrick", "Philo", "Ralph",
		"Ray", "Roy", "Rufus", "Russell", "Sanford", "Shadrach", "Sidney", "Stanley", "Sylvester", "Thaddeus",
		"Thomas", "Virgil", "Wade", "Walter", "Wesley", "Wilson", "Zebulon", "Augustus", "Elmore", "Lorenzo",
		"Merritt", "Royal", "Squire", "Tillman", "Uriah", "Wash", "Asa", "Hiram", "Aaron", "Abraham",
		"Adam", "Addison", "Alexander", "Alfred", "Allen", "Alpheus", "Alva", "Amasa", "Anderson", "Anson",
		"Anthony", "Arch", "Arnold", "Asher", "Austin", "Azariah", "Barney", "Benjamin", "Benton", "Bernard",
		"Blaine", "Bob", "Brady", "Brigham", "Bryant", "Burton", "Butler", "Charley", "Christopher", "Clark",
		"Claude", "Clifford", "Clinton", "Columbus", "Conrad", "Crockett", "Damon", "Daniel", "Darius", "David",
		"Davis", "Dennis", "Dick", "Dock", "Dolph", "Donald", "Douglas", "Drury", "Ebenezer", "Ed",
		"Edward", "Egbert", "Eldon", "Eldridge", "Elwood", "Emanuel", "Emil", "Erastus", "Ethan", "Evan",
		"Fayette", "Ferdinand", "Fred", "Frederick", "Gabe", "Garrett", "Gerald", "Gilford", "Glen", "Gordon",
		"Green", "Griffin", "Harmon", "Harris", "Harry", "Hayden", "Hector", "Hubert", "Hudson", "Hugh",
		"Humphrey", "Irvin", "Isham", "Israel", "James", "Jarvis", "Jay", "Jed", "Jeff", "Jerome",
		"Jim", "Joe", "Johnny", "Jonas", "Jonathan", "Julius", "King", "Lambert", "Lawrence", "Leander",
		"Lee", "Leon", "Leonard", "Lincoln", "Linus", "Lloyd", "Louis", "Lucian", "Lycurgus", "Madison",
		"Manley", "Marcus", "Mark", "Marshall", "Martin", "Matthew", "Maurice", "Melvin", "Michael", "Miles",
		"Mitchell", "Morris", "Murray", "Nelson", "Nicholas", "Norman", "Orlando", "Orson", "Osborne", "Paul",
		"Perry", "Peter", "Pleasant", "Raymond", "Reed", "Reese", "Richard", "Riley", "Robert", "Rodney",
		"Roland", "Rollin", "Romulus", "Rudolph", "Rush", "Samuel", "Sampson", "Sebastian", "Seymour", "Simeon",
		"Spencer", "Stephen", "Stewart", "Terrell", "Theodore", "Timothy", "Titus", "Truman", "Valentine", "Van",
		"Victor", "Webster", "Wilbert", "Will", "William", "Willis", "Wilmer", "Winfield", "Worth", "Zack",
		"Zadok", "Zenas", "Anse", "Arvel", "Aubrey", "Beverly", "Booker", "Boston", "Bud", "Cap",
		"Cash", "Cato", "Chas", "Clifton", "Coy", "Doc", "Dorsey", "Early", "Eldred", "Elmo",
		"Ely", "Eph", "Erwin", "Ewell", "Ewing", "Finis", "Foster", "Garfield", "Gilliam", "Hardin",
		"Harley", "Haywood", "Hub", "Jabez", "Jake", "Jarrett", "Jerry", "Joab", "Josh", "Kirby",
		"Lark", "Lem", "Lige", "Loyd", "Luke", "Lum", "Mart", "Nimrod", "Ott", "Pope",
		"Rob", "Rube", "Sim", "Tandy", "Tip", "Toby"
	};

	public static readonly string[] AmericanLast = new string[465]
	{
		"Boone", "McCrae", "Pike", "Slade", "Granger", "Hatch", "Kimbrough", "Langley", "Odom", "Pickett",
		"Rawlins", "Sayer", "Tillman", "Varner", "Yates", "Cobb", "Dancy", "Ellery", "Fenwick", "Gentry",
		"Hargrove", "Ivey", "Jessup", "Ketchum", "Lomax", "Merritt", "Nance", "Overby", "Purdy", "Quarles",
		"Rucker", "Stroud", "Thrasher", "Upton", "Wheeler", "Barlow", "Dobbs", "Elkins", "Fowler", "Hightower",
		"Ingram", "Jarrell", "Kilgore", "Lusk", "Mayhew", "Nix", "Pettibone", "Ragsdale", "Shanks", "Tuttle",
		"Vickery", "Blevins", "Coldwell", "Driscoll", "Faulk", "Gatlin", "Hux", "Lankford", "Pettit", "Scruggs",
		"Tidwell", "Weems", "Brackett", "Colt", "Grimes", "Quill", "Abbott", "Atwood", "Bailey", "Barnes",
		"Bishop", "Bowman", "Bradshaw", "Burke", "Carver", "Chandler", "Cockrell", "Conley", "Darby", "Denton",
		"Eckert", "Emery", "Farley", "Fitch", "Garrison", "Goodnight", "Hale", "Harlow", "Haskell", "Hogg",
		"Holt", "Huckaby", "Keller", "Landry", "Lowery", "Maddox", "Marsh", "McKee", "Mercer", "Nash",
		"Norwood", "Oakes", "Parrish", "Prather", "Redd", "Riggs", "Rowe", "Sawyer", "Shelton", "Skaggs",
		"Spurlock", "Starnes", "Sutton", "Tanner", "Teague", "Thorne", "Truitt", "Varney", "Wade", "Walker",
		"Weatherby", "Whitaker", "Winslow", "Yancy", "Youngblood", "Ainsley", "Bancroft", "Birch", "Brennan", "Cady",
		"Cutler", "Dawes", "Eastman", "Ferris", "Gale", "Hadley", "Hollis", "Jennings", "Kearney", "Larkin",
		"Lockhart", "Mabe", "Nichols", "Osgood", "Penrose", "Quaid", "Ridley", "Sloan", "Stockton", "Thatcher",
		"Underhill", "Vail", "Webb", "Wilder", "Yardley", "Ackerman", "Ashcroft", "Beckett", "Blackburn", "Bramble",
		"Buckner", "Caldwell", "Callahan", "Cantrell", "Clayborne", "Coffey", "Corbin", "Crowder", "Dillard", "Draper",
		"Dunbar", "Everhart", "Fairley", "Fletcher", "Frost", "Gilchrist", "Goforth", "Hammond", "Harper", "Hawley",
		"Hendricks", "Hollins", "Hooper", "Hunnicutt", "Jameson", "Kimball", "Kincaid", "Lattimore", "Ledbetter", "Lindsey",
		"Mahan", "Mayfair", "McAllister", "Medford", "Moffett", "Mullins", "Newberry", "Orr", "Pemberton", "Pollard",
		"Ramsey", "Ratliff", "Reeves", "Renfro", "Sampson", "Satterly", "Selby", "Sewell", "Shipley", "Stanton",
		"Stillwell", "Strickland", "Swain", "Tolliver", "Townsend", "Trimble", "Truman", "Upchurch", "Vaughn", "Waller",
		"Wardlow", "Wick", "Willoughby", "Womack", "Woodall", "Wren", "Yeager", "Zimmer", "Bostick", "Cuthbert",
		"Gaskill", "Hobbs", "Adair", "Akers", "Alcott", "Aldridge", "Allard", "Arnett", "Ashworth", "Ballard",
		"Banks", "Barton", "Beale", "Beckham", "Belcher", "Benson", "Bentley", "Biggs", "Billings", "Blaylock",
		"Boggs", "Bolton", "Bonner", "Bowers", "Bradford", "Bragg", "Brantley", "Bridger", "Briggs", "Brock",
		"Bryce", "Buckley", "Bunch", "Burgess", "Burleson", "Burnett", "Burrows", "Bush", "Butler", "Byers",
		"Cabell", "Cannon", "Capps", "Carlisle", "Cash", "Castle", "Chaffin", "Chambers", "Chapman", "Cheek",
		"Childress", "Clements", "Cline", "Coates", "Cochran", "Colby", "Collier", "Combs", "Compton", "Cooley",
		"Copeland", "Cotter", "Cowan", "Cox", "Crabtree", "Craddock", "Crane", "Crocker", "Crump", "Culver",
		"Cummings", "Curry", "Danforth", "Darnell", "Deal", "Decker", "Dennison", "Dickerson", "Dixon", "Dodd",
		"Dorsey", "Doss", "Dowd", "Drummond", "Dunlap", "Durham", "Dyer", "Earle", "Eaton", "Edgerton",
		"Elder", "Ellsworth", "Estes", "Ewing", "Fannin", "Farnsworth", "Felton", "Ferguson", "Finch", "Fisk",
		"Flagg", "Fleming", "Flint", "Forbes", "Fortune", "Foss", "Fox", "Franks", "Fuller", "Fulton",
		"Galloway", "Garner", "Garrett", "Gates", "Gibbs", "Giddings", "Gilmore", "Gladden", "Glover", "Goode",
		"Gordon", "Gough", "Graves", "Gridley", "Griggs", "Grubb", "Gunn", "Hackett", "Haines", "Hamlin",
		"Hancock", "Hardin", "Hardwick", "Harmon", "Hartley", "Hastings", "Hawkins", "Hayden", "Haynes", "Hazard",
		"Heath", "Henley", "Herndon", "Hickman", "Hill", "Hilliard", "Hinson", "Hitchcock", "Hodge", "Holcomb",
		"Holden", "Honeycutt", "Hopper", "Horne", "Houck", "Howell", "Hubbard", "Huff", "Hull", "Humphries",
		"Hutchins", "Inman", "Jarvis", "Jeffries", "Jewell", "Kendrick", "Kent", "Kerr", "Kidd", "Kilburn",
		"Kinney", "Kirby", "Knapp", "Knowles", "Lacey", "Lamb", "Lander", "Lawson", "Leach", "Lindley",
		"Linville", "Littlefield", "Lloyd", "Loftis", "Logan", "Love", "Lyon", "Macy", "Mallory", "Mann",
		"Manning", "Markham", "Marlow", "Massey", "Mathis", "Maxwell", "McCall", "McCoy", "McDaniel", "McKinney",
		"Mead", "Meeks", "Melton", "Metcalf", "Middleton", "Milburn", "Mims", "Mobley", "Monk", "Moss",
		"Munson", "Murdock", "Neal", "Neely", "Newcomb", "Newsom", "Norris", "Nutt", "Odell", "Ogden",
		"Oldham", "Orton", "Overstreet", "Owens", "Pace", "Packard", "Paine", "Parks", "Patton", "Paxton",
		"Peck", "Pennington", "Perkins", "Petty", "Pierce"
	};

	public static readonly string[] ArmyLast = AmericanLast.Concat(new string[2] { "Schupe", "Zannetakis" }).ToArray();

	public static readonly string[] AmericanTitles = new string[116]
	{
		"the Rattler", "Dead-Eye", "of Thieves' Landing", "the Scarred", "Half-Hanged", "the Grinner", "the Undertaker", "Two-Knives", "the Buzzard", "the Preacher",
		"Gravedigger", "the Mule", "Quickdraw", "of the Bayou", "Coyote", "Lucky", "the Butcher", "Gunsmoke", "Dry Gulch", "of Tumbleweed",
		"the Skinner", "Sidewinder", "the Deacon", "Hangtree", "the Vulture", "Blackwater", "of Big Valley", "the Kid", "Long Rifle", "the Mortician",
		"Iron Jaw", "of Cholla Springs", "Rawhide", "the Gambler", "Copperhead", "the Lame", "Firewater", "of Rio Bravo", "the Hermit", "Bullwhip",
		"the Drifter", "Lockjaw", "of the Heartlands", "Bad Penny", "the Tanner", "Dynamite", "the Card Sharp", "Hoss", "of Gaptooth Ridge", "Snake Oil",
		"the Preacher's Son", "One-Ear", "Saddle Sore", "Ten Cent", "Crooked Smile", "Whistler", "the Widowmaker", "Brushfire", "Lefty", "Shorty",
		"Slim", "Tex", "Blackjack", "the Parson", "Six-Gun", "Smokey", "Cactus", "Mesquite", "Sagebrush", "the Barber",
		"Buckshot", "Lariat", "Spurs", "Bootjack", "of Strawberry", "of Valentine", "of Armadillo", "of Annesburg", "of Emerald Ranch", "of Flatneck",
		"Horseshoe", "the Blacksmith", "the Cooper", "Tin Cup", "Ace", "Deuce", "the Hangman's Nephew", "Mule-Ear", "Sawbuck", "Wagon Wheel",
		"Cold Coffee", "Hardscrabble", "the Teamster", "Prairie Dog", "the Bone Collector", "Rimfire", "the Swede", "Doc", "the Professor", "Arkansas",
		"Tennessee", "Missouri", "Kansas", "Scratch", "Bad Eye", "Beans", "the Lawyer", "Sundown", "the Mustanger", "Tinhorn",
		"the Woodcutter", "Crowbait", "Turkey Neck", "Hogleg", "Flapjack", "Squint"
	};

	public static readonly string[] IrishFirst = new string[179]
	{
		"Jack", "Seamus", "Declan", "Cormac", "Fintan", "Padraig", "Eamon", "Ronan", "Niall", "Brendan",
		"Cillian", "Desmond", "Liam", "Tadhg", "Ciaran", "Dermot", "Fergal", "Aidan", "Bartley", "Colm",
		"Conor", "Donal", "Eoin", "Finbar", "Fionn", "Kieran", "Lorcan", "Malachy", "Michael", "Mick",
		"Paddy", "Rory", "Sean", "Shane", "Tiernan", "Turlough", "Cathal", "Diarmuid", "Oisin", "Peadar",
		"Ruairi", "Aengus", "Brian", "Colin", "Conall", "Dara", "Domhnall", "Enda", "Feargal", "Garret",
		"Lochlann", "Manus", "Mortimer", "Neasan", "Odhran", "Phelim", "Proinsias", "Rian", "Senan", "Ultan",
		"Ambrose", "Barry", "Cathair", "Christy", "Cian", "Connell", "Daithi", "Dominic", "Donncha", "Emmet",
		"Fachtna", "Flann", "Gerard", "Hugh", "Jarlath", "Kevin", "Laurence", "Malcolm", "Matthew", "Naoise",
		"Owen", "Pearse", "Redmond", "Ross", "Ryan", "Stiofan", "Thady", "Terence", "Vincent", "Hubert",
		"Alphonsus", "Anthony", "Augustine", "Bernard", "Bartholomew", "Cahir", "Canice", "Carroll", "Cyril", "Dan",
		"Daniel", "Denis", "Donogh", "Edmund", "Edward", "Eugene", "Fergus", "Festus", "Frank", "Harry",
		"Ignatius", "James", "Jerome", "Joseph", "Luke", "Mathias", "Maurice", "Miles", "Murtagh", "Nicholas",
		"Noel", "Oliver", "Oscar", "Pat", "Patrick", "Peter", "Philip", "Raymond", "Richard", "Robert",
		"Roger", "Sylvester", "Teague", "Timothy", "Walter", "William", "Willie", "Aloysius", "Bat", "Cornelius",
		"Darby", "Dinny", "Edmond", "Jeremiah", "Larry", "Ned", "Owney", "Paudie", "Phil", "Stephen",
		"Terry", "Christopher", "Colman", "Columba", "Cuan", "Dualta", "Fiachra", "Mel", "Moss", "Piaras",
		"Ruadhan", "Tomas", "Alastar", "Breandan", "Caoimhin", "Colum", "Conan", "Dallan", "Eanna", "Faolan",
		"Gearoid", "Lonan", "Riocard", "Seosamh", "Uinseann", "Patsy", "Barney", "Jem", "Myles"
	};

	public static readonly string[] IrishLast = new string[211]
	{
		"Webber", "Doyle", "Byrne", "Kavanagh", "Brennan", "Flanagan", "Gallagher", "Kilbride", "Lynch", "Mahoney",
		"McGuire", "Nolan", "O'Brien", "O'Hara", "O'Leary", "Quinn", "Regan", "Sheehan", "Sullivan", "Walsh",
		"Coughlan", "Dempsey", "Fitzgerald", "Hennessy", "Keane", "Lenihan", "Mulcahy", "Murphy", "O'Neill", "Rafferty",
		"Tierney", "Duffy", "Costello", "Hogan", "Moran", "Feeney", "Gilligan", "Hurley", "Kinsella", "Madigan",
		"Scanlon", "Boland", "Cahill", "Carrigan", "Cleary", "Conroy", "Corrigan", "Delaney", "Donnelly", "Egan",
		"Farrell", "Finnegan", "Garrity", "Heffernan", "Keogh", "Lanigan", "McCarthy", "McNally", "Moriarty", "Nugent",
		"O'Rourke", "O'Shea", "Phelan", "Quigley", "Riordan", "Shanahan", "Tobin", "Whelan", "Brady", "Coyne",
		"Duggan", "Ahern", "Barrett", "Behan", "Breen", "Callaghan", "Clancy", "Colgan", "Crowley", "Cullinane",
		"Daly", "Devlin", "Dolan", "Fahy", "Flynn", "Gaffney", "Geraghty", "Grogan", "Healy", "Hickey",
		"Joyce", "Kehoe", "Lally", "Lavelle", "MacNamara", "Mangan", "McGrath", "Meehan", "Mullan", "Noonan",
		"O'Connell", "O'Dwyer", "O'Sullivan", "Rooney", "Ryan", "Tuohy", "Barry", "Boyle", "Brophy", "Butler",
		"Canavan", "Carey", "Carroll", "Casey", "Cassidy", "Concannon", "Condon", "Connolly", "Connors", "Cooney",
		"Corcoran", "Cronin", "Cullen", "Cunningham", "Curran", "Deasy", "Dillon", "Dineen", "Dooley", "Dorgan",
		"Dowling", "Downey", "Doherty", "Dunne", "Fallon", "Fennelly", "Finn", "Fogarty", "Foley", "Galvin",
		"Gormley", "Griffin", "Hanley", "Hanlon", "Hannigan", "Harrington", "Hartigan", "Hayes", "Hegarty", "Higgins",
		"Horgan", "Houlihan", "Hughes", "Kearns", "Keating", "Kelleher", "Kelly", "Kennedy", "Kiely", "Kirwan",
		"Leahy", "Lonergan", "Loughlin", "Lucey", "Lyons", "McMahon", "Maher", "Mallon", "Malone", "Mannion",
		"McCann", "McCormack", "McDermott", "McDonagh", "McEvoy", "McGee", "McGovern", "McHugh", "McKenna", "McLoughlin",
		"McManus", "McNamee", "Monaghan", "Mooney", "Morrissey", "Mulligan", "Mullaney", "Mulvaney", "Naughton", "O'Connor",
		"O'Donnell", "O'Donovan", "O'Flaherty", "O'Grady", "O'Keefe", "O'Mahony", "O'Malley", "O'Reilly", "O'Toole", "Power",
		"Prendergast", "Quinlan", "Sheridan", "Spillane", "Sweeney", "Toomey", "Treacy", "Twomey", "Breslin", "Brogan",
		"Carney"
	};

	public static readonly string[] WapitiFirst = new string[40]
	{
		"Grey", "Black", "White", "Red", "Lone", "Little", "Tall", "Swift", "Iron", "Yellow",
		"Standing", "Running", "Walking", "Young", "Old", "High", "Quiet", "Spotted", "Hollow", "Painted",
		"Crooked", "Bright", "Strong", "Shining", "Silent", "Sleeping", "Dancing", "Rising", "Flying", "Many",
		"Sharp", "Broken", "Morning", "Evening", "Blue", "Wandering", "Short", "Charging", "Gentle", "Distant"
	};

	public static readonly string[] WapitiLast = new string[48]
	{
		"Hawk", "Elk", "Bear", "Wolf", "Horse", "Thunder", "Feather", "Bull", "Crow", "Eagle",
		"Shield", "Buffalo", "Antelope", "Owl", "Fox", "Lance", "Moon", "Cloud", "Rock", "Deer",
		"Wind", "Arrow", "Pony", "Badger", "Raven", "Otter", "Beaver", "Coyote", "Heron", "Magpie",
		"Turtle", "Lynx", "Ram", "Hail", "Sky", "River", "Stone", "Water", "Fire", "Snow",
		"Lightning", "Spear", "Bow", "Robe", "Pine", "Sun", "Grass", "Mountain"
	};

	public static readonly string[] WapitiExcluded = new string[26]
	{
		"Red Cloud", "Black Elk", "Running Antelope", "Little Wolf", "Tall Bull", "Lone Wolf", "Red Horse", "White Bull", "Yellow Wolf", "Black Hawk",
		"Spotted Elk", "Iron Hawk", "Young Bear", "Little Crow", "Spotted Eagle", "White Cloud", "Little Raven", "Black Coyote", "Flying Hawk", "Blue Horse",
		"Short Bull", "Little Thunder", "Yellow Robe", "Black Robe", "Broken Arrow", "Rising Sun"
	};

	public static readonly string[] WapitiTitles = new string[36]
	{
		"the Tracker", "the Horse Breaker", "the Bow Maker", "the Quiet One", "the Lookout", "the Elk Hunter", "the Runner", "the Rider", "of the Grizzlies", "the Scout",
		"Keeper of Horses", "the Arrow Maker", "the Watcher", "the Far Rider", "of the Dakota River", "the Patient", "the Storyteller", "Strong Arm", "the Healer", "the Fisherman",
		"the Trader", "the Hide Tanner", "the Bear Hunter", "the Peacemaker", "the Singer", "the Drummer", "the Firekeeper", "the Pathfinder", "the Trapper", "the Snow Walker",
		"Steady Hand", "the Swimmer", "the Wise", "of Cumberland Forest", "of Wapiti", "of O'Creagh's Run"
	};

	public static readonly string[] ODriscollTitles = new string[80]
	{
		"the Hound", "Red Hand", "the Hangman", "Knuckles", "the Fiddler", "Moonshine", "of Six Point Cabin", "of Hanging Dog Ranch", "the Wolf", "Black Powder",
		"the Mad Dog", "Two-Gun", "the Cutthroat", "Whiskey", "the Bogman", "Shillelagh", "the Tinker", "Banshee", "of Cumberland Forest", "the Bare-Knuckle",
		"Lucky Shamrock", "the Rope", "Colm's Dog", "Stonecold", "the Breaker", "the Leprechaun", "Pint", "Black Irish", "the Ferryman", "Peat Smoke",
		"the Docker", "Shamrock Jack", "the Wake", "Rotten Luck", "the Sexton", "Crowbar", "Ballycastle", "the Fenian", "Pikeman", "Gallows Bait",
		"the Galway Man", "of Cork", "of Kerry", "Donnybrook", "the Poteen Man", "Blackthorn", "the Navvy", "Rook", "the Bog Hound", "Ironfist",
		"the Shebeen", "the Kerryman", "the Corkonian", "Turf Cutter", "the Ribbonman", "Whiteboy", "Tipperary", "the Mayo Man", "Hurley Stick", "Crossroads",
		"the Coffin Ship", "the Crow", "Black Pudding", "Bodhran", "the Piper", "Harp", "the Rambler", "Sligo", "Donegal", "the Smuggler",
		"Rusty Nail", "Kilkenny Cat", "Gunner", "the Brawler", "Half-Pint", "Shiv", "the Ferret", "Gutter", "the Rat Catcher", "Dockside"
	};

	public static readonly string[] AmericanFemaleFirst = new string[234]
	{
		"Ada", "Adelaide", "Agnes", "Alice", "Almira", "Amelia", "Anna", "Annie", "Belle", "Bertha",
		"Bess", "Carrie", "Cassie", "Celia", "Charlotte", "Clara", "Cora", "Delia", "Dora", "Dorothea",
		"Edith", "Edna", "Effie", "Ella", "Ellen", "Elsie", "Emma", "Esther", "Etta", "Eudora",
		"Eunice", "Eva", "Evangeline", "Fannie", "Flora", "Florence", "Frances", "Georgia", "Gertrude", "Grace",
		"Hannah", "Harriet", "Hattie", "Hazel", "Helen", "Henrietta", "Hester", "Ida", "Imogene", "Inez",
		"Irene", "Iris", "Ivy", "Jane", "Jennie", "Josephine", "Julia", "Kate", "Laura", "Lavinia",
		"Lenora", "Letitia", "Lila", "Lillian", "Lizzie", "Lottie", "Louisa", "Lucinda", "Lucy", "Lula",
		"Mabel", "Mae", "Maggie", "Mamie", "Margaret", "Martha", "Maude", "Mercy", "Millie", "Minerva",
		"Minnie", "Myra", "Myrtle", "Nancy", "Nell", "Nettie", "Nora", "Olive", "Opal", "Ophelia",
		"Pearl", "Phoebe", "Polly", "Prudence", "Rachel", "Rebecca", "Rhoda", "Rosa", "Rose", "Ruth",
		"Sallie", "Sarah", "Selma", "Sophia", "Stella", "Temperance", "Theodosia", "Vera", "Viola", "Violet",
		"Virginia", "Winnie", "Zora", "Marion", "Shelby", "Morgan", "Kit", "Abbie", "Adah", "Addie",
		"Adella", "Agatha", "Alberta", "Alma", "Alta", "Amanda", "Amy", "Angeline", "Arabella", "Arvilla",
		"Augusta", "Aurelia", "Bettie", "Caroline", "Cecilia", "Christina", "Clarissa", "Cordelia", "Cynthia", "Daisy",
		"Della", "Dollie", "Drusilla", "Electa", "Eleanor", "Elizabeth", "Ellie", "Elmira", "Eloise", "Elvira",
		"Emeline", "Emily", "Ernestine", "Estella", "Ethel", "Eugenia", "Eveline", "Fidelia", "Frankie", "Freda",
		"Hallie", "Hilda", "Hortense", "Huldah", "Isabel", "Isabella", "Jemima", "Jessie", "Keziah", "Leila",
		"Lena", "Libbie", "Lida", "Lois", "Lou", "Louise", "Lovina", "Lucretia", "Lydia", "Malinda",
		"Malvina", "Marietta", "Marilla", "Mariah", "Matilda", "Melissa", "Melvina", "Mina", "Miranda", "Nan",
		"Naomi", "Nellie", "Nina", "Olivia", "Orpha", "Ora", "Pauline", "Philena", "Priscilla", "Rena",
		"Roxanna", "Samantha", "Serena", "Sibyl", "Sophronia", "Susanna", "Sylvia", "Tabitha", "Theresa", "Velma",
		"Verna", "Victoria", "Wilma", "Zilpha", "Callie", "Dovie", "Elnora", "Euphemia", "Flossie", "Gussie",
		"Ina", "Jessamine", "Lulu", "Nannie", "Ola", "Pearlie", "Sudie", "Vesta", "Zelma", "Dessie",
		"Alvira", "Belinda", "Charity", "Constance"
	};

	public static readonly string[] SouthernFemaleFirst = new string[100]
	{
		"Antoinette", "Augustine", "Belle", "Camille", "Celestine", "Clementine", "Coralie", "Delphine", "Dixie", "Eulalie",
		"Evangeline", "Honorine", "Josephine", "Lavinia", "Leona", "Lorena", "Magnolia", "Marguerite", "Mattie", "Odile",
		"Olympe", "Ophelia", "Philomene", "Savannah", "Seraphine", "Sidonie", "Tallulah", "Varina", "Willa", "Zelie",
		"Ruby", "Pearl", "Adeline", "Blanche", "Dorcas", "Easter", "Geneva", "Lettie", "Melisande", "Narcissa",
		"Octavia", "Permelia", "Rosalie", "Sabine", "Theodora", "Verbena", "Wilhelmina", "Beulah", "Docia", "Lurleen",
		"Aimee", "Alphonsine", "Amelie", "Anais", "Arcadie", "Aurore", "Azelie", "Bernadine", "Carmelite", "Cecile",
		"Celeste", "Clothilde", "Elodie", "Emerante", "Eugenie", "Euphrosine", "Felicite", "Genevieve", "Hermine", "Irma",
		"Laurentine", "Leontine", "Lucille", "Mathilde", "Melanie", "Modeste", "Nathalie", "Noemie", "Orelia", "Ozite",
		"Palmyre", "Pelagie", "Sylvanie", "Ursule", "Virginie", "Zulma", "Essie", "Mittie", "Pinkie", "Queenie",
		"Texie", "Vashti", "Corinna", "Arrie", "Lessie", "Ludie", "Dellie", "Lucinda", "Callie", "Dovie"
	};

	public static readonly string[] IrishFemaleFirst = new string[88]
	{
		"Aileen", "Aine", "Aoife", "Bernadette", "Bridget", "Brigid", "Caitlin", "Ciara", "Clodagh", "Deirdre",
		"Dervla", "Eileen", "Eithne", "Emer", "Fionnuala", "Grainne", "Honora", "Kathleen", "Maeve", "Mairead",
		"Maire", "Maureen", "Moira", "Muireann", "Nora", "Nuala", "Orla", "Peig", "Roisin", "Siobhan",
		"Sinead", "Sorcha", "Una", "Bronagh", "Ide", "Kitty", "Margaret", "Mary", "Delia", "Annie",
		"Ellen", "Catherine", "Johanna", "Winifred", "Ailbhe", "Bairbre", "Biddy", "Brid", "Bridie", "Cait",
		"Cliona", "Doireann", "Eibhlin", "Etain", "Gobnait", "Isibeal", "Ita", "Mairin", "Marcella", "Mona",
		"Nainsi", "Niamh", "Noreen", "Oonagh", "Peggy", "Rosaleen", "Sheila", "Sile", "Treasa", "Teresa",
		"Ailish", "Breda", "Dymphna", "Fidelma", "Finola", "Josie", "Nessa", "Philomena", "Rosanna", "Sabia",
		"Sadhbh", "Sarah", "Veronica", "Anne", "Julia", "Elizabeth", "Jane", "Ann"
	};

	public static readonly string[] FemaleTitles = new string[80]
	{
		"the Widow", "Calico", "Lace", "the Seamstress", "Two-Bit", "Silver Tongue", "the Schoolmarm", "Sharp-Eyes", "the Laundress", "Rosebud",
		"Hatpin", "Whiskey", "the Songbird", "Old Rose", "the Matron", "Parasol", "Blue Bonnet", "the Fortune Teller", "Diamond", "Ribbons",
		"Sugar", "Thimble", "the Spinster", "Copper", "Lilac", "the Preacher's Wife", "Prairie Rose", "Dimes", "the Cook", "Buttons",
		"Lantern", "of the Heartlands", "of Big Valley", "of the Bayou", "Pistol", "the Weaver", "Tumbleweed", "Shotgun", "Midnight", "the Gambler",
		"Peaches", "Honey", "the Milliner", "Bluebell", "the Nurse", "Lady Luck", "Satin", "the Telegrapher", "Ma", "Granny",
		"Little Bit", "the Midwife", "the Baker", "Whip", "Patches", "Molasses", "Cinnamon", "the Dressmaker", "Garter", "Sparrow",
		"Hummingbird", "the Rancher's Widow", "Mustang", "Spurs", "Sixgun", "Lucky Penny", "Red", "Goldie", "Starlight", "the Organist",
		"Mother Hen", "Cricket", "Bonnet", "Pepper", "Wildflower", "the Gunsmith's Daughter", "of Valentine", "of Strawberry", "of Saint Denis", "of Annesburg"
	};

	public static readonly string[] SouthernFirst = new string[210]
	{
		"Beauregard", "Jebediah", "Cletus", "Ezekiel", "Amos", "Josiah", "Boone", "Clayton", "Earl", "Virgil",
		"Otis", "Luther", "Hollis", "Rufus", "Wade", "Tobias", "Elias", "Cyrus", "Lemuel", "Ambrose",
		"Thaddeus", "Ransom", "Beau", "Delmar", "Merle", "Purvis", "Harlan", "Lyle", "Royce", "Walton",
		"Jubal", "Obadiah", "Zebulon", "Clement", "Erasmus", "Lucius", "Asa", "Hiram", "Absalom", "Tolliver",
		"Augustus", "Bartholomew", "Bedford", "Calhoun", "Cassius", "Dandridge", "Ellsworth", "Fitzhugh", "Gideon", "Hamilton",
		"Jefferson", "Lafayette", "Leonidas", "Marcellus", "Napoleon", "Octavius", "Peyton", "Randolph", "Seaborn", "Sumner",
		"Tecumseh", "Ulric", "Wardell", "Washington", "Zephaniah", "Bascom", "Coleman", "Dabney", "Emmitt", "Forrest",
		"Alcide", "Anselm", "Archibald", "Boyce", "Burrell", "Claiborne", "Cicero", "Crawford", "Dempsey", "Elam",
		"Evander", "Fielding", "Granville", "Hardy", "Hezekiah", "Isom", "Jasper", "Lamar", "Lucien", "Mattox",
		"Micajah", "Monroe", "Noah", "Pinckney", "Preston", "Quitman", "Roscoe", "Sidney", "Thornton", "Tyree",
		"Urban", "Vernon", "Wiley", "Yancy", "Zollie", "Alpheus", "Amasa", "Anderson", "Aubrey", "Bailey",
		"Benjamin", "Beverly", "Bradford", "Braxton", "Brantley", "Burwell", "Carter", "Cato", "Champ", "Chandler",
		"Cleon", "Clinton", "Columbus", "Darius", "Davis", "Dock", "Dorsey", "Drury", "Early", "Eldridge",
		"Emanuel", "Esom", "Ewell", "Fenner", "Ferrell", "Finis", "Fleming", "Furman", "Gaston", "Green",
		"Hampton", "Hardin", "Haywood", "Henderson", "Hilliard", "Hubbard", "Irby", "Isham", "Jarrett", "Jordan",
		"Junius", "Kinchen", "Lawson", "Leander", "Littleton", "Madison", "Major", "Manly", "Marshall", "Meriwether",
		"Milledge", "Newton", "Orlando", "Osborne", "Pleasant", "Pressley", "Redmond", "Rowland", "Sampson", "Shelton",
		"Sherrod", "Simeon", "Tandy", "Taylor", "Turner", "Tyler", "Warner", "Watkins", "Weldon", "Wilburn",
		"Wilkes", "Willis", "Woodson", "Zachary", "Adolphe", "Alphonse", "Amedee", "Aristide", "Armand", "Arsene",
		"Avit", "Baptiste", "Celestin", "Clovis", "Dieudonne", "Desire", "Dorsin", "Elphege", "Eloi", "Emile",
		"Etienne", "Euclide", "Evariste", "Fabien", "Felicien", "Gustave", "Hippolyte", "Honore", "Joachim", "Jules"
	};

	public static readonly string[] SouthernLast = new string[208]
	{
		"Hollister", "Tate", "Mayfield", "Boudreaux", "Thibodeaux", "Fontenot", "Haskins", "Gaines", "Crenshaw", "Holloway",
		"Rutledge", "Satterfield", "Merriweather", "Talbot", "Duvall", "Pettigrew", "Dabney", "Fairchild", "Lumpkin", "Purcell",
		"Rawlings", "Ashby", "Bledsoe", "Coker", "Beasley", "Tolbert", "Vance", "Whitfield", "Yancey", "Cartwright",
		"Dunaway", "Hatcher", "Larkin", "Moody", "Pickens", "Ruffin", "Stidham", "Tubbs", "Wyatt", "Abernathy",
		"Barksdale", "Beaumont", "Breckenridge", "Bullard", "Burnside", "Calloway", "Carmichael", "Chastain", "Culpepper", "Delacroix",
		"Dupree", "Fairbanks", "Garrard", "Guidry", "Hebert", "Hollingsworth", "Lafitte", "Ledoux", "Mabry", "Ogletree",
		"Prejean", "Robichaux", "Rountree", "Sanderford", "Thigpen", "Treadwell", "Vardaman", "Whitworth", "Woodruff", "Arceneaux",
		"Batiste", "Benoit", "Broussard", "Comeaux", "Cormier", "Daigle", "Dugas", "Fruge", "Gautreaux", "Hargis",
		"Hutto", "Jolivette", "Kilpatrick", "Landreneaux", "LeBlanc", "Mouton", "Naquin", "Ortego", "Pitre", "Primeaux",
		"Reynaud", "Savoie", "Sonnier", "Theriot", "Toups", "Venable", "Wigginton", "Bodine", "Crowley", "Dumas",
		"Fortenberry", "Hambrick", "Lightsey", "Gentry", "Ancelet", "Babineaux", "Bergeron", "Bourgeois", "Boutte", "Breaux",
		"Brignac", "Castille", "Chaisson", "Champagne", "Credeur", "Cheramie", "Darbonne", "Doucet", "Dupuis", "Duhon",
		"Fuselier", "Gaspard", "Guilbeau", "Guillory", "Istre", "Lalonde", "Lanclos", "Lejeune", "Leger", "Lemaire",
		"Lafleur", "Marcantel", "Melancon", "Menard", "Mire", "Montet", "Patin", "Perrin", "Petitjean", "Picard",
		"Poche", "Sibille", "Soileau", "Simoneaux", "Stelly", "Tauzin", "Trahan", "Vidrine", "Alford", "Applewhite",
		"Avery", "Bankston", "Barfield", "Beauchamp", "Blackwell", "Boatwright", "Bolling", "Brasfield", "Bridgewater", "Broadnax",
		"Bunkley", "Byrd", "Carraway", "Causey", "Chesnut", "Clopton", "Cothran", "Crumpton", "Dabbs", "Dowdy",
		"Drewry", "Edmondson", "Eppes", "Faircloth", "Fortson", "Fulcher", "Gholson", "Godwin", "Goolsby", "Hairston",
		"Hamby", "Harrelson", "Hinton", "Holcombe", "Hollowell", "Jernigan", "Kittrell", "Lassiter", "Lovett", "Mangum",
		"Massengill", "Munford", "Ogburn", "Overton", "Partain", "Peebles", "Pinson", "Ponder", "Poindexter", "Raines",
		"Rainwater", "Royster", "Sasser", "Scoggins", "Shackleford", "Stallings", "Strother", "Swanner"
	};

	public static readonly string[] RaiderTitles = new string[70]
	{
		"the Rebel", "Graycoat", "the Bayou Rat", "Lemoyne Lightning", "the Sharpshooter", "Old Glory", "the Deserter", "Swamp Fox", "the Firebrand", "Cottonmouth",
		"the Bushwhacker", "Shiloh", "the Turncoat", "Moonshine", "of Bluewater Marsh", "of Scarlett Meadows", "the Gator", "Long Rifle", "the Sawbones", "Hangfire",
		"the Cavalier", "Reb Yell", "Copperhead", "Pickett's Boy", "Magnolia", "the Planter", "Bourbon Bill", "Saltpeter", "the Picket", "Johnny Reb",
		"Spanish Moss", "Cane Brake", "the Bugler of Shiloh", "Muzzle-Loader", "the Carpetbagger Killer", "the Partisan", "Ironclad", "Grapeshot", "the Forager", "Bayou Ghost",
		"of Lagras", "of Rhodes", "of Bolger Glade", "of Fort Brennand", "the Vidette", "Chickamauga", "Manassas", "Vicksburg", "Cold Harbor", "Old Dixie",
		"the Sergeant", "Canister", "Fuse", "Muskrat", "Sorghum", "Pecan", "Butternut", "Gray Fox", "the Ranger", "the Courier",
		"Brimstone", "the Gunboat", "Lemoyne Thunder", "the Bugle", "Cane Knife", "Snapping Turtle", "the Magnolia Kid", "Powder Horn", "Sugar Cane", "Hush Puppy"
	};

	public static readonly string[] ArmyTitles = new string[70]
	{
		"Iron Boots", "the Bugler", "Bayonet", "Brass Buttons", "the Drill", "Ramrod", "Saber", "of Fort Wallace", "the Quartermaster", "Dog Tags",
		"the Marksman", "Sawtooth", "Gunpowder", "the Cavalryman", "Stonewall", "Reveille", "the Deserter", "Old Ironsides", "Muster", "Taps",
		"Hardtack", "Picket Duty", "the Recruit", "Spit-Shine", "Bugle Boy", "Garrison", "Long Tom", "the Sentry", "Canteen", "Bivouac",
		"Wallace Iron", "Rifle Pit", "the Drillmaster", "Double Time", "Blue Coat", "Mess Kit", "Spurs", "the Corporal", "Color Guard", "Saddle Soap",
		"the Farrier", "Muleskinner", "Gatling", "Springfield", "Trapdoor", "Carbine", "the Orderly", "Guidon", "Field Glass", "Forage Cap",
		"Kepi", "Coffee Boiler", "Shavetail", "Yellowleg", "Pony Soldier", "Brevet", "Old Army", "Short Rations", "the Paymaster", "Stockade",
		"Guardhouse", "Boots and Saddles", "Tattoo", "Lanyard", "the Signalman", "Heliograph", "of Fort Riggs", "of Fort Mercer", "the Adjutant", "Cartridge Box"
	};

	public static readonly string[] KlanTitles = new string[52]
	{
		"Pig-Eyes", "the Sheet", "Hoodwink", "Kerosene", "the Tinderbox", "Lantern-Jaw", "the Coward", "Buttons", "Mush-Mouth", "the Bedsheet",
		"Torch-Fumble", "Half-Wit", "the Lamp", "Matchstick", "Bucket-Head", "the Sweat", "Lamp-Oil", "Pinhead", "Mumbles", "the Draft",
		"Tripwire", "Two-Left-Feet", "Sooty", "the Hem", "Pillowcase", "Ash-Brain", "Wet-Wick", "Nightshirt", "Smudge", "Stubbed-Toe",
		"Bed-Linen", "Moth-Eaten", "Sheet-Snag", "Damp Fuse", "Tangle-Foot", "the Stumble", "Drip-Wax", "Eyeholes", "Crooked Hood", "Slack-Jaw",
		"Gas-Bag", "Blowhard", "the Bluster", "Thimble-Wit", "Bean-Brain", "Puddle", "Hem-Tripper", "Dunce-Cap", "Hiccup", "Scorch-Pants",
		"Tallow-Head", "Nightcap"
	};

	public static readonly string[] ItalianFirst = new string[101]
	{
		"Spencer", "Salvatore", "Giuseppe", "Vincenzo", "Antonio", "Carmine", "Luigi", "Domenico", "Rocco", "Francesco",
		"Gaetano", "Pasquale", "Nunzio", "Ignazio", "Calogero", "Rosario", "Benedetto", "Enzo", "Aldo", "Bruno",
		"Cosimo", "Dario", "Emilio", "Fabrizio", "Gennaro", "Leonardo", "Marco", "Nicola", "Orazio", "Paolo",
		"Raffaele", "Sergio", "Tommaso", "Umberto", "Vito", "Achille", "Baldassare", "Corrado", "Filippo", "Gino",
		"Lorenzo", "Mariano", "Ottavio", "Pietro", "Santo", "Teodoro", "Carlo", "Michele", "Matteo", "Saverio",
		"Agostino", "Alessandro", "Alfonso", "Alfredo", "Amedeo", "Arcangelo", "Armando", "Arturo", "Attilio", "Augusto",
		"Bartolomeo", "Battista", "Biagio", "Carmelo", "Cesare", "Ciro", "Clemente", "Costantino", "Cristoforo", "Damiano",
		"Donato", "Edoardo", "Egidio", "Elio", "Ercole", "Ernesto", "Ettore", "Eugenio", "Fedele", "Federico",
		"Felice", "Ferdinando", "Fiorenzo", "Fortunato", "Franco", "Generoso", "Giacomo", "Giovanni", "Giorgio", "Girolamo",
		"Giulio", "Guglielmo", "Leopoldo", "Liborio", "Luciano", "Mario", "Massimo", "Mauro", "Natale", "Onofrio",
		"Oreste"
	};

	public static readonly string[] ItalianLast = new string[131]
	{
		"Moorhead", "Esposito", "Russo", "Romano", "Ricci", "Marino", "Greco", "Bruno", "Gallo", "Conti",
		"De Luca", "Costa", "Giordano", "Mancini", "Rizzo", "Lombardi", "Moretti", "Barbieri", "Fontana", "Santoro",
		"Mariani", "Rinaldi", "Caruso", "Ferrara", "Galli", "Martini", "Leone", "Longo", "Gentile", "Martinelli",
		"Vitale", "Lombardo", "Serra", "Coppola", "De Santis", "D'Angelo", "Marchetti", "Parisi", "Villa", "Conte",
		"Ferraro", "Ferri", "Fabbri", "Bianco", "Marini", "Grasso", "Valentini", "Messina", "Sala", "De Angelis",
		"Gatti", "Pellegrini", "Palumbo", "Sanna", "Farina", "Rizzi", "Monti", "Cattaneo", "Morelli", "Amato",
		"Silvestri", "Mazza", "Testa", "Grassi", "Pellegrino", "Carbone", "Abate", "Albanese", "Amoroso", "Aiello",
		"Battaglia", "Bellini", "Benedetti", "Bianchi", "Bonanno", "Borrelli", "Calabrese", "Cappello", "Caputo", "Cardinale",
		"Castellano", "Catalano", "Cirillo", "Colombo", "Cuccia", "D'Amico", "De Rosa", "Di Stefano", "Donati", "Fiore",
		"Fusco", "Gagliardi", "Giuliani", "Grieco", "Iannone", "La Rosa", "Lanza", "Leto", "LoBianco", "Maggio",
		"Marchese", "Mazzola", "Melillo", "Napoli", "Orlando", "Palermo", "Pagano", "Perrone", "Piazza", "Pugliese",
		"Puglisi", "Randazzo", "Riccio", "Rossi", "Ruggiero", "Sabatini", "Scalise", "Schiavone", "Sciortino", "Spada",
		"Sorrentino", "Tedesco", "Toscano", "Trapani", "Vaccaro", "Valenti", "Zito", "Zappala", "Cusumano", "Di Marco",
		"Esposti"
	};

	public static readonly string[] MobTitles = new string[57]
	{
		"the Ox", "Two Knives", "the Accountant", "the Docker", "Sweet Talk", "the Undertaker", "Knuckles", "the Priest", "Gloves", "the Barber",
		"Silk", "the Tailor", "Mezzanotte", "the Wharf Rat", "the Collector", "Velvet", "the Sicilian", "the Neapolitan", "Cufflinks", "the Butcher of Lannahechee",
		"Ledger", "the Choirboy", "Fat Sal", "the Smile", "Stiletto", "the Gentleman", "Hatpin", "the Nephew", "the Fish", "the Cardinal",
		"Fingers", "the Baker", "Dominoes", "the Florist", "Ice Pick", "the Bishop", "Marble", "the Lamplighter", "the Shoemaker", "Saint",
		"the Harbormaster", "Cannonball", "Bootblack", "Garrote", "the Mute", "Nine Lives", "the Calabrian", "the Roman", "the Count", "the Bookmaker",
		"Snake Eyes", "the Fixer", "Muscles", "the Vintner", "Grappa", "Vespers", "Dock Hook"
	};

	public static readonly string[] BackwoodsFirst = new string[83]
	{
		"Nicholas", "Ezekiel", "Jebediah", "Cletus", "Obadiah", "Zebulon", "Hezekiah", "Abner", "Uriah", "Enoch",
		"Lemuel", "Amos", "Silas", "Josiah", "Gideon", "Thaddeus", "Ephraim", "Elkanah", "Malachi", "Absalom",
		"Jethro", "Hiram", "Asa", "Eli", "Lazarus", "Nehemiah", "Barnabas", "Caleb", "Dathan", "Ishmael",
		"Jubal", "Lot", "Mordecai", "Noah", "Reuben", "Shadrach", "Tobit", "Zeke", "Rufus", "Vester",
		"Orrin", "Purvis", "Ahab", "Archelaus", "Azariah", "Bezaleel", "Boaz", "Cain", "Cephas", "Eber",
		"Elihu", "Gershom", "Jabez", "Jair", "Japheth", "Jehu", "Jephthah", "Jeremiah", "Joab", "Jonas",
		"Kish", "Laban", "Lamech", "Lige", "Manasseh", "Methuselah", "Nahum", "Nimrod", "Phinehas", "Rehoboam",
		"Shem", "Simeon", "Zadok", "Zedekiah", "Zophar", "Abijah", "Benaiah", "Hosie", "Jasher", "Melchizedek",
		"Uzziah", "Zenas", "Cooter"
	};

	public static readonly string[] MurfreeLast = new string[26]
	{
		"Murfree", "Murfree", "Murfree", "Murfree", "Murfree", "Murfree", "Hollins", "Pike", "Tunstall", "Gorham",
		"Riddle", "Coffey", "Starling", "Murfree", "Murfree", "Murfree", "Murfree", "Murfree", "Murfree", "Tackett",
		"Slone", "Hensley", "Caudill", "Couch", "Ritchie", "Sizemore"
	};

	public static readonly string[] MurfreeTitles = new string[42]
	{
		"the Hollow", "Rot-Tooth", "the Kin", "Crow", "the Skinner", "Gristle", "the Quiet One", "Mudlark", "the Watcher", "Ashes",
		"Bone-Picker", "the Elder's Boy", "Sallow", "Lantern", "the Cousin", "Hush", "the Pit", "Rattle", "Blackwater Bill", "the Brother-Uncle",
		"Gravedigger", "Possum", "the Cellar", "Sinkhole", "Moss-Back", "Hog-Tooth", "the Holler", "Lye", "the Shrike", "Hollow-Eye",
		"Grub", "the Night Kin", "Blackroot", "Thornback", "Ratbone", "Dirt-Nail", "the Hound's Boy", "Ridge-Runner", "Copper Kettle", "Gourd",
		"Burlap", "the Woodpile"
	};

	public static readonly string[] SkinnerLast = new string[24]
	{
		"Skinner", "Skinner", "Skinner", "Skinner", "Skinner", "Hatch", "Crowe", "Ravel", "Burke", "Tull",
		"Marsh", "Vane", "Skinner", "Skinner", "Skinner", "Skinner", "Skinner", "Harker", "Stroup", "Kell",
		"Croft", "Bragg", "Gault", "Tolle"
	};

	public static readonly string[] SkinnerTitles = new string[42]
	{
		"the Scalper", "Elk-Hide", "the Hermit", "Bear-Tooth", "the Tanner", "Two Pelts", "the Flayer", "Lynx", "the Trapper", "Pine Knot",
		"Red Hand", "the Hide", "Wolfbait", "Grizzly", "the Fur", "Saw-Tooth", "the Old Brother", "Snowblind", "Tallow", "Gutshot",
		"Antler", "the Fleshing Knife", "Bighorn", "Marten", "Wolverine", "Beaver Pelt", "the Mountain", "Cold Camp", "Frostbite", "the Bone Saw",
		"Hatchet", "Pemmican", "Buckskin", "Ermine", "Musk", "Deadfall", "Snare", "the Taxidermist", "Ridge Wolf", "Scalp-Belt",
		"Grease", "the Hide Scraper"
	};

	public static readonly string[] MexicanFirst = new string[53]
	{
		"Alejandro", "Andres", "Armando", "Bartolo", "Benito", "Carlos", "Cipriano", "Diego", "Domingo", "Eduardo",
		"Emilio", "Esteban", "Faustino", "Felipe", "Fermin", "Gaspar", "Gregorio", "Guillermo", "Hector", "Ignacio",
		"Isidro", "Jacinto", "Javier", "Joaquin", "Jose", "Juan", "Julian", "Leandro", "Lorenzo", "Lucio",
		"Manuel", "Marcos", "Mateo", "Miguel", "Nicolas", "Octavio", "Pablo", "Pascual", "Pedro", "Rafael",
		"Ramiro", "Ramon", "Raul", "Ricardo", "Rodrigo", "Ruben", "Salvador", "Santiago", "Teodoro", "Tomas",
		"Ulises", "Valentin", "Vicente"
	};

	public static readonly string[] MexicanLast = new string[50]
	{
		"Aguilar", "Alvarado", "Arriaga", "Bautista", "Benavides", "Cabrera", "Calderon", "Camacho", "Castillo", "Cervantes",
		"Contreras", "Delgado", "Dominguez", "Escobar", "Espinoza", "Estrada", "Fuentes", "Gallegos", "Garza", "Guerrero",
		"Ibarra", "Juarez", "Lara", "Leal", "Luna", "Maldonado", "Medina", "Mendoza", "Montoya", "Navarro",
		"Ochoa", "Orozco", "Pacheco", "Padilla", "Quintero", "Ramos", "Rios", "Robles", "Rojas", "Salazar",
		"Sandoval", "Serrano", "Soto", "Tapia", "Trevino", "Valdez", "Vargas", "Vega", "Villa", "Zamora"
	};

	public static readonly string[] DelLoboTitles = new string[35]
	{
		"el Lobo", "the Wolf", "el Gato", "Scorpion", "el Diablo", "Sidewinder", "el Flaco", "Rattlesnake", "el Gordo", "the Vulture",
		"el Tuerto", "One-Eye", "Coyote", "el Cuervo", "the Crow", "Mesquite", "el Machete", "Dust Devil", "el Chacal", "Gila",
		"el Zurdo", "Lefty", "Cactus", "el Rayo", "Lightning", "Bandolero", "el Guapo", "Tequila", "Buzzard", "el Silencio",
		"the Knife", "Arroyo", "el Toro", "Spur", "Viper"
	};

	public static readonly string[] LaramieTitles = new string[30]
	{
		"Brand-Changer", "Long Rope", "the Drover", "Tumbleweed", "Six-Gun", "Cinch", "the Wrangler", "Mustang", "Stirrup", "Dusty",
		"Rawhide", "the Kid", "Hoss", "Lasso", "Saddle Sore", "the Foreman", "Red", "Slim", "Calico", "Prairie Dog",
		"Running Iron", "Maverick", "Sagebrush", "Chaps", "the Branding Iron", "Dogie", "Bronc", "Pinto", "Buckshot", "Barbed Wire"
	};

	public static readonly string[] CajunFirst = new string[33]
	{
		"Achille", "Adolphe", "Alcide", "Amedee", "Anatole", "Aristide", "Baptiste", "Bertrand", "Celestin", "Clovis",
		"Desire", "Dorsin", "Elphege", "Euclide", "Evariste", "Fernand", "Gaston", "Hippolyte", "Hubert", "Jules",
		"Lucien", "Marcel", "Narcisse", "Octave", "Onezime", "Ovide", "Placide", "Remy", "Sosthene", "Theophile",
		"Ulysse", "Valery", "Zenon"
	};

	public static readonly string[] CajunLast = new string[24]
	{
		"Arceneaux", "Babineaux", "Bergeron", "Boudreaux", "Breaux", "Comeaux", "Doucet", "Dugas", "Fontenot", "Guidry",
		"Hebert", "LaBove", "Landry", "LeBlanc", "Melancon", "Mouton", "Naquin", "Ortego", "Prejean", "Robichaux",
		"Savoie", "Thibodeaux", "Trahan", "Verret"
	};

	public static readonly string[] NightFolkTitles = new string[20]
	{
		"the Lantern", "Cottonmouth", "the Drowned", "Gator", "the Hush", "Spanish Moss", "the Whisper", "Mudfish", "the Hollow-Eyed", "Cypress",
		"the Croak", "Bog-Lily", "the Smiler", "Marsh Light", "the Stillwater", "Snapping Turtle", "the Grin", "Heron", "the Mire", "Black Water"
	};

	public static string Surname(string full)
	{
		if (string.IsNullOrEmpty(full))
		{
			return "";
		}
		int num = full.IndexOf(' ');
		if (num >= 0)
		{
			return full.Substring(num + 1);
		}
		return "";
	}
}
