namespace AssetCare.Application.Interfaces;

public interface IQrCodeGenerator
{
    byte[] CreatePng(string content);
}
