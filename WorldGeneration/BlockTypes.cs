using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

public class BlockType
{
    public string Name;
    public bool Solid;
    public Vector2 TextureAtlasOffsetTop;
    public Vector2 TextureAtlasOffsetBottom;
    public Vector2 TextureAtlasOffsetLeft;
    public Vector2 TextureAtlasOffsetRight;
    public Vector2 TextureAtlasOffsetFront;
    public Vector2 TextureAtlasOffsetBack;
}

public class RawBlockType
{
    public bool Transparent { get; set; }
    public List<int> TextureAtlasOffsetTop { get; set; }
    public List<int> TextureAtlasOffsetBottom { get; set; }
    public List<int> TextureAtlasOffsetLeft { get; set; }
    public List<int> TextureAtlasOffsetRight { get; set; }
    public List<int> TextureAtlasOffsetFront { get; set; }
    public List<int> TextureAtlasOffsetBack { get; set; }
}

public static class BlockTypes
{
    public static bool loaded = false;
    public static Dictionary<string, BlockType> types = new();
    public static string blocksDirectory = "data/blocks";

    public static void Load()
    {
        types.Clear();
        
        foreach (var file in Directory.GetFiles(blocksDirectory))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var blockType = LoadBlock(name);
            types.Add(name, blockType);
        }

        loaded = true;
    }

    private static BlockType LoadBlock(string name)
    {
        var deserializer = new DeserializerBuilder()
            .Build();

        var text = File.ReadAllText($"{blocksDirectory}/{name}.yaml");
        var data = deserializer.Deserialize<RawBlockType>(text);

        return new BlockType
        {
            Name = name,
            Solid = !data.Transparent,
            TextureAtlasOffsetTop = new Vector2(data.TextureAtlasOffsetTop[0], data.TextureAtlasOffsetTop[1]),
            TextureAtlasOffsetBottom = new Vector2(data.TextureAtlasOffsetBottom[0], data.TextureAtlasOffsetBottom[1]),
            TextureAtlasOffsetLeft = new Vector2(data.TextureAtlasOffsetLeft[0], data.TextureAtlasOffsetLeft[1]),
            TextureAtlasOffsetRight = new Vector2(data.TextureAtlasOffsetRight[0], data.TextureAtlasOffsetRight[1]),
            TextureAtlasOffsetFront = new Vector2(data.TextureAtlasOffsetFront[0], data.TextureAtlasOffsetFront[1]),
            TextureAtlasOffsetBack = new Vector2(data.TextureAtlasOffsetBack[0], data.TextureAtlasOffsetBack[1]),
        };
    }

    public static BlockType Get(string name)
    {
        return types.ContainsKey(name) ? types[name] : null;
    }
}
