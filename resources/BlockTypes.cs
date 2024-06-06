using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;


public enum Direction
{
    Down,
    Up,
    North,
    South,
    West,
    East
}

public class Face
{
    public List<Vector3> Vertices;
    public Vector2 TextureAtlasOffset;
    public Nullable<Direction> OccludedBy;
    public List<float> UV;
}

public class BlockType
{
    public byte Id;
    public string Name;
    public bool Transparent;
    public bool CullsSelf;
    public bool HasCollision;
    public List<Direction> Occludes;
    public List<Face> Faces;
}

public class RawBlock
{
    [DefaultValue(false)]
    public bool Transparent { get; set; } = false;
    [DefaultValue(false)]
    public bool CullsSelf { get; set; } = false;
    [DefaultValue(true)]
    public bool HasCollision { get; set; } = true;
    public string Model { get; set; }
    public Dictionary<string, string> Textures { get; set; }
}

public class RawFace
{
    public List<List<float>> Vertices { get; set; }
    public string Texture { get; set; }
    [DefaultValue(null)]
    public string OccludedBy { get; set; } = null;
    [DefaultValue(new int[] { 0, 0, 1, 1 })]
    public List<float> Uv { get; set; } = new() { 0, 0, 1, 1 };
}

public class RawModel
{
    [DefaultValue(new string[] { })]
    public List<string> Occludes { get; set; } = new() { };
    public List<RawFace> Faces { get; set; }
}

namespace Aeon
{
    public class BlockTypes
    {
        protected string blocksDirectory = "data/blocks";
        protected string modelsDirectory = "data/models";
        protected string extension = ".yaml";
        public bool loaded = false;
        public ConcurrentDictionary<byte, BlockType> blockTypesMap = new();
        protected ConcurrentDictionary<string, byte> nameToIdMap = new();

        private static BlockTypes _instance;

        public static BlockTypes Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new();
                }
                return _instance;
            }
        }

        private BlockTypes() { }

        public BlockType Get(byte id)
        {
            return blockTypesMap.ContainsKey(id) ? blockTypesMap[id] : null;
        }

        public BlockType Get(string name)
        {
            var id = nameToIdMap.ContainsKey(name) ? nameToIdMap[name] : (byte)0;
            return Get(id);
        }

        public void Load(Dictionary<string, Vector2I> textureAtlasOffsets)
        {
            var models = LoadModels();
            LoadBlocks(models, textureAtlasOffsets);
            loaded = true;
        }

        protected Dictionary<string, RawModel> LoadModels()
        {
            Dictionary<string, RawModel> models = new();

            foreach (var file in Directory.GetFiles(modelsDirectory))
            {
                if (Path.GetExtension(file) != extension)
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(file);
                var model = LoadModelFile(name);
                models.Add(name, model);
            }

            return models;
        }

        protected RawModel LoadModelFile(string name)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var text = File.ReadAllText($"{modelsDirectory}/{name}{extension}");
            var data = deserializer.Deserialize<RawModel>(text);

            return data;
        }

        protected void LoadBlocks(Dictionary<string, RawModel> models, Dictionary<string, Vector2I> textureAtlasOffsets)
        {
            foreach (var file in Directory.GetFiles(blocksDirectory))
            {
                if (Path.GetExtension(file) != extension)
                {
                    continue;
                }

                LoadBlockFile(Path.GetFileNameWithoutExtension(file), models, textureAtlasOffsets);
            }
        }

        protected void LoadBlockFile(string name, Dictionary<string, RawModel> models, Dictionary<string, Vector2I> textureAtlasOffsets)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var text = File.ReadAllText($"{blocksDirectory}/{name}{extension}");
            var data = deserializer.Deserialize<RawBlock>(text);

            var model = models[data.Model];

            var id = (byte)blockTypesMap.Count;

            var blockType = new BlockType
            {
                Id = id,
                Name = name,
                Transparent = data.Transparent,
                CullsSelf = data.CullsSelf,
                HasCollision = data.HasCollision,
                Occludes = model.Occludes.ConvertAll(s => (Direction)Enum.Parse(typeof(Direction), s, true)),
                Faces = new List<Face>()
            };

            foreach (var face in model.Faces)
            {
                var texture = data.Textures[face.Texture];

                if (textureAtlasOffsets.TryGetValue(texture, out Vector2I atlasOffset))
                {
                    blockType.Faces.Add(new Face
                    {
                        Vertices = face.Vertices.ConvertAll(v => new Vector3(v[0], v[1], v[2])),
                        TextureAtlasOffset = atlasOffset,
                        OccludedBy = face.OccludedBy == null ? null : (Direction)Enum.Parse(typeof(Direction), face.OccludedBy, true),
                        UV = face.Uv
                    });
                }
                else
                {
                    GD.Print($"Atlas offset not found for texture: {face.Texture}");
                }
            }

            blockTypesMap[id] = blockType;
            nameToIdMap[name] = id;
        }
    }
}
