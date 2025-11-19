using System.Collections.Generic;

namespace Spittoon.Tests;

public class AppSettingsDocument
{
    public LoggingSettings AppSettings { get; set; } = new();
    public List<UserRow> users { get; set; } = new();
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
    public long id { get; set; }
    public string name { get; set; } = string.Empty;
    public bool active { get; set; }
}
