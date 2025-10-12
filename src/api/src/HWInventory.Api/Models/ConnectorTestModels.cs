namespace HWInventory.Api.Models;

public record ConnectorTestRequestDto(string? Target);

public record ConnectorTestResponse(bool Success, string Message, ConnectorSummaryResponse? Connector);
