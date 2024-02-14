using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Enums;

public enum YesNo
{
    NoMatch,
    [Alias("True", "Y")] Yes,
    [Alias("False", "N")] No
}

public enum TrueFalse
{
    NoMatch,
    [Alias("Yes", "1", "Y")] True,
    [Alias("No", "0", "N")] False
}