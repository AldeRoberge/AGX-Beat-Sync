using System;
using System.Collections.Generic;
using AGX_Beat_Sync.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.UI;

public class GameViewPanel : PanelBase
{
    private const int ControlBarHeight = 28;
    private const int ButtonWidth = 60;
    private const int ButtonMargin = 6;
    private const int BackgroundOptionCount = 3;
    private const int EnemyOptionCount = 3;
    private const float CameraMoveSpeed = 300f;
    private const float ProjectileSpeed = 280f;
    private const float ProjectileLifetime = 2.5f;

    public Input.InputManager? Input { get; set; }
    public Project? Project { get; set; }
    public Transport? Transport { get; set; }

    /// <summary>When true, game view expands over the timeline area. Set by layout from this each frame.</summary>
    public bool Expanded { get; set; }

    private const int ExpandButtonWidth = 28;

    private int _selectedBackground;
    private int _selectedEnemy;
    private Vector2 _cameraPosition;

    private readonly List<Projectile> _projectiles = new();
    private double _lastEventTime;
    private bool _hadLastEventTime;
    private bool _wasPlaying;

    private sealed class Projectile
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Lifetime;
    }

    public GameViewPanel()
    {
        Title = "Game Preview";
        BackgroundColor = new Color(24, 26, 30);
    }

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        UpdateBeatEvents(dt);
        UpdateCameraAndUi(dt);
    }

    private void UpdateBeatEvents(float dt)
    {
        if (Project == null || Transport == null)
            return;

        bool playing = Transport.IsPlaying;
        double currentTime = Transport.CurrentTime;

        if (!playing)
        {
            _hadLastEventTime = false;
            _projectiles.Clear();
            _wasPlaying = false;
            return;
        }

        if (!_wasPlaying || !_hadLastEventTime)
        {
            _lastEventTime = currentTime;
            _hadLastEventTime = true;
        }
        else if (currentTime >= _lastEventTime)
        {
            SpawnProjectilesBetween(_lastEventTime, currentTime);
            _lastEventTime = currentTime;
        }
        else
        {
            _lastEventTime = currentTime;
        }

        _wasPlaying = playing;

        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            p.Position += p.Velocity * dt;
            p.Lifetime -= dt;
            if (p.Lifetime <= 0)
                _projectiles.RemoveAt(i);
        }
    }

    private void SpawnProjectilesBetween(double fromTime, double toTime)
    {
        if (Project == null || Project.NoteTracks.Count == 0)
            return;

        var track = Project.NoteTracks[0];
        if (track.Notes.Count == 0)
            return;

        foreach (var note in track.Notes)
        {
            if (note.Time > fromTime && note.Time <= toTime)
            {
                SpawnProjectileForNote(note);
            }
        }
    }

    private void SpawnProjectileForNote(NoteEvent note)
    {
        Vector2 origin = GetEnemyWorldPosition();

        float angle;
        int lane = Math.Max(0, note.Lane);
        switch (lane % 4)
        {
            case 0: angle = -MathF.PI / 2f; break;           // up
            case 1: angle = -MathF.PI / 4f; break;           // up-right
            case 2: angle = MathF.PI / 4f; break;            // down-right
            default: angle = MathF.PI / 2f; break;           // down
        }

        var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ProjectileSpeed;

        _projectiles.Add(new Projectile
        {
            Position = origin,
            Velocity = velocity,
            Lifetime = ProjectileLifetime
        });
    }

    private void UpdateCameraAndUi(float dt)
    {
        if (Input == null)
            return;

        if (ContainsPoint(Input.MousePosition))
        {
            var move = Vector2.Zero;
            if (Input.IsKeyDown(Keys.W)) move.Y -= 1;
            if (Input.IsKeyDown(Keys.S)) move.Y += 1;
            if (Input.IsKeyDown(Keys.A)) move.X -= 1;
            if (Input.IsKeyDown(Keys.D)) move.X += 1;

            if (move != Vector2.Zero)
            {
                move.Normalize();
                _cameraPosition += move * CameraMoveSpeed * dt;
            }

            HandleControlClicks();
        }

        // Expand/collapse button in header (works even when mouse not over content)
        if (Input.MouseLeftPressed && GetExpandButtonRect().Contains(Input.MousePosition))
            Expanded = !Expanded;
    }

    private Rectangle GetExpandButtonRect()
    {
        var header = HeaderBounds;
        return new Rectangle(header.Right - ExpandButtonWidth, header.Y, ExpandButtonWidth, header.Height);
    }

    private void HandleControlClicks()
    {
        if (Input == null || !Input.MouseLeftPressed)
            return;

        var content = ContentBounds;
        var controls = new Rectangle(content.X, content.Y, content.Width, ControlBarHeight);
        if (!controls.Contains(Input.MousePosition))
            return;

        for (int i = 0; i < BackgroundOptionCount; i++)
        {
            if (GetBackgroundButtonRect(content, i).Contains(Input.MousePosition))
            {
                _selectedBackground = i;
                return;
            }
        }

        for (int i = 0; i < EnemyOptionCount; i++)
        {
            if (GetEnemyButtonRect(content, i).Contains(Input.MousePosition))
            {
                _selectedEnemy = i;
                return;
            }
        }
    }

    private static Rectangle GetBackgroundButtonRect(Rectangle content, int index)
    {
        int x = content.X + ButtonMargin + index * (ButtonWidth + ButtonMargin);
        int y = content.Y + 4;
        int h = ControlBarHeight - 8;
        return new Rectangle(x, y, ButtonWidth, h);
    }

    private static Rectangle GetEnemyButtonRect(Rectangle content, int index)
    {
        int startX = content.X + ButtonMargin + BackgroundOptionCount * (ButtonWidth + ButtonMargin) + 24;
        int x = startX + index * (ButtonWidth + ButtonMargin);
        int y = content.Y + 4;
        int h = ControlBarHeight - 8;
        return new Rectangle(x, y, ButtonWidth, h);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);
        DrawExpandButton(spriteBatch);
    }

    private void DrawExpandButton(SpriteBatch spriteBatch)
    {
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        var rect = GetExpandButtonRect();
        var bg = new Color(60, 65, 75);
        var icon = new Color(200, 205, 215);
        spriteBatch.Draw(pixel, rect, bg);
        int cx = rect.X + rect.Width / 2;
        int cy = rect.Y + rect.Height / 2;
        int arrow = 6;
        if (Expanded)
        {
            for (int i = -arrow; i <= arrow; i++)
            {
                int j = (i * (rect.Height / 2 - 2)) / arrow;
                spriteBatch.Draw(pixel, new Rectangle(cx + i - 1, cy - j, 2, 2), icon);
            }
        }
        else
        {
            for (int i = -arrow; i <= arrow; i++)
            {
                int j = (i * (rect.Width / 2 - 2)) / arrow;
                spriteBatch.Draw(pixel, new Rectangle(cx - j, cy + i - 1, 2, 2), icon);
            }
        }
    }

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        var content = ContentBounds;

        var controls = new Rectangle(content.X, content.Y, content.Width, ControlBarHeight);
        var viewport = new Rectangle(content.X, content.Y + ControlBarHeight, content.Width, content.Height - ControlBarHeight);

        DrawControls(spriteBatch, pixel, controls);
        DrawWorld(spriteBatch, pixel, viewport);
    }

    private void DrawControls(SpriteBatch spriteBatch, Texture2D pixel, Rectangle controls)
    {
        var bgStrip = new Color(36, 39, 45);
        var buttonBg = new Color(52, 56, 64);
        var buttonSelected = new Color(92, 148, 255);
        var buttonSelectedBorder = new Color(132, 188, 255);

        spriteBatch.Draw(pixel, controls, bgStrip);

        for (int i = 0; i < BackgroundOptionCount; i++)
        {
            var rect = GetBackgroundButtonRect(controls, i);
            bool selected = i == _selectedBackground;
            var color = selected ? buttonSelected : buttonBg;
            spriteBatch.Draw(pixel, rect, color);
            if (selected)
            {
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), buttonSelectedBorder);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), buttonSelectedBorder);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), buttonSelectedBorder);
                spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), buttonSelectedBorder);
            }
        }

        for (int i = 0; i < EnemyOptionCount; i++)
        {
            var rect = GetEnemyButtonRect(controls, i);
            bool selected = i == _selectedEnemy;
            var color = selected ? buttonSelected : buttonBg;
            spriteBatch.Draw(pixel, rect, color);
            if (selected)
            {
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), buttonSelectedBorder);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), buttonSelectedBorder);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), buttonSelectedBorder);
                spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), buttonSelectedBorder);
            }
        }
    }

    private void DrawWorld(SpriteBatch spriteBatch, Texture2D pixel, Rectangle viewport)
    {
        if (viewport.Height <= 0 || viewport.Width <= 0)
            return;

        DrawBackground(spriteBatch, pixel, viewport);

        var originScreen = WorldToScreen(Vector2.Zero, viewport);

        var axisColor = new Color(90, 95, 105);
        spriteBatch.Draw(pixel, new Rectangle((int)originScreen.X - 1, viewport.Y, 2, viewport.Height), axisColor);
        spriteBatch.Draw(pixel, new Rectangle(viewport.X, (int)originScreen.Y - 1, viewport.Width, 2), axisColor);

        DrawEnemy(spriteBatch, pixel, viewport, originScreen);
        DrawProjectiles(spriteBatch, pixel, viewport);
    }

    private void DrawBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle viewport)
    {
        switch (_selectedBackground)
        {
            default:
            case 0:
                spriteBatch.Draw(pixel, viewport, new Color(20, 22, 26));
                break;
            case 1:
                spriteBatch.Draw(pixel, viewport, new Color(16, 18, 22));
                int gridSize = 40;
                var gridColor = new Color(40, 44, 52);
                for (int x = viewport.X; x < viewport.Right; x += gridSize)
                    spriteBatch.Draw(pixel, new Rectangle(x, viewport.Y, 1, viewport.Height), gridColor);
                for (int y = viewport.Y; y < viewport.Bottom; y += gridSize)
                    spriteBatch.Draw(pixel, new Rectangle(viewport.X, y, viewport.Width, 1), gridColor);
                break;
            case 2:
                spriteBatch.Draw(pixel, viewport, new Color(12, 14, 20));
                int stripeHeight = 24;
                var stripeColor1 = new Color(26, 30, 40);
                var stripeColor2 = new Color(18, 20, 28);
                for (int i = 0; i * stripeHeight < viewport.Height; i++)
                {
                    var rect = new Rectangle(viewport.X, viewport.Y + i * stripeHeight, viewport.Width, stripeHeight);
                    if (rect.Bottom > viewport.Bottom)
                        rect.Height = viewport.Bottom - rect.Y;
                    spriteBatch.Draw(pixel, rect, (i & 1) == 0 ? stripeColor1 : stripeColor2);
                }
                break;
        }
    }

    private void DrawEnemy(SpriteBatch spriteBatch, Texture2D pixel, Rectangle viewport, Vector2 originScreen)
    {
        var enemyColor = new Color(230, 130, 90);
        var enemySecondary = new Color(180, 90, 60);

        Vector2 enemyWorldPos = GetEnemyWorldPosition();
        var enemyScreen = WorldToScreen(enemyWorldPos, viewport);

        switch (_selectedEnemy)
        {
            default:
            case 0:
                int size = 40;
                var rect = new Rectangle((int)enemyScreen.X - size / 2, (int)enemyScreen.Y - size / 2, size, size);
                spriteBatch.Draw(pixel, rect, enemySecondary);
                spriteBatch.Draw(pixel, new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8), enemyColor);
                break;
            case 1:
                int radius = 26;
                for (int y = -radius; y <= radius; y++)
                {
                    int span = (int)Math.Sqrt(radius * radius - y * y);
                    var row = new Rectangle((int)enemyScreen.X - span, (int)enemyScreen.Y + y, span * 2, 1);
                    spriteBatch.Draw(pixel, row, enemyColor);
                }
                break;
            case 2:
                int triHeight = 46;
                for (int y = 0; y < triHeight; y++)
                {
                    float t = y / (float)triHeight;
                    int halfWidth = (int)(t * triHeight);
                    int sx = (int)enemyScreen.X - halfWidth;
                    int sy = (int)enemyScreen.Y + y - triHeight / 2;
                    spriteBatch.Draw(pixel, new Rectangle(sx, sy, halfWidth * 2, 1), enemyColor);
                }
                break;
        }
    }

    private Vector2 GetEnemyWorldPosition()
    {
        return _selectedEnemy switch
        {
            1 => new Vector2(80, -40),
            2 => new Vector2(-120, 40),
            _ => new Vector2(0, -80)
        };
    }

    private void DrawProjectiles(SpriteBatch spriteBatch, Texture2D pixel, Rectangle viewport)
    {
        var color = new Color(255, 220, 150);
        foreach (var p in _projectiles)
        {
            var screen = WorldToScreen(p.Position, viewport);
            int size = 10;
            var rect = new Rectangle((int)screen.X - size / 2, (int)screen.Y - size / 2, size, size);
            if (!viewport.Intersects(rect))
                continue;
            spriteBatch.Draw(pixel, rect, color);
        }
    }

    private Vector2 WorldToScreen(Vector2 world, Rectangle viewport)
    {
        return new Vector2(
            viewport.Center.X + (world.X - _cameraPosition.X),
            viewport.Center.Y + (world.Y - _cameraPosition.Y));
    }
}
