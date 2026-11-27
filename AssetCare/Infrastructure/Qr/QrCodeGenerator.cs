using AssetCare.Application.Interfaces;
using QRCoder;

namespace AssetCare.Infrastructure.Qr;

public class QrCodeGenerator : IQrCodeGenerator
{
    public byte[] CreatePng(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(10);
    }
}
