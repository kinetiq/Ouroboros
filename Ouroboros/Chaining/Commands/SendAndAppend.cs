namespace Ouroboros.Chaining.Commands;

internal class SendAndAppend : IChatCommand
{
    public string ElementName { get; set; }

    public SendAndAppend(string elementName = "")
    {
        ElementName = elementName;
    }
}