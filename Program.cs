// Program.cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RoslynCodeAnalyzer;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8; // Ensure correct JSON output
        Console.Error.WriteLine("Roslyn Code Analyzer"); // Log to stderr

        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: RoslynCodeAnalyzer <path/to/solution.sln | path/to/project.csproj>");
            return;
        }

        string projectOrSolutionPath = args[0];
        if (!File.Exists(projectOrSolutionPath) && !Directory.Exists(Path.GetDirectoryName(projectOrSolutionPath))) // Basic check
        {
             Console.Error.WriteLine($"Error: Path not found - {projectOrSolutionPath}");
             return;
        }

        var stopwatch = Stopwatch.StartNew();

        Console.Error.WriteLine($"Loading workspace for: {projectOrSolutionPath}...");
        using var workspace = MSBuildWorkspace.Create();
        workspace.LoadMetadataForReferencedProjects = true; // Important for resolving types across projects

        try
        {
            Project? singleProject = null;
            Solution? solution = null;

            if (projectOrSolutionPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                singleProject = await workspace.OpenProjectAsync(projectOrSolutionPath);
                Console.Error.WriteLine($"Loaded project: {singleProject.Name}");
                solution = singleProject.Solution; // Get solution containing the project
            }
            else if (projectOrSolutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                solution = await workspace.OpenSolutionAsync(projectOrSolutionPath);
                Console.Error.WriteLine($"Loaded solution with {solution.Projects.Count()} projects.");
            }
            else
            {
                 Console.Error.WriteLine("Error: Invalid file type. Please provide a .sln or .csproj file.");
                 return;
            }

            if (solution == null)
            {
                 Console.Error.WriteLine("Error: Could not load solution or project.");
                 return;
            }


            // --- Analysis ---
            Console.Error.WriteLine("Analyzing code...");
            var allNodes = new Dictionary<string, CodeNode>();
            var allEdges = new List<CodeEdge>();

            // Process projects in parallel for potentially faster analysis
            var projectTasks = solution.Projects.Select(async proj =>
            {
                 Console.Error.WriteLine($"  Analyzing project: {proj.Name}");
                 var nodesInProject = new Dictionary<string, CodeNode>();
                 var edgesInProject = new List<CodeEdge>();
                 var compilation = await proj.GetCompilationAsync(); // Get compilation for semantic info

                 if (compilation == null)
                 {
                      Console.Error.WriteLine($"Warning: Could not get compilation for project {proj.Name}. Skipping.");
                      return (nodesInProject, edgesInProject); // Return empty results for this project
                 }

                 foreach (var document in proj.Documents)
                 {
                     // Only process C# files that are part of the compilation
                      if (document.SourceCodeKind != SourceCodeKind.Regular || !document.SupportsSyntaxTree || !document.SupportsSemanticModel) continue;

                     try
                     {
                          var syntaxTree = await document.GetSyntaxTreeAsync();
                          var semanticModel = await document.GetSemanticModelAsync();

                          if (syntaxTree != null && semanticModel != null)
                          {
                              var walker = new CodeStructureWalker(semanticModel, document.FilePath ?? "unknown");
                              walker.Visit(syntaxTree.GetRoot());
                              var (nodes, edges) = walker.GetResults();
                              
                              // Merge results from this document
                              foreach(var kvp in nodes) { nodesInProject.TryAdd(kvp.Key, kvp.Value); } // Add new nodes found in this doc
                              edgesInProject.AddRange(edges);
                          }
                     }
                     catch (Exception ex)
                     {
                          Console.Error.WriteLine($"Error processing document {document.Name} in project {proj.Name}: {ex.Message}");
                           // Optionally continue processing other documents
                     }
                 }
                 return (nodesInProject, edgesInProject);

            });

             // Await all project analyses and aggregate results
            var results = await Task.WhenAll(projectTasks);
            foreach(var (projectNodes, projectEdges) in results) 
            {
                 foreach(var kvp in projectNodes) { allNodes.TryAdd(kvp.Key, kvp.Value); } // Merge nodes globally
                 allEdges.AddRange(projectEdges); // Add all edges
            }


            // --- Output ---
            Console.Error.WriteLine("Analysis complete. Serializing JSON...");
            var analysisResult = new AnalysisResult(allNodes.Values.ToList(), allEdges);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true, // Pretty print JSON
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase // Use camelCase for properties
            };

            string jsonOutput = JsonSerializer.Serialize(analysisResult, options);

            Console.WriteLine(jsonOutput); // Write JSON to standard output

            stopwatch.Stop();
            Console.Error.WriteLine($"Analysis finished in {stopwatch.ElapsedMilliseconds} ms.");
            Console.Error.WriteLine($"Found {allNodes.Count} nodes and {allEdges.Count} edges.");

        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"An error occurred: {ex.ToString()}");
        }
        finally
        {
             workspace.Dispose(); // Dispose the workspace
        }
    }
}