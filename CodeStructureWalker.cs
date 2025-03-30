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
    // ... (Existing fields and constructor remain the same) ...
    private readonly SemanticModel _semanticModel;
    private readonly Dictionary<string, CodeNode> _nodes = new(); 
    private readonly List<CodeEdge> _edges = new();
    private readonly string _filePath;
    private readonly Stack<string> _containerIdStack = new(); 
    private static readonly SymbolDisplayFormat FullyQualifiedFormat = new(/* ... existing format options ... */);

    public CodeStructureWalker(SemanticModel semanticModel, string filePath)
    {
        _semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _filePath = filePath;
    }
    
    // ... (GetResults, AddNode, AddEdge, GetLocation, GetSummaryComment remain the same) ...
    
     public (Dictionary<string, CodeNode> Nodes, List<CodeEdge> Edges) GetResults()
    {
        return (_nodes, _edges);
    }
    
    private void AddNode(CodeNode node)
    {
        _nodes.TryAdd(node.Id, node); 
        if (_containerIdStack.TryPeek(out var containerId))
        {
             AddEdge(new CodeEdge(containerId, node.Id, "CONTAINS"));
        }
    }
     
    private void AddEdge(CodeEdge edge)
    {
        _edges.Add(edge);
    }
    
     private LocationInfo GetLocation(SyntaxNode node)
    {
         var lineSpan = node.GetLocation().GetLineSpan();
         return new LocationInfo(
             _filePath, 
             lineSpan.StartLinePosition.Line + 1, 
             lineSpan.EndLinePosition.Line + 1
         );
    }
    
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
                 var xmlDoc = XDocument.Parse("<root>" + xmlContent + "</root>", LoadOptions.PreserveWhitespace); 
                 return xmlDoc.Descendants("summary").FirstOrDefault()?.Value.Trim();
            }
            catch (Exception ex) 
            {
                 Console.Error.WriteLine($"Warning: Failed to parse XML comment for node near line {GetLocation(node).StartLine}. Error: {ex.Message}");
                 return null; 
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
            // Typically don't need full code snippet for namespace declaration itself
            var codeNode = new CodeNode(id, "Namespace", symbol.Name, location.FilePath, location.StartLine, location.EndLine); 
            AddNode(codeNode);

            _containerIdStack.Push(id); 
            base.VisitNamespaceDeclaration(node); 
            _containerIdStack.Pop(); 
        } else {
             base.VisitNamespaceDeclaration(node);
        }
    }
     
    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol != null)
        {
            string id = symbol.ToDisplayString(FullyQualifiedFormat);
            var location = GetLocation(node); 
             // Typically don't need full code snippet for namespace declaration itself
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
            string codeSnippet = node.ToString(); // <<< GET THE CODE SNIPPET

            // <<< PASS SNIPPET TO CodeNode CONSTRUCTOR
            var codeNode = new CodeNode(id, "Class", symbol.Name, location.FilePath, location.StartLine, location.EndLine, Comment: comment, CodeSnippet: codeSnippet); 
            AddNode(codeNode);

             // Handle Inheritance (remains the same)
             if (symbol.BaseType != null && symbol.BaseType.SpecialType != SpecialType.System_Object) 
             {
                 string baseTypeId = symbol.BaseType.ToDisplayString(FullyQualifiedFormat);
                 AddEdge(new CodeEdge(id, baseTypeId, "INHERITS_FROM"));
             }
             foreach (var iface in symbol.Interfaces)
             {
                 string interfaceId = iface.ToDisplayString(FullyQualifiedFormat);
                 AddEdge(new CodeEdge(id, interfaceId, "IMPLEMENTS"));
             }

            _containerIdStack.Push(id); 
            base.VisitClassDeclaration(node); 
            _containerIdStack.Pop(); 
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
             string codeSnippet = node.ToString(); // <<< GET THE CODE SNIPPET

             // <<< PASS SNIPPET TO CodeNode CONSTRUCTOR
             var codeNode = new CodeNode(id, "Interface", symbol.Name, location.FilePath, location.StartLine, location.EndLine, Comment: comment, CodeSnippet: codeSnippet); 
             AddNode(codeNode);

             // Handle Interface Inheritance (remains the same)
              foreach (var iface in symbol.Interfaces) 
             {
                 string baseInterfaceId = iface.ToDisplayString(FullyQualifiedFormat);
                 AddEdge(new CodeEdge(id, baseInterfaceId, "INHERITS_FROM")); 
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
                .WithMemberOptions(SymbolDisplayMemberOptions.IncludeParameters)
                .WithGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeParameters) 
                .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeParamsRefOut)
                ); 
            string codeSnippet = node.ToString(); // <<< GET THE CODE SNIPPET

            // <<< PASS SNIPPET TO CodeNode CONSTRUCTOR
            var codeNode = new CodeNode(id, "Method", symbol.Name, location.FilePath, location.StartLine, location.EndLine, Comment: comment, Signature: signature, CodeSnippet: codeSnippet); 
            AddNode(codeNode);

            base.VisitMethodDeclaration(node); // Visit method body (e.g., for future call analysis)
        } else {
            base.VisitMethodDeclaration(node);
        }
    }

    // TODO: Add VisitInvocationExpression for CALLS edges (more complex)
    // TODO: Add Visit Struct, Enum, Property, Field etc. and include CodeSnippet as needed

}