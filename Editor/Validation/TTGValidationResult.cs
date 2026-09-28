namespace TripleTapGames.Foundation.Editor
{
    public enum TTGValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class TTGValidationResult
    {
        public string Category { get; }
        public string Message { get; }
        public TTGValidationSeverity Severity { get; }

        public TTGValidationResult(string category, string message, TTGValidationSeverity severity)
        {
            Category = category;
            Message = message;
            Severity = severity;
        }
    }
}
