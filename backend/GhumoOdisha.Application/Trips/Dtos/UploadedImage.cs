namespace GhumoOdisha.Application.Trips.Dtos;

public record UploadedImage(Stream Content, string FileName, string ContentType, long Length);
