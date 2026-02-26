using HearthstoneServer.Entities;

namespace HearthstoneServer;

/// <summary>
/// A list of entities that uses reference equality for containment checks.
/// Used for Player hand/deck/field zones.
/// </summary>
public class CardList<T> : List<T> where T : class
{
	public CardList() { }
	public CardList(IEnumerable<T> items) : base(items) { }
	public CardList(int capacity) : base(capacity) { }

	/// <summary>
	/// Reference-equality containment check.
	/// </summary>
	public new bool Contains(T item)
	{
		for (int i = 0; i < Count; i++)
		{
			if (ReferenceEquals(this[i], item))
				return true;
		}
		return false;
	}

	/// <summary>
	/// Value-equality containment check.
	/// Supports checking by card ID string if T is BaseEntity.
	/// </summary>
	public bool ContainsByValue(T item)
	{
		for (int i = 0; i < Count; i++)
		{
			if (Equals(this[i], item))
				return true;
		}
		return false;
	}

	/// <summary>
	/// Find index by reference equality.
	/// </summary>
	public new int IndexOf(T item)
	{
		for (int i = 0; i < Count; i++)
		{
			if (ReferenceEquals(this[i], item))
				return i;
		}
		return -1;
	}

	/// <summary>
	/// Remove by reference equality.
	/// </summary>
	public new bool Remove(T item)
	{
		for (int i = 0; i < Count; i++)
		{
			if (ReferenceEquals(this[i], item))
			{
				RemoveAt(i);
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Return a new CardList excluding specific items (by reference).
	/// </summary>
	public CardList<T> Exclude(params T[] items)
	{
		var result = new CardList<T>();
		foreach (var e in this)
		{
			bool excluded = false;
			foreach (var ex in items)
			{
				if (ReferenceEquals(e, ex))
				{
					excluded = true;
					break;
				}
			}
			if (!excluded)
				result.Add(e);
		}
		return result;
	}

	/// <summary>
	/// Filter entities by a property value (reflection-based,.
	/// </summary>
	public CardList<T> Filter(string propertyName, object value)
	{
		var result = new CardList<T>();
		var prop = typeof(T).GetProperty(propertyName);
		if (prop == null) return result;

		foreach (var e in this)
		{
			var p = prop.GetValue(e);
			if (p is System.Collections.IEnumerable enumerable && p is not string)
			{
				foreach (var item in enumerable)
				{
					if (Equals(item, value))
					{
						result.Add(e);
						break;
					}
				}
			}
			else if (Equals(p, value))
			{
				result.Add(e);
			}
		}
		return result;
	}

	/// <summary>
	/// Exclude by property value (.
	/// </summary>
	public CardList<T> ExcludeByProperty(string propertyName, object value)
	{
		var result = new CardList<T>();
		var prop = typeof(T).GetProperty(propertyName);
		if (prop == null) return new CardList<T>(this);

		foreach (var e in this)
		{
			var p = prop.GetValue(e);
			if (!Equals(p, value))
				result.Add(e);
		}
		return result;
	}

	/// <summary>
	/// Filter with a predicate.
	/// </summary>
	public CardList<T> Where(Func<T, bool> predicate)
	{
		var result = new CardList<T>();
		foreach (var e in this)
		{
			if (predicate(e))
				result.Add(e);
		}
		return result;
	}

	public override string ToString() => $"[{string.Join(", ", this)}]";
}
