using MediatR;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Features.Media.UploadImage;

public sealed class UploadImageEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/media/images", async (
                IFormFile file,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await using var content = file.OpenReadStream();

                var response = await sender.Send(
                    new UploadImageCommand(content, file.FileName, file.ContentType, file.Length),
                    cancellationToken);

                return Results.Ok(response);
            })
            .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Seller), nameof(UserRole.Admin)))
            .RequireRateLimiting(RateLimitingExtensions.MediaUploadPolicy)
            .DisableAntiforgery()
            .WithName("UploadImage")
            .WithTags("Media")
            .WithSummary("Stores an auction image and returns the URL to attach to an auction.")
            .Produces<UploadImageResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
