
/*

Example:
llfor f in *.png pngout %%f

Options:
    /s include sub-directories
    /h hide sub-process windows

*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace llfor
{
    class Program
    {

        static void usage( )
        {
            Console.Write(
@"Usage: llfor (options) [variable] in [pattern] [command] 
Options: 
    /s  Include subdirectories
    /h  Hide sub-process windows
    /t (number)  max parallel task count

[pattern] is a file name, wildcards optional. file system wildcard, not a regex.
[command] is a shell command in which any occurrence of ""%%variable"" will be
    replaced by the current matching file name.
");
        }
        static void Main(string[] args)
        {
            bool configHiddenWindow = true;
            string varname = "";
            string filter = "";
            string subargs = "/C ";
            int argofs = 0;
            bool subdir = false;
            int tCount = Environment.ProcessorCount;
            for (argofs = 0; argofs < args.Length; argofs++)
            {
                if (args[argofs][0] == '/')
                {
                    if (args[argofs].ToLower() == "/s") { subdir = true; }
                    else if (args[argofs].ToLower() == "/h") { configHiddenWindow = false; }
                    else if (args[argofs].ToLower() == "/t")
                    {
                        argofs++;
                        if (argofs >= args.Length) { break; }
                        int tmp = int.Parse(args[argofs]);
                        if (tmp > 0) { tCount = tmp; }
                    }
                }
                else { break; }
            }
            if (args[argofs + 1].ToLower() == "in" && args.Length >= argofs + 4)
            {
                varname = args[argofs + 0];
                filter = args[argofs + 2];
                for (int i = argofs + 3; i < args.Length; i++)
                {
                    subargs += args[i] + " ";
                }
                // trim final space
                subargs = subargs.Substring(0, subargs.Length - 1);
                System.IO.SearchOption subdirEnum = System.IO.SearchOption.TopDirectoryOnly;
                if (subdir)
                {
                    subdirEnum = System.IO.SearchOption.AllDirectories;
                }

                string scanDir = ".";
                string postFilter = filter;
                string pp = Path.GetDirectoryName(filter);
                if (pp.Length > 0)
                {
                    if (filter.Substring(0, pp.Length) == pp)
                    {
                        scanDir = pp;
                        postFilter = filter.Substring(pp.Length, filter.Length - pp.Length);
                        if (postFilter[0] == '\\' || postFilter[0] == '/')
                        {
                            postFilter = postFilter.Substring(1, postFilter.Length - 1);
                        }
                    }
                }
                IEnumerable<string> matches = new List<string>(); // gotta initialize with something
                bool bContinue = true;
                try
                {
                    matches = System.IO.Directory.EnumerateFiles(scanDir, postFilter, subdirEnum);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception in directory scan: " + e.Message);
                    bContinue = false;
                }
                if (bContinue)
                {
                    try
                    {
                        Parallel.ForEach<string>(matches,
                            new ParallelOptions { MaxDegreeOfParallelism = tCount },
                            (string filename) =>
                            {
                                System.Console.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + " Start " + filename);
                                try
                                {
                                    System.Diagnostics.Process llproc = new System.Diagnostics.Process();
                                    System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                                    if (configHiddenWindow) { startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden; }
                                    else { startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal; }
                                    startInfo.FileName = "cmd.exe";
                                    string repargs = subargs.Replace("%%" + varname, filename);
                                    startInfo.Arguments = repargs; // subargs;
                                    llproc.StartInfo = startInfo;
                                    llproc.Start();
                                    llproc.WaitForExit();
                                    System.Console.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + " Exit code " + llproc.ExitCode.ToString() + " " + filename);
                                }
                                catch (Exception e)
                                {
                                    System.Console.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString()
                                        + " Error in file " + filename
                                        + ": " + e.Message);
                                }
                            });
                    }
                    catch (Exception e)
                    {
                        System.Console.WriteLine("Exception in parallel for loop: " + e.Message);
                    }
                    finally
                    {
                        System.Console.WriteLine("Controller process finished. Press any key to exit.");
                        System.Console.ReadKey();
                    }
                }
            }
            else
            {
                usage();
            }
        }
    }
}
