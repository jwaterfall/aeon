using Godot;

namespace Aeon 
{
    public partial class Configuration : Node
    {
        public static readonly Vector3I CHUNK_DIMENSION = new(32, 256, 32);
        public static readonly Vector2I TEXTURE_ATLAS_SIZE = new(8, 2);
        public static readonly float MOUSE_SENSITIVITY = 0.3f;
        public static readonly float MOVEMENT_SPEED = 50;
        public static readonly float GRAVITY = 30;
        public static readonly float JUMP_VELOCITY = 7.5f;
        public static readonly bool FLYING_ENABLED = true;
        public static readonly int CHUNK_LOAD_RADIUS = 10;
        public static readonly int MAX_STALE_CHUNKS = 100;
        public static readonly int TEXTURE_SIZE = 32;
    }
}