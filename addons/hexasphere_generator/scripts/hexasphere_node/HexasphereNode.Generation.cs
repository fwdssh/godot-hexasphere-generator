using Godot;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public partial class HexasphereNode
{
    [Signal] public delegate void GenerationStartedEventHandler();
    [Signal] public delegate void GenerationCancelledEventHandler();
    [Signal] public delegate void GenerationFailedEventHandler(string error);
    [Signal] public delegate void GenerationStateChangedEventHandler(int state);
    [Signal] public delegate void GenerationProgressChangedEventHandler(int stage, int completedStages, int totalStages);

    [ExportGroup("Generation")]
    /// <summary>Generate automatically in _Ready. Disable to start explicitly from code.</summary>
    [Export] public bool AutoGenerate { get => _shouldAutoGenerate; set => _shouldAutoGenerate = value; }
    /// <summary>Soft per-node budget for sphere/border uploads and default initial colors. Zero keeps synchronous setup.</summary>
    [Export(PropertyHint.Range, "0,16,0.5,or_greater")] public float MeshUploadBudgetMilliseconds { get; set; } = 4;
    /// <summary>Target cells per sphere surface and color batch; borders target six vertices per cell. Surface sizes grow to fit engine limits.</summary>
    [Export(PropertyHint.Range, "1,65536,1,or_greater")] public int MeshUploadTilesPerSurface { get; set; } = 8192;

    public HexasphereGenerationState GenerationState { get; private set; }
    public HexasphereGenerationStage GenerationStage { get; private set; }
    public string LastGenerationError { get; private set; } = "";
    public int CompletedGenerationStages { get; private set; }
    public const int TotalGenerationStages = 4;
    /// <summary>Cumulative main-thread mesh build/upload work; excludes waiting between frames and other stages.</summary>
    public double LastMeshBuildMilliseconds { get; private set; }
    public int CompletedMeshUploadSurfaces { get; private set; }
    public int TotalMeshUploadSurfaces { get; private set; }

    /// <summary>Captured base settings for the current extension hook.</summary>
    protected HexasphereGenerationSettings CurrentGenerationSettings =>
        _workerGeneration.Value?.Settings ?? _activeGeneration?.Settings;
    /// <summary>Check this in custom background loops to support cancellation between native calls.</summary>
    protected CancellationToken GenerationCancellationToken => _workerGeneration.Value?.Token ?? CancellationToken.None;

    private readonly object _generationLock = new();
    private readonly ConcurrentQueue<GenerationMessage> _generationMessages = new();
    private readonly AsyncLocal<GenerationRequest> _workerGeneration = new();
    private GenerationRequest _activeGeneration;
    private Task _generationTask = Task.CompletedTask;
    private long _generationSerial;
    private bool _generationInitialized;
    private bool _generationShuttingDown;
    private SceneTree _generationTree;
    private float _generatedRadius;

    private sealed class GenerationRequest
    {
        public readonly long Id;
        public readonly HexasphereGenerationSettings Settings;
        public readonly CancellationToken Token;
        private readonly CancellationTokenSource _cancellation = new();
        private bool _disposed;
        public bool WorkerStarted;
        public bool Published;
        public bool Finalizing;
        public bool ShaderReadyPending;
        public NativeHexasphere Native;
        public Vector3[] Centers;
        public ICellData[] Cells;
        public PreparedSpatialIndex SpatialIndex;
        public PlanetBorderRenderer.PreparedGeometry Borders;
        public int MeshChunksRemaining;
        public ArrayMesh UploadMesh;
        public NativeHexasphere UploadSource;
        public long UploadVersion;
        public Color[] PreparedColors;
        public int PreparedColorCount;

        public GenerationRequest(long id, HexasphereGenerationSettings settings)
        {
            Id = id; Settings = settings; Token = _cancellation.Token;
        }
        public void Cancel()
        {
            lock (_cancellation) { if (!_disposed) _cancellation.Cancel(); }
        }
        public void DisposeCancellation()
        {
            lock (_cancellation)
            {
                if (_disposed) return;
                _disposed = true;
                _cancellation.Dispose();
            }
        }
    }

    private sealed record GenerationMessage(GenerationRequest Request,
        HexasphereGenerationStage Stage, bool Completed = false, string Error = null);

    public int GetGenerationState() => (int)GenerationState;
    public int GetGenerationStage() => (int)GenerationStage;
    public bool IsGenerating() => GenerationState == HexasphereGenerationState.Generating;
    public string GetLastGenerationError() => LastGenerationError;
    public int GetCompletedGenerationStages() => CompletedGenerationStages;
    public int GetTotalGenerationStages() => TotalGenerationStages;
    public double GetLastMeshBuildMilliseconds() => LastMeshBuildMilliseconds;
    public int GetCompletedMeshUploadSurfaces() => CompletedMeshUploadSurfaces;
    public int GetTotalMeshUploadSurfaces() => TotalMeshUploadSurfaces;

    /// <summary>Starts from current settings, replacing an active request. Call from the main thread.</summary>
    public bool GeneratePlanet()
    {
        if (!_generationInitialized || _generationShuttingDown || !IsInsideTree() || IsQueuedForDeletion()) return false;
        long previous = _generationSerial;
        StartGeneration();
        return !_generationShuttingDown && IsInsideTree() && !IsQueuedForDeletion()
            && _generationSerial != previous && GenerationState != HexasphereGenerationState.Failed;
    }

    /// <summary>Restarts using current settings. Old work is cancelled without blocking the main thread.</summary>
    public bool RestartGeneration() => GeneratePlanet();

    /// <summary>Invalidates the active request. An in-progress native call finishes without being applied.</summary>
    public bool CancelGeneration()
    {
        if (!IsGenerating()) return false;
        long beforeCleanup = _generationSerial;
        CleanupResources();
        if (!CanContinueAfterCleanup(beforeCleanup)) return true;
        long serial = _generationSerial;
        SetGenerationState(HexasphereGenerationState.Cancelled);
        if (_generationSerial == serial)
            EmitSignal(SignalName.GenerationCancelled);
        return true;
    }

    public override void _EnterTree()
    {
        _generationShuttingDown = false;
        if (_generationInitialized)
        {
            InitializeGenerationLifecycle();
            if (!GodotObject.IsInstanceValid(VisualController)) SetVisualController();
            if (GodotObject.IsInstanceValid(UvProjector))
            {
                UvProjector.ProjectionClosed -= OnProjectionClosed;
                UvProjector.ProjectionClosed += OnProjectionClosed;
            }
        }
    }

    private void InitializeGenerationLifecycle()
    {
        _generationInitialized = true;
        HexasphereInputRouter.Register(this);
        var tree = GetTree();
        if (_generationTree == tree) return;
        if (GodotObject.IsInstanceValid(_generationTree)) _generationTree.ProcessFrame -= DrainGenerationMessages;
        _generationTree = tree;
        // Independent of derived _Process overrides and of the node's process/paused mode.
        _generationTree.ProcessFrame += DrainGenerationMessages;
    }

    private void ShutdownGenerationLifecycle()
    {
        lock (_generationLock) _generationShuttingDown = true;
        if (GodotObject.IsInstanceValid(_generationTree)) _generationTree.ProcessFrame -= DrainGenerationMessages;
        _generationTree = null;
        if (IsGenerating()) GenerationState = HexasphereGenerationState.Cancelled;
        else if (GenerationState == HexasphereGenerationState.Ready)
        {
            GenerationState = HexasphereGenerationState.NotStarted;
            GenerationStage = HexasphereGenerationStage.NotStarted;
            CompletedGenerationStages = 0;
        }
        // Invalidate even if a subclass customizes CleanupResources.
        InvalidateGenerationRequest();
    }

    private void InvalidateGenerationRequest()
    {
        lock (_generationLock)
        {
            _generationSerial++;
            var request = _activeGeneration;
            _activeGeneration = null;
            if (request != null)
            {
                request.Cancel();
                // Only published native data belongs to the main thread.
                // Running workers dispose their own native object after returning.
                if (request.Published)
                {
                    request.UploadMesh?.Dispose(); request.UploadMesh = null;
                    request.UploadSource = null;
                    request.Native?.Dispose(); request.Native = null;
                    request.Centers = null; request.Cells = null;
                    request.SpatialIndex = null; request.Borders = null;
                    request.PreparedColors = null;
                }
                if (!request.WorkerStarted) request.DisposeCancellation();
            }
            while (_generationMessages.TryDequeue(out _)) { }
        }
    }

    private void BeginGenerationRequest()
    {
        if (!_generationInitialized || _generationShuttingDown || !IsInsideTree() || IsQueuedForDeletion()) return;
        if (IsGenerating())
        {
            long previous = _generationSerial;
            CancelGeneration();
            // A cancellation signal handler may have started another request.
            if (_generationSerial != previous + 1 || _generationShuttingDown || !IsInsideTree()) return;
        }
        long beforeCleanup = _generationSerial;
        CleanupResources();
        if (!CanContinueAfterCleanup(beforeCleanup)) return;
        LastGenerationError = "";
        LastMeshBuildMilliseconds = 0;
        CompletedMeshUploadSurfaces = 0;
        TotalMeshUploadSurfaces = 0;
        GenerationStage = HexasphereGenerationStage.NotStarted;
        CompletedGenerationStages = 0;
        GenerationRequest request;
        try
        {
            var settings = new HexasphereGenerationSettings(this);
            if (!float.IsFinite(settings.Radius) || settings.Radius <= 0)
                throw new ArgumentOutOfRangeException(nameof(PlanetRadius), "PlanetRadius must be finite and positive.");
            if (settings.Subdivision < 1 || 10.0 * settings.Subdivision * settings.Subdivision + 2 > int.MaxValue / 18)
                throw new ArgumentOutOfRangeException(nameof(SubDivision), "SubDivision must be positive and fit the mesh's 32-bit vertex counts.");
            if (!float.IsFinite(settings.HexSize) || settings.HexSize < 0.1f || settings.HexSize > 1)
                throw new ArgumentOutOfRangeException(nameof(HexSize), "HexSize must be finite and within 0.1..1.");
            if (!float.IsFinite(settings.MeshUploadBudgetMilliseconds) || settings.MeshUploadBudgetMilliseconds < 0
                || settings.MeshUploadTilesPerSurface < 1)
                throw new ArgumentOutOfRangeException(nameof(MeshUploadBudgetMilliseconds), "Supply a finite nonnegative upload budget and positive tiles per surface.");
            if (settings.ColorsShader == null || (settings.Borders && settings.BordersShader == null))
                throw new InvalidOperationException("Supply the color shader and, when borders are enabled, the border shader.");
            lock (_generationLock)
                _activeGeneration = request = new GenerationRequest(++_generationSerial, settings);
        }
        catch (Exception error) { FailGeneration(null, error); return; }

        SetGenerationState(HexasphereGenerationState.Generating);
        if (!IsCurrentGeneration(request)) return;
        EmitSignal(SignalName.GenerationStarted);
        if (!ReportGenerationStage(request, HexasphereGenerationStage.GeneratingTopology)) return;
        request.WorkerStarted = true;
        var previousTask = _generationTask;
        // Serialize each node's custom background hooks. Cancelled queued requests skip their work.
        _generationTask = Task.Run(async () =>
        {
            try
            {
                await previousTask.ConfigureAwait(false);
                request.Token.ThrowIfCancellationRequested();
                _workerGeneration.Value = request;
                request.Native = new NativeHexasphere();
                GenerateInBackground(request.Native);
                request.Token.ThrowIfCancellationRequested();
                if (request.Centers == null || request.Cells == null)
                    throw new InvalidOperationException("GenerateInBackground overrides must call base or PublishGenerationResult.");
                // Prepare request-owned CPU data without touching live node/controller state.
                // Run after the complete custom hook, including custom published results.
                if (request.Settings.MeshUploadBudgetMilliseconds > 0
                    && request.Centers.Length > request.Settings.MeshUploadTilesPerSurface)
                    request.MeshChunksRemaining = request.Native.PrepareMeshChunks(request.Settings.MeshUploadTilesPerSurface);
                if (request.MeshChunksRemaining == 0) request.Native.PrepareMesh();
                request.Token.ThrowIfCancellationRequested();
                request.SpatialIndex = PrepareSpatialIndex(request.Centers, request.Token);
                if (request.Settings.Borders)
                    request.Borders = PlanetBorderRenderer.PrepareGeometry(request.Native, request.Token,
                        request.Settings.MeshUploadBudgetMilliseconds > 0
                            ? (int)Math.Min((long)request.Settings.MeshUploadTilesPerSurface * 6, int.MaxValue) : int.MaxValue);
                request.Token.ThrowIfCancellationRequested();
                lock (_generationLock)
                {
                    if (_generationShuttingDown || _activeGeneration != request) return;
                    request.Published = true;
                    _generationMessages.Enqueue(new GenerationMessage(request, HexasphereGenerationStage.BuildingMesh, Completed: true));
                }
            }
            catch (OperationCanceledException) when (request.Token.IsCancellationRequested) { }
            catch (Exception error)
            {
                lock (_generationLock)
                    if (!_generationShuttingDown && _activeGeneration == request)
                        _generationMessages.Enqueue(new GenerationMessage(request, HexasphereGenerationStage.NotStarted, Error: error.Message));
            }
            finally
            {
                _workerGeneration.Value = null;
                if (!request.Published)
                {
                    request.Native?.Dispose(); request.Native = null;
                    request.Centers = null; request.Cells = null;
                    request.SpatialIndex = null; request.Borders = null;
                }
                request.DisposeCancellation();
            }
        });
    }

    private bool IsCurrentGeneration(GenerationRequest request) => request != null
        && !_generationShuttingDown && _activeGeneration == request
        && !request.Token.IsCancellationRequested && IsInsideTree() && !IsQueuedForDeletion();

    private bool CanContinueAfterCleanup(long previousSerial) => _generationSerial == previousSerial + 1
        && !_generationShuttingDown && IsInsideTree() && !IsQueuedForDeletion();

    private void GenerateRequestInBackground(NativeHexasphere native)
    {
        var request = _workerGeneration.Value ?? throw new InvalidOperationException("Start generation through GeneratePlanet().");
        var settings = request.Settings;
        request.Token.ThrowIfCancellationRequested();
        native.Generate(settings.Radius, settings.Subdivision, settings.HexSize);
        request.Token.ThrowIfCancellationRequested();
        int count = native.GetTileCount();
        request.Centers = native.GetAllTileCenters();
        lock (_generationLock)
            if (!_generationShuttingDown && _activeGeneration == request)
                _generationMessages.Enqueue(new GenerationMessage(request, HexasphereGenerationStage.CreatingCellData));
        var cells = CreateCellData(count, request.Centers);
        request.Token.ThrowIfCancellationRequested();
        if (cells == null || cells.Length != count)
            throw new InvalidOperationException($"CreateCellData must return exactly {count} cells.");
        PublishGenerationResult(native, request.Centers, cells);
    }

    /// <summary>
    /// Publishes a fully custom GenerateInBackground result. Call on that request's worker,
    /// using its supplied native instance; the lifecycle marshals it to the main thread.
    /// </summary>
    protected void PublishGenerationResult(NativeHexasphere native, Vector3[] centers, ICellData[] cells)
    {
        var request = _workerGeneration.Value ?? throw new InvalidOperationException("No background request is active on this thread.");
        request.Token.ThrowIfCancellationRequested();
        if (native != request.Native || centers == null || cells == null || centers.Length != cells.Length
            || cells.Length == 0 || cells.Length != native.GetTileCount())
            throw new ArgumentException("Publish the request's native instance and exactly one center/data item per generated tile.");
        request.Centers = centers;
        request.Cells = cells;
    }

    private void DrainGenerationMessages()
    {
        while (_generationMessages.TryDequeue(out var message))
        {
            var request = message.Request;
            if (!IsCurrentGeneration(request)) continue;
            try
            {
                if (message.Error != null) { FailGeneration(request, new InvalidOperationException(message.Error)); continue; }
                if (!ReportGenerationStage(request, message.Stage) || !message.Completed) continue;
                if (request.MeshChunksRemaining > 0)
                {
                    request.UploadMesh = new ArrayMesh();
                    request.UploadSource = request.Native;
                    request.UploadVersion = request.Native.GeometryVersion;
                    TotalMeshUploadSurfaces = request.MeshChunksRemaining;
                }
                else FinalizePublishedGeneration(request);
            }
            catch (Exception error) { FailGeneration(request, error); }
        }
        var active = _activeGeneration;
        if (IsCurrentGeneration(active) && active.PreparedColors != null)
        {
            try { AdvanceGenerationColors(active); }
            catch (Exception error) { FailGeneration(active, error); }
            return;
        }
        if (IsCurrentGeneration(active) && active.UploadMesh != null && active.Native != null)
        {
            try
            {
                if (active.MeshChunksRemaining > 0) AdvanceMeshUpload(active);
                else FinalizePublishedGeneration(active);
            }
            catch (Exception error) { FailGeneration(active, error); }
        }
    }

    private void FinalizePublishedGeneration(GenerationRequest request)
    {
        // Preserve the complete main-thread finalization extension point after uploads.
        _pendingHexasphere = request.Native;
        request.Native = null;
        _pendingCenters = request.Centers;
        _pendingCellDatas = request.Cells;
        request.Centers = null;
        request.Cells = null;
        request.Finalizing = true;
        try { FinalizePlanet(); }
        finally
        {
            request.Finalizing = false;
            // Custom finalizers may supply their own mesh without calling base.
            request.UploadMesh?.Dispose(); request.UploadMesh = null;
            request.UploadSource = null;
        }
        if (request.ShaderReadyPending && IsCurrentGeneration(request)) OnGenerationShaderReady();
    }

    private void AdvanceMeshUpload(GenerationRequest request)
    {
        var frame = Stopwatch.StartNew();
        do
        {
            var chunk = Stopwatch.StartNew();
            request.MeshChunksRemaining = request.Native.AppendMeshChunk(request.UploadMesh);
            double elapsed = chunk.Elapsed.TotalMilliseconds;
            LastMeshBuildMilliseconds += elapsed;
            CompletedMeshUploadSurfaces++;
            if (!IsCurrentGeneration(request)) return;
            if (request.MeshChunksRemaining == 0)
            {
                // Material/border/color work starts on a later frame, rather than
                // accumulating on top of the last surface upload.
                return;
            }
            // Leave room for another similarly sized chunk; one API call itself
            // cannot be interrupted, so this is a soft budget rather than a cap.
            if (frame.Elapsed.TotalMilliseconds + elapsed >= request.Settings.MeshUploadBudgetMilliseconds) return;
        } while (IsCurrentGeneration(request));
    }

    private void FinalizeGenerationRequest()
    {
        var request = _activeGeneration;
        if (!IsCurrentGeneration(request) || _pendingHexasphere == null) return;
        _hexasphere = _pendingHexasphere;
        _generatedRadius = request.Settings.Radius;
        _cellDatas = _pendingCellDatas;
        var centers = _pendingCenters;
        _pendingHexasphere = null;
        _pendingCenters = null;
        _pendingCellDatas = null;
        if (!GodotObject.IsInstanceValid(VisualController))
            throw new InvalidOperationException("The visual controller was removed during generation.");
        ArrayMesh mesh;
        if (request.UploadMesh != null && ReferenceEquals(request.UploadSource, _hexasphere)
            && request.UploadVersion == _hexasphere.GeometryVersion)
        {
            mesh = request.UploadMesh;
            request.UploadMesh = null;
        }
        else
        {
            request.UploadMesh?.Dispose(); request.UploadMesh = null;
            var clock = Stopwatch.StartNew();
            using var result = _hexasphere.BuildMesh();
            LastMeshBuildMilliseconds += clock.Elapsed.TotalMilliseconds;
            mesh = (ArrayMesh)result["mesh"];
        }
        request.UploadSource = null;
        try
        {
            BuildSpatialIndex(centers);
            request.SpatialIndex = null;
            if (!ReportGenerationStage(request, HexasphereGenerationStage.PreparingVisuals)) { mesh.Dispose(); return; }
            var settings = request.Settings;
            VisualController.SetNativeHexasphere(_hexasphere);
            VisualController.ShaderReady += OnGenerationShaderReady;
            VisualController.ShaderInitializationFailed += OnGenerationShaderFailed;
            VisualController.SetPreparedBorders(request.Borders);
            request.Borders = null;
            VisualController.SetUploadBudget(settings.MeshUploadBudgetMilliseconds);
            VisualController.ApplyGenerated(mesh, settings.Borders, settings.ColorsShader, settings.BordersShader);
            if (IsCurrentGeneration(request)) VisualController.SetBorderColor(settings.BorderColor);
        }
        catch { mesh.Dispose(); throw; }
    }

    private void OnGenerationShaderReady()
    {
        var request = _activeGeneration;
        if (!IsCurrentGeneration(request) || !IsGenerating()) return;
        // Custom controllers may emit synchronously from ApplyGenerated. Finish
        // only after the complete finalization hook has returned successfully.
        if (request.Finalizing) { request.ShaderReadyPending = true; return; }
        VisualController.ShaderReady -= OnGenerationShaderReady;
        VisualController.ShaderInitializationFailed -= OnGenerationShaderFailed;
        try
        {
            // Preserve synchronous base-call semantics inside custom ready/draw
            // hooks. GetColor remains virtual and runs on the main thread.
            if (request.Settings.MeshUploadBudgetMilliseconds > 0
                && _cellDatas.Length > request.Settings.MeshUploadTilesPerSurface
                && GetType().GetMethod(nameof(OnShaderReady), System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null)?.DeclaringType == typeof(HexasphereNode)
                && VisualController.CanPrepareColorsIncrementally)
                request.PreparedColors = new Color[_cellDatas.Length];
            else FinishGenerationVisuals(request);
        }
        catch (Exception error) { FailGeneration(request, error); }
    }

    private void AdvanceGenerationColors(GenerationRequest request)
    {
        if (!GodotObject.IsInstanceValid(VisualController) || !VisualController.IsInsideTree())
            throw new InvalidOperationException("The visual controller was removed during color preparation.");
        if (request.PreparedColorCount == request.PreparedColors.Length)
        {
            // Upload the packed texture and invoke the ready hook on a fresh frame.
            FinishGenerationVisuals(request);
            return;
        }
        var frame = Stopwatch.StartNew();
        do
        {
            var chunk = Stopwatch.StartNew();
            int count = Math.Min(request.Settings.MeshUploadTilesPerSurface,
                request.PreparedColors.Length - request.PreparedColorCount);
            int end = request.PreparedColorCount + count;
            for (int i = request.PreparedColorCount; i < end; i++)
            {
                Color color = VisualController.GetColor(_cellDatas[i]);
                // A custom color hook may cancel, restart or remove the node.
                if (!IsCurrentGeneration(request)) return;
                request.PreparedColors[i] = color;
                VisualController.PrepareTileColor(i, color);
            }
            request.PreparedColorCount = end;
            if (end == request.PreparedColors.Length) return;
            if (frame.Elapsed.TotalMilliseconds + chunk.Elapsed.TotalMilliseconds >= request.Settings.MeshUploadBudgetMilliseconds) return;
        } while (IsCurrentGeneration(request));
    }

    private void FinishGenerationVisuals(GenerationRequest request)
    {
        OnShaderReady();
        if (IsCurrentGeneration(request)) CompleteGenerationRequest();
    }

    private void OnGenerationShaderFailed(string error) => FailGeneration(_activeGeneration, new InvalidOperationException(error));

    private void PrepareGenerationColors()
    {
        var request = _activeGeneration;
        if (!IsCurrentGeneration(request)) return;
        if (request.PreparedColors != null) VisualController.UsePreparedColors(_cellDatas);
        VisualController.DrawColors(_cellDatas);
        if (!IsCurrentGeneration(request)) return;
        VisualController.SetEmissive(request.Settings.Emissive);
        if (!IsCurrentGeneration(request)) return;
        if (request.PreparedColors != null)
        {
            _tileColors = request.PreparedColors;
            request.PreparedColors = null;
        }
        else
        {
            _tileColors = new Color[_cellDatas.Length];
            for (int i = 0; i < _tileColors.Length; i++) _tileColors[i] = VisualController.GetColor(_cellDatas[i]);
        }
    }

    private void CompleteGenerationRequest()
    {
        var request = _activeGeneration;
        if (!IsCurrentGeneration(request)) return;
        if (_tileColors == null)
            throw new InvalidOperationException("OnShaderReady overrides must call base or prepare the tile color cache.");
        if (!ReportGenerationStage(request, HexasphereGenerationStage.Ready)) return;
        _planetReady = true;
        SetGenerationState(HexasphereGenerationState.Ready);
        if (!IsCurrentGeneration(request)) return;
        EmitSignal(SignalName.PlanetGenerated, _cellDatas.Length);
        if (_activeGeneration == request) _activeGeneration = null;
    }

    private void FailGeneration(GenerationRequest request, Exception error)
    {
        if (request != null && (!IsCurrentGeneration(request) || !IsGenerating())) return;
        if (_generationShuttingDown || !IsInsideTree()) return;
        long beforeCleanup = _generationSerial;
        CleanupResources();
        if (!CanContinueAfterCleanup(beforeCleanup)) return;
        LastGenerationError = error.Message;
        long serial = _generationSerial;
        SetGenerationState(HexasphereGenerationState.Failed);
        if (_generationSerial == serial) EmitSignal(SignalName.GenerationFailed, LastGenerationError);
    }

    private void SetGenerationState(HexasphereGenerationState state)
    {
        if (GenerationState == state) return;
        GenerationState = state;
        EmitSignal(SignalName.GenerationStateChanged, (int)state);
    }

    private bool ReportGenerationStage(GenerationRequest request, HexasphereGenerationStage stage)
    {
        if (!IsCurrentGeneration(request)) return false;
        GenerationStage = stage;
        CompletedGenerationStages = stage == HexasphereGenerationStage.Ready ? TotalGenerationStages : (int)stage - 1;
        EmitSignal(SignalName.GenerationProgressChanged, (int)stage, CompletedGenerationStages, TotalGenerationStages);
        return IsCurrentGeneration(request);
    }
}
