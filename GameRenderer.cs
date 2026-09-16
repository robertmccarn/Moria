using System.Drawing;
using System.Drawing.Drawing2D;
using Moria.Art;
using Moria.Core;
using Moria.Entities;
using Moria.Rendering;
using Moria.UI;
using Moria.World;

namespace Moria;

public sealed partial class Game
{
    private const int RenderScale = 3;
    private const int WorldPixelWidth = 1696;
    private const int WorldPixelHeight = 640;
    private const int WorldOffsetY = 64;

    private readonly AssetAtlas assets = new();
    private readonly Camera2D camera = new(106, 40, 16);
    private readonly UiRenderer uiRenderer = new();
    private Position lastRenderedPlayerPosition;
    private Direction playerFacing = Direction.Down;

    private void DrawFrame(Graphics target)
    {
        target.Clear(Color.Black);
        target.SmoothingMode = SmoothingMode.None;
        target.InterpolationMode = InterpolationMode.NearestNeighbor;
        target.PixelOffsetMode = PixelOffsetMode.Half;

        if (!started)
        {
            DrawScaledLogicalScreen(target, DrawTitleScreen);
            return;
        }

        DrawWorld(target);

        GraphicsState state = target.Save();
        try
        {
            target.ScaleTransform(RenderScale, RenderScale);
            uiRenderer.Draw(target, player, message, runOver, victory, lastRunReward);
        }
        finally
        {
            target.Restore(state);
        }
    }

    private static void DrawScaledLogicalScreen(Graphics target, Action<Graphics> draw)
    {
        GraphicsState state = target.Save();
        try
        {
            target.ScaleTransform(RenderScale, RenderScale);
            draw(target);
        }
        finally
        {
            target.Restore(state);
        }
    }

    private void DrawTitleScreen(Graphics g)
    {
        g.Clear(Color.FromArgb(4, 5, 8));

        using SolidBrush upperGlow = new(Color.FromArgb(10, 12, 18));
        g.FillRectangle(upperGlow, 0, 0, VirtualCanvas.Width, 215);
        DrawCavernBackdrop(g);

        using Pen outer = new(Color.FromArgb(68, 72, 82), 2);
        using Pen inner = new(Color.FromArgb(38, 41, 49), 1);
        g.DrawRectangle(outer, 72, 48, 496, 256);
        g.DrawRectangle(inner, 78, 54, 484, 244);

        using Font titleShadow = new("Consolas", 38, FontStyle.Bold);
        using Font title = new("Consolas", 38, FontStyle.Bold);
        using Font subtitle = new("Consolas", 9, FontStyle.Bold);
        using Font label = new("Consolas", 10, FontStyle.Bold);
        using Font input = new("Consolas", 14, FontStyle.Bold);
        using Font small = new("Consolas", 8, FontStyle.Bold);

        using SolidBrush titleShadowBrush = new(Color.FromArgb(20, 22, 27));
        using SolidBrush titleBrush = new(Color.FromArgb(224, 176, 62));
        using SolidBrush subtitleBrush = new(Color.FromArgb(154, 158, 168));
        using SolidBrush labelBrush = new(Color.FromArgb(218, 220, 224));
        using SolidBrush divider = new(Color.FromArgb(112, 91, 47));
        using SolidBrush beginBrush = new(Color.FromArgb(210, 170, 75));
        using SolidBrush hintBrush = new(Color.FromArgb(112, 116, 126));
        using SolidBrush legacyBrush = new(Color.FromArgb(122, 128, 138));

        DrawCentered(g, "MORIA", titleShadow, titleShadowBrush, 106, 3);
        DrawCentered(g, "MORIA", title, titleBrush, 103);
        DrawCentered(g, "A DESCENT INTO DARKNESS", subtitle, subtitleBrush, 151);

        g.FillRectangle(divider, 142, 168, 356, 1);
        g.FillRectangle(divider, 222, 167, 196, 3);
        DrawCentered(g, "ENTER YOUR NAME", label, labelBrush, 188);

        Rectangle nameBox = new(174, 208, 292, 36);
        using SolidBrush inputBackground = new(Color.FromArgb(10, 12, 16));
        using Pen inputBorder = new(Color.FromArgb(132, 106, 52), 2);
        g.FillRectangle(inputBackground, nameBox);
        g.DrawRectangle(inputBorder, nameBox);

        string displayName = titleName.Length == 0 ? "NAME" : titleName;
        using SolidBrush inputText = new(titleName.Length == 0 ? Color.FromArgb(82, 86, 96) : Color.Gainsboro);
        SizeF textSize = g.MeasureString(displayName, input);
        float textX = nameBox.X + (nameBox.Width - textSize.Width) / 2f;
        g.DrawString(displayName, input, inputText, textX, nameBox.Y + 7);

        if (titleName.Length > 0 && (Environment.TickCount / 500) % 2 == 0)
        {
            float cursorX = Math.Min(nameBox.Right - 12, textX + textSize.Width + 2);
            using Pen cursor = new(Color.FromArgb(224, 176, 62), 2);
            g.DrawLine(cursor, cursorX, nameBox.Y + 7, cursorX, nameBox.Bottom - 7);
        }

        DrawCentered(g, "ENTER  BEGIN RUN", small, beginBrush, 258);
        DrawCentered(g, "BACKSPACE  EDIT     ESC  QUIT", small, hintBrush, 274);
        DrawCentered(g, $"LEGACY GOLD  {LoadLegacyGold():N0}", small, legacyBrush, 290);
    }

    private static void DrawCavernBackdrop(Graphics g)
    {
        using SolidBrush rock = new(Color.FromArgb(20, 22, 27));
        using SolidBrush rockLight = new(Color.FromArgb(29, 31, 37));
        using SolidBrush deep = new(Color.FromArgb(11, 13, 17));

        Point[] ceiling =
        [
            new(0, 0), new(0, 70), new(72, 70), new(92, 58), new(130, 64),
            new(168, 45), new(214, 55), new(258, 38), new(304, 54),
            new(350, 40), new(398, 57), new(444, 44), new(486, 61),
            new(528, 48), new(568, 70), new(640, 70), new(640, 0)
        ];
        g.FillPolygon(rock, ceiling);

        Point[] floor =
        [
            new(0, 360), new(0, 322), new(72, 322), new(102, 311), new(146, 318),
            new(190, 305), new(236, 316), new(278, 302), new(320, 315),
            new(364, 303), new(410, 317), new(456, 307), new(502, 319),
            new(548, 309), new(584, 322), new(640, 322), new(640, 360)
        ];
        g.FillPolygon(deep, floor);

        g.FillRectangle(rockLight, 112, 63, 6, 18);
        g.FillRectangle(rockLight, 524, 66, 7, 22);
        g.FillRectangle(rockLight, 88, 86, 4, 12);
        g.FillRectangle(rockLight, 552, 91, 5, 16);

        using Pen path = new(Color.FromArgb(31, 33, 39), 1);
        g.DrawLine(path, 95, 320, 160, 296);
        g.DrawLine(path, 160, 296, 216, 320);
        g.DrawLine(path, 424, 320, 480, 297);
        g.DrawLine(path, 480, 297, 545, 321);
    }

    private static void DrawCentered(Graphics g, string text, Font font, Brush brush, float y, float xOffset = 0)
    {
        SizeF size = g.MeasureString(text, font);
        float x = (VirtualCanvas.Width - size.Width) / 2f + xOffset;
        g.DrawString(text, font, brush, x, y);
    }

    private void DrawWorld(Graphics g)
    {
        camera.Follow(player.Position, dungeon.Width, dungeon.Height);
        g.Clear(DepthBackgroundColor(player.DungeonLevel));

        GraphicsState worldState = g.Save();
        try
        {
            g.TranslateTransform((1920 - WorldPixelWidth) / 2f, WorldOffsetY);

            if (lastRenderedPlayerPosition != player.Position)
            {
                int dx = player.Position.X - lastRenderedPlayerPosition.X;
                int dy = player.Position.Y - lastRenderedPlayerPosition.Y;
                if (Math.Abs(dx) >= Math.Abs(dy) && dx != 0)
                    playerFacing = dx > 0 ? Direction.Right : Direction.Left;
                else if (dy != 0)
                    playerFacing = dy > 0 ? Direction.Down : Direction.Up;
                lastRenderedPlayerPosition = player.Position;
            }

            int minX = camera.X;
            int maxX = Math.Min(dungeon.Width - 1, camera.X + camera.ViewWidthTiles - 1);
            int minY = camera.Y;
            int maxY = Math.Min(dungeon.Height - 1, camera.Y + camera.ViewHeightTiles - 1);

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                Position position = new(y, x);
                Rectangle rect = camera.TileRectangle(position);
                if (!visibility.HasBeenSeen(position))
                {
                    using SolidBrush black = new(Color.FromArgb(255, 3, 4, 7));
                    g.FillRectangle(black, rect);
                    continue;
                }

                if (dungeon[position].Type == TileType.Floor)
                    assets.DrawFloorBlock(g, rect);
                else
                    assets.DrawTile(g, dungeon[position].Type, rect, x, y);

                if (!visibility.IsVisible(position))
                {
                    using SolidBrush fog = new(Color.FromArgb(175, 4, 5, 8));
                    g.FillRectangle(fog, rect);
                }
                else
                {
                    DrawDepthTint(g, rect, player.DungeonLevel);
                    DrawEntities(g, position, rect);
                }
            }

            if (visibility.IsVisible(player.Position))
            {
                Rectangle playerTile = camera.TileRectangle(player.Position);
                DrawPlayerEnergy(g, playerTile);
                assets.DrawPlayer(g, CenteredSpriteRect(playerTile, 45), playerFacing, player.Alive);
            }

            if (runOver || victory) DrawDeathOverlay(g);
        }
        finally
        {
            g.Restore(worldState);
        }
    }

    private void DrawEntities(Graphics g, Position position, Rectangle rect)
    {
        if (position == player.Position) return;
        Monster? monster = dungeon.MonsterAt(position);
        if (monster != null)
        {
            int size = monster.IsBoss ? 84 : 45;
            assets.DrawMonster(g, CenteredSpriteRect(rect, size), monster);
            DrawMonsterMarker(g, rect, monster);
            return;
        }

        Tile tile = dungeon[position];
        if (tile.GearLoot.Count > 0)
            assets.DrawGear(g, CenteredSpriteRect(rect, 42), tile.GearLoot[^1]);
        else if (tile.HasItem)
            assets.DrawPotion(g, CenteredSpriteRect(rect, 42));
    }

    private static Color DepthBackgroundColor(int level)
    {
        double progress = Math.Clamp((level - 1) / (double)(Dungeon.MaximumDepth - 1), 0.0, 1.0);
        int red = 4 + (int)Math.Round(progress * 22);
        int green = 6 - (int)Math.Round(progress * 4);
        int blue = 9 - (int)Math.Round(progress * 4);
        return Color.FromArgb(red, Math.Max(1, green), Math.Max(2, blue));
    }

    private static void DrawDepthTint(Graphics g, Rectangle rect, int level)
    {
        double progress = Math.Clamp((level - 1) / (double)(Dungeon.MaximumDepth - 1), 0.0, 1.0);
        int alpha = (int)Math.Round(progress * 70);
        if (alpha <= 0) return;
        using SolidBrush tint = new(Color.FromArgb(alpha, 145, 12, 12));
        g.FillRectangle(tint, rect);
    }

    private static Rectangle CenteredSpriteRect(Rectangle tile, int size) =>
        new(tile.X + (tile.Width - size) / 2, tile.Y + (tile.Height - size) / 2, size, size);

    private static void DrawMonsterMarker(Graphics g, Rectangle tile, Monster monster)
    {
        int size = monster.IsBoss ? 12 : 9;
        using SolidBrush marker = new(Color.FromArgb(monster.IsBoss ? 235 : 205, 190, 45, 40));
        g.FillEllipse(marker, tile.Right - size - 3, tile.Y + 3, size, size);
    }

    private static void DrawPlayerEnergy(Graphics g, Rectangle tile)
    {
        using SolidBrush glow = new(Color.FromArgb(80, 75, 165, 205));
        g.FillEllipse(glow, tile.X + 3, tile.Bottom - 15, tile.Width - 6, 12);
    }

    private void DrawDeathOverlay(Graphics g)
    {
        using SolidBrush shade = new(Color.FromArgb(190, 0, 0, 0));
        g.FillRectangle(shade, 0, 0, WorldPixelWidth, WorldPixelHeight);
        using Font title = new("Segoe UI", 22, FontStyle.Bold);
        using Font body = new("Segoe UI", 10, FontStyle.Bold);
        using SolidBrush text = new(Color.Gainsboro);
        string heading = victory ? "MORIA CONQUERED" : "YOU DIED";
        string detail = victory ? $"Legacy Gold earned: {lastRunReward}" : $"Legacy Gold recovered: {lastRunReward}";
        g.DrawString(heading, title, text, 214, 95);
        g.DrawString(detail, body, text, 244, 132);
        g.DrawString("N: New Run    ESC: Quit", body, text, 242, 154);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        DrawFrame(e.Graphics);
    }
}