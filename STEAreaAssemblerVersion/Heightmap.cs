using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;

namespace STEAreaAssemblerVersion
{
    public class Heightmap
    {
        public byte unkByte;
        public uint width;
        public uint depth;
        public bool containsHoles;
        public byte[] holeMap;
        public int NumTexture;
        public byte[] TextureIDs;
        public ushort[] indicesArray;
        public int numFaces;
        public Vector3[] verts;
        public Vector2[] uvs;
        private Dictionary<int, string> areaTextures;
        public void Read(string instanceID, byte[] vertexData, string outPath, string datPath, Vector3? Positions)
        {
            try
            {
                using (var ms = new MemoryStream(vertexData))
                using (var br = new BinaryReader(ms))
                {
                    unkByte = br.ReadByte();
                    byte flag = unkByte;
                    width = br.ReadUInt32();
                    depth = br.ReadUInt32();
                    if ((unkByte & 8) != 0)
                    {
                        br.BaseStream.Position += 12;
                    }
                    containsHoles = br.ReadBoolean();
                    if (containsHoles)
                    {
                        uint arraySize = (width * depth + 7) >>> 3;
                        holeMap = new byte[arraySize];
                        br.Read(holeMap, 0, (int)arraySize);
                    }
                    
                    double minX = -0.2 * 0.5 * (width - 1) - (width % 2 == 0 ? 0.1 : 0);
                    double maxX = 0.2 * 0.5 * (width - 1) - (width % 2 == 0 ? 0.1 : 0);
                    double minY = -0.2 * 0.5 * (depth - 1) - (depth % 2 == 0 ? 0.1 : 0);
                    double maxY = 0.2 * 0.5 * (depth - 1) - (depth % 2 == 0 ? 0.1 : 0);

                    // Initialize verts array
                    verts = new Vector3[width * depth];
                    for (int i = 0; i < verts.Length; i++)
                    {
                        verts[i] = new Vector3(0.0, 0.0, 0.0);  // Set all to (0,0,0)
                    }

                    // Populate the X and Z values in the vertices
                    int index = 0;
                    for (int y = 0; y < depth; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            double xVal = 0.2 * (-0.5 * (width - 1) + x) - (width % 2 == 0 ? 0.1 : 0); // X
                            double zVal = 0.2 * (-0.5 * (depth - 1) + y) - (depth % 2 == 0 ? 0.1 : 0); // Z

                            // Set values for verts
                            verts[index] = new Vector3((float)xVal, verts[index].Y, (float)zVal);

                            index++;
                        }
                    }

                    // Read and update Y values (height) from the binary reader for border edges
                    for (uint x = 0; x < width; x++)
                    {
                        double a = br.ReadSingle();
                        double b = br.ReadSingle();
                        uint idx1 = IdxFromPos(x, 0);
                        uint idx2 = IdxFromPos(x, depth - 1);

                        // Set Y values
                        verts[idx1].Y = a;
                        verts[idx2].Y = b;
                    }

                    // Read and update Y values for the other border edges
                    for (uint y = 1; y < depth - 1; y++)
                    {
                        double a = br.ReadSingle();
                        double b = br.ReadSingle();
                        uint idx1 = IdxFromPos(0, y);
                        uint idx2 = IdxFromPos(width - 1, y);

                        // Set Y values
                        verts[idx1].Y = a;
                        verts[idx2].Y = b;
                    }

                    // Read and update Y values for the inner grid points
                    for (uint x = 1; x < width - 1; x++)
                    {
                        for (uint y = 1; y < depth - 1; y++)
                        {
                            double v = br.ReadUInt16() * 0.0019531548;  // Normalize value. Comes from disassembly of game client.
                            uint idx = IdxFromPos(x, y);

                            // Set Y values
                            verts[idx].Y = v;
                        }
                    }

                    // probably incorrect.
                    double scaleX = maxX - minX;
                    double scaleZ = maxY - minY;
                    uvs = new Vector2[verts.Length];
                    for (int i = 0; i < verts.Length; i++)
                    {
                        double u = ((verts[i].X - minX) / scaleX);
                        double v = ((verts[i].Z - minY) / scaleZ);
                        u = Math.Clamp(u, 0.0, 1.0);
                        v = Math.Clamp(v, 0.0, 1.0);
                        uvs[i] = new Vector2(u, v);
                    }

                    indicesArray = new ushort[6 * (width - 1) * (depth - 1)];
                    // C-3PO supplied code. Converted from javascript.
                    numFaces = 0;
                    for (int y = 0; y < depth - 1; y++)
                    {
                        for (int x = 0; x < width - 1; x++)
                        {
                            if (!containsHoles || (CheckNoHole(holeMap, depth, y, x + 1) && CheckNoHole(holeMap, depth, y + 1, x))) //depth && depth
                            {
                                if (!containsHoles || CheckNoHole(holeMap, depth, y, x)) //depth
                                {
                                    indicesArray[numFaces] = (ushort)(y * width + x); //width
                                    indicesArray[numFaces + 1] = (ushort)((y + 1) * width + x); //width
                                    indicesArray[numFaces + 2] = (ushort)(y * width + x + 1); //width
                                    numFaces += 3;
                                }
                                if (!containsHoles || CheckNoHole(holeMap, depth, y + 1, x + 1)) //depth
                                {
                                    indicesArray[numFaces] = (ushort)(y * width + x + 1); //width
                                    indicesArray[numFaces + 1] = (ushort)((y + 1) * width + x); //width
                                    indicesArray[numFaces + 2] = (ushort)((y + 1) * width + x + 1); //width
                                    numFaces += 3;
                                }
                            }
                        }
                    }
                }
                Writer(instanceID, outPath, verts, uvs);
                Console.WriteLine($"{instanceID}.obj done");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled Exceptions in Heightmap.Read(): {ex.Message}\n{ex.StackTrace}");
                Debugger.Break();
                throw;
            }
        }

        private void Writer(string instanceID, string outPath, Vector3[] verts, Vector2[] uvs)
        {
            try
            {
                string outFile = Path.Combine(outPath, instanceID + ".obj");
                Directory.CreateDirectory(outPath);
                using (var sw = new StreamWriter(outFile, false))
                {

                    switch (numFaces)
                    {
                        case 0:
                            for (int i = 0; i < verts.Length; i++)
                            {
                                sw.WriteLine($"v {verts[i].X.ToString(CultureInfo.InvariantCulture)} {verts[i].Y.ToString(CultureInfo.InvariantCulture)} {verts[i].Z.ToString(CultureInfo.InvariantCulture)}");
                            }
                            sw.WriteLine($"\r\ns 1\r\n");
                            for (int i = 0; i < uvs.Length; i++)
                            {
                                sw.WriteLine($"vt {uvs[i].U.ToString(CultureInfo.InvariantCulture)} {uvs[i].V.ToString(CultureInfo.InvariantCulture)}");
                            }
                            sw.WriteLine($"");
                            for (int y = 0; y < depth - 1; ++y)
                            {
                                for (int x = 0; x < width - 1; ++x)
                                {
                                    int v1 = (int)(y * width + x + 1);
                                    int v2 = v1 + 1;
                                    int v3 = (int)(v1 + width);
                                    int v4 = v3 + 1;
                                    sw.WriteLine($"f {v1} {v3} {v4}");
                                    sw.WriteLine($"f {v1} {v4} {v2}");
                                }
                            }
                            break;
                        default:
                            for (int i = 0; i < verts.Length; i++)
                            {
                                sw.WriteLine($"v {verts[i].X.ToString(CultureInfo.InvariantCulture)} {verts[i].Y.ToString(CultureInfo.InvariantCulture)} {verts[i].Z.ToString(CultureInfo.InvariantCulture)}");
                            }
                            sw.WriteLine($"\r\ns 1\r\n");
                            for (int i = 0; i < uvs.Length; i++)
                            {
                                sw.WriteLine($"vt {uvs[i].U.ToString(CultureInfo.InvariantCulture)} {uvs[i].V.ToString(CultureInfo.InvariantCulture)}");
                            }
                            sw.WriteLine($"");
                            for (int i = 0; i < numFaces; i += 3)
                            {
                                int v1 = indicesArray[i] + 1;
                                int v2 = indicesArray[i + 1] + 1;
                                int v3 = indicesArray[i + 2] + 1;
                                sw.WriteLine($"f {v1.ToString(CultureInfo.InvariantCulture)}/{v1.ToString(CultureInfo.InvariantCulture)} {v2.ToString(CultureInfo.InvariantCulture)}/{v2.ToString(CultureInfo.InvariantCulture)} {v3.ToString(CultureInfo.InvariantCulture)}/{v3.ToString(CultureInfo.InvariantCulture)}");
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled Exception in Heightmap.Writer(): {ex.Message}\n{ex.StackTrace}");
                Debugger.Break();
                throw;
            }
        }

        // get index for vertex from position
        private uint IdxFromPos(uint x, uint y)
        {
            return x + y * width;
        }

        // check for holes if the hasHoles bool is set
        private bool CheckNoHole(byte[] map, uint depth, int k, int j)
        {
            int index = j * (int)depth + k;
            int byteIdx = index >>> 3;
            int bit = 7 - (index & 0x7);
            return (map[byteIdx] & (1 << bit)) != 0;
        }
    }

    public class Vector3
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class Vector2
    {
        public double U { get; set; }
        public double V { get; set; }
        public Vector2(double u, double v)
        {
            U = u;
            V = v;
        }
    }
}
