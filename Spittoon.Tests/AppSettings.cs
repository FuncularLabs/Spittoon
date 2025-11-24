using System.Collections.Generic;

namespace Spittoon.Tests;

public class AppSettingsDocument
{
    public LoggingSettings AppSettings { get; set; } = new();
    public List<UserRow> Users { get; set; } = [];
}

public class LoggingSettings
{
    public LogLevelSettings Logging { get; set; } = new();
}

public class LogLevelSettings
{
    public string Default { get; set; } = "Information";
}

public class UserRow
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
}
