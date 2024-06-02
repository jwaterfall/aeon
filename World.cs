using Godot;

namespace Aeon
{
    public partial class World : Node3D
    {
        public override void _Ready()
        {
            BlockTypes.Instance.Load(BlockTextures.Instance.Load());
            WorldPresets.Instance.Load();
        }

        public override void _Process(double delta)
        {
            var player = GetNode<Player>("Player");
            var chunkManager = GetNode<ChunkManager>("ChunkManager");
            var terrainGenerator = GetNode<TerrainGenerator>("/root/TerrainGenerator");

            if (!BlockTextures.Instance.loaded || !BlockTypes.Instance.loaded || !terrainGenerator.initialized)
            {
                return;
            }

            chunkManager.Update(player.Position, terrainGenerator);
        }
    }
}
