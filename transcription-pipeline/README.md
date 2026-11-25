# Transcription Pipeline

A small ASP.NET Core (.NET 8) API that takes an audio file and returns the transcript with timestamps per segment.

It uses [Whisper.net](https://github.com/sandrohanea/whisper.net) (open-source .NET bindings for whisper.cpp), so everything runs locally and the audio never leaves the server. ffmpeg converts any input format into the 16 kHz mono WAV that Whisper expects.

## How it works

```
upload (wav/mp3/m4a/flac/ogg)
   -> check extension + size, save to temp file
   -> ffmpeg: convert to 16 kHz mono PCM WAV  (also catches corrupt / non-audio files)
   -> Whisper.net: transcribe, language auto-detected
   -> JSON: full text + segments with start/end seconds
   -> temp files deleted
```

## Requirements

- .NET 8 SDK
- ffmpeg on your PATH (`winget install ffmpeg` on Windows)
- A Whisper ggml model file

## Getting the model

Download `ggml-base.bin` into `src/TranscriptionApi/WhisperModels/`.

PowerShell:
```powershell
Invoke-WebRequest https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin -OutFile src/TranscriptionApi/WhisperModels/ggml-base.bin
```

bash:
```bash
curl -L -o src/TranscriptionApi/WhisperModels/ggml-base.bin https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin
```

`base` is fine for testing. `small` or `medium` are more accurate but slower. Change `Whisper:ModelPath` in `appsettings.json` to use another one.

## Running it

```bash
cd src/TranscriptionApi
dotnet run
```

Open `http://localhost:<port>/swagger` in the browser (the root URL redirects there too). Use **POST /transcribe**, click *Try it out*, pick an audio file and hit *Execute*.

Or send a file with curl (use the port shown in the console):

```bash
curl -F "file=@meeting.mp3" http://localhost:5000/transcribe
```

Example response: [`samples/sample-response.json`](samples/sample-response.json)

## Errors

| Case | Response |
|---|---|
| Unsupported extension | 415 |
| Empty file | 400 |
| ffmpeg can't decode it (corrupt, not audio) | 422 |
| Over 200 MB | rejected by Kestrel |

## Project structure

```
src/TranscriptionApi/
  Program.cs                        endpoint + validation
  Services/AudioConverter.cs        ffmpeg conversion
  Services/TranscriptionService.cs  Whisper.net transcription + segment mapping
  Models/TranscriptModels.cs        response records
```

## What I'd change for production

I kept this version simple and synchronous on purpose. For real traffic and long files:

- **Async jobs:** upload returns `202 Accepted` with a job id, a background worker (BackgroundService + Channel, or Service Bus + Azure Functions) does the work, client polls `GET /api/v1/transcriptions/{id}` or gets a webhook.
- **Long files:** split on silence (VAD) into 30-60 s chunks with ~1 s overlap, transcribe in parallel, shift timestamps by chunk offset, drop repeated words from the overlap.
- **Concurrency:** fixed number of workers pulling from a queue so the CPU/GPU isn't overloaded, rate limit per API key, 429 + Retry-After when the queue is full.
- **Retries:** job status in a DB, retry only temporary failures with backoff (Polly), dead-letter after 3 attempts, save results per chunk so a retry resumes instead of starting over.
- **API:** versioned REST, transcript as json/srt/vtt, DELETE endpoint for privacy, API key / Azure AD auth, OpenAPI docs, SAS URLs for uploading big files straight to blob storage.
