namespace HearthstoneServer.Dsl;

// ═══════════════════════════════════════════════════
// CardPack — 卡牌集基类
// 每个扩展包继承此类，在 Register() 中用 CardBuilder 声明式注册卡牌
// ═══════════════════════════════════════════════════

/// <summary>
/// 卡牌集基类。每个扩展包继承此类并重写 <see cref="Register"/>。
/// <para>用法：</para>
/// <code>
/// public class ClassicDemonhunter : CardPack
/// {
///     public override void Register(CardBuilder b)
///     {
///         b.Card("BT_142", play: new Enchant(S.CONTROLLER, "BT_142e"));
///         b.Buff("BT_142e", atk: 1);
///     }
/// }
/// </code>
/// </summary>
public abstract class CardPack
{
	/// <summary>用 CardBuilder 注册此卡牌集的所有卡牌脚本。</summary>
	public abstract void Register(CardBuilder b);

	/// <summary>
	/// 扫描并自动注册所有 CardPack 子类。
	/// 在 CardScriptRegistry.Initialize() 之后调用。
	/// </summary>
	public static void RegisterAll(CardBuilder? builder = null)
	{
		builder ??= new CardBuilder();
		var packTypes = typeof(CardPack).Assembly.GetTypes()
			.Where(t => t.IsSubclassOf(typeof(CardPack)) && !t.IsAbstract);

		foreach (var type in packTypes)
		{
			var pack = (CardPack)Activator.CreateInstance(type)!;
			pack.Register(builder);
		}
	}
}
