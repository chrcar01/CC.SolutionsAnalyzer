using System.Diagnostics;
using CC.SolutionsAnalyzer;

namespace GraphvizDemo;

internal class Program
{
    static void Main(string[] args)
    {
        var solutionFileParser = new SolutionFileParser();

        // Get all solutions we want to map dependencies for
        var solutionFiles = new[]
        {
            @"D:\source\svn\ARS\Omnis\ErpTaskRunner.sln",
            @"D:\source\svn\ARS\Omnis\OMNIS.sln",
            @"D:\source\svn\ARS\Omnis\UmlerEhmsTaskRunner.sln"
        };
        var solutions = solutionFiles.Select(solutionFileParser.GetSolution);

        // Alternatively, you can use the solution file parser to get all solutions in a directory:
        // var solutions = solutionFileParser.GetSolutionsInDirectory(@"D:\source\svn\ARS\Omnis");

        // Create a graphviz dot string, https://graphviz.org/doc/info/lang.html
        var dotString = CreateSolutionsAndProjectsDependenciesDotString(solutions);
        // Test out the dot string here: https://dreampuf.github.io/GraphvizOnline/

        // make sure you have graphviz installed: https://graphviz.org/download/
        var dotExePath = @"C:\Program Files\Graphviz\bin\dot.exe";
        var dot = new GraphvizDotWrapper(dotExePath);

        // Render the dot string to a byte array
        var svg = dot.Render(dotString, format: "svg");

        // Save the byte array to a file
        var diagramSvgFilePath = @"D:\output.svg";
        File.WriteAllBytes(diagramSvgFilePath, svg);

        // Open the file
        OpenWithDefaultProgram(diagramSvgFilePath);
    }

    static string CreateSolutionsAndProjectsDependenciesDotString(IEnumerable<VisualStudioSolution> solutions)
    {
        var allProjects = solutions
            .SelectMany(s => s.Projects)
            .GroupBy(p => p.Name)
            .Select(g => g.First());

        using var text = new StringWriter();
        text.WriteLine("digraph Dependencies {");
        text.WriteLine("    rankdir=LR;");
        text.WriteLine("    node[shape = rect];");
        foreach (var sol in solutions)
        {
            text.WriteLine($"   \"{sol.Name}.sln\" [shape=circle,style=filled,color=red]");
            foreach (var p in sol.Projects)
            {
                text.WriteLine($"   \"{sol.Name}.sln\" -> \"{p.Name}.csproj\"");
                if (!p.ReferencedProjects.Any()) continue;
                    
                foreach (var projectName in p.ReferencedProjects.Select(x => x.Name))
                {
                    var dep = allProjects.FirstOrDefault(proj => proj.Name == projectName);
                    if (dep != null)
                    {
                        text.WriteLine($"   \"{p.Name}.csproj\" -> \"{dep.Name}.csproj\"");
                    }
                }
            }
        }
        text.WriteLine("}");
        return text.ToString();
    }

    /// <summary>
    /// Ripped from https://stackoverflow.com/a/54275102
    /// </summary>
    /// <param name="path">Fully qualified path to the file you want to open</param>
    static void OpenWithDefaultProgram(string path)
    {
        using Process fileopener = new Process();

        fileopener.StartInfo.FileName = "explorer";
        fileopener.StartInfo.Arguments = "\"" + path + "\"";
        fileopener.Start();
    }

}