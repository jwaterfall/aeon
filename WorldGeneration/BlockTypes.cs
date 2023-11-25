using Godot;
using System;

public class BlockType
{
    public string Name;
	public bool Solid = true;
    public Vector2 TextureAtlasOffsetTop = Vector2.Zero;
	public Vector2 TextureAtlasOffsetBottom = Vector2.Zero;
    public Vector2 TextureAtlasOffsetLeft = Vector2.Zero;
	public Vector2 TextureAtlasOffsetRight = Vector2.Zero;
    public Vector2 TextureAtlasOffsetFront = Vector2.Zero;
    public Vector2 TextureAtlasOffsetBack = Vector2.Zero;
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

    public static readonly BlockType Water = new BlockType
    {
        Solid = false,
        TextureAtlasOffsetTop = new(1, 1),
        TextureAtlasOffsetBottom = new(1, 1),
        TextureAtlasOffsetLeft = new(1, 1),
        TextureAtlasOffsetRight = new(1, 1),
        TextureAtlasOffsetFront = new(1, 1),
        TextureAtlasOffsetBack = new(1, 1),
    };
}
