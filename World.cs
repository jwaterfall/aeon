using Godot;

namespace Aeon
{
    public partial class World : Node3D
    {
        private Player _player;
        private ChunkManager _chunkManager;
        private TerrainGenerator _terrainGenerator;

        public override void _Ready()
        {
            BlockTypes.Instance.Load(BlockTextures.Instance.Load());
            WorldPresets.Instance.Load();

            _player = GetNode<Player>("Player");
            _chunkManager = GetNode<ChunkManager>("ChunkManager");
            _terrainGenerator = GetNode<TerrainGenerator>("/root/TerrainGenerator");
        }

        public override void _Process(double delta)
        {
            if (!BlockTextures.Instance.loaded || !BlockTypes.Instance.loaded || !_terrainGenerator.initialized)
            {
                return;
            }

            _chunkManager.Update(_player.Position, _terrainGenerator);
        }
    }
}
