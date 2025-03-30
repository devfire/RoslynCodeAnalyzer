// DataModel.cs
using System.Collections.Generic;

namespace RoslynCodeAnalyzer;

// Record to hold the final analysis result
public record AnalysisResult(
    List<CodeNode> Nodes,
    List<CodeEdge> Edges
);

// Represents an entity in the code (class, interface, method, etc.)
public record CodeNode(
    string Id,         // Unique identifier (e.g., fully qualified name)
    string Type,       // e.g., "Class", "Interface", "Method", "Namespace"
    string Name,       // Simple name
    string FilePath,
    int StartLine,
    int EndLine,
    string? Comment = null,     // Optional: XML doc comment summary
    string? Signature = null,   // Optional: Method signature
    string? CodeSnippet = null // <<< ADD OR ENSURE THIS LINE EXISTS
);

// Represents a relationship between two nodes
public record CodeEdge(
    string SourceId,   // ID of the source node
    string TargetId,   // ID of the target node
    string Type        // e.g., "CONTAINS", "INHERITS_FROM", "IMPLEMENTS", "CALLS"
);

// Helper to get location info
public record LocationInfo(
    string FilePath,
    int StartLine,
    int EndLine
);