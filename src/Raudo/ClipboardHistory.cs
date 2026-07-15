using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;

namespace Raudo
{
    internal enum ClipboardHistoryQueryStatus
    {
        Success,
        Disabled,
        AccessDenied,
        Unavailable,
        Failed
    }

    internal enum ClipboardHistorySessionPhase
    {
        Inactive,
        Pending,
        Loading,
        Results
    }

    internal sealed class ClipboardHistoryEntry
    {
        public ClipboardHistoryEntry(string text)
        {
            Text = text ?? string.Empty;
        }

        public string Text { get; private set; }

        public string Preview
        {
            get
            {
                const int maximumPreviewCharacters = 72;
                StringBuilder preview = new StringBuilder(maximumPreviewCharacters + 1);
                bool previousWasSpace = true;
                for (int index = 0; index < Text.Length; index++)
                {
                    char character = Text[index];
                    bool isSpace = char.IsWhiteSpace(character);
                    if (isSpace)
                    {
                        if (!previousWasSpace)
                        {
                            preview.Append(' ');
                        }
                    }
                    else
                    {
                        preview.Append(character);
                    }

                    previousWasSpace = isSpace;
                    if (preview.Length > maximumPreviewCharacters)
                    {
                        break;
                    }
                }

                string singleLine = preview.ToString().TrimEnd();
                return singleLine.Length <= maximumPreviewCharacters
                    ? singleLine
                    : singleLine.Substring(0, maximumPreviewCharacters - 1) + "…";
            }
        }
    }

    internal sealed class ClipboardHistoryQueryResult
    {
        public ClipboardHistoryQueryResult(
            ClipboardHistoryQueryStatus status,
            IList<ClipboardHistoryEntry> entries)
        {
            Status = status;
            Entries = entries ?? new List<ClipboardHistoryEntry>();
        }

        public ClipboardHistoryQueryStatus Status { get; private set; }
        public IList<ClipboardHistoryEntry> Entries { get; private set; }
    }

    internal interface IClipboardHistoryProvider
    {
        Task<ClipboardHistoryQueryResult> QueryAsync(
            string filter,
            int maximumItems,
            CancellationToken cancellationToken);
    }

    internal sealed class ClipboardHistoryProvider : IClipboardHistoryProvider
    {
        internal const int MaximumTextCharacters = 64 * 1024;
        private const string ClipboardRuntimeClass =
            "Windows.ApplicationModel.DataTransfer.Clipboard";

        public async Task<ClipboardHistoryQueryResult> QueryAsync(
            string filter,
            int maximumItems,
            CancellationToken cancellationToken)
        {
            int limit = Math.Max(1, Math.Min(5, maximumItems));
            if (!IsRuntimeAvailable())
            {
                return Result(ClipboardHistoryQueryStatus.Unavailable);
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ClipboardHistoryItemsResult history = await Clipboard
                    .GetHistoryItemsAsync()
                    .AsTask(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (history.Status == ClipboardHistoryItemsResultStatus.AccessDenied)
                {
                    return Result(ClipboardHistoryQueryStatus.AccessDenied);
                }
                if (history.Status == ClipboardHistoryItemsResultStatus.ClipboardHistoryDisabled)
                {
                    return Result(ClipboardHistoryQueryStatus.Disabled);
                }
                if (history.Status != ClipboardHistoryItemsResultStatus.Success)
                {
                    return Result(ClipboardHistoryQueryStatus.Failed);
                }

                string normalizedFilter = RaudoActionCatalog.Normalize(filter);
                HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
                List<ClipboardHistoryEntry> entries = new List<ClipboardHistoryEntry>(limit);
                for (int index = 0;
                    index < history.Items.Count && entries.Count < limit;
                    index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DataPackageView content = history.Items[index].Content;
                    if (content == null || !content.Contains(StandardDataFormats.Text))
                    {
                        continue;
                    }

                    string text;
                    try
                    {
                        text = await content.GetTextAsync().AsTask(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(text)
                        || text.Length > MaximumTextCharacters
                        || !unique.Add(text))
                    {
                        continue;
                    }
                    if (normalizedFilter.Length > 0
                        && RaudoActionCatalog.Normalize(text).IndexOf(
                            normalizedFilter,
                            StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    entries.Add(new ClipboardHistoryEntry(text));
                }

                return new ClipboardHistoryQueryResult(
                    ClipboardHistoryQueryStatus.Success,
                    entries);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return Result(ClipboardHistoryQueryStatus.Failed);
            }
        }

        internal static bool IsRuntimeAvailable()
        {
            try
            {
                return ApiInformation.IsMethodPresent(
                    ClipboardRuntimeClass,
                    "GetHistoryItemsAsync");
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static ClipboardHistoryQueryResult Result(
            ClipboardHistoryQueryStatus status)
        {
            return new ClipboardHistoryQueryResult(
                status,
                new List<ClipboardHistoryEntry>());
        }
    }

    internal static class ClipboardHistoryQuery
    {
        public static bool TryParse(string query, out string filter)
        {
            filter = string.Empty;
            string normalized = RaudoActionCatalog.Normalize(query);
            string command;
            if (normalized == "portapapeles" || normalized.StartsWith("portapapeles "))
            {
                command = "portapapeles";
            }
            else if (normalized == "clipboard" || normalized.StartsWith("clipboard "))
            {
                command = "clipboard";
            }
            else
            {
                return false;
            }

            filter = normalized.Length == command.Length
                ? string.Empty
                : normalized.Substring(command.Length).Trim();
            return true;
        }
    }
}
