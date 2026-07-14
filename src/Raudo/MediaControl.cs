using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Raudo
{
    internal enum MediaCommand
    {
        TogglePlayPause,
        PreviousTrack,
        NextTrack,
        ToggleMute,
        VolumeDown,
        VolumeUp
    }

    internal sealed class MediaControlDefinition
    {
        public MediaControlDefinition(
            string id,
            MediaCommand command,
            string title,
            string description,
            string keywords,
            RaudoActionGlyph glyph)
        {
            Id = id;
            Command = command;
            Title = title;
            Description = description;
            Keywords = keywords;
            Glyph = glyph;
        }

        public string Id { get; private set; }
        public MediaCommand Command { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string Keywords { get; private set; }
        public RaudoActionGlyph Glyph { get; private set; }
    }

    internal static class MediaControlCatalog
    {
        private static readonly MediaControlDefinition[] Definitions =
        {
            new MediaControlDefinition(
                "media.play-pause",
                MediaCommand.TogglePlayPause,
                "Reproducir o pausar",
                "Control multimedia de Windows",
                "media multimedia musica audio video reproducir pausar pausa play pause youtube spotify",
                RaudoActionGlyph.MediaPlayPause),
            new MediaControlDefinition(
                "media.previous",
                MediaCommand.PreviousTrack,
                "Pista anterior",
                "Control multimedia de Windows",
                "media multimedia musica audio video pista anterior previous back youtube spotify",
                RaudoActionGlyph.MediaPrevious),
            new MediaControlDefinition(
                "media.next",
                MediaCommand.NextTrack,
                "Pista siguiente",
                "Control multimedia de Windows",
                "media multimedia musica audio video pista siguiente next skip youtube spotify",
                RaudoActionGlyph.MediaNext),
            new MediaControlDefinition(
                "media.mute",
                MediaCommand.ToggleMute,
                "Silenciar o restaurar audio",
                "Volumen de salida de Windows",
                "media multimedia audio sonido volumen silenciar restaurar mute unmute",
                RaudoActionGlyph.VolumeMute),
            new MediaControlDefinition(
                "media.volume-down",
                MediaCommand.VolumeDown,
                "Bajar volumen",
                "Volumen de salida de Windows",
                "media multimedia audio sonido volumen bajar disminuir down menos",
                RaudoActionGlyph.VolumeDown),
            new MediaControlDefinition(
                "media.volume-up",
                MediaCommand.VolumeUp,
                "Subir volumen",
                "Volumen de salida de Windows",
                "media multimedia audio sonido volumen subir aumentar up mas",
                RaudoActionGlyph.VolumeUp)
        };

        public static IList<MediaControlDefinition> GetDefinitions()
        {
            return Array.AsReadOnly(Definitions);
        }

        public static IList<RaudoAction> CreateActions(MediaControlService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException("service");
            }

            List<RaudoAction> actions = new List<RaudoAction>(Definitions.Length);
            foreach (MediaControlDefinition rawDefinition in Definitions)
            {
                MediaControlDefinition definition = rawDefinition;
                actions.Add(new RaudoAction(
                    definition.Id,
                    definition.Title,
                    definition.Description,
                    definition.Keywords,
                    "Controlar",
                    definition.Glyph,
                    RaudoActionKind.Media,
                    false,
                    6,
                    delegate { return service.TryExecute(definition.Command); }));
            }

            return actions;
        }
    }

    internal sealed class MediaControlService
    {
        private const uint InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;
        private const ushort VkVolumeMute = 0xAD;
        private const ushort VkVolumeDown = 0xAE;
        private const ushort VkVolumeUp = 0xAF;
        private const ushort VkMediaNextTrack = 0xB0;
        private const ushort VkMediaPreviousTrack = 0xB1;
        private const ushort VkMediaPlayPause = 0xB3;

        private readonly Func<NativeMethods.Input[], uint> inputSender;

        public MediaControlService()
            : this(SendInputs)
        {
        }

        internal MediaControlService(Func<NativeMethods.Input[], uint> sender)
        {
            if (sender == null)
            {
                throw new ArgumentNullException("sender");
            }

            inputSender = sender;
        }

        public string TryExecute(MediaCommand command)
        {
            NativeMethods.Input[] inputs = CreateInputs(command);
            uint sent = inputSender(inputs);
            if (sent == inputs.Length)
            {
                return null;
            }

            int win32Error = Marshal.GetLastWin32Error();
            return win32Error == 0
                ? "Windows no pudo enviar el control multimedia."
                : new Win32Exception(win32Error).Message;
        }

        internal static NativeMethods.Input[] CreateInputs(MediaCommand command)
        {
            ushort virtualKey = GetVirtualKey(command);
            NativeMethods.Input[] inputs = new NativeMethods.Input[2];
            inputs[0] = CreateKeyboardInput(virtualKey, false);
            inputs[1] = CreateKeyboardInput(virtualKey, true);
            return inputs;
        }

        internal static ushort GetVirtualKey(MediaCommand command)
        {
            switch (command)
            {
                case MediaCommand.TogglePlayPause:
                    return VkMediaPlayPause;
                case MediaCommand.PreviousTrack:
                    return VkMediaPreviousTrack;
                case MediaCommand.NextTrack:
                    return VkMediaNextTrack;
                case MediaCommand.ToggleMute:
                    return VkVolumeMute;
                case MediaCommand.VolumeDown:
                    return VkVolumeDown;
                case MediaCommand.VolumeUp:
                    return VkVolumeUp;
                default:
                    throw new ArgumentOutOfRangeException("command");
            }
        }

        private static uint SendInputs(NativeMethods.Input[] inputs)
        {
            return NativeMethods.SendInput(
                (uint)inputs.Length,
                inputs,
                Marshal.SizeOf(typeof(NativeMethods.Input)));
        }

        private static NativeMethods.Input CreateKeyboardInput(ushort virtualKey, bool keyUp)
        {
            NativeMethods.Input input = new NativeMethods.Input();
            input.Type = InputKeyboard;
            input.Union.Keyboard = new NativeMethods.KeyboardInput();
            input.Union.Keyboard.VirtualKey = virtualKey;
            input.Union.Keyboard.Flags = keyUp ? KeyEventKeyUp : 0;
            return input;
        }
    }
}
