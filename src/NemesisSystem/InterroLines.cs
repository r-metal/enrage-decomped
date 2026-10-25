using System;
using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

internal static class InterroLines
{
	private static readonly Dictionary<string, Dictionary<string, string[]>> ByFaction = new Dictionary<string, Dictionary<string, string[]>>
	{
		["odriscoll"] = new Dictionary<string, string[]>
		{
			["open"] = new string[16]
			{
				"Get yer hands off me, ye feckin' animal.", "Van der Linde's dog. I knew it.", "Colm'll hear about this, boyo.", "Ye picked the wrong Irishman to grab.", "Go on then. What d'ye want?", "Jaysus, the smell on ye. What is it?", "Ye think ye scare me? I've been beaten by better on a Sunday.", "Dutch send ye? Course he did. Coward.", "Ah, the famous Arthur Morgan. Dutch's errand boy.", "Let go o' me collar before I bite yer hand off.",
				"Ye'll be sorry ye ever crossed the boys, so ye will.", "Well? Out with it. I haven't got all day.", "Is this how Dutch's lot say hello now?", "Ye've a death wish, grabbin' one of Colm's own.", "Feck off and die somewhere quiet.", "Ye want somethin'? Ask nice and I might laugh."
			},
			["open_scared"] = new string[8] { "Jaysus, Mary and Joseph... don't kill me.", "Easy, easy! I'm just a lad from Cork, I'm nobody!", "Please, mister - I only ride with 'em for the money.", "Mother of God. Alright. Alright, what d'ye want?", "Don't! I've done nothin' to Dutch, I swear it!", "Holy Saint Patrick... put the gun down, please.", "I'm only new, mister, I don't know a thing!", "Ah God, ah God, I don't want to die in this country." },
			["refuse"] = new string[18]
			{
				"I'll tell ye nothin', ye Van der Linde scum.", "Colm'd have me tongue out. I'd rather it was you.", "Go and boil yer head.", "Ye can kill me. Colm'll kill ye slower.", "Not a feckin' word, boyo.", "We don't squeal. Not to Dutch's lot.", "Ask yer precious Dutch. He knows all about runnin'.", "I've seen Pinkertons try harder than you.", "Ye'll get more out of a dead sheep.", "Ye'll get the same from me as the Pinkertons get from Colm. Nothin'.",
				"Away with ye.", "Me lips are sealed tighter than a priest's purse.", "Talkin' to ye? I'd sooner drink English beer.", "Ye want answers? Go to confession.", "Colm's been hunted half his life. He didn't last this long with rats about.", "I'll take it to me grave and spit on yers.", "There's nothin' ye can do to me that Colm can't do worse.", "Ask Dutch about Colm's brother. Then ask me nothin'."
			},
			["refuse_scared"] = new string[8] { "I can't! Colm hangs rats from the trees, ye don't understand!", "Please - he'd find out. He always finds out.", "They'll string me up at Six Point if I talk. Please...", "I swear on me mother, I can't say!", "He'd skin me, mister, he'd skin me alive!", "Please, ye don't know what he does to talkers!", "I've seen what's left of the last fella who blabbed. Please.", "Don't make me. Colm's got ears everywhere." },
			["lock"] = new string[6] { "That's all ye'll get about that.", "Ask me that again and ye'll be askin' a corpse.", "Not that. Never that.", "Ye can ask the Pope. Ye'll get nothin' more.", "That's the end of it, boyo.", "Me mouth's shut on that, and it's stayin' shut." },
			["hit"] = new string[6] { "Ha! Ye punch like a Protestant.", "That's it? Me granny hits harder.", "Ye'll pay for that, boyo.", "Is that it? Me sister's got a better right hook.", "Ha! I've had worse in a Dublin pub.", "Go on, ye've loosened a tooth. Proud of yerself?" },
			["hit_scared"] = new string[6] { "Stop! Christ, stop!", "Not the face, not the face!", "Alright! Alright, ye made yer point!", "Mercy! Holy Mother, mercy!", "No more, I'm beggin' ye!", "Alright, alright, Jaysus, alright!" },
			["cut"] = new string[4] { "Ye cut me, ye feckin'... !", "Blood for blood. Remember that.", "Ah, ye dirty... that'll scar.", "Ye'll get that back tenfold, boyo." },
			["cut_scared"] = new string[4] { "Jaysus, I'm bleedin'! Stop!", "Put it away, put the blade away!", "Ah God, it won't stop bleedin'!", "Not the knife, please, not the knife!" },
			["shot"] = new string[4] { "Aagh! Ye shot me! Ye shot me, ye bastard!", "Colm'll have yer heart for that!", "Ye shot me in the leg! Ye feckin' animal!", "That's a bullet ye'll get back, Morgan." },
			["shot_scared"] = new string[4] { "I'll talk! Don't shoot me again, I'll talk!", "Holy God... alright, ALRIGHT!", "Stop, stop! Mother of God, I'm hit!", "I'll say whatever ye want! Just no more!" },
			["near"] = new string[4] { "Ye missed. Ye always miss.", "Is that meant to scare me?", "Ha! Dutch teach ye to shoot like that?", "Close. Not close enough, boyo." },
			["near_scared"] = new string[4] { "Jaysus! That near took me ear off!", "Don't! Don't, I'm beggin' ye!", "Holy God! Mind where ye point that!", "Alright! Christ, ye're mad!" },
			["threat_defy"] = new string[6] { "Do it then. Colm'll avenge me.", "Ye haven't the stones.", "I'll see ye in hell, Morgan.", "Ye'll be dead in a ditch within the week.", "Go on. The boys'll drink to me tonight.", "Threaten all ye like. I'm Irish. We're born to suffer." },
			["threat_scared"] = new string[4] { "No! No, don't, please - I've a sister in Boston!", "Mother of God, don't!", "Please, I'll go back to Ireland, ye'll never see me again!", "Don't! Don't, I'm too young!" },
			["threat"] = new string[4] { "Big talk for a Van der Linde.", "Is that right, boyo?", "Colm's said worse to me over breakfast.", "We'll see who's sayin' that in a month." },
			["bribe_loyal"] = new string[4] { "Keep yer dirty dollars. Colm's family.", "Ye think I'd sell me own for Van der Linde money?", "Shove yer money where the sun don't shine.", "Colm took me in when I'd nothin'. Ye can't buy that." },
			["bribe_greedy"] = new string[4] { "Now there's a language I speak.", "Ah, money. Ye should've led with that, boyo.", "Jaysus, that's more than Colm pays in a month.", "Now ye're talkin' sense, so ye are." },
			["bribe_rich"] = new string[2] { "Ye think that impresses me? I've robbed better men than you of more.", "Pocket change. I've drunk more than that in a night." },
			["bribe_poor"] = new string[2] { "God bless ye... I haven't eaten in two days.", "That'll put me halfway to a boat home. Alright, ask." },
			["bribe"] = new string[2] { "Money's money. Ask, then.", "A few dollars won't make me a rat. But go on." },
			["lead"] = new string[6] { "Fine. Fine, ye bastard.", "Alright, alright...", "Ye didn't hear this from me.", "Swear ye'll never say where ye heard it.", "Christ, Colm'll kill me... alright.", "Here's the truth, for what it's worth." },
			["lead_scared"] = new string[6] { "Alright! I'll tell ye, I'll tell ye!", "God forgive me...", "Don't let Colm know it was me...", "I'll tell ye everythin', just put it down!", "Alright, alright, holy God...", "Please, it's all I know, I swear!" },
			["letgo"] = new string[6] { "Ye're lettin' me walk? Ye're softer than they say.", "I'll not forget this, boyo. Not sure yet which way.", "Mad. Ye're mad. ...Thank ye.", "Next time we meet, I'll pretend I never saw ye.", "Don't expect a pint for it.", "Colm'd have shot me. Strange world." },
			["letgo_scared"] = new string[5] { "God bless ye, God bless ye, I'm gone!", "Ye'll not see me again, I swear on Saint Patrick!", "Thank ye, thank ye - I'm off to Boston, so I am!", "I'm away, mister. I'm away!", "Mother Mary, I'm still breathin'..." },
			["bargain_pay"] = new string[3] { "Here - ${dollars}, every cent. Take it and I was never here.", "${dollars}. That's me boat fare home. Take it.", "Ye want paid? ${dollars}. Now let me up, for God's sake." },
			["bargain_inform"] = new string[3] { "I'll tell ye where the boys ride. Every job. Just let me breathe.", "Colm'd skin me for this... aye, I'll send word before every job.", "I'll be yer ears in the camp, God forgive me." },
			["bargain_owe"] = new string[3] { "Then I owe ye one. One, mind.", "An Irishman pays his debts. Once. Then we're square.", "I'll stand down the next time, boyo. Just the once." },
			["bargain_leave"] = new string[3] { "I'll be on the next boat. Ye'll never see me again.", "I'm done with the boys and done with this country. I swear it.", "I'll ride so far ye'll need a map to hate me." },
			["bargain_again"] = new string[2] { "I already bought me life off ye once. Go on and finish it.", "We had a deal last time. There's no second deal, boyo." },
			["bargain_plead"] = new string[3] { "I've a mam in Cork, mister! Let me go home to her!", "I'm nobody, I swear! Let me walk and ye'll never see me!", "Please - I only joined for the grub!" }
		},
		["lemoyneraiders"] = new Dictionary<string, string[]>
		{
			["open"] = new string[12]
			{
				"Unhand me, you damned carpetbagger.", "You ain't from Lemoyne. I can smell it on you.", "Another Yankee thinks he owns this state.", "I fought at Petersburg. You think you scare me?", "The General will hear of this.", "This is Lemoyne soil you're standing on, mister.", "Get your filthy hands off a veteran.", "{leader}'ll have you strung up at Fort Brennand.", "Lemoyne don't need your kind, mister.", "You some Yankee lawman? Or just a thief?",
				"I've been in tighter spots than this. Ask Sherman.", "Well? Say your piece, carpetbagger."
			},
			["open_scared"] = new string[6] { "Easy now... easy. I'm just a farmer, I swear it.", "Don't shoot - I got a wife in Rhodes!", "Lord have mercy... what do you want with me?", "Please, mister, I only joined up for the meals.", "I wasn't even at the war, I swear it!", "Lord, don't let it end like this. Not here." },
			["refuse"] = new string[16]
			{
				"I'd sooner kiss a Yankee boot.", "I didn't talk to Sherman's men. I sure won't talk to you.", "The Cause ain't dead, mister. Neither's my mouth shut.", "Go on back north where you belong.", "You'll get nothing from a son of Lemoyne.", "My daddy died at Shiloh with his mouth shut. I'll do the same.", "The General treats his men right. You won't turn me.", "Carpetbagger questions get carpetbagger answers. None.", "I've held my tongue before Yankee officers. You're nothing next to them.", "Lemoyne keeps its own secrets.",
				"You'll have to shoot me, Yankee.", "{leader} never broke faith with us. I won't break it with him.", "I'll tell you where to go. North.", "Not one word. Not for you, not for any Yankee.", "We lost the war. We ain't lost our pride.", "Ask the carpetbaggers. They know everything that's for sale."
			},
			["refuse_scared"] = new string[6] { "They'd hang me from the courthouse oak if I talked.", "Please... the General don't forgive.", "I can't. They'll burn my place. My wife's in it.", "They'd tar and feather me, mister. Worse.", "Please, {leader} has men in every parish!", "I can't talk. They'd know it was me." },
			["lock"] = new string[4] { "That's my last word on it, Yankee.", "Ask all you like. That door's shut.", "I'm done talking, Yankee.", "That's buried, mister. Deep as the Cause." },
			["hit"] = new string[6] { "I've been hit by Union shot. That's nothing.", "You hit like a Yankee quartermaster.", "Is that how they fight up north?", "My mammy hit harder with a switch.", "That all the North taught you?", "Go on. I've had my teeth knocked out before." },
			["hit_scared"] = new string[4] { "Enough! Lord, enough!", "Stop, stop - I'm an old soldier!", "Lord have mercy! No more!", "Please! I'm done, I'm done!" },
			["cut"] = new string[4] { "You'll hang for that, boy.", "Cut me, then. Lemoyne bleeds for its own.", "Yankee bayonets did worse at Chancellorsville.", "That'll cost you, Yankee. Mark it." },
			["cut_scared"] = new string[4] { "Oh God, the blood - stop!", "Don't, please! I'll bleed out!", "Lord, I'm cut open! Stop!", "Put that blade down, please, sir!" },
			["shot"] = new string[4] { "Argh! The General will skin you!", "Damn you to hell!", "Shot by a carpetbagger. Of all the luck.", "Argh! You yellow-bellied Yankee!" },
			["shot_scared"] = new string[4] { "Mercy! I'll say it, I'll say anything!", "Don't shoot again! I yield, I yield!", "Lord Jesus, I'll talk! I'll talk!", "No more! No more, I surrender!" },
			["near"] = new string[4] { "You call that shooting?", "The boys at Antietam shot closer than that.", "Hah. Sherman's boys couldn't shoot neither.", "You'll need more practice than that." },
			["near_scared"] = new string[4] { "Jesus Christ! Don't!", "Alright! Lord, alright!", "Lord! You near killed me!", "Easy! Easy with that!" },
			["threat_defy"] = new string[4] { "I've stared down worse at Gettysburg.", "Do your worst. Lemoyne remembers.", "Lemoyne buried better men than you.", "I've had Yankees threaten me since I was fifteen." },
			["threat_scared"] = new string[4] { "No sir! Please, sir!", "I got children, mister. Please.", "Please, mister, I'll leave the county!", "Don't, sir! I'm begging you!" },
			["threat"] = new string[3] { "Yankee talk.", "You talk mighty big for a carpetbagger.", "We'll see, mister. We'll see." },
			["bribe_loyal"] = new string[4] { "Yankee dollars. Keep 'em.", "I don't sell out my own, not for any price.", "I didn't fight four years to be bought by your kind.", "Keep your blood money, Yankee." },
			["bribe_greedy"] = new string[4] { "Well now. Money's money, whoever's it is.", "A man's got to eat, even in Lemoyne.", "Well, the Cause don't pay much these days.", "Lemoyne's a poor state, mister. I'll take it." },
			["bribe_rich"] = new string[2] { "My family had land here before your kind came. Keep it.", "I don't need Yankee charity." },
			["bribe_poor"] = new string[2] { "Lord knows the war took everything. Alright.", "That'll feed my young'uns a week. Ask." },
			["bribe"] = new string[2] { "Money. Well, it's something.", "Alright. But this don't make us friends." },
			["lead"] = new string[6] { "Fine. But you didn't get it from me.", "Alright, damn you.", "Listen close, 'cause I won't say it twice.", "You want the truth? Here it is.", "God help me. Alright.", "Keep this between us and the Lord." },
			["lead_scared"] = new string[4] { "Alright, alright! God forgive me...", "I'll tell you - just don't hurt me no more.", "Alright, I'll tell it! Just put that down!", "Mercy, I'll tell you all of it!" },
			["letgo"] = new string[6] { "Mercy from a Yankee. Don't that beat all.", "I'll remember this, mister. Can't say how yet.", "You're lettin' me walk? Huh. Alright.", "Don't think this makes us friends.", "The General'd have hanged you. Fool.", "Much obliged, I suppose." },
			["letgo_scared"] = new string[5] { "Bless you, sir. Bless you!", "I'm goin', I'm goin' - back to the farm!", "Lord be praised. I'm gone.", "Thank you kindly, mister - you won't see me again.", "I'll pray for you, sir. Honest I will." },
			["bargain_pay"] = new string[3] { "${dollars}. Every Yankee dollar I own. Take it.", "Here's ${dollars} - take it and let a man go home.", "${dollars}. The war took the rest." },
			["bargain_inform"] = new string[3] { "I'll tell you where the boys ride. Every job, before they mount up.", "God help me... I'll send word of what the General's planning.", "I'll be your man inside. Just let me live." },
			["bargain_owe"] = new string[3] { "Then I owe you a debt. A Southern man pays his debts.", "One time, mister. I'll back off one time. Then we're even.", "On my honor, I owe you. Once." },
			["bargain_leave"] = new string[3] { "I'll go west. Far from Lemoyne. You have my word.", "I'm done with the Cause. I'll leave the state tonight.", "I'll take my family and go. You'll never see me again." },
			["bargain_again"] = new string[2] { "I already bought my life off you. That's all the mercy there is.", "We made a deal last time, Yankee. Don't push it." },
			["bargain_plead"] = new string[3] { "I got young'uns in Rhodes, mister! Please!", "I only rode with 'em for the pay! Let me go home!", "I'm an old soldier, sir. Let me die in my bed." }
		},
		["army"] = new Dictionary<string, string[]>
		{
			["open"] = new string[12]
			{
				"You're assaulting a soldier of the United States.", "Let me go and you might only hang once.", "Colonel Favours will have every trooper at Fort Wallace on you.", "Private. That's all you get. Private.", "You'll swing for this, outlaw.", "Unhand me. That's an order.", "You've just made the worst mistake of your life.", "There's a patrol out here somewhere. They'll find you.", "Do you know the penalty for this? Do you?", "I'm a soldier, outlaw. I've been trained for worse.",
				"{leader} hangs men like you, and he sleeps fine after.", "Get your hands off the uniform."
			},
			["open_scared"] = new string[6] { "Easy - easy! I'm just a private, I only do what I'm told!", "Don't shoot, mister, I enlisted for the pay!", "Please... I'm only nineteen.", "Don't shoot! I've only been in a year!", "Please, I just drive the supply wagon!", "Please - I just cook for the company!" },
			["refuse"] = new string[14]
			{
				"I took an oath.", "Name and rank, outlaw. That's all the army owes you.", "You'll get nothing. Not from a soldier.", "The Colonel would have me court-martialed for less.", "You'll hang either way. I won't help you along.", "I'm not talking to a common thief.", "Go to hell. Sir.", "The army doesn't bargain with bandits.", "I'll answer to the Colonel. Not to you.", "You can beat me. I've been beaten by sergeants.",
				"Private, United States Army. And that's it.", "Every word I give you gets a trooper killed. Not happening.", "I'd rather the stockade than talk to you.", "Regulations. I don't talk."
			},
			["refuse_scared"] = new string[6] { "They'd court-martial me. They'd shoot me!", "I can't. Favours would have me flogged.", "Please, I'd lose everything - my pay, my pension...", "If the Colonel found out, I'd be in irons for life!", "Please, they shoot deserters, they'd shoot me too!", "I can't! They'd drum me out and hang me after!" },
			["lock"] = new string[4] { "That's classified. To you, everything is.", "No further comment, outlaw.", "That's all the army's telling you.", "Request denied, outlaw." },
			["hit"] = new string[4] { "Our sergeant hits harder at drill.", "Striking a soldier. Add it to the list.", "Basic drill was worse than this.", "That'll look good at your trial." },
			["hit_scared"] = new string[4] { "Stop! Please, stop!", "Alright! Christ, alright!", "Enough! Enough, for God's sake!", "Please! I'm just a private!" },
			["cut"] = new string[4] { "You'll hang twice for that.", "Medic! ...Damn you.", "That's another count against you.", "I've had worse from a mule." },
			["cut_scared"] = new string[4] { "I'm cut - I'm cut bad! Stop!", "Please! I'll bleed out out here!", "Christ, it's deep! Stop!", "Get the surgeon! Please!" },
			["shot"] = new string[4] { "Argh! You've shot a soldier of the Republic!", "They'll send the whole cavalry for this!", "You've just signed your own warrant!", "Argh! The whole regiment will hunt you!" },
			["shot_scared"] = new string[4] { "Don't! I'll tell you, I'll tell you!", "Mercy! I'm just a private!", "Stop! I'll talk, for God's sake!", "Don't fire! I yield, I yield!" },
			["near"] = new string[4] { "Worst marksmanship I've seen west of the Dakota.", "Is that supposed to rattle me?", "Our recruits shoot better than that.", "You're wasting cartridges, outlaw." },
			["near_scared"] = new string[4] { "Holy -! Don't, please!", "Alright! Alright, don't shoot!", "Jesus! Hold your fire!", "Cease fire! Please!" },
			["threat_defy"] = new string[4] { "Kill a soldier and the army never stops hunting you.", "I'm not afraid of you.", "Threatening a soldier. That's another rope.", "The army's buried a thousand like you." },
			["threat_scared"] = new string[4] { "No! Please - I've got a mother in Ohio!", "Don't, sir. Please don't.", "Please, I'll desert, I'll never wear the blue again!", "Don't, sir! I've a girl back home!" },
			["threat"] = new string[3] { "Is that a threat against the United States?", "Noted, outlaw.", "We'll see what the Colonel says about that." },
			["bribe_loyal"] = new string[4] { "A soldier's honor isn't for sale.", "Keep it. I'm not some saloon informer.", "That's attempted bribery of a soldier. Another charge.", "My loyalty's to the flag. Not your purse." },
			["bribe_greedy"] = new string[4] { "Thirteen dollars a month don't go far. Alright.", "The army barely pays me. Fine.", "Paymaster's three months late. Alright.", "Well. Nobody at the fort needs to know." },
			["bribe_rich"] = new string[2] { "My family's in banking back in Philadelphia. Keep it.", "You think an officer's son needs your money?" },
			["bribe_poor"] = new string[2] { "That's a month's pay. God help me. Ask.", "I send every cent home to my mother. Alright." },
			["bribe"] = new string[2] { "Not much of a bribe. But alright.", "Fine. Ask quickly." },
			["lead"] = new string[6] { "This stays between us.", "Fine. But the Colonel never hears this.", "Alright. Listen.", "If anyone asks, you beat it out of me.", "Off the record. Understood?", "I'll say it once, outlaw." },
			["lead_scared"] = new string[4] { "I'll tell you! Just let me go after!", "Alright - alright, I'll say it!", "Don't hurt me, I'll tell you everything!", "Alright - God, alright, here it is!" },
			["letgo"] = new string[6] { "You're... letting a soldier go?", "This doesn't clear your name, outlaw.", "I'll still have to report this.", "I'll say I lost you in the woods.", "Strange kind of outlaw.", "Obliged. Don't make me regret it." },
			["letgo_scared"] = new string[5] { "Thank you - thank you, I'll go back to the fort!", "I'm going! I won't say I saw you!", "God bless you, mister!", "I'm done with the army, I swear it!", "I'll desert. I'll go home. Thank you." },
			["bargain_pay"] = new string[3] { "${dollars}. My pay for the month. Take it.", "Here - ${dollars}. That's all a private's got.", "${dollars}. Take it and I never saw you." },
			["bargain_inform"] = new string[3] { "I'll tell you when the patrols ride out. Every one.", "I'll pass you the orders. God help me if they find out.", "I'll send word from the fort. Just let me live." },
			["bargain_owe"] = new string[3] { "Then I owe you. I'll look the other way. Once.", "Once, outlaw. I'll stand down once.", "A soldier honors a debt. One time." },
			["bargain_leave"] = new string[3] { "I'll desert. Tonight. You'll never see this uniform again.", "I'm done with the army. I'll go back east.", "I'll take off the blue and go home. I swear it." },
			["bargain_again"] = new string[2] { "You already let me go once. There's no second time.", "We had our bargain. I'm out of things to give you." },
			["bargain_plead"] = new string[3] { "I'm just a private! I enlisted for the pay!", "I got a mother in Ohio, please!", "I've never even fired at anyone, I swear!" }
		},
		["wapiti"] = new Dictionary<string, string[]>
		{
			["open"] = new string[12]
			{
				"You are no different from the soldiers.", "Take your hands off me.", "Rains Fall said men like you would come.", "What do you want from me?", "My people have nothing left for you to take.", "Speak. Then let me go.", "You come with a gun, like all of them.", "Why do you do this?", "Let me stand, at least.", "The soldiers took our land. What will you take?",
				"Say what you came to say.", "There is nothing for you here."
			},
			["open_scared"] = new string[4] { "Please. I have a family on the reservation.", "I have done nothing to you.", "Do not hurt me. I am only a hunter.", "Please. My mother is old, and she needs me." },
			["refuse"] = new string[12]
			{
				"I will not speak.", "You have taken enough from us.", "I will not betray my people to you.", "Ask the soldiers. They know everything about us.", "My father died rather than talk. I am his son.", "Hurt me if you want. I will still say nothing.", "My words are not yours to take.", "You will learn nothing from me.", "Rains Fall would not forgive me. I would not forgive myself.", "Many men have asked. None have heard.",
				"I am silent. Do what you will.", "We have been lied to for many years. Now you want the truth from us?"
			},
			["refuse_scared"] = new string[4] { "If I speak, they will know. Please.", "I cannot. They would never forgive me.", "If I speak, my people suffer for it.", "Please. Do not ask me this." },
			["lock"] = new string[4] { "I have said all I will say about that.", "No. Not that.", "That is all.", "Do not ask again." },
			["hit"] = new string[4] { "The soldiers hit harder.", "This changes nothing.", "I have known worse pain.", "You only show who you are." },
			["hit_scared"] = new string[4] { "Stop. Please stop.", "Enough.", "Please. No more.", "Stop. I am hurt." },
			["cut"] = new string[4] { "You are like them. All of you.", "Is this how you treat a man?", "My blood is on your hands now.", "You cut a man who cannot fight back." },
			["cut_scared"] = new string[4] { "Stop! Stop, please!", "Enough... enough.", "Please, the bleeding...", "Stop. I beg you." },
			["shot"] = new string[4] { "You shot me... for nothing.", "My people will remember this.", "You are a coward.", "Rains Fall will hear what you did." },
			["shot_scared"] = new string[4] { "Do not shoot again. I will speak.", "Please... I will tell you.", "Stop. I will tell you what I know.", "No more. Please." },
			["near"] = new string[4] { "You will not frighten me that way.", "I have heard guns before.", "You waste your bullets.", "I am still here." },
			["near_scared"] = new string[4] { "No! Please!", "Stop! I will listen.", "Do not! Please!", "Enough. I hear you." },
			["threat_defy"] = new string[4] { "Then do it. I am not afraid to die.", "I have seen worse than you.", "Many of my people have died. I am no better than them.", "Kill me, and you will be no different from the soldiers." },
			["threat_scared"] = new string[4] { "Please. My children.", "No... please.", "Do not. My wife is waiting for me.", "Please. I want to see my children again." },
			["threat"] = new string[3] { "Words.", "I have heard such words from the soldiers.", "You say that easily." },
			["bribe_loyal"] = new string[4] { "I will not sell my people for your money.", "Keep it. It is worth nothing to me.", "My people are not for sale.", "The government also offered money. We remember what it bought." },
			["bribe_greedy"] = new string[4] { "Money buys food for the winter. Ask.", "...The reservation is hungry. Ask your question.", "The agent gives us little. I will take it.", "Coin buys blankets. Ask." },
			["bribe_poor"] = new string[2] { "My family is hungry. I will answer.", "This will buy flour. Ask." },
			["bribe"] = new string[2] { "I will take it. Ask.", "Money. Very well." },
			["lead"] = new string[6] { "I will tell you this once.", "Listen, then.", "You will leave us alone after.", "This, and nothing more.", "Hear me, then.", "I tell you this for my family's sake." },
			["lead_scared"] = new string[4] { "I will tell you. Then let me go.", "Alright. Alright.", "I will tell you. Please, then go.", "Listen. I will say it." },
			["letgo"] = new string[6] { "You let me go. I will remember.", "Hm. Not all of you are the same, then.", "Go your way. I will go mine.", "My people will hear of this.", "This is a strange mercy.", "I will not thank you. But I will not forget." },
			["letgo_scared"] = new string[4] { "I go. I go now.", "Thank you. I will go home to my family.", "I will not come back here.", "My children will see me again. Thank you." },
			["bargain_pay"] = new string[2] { "${dollars}. It is all I carry.", "Take it. ${dollars}. Let me go to my family." },
			["bargain_inform"] = new string[2] { "I will tell you where the young men ride. Before they go.", "I will send word. Only so no more of us die." },
			["bargain_owe"] = new string[2] { "Then I owe you a life. I will remember it, once.", "One debt. I will repay it, and then it is done." },
			["bargain_leave"] = new string[3] { "I will go north. Far from here.", "I will leave with the others. You will not see me again.", "I will go where the soldiers are not." },
			["bargain_again"] = new string[2] { "I have already given you what I had. There is nothing more.", "You spared me once. That was the bargain." },
			["bargain_plead"] = new string[3] { "I have children. They are waiting for me.", "I am no warrior. Let me go to my people.", "Please. My family has lost enough." }
		},
		["klan"] = new Dictionary<string, string[]>
		{
			["open"] = new string[8] { "You don't know who you're dealing with.", "Take your hands off me, boy. We're everywhere.", "The High Warden will hear about this.", "There's a lot more of us than you think.", "You lay a hand on me, you'll answer for it.", "We've got friends in high places, mister.", "You'll be sorry when the riders come.", "I got nothing to say to you." },
			["open_scared"] = new string[4] { "Don't hurt me! It's - it was just a meeting, that's all!", "Please, mister, I just stand at the back!", "I ain't really one of 'em, I swear! I just hold the horses!", "Please! My brother made me come along!" },
			["refuse"] = new string[10] { "You'll get nothing from me.", "We look after our own.", "Ask all you like, the brotherhood don't talk.", "You'll find out who we are soon enough.", "I ain't saying a damn word.", "Some oaths a man keeps.", "The High Warden'd know if I talked.", "You'll get the same as the sheriff gets. Nothing.", "I don't know nothing about no riders.", "Ain't nobody talks. Not ever." },
			["refuse_scared"] = new string[4] { "They'd burn me out if I talked!", "Please - they know where I live!", "They'd find me. They always find who talks!", "Please, mister, I got a family!" },
			["lock"] = new string[4] { "That's all you're getting.", "Never. Not that.", "I'm done.", "Not another word, you hear?" },
			["hit"] = new string[4] { "That all you got?", "You'll regret that.", "That supposed to hurt?", "You'll get yours." },
			["hit_scared"] = new string[4] { "Stop! Please stop!", "Alright! Enough!", "Please! I'm sorry!", "Don't! Don't hit me no more!" },
			["cut"] = new string[4] { "You're dead, you hear me? Dead!", "You'll pay for that!", "You crazy? You cut me!", "They'll come for you for that!" },
			["cut_scared"] = new string[4] { "I'm bleeding! Stop, please!", "Don't! Don't cut me again!", "Oh Lord, oh Lord, I'm bleeding!", "Stop! I'll do anything!" },
			["shot"] = new string[4] { "Argh! You'll burn for this!", "You son of a -", "You shot me! You actually shot me!", "Argh! They'll hang you for this!" },
			["shot_scared"] = new string[4] { "Don't shoot! I'll talk, I swear!", "Mercy! Please!", "Stop! Stop! I'll say it!", "Don't kill me! Please!" },
			["near"] = new string[4] { "Missed.", "That supposed to scare me?", "Ha. Bad shot.", "Try that again, I dare you." },
			["near_scared"] = new string[4] { "God! Don't! Please!", "Alright! Alright!", "Lord! Don't!", "Please! I'll talk!" },
			["threat_defy"] = new string[4] { "Go on then. They'll come for you.", "You won't.", "You don't know who my friends are.", "There's more of us than bullets in that gun." },
			["threat_scared"] = new string[4] { "No! Please, no!", "I'll do anything, mister!", "Please, mister, I ain't even important!", "I'll quit 'em! I swear I'll quit!" },
			["threat"] = new string[3] { "Big words.", "You'll be singing a different tune.", "Uh-huh." },
			["bribe_loyal"] = new string[4] { "Keep your money.", "Brotherhood ain't for sale.", "I don't take money from strangers.", "You can't buy a sworn man." },
			["bribe_greedy"] = new string[4] { "...Money talks, I guess.", "Alright. For that, alright.", "Well... that's real money.", "Alright, alright. Ask quick." },
			["bribe_poor"] = new string[2] { "I could use that. Ask.", "Times are hard. Fine." },
			["bribe"] = new string[2] { "Fine. But I never seen you.", "Alright, I'll take it." },
			["lead"] = new string[4] { "Fine. Fine!", "You didn't hear this from me.", "Alright, here's what I know.", "Keep my name out of it." },
			["lead_scared"] = new string[4] { "I'll tell you! Just let me go!", "Alright, alright!", "Okay! Okay, here it is!", "Don't hurt me, I'm telling you!" },
			["letgo"] = new string[6] { "Knew you didn't have the guts.", "You'll regret this. We're everywhere.", "Heh. Soft. Real soft.", "Don't think this makes us square.", "We'll see you again. At night.", "Huh. Fine." },
			["letgo_scared"] = new string[5] { "I'm goin'! I'm done with all of it!", "Thank you, mister - I'll burn the hood, I swear!", "I'm gone! I'm gone!", "Never again, mister. Never again.", "I'll go home and stay home." },
			["bargain_pay"] = new string[3] { "${dollars}. Take it. Take it all.", "Here, ${dollars} - just don't tell nobody you caught me.", "${dollars}. Don't tell the others." },
			["bargain_inform"] = new string[3] { "I'll tell you where the next meetin' is. Every one.", "I'll give you names. Just let me live.", "I'll send word before the next ride. Promise." },
			["bargain_owe"] = new string[3] { "Fine. I owe you. Once.", "I'll back off once. That's it.", "One time, mister. Then we're even." },
			["bargain_leave"] = new string[3] { "I'll leave the county. Tonight.", "I'm done with 'em. I'll go somewhere nobody knows me.", "You'll never see me again. I swear it." },
			["bargain_again"] = new string[2] { "I already paid you once. What more you want?", "You let me go before. That was the deal." },
			["bargain_plead"] = new string[3] { "I only went 'cause my brother made me!", "I ain't never hurt nobody, I swear!", "Please, mister, I got a wife!" }
		},
		["bronte"] = new Dictionary<string, string[]>
		{
			["open"] = new string[8] { "You know who I work for? You're already dead.", "{leader} owns this city. Now he owns your funeral.", "Hands off the suit, cafone.", "You want something? Make an appointment.", "Madonna... you got no idea what you just did.", "In Saint Denis, nobody touches a man of the family.", "What is this, huh? What is this?", "The police chief eats at the boss's table. Think about that." },
			["open_scared"] = new string[4] { "Please, I just collect for the family, that's all!", "Dio mio, don't shoot! I got a mother!", "I'm nobody, I just stand at the door!", "Okay, okay - what you want, huh?" },
			["refuse"] = new string[8] { "Silence, my friend. That's how the family works.", "I don't know nothing. I don't see nothing.", "The family don't talk to outsiders.", "You ask the wrong man, amico.", "I talk, I'm in the river by morning. I don't talk.", "{leader} feeds rats to the alligators. Not me.", "Go ask the police. They work for us anyway.", "Silence is good business, mister." },
			["refuse_scared"] = new string[4] { "They'd feed me to the gators, I swear to God!", "Please, they cut out tongues for this!", "My whole family, they'd hurt them, capisci?", "I can't, I can't, the boss would know by supper." },
			["lock"] = new string[3] { "Basta. That's it.", "No more about that. Finito.", "That's family business. Closed." },
			["hit"] = new string[3] { "Ha, you hit like a dockworker's wife.", "That's all? In the Old Country they hit harder.", "You just made it personal, cafone." },
			["hit_scared"] = new string[3] { "Basta! Basta, please!", "Okay! Okay, no more!", "Madonna, stop!" },
			["cut"] = new string[2] { "You cut a man of the family. You're finished.", "This suit cost more than your horse." },
			["cut_scared"] = new string[2] { "Dio, the blood! Stop!", "Please, put the knife away!" },
			["shot"] = new string[2] { "You shot me! The family will bury you!", "Argh! You know who I am?" },
			["shot_scared"] = new string[2] { "Don't shoot! I talk, I talk!", "Please! Mercy, I tell you everything!" },
			["near"] = new string[2] { "In Saint Denis we shoot better.", "Missed. Lucky for me." },
			["near_scared"] = new string[2] { "Madonna! Watch it!", "Okay! Okay! Easy!" },
			["threat_defy"] = new string[3] { "Kill me, and you never sleep again.", "The family has a long memory, my friend.", "Go ahead. They'll find you face down in the bayou." },
			["threat_scared"] = new string[2] { "Please, I got kids, little ones!", "No, no, no! I do whatever you want!" },
			["threat"] = new string[3] { "Tough guy, huh?", "We'll see, amico.", "Big words for a country boy." },
			["bribe_loyal"] = new string[2] { "You insult the family with this.", "Put your money away. I'm no rat." },
			["bribe_greedy"] = new string[2] { "Now we talk business.", "Ah, a man who understands Saint Denis." },
			["bribe_rich"] = new string[2] { "You think I need your money? Look at my shoes.", "That's tip money, my friend." },
			["bribe_poor"] = new string[2] { "Okay. Okay, I need it. Ask.", "The family pays late. Fine." },
			["bribe"] = new string[2] { "Money is money. Ask.", "Alright. This stays between us." },
			["lead"] = new string[3] { "You didn't hear it from me, capisci?", "Okay. Listen good.", "This is between you and me." },
			["lead_scared"] = new string[3] { "Okay, okay, I tell you!", "Please, I tell you, then let me go!", "Madonna... here it is." },
			["letgo"] = new string[6] { "You let me walk? The family remembers, my friend.", "Hm. Respect. I didn't expect it.", "Molto bene. I owe you nothing, though.", "Next time, maybe I let you walk too.", "Strange. Most men would have finished it.", "Grazie. Don't tell anyone." },
			["letgo_scared"] = new string[5] { "Grazie, grazie! I'm gone!", "Madonna... thank you, thank you!", "I go home to my mother. You never see me again!", "I'm going! I swear on my mother!", "Thank God. Thank God." },
			["bargain_pay"] = new string[3] { "${dollars}. Cash. The family always pays.", "Here - ${dollars}. Take it, capisci? We never met.", "${dollars}. A good price for a life, no?" },
			["bargain_inform"] = new string[3] { "I tell you what the family plans. Every job, before it happens.", "You want ears in Saint Denis? You got them. Just let me live.", "I send word. If they find out, I'm in the swamp. But I send word." },
			["bargain_owe"] = new string[3] { "Then I owe you a favor. The family pays its favors. Once.", "One favor, my friend. Then we are even.", "A debt of honor. I pay it once." },
			["bargain_leave"] = new string[3] { "I take the next boat to Napoli. I swear it.", "I'm done with the family. I leave the city tonight.", "You never see my face in Saint Denis again." },
			["bargain_again"] = new string[2] { "I already bought my life from you. No more deals, my friend.", "We made our deal. The family doesn't pay twice." },
			["bargain_plead"] = new string[3] { "I have a mother, a sister! Please!", "I just carry crates, signore! I'm nobody!", "Please, I'm just a driver for them!" }
		},
		["murfree"] = new Dictionary<string, string[]>
		{
			["open"] = new string[8] { "Pa's gonna hear you hollerin'.", "You smell like town. Smells good.", "Ain't nobody comes up the ridge and leaves.", "Brood's watchin' you right now, stranger.", "Let go a' me. I bite.", "Heh. You're a big one.", "What you want? Ain't got nothin' but my teeth.", "Kin's gonna find you. Then we eat good." },
			["open_scared"] = new string[4] { "Don't hurt me, I'm just the young'un!", "Please, mister, Pa makes me do it!", "I ain't done nothin', mister, they made me!", "Don't! Don't put me in the pit!" },
			["refuse"] = new string[8] { "Brood don't talk to outsiders.", "You ain't kin. You get nothin'.", "Heh. Go ask the ones in the cellar.", "Ain't sayin'. Pa'd skin me.", "Hills keep secrets. So do I.", "{leader}'ll have your hide for askin'.", "Go on back down the mountain.", "Nothin' for you. Nothin'." },
			["refuse_scared"] = new string[3] { "Pa'd chain me in the dark if I told!", "They'd put me in the pit with the others!", "Please, they'd know, they always know!" },
			["lock"] = new string[3] { "Ain't sayin' more.", "That's Brood business. Shut.", "No more. Hills got ears." },
			["hit"] = new string[3] { "Heh. Do it again.", "Pa hits harder'n that.", "That tickles, stranger." },
			["hit_scared"] = new string[2] { "Stop! Stop it!", "Ow! Please, no more!" },
			["cut"] = new string[2] { "Heh... I like the red.", "You'll bleed worse when kin finds you." },
			["cut_scared"] = new string[2] { "Ah! It hurts, it hurts!", "Stop! I'm leakin'!" },
			["shot"] = new string[2] { "You shot me! Kin's gonna gut you!", "Agh! Brood'll get you for that!" },
			["shot_scared"] = new string[2] { "Don't shoot! I'll tell, I'll tell!", "No more, mister! I'll say it!" },
			["near"] = new string[2] { "Heh. Missed me.", "Can't shoot neither, city man." },
			["near_scared"] = new string[2] { "Don't! Don't!", "Okay! Okay, mister!" },
			["threat_defy"] = new string[2] { "Kill me. Brood's got plenty more.", "Go on. I ain't scared a' dyin'." },
			["threat_scared"] = new string[2] { "Please! I don't wanna go in the ground!", "No! I'll be good, I'll be good!" },
			["threat"] = new string[3] { "Heh.", "Talk all you want.", "Pa says worse at supper." },
			["bribe_loyal"] = new string[2] { "Kin's kin. Keep your money.", "Brood don't need town money." },
			["bribe_greedy"] = new string[2] { "Shiny. Alright.", "Heh. I'll take that." },
			["bribe_poor"] = new string[2] { "Ain't never held that much. Ask.", "Alright. Alright, ask." },
			["bribe"] = new string[2] { "What's that for? ...Fine.", "Money. Huh. Ask then." },
			["lead"] = new string[3] { "Heh. Alright.", "You want it? Here.", "Don't tell Pa." },
			["lead_scared"] = new string[2] { "I'll tell! Just don't hurt me!", "Okay, okay, here!" },
			["letgo"] = new string[6] { "You lettin' me go? Pa won't believe it.", "Heh. You'll be back. They always come back.", "Huh. Nobody ever let me go before.", "Go on then. The hills'll get you anyhow.", "I'll remember your face, mister. Real good.", "Strange one, ain't ya." },
			["letgo_scared"] = new string[4] { "I'm goin' home! I'm goin'!", "Don't tell Pa! I'm goin'!", "Thank you, thank you, mister!", "I'll hide in the hollow, you'll never find me!" },
			["bargain_pay"] = new string[2] { "${dollars}. Took it off a fella. Yours now.", "Here. ${dollars}. Don't ask where from." },
			["bargain_inform"] = new string[2] { "I'll tell you where the kin go huntin'. All of it.", "I'll tell you where they keep 'em. Just let me go." },
			["bargain_owe"] = new string[2] { "Then I owe ya. Once. Kin pays once.", "One time, mister. Then I'll hunt you like the rest." },
			["bargain_leave"] = new string[3] { "I'll go far off. Past the mountains. Swear.", "I'll leave the Ridge. You'll never see me.", "I'm done with the kin. I'll run off for good." },
			["bargain_again"] = new string[2] { "Already let me go once. Ain't nothin' left to trade.", "You done had your deal, mister." },
			["bargain_plead"] = new string[3] { "I'm just the youngest! Pa makes me do it!", "Please, I don't wanna go in the pit!", "Let me go, mister! I didn't hurt nobody!" }
		},
		["skinners"] = new Dictionary<string, string[]>
		{
			["open"] = new string[8] { "You'll make a fine pelt.", "Tall Trees don't forgive trespass.", "My brothers are in the trees. Watching.", "Let go. I'll take your skin slow.", "You come into our woods? Fool.", "Smell of you. Town man.", "What do you want, meat?", "Brothers'll hang you from a pine." },
			["open_scared"] = new string[4] { "Don't! I just set the traps!", "Please, I ain't the one does the skinning!", "Easy... easy, mister.", "Don't kill me in the woods. Not here." },
			["refuse"] = new string[8] { "Skinners don't talk.", "You'll get nothin' but my teeth.", "Ask the trees.", "I'll be skinnin' you before I'm talkin' to you.", "Brothers'd cut my tongue for less.", "Nothin'.", "Go on, cut. I've cut worse.", "Woods keep our secrets." },
			["refuse_scared"] = new string[3] { "They'd hang me up like a buck!", "Please... they'd skin me, they'd skin me!", "The brothers know. They always know." },
			["lock"] = new string[3] { "Done talkin'.", "That's all. Woods are listenin'.", "Nothing more." },
			["hit"] = new string[3] { "Hah. Bear hits harder.", "Do it again, I'll remember.", "That's nothin'." },
			["hit_scared"] = new string[2] { "Stop! Stop!", "Enough, enough!" },
			["cut"] = new string[2] { "Cut me? I cut for a living.", "I know a blade. That's a poor one." },
			["cut_scared"] = new string[2] { "Ah! Stop it!", "The blood! Stop!" },
			["shot"] = new string[2] { "Agh! Brothers'll have your hide!", "You'll hang from a tree for that!" },
			["shot_scared"] = new string[2] { "Don't shoot! I'll say!", "No more! I'll tell you!" },
			["near"] = new string[2] { "Missed. Brothers don't miss.", "You'd starve out here." },
			["near_scared"] = new string[2] { "Whoa! Don't!", "Alright! Alright, easy!" },
			["threat_defy"] = new string[2] { "Kill me. Brothers'll make a coat of you.", "I've seen men die harder than you'd ever manage." },
			["threat_scared"] = new string[2] { "Please! I'll leave the woods!", "Don't! I'll talk!" },
			["threat"] = new string[3] { "Big talk.", "Out here, words don't mean much.", "Hm." },
			["bribe_loyal"] = new string[2] { "Brothers ain't for sale.", "Keep your coin." },
			["bribe_greedy"] = new string[2] { "Pelts don't sell much. Alright.", "Money. Good. Ask." },
			["bribe_poor"] = new string[2] { "That's a season of furs. Ask.", "Alright. Need it." },
			["bribe"] = new string[2] { "Coin is coin. Ask.", "Huh. Alright." },
			["lead"] = new string[3] { "Alright. Hear it.", "Here.", "Tell no one." },
			["lead_scared"] = new string[2] { "I'll tell! Don't hurt me!", "Alright, alright, here it is!" },
			["letgo"] = new string[6] { "Hm. Go on, then.", "The brothers won't hear of this.", "You'll not get that twice.", "Strange. A hunter lettin' the game go.", "I'll remember the smell of you.", "Fine." },
			["letgo_scared"] = new string[4] { "I'm goin'. Into the trees. Gone.", "Thank you. I'll leave the woods.", "I'll not come back. Swear.", "Gone. You won't see me." },
			["bargain_pay"] = new string[2] { "${dollars}. From the pelts. Take it.", "Here. ${dollars}. Now leave me be." },
			["bargain_inform"] = new string[2] { "I'll tell you where the brothers set the traps. Every trail.", "I'll send word when they go out. Every time." },
			["bargain_owe"] = new string[2] { "I owe you. Once. Then we're square.", "One debt. I'll pay it." },
			["bargain_leave"] = new string[3] { "I'll go north. Far from Tall Trees.", "I'll leave the brothers. Tonight.", "You'll not see me in these woods again." },
			["bargain_again"] = new string[2] { "You had your deal. There's no other.", "Already bought my life. Nothing left." },
			["bargain_plead"] = new string[3] { "I just skin what they bring in. Let me go.", "Please. I'm nobody to them.", "I only trap rabbits, mister. Please." }
		},
		["laramie"] = new Dictionary<string, string[]>
		{
			["letgo"] = new string[6] { "You're turnin' me loose? Huh. Your mistake.", "Don't figure I'll do the same for you.", "Well, I'll be damned.", "I'll tell the boys you got soft.", "Much obliged. Don't come looking for thanks.", "Fine by me." },
			["letgo_scared"] = new string[4] { "I'm ridin'! I'm ridin' out!", "Thank you, mister - I'm done with the gang!", "You won't see me again, I swear it!", "I'm gone, mister. Gone." },
			["bargain_pay"] = new string[3] { "${dollars}. All my take. Take it.", "Here's ${dollars} - now we're done here.", "${dollars}. That's a month of rustlin'." },
			["bargain_inform"] = new string[3] { "I'll tell you which ranch they hit next. Every time.", "I'll send word before they ride. Just let me live.", "I'll keep you posted on the gang. God help me." },
			["bargain_owe"] = new string[3] { "Fine. I owe you. One time.", "I'll stand off once. That's it.", "A debt's a debt. Once." },
			["bargain_leave"] = new string[3] { "I'll ride to Montana. Tonight.", "I'm done with this outfit. I swear it.", "I'll go somewhere they never heard of me." },
			["bargain_again"] = new string[2] { "I already paid you once. Ain't payin' twice.", "We had a deal. There ain't another." },
			["bargain_plead"] = new string[3] { "I only mind the horses! Please!", "I got a girl in Blackwater, mister!", "I'm new, I ain't done nothin' yet!" }
		},
		["delobo"] = new Dictionary<string, string[]>
		{
			["letgo"] = new string[6] { "You let me go, gringo? Hm.", "Gracias. But don't think we're friends.", "Next time, maybe I won't be so lucky.", "Loco. You're loco.", "I remember this, amigo.", "Bueno. I go." },
			["letgo_scared"] = new string[4] { "Gracias, gracias! I'm gone!", "Dios te bendiga! I go home!", "I go back to Mexico, I swear!", "Thank you, señor! You never see me again!" },
			["bargain_pay"] = new string[3] { "${dollars}. Everything. Take it.", "Toma - ${dollars}. Now let me go.", "${dollars}, amigo. Fair price, no?" },
			["bargain_inform"] = new string[3] { "I tell you where the Del Lobos ride. Every time.", "I send word before every job. Just let me live.", "I'll be your ears, gringo. Dios me perdone." },
			["bargain_owe"] = new string[3] { "Then I owe you, gringo. One time.", "One debt. I pay it, then we're even.", "Una vez. I owe you once." },
			["bargain_leave"] = new string[3] { "I go south. Back across the river.", "I'm done with the Del Lobos. I leave tonight.", "You never see me again, señor. I swear." },
			["bargain_again"] = new string[2] { "I already bought my life. No more, gringo.", "We had a deal. There's no other one." },
			["bargain_plead"] = new string[3] { "I have a family in Chihuahua! Please!", "I just hold the horses, señor!", "Please, I'm nobody to them!" }
		},
		["nightfolk"] = new Dictionary<string, string[]>
		{
			["letgo"] = new string[6] { "...You let me go.", "The bayou will remember you.", "Heh-heh... we'll meet in the dark.", "...Go on, then.", "You'll hear us. At night.", "...Strange." },
			["letgo_scared"] = new string[4] { "...I go. Into the water. Gone.", "Don't follow. Please.", "...Thank you. Thank you.", "I go deep. You won't see me." },
			["bargain_pay"] = new string[2] { "${dollars}... from the dead ones. Take it.", "Take it. ${dollars}. Leave me be." },
			["bargain_inform"] = new string[2] { "I'll tell you where they wait. In the trees.", "I'll whisper you their paths. Just let me go." },
			["bargain_owe"] = new string[2] { "...Then I owe you. Once.", "One life for one life. Once." },
			["bargain_leave"] = new string[3] { "I'll go deep. Past the swamp. Gone.", "You'll never see me again. Never.", "I'll leave the bayou. I swear it." },
			["bargain_again"] = new string[2] { "...You had your bargain.", "Nothing more to give. Nothing." },
			["bargain_plead"] = new string[3] { "...Please. Please.", "I'm not one of them. Not really.", "Let me go back to the water." }
		},
		[""] = new Dictionary<string, string[]>
		{
			["open"] = new string[14]
			{
				"What the hell do you want?", "Get off me!", "You picked the wrong man, mister.", "...What.", "I ain't done nothing to you.", "You got some nerve.", "Who the hell are you?", "Mister, you got the wrong fella.", "What is this? A robbery?", "Let go of me, damn you.",
				"You lost, friend?", "Make it quick, whatever it is.", "I don't want no trouble.", "Easy, now. What's this about?"
			},
			["open_scared"] = new string[6] { "W-what do you want? I don't know nothin'!", "Please, mister, I got money, take it!", "Don't kill me, please!", "Oh God, oh God, please...", "I ain't armed, mister! See?", "What'd I do? What'd I do?" },
			["refuse"] = new string[12]
			{
				"I ain't telling you nothing.", "Go to hell.", "Ask somebody who cares.", "I don't know, and I wouldn't tell you if I did.", "You're wasting your breath, mister.", "Find somebody else to bother.", "Not a chance.", "You can go to the devil.", "I keep my mouth shut. Lived this long doing it.", "Don't know, don't care.",
				"Ain't my business, and it sure ain't yours.", "I got nothing for you."
			},
			["refuse_scared"] = new string[6] { "I don't know! I swear I don't know!", "Please, I can't!", "They'd kill me, mister!", "Please, I'd be a dead man!", "I can't tell you, mister, honest to God!", "Don't make me say it, please!" },
			["lock"] = new string[4] { "That's all I'm saying on that.", "Don't ask me that again.", "That's the end of it.", "Leave it be, mister." },
			["hit"] = new string[4] { "That the best you got?", "Hit me again and see what happens.", "My old man hit harder.", "You'll regret that, mister." },
			["hit_scared"] = new string[4] { "Okay! OKAY! Stop, please!", "Enough! Enough!", "Please! No more!", "I give! I give!" },
			["cut"] = new string[4] { "You cut me... you cut me!", "You crazy son of a -", "You're a madman!", "Ah! You'll pay for that!" },
			["cut_scared"] = new string[4] { "No more! Please, no more!", "Put it away! Please!", "I'm bleeding! Please stop!", "Not the knife! Please!" },
			["shot"] = new string[4] { "Gah! You son of a...", "You shot me!", "Argh! Damn you!", "You crazy bastard, you shot me!" },
			["shot_scared"] = new string[4] { "AAH! Stop! Stop, I'll talk!", "Don't shoot! Please!", "No more! I'll tell you, I'll tell you!", "Please! I don't want to die!" },
			["near"] = new string[4] { "...You missed.", "Ha. Try again.", "Can't shoot neither, huh?", "Wasting good bullets." },
			["near_scared"] = new string[4] { "Jesus! Don't! Don't shoot!", "Okay! Okay!", "God almighty! Stop!", "Don't! I'm listening!" },
			["threat_defy"] = new string[4] { "Go on then. Do it.", "...You wouldn't.", "I ain't scared of you.", "You'd hang for it." },
			["threat_scared"] = new string[4] { "N-no! No, don't!", "Please! I'll do anything!", "Please, mister, I'll leave town!", "Don't! I got a family!" },
			["threat"] = new string[6] { "...You wouldn't.", "You talk a lot.", "Is that so.", "Sure you would.", "Uh huh.", "That right?" },
			["bribe_loyal"] = new string[2] { "You think I'd sell them out for your money? Keep it.", "They're my friends. Your money don't change that." },
			["bribe_rich"] = new string[4] { "Your money? I've got more than you'll ever see.", "Put it away. You can't afford me.", "I don't need your money, friend.", "That's an insult, mister." },
			["bribe_greedy"] = new string[4] { "...Now we're talking. Ask your questions.", "Money. Finally, some sense.", "Well, now. That's a different story.", "Money first. Then talk." },
			["bribe_poor"] = new string[4] { "...That's a week's eating. Alright. Ask.", "Lord knows I need it. Ask.", "That's rent. God help me. Ask.", "I'll take it. Hard times." },
			["bribe"] = new string[4] { "Money don't buy everything. But it helps.", "Alright. Ask, then.", "Fine. Ask.", "Alright, for that I'll talk." },
			["lead"] = new string[6] { "Alright. Alright.", "Fine.", "You want to know? Fine.", "Here's the truth of it.", "Alright, listen.", "Don't say where you heard it." },
			["lead_scared"] = new string[4] { "Okay! Okay, I'll tell you!", "I'll talk! Just stop!", "Alright! I'll say it!", "Please, here it is, all of it!" },
			["letgo"] = new string[10] { "...That's it? I can go?", "Huh. Thanks, I guess.", "You're a strange one, mister.", "I won't forget this.", "Don't expect me to thank you.", "Alright. I'm going.", "Next time, just leave me be.", "Well. That's a first.", "I'll be on my way, then.", "Lucky day, I suppose." },
			["letgo_scared"] = new string[7] { "Thank you! Thank you, mister!", "I'm going! I'm going!", "God bless you, mister!", "You won't see me again, I swear!", "Oh, thank the Lord...", "I'm leaving town. Tonight.", "I'll never cross you again!" },
			["bargain_pay"] = new string[4] { "Here - ${dollars}. All I got on me. Take it and we never met.", "${dollars}. Take it. Please.", "That's ${dollars}. Every cent. Let me go.", "${dollars}. Cheap for a life, ain't it?" },
			["bargain_inform"] = new string[3] { "I'll tell you what they're planning. Every job, before they ride. Just let me live.", "I'll send word. Every job. Just let me go.", "I'll keep you informed, mister. Whatever they do." },
			["bargain_owe"] = new string[3] { "Then I owe you. I pay my debts. Once. After that, we're square.", "I owe you one. Just one.", "One time, mister. I'll back off one time." },
			["bargain_leave"] = new string[4] { "I'll go. Tonight. You'll never see my face again, I swear on my mother.", "I'll leave the territory. You have my word.", "I'm out. I'll go somewhere nobody knows me.", "I'll ride out tonight and never come back." },
			["bargain_again"] = new string[3] { "I already bought my life off you. You want more, you'll have to kill me.", "We had a deal. I got nothing left.", "You let me go once. That was the bargain." },
			["bargain_plead"] = new string[5] { "I got a family! Please! I'll ride out of here tonight!", "Please, mister, I got kids!", "I ain't nobody! Let me go and you'll never see me!", "I'll go straight, I swear it! Please!", "I got a sick mother, mister. Please." }
		}
	};

	private static readonly Dictionary<Personality, Dictionary<string, string[]>> ByTemper = new Dictionary<Personality, Dictionary<string, string[]>>
	{
		[Personality.Hothead] = new Dictionary<string, string[]>
		{
			["open"] = new string[6] { "Let me up and fight me like a man!", "You're dead. You hear me? DEAD.", "I'll rip your throat out!", "Untie me and we'll see who's tough!", "I'll break every bone in your body!", "Come closer. Come on!" },
			["refuse"] = new string[8] { "Hit me again. Go on, I dare you!", "That all you got?", "I'll gut you for this!", "Untie me and say that!", "I'll spit in your face first!", "You'll get nothin' but my fist!", "Make me, you yellow dog!", "Nobody scares me. Nobody!" },
			["hit"] = new string[4] { "I'll kill you for that!", "Do it again. Do it again!", "Ha! Is that it? Again!", "You'll be eatin' through a straw!" },
			["cut"] = new string[2] { "I'll cut you back twice as deep!", "You'll pay for every drop!" },
			["shot"] = new string[2] { "I'll live just to kill you!", "You'll die screamin' for that!" },
			["threat_defy"] = new string[4] { "Do it! Go on, DO IT!", "I'll come back from the grave for you!", "Kill me, and you'd better do it right!", "Threaten me again. I dare you!" },
			["letgo"] = new string[3] { "This ain't over. You hear me?", "Next time I won't be tied up.", "You'll regret this. I swear you will." }
		},
		[Personality.Coward] = new Dictionary<string, string[]>
		{
			["open"] = new string[6] { "P-please, I ain't nobody!", "Don't hurt me! I'll do anything!", "Oh Lord, oh Lord...", "Oh no, oh no, oh no...", "I'll tell you anything, just don't shoot!", "Please, I got nothin' to do with nothin'!" },
			["refuse"] = new string[6] { "Please... I can't, they'll kill me!", "Don't hurt me, mister, please!", "I- I don't know nothin', I swear!", "I want to tell you, I swear, I just can't!", "Please don't make me, mister!", "They'll know it was me!" },
			["hit"] = new string[4] { "Okay! OKAY! Stop, please!", "Don't! I'll be good!", "No! Please! I'm sorry!", "Ow! Ow, stop, please!" },
			["shot_scared"] = new string[2] { "I don't wanna die, I don't wanna die!", "Mercy! Mercy!" },
			["threat_scared"] = new string[4] { "Please! I'll tell you anything!", "No no no no...", "I'll do whatever you want!", "Please, please, please..." },
			["lead_scared"] = new string[2] { "Okay, okay, here, here it is!", "I'll tell you, I'll tell you everything!" },
			["letgo"] = new string[2] { "R-really? I can go?", "I can go? I can really go?" },
			["letgo_scared"] = new string[2] { "Thank you, thank you, thank you!", "I'm going, I'm going, don't shoot!" }
		},
		[Personality.Proud] = new Dictionary<string, string[]>
		{
			["open"] = new string[6] { "Get your hands off me.", "Do you know who I am?", "You'll regret laying a hand on me.", "Unhand me, you brute.", "I don't speak to men like you.", "You've no idea who you've laid hands on." },
			["refuse"] = new string[6] { "You think fists'll make me crawl? Not me.", "I don't bow to the likes of you.", "Beat me all you want. I'm better than you.", "I won't lower myself to answer.", "You'll get nothing from me, ever.", "Do your worst. I'll not beg." },
			["hit"] = new string[4] { "Ha. My sister hits harder.", "Is that all? Pathetic.", "You strike like a schoolgirl.", "I've known stronger drunks." },
			["threat_defy"] = new string[4] { "You wouldn't dare.", "Threats. How common.", "I'd sooner die than bow to you.", "Your threats bore me." },
			["bribe"] = new string[2] { "You think I can be bought? How insulting.", "Put that away before you embarrass yourself." },
			["bribe_rich"] = new string[2] { "I've spent more than that on a pair of boots.", "Keep your pennies. You need them more than I do." },
			["letgo"] = new string[3] { "I'll not thank you for it.", "Don't expect gratitude.", "You've made a mistake." }
		},
		[Personality.Cold] = new Dictionary<string, string[]>
		{
			["open"] = new string[6] { "...", "Get on with it.", "I've nothing to say to you.", "Say what you want.", "Well?", "Ask your question." },
			["refuse"] = new string[8] { "...", "You're wasting your time.", "I've had worse from better men.", "No.", "Not today.", "Waste of breath.", "You won't get it.", "Keep going. Nothing changes." },
			["hit"] = new string[4] { "...Ugh.", "Finished?", "...That it?", "...Again, then." },
			["threat_defy"] = new string[4] { "Then do it.", "I don't scare.", "Everyone dies.", "Get on with it, then." },
			["letgo"] = new string[3] { "...", "Hm.", "Your choice." }
		},
		[Personality.Greedy] = new Dictionary<string, string[]>
		{
			["open"] = new string[4] { "Whatever you want, it'll cost you.", "Let's talk business, friend.", "Everything's for sale. Including me.", "You got money? Then we can talk." },
			["refuse"] = new string[8] { "What's in it for me, huh?", "Information ain't free, friend.", "A man's gotta eat. Talking don't feed me.", "Make it worth my while.", "Dollars loosen my tongue. Fists don't.", "Pay me and I'll sing like a bird.", "Nothing for free, mister.", "My memory gets better with money." },
			["bribe_greedy"] = new string[2] { "That's more like it.", "Now you're speaking my language." },
			["lead"] = new string[4] { "For that? Alright.", "Seeing as you're paying...", "Pleasure doing business.", "Money well spent, friend." },
			["letgo"] = new string[2] { "Free, and I still got my money. Good day.", "No charge? Generous." },
			["bargain_pay"] = new string[1] { "${dollars}. You drive a hard bargain." }
		},
		[Personality.Loyal] = new Dictionary<string, string[]>
		{
			["open"] = new string[4] { "You'll get nothing. The boss is family.", "I'd die before I talk.", "I'll not betray my brothers.", "Do what you want. I stay true." },
			["refuse"] = new string[8] { "I ain't no rat. Never.", "The boss is family. You ain't.", "Kill me then. I ain't talking.", "My people trust me. I won't break that.", "Some men can't be broken. I'm one.", "I'd rather die than be a turncoat.", "They'd do the same for me.", "You'll never get a name out of me." },
			["bribe_loyal"] = new string[2] { "You can't buy loyalty, mister.", "Not for all the gold in Saint Denis." },
			["threat_defy"] = new string[4] { "Then I die loyal.", "Do it. I'll never betray them.", "My people will remember me.", "Then I die with my honor." },
			["letgo"] = new string[2] { "My people will still hunt you.", "This changes nothing." }
		},
		[Personality.Drifter] = new Dictionary<string, string[]>
		{
			["open"] = new string[6] { "Ain't my fight, mister.", "I just ride where the work is.", "Look, I don't even like these people.", "I just met these fellas, mister.", "Whatever this is, I'm just passing through.", "Don't look at me. I'm hired help." },
			["refuse"] = new string[8] { "Ain't my fight, mister.", "Why should I care?", "Leave me be.", "I don't know them that well, honest.", "Why'd I stick my neck out for 'em?", "I don't know much, mister. Honest.", "I keep my head down. That's all.", "Not my problem." },
			["lead"] = new string[4] { "Ain't my business to protect 'em anyhow.", "Fine. I don't owe 'em nothing.", "Ain't like I owe 'em loyalty.", "Sure. Why not." },
			["letgo"] = new string[2] { "Fine by me. I'm moving on anyhow.", "Wasn't my fight to begin with." }
		}
	};

	private static readonly LinkedList<string> _recent = new LinkedList<string>();

	private static readonly HashSet<string> _recentSet = new HashSet<string>();

	public static string PlayerName = "Arthur Morgan";

	public static string Pick(string situation, Gang gang, Personality? temper, Random rng, string boss = null, string name = null, bool leaderOut = false, bool woman = false)
	{
		List<string> list = new List<string>();
		string text = gang?.Id ?? "";
		if (!ByFaction.ContainsKey(text))
		{
			text = "";
		}
		if (ByFaction[text].TryGetValue(situation, out var value))
		{
			list.AddRange(value);
			list.AddRange(value);
		}
		if (temper.HasValue && ByTemper.TryGetValue(temper.Value, out var value2) && value2.TryGetValue(situation, out var value3))
		{
			list.AddRange(value3);
		}
		if (text != "" && ByFaction[""].TryGetValue(situation, out var value4))
		{
			list.AddRange(value4);
		}
		if (leaderOut)
		{
			list.RemoveAll((string l) => l.Contains("{leader}") || (boss == null && l.Contains("{boss}")) || l.Contains("Colm") || l.Contains("the General") || l.Contains("Favours") || l.Contains("Rains Fall"));
		}
		if (woman)
		{
			list.RemoveAll((string l) => l.Contains("wife") || l.Contains("girl in") || l.Contains("a man go"));
		}
		if (list.Count == 0)
		{
			return null;
		}
		List<string> list2 = list.Where((string l) => !_recentSet.Contains(l)).ToList();
		string text2 = ((list2.Count > 0) ? list2 : list)[rng.Next((list2.Count > 0) ? list2.Count : list.Count)];
		_recent.AddLast(text2);
		_recentSet.Add(text2);
		while (_recent.Count > 60)
		{
			_recentSet.Remove(_recent.First.Value);
			_recent.RemoveFirst();
		}
		string text3 = gang?.LeaderName ?? "the boss";
		text2 = text2.Replace("{leader}", text3).Replace("{boss}", boss ?? text3).Replace("{name}", name ?? "");
		if (gang?.Id == "odriscoll" && (gang.LeaderName == null || !gang.LeaderName.Contains("Colm")))
		{
			text2 = text2.Replace("Colm's", Short(gang.LeaderName) + "'s").Replace("Colm", Short(gang.LeaderName));
		}
		if (gang?.Id == "lemoyneraiders")
		{
			text2 = text2.Replace("the General", (gang.LeaderName != null) ? Short(gang.LeaderName) : "the boss");
		}
		if (gang?.Id == "army" && (gang.LeaderName == null || !gang.LeaderName.Contains("Favours")))
		{
			text2 = text2.Replace("Colonel Favours", gang.LeaderName ?? "the colonel").Replace("Favours", Short(gang.LeaderName));
		}
		if (PlayerName != "Arthur Morgan")
		{
			text2 = text2.Replace("Arthur Morgan", PlayerName).Replace("Dutch's errand boy", "the one that got away");
		}
		return text2;
		static string Short(string full)
		{
			if (string.IsNullOrEmpty(full))
			{
				return "the boss";
			}
			string[] array = full.Split(' ');
			if (array.Length <= 1 || !array[0].EndsWith("."))
			{
				return array[0];
			}
			return array[array.Length - 1];
		}
	}
}
