using System.Diagnostics;

namespace GraphvizDemo
{
    /// <summary>
    /// Wrapper around the Graphviz dot executable.
    /// </summary>
    public class GraphvizDotWrapper
    {
        private readonly string _dotExePath;
        /// <summary>
        /// Initializes a new instance of the <see cref="GraphvizDotWrapper"/> class.
        /// </summary>
        /// <param name="dotExePath">Fully qualified path to the dot.exe executable e.g. C:\Program Files\Graphviz\bin\dot.exe</param>
        /// <exception cref="ArgumentNullException"></exception>
        public GraphvizDotWrapper(string dotExePath)
        {
            _dotExePath = dotExePath ?? throw new ArgumentNullException(nameof(dotExePath));
        }

        public byte[] Render(string dotString, string? format = "svg")
        {
            if (!new[] { "svg", "pdf", "jpg", "gif" }.Contains(format, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Invalid format: {format}", nameof(format));
            }

            var dotFile = Path.GetTempFileName();
            File.WriteAllText(dotFile, dotString);
            var outputFile = Path.GetTempFileName();

            var process = new Process();
            // Stop the process from opening a new window
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // Setup exe and params
            process.StartInfo.FileName = _dotExePath;
            process.StartInfo.Arguments = $@" -T{format} {dotFile}  -o {outputFile}";

            // Go!
            process.Start();

            // Wait for process to finish
            process.WaitForExit();

            // Read the diagram from the temp file
            return File.ReadAllBytes(outputFile);
        }
    }
}
