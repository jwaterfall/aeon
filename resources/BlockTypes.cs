using Godot;
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
    public Textures Textures { get; set; }
}

public class Textures
{
    public string Top { get; set; }
    public string Bottom { get; set; }
    public string Left { get; set; }
    public string Right { get; set; }
    public string Front { get; set; }
    public string Back { get; set; }
}

namespace Aeon
{
    public class BlockTypes : ResourceLoader
    {
        public Dictionary<string, BlockType> blockTypes = new();
        public Dictionary<string, Vector2I> textureAtlasOffsets;

        public BlockTypes(Dictionary<string, Vector2I> textureAtlasOffsets) : base("data/blocks", ".yaml")
        {
            this.textureAtlasOffsets = textureAtlasOffsets;
        }

        public BlockType Get(string name)
        {
            return blockTypes.ContainsKey(name) ? blockTypes[name] : null;
        }

        protected override void LoadFile(string name)
        {
            var deserializer = new DeserializerBuilder()
                .Build();

            var text = File.ReadAllText($"{directory}/{name}{extension}");
            var data = deserializer.Deserialize<RawBlockType>(text);

            var blockType = new BlockType
            {
                Name = name,
                Solid = !data.Transparent,
                TextureAtlasOffsetTop = textureAtlasOffsets[data.Textures.Top],
                TextureAtlasOffsetBottom = textureAtlasOffsets[data.Textures.Bottom],
                TextureAtlasOffsetLeft = textureAtlasOffsets[data.Textures.Left],
                TextureAtlasOffsetRight = textureAtlasOffsets[data.Textures.Right],
                TextureAtlasOffsetFront = textureAtlasOffsets[data.Textures.Front],
                TextureAtlasOffsetBack = textureAtlasOffsets[data.Textures.Back]
            };

            blockTypes.Add(name, blockType);
        }
    }

}