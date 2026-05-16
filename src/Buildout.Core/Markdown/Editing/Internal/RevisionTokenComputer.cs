using System.Globalization;
using System.IO.Hashing;
using System.Text;

namespace Buildout.Core.Markdown.Editing.Internal;

public static class RevisionTokenComputer
{
    public static string Compute(string anchoredMarkdown)
    {
        uint crc = Crc32.HashToUInt32(Encoding.UTF8.GetBytes(anchoredMarkdown));
        return crc.ToString("x8", CultureInfo.InvariantCulture);
    }
}
