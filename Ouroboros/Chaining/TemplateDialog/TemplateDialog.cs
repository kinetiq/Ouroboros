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
	private readonly ITemplateEndpoint AiEndpoint;

	/// <summary>
	/// Internal list of chained commands. These are executed sequentially,
	/// allowing us to chain together multiple commands and their output.
	/// </summary>
	private List<ITemplateCommand> Commands { get; set; } = new();

	/// <summary>
	/// The last response we've received from our Endpoint.
	/// </summary>
	private OuroResponseBase? LastResponse;

	public static string GetByName(string name)
	{
		return $"[[x]]{name}[[x]]";
	}

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
	
		private Dictionary<string, string> TempVariableStorage = new();

		private Dictionary<string, string> VariableStorage = new();

		public string GetParameter(string name)
		{
			if (VariableStorage.TryGetValue(name, out var result))
			{
				return result;
			}

			throw new InvalidOperationException($"Variable {name} couldn't be found in Parameters.");
		}

	#endregion 
	
	#region Builder Pattern Commands
	
	public TemplateDialog Send(IDialogTemplate template, bool fillFromStorage = false)
	{
		Commands.Add(new Send<IDialogTemplate>(template, fillFromStorage));

		return this;
	}

	public TemplateDialog Send(string templateName, IDialogTemplate template, bool fillFromStorage = false)
	{
		Commands.Add(new Send<IDialogTemplate>(templateName, template, fillFromStorage));

		return this;
	}

	public TemplateDialog StoreParams(bool overrideExisting = false)
	{
		Commands.Add(new StoreParams(overrideExisting));

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
					case StoreParams storeParams:
						HandleStoreParams(storeParams);
						break;
					default:
						throw new InvalidOperationException($"Unhandled command: {nameof(command)}");
				}
			}

			return LastResponse ?? new OuroResponseFailure("Unknown Error");
		}

		private async Task<OuroResponseBase> HandleSend(Send<IDialogTemplate> send)
		{
			//Clear TempVariableStorage
			TempVariableStorage.Clear();
		
			var templateType = send.Template.GetType();
			var defaultTemplateInstance = Activator.CreateInstance(templateType);

			// Check all the template Properties and update them as requested
			// Manually declared values are not touched by this process
			foreach (var property in templateType.GetProperties())
			{
				var currentValue = property.GetValue(send.Template);
				var defaultValue = property.GetValue(defaultTemplateInstance);

				// If the currentValue isn't different, and we want to Fill Parameters From Storage,
				// Check if there is a matching parameter in storage and use that
				if (Equals(currentValue, defaultValue) && send.FillParametersFromStorage)
				{
					if (!VariableStorage.TryGetValue(property.Name, out var storedValue)) continue;
					
					var value = Convert.ChangeType(storedValue, property.PropertyType);
					property.SetValue(send.Template, value);
				}
				// If the current value is a string, and contains [[x]], the user want's to manually put a variable in
				// Find that variable and set the property value to it.
				else if (currentValue != null && currentValue.IsValidString() && currentValue.ToString()!.Contains("[[x]]"))
				{
					var pattern = @"\[\[x\]\](.*?)\[\[x\]\]";
					var match = Regex.Match(currentValue.ToString()!, pattern);

					if (!match.Success) continue;

					var variableName = match.Groups[1].Value;
					if (!VariableStorage.TryGetValue(variableName, out var storedValue))
						throw new InvalidOperationException($"Variable {variableName} was not stored and cannot be retrieved.");

					var updatedPropertyValue = Regex.Replace(currentValue.ToString()!, pattern, storedValue);
					var value = Convert.ChangeType(updatedPropertyValue, property.PropertyType);
						
					property.SetValue(send.Template, value);

				}
			}
			
			//Store Template Params in TempVariableStorage. We may save them later.
			var properties = templateType.GetProperties();
			foreach (var property in properties)
			{
				var value = property.GetValue(send.Template);
				TempVariableStorage[property.Name] = value?.ToString() ?? string.Empty;
			}
			
			//Await Endpoint Response
			var response = await AiEndpoint.SendTemplateAsync(send.TemplateName, send.Template);

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


		private void HandleStoreParams(StoreParams storeParams)
		{
			Dictionary<string, string> result;

			if (storeParams.OverrideExisting)
			{
				result = TempVariableStorage
					.Union(VariableStorage.Where(pair => !TempVariableStorage.ContainsKey(pair.Key)))
					.ToDictionary(pair => pair.Key, pair => pair.Value);
			}
			else
			{
				result = VariableStorage
					.Union(TempVariableStorage.Where(pair => !VariableStorage.ContainsKey(pair.Key)))
					.ToDictionary(pair => pair.Key, pair => pair.Value);
			}

			VariableStorage.Clear();
			VariableStorage = result;
			TempVariableStorage.Clear();
		}

	#endregion

	public TemplateDialog(ITemplateEndpoint aiEndpoint)
	{
		AiEndpoint = aiEndpoint;
	}

}
