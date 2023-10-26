using System.ComponentModel;

namespace Ouroboros.Core;

public enum MessageRoles
{
    [Description("system")]
    System,
    [Description("user")]
    User,
    [Description("assistant")]
    Assistant
}