using GhumoOdisha.Application.Blog;
using GhumoOdisha.Application.Blog.Dtos;
using GhumoOdisha.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/blog")]
public class BlogController(IBlogService blogService) : ControllerBase
{
    /// <summary>Published travel stories, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BlogPostSummaryDto>>>> GetPosts([FromQuery] int limit = 30, CancellationToken cancellationToken = default)
    {
        var result = await blogService.GetPublishedAsync(limit, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<BlogPostSummaryDto>>.Ok(result));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ApiResponse<BlogPostDetailDto>>> GetPost(string slug, CancellationToken cancellationToken)
    {
        var result = await blogService.GetPublishedBySlugAsync(slug, cancellationToken);
        return Ok(ApiResponse<BlogPostDetailDto>.Ok(result));
    }
}
