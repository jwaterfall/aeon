using Godot;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

public class BlockType
{
    public string Name;
    public bool Transparent;
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
    public class BlockTypes
    {
        protected string directory = "data/blocks";
        protected string extension = ".yaml";
        public bool loaded = false;
        public ConcurrentDictionary<string, BlockType> blockTypes = new();

        private static BlockTypes _instance;

        public static BlockTypes Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new BlockTypes();
                }
                return _instance;
            }
        }

        private BlockTypes() {}

        public BlockType Get(string name)
        {
            return blockTypes.ContainsKey(name) ? blockTypes[name] : null;
        }

        public void Load(Dictionary<string, Vector2I> textureAtlasOffsets)
        {
            foreach (var file in Directory.GetFiles(directory))
            {
                if (Path.GetExtension(file) != extension)
                {
                    continue;
                }

                LoadFile(Path.GetFileNameWithoutExtension(file), textureAtlasOffsets);
            }

            loaded = true;
        }

        protected void LoadFile(string name, Dictionary<string, Vector2I> textureAtlasOffsets)
        {
            var deserializer = new DeserializerBuilder()
                .Build();

            var text = File.ReadAllText($"{directory}/{name}{extension}");
            var data = deserializer.Deserialize<RawBlockType>(text);

            var blockType = new BlockType
            {
                Name = name,
                Transparent = data.Transparent,
                TextureAtlasOffsetTop = textureAtlasOffsets[data.Textures.Top],
                TextureAtlasOffsetBottom = textureAtlasOffsets[data.Textures.Bottom],
                TextureAtlasOffsetLeft = textureAtlasOffsets[data.Textures.Left],
                TextureAtlasOffsetRight = textureAtlasOffsets[data.Textures.Right],
                TextureAtlasOffsetFront = textureAtlasOffsets[data.Textures.Front],
                TextureAtlasOffsetBack = textureAtlasOffsets[data.Textures.Back]
            };

            blockTypes[name] = blockType;
        }
    }
}