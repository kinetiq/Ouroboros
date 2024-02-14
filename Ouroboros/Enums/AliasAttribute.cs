using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.Enums;


[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
public sealed class AliasAttribute : Attribute
{
    public string Alias { get; }

    public AliasAttribute(string alias)
    {
        Alias = alias;
    }
}

[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class NoMatchAttribute : Attribute
{
}
