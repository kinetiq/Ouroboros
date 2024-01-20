using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ouroboros.Chaining.TemplateDialog.Templates;
using Ouroboros.Endpoints;

namespace Ouroboros.Chaining.TemplateDialog.Commands;
internal class Send<TDialogTemplate> : ITemplateCommand where TDialogTemplate : IOuroTemplateBase
{
	public TDialogTemplate Template { get; set; }
	public ITemplateEndpoint? CustomEndpoint { get; set; }

	public Send(TDialogTemplate template, ITemplateEndpoint? customEndpoint = null)
	{
		Template = template;
		CustomEndpoint = customEndpoint;
	}
}
