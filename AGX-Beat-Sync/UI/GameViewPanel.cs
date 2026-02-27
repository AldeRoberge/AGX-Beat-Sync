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
    public void SpawnEntity(Vector3 position, Vector3 rotationEulerRadians, float speed)
    {
        var rot = Matrix.CreateRotationX(rotationEulerRadians.X) * Matrix.CreateRotationY(rotationEulerRadians.Y) * Matrix.CreateRotationZ(rotationEulerRadians.Z);
        var forward = Vector3.Transform(-Vector3.UnitZ, rot);
        if (forward.LengthSquared() > 0.0001f)
            forward.Normalize();
        _spawnedEntities.Add(new SpawnedEntity
        {
            Position = position,
            Velocity = forward * speed,
            SpawnTime = (float)(Transport?.CurrentTime ?? 0)
        });
    }

    public override string? GetHoverText(Point mouse)
    {
        if (!ContainsPoint(mouse)) return null;
        var content = ContentBounds;
        var viewportRect = new Rectangle(content.X, content.Y, content.Width, Math.Max(0, content.Height));
        if (viewportRect.Contains(mouse))
            return "Game View — WASD move player. Middle-drag: orbit. Right-drag: FPS camera. Q/E: orbit yaw";
        return "Game View";
    }

    /// <summary>Clear all spawned entities (e.g. when user seeks or stops).</summary>
    public void ClearSpawnedEntities()
    {
        _spawnedEntities.Clear();
    }

    /// <summary>True while right-drag look is active; game should hide cursor.</summary>
    public bool IsCapturingMouse => _camera.IsCapturingMouse;

    private Matrix _lastView;
    private Matrix _lastProjection;
    private int _lastViewportW;
    private int _lastViewportH;

    private RenderTarget2D? _renderTarget;
    private BasicEffect? _effect;
    private VertexBuffer? _cubeBuffer;
    private IndexBuffer? _cubeIndices;
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

            // Static blue cube at world origin
            _effect.World = Matrix.CreateTranslation(Vector3.Zero);
            _effect.CurrentTechnique.Passes[0].Apply();
            device.SetVertexBuffer(_cubeBuffer);
            device.Indices = _cubeIndices;
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 12);

            foreach (var e in _spawnedEntities)
            {
                _effect.World = Matrix.CreateTranslation(e.Position);
                _effect.CurrentTechnique.Passes[0].Apply();
                device.SetVertexBuffer(_cubeBuffer);
                device.Indices = _cubeIndices;
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

        // Cube: 1x1x1 centered at origin -> 8 vertices, 12 triangles
        var cubeVerts = new VertexPositionColor[8];
        float h = 0.5f;
        cubeVerts[0] = new VertexPositionColor(new Vector3(-h, -h, -h), new Color(92, 148, 255));
        cubeVerts[1] = new VertexPositionColor(new Vector3( h, -h, -h), new Color(92, 148, 255));
        cubeVerts[2] = new VertexPositionColor(new Vector3( h,  h, -h), new Color(132, 188, 255));
        cubeVerts[3] = new VertexPositionColor(new Vector3(-h,  h, -h), new Color(132, 188, 255));
        cubeVerts[4] = new VertexPositionColor(new Vector3(-h, -h,  h), new Color(110, 170, 255));
        cubeVerts[5] = new VertexPositionColor(new Vector3( h, -h,  h), new Color(110, 170, 255));
        cubeVerts[6] = new VertexPositionColor(new Vector3( h,  h,  h), new Color(150, 200, 255));
        cubeVerts[7] = new VertexPositionColor(new Vector3(-h,  h,  h), new Color(150, 200, 255));

        ushort[] cubeIndices =
        {
            0, 1, 2, 0, 2, 3, 1, 5, 6, 1, 6, 2, 5, 4, 7, 5, 7, 6,
            4, 0, 3, 4, 3, 7, 3, 2, 6, 3, 6, 7, 0, 4, 5, 0, 5, 1
        };

        _cubeBuffer = new VertexBuffer(device, VertexPositionColor.VertexDeclaration, 8, BufferUsage.None);
        _cubeBuffer.SetData(cubeVerts);
        _cubeIndices = new IndexBuffer(device, IndexElementSize.SixteenBits, 36, BufferUsage.None);
        _cubeIndices.SetData(cubeIndices);

        // Plane: Y=0, 10x10 in XZ
        float s = 5f;
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

        // Grid on Y=0: lines in XZ to match plane extent (±5), spacing 1
        const float gridExtent = 5f;
        const float gridSpacing = 1f;
        var gridColor = new Color(70, 76, 88);
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

        foreach (var e in _spawnedEntities)
            e.Position += e.Velocity * dt;
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
        if (PlayerTexture == null || _lastViewportW <= 0 || _lastViewportH <= 0)
            return;
        Vector3 worldPos = _player.Position;
        if (!ProjectWorldToScreen(worldPos, _lastView, _lastProjection, _lastViewportW, _lastViewportH, out float sx, out float sy))
            return;
        var source = _player.GetSourceRectangle(PlayerTexture);
        const int drawHeight = 24;
        int drawWidth = drawHeight * source.Width / Math.Max(1, source.Height);
        int x = (int)sx - drawWidth / 2;
        int y = (int)sy - drawHeight;
        var dest = new Rectangle(viewport.X + x, viewport.Y + y, drawWidth, drawHeight);
        spriteBatch.Draw(PlayerTexture, dest, source, Color.White);
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
