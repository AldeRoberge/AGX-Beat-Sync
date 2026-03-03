using System;
using System.Collections.Generic;
using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Drawing;

namespace AGX_Beat_Sync.UI;

/// <summary>One spawned entity in the game view (from a Spawn Entity event).</summary>
public class SpawnedEntity
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float SpawnTime;
    /// <summary>Lifetime in seconds before removal.</summary>
    public float Lifetime { get; set; } = 5f;

    /// <summary>If false, entity is a small cube (stationary); if true, projectile with velocity.</summary>
    public bool IsProjectile { get; set; } = true;

    // Direction pattern (Linear = use Velocity as-is)
    public ProjectileDirectionPattern DirectionPattern { get; set; } = ProjectileDirectionPattern.Linear;
    public Vector3 InitialDirection { get; set; }
    public float Speed { get; set; }
    public float OscillationAmplitude { get; set; }
    public float OrbitingDistance { get; set; }
    public Vector3 OrbitCenter { get; set; }
    /// <summary>Orbit axes (only for Orbiting). OrbitRight and tangent at spawn.</summary>
    public Vector3 OrbitRight { get; set; }
    public Vector3 OrbitTangent { get; set; }
    /// <summary>Current orbit angle in radians (only for Orbiting). Advanced by frame dt for smooth motion.</summary>
    public float OrbitAngle { get; set; }
    /// <summary>Spawn position (for Boomerang: ease out and back to this line).</summary>
    public Vector3 SpawnPosition { get; set; }
}

public class GameViewPanel : PanelBase
{
    public Input.InputManager? Input { get; set; }
    public Project? Project { get; set; }
    public Transport? Transport { get; set; }
    public GraphicsDevice? GraphicsDevice { get; set; }
    /// <summary>Four-frame sprite (0=right, 1=down, 2=left, 3=up). Set from game LoadContent.</summary>
    public Texture2D? PlayerTexture { get; set; }

    private readonly List<SpawnedEntity> _spawnedEntities = new();
    private readonly GameViewPlayer _player = new();
    private readonly GameViewOrbitCamera _camera = new();

    // Screenshake: when an event fires, shake decays over Duration
    private float _screenshakeRemaining;
    private float _screenshakeDuration;
    private float _screenshakeAmplitude;
    private Vector2 _shakeOffset;
    private static readonly Random s_shakeRandom = new();

    /// <summary>Current weather (Rain or Sunny). Set by Change Weather events.</summary>
    private WeatherKind _weather = WeatherKind.Sunny;

    /// <summary>Accumulated real time for rain animation. Updated every frame for smooth 60+ fps rain.</summary>
    private double _rainAnimationTime;

    /// <summary>Current tile shape (Circle, Square, Line, Cone). Set by Change Tiles events.</summary>
    private ChangeTilesShape _tileShape = ChangeTilesShape.Circle;

    /// <summary>Active dialogue bubble: text and time (TotalGameTime) until it hides. Null when no bubble.</summary>
    private (string Text, double ShowUntilTime)? _dialogueBubble;

    /// <summary>Cached bold text texture for the current dialogue text. Disposed when text changes.</summary>
    private Texture2D? _dialogueTextTexture;
    private string _dialogueCachedText = "";

    /// <summary>Set the weather. Called when a Change Weather event fires.</summary>
    public void SetWeather(WeatherKind kind)
    {
        _weather = kind;
    }

    /// <summary>Set the tile shape (Circle, Square, Line, Cone). Called when a Change Tiles event fires.</summary>
    public void SetTileShape(ChangeTilesShape shape)
    {
        _tileShape = shape;
    }

    /// <summary>Show a dialogue chat bubble above the enemy. Duration in seconds. Called when a Dialogue event fires.</summary>
    public void ShowDialogueBubble(string text, double durationSeconds)
    {
        double now = Transport?.CurrentTime ?? 0;
        _dialogueBubble = (text ?? "", now + durationSeconds);
    }

    /// <summary>Clear the dialogue bubble (e.g. when seeking backward).</summary>
    public void ClearDialogueBubble()
    {
        _dialogueBubble = null;
    }

    /// <summary>Spawn an entity at the given world position with velocity (direction * speed). Called when a Spawn Entity event fires.</summary>
    public void SpawnEntity(Vector3 position, Vector3 rotationEulerRadians, float speed, float lifetime = 5f)
    {
        var rot = Matrix.CreateRotationX(rotationEulerRadians.X) * Matrix.CreateRotationY(rotationEulerRadians.Y) * Matrix.CreateRotationZ(rotationEulerRadians.Z);
        var forward = Vector3.Transform(-Vector3.UnitZ, rot);
        if (forward.LengthSquared() > 0.0001f)
            forward.Normalize();
        SpawnEntityWithDirection(position, forward, speed, lifetime);
    }

    /// <summary>Spawn an entity with a world-space direction (normalized or not). Used for pattern bursts.</summary>
    public void SpawnEntityWithDirection(Vector3 position, Vector3 direction, float speed, float lifetime = 5f)
    {
        SpawnEntityWithDirection(position, direction, speed, lifetime, ProjectileDirectionPattern.Linear, 0f, 0f, isProjectile: true);
    }

    /// <summary>Spawn an entity with optional direction pattern (Oscillation, Orbiting). When isProjectile is false (small cube), speed is ignored and entity is stationary.</summary>
    public void SpawnEntityWithDirection(Vector3 position, Vector3 direction, float speed, float lifetime,
        ProjectileDirectionPattern directionPattern, float oscillationAmplitude, float orbitingDistance, bool isProjectile = true)
    {
        var d = direction;
        if (d.LengthSquared() < 1e-8f)
            d = -Vector3.UnitZ;
        else
            d.Normalize();

        float effectiveSpeed = isProjectile ? speed : 0f;

        if (isProjectile && directionPattern == ProjectileDirectionPattern.Orbiting && orbitingDistance > 0.001f)
        {
            var radius = Math.Max(0.001f, orbitingDistance);
            var right = Vector3.Cross(d, Vector3.UnitY);
            if (right.LengthSquared() < 1e-10f) right = Vector3.UnitX;
            else right.Normalize();
            var tangent = Vector3.Cross(Vector3.UnitY, right);
            if (Vector3.Dot(tangent, d) < 0) tangent = -tangent;
            tangent.Normalize();
            var startPos = position + right * radius;
            _spawnedEntities.Add(new SpawnedEntity
            {
                Position = startPos,
                Velocity = tangent * effectiveSpeed,
                SpawnTime = (float)(Transport?.CurrentTime ?? 0),
                Lifetime = lifetime,
                IsProjectile = true,
                DirectionPattern = directionPattern,
                Speed = effectiveSpeed,
                OrbitingDistance = radius,
                OrbitCenter = position,
                OrbitRight = right,
                OrbitTangent = tangent
            });
        }
        else if (isProjectile && directionPattern == ProjectileDirectionPattern.Oscillation)
        {
            _spawnedEntities.Add(new SpawnedEntity
            {
                Position = position,
                Velocity = d * effectiveSpeed,
                SpawnTime = (float)(Transport?.CurrentTime ?? 0),
                Lifetime = lifetime,
                IsProjectile = true,
                DirectionPattern = directionPattern,
                InitialDirection = d,
                Speed = effectiveSpeed,
                OscillationAmplitude = Math.Abs(oscillationAmplitude)
            });
        }
        else
        {
            // Linear, Boomerang, or non-projectile: explicit pattern and zero oscillation/orbit
            var entity = new SpawnedEntity
            {
                Position = position,
                Velocity = d * effectiveSpeed,
                SpawnTime = (float)(Transport?.CurrentTime ?? 0),
                Lifetime = lifetime,
                IsProjectile = isProjectile,
                DirectionPattern = isProjectile ? directionPattern : ProjectileDirectionPattern.Linear,
                InitialDirection = d,
                Speed = effectiveSpeed,
                OscillationAmplitude = 0f,
                OrbitingDistance = 0f
            };
            if (isProjectile && directionPattern == ProjectileDirectionPattern.Boomerang)
                entity.SpawnPosition = position;
            _spawnedEntities.Add(entity);
        }
    }

    /// <summary>Spawn an entity that moves from position toward a target world position (e.g. player).</summary>
    public void SpawnEntityTowards(Vector3 position, Vector3 targetWorldPosition, float speed, float lifetime = 5f)
    {
        var toTarget = targetWorldPosition - position;
        if (toTarget.LengthSquared() < 1e-8f)
            toTarget = -Vector3.UnitZ;
        else
            toTarget.Normalize();
        _spawnedEntities.Add(new SpawnedEntity
        {
            Position = position,
            Velocity = toTarget * speed,
            SpawnTime = (float)(Transport?.CurrentTime ?? 0),
            Lifetime = lifetime
        });
    }

    /// <summary>Current player world position (for Towards mode).</summary>
    public Vector3 GetPlayerPosition()
    {
        var (target, _, _, _) = GetCameraState();
        return target;
    }

    public override string? GetHoverText(Point mouse)
    {
        if (!ContainsPoint(mouse)) return null;
        var content = ContentBounds;
        var viewportRect = new Rectangle(content.X, content.Y, content.Width, Math.Max(0, content.Height));
        if (viewportRect.Contains(mouse))
            return "Game View — WASD move player. Middle-drag: orbit. Q/E: orbit yaw. Scroll: zoom";
        return "Game View";
    }

    /// <summary>Clear all spawned entities and reset enemy state (e.g. when user seeks or stops).</summary>
    public void ClearSpawnedEntities()
    {
        _spawnedEntities.Clear();
        _screenshakeRemaining = 0f;
        _enemyCubePosition = Vector3.Zero;
        _enemyMovementKind = EntityMovementKind.Stationary;
    }

    /// <summary>Trigger a screenshake (called when a Screenshake event fires). New shake overwrites any current shake.</summary>
    public void TriggerScreenshake(float amplitude, float duration)
    {
        _screenshakeAmplitude = Math.Max(0f, amplitude);
        _screenshakeDuration = Math.Max(0.001f, duration);
        _screenshakeRemaining = _screenshakeDuration;
    }

    /// <summary>True while right-drag look is active; game should hide cursor.</summary>
    public bool IsCapturingMouse => _camera.IsCapturingMouse;

    /// <summary>Get current camera state for persistence (target = player position, orbit yaw/pitch/distance).</summary>
    public (Vector3 target, float orbitYaw, float orbitPitch, float orbitDistance) GetCameraState()
    {
        return (_player.Position, _camera.OrbitYaw, _camera.OrbitPitch, _camera.Distance);
    }

    /// <summary>Restore camera state from saved session (player position and orbit).</summary>
    public void SetCameraState(Vector3 target, float orbitYaw, float orbitPitch, float orbitDistance)
    {
        _player.SetPosition(target);
        _camera.Target = _player.Position;
        _camera.SetOrbitState(orbitYaw, orbitPitch, orbitDistance);
    }

    private Matrix _lastView;
    private Matrix _lastProjection;
    private int _lastViewportW;
    private int _lastViewportH;

    private RenderTarget2D? _renderTarget;
    private BasicEffect? _effect;
    private VertexBuffer? _cubeBuffer;
    private IndexBuffer? _cubeIndices;
    private VertexBuffer? _projectileBuffer;
    private IndexBuffer? _projectileIndices;
    private VertexBuffer? _planeBuffer;
    private VertexBuffer? _gridBuffer;
    private int _gridLineCount;
    private Color _enemyCubeColor = new Color(200, 80, 80);

    /// <summary>Enemy cube position. Updated by movement logic.</summary>
    private Vector3 _enemyCubePosition = Vector3.Zero;
    /// <summary>Enemy cube movement mode. Set by Change Entity Movement events.</summary>
    private EntityMovementKind _enemyMovementKind = EntityMovementKind.Stationary;
    private float _enemyOrbitAngle;
    private float _enemyWanderPhase;
    private static readonly Random s_enemyWanderRandom = new();

    /// <summary>Set the entity (enemy cube) color. Called when a Change Entity Color event fires.</summary>
    public void SetEnemyCubeColor(Color color)
    {
        _enemyCubeColor = color;
        if (_cubeBuffer != null)
            RebuildEnemyCubeBuffer();
    }

    /// <summary>Set the entity (enemy cube) movement mode. Called when a Change Entity Movement event fires.</summary>
    public void SetEnemyMovement(EntityMovementKind kind)
    {
        _enemyMovementKind = kind;
    }

    private VertexPositionColor[] BuildEnemyCubeVertices(float h)
    {
        var c = _enemyCubeColor;
        byte r = c.R, g = c.G, b = c.B;
        var dark = new Color((byte)Math.Max(0, r - 40), (byte)Math.Max(0, g - 40), (byte)Math.Max(0, b - 40));
        var mid = new Color((byte)Math.Min(255, r + 10), (byte)Math.Min(255, g + 10), (byte)Math.Min(255, b + 10));
        var bright = new Color((byte)Math.Min(255, r + 40), (byte)Math.Min(255, g + 40), (byte)Math.Min(255, b + 40));
        return new VertexPositionColor[]
        {
            new(new Vector3(-h, -h, -h), dark),
            new(new Vector3( h, -h, -h), dark),
            new(new Vector3( h,  h, -h), mid),
            new(new Vector3(-h,  h, -h), mid),
            new(new Vector3(-h, -h,  h), mid),
            new(new Vector3( h, -h,  h), mid),
            new(new Vector3( h,  h,  h), bright),
            new(new Vector3(-h,  h,  h), bright)
        };
    }

    private void RebuildEnemyCubeBuffer()
    {
        var device = GraphicsDevice;
        if (device == null || _cubeBuffer == null) return;
        var verts = BuildEnemyCubeVertices(0.5f);
        _cubeBuffer.SetData(verts);
    }

    public GameViewPanel()
    {
        Title = "Game Preview";
        BackgroundColor = new Color(24, 26, 30);
    }

    /// <summary>Call before Draw(spriteBatch) to render 3D scene into the panel's render target.</summary>
    public void Draw3DScene()
    {
        var device = GraphicsDevice;
        if (device == null)
            return;

        var content = ContentBounds;
        int w = Math.Max(1, content.Width);
        int h = Math.Max(1, content.Height);
        if (h <= 0) h = 1;

        if (_renderTarget == null || _renderTarget.Width != w || _renderTarget.Height != h)
        {
            _renderTarget?.Dispose();
            _renderTarget = new RenderTarget2D(device, w, h, false, SurfaceFormat.Color, DepthFormat.Depth24);
        }

        Ensure3DResources(device);

        var prevTargets = device.GetRenderTargets();
        device.SetRenderTarget(_renderTarget);
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Color(20, 22, 26), 1f, 0);

        if (_effect != null && _cubeBuffer != null && _planeBuffer != null)
        {
            float aspect = (float)w / h;
            _lastProjection = Matrix.CreatePerspectiveFieldOfView(MathF.PI / 4f, aspect, 0.1f, 1000f);
            _camera.Target = _player.Position;
            Matrix baseView = _camera.GetViewMatrix();
            _lastView = _screenshakeRemaining > 0
                ? Matrix.CreateTranslation(_shakeOffset.X, _shakeOffset.Y, 0f) * baseView
                : baseView;
            _lastViewportW = w;
            _lastViewportH = h;
            _effect.Projection = _lastProjection;
            _effect.View = _lastView;
            _effect.World = Matrix.Identity;
            _effect.CurrentTechnique.Passes[0].Apply();

            device.SetVertexBuffer(_planeBuffer);
            device.Indices = null;
            device.DrawPrimitives(PrimitiveType.TriangleList, 0, 2);

            if (_gridBuffer != null)
            {
                device.SetVertexBuffer(_gridBuffer);
                device.DrawPrimitives(PrimitiveType.LineList, 0, _gridLineCount);
            }

            // Enemy cube (position updated by movement logic)
            _effect.World = Matrix.CreateTranslation(_enemyCubePosition);
            _effect.CurrentTechnique.Passes[0].Apply();
            device.SetVertexBuffer(_cubeBuffer);
            device.Indices = _cubeIndices;
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 12);

            foreach (var e in _spawnedEntities)
            {
                var dir = e.Velocity;
                if (dir.LengthSquared() < 1e-8f) dir = e.InitialDirection;
                if (dir.LengthSquared() < 1e-8f) dir = -Vector3.UnitZ;
                else dir.Normalize();
                var rot = RotationFromForward(-Vector3.UnitZ, dir);
                if (e.IsProjectile)
                {
                    _effect.World = rot * Matrix.CreateTranslation(e.Position);
                    _effect.CurrentTechnique.Passes[0].Apply();
                    device.SetVertexBuffer(_projectileBuffer);
                    device.Indices = _projectileIndices;
                    device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 12);
                }
                else
                {
                    const float smallCubeScale = 0.25f;
                    _effect.World = rot * Matrix.CreateScale(smallCubeScale) * Matrix.CreateTranslation(e.Position);
                    _effect.CurrentTechnique.Passes[0].Apply();
                    device.SetVertexBuffer(_cubeBuffer);
                    device.Indices = _cubeIndices;
                    device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 12);
                }
            }

            if (_weather == WeatherKind.Rain)
                DrawRain(device);
        }

        device.SetRenderTargets(prevTargets);
    }

    /// <summary>Draw rain as falling line segments in front of the camera. Uses per-frame animation time for smooth 60+ fps.</summary>
    private void DrawRain(GraphicsDevice device)
    {
        if (_effect == null) return;
        double time = _rainAnimationTime;
        const int RainDropCount = 400;
        const float FallSpeed = 12f;
        const float DropLength = 0.5f;
        const float SpreadX = 24f;
        const float SpreadZ = 24f;
        const float HeightCycle = 18f;
        var rainColor = new Color(180, 200, 220, 200);
        var verts = new VertexPositionColor[RainDropCount * 2];
        for (int i = 0; i < RainDropCount; i++)
        {
            float px = ((i * 1.73f) % SpreadX) - SpreadX * 0.5f;
            float pz = ((i * 2.31f) % SpreadZ) - SpreadZ * 0.5f;
            float phase = (float)(time * FallSpeed + i * 0.07) % HeightCycle;
            float y = 14f - phase;
            float slantX = 0.02f;
            float slantZ = 0.03f;
            var start = new Vector3(px, y, pz);
            var end = new Vector3(px - slantX, y - DropLength, pz - slantZ);
            verts[i * 2] = new VertexPositionColor(start, rainColor);
            verts[i * 2 + 1] = new VertexPositionColor(end, rainColor);
        }
        _effect.World = Matrix.Identity;
        _effect.View = _lastView;
        _effect.Projection = _lastProjection;
        _effect.CurrentTechnique.Passes[0].Apply();
        device.DrawUserPrimitives(PrimitiveType.LineList, verts, 0, RainDropCount);
    }

    private void Ensure3DResources(GraphicsDevice device)
    {
        if (_effect != null)
            return;

        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false
        };

        // Cube: 1x1x1 centered at origin (enemy) -> 8 vertices, 12 triangles; color from _enemyCubeColor
        float h = 0.5f;
        var cubeVerts = BuildEnemyCubeVertices(h);
        ushort[] cubeIndices =
        {
            0, 1, 2, 0, 2, 3, 1, 5, 6, 1, 6, 2, 5, 4, 7, 5, 7, 6,
            4, 0, 3, 4, 3, 7, 3, 2, 6, 3, 6, 7, 0, 4, 5, 0, 5, 1
        };

        _cubeBuffer = new VertexBuffer(device, VertexPositionColor.VertexDeclaration, 8, BufferUsage.None);
        _cubeBuffer.SetData(cubeVerts);
        _cubeIndices = new IndexBuffer(device, IndexElementSize.SixteenBits, 36, BufferUsage.None);
        _cubeIndices.SetData(cubeIndices);
        // Projectile: elongated box (nose at -Z, tail at +Z), bullet-like colors
        float pr = 0.05f;
        float pz = 0.2f;
        var nose = new Color(255, 220, 120);
        var body = new Color(255, 140, 60);
        var projVerts = new VertexPositionColor[]
        {
            new(new Vector3(-pr, -pr, -pz), nose),
            new(new Vector3( pr, -pr, -pz), nose),
            new(new Vector3( pr,  pr, -pz), nose),
            new(new Vector3(-pr,  pr, -pz), nose),
            new(new Vector3(-pr, -pr,  pz), body),
            new(new Vector3( pr, -pr,  pz), body),
            new(new Vector3( pr,  pr,  pz), body),
            new(new Vector3(-pr,  pr,  pz), body),
        };
        ushort[] projIndices =
        {
            0, 1, 2, 0, 2, 3, 1, 5, 6, 1, 6, 2, 5, 4, 7, 5, 7, 6,
            4, 0, 3, 4, 3, 7, 3, 2, 6, 3, 6, 7, 0, 4, 5, 0, 5, 1
        };
        _projectileBuffer = new VertexBuffer(device, VertexPositionColor.VertexDeclaration, 8, BufferUsage.None);
        _projectileBuffer.SetData(projVerts);
        _projectileIndices = new IndexBuffer(device, IndexElementSize.SixteenBits, 36, BufferUsage.None);
        _projectileIndices.SetData(projIndices);

        // Plane: Y=0, 50x50 in XZ
        float s = 25f;
        var planeVerts = new VertexPositionColor[6];
        var planeColor = new Color(52, 56, 64);
        planeVerts[0] = new VertexPositionColor(new Vector3(-s, 0, -s), planeColor);
        planeVerts[1] = new VertexPositionColor(new Vector3( s, 0, -s), planeColor);
        planeVerts[2] = new VertexPositionColor(new Vector3(-s, 0,  s), planeColor);
        planeVerts[3] = new VertexPositionColor(new Vector3(-s, 0,  s), planeColor);
        planeVerts[4] = new VertexPositionColor(new Vector3( s, 0, -s), planeColor);
        planeVerts[5] = new VertexPositionColor(new Vector3( s, 0,  s), planeColor);

        _planeBuffer = new VertexBuffer(device, VertexPositionColor.VertexDeclaration, 6, BufferUsage.None);
        _planeBuffer.SetData(planeVerts);

        // Grid on Y=0: lines in XZ to match plane extent (±25), spacing 2 for more sections/lines
        const float gridExtent = 25f;
        const float gridSpacing = 2f;
        var gridColor = new Color(38, 42, 50);
        var gridVerts = new List<VertexPositionColor>();
        for (float z = -gridExtent; z <= gridExtent; z += gridSpacing)
        {
            gridVerts.Add(new VertexPositionColor(new Vector3(-gridExtent, 0.001f, z), gridColor));
            gridVerts.Add(new VertexPositionColor(new Vector3( gridExtent, 0.001f, z), gridColor));
        }
        for (float x = -gridExtent; x <= gridExtent; x += gridSpacing)
        {
            gridVerts.Add(new VertexPositionColor(new Vector3(x, 0.001f, -gridExtent), gridColor));
            gridVerts.Add(new VertexPositionColor(new Vector3(x, 0.001f,  gridExtent), gridColor));
        }
        _gridLineCount = gridVerts.Count / 2;
        _gridBuffer = new VertexBuffer(device, VertexPositionColor.VertexDeclaration, gridVerts.Count, BufferUsage.None);
        _gridBuffer.SetData(gridVerts.ToArray());
    }

    private void UpdateEnemyMovement(float dt)
    {
        const float circleRadius = 3f;
        const float circleSpeed = 1f;
        const float wanderSpeed = 2f;
        const float wanderRadius = 6f;
        const float chaseSpeed = 4f;
        const float hoverAmplitude = 0.4f;
        const float hoverFreq = 3f;

        switch (_enemyMovementKind)
        {
            case EntityMovementKind.Stationary:
                break;
            case EntityMovementKind.Circle:
                _enemyOrbitAngle += circleSpeed * dt;
                _enemyCubePosition = new Vector3(
                    MathF.Cos(_enemyOrbitAngle) * circleRadius,
                    0f,
                    MathF.Sin(_enemyOrbitAngle) * circleRadius);
                break;
            case EntityMovementKind.Wandering:
                _enemyWanderPhase += dt;
                float angle = _enemyWanderPhase + (float)s_enemyWanderRandom.NextDouble() * 0.5f;
                float r = (float)s_enemyWanderRandom.NextDouble() * wanderSpeed * dt;
                _enemyCubePosition += new Vector3(MathF.Cos(angle) * r, 0f, MathF.Sin(angle) * r);
                float dist = MathF.Sqrt(_enemyCubePosition.X * _enemyCubePosition.X + _enemyCubePosition.Z * _enemyCubePosition.Z);
                if (dist > wanderRadius)
                {
                    var dir = Vector3.Normalize(new Vector3(_enemyCubePosition.X, 0, _enemyCubePosition.Z));
                    _enemyCubePosition = dir * wanderRadius;
                }
                break;
            case EntityMovementKind.Chasing:
                {
                    var toPlayer = _player.Position - _enemyCubePosition;
                    toPlayer.Y = 0;
                    if (toPlayer.LengthSquared() > 0.01f)
                    {
                        toPlayer.Normalize();
                        _enemyCubePosition += toPlayer * chaseSpeed * dt;
                    }
                }
                break;
            case EntityMovementKind.Hovering:
                float y = MathF.Sin((float)(Transport?.CurrentTime ?? 0) * hoverFreq) * hoverAmplitude;
                _enemyCubePosition = new Vector3(0f, y, 0f);
                break;
        }
    }

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var content = ContentBounds;
        var viewportRect = new Rectangle(content.X, content.Y, content.Width, Math.Max(0, content.Height));

        _camera.Target = _player.Position;
        _camera.HandleInput(Input, viewportRect, dt);

        // Update screenshake: random offset that decays over remaining time
        if (_screenshakeRemaining > 0)
        {
            float t = _screenshakeRemaining / Math.Max(0.001f, _screenshakeDuration);
            float magnitude = _screenshakeAmplitude * t * 2f; // scale so visible in world units
            float angle = (float)(s_shakeRandom.NextDouble() * Math.Tau);
            _shakeOffset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * magnitude;
            _screenshakeRemaining -= dt;
            if (_screenshakeRemaining < 0)
                _screenshakeRemaining = 0;
        }
        else
            _shakeOffset = Vector2.Zero;

        var (forwardXZ, rightXZ) = _camera.GetCameraForwardRightXZ();
        _player.Update(Input, viewportRect, dt, forwardXZ, rightXZ);

        _rainAnimationTime += gameTime.ElapsedGameTime.TotalSeconds;
        UpdateEnemyMovement(dt);

        float currentTime = (float)(Transport?.CurrentTime ?? 0);
        for (int i = _spawnedEntities.Count - 1; i >= 0; i--)
        {
            var e = _spawnedEntities[i];
            if (currentTime - e.SpawnTime >= e.Lifetime)
            {
                _spawnedEntities.RemoveAt(i);
                continue;
            }
            float elapsed = currentTime - e.SpawnTime;
            if (!e.IsProjectile)
            {
                // Small cube: no movement
                continue;
            }
            if (e.DirectionPattern == ProjectileDirectionPattern.Boomerang)
            {
                // Smooth ease-out and ease-in: slow to stop at peak, then accelerate back (like a real boomerang)
                // progress = sin(elapsed/lifetime * PI): 0 -> 1 at mid -> 0 at end; velocity = derivative
                float t = Math.Clamp(elapsed / e.Lifetime, 0f, 1f);
                float pi = MathF.PI;
                float progress = MathF.Sin(t * pi);
                float maxDistance = e.Speed * e.Lifetime / pi; // so initial velocity magnitude = Speed
                e.Position = e.SpawnPosition + e.InitialDirection * (maxDistance * progress);
                float velocityMagnitude = maxDistance * (pi / e.Lifetime) * MathF.Cos(t * pi);
                e.Velocity = e.InitialDirection * velocityMagnitude;
                continue;
            }
            else if (e.DirectionPattern == ProjectileDirectionPattern.Oscillation && e.OscillationAmplitude > 0)
            {
                float angleRad = (e.OscillationAmplitude * MathF.PI / 180f) * MathF.Sin(elapsed * 4f);
                var right = Vector3.Cross(e.InitialDirection, Vector3.UnitY);
                if (right.LengthSquared() < 1e-10f) right = Vector3.UnitX;
                else right.Normalize();
                var dir = Vector3.Normalize(e.InitialDirection * MathF.Cos(angleRad) + right * MathF.Sin(angleRad));
                e.Velocity = dir * e.Speed;
            }
            else if (e.DirectionPattern == ProjectileDirectionPattern.Orbiting && e.OrbitingDistance > 0)
            {
                float omega = e.Speed / e.OrbitingDistance;
                // Advance angle by frame dt when playing so orbit is smooth (transport time updates at audio buffer rate)
                if (Transport?.IsPlaying == true)
                    e.OrbitAngle += omega * dt;
                float cosA = MathF.Cos(e.OrbitAngle), sinA = MathF.Sin(e.OrbitAngle);
                e.Position = e.OrbitCenter + (e.OrbitRight * cosA + e.OrbitTangent * sinA) * e.OrbitingDistance;
                e.Velocity = (-e.OrbitRight * sinA + e.OrbitTangent * cosA) * e.Speed;
                continue;
            }
            e.Position += e.Velocity * dt;
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        DrawPanelBackground(spriteBatch);
        var content = ContentBounds;
        var viewport = new Rectangle(content.X, content.Y, content.Width, content.Height);
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);

        if (viewport.Height > 0 && viewport.Width > 0 && _renderTarget != null)
        {
            spriteBatch.Draw(_renderTarget, viewport, new Rectangle(0, 0, _renderTarget.Width, _renderTarget.Height), Color.White);
            // Pixel-perfect (nearest-neighbor) for character sprite
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, RasterizerState.CullNone);
            DrawPlayerOverlay(spriteBatch, viewport);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, RasterizerState.CullNone);
            DrawDialogueBubble(spriteBatch, viewport);
        }
        else
            spriteBatch.Draw(pixel, viewport, new Color(20, 22, 26));
    }

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        // Game view uses custom Draw() for player overlay with PointClamp
    }

    private void DrawPlayerOverlay(SpriteBatch spriteBatch, Rectangle viewport)
    {
        if (_lastViewportW <= 0 || _lastViewportH <= 0)
            return;
        Vector3 worldPos = _player.Position;
        if (!ProjectWorldToScreen(worldPos, _lastView, _lastProjection, _lastViewportW, _lastViewportH, out float sx, out float sy))
            return;

        // Scale player and shadow by camera distance so they shrink/grow with zoom (reference distance 8 = 24px height)
        const float referenceDistance = 8f;
        const int referenceDrawHeight = 24;
        float scale = referenceDistance / Math.Max(0.1f, _camera.Distance);
        int drawHeight = (int)(referenceDrawHeight * scale);
        drawHeight = Math.Clamp(drawHeight, 4, 200);

        // Shadow: dark ellipse under the feet (drawn first so player is on top), scaled with player
        int shadowWidth = Math.Max(4, (int)(20 * scale));
        int shadowHeight = Math.Max(2, (int)(6 * scale));
        var shadowColor = new Color(0, 0, 0, 100);
        int shadowX = (int)(viewport.X + sx) - shadowWidth / 2;
        int shadowY = (int)(viewport.Y + sy) - shadowHeight / 2;
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        spriteBatch.Draw(pixel, new Rectangle(shadowX, shadowY, shadowWidth, shadowHeight), shadowColor);

        if (PlayerTexture == null)
            return;
        var (cameraForward, cameraRight) = _camera.GetCameraForwardRightXZ();
        Vector3 facingXZ = _player.GetFacingDirectionXZ();
        // Pick sprite frame from actual direction in camera-relative (screen) space: right=0, left=2, toward camera=1, away=3
        int frame = GetCameraRelativeFrame(facingXZ, cameraForward, cameraRight);
        var source = GameViewPlayer.GetSourceRectangleForFrame(PlayerTexture, frame);
        int drawWidth = drawHeight * source.Width / Math.Max(1, source.Height);
        var dest = new Rectangle((int)(viewport.X + sx) - drawWidth / 2, (int)(viewport.Y + sy) - drawHeight, drawWidth, drawHeight);
        spriteBatch.Draw(PlayerTexture, dest, source, Color.White);
    }

    private void DrawDialogueBubble(SpriteBatch spriteBatch, Rectangle viewport)
    {
        if (!_dialogueBubble.HasValue || _lastViewportW <= 0 || _lastViewportH <= 0)
            return;
        var (text, showUntil) = _dialogueBubble.Value;
        double currentTime = Transport?.CurrentTime ?? 0;
        if (currentTime >= showUntil || string.IsNullOrWhiteSpace(text))
            return;

        if (!ProjectWorldToScreen(new Vector3(0, 1f, 0), _lastView, _lastProjection, _lastViewportW, _lastViewportH, out float sx, out float sy))
            return;

        const int padding = 10;
        const int tailWidth = 16;
        const int tailHeight = 10;
        const int gapAboveAnchor = 4;
        const int bubbleFontSize = 14;

        var device = spriteBatch.GraphicsDevice;
        if (device == null) return;

        if (_dialogueCachedText != text)
        {
            _dialogueTextTexture?.Dispose();
            _dialogueTextTexture = null;
            _dialogueCachedText = text;
        }
        if (_dialogueTextTexture == null)
        {
            _dialogueTextTexture = TextTextureHelper.Create(device, text, "Segoe UI", bubbleFontSize, FontStyle.Bold);
            if (_dialogueTextTexture == null) return;
        }

        int textW = _dialogueTextTexture.Width;
        int textH = _dialogueTextTexture.Height;
        int bubbleW = Math.Max(textW + padding * 2, tailWidth + padding);
        int bubbleH = textH + padding * 2;

        int screenX = (int)(viewport.X + sx);
        int screenY = (int)(viewport.Y + sy);
        int bubbleLeft = screenX - bubbleW / 2;
        int bubbleBottom = screenY - gapAboveAnchor;
        int bubbleTop = bubbleBottom - bubbleH;

        var pixel = GetPixelTexture(device);
        var black = Color.Black;

        // Bubble body (black background)
        spriteBatch.Draw(pixel, new Rectangle(bubbleLeft, bubbleTop, bubbleW, bubbleH), black);
        // Tail: middle-centered, pointing down (small black rect below bubble)
        int tailLeft = screenX - tailWidth / 2;
        spriteBatch.Draw(pixel, new Rectangle(tailLeft, bubbleBottom, tailWidth, tailHeight), black);

        // White bold text centered in bubble
        int textX = bubbleLeft + (bubbleW - textW) / 2;
        int textY = bubbleTop + (bubbleH - textH) / 2;
        spriteBatch.Draw(_dialogueTextTexture, new Rectangle(textX, textY, textW, textH), Color.White);
    }

    /// <summary>Frame index (0=right, 1=down, 2=left, 3=up) from world facing expressed in camera-relative terms.</summary>
    private static int GetCameraRelativeFrame(Vector3 facingXZ, Vector3 cameraForward, Vector3 cameraRight)
    {
        float rightComponent = Vector3.Dot(facingXZ, cameraRight);
        float forwardComponent = Vector3.Dot(facingXZ, cameraForward);
        if (Math.Abs(rightComponent) >= Math.Abs(forwardComponent))
            return rightComponent >= 0 ? 2 : 0; // screen right → 2 (left sprite), screen left → 0 (right sprite)
        return forwardComponent >= 0 ? 1 : 3;   // away from camera → 1 (down), toward camera → 3 (up)
    }

    /// <summary>Rotation matrix that rotates 'from' onto 'to'. Both vectors must be unit length.</summary>
    private static Matrix RotationFromForward(Vector3 from, Vector3 to)
    {
        float d = Vector3.Dot(from, to);
        if (d >= 0.9999f) return Matrix.Identity;
        if (d <= -0.9999f)
        {
            var axis = Math.Abs(Vector3.Dot(from, Vector3.UnitY)) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
            return Matrix.CreateFromAxisAngle(Vector3.Cross(from, axis), MathF.PI);
        }
        var rotAxis = Vector3.Cross(from, to);
        rotAxis.Normalize();
        float angle = MathF.Acos(Math.Clamp(d, -1f, 1f));
        return Matrix.CreateFromAxisAngle(rotAxis, angle);
    }

    private static bool ProjectWorldToScreen(Vector3 world, Matrix view, Matrix projection, int viewportW, int viewportH, out float screenX, out float screenY)
    {
        var clip = Vector4.Transform(new Vector4(world, 1f), view * projection);
        if (clip.W <= 0.0001f) { screenX = screenY = 0; return false; }
        float ndcX = clip.X / clip.W;
        float ndcY = clip.Y / clip.W;
        if (ndcX < -1f || ndcX > 1f || ndcY < -1f || ndcY > 1f) { screenX = screenY = 0; return false; }
        screenX = (ndcX + 1f) * 0.5f * viewportW;
        screenY = (1f - ndcY) * 0.5f * viewportH;
        return true;
    }
}
