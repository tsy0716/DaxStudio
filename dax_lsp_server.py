#!/usr/bin/env python3

import asyncio
import json
import subprocess
import threading
from typing import List, Optional, Dict, Any
import logging

from lsprotocol import types as lsp
from pygls.server import LanguageServer
from pygls.workspace import Document

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class DaxStudioBridge:
    """Bridge to communicate with the .NET DaxStudio language service"""
    
    def __init__(self, executable_path: str = "./DaxLanguageService.exe"):
        self.executable_path = executable_path
        self.process = None
        self._lock = threading.Lock()
        
    def start(self):
        """Start the .NET language service process"""
        try:
            self.process = subprocess.Popen(
                [self.executable_path],
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                bufsize=1
            )
            logger.info("DaxStudio language service started")
        except Exception as e:
            logger.error(f"Failed to start DaxStudio service: {e}")
            raise
    
    def stop(self):
        """Stop the .NET language service process"""
        if self.process:
            self.process.terminate()
            self.process.wait()
            logger.info("DaxStudio language service stopped")
    
    async def send_command(self, method: str, params: Dict[str, Any]) -> Dict[str, Any]:
        """Send a command to the .NET service and get the response"""
        if not self.process:
            raise Exception("DaxStudio service not started")
        
        command = {
            "method": method,
            "params": params
        }
        
        try:
            with self._lock:
                # Send command
                command_json = json.dumps(command) + "\n"
                self.process.stdin.write(command_json)
                self.process.stdin.flush()
                
                # Read response
                response_line = self.process.stdout.readline()
                if not response_line:
                    raise Exception("No response from DaxStudio service")
                
                return json.loads(response_line.strip())
        except Exception as e:
            logger.error(f"Error communicating with DaxStudio service: {e}")
            raise

class DaxLanguageServer(LanguageServer):
    """DAX Language Server that wraps DaxStudio functionality"""
    
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.dax_bridge = DaxStudioBridge()
        
    def start_dax_service(self):
        """Start the DaxStudio bridge service"""
        self.dax_bridge.start()
        
    def stop_dax_service(self):
        """Stop the DaxStudio bridge service"""
        self.dax_bridge.stop()

# Create the language server instance
server = DaxLanguageServer("dax-lsp", "v0.1.0")

@server.feature(lsp.INITIALIZE)
async def initialize(params: lsp.InitializeParams) -> lsp.InitializeResult:
    """Initialize the language server"""
    logger.info("Initializing DAX Language Server")
    
    # Start the DaxStudio bridge
    server.start_dax_service()
    
    return lsp.InitializeResult(
        capabilities=lsp.ServerCapabilities(
            text_document_sync=lsp.TextDocumentSyncOptions(
                open_close=True,
                change=lsp.TextDocumentSyncKind.Full,
            ),
            completion_provider=lsp.CompletionOptions(
                trigger_characters=["[", "'", "(", ",", " "],
                resolve_provider=False
            ),
            signature_help_provider=lsp.SignatureHelpOptions(
                trigger_characters=["(", ","]
            ),
            hover_provider=True,
            diagnostic_provider=lsp.DiagnosticOptions(
                identifier="dax",
                inter_file_dependencies=False,
                workspace_diagnostics=False
            )
        )
    )

@server.feature(lsp.TEXT_DOCUMENT_COMPLETION)
async def completion(params: lsp.CompletionParams) -> lsp.CompletionList:
    """Provide completion suggestions"""
    try:
        document = server.workspace.get_document(params.text_document.uri)
        line = document.lines[params.position.line]
        
        # Prepare request for DaxStudio service
        request_params = {
            "line": line,
            "column": params.position.character,
            "lineOffset": document.offset_at_position(lsp.Position(line=params.position.line, character=0)),
            "fullText": document.source
        }
        
        # Get completions from DaxStudio
        response = await server.dax_bridge.send_command("completion", request_params)
        
        if "error" in response:
            logger.error(f"Completion error: {response['error']}")
            return lsp.CompletionList(is_incomplete=False, items=[])
        
        # Convert DaxStudio completion items to LSP format
        completion_items = []
        for item in response.get("items", []):
            lsp_item = lsp.CompletionItem(
                label=item.get("label", ""),
                detail=item.get("detail"),
                documentation=item.get("documentation"),
                kind=convert_completion_kind(item.get("kind", 1)),
                sort_text=item.get("sortText"),
                insert_text=item.get("insertText") or item.get("label", ""),
                filter_text=item.get("filterText")
            )
            completion_items.append(lsp_item)
        
        return lsp.CompletionList(
            is_incomplete=response.get("isIncomplete", False),
            items=completion_items
        )
        
    except Exception as e:
        logger.error(f"Completion failed: {e}")
        return lsp.CompletionList(is_incomplete=False, items=[])

@server.feature(lsp.TEXT_DOCUMENT_SIGNATURE_HELP)
async def signature_help(params: lsp.SignatureHelpParams) -> Optional[lsp.SignatureHelp]:
    """Provide signature help for functions"""
    try:
        document = server.workspace.get_document(params.text_document.uri)
        line = document.lines[params.position.line]
        
        request_params = {
            "line": line,
            "column": params.position.character,
            "fullText": document.source
        }
        
        response = await server.dax_bridge.send_command("signatureHelp", request_params)
        
        if "error" in response:
            logger.error(f"Signature help error: {response['error']}")
            return None
        
        signatures = []
        for sig in response.get("signatures", []):
            parameters = []
            for param in sig.get("parameters", []):
                parameters.append(lsp.ParameterInformation(
                    label=param.get("label", ""),
                    documentation=param.get("documentation")
                ))
            
            signatures.append(lsp.SignatureInformation(
                label=sig.get("label", ""),
                documentation=sig.get("documentation"),
                parameters=parameters
            ))
        
        return lsp.SignatureHelp(
            signatures=signatures,
            active_signature=response.get("activeSignature", 0),
            active_parameter=response.get("activeParameter", 0)
        )
        
    except Exception as e:
        logger.error(f"Signature help failed: {e}")
        return None

@server.feature(lsp.TEXT_DOCUMENT_HOVER)
async def hover(params: lsp.HoverParams) -> Optional[lsp.Hover]:
    """Provide hover information"""
    try:
        document = server.workspace.get_document(params.text_document.uri)
        line = document.lines[params.position.line]
        
        request_params = {
            "line": line,
            "column": params.position.character,
            "lineOffset": document.offset_at_position(lsp.Position(line=params.position.line, character=0)),
            "fullText": document.source
        }
        
        response = await server.dax_bridge.send_command("hover", request_params)
        
        if "error" in response or not response.get("contents"):
            return None
        
        return lsp.Hover(
            contents=lsp.MarkupContent(
                kind=lsp.MarkupKind.Markdown,
                value=response["contents"]
            ),
            range=convert_range(response.get("range")) if response.get("range") else None
        )
        
    except Exception as e:
        logger.error(f"Hover failed: {e}")
        return None

@server.feature(lsp.TEXT_DOCUMENT_DIAGNOSTIC)
async def diagnostic(params: lsp.DocumentDiagnosticParams) -> lsp.DocumentDiagnosticReport:
    """Provide diagnostics (syntax errors, etc.)"""
    try:
        document = server.workspace.get_document(params.text_document.uri)
        
        request_params = {
            "fullText": document.source,
            "uri": params.text_document.uri
        }
        
        response = await server.dax_bridge.send_command("diagnostics", request_params)
        
        if "error" in response:
            logger.error(f"Diagnostics error: {response['error']}")
            return lsp.RelatedFullDocumentDiagnosticReport(items=[])
        
        diagnostics = []
        for diag in response.get("diagnostics", []):
            diagnostics.append(lsp.Diagnostic(
                range=convert_range(diag.get("range")),
                severity=convert_diagnostic_severity(diag.get("severity", 1)),
                message=diag.get("message", ""),
                source=diag.get("source", "dax"),
                code=diag.get("code")
            ))
        
        return lsp.RelatedFullDocumentDiagnosticReport(items=diagnostics)
        
    except Exception as e:
        logger.error(f"Diagnostics failed: {e}")
        return lsp.RelatedFullDocumentDiagnosticReport(items=[])

# Custom command to update the model metadata
@server.command("dax.updateModel")
async def update_model(params: List[Any]) -> None:
    """Update the DAX model metadata"""
    try:
        model_metadata = params[0] if params else {}
        await server.dax_bridge.send_command("setModel", model_metadata)
        logger.info("Model metadata updated successfully")
    except Exception as e:
        logger.error(f"Failed to update model: {e}")

@server.feature(lsp.SHUTDOWN)
async def shutdown(params: Any = None) -> None:
    """Shutdown the language server"""
    logger.info("Shutting down DAX Language Server")
    server.stop_dax_service()

# Helper functions
def convert_completion_kind(kind: int) -> lsp.CompletionItemKind:
    """Convert DaxStudio completion kind to LSP completion kind"""
    mapping = {
        1: lsp.CompletionItemKind.Text,
        2: lsp.CompletionItemKind.Method,
        3: lsp.CompletionItemKind.Function,
        5: lsp.CompletionItemKind.Field,
        6: lsp.CompletionItemKind.Variable,
        7: lsp.CompletionItemKind.Class,
        14: lsp.CompletionItemKind.Keyword,
    }
    return mapping.get(kind, lsp.CompletionItemKind.Text)

def convert_diagnostic_severity(severity: int) -> lsp.DiagnosticSeverity:
    """Convert DaxStudio diagnostic severity to LSP severity"""
    mapping = {
        1: lsp.DiagnosticSeverity.Error,
        2: lsp.DiagnosticSeverity.Warning,
        3: lsp.DiagnosticSeverity.Information,
        4: lsp.DiagnosticSeverity.Hint,
    }
    return mapping.get(severity, lsp.DiagnosticSeverity.Error)

def convert_range(range_data: Dict[str, Any]) -> lsp.Range:
    """Convert DaxStudio range to LSP range"""
    if not range_data:
        return lsp.Range(
            start=lsp.Position(line=0, character=0),
            end=lsp.Position(line=0, character=0)
        )
    
    start = range_data.get("start", {})
    end = range_data.get("end", {})
    
    return lsp.Range(
        start=lsp.Position(
            line=start.get("line", 0),
            character=start.get("character", 0)
        ),
        end=lsp.Position(
            line=end.get("line", 0),
            character=end.get("character", 0)
        )
    )

if __name__ == "__main__":
    server.start_io()