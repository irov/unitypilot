namespace Pilot.SDK
{
    /// <summary>
    /// Provides a dynamic value for a widget property or attribute.
    /// Called on the SDK poll thread — keep implementations lightweight.
    /// </summary>
    public delegate object PilotValueProvider();
}
