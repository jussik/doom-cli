using System.Text;
using System.IO.Compression;
using nz.doom.WadParser;

namespace DoomCli;

public interface ILumpArchive
{
    bool TryReadLump(string name, out string lump);
}

public class WadLumpArchive(Wad wad) : ILumpArchive
{
    public bool TryReadLump(string name, out string lump)
    {
        if (wad.GetLumpByName(name) is {Bytes: var bytes})
        {
            lump = Encoding.UTF8.GetString(bytes);
            return true;
        }

        lump = "";
        return false;
    }
}

public class ZipLumpArchive(ZipArchive zip) : ILumpArchive
{
    public bool TryReadLump(string name, out string lump)
    {
        if (zip.GetEntry(name) is { } entry)
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            lump = reader.ReadToEnd();
            return true;
        }
        lump = "";
        return false;
    }
}