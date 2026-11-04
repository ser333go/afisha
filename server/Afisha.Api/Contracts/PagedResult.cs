namespace Afisha.Api.Contracts;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public sealed record PosterUploadedDto(string PosterUrl);
