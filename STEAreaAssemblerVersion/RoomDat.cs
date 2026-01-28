using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ZstdSharp;

namespace STEAreaAssemblerVersion
{
    public class RoomDat
    {
        public void ReadFile(string datPath, string outPath)
        {
            try
            {
                Heightmap hm;
                using (FileStream fs = new(datPath, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    hm = new();
                    char textIndicator = br.ReadChar();
                    if (textIndicator != '!')
                    {
                        br.BaseStream.Position -= 1;
                        ReadFileBinary(br, hm, datPath, outPath);
                    }
                    else
                    {
                        br.Close();
                        ReadFileText(fs, hm, datPath, outPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception in RoomDat.ReadFile(): {ex.Message}\n{ex.StackTrace}");
                Debugger.Break();
            }
        }
        private void ReadFileBinary(BinaryReader br, Heightmap hm, string datPath, string outPath)
        {
            try
            {
                string Signature = br.ReadString(true); //0x00
                if (Signature != "ROOM_DAT_BINARY_FORMAT_")
                    return;
                int instancesOffset = br.ReadInt32();      //0x1C
                br.BaseStream.Position = instancesOffset;
                int numInstances = br.ReadInt32();
                for (int i = 0; i < numInstances; ++i)
                {
                    uint instanceHeader = br.ReadUInt32(); //0xABCD1234 (4)
                    if (instanceHeader != 0xABCD1234)
                        break;
                    br.BaseStream.Position += 1;           //null       (1)
                    ulong instanceID = br.ReadUInt64();    //UInt64     (8)
                    ulong assetID = br.ReadUInt64();       //UInt64     (8)
                    br.BaseStream.Position += 1;           //null       (1)
                    int numProps = br.ReadInt32();         //int        (4)
                    int PropertiesLength = br.ReadInt32(); //int        (4)
                    br.BaseStream.Position += 1;           //null       (1)
                    Vector3 position = new Vector3(0, 0, 0);
                    for (int j = 0; j < numProps; ++j)
                    {
                        Properties t = (Properties)br.ReadByte();
                        uint p = br.ReadUInt32();
                        switch (t)
                        {
                            case Properties.Bool:
                                br.BaseStream.Position += 1;
                                break;
                            case Properties.Int32:
                                br.BaseStream.Position += 4;
                                break;
                            case Properties.UInt32:
                                br.BaseStream.Position += 4;
                                break;
                            case Properties.Single:
                                br.BaseStream.Position += 4;
                                break;
                            case Properties.UInt64:
                                br.BaseStream.Position += 8;
                                break;
                            case Properties.Vector3:
                                switch (p)
                                {
                                    case 0x4f77e269:
                                        position.X = br.ReadSingle();
                                        position.Y = br.ReadSingle();
                                        position.Z = br.ReadSingle();
                                        break;
                                    default:
                                        br.BaseStream.Position += 12;
                                        break;
                                }
                                break;
                            case Properties.Vector4:
                                br.BaseStream.Position += 16;
                                break;
                            case Properties.String:
                                br.ReadString(true); // custom ReadString implementation that takes a boolean to tell whether or not the string has a "\0" terminator
                                break;
                            case Properties.Data:
                                switch (p)
                                {
                                    case 0xA3AB26AE:
                                        int length = br.ReadInt32();
                                        if (length == 0)
                                            break;
                                        ProcessHeightmap(br, hm, $"{instanceID}", length, outPath, datPath);
                                        break;
                                    default:
                                        length = br.ReadInt32();
                                        br.BaseStream.Position += length;
                                        break;
                                }
                                break;

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled Exception in RoomDat.ReadFileBinary(): {ex.Message}\n{ex.StackTrace}");
                Debugger.Break();
            }
        }
        private enum FileSection
        {
            Header,
            Instances,
            VisibleRooms,
            Settings,
            Unk
        }
        private Regex SectionParser = new Regex("\\[(\\w+)\\]");
        private Regex InstanceHeaderParser = new Regex("(\\d+)=(\\d+)");
        private void ReadFileText(FileStream fs, Heightmap hm, string datPath, string outPath)
        {
            try
            {
                FileSection currSection = FileSection.Header;
                string currLine;
                string trimmedLine;
                string instanceHeader = "";
                using (fs = new FileStream(datPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var sr = new StreamReader(fs, Encoding.UTF8, true, (int)fs.Length, true))
                {
                    while (!sr.EndOfStream)
                    {
                        currLine = sr.ReadLine();
                        trimmedLine = currLine.Trim();
                        if (currLine.StartsWith('!'))
                        {
                            continue;
                        }
                        if (currLine.StartsWith('['))
                        {
                            var match = SectionParser.Match(currLine);
                            string sectionHeader = String.Empty;
                            if (match.Success)
                            {
                                sectionHeader = match.Groups[1].Value;
                            }
                            if (sectionHeader != null)
                                sectionHeader = sectionHeader.ToLower();
                            switch (sectionHeader)
                            {
                                case "instances":
                                    currSection = FileSection.Instances;
                                    break;
                                case "visible":
                                    currSection = FileSection.VisibleRooms;
                                    break;
                                case "settings":
                                    currSection = FileSection.Settings;
                                    break;
                                default:
                                    currSection = FileSection.Unk;
                                    break;
                            }
                            continue;
                        }
                        switch (currSection)
                        {
                            case FileSection.Instances:
                                var match = InstanceHeaderParser.Match(trimmedLine);
                                if (match.Success)
                                {
                                    instanceHeader = match.Groups[1].Value;
                                }
                                if (!trimmedLine.StartsWith(".VertexData="))
                                    continue;
                                if (trimmedLine.StartsWith(".VertexData="))
                                {
                                    string vertexData = trimmedLine.Split("=").Last();
                                    if (vertexData == "")
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        byte[] v = Enumerable.Range(0, vertexData.Length / 2).Select(i => Convert.ToByte(vertexData.Substring(i * 2, 2), 16)).ToArray();
                                        ProcessHeightmap(null, hm, instanceHeader, null, outPath, datPath, true, v, new Vector3(0, 0, 0));
                                    }
                                }
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled Exception in RoomDat.ReadRoomText(): {ex.Message}\n{ex.StackTrace}");
                Debugger.Break();
                throw;
            }
        }

        private void ProcessHeightmap(BinaryReader? br, Heightmap hm, string instanceID, int? length, string outPath, string datPath, bool text, byte[]? textV, Vector3 pos)
        {
            try
            {
                byte[] compressedData;
                byte[] vertexData;

                CompressionTypes t = (CompressionTypes)(ushort)(textV[0] | (textV[1] << 8));
                vertexData = DecompressVertexData(t, textV);
                compressedData = null;

                if (vertexData != null)
                    hm.Read(instanceID, vertexData, outPath, datPath, pos);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled Exception in RoomDat.ProcessHeightmap(): {ex.Message}\n{ex.StackTrace}");
                Debugger.Break();
                throw;
            }

        }
        private void ProcessHeightmap(BinaryReader? br, Heightmap hm, string instanceID, int? length, string outPath, string datPath)
        {
            try
            {
                byte[] compressedData;
                byte[] vertexData;

                CompressionTypes t = (CompressionTypes)br.ReadUInt16();
                br.BaseStream.Position -= 2;
                compressedData = br.ReadBytes(length.Value);

                if (t != CompressionTypes.GZIP && t != CompressionTypes.ZLIB && t != CompressionTypes.ZSTD)
                {
                    throw new Exception($"Compression type error. Unknown compression type: {t}");
                }

                vertexData = DecompressVertexData(t, compressedData);
                compressedData = null;

                if (vertexData != null)
                    hm.Read(instanceID, vertexData, outPath, datPath, new Vector3(0, 0, 0));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled Exception in RoomDat.ProcessHeightmap(): {ex.Message}\n{ex.StackTrace}");
                Debugger.Break();
                throw;
            }
        }

        private byte[] DecompressVertexData(CompressionTypes t, byte[] compressedData)
        {
            try
            {
                switch (t)
                {
                    case CompressionTypes.GZIP:
                        using (var i = new MemoryStream(compressedData))
                        using (var g = new GZipStream(i, CompressionMode.Decompress))
                        using (var o = new MemoryStream())
                        {
                            g.CopyTo(o);
                            return o.ToArray();
                        }
                    case CompressionTypes.ZLIB:
                        using (var i = new MemoryStream(compressedData))
                        using (var zl = new ZLibStream(i, CompressionMode.Decompress))
                        using (var o = new MemoryStream())
                        {
                            zl.CopyTo(o);
                            return o.ToArray();
                        }
                    case CompressionTypes.ZSTD:
                        using (var zs = new Decompressor())
                        {
                            return zs.Unwrap(compressedData).ToArray();
                        }
                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled Exceptions in RoomDat.DecompressVertexData(): {ex.Message}\n{ex.StackTrace}");
                Debugger.Break();
                throw;
            }
        }

        enum Properties
        {
            Bool = 0x00,
            Int32 = 0x01,
            Unk = 0x02,
            UInt32 = 0x03,
            Single = 0x04,
            UInt64 = 0x05,
            Vector3 = 0x06,
            Vector4 = 0x07,
            String = 0x08,
            Data = 0x09
        }

        enum CompressionTypes
        {
            ZLIB = 0x9C78,
            GZIP = 0x8B1F,
            ZSTD = 0xB528,
        }
    }
}