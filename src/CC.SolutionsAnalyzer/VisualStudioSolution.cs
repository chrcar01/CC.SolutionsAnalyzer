namespace CC.SolutionsAnalyzer;

public class VisualStudioSolution
{
    public string Name { get; set; }
    public string FilePath { get; set; }
    public IEnumerable<VisualStudioProject> Projects { get; set; }

    public VisualStudioSolution(string name, string filePath, IEnumerable<VisualStudioProject> projects) =>
        (Name, FilePath, Projects) = (name, filePath, projects);
}