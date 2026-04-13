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

        internal static PilotRunner Instance => s_instance;

        internal static void EnsureExists()
        {
            if (s_instance != null) return;

            var go = new GameObject("[PilotSDK]");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            s_instance = go.AddComponent<PilotRunner>();
        }

        private void Update()
        {
            PilotSDK.OnUpdate();
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
