using Godot;

namespace Aeon
{
    public partial class World : Node3D
    {
        public override void _Ready()
        {
            BlockTypes.Instance.Load(BlockTextures.Instance.Load());
        }

        public override void _Process(double delta)
        {
            Node3D player = GetNode<Player>("Player");
            ChunkManager chunkManager = GetNode<ChunkManager>("ChunkManager");

            if (!BlockTextures.Instance.loaded || !BlockTypes.Instance.loaded)
            {
                return;
            }

            chunkManager.Update(player.Position);
        }
    }
}
