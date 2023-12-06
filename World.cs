using Godot;
using System;

public partial class World : Node3D
{
    private TextureAtlasLoader blockTextureAtlasLoader;
    private BlockTypes blockTypes;

    public override void _Ready()
    {
        blockTextureAtlasLoader = new TextureAtlasLoader(Configuration.TEXTURE_SIZE, "textures/blocks");
        blockTypes = new BlockTypes(blockTextureAtlasLoader.Load());
        blockTypes.Load();
    }

    public override void _Process(double delta)
    {
        Node3D player = GetNode<Player>("Player");
        ChunkManager chunkManager = GetNode<ChunkManager>("ChunkManager");

        if (blockTypes != null && !blockTypes.loaded)
        {
            return;
        }

        chunkManager.Update(player.Position, blockTypes, blockTextureAtlasLoader);
    }
}
