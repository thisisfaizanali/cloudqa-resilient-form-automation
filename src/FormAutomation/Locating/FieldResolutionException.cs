namespace FormAutomation.Locating;

/// <summary>One scored candidate, as shown in error messages.</summary>
public sealed record CandidateInfo(string Tag, string? Type, string? Id, string? Name, int Score, string Reasons)
{
    public override string ToString()
        => $"[{Tag} type={Type} id={Id} name={Name} score={Score} ({(Reasons.Length == 0 ? "no signals" : Reasons)})]";
}

/// <summary>Thrown when a field cannot be resolved uniquely from its label.</summary>
public sealed class FieldResolutionException : Exception
{
    public FieldResolutionException(string message) : base(message) { }

    public static FieldResolutionException NotFound(string kind, string label, IReadOnlyList<CandidateInfo> candidates)
        => new($"No {kind} found for label \"{label}\": {candidates.Count} candidates considered. {Join(candidates)}");

    public static FieldResolutionException Ambiguous(string kind, string label, int topScore, IReadOnlyList<CandidateInfo> tied)
        => new($"Ambiguous {kind} for label \"{label}\": {tied.Count} candidates tied at {topScore}. {Join(tied)}");

    private static string Join(IEnumerable<CandidateInfo> candidates) => string.Join(" ", candidates);
}
