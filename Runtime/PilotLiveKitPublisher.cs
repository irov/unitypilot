#if PILOT_LIVEKIT
using System;
using System.Collections;
using LiveKit;
using LiveKit.Proto;
using UnityEngine;
using RoomOptions = LiveKit.RoomOptions;
using TrackPublishOptions = LiveKit.Proto.TrackPublishOptions;
using VideoEncoding = LiveKit.Proto.VideoEncoding;

namespace Pilot.SDK
{
    internal sealed class PilotLiveKitPublisher
    {
        private Room m_room;
        private LocalVideoTrack m_screenTrack;
        private PilotScreenVideoSource m_screenSource;
        private LiveQuality m_currentQuality = LiveQuality.Default();
        private readonly object m_lock = new object();

        internal void Start(string serverUrl, string participantToken,
            string presetName, int maxDimension, int framesPerSecond,
            Action<Exception> onComplete)
        {
            lock (m_lock)
            {
                StopInternal();
                m_currentQuality = new LiveQuality(presetName, maxDimension, framesPerSecond);
            }

            var room = new Room();

            var connect = room.Connect(serverUrl, participantToken, new RoomOptions());

            PilotRunner.Instance.StartCoroutine(WaitConnect(connect, room, onComplete));
        }

        private IEnumerator WaitConnect(ConnectInstruction connect, Room room, Action<Exception> onComplete)
        {
            yield return connect;

            if (connect.IsError)
            {
                try { room.Disconnect(); } catch { }
                onComplete?.Invoke(new PilotException("Failed to connect to LiveKit"));
                yield break;
            }

            lock (m_lock)
            {
                m_room = room;
            }

            onComplete?.Invoke(null);
        }

        internal void EnableScreenShare(Action<Exception> onComplete)
        {
            Room activeRoom;
            LiveQuality quality;
            lock (m_lock)
            {
                activeRoom = m_room;
                quality = m_currentQuality;
            }

            if (activeRoom == null)
            {
                onComplete?.Invoke(new PilotException("Room not connected"));
                return;
            }

            PilotRunner.Instance.StartCoroutine(DoEnableScreenShare(activeRoom, quality, onComplete));
        }

        private IEnumerator DoEnableScreenShare(Room room, LiveQuality quality, Action<Exception> onComplete)
        {
            PilotScreenVideoSource source;
            try
            {
                source = new PilotScreenVideoSource(quality.MaxDimension);
            }
            catch (Exception e)
            {
                onComplete?.Invoke(new PilotException("Failed to create screen source: " + e.Message, e));
                yield break;
            }

            var track = LocalVideoTrack.CreateVideoTrack("pilot-app-screen", source, room);

            var options = new TrackPublishOptions();
            options.Source = TrackSource.SourceScreenshare;

            var encoding = new VideoEncoding();
            encoding.MaxBitrate = (ulong)ResolveMaxBitrate(quality);
            encoding.MaxFramerate = quality.FramesPerSecond;
            options.VideoEncoding = encoding;
            options.Simulcast = false;

            var publish = room.LocalParticipant.PublishTrack(track, options);
            yield return publish;

            if (publish.IsError)
            {
                onComplete?.Invoke(new PilotException("Failed to publish screen share"));
                yield break;
            }

            source.Start();
            PilotRunner.Instance.StartCoroutine(source.Update());

            lock (m_lock)
            {
                m_screenTrack = track;
                m_screenSource = source;
            }

            onComplete?.Invoke(null);
        }

        internal void UpdateQuality(string presetName, int maxDimension, int framesPerSecond,
            Action<bool, Exception> onComplete)
        {
            Room activeRoom;
            lock (m_lock)
            {
                activeRoom = m_room;
            }

            if (activeRoom == null)
            {
                onComplete?.Invoke(false, new PilotException("Room not connected"));
                return;
            }

            var nextQuality = new LiveQuality(presetName, maxDimension, framesPerSecond);

            lock (m_lock)
            {
                if (m_screenTrack == null)
                {
                    m_currentQuality = nextQuality;
                    onComplete?.Invoke(false, null);
                    return;
                }
            }

            PilotRunner.Instance.StartCoroutine(DoUpdateQuality(activeRoom, nextQuality, onComplete));
        }

        private IEnumerator DoUpdateQuality(Room room, LiveQuality nextQuality,
            Action<bool, Exception> onComplete)
        {
            LocalVideoTrack oldTrack;
            ScreenVideoSource oldSource;

            lock (m_lock)
            {
                oldTrack = m_screenTrack;
                oldSource = m_screenSource;
                m_screenTrack = null;
                m_screenSource = null;
            }

            if (oldTrack != null)
            {
                var unpublish = room.LocalParticipant.UnpublishTrack(oldTrack, true);
                yield return unpublish;
                oldSource?.Stop();
            }

            Exception publishError = null;
            bool screenShareActive = false;

            EnableScreenShareWithQuality(room, nextQuality, (error) =>
            {
                publishError = error;
                screenShareActive = error == null;
            });

            // Wait for the callback
            yield return null;

            if (publishError != null)
            {
                onComplete?.Invoke(false, publishError);
                yield break;
            }

            lock (m_lock)
            {
                m_currentQuality = nextQuality;
            }

            onComplete?.Invoke(screenShareActive, null);
        }

        private void EnableScreenShareWithQuality(Room room, LiveQuality quality, Action<Exception> onComplete)
        {
            lock (m_lock)
            {
                m_currentQuality = quality;
            }

            PilotRunner.Instance.StartCoroutine(DoEnableScreenShare(room, quality, onComplete));
        }

        internal void Stop()
        {
            lock (m_lock)
            {
                StopInternal();
            }
        }

        private void StopInternal()
        {
            var room = m_room;
            var source = m_screenSource;
            m_room = null;
            m_screenTrack = null;
            m_screenSource = null;
            m_currentQuality = LiveQuality.Default();

            if (source != null)
            {
                try { source.Stop(); } catch { }
            }

            if (room != null)
            {
                try { room.Disconnect(); } catch { }
            }
        }

        private static int ResolveMaxBitrate(LiveQuality quality)
        {
            switch (quality.PresetName.ToLowerInvariant())
            {
                case "low": return 300000;
                case "balanced": return 600000;
                case "high": return 1200000;
                default:
                    int computed = quality.MaxDimension * quality.FramesPerSecond * 278;
                    return Math.Max(180000, Math.Min(computed, 2500000));
            }
        }

        internal struct LiveQuality
        {
            internal readonly string PresetName;
            internal readonly int MaxDimension;
            internal readonly int FramesPerSecond;

            internal LiveQuality(string presetName, int maxDimension, int framesPerSecond)
            {
                PresetName = presetName;
                MaxDimension = maxDimension;
                FramesPerSecond = framesPerSecond;
            }

            internal static LiveQuality Default()
            {
                return new LiveQuality("low", 540, 2);
            }
        }
    }
}
#endif
