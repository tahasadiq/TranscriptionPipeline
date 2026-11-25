using System.Diagnostics;

namespace TranscriptionApi.Services;

public static class AudioConverter
{
    public static async Task<string> ToWhisperWavAsync(string inputPath, CancellationToken ct)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");

        var psi = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var arg in new[] { "-y", "-i", inputPath,
                                    "-ar", "16000", "-ac", "1",
                                    "-c:a", "pcm_s16le", outputPath })
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start ffmpeg. Is it installed and on PATH?");

        _ = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            throw new InvalidDataException("We couldn't read this audio file. It may be corrupt or not really audio.");
        }

        return outputPath;
    }
}
