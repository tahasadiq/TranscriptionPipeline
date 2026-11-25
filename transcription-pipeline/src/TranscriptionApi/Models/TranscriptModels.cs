namespace TranscriptionApi.Models;

public record TranscriptSegment(double Start, double End, string Text);

public record TranscriptResult(
    string FullText,
    double DurationSeconds,
    List<TranscriptSegment> Segments);
