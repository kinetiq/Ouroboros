namespace Ouroboros.Chaining.Commands;

internal class RemoveStartingAtIndex : IChatCommand
{
    public int Index { get; set; }

    public RemoveStartingAtIndex(int index)
    {
        Index = index;
    }
}