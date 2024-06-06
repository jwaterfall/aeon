using Godot;

namespace Aeon
{
    public partial class World : Node3D
    {
        Player Player;
        ChunkManager ChunkManager;
        TerrainGenerator TerrainGenerator;

        public override void _Ready()
        {
            BlockTypes.Instance.Load(BlockTextures.Instance.Load());
            WorldPresets.Instance.Load();

            Player = GetNode<Player>("Player");
            ChunkManager = GetNode<ChunkManager>("ChunkManager");
            TerrainGenerator = GetNode<TerrainGenerator>("/root/TerrainGenerator");
        }

        public override void _Process(double delta)
        {
            if (!BlockTextures.Instance.loaded || !BlockTypes.Instance.loaded || !TerrainGenerator.initialized)
            {
                return;
            }

            ChunkManager.Update(Player.Position, TerrainGenerator);
        }
    }
}
