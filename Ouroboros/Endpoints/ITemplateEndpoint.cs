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
	Task<OuroResponseBase> SendTemplateAsync(IOuroTemplateBase templateBase);
}
