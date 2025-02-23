namespace PgReorder.Core;

public static class PgShared
{
    public static string? Escape(string? identifier)
    {
        if (identifier is null)
        {
            return null;
        }

        if (identifier.StartsWith('\"'))
        {
            return identifier;
        }

        if (identifier.Contains(' '))
        {
            return $"\"{identifier}\"";
        }

        return identifier;
    }

    public static string? EscapeQuotes(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Contains('\'') ? value.Replace("'", "''") : value;
    }
}