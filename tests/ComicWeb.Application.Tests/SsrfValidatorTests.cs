using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ComicWeb.Persistence.Content;
using ComicWeb.Application.Common.Exceptions;

namespace ComicWeb.Application.Tests
{
    public class SsrfValidatorTests
    {
        [Theory]
        [InlineData("http://localhost")]
        [InlineData("https://127.0.0.1")]
        [InlineData("http://[::1]")]
        [InlineData("http://192.168.1.1")]
        [InlineData("https://10.0.0.1")]
        [InlineData("http://172.16.0.1")]
        [InlineData("http://169.254.169.254")]
        public async Task ValidateUrlAsync_ShouldThrowException_ForPrivateOrLoopbackUrls(string url)
        {
            var exception = await Assert.ThrowsAsync<AppException>(() =>
                SsrfValidator.ValidateUrlAsync(url, CancellationToken.None));

            Assert.Equal("SSRF_ATTEMPT", exception.Code);
            Assert.Equal(400, exception.StatusCode);
        }

        [Theory]
        [InlineData("ftp://google.com")]
        [InlineData("file:///etc/passwd")]
        public async Task ValidateUrlAsync_ShouldThrowException_ForUnsupportedSchemes(string url)
        {
            var exception = await Assert.ThrowsAsync<AppException>(() =>
                SsrfValidator.ValidateUrlAsync(url, CancellationToken.None));

            Assert.Equal("DISALLOWED_SCHEME", exception.Code);
            Assert.Equal(400, exception.StatusCode);
        }

        [Theory]
        [InlineData("https://shopee.vn")]
        [InlineData("http://google.com")]
        [InlineData("https://github.com")]
        public async Task ValidateUrlAsync_ShouldPass_ForValidPublicUrls(string url)
        {
            // Should not throw any exception
            await SsrfValidator.ValidateUrlAsync(url, CancellationToken.None);
        }
    }
}
