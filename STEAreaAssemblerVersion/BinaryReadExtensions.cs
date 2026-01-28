using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STEAreaAssemblerVersion
{
    public static class BinaryReaderExtensions
    {
        public static string ReadString(this BinaryReader reader, bool terminator)
        {
            var value = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));

            return terminator ? value[..^1] : value;
        }
    }
}
