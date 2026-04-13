using UnityEngine;

namespace Pilot.SDK
{
    /// <summary>
    /// Overlay that draws a circular touch indicator for remote live touches.
    /// Attached to a hidden Canvas that renders on top of everything.
    /// </summary>
    internal sealed class PilotLiveOverlayView : MonoBehaviour
    {
        private const float TapReleaseSeconds = 0.09f;
        private const float TapHideSeconds = 0.42f;
        private const float ReleaseHideSeconds = 0.26f;

        private static readonly Color OuterColor = new Color(1f, 1f, 1f, 0.4f);
        private static readonly Color InnerColor = new Color(1f, 0.439f, 0.263f, 1f); // #FF7043

        private bool m_visible;
        private bool m_pressed;
        private float m_centerX;
        private float m_centerY;
        private float m_timer;
        private float m_releaseTimer;
        private bool m_pendingRelease;
        private bool m_pendingHide;
        private float m_hideTimer;

        private Texture2D m_circleTexture;
        private GUIStyle m_outerStyle;
        private GUIStyle m_innerStyle;

        void Awake()
        {
            m_circleTexture = CreateCircleTexture(64);

            m_outerStyle = new GUIStyle();
            m_outerStyle.normal.background = m_circleTexture;

            m_innerStyle = new GUIStyle();
            m_innerStyle.normal.background = m_circleTexture;
        }

        void Update()
        {
            if (!m_visible) return;

            if (m_pendingRelease)
            {
                m_releaseTimer -= Time.unscaledDeltaTime;
                if (m_releaseTimer <= 0f)
                {
                    m_pressed = false;
                    m_pendingRelease = false;
                }
            }

            if (m_pendingHide)
            {
                m_hideTimer -= Time.unscaledDeltaTime;
                if (m_hideTimer <= 0f)
                {
                    m_visible = false;
                    m_pressed = false;
                    m_pendingHide = false;
                }
            }
        }

        void OnGUI()
        {
            if (!m_visible) return;

            float outerRadius = m_pressed ? 28f : 22f;
            float innerRadius = m_pressed ? 12f : 8f;

            GUI.color = OuterColor;
            GUI.DrawTexture(
                new Rect(m_centerX - outerRadius, m_centerY - outerRadius, outerRadius * 2, outerRadius * 2),
                m_circleTexture, ScaleMode.StretchToFill);

            GUI.color = InnerColor;
            GUI.DrawTexture(
                new Rect(m_centerX - innerRadius, m_centerY - innerRadius, innerRadius * 2, innerRadius * 2),
                m_circleTexture, ScaleMode.StretchToFill);

            GUI.color = Color.white;
        }

        internal void ShowTap(float x, float y)
        {
            m_centerX = x;
            m_centerY = y;
            m_pressed = true;
            m_visible = true;
            m_pendingRelease = true;
            m_releaseTimer = TapReleaseSeconds;
            m_pendingHide = true;
            m_hideTimer = TapHideSeconds;
        }

        internal void ShowPress(float x, float y)
        {
            m_centerX = x;
            m_centerY = y;
            m_pressed = true;
            m_visible = true;
            m_pendingRelease = false;
            m_pendingHide = false;
        }

        internal void ShowRelease(float x, float y)
        {
            m_centerX = x;
            m_centerY = y;
            m_pressed = false;
            m_visible = true;
            m_pendingRelease = false;
            m_pendingHide = true;
            m_hideTimer = ReleaseHideSeconds;
        }

        internal void ClearIndicator()
        {
            m_visible = false;
            m_pressed = false;
            m_pendingRelease = false;
            m_pendingHide = false;
        }

        void OnDestroy()
        {
            if (m_circleTexture != null)
                Destroy(m_circleTexture);
        }

        private static Texture2D CreateCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float radiusSq = center * center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float distSq = dx * dx + dy * dy;

                    if (distSq <= radiusSq)
                    {
                        float edge = Mathf.Clamp01((Mathf.Sqrt(radiusSq) - Mathf.Sqrt(distSq)) * 2f / size * size);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Min(edge, 1f)));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            return tex;
        }
    }
}
