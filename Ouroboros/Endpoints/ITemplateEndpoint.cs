using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ouroboros.Responses;

namespace Ouroboros.Endpoints;
public interface ITemplateEndpoint
{
	public string Name { get; set; }
	public Dictionary<string, string> Parameters { get; set; }

	Task<OuroResponseBase> SendTemplateAsync<T>(string templateName, T template);

	private void CaptureParameters<T>(T template)
	{
		Parameters.Clear();

		var properties = typeof(T).GetProperties();
		foreach (var property in properties)
		{
			var value = property.GetValue(template);
			Parameters[property.Name] = value?.ToString() ?? string.Empty;
		}
	}

}
