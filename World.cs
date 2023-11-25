using Godot;
using System;

public partial class World : Node3D
{
    public override void _Process(double delta)
    {
        Node3D player = GetNode<Player>("Player");
        ChunkManager chunkManager = GetNode<ChunkManager>("ChunkManager");

        chunkManager.Update(player.Position);
    }
}
