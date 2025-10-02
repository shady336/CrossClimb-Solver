using System.Text.Json.Serialization;

public class CandidateGenerationResponse
{
    [JsonPropertyName("wordLength")]
    public required int WordLength { get; set; }

    [JsonPropertyName("items")]
    public required ClueWithCandidates[] Items { get; set; }
}

public class ClueWithCandidates
{
    [JsonPropertyName("clue")]
    public required string Clue { get; set; }

    [JsonPropertyName("candidates")]
    public required Candidate[] Candidates { get; set; }
}

public class Candidate
{
    [JsonPropertyName("word")]
    public required string Word { get; set; }

    [JsonPropertyName("reason")]
    public required string Reason { get; set; }
}
