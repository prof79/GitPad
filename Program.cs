//----------------------------------------------------------------------------
// <copyright file="Program.cs"
//      company="GitPad Team">
//      Copyright (C) 2014 GitPad Team. All rights reserved.
// </copyright>
// <author>GitPad Team</author>
// <description>This is the main code for GitPad.</description>
// <version>v1.4.0 2014-09-16</version>
//
// Based on: https://github.com/GitHub/GitPad
//
//----------------------------------------------------------------------------

namespace Gitpad
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Windows.Forms;

    public class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                if (IsProcessElevated())
                {
                    MessageBox.Show("Run this application as a normal user (not as Elevated Administrator)",
                        "App is Elevated", MessageBoxButtons.OK, MessageBoxIcon.Error );
                    return -1;
                }

                if (MessageBox.Show(
                        "Do you want to use your default text editor as your commit editor?", 
                        "Installing GitPad", MessageBoxButtons.YesNo) 
                    != DialogResult.Yes)
                {
                    return -1;
                }

                var target = new DirectoryInfo(Environment.ExpandEnvironmentVariables(@"%AppData%\GitPad"));
                if (!target.Exists)
                {
                    target.Create();
                }

                var dest = new FileInfo(Environment.ExpandEnvironmentVariables(@"%AppData%\GitPad\GitPad.exe"));
                File.Copy(Assembly.GetExecutingAssembly().Location, dest.FullName, true);

                Environment.SetEnvironmentVariable("EDITOR", dest.FullName.Replace('\\', '/'), EnvironmentVariableTarget.User);
                return 0;
            }

            int ret = 0;
            string fileData = null;
            string path = null;
            try
            {
                fileData = File.ReadAllText(args[0], Encoding.UTF8);
                path = Path.GetRandomFileName() + ".txt";
                WriteStringToFile(path, fileData, LineEndingType.Windows, true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                ret = -1;
                goto bail;
            }

            var psi = new ProcessStartInfo(path)
            {
                WindowStyle = ProcessWindowStyle.Normal,
                UseShellExecute = true,
            };

            Process proc;

            try
            {
                proc = Process.Start(psi);
            }
            catch
            {
                Console.Error.WriteLine("Could not launch the default text editor, falling back to notepad.");

                psi.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
                psi.Arguments = path;

                proc = Process.Start(psi);
            }

            // See http://stackoverflow.com/questions/3456383/process-start-returns-null
            // In case of editor reuse (think VS) we can't block on the process so we only have two options. Either try 
            // to be clever and monitor the file for changes but it's quite possible that users save their file before 
            // being done with them so we'll go with the semi-sucky method of showing a message on the console
            if (proc == null)
            {
                Console.CancelKeyPress += (s, e) => File.Delete(path);

                Console.WriteLine("Press enter when you're done editing your commit message, or CTRL+C to abort");
                Console.ReadLine();
            }
            else
            {
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    ret = proc.ExitCode;
                    goto bail;
                }
            }

            try
            {
                fileData = File.ReadAllText(path, Encoding.UTF8);
                WriteStringToFile(args[0], fileData, LineEndingType.Posix, false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                ret = -1;
                goto bail;
            }

        bail:
            if (File.Exists(path))
                File.Delete(path);
            return ret;
        }

        static void WriteStringToFile(string path, string fileData, LineEndingType lineType, bool emitUTF8Preamble)
        {
            using(var of = File.Open(path, FileMode.Create))
            {
                var buf = Encoding.UTF8.GetBytes(ForceLineEndings(fileData, lineType));
                if (emitUTF8Preamble)
                    of.Write(Encoding.UTF8.GetPreamble(), 0, Encoding.UTF8.GetPreamble().Length);
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

            // Don't add new lines to the end of the file.
            string str = ret.ToString();
            return str.Substring(0, str.Length - ending.Length);
        }

        public static unsafe bool IsProcessElevated()
        {
            if (Environment.OSVersion.Version < new Version(6,0,0,0)) 
            {
                // Elevation is not a thing.
                return false;
            }

            IntPtr tokenHandle;

            if (!NativeApi.OpenProcessToken(NativeApi.GetCurrentProcess(), NativeApi.TOKEN_QUERY, out tokenHandle))
            {
                throw new Exception("OpenProcessToken failed", new Win32Exception());
            }

            try
            {
                TOKEN_ELEVATION_TYPE elevationType;
                uint dontcare;
                if (!NativeApi.GetTokenInformation(tokenHandle, TOKEN_INFORMATION_CLASS.TokenElevationType, out elevationType, (uint)sizeof(TOKEN_ELEVATION_TYPE), out dontcare))
                {
                    throw new Exception("GetTokenInformation failed", new Win32Exception());
                }

                return (elevationType == TOKEN_ELEVATION_TYPE.TokenElevationTypeFull);
            }
            finally
            {
                NativeApi.CloseHandle(tokenHandle);
            }
        }
    }
}
