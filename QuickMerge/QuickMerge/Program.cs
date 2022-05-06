using PeNet;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading.Tasks;

namespace QuickMerge
{
    internal class Program
    {
        #region Util
        static byte[] ResourceToBytes(string resource)
        {
            try
            {
                using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        input.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return new byte[0];
        }

        static int ExecuteAssemblyFromMemory(byte[] shellcode, params string[] args)
        {
            Assembly dotnetAssembly = Assembly.Load(shellcode);
            MethodInfo m = dotnetAssembly.EntryPoint;
            var parameters = m.GetParameters().Length == 0 ? null : new[] { args };
            return (int)m.Invoke(null, parameters);
        }
        #endregion



        #region Printing
        static void _Write(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        static void WriteError(string message) => _Write(message, ConsoleColor.Red);

        static void WriteInfo(string message) => _Write(message, ConsoleColor.Yellow);

        static void WriteSuccess(string message) => _Write(message, ConsoleColor.Green);
        #endregion



        #region Core
        static List<string> GetReferencedAssemblies(string directory, Assembly asm)
        {
            List<string> references = new List<string>();

            // there is no reliable way to get the paths of the assemblies,
            // so we just expect them to be in the same directory 
            foreach (var dependency in asm.GetReferencedAssemblies())
            {
                if (dependency.FullName == asm.FullName)
                    continue;

                string name = dependency.Name;

                if (File.Exists(Path.Combine(directory, name + ".dll")) == true)
                    name += ".dll";
                else
                if (File.Exists(Path.Combine(directory, name + ".exe")) == true)
                    name += ".exe";
                else
                    continue;

                // maybe remove this later?
                // im unsure if ilmerge can merge exes, i think its only for dll's
                if (name.EndsWith(".exe"))
                    continue;

                references.Add(Path.Combine(directory, name));
            }

            foreach(var reference in references)
            {
                var temp = Assembly.LoadFrom(reference);
                foreach (var _ref in GetReferencedAssemblies(directory, temp))
                {
                    if (references.Contains(_ref) == true)
                        continue;

                    references.Add(_ref);
                }
            }

            return references;
        }

        static void Merge(string inputFile)
        {
            WriteSuccess("Input file found.");
            WriteInfo("Checking dependencies...");

            var inputDirectory = Path.GetDirectoryName(inputFile);
            var output = Path.Combine(inputDirectory, Path.GetFileNameWithoutExtension(inputFile) + "_merged.exe");
            var pe = new PeFile(inputFile);
            var asm = Assembly.LoadFrom(inputFile);
            
            if(pe.IsDotNet == false)
            {
                WriteError("File is not a .NET Assembly!");
                return;
            }

            List<string> references = GetReferencedAssemblies(inputDirectory, asm);

            WriteInfo("Referenced Assemblies:");
            foreach (var assembly in references)
            {
                WriteInfo($"> '{assembly}'");
            }

            WriteInfo("Extracting ILMerge...");
            var shellcode = ResourceToBytes("QuickMerge.References.ILMerge.exe");

            WriteInfo("Merging...");

            #region Merge / Memory Invokation
            List<string> args = new List<string>();

            args.Add($"{inputFile}");

            foreach(string reference in references)
                args.Add($"{reference}");
           
            args.Add($"/out:{output}");

#if DEBUG
            foreach(var arg in args)
            {
                Console.WriteLine(arg);
            }
#endif 

            if (File.Exists(output) == true)
                File.Delete(output);

            ExecuteAssemblyFromMemory(shellcode, args.ToArray());
            #endregion

            WriteSuccess($"Result is located at: '{output}'");
        }
        #endregion

        static void Main(string[] args)
        {
#if DEBUG
            args = new string[]
            {
                @"D:\Repositories\QuickMerge\Test Subject\EdenPaste.exe"
            };
#else
            if (args.Length == 0)
            {
                WriteError("Missing input-file argument.");
                return;
            }
#endif 

            string input = args[0];

            WriteInfo($"Input File is: '{input}'");

            if(File.Exists(input) == false)
            {
                WriteError($"Couldn't find file :'{input}'");
            }
            else
            {
                Merge(input);
            }

#if DEBUG 
            Console.Read();
#endif 
        }
    }
}
