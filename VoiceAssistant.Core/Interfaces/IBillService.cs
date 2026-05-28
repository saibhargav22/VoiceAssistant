namespace VoiceAssistant.Core.Interfaces;

public record ParsedBillItem(string Name, string Unit, decimal Qty, decimal Price, float Confidence);
public record ParsedBill(string Store, DateTime Date, List<ParsedBillItem> Items);

public interface IBillService
{
    Task<ParsedBill> ScanBillAsync(byte[] imageData, CancellationToken ct = default);
    Task SaveBillAsync(ParsedBill bill, string imagePath, CancellationToken ct = default);
}
