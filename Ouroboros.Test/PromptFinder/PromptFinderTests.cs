using Ouroboros.Documents.Elements;
using Ouroboros.Documents.Factories;

namespace Ouroboros.Test.PromptFinder;

public class PromptFinderTests
{
    /// <summary>
    /// Lacking a prompt element, PromptFinder should take the first line of the text element and turn that into a prompt element.
    /// </summary>
    [Fact]
    public void Prompt_Is_Grafted_From_Text()
    {
        var elementFactory = new ElementFactory();
            
        var docElements = new List<ElementBase>()
        {
            elementFactory.Create("<Text>" +
                                  "This line should become the prompt.\n" +
                                  "This line will remain in the text element." +
                                  "</Text>")
        };

        var promptFinder = new Documents.Factories.PromptFinder(docElements);

        promptFinder.FindPrompt();

        Assert.Equal(2, docElements.Count);
        Assert.IsType<PromptElement>(docElements[0]);
        Assert.Equal("This line should become the prompt.", docElements[0].Text);
        Assert.Equal("This line will remain in the text element.", docElements[1].Text);
    }

    /// <summary>
    /// Ensure that if the first line has whitespace, we don't use that and instead use the first line with text.
    /// </summary>
    [Fact]
    public void Prompt_Is_Grafted_From_Text_With_Whitespace()
    {
        var elementFactory = new ElementFactory();

        var docElements = new List<ElementBase>()
        {
            elementFactory.Create("<Text>\n" +
                                  "\n" +
                                  "     \n" +
                                  "This line should become the prompt.\n" +
                                  "  \n" +
                                  "This line will remain in the text element." +
                                  "</Text>")
        };

        var promptFinder = new Documents.Factories.PromptFinder(docElements);

        promptFinder.FindPrompt();

        Assert.Equal(2, docElements.Count);
        Assert.IsType<PromptElement>(docElements[0]);
        Assert.Equal("This line should become the prompt.", docElements[0].Text);
        Assert.Equal("  \nThis line will remain in the text element.", docElements[1].Text);
    }

    /// <summary>
    /// If the text element is empty after the graft, it should be removed.
    /// </summary>
    [Fact]
    public void Empty_TextElement_Is_Removed()
    {
        var elementFactory = new ElementFactory();

        var docElements = new List<ElementBase>()
        {
            elementFactory.Create("<Text>This line should become the prompt.</Text>")
        };

        var promptFinder = new Documents.Factories.PromptFinder(docElements);

        promptFinder.FindPrompt();

        Assert.Single(docElements);
        Assert.IsType<PromptElement>(docElements[0]);
        Assert.Equal("This line should become the prompt.", docElements[0].Text);
    }
}