﻿using System;
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

namespace Ouroboros.Chaining.TemplateDialog;

/// <summary>
/// Allows chaining of prompts.
/// This version is used when working with templates managed by a third party service such as PromptLayer.
/// </summary>
public class TemplateDialog
{
    private readonly OuroClient Client;

    #region Variable Storage

    private readonly Dictionary<string, string> VariableStorage = new();

    #endregion

    /// <summary>
    /// The last response we've received from our Endpoint.
    /// </summary>
    private OuroResponseBase? LastResponse;

    /// <summary>
    /// Internal list of chained commands. These are executed sequentially,
    /// allowing us to chain together multiple commands and their output.
    /// </summary>
    private List<ITemplateCommand> Commands { get; } = new();

    public TemplateDialog(OuroClient client)
    {
        Client = client;
    }

    #region Error Handling

    /// <summary>
    /// Returns true if there are errors. Because TemplateDialog does not always return
    /// a response object, this gives us a way to be sure the entire chained operation
    /// succeeded.
    /// </summary>
    public bool HasErrors { get; private set; }

    /// <summary>
    /// If there was an error, this provides a way to at least see the most recent one.
    /// </summary>
    public string LastError { get; private set; } = "";

    #endregion

    #region Builder Pattern Commands

    public TemplateDialog Send(IOuroTemplateBase templateBase, ITemplateEndpoint? customEndpoint = null)
    {
        Commands.Add(new Send<IOuroTemplateBase>(templateBase, customEndpoint));

        return this;
    }

    public TemplateDialog StoreOutputAs(string variableName)
    {
        Commands.Add(new StoreOutputAs(variableName));

        return this;
    }

    #endregion

    #region Command Execution and Handling

    #region Terminators

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
    /// Extracts to an arbitrary enum. The enum must have a member called "NoMatch", or there will be exceptions at
    /// runtime.
    /// </summary>
    /// <typeparam name="TEnum">An enum that has a member called NoMatch.</typeparam>
    public async Task<TEnum> ExtractEnum<TEnum>() where TEnum : struct, Enum
    {
        var response = await ExecuteChainableCommands();

        var enumResult = response.ExtractEnum<TEnum>();

        // If we didn't get a match, and we don't already have an error, set the error. 
        // If we do have an error, that will be the more specific problem, so we don't want to overwrite it.
        if (enumResult.IsNoMatch() && HasErrors == false)
        {
            HasErrors = true;
            LastError = $"After executing the chain with no errors, tried to extract to {typeof(TEnum).Name} from the " + 
                        "response text, but no match was found. Check LastResponse for more details or use ExecuteToString instead.";
        }

        return enumResult;
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
    /// NoMatch results in the dialog being marked as having errors.
    /// </summary>
    public async Task<YesNo> ExecuteToYesNo()
    {
        var response = await ExecuteChainableCommands();

        var result = response.ExtractYesNo();

        if (result == YesNo.NoMatch && HasErrors == false)
        {
            HasErrors = true;
            LastError = $"After executing the chain with no errors, tried to extract to {nameof(YesNo)} from the response text, " + 
                        "but no match was found. Check LastResponse for more details or use ExecuteToString instead.";
        }

        return result;
    }

    /// <summary>
    /// Extracts the results to an arbitrary model. If the model cannot be fully bound, an error is set on the dialog.
    /// </summary>
    public async Task<T> ExtractModel<T>() where T : CodexModel
    {
        var response = await Execute();

        var codex = new HermeticCodex<T>();

        var result = codex.Bind(response.ResponseText);

        // If there is a problem binding the model, set the error. But if there was an error deeper in the chain, don't overwrite it.
        if (!result.IsComplete() && HasErrors == false)
        {
            HasErrors = true;
            LastError = "HermeticCodex > Generation appears to be successful, however the model could not be fully bound. " + 
                        "Check the model for more details.";
        }

        return result;
    }

    #endregion

    private async Task<OuroResponseBase> ExecuteChainableCommands()
    {
        LastResponse = null;

        foreach (var command in Commands)
            switch (command)
            {
                case Send<IOuroTemplateBase> send:
                    var response = await HandleSend(send);

                    LastResponse = response;

                    // If there is an error talking to OpenAI, stop execution immediately.
                    if (!response.Success)
                        return response;

                    break;
                case StoreOutputAs storeOutputAs:
                    HandleStoreOutputAs(storeOutputAs);
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled command: {nameof(command)}");
            }

        return LastResponse ?? new OuroResponseInternalError(
            "ExecuteChainableCommands was called, but when it was finished, " +
            "there were no responses to return. This should never happen.");
    }

    private async Task<OuroResponseBase> HandleSend(Send<IOuroTemplateBase> send)
    {
        UpdateTemplateProperties(send);

        // Send to endpoint
        var response = await Client.SendTemplateAsync(send.Template, send.CustomEndpoint);

        //Parse response and return
        if (!response.Success)
        {
            HasErrors = true;
            LastError = response.ResponseText;
        }

        return response;
    }

    #region Template Properties

    /// <summary>
    /// Update template properties with stored values. This allows us to use [[x]] to manually insert variables into the
    /// template,
    /// which is how Storage.GetByName works.
    /// </summary>
    private void UpdateTemplateProperties(Send<IOuroTemplateBase> send)
    {
        var templateType = send.Template.GetType();

        // Check all the template Properties and update them as requested
        // Manually declared values are not touched by this process
        foreach (var property in templateType.GetProperties())
        {
            var currentValue = property.GetValue(send.Template);

            // If the current value is a string, and contains [[x]], the user wants to manually put a variable in
            // Find that variable and set the property value to it.
            if (currentValue is not string ||
                !currentValue.ToString()!.Contains("[[x]]")) continue;

            //Get all the matching patterns
            var pattern = @"\[\[x\]\](.*?)\[\[x\]\]";
            var matches = Regex.Matches(currentValue.ToString()!, pattern);

            if (matches.Count == 0) continue;

            //Update each match found in the value
            var updatedPropertyValue = ReplaceMatchesWithVariables(matches, currentValue.ToString()!);

            //Finally, update the property itself
            var value = Convert.ChangeType(updatedPropertyValue, property.PropertyType);
            property.SetValue(send.Template, value);
        }
    }

    /// <summary>
    /// For each [[x]], replace the [[x]] with our stored variable.
    /// </summary>
    /// <returns>The updated property value after substitutions.</returns>
    /// <exception cref="InvalidOperationException">If a variable is referenced in an [[x]] but doesn't exist, we throw.</exception>
    private string ReplaceMatchesWithVariables(MatchCollection matches, string propertyValue)
    {
        foreach (Match match in matches)
        {
            var variableName = match.Groups[1].Value;
            if (!VariableStorage.TryGetValue(variableName, out var storedValue))
                throw new InvalidOperationException($"Variable {variableName} was not stored and cannot be retrieved.");

            propertyValue = Regex.Replace(propertyValue, @"\[\[x\]\]" + Regex.Escape(variableName) + @"\[\[x\]\]",
                storedValue);
        }

        return propertyValue;
    }

    #endregion

    private void HandleStoreOutputAs(StoreOutputAs storeOutputAs)
    {
        if (LastResponse == null)
            throw new InvalidOperationException("A response is required. Used .Send() before calling .StoreOutputAs()");

        VariableStorage[storeOutputAs.VariableName] = LastResponse.ResponseText;
    }

    /// <summary>
    /// Gets the last response. Useful in cases where we try to extract a specific type of result and there is no match,
    /// and then
    /// we want find ourselves wanting the actual message.
    /// </summary>
    public OuroResponseBase? GetLastResponse()
    {
        return LastResponse;
    }

    #endregion
}