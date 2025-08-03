using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReddWare.Language.Json.Conversion
{
    /// <summary>
    /// Describes the levels of chunk quote searching used in the JsonTokenizer chunking process (for handling huge strings)
    /// </summary>
    internal enum ChunkQuoteEnum
    {
        Unknown = 0,
        Escaped = 1, 
        Valid = 2, 
        Terminator = 3, 
        Orphaned = 4
    }
}
