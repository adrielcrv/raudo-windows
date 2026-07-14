using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace Raudo
{
    internal sealed class MediaSessionDescriptor
    {
        public MediaSessionDescriptor(
            string id,
            string displayName,
            string statusText,
            bool isCurrent,
            bool isSelected,
            bool canPlayPause,
            bool canPrevious,
            bool canNext)
        {
            Id = id;
            DisplayName = displayName;
            StatusText = statusText;
            IsCurrent = isCurrent;
            IsSelected = isSelected;
            CanPlayPause = canPlayPause;
            CanPrevious = canPrevious;
            CanNext = canNext;
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string StatusText { get; private set; }
        public bool IsCurrent { get; private set; }
        public bool IsSelected { get; private set; }
        public bool CanPlayPause { get; private set; }
        public bool CanPrevious { get; private set; }
        public bool CanNext { get; private set; }

        public string MenuLabel
        {
            get { return DisplayName + " · " + StatusText; }
        }
    }

    internal sealed class MediaSessionSnapshot
    {
        public MediaSessionSnapshot(
            bool isAvailable,
            string error,
            IList<MediaSessionDescriptor> sessions)
        {
            IsAvailable = isAvailable;
            Error = error ?? string.Empty;
            Sessions = sessions ?? new List<MediaSessionDescriptor>();
        }

        public bool IsAvailable { get; private set; }
        public string Error { get; private set; }
        public IList<MediaSessionDescriptor> Sessions { get; private set; }
    }

    internal interface IMediaSessionService : IDisposable
    {
        bool HasSelectedSession { get; }
        string SelectedDisplayName { get; }
        bool CanPrevious { get; }
        bool CanNext { get; }
        Task<MediaSessionSnapshot> GetSnapshotAsync();
        bool TrySelect(string id);
        void SelectAutomatic();
        Task<string> TryExecuteAsync(MediaCommand command);
    }

    internal sealed class MediaSessionService : IMediaSessionService
    {
        private readonly MediaControlService fallback;
        private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> sessionsById;
        private GlobalSystemMediaTransportControlsSessionManager manager;
        private GlobalSystemMediaTransportControlsSession selectedSession;
        private string selectedSessionId;
        private string selectedDisplayName;
        private bool selectedCanPrevious;
        private bool selectedCanNext;
        private bool selectedCanPlayPause;
        private bool initializationAttempted;
        private string initializationError;
        private bool disposed;

        public MediaSessionService(MediaControlService mediaControlService)
        {
            if (mediaControlService == null)
            {
                throw new ArgumentNullException("mediaControlService");
            }

            fallback = mediaControlService;
            sessionsById = new Dictionary<string, GlobalSystemMediaTransportControlsSession>(
                StringComparer.Ordinal);
            selectedSessionId = string.Empty;
            selectedDisplayName = string.Empty;
            initializationError = string.Empty;
        }

        public bool HasSelectedSession
        {
            get { return selectedSession != null; }
        }

        public string SelectedDisplayName
        {
            get { return selectedDisplayName; }
        }

        public bool CanPrevious
        {
            get { return selectedSession == null || selectedCanPrevious; }
        }

        public bool CanNext
        {
            get { return selectedSession == null || selectedCanNext; }
        }

        public async Task<MediaSessionSnapshot> GetSnapshotAsync()
        {
            if (disposed)
            {
                return Unavailable("El selector multimedia ya no está disponible.");
            }

            if (!await EnsureManagerAsync())
            {
                return Unavailable(initializationError);
            }

            try
            {
                IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions =
                    manager.GetSessions();
                GlobalSystemMediaTransportControlsSession current = manager.GetCurrentSession();
                sessionsById.Clear();
                List<MediaSessionDescriptor> descriptors =
                    new List<MediaSessionDescriptor>(sessions.Count);
                Dictionary<string, int> sourceTotals =
                    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> sourceOccurrences =
                    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                for (int index = 0; index < sessions.Count; index++)
                {
                    string source = sessions[index].SourceAppUserModelId ?? string.Empty;
                    int total;
                    sourceTotals.TryGetValue(source, out total);
                    sourceTotals[source] = total + 1;
                }

                for (int index = 0; index < sessions.Count; index++)
                {
                    GlobalSystemMediaTransportControlsSession session = sessions[index];
                    string source = session.SourceAppUserModelId ?? string.Empty;
                    int occurrence;
                    if (!sourceOccurrences.TryGetValue(source, out occurrence))
                    {
                        occurrence = 0;
                    }
                    sourceOccurrences[source] = occurrence + 1;

                    string id = source + "\u001f" + occurrence.ToString();
                    sessionsById[id] = session;
                    GlobalSystemMediaTransportControlsSessionPlaybackInfo playback =
                        session.GetPlaybackInfo();
                    GlobalSystemMediaTransportControlsSessionPlaybackControls controls =
                        playback == null ? null : playback.Controls;
                    bool canPlayPause = controls == null
                        || controls.IsPlayEnabled
                        || controls.IsPauseEnabled;
                    bool canPrevious = controls == null || controls.IsPreviousEnabled;
                    bool canNext = controls == null || controls.IsNextEnabled;
                    bool isSelected = string.Equals(
                        selectedSessionId,
                        id,
                        StringComparison.Ordinal);
                    bool isCurrent = IsSameSession(session, current);
                    int sourceTotal;
                    if (!isCurrent
                        && current != null
                        && sourceTotals.TryGetValue(source, out sourceTotal)
                        && sourceTotal == 1)
                    {
                        isCurrent = string.Equals(
                            source,
                            current.SourceAppUserModelId,
                            StringComparison.OrdinalIgnoreCase);
                    }
                    descriptors.Add(new MediaSessionDescriptor(
                        id,
                        GetFriendlySourceName(source),
                        GetPlaybackStatusText(playback),
                        isCurrent,
                        isSelected,
                        canPlayPause,
                        canPrevious,
                        canNext));

                    if (isSelected)
                    {
                        selectedSession = session;
                        selectedDisplayName = descriptors[descriptors.Count - 1].DisplayName;
                        selectedCanPlayPause = canPlayPause;
                        selectedCanPrevious = canPrevious;
                        selectedCanNext = canNext;
                    }
                }

                if (selectedSession != null && !sessionsById.ContainsKey(selectedSessionId))
                {
                    SelectAutomatic();
                }

                return new MediaSessionSnapshot(true, string.Empty, descriptors);
            }
            catch (Exception exception)
            {
                return Unavailable(
                    "Windows no pudo enumerar los reproductores: " + exception.Message);
            }
        }

        public bool TrySelect(string id)
        {
            GlobalSystemMediaTransportControlsSession session;
            if (disposed
                || string.IsNullOrWhiteSpace(id)
                || !sessionsById.TryGetValue(id, out session))
            {
                return false;
            }

            try
            {
                GlobalSystemMediaTransportControlsSessionPlaybackInfo playback =
                    session.GetPlaybackInfo();
                GlobalSystemMediaTransportControlsSessionPlaybackControls controls =
                    playback == null ? null : playback.Controls;
                bool canPlayPause = controls == null
                    || controls.IsPlayEnabled
                    || controls.IsPauseEnabled;
                if (!canPlayPause)
                {
                    return false;
                }

                selectedSession = session;
                selectedSessionId = id;
                selectedDisplayName = GetFriendlySourceName(session.SourceAppUserModelId);
                selectedCanPlayPause = true;
                selectedCanPrevious = controls == null || controls.IsPreviousEnabled;
                selectedCanNext = controls == null || controls.IsNextEnabled;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void SelectAutomatic()
        {
            selectedSession = null;
            selectedSessionId = string.Empty;
            selectedDisplayName = string.Empty;
            selectedCanPlayPause = false;
            selectedCanPrevious = false;
            selectedCanNext = false;
        }

        public async Task<string> TryExecuteAsync(MediaCommand command)
        {
            if (disposed)
            {
                return "El control multimedia ya no está disponible.";
            }

            if (selectedSession == null
                || command == MediaCommand.ToggleMute
                || command == MediaCommand.VolumeDown
                || command == MediaCommand.VolumeUp)
            {
                return fallback.TryExecute(command);
            }

            if (command == MediaCommand.TogglePlayPause && !selectedCanPlayPause)
            {
                return "El reproductor seleccionado no permite cambiar la reproducción.";
            }
            if (command == MediaCommand.PreviousTrack && !selectedCanPrevious)
            {
                return "El reproductor seleccionado no ofrece una pista anterior.";
            }
            if (command == MediaCommand.NextTrack && !selectedCanNext)
            {
                return "El reproductor seleccionado no ofrece una pista siguiente.";
            }

            try
            {
                bool succeeded;
                switch (command)
                {
                    case MediaCommand.TogglePlayPause:
                        succeeded = await selectedSession.TryTogglePlayPauseAsync().AsTask();
                        break;
                    case MediaCommand.PreviousTrack:
                        succeeded = await selectedSession.TrySkipPreviousAsync().AsTask();
                        break;
                    case MediaCommand.NextTrack:
                        succeeded = await selectedSession.TrySkipNextAsync().AsTask();
                        break;
                    default:
                        return "El comando multimedia no es válido.";
                }

                return succeeded
                    ? string.Empty
                    : "El reproductor seleccionado rechazó el comando.";
            }
            catch (Exception exception)
            {
                return "No se pudo controlar " + selectedDisplayName + ": " + exception.Message;
            }
        }

        public void Dispose()
        {
            disposed = true;
            manager = null;
            sessionsById.Clear();
            SelectAutomatic();
        }

        internal static string GetFriendlySourceName(string sourceAppUserModelId)
        {
            string source = (sourceAppUserModelId ?? string.Empty).Trim();
            if (source.Length == 0)
            {
                return "Reproductor multimedia";
            }

            string lower = source.ToLowerInvariant();
            if (lower.Contains("chrome"))
            {
                return "Google Chrome";
            }
            if (lower.Contains("msedge") || lower.Contains("microsoftedge"))
            {
                return "Microsoft Edge";
            }
            if (lower.Contains("spotify"))
            {
                return "Spotify";
            }
            if (lower.Contains("vlc"))
            {
                return "VLC";
            }
            if (lower.Contains("firefox"))
            {
                return "Firefox";
            }
            if (lower.Contains("zunemusic") || lower.Contains("mediaplayer"))
            {
                return "Media Player";
            }
            if (lower.Contains("itunes"))
            {
                return "iTunes";
            }

            int separator = source.LastIndexOf('!');
            if (separator >= 0 && separator < source.Length - 1)
            {
                source = source.Substring(separator + 1);
            }
            else
            {
                separator = source.IndexOf('_');
                if (separator > 0)
                {
                    source = source.Substring(0, separator);
                }
            }

            source = Path.GetFileNameWithoutExtension(source);
            separator = source.LastIndexOf('.');
            if (separator >= 0 && separator < source.Length - 1)
            {
                source = source.Substring(separator + 1);
            }

            return string.IsNullOrWhiteSpace(source)
                ? "Reproductor multimedia"
                : source.Replace('_', ' ').Trim();
        }

        private async Task<bool> EnsureManagerAsync()
        {
            if (manager != null)
            {
                return true;
            }
            if (initializationAttempted)
            {
                return false;
            }

            initializationAttempted = true;
            try
            {
                manager = await GlobalSystemMediaTransportControlsSessionManager
                    .RequestAsync()
                    .AsTask();
                return manager != null;
            }
            catch (Exception exception)
            {
                initializationError =
                    "La selección de reproductor no está disponible: " + exception.Message;
                return false;
            }
        }

        private static bool IsSameSession(
            GlobalSystemMediaTransportControlsSession left,
            GlobalSystemMediaTransportControlsSession right)
        {
            if (left == null || right == null)
            {
                return false;
            }
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left.Equals(right);
        }

        private static string GetPlaybackStatusText(
            GlobalSystemMediaTransportControlsSessionPlaybackInfo playback)
        {
            if (playback == null)
            {
                return "Disponible";
            }

            switch (playback.PlaybackStatus)
            {
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing:
                    return "Reproduciendo";
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused:
                    return "En pausa";
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped:
                    return "Detenido";
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed:
                    return "Cerrado";
                default:
                    return "Disponible";
            }
        }

        private static MediaSessionSnapshot Unavailable(string error)
        {
            return new MediaSessionSnapshot(
                false,
                string.IsNullOrWhiteSpace(error)
                    ? "La selección de reproductor no está disponible."
                    : error,
                new List<MediaSessionDescriptor>());
        }
    }
}
