namespace CC.SolutionsAnalyzer;

public class VisualStudioProject
{
    public Guid ProjectGuid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public IEnumerable<VisualStudioProject> ReferencedProjects { get; set; } = new List<VisualStudioProject>();
    public IEnumerable<Package> Packages { get; set; } = new List<Package>();
    public IEnumerable<string> References { get; set; } = new List<string>();
    public string FilePath => Path.GetFullPath(RelativePath);
    public bool IsPackageReferenceProject { get; set; }
    public string[] TargetFrameworks { get; set; }
}