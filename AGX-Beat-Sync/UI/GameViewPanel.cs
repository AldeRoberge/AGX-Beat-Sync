using System;
using System.Collections.Generic;
using AGX_Beat_Sync.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.UI;

/// <summary>One spawned entity in the game view (from a Spawn Entity event).</summary>
public class SpawnedEntity
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float SpawnTime;
    /// <summary>Lifetime in seconds before removal.</summary>
    public float Lifetime { get; set; } = 5f;

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
        SpawnEntityWithDirection(position, direction, speed, lifetime, ProjectileDirectionPattern.Linear, 0f, 0f);
    }

    /// <summary>Spawn an entity with optional direction pattern (Oscillation, Orbiting).</summary>
    public void SpawnEntityWithDirection(Vector3 position, Vector3 direction, float speed, float lifetime,
        ProjectileDirectionPattern directionPattern, float oscillationAmplitude, float orbitingDistance)
    {
        var d = direction;
        if (d.LengthSquared() < 1e-8f)
            d = -Vector3.UnitZ;
        else
            d.Normalize();

        if (directionPattern == ProjectileDirectionPattern.Orbiting && orbitingDistance > 0.001f)
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
                Velocity = tangent * speed,
                SpawnTime = (float)(Transport?.CurrentTime ?? 0),
                Lifetime = lifetime,
                DirectionPattern = directionPattern,
                Speed = speed,
                OrbitingDistance = radius,
                OrbitCenter = position,
                OrbitRight = right,
                OrbitTangent = tangent
            });
        }
        else if (directionPattern == ProjectileDirectionPattern.Oscillation)
        {
            _spawnedEntities.Add(new SpawnedEntity
            {
                Position = position,
                Velocity = d * speed,
                SpawnTime = (float)(Transport?.CurrentTime ?? 0),
                Lifetime = lifetime,
                DirectionPattern = directionPattern,
                InitialDirection = d,
                Speed = speed,
                OscillationAmplitude = Math.Abs(oscillationAmplitude)
            });
        }
        else
        {
            _spawnedEntities.Add(new SpawnedEntity
            {
                Position = position,
                Velocity = d * speed,
                SpawnTime = (float)(Transport?.CurrentTime ?? 0),
                Lifetime = lifetime
            });
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
            return "Game View — WASD move player. Middle-drag: orbit. Right-drag: FPS camera. Q/E: orbit yaw. Scroll: zoom";
        return "Game View";
    }

    /// <summary>Clear all spawned entities (e.g. when user seeks or stops).</summary>
    public void ClearSpawnedEntities()
    {
        _spawnedEntities.Clear();
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
            _lastView = _camera.GetViewMatrix();
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

            // Enemy cube at world origin
            _effect.World = Matrix.CreateTranslation(Vector3.Zero);
            _effect.CurrentTechnique.Passes[0].Apply();
            device.SetVertexBuffer(_cubeBuffer);
            device.Indices = _cubeIndices;
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 12);

            foreach (var e in _spawnedEntities)
            {
                var dir = e.Velocity;
                if (dir.LengthSquared() < 1e-8f) dir = -Vector3.UnitZ;
                else dir.Normalize();
                var rot = RotationFromForward(-Vector3.UnitZ, dir);
                _effect.World = rot * Matrix.CreateTranslation(e.Position);
                _effect.CurrentTechnique.Passes[0].Apply();
                device.SetVertexBuffer(_projectileBuffer);
                device.Indices = _projectileIndices;
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 12);
            }
        }

        device.SetRenderTargets(prevTargets);
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

        // Cube: 1x1x1 centered at origin (enemy) -> 8 vertices, 12 triangles
        var cubeVerts = new VertexPositionColor[8];
        float h = 0.5f;
        cubeVerts[0] = new VertexPositionColor(new Vector3(-h, -h, -h), new Color(200, 80, 80));
        cubeVerts[1] = new VertexPositionColor(new Vector3( h, -h, -h), new Color(200, 80, 80));
        cubeVerts[2] = new VertexPositionColor(new Vector3( h,  h, -h), new Color(230, 100, 100));
        cubeVerts[3] = new VertexPositionColor(new Vector3(-h,  h, -h), new Color(230, 100, 100));
        cubeVerts[4] = new VertexPositionColor(new Vector3(-h, -h,  h), new Color(210, 90, 90));
        cubeVerts[5] = new VertexPositionColor(new Vector3( h, -h,  h), new Color(210, 90, 90));
        cubeVerts[6] = new VertexPositionColor(new Vector3( h,  h,  h), new Color(240, 120, 120));
        cubeVerts[7] = new VertexPositionColor(new Vector3(-h,  h,  h), new Color(240, 120, 120));

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

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var content = ContentBounds;
        var viewportRect = new Rectangle(content.X, content.Y, content.Width, Math.Max(0, content.Height));

        _camera.Target = _player.Position;
        _camera.HandleInput(Input, viewportRect, dt);

        if (_camera.IsCapturingMouse)
            _player.SetPosition(_camera.Target); // FPS mode: player follows camera target
        else
        {
            var (forwardXZ, rightXZ) = _camera.GetCameraForwardRightXZ();
            _player.Update(Input, viewportRect, dt, forwardXZ, rightXZ);
        }

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
            if (e.DirectionPattern == ProjectileDirectionPattern.Oscillation && e.OscillationAmplitude > 0)
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
                float a = omega * elapsed;
                float cosA = MathF.Cos(a), sinA = MathF.Sin(a);
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
