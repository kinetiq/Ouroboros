using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Enums;


[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class AliasAttribute : Attribute
{
    public string[] Aliases { get; }

    public AliasAttribute(params string[] aliases)
    {
        Aliases = aliases;
    }
}

[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class NoMatchAttribute : Attribute
{
}
