namespace MpParserAPI.Interfaces
{
    public interface ICloudinaryService
    {
        Task<string> UploadImageAsync(byte[] imageBytes);
    }
}
