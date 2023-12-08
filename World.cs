using Godot;

namespace Aeon
{
    public partial class World : Node3D
    {
        public override void _Ready()
        {
            BlockTypes.Instance.Load(BlockTextures.Instance.Load());

            GD.Print("Loaded textures");
            GD.Print($"Loaded {BlockTypes.Instance.blockTypes.Count} block types");
        }

        public override void _Process(double delta)
        {
            Node3D player = GetNode<Player>("Player");
            ChunkManager chunkManager = GetNode<ChunkManager>("ChunkManager");

            if (!BlockTypes.Instance.loaded)
            {
                return;
            }

            chunkManager.Update(player.Position);
        }
    }
}
