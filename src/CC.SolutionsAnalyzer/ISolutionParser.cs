namespace CC.SolutionsAnalyzer;

public interface ISolutionParser
{
    VisualStudioSolution GetSolution(string filePath);

    IEnumerable<VisualStudioSolution> GetSolutions(string topDirectory);

    VisualStudioProject GetProject(string projectFilePath, bool loadReferencedProjects = true);
    (IEnumerable<Package> Packages, bool UsesProjectPackageReferences) GetPackages(string projectFilePath);
    IEnumerable<string> GetProjectReferences(string projectFilePath);
    IEnumerable<string> GetReferences(string projectFilePath);
}