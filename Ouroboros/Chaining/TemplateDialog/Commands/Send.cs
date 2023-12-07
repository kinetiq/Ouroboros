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

	public Send(TDialogTemplate template)
	{
		Template = template;
		TemplateName = nameof(template);
	}

	public Send(string templateName, TDialogTemplate template)
	{
		TemplateName = templateName;
		Template = template;
	}

}
