using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace ChatAPI.Services
{
    public class CloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(IConfiguration config)
        {
            var account = new Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]
            );
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
        }

        public async Task<string> SubirArchivoAsync(IFormFile file, string tipo)
        {
            await using var stream = file.OpenReadStream();

            if (tipo == "imagen")
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "chatapp/imagenes",
                    UseFilename = false,
                    UniqueFilename = true
                };
                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result.Error != null) throw new Exception(result.Error.Message);
                return result.SecureUrl.ToString();
            }
            else if (tipo == "video")
            {
                var uploadParams = new VideoUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "chatapp/videos",
                    UseFilename = false,
                    UniqueFilename = true
                };
                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result.Error != null) throw new Exception(result.Error.Message);
                return result.SecureUrl.ToString();
            }
            else
            {
                var uploadParams = new RawUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "chatapp/archivos",
                    UseFilename = true,
                    UniqueFilename = true
                };
                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result.Error != null) throw new Exception(result.Error.Message);
                return result.SecureUrl.ToString();
            }
        }
    }
}
