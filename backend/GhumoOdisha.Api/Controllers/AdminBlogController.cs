using GhumoOdisha.Application.Blog;
using GhumoOdisha.Application.Blog.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/admin/blog")]
[Authorize(Roles = "Admin")]
public class AdminBlogController(IBlogService blogService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminBlogPostListItemDto>>>> GetPosts(CancellationToken cancellationToken)
    {
        var result = await blogService.GetAdminPostsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminBlogPostListItemDto>>.Ok(result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<AdminBlogPostDetailDto>>> GetPost(int id, CancellationToken cancellationToken)
    {
        var result = await blogService.GetAdminPostAsync(id, cancellationToken);
        return Ok(ApiResponse<AdminBlogPostDetailDto>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(SaveBlogPostRequest request, CancellationToken cancellationToken)
    {
        var blogPostId = await blogService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { blogPostId }, "Story created."));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(int id, SaveBlogPostRequest request, CancellationToken cancellationToken)
    {
        await blogService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Story saved."));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken cancellationToken)
    {
        await blogService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Story deleted."));
    }

    [HttpPost("{id:int}/hero-image")]
    public async Task<ActionResult<ApiResponse<object>>> SetHeroImage(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        await blogService.SetHeroImageAsync(id, ToImage(file), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Hero photo updated."));
    }

    [HttpPost("{id:int}/photos")]
    public async Task<ActionResult<ApiResponse<BlogPhotoDto>>> AddPhoto(int id, IFormFile? file, [FromForm] string? caption, CancellationToken cancellationToken)
    {
        var result = await blogService.AddPhotoAsync(id, ToImage(file), caption, cancellationToken);
        return Ok(ApiResponse<BlogPhotoDto>.Ok(result, "Photo added."));
    }

    [HttpDelete("{id:int}/photos/{photoId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePhoto(int id, int photoId, CancellationToken cancellationToken)
    {
        await blogService.DeletePhotoAsync(id, photoId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Photo deleted."));
    }

    private static UploadedImage ToImage(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationAppException(["A photo file is required."]);
        }

        return new UploadedImage(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
    }
}
