using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ComicWeb.Application.Common.Exceptions;

namespace ComicWeb.Persistence.Content
{
    public static class SsrfValidator
    {
        public static async Task ValidateUrlAsync(string url, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new AppException("URL_REQUIRED", 400, "Validation failed", "Đường dẫn (URL) là bắt buộc.");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                throw new AppException("INVALID_URL", 400, "Validation failed", "Định dạng đường dẫn (URL) không hợp lệ.");
            }

            if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("DISALLOWED_SCHEME", 400, "Validation failed", "Hệ thống chỉ hỗ trợ giao thức HTTP và HTTPS.");
            }

            var host = uri.DnsSafeHost;

            // Chặn trực tiếp loopback/private host names cơ bản trước khi resolve
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                host.Equals("127.0.0.1") ||
                host.Equals("::1"))
            {
                throw new AppException("SSRF_ATTEMPT", 400, "Security validation failed", "Đường dẫn trỏ đến địa chỉ bị cấm (Loopback).");
            }

            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(host, ct);
            }
            catch (Exception ex)
            {
                throw new AppException("DNS_RESOLUTION_FAILED", 400, "Validation failed", $"Không thể phân giải tên miền: {host}. Lỗi: {ex.Message}");
            }

            foreach (var ip in addresses)
            {
                if (IsPrivateOrInternal(ip))
                {
                    throw new AppException("SSRF_ATTEMPT", 400, "Security validation failed", $"Đường dẫn trỏ đến địa chỉ IP nội bộ hoặc riêng tư bị cấm: {ip}");
                }
            }
        }

        private static bool IsPrivateOrInternal(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip))
            {
                return true;
            }

            var bytes = ip.GetAddressBytes();

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                // IPv4 private/internal address spaces:
                // 10.0.0.0/8
                if (bytes[0] == 10) return true;
                
                // 172.16.0.0/12
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                
                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168) return true;
                
                // 169.254.0.0/16 (Link-local)
                if (bytes[0] == 169 && bytes[1] == 254) return true;
                
                // 100.64.0.0/10 (Carrier-grade NAT)
                if (bytes[0] == 100 && (bytes[1] & 0xC0) == 64) return true;
            }
            else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // IPv6 private/internal address spaces:
                // Link-local (fe80::/10)
                if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80) return true;
                
                // Unique Local (fc00::/7)
                if ((bytes[0] & 0xFE) == 0xFC) return true;
                
                // Unspecified (::)
                if (bytes.All(b => b == 0)) return true;
            }

            return false;
        }
    }
}
