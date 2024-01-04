using Godot;
using System.Collections.Concurrent;
using System.IO;
using YamlDotNet.Serialization;

public class WorldPreset
{
    public Ore[] Ores { get; set; }
}

public class Ore
{
    public string Block { get; set; }
    public int Size { get; set; }
    public int Frequency { get; set; }
    public int MinHeight { get; set; }
    public int MaxHeight { get; set; }
}

namespace Aeon
{
    public class WorldPresets
    {
        protected string directory = "data/world_presets";
        protected string extension = ".yaml";
        public bool loaded = false;
        public ConcurrentDictionary<string, WorldPreset> worldPresets = new();

        private static WorldPresets _instance;

        public static WorldPresets Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new WorldPresets();
                }
                return _instance;
            }
        }

        public WorldPreset Get(string name)
        {
            return worldPresets.ContainsKey(name) ? worldPresets[name] : null;
        }

        public void Load()
        {
            foreach (var file in Directory.GetFiles(directory))
            {
                if (Path.GetExtension(file) != extension)
                {
                    continue;
                }

                LoadFile(Path.GetFileNameWithoutExtension(file));
            }

            loaded = true;
        }

        protected void LoadFile(string name)
        {
            var deserializer = new DeserializerBuilder()
                .Build();

            var text = File.ReadAllText($"{directory}/{name}{extension}");
            var worldPreset = deserializer.Deserialize<WorldPreset>(text);

            worldPresets[name] = worldPreset;
        }
    }
}