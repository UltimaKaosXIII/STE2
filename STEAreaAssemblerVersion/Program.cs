using System.Diagnostics;

namespace STEAreaAssemblerVersion
{
    class Program
    {
        public static List<string> areaList;
        public static Dictionary<int, string> areaTextures = new();
        public static void Main()
        {
            Console.Title = "Area Assembler Terrain Extractor";
            Console.WriteLine("--------------------------------------------------------\n");
            Console.WriteLine("Star Wars: The Old Republic Terrain Extractor\n");
            Console.WriteLine("--------------------------------------------------------\n");
            InteractiveMain();
        }
        public static void InteractiveMain()
        {
            string resourcesPath = "";
            Console.WriteLine("Enter path to your \\resources\\world folder");
            resourcesPath = Console.ReadLine();
            switch (Directory.Exists($"{Path.GetFullPath($"{resourcesPath}\\areas")}"))
            {
                case true:
                    List<string> datsLiveContent = new List<string>();
                    List<string> datsAreas = new();
                    areaList = new();
                    foreach (var file in Directory.EnumerateFiles($"{resourcesPath}\\livecontent\\systemgenerated", "*.dat", SearchOption.AllDirectories))
                    {
                        datsLiveContent.Add(file);
                    }
                    foreach (var file in Directory.EnumerateFiles($"{resourcesPath}\\areas", "*.dat", SearchOption.AllDirectories))
                    {
                        datsAreas.Add(file);
                    }
                    areaList.AddRange(datsLiveContent);
                    areaList.AddRange(datsAreas);
                    datsLiveContent.Clear();
                    datsAreas.Clear();
                    datsLiveContent = null;
                    datsAreas = null;

                    Assembler(resourcesPath, areaList);
                    break;
                case false:
                    Console.WriteLine($"{resourcesPath} is not valid, please try again");
                    InteractiveMain();
                    break;
            }
        }
        private static void Assembler(string resourcesPath, List<string> areaList)
        {
            string outPath = Path.Combine(resourcesPath, "heightmaps");
            RoomDat r;
            Stopwatch sw;
            Console.WriteLine($"All heightmaps will be dumped to {outPath}");
            Console.WriteLine($"This could take a lot of space and a LONG time.\nAre you sure you wish to continue? Y/N");
            ConsoleKeyInfo ynKey = Console.ReadKey();
            switch (ynKey.Key)
            {
                case ConsoleKey.Enter:
                case ConsoleKey.Y:
                    Console.WriteLine("\nDumping terrain...");
                    r = new();
                    sw = new();
                    sw.Start();
                    ParallelOptions options = new ParallelOptions()
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount
                    };
                    try
                    {

                        areaList.RemoveAll(a => Path.GetFileNameWithoutExtension(a) == "area");
                        Parallel.ForEach(areaList, options, async (filepath, _) =>
                        {
                            if (Path.GetFileName(filepath) != "area.dat")
                            {
                                r.ReadFile(Path.GetFullPath(filepath), outPath);
                            }
                        });
                        sw.Stop();
                        Console.WriteLine($"All heightmaps dumped! Completed in: {sw.Elapsed}\nPress Enter to exit.");
                        Console.ReadLine();
                        sw.Reset();
                        sw = null;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Unhandled Exception in STE.Assembler(): {ex.Message}\n{ex.StackTrace}");
                        Debugger.Break();
                    }
                    break;
                case ConsoleKey.N:
                case ConsoleKey.Escape:
                    Console.WriteLine($"STE Closing...");
                    Thread.Sleep(3000);
                    Environment.Exit(0);
                    break;
                default:
                    Console.WriteLine($"{ynKey.Key} is not a valid input.\n");
                    Assembler(resourcesPath, areaList);
                    break;
            }
        }
    }
}

