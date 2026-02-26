namespace HearthstoneServer;

/// <summary>
/// Exception thrown when an invalid game action is attempted.
/// </summary>
public class InvalidActionException : Exception
{
	public InvalidActionException() { }
	public InvalidActionException(string message) : base(message) { }
	public InvalidActionException(string message, Exception innerException)
		: base(message, innerException) { }
}
