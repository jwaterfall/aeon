using Godot;
using System;

public partial class Configuration : Node
{
	public static Vector3 CHUNK_DIMENSION = new(16, 32, 16);
	public static Vector2 TEXTURE_ATLAS_SIZE = new(8, 2);
}
