using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ouroboros.Chaining.TemplateDialog.Commands;
using Ouroboros.Chaining.TemplateDialog.Templates;
using Ouroboros.Endpoints;
using Ouroboros.Responses;
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

		public static string GetByName(string name)
		{
			return $"[[x]]{name}[[x]]";
		}
	#endregion 
	
	#region Builder Pattern Commands
	
	public TemplateDialog Send(IDialogTemplate template)
	{
		Commands.Add(new Send<IDialogTemplate>(template));

		return this;
	}

	public TemplateDialog Send(string templateName, IDialogTemplate template, ITemplateEndpoint? customEndpoint = null)
	{
		Commands.Add(new Send<IDialogTemplate>(templateName, template, customEndpoint));

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

		private async Task<OuroResponseBase> ExecuteChainableCommands()
		{
			LastResponse = null;

			foreach (var command in Commands)
			{
				switch (command)
				{
					case Send<IDialogTemplate> send:
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

		private async Task<OuroResponseBase> HandleSend(Send<IDialogTemplate> send)
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
