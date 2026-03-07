namespace HearthstoneServer.Dsl.Cards;

public class ClassicDeathknight : CardPack
{
	public override void Register(CardBuilder b)
	{
		// 数据库补齐：当前仅依赖卡牌数据或后续脚本实现。
		b.Card("GIFT_04"); // 阿尔萨斯的礼物
	}
}
