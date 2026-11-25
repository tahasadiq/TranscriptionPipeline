using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;
using TranscriptionApi.Models;
using TranscriptionApi.Services;

const long maxBytes = 200L * 1024 * 1024;
string[] allowed = [".wav", ".mp3", ".m4a", ".flac", ".ogg"];

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxBytes);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxBytes);
builder.Services.AddSingleton<TranscriptionService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Transcription API",
        Version = "v1",
        Description = "Upload an audio file (wav, mp3, m4a, flac, ogg) and get the transcript with timestamps per segment."
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transcription API v1");
    c.DocumentTitle = "Transcription API";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapPost("/transcribe", async (IFormFile file, TranscriptionService svc, CancellationToken ct) =>
{
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowed.Contains(ext))
        return Results.Problem($"{ext} files aren't supported.", statusCode: 415);

    if (file.Length == 0)
        return Results.Problem("The file is empty.", statusCode: 400);

    var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{ext}");
    try
    {
        await using (var fs = File.Create(tempPath))
            await file.CopyToAsync(fs, ct);

        var result = await svc.TranscribeAsync(tempPath, ct);
        return Results.Ok(result);
    }
    catch (InvalidDataException ex)
    {
        return Results.Problem(ex.Message, statusCode: 422);
    }
    finally
    {
        File.Delete(tempPath);
    }
})
.WithName("Transcribe")
.WithTags("Transcription")
.WithSummary("Transcribe an audio file")
.WithDescription("Accepts wav, mp3, m4a, flac or ogg up to 200 MB. Returns the full text and the segments with start/end time in seconds.")
.Produces<TranscriptResult>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
.ProducesProblem(StatusCodes.Status422UnprocessableEntity)
.DisableAntiforgery();

app.Run();
