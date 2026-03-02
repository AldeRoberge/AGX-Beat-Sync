using System.Diagnostics;
using System.Globalization;
using Spectre.Console;

namespace AGX_Beat_Sync_Deploy;

class Program
{
    private const string GcsBucketName = "agx-beat-sync-builds";
    /// <summary>GCP project that owns the bucket (must have active billing).</summary>
    private const string GcpProjectId = "aggh-483802";

    /// <summary>Solution root (AGX-Beat-Sync), resolved from executable location so paths work when run from bin/Debug/net9.0.</summary>
    private static string SolutionRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string ProjectPath => Path.Combine(SolutionRoot, "AGX-Beat-Sync", "AGX-Beat-Sync.csproj");

    static async Task<int> Main(string[] args)
    {
        try
        {
            AnsiConsole.Write(new FigletText("AGX Beat Sync Deploy").Color(Color.Aqua));

            // Parse arguments
            var configuration = args.Length > 0 ? args[0] : "Release";
            var runtime = args.Length > 1 ? args[1] : "win-x64";

            AnsiConsole.MarkupLine($"[cyan]Configuration:[/] {configuration}");
            AnsiConsole.MarkupLine($"[cyan]Runtime:[/] {runtime}");
            AnsiConsole.WriteLine();

            // Step 1: Build the project (fail fast on compile errors)
            if (!await BuildProject(configuration, runtime))
            {
                AnsiConsole.MarkupLine("[red]Build failed. Fix errors before deploying.[/]");
                return 1;
            }

            // Step 2: Publish the project
            var publishPath = await PublishProject(configuration, runtime);
            if (publishPath == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to publish project[/]");
                return 1;
            }

            // Step 3: Zip the published output
            var versionFolder = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            var zipPath = await ZipPublishDirectory(publishPath, versionFolder, runtime);
            if (zipPath == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to create zip archive[/]");
                return 1;
            }

            // Step 4: Ensure GCS bucket exists
            if (!await EnsureBucketExists())
            {
                AnsiConsole.MarkupLine("[red]Failed to create or verify GCS bucket[/]");
                return 1;
            }

            // Step 5: Upload to GCS (use UTC for version folder name so builds are comparable across timezones)
            var uploadSuccess = await UploadToGcs(zipPath, versionFolder, runtime);

            if (uploadSuccess)
            {
                AnsiConsole.MarkupLine($"[green]✓ Deployment successful![/]");
                AnsiConsole.MarkupLine($"[cyan]Version:[/] {versionFolder}");
                AnsiConsole.MarkupLine($"[cyan]GCS Path:[/] gs://{GcsBucketName}/{runtime}/{versionFolder}.zip");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Failed to upload to GCS[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    static async Task<bool> BuildProject(string configuration, string runtime)
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Building AGX-Beat-Sync...", async _ =>
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"build \"{ProjectPath}\" -c {configuration} -r {runtime}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var process = Process.Start(startInfo);
                if (process == null)
                {
                    AnsiConsole.MarkupLine("[red]Failed to start dotnet build process.[/]");
                    return false;
                }
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    AnsiConsole.MarkupLine("[red]Build failed:[/]");
                    AnsiConsole.WriteLine(output);
                    AnsiConsole.WriteLine(error);
                    return false;
                }
                AnsiConsole.MarkupLine("[green]✓ Build succeeded[/]");
                return true;
            });
    }

    static async Task<string?> PublishProject(string configuration, string runtime)
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Publishing AGX-Beat-Sync...", async ctx =>
            {
                var publishDir = Path.Combine(Path.GetTempPath(), $"agx-beat-sync-publish-{Guid.NewGuid()}");
                Directory.CreateDirectory(publishDir);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"publish \"{ProjectPath}\" -c {configuration} -r {runtime} --self-contained true -o \"{publishDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    AnsiConsole.MarkupLine("[red]Failed to start dotnet publish process[/]");
                    return null;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    AnsiConsole.MarkupLine("[red]Publish failed:[/]");
                    AnsiConsole.WriteLine(output);
                    AnsiConsole.WriteLine(error);
                    return null;
                }

                ctx.Status($"Published to {publishDir}");
                AnsiConsole.MarkupLine($"[green]✓ Published successfully[/]");

                var files = Directory.GetFiles(publishDir, "*", SearchOption.AllDirectories);
                AnsiConsole.MarkupLine($"[dim]  {files.Length} files in publish directory[/]");

                return publishDir;
            });
    }

    static async Task<string?> ZipPublishDirectory(string publishDir, string versionFolder, string runtime)
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Zipping published output...", async _ =>
            {
                await Task.Yield();
                var zipPath = Path.Combine(Path.GetTempPath(), $"agx-beat-sync-{runtime}-{versionFolder}.zip");
                try
                {
                    System.IO.Compression.ZipFile.CreateFromDirectory(publishDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);

                    var info = new FileInfo(zipPath);
                    var sizeMb = info.Length / (1024.0 * 1024.0);
                    AnsiConsole.MarkupLine($"[green]✓ Zipped to {Path.GetFileName(zipPath)} ({sizeMb:F1} MB)[/]");

                    // Clean up unzipped publish directory now that we have the archive
                    try { Directory.Delete(publishDir, true); } catch { /* best-effort */ }

                    return zipPath;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to create zip: {ex.Message}[/]");
                    return null;
                }
            });
    }

    static async Task<bool> EnsureBucketExists()
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Ensuring GCS bucket '{GcsBucketName}' exists...", async _ =>
            {
                // Check if bucket exists via `gsutil ls gs://bucket`
                var checkInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c gsutil ls gs://{GcsBucketName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var checkProcess = Process.Start(checkInfo);
                if (checkProcess == null)
                {
                    AnsiConsole.MarkupLine("[red]Failed to start gsutil. Ensure Google Cloud SDK is installed and on PATH.[/]");
                    return false;
                }
                await checkProcess.WaitForExitAsync();

                if (checkProcess.ExitCode == 0)
                {
                    AnsiConsole.MarkupLine($"[green]✓ Bucket '{GcsBucketName}' already exists[/]");
                    return true;
                }

                // Bucket doesn't exist — create it
                AnsiConsole.MarkupLine($"[yellow]Bucket '{GcsBucketName}' not found. Creating...[/]");

                var createInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c gsutil mb -p {GcpProjectId} gs://{GcsBucketName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var createProcess = Process.Start(createInfo);
                if (createProcess == null)
                {
                    AnsiConsole.MarkupLine("[red]Failed to start gsutil mb.[/]");
                    return false;
                }

                var output = await createProcess.StandardOutput.ReadToEndAsync();
                var error = await createProcess.StandardError.ReadToEndAsync();
                await createProcess.WaitForExitAsync();

                if (createProcess.ExitCode != 0)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to create bucket '{GcsBucketName}':[/]");
                    AnsiConsole.WriteLine(output);
                    AnsiConsole.WriteLine(error);
                    AnsiConsole.MarkupLine("[yellow]Run [bold]gcloud auth login[/] if you need to authenticate.[/]");
                    return false;
                }

                AnsiConsole.MarkupLine($"[green]✓ Bucket '{GcsBucketName}' created[/]");
                return true;
            });
    }

    static async Task<bool> UploadToGcs(string zipPath, string versionFolder, string runtime)
    {
        var destination = $"gs://{GcsBucketName}/{runtime}/{versionFolder}.zip";
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c gsutil cp \"{zipPath}\" \"{destination}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = Process.Start(startInfo);
        if (process == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to start gsutil. Ensure Google Cloud SDK is installed and on PATH.[/]");
            return false;
        }

        AnsiConsole.MarkupLine("[cyan]Uploading zip to GCS (gsutil progress below)...[/]");
        AnsiConsole.WriteLine();

        var outTask = StreamOutputToConsole(process.StandardOutput);
        var errTask = StreamOutputToConsole(process.StandardError);
        await process.WaitForExitAsync();
        await Task.WhenAll(outTask, errTask);

        if (process.ExitCode != 0)
        {
            AnsiConsole.MarkupLine("[red]gsutil upload failed.[/]");
            AnsiConsole.MarkupLine("[yellow]Run [bold]gcloud auth login[/] if you need to authenticate.[/]");
            return false;
        }

        AnsiConsole.MarkupLine("[green]✓ Uploaded to GCS[/]");

        try
        {
            File.Delete(zipPath);
            AnsiConsole.MarkupLine("[dim]  Cleaned up temporary zip file[/]");
        }
        catch
        {
            AnsiConsole.MarkupLine($"[yellow]  Warning: Could not clean up {zipPath}[/]");
        }

        return true;
    }

    static async Task StreamOutputToConsole(StreamReader reader)
    {
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory())) > 0)
            Console.Out.Write(buffer.AsSpan(0, count));
    }
}