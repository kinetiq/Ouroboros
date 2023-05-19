using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.TextProcessing
{
    public class ListItem
    {
        public string Text { get; set; }

        public ListItem(string text)
        {
            Text = text;
        }
    }

    public class NumberedListItem : ListItem
    {
        public int Index { get; set; }

        public NumberedListItem(int index, string text) : base(text)
        {
            Index = index;
        }   
    }
}
