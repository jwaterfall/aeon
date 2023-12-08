namespace Aeon
{
    public class BlockTextures : TextureAtlasLoader
    {
        private static BlockTextures _instance;

        public static BlockTextures Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new BlockTextures();
                }
                return _instance;
            }
        }

        private BlockTextures() : base(Configuration.TEXTURE_SIZE, "textures/blocks") {}
    }
}