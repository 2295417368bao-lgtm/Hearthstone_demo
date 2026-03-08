using HearthstoneServer.Auras;
using HearthstoneServer.Entities;
using HearthstoneServer.Events;
using HearthstoneServer.Targeting;
using static HearthstoneServer.Cards.CardScriptUtils;
using static HearthstoneServer.Dsl.BusEvent;
using static HearthstoneServer.Dsl.EventFilters;
using static HearthstoneServer.Dsl.LazyNum;
using static HearthstoneServer.Dsl.RandomCards;

namespace HearthstoneServer.Dsl.Cards;

public class LoeCards : CardPack
{
	public override void Register(CardBuilder b)
	{
		var oneCostMinionInFriendlyDeck = S.FRIENDLY_DECK + S.MINION + S.MaxCost(1) + S.MinCost(1);
		var oneCostMinionInEnemyDeck = S.ENEMY_DECK + S.MINION + S.MaxCost(1) + S.MinCost(1);
		Evaluator HasOtherMech() => new Find(S.FRIENDLY_MINIONS + S.MECH - S.SELF);
		Evaluator HasOtherBeast() => new Find(S.FRIENDLY_MINIONS + S.BEAST - S.SELF);
		Evaluator HasSixOtherMinions() => new LazyNumEvaluator(new Count(S.FRIENDLY_MINIONS - S.SELF), 6, GE());
		Evaluator OpponentHasThreeOtherMinions() => SelectorCountAtLeast(S.ENEMY_MINIONS - S.EVT_SOURCE, 3);

		b.Buff("LOE_006e", cost: -1);
		b.Buff("LOE_017e", atkMod: SetTo(3), maxHealthMod: SetTo(3));
		b.Buff("LOE_018e", atk: 1);
		b.Buff("LOE_061e", atk: 3, health: 3);
		b.Buff("LOE_105e",
			atk: 1,
			health: 1,
			deathrattle: new AddToHand(S.CONTROLLER, "LOE_105"));
		b.Buff("LOE_113e", atk: 2, health: 2);
		b.Buff("LOE_116e", atk: 4, health: 4);
		b.Buff("LOEA16_3e", atk: 10, health: 10);
		b.Buff("LOE_009e");

		b.Card("LOEA10_3");
		b.Card("LOE_022");
		b.Card("LOE_010");
		b.Card("LOE_046", deathrattle: new DealDamage(S.RANDOM_ENEMY_CHARACTER, 1));
		b.Card("LOE_050", deathrattle: new Summon(S.CONTROLLER, RandomCard().Minion().Cost(1)));
		b.Card("LOE_061", deathrattle: new Enchant(S.RANDOM_FRIENDLY_MINION, "LOE_061e"));
		b.Card("LOE_089",
			deathrattle: Acts(
				new Summon(S.CONTROLLER, "LOE_089t"),
				new Summon(S.CONTROLLER, "LOE_089t2"),
				new Summon(S.CONTROLLER, "LOE_089t3")));
		b.Card("LOE_012", deathrattle: new AddToHand(S.CONTROLLER, THE_COIN));
		b.Card("LOE_073",
			battlecry: IfElse(HasOtherBeast(), Taunt(S.SELF), NOTHING));
		b.Card("LOE_017",
			battlecry: Acts(
				new Enchant(S.TARGET, "LOE_017e"),
				new SetCurrentHealth(S.TARGET, 3)),
			target: Target.Required().Minion());
		b.Card("LOE_116",
			battlecry: IfElse(HasSixOtherMinions(), new Enchant(S.SELF, "LOE_116e"), NOTHING));
		b.Card("LOE_009",
			on: When<TurnEndEvent>(OWN, ON, new Summon(S.CONTROLLER, "LOE_009t")));
		b.Card("LOE_111",
			play: Acts(
				new DealDamage(S.ALL_MINIONS, 3),
				new ShuffleIntoDeck(S.OPPONENT, "LOE_111")));
		b.Card("LOE_104",
			play: new ShuffleIntoDeck(S.CONTROLLER, S.TARGET),
			target: Target.Required().Enemy().Minion());
		b.Card("LOE_002",
			play: Acts(
				new DealDamage(S.TARGET, 3),
				new ShuffleIntoDeck(S.CONTROLLER, "LOE_002t")),
			target: Target.Required());
		b.Card("LOE_002t",
			play: new DealDamage(S.TARGET, 6),
			target: Target.Required());
		b.Card("LOE_105",
			play: new Enchant(S.TARGET, "LOE_105e"),
			target: Target.Required().Minion());

		b.Card("LOE_115",
			chooseCards: new[] { "LOE_115a", "LOE_115b" });
		b.Card("LOE_115a",
			play: MakeDiscover(S.CONTROLLER, DiscoverFilter.Minion()));
		b.Card("LOE_115b",
			play: MakeDiscover(S.CONTROLLER, DiscoverFilter.Spell()));
		b.Card("LOE_003",
			battlecry: MakeDiscover(S.CONTROLLER, DiscoverFilter.Spell()));
		b.Card("LOE_029",
			battlecry: MakeDiscover(S.CONTROLLER, DiscoverFilter.All().Cost(3)));
		b.Card("LOE_047",
			battlecry: MakeDiscover(S.CONTROLLER, DiscoverFilter.OfRace(Race.Beast)));
		b.Card("LOE_039",
			battlecry: IfElse(
				HasOtherMech(),
				MakeDiscover(S.CONTROLLER, DiscoverFilter.OfRace(Race.Mechanical)),
				NOTHING));
		b.Card("LOE_006",
			battlecry: MakeDiscover(S.CONTROLLER, DiscoverFilter.Minion().HasMechanic(Mechanic.DEATHRATTLE))
				.Then(new Enchant(S.CURRENT_TARGET, "LOE_006e")));
		b.Card("LOE_023",
			battlecry: MakeDiscover(S.CONTROLLER, DiscoverFilter.All().Cost(1)));
		b.Card("LOE_076",
			battlecry: new Discover(S.CONTROLLER, new BasicHeroPowerPicker())
			{
				Mode = DiscoverMode.Pick,
			}.Then(new ReplaceHeroPower(S.CONTROLLER, S.CURRENT_TARGET)));
		b.Card("LOE_092",
			battlecry: MakeDiscover(S.CONTROLLER, "LOEA16_3", "LOEA16_4", "LOEA16_5"));
		b.Card("LOEA16_3",
			play: new Enchant(S.TARGET, "LOEA16_3e"),
			target: Target.Required().Minion());
		b.Card("LOEA16_4",
			play: new RepeatingDamage(S.ENEMY_CHARACTERS, 1, false, BounceStop.MaxOnly, 10));
		b.Card("LOEA16_5",
			play: new Summon(S.CONTROLLER, "LOEA16_5t") * Of(source =>
			{
				var controller = (source as BuffableEntity)?.Controller;
				return Math.Max(0, 7 - (controller?.Field.Count ?? 0));
			}));
		b.Card("LOEA16_5t");
		b.Card("LOE_020",
			battlecry: Acts(
				new Summon(S.CONTROLLER, S.Random(oneCostMinionInFriendlyDeck)),
				new Summon(S.OPPONENT, S.Random(oneCostMinionInEnemyDeck))));

		b.Card("LOE_077",
			update: new Refresh(S.CONTROLLER, new() { { GameTag.ExtraBattlecries, 1 } }));
		b.Card("LOE_051",
			update: new Refresh(S.ALL_PLAYERS, new() { { GameTag.Spellpower, 2 } }));
		b.Card("LOE_086",
			on: When<SpellCastEvent>(OWN, AFTER,
				new Summon(S.CONTROLLER, RandomCard().Minion().Cost(new Attr(S.EVT_SOURCE, GameTag.Cost)))));
		b.Card("LOE_018",
			on: When<OverloadEvent>(OWN, ON, new Enchant(S.SELF, "LOE_018e") * EVT_AMOUNT));
		b.Card("LOE_016",
			on: When<CardPlayedEvent>(OWN, AFTER,
				new DealDamage(S.RANDOM_ENEMY_CHARACTER, 2),
				when: PlayedHasMechanic(Mechanic.BATTLECRY)));
		b.Card("LOE_107",
			update: new Refresh(S.SELF,
				setTags: new() { { GameTag.CantAttack, Of(source => (source.Game?.Entities.OfType<Minion>().Count(m => m.Zone == Zone.Play && !m.Dormant) ?? 0) > 1 ? 1 : 0) } }));
		b.Card("LOE_119",
			on: When<PreDamageEvent>(OWN, ON,
				new ModifyPreDamage(clampMax: 1),
				when: EventTargetIn<PreDamageEvent>(S.FRIENDLY_HERO)));
		b.Card("LOE_053",
			on: When<SpellCastEvent>(OWN, AFTER,
				new CopySpellEffectOn(S.EVT_SOURCE, S.SELF),
				when: SpellTargets(S.FRIENDLY_MINIONS - S.SELF)));
		b.Card("LOE_007",
			play: new AddToHand(S.OPPONENT, "LOE_007t"));
		b.Card("LOE_007t",
			on: When<TurnStartEvent>(OWN, ON, new DealDamage(S.FRIENDLY_HERO, 2)).InHand());
		b.Card("LOE_113",
			play: new Enchant(S.FRIENDLY_MINIONS, "LOE_113e"),
			costMod: new Count(S.FRIENDLY_MINIONS + S.MURLOC) * -1);
		b.Card("LOE_021",
			secret: When<HeroPowerUsedEvent>(ENEMY, AFTER, new DealDamage(S.RANDOM_ENEMY_CHARACTER, 5)));
		b.Card("LOE_027",
			secret: When<MinionPlayedEvent>(ENEMY, AFTER,
				new Destroy(S.EVT_SOURCE),
				when: Condition<MinionPlayedEvent>(OpponentHasThreeOtherMinions())));

		b.Card("LOE_011",
			battlecry: IfElse(Not(new FindDuplicates(S.FRIENDLY_DECK)), new FullRestore(S.FRIENDLY_HERO), NOTHING));
		b.Card("LOE_079",
			battlecry: new ShuffleIntoDeck(S.CONTROLLER, "LOE_019t"));
		b.Card("LOE_019t",
			play: Acts(
				new ShuffleIntoDeck(S.CONTROLLER, "LOE_019t2"),
				new DrawCard(S.CONTROLLER)));
		b.Card("LOE_019t2",
			battlecry: new ReplaceAllWithLegendaries(S.CONTROLLER));
		b.Card("LOE_038",
			update: new Refresh(S.FRIENDLY_HAND, setTags: new() { { GameTag.Cost, 5 } }));
		b.Card("LOE_110",
			battlecry: new ShuffleIntoDeck(S.CONTROLLER, "LOE_110t"));
		b.Card("LOE_110t",
			draw: new DealDamage(S.FRIENDLY_HERO, 7));
		b.Card("LOE_019",
			battlecry: new CopyDeathrattle(S.TARGET, ""),
			target: Target.IfAvailable().Friendly().Minion());
		b.Card("LOE_118",
			on: When<PreDamageEvent>(OWN, ON,
				new ModifyPreDamage(multiply: 2),
				when: FriendlyHeroWouldTakeDamage()));
		b.Card("LOE_026",
			play: new SummonCopy(S.CONTROLLER, S.Random(S.KILLED + S.MURLOC + S.MINION, 7)));

		b.Card("LOE_089t");
		b.Card("LOE_089t2");
		b.Card("LOE_089t3");
		b.Card("LOE_009t");
	}
}
