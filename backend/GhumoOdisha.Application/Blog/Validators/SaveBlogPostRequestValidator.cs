using FluentValidation;
using GhumoOdisha.Application.Blog.Dtos;

namespace GhumoOdisha.Application.Blog.Validators;

public class SaveBlogPostRequestValidator : AbstractValidator<SaveBlogPostRequest>
{
    public SaveBlogPostRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug can only contain lowercase letters, numbers and hyphens.");
        RuleFor(x => x.Place).MaximumLength(100);
        RuleFor(x => x.Excerpt).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(100_000);
        RuleFor(x => x.Tags).Must(t => t is null || t.Count <= BlogContent.MaxTags)
            .WithMessage($"Add at most {BlogContent.MaxTags} tags.");
        RuleForEach(x => x.Tags).MaximumLength(BlogContent.MaxTagLength)
            .WithMessage($"Each tag must be {BlogContent.MaxTagLength} characters or fewer.");
    }
}
