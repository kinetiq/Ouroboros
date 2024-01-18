using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ouroboros.Chaining.TemplateDialog.Commands;
using Ouroboros.Chaining.TemplateDialog.Templates;
using Ouroboros.Endpoints;
using Ouroboros.Enums;
using Ouroboros.Extensions;
using Ouroboros.Responses;
using Ouroboros.TextProcessing;
using Z.Core.Extensions;

namespace Ouroboros.Chaining.TemplateDialog;
public class TemplateDialog
{
	private readonly OuroClient Client;

	/// <summary>
	/// Internal list of chained commands. These are executed sequentially,
	/// allowing us to chain together multiple commands and their output.
	/// </summary>
	private List<ITemplateCommand> Commands { get; set; } = new();

	/// <summary>
	/// The last response we've received from our Endpoint.
	/// </summary>
	private OuroResponseBase? LastResponse;

	#region Error Handling

	/// <summary>
	/// Returns true if there are errors. Because TemplateDialog does not always return
	/// a response object, this gives us a way to be sure the entire chained operation
	/// succeeded.
	/// </summary>
	public bool HasErrors { get; private set; } = false;

	/// <summary>
	/// If there was an error, this provides a way to at least see the most recent one.
	/// </summary>
	public string LastError { get; private set; } = "";

	#endregion
	
	#region Variable Storage
	
    private Dictionary<string, string> VariableStorage = new();

	#endregion 
	
	#region Builder Pattern Commands
	
	public TemplateDialog Send(IOuroTemplateBase templateBase, ITemplateEndpoint? customEndpoint = null)
	{
		Commands.Add(new Send<IOuroTemplateBase>(templateBase, customEndpoint));

		return this;
	}

	public TemplateDialog Send(string templateName, IOuroTemplateBase templateBase, ITemplateEndpoint? customEndpoint = null)
	{
		Commands.Add(new Send<IOuroTemplateBase>(templateName, templateBase, customEndpoint));

		return this;
	}

	public TemplateDialog StoreOutputAs(string variableName)
	{
		Commands.Add(new StoreOutputAs(variableName));

		return this;
	}
	#endregion

	#region Command Execution and Handling
	public async Task<OuroResponseBase> Execute()
	{
		return await ExecuteChainableCommands();
	}

	public async Task<string> ExecuteToString()
	{
		var result = await ExecuteChainableCommands();
		return result.ResponseText;
	}

    /// <summary>
    /// Sends the chat payload for completion, then senses the list type and splits the text into a list.
    /// Works with numbered lists and lists separated by any type of newline. 
    /// </summary>
    public async Task<List<ListItem>> ExecuteAndExtractList()
    {
        var response = await Execute();

        return response.ExtractList();
    }

    /// <summary>
    /// Sends the chat payload for completion, then splits the result into a numbered list.
    /// Any item that doesn't start with a number is discarded. Note that this is different from SendAndExtractList
    /// in a few ways, including the result type, which in this case is able to include the item number (since these
    /// items are numbered).
    /// </summary>
    public async Task<List<NumberedListItem>> ExecuteAndExtractNumberedList()
    {
        var response = await Execute();

        return response.ExtractNumberedList();
    }

	/// <summary>
	/// Returns an enum containing Yes, No, or NoMatch if we are unable to parse the response.
	/// </summary>
    public async Task<YesNo> ExecuteToYesNo()
    {
        var result = await ExecuteChainableCommands();

        return result.ExtractYesNo();
    }

    private async Task<OuroResponseBase> ExecuteChainableCommands()
		{
			LastResponse = null;

			foreach (var command in Commands)
			{
				switch (command)
				{
					case Send<IOuroTemplateBase> send:
						LastResponse = await HandleSend(send);
						break;
					case StoreOutputAs storeOutputAs:
						HandleStoreOutputAs(storeOutputAs);
						break;
					default:
						throw new InvalidOperationException($"Unhandled command: {nameof(command)}");
				}
			}

			return LastResponse ?? new OuroResponseFailure("Unknown Error");
		}

	private async Task<OuroResponseBase> HandleSend(Send<IOuroTemplateBase> send)
	{
		var templateType = send.Template.GetType();

		// Check all the template Properties and update them as requested
		// Manually declared values are not touched by this process
		foreach (var property in templateType.GetProperties())
		{
			var currentValue = property.GetValue(send.Template);
			// If the current value is a string, and contains [[x]], the user want's to manually put a variable in
			// Find that variable and set the property value to it.
			if (currentValue == null || !currentValue.IsValidString() ||
				!currentValue.ToString()!.Contains("[[x]]")) continue;

			//Get all the matching patterns
			var pattern = @"\[\[x\]\](.*?)\[\[x\]\]";
			var matches = Regex.Matches(currentValue.ToString()!, pattern);

			if (matches.Count == 0) continue;

			//Update each match found in the value
			var updatedPropertyValue = currentValue.ToString()!;

			foreach (Match match in matches)
			{
				var variableName = match.Groups[1].Value;
				if (!VariableStorage.TryGetValue(variableName, out var storedValue))
					throw new InvalidOperationException($"Variable {variableName} was not stored and cannot be retrieved.");

				updatedPropertyValue = Regex.Replace(updatedPropertyValue,@"\[\[x\]\]" + Regex.Escape(variableName) + @"\[\[x\]\]", storedValue);
				
			}

			//Finally, update the property itself
			var value = Convert.ChangeType(updatedPropertyValue, property.PropertyType);
			property.SetValue(send.Template, value);
		}
		
		//Await Endpoint Response
		var response = await Client.SendTemplateAsync(send.TemplateName, send.Template, send.CustomEndpoint);

		//Parse response and return
		if (!response.Success)
		{
			HasErrors = true;
			LastError = response.ResponseText;
		}

		return response;
	}

	private void HandleStoreOutputAs(StoreOutputAs storeOutputAs)
	{
		if (LastResponse == null)
			throw new InvalidOperationException($"A response is required. Used .Send() before calling .StoreOutputAs()");
		
		VariableStorage[storeOutputAs.VariableName] = LastResponse.ResponseText;
		
	}
		
	#endregion

	public TemplateDialog(OuroClient client)
	{
		Client = client;
	}

}
