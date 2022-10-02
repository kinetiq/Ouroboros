using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Ouroboros.Document.Elements;
using Z.Core.Extensions;

namespace Ouroboros.Document.Factories
{
    internal class ElementFactory
    {
        public ElementBase Create(string text)
        {
            var doc = XDocument.Parse(text);
            var element = doc.Root;

            return element!.Name.LocalName.ToLower() switch
            {
                "prompt" => CreatePromptElement(element),
                "text" => CreateTextElement(element),
                "resolve" => CreateResolveElement(element),
                _ => throw new InvalidOperationException("Unexpected name: " + element!.Name.LocalName)
            };
        }

        private ElementBase CreateResolveElement(XElement element)
        {
            var promptAttr = element.Attribute("Prompt");

            if (promptAttr == null || promptAttr.Value.IsNullOrWhiteSpace())
                throw new InvalidOperationException("Resolve tag is missing the required attribute 'prompt': " + element.Value);
            
            return new ResolveElement()
            {
                Prompt = promptAttr.Value,
                Content = PrepareContent(element)
            };
        }

        private ElementBase CreateTextElement(XElement element)
        {
            return new TextElement()
            {
                Content = PrepareContent(element)
            };
        }

        private ElementBase CreatePromptElement(XElement element)
        {
            return new PromptElement()
            {
                Content = PrepareContent(element)
            };
        }

        /// <summary>
        /// Because of the structure of xml tags, there can be an extra newline that should not
        /// be included. If that doesn't make sense, here's an example:
        /// </summary>
        private string PrepareContent(XElement element)
        {
            var content = element.Value;

            // Cut only the final whitespace, since it is an artifact caused by the tag system.
            if (content.EndsWith("\n"))
                content = content.Left(content.Length - 1);

            return content;
        }
    }
}
