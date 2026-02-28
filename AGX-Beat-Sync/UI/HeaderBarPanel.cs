using AGX_Beat_Sync.Editor;
using AGX_Beat_Sync.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Top header bar with File and Edit menus.
/// </summary>
public class HeaderBarPanel
{
    private const int MenuItemHeight = 24;
    private const int DropdownPadding = 6;
    private const int DropdownMinWidth = 180;
    private const int MenuButtonWidth = 48;
    private const int MenuButtonHeight = 22;
    private const int ButtonGap = 2;

    private Rectangle _fileButtonRect;
    private Rectangle _editButtonRect;
    private Rectangle _dropdownRect;
    private readonly Rectangle[] _menuItemRects = new Rectangle[8]; // max of File(5) and Edit(5)
    private int _openDropdown; // 0 = none, 1 = File, 2 = Edit

    private static readonly string[] FileItems = { "Save", "Save As…", "Import Music…", "Open Project…", "Recent Projects" };
    private static readonly string[] EditItems = { "Undo", "Redo", "Cut", "Copy", "Paste" };

    public GraphicsDevice? GraphicsDevice { get; set; }
    public InputManager? Input { get; set; }
    public Rectangle Bounds { get; set; }

    // File
    public Action? OnSave { get; set; }
    public Action? OnSaveAs { get; set; }
    public Action? OnImportMusic { get; set; }
    public Action? OnOpenProject { get; set; }
    public Action? OnRecentProjects { get; set; }

    // Edit
    public Action? OnUndo { get; set; }
    public Action? OnRedo { get; set; }
    public Action? OnCut { get; set; }
    public Action? OnCopy { get; set; }
    public Action? OnPaste { get; set; }

    public void Update(GameTime gameTime)
    {
        if (Input == null) return;

        int y = Bounds.Y + (Bounds.Height - MenuButtonHeight) / 2;
        _fileButtonRect = new Rectangle(Bounds.X + 6, y, MenuButtonWidth, MenuButtonHeight);
        _editButtonRect = new Rectangle(_fileButtonRect.Right + ButtonGap, y, MenuButtonWidth, MenuButtonHeight);

        if (Input.MouseLeftPressed)
        {
            var mouse = Input.MousePosition;
            if (_openDropdown != 0)
            {
                string[] items = _openDropdown == 1 ? FileItems : EditItems;
                int n = items.Length;
                bool hitItem = false;
                for (int i = 0; i < n && i < _menuItemRects.Length; i++)
                {
                    if (_menuItemRects[i].Contains(mouse))
                    {
                        hitItem = true;
                        int which = _openDropdown;
                        _openDropdown = 0;
                        if (which == 1)
                        {
                            switch (i)
                            {
                                case 0: OnSave?.Invoke(); break;
                                case 1: OnSaveAs?.Invoke(); break;
                                case 2: OnImportMusic?.Invoke(); break;
                                case 3: OnOpenProject?.Invoke(); break;
                                case 4: OnRecentProjects?.Invoke(); break;
                            }
                        }
                        else
                        {
                            switch (i)
                            {
                                case 0: OnUndo?.Invoke(); break;
                                case 1: OnRedo?.Invoke(); break;
                                case 2: OnCut?.Invoke(); break;
                                case 3: OnCopy?.Invoke(); break;
                                case 4: OnPaste?.Invoke(); break;
                            }
                        }
                        break;
                    }
                }
                if (!hitItem && !_fileButtonRect.Contains(mouse) && !_editButtonRect.Contains(mouse) && !_dropdownRect.Contains(mouse))
                    _openDropdown = 0;
            }
            else
            {
                if (_fileButtonRect.Contains(mouse))
                    _openDropdown = 1;
                else if (_editButtonRect.Contains(mouse))
                    _openDropdown = 2;
            }
        }

        if (_openDropdown == 1)
        {
            int dh = FileItems.Length * MenuItemHeight + DropdownPadding * 2;
            _dropdownRect = new Rectangle(_fileButtonRect.X, _fileButtonRect.Bottom + 2, DropdownMinWidth, dh);
            for (int i = 0; i < FileItems.Length; i++)
                _menuItemRects[i] = new Rectangle(_dropdownRect.X, _dropdownRect.Y + DropdownPadding + i * MenuItemHeight, _dropdownRect.Width, MenuItemHeight);
        }
        else if (_openDropdown == 2)
        {
            int dh = EditItems.Length * MenuItemHeight + DropdownPadding * 2;
            _dropdownRect = new Rectangle(_editButtonRect.X, _editButtonRect.Bottom + 2, DropdownMinWidth, dh);
            for (int i = 0; i < EditItems.Length; i++)
                _menuItemRects[i] = new Rectangle(_dropdownRect.X, _dropdownRect.Y + DropdownPadding + i * MenuItemHeight, _dropdownRect.Width, MenuItemHeight);
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (GraphicsDevice == null) return;
        var pixel = PanelBase.GetPixelTexture(spriteBatch.GraphicsDevice);
        var mouse = Input?.MousePosition ?? Point.Zero;

        // Bar background
        spriteBatch.Draw(pixel, Bounds, new Color(38, 40, 44));
        spriteBatch.Draw(pixel, new Rectangle(Bounds.X, Bounds.Bottom - 1, Bounds.Width, 1), new Color(55, 58, 64));

        // File button
        bool fileHover = _fileButtonRect.Contains(mouse);
        spriteBatch.Draw(pixel, _fileButtonRect, _openDropdown == 1 || fileHover ? new Color(55, 58, 65) : new Color(42, 45, 50));
        InspectorDrawer.DrawLabel(spriteBatch, GraphicsDevice, _fileButtonRect.X + 8, _fileButtonRect.Y + 4, "File", pixel, new Color(235, 235, 240));

        // Edit button
        bool editHover = _editButtonRect.Contains(mouse);
        spriteBatch.Draw(pixel, _editButtonRect, _openDropdown == 2 || editHover ? new Color(55, 58, 65) : new Color(42, 45, 50));
        InspectorDrawer.DrawLabel(spriteBatch, GraphicsDevice, _editButtonRect.X + 6, _editButtonRect.Y + 4, "Edit", pixel, new Color(235, 235, 240));

        // Dropdown
        if (_openDropdown != 0)
        {
            string[] items = _openDropdown == 1 ? FileItems : EditItems;
            spriteBatch.Draw(pixel, _dropdownRect, new Color(38, 40, 44));
            spriteBatch.Draw(pixel, new Rectangle(_dropdownRect.X, _dropdownRect.Y, _dropdownRect.Width, 1), new Color(60, 64, 72));
            spriteBatch.Draw(pixel, new Rectangle(_dropdownRect.X, _dropdownRect.Bottom - 1, _dropdownRect.Width, 1), new Color(60, 64, 72));
            spriteBatch.Draw(pixel, new Rectangle(_dropdownRect.X, _dropdownRect.Y, 1, _dropdownRect.Height), new Color(60, 64, 72));
            spriteBatch.Draw(pixel, new Rectangle(_dropdownRect.Right - 1, _dropdownRect.Y, 1, _dropdownRect.Height), new Color(60, 64, 72));

            for (int i = 0; i < items.Length; i++)
            {
                var r = _menuItemRects[i];
                bool hover = r.Contains(mouse);
                if (hover)
                    spriteBatch.Draw(pixel, r, new Color(52, 56, 62));
                InspectorDrawer.DrawLabel(spriteBatch, GraphicsDevice, r.X + DropdownPadding, r.Y + 3, items[i], pixel, new Color(235, 235, 240));
            }
        }
    }

    public bool ContainsPoint(Point point) => Bounds.Contains(point);

    public string? GetHoverText(Point mouse)
    {
        if (!ContainsPoint(mouse)) return null;
        if (_fileButtonRect.Contains(mouse)) return "File menu";
        if (_editButtonRect.Contains(mouse)) return "Edit menu";
        return "Header";
    }
}
