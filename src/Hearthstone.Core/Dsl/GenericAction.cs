using HearthstoneServer.Entities;

namespace HearthstoneServer.Dsl;

/// <summary>
/// A simple action that executes a callback delegate.
/// Useful for tests and inline scripting.
/// </summary>
public class GenericAction : GameAction
{
	private readonly System.Action _callback;

	public GenericAction(System.Action callback)
	{
		_callback = callback;
	}

	public override void Do(BaseEntity source, params object[] args)
	{
		_callback();
	}
}
