using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STEAreaAssemblerVersion
{
    public class Area
    {
        public static ulong areaID { get; set; }
        public Dictionary<int, string> textures { get; set; } = new();

        //TODO: Text Based Area Files
        public Dictionary<int, string> ReadFile(string datPath)
        {
            try
            {
                using (FileStream fs = new(datPath, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    char textIndicator = br.ReadChar();
                    if (textIndicator != '!')
                    {
                        br.BaseStream.Position -= 1;
                        ReadFileBinary(br, datPath);
                        return textures;
                    }
                    else
                    {
                        br.Close();
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                foreach (var kvp in textures)
                {
                    Console.WriteLine($"{kvp.Key}:{kvp.Value}");
                }
                Console.WriteLine($"Unhandled exception in AreaDat.ReadFile({datPath}): {ex.Message}\n{ex.StackTrace}");
                Debugger.Break();
                return null;
            }
        }

        private void ReadFileBinary(BinaryReader br, string datPath)
        {
            string signature = br.ReadString(true);
            br.BaseStream.Seek(0x2C, SeekOrigin.Begin);
            int terTextures = br.ReadInt32();
            br.BaseStream.Seek(terTextures, SeekOrigin.Begin);
            int numTextures = br.ReadInt32();
            for (int i = 0; i < numTextures; i++)
            {
                int texID = br.ReadInt32();
                int texLayer = br.ReadInt32();
                string texName = br.ReadString(true);
                if (textures.ContainsKey(texID))
                {
                    textures[texID] = texName;
                }
                else
                    textures.Add(texID, texName);
            }
        }
    }
}
