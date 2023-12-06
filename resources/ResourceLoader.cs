using System.IO;

namespace Aeon
{
    public abstract class ResourceLoader
    {
        protected string directory;
        protected string extension;
        public bool loaded = false;

        public ResourceLoader(string directory, string extension)
        {
            this.directory = directory;
            this.extension = extension;
        }

        public void Load()
        {
            foreach (var file in Directory.GetFiles(directory))
            {
                if (Path.GetExtension(file) != extension)
                {
                    continue;
                }

                LoadFile(Path.GetFileNameWithoutExtension(file));
            }

            loaded = true;
        }

        protected abstract void LoadFile(string name);
    }
}