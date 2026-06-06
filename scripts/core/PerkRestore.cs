using System;

// A token returned when a perk is applied. It closes over the exact values that were
// overwritten, so reverting restores them precisely regardless of order.
public class PerkRestore
{
	private readonly Action _revert;

	public PerkRestore(Action revert)
	{
		_revert = revert;
	}

	public void Revert()
	{
		_revert?.Invoke();
	}
}
