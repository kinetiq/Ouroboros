using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ouroboros.Chaining.TemplateDialog.Templates;
using Ouroboros.Responses;

namespace Ouroboros.Endpoints;
public interface ITemplateEndpoint
{
	public string Name { get; set; }
	public Dictionary<string, string> Parameters { get; set; }

	Task<OuroResponseBase> SendTemplateAsync(string templateName, IDialogTemplate template);

	

}
