using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ADOTabular;
using DaxStudio.UI.Utils;
using DaxStudio.UI.Utils.Intellisense;
using DaxStudio.UI.ViewModels;
using DaxStudio.UI.Model;
using DaxStudio.Interfaces;
using Caliburn.Micro;
using ICSharpCode.AvalonEdit.CodeCompletion;

namespace DaxLanguageService
{
    public class DaxLanguageService
    {
        private DaxIntellisenseProvider _intellisenseProvider;
        private DocumentViewModel _documentViewModel;
        private ADOTabularModel _model;
        private IEventAggregator _eventAggregator;
        private IGlobalOptions _options;

        public DaxLanguageService()
        {
            _eventAggregator = new EventAggregator();
            _options = new MockGlobalOptions();
            InitializeService();
        }

        private void InitializeService()
        {
            // Initialize with your semantic model provider
            // This would connect to your existing semantic provider
            _documentViewModel = new DocumentViewModel(_eventAggregator, _options);
            _intellisenseProvider = new DaxIntellisenseProvider(_documentViewModel, _eventAggregator, _options);
        }

        public async Task<CompletionResponse> GetCompletions(CompletionRequest request)
        {
            try
            {
                // Parse the line state using DaxStudio's parser
                var lineState = DaxLineParser.ParseLine(
                    request.Line, 
                    request.Column, 
                    request.LineOffset
                );

                // Get completion items based on context
                var completionItems = new List<CompletionItem>();
                
                if (lineState.LineState == LineState.Column && !string.IsNullOrEmpty(lineState.TableName))
                {
                    // Get columns for specific table
                    completionItems.AddRange(GetTableColumns(lineState.TableName));
                }
                else if (lineState.LineState == LineState.Table)
                {
                    // Get all tables
                    completionItems.AddRange(GetAllTables());
                }
                else
                {
                    // Default context: functions, keywords, tables, measures
                    completionItems.AddRange(GetAllFunctions());
                    completionItems.AddRange(GetAllKeywords());
                    completionItems.AddRange(GetAllTables());
                    completionItems.AddRange(GetAllMeasures());
                }

                return new CompletionResponse
                {
                    Items = completionItems,
                    IsIncomplete = false
                };
            }
            catch (Exception ex)
            {
                return new CompletionResponse
                {
                    Items = new List<CompletionItem>(),
                    IsIncomplete = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<SignatureHelpResponse> GetSignatureHelp(SignatureHelpRequest request)
        {
            try
            {
                // Extract function name from current position
                var functionName = DaxLineParser.GetPreceedingWord(request.Line);
                
                // Get function signature from DaxStudio's function metadata
                var function = GetFunctionSignature(functionName);
                
                if (function != null)
                {
                    return new SignatureHelpResponse
                    {
                        Signatures = new List<SignatureInformation>
                        {
                            new SignatureInformation
                            {
                                Label = function.Signature,
                                Documentation = function.Description,
                                Parameters = function.Parameters
                            }
                        },
                        ActiveSignature = 0,
                        ActiveParameter = GetActiveParameter(request.Line, request.Column)
                    };
                }

                return new SignatureHelpResponse { Signatures = new List<SignatureInformation>() };
            }
            catch (Exception ex)
            {
                return new SignatureHelpResponse 
                { 
                    Signatures = new List<SignatureInformation>(),
                    Error = ex.Message 
                };
            }
        }

        public async Task<HoverResponse> GetHover(HoverRequest request)
        {
            try
            {
                var lineState = DaxLineParser.ParseLine(request.Line, request.Column, request.LineOffset);
                var word = GetWordAtPosition(request.Line, request.Column);

                string hoverContent = null;

                // Check if it's a table reference
                if (lineState.LineState == LineState.Table && !string.IsNullOrEmpty(word))
                {
                    var table = GetTable(word);
                    if (table != null)
                    {
                        hoverContent = $"**Table: {table.Name}**\n\n{table.Description}";
                    }
                }
                // Check if it's a column reference
                else if (lineState.LineState == LineState.Column && !string.IsNullOrEmpty(lineState.TableName))
                {
                    var column = GetTableColumn(lineState.TableName, word);
                    if (column != null)
                    {
                        hoverContent = $"**Column: {column.Name}**\n\nTable: {lineState.TableName}\nData Type: {column.DataType}\n\n{column.Description}";
                    }
                }
                // Check if it's a function
                else if (IsFunctionName(word))
                {
                    var function = GetFunction(word);
                    if (function != null)
                    {
                        hoverContent = $"**Function: {function.Name}**\n\n{function.Description}\n\n**Syntax:**\n```dax\n{function.Syntax}\n```";
                    }
                }

                return new HoverResponse
                {
                    Contents = hoverContent,
                    Range = GetWordRange(request.Line, request.Column)
                };
            }
            catch (Exception ex)
            {
                return new HoverResponse { Error = ex.Message };
            }
        }

        public async Task<DiagnosticsResponse> GetDiagnostics(DiagnosticsRequest request)
        {
            try
            {
                var diagnostics = new List<Diagnostic>();

                // Use DaxStudio's parsing to detect syntax errors
                // This would integrate with your existing validation logic
                
                return new DiagnosticsResponse
                {
                    Diagnostics = diagnostics
                };
            }
            catch (Exception ex)
            {
                return new DiagnosticsResponse 
                { 
                    Diagnostics = new List<Diagnostic>(),
                    Error = ex.Message 
                };
            }
        }

        public void SetModel(ModelMetadata modelMetadata)
        {
            // Update the model with your semantic provider data
            _model = ConvertToADOTabularModel(modelMetadata);
            
            // Update intellisense provider with new model
            _intellisenseProvider = new DaxIntellisenseProvider(
                _documentViewModel, 
                _eventAggregator, 
                _options, 
                _model, 
                modelMetadata.DMVs, 
                modelMetadata.Functions
            );
        }

        // Helper methods for data conversion
        private List<CompletionItem> GetTableColumns(string tableName)
        {
            var items = new List<CompletionItem>();
            var table = _model?.Tables?.FirstOrDefault(t => 
                t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));
            
            if (table != null)
            {
                foreach (var column in table.Columns.Where(c => c.ObjectType == ADOTabularObjectType.Column))
                {
                    items.Add(new CompletionItem
                    {
                        Label = $"[{column.Name}]",
                        Detail = column.Caption,
                        Documentation = column.Description,
                        Kind = CompletionItemKind.Field,
                        SortText = "050"
                    });
                }
            }
            
            return items;
        }

        private List<CompletionItem> GetAllTables()
        {
            var items = new List<CompletionItem>();
            
            if (_model?.Tables != null)
            {
                foreach (var table in _model.Tables)
                {
                    items.Add(new CompletionItem
                    {
                        Label = table.DaxName,
                        Detail = table.Caption,
                        Documentation = table.Description,
                        Kind = CompletionItemKind.Class,
                        SortText = "100"
                    });
                }
            }
            
            return items;
        }

        private List<CompletionItem> GetAllFunctions()
        {
            var items = new List<CompletionItem>();
            
            // Get functions from DaxStudio's function metadata
            // This would integrate with your function provider
            
            return items;
        }

        private List<CompletionItem> GetAllKeywords()
        {
            return new List<CompletionItem>
            {
                new CompletionItem { Label = "EVALUATE", Kind = CompletionItemKind.Keyword, SortText = "200" },
                new CompletionItem { Label = "DEFINE", Kind = CompletionItemKind.Keyword, SortText = "200" },
                new CompletionItem { Label = "MEASURE", Kind = CompletionItemKind.Keyword, SortText = "200" },
                new CompletionItem { Label = "ORDER BY", Kind = CompletionItemKind.Keyword, SortText = "200" },
                new CompletionItem { Label = "ASC", Kind = CompletionItemKind.Keyword, SortText = "200" },
                new CompletionItem { Label = "DESC", Kind = CompletionItemKind.Keyword, SortText = "200" }
            };
        }

        private List<CompletionItem> GetAllMeasures()
        {
            var items = new List<CompletionItem>();
            
            if (_model?.Tables != null)
            {
                foreach (var table in _model.Tables)
                {
                    foreach (var measure in table.Columns.Where(c => c.ObjectType == ADOTabularObjectType.Measure))
                    {
                        items.Add(new CompletionItem
                        {
                            Label = $"[{measure.Name}]",
                            Detail = measure.Caption,
                            Documentation = measure.Description,
                            Kind = CompletionItemKind.Variable,
                            SortText = "050"
                        });
                    }
                }
            }
            
            return items;
        }

        // Additional helper methods would go here...
        private string GetWordAtPosition(string line, int column) { /* Implementation */ return ""; }
        private TableMetadata GetTable(string name) { /* Implementation */ return null; }
        private ColumnMetadata GetTableColumn(string tableName, string columnName) { /* Implementation */ return null; }
        private FunctionMetadata GetFunction(string name) { /* Implementation */ return null; }
        private bool IsFunctionName(string word) { /* Implementation */ return false; }
        private Range GetWordRange(string line, int column) { /* Implementation */ return new Range(); }
        private int GetActiveParameter(string line, int column) { /* Implementation */ return 0; }
        private FunctionSignature GetFunctionSignature(string functionName) { /* Implementation */ return null; }
        private ADOTabularModel ConvertToADOTabularModel(ModelMetadata metadata) { /* Implementation */ return null; }
    }

    // Mock options class for standalone operation
    public class MockGlobalOptions : IGlobalOptions
    {
        public bool EditorEnableIntellisense { get; set; } = true;
        public double CodeCompletionWindowWidthIncrease { get; set; } = 100;
        // Implement other required properties with default values
    }
}