namespace AssetCare.Application.Dtos;

public record SummaryCard(string Title, int Value, string Icon, string Tone);

public record HomeSummary(IReadOnlyList<SummaryCard> Cards);
