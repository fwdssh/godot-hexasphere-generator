using Godot;
using System.Collections.Generic;

/// <summary>
/// Static singleton that coordinates input between multiple HexasphereNode instances.
/// Ensures only one sphere processes input at a time, manages camera state,
/// and enforces single active UV projection.
/// </summary>
public static class HexasphereInputRouter
{
    private static readonly List<HexasphereNode> _registeredSpheres = new();
    private static HexasphereNode _uvProjectionOwner;
    private static Camera3D _managedCamera;
    private static bool _cameraDisabledByUs;
    
    // Cache for per-event arbitration result (avoids O(N²) on mouse motion)
    private static InputEvent _cachedEvent;
    private static HexasphereNode _cachedWinner;
    private static float _cachedHitDistance;

    public static void Register(HexasphereNode sphere)
    {
        if (!_registeredSpheres.Contains(sphere))
            _registeredSpheres.Add(sphere);
    }

    public static void Unregister(HexasphereNode sphere)
    {
        _registeredSpheres.Remove(sphere);
        if (_uvProjectionOwner == sphere)
        {
            // Delegate to the sphere's own close path — hides the projector UI,
            // exits UV mode (camera), and clears router ownership, all in one place.
            sphere.CloseUvProjectorFromRouter();
        }
    }

    /// <summary>
    /// Find which registered sphere is under the given screen position.
    /// Uses ray-sphere intersection for proper depth sorting.
    /// Caches result per InputEvent to avoid O(N²) on mouse motion.
    /// Returns the winner sphere and outputs the intersection distance.
    /// Returns null if no sphere is under the cursor.
    /// </summary>
    public static HexasphereNode FindSphereUnderCursor(InputEvent evt, Vector2 screenPos, Viewport viewport, out float hitDistance)
    {
        // Return cached result if same event (Godot dispatches same event to all nodes)
        if (ReferenceEquals(evt, _cachedEvent))
        {
            hitDistance = _cachedHitDistance;
            return _cachedWinner;
        }
        
        hitDistance = float.MaxValue;
        
        if (viewport == null) return null;

        var camera = viewport.GetCamera3D();
        if (camera == null) return null;

        HexasphereNode bestSphere = null;
        float bestWorldDist = float.MaxValue;

        for (int i = 0; i < _registeredSpheres.Count; i++)
        {
            var sphere = _registeredSpheres[i];
            if (!sphere.IsReady) continue;
            
            // Get ray-sphere intersection in world space for correct comparison
            if (sphere.TryGetRayIntersectionWorldDistance(screenPos, camera, out float worldDist))
            {
                // Prefer closest intersection (smallest world distance)
                if (worldDist < bestWorldDist)
                {
                    bestWorldDist = worldDist;
                    bestSphere = sphere;
                }
            }
        }
        
        // Cache result for this event
        _cachedEvent = evt;
        _cachedWinner = bestSphere;
        _cachedHitDistance = bestWorldDist;
        
        hitDistance = bestWorldDist;
        return bestSphere;
    }

    /// <summary>
    /// Request to open UV projection. Closes any existing projection first.
    /// Returns true if the request was granted.
    /// </summary>
    public static bool RequestUvProjection(HexasphereNode sphere)
    {
        if (_uvProjectionOwner != null && _uvProjectionOwner != sphere)
        {
            // Close existing projection
            _uvProjectionOwner.CloseUvProjectorFromRouter();
        }
        _uvProjectionOwner = sphere;
        return true;
    }

    public static void OnUvProjectionClosed(HexasphereNode sphere)
    {
        // If sphere is null (called from projector directly), always clear
        // If sphere is specified, only clear if it matches the current owner
        if (sphere == null || _uvProjectionOwner == sphere)
            _uvProjectionOwner = null;
    }

    public static bool IsUvProjectionOpen => _uvProjectionOwner != null;

    /// <summary>
    /// Notify that selection changed to a specific sphere.
    /// Clears selection from all other registered spheres.
    /// </summary>
    public static void NotifySelectionChanged(HexasphereNode selectedSphere)
    {
        for (int i = 0; i < _registeredSpheres.Count; i++)
        {
            var sphere = _registeredSpheres[i];
            if (sphere != selectedSphere)
                sphere.ClearSelection();
        }
    }

    /// <summary>
    /// Disable the shared Camera3D when entering UV mode.
    /// Called by the sphere that is opening UV projection.
    /// </summary>
    public static void EnterUvMode(Camera3D camera3D)
    {
        if (camera3D == null || !GodotObject.IsInstanceValid(camera3D)) return;
        _managedCamera = camera3D;
        camera3D.ProcessMode = Node.ProcessModeEnum.Disabled;
        camera3D.Current = false;
        _cameraDisabledByUs = true;
    }

    /// <summary>
    /// Restore the shared Camera3D when leaving UV mode.
    /// </summary>
    public static void ExitUvMode()
    {
        if (_managedCamera != null && _cameraDisabledByUs && GodotObject.IsInstanceValid(_managedCamera))
        {
            _managedCamera.ProcessMode = Node.ProcessModeEnum.Inherit;
            _managedCamera.Current = true;
        }
        _cameraDisabledByUs = false;
        _managedCamera = null;
    }
}
