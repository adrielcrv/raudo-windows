using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Raudo
{
    internal sealed class UpdateCheckResult
    {
        public bool IsAvailable { get; set; }
        public Version LatestVersion { get; set; }
        public Uri ReleasePage { get; set; }
        public string Message { get; set; }
    }

    internal static class UpdateService
    {
        private const string LatestReleaseApi =
            "https://api.github.com/repos/adrielcrv/raudo-windows/releases/latest";
        private const int MaximumResponseBytes = 65536;

        public static Task<UpdateCheckResult> CheckAsync()
        {
            return Task.Factory.StartNew<UpdateCheckResult>(
                delegate { return Check(); },
                System.Threading.CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);
        }

        private static UpdateCheckResult Check()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(LatestReleaseApi);
                request.Method = "GET";
                request.UserAgent = "Raudo/" + GetCurrentVersion().ToString(3);
                request.Accept = "application/vnd.github+json";
                request.Timeout = 6000;
                request.ReadWriteTimeout = 6000;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                {
                    string json = ReadLimited(stream, MaximumResponseBytes);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    Dictionary<string, object> release =
                        serializer.Deserialize<Dictionary<string, object>>(json);

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

                    bool available = latestVersion.CompareTo(GetCurrentVersion()) > 0;
                    return new UpdateCheckResult
                    {
                        IsAvailable = available,
                        LatestVersion = latestVersion,
                        ReleasePage = releasePage,
                        Message = available
                            ? "Raudo " + latestVersion.ToString(3) + " está disponible."
                            : "Estás usando la versión más reciente."
                    };
                }
            }
            catch (WebException exception)
            {
                HttpWebResponse response = exception.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    response.Dispose();
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

        private static string ReadLimited(Stream stream, int maximumBytes)
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                byte[] chunk = new byte[4096];
                int read;
                while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (buffer.Length + read > maximumBytes)
                    {
                        throw new InvalidDataException("La respuesta de actualización es demasiado grande.");
                    }

                    buffer.Write(chunk, 0, read);
                }

                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            object value;
            return values.TryGetValue(key, out value) ? value as string : null;
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

        private static bool IsValidReleasePage(Uri uri)
        {
            return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.StartsWith(
                    "/adrielcrv/raudo-windows/releases/",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }
    }
}
