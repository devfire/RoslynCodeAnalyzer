// CodeStructureWalker.cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq; // For parsing XML comments

namespace RoslynCodeAnalyzer;

public class CodeStructureWalker : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly Dictionary<string, CodeNode> _nodes = new(); // Use dictionary to avoid duplicates
    private readonly List<CodeEdge> _edges = new();
    private readonly string _filePath;

    // Keep track of the current container (namespace/class) to create CONTAINS edges
    private readonly Stack<string> _containerIdStack = new(); 

    // Format for generating unique IDs using fully qualified names
    private static readonly SymbolDisplayFormat FullyQualifiedFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeModifiers,
        delegateStyle: SymbolDisplayDelegateStyle.NameAndParameters,
        extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeDefaultValue,
        propertyStyle: SymbolDisplayPropertyStyle.NameOnly, // Adjust as needed
        localOptions: SymbolDisplayLocalOptions.None,
        kindOptions: SymbolDisplayKindOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );


    public CodeStructureWalker(SemanticModel semanticModel, string filePath)
    {
        _semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _filePath = filePath;
    }

    public (Dictionary<string, CodeNode> Nodes, List<CodeEdge> Edges) GetResults()
    {
        return (_nodes, _edges);
    }

    // Helper to add node if it doesn't exist
    private void AddNode(CodeNode node)
    {
        _nodes.TryAdd(node.Id, node); // Add only if ID is not already present

        // Add CONTAINS edge from current container if applicable
        if (_containerIdStack.TryPeek(out var containerId))
        {
             AddEdge(new CodeEdge(containerId, node.Id, "CONTAINS"));
        }
    }
     
    // Helper to add edge
    private void AddEdge(CodeEdge edge)
    {
        // Optional: Could check for duplicates if necessary, but list is often fine
        _edges.Add(edge);
    }

    // Helper to get location
    private LocationInfo GetLocation(SyntaxNode node)
    {
         var lineSpan = node.GetLocation().GetLineSpan();
         return new LocationInfo(
             _filePath, // Use the walker's file path
             lineSpan.StartLinePosition.Line + 1, // Line numbers are 0-based
             lineSpan.EndLinePosition.Line + 1
         );
    }
    
    // Helper to get XML comments summary
    private string? GetSummaryComment(SyntaxNode node)
    {
        var xmlTrivia = node.GetLeadingTrivia()
            .Select(i => i.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        if (xmlTrivia != null)
        {
            try
            {
                var xmlContent = xmlTrivia.Content.ToString();
                // Basic parsing - robust parsing might need more effort
                 var xmlDoc = XDocument.Parse("<root>" + xmlContent + "</root>", LoadOptions.PreserveWhitespace); // Wrap for valid XML
                 return xmlDoc.Descendants("summary").FirstOrDefault()?.Value.Trim();
            }
            catch (Exception ex) 
            {
                 Console.Error.WriteLine($"Warning: Failed to parse XML comment for node near line {GetLocation(node).StartLine}. Error: {ex.Message}");
                 return null; // Failed to parse
            }
        }
        return null;
    }


    // --- Visit Methods ---

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol != null)
        {
            string id = symbol.ToDisplayString(FullyQualifiedFormat);
            var location = GetLocation(node);
            var codeNode = new CodeNode(id, "Namespace", symbol.Name, location.FilePath, location.StartLine, location.EndLine);
            AddNode(codeNode);

            _containerIdStack.Push(id); // Push namespace onto stack
            base.VisitNamespaceDeclaration(node); // Visit children
            _containerIdStack.Pop(); // Pop namespace from stack
        } else {
             // Handle case where namespace symbol couldn't be resolved (less common)
             base.VisitNamespaceDeclaration(node);
        }
    }
     // Handle FileScopedNamespaceDeclarationSyntax as well (C# 10+)
    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol != null)
        {
            string id = symbol.ToDisplayString(FullyQualifiedFormat);
            var location = GetLocation(node); 
            var codeNode = new CodeNode(id, "Namespace", symbol.Name, location.FilePath, location.StartLine, location.EndLine);
            AddNode(codeNode);

            _containerIdStack.Push(id); 
            base.VisitFileScopedNamespaceDeclaration(node); 
            _containerIdStack.Pop(); 
        } else {
             base.VisitFileScopedNamespaceDeclaration(node);
        }
    }


    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol != null)
        {
            string id = symbol.ToDisplayString(FullyQualifiedFormat);
            var location = GetLocation(node);
            var comment = GetSummaryComment(node);
            var codeNode = new CodeNode(id, "Class", symbol.Name, location.FilePath, location.StartLine, location.EndLine, Comment: comment);
            AddNode(codeNode);

             // Handle Inheritance (Base Class)
             if (symbol.BaseType != null && symbol.BaseType.SpecialType != SpecialType.System_Object) // Don't link to System.Object explicitly unless needed
             {
                 string baseTypeId = symbol.BaseType.ToDisplayString(FullyQualifiedFormat);
                 AddEdge(new CodeEdge(id, baseTypeId, "INHERITS_FROM"));
                 // Optional: Add node for base type if not found? Might cross assembly boundaries.
             }

             // Handle Implemented Interfaces
             foreach (var iface in symbol.Interfaces)
             {
                 string interfaceId = iface.ToDisplayString(FullyQualifiedFormat);
                 AddEdge(new CodeEdge(id, interfaceId, "IMPLEMENTS"));
                  // Optional: Add node for interface type if not found?
             }


            _containerIdStack.Push(id); // Push class onto stack
            base.VisitClassDeclaration(node); // Visit children (methods, nested classes)
            _containerIdStack.Pop(); // Pop class from stack
        } else {
             base.VisitClassDeclaration(node);
        }
    }

     public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
         var symbol = _semanticModel.GetDeclaredSymbol(node);
         if (symbol != null)
         {
             string id = symbol.ToDisplayString(FullyQualifiedFormat);
             var location = GetLocation(node);
             var comment = GetSummaryComment(node);
             var codeNode = new CodeNode(id, "Interface", symbol.Name, location.FilePath, location.StartLine, location.EndLine, Comment: comment);
             AddNode(codeNode);

             // Handle Interface Inheritance (Base Interfaces)
              foreach (var iface in symbol.Interfaces) // Interfaces inherit from other interfaces via 'Interfaces' property
             {
                 string baseInterfaceId = iface.ToDisplayString(FullyQualifiedFormat);
                 AddEdge(new CodeEdge(id, baseInterfaceId, "INHERITS_FROM")); // Use INHERITS_FROM for interface inheritance too
             }

             _containerIdStack.Push(id); 
             base.VisitInterfaceDeclaration(node); 
             _containerIdStack.Pop();
         } else {
             base.VisitInterfaceDeclaration(node);
         }
    }


    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol != null)
        {
            string id = symbol.ToDisplayString(FullyQualifiedFormat);
            var location = GetLocation(node);
            var comment = GetSummaryComment(node);
           var signature = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat
                // Member options only control things directly related to the member itself (like parameters)
                .WithMemberOptions(SymbolDisplayMemberOptions.IncludeParameters) 
                // Use GenericsOptions to control display of type parameters (<T>)
                .WithGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeParameters) 
                .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeParamsRefOut)
                );

            var codeNode = new CodeNode(id, "Method", symbol.Name, location.FilePath, location.StartLine, location.EndLine, Comment: comment, Signature: signature);
            AddNode(codeNode);

            // Don't push methods onto the container stack unless you want to track variables defined inside
            base.VisitMethodDeclaration(node); // Visit the method body
        } else {
            base.VisitMethodDeclaration(node);
        }
    }

    // TODO: Add VisitInvocationExpression for CALLS edges (more complex)
    // TODO: Add Visit Struct, Enum, Property, Field etc. as needed

}