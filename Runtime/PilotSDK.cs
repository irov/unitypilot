using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Pilot.SDK
{
    /// <summary>
    /// Pilot SDK — remote debug panel, logging, and metrics for Unity applications.
    ///
    /// <code>
    /// // 1. Initialize
    /// var config = new PilotConfig.Builder("https://pilot.example.com", "plt_your_token")
    ///     .SetDeviceId("my-device")
    ///     .SetDeviceName("Unity Editor")
    ///     .Build();
    /// Pilot.Initialize(config);
    ///
    /// // 2. Listen for actions
    /// Pilot.AddActionListener(new MyActionListener());
    ///
    /// // 3. Build UI
    /// PilotUI ui = Pilot.GetUI();
    /// PilotTab tab = ui.AddTab("Controls");
    /// PilotLayout root = tab.Vertical();
    /// root.AddButton("Restart")
    ///     .Variant("contained").Color("error")
    ///     .OnClick(action => RestartGame());
    ///
    /// // 4. Connect
    /// Pilot.Connect();
    ///
    /// // 5. Send logs anytime
    /// Pilot.Log(PilotLogLevel.Info, "Game started");
    ///
    /// // 6. Shutdown on exit
    /// Pilot.Shutdown();
    /// </code>
    /// </summary>
    public sealed class PilotSDK
    {
        public const string VERSION = "1.0.0";

        private static volatile PilotSDK s_instance;
        private static readonly object s_lock = new object();

        private readonly PilotConfig m_config;
        private readonly PilotHttpClient m_httpClient;

        private volatile string m_sessionToken;
        private volatile string m_requestId;
        private volatile PilotSessionStatus m_status = PilotSessionStatus.Disconnected;
        private volatile bool m_running;
        private volatile bool m_actionPollInFlight;

        private readonly List<PilotLogEntry> m_logBuffer = new List<PilotLogEntry>();
        private readonly object m_logLock = new object();
        private bool m_logOverflowWarned;
        private readonly Dictionary<string, object> m_sessionAttributeCache = new Dictionary<string, object>();

        private readonly List<IPilotActionListener> m_actionListeners = new List<IPilotActionListener>();
        private readonly List<IPilotSessionListener> m_sessionListeners = new List<IPilotSessionListener>();
        private readonly List<PilotWidgetCallback> m_actionCallbacks = new List<PilotWidgetCallback>();

        private Thread m_connectionThread;
        private Timer m_actionPollTimer;
        private Timer m_metricSampleTimer;
        private long m_currentActionPollIntervalMs;

        private readonly PilotUI m_ui = new PilotUI();
        private readonly PilotMetrics m_metrics = new PilotMetrics();
        private readonly PilotDefaultMetricCollector m_defaultCollector = new PilotDefaultMetricCollector();
        private readonly PilotLiveManager m_liveManager;

        // Main-thread dispatch queue
        private readonly List<Action> m_mainThreadQueue = new List<Action>();
        private readonly object m_mainThreadLock = new object();

        private PilotSDK(PilotConfig config)
        {
            m_config = config;
            m_httpClient = new PilotHttpClient(config.BaseUrl, config.ApiToken);
            m_currentActionPollIntervalMs = config.ActionPollIntervalMs;
            PilotLog.SetLevel(config.LogConfig.GetLogLevel());
            PilotLog.SetLoggerListener(config.LoggerListener);

            m_liveManager = new PilotLiveManager(m_httpClient);
            m_liveManager.OnLiveModeChanged = (enabled, pollMs) =>
            {
                if (enabled && pollMs > 0)
                {
                    var token = m_sessionToken;
                    if (token != null)
                        ScheduleActionPolling(token, pollMs);
                }
                else if (!enabled)
                {
                    var token = m_sessionToken;
                    if (token != null)
                        ScheduleActionPolling(token, m_config.ActionPollIntervalMs);
                }
            };

            var mc = config.MetricConfig;
            if (mc.IsEnabled())
            {
                m_metrics.SetSampleIntervalMs(mc.GetSampleIntervalMs());
                m_metrics.SetBufferSize(mc.GetBufferSize());
                m_metrics.SetBatchSize(mc.GetBatchSize());
            }
        }

        // ══════════════════════════════════════════════════════
        //  Static API
        // ══════════════════════════════════════════════════════

        public static void Initialize(PilotConfig config)
        {
            if (s_instance != null)
            {
                PilotLog.Warn("Pilot.Initialize() called more than once, ignoring");
                return;
            }

            lock (s_lock)
            {
                if (s_instance == null)
                {
                    PilotLog.SetMainThreadId(System.Threading.Thread.CurrentThread.ManagedThreadId);

                    var p = new PilotSDK(config);
                    s_instance = p;

                    // Ensure MonoBehaviour runner exists
                    PilotRunner.EnsureExists();

                    PilotLog.Info("Pilot SDK initialized (server: {0})", config.BaseUrl);

                    if (config.SessionListener != null)
                        p.m_sessionListeners.Add(config.SessionListener);
                    if (config.ActionListener != null)
                        p.m_actionListeners.Add(config.ActionListener);

                    var mc = config.MetricConfig;
                    if (mc.IsEnabled())
                    {
                        p.m_metrics.AddCollector(p.m_defaultCollector);
                        foreach (var collector in mc.GetCollectors())
                            p.m_metrics.AddCollector(collector);
                        PilotLog.Info("Built-in metrics enabled (sample interval: {0}ms)", mc.GetSampleIntervalMs());
                    }

                    if (config.AutoConnect)
                        p.StartConnection();
                }
            }
        }

        public static bool IsInitialized => s_instance != null;

        public static PilotSessionStatus GetStatus()
        {
            var p = s_instance;
            return p != null ? p.m_status : PilotSessionStatus.Disconnected;
        }

        public static void AddActionListener(IPilotActionListener listener)
        {
            var p = RequireInstance();
            lock (s_lock) { p.m_actionListeners.Add(listener); }
        }

        public static void RemoveActionListener(IPilotActionListener listener)
        {
            var p = s_instance;
            if (p != null)
                lock (s_lock) { p.m_actionListeners.Remove(listener); }
        }

        public static void AddSessionListener(IPilotSessionListener listener)
        {
            var p = RequireInstance();
            lock (s_lock) { p.m_sessionListeners.Add(listener); }
        }

        public static void RemoveSessionListener(IPilotSessionListener listener)
        {
            var p = s_instance;
            if (p != null)
                lock (s_lock) { p.m_sessionListeners.Remove(listener); }
        }

        public static PilotUI GetUI()
        {
            return RequireInstance().m_ui;
        }

        public static PilotMetrics GetMetrics()
        {
            return RequireInstance().m_metrics;
        }

        public static void Connect()
        {
            RequireInstance().StartConnection();
        }

        public static void Disconnect()
        {
            var p = s_instance;
            if (p != null) p.StopConnection();
        }

        // ── Logging API ──

        public static void Log(PilotLogLevel level, string message)
        {
            var p = s_instance;
            if (p != null && p.m_config.LogConfig.IsEnabled())
                p.BufferLog(new PilotLogEntry(level, message, null, null, null, p.ResolveLogAttributes()));
        }

        public static void Log(PilotLogLevel level, string message, string category, string thread = null)
        {
            var p = s_instance;
            if (p != null && p.m_config.LogConfig.IsEnabled())
                p.BufferLog(new PilotLogEntry(level, message, category, thread, null, p.ResolveLogAttributes()));
        }

        public static void Log(PilotLogLevel level, string message, Dictionary<string, object> metadata)
        {
            var p = s_instance;
            if (p != null && p.m_config.LogConfig.IsEnabled())
                p.BufferLog(new PilotLogEntry(level, message, null, null, metadata, p.ResolveLogAttributes()));
        }

        public static void Log(PilotLogLevel level, string message, string category, string thread,
            Dictionary<string, object> metadata)
        {
            var p = s_instance;
            if (p != null && p.m_config.LogConfig.IsEnabled())
                p.BufferLog(new PilotLogEntry(level, message, category, thread, metadata, p.ResolveLogAttributes()));
        }

        public static void Log(PilotLogEntry entry)
        {
            var p = s_instance;
            if (p != null && p.m_config.LogConfig.IsEnabled())
                p.BufferLog(entry);
        }

        // ── Structured event/revenue API ──

        public static void Event(string message, Dictionary<string, object> metadata = null)
        {
            Event(message, null, metadata, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void Event(string message, string category,
            Dictionary<string, object> metadata = null)
        {
            Event(message, category, metadata, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void Event(string message, string category,
            Dictionary<string, object> metadata, long clientTimestampMs)
        {
            BufferStructuredLog("event", message, category, metadata, clientTimestampMs);
        }

        public static void Revenue(string message, Dictionary<string, object> metadata = null)
        {
            Revenue(message, null, metadata, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void Revenue(string message, string category,
            Dictionary<string, object> metadata = null)
        {
            Revenue(message, category, metadata, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void Revenue(string message, string category,
            Dictionary<string, object> metadata, long clientTimestampMs)
        {
            BufferStructuredLog("revenue", message, category, metadata, clientTimestampMs);
        }

        public static void ChangeScreen(string screenType, string screenName)
        {
            var metadata = new Dictionary<string, object>
            {
                ["pilot_command"] = "change_screen",
                ["pilot_slice_type"] = "screen",
                ["pilot_slice_name"] = screenName,
                ["screen_type"] = screenType,
                ["screen_name"] = screenName
            };
            Event("change_screen", "change_screen", metadata);
        }

        public static void SetInAppProducts(List<Dictionary<string, object>> products)
        {
            var normalized = new List<object>();
            foreach (var product in products)
                normalized.Add(product != null ? new Dictionary<string, object>(product) : new Dictionary<string, object>());

            var metadata = new Dictionary<string, object>
            {
                ["pilot_command"] = "set_in_app_products",
                ["pilot_purchase_entry_type"] = "catalog",
                ["in_app_products"] = normalized,
                ["in_app_product_count"] = normalized.Count
            };
            BufferStructuredLog("purchase", "set_in_app_products", "catalog", metadata,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void SetOwnedInAppProducts(List<string> productIds)
        {
            var normalized = new List<object>(productIds);
            var metadata = new Dictionary<string, object>
            {
                ["pilot_command"] = "set_owned_in_app_products",
                ["pilot_purchase_entry_type"] = "owned",
                ["owned_in_app_products"] = normalized,
                ["owned_in_app_product_count"] = normalized.Count
            };
            BufferStructuredLog("purchase", "set_owned_in_app_products", "owned", metadata,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void PurchaseInApp(string transactionId, List<string> productIds,
            Dictionary<string, object> metadata = null)
        {
            var normalized = new List<object>(productIds);
            var purchaseMetadata = new Dictionary<string, object>();
            if (metadata != null)
                foreach (var kvp in metadata)
                    purchaseMetadata[kvp.Key] = kvp.Value;

            purchaseMetadata["pilot_command"] = "purchase_in_app";
            purchaseMetadata["pilot_purchase_entry_type"] = "purchase";
            purchaseMetadata["in_app_products"] = normalized;
            purchaseMetadata["in_app_product_count"] = normalized.Count;

            if (!string.IsNullOrEmpty(transactionId))
                purchaseMetadata["transaction_id"] = transactionId;

            string message = normalized.Count == 0 ? "purchase_in_app" : productIds[0];
            BufferStructuredLog("purchase", message, "purchase", purchaseMetadata,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void AcknowledgeAction(string actionId, Dictionary<string, object> ackPayload = null)
        {
            var p = s_instance;
            if (p == null) return;
            string token = p.m_sessionToken;
            if (token == null) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { p.m_httpClient.AcknowledgeAction(token, actionId, ackPayload); }
                catch (PilotException e) { PilotLog.Error("Failed to acknowledge action", e); }
            });
        }

        public static void Shutdown()
        {
            PilotSDK p;
            lock (s_lock)
            {
                p = s_instance;
                s_instance = null;
            }
            if (p != null) p.DoShutdown();
        }

        // ══════════════════════════════════════════════════════
        //  Internal (called by PilotRunner MonoBehaviour)
        // ══════════════════════════════════════════════════════

        internal static void OnUpdate()
        {
            var p = s_instance;
            if (p == null) return;

            // Flush deferred log messages from background threads
            PilotLog.FlushDeferred();

            // Update delta time for FPS tracking
            p.m_defaultCollector.UpdateDeltaTime(Time.unscaledDeltaTime);

            // Cache Unity API values that can only be read from main thread
            p.m_defaultCollector.UpdateFromMainThread();

            // Poll widget value providers on the main thread (providers may access Unity APIs)
            p.m_ui.PollValues();

            // Process main-thread dispatch queue
            p.ProcessMainThreadQueue();
        }

        internal static void OnApplicationQuit()
        {
            Shutdown();
        }

        // ══════════════════════════════════════════════════════
        //  Internal implementation
        // ══════════════════════════════════════════════════════

        private static PilotSDK RequireInstance()
        {
            var p = s_instance;
            if (p == null)
                throw new InvalidOperationException("Pilot.Initialize() must be called first");
            return p;
        }

        private void StartConnection()
        {
            if (m_running) { PilotLog.Warn("Already connecting/connected"); return; }
            m_running = true;

            m_connectionThread = new Thread(ConnectAndWaitApproval) { IsBackground = true, Name = "PilotSDK-Connect" };
            m_connectionThread.Start();
        }

        private void StopConnection()
        {
            if (!m_running) return;
            m_running = false;

            CancelScheduledTasks();

            m_liveManager.OnSessionClosed();

            string token = m_sessionToken;
            m_sessionToken = null;

            if (token != null)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        FlushLogs(token);
                        FlushMetrics(token);
                        m_httpClient.CloseSession(token);
                    }
                    catch (PilotException) { }

                    SetStatus(PilotSessionStatus.Closed);
                    RunOnMainThread(() => NotifySessionClosed());
                });
            }
            else
            {
                SetStatus(PilotSessionStatus.Disconnected);
            }
        }

        private void DoShutdown()
        {
            PilotLog.Info("Shutting down Pilot SDK");
            m_liveManager.Shutdown();
            StopConnection();
            m_httpClient.Shutdown();
            lock (s_lock)
            {
                m_actionListeners.Clear();
                m_sessionListeners.Clear();
            }
            lock (m_logLock) { m_logBuffer.Clear(); }
            m_metrics.Clear();
        }

        private void ConnectAndWaitApproval()
        {
            int retryCount = 0;
            long retryDelayMs = 2000;
            const long maxRetryDelayMs = 30000;

            while (m_running)
            {
                try
                {
                    DoConnectAndWaitApproval();
                    return;
                }
                catch (PilotException e)
                {
                    if (e.IsUnauthorized)
                    {
                        PilotLog.Error("Authentication failed", e);
                        SetStatus(PilotSessionStatus.AuthFailed);
                        m_running = false;
                        RunOnMainThread(() => NotifyAuthFailed());
                        return;
                    }

                    if (!e.IsNetworkError)
                    {
                        PilotLog.Error("Server error, stopping connection: {0}", e.Message);
                        SetStatus(PilotSessionStatus.Error);
                        m_running = false;
                        RunOnMainThread(() => NotifyError(e));
                        return;
                    }

                    retryCount++;
                    PilotLog.Warn("Connection attempt {0} failed (network): {1}, retrying in {2}ms",
                        retryCount, e.Message, retryDelayMs);
                    SetStatus(PilotSessionStatus.Connecting);

                    Thread.Sleep((int)retryDelayMs);
                    retryDelayMs = Math.Min(retryDelayMs * 2, maxRetryDelayMs);
                }
            }
        }

        private void DoConnectAndWaitApproval()
        {
            SetStatus(PilotSessionStatus.Connecting);
            RunOnMainThread(() => NotifyConnecting());

            string deviceId = m_config.DeviceId;
            string deviceName = m_config.DeviceName;

            if (string.IsNullOrEmpty(deviceId))
                deviceId = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(deviceName))
                deviceName = SystemInfo.deviceModel + " (Unity " + Application.unityVersion + ")";

            var resp = m_httpClient.Connect(deviceId, deviceName, ResolveAllSessionAttributes());
            m_requestId = resp.RequestId;

            PilotLog.Info("Connect request sent, request_id={0}, status={1}", resp.RequestId, resp.Status);

            if (resp.IsApproved && resp.SessionToken != null)
            {
                OnApproved(resp.SessionToken);
                return;
            }

            if (resp.IsRejected)
            {
                OnRejected();
                return;
            }

            SetStatus(PilotSessionStatus.WaitingApproval);
            RunOnMainThread(() => NotifyWaitingApproval(resp.RequestId));

            while (m_running)
            {
                Thread.Sleep((int)m_config.PollIntervalMs);

                var pollResp = m_httpClient.PollStatus(resp.RequestId);

                if (pollResp.IsApproved && pollResp.SessionToken != null)
                {
                    OnApproved(pollResp.SessionToken);
                    return;
                }

                if (pollResp.IsRejected)
                {
                    OnRejected();
                    return;
                }
            }
        }

        private void OnApproved(string sessionToken)
        {
            m_sessionToken = sessionToken;
            SetStatus(PilotSessionStatus.Active);

            PilotLog.Info("Session approved and active");

            RunOnMainThread(() => NotifySessionStarted(sessionToken));

            // Send initial UI
            if (m_ui.HasTabs())
            {
                var snapshot = m_ui.ToDict();
                try
                {
                    m_httpClient.SubmitPanel(sessionToken, snapshot);
                    m_ui.MarkSent();
                    PilotLog.Debug("Initial UI submitted (revision={0})", m_ui.Revision);
                }
                catch (PilotException e)
                {
                    PilotLog.Error("Failed to submit initial UI", e);
                    RunOnMainThread(() => NotifyError(e));
                }
            }

            // Start action polling
            ScheduleActionPolling(sessionToken, m_config.ActionPollIntervalMs);

            // Start metric sampling
            if (m_config.MetricConfig.IsEnabled())
            {
                long sampleMs = m_metrics.GetSampleIntervalMs();
                m_metricSampleTimer = new Timer(_ => m_metrics.Sample(), null, sampleMs, sampleMs);
            }
        }

        private void OnRejected()
        {
            PilotLog.Warn("Connection request rejected");
            SetStatus(PilotSessionStatus.Rejected);
            m_running = false;
            RunOnMainThread(() => NotifyRejected());
        }

        private void DoPollActions(string sessionToken)
        {
            if (!m_running || m_actionPollInFlight) return;
            m_actionPollInFlight = true;

            try
            {
                var changedAttrs = ResolveChangedSessionAttributes();
                var logChunk = DrainLogChunk();
                var metricChunk = m_metrics.Drain();

                // Poll value providers and snapshot UI
                // NOTE: PollValues() is called from OnUpdate() on the main thread
                Dictionary<string, object> uiSnapshot = m_ui.HasUnsent() ? m_ui.ToDict() : null;

                if (uiSnapshot != null)
                {
                    try
                    {
                        m_httpClient.SubmitPanel(sessionToken, uiSnapshot);
                        m_ui.MarkSent();
                        PilotLog.Debug("UI submitted (revision={0})", m_ui.Revision);
                    }
                    catch (PilotException e)
                    {
                        PilotLog.Error("Failed to submit UI", e);
                        RunOnMainThread(() => NotifyError(e));
                    }
                }

                try
                {
                    var json = m_httpClient.PollActions(sessionToken, changedAttrs, logChunk, metricChunk);
                    var actionsArr = json.GetArray("actions");
                    if (actionsArr != null && actionsArr.Count > 0)
                    {
                        foreach (var actionObj in actionsArr)
                        {
                            if (actionObj is Dictionary<string, object> actionDict)
                            {
                                var action = PilotAction.FromDict(new SimpleJson(actionDict));
                                RunOnMainThread(() => DispatchAction(action));
                            }
                        }
                    }

                    if (logChunk.Count > 0)
                        m_logOverflowWarned = false;
                }
                catch (PilotException e)
                {
                    RequeueLogs(logChunk);
                    m_metrics.Requeue(metricChunk);

                    if (e.IsNetworkError)
                    {
                        PilotLog.Warn("Network error during action poll, will retry");
                    }
                    else
                    {
                        PilotLog.Warn("Action poll failed with server error, attempting reconnect");
                        AttemptReconnect(sessionToken);
                    }
                }
            }
            finally
            {
                m_actionPollInFlight = false;
            }
        }

        private void DispatchAction(PilotAction action)
        {
            bool handled = HandleInternalAction(action);

            if (!handled)
                m_ui.DispatchAction(action);

            if (!handled)
            {
                List<IPilotActionListener> listeners;
                lock (s_lock) { listeners = new List<IPilotActionListener>(m_actionListeners); }
                foreach (var listener in listeners)
                {
                    try { listener.OnPilotActionReceived(action); }
                    catch (Exception ex) { PilotLog.Error("Action listener threw exception", ex); }
                }
            }
        }

        private bool HandleInternalAction(PilotAction action)
        {
            switch (action.ActionType)
            {
                case PilotActionType.LiveStart:
                {
                    string token = m_sessionToken;
                    if (token == null) return false;
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        var ack = m_liveManager.Start(token, action.Payload?.ToDictionary());
                        AcknowledgeAction(action.Id, ack);
                    });
                    return true;
                }
                case PilotActionType.LiveUpdate:
                {
                    string token = m_sessionToken;
                    if (token == null) return false;
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        var ack = m_liveManager.Update(token, action.Payload?.ToDictionary());
                        AcknowledgeAction(action.Id, ack);
                    });
                    return true;
                }
                case PilotActionType.LiveStop:
                {
                    var ack = m_liveManager.Stop();
                    AcknowledgeAction(action.Id, ack);
                    return true;
                }
                case PilotActionType.LiveTap:
                {
                    var ack = m_liveManager.Tap(action.Payload?.ToDictionary());
                    AcknowledgeAction(action.Id, ack);
                    return true;
                }
                case PilotActionType.LiveLongPress:
                {
                    var ack = m_liveManager.LongPress(action.Payload?.ToDictionary());
                    AcknowledgeAction(action.Id, ack);
                    return true;
                }
                default:
                    return false;
            }
        }

        private void ScheduleActionPolling(string sessionToken, long intervalMs)
        {
            if (!m_running) return;

            m_actionPollTimer?.Dispose();
            m_currentActionPollIntervalMs = intervalMs;
            m_actionPollTimer = new Timer(_ => DoPollActions(sessionToken), null, 0, intervalMs);
        }

        private void CancelScheduledTasks()
        {
            m_actionPollTimer?.Dispose();
            m_actionPollTimer = null;
            m_metricSampleTimer?.Dispose();
            m_metricSampleTimer = null;
        }

        // ── Log buffering ──

        private void BufferLog(PilotLogEntry entry)
        {
            lock (m_logLock)
            {
                if (m_logBuffer.Count >= m_config.LogConfig.GetBufferSize())
                {
                    m_logBuffer.RemoveAt(0);
                    if (!m_logOverflowWarned)
                    {
                        m_logOverflowWarned = true;
                        PilotLog.Warn("Log buffer overflow ({0}), dropping oldest entries",
                            m_config.LogConfig.GetBufferSize());
                    }
                }
                m_logBuffer.Add(entry);
            }
        }

        private List<PilotLogEntry> DrainLogChunk()
        {
            lock (m_logLock)
            {
                if (m_logBuffer.Count == 0)
                    return new List<PilotLogEntry>();

                int count = Math.Min(m_logBuffer.Count, m_config.LogConfig.GetBatchSize());
                var chunk = new List<PilotLogEntry>(m_logBuffer.GetRange(0, count));
                m_logBuffer.RemoveRange(0, count);
                return chunk;
            }
        }

        private void RequeueLogs(List<PilotLogEntry> chunk)
        {
            if (chunk.Count == 0) return;
            lock (m_logLock)
            {
                m_logBuffer.InsertRange(0, chunk);
                while (m_logBuffer.Count > m_config.LogConfig.GetBufferSize())
                    m_logBuffer.RemoveAt(m_logBuffer.Count - 1);
            }
        }

        private void FlushLogs(string sessionToken)
        {
            var chunk = DrainLogChunk();
            if (chunk.Count == 0) return;
            try { m_httpClient.SendLogs(sessionToken, chunk); }
            catch (PilotException) { RequeueLogs(chunk); }
        }

        private void FlushMetrics(string sessionToken)
        {
            var chunk = m_metrics.Drain();
            if (chunk.Count == 0) return;
            try { m_httpClient.SendMetrics(sessionToken, chunk); }
            catch (PilotException) { m_metrics.Requeue(chunk); }
        }

        // ── Attribute resolution ──

        private Dictionary<string, object> ResolveLogAttributes()
        {
            var builder = m_config.LogConfig.GetAttributes();
            var staticAttrs = builder.GetStaticAttributes();
            var dynamicAttrs = builder.GetDynamicAttributes();

            if (staticAttrs.Count == 0 && dynamicAttrs.Count == 0)
                return null;

            var attributes = new Dictionary<string, object>();
            foreach (var kvp in staticAttrs)
                attributes[kvp.Key] = kvp.Value;
            foreach (var kvp in dynamicAttrs)
            {
                try { attributes[kvp.Key] = kvp.Value(); }
                catch { }
            }

            return attributes.Count == 0 ? null : attributes;
        }

        private Dictionary<string, object> ResolveAllSessionAttributes()
        {
            var builder = m_config.SessionAttributes;
            var merged = new Dictionary<string, object>(builder.GetStaticAttributes());

            foreach (var kvp in builder.GetDynamicAttributes())
            {
                try
                {
                    object value = kvp.Value();
                    merged[kvp.Key] = value;
                    m_sessionAttributeCache[kvp.Key] = value;
                }
                catch (Exception e)
                {
                    PilotLog.Error("Session attribute provider failed: " + kvp.Key, e);
                }
            }

            return merged;
        }

        private Dictionary<string, object> ResolveChangedSessionAttributes()
        {
            var dynamicAttrs = m_config.SessionAttributes.GetDynamicAttributes();
            if (dynamicAttrs.Count == 0) return null;

            Dictionary<string, object> changed = null;
            foreach (var kvp in dynamicAttrs)
            {
                try
                {
                    object value = kvp.Value();
                    object cached;
                    m_sessionAttributeCache.TryGetValue(kvp.Key, out cached);

                    if (!Equals(value, cached))
                    {
                        m_sessionAttributeCache[kvp.Key] = value;
                        if (changed == null) changed = new Dictionary<string, object>();
                        changed[kvp.Key] = value;
                    }
                }
                catch (Exception e)
                {
                    PilotLog.Error("Session attribute provider failed: " + kvp.Key, e);
                }
            }

            return changed;
        }

        private static void BufferStructuredLog(string kind, string message, string category,
            Dictionary<string, object> metadata, long clientTimestampMs)
        {
            var p = s_instance;
            if (p == null || !p.m_config.LogConfig.IsEnabled()) return;

            string resolvedCategory = ResolveStructuredCategory(kind, category);
            var resolvedMetadata = MergeStructuredMetadata(kind, metadata);

            p.BufferLog(new PilotLogEntry(
                PilotLogLevel.Info, message, resolvedCategory, null,
                resolvedMetadata, p.ResolveLogAttributes(), clientTimestampMs));
        }

        private static string ResolveStructuredCategory(string kind, string category)
        {
            if (string.IsNullOrEmpty(category)) return kind;
            if (category == kind || category.StartsWith(kind + "_")) return category;
            return kind + "_" + category;
        }

        private static Dictionary<string, object> MergeStructuredMetadata(string kind,
            Dictionary<string, object> metadata)
        {
            var merged = new Dictionary<string, object>();
            if (metadata != null)
                foreach (var kvp in metadata)
                    merged[kvp.Key] = kvp.Value;
            merged["pilot_kind"] = kind;
            return merged;
        }

        // ── Reconnect ──

        private void AttemptReconnect(string token)
        {
            CancelScheduledTasks();
            SetStatus(PilotSessionStatus.Connecting);

            long retryDelay = 2000;
            const long maxDelay = 30000;

            while (m_running)
            {
                Thread.Sleep((int)retryDelay);
                if (!m_running) return;

                try
                {
                    m_httpClient.PollActions(token, null, new List<PilotLogEntry>(), new List<PilotMetricEntry>());
                    PilotLog.Info("Reconnect successful, session still active");
                    SetStatus(PilotSessionStatus.Active);
                    ScheduleActionPolling(token, m_currentActionPollIntervalMs);
                    return;
                }
                catch (PilotException e)
                {
                    if (e.IsNetworkError)
                    {
                        PilotLog.Warn("Reconnect: no network, retrying in {0}ms", retryDelay);
                        retryDelay = Math.Min(retryDelay * 2, maxDelay);
                        continue;
                    }

                    PilotLog.Warn("Session lost (server error), starting fresh connection");
                    ResetAndRestart();
                    return;
                }
            }
        }

        private void ResetAndRestart()
        {
            m_running = false;
            m_sessionToken = null;
            m_requestId = null;
            SetStatus(PilotSessionStatus.Disconnected);

            StartConnection();
        }

        // ── Status and notifications ──

        private void SetStatus(PilotSessionStatus status) { m_status = status; }

        private void NotifyConnecting()
        {
            List<IPilotSessionListener> listeners;
            lock (s_lock) { listeners = new List<IPilotSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnPilotSessionConnecting(); } catch { } }
        }

        private void NotifyWaitingApproval(string requestId)
        {
            List<IPilotSessionListener> listeners;
            lock (s_lock) { listeners = new List<IPilotSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnPilotSessionWaitingApproval(requestId); } catch { } }
        }

        private void NotifySessionStarted(string sessionToken)
        {
            List<IPilotSessionListener> listeners;
            lock (s_lock) { listeners = new List<IPilotSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnPilotSessionStarted(sessionToken); } catch { } }
        }

        private void NotifySessionClosed()
        {
            List<IPilotSessionListener> listeners;
            lock (s_lock) { listeners = new List<IPilotSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnPilotSessionClosed(); } catch { } }
        }

        private void NotifyRejected()
        {
            List<IPilotSessionListener> listeners;
            lock (s_lock) { listeners = new List<IPilotSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnPilotSessionRejected(); } catch { } }
        }

        private void NotifyAuthFailed()
        {
            List<IPilotSessionListener> listeners;
            lock (s_lock) { listeners = new List<IPilotSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnPilotSessionAuthFailed(); } catch { } }
        }

        private void NotifyError(PilotException exception)
        {
            List<IPilotSessionListener> listeners;
            lock (s_lock) { listeners = new List<IPilotSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnPilotSessionError(exception); } catch { } }
        }

        // ── Main-thread dispatch ──

        private void RunOnMainThread(Action action)
        {
            lock (m_mainThreadLock) { m_mainThreadQueue.Add(action); }
        }

        private void ProcessMainThreadQueue()
        {
            List<Action> actions;
            lock (m_mainThreadLock)
            {
                if (m_mainThreadQueue.Count == 0) return;
                actions = new List<Action>(m_mainThreadQueue);
                m_mainThreadQueue.Clear();
            }

            foreach (var action in actions)
            {
                try { action(); }
                catch (Exception e) { PilotLog.Error("Main thread dispatch failed", e); }
            }
        }
    }
}
