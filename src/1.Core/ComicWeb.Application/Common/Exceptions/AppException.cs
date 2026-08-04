namespace ComicWeb.Application.Common.Exceptions;

public sealed class AppException(string code, int statusCode, string title, string detail) : Exception(detail)
{ public string Code { get; } = code; public int StatusCode { get; } = statusCode; public string Title { get; } = title; }
