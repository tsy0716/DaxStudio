using System.Collections.Generic;

namespace DaxLanguageService
{
    // Request/Response models for LSP communication
    
    public class CompletionRequest
    {
        public string Line { get; set; }
        public int Column { get; set; }
        public int LineOffset { get; set; }
        public string FullText { get; set; }
    }

    public class CompletionResponse
    {
        public List<CompletionItem> Items { get; set; } = new List<CompletionItem>();
        public bool IsIncomplete { get; set; }
        public string Error { get; set; }
    }

    public class CompletionItem
    {
        public string Label { get; set; }
        public string Detail { get; set; }
        public string Documentation { get; set; }
        public CompletionItemKind Kind { get; set; }
        public string SortText { get; set; }
        public string InsertText { get; set; }
        public string FilterText { get; set; }
    }

    public enum CompletionItemKind
    {
        Text = 1,
        Method = 2,
        Function = 3,
        Constructor = 4,
        Field = 5,
        Variable = 6,
        Class = 7,
        Interface = 8,
        Module = 9,
        Property = 10,
        Unit = 11,
        Value = 12,
        Enum = 13,
        Keyword = 14,
        Snippet = 15,
        Color = 16,
        File = 17,
        Reference = 18,
        Folder = 19,
        EnumMember = 20,
        Constant = 21,
        Struct = 22,
        Event = 23,
        Operator = 24,
        TypeParameter = 25
    }

    public class SignatureHelpRequest
    {
        public string Line { get; set; }
        public int Column { get; set; }
        public string FullText { get; set; }
    }

    public class SignatureHelpResponse
    {
        public List<SignatureInformation> Signatures { get; set; } = new List<SignatureInformation>();
        public int ActiveSignature { get; set; }
        public int ActiveParameter { get; set; }
        public string Error { get; set; }
    }

    public class SignatureInformation
    {
        public string Label { get; set; }
        public string Documentation { get; set; }
        public List<ParameterInformation> Parameters { get; set; } = new List<ParameterInformation>();
    }

    public class ParameterInformation
    {
        public string Label { get; set; }
        public string Documentation { get; set; }
    }

    public class HoverRequest
    {
        public string Line { get; set; }
        public int Column { get; set; }
        public int LineOffset { get; set; }
        public string FullText { get; set; }
    }

    public class HoverResponse
    {
        public string Contents { get; set; }
        public Range Range { get; set; }
        public string Error { get; set; }
    }

    public class DiagnosticsRequest
    {
        public string FullText { get; set; }
        public string Uri { get; set; }
    }

    public class DiagnosticsResponse
    {
        public List<Diagnostic> Diagnostics { get; set; } = new List<Diagnostic>();
        public string Error { get; set; }
    }

    public class Diagnostic
    {
        public Range Range { get; set; }
        public DiagnosticSeverity Severity { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
        public string Code { get; set; }
    }

    public enum DiagnosticSeverity
    {
        Error = 1,
        Warning = 2,
        Information = 3,
        Hint = 4
    }

    public class Range
    {
        public Position Start { get; set; } = new Position();
        public Position End { get; set; } = new Position();
    }

    public class Position
    {
        public int Line { get; set; }
        public int Character { get; set; }
    }

    // Model metadata for your semantic provider integration
    public class ModelMetadata
    {
        public List<TableMetadata> Tables { get; set; } = new List<TableMetadata>();
        public List<MeasureMetadata> Measures { get; set; } = new List<MeasureMetadata>();
        public List<FunctionMetadata> Functions { get; set; } = new List<FunctionMetadata>();
        public object DMVs { get; set; }  // ADOTabularDynamicManagementViewCollection
    }

    public class TableMetadata
    {
        public string Name { get; set; }
        public string Caption { get; set; }
        public string Description { get; set; }
        public List<ColumnMetadata> Columns { get; set; } = new List<ColumnMetadata>();
    }

    public class ColumnMetadata
    {
        public string Name { get; set; }
        public string Caption { get; set; }
        public string Description { get; set; }
        public string DataType { get; set; }
        public bool IsHidden { get; set; }
        public string TableName { get; set; }
    }

    public class MeasureMetadata
    {
        public string Name { get; set; }
        public string Caption { get; set; }
        public string Description { get; set; }
        public string Expression { get; set; }
        public string TableName { get; set; }
    }

    public class FunctionMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Syntax { get; set; }
        public List<string> Parameters { get; set; } = new List<string>();
        public string Category { get; set; }
    }

    public class FunctionSignature
    {
        public string Signature { get; set; }
        public string Description { get; set; }
        public List<ParameterInformation> Parameters { get; set; } = new List<ParameterInformation>();
    }
}