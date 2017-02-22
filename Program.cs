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
@"llfor (options) [variable] in [pattern] [command] 
[pattern] is a file name, optional file system wildcard, not a regex.
[command] is a shell/cmd command in which any occurrence of ""%%variable"" will 
    be replaced by the current matching file name.
Options: 
    /s  Include subdirectories
    /h  Hide sub-process windows
    /t (number)  Max parallel task count.
                 Defaults to (Environment.ProcessorCount).
    /q  Quiet. Implies /-w
    /w  Wait for a key press when complete.
    TODO: /p  Pipe the list of variable substitutions (varsubs) from stdin.
            The command will be executed for each new line.
        /f (filename)  Read the list of varsubs ^ from a file.
Example:
    llfor /w /t 8 filename in *.png pngout %%filename
    llfor /q /t 8 logfile in ""My Documents\*.log"" MyParse.exe %%logfile
");
        }
        enum InputModes
        {
            DIRSCAN,
            PIPE,
            FILE
        };

        static void Main(string[] args)
        {
            bool configHiddenWindow = false;
            string varname = "";
            string filter = "";
            string subargs = "/C ";
            int argofs = 0;
            bool subdir = false;
            bool quiet = false;
            bool wait = false;

            //----------------------
            InputModes imode = InputModes.DIRSCAN;
            // Pipe and file option bools are just flags for whether they've been encountered on the cmd line,
            //   not whether the option is currently active, because it could be flipped with /-p or /-f.
            //  In that case, these bools are used to determine which state to revert to when using the mode enum.
            //  If one of these modes has been previously specified, and the other one's /- argument is read, 
            //  it will revert to the previously encountered mode, rather than the default directory scan.
            bool pipeOption = false;
            bool fileInOption = false;
            int userCommandArgOffset = 3; // This could vary if no file pattern needs to be specified, as in /p and /f
            //----------------------

            int tCount = Environment.ProcessorCount;
            for (argofs = 0; argofs < args.Length; argofs++)
            {
                if (args[argofs][0] == '/')
                {
                    string curArg = args[argofs].ToLower();
                    if (curArg == "/s") { subdir = true; }
                    else if (curArg == "/-s") { subdir = false; }
                    else if (curArg == "/+s") { subdir = true; }

                    else if (curArg == "/h") { configHiddenWindow = true; }
                    else if (curArg == "/-h") { configHiddenWindow = false; }
                    else if (curArg == "/+h") { configHiddenWindow = true; }

                    else if (curArg == "/q") { quiet = true; }
                    else if (curArg == "/-q") { quiet = false; }
                    else if (curArg == "/+q") { quiet = true; }

                    else if (curArg == "/w") { wait = true; }
                    else if (curArg == "/-w") { wait = false; }
                    else if (curArg == "/+w") { wait = true; }

                    else if (curArg == "/p") { imode = InputModes.PIPE; pipeOption = true; }
                    else if (curArg == "/+p") { imode = InputModes.PIPE; pipeOption = true; }
                    else if (curArg == "/-p")
                    {
                        if (imode == InputModes.PIPE)
                        {
                            if (fileInOption) { imode = InputModes.FILE; }
                            else { imode = InputModes.DIRSCAN; }
                        }
                    }

                    else if (curArg == "/f") { imode = InputModes.FILE; fileInOption = true; }
                    else if (curArg == "/+f") { imode = InputModes.FILE; fileInOption = true; }
                    else if (curArg == "/-f")
                    {
                        if (imode == InputModes.FILE)
                        {
                            if (pipeOption) { imode = InputModes.PIPE; }
                            else { imode = InputModes.DIRSCAN; }
                        }
                    }

                    else if (curArg == "/t")
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
                for (int i = argofs + userCommandArgOffset; i < args.Length; i++)
                {
                    subargs += args[i] + " ";
                }
                // trim final space
                subargs = subargs.Substring(0, subargs.Length - 1);


                System.IO.SearchOption subdirEnum = (subdir ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly);
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
                    if (!quiet)
                    {
                        Console.WriteLine("Exception in directory scan: " + e.Message);
                    }
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
                                if (!quiet)
                                {
                                    System.Console.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + " Start " + filename);
                                }
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
                                    if (!quiet)
                                    {
                                        System.Console.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + " Exit code " + llproc.ExitCode.ToString() + " " + filename);
                                    }
                                }
                                catch (Exception e)
                                {
                                    if (!quiet)
                                    {
                                        System.Console.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString()
                                            + " Error in file " + filename
                                            + ": " + e.Message);
                                    }
                                }
                            });
                    }
                    catch (Exception e)
                    {
                        if (!quiet)
                        {
                            System.Console.WriteLine("Exception in parallel for loop: " + e.Message);
                        }
                    }
                    finally
                    {
                        if (!quiet)
                        {
                            System.Console.WriteLine("Controller process finished. Press any key to exit.");
                            
                            if (wait)
                            {
                                System.Console.ReadKey();
                            }
                        }
                    }
                }
            }
            //else if (args[argofs + 1] == "=" 
            //    && args[argofs + 3].ToLower() == "to"
            //    && args.Length >= argofs + 4)
            //{
            //
            //}
            else
            {
                usage();
            }
        }
    }
}
