# What is Ouroboros?
Ouroboros makes it easy to chain and transform API calls with OpenAI. You get:
 - **Clean chaining:** Fluent interface for feeding the output of one API call into the input of another.
	- Easily capture the output of one call, save it to a variable, and then use it as input downchain.
 - **Template engine:** Store your prompts as markdown right in your project, with a corresponding class for fields.
	- Store your prompts as markdown right in your project.
	- You can use a class with the same name for field injection using {{ mustache syntax }}.
	- You can also inject other templates using the same syntax.
 - **Transform model results into code:** using our Hermetic Codex and Proteus Convert (both parts of this project).
	- Detect a numbered list and auto-transform it into a List\<String> or List\<NumberedListItem>
	- Convert results directly into classes or enums.
	- Smart, recoverable detection of errors / unmappable output.
 - **Exponential backoff (retry)**: Always on, for transient outages via <a href="https://github.com/App-vNext/Polly">Polly</a> on all calls.
 - **Simple Chat API**: You can also do regular Chat Completions calls, which gives you Retry.

# How do I get started?
Check out the [Getting Started Guide](https://github.com/kinetiq/Ouroboros/wiki/Getting-Started) (this needs updating). 

# Where do I get it?
First, <a href="http://docs.nuget.org/docs/start-here/installing-nuget">install NuGet</a>. Then you can install Ouroboros from the package manager console:

>PM> Install-Package OuroborosAI.Core

# Limits and Possible Contributions
Ouroboros is production-ready, but it does have limits. If you would like those limits to go away, get involved!
 - Only supports OpenAI API calls. We built this on top of Betalgo, so it should also be possible to support OpenAI on Azure.
 - Chaining only supports the Chat Completions API, meaning GPT 4.1, GPT 4o, etc.
 - You can't modify our retry policy, although you _can_ turn it off.
 - We could use some help implementing Logging, support for other providers, and the new Responses API.
