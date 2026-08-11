using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.WebApi.Controllers;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ComicWeb.Application.Tests;

public class UploadControllerTests
{
    private class FakeImageStorageService : IImageStorageService
    {
        public Task<string> UploadImageAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("http://localhost:8080/uploads/success.png");
        }

        public Task DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private readonly FakeImageStorageService _fakeStorage;
    private readonly UploadController _controller;

    public UploadControllerTests()
    {
        _fakeStorage = new FakeImageStorageService();
        _controller = new UploadController(_fakeStorage);
        
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task UploadImage_ShouldThrowException_WhenFileIsNull()
    {
        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _controller.UploadImage(null!, CancellationToken.None));

        Assert.Equal("FILE_REQUIRED", exception.Code);
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task UploadImage_ShouldThrowException_WhenFileTooLarge()
    {
        var dummyBytes = new byte[3 * 1024 * 1024]; // 3MB
        var stream = new MemoryStream(dummyBytes);
        var file = new FormFile(stream, 0, stream.Length, "file", "test.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _controller.UploadImage(file, CancellationToken.None));

        Assert.Equal("FILE_TOO_LARGE", exception.Code);
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task UploadImage_ShouldThrowException_WhenInvalidMimeType()
    {
        var dummyBytes = new byte[100];
        var stream = new MemoryStream(dummyBytes);
        var file = new FormFile(stream, 0, stream.Length, "file", "test.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _controller.UploadImage(file, CancellationToken.None));

        Assert.Equal("INVALID_FILE_TYPE", exception.Code);
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task UploadImage_ShouldThrowException_WhenInvalidSignature()
    {
        var dummyBytes = Encoding.UTF8.GetBytes("Not A PNG File");
        var stream = new MemoryStream(dummyBytes);
        var file = new FormFile(stream, 0, stream.Length, "file", "test.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _controller.UploadImage(file, CancellationToken.None));

        Assert.Equal("INVALID_FILE_SIGNATURE", exception.Code);
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task UploadImage_ShouldSucceed_WhenValidPng()
    {
        // Valid PNG signature: 89 50 4E 47
        var validPngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var stream = new MemoryStream(validPngBytes);
        var file = new FormFile(stream, 0, stream.Length, "file", "test.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var response = await _controller.UploadImage(file, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var envelope = Assert.IsType<ApiEnvelope<string>>(okResult.Value);

        Assert.Equal("http://localhost:8080/uploads/success.png", envelope.Data);
    }
}
