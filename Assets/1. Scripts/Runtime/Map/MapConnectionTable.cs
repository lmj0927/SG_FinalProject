using System.Collections.Generic;

public readonly struct MapExit
{
    public BackgroundType? Target { get; }
    public string Label { get; }

    public MapExit(BackgroundType? target, string label)
    {
        Target = target;
        Label = label;
    }

    public static MapExit None => new MapExit(null, string.Empty);
}

public static class MapConnectionTable
{
    private static readonly HashSet<BackgroundType> NavigableMaps = new HashSet<BackgroundType>
    {
        BackgroundType.Classroom,
        BackgroundType.Hallway,
        BackgroundType.Library,
        BackgroundType.Playground,
        BackgroundType.Canteen
    };

    private static readonly Dictionary<(BackgroundType location, MapDirection direction), MapExit> Exits =
        new Dictionary<(BackgroundType, MapDirection), MapExit>
        {
            { (BackgroundType.Classroom, MapDirection.Left), MapExit.None },
            { (BackgroundType.Classroom, MapDirection.Right), new MapExit(BackgroundType.Hallway, "복도") },
            { (BackgroundType.Classroom, MapDirection.Up), MapExit.None },

            { (BackgroundType.Hallway, MapDirection.Left), new MapExit(BackgroundType.Library, "도서관") },
            { (BackgroundType.Hallway, MapDirection.Right), new MapExit(BackgroundType.Playground, "운동장") },
            { (BackgroundType.Hallway, MapDirection.Up), new MapExit(BackgroundType.Classroom, "교실") },

            { (BackgroundType.Library, MapDirection.Left), new MapExit(BackgroundType.Hallway, "복도") },
            { (BackgroundType.Library, MapDirection.Right), MapExit.None },
            { (BackgroundType.Library, MapDirection.Up), MapExit.None },

            { (BackgroundType.Playground, MapDirection.Left), new MapExit(BackgroundType.Hallway, "복도") },
            { (BackgroundType.Playground, MapDirection.Right), new MapExit(BackgroundType.Canteen, "매점") },
            { (BackgroundType.Playground, MapDirection.Up), MapExit.None },

            { (BackgroundType.Canteen, MapDirection.Left), new MapExit(BackgroundType.Playground, "운동장") },
            { (BackgroundType.Canteen, MapDirection.Right), MapExit.None },
            { (BackgroundType.Canteen, MapDirection.Up), MapExit.None },
        };

    public static bool IsNavigableMap(BackgroundType location)
    {
        return NavigableMaps.Contains(location);
    }

    public static MapExit GetExit(BackgroundType location, MapDirection direction)
    {
        if (Exits.TryGetValue((location, direction), out MapExit exit))
        {
            return exit;
        }

        return MapExit.None;
    }
}
