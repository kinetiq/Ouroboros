namespace Ouroboros.Chaining.Commands;

/// <summary>
/// Stores the last response in a variable.
/// </summary>
internal class StoreOutputAs : IChatCommand
{
    public string VariableName { get; set; }

    public StoreOutputAs(string variableName)
    {
        VariableName = variableName;
    }
}
