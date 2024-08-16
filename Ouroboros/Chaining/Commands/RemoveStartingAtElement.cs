namespace Ouroboros.Chaining.Commands;

internal class RemoveStartingAtElement : IChatCommand
{
    public string ElementName { get; set; }

    public RemoveStartingAtElement(string elementName)
    {
        ElementName = elementName;
    }
}