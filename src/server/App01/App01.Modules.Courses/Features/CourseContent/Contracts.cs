using MediatR;


namespace App01.Modules.Courses.Features.CourseContent;


public class Contracts
{
    public record Request(string Slug) : IRequest<Response>;

    // Treść opublikowanego kursu (Markdown bez frontmattera) i bazowy URL mediów kursu
    public record Response(
        string Slug,
        string Title,
        IReadOnlyList<string> Tags,
        string Content,
        string MediaBaseUrl
    );
}