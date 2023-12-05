using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Chaining.TemplateDialog.Commands;
internal class Send<T> : ITemplateCommand
{
	public T Template { get; set; }
	public string TemplateName { get; set; }
	public bool FillParametersFromStorage { get; set; } = false;

	public Send(T template, bool fillFromStorage)
	{
		Template = template;
		TemplateName = nameof(template);
		FillParametersFromStorage = fillFromStorage;
	}

	public Send(string templateName, T template, bool fillFromStorage)
	{
		TemplateName = templateName;
		Template = template;
		FillParametersFromStorage = fillFromStorage;
	}

}
