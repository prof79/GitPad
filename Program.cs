﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Gitpad
{
    public enum LineEndingType
    {
        Windows, /*CR+LF*/
        Posix, /*LF*/
        MacOS9, /*CR*/
        Unsure,
    }

    public class Program
    {
        public static int Main(string[] args)
        {
            int ret = 0;
            if (args.Length == 0)
            {
                if (MessageBox.Show("Do you want to install Notepad as your default Git editor?", 
                    "Installing GitPad", MessageBoxButtons.YesNo) != DialogResult.Yes)
                {
                    return -1;
                }

                var target = new DirectoryInfo(Environment.ExpandEnvironmentVariables(@"%AppData%\GitPad"));
                if (!target.Exists)
                {
                    target.Create();
                }

                var dest = Environment.ExpandEnvironmentVariables(@"%AppData%\GitPad\GitPad.exe");
                File.Copy(Assembly.GetExecutingAssembly().Location, dest);
                Environment.SetEnvironmentVariable("EDITOR", dest, EnvironmentVariableTarget.User);

                return 0;
            }

            string fileData = null;
            string path = null;
            try
            {
                fileData = File.ReadAllText(args[0], Encoding.UTF8);
                path = Path.GetTempFileName();
                WriteStringToFile(path, fileData, LineEndingType.Windows);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                ret = -1;
                goto bail;
            }

            var psi = new ProcessStartInfo(Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\Notepad.exe"), path)
            {
                WindowStyle = ProcessWindowStyle.Normal,
                UseShellExecute = false,
            };

            var proc = Process.Start(psi);
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                ret = proc.ExitCode;
                goto bail;
            }

            try
            {
                fileData = File.ReadAllText(path, Encoding.UTF8);
                WriteStringToFile(args[0], fileData, LineEndingType.Posix);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                ret = -1;
                goto bail;
            }

        bail:
            File.Delete(path);
            return ret;
        }

        static void WriteStringToFile(string path, string fileData, LineEndingType lineType)
        {
            using(var of = File.OpenWrite(path))
            {
                var buf = Encoding.UTF8.GetBytes(ForceLineEndings(fileData, lineType));
                of.Write(buf, 0, buf.Length);
            }
        }

        public static string ForceLineEndings(string fileData, LineEndingType type)
        {
            var ret = new StringBuilder(fileData.Length);

            string ending;
            switch(type)
            {
                case LineEndingType.Windows:
                    ending = "\r\n";
                    break;
                case LineEndingType.Posix:
                    ending = "\n";
                    break;
                case LineEndingType.MacOS9:
                    ending = "\r";
                    break;
                default:
                    throw new Exception("Specify an explicit line ending type");
            }

            foreach (var line in fileData.Split('\n'))
            {
                var fixedLine = line.Replace("\r", "");
                ret.Append(fixedLine);
                ret.Append(ending);
            }

            return ret.ToString();
        }
    }
}