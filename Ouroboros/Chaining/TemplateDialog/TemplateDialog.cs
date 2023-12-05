using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ouroboros.Chaining.TemplateDialog.Commands;
using Ouroboros.Responses;

namespace Ouroboros.Chaining.TemplateDialog;
public class TemplateDialog
{

	/// <summary>
	/// Internal list of chained commands. These are executed sequentially,
	/// allowing us to chain together multiple commands and their output.
	/// </summary>
	private List<ITemplateCommand> Commands { get; set; } = new();

	#region Variable Storage
	private Dictionary<string, string> TempVariableStorage = new();

	private Dictionary<string, string> VariableStorage = new();

	private OuroResponseBase? LastResponse;
	#endregion 
	

	#region Builder Pattern Commands
	
	public TemplateDialog Send<T>(T template, bool fillFromStorage = false)
	{
		Commands.Add(new Send<T>(template, fillFromStorage));

		return this;
	}

	public TemplateDialog Send<T>(string templateName, T template, bool fillFromStorage = false)
	{
		Commands.Add(new Send<T>(templateName, template, fillFromStorage));

		return this;
	}

	public TemplateDialog StoreParams(bool overrideExisting = false)
	{
		Commands.Add(new StoreParams());

		return this;
	}

	public TemplateDialog StoreOutputAs(string variableName)
	{
		Commands.Add(new StoreOutputAs(variableName));

		return this;
	}
	#endregion

	#region Command Execution and Handling
		private async Task<OuroResponseBase> Execute()
		{
			return await ExecuteChainableCommands();
		}

		private async Task<OuroResponseBase> ExecuteChainableCommands()
		{
			LastResponse = null;

			foreach (var command in Commands)
			{
				switch (command)
				{
					case Send<Task> send:
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

		private async Task<OuroResponseBase> HandleSend<T>(Send<T> send)
		{
			//Clear TempVariableStorage
			TempVariableStorage.Clear();

			//Fill Template Parameters
			
			//Store Template Params in TempVariableStorage

			//Await Endpoint Response
			//Parse response and return

		}

		private void HandleStoreOutputAs(StoreOutputAs storeOutputAs)
		{
			if (LastResponse == null)
				throw new InvalidOperationException($"A response is required. Used .Send() before calling .StoreOutputAs()");
			
			VariableStorage.Add(storeOutputAs.VariableName, LastResponse.ResponseText);
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

	

}
