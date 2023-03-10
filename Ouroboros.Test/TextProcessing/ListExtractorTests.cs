using Ouroboros.TextProcessing;

namespace Ouroboros.Test.TextProcessing;

public class ListExtractorTests
{
    [Fact]
    public void Numbered_List_Extraction_Works()
    {
        var text = "1. This is the basic case\r\n\r\n2. It just works.\r\n";

        var list = ListExtractor.ExtractList(text);

        Assert.Equal(2, list.Count);
        Assert.Equal("This is the basic case", list[0]);
        Assert.Equal("It just works.", list[1]);
    }
    
    [Fact]
    public void Numbered_List_Extraction_Works1()
    {
        var text = "1.       This is the basic case\n2. It just works.";

        var list = ListExtractor.ExtractList(text);

        Assert.Equal(2, list.Count);
        Assert.Equal("This is the basic case", list[0]);
        Assert.Equal("It just works.", list[1]);
    }


    [Fact]
    public void Numbered_List_Extraction_Works_Without_Periods()
    {
        var text = "1       This is the basic case\n2 It just works.";
        
        var list = ListExtractor.ExtractList(text);

        Assert.Equal(2, list.Count);
        Assert.Equal("This is the basic case", list[0]);
        Assert.Equal("It just works.", list[1]);
    }

    [Fact]
    public void Numbered_List_Extraction_Works2()
    {
        var text = "1. This is the basic case";
        
        var list = ListExtractor.ExtractList(text);
        
        Assert.Single(list);
        Assert.Equal("This is the basic case", list[0]);
    }

    [Fact]
    public void NewLine_List_Extraction_Works()
    {
        var text = "This is the basic case\r\n\r\nIt just works.";

        var list = ListExtractor.ExtractList(text);

        Assert.Equal(2, list.Count);
        Assert.Equal("This is the basic case", list[0]);
        Assert.Equal("It just works.", list[1]);
    }


    [Fact]
    public void Single_Lne_Works()
    {
        var text = "This is the basic case";

        var list = ListExtractor.ExtractList(text);
        
        Assert.Single(list);
        Assert.Equal("This is the basic case", list[0]);
    }

    [Fact]
    public void Empty_List_Extraction_Works()
    {
        var list = ListExtractor.ExtractList("");

        Assert.Empty(list);
    }

    [Fact]
    public void WhiteSpace_List_Extraction_Works()
    {
        var list = ListExtractor.ExtractList(" \n \r\n  ");

        Assert.Empty(list);
    }
}