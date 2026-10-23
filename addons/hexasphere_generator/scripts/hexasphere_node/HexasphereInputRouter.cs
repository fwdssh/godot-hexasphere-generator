using Godot;
using System.Collections.Generic;

/// <summary>Coordinates picking, selection and UV camera ownership within each viewport.</summary>
public static class HexasphereInputRouter
{
    private static readonly List<HexasphereNode> _registeredSpheres = new();
    private sealed class ViewportState
    {
        public HexasphereNode Owner;
        public Camera3D Camera;
        public long Revision;
        public long SelectionRevision;
        public InputEvent CachedEvent;
        public Camera3D CachedCamera;
        public Transform3D CameraTransform;
        public Projection CameraProjection;
        public Vector2 ScreenPosition;
        public Rect2 ViewRect;
        public PickCandidate[] Candidates;
        public HexasphereNode Winner;
        public float Distance;
    }
    private readonly record struct PickCandidate(HexasphereNode Sphere, Transform3D Transform, long Revision);
    private static readonly Dictionary<Viewport, ViewportState> _viewports = new();

    private static ViewportState State(Viewport viewport)
    {
        if (!_viewports.TryGetValue(viewport, out var state))
            _viewports[viewport] = state = new ViewportState();
        return state;
    }
    private static bool Alive(Node node) => GodotObject.IsInstanceValid(node) && node.IsInsideTree() && !node.IsQueuedForDeletion();

    public static void Register(HexasphereNode sphere)
    {
        if (!_registeredSpheres.Contains(sphere)) _registeredSpheres.Add(sphere);
        InvalidatePicking();
    }

    public static void Unregister(HexasphereNode sphere)
    {
        sphere.CloseUvProjectorFromRouter();
        _registeredSpheres.Remove(sphere);
        InvalidatePicking();
        foreach (var viewport in new List<Viewport>(_viewports.Keys))
        {
            var state = _viewports[viewport];
            if (state.Owner == sphere) Release(state);
            if (state.Owner == null && state.Camera == null) _viewports.Remove(viewport);
        }
    }

    public static bool IsEligible(HexasphereNode sphere, InputEvent evt, Viewport viewport)
    {
        if (!Alive(sphere) || !sphere.IsReady || !sphere.IsVisibleInTree() || !sphere.CanProcess()
            || !sphere.IsProcessingUnhandledInput() || sphere.GetViewport() != viewport) return false;
        return evt is InputEventMouseMotion ? sphere.IsHoverEnabled
            : (evt is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } || evt is InputEventKey)
                && sphere.IsClickEnabled;
    }

    public static HexasphereNode FindSphereUnderCursor(InputEvent evt, Vector2 screenPos, Viewport viewport, out float hitDistance)
    {
        hitDistance = float.MaxValue;
        if (!Alive(viewport) || IsUvProjectionOpenFor(viewport)) return null;
        var camera = viewport.GetCamera3D();
        if (!Alive(camera)) return null;
        var state = State(viewport);
        var candidates = new List<PickCandidate>();
        foreach (var sphere in _registeredSpheres.ToArray())
            if (IsEligible(sphere, evt, viewport))
                candidates.Add(new PickCandidate(sphere, sphere.GlobalTransform, sphere.InteractionRevision));
        bool sameCandidates = state.Candidates != null && state.Candidates.Length == candidates.Count;
        if (sameCandidates)
            for (int i = 0; i < candidates.Count; i++)
                if (state.Candidates[i] != candidates[i]) { sameCandidates = false; break; }
        var cameraTransform = camera.GetCameraTransform();
        var cameraProjection = camera.GetCameraProjection();
        var viewRect = viewport.GetVisibleRect();
        if (ReferenceEquals(evt, state.CachedEvent) && state.CachedCamera == camera
            && state.CameraTransform == cameraTransform && state.CameraProjection == cameraProjection
            && state.ScreenPosition == screenPos && state.ViewRect == viewRect && sameCandidates)
        {
            hitDistance = state.Distance;
            return state.Winner;
        }
        HexasphereNode winner = null;
        foreach (var candidate in candidates)
        {
            var sphere = candidate.Sphere;
            if (!IsEligible(sphere, evt, viewport)) continue;
            if (sphere.TryGetRayIntersectionWorldDistance(screenPos, camera, out float distance)
                && IsEligible(sphere, evt, viewport) && float.IsFinite(distance) && distance < hitDistance)
            {
                hitDistance = distance;
                winner = sphere;
            }
        }
        state.CachedEvent = evt;
        state.CachedCamera = camera;
        state.CameraTransform = cameraTransform;
        state.CameraProjection = cameraProjection;
        state.ScreenPosition = screenPos;
        state.ViewRect = viewRect;
        state.Candidates = candidates.ToArray();
        state.Winner = winner;
        state.Distance = hitDistance;
        return winner;
    }

    /// <summary>Call after custom picking state changes inside a public ray-intersection override.</summary>
    public static void InvalidatePicking()
    {
        foreach (var state in _viewports.Values)
        {
            state.CachedEvent = null;
            state.Candidates = null;
            state.Winner = null;
        }
    }

    public static bool RequestUvProjection(HexasphereNode sphere)
    {
        if (!Alive(sphere)) return false;
        var state = State(sphere.GetViewport());
        if (state.Owner == sphere) return true;
        state.CachedEvent = null;
        long revision = ++state.Revision;
        var previous = state.Owner;
        if (Alive(previous)) previous.CloseUvProjectorFromRouter();
        else Release(state);
        // Closing emits public signals: a nested request owns the newer decision.
        if (state.Revision != revision || !Alive(sphere)) return false;
        state.Owner = sphere;
        return true;
    }

    public static void OnUvProjectionClosed(HexasphereNode sphere)
    {
        if (sphere == null) return;
        foreach (var state in new List<ViewportState>(_viewports.Values))
            if (state.Owner == sphere) Release(state);
    }

    public static bool IsUvProjectionOpen
    {
        get
        {
            foreach (var viewport in new List<Viewport>(_viewports.Keys))
                if (IsUvProjectionOpenFor(viewport)) return true;
            return false;
        }
    }
    public static bool IsUvProjectionOpenFor(Viewport viewport)
    {
        if (viewport == null || !_viewports.TryGetValue(viewport, out var state)) return false;
        if (state.Owner != null && !Alive(state.Owner)) Release(state);
        if (state.Owner != null && (!state.Owner.IsReady || !state.Owner.IsVisibleInTree() || !state.Owner.CanProcess()))
            state.Owner.CloseUvProjectorFromRouter();
        return state.Owner != null;
    }
    public static bool IsUvProjectionOwner(HexasphereNode sphere)
    {
        if (!GodotObject.IsInstanceValid(sphere)) return false;
        foreach (var state in _viewports.Values) if (state.Owner == sphere) return true;
        return false;
    }

    public static void NotifySelectionChanged(HexasphereNode selectedSphere)
    {
        if (!Alive(selectedSphere)) return;
        var viewport = selectedSphere.GetViewport();
        var state = State(viewport);
        long selectionRevision = ++state.SelectionRevision;
        long generationRevision = selectedSphere.InteractionRevision;
        foreach (var sphere in _registeredSpheres.ToArray())
        {
            if (!Alive(selectedSphere) || !selectedSphere.IsReady
                || selectedSphere.InteractionRevision != generationRevision || state.SelectionRevision != selectionRevision) return;
            if (sphere != selectedSphere && Alive(sphere) && sphere.GetViewport() == viewport)
                sphere.ClearSelection();
        }
    }

    public static void EnterUvMode(Camera3D camera3D)
    {
        if (!Alive(camera3D)) return;
        var state = State(camera3D.GetViewport());
        if (state.Camera == camera3D) return;
        RestoreCamera(state);
        state.Camera = camera3D;
        camera3D.Current = false;
    }
    public static void ExitUvMode(HexasphereNode sphere)
    {
        foreach (var state in _viewports.Values)
            if (state.Owner == sphere) RestoreCamera(state);
    }
    /// <summary>Compatibility for callers using the old global camera entry point.</summary>
    public static void ExitUvMode()
    {
        foreach (var state in _viewports.Values) RestoreCamera(state);
    }
    private static void Release(ViewportState state)
    {
        state.Owner = null;
        state.CachedEvent = null;
        RestoreCamera(state);
    }
    private static void RestoreCamera(ViewportState state)
    {
        var camera = state.Camera;
        state.Camera = null;
        if (Alive(camera)) camera.MakeCurrent();
    }
}
