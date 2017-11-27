using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MSBuildBatchEditor
{
    class Program
    {
        const string langVersionTag = "LangVersion";
        const string outputTypeTag = "OutputType";
        static readonly Action<string> logger = Console.WriteLine;

        static void Main(string[] args)
        {
            string rootDirectory;
            string projectFileFormat = "*.csproj";
            string langVersion;
            bool saveChanges;

            ParseCommandLine(args, out rootDirectory, out langVersion, out saveChanges);

            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                Console.WriteLine("Usage: msbuild-editor /rootdir <dir containing project files> [/langversion <version string>] [/save]");
                return;
            }

            var projectFiles = Directory.EnumerateFiles(rootDirectory, projectFileFormat, SearchOption.AllDirectories);

            Console.WriteLine($"Found {projectFiles.Count()} project(s)");
            ProcessProjectFiles(projectFiles, langVersion, saveChanges);
        }

        static void ParseCommandLine(string[] args, out string rootDirectory, out string langVersion, out bool saveChanges)
        {
            rootDirectory = null;
            langVersion = null;
            saveChanges = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (string.IsNullOrWhiteSpace(arg))
                    continue;

                arg = arg.Trim();

                if (string.Compare(arg, "/rootdir", ignoreCase: true) == 0)
                {
                    rootDirectory = args.GetUpperBound(0) > i ? args[i + 1] : string.Empty;
                    i++;
                }
                else if (string.Compare(arg, "/langversion", ignoreCase: true) == 0)
                {
                    langVersion = args.GetUpperBound(0) > i ? args[i + 1] : string.Empty;
                    i++;
                }
                else if (string.Compare(arg, "/save", ignoreCase: true) == 0)
                {
                    saveChanges = true;
                }
            }
        }

        static void ProcessProjectFiles(IEnumerable<string> projectFiles, string langVersion, bool saveChanges)
        {
            long totalChanges = 0;

            Parallel.ForEach(projectFiles, (projectFilePath) =>
            {
                var numOfChanges = 0;
                var projectFilename = Path.GetFileName(projectFilePath);

                if (!string.IsNullOrWhiteSpace(langVersion))
                {
                    var projectFile = XDocument.Load(projectFilePath);
                    var defaultNamespaceAttr = projectFile.Root.Attribute("xmlns");
                    var xmlns = defaultNamespaceAttr?.Value;

                    var targetElements = projectFile.Descendants(XName.Get(langVersionTag, xmlns));

                    if (targetElements.Any())
                    {
                        foreach (var el in targetElements)
                        {
                            el.Value = langVersion;
                            numOfChanges++;
                        }
                    }
                    else
                    {
                        targetElements = projectFile.Descendants(XName.Get(outputTypeTag, xmlns));

                        if (targetElements.Any())
                        {
                            targetElements.First().AddAfterSelf(new XElement(XName.Get(langVersionTag, xmlns), langVersion));
                            numOfChanges++;
                        }
                        else
                        {
                            logger($"Could not set {langVersionTag} for project file {projectFilename}");
                        }
                    }

                    if (numOfChanges > 0)
                        projectFile.Save(projectFilePath, SaveOptions.None);
                }

                if (numOfChanges > 0)
                    logger($"Number of changes for project file {projectFilename}: {numOfChanges}");
                else
                    logger($"No changes for project file {projectFilename}");

                Interlocked.Add(ref totalChanges, numOfChanges);
            });

            logger($"Total changes: {totalChanges}");

            if (saveChanges)
                logger("All changes were saved!");
        }
    }
}
