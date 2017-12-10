using System;

namespace neo_scanner
{
    class Program
    {
        static Scanner scanner;
        static void Main(string[] args)
        {
            var config = System.IO.File.ReadAllText("config.json");
            var json = MyJson.Parse(config) as MyJson.JsonNode_Object;
            scanner = new Scanner(json);
            scanner.Begin();
            Console.WriteLine("BeginScan!");
            while (true)
            {
                Console.Write("cmd>");
                string cmd = Console.ReadLine();
                cmd = cmd.Replace(" ", "");
                if (cmd == "") continue;
                switch (cmd)
                {
                    case "exit":
                        scanner.Exit();
                        return;
                    case "help":
                    case "?":
                        ShowCmdHelp();
                        break;
                    case "state":
                        ShowState();
                        break;
                    case "save":
                        scanner.Save();
                        break;
                    default:
                        Console.WriteLine("unknown cmd,type help to get more.");
                        break;
                }

            }
        }
        static void ShowState()
        {
            Console.WriteLine("sync height=" + scanner.processedBlock + "  remote height=" + scanner.remoteBlockHeight);
        }
        static void ShowCmdHelp()
        {
            Console.WriteLine("neo_scanner 0.01");
            Console.WriteLine("help -> print helpinfo");
            Console.WriteLine("exit -> exit program");
            Console.WriteLine("state -> show state");
            Console.WriteLine("save -> save a state now.");

        }

    }
}
