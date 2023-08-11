# What is Ouroboros?
Ouroboros makes it easy to chain API calls with OpenAI. You get:
 - Fluent interface for chaining LLM calls
 - Powerful tools for transforming results into useful forms. For instance, we can detect a numbered list and transform that into a List\<String> or List\<NumberedListItem>
 - Retry capability for transient outages via <a href="https://github.com/App-vNext/Polly">Polly</a>.

# How do I get started?
Check out the [Getting Started Guide](https://github.com/kinetiq/Ouroboros/wiki/Getting-Started).

# Where do I get it?
First, <a href="http://docs.nuget.org/docs/start-here/installing-nuget">install NuGet</a>. Then you can install Ouroboros from the package manager console:

>PM> Install-Package OuroborosAI.Core

# Current Limitations
Ouroboros is production-ready, but it does have limits. If you would like those limits to go away, get involved!
 - Only supports OpenAI API calls. We built this on top of Betalgo, so it should also be possible to support OpenAI on Azure.
 - Only supports chat-based API calls, meaning GPT 3.5-turbo, GPT 4, etc.
 - You can't modify our retry policy, although you _can_ turn it off.
 - It would be great if there was an event of some kind that could be called any time we make a call to OpenAI, to make logging easier. But that doesn't exist.
