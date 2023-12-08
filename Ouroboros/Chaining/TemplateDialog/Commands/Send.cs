using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ouroboros.Chaining.TemplateDialog.Templates;
using Ouroboros.Endpoints;

namespace Ouroboros.Chaining.TemplateDialog.Commands;
internal class Send<TDialogTemplate> : ITemplateCommand
{
	public TDialogTemplate Template { get; set; }
	public string TemplateName { get; set; }
	public ITemplateEndpoint? CustomEndpoint { get; set; }

	public Send(TDialogTemplate template, ITemplateEndpoint? customEndpoint = null)
	{
		Template = template;
		TemplateName = nameof(template);
		CustomEndpoint = customEndpoint;
	}

	public Send(string templateName, TDialogTemplate template, ITemplateEndpoint? customEndpoint = null)
	{
		TemplateName = templateName;
		Template = template;
		CustomEndpoint = customEndpoint;
	}

}
