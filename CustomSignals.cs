using Godot;

namespace Aeon
{
    public partial class CustomSignals : Node
    {
        [Signal]
        public delegate void BreakBlockEventHandler(Vector3I worldPosition);
        [Signal]
        public delegate void PlaceBlockEventHandler(Vector3I worldPosition, string block);
    }

}