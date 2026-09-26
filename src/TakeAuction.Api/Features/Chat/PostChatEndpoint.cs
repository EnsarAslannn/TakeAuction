using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TakeAuction.Api.Common.Api;

namespace TakeAuction.Api.Features.Chat;

public sealed class PostChatEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder) => Map(builder, "/chat", "PostVersionedChat");

    internal static void Map(IEndpointRouteBuilder builder, string pattern, string name)
    {
        builder.MapPost(pattern, Handle)
            .AllowAnonymous()
            .WithName(name)
            .WithTags("Chat")
            .WithSummary("Answers questions about TakeAuction from the built-in bilingual knowledge base.")
            .Produces<ChatResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> Handle(
        ChatRequest? request,
        IChatService service,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Message) ||
            request.Message.Length > 1_000 ||
            request.History is null ||
            request.History.Count > 12 ||
            request.History.Any(item =>
                item.Content is null ||
                item.Content.Length > 2_000 ||
                item.Role is not ("user" or "assistant")) ||
            request.Language is not ("tr" or "en") ||
            request.Context is { Path.Length: > 256 })
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid chat request",
                Detail = "Message, language and recent history must follow the chat contract.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var userId = principal.GetUserId();
        return Results.Ok(await service.ReplyAsync(
            request,
            userId == Guid.Empty ? null : userId,
            cancellationToken));
    }
}
