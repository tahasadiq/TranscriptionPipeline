using TranscriptionApi.Models;
using Whisper.net;

namespace TranscriptionApi.Services;

public class TranscriptionService : IDisposable
{
    private readonly WhisperFactory _factory;

    public TranscriptionService(IConfiguration config)
    {
        var modelPath = config["Whisper:ModelPath"] ?? "WhisperModels/ggml-base.bin";
        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                $"Whisper model not found at '{modelPath}'. See README for how to download it.");

        _factory = WhisperFactory.FromPath(modelPath);
    }

    public async Task<TranscriptResult> TranscribeAsync(string inputPath, CancellationToken ct)
    {
        var wavPath = await AudioConverter.ToWhisperWavAsync(inputPath, ct);
        try
        {
            var raw = await RunWhisperAsync(wavPath, ct);

            var segments = raw
                .Select(s => new TranscriptSegment(
                    Math.Round(s.Start.TotalSeconds, 2),
                    Math.Round(s.End.TotalSeconds, 2),
                    s.Text.Trim()))
                .Where(s => s.Text.Length > 0)
                .ToList();

            return new TranscriptResult(
                string.Join(" ", segments.Select(s => s.Text)),
                segments.LastOrDefault()?.End ?? 0,
                segments);
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    private async Task<List<SegmentData>> RunWhisperAsync(string wavPath, CancellationToken ct)
    {
        using var processor = _factory.CreateBuilder()
            .WithLanguage("auto")
            .Build();

        await using var audio = File.OpenRead(wavPath);

        var segments = new List<SegmentData>();
        await foreach (var segment in processor.ProcessAsync(audio, ct))
            segments.Add(segment);

        return segments;
    }

    public void Dispose() => _factory.Dispose();
}
