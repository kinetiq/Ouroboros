using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Enums;

public enum AcceptReject
{
    NoMatch,
    [Alias("Allow")] Accept,
    [Alias("Deny")] Reject
}