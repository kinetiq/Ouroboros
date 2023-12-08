using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Chaining.TemplateDialog.Commands;
internal class StoreOutputAs : ITemplateCommand
{
	public string VariableName { get; set; } = "";

	public StoreOutputAs(string variableName)
	{
		VariableName = variableName;
	}
}
