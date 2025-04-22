# Roslyn Code Analyzer: C# Codebase Graph Generator

## Overview

The Roslyn Code Analyzer is a powerful tool built using the .NET Roslyn compiler platform. Its primary purpose is to analyze C# solutions (.sln) or projects (.csproj) and generate a detailed **graph representation** of the codebase. This graph, consisting of nodes (code elements) and edges (relationships), is outputted as a JSON object, making it ideal for consumption by downstream analysis tools, particularly **Retrieval-Augmented Generation (RAG)** systems.

By parsing the syntax and leveraging semantic analysis, the tool extracts rich information including namespaces, types (classes, interfaces, enums, structs), members (methods, properties, fields), documentation comments, code snippets, and structural relationships like inheritance and containment.

Primarily, it's meant to be used by https://github.com/devfire/lightrag-csharp for Neo4j upload but can be used with any graph database (AWS Neptune, etc.)

## Key Features

*   **Roslyn-Powered Analysis:** Utilizes the official .NET Compiler Platform (Roslyn) for accurate syntax and semantic analysis of C# code.
*   **Graph Representation:** Models the codebase as a graph with `CodeNode` (representing code elements) and `CodeEdge` (representing relationships).
*   **Rich Metadata Extraction:** Captures details like fully qualified names, element types, file locations, XML documentation comments, code snippets, and method signatures.
*   **Relationship Mapping:** Identifies and records relationships such as `CONTAINS`, `INHERITS_FROM`, and `IMPLEMENTS`.
*   **Solution & Project Support:** Can analyze both individual `.csproj` files and entire `.sln` solutions.
*   **JSON Output:** Exports the code graph structure in a clean, machine-readable JSON format (using camelCase).
*   **Targeted for RAG:** Specifically designed to produce structured data suitable for feeding into RAG pipelines for code understanding and generation tasks.

## How It Works

The analysis process follows these steps:

1.  **Workspace Loading (`Program.cs`):** The application takes the path to a `.sln` or `.csproj` file as a command-line argument. It uses `Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace` to load the specified solution or project, including referenced projects and metadata.
2.  **Project Iteration (`Program.cs`):** It iterates through each project within the loaded workspace. Analysis of projects can occur in parallel for efficiency.
3.  **Document Processing (`Program.cs`):** For each C# document within a project, it retrieves the `SyntaxTree` and `SemanticModel`.
4.  **Code Traversal (`CodeStructureWalker.cs`):** An instance of `CodeStructureWalker` (a `CSharpSyntaxWalker`) traverses the syntax tree of each document.
5.  **Node & Edge Creation (`CodeStructureWalker.cs`):** As the walker visits different syntax nodes (like `ClassDeclarationSyntax`, `MethodDeclarationSyntax`, etc.), it uses the `SemanticModel` to get detailed symbol information. It creates:
    *   `CodeNode` objects for elements like namespaces, classes, interfaces, methods, properties, fields, enums, and enum members. Each node stores metadata (ID, type, name, location, comment, snippet, signature).
    *   `CodeEdge` objects to represent relationships like `CONTAINS` (e.g., a class contains a method), `INHERITS_FROM` (class inheritance), and `IMPLEMENTS` (class/struct implementing an interface).
6.  **Aggregation (`Program.cs`):** The nodes and edges collected from all documents and projects are aggregated into a single list.
7.  **JSON Serialization (`Program.cs`, `DataModel.cs`):** The final aggregated list of nodes and edges is encapsulated in an `AnalysisResult` object and serialized to JSON using `System.Text.Json`. The JSON output is written to the standard output stream (`stdout`). Progress and error messages are written to standard error (`stderr`).

## Output Format

The tool outputs a single JSON object to `stdout`. The structure is defined in `DataModel.cs`:

```json
{
  "nodes": [
    {
      "id": "string (Fully Qualified Name)",
      "type": "string (e.g., Class, Method, Namespace)",
      "name": "string (Simple Name)",
      "filePath": "string",
      "startLine": number,
      "endLine": number,
      "comment": "string | null (XML Doc Summary)",
      "signature": "string | null (Method Signature)",
      "codeSnippet": "string | null (Source Code Text)"
    }
    // ... more nodes
  ],
  "edges": [
    {
      "sourceId": "string (ID of source node)",
      "targetId": "string (ID of target node)",
      "type": "string (e.g., CONTAINS, INHERITS_FROM, IMPLEMENTS)"
    }
    // ... more edges
  ]
}
```

**Example Snippet:**

```json
{
  "nodes": [
    {
      "id": "MyNamespace.MyClass",
      "type": "Class",
      "name": "MyClass",
      "filePath": "/path/to/MyClass.cs",
      "startLine": 10,
      "endLine": 55,
      "comment": "This is a sample class.",
      "signature": null,
      "codeSnippet": "public class MyClass : IMyInterface\n{\n    // ... members ...\n}"
    },
    {
      "id": "MyNamespace.MyClass.MyMethod(int)",
      "type": "Method",
      "name": "MyMethod",
      "filePath": "/path/to/MyClass.cs",
      "startLine": 25,
      "endLine": 35,
      "comment": "Performs an important task.",
      "signature": "MyMethod(int value)",
      "codeSnippet": "public void MyMethod(int value)\n    {\n        // ... implementation ...\n    }"
    }
  ],
  "edges": [
    {
      "sourceId": "MyNamespace.MyClass",
      "targetId": "MyNamespace.MyClass.MyMethod(int)",
      "type": "CONTAINS"
    },
    {
      "sourceId": "MyNamespace.MyClass",
      "targetId": "MyNamespace.IMyInterface",
      "type": "IMPLEMENTS"
    }
  ]
}
```

## Use Case: RAG Input

The primary goal of this tool is to generate structured data about a codebase that can be effectively used by Retrieval-Augmented Generation (RAG) systems. The JSON output provides:

*   **Nodes:** Discrete units of code (classes, methods) with their source code (`codeSnippet`) and documentation (`comment`).
*   **Edges:** Relationships between these units, providing context about inheritance, implementation, and structure.

This graph allows RAG systems to retrieve relevant code snippets and understand their context within the larger codebase, leading to more accurate and context-aware code generation or analysis.

## Requirements

*   [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.

## Installation & Setup

1.  **Clone the Repository:**
    ```sh
    git clone https://github.com/your-username/RoslynCodeAnalyzer.git # Replace with actual URL if available
    cd RoslynCodeAnalyzer
    ```
2.  **Restore Dependencies:**
    ```sh
    dotnet restore
    ```

## Building the Project

Build the project using the .NET CLI:

```sh
dotnet build
```

This command compiles the project. Use `-c Release` for a release build:

```sh
dotnet build -c Release
```

## Usage

Run the analyzer from the command line, providing the path to the solution or project file as an argument.

```sh
# Analyze a solution file
dotnet run --project ./RoslynCodeAnalyzer.csproj -- /path/to/your/solution.sln > output.json

# Analyze a project file
dotnet run --project ./RoslynCodeAnalyzer.csproj -- /path/to/your/project.csproj > output.json

# Using the built executable (e.g., after 'dotnet build -c Release')
./bin/Release/net8.0/RoslynCodeAnalyzer /path/to/your/solution.sln > output.json
```

*   Replace `/path/to/your/solution.sln` or `/path/to/your/project.csproj` with the actual path to your target file.
*   The `>` redirects the JSON output (stdout) to a file named `output.json`.
*   Progress messages and errors will be printed to the console (stderr).

## Data Model

*   **`CodeNode`**: Represents a code element.
    *   `Id`: Fully qualified name (unique identifier).
    *   `Type`: Category (e.g., "Class", "Method").
    *   `Name`: Simple name of the element.
    *   `FilePath`: Path to the source file.
    *   `StartLine`, `EndLine`: Location within the file.
    *   `Comment`: XML documentation summary.
    *   `Signature`: Formal signature (primarily for methods).
    *   `CodeSnippet`: The raw source code text of the element.
*   **`CodeEdge`**: Represents a relationship between two nodes.
    *   `SourceId`: ID of the node where the relationship originates.
    *   `TargetId`: ID of the node where the relationship terminates.
    *   `Type`: Type of relationship (e.g., "CONTAINS", "INHERITS_FROM", "IMPLEMENTS").

## Future Enhancements

*   **`CALLS` Edges:** Implement analysis of method invocation expressions (`VisitInvocationExpression`) to create `CALLS` edges, showing which methods call others.
*   **Attribute Analysis:** Extract information from attributes decorating code elements.
*   **More Granular Snippets:** Option for smaller, more focused code snippets (e.g., method body only).
*   **Configuration Options:** Allow configuration via file or command-line arguments (e.g., filtering elements, choosing output details).

## Contributing

Contributions are welcome! Please follow standard fork-and-pull-request workflow. Ensure code style consistency and add tests where appropriate.

1.  Fork the repository.
2.  Create a feature branch (`git checkout -b feature/my-new-feature`).
3.  Commit your changes (`git commit -am 'Add some feature'`).
4.  Push to the branch (`git push origin feature/my-new-feature`).
5.  Open a Pull Request.

## License

This project is licensed under the MIT License. 
