using System.Diagnostics;
using System.Xml;

namespace CC.SolutionsAnalyzer;

public class SolutionFileParser : ISolutionParser
{
    public VisualStudioSolution GetSolution(string filePath)
    {
        var projects = ParseProjects(filePath);
        return new VisualStudioSolution(new FileInfo(filePath).Name.Replace(".sln", ""), filePath, projects);
    }

    public IEnumerable<string> GetProjectReferences(string projectFilePath)
    {
        var doc = new XmlDocument();
        doc.Load(projectFilePath);

        var nsm = new XmlNamespaceManager(doc.NameTable);
        nsm.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003");

        return GetProjectReferences(doc, nsm) ?? new List<string>();
    }
        
    private static IEnumerable<string> GetProjectReferences(XmlDocument doc, XmlNamespaceManager nsm)
    {
        var projectReferences = doc.SelectNodes("//ms:ProjectReference/@Include", nsm)?.Cast<XmlAttribute>()
            .Select(x => x?.Value ?? string.Empty) ?? new List<string>();

        if (!projectReferences.Any())
        {
            projectReferences = doc.SelectNodes("//ProjectReference/@Include")?.Cast<XmlAttribute>()
                .Select(x => x?.Value ?? string.Empty) ?? new List<string>();
        }

        return projectReferences;
    }

    private static (IEnumerable<Package> Packages, bool UsesProjectPackageReferences) GetPackages(XmlDocument doc, XmlNamespaceManager nsm, string projectFilePath)
    {
        var usesProjectPackageReferences = false;
        var packages = new List<Package>();
        var packageReferenceNodes = doc.SelectNodes("//PackageReference");
        if (packageReferenceNodes is { Count: > 0 })
        {
            usesProjectPackageReferences = true;
            foreach (XmlNode packageReferenceNode in packageReferenceNodes)
            {
                if (packageReferenceNode.Attributes != null)
                {
                    var packageName = packageReferenceNode.Attributes["Include"]?.Value ?? string.Empty;
                    var packageVersion = packageReferenceNode.Attributes["Version"]?.Value ?? string.Empty;
                    if (packageVersion == string.Empty)
                    {
                        packageVersion = packageReferenceNode.SelectSingleNode("./Version")?.Value;
                    }
                    packages.Add(new(packageName, packageVersion));
                }
            }
        }
        else
        {
            var fileInfo = new FileInfo(projectFilePath);
            var packagesConfigPath = Path.Combine(fileInfo.DirectoryName, "packages.config");
            if (File.Exists(packagesConfigPath))
            {
                var packagesConfigDoc = new XmlDocument();
                packagesConfigDoc.Load(packagesConfigPath);
                var packageNodes = packagesConfigDoc.SelectNodes("//package");
                if (packageNodes is { Count: > 0 })
                {
                    foreach (XmlNode packageNode in packageNodes)
                    {
                        var packageName = packageNode.Attributes?["id"]?.Value ?? string.Empty;
                        var packageVersion = packageNode.Attributes?["version"]?.Value ?? string.Empty;
                        if (packageName != string.Empty && packageVersion != string.Empty)
                        {
                            packages.Add(new(packageName, packageVersion));
                        }
                    }
                }
            }
        }

        return (packages, usesProjectPackageReferences);
    }
        
    public (IEnumerable<Package> Packages, bool UsesProjectPackageReferences) GetPackages(string projectFilePath)
    {
        var doc = new XmlDocument();
        doc.Load(projectFilePath);
        var nsm = new XmlNamespaceManager(doc.NameTable);
        nsm.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003");

        return GetPackages(doc, nsm, projectFilePath);
    }


    public IEnumerable<string> GetReferences(string projectFilePath)
    {
        var doc = new XmlDocument();
        doc.Load(projectFilePath);

        var nsm = new XmlNamespaceManager(doc.NameTable);
        nsm.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003");

        return GetReferences(doc, nsm);
    }

    private IEnumerable<string> GetReferences(XmlDocument doc, XmlNamespaceManager nsm)
    {
        string ParseReferenceName(XmlNode node)
        {
            var include = node.Attributes?["Include"]?.Value ?? string.Empty;
            var parts = include.Split(',');
            var name = parts[0];
            return name;
        }

        return doc.SelectNodes("//ms:Reference", nsm)?.Cast<XmlNode>().Select(ParseReferenceName) ?? new List<string>();
    }


    public VisualStudioProject GetProject(string projectFilePath, bool loadReferencedProjects = true)
    {
        var doc = new XmlDocument();
        doc.Load(projectFilePath);

        var nsm = new XmlNamespaceManager(doc.NameTable);
        nsm.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003");

        var projectReferences = GetProjectReferences(doc, nsm);
        var referencedProjects = new List<VisualStudioProject>();
        if (loadReferencedProjects && projectReferences?.Any() == true)
        {
            var basePath = new FileInfo(projectFilePath).DirectoryName;
            foreach (var projectRef in projectReferences)
            {
                var fullPath = Path.GetFullPath(projectRef, basePath);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                var referencedProject = GetProject(fullPath, loadReferencedProjects: false);
                referencedProjects.Add(referencedProject);
            }
        }

        var packageReferences = GetPackages(doc, nsm, projectFilePath);

        var references = GetReferences(doc, nsm);

        var projectName = GetRootNamespace(doc, nsm);
        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = new FileInfo(projectFilePath).Name.Replace(".csproj", "");
        }

        var project = new VisualStudioProject
        {
            Name = projectName,
            RelativePath = projectFilePath,
            ReferencedProjects = referencedProjects,
            Packages = packageReferences.Packages,
            References = references,    
            IsPackageReferenceProject = packageReferences.UsesProjectPackageReferences,
            ProjectGuid = GetProjectGuid(doc, nsm),
            TargetFrameworks = GetTargetFrameworks(doc, nsm)
        };

        return project;
    }

    private string[] GetTargetFrameworks(XmlDocument doc, XmlNamespaceManager nsm)
    {
        var result = new List<string>();
        var text = doc.SelectSingleNode("//ms:TargetFrameworkVersion", nsm)?.InnerText;
            
        if (!string.IsNullOrWhiteSpace(text))
        {
            result.Add(text);
        }

        if (result.Count == 0)
        {
            text = doc.SelectSingleNode("//TargetFramework")?.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Add(text);
            }
        }

        if (result.Count == 0)
        {
            text = doc.SelectSingleNode("//TargetFrameworks")?.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.AddRange(text.Split(new []{';'}, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        if (result.Count == 0)
        {
            Debugger.Break();
        }

        return result.ToArray();
    }

    private Guid GetProjectGuid(XmlDocument doc, XmlNamespaceManager nsm)
    {
        var projectGuidNode = doc.SelectSingleNode("//ms:ProjectGuid", nsm);
        if (projectGuidNode == null)
        {
            projectGuidNode = doc.SelectSingleNode("//ProjectGuid");
        }

        if (!string.IsNullOrWhiteSpace(projectGuidNode?.InnerText))
        {
            return new Guid(projectGuidNode.InnerText);
        }

        return Guid.NewGuid();
    }

    private string GetRootNamespace(XmlDocument doc, XmlNamespaceManager nsm)
    {
        var rootNamespaceNode = doc.SelectSingleNode("//ms:RootNamespace", nsm);
        if (rootNamespaceNode == null)
        {
            rootNamespaceNode = doc.SelectSingleNode("//RootNamespace");
        }
        return rootNamespaceNode?.InnerText;
    }

    private IEnumerable<VisualStudioProject> ParseProjects(string solutionFilePath)
    {
        var solutionFileInfo = new FileInfo(solutionFilePath);
        var lines = File.ReadAllLines(solutionFilePath);

        string ParseProjectPath(string line)
        {
            var lineParts = line.Split(',');
            var projectName = lineParts[0].Split('=')[1].Replace("\"", "").Trim();
            var relativePath = lineParts[1].Replace("\"", "").Trim();
            var filePath = Path.GetFullPath(Path.Combine(solutionFileInfo.DirectoryName ?? string.Empty, relativePath));
            return filePath;
        }

        var projects = new List<VisualStudioProject>();
        foreach (var line in lines)
        {
            if (line.StartsWith("Project"))
            {
                var path = ParseProjectPath(line);
                if (!File.Exists(path) || !string.Equals(".csproj", new FileInfo(path).Extension, StringComparison.OrdinalIgnoreCase))
                    continue;

                var project = GetProject(path);
                projects.Add(project);
            }

        }
        return projects;
    }

    public IEnumerable<VisualStudioSolution> GetSolutions(string topDirectory)
    {
        var solutionFiles = Directory.EnumerateFiles(topDirectory, "*.sln", SearchOption.AllDirectories);
        var result = new List<VisualStudioSolution>();
        foreach (var solutionFile in solutionFiles)
        {
            var solution = GetSolution(solutionFile);
            result.Add(solution);
        }
        return result;
    }
}