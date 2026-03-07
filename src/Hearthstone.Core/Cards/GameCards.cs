using HearthstoneServer.Auras;
using HearthstoneServer.Events;
using HearthstoneServer.Targeting;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.BusEvent;

namespace HearthstoneServer.Dsl.Cards;

public class GameCards : CardPack
{
	public override void Register(CardBuilder b)
	{
		b.Card("CS2_034",
			play: new DealDamage(S.TARGET, 1),
			target: Target.Required());
		b.Card("HERO_08bp",
			play: new DealDamage(S.TARGET, 1),
			target: Target.Required());

		b.Card("CS1_001",
			play: new RestoreHealth(S.TARGET, 2),
			target: Target.Required());
		b.Card("HERO_09bp",
			play: new RestoreHealth(S.TARGET, 2),
			target: Target.Required());

		b.Card("CS2_083b", play: new EquipWeapon(S.CONTROLLER, "CS2_082"));
		b.Card("HERO_03bp", play: new EquipWeapon(S.CONTROLLER, "CS2_082"));

		b.Card("CS2_049",
			play: new Summon(S.CONTROLLER, new RandomCardPicker()),
			entourage: new[] { "CS2_050", "CS2_051", "CS2_052", "NEW1_009" });
		b.Card("HERO_02bp",
			play: new Summon(S.CONTROLLER, new RandomCardPicker()),
			entourage: new[] { "CS2_050", "CS2_051", "CS2_052", "NEW1_009" });

		b.Card("CS2_101", play: new Summon(S.CONTROLLER, "CS2_101t"));
		b.Card("HERO_04bp", play: new Summon(S.CONTROLLER, "CS2_101t"));

		b.Card("CS2_102", play: new GainArmor(S.FRIENDLY_HERO, 2));
		b.Card("HERO_01bp", play: new GainArmor(S.FRIENDLY_HERO, 2));

		b.Card("DS1h_292", play: new DealDamage(S.ENEMY_HERO, 2));
		b.Card("HERO_05bp", play: new DealDamage(S.ENEMY_HERO, 2));

		b.Card("CS2_056", play: Acts(new DealDamage(S.FRIENDLY_HERO, 2), new DrawCard(S.CONTROLLER)));
		b.Card("HERO_07bp", play: Acts(new DealDamage(S.FRIENDLY_HERO, 2), new DrawCard(S.CONTROLLER)));

		b.Card("CS2_017", play: Acts(new Enchant(S.FRIENDLY_HERO, "CS2_017o"), new GainArmor(S.FRIENDLY_HERO, 1)));
		b.Card("HERO_06bp", play: Acts(new Enchant(S.FRIENDLY_HERO, "CS2_017o"), new GainArmor(S.FRIENDLY_HERO, 1)));
		b.Buff("CS2_017o", atk: 1, on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));

		b.Card("HERO_10bp", play: new Enchant(S.FRIENDLY_HERO, "HERO_10pe"));
		b.Buff("HERO_10pe", atk: 1, on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));
		b.Card("HERO_10bp2", play: new Enchant(S.FRIENDLY_HERO, "HERO_10bp2e"));
		b.Buff("HERO_10bp2e", atk: 2, on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));

		b.Card("HERO_11bp", play: new Summon(S.CONTROLLER, "HERO_11bpt"));
		b.Card("HERO_11bp2", play: new Summon(S.CONTROLLER, "HERO_11bp2t"));
		b.Card("HERO_11bpt", on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));
		b.Card("HERO_11bp2t", on: When<TurnEndEvent>(OWN, ON, new Destroy(S.SELF)));

		b.Card("AT_132_DRUIDa",
			play: Acts(new Enchant(S.FRIENDLY_HERO, "AT_132_DRUIDe"), new GainArmor(S.FRIENDLY_HERO, 2)));
		b.Card("AT_132_HUNTER_H1", play: new DealDamage(S.ENEMY_HERO, 3));
		b.Card("CS2_034_H1_AT_132",
			play: new DealDamage(S.TARGET, 2),
			target: Target.Required());
		b.Card("CS2_101_H1_AT_132", play: new Summon(S.CONTROLLER, "CS2_101t") * 2);
		b.Card("CS1h_001_H1_AT_132",
			play: new RestoreHealth(S.TARGET, 4),
			target: Target.Required());
		b.Card("AT_132_ROGUE_H1", play: new EquipWeapon(S.CONTROLLER, "AT_132_ROGUEt_H1"));
		b.Card("CS2_049_H1_AT_132",
			play: new ChooseOneAndSummon(S.CONTROLLER, new[]
			{
				"AT_132_SHAMANa",
				"AT_132_SHAMANb",
				"AT_132_SHAMANc",
				"AT_132_SHAMANd",
				"AT_132_SHAMANe"
			}));
		b.Card("AT_132_WARLOCKa", play: new DrawCard(S.CONTROLLER));
		b.Card("CS2_102_H1_AT_132", play: new GainArmor(S.FRIENDLY_HERO, 4));
	}
}
