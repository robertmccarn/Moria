using Moria.Items;

namespace Moria.World;

public enum TileType { Rock, Floor, Wall, Door, StairsUp, StairsDown, Trap }

public sealed class Tile
{
    public TileType Type { get; set; } = TileType.Rock;
    public bool Lit { get; set; }
    public bool Seen { get; set; }
    public bool HasItem { get; set; }
    public Gear? Gear { get; set; }

    public char Symbol => Type switch
    {
        TileType.Floor => '.',
        TileType.Wall => '#',
        TileType.Door => '+',
        TileType.StairsUp => '<',
        TileType.StairsDown => '>',
        TileType.Trap => '^',
        _ => '#'
    };
}
