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
@"
    llfor   -   parallel for loop

To run a command for a set of files:
        llfor (options) [variable] in [pattern] [command] 
To run a command for an arbitrary list of inputs...
    from stdin:
        llfor [options] /p [variable] [command]
    from a file:
        llfor [options] /f [filename] [variable] [command]

[pattern] is a file name or file system wildcard, not a regex.
    May be enclosed in double quotes if spaces are needed.
[command] is a shell/cmd command in which any occurrence of ""%%variable"" will 
    be replaced by the current matching file name.

Options: 
    /s  Include subdirectories
    /h  Hide sub-process windows
    /t (number)  Max parallel task count.
                 Defaults to (Environment.ProcessorCount).
    /q  Quiet. Implies /-w
    /w  Wait for a key press when complete.
    /p  Pipe the list of variable substitutions (varsubs) from stdin.
        The command will be executed for each new line.
    /f (filename)  Read the list of varsubs from a file.

Most options may be reset on or off by using /+ and /-
    followed by the letter of the option in question.

Examples:
    llfor file in *.png pngout %%file
    llfor /w /t 8 logfile in ""My Documents\*.log"" MyParse.exe %%logfile
    llfor /q /p line echo %%line
    llfor /f batchList.txt /q username copy NOTICE.txt \Users\%%username\Desktop

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
            string fileInOptionFilename = "";
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

                    else if (curArg == "/f" || curArg == "/+f")
                    {
                        imode = InputModes.FILE;
                        fileInOption = true;
                        argofs++;
                        if (argofs >= args.Length) { usage(); return; }
                        fileInOptionFilename = args[argofs];
                    }
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
                    else
                    {
                        if (!quiet) { usage(); }
                        return;
                    }
                }
                else { break; }
            }

            if (imode== InputModes.DIRSCAN) { userCommandArgOffset = 3; }
            else if (imode == InputModes.FILE) { userCommandArgOffset = 1; }
            else if (imode == InputModes.PIPE) { userCommandArgOffset = 1; }

            if (argofs + userCommandArgOffset >= args.Length) { usage(); return; }
            varname = args[argofs + 0];
            for (int i = argofs + userCommandArgOffset; i < args.Length; i++)
            {
                subargs += args[i] + " ";
            }
            // trim final space
            subargs = subargs.Substring(0, subargs.Length - 1);

            IEnumerable<string> matches = new List<string>(); // gotta initialize with something

            if ((imode == InputModes.DIRSCAN) && (args[argofs + 1].ToLower() == "in" && args.Length >= argofs + 4))
            {
                filter = args[argofs + 2];
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
                    iterate(configHiddenWindow, varname, subargs, quiet, wait, tCount, matches);
                }
            }
            //else if (args[argofs + 1] == "=" 
            //    && args[argofs + 3].ToLower() == "to"
            //    && args.Length >= argofs + 4)
            //{
            //
            //}
            else if (imode == InputModes.FILE)
            {
                try
                {
                    matches = new llfor.fileLineEnumerable(fileInOptionFilename);
                }
                catch (Exception e)
                {
                    if (!quiet)
                    {
                        Console.WriteLine("Error trying to open input file: " + e.Message);
                    }
                    return;
                }
                iterate(configHiddenWindow, varname, subargs, quiet, wait, tCount, matches);
            }
            else if (imode == InputModes.PIPE)
            {
                try
                {
                    matches = new llfor.stdinEnumerable();
                }
                catch (Exception e)
                {
                    if (!quiet)
                    {
                        Console.WriteLine("Error trying to open standard input/pipe: " + e.Message);
                    }
                    return;
                }
                iterate(configHiddenWindow, varname, subargs, quiet, wait, tCount, matches);
            }
            else
            {
                if (!quiet) { usage(); return; }
            }
        }

        private static void iterate(bool configHiddenWindow, string varname, string subargs, bool quiet, bool wait, int tCount, IEnumerable<string> matches)
        {
            try
            {
                Parallel.ForEach<string>(matches,
                    new ParallelOptions { MaxDegreeOfParallelism = tCount },
                    (string filename) =>
                    {
                        llCallback(filename, configHiddenWindow, varname, subargs, quiet);
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
                    System.Console.WriteLine("Controller process finished.");
                    if (wait)
                    {
                        System.Console.WriteLine("Press any key to exit.");
                        System.Console.ReadKey();
                    }
                }
            }
        }

        private static void llCallback(string filename, bool configHiddenWindow, string varname, string subargs, bool quiet)
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
        }
    }
}
