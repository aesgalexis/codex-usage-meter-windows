using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using CodexUsageMeter.Infrastructure;
using CodexUsageMeter.App;

internal static class AppUpdateChecks
{
    public static async Task RunAsync()
    {
        CheckInstallerArguments();
        var payload = Encoding.UTF8.GetBytes("synthetic installer content");
        var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var metadata = $$"""
            {"tag_name":"v0.7.0","draft":false,"prerelease":false,"assets":[{
              "name":"CodexUsageMeter-Setup-win-x64.exe",
              "browser_download_url":"https://github.com/aesgalexis/codex-usage-meter-windows/releases/download/v0.7.0/CodexUsageMeter-Setup-win-x64.exe",
              "digest":"sha256:{{hash}}"}]}
            """;
        using var handler = new UpdateHandler(metadata, payload);
        using var client = new HttpClient(handler);
        var service = new AppUpdateService(client);
        var release = await service.CheckAsync(new Version(0, 6, 6, 0));
        Require(release?.Version == new Version(0, 7, 0), "Debe encontrar la versión estable más reciente.");
        Require(handler.SawUserAgent, "GitHub requiere una cabecera User-Agent.");
        Require(await service.CheckAsync(new Version(0, 7, 0, 0)) is null, "La misma versión con revisión .0 no debe actualizarse.");
        Require(await service.CheckAsync(new Version(0, 8, 0)) is null, "No debe ofrecer una versión anterior.");

        foreach (var flag in new[] { "draft", "prerelease" })
        {
            handler.Metadata = metadata.Replace($"\"{flag}\":false", $"\"{flag}\":true");
            Require(await service.CheckAsync(new Version(0, 6, 6)) is null, "Debe ignorar borradores y versiones preliminares.");
        }
        foreach (var invalidMetadata in new[]
        {
            "null", "[]", "{broken", metadata.Replace("v0.7.0", "v0.7.0-beta.1"),
            metadata.Replace("v0.7.0", "vv0.7.0"), metadata.Replace("v0.7.0", "v0.7"),
            metadata.Replace("CodexUsageMeter-Setup-win-x64.exe", "other.exe"),
            metadata.Replace("https://github.com/", "https://example.org/"),
            metadata.Replace("sha256:", "sha512:"), metadata.Replace(hash, new string('x', 64)),
            metadata.Replace($"\"sha256:{hash}\"", "null")
        })
        {
            handler.Metadata = invalidMetadata;
            await RejectAsync(() => service.CheckAsync(new Version(0, 6, 6)), "Debe rechazar metadatos incompletos o inseguros.");
        }
        handler.Metadata = metadata;
        handler.Status = HttpStatusCode.Forbidden;
        await RejectAsync(() => service.CheckAsync(new Version(0, 6, 6)), "Un límite de GitHub debe comunicarse como error.");
        handler.Status = HttpStatusCode.NotFound;
        Require(await service.CheckAsync(new Version(0, 6, 6)) is null, "Sin versiones publicadas no debe haber actualización.");
        handler.Status = HttpStatusCode.OK;

        var directory = Path.Combine(Path.GetTempPath(), "CodexUsageMeter-update-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "installer.exe");
        try
        {
            await service.DownloadInstallerAsync(release!, destination);
            Require((await File.ReadAllBytesAsync(destination)).SequenceEqual(payload), "Debe guardar íntegra la descarga verificada.");
            await RejectAsync(() => service.DownloadInstallerAsync(release!, destination), "No debe sobrescribir un fichero existente.");
            Require((await File.ReadAllBytesAsync(destination)).SequenceEqual(payload), "Un fichero preexistente debe conservarse.");
            File.Delete(destination);

            await RejectAsync(() => service.DownloadInstallerAsync(release! with { Sha256 = new string('0', 64) }, destination),
                "Un checksum incorrecto debe impedir la instalación.");
            Require(!File.Exists(destination), "La descarga que falla debe eliminarse.");
            handler.Status = HttpStatusCode.ServiceUnavailable;
            await RejectAsync(() => service.DownloadInstallerAsync(release!, destination), "Un error HTTP de descarga debe propagarse.");
            Require(!File.Exists(destination), "Un error HTTP no debe dejar un instalador vacío.");
            handler.Status = HttpStatusCode.OK;

            await RejectAsync(() => service.DownloadInstallerAsync(release! with { InstallerUrl = new Uri("https://example.org/installer.exe") }, destination),
                "Una URL ajena al proyecto debe rechazarse también al descargar.");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await RejectAsync(() => service.DownloadInstallerAsync(release!, destination, cancellation.Token), "Debe permitir cancelar al salir.");
            Require(!File.Exists(destination), "Cancelar no debe dejar un instalador parcial.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void CheckInstallerArguments()
    {
        const string directory = @"C:\Apps with spaces\Codex Usage Meter\";
        var installed = AppUpdateInstaller.CreateStartInfo(@"C:\Updates\setup.exe", directory, true, AppText.English);
        Require(installed.UseShellExecute && installed.FileName == @"C:\Updates\setup.exe", "Debe iniciar el instalador verificado.");
        foreach (var argument in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/SP-", "/NORESTART",
                     "/CLOSEAPPLICATIONS", "/FORCECLOSEAPPLICATIONS", "/NORESTARTAPPLICATIONS", "/AUTOMATICUPDATE=1" })
            Require(installed.ArgumentList.Contains(argument), "La actualización debe instalar y cerrar la app sin preguntas ni reiniciar Windows.");
        Require(installed.ArgumentList.Contains(@"/DIR=C:\Apps with spaces\Codex Usage Meter"), "Debe conservar la carpeta instalada, incluidos sus espacios.");
        Require(installed.ArgumentList.Contains(@"/RESTARTAPP=C:\Apps with spaces\Codex Usage Meter\CodexUsageMeter.exe"), "Debe poder recuperar la copia original si falla.");
        Require(installed.ArgumentList.Contains("/LANG=english"), "Debe conservar el idioma inglés.");
        var portable = AppUpdateInstaller.CreateStartInfo(@"C:\Updates\setup.exe", directory, false, AppText.Spanish);
        Require(!portable.ArgumentList.Any(argument => argument.StartsWith("/DIR=", StringComparison.Ordinal)), "La copia portable debe usar la carpeta de instalación habitual.");
        Require(portable.ArgumentList.Contains("/LANG=spanish") && portable.ArgumentList.Contains("/AUTOMATICUPDATE=1"), "La copia portable también debe actualizarse en silencio y reabrirse.");
    }

    private static async Task RejectAsync(Func<Task> action, string message)
    {
        try { await action(); }
        catch (Exception exception) when (exception is IOException or InvalidDataException or HttpRequestException or
            System.Text.Json.JsonException or OperationCanceledException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class UpdateHandler(string metadata, byte[] payload) : HttpMessageHandler
    {
        public string Metadata { get; set; } = metadata;
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public bool SawUserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isMetadata = request.RequestUri!.Host == "api.github.com";
            if (isMetadata) SawUserAgent = request.Headers.UserAgent.Any();
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = isMetadata ? new StringContent(Metadata) : new ByteArrayContent(payload)
            });
        }
    }
}
