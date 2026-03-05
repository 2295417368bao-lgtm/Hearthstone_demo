namespace HearthstoneServer;

public enum CardClass : int
{
	Invalid = 0,
	Deathknight = 1,
	Druid = 2,
	Hunter = 3,
	Mage = 4,
	Paladin = 5,
	Priest = 6,
	Rogue = 7,
	Shaman = 8,
	Warlock = 9,
	Warrior = 10,
	Dream = 11,
	Neutral = 12,
	Whizbang = 13,
	Demonhunter = 14,
}

public enum CardSet : int
{
	Invalid = 0,
	TestTemporary = 1,
	Basic = 2,
	Expert1 = 3,
	Hof = 4,
	Missions = 5,
	Demo = 6,
	None = 7,
	Cheat = 8,
	Blank = 9,
	DebugSp = 10,
	Promo = 11,
	Naxx = 12,
	Gvg = 13,
	Brm = 14,
	Tgt = 15,
	Credits = 16,
	HeroSkins = 17,
	Tb = 18,
	Slush = 19,
	Loe = 20,
	Og = 21,
	OgReserve = 22,
	Kara = 23,
	KaraReserve = 24,
	Gangs = 25,
	GangsReserve = 26,
	Ungoro = 27,
	Icecrown = 1001,
	TbDev = 1003,
	Lootapalooza = 1004,
	Gilneas = 1125,
	Boomsday = 1127,
	Troll = 1129,
	Dalaran = 1130,
	TavernsOfTime = 1143,
	Uldum = 1158,
	Dragons = 1347,
	YearOfTheDragon = 1403,
	BlackTemple = 1414,
	WildEvent = 1439,
	Scholomance = 1443,
	Battlegrounds = 1453,
	DemonHunterInitiate = 1463,
	DarkmoonFaire = 1466,
	TheBarrens = 1525,
	WailingCaverns = 1559,
	Stormwind = 1578,
	Lettuce = 1586,
	AlteracValley = 1626,
	Legacy = 1635,
	Core = 1637,
	Vanilla = 1646,
	TheSunkenCity = 1658,
	Revendreth = 1691,
	MercenariesDev = 1705,
	ReturnOfTheLichKing = 1776,
	BattleOfTheBands = 1809,
	Placeholder202204 = 1810,
	Titans = 1858,
	PathOfArthas = 1869,
	WildWest = 1892,
	WhizbangsWorkshop = 1897,
	Wonders = 1898,
	Tutorial = 1904,
	IslandVacation = 1905,
	Space = 1935,
	Event = 1941,
	EmeraldDream = 1946,
	TheLostCity = 1952,
	TimeTravel = 1957,
	Pet = 1961,
	Cataclysm = 1980,
}

public enum CardType : int
{
	Invalid = 0,
	Game = 1,
	Player = 2,
	Hero = 3,
	Minion = 4,
	Spell = 5,
	Enchantment = 6,
	Weapon = 7,
	Item = 8,
	Token = 9,
	HeroPower = 10,
	Blank = 11,
	GameModeButton = 12,
	MoveMinionHoverTarget = 22,
	LettuceAbility = 23,
	BattlegroundHeroBuddy = 24,
	Location = 39,
	BattlegroundQuestReward = 40,
	BattlegroundSpell = 42,
	BattlegroundAnomaly = 43,
	BattlegroundTrinket = 44,
	Pet = 45,
}

public enum Faction : int
{
	Invalid = 0,
	Horde = 1,
	Alliance = 2,
	Neutral = 3,
}

public enum Race : int
{
	Invalid = 0,
	Bloodelf = 1,
	Draenei = 2,
	Dwarf = 3,
	Gnome = 4,
	Goblin = 5,
	Human = 6,
	Nightelf = 7,
	Orc = 8,
	Tauren = 9,
	Troll = 10,
	Undead = 11,
	Worgen = 12,
	Goblin2 = 13,
	Murloc = 14,
	Demon = 15,
	Scourge = 16,
	Mechanical = 17,
	Elemental = 18,
	Ogre = 19,
	Beast = 20,
	Totem = 21,
	Nerubian = 22,
	Pirate = 23,
	Dragon = 24,
	Blank = 25,
	All = 26,
	Egg = 38,
	Quilboar = 43,
	Centaur = 80,
	Furbolg = 81,
	Highelf = 83,
	Treant = 84,
	Owlkin = 85,
	Halforc = 88,
	Lock = 89,
	Naga = 92,
	Oldgod = 93,
	Pandaren = 94,
	Gronn = 95,
	Celestial = 96,
	Gnoll = 97,
	Golem = 98,
	Harpy = 99,
	Vulpera = 100,
}

public enum Rarity : int
{
	Invalid = 0,
	Common = 1,
	Free = 2,
	Rare = 3,
	Epic = 4,
	Legendary = 5,
	Unknown6 = 6,
}
public enum SpellSchool : int
{
	None = 0,
	Arcane = 1,
	Fire = 2,
	Frost = 3,
	Nature = 4,
	Holy = 5,
	Shadow = 6,
	Fel = 7,
	PhysicalCombat = 8,
	Tavern = 9,
	Spellcraft = 10,
	LesserTrinket = 11,
	GreaterTrinket = 12,
	Upgrade = 13,
}

public enum FactionType : int
{
	Generic = 0,
	GrimyGoons = 1,
	Kabal = 2,
	JadeLotus = 3,
	Zerg = 4,
	Terran = 5,
	Protoss = 6,
}
public enum Mechanic
{
	ADJACENT_BUFF, // "ADJACENT_BUFF"
	AFFECTED_BY_SPELL_POWER, // "AFFECTED_BY_SPELL_POWER"
	AI_MUST_PLAY, // "AI_MUST_PLAY"
	APPEAR_FUNCTIONALLY_DEAD, // "APPEAR_FUNCTIONALLY_DEAD"
	AURA, // "AURA"
	AUTO_ATTACK, // "AUTO_ATTACK"
	AVENGE, // "AVENGE"
	BACON_RALLY, // "BACON_RALLY"
	BACON_SPELLCRAFT_ID, // "BACON_SPELLCRAFT_ID"
	BATTLECRY, // "BATTLECRY"
	CANT_ATTACK, // "CANT_ATTACK"
	CANT_BE_DESTROYED, // "CANT_BE_DESTROYED"
	CANT_BE_FATIGUED, // "CANT_BE_FATIGUED"
	CANT_BE_SILENCED, // "CANT_BE_SILENCED"
	CANT_BE_TARGETED_BY_HERO_POWERS, // "CANT_BE_TARGETED_BY_HERO_POWERS"
	CANT_BE_TARGETED_BY_SPELLS, // "CANT_BE_TARGETED_BY_SPELLS"
	CHARGE, // "CHARGE"
	CHOOSE_ONE, // "CHOOSE_ONE"
	COLLECTIONMANAGER_FILTER_MANA_EVEN, // "COLLECTIONMANAGER_FILTER_MANA_EVEN"
	COLLECTIONMANAGER_FILTER_MANA_ODD, // "COLLECTIONMANAGER_FILTER_MANA_ODD"
	COLOSSAL, // "COLOSSAL"
	COMBO, // "COMBO"
	CORRUPT, // "CORRUPT"
	COUNTER, // "COUNTER"
	DEATHRATTLE, // "DEATHRATTLE"
	DEATH_KNIGHT, // "DEATH_KNIGHT"
	DISCOVER, // "DISCOVER"
	DIVINE_SHIELD, // "DIVINE_SHIELD"
	DREDGE, // "DREDGE"
	DUNGEON_PASSIVE_BUFF, // "DUNGEON_PASSIVE_BUFF"
	ECHO, // "ECHO"
	ELUSIVE, // "ELUSIVE"
	ENCHANTMENT_INVISIBLE, // "ENCHANTMENT_INVISIBLE"
	END_OF_TURN_TRIGGER, // "END_OF_TURN_TRIGGER"
	ENRAGED, // "ENRAGED"
	EVIL_GLOW, // "EVIL_GLOW"
	EXCAVATE, // "EXCAVATE"
	FINISH_ATTACK_SPELL_ON_DAMAGE, // "FINISH_ATTACK_SPELL_ON_DAMAGE"
	FORGE, // "FORGE"
	FORGETFUL, // "FORGETFUL"
	FREEZE, // "FREEZE"
	FRENZY, // "FRENZY"
	GEARS, // "GEARS"
	GHOSTLY, // "GHOSTLY"
	GIGANTIFY, // "GIGANTIFY"
	GRIMY_GOONS, // "GRIMY_GOONS"
	HEROPOWER_DAMAGE, // "HEROPOWER_DAMAGE"
	HONORABLE_KILL, // "HONORABLE_KILL"
	IGNORE_HIDE_STATS_FOR_BIG_CARD, // "IGNORE_HIDE_STATS_FOR_BIG_CARD"
	IMMUNE, // "IMMUNE"
	INFUSE, // "INFUSE"
	INSPIRE, // "INSPIRE"
	ImmuneToSpellpower, // "ImmuneToSpellpower"
	InvisibleDeathrattle, // "InvisibleDeathrattle"
	JADE_GOLEM, // "JADE_GOLEM"
	JADE_LOTUS, // "JADE_LOTUS"
	KABAL, // "KABAL"
	LIFESTEAL, // "LIFESTEAL"
	MAGNETIC, // "MAGNETIC"
	MANATHIRST, // "MANATHIRST"
	MINIATURIZE, // "MINIATURIZE"
	MORPH, // "MORPH"
	MULTIPLY_BUFF_VALUE, // "MULTIPLY_BUFF_VALUE"
	OUTCAST, // "OUTCAST"
	OVERHEAL, // "OVERHEAL"
	OVERKILL, // "OVERKILL"
	OVERLOAD, // "OVERLOAD"
	POISONOUS, // "POISONOUS"
	PUZZLE, // "PUZZLE"
	QUEST, // "QUEST"
	QUICKDRAW, // "QUICKDRAW"
	REBORN, // "REBORN"
	RECEIVES_DOUBLE_SPELLDAMAGE_BONUS, // "RECEIVES_DOUBLE_SPELLDAMAGE_BONUS"
	RUSH, // "RUSH"
	SECRET, // "SECRET"
	SIDE_QUEST, // "SIDE_QUEST"
	SILENCE, // "SILENCE"
	SPARE_PART, // "SPARE_PART"
	SPELLBURST, // "SPELLBURST"
	SPELLPOWER, // "SPELLPOWER"
	STARSHIP, // "STARSHIP"
	STARSHIP_PIECE, // "STARSHIP_PIECE"
	START_OF_COMBAT, // "START_OF_COMBAT"
	START_OF_GAME_KEYWORD, // "START_OF_GAME_KEYWORD"
	STEALTH, // "STEALTH"
	SUMMONED, // "SUMMONED"
	TAG_ONE_TURN_EFFECT, // "TAG_ONE_TURN_EFFECT"
	TAUNT, // "TAUNT"
	TITAN, // "TITAN"
	TRADEABLE, // "TRADEABLE"
	TRIGGER_VISUAL, // "TRIGGER_VISUAL"
	TWINSPELL, // "TWINSPELL"
	UNTOUCHABLE, // "UNTOUCHABLE"
	VENOMOUS, // "VENOMOUS"
	WINDFURY // "WINDFURY"
}
public enum MultiClassGroup : int
{
	Invalid = 0,
	GrimyGoons = 1,
	JadeLotus = 2,
	Kabal = 3,
	PaladinPriest = 4,
	PriestWarlock = 5,
	WarlockDemonhunter = 6,
	HunterDemonhunter = 7,
	DruidHunter = 8,
	DruidShaman = 9,
	MageShaman = 10,
	MageRogue = 11,
	RogueWarrior = 12,
	PaladinWarrior = 13,
	MageHunter = 28,
	HunterDeathknight = 29,
	DeathknightPaladin = 30,
	PaladinShaman = 31,
	ShamanWarrior = 32,
	WarriorDemonhunter = 33,
	DemonhunterRogue = 34,
	RoguePriest = 35,
	PriestDruid = 36,
	DruidWarlock = 37,
	WarlockMage = 38,
}

