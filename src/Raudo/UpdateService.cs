using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Raudo
{
    internal sealed class UpdatePackageInfo
    {
        public Version Version { get; set; }
        public Uri AssetApiUri { get; set; }
        public string Sha256 { get; set; }
        public long Size { get; set; }
    }

    internal sealed class UpdateCheckResult
    {
        public bool IsAvailable { get; set; }
        public bool CanInstall { get; set; }
        public Version LatestVersion { get; set; }
        public Uri ReleasePage { get; set; }
        public UpdatePackageInfo Package { get; set; }
        public string Message { get; set; }
    }

    internal sealed class UpdateInstallResult
    {
        public bool Started { get; set; }
        public string Message { get; set; }
    }

    internal static class UpdateService
    {
        private const string LatestReleaseApi =
            "https://api.github.com/repos/adrielcrv/raudo-windows/releases/latest";
        private const string ReleaseAssetApiPrefix =
            "/repos/adrielcrv/raudo-windows/releases/assets/";
        private const int MaximumResponseBytes = 65536;
        private const int MaximumPackageBytes = 16 * 1024 * 1024;
        private const int MaximumExecutableBytes = 8 * 1024 * 1024;
        private const int MaximumChecksumBytes = 4096;

        public static Task<UpdateCheckResult> CheckAsync()
        {
            return Task.Factory.StartNew<UpdateCheckResult>(
                delegate { return Check(); },
                System.Threading.CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);
        }

        public static Task<UpdateInstallResult> InstallAsync(UpdateCheckResult update)
        {
            return Task.Factory.StartNew<UpdateInstallResult>(
                delegate { return PrepareAndLaunch(update); },
                System.Threading.CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);
        }

        internal static UpdateCheckResult ParseRelease(string json, Version currentVersion)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> release =
                serializer.Deserialize<Dictionary<string, object>>(json);
            if (release == null)
            {
                throw new InvalidDataException("GitHub devolvió una publicación vacía.");
            }

            string tag = GetString(release, "tag_name");
            string page = GetString(release, "html_url");
            Version latestVersion;
            Uri releasePage;
            if (!TryParseVersion(tag, out latestVersion)
                || !Uri.TryCreate(page, UriKind.Absolute, out releasePage)
                || !IsValidReleasePage(releasePage))
            {
                throw new InvalidDataException("GitHub devolvió metadatos de versión no válidos.");
            }

            bool available = latestVersion.CompareTo(currentVersion) > 0;
            UpdatePackageInfo package = available
                ? FindPackage(release, latestVersion)
                : null;
            bool canInstall = package != null && IsInstalledLocation(CurrentExecutablePath());
            return new UpdateCheckResult
            {
                IsAvailable = available,
                CanInstall = canInstall,
                LatestVersion = latestVersion,
                ReleasePage = releasePage,
                Package = package,
                Message = available
                    ? "Raudo " + latestVersion.ToString(3) + " está disponible."
                    : "Estás usando la versión más reciente."
            };
        }

        internal static bool IsInstalledLocation(string executablePath)
        {
            return InstallationService.IsInstalledLocation(executablePath);
        }

        internal static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(stream);
                StringBuilder value = new StringBuilder(digest.Length * 2);
                foreach (byte item in digest)
                {
                    value.Append(item.ToString("x2"));
                }

                return value.ToString();
            }
        }

        private static UpdateCheckResult Check()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                HttpWebRequest request = CreateGitHubRequest(new Uri(LatestReleaseApi));
                request.Accept = "application/vnd.github+json";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                {
                    string json = ReadLimitedText(stream, MaximumResponseBytes);
                    return ParseRelease(json, GetCurrentVersion());
                }
            }
            catch (WebException exception)
            {
                HttpWebResponse response = exception.Response as HttpWebResponse;
                bool notFound = response != null
                    && response.StatusCode == HttpStatusCode.NotFound;
                if (response != null)
                {
                    response.Dispose();
                }

                if (notFound)
                {
                    return new UpdateCheckResult
                    {
                        Message = "Todavía no hay una versión pública disponible."
                    };
                }

                return new UpdateCheckResult
                {
                    Message = "No se pudo consultar GitHub. Verifica tu conexión e inténtalo de nuevo."
                };
            }
            catch (Exception)
            {
                return new UpdateCheckResult
                {
                    Message = "No se pudo comprobar la versión disponible."
                };
            }
        }

        private static UpdateInstallResult PrepareAndLaunch(UpdateCheckResult update)
        {
            string updateDirectory = null;
            try
            {
                if (update == null
                    || !update.IsAvailable
                    || !update.CanInstall
                    || update.Package == null)
                {
                    return new UpdateInstallResult
                    {
                        Message = "Esta instalación no admite la actualización directa."
                    };
                }

                string target = CurrentExecutablePath();
                if (!IsInstalledLocation(target))
                {
                    return new UpdateInstallResult
                    {
                        Message = "La actualización directa sólo está disponible para Raudo instalado."
                    };
                }

                updateDirectory = Path.Combine(
                    UpdateInstaller.UpdateRootDirectory,
                    "v" + update.LatestVersion.ToString(3) + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(updateDirectory);
                string packagePath = Path.Combine(updateDirectory, "package.zip");
                string stagedExecutable = Path.Combine(updateDirectory, "Raudo.exe");

                DownloadPackage(update.Package, packagePath);
                string packageHash = ComputeSha256(packagePath);
                if (!string.Equals(
                    packageHash,
                    update.Package.Sha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("La suma SHA-256 del paquete no coincide.");
                }

                ExtractAndValidateExecutable(
                    packagePath,
                    stagedExecutable,
                    update.LatestVersion);
                File.Delete(packagePath);

                ProcessStartInfo updater = new ProcessStartInfo();
                updater.FileName = stagedExecutable;
                updater.WorkingDirectory = updateDirectory;
                updater.UseShellExecute = false;
                updater.CreateNoWindow = true;
                updater.Arguments = string.Join(
                    " ",
                    "--apply-update",
                    "--target=" + QuoteArgument(target),
                    "--wait-pid=" + Process.GetCurrentProcess().Id,
                    "--expected-version=" + update.LatestVersion.ToString(3),
                    "--update-dir=" + QuoteArgument(updateDirectory));
                Process.Start(updater);

                return new UpdateInstallResult
                {
                    Started = true,
                    Message = "La actualización está lista para instalarse."
                };
            }
            catch (WebException)
            {
                DeleteDirectory(updateDirectory);
                return new UpdateInstallResult
                {
                    Message = "No se pudo descargar la actualización desde GitHub."
                };
            }
            catch (UnauthorizedAccessException)
            {
                DeleteDirectory(updateDirectory);
                return new UpdateInstallResult
                {
                    Message = "Windows no permitió preparar la actualización."
                };
            }
            catch (IOException)
            {
                DeleteDirectory(updateDirectory);
                return new UpdateInstallResult
                {
                    Message = "No se pudo preparar el archivo de actualización."
                };
            }
            catch (Exception)
            {
                DeleteDirectory(updateDirectory);
                return new UpdateInstallResult
                {
                    Message = "La actualización no superó las validaciones de seguridad."
                };
            }
        }

        private static UpdatePackageInfo FindPackage(
            Dictionary<string, object> release,
            Version version)
        {
            object rawAssets;
            if (!release.TryGetValue("assets", out rawAssets))
            {
                return null;
            }

            string expectedName = "Raudo-v" + version.ToString(3) + "-win.zip";
            foreach (object rawAsset in Enumerate(rawAssets))
            {
                Dictionary<string, object> asset = rawAsset as Dictionary<string, object>;
                if (asset == null
                    || !string.Equals(
                        GetString(asset, "name"),
                        expectedName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                Uri apiUri;
                long size = GetLong(asset, "size");
                string digest = NormalizeDigest(GetString(asset, "digest"));
                if (!Uri.TryCreate(GetString(asset, "url"), UriKind.Absolute, out apiUri)
                    || !IsValidAssetApiUri(apiUri)
                    || size <= 0
                    || size > MaximumPackageBytes
                    || digest == null)
                {
                    return null;
                }

                return new UpdatePackageInfo
                {
                    Version = version,
                    AssetApiUri = apiUri,
                    Sha256 = digest,
                    Size = size
                };
            }

            return null;
        }

        private static IEnumerable Enumerate(object values)
        {
            IEnumerable enumerable = values as IEnumerable;
            return enumerable ?? new object[0];
        }

        private static void DownloadPackage(UpdatePackageInfo package, string destination)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpWebRequest request = CreateGitHubRequest(package.AssetApiUri);
            request.Accept = "application/octet-stream";
            request.AllowAutoRedirect = true;
            request.MaximumAutomaticRedirections = 5;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (!IsValidDownloadResponse(response.ResponseUri)
                    || response.ContentLength > MaximumPackageBytes)
                {
                    throw new InvalidDataException("GitHub devolvió una descarga no válida.");
                }

                using (Stream input = response.GetResponseStream())
                using (FileStream output = new FileStream(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    long copied = CopyLimited(input, output, MaximumPackageBytes);
                    if (copied != package.Size)
                    {
                        throw new InvalidDataException("El tamaño del paquete no coincide.");
                    }
                }
            }
        }

        internal static void ExtractAndValidateExecutable(
            string packagePath,
            string destination,
            Version expectedVersion)
        {
            using (FileStream package = File.OpenRead(packagePath))
            using (ZipArchive archive = new ZipArchive(package, ZipArchiveMode.Read, false))
            {
                ZipArchiveEntry executableEntry = null;
                ZipArchiveEntry checksumEntry = null;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.Equals(entry.FullName, "Raudo.exe", StringComparison.Ordinal))
                    {
                        executableEntry = entry;
                    }
                    else if (string.Equals(
                        entry.FullName,
                        "SHA256SUMS.txt",
                        StringComparison.Ordinal))
                    {
                        checksumEntry = entry;
                    }
                }

                if (executableEntry == null
                    || checksumEntry == null
                    || executableEntry.Length <= 0
                    || executableEntry.Length > MaximumExecutableBytes)
                {
                    throw new InvalidDataException("El paquete no contiene los archivos esperados.");
                }

                string expectedExecutableHash;
                using (Stream checksumStream = checksumEntry.Open())
                {
                    string checksumText = ReadLimitedText(
                        checksumStream,
                        MaximumChecksumBytes);
                    expectedExecutableHash = ParseExecutableChecksum(checksumText);
                }

                using (Stream input = executableEntry.Open())
                using (FileStream output = new FileStream(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    long copied = CopyLimited(input, output, MaximumExecutableBytes);
                    if (copied != executableEntry.Length)
                    {
                        throw new InvalidDataException("El ejecutable extraído está incompleto.");
                    }
                }

                string actualExecutableHash = ComputeSha256(destination);
                if (!string.Equals(
                    expectedExecutableHash,
                    actualExecutableHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("La suma del ejecutable no coincide.");
                }

                Version executableVersion = AssemblyName.GetAssemblyName(destination).Version;
                if (executableVersion == null
                    || executableVersion.Major != expectedVersion.Major
                    || executableVersion.Minor != expectedVersion.Minor
                    || executableVersion.Build != expectedVersion.Build)
                {
                    throw new InvalidDataException("La versión del ejecutable no coincide.");
                }
            }
        }

        private static string ParseExecutableChecksum(string contents)
        {
            string[] lines = contents.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (!line.EndsWith("Raudo.exe", StringComparison.Ordinal))
                {
                    continue;
                }

                string hash = line.Substring(0, line.Length - "Raudo.exe".Length).Trim();
                if (IsSha256(hash))
                {
                    return hash.ToLowerInvariant();
                }
            }

            throw new InvalidDataException("El paquete no contiene la suma del ejecutable.");
        }

        private static HttpWebRequest CreateGitHubRequest(Uri uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.UserAgent = "Raudo/" + GetCurrentVersion().ToString(3);
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            return request;
        }

        private static long CopyLimited(Stream input, Stream output, int maximumBytes)
        {
            byte[] chunk = new byte[8192];
            long total = 0;
            int read;
            while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
            {
                total += read;
                if (total > maximumBytes)
                {
                    throw new InvalidDataException("La descarga supera el tamaño permitido.");
                }

                output.Write(chunk, 0, read);
            }

            return total;
        }

        private static string ReadLimitedText(Stream stream, int maximumBytes)
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                CopyLimited(stream, buffer, maximumBytes);
                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            object value;
            return values.TryGetValue(key, out value) ? value as string : null;
        }

        private static long GetLong(Dictionary<string, object> values, string key)
        {
            object value;
            if (!values.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt64(value);
            }
            catch (FormatException)
            {
                return 0;
            }
            catch (InvalidCastException)
            {
                return 0;
            }
            catch (OverflowException)
            {
                return 0;
            }
        }

        private static bool TryParseVersion(string tag, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            string normalized = tag.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(1);
            }

            Version parsed;
            if (!Version.TryParse(normalized, out parsed) || parsed.Major < 1)
            {
                return false;
            }

            version = parsed;
            return true;
        }

        private static string NormalizeDigest(string digest)
        {
            if (string.IsNullOrWhiteSpace(digest)
                || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string value = digest.Substring("sha256:".Length);
            return IsSha256(value) ? value.ToLowerInvariant() : null;
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
            {
                return false;
            }

            foreach (char character in value)
            {
                bool hexadecimal = (character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F');
                if (!hexadecimal)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidReleasePage(Uri uri)
        {
            return IsHttpsHost(uri, "github.com")
                && uri.AbsolutePath.StartsWith(
                    "/adrielcrv/raudo-windows/releases/",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidAssetApiUri(Uri uri)
        {
            return IsHttpsHost(uri, "api.github.com")
                && uri.AbsolutePath.StartsWith(
                    ReleaseAssetApiPrefix,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidDownloadResponse(Uri uri)
        {
            return IsHttpsHost(uri, "api.github.com")
                || IsHttpsHost(uri, "github.com")
                || IsHttpsHost(uri, "release-assets.githubusercontent.com");
        }

        private static bool IsHttpsHost(Uri uri, string host)
        {
            return uri != null
                && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase);
        }

        private static string QuoteArgument(string value)
        {
            if (value == null || value.IndexOf('"') >= 0)
            {
                throw new ArgumentException("El argumento contiene caracteres no válidos.");
            }

            return "\"" + value + "\"";
        }

        private static string CurrentExecutablePath()
        {
            return Assembly.GetExecutingAssembly().Location;
        }

        private static Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void DeleteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
