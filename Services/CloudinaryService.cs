using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace API_DJCONNECT.Services
{
    public class CloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(IConfiguration config)
        {
            // Las claves estarán en appsettings.json
            var account = new Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]
            );
            _cloudinary = new Cloudinary(account);
        }

        public async Task<ImageUploadResult> UploadImageAsync(IFormFile file)
        {
            var uploadResult = new ImageUploadResult();
            if (file.Length > 0)
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Transformation = new Transformation().Height(800).Width(800).Crop("limit"),

                    // AÑADE ESTA LÍNEA AQUÍ:
                    Folder = "dj_portfolio_images"
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams);
            }
            return uploadResult;
        }

        // Sirve para VIDEO y AUDIO (Cloudinary maneja audio como recurso tipo video/auto)
        public async Task<VideoUploadResult> UploadVideoOrAudioAsync(IFormFile file)
        {
            var uploadResult = new VideoUploadResult();
            if (file.Length > 0)
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new VideoUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "dj_portfolio_media"
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams);
            }
            return uploadResult;
        }

        public async Task<DeletionResult> DeleteFileAsync(string publicId, ResourceType resourceType)
        {
            var deleteParams = new DeletionParams(publicId)
            {
                ResourceType = resourceType
            };
            return await _cloudinary.DestroyAsync(deleteParams);
        }
    }
}