using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Chaining.TemplateDialog;
public static class Storage
{
	public static string GetByName(string name)
	{
		return $"[[x]]{name}[[x]]";
	}
}
