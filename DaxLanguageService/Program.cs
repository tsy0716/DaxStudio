using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace DaxLanguageService
{
    class Program
    {
        private static DaxLanguageService _languageService;

        static async Task Main(string[] args)
        {
            _languageService = new DaxLanguageService();
            
            Console.WriteLine("DAX Language Service Started");
            
            // Listen for JSON-RPC style commands from Python
            while (true)
            {
                try
                {
                    var line = Console.ReadLine();
                    if (string.IsNullOrEmpty(line))
                        break;

                    var response = await ProcessCommand(line);
                    Console.WriteLine(response);
                }
                catch (Exception ex)
                {
                    var errorResponse = JsonSerializer.Serialize(new { error = ex.Message });
                    Console.WriteLine(errorResponse);
                }
            }
        }

        private static async Task<string> ProcessCommand(string jsonCommand)
        {
            var command = JsonSerializer.Deserialize<JsonElement>(jsonCommand);
            var method = command.GetProperty("method").GetString();

            switch (method)
            {
                case "completion":
                    var completionRequest = JsonSerializer.Deserialize<CompletionRequest>(
                        command.GetProperty("params").GetRawText());
                    var completionResponse = await _languageService.GetCompletions(completionRequest);
                    return JsonSerializer.Serialize(completionResponse);

                case "signatureHelp":
                    var signatureRequest = JsonSerializer.Deserialize<SignatureHelpRequest>(
                        command.GetProperty("params").GetRawText());
                    var signatureResponse = await _languageService.GetSignatureHelp(signatureRequest);
                    return JsonSerializer.Serialize(signatureResponse);

                case "hover":
                    var hoverRequest = JsonSerializer.Deserialize<HoverRequest>(
                        command.GetProperty("params").GetRawText());
                    var hoverResponse = await _languageService.GetHover(hoverRequest);
                    return JsonSerializer.Serialize(hoverResponse);

                case "diagnostics":
                    var diagnosticsRequest = JsonSerializer.Deserialize<DiagnosticsRequest>(
                        command.GetProperty("params").GetRawText());
                    var diagnosticsResponse = await _languageService.GetDiagnostics(diagnosticsRequest);
                    return JsonSerializer.Serialize(diagnosticsResponse);

                case "setModel":
                    var modelMetadata = JsonSerializer.Deserialize<ModelMetadata>(
                        command.GetProperty("params").GetRawText());
                    _languageService.SetModel(modelMetadata);
                    return JsonSerializer.Serialize(new { success = true });

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown method: {method}" });
            }
        }
    }
}