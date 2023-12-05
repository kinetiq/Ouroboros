using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Chaining.TemplateDialog.Commands;
internal class StoreParams : ITemplateCommand
{
	public bool OverrideExisting { get; set; }
}
