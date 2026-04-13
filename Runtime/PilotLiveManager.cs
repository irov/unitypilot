using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Pilot.SDK
{
    internal sealed class PilotLiveManager
    {
        internal delegate void LiveModeChangedHandler(bool enabled, long actionPollIntervalMs);
        internal LiveModeChangedHandler OnLiveModeChanged;

        private readonly PilotHttpClient m_httpClient;
#if PILOT_LIVEKIT
        private readonly PilotLiveKitPublisher m_publisher = new PilotLiveKitPublisher();
#endif
        private readonly object m_lock = new object();

        private volatile bool m_isLive;
        private LiveSettings m_settings = LiveSettings.Low();
        private PilotLiveOverlayView m_overlayView;

        internal PilotLiveManager(PilotHttpClient httpClient)
        {
            m_httpClient = httpClient;
        }

        // ── Public action handlers ──

        internal Dictionary<string, object> Start(string sessionToken, Dictionary<string, object> payload)
        {
            bool wasLive;
            lock (m_lock)
            {
                wasLive = m_isLive;
            }

            StopLiveRuntime();
            if (wasLive)
            {
                OnLiveModeChanged?.Invoke(false, 0);
            }

            try
            {
                var requested = LiveSettings.FromPayload(payload);
                var session = FetchPublisherSession(sessionToken, requested);
                var settings = session.Settings;

                var startError = StartPublisherSync(session.ServerUrl, session.ParticipantToken,
                    settings.PresetName, settings.MaxDimension, settings.FramesPerSecond);

                if (startError != null)
                    throw startError;

                lock (m_lock)
                {
                    m_isLive = true;
                    m_settings = settings;
                }

                // Enable screen share
                var shareError = EnableScreenShareSync();
                if (shareError != null)
                {
                    PilotLog.Warn("Screen share enable failed: {0}", shareError.Message);
                }

                OnLiveModeChanged?.Invoke(true, settings.ActionPollIntervalMs);

                var metadata = new Dictionary<string, object>
                {
                    ["preset"] = settings.PresetName,
                    ["max_dimension"] = settings.MaxDimension,
                    ["fps"] = settings.FramesPerSecond
                };
                if (session.RoomName != null) metadata["room_name"] = session.RoomName;
                if (session.ParticipantIdentity != null) metadata["participant_identity"] = session.ParticipantIdentity;
                metadata["video_track_name"] = session.VideoTrackName;
                PilotSDK.Event("live_started", "live", metadata);

                var ack = BuildAck(true, "live_started");
                ack["preset"] = settings.PresetName;
                ack["max_dimension"] = settings.MaxDimension;
                ack["fps"] = settings.FramesPerSecond;
                ack["room_name"] = session.RoomName;
                ack["video_track_name"] = session.VideoTrackName;
                return ack;
            }
            catch (PilotException e)
            {
                StopLiveRuntime();
                PilotLog.Error("Failed to start LiveKit live", e);
                PilotSDK.Event("live_start_failed", "live", new Dictionary<string, object>
                {
                    ["message"] = e.Message,
                    ["http_code"] = e.HttpCode
                });
                return BuildAck(false, e.Message ?? "Failed to start live");
            }
            catch (Exception e)
            {
                StopLiveRuntime();
                PilotLog.Error("Failed to start LiveKit live", e);
                PilotSDK.Event("live_start_failed", "live", new Dictionary<string, object>
                {
                    ["message"] = e.Message
                });
                return BuildAck(false, e.Message ?? "Failed to start live");
            }
        }

        internal Dictionary<string, object> Update(string sessionToken, Dictionary<string, object> payload)
        {
            bool live;
            lock (m_lock) { live = m_isLive; }

            if (!live)
                return BuildAck(false, "Live is not active");

            try
            {
                var requested = LiveSettings.FromPayload(payload);
                var session = FetchPublisherSession(sessionToken, requested);
                var settings = session.Settings;

                var updateError = UpdateQualitySync(settings.PresetName, settings.MaxDimension, settings.FramesPerSecond, out bool screenShareActive);
                if (updateError != null)
                    throw updateError;

                lock (m_lock) { m_settings = settings; }
                OnLiveModeChanged?.Invoke(true, settings.ActionPollIntervalMs);

                var metadata = new Dictionary<string, object>
                {
                    ["preset"] = settings.PresetName,
                    ["max_dimension"] = settings.MaxDimension,
                    ["fps"] = settings.FramesPerSecond,
                    ["screen_share_active"] = screenShareActive
                };
                if (session.RoomName != null) metadata["room_name"] = session.RoomName;
                if (session.ParticipantIdentity != null) metadata["participant_identity"] = session.ParticipantIdentity;
                metadata["video_track_name"] = session.VideoTrackName;
                PilotSDK.Event("live_updated", "live", metadata);

                var ack = BuildAck(true, screenShareActive ? "live_updated" : "live_updated_pending_capture");
                ack["preset"] = settings.PresetName;
                ack["max_dimension"] = settings.MaxDimension;
                ack["fps"] = settings.FramesPerSecond;
                ack["room_name"] = session.RoomName;
                ack["video_track_name"] = session.VideoTrackName;
                ack["screen_share_active"] = screenShareActive;
                return ack;
            }
            catch (PilotException e)
            {
                PilotLog.Error("Failed to update LiveKit live quality", e);
                PilotSDK.Event("live_update_failed", "live", new Dictionary<string, object>
                {
                    ["message"] = e.Message,
                    ["http_code"] = e.HttpCode
                });
                return BuildAck(false, e.Message ?? "Failed to update live quality");
            }
            catch (Exception e)
            {
                PilotLog.Error("Failed to update LiveKit live quality", e);
                PilotSDK.Event("live_update_failed", "live", new Dictionary<string, object>
                {
                    ["message"] = e.Message
                });
                return BuildAck(false, e.Message ?? "Failed to update live quality");
            }
        }

        internal Dictionary<string, object> Stop()
        {
            bool wasLive;
            lock (m_lock) { wasLive = m_isLive; }

            StopLiveRuntime();
            OnLiveModeChanged?.Invoke(false, 0);

            if (wasLive)
                PilotSDK.Event("live_stopped", "live", null);

            return BuildAck(true, wasLive ? "live_stopped" : "live_already_stopped");
        }

        internal Dictionary<string, object> Tap(Dictionary<string, object> payload)
        {
            if (!m_isLive)
                return BuildAck(false, "Live is not active");

            double normalizedX = ClampD(GetDouble(payload, "normalized_x", 0.5), 0.0, 1.0);
            double normalizedY = ClampD(GetDouble(payload, "normalized_y", 0.5), 0.0, 1.0);

            float screenX = (float)(normalizedX * Screen.width);
            float screenY = (float)(normalizedY * Screen.height);

            // Dispatch synthetic pointer event via EventSystem
            DispatchSyntheticTap(screenX, screenY);
            ShowTapOverlay(screenX, screenY);

            PilotSDK.Event("live_tap", "live_input", new Dictionary<string, object>
            {
                ["normalized_x"] = normalizedX,
                ["normalized_y"] = normalizedY,
                ["x"] = screenX,
                ["y"] = screenY
            });

            return BuildAck(true, "tap_sent");
        }

        internal Dictionary<string, object> LongPress(Dictionary<string, object> payload)
        {
            if (!m_isLive)
                return BuildAck(false, "Live is not active");

            double normalizedX = ClampD(GetDouble(payload, "normalized_x", 0.5), 0.0, 1.0);
            double normalizedY = ClampD(GetDouble(payload, "normalized_y", 0.5), 0.0, 1.0);
            int durationMs = ClampI(GetInt(payload, "duration_ms", 800), 250, 2000);

            float screenX = (float)(normalizedX * Screen.width);
            float screenY = (float)(normalizedY * Screen.height);

            DispatchSyntheticTap(screenX, screenY);
            ShowPressOverlay(screenX, screenY);

            // Schedule release after duration
            PilotRunner.Instance.StartCoroutine(DelayedRelease(screenX, screenY, durationMs / 1000f));

            PilotSDK.Event("live_long_press", "live_input", new Dictionary<string, object>
            {
                ["normalized_x"] = normalizedX,
                ["normalized_y"] = normalizedY,
                ["x"] = screenX,
                ["y"] = screenY,
                ["duration_ms"] = durationMs
            });

            var ack = BuildAck(true, "long_press_sent");
            ack["duration_ms"] = durationMs;
            return ack;
        }

        internal void OnSessionClosed()
        {
            StopLiveRuntime();
        }

        internal void Shutdown()
        {
            OnSessionClosed();
        }

        internal bool IsLive
        {
            get { lock (m_lock) { return m_isLive; } }
        }

        // ── Private ──

        private void StopLiveRuntime()
        {
            lock (m_lock)
            {
                m_isLive = false;
                m_settings = LiveSettings.Low();
            }

#if PILOT_LIVEKIT
            m_publisher.Stop();
#endif

            if (m_overlayView != null)
            {
                m_overlayView.ClearIndicator();
            }
        }

        private Exception StartPublisherSync(string serverUrl, string token,
            string presetName, int maxDimension, int framesPerSecond)
        {
#if PILOT_LIVEKIT
            Exception result = null;
            var wait = new ManualResetEventSlim(false);

            m_publisher.Start(serverUrl, token, presetName, maxDimension, framesPerSecond, (error) =>
            {
                result = error;
                wait.Set();
            });

            wait.Wait(TimeSpan.FromSeconds(15));
            return result;
#else
            return new PilotException("LiveKit is not available (PILOT_LIVEKIT not defined)");
#endif
        }

        private Exception EnableScreenShareSync()
        {
#if PILOT_LIVEKIT
            Exception result = null;
            var wait = new ManualResetEventSlim(false);

            m_publisher.EnableScreenShare((error) =>
            {
                result = error;
                wait.Set();
            });

            wait.Wait(TimeSpan.FromSeconds(15));
            return result;
#else
            return new PilotException("LiveKit is not available (PILOT_LIVEKIT not defined)");
#endif
        }

        private Exception UpdateQualitySync(string presetName, int maxDimension, int framesPerSecond, out bool screenShareActive)
        {
#if PILOT_LIVEKIT
            Exception resultError = null;
            bool resultActive = false;
            var wait = new ManualResetEventSlim(false);

            m_publisher.UpdateQuality(presetName, maxDimension, framesPerSecond, (active, error) =>
            {
                resultActive = active;
                resultError = error;
                wait.Set();
            });

            wait.Wait(TimeSpan.FromSeconds(15));
            screenShareActive = resultActive;
            return resultError;
#else
            screenShareActive = false;
            return new PilotException("LiveKit is not available (PILOT_LIVEKIT not defined)");
#endif
        }

        private PublisherSession FetchPublisherSession(string sessionToken, LiveSettings requestedSettings)
        {
            var response = m_httpClient.GetLivePublisherState(sessionToken);
            string statusMessage = TrimToNull(response.GetString("status_message"));

            if (!response.GetBool("configured", false))
                throw new PilotException(statusMessage ?? "LiveKit is not configured on the server");

            if (!response.GetBool("requested", false))
                throw new PilotException(statusMessage ?? "Live request is no longer active");

            string serverUrl = TrimToNull(response.GetString("server_url"));
            string participantToken = TrimToNull(response.GetString("participant_token"));
            string videoTrackName = TrimToNull(response.GetString("video_track_name"));

            if (serverUrl == null || participantToken == null || videoTrackName == null)
                throw new PilotException("Server returned incomplete live credentials");

            string presetName = TrimToNull(response.GetString("preset")) ?? requestedSettings.PresetName;
            int maxDimension = ClampI(response.GetInt("max_dimension", requestedSettings.MaxDimension), 240, 1440);
            int fps = ClampI(response.GetInt("fps", requestedSettings.FramesPerSecond), 1, 6);
            long actionPollIntervalMs = ClampL(
                response.GetLong("action_poll_interval_ms", requestedSettings.ActionPollIntervalMs),
                200L, 2000L);

            return new PublisherSession(
                serverUrl, participantToken,
                TrimToNull(response.GetString("room_name")),
                TrimToNull(response.GetString("participant_identity")),
                videoTrackName,
                new LiveSettings(presetName, maxDimension, fps, actionPollIntervalMs)
            );
        }

        // ── Touch dispatch ──

        private void DispatchSyntheticTap(float screenX, float screenY)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var pointerData = new PointerEventData(eventSystem)
            {
                position = new Vector2(screenX, Screen.height - screenY)
            };

            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, results);

            if (results.Count > 0)
            {
                pointerData.pointerCurrentRaycast = results[0];
                pointerData.pointerPressRaycast = results[0];

                var target = results[0].gameObject;
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);
            }
        }

        // ── Overlay ──

        private PilotLiveOverlayView EnsureOverlay()
        {
            if (m_overlayView == null)
            {
                var go = new GameObject("[Pilot Live Overlay]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                m_overlayView = go.AddComponent<PilotLiveOverlayView>();
            }
            return m_overlayView;
        }

        private void ShowTapOverlay(float x, float y)
        {
            EnsureOverlay().ShowTap(x, y);
        }

        private void ShowPressOverlay(float x, float y)
        {
            EnsureOverlay().ShowPress(x, y);
        }

        private System.Collections.IEnumerator DelayedRelease(float x, float y, float delaySec)
        {
            yield return new WaitForSecondsRealtime(delaySec);
            EnsureOverlay().ShowRelease(x, y);
        }

        // ── Helpers ──

        private static Dictionary<string, object> BuildAck(bool ok, string status)
        {
            return new Dictionary<string, object>
            {
                ["ok"] = ok,
                ["status"] = status
            };
        }

        private static string TrimToNull(string value)
        {
            if (value == null) return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static int ClampI(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static long ClampL(long value, long min, long max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static double ClampD(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static double GetDouble(Dictionary<string, object> dict, string key, double defaultValue)
        {
            if (dict == null || !dict.ContainsKey(key)) return defaultValue;
            try { return Convert.ToDouble(dict[key]); }
            catch { return defaultValue; }
        }

        private static int GetInt(Dictionary<string, object> dict, string key, int defaultValue)
        {
            if (dict == null || !dict.ContainsKey(key)) return defaultValue;
            try { return Convert.ToInt32(dict[key]); }
            catch { return defaultValue; }
        }

        // ── Internal types ──

        private struct LiveSettings
        {
            internal readonly string PresetName;
            internal readonly int MaxDimension;
            internal readonly int FramesPerSecond;
            internal readonly long ActionPollIntervalMs;

            internal LiveSettings(string presetName, int maxDimension, int framesPerSecond, long actionPollIntervalMs)
            {
                PresetName = presetName;
                MaxDimension = maxDimension;
                FramesPerSecond = framesPerSecond;
                ActionPollIntervalMs = actionPollIntervalMs;
            }

            internal static LiveSettings Low()
            {
                return new LiveSettings("low", 540, 2, 500);
            }

            internal static LiveSettings Balanced()
            {
                return new LiveSettings("balanced", 720, 3, 400);
            }

            internal static LiveSettings High()
            {
                return new LiveSettings("high", 1080, 4, 300);
            }

            internal static LiveSettings FromPayload(Dictionary<string, object> payload)
            {
                LiveSettings baseSettings = Low();
                if (payload == null) return baseSettings;

                string preset = null;
                if (payload.ContainsKey("preset"))
                    preset = payload["preset"] as string;

                if (preset == "balanced") baseSettings = Balanced();
                else if (preset == "high") baseSettings = High();
                else preset = baseSettings.PresetName;

                int maxDim = ClampI(GetInt(payload, "max_dimension", baseSettings.MaxDimension), 240, 1440);
                int fps = ClampI(GetInt(payload, "fps", baseSettings.FramesPerSecond), 1, 6);
                long pollMs = ClampL(
                    GetLong(payload, "action_poll_interval_ms", baseSettings.ActionPollIntervalMs),
                    200L, 2000L);

                return new LiveSettings(
                    (preset == "low" || preset == "balanced" || preset == "high") ? preset : "low",
                    maxDim, fps, pollMs
                );
            }

            private static long GetLong(Dictionary<string, object> dict, string key, long defaultValue)
            {
                if (dict == null || !dict.ContainsKey(key)) return defaultValue;
                try { return Convert.ToInt64(dict[key]); }
                catch { return defaultValue; }
            }
        }

        private struct PublisherSession
        {
            internal readonly string ServerUrl;
            internal readonly string ParticipantToken;
            internal readonly string RoomName;
            internal readonly string ParticipantIdentity;
            internal readonly string VideoTrackName;
            internal readonly LiveSettings Settings;

            internal PublisherSession(string serverUrl, string participantToken,
                string roomName, string participantIdentity, string videoTrackName,
                LiveSettings settings)
            {
                ServerUrl = serverUrl;
                ParticipantToken = participantToken;
                RoomName = roomName;
                ParticipantIdentity = participantIdentity;
                VideoTrackName = videoTrackName;
                Settings = settings;
            }
        }
    }
}
