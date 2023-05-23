using Ouroboros.TextProcessing;
using Xunit.Abstractions;

namespace Ouroboros.Test.TextProcessing;

public class ListExtractorTests
{
    [Fact]
    public void Numbered_List_Extraction_Works()
    {
        var text = "1. This is the basic case\r\n\r\n2. It just works.\r\n";

        var items = ListExtractor.ExtractNumbered(text);

        Assert.Equal(2, items.Count);
        
        Assert.Equal("This is the basic case", items[0].Text);
        Assert.Equal(1, items[0].Index);

        Assert.Equal("It just works.", items[1].Text);
        Assert.Equal(2, items[1].Index);
    }

    [Fact]
    public void Numbered_List_Extraction_Works1()
    {
        var text = "1.       This is the basic case\n2. It just works.";

        var items = ListExtractor.ExtractNumbered(text);

        Assert.Equal(2, items.Count);
        Assert.Equal("This is the basic case", items[0].Text);
        Assert.Equal(1, items[0].Index);
        Assert.Equal("It just works.", items[1].Text);
        Assert.Equal(2, items[1].Index);
    }


    [Fact]
    public void Numbered_List_Extraction_Works_Without_Periods()
    {
        var text = "1       Numbers in the string 1234 2. are allowed\n2 It just works.";
        
        var items = ListExtractor.ExtractNumbered(text);

        Assert.Equal(2, items.Count);
        Assert.Equal("Numbers in the string 1234 2. are allowed", items[0].Text);
        Assert.Equal(1, items[0].Index);
        Assert.Equal("It just works.", items[1].Text);
        Assert.Equal(2, items[1].Index);
    }

    [Fact]
    public void Numbered_List_Extraction_Works2()
    {
        var text = "1. This is the basic case";
        
        var items = ListExtractor.ExtractNumbered(text);
        
        Assert.Single(items);
        Assert.Equal("This is the basic case", items[0].Text);
    }

    [Fact]
    public void NewLine_List_Extraction_Works()
    {
        var text = "This is the basic case\r\n\r\nIt just works.";

        var items = ListExtractor.Extract(text);

        Assert.Equal(2, items.Count);
        Assert.Equal("This is the basic case", items[0].Text);
        Assert.Equal("It just works.", items[1].Text);
    }

    [Fact]
    public void Unnumbered_Items_Are_Discard_By_ExtractNumbered()
    {
        var text = "This is the basic case\r\n\r\nIt just works.";

        var items = ListExtractor.ExtractNumbered(text);

        Assert.Empty(items);
    }

    [Fact]
    public void Unnumbered_Items_Are_Discard_By_ExtractNumbered_2()
    {
        var text = "This is the basic case\r\n\r\nIt just works.\r\n1. Test";

        var items = ListExtractor.ExtractNumbered(text);

        Assert.Empty(items);
    }


    [Fact]
    public void Single_Line_Works()
    {
        var text = "This is the basic case";

        var items = ListExtractor.Extract(text);
        
        Assert.Single(items);
        Assert.Equal("This is the basic case", items[0].Text);
    }

    [Fact]
    public void Empty_List_Extraction_Works()
    {
        var items = ListExtractor.Extract("");

        Assert.Empty(items);
    }

    [Fact]
    public void WhiteSpace_List_Extraction_Works()
    {
        var items = ListExtractor.Extract(" \n \r\n  ");

        Assert.Empty(items);
    }

    [Fact]
    public void Leading_WhiteSpace_No_Dot_No_Text()
    {
        var items = ListExtractor.ExtractNumbered(" 1252532  ");

        Assert.Single(items);
        Assert.Equal("", items[0].Text);
        Assert.Equal(1252532, items[0].Index);
    }

    [Fact]
    public void Leading_WhiteSpace_Dot_No_Text()
    {
        var items = ListExtractor.ExtractNumbered(" 1252532.  ");

        Assert.Single(items);
        Assert.Equal("", items[0].Text);
        Assert.Equal(1252532, items[0].Index);
    }
}