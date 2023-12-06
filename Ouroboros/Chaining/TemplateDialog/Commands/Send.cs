using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ouroboros.Chaining.TemplateDialog.Templates;

namespace Ouroboros.Chaining.TemplateDialog.Commands;
internal class Send<TDialogTemplate> : ITemplateCommand
{
	public TDialogTemplate Template { get; set; }
	public string TemplateName { get; set; }
	public bool FillParametersFromStorage { get; set; } = false;

	public Send(TDialogTemplate template, bool fillFromStorage)
	{
		Template = template;
		TemplateName = nameof(template);
		FillParametersFromStorage = fillFromStorage;
	}

	public Send(string templateName, TDialogTemplate template, bool fillFromStorage)
	{
		TemplateName = templateName;
		Template = template;
		FillParametersFromStorage = fillFromStorage;
	}

}
