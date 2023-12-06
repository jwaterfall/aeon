using System;
using System.IO;

public abstract class FileLoader
{
    protected string directory;
    protected string extension;
    public bool loaded = false;

    public FileLoader(string directory, string extension)
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

            var name = Path.GetFileNameWithoutExtension(file);
            LoadFile(name);
        }

        loaded = true;
    }
    
    protected abstract void LoadFile(string name);
}
