ok on veut quand on fait un appel ad?q= , que on puisse soit crée un nouveau deck en db . soit un modif un de nos deck existant en db , soit load un deck . 

---
pour load un deck on le fait via son id unique 
et la querry returne un json contenant l'id du deck / le nom du deck /  l'id de la personne qui la crée / et la liste des cartes et leurs nombre et leurs zone 

---
pour crée un deck il faut y préciser un nom . et la liste des cartes sous un des format mis plus bas dans le fichier .
on y ajoute un id unique . on y save l'id de la personne qui la crée et la liste des cartes et leurs nombre et leurs zone dans un champs json 

---
pour modif un deck c'est la meme querry que pour crée le deck mais que si l'id du deck est dans la querry et que la personne qui as créé le deck et le meme qui fait la request de creation sur le meme id .si on essaye de modif un deck dans en précisant un id et que l'id du deck soit n'existe pas soit n'est pas lié a notre user on crée un nouveau deck avec un id auto incrémenté . 


si resquest corect 
la querry doit return l'id du deck . son nom . et la liste des cartes le composant et les truc associer . et doit ajouté la liste des carte du deck a l'instance dans la quel est la personne qui fait la requete . 

si la querry est mal formuler 
retourné l'érreur 

---

format de la table en db

id unique | nom  du deck | id createur | json | create at | update at |

zone json 
nbr | zone | id card 
1 | m | 00011897-9c8b-482f-8d64-9f2cd8403b6a



--------------------------------------------------------------------------------
( format moxfield )
nbr de fois ou est la carte , puis nom de la carte , puis set , puis colector number ( peut contenir que le nombre de fois ou est la cartes et son nom , dans se cas retourné le premier resultat de la cartes dans la langue demander )


1 Norman Osborn / Green Goblin (SPM) 220
1 Anger (M3C) 208
1 Arcane Signet (AFC) 197
1 Big Score (SNC) 102
1 Bitter Triumph (LCI) 91
1 Blazemire Verge (DSK) 256
1 Blood Crypt (SLD) 125
1 Bone Miser (C19) 15
1 Brallin, Skyshark Rider (C20) 4
1 Canyon Slough (WHO) 259
1 Careful Study (ODY) 70
1 Cephalid Coliseum (TDC) 349
1 Change of Fortune (VOW) 150
1 Chaos Warp (C17) 131
1 Command Tower (LTC) 301
1 Crumbling Necropolis (NCC) 397
1 Cryptcaller Chariot (DFT) 80
1 Deadly Rollick (C20) 42
1 Decaying Time Loop (WHO) 80
1 Deflecting Swat (C20) 50
1 Dragonskull Summit (LTC) 304
1 Drowned Catacomb (LTC) 305
1 Emet-Selch of the Third Seat (FIC) 81
1 Fabled Passage (BLB) 252
1 Faithless Looting (UMA) 128
1 Fellwar Stone (BLC) 269
1 Fetid Pools (PIP) 265
1 Fierce Guardianship (C20) 35
1 Forgotten Creation (SOI) 63 *F*
1 Frantic Search (CMM) 632
1 Ghostly Pilferer (VOC) 105
1 Glint-Horn Buccaneer (J22) 539
1 Gloomlake Verge (DSK) 260
1 Green Goblin, Nemesis (SPE) 23
1 Haunted Ridge (MID) 282
1 Henneth Annûn (LTC) 373
1 Iraxxa, Empress of Mars (WHO) 89
2 Island (LTR) 274
1 Ivora, Insatiable Heir (J25) 50
1 Kess, Dissident Mage (NCC) 344
1 Laughing Mad (FIN) 143
1 Ledger Shredder (SNC) 46
1 Lethal Scheme (TDC) 184
1 Lier, Disciple of the Drowned (TDC) 157
1 Lightning Greaves (AFC) 331
1 Likeness Looter (WOE) 208
1 Lórien Revealed (LTR) 60
1 Magmakin Artillerist (DFT) 137 *F*
1 Malakir Rebirth / Malakir Mire (ZNR) 111
1 Minas Morgul, Dark Fortress (LTC) 514
1 Monument to Endurance (DFT) 237
2 Mountain (LTR) 278
1 Oscorp Industries (SPM) 182
1 Path of Ancestry (DRC) 166
1 Pirate's Pillage (RIX) 109
1 Polluted Delta (KTK) 239
1 Psychic Frog (MH3) 199
1 Rakdos Signet (C20) 249
1 Reanimate (MAR) 20
1 Relic of Sauron (LTC) 79
1 Rogue's Passage (LTC) 326
1 Rona, Herald of Invasion / Rona, Tolarian Obliterator (MOM) 75
1 Scalding Tarn (MH2) 254
1 Secrets of the Dead (C19) 95
1 Seething Landscape (MH3) 225
1 Seize the Spoils (KHM) 149
1 Shadowspear (PLST) THB-236
1 Shipwreck Marsh (MID) 267
1 Shivan Reef (BLC) 331
1 Sire of Seven Deaths (FDN) 1
1 Sol Ring (PIP) 239
1 Steam Vents (GRN) 257
1 Stitch Together (CM2) 78
1 Stormcarved Coast (VOW) 284
1 Sulfur Falls (LTC) 333
1 Sundering Eruption / Volcanic Fissure (MH3) 248
1 Surly Badgersaur (C20) 57
2 Swamp (LTR) 276
1 Takenuma, Abandoned Mire (NEO) 278
1 Talisman of Creativity (CMM) 979
1 Talisman of Dominance (WOC) 150
1 Talisman of Indulgence (AFC) 219
1 Teferi's Ageless Insight (J25) 371
1 Terror of the Peaks (M21) 164
1 Teval's Judgment (TDC) 28
1 The Balrog of Moria (LTC) 129
1 The Locust God (MOC) 335
1 Thrill of Possibility (STA) 46
1 Tolarian Winds (USG) 104
1 Tortured Existence (STH) 74
1 Troll of Khazad-dûm (LTR) 111
1 Ultimate Green Goblin (SPM) 157
1 Unexpected Windfall (AFR) 164
1 Watery Grave (GRN) 259
1 Witch-king of Angmar (LTR) 114
1 Withering Torment (DSK) 124
1 Xander's Lounge (SNC) 294

--------------------------------------------------------------------------------
( format deckstat )
2eme format posible 

indicateur d'ou est la carte . avec le // 
nombre de fois ou la carte est présente , puis entre [] le set , puis toujours dans [] le colector number , puis nom de la cartes . ( comme précédament il est possible que il n'y est pas de set/ colector number , dans se cas retourné le premier resultat de la cartes dans la langue demander )

si après le nom de la carte il y as #!Commander le mettre en tant que commander 


//Main
1 [ONC#114] Adriana, Captain of the Guard
1 [FCA#21] Akroma's Will
1 Aragorn, Hornburg Hero
1 [SPG#114] Arid Mesa
1 [MKM#317] Aurelia, the Law Above
1 Bastion Protector
1 [CLU#158] Beast Whisperer
1 [SLD#323] Beast Within
1 [SLD#217] Boros Charm
1 [CLB#601] Bountiful Promenade
1 [FIC#159] Bugenhagen, Wise Elder
1 [FIN#351] Buster Sword
1 [OTJ#199] Cactusfolk Sureshot
1 [FIC#378] Canopy Vista
1 [FIC#380] Cinder Glade
1 [FIC#202] Cloud, Ex-SOLDIER
1 [J22#87] Colossal Majesty
1 [LTC#212] Combat Celebrant
1 [MKM#324] Commercial District
1 [FIC#127] Conformer Shuriken
1 [LTR#774] Delighted Halfling
1 [ALA#128] Druid of the Anima
1 [MKM#325] Elegant Parlor
1 [MH2#12] Esper Sentinel
1 [FDN#329] Etali, Primal Storm
1 [SLD#2222] Farseek
13 [FIC#482] Forest
1 [SCD#185] Garruk's Uprising
1 [FDN#335] Ghalta, Primal Hunger
1 [MH2#200] Goblin Anarchomancer
1 [FIC#153] Gogo, Mysterious Mime
1 [SLD#1229] Goreclaw, Terror of Qal Sisma
1 [FDN#659] Halana and Alena, Partners
1 [FCA#1] Adeline, Resplendent Cathar
1 [SLD#1872] Heroic Intervention
1 [SLD#2215] Iroas, God of Victory
1 [SNC#291] Jetmir's Garden
1 [FIN#343] Jumbo Cactuar
1 [FIC#406] Jungle Shrine
1 [SLD#371] Kodama's Reach
1 [FIC#349] Lightning Greaves
1 [FIN#400] Lightning, Army of One
1 [MKM#327] Lush Portico
1 [SLD#26] Mirri, Weatherlight Duelist
1 [LTR#379] Mithril Coat
4 [FIC#481] Mountain
1 [TDM#349] Nature's Rhythm
1 [DMC#162] Naya Charm
1 [SLD#237] Ohran Frostfang
1 [AFR#296] Old Gnawbone
1 [FIC#483] Birds of Paradise
1 [MAR#4] Path to Exile
1 [SPG#91] Pathbreaker Ibex
2 [FIC#478] Plains
1 [FIC#296] Professional Face-Breaker
1 [FIC#313] Rampant Growth
1 [FIC#181] Red XIII, Proud Warrior
1 [SLD#1740] Rhythm of the Wild
1 [PLS#141] Rith's Grove
1 [EOE#282] Sacred Foundry
1 [CMM#681] Selvala, Heart of the Wilds
1 [FIC#182] Sephiroth, Fallen Hero
1 [ONC#31] Skyhunter Strike Force
1 [PRM#85956] Spectator Seating
1 [SPE#18] Spider-Man, Miles Morales
1 [CLB#606] Spire Garden
1 [EOE#283] Stomping Ground
1 [FIC#361] Swiftfoot Boots
1 [ECL#351] Temple Garden
1 [LTC#378] The Great Henge
1 Three Visits
1 [DSC#201] Thunderfoot Baloth
1 [KHM#319] Toski, Bearer of Secrets
1 [MOM#373] Tribute to the World Tree
1 [SLD#865] Unnatural Growth
1 [FIC#157] Vincent, Vengeful Atoner
1 [TDM#331] Voice of Victory
1 [MH3#360] Windswept Heath
1 [MH3#361] Wooded Foothills
1 [AFC#329] Wulfgar of Icewind Dale
1 [ONE#315] Zopandrel, Hunger Dominus
1 [PF25#12] Swords to Plowshares
1 [FIN#337] The Fire Crystal

//Sideboard
1 [FIC#188] Tifa, Martial Artist #!Commander

--------------------------------------------------------------------------------
(format data base) 
nombre de fois ou est la cartes et son id unique .

1 00011897-9c8b-482f-8d64-9f2cd8403b6a
3 0000a54c-a511-4925-92dc-01b937f9afad
1 0000579f-7b35-4ed3-b44c-db2a538066fe
1 0000419b-0bba-4488-8f7a-6194544ce91e



--------------------------------------------------------------------------------
liste des différente zone d'un deck possible 

Zone (Deckbuilding)	Rôle / Description	Max / Min	Format principal
Main Deck	Deck principal (toutes les cartes jouées)	min 60 / 100	Tous
Sideboard	Réserve de cartes pour modification entre manches	max 15	Construits
Commander Zone	Contient ton ou tes Commanders	1 ou 2	Commander, Brawl
Companion Zone	Contient le compagnon	1	Tous (optionnel)
Oathbreaker Zone	Contient Oathbreaker + Signature Spell	2	Oathbreaker
Wishboard / Outside Game	Cartes hors du jeu accessibles via effets spéciaux	variable	Casual, Legacy