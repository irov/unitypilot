using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pilot.SDK
{
    /// <summary>
    /// Internal MonoBehaviour that drives the Pilot SDK on the Unity main thread.
    /// Created automatically when PilotSDK.Initialize() is called.
    /// </summary>
    internal sealed class PilotRunner : MonoBehaviour
    {
        private static PilotRunner s_instance;
        private static readonly List<Action> s_pendingActions = new List<Action>();
        private static readonly object s_pendingLock = new object();

        internal static PilotRunner Instance => s_instance;

        internal static void EnsureExists()
        {
            if (s_instance != null) return;

            var go = new GameObject("[PilotSDK]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            s_instance = go.AddComponent<PilotRunner>();
        }

        internal static void RunOnMainThread(Action action)
        {
            lock (s_pendingLock)
            {
                s_pendingActions.Add(action);
            }
        }

        private void Update()
        {
            FlushPendingActions();
            PilotSDK.OnUpdate();
        }

        private void FlushPendingActions()
        {
            List<Action> actions;
            lock (s_pendingLock)
            {
                if (s_pendingActions.Count == 0) return;
                actions = new List<Action>(s_pendingActions);
                s_pendingActions.Clear();
            }

            foreach (var action in actions)
            {
                try { action(); }
                catch (Exception e) { PilotLog.Error("PilotRunner main-thread action failed", e); }
            }
        }

        private void OnApplicationQuit()
        {
            PilotSDK.OnApplicationQuit();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }
    }
}
