using Godot;
using System;

public class BlockType
{
    public string Name;
	public bool Solid = true;
    public Vector2I TextureAtlasOffsetTop = Vector2I.Zero;
	public Vector2I TextureAtlasOffsetBottom = Vector2I.Zero;
    public Vector2I TextureAtlasOffsetLeft = Vector2I.Zero;
	public Vector2I TextureAtlasOffsetRight = Vector2I.Zero;
    public Vector2I TextureAtlasOffsetFront = Vector2I.Zero;
    public Vector2I TextureAtlasOffsetBack = Vector2I.Zero;
}

public static class BlockTypes
{
    public static readonly BlockType Air = new BlockType
    { 
        Solid = false,
    };

    public static readonly BlockType Grass = new BlockType
    {
        TextureAtlasOffsetTop = new(0, 0),
        TextureAtlasOffsetBottom = new(2, 0),
        TextureAtlasOffsetLeft = new(1, 0),
        TextureAtlasOffsetRight = new(1, 0),
        TextureAtlasOffsetFront = new(1, 0),
        TextureAtlasOffsetBack = new(1, 0),
    };

    public static readonly BlockType Dirt = new BlockType
    {
        TextureAtlasOffsetTop = new(2, 0),
        TextureAtlasOffsetBottom = new(2, 0),
        TextureAtlasOffsetLeft = new(2, 0),
        TextureAtlasOffsetRight = new(2, 0),
        TextureAtlasOffsetFront = new(2, 0),
        TextureAtlasOffsetBack = new(2, 0),
    };

    public static readonly BlockType Stone = new BlockType
    {
        TextureAtlasOffsetTop = new(3, 0),
        TextureAtlasOffsetBottom = new(3, 0),
        TextureAtlasOffsetLeft = new(3, 0),
        TextureAtlasOffsetRight = new(3, 0),
        TextureAtlasOffsetFront = new(3, 0),
        TextureAtlasOffsetBack = new(3, 0),
    };
}
