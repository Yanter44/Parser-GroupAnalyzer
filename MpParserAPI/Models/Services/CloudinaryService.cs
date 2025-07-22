using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using MpParserAPI.Interfaces;

namespace MpParserAPI.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private Cloudinary _cloudinary;
        private readonly IConfiguration _configuration;
        public CloudinaryService(IConfiguration configuration)
        {
            _configuration = configuration;

            var account = new Account(
                _configuration["CloudinaryService:Cloud"],
                _configuration["CloudinaryService:ApiKey"],
                _configuration["CloudinaryService:ApiSecret"]
            );
            _cloudinary = new Cloudinary(account);
        }
        public async Task<string> UploadImageAsync(byte[] imageBytes)
        {
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription("avatar.jpg", new System.IO.MemoryStream(imageBytes))
            };
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return uploadResult.SecureUrl.ToString(); 
            }
            throw new Exception("Ошибка при загрузке изображения");
        }
    }
}
