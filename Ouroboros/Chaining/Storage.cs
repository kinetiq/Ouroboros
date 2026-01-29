namespace Ouroboros.Chaining;

/// <summary>
/// Provides placeholder-based variable references for TemplateDialog chains.
/// 
/// TemplateDialog is stateless - each Send() is an independent API call, so there's no
/// conversation context carried between calls. To pass output from one call to the next,
/// we use a placeholder pattern:
/// 
/// 1. StoreOutputAs("Analysis") stores the response text in VariableStorage["Analysis"]
/// 2. Storage.GetByName("Analysis") returns the placeholder "[[x]]Analysis[[x]]"
/// 3. When the next Send() executes, TemplateDialog scans template properties for [[x]] markers
///    and replaces them with the actual stored values before sending to the API.
/// 
/// This allows you to reference a variable in a template parameter BEFORE the chain executes,
/// because the placeholder gets resolved at execution time, not at chain-building time.
/// 
/// Example:
/// <code>
/// var result = await dialog
///     .Send(new Analyzer { Text = input })
///     .StoreOutputAs("Analysis")
///     .Send(new Summarizer { PreviousResult = Storage.GetByName("Analysis") })
///     .ExecuteToString();
/// </code>
/// 
/// Note: Dialog (the stateful alternative) uses a simpler approach with Variables dictionary
/// since it maintains conversation context automatically.
/// </summary>
public static class Storage
{
    /// <summary>
    /// Returns a placeholder marker that will be resolved to the stored variable's value at execution time.
    /// The [[x]] delimiters are used by TemplateDialog.UpdateTemplateProperties() to find and replace these markers.
    /// </summary>
    public static string GetByName(string name)
    {
        return $"[[x]]{name}[[x]]";
    }
}
