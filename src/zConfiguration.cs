using System;
using Newtonsoft.Json;
using PepperDash.Core;
using Serilog.Events;

namespace PepperDash.Essentials.Plugins
{
    /// <summary>
    /// zConfiguration class structure
    /// </summary>
    public class zConfiguration
    {
        public class Sharing
        {
            [JsonProperty("optimize_video_sharing")]
            public bool OptimizeVideoSharing { get; set; }
        }

        public class Camera : NotifiableObject
        {
            private bool _mute;

            public bool Mute
            {
                get { return _mute; }
                set
                {
                    Debug.LogMessage(LogEventLevel.Information, "Camera Mute response received: {Value}", value);

                    if (value == _mute) return;

                    _mute = value;
                    NotifyPropertyChanged("Mute");
                }
            }
        }

        public class Microphone : NotifiableObject
        {
            private bool _mute;

            public bool Mute
            {
                get
                {
                    return _mute;
                }
                set
                {
                    if (value != _mute)
                    {
                        _mute = value;
                        NotifyPropertyChanged("Mute");
                    }
                }
            }
        }

        // Values must be distinct bit positions -- these are combined with | and tested with HasFlag().
        // (Previous sequential values 1,2,3,4,5,6 collided under bitwise OR, e.g. Thumbnail(3) == Gallery(1)|Speaker(2),
        // causing layouts to spuriously report as available.)
        [Flags]
        public enum eLayoutStyle
        {
            None = 0,
            Gallery = 1 << 0,
            Speaker = 1 << 1,
            Thumbnail = 1 << 2,
            ContentOnly = 1 << 3,
            CancelContentOnly = 1 << 4,
            Dynamic = 1 << 5,
            /// <summary>Zoom Room controller UI "Multi-Speaker" layout (SDK ScreenLayoutSourceType.Spotlight).</summary>
            MultiSpeaker = 1 << 6,
            /// <summary>Zoom Room controller UI "Thumbnail &amp; Share" layout, shown while content is shared (SDK ScreenLayoutSourceType.ThumbnailShareView).</summary>
            ThumbnailAndShare = 1 << 7,
        }

        public enum eLayoutSize
        {
            Off,
            Size1,
            Size2,
            Size3,
            Strip
        }

        public enum eLayoutPosition
        {
            Center,
            Up,
            Right,
            UpRight,
            Down,
            DownRight,
            Left,
            UpLeft,
            DownLeft
        }

        public class Layout : NotifiableObject
        {
            private bool _shareThumb;
            private eLayoutStyle _style;
            private eLayoutSize _size;
            private eLayoutPosition _position;

            public bool ShareThumb
            {
                get { return _shareThumb; }
                set
                {
                    if (value != _shareThumb)
                    {
                        _shareThumb = value;
                        NotifyPropertyChanged("ShareThumb");
                    }
                }
            }

            public eLayoutStyle Style
            {
                get { return _style; }
                set
                {
                    if (value != _style)
                    {
                        _style = value;
                        NotifyPropertyChanged("Style");
                    }
                }
            }

            public eLayoutSize Size
            {
                get { return _size; }
                set
                {
                    if (value != _size)
                    {
                        _size = value;
                        NotifyPropertyChanged("Size");
                    }
                }
            }

            public eLayoutPosition Position
            {
                get { return _position; }
                set
                {
                    if (value != _position)
                    {
                        _position = value;
                        NotifyPropertyChanged("Position");
                    }
                }
            }
        }

        public class Lock : NotifiableObject
        {
            private bool _enable;

            public bool Enable 
            {
                get
                {
                    return _enable;
                }
                set
                {
                    if (value != _enable)
                    {
                        _enable = value;
                        NotifyPropertyChanged("Enable");
                    }
                }
            }
        }

        public class ClosedCaption
        {
            public bool Visible { get; set; }
            public int FontSize { get; set; }
        }

        public class MuteUserOnEntry
        {
            public bool Enable { get; set; }
        }

        public class Call
        {
            public Sharing Sharing { get; set; }
            public Camera Camera { get; set; }
            public Microphone Microphone { get; set; }
            public Layout Layout { get; set; }
            public Lock Lock { get; set; }
            public MuteUserOnEntry MuteUserOnEntry { get; set; }
            public ClosedCaption ClosedCaption { get; set; }


            public Call()
            {
                Sharing = new Sharing();
                Camera = new Camera();
                Microphone = new Microphone();
                Layout = new Layout();
                Lock = new Lock();
                MuteUserOnEntry = new MuteUserOnEntry();
                ClosedCaption = new ClosedCaption();
            }
        }

        public class Audio
        {
            public Input Input { get; set; }
            public Output Output { get; set; }

            public Audio()
            {
                Input = new Input();
                Output = new Output();
            }
        }

        public class Input : Output
        {
            [JsonProperty("reduce_reverb")]
            public bool ReduceReverb { get; set; }
        }

        public class Output : NotifiableObject
        {
            private int _volume;

            [JsonProperty("volume")]
            public int Volume
            {
                get
                {
                    return _volume;
                }
                set
                {
                    if (value != _volume)
                    {
                        _volume = value;
                        NotifyPropertyChanged("Volume");
                    }
                }
            }
            [JsonProperty("selectedId")]
            public string SelectedId { get; set; }
            [JsonProperty("is_sap_disabled")]
            public bool IsSapDisabled { get; set; }
        }

        public class Video : NotifiableObject
        {
            private bool _hideConfSelfVideo;

            [JsonProperty("hide_conf_self_video")]
            public bool HideConfSelfVideo
            {
                get
                {
                    return _hideConfSelfVideo;
                }
                set
                {
                    //if (value != _hideConfSelfVideo)
                    //{
                    _hideConfSelfVideo = value;
                    NotifyPropertyChanged("HideConfSelfVideo");
                    //}
                }
            }

            public VideoCamera Camera { get; set; }

            public Video()
            {
                Camera = new VideoCamera();
            }
        }

        public class VideoCamera : NotifiableObject
        {
            private string _selectedId;

            [JsonProperty("selectedId")]
            public string SelectedId
            {
                get
                {
                    return _selectedId;
                }
                set
                {
                    if (value != _selectedId)
                    {
                        _selectedId = value;
                        NotifyPropertyChanged("SelectedId");
                    }
                }

            }
            public bool Mirror { get; set; }
        }

        public class Client
        {
            public string appVersion { get; set; }
            public string deviceSystem { get; set; }

            // This doesn't belong here, but there's a bug in the object structure of Zoom Room 5.6.3 that puts it here
            public zConfiguration.Call Call { get; set; }

            public Client()
            {
                Call = new zConfiguration.Call();
            }
        }

    }
}