using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace ServicesLayer;

public sealed record WebPageExtractionResult(string Title, string Text, string SourceUrl, bool UsedBrowserRenderer);

public interface IWebPageTextExtractor
{
    Task<WebPageExtractionResult> ExtractAsync(string url, CancellationToken cancellationToken = default);
}

public sealed class WebPageTextExtractor : IWebPageTextExtractor
{
    private static readonly Regex ScriptAndStyleRegex = new(
        @"<(script|style|noscript|svg|canvas|iframe)\b[^>]*>.*?</\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s{2,}", RegexOptions.Compiled);
    private static readonly Regex TitleRegex = new(
        @"<title\b[^>]*>(?<title>.*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly HttpClient _httpClient;

    public WebPageTextExtractor(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WebPageExtractionResult> ExtractAsync(string url, CancellationToken cancellationToken = default)
    {
        var uri = ValidateUrl(url);

        try
        {
            var rendered = await ExtractWithPlaywrightAsync(uri, cancellationToken);
            if (!string.IsNullOrWhiteSpace(rendered.Text))
            {
                return rendered;
            }
        }
        catch (PlaywrightException)
        {
            // Fall back to static HTML extraction when browser binaries are not installed.
        }
        catch (TimeoutException)
        {
            // Fall back to static HTML extraction for slow pages.
        }

        var fallback = await ExtractStaticHtmlAsync(uri, cancellationToken);
        if (string.IsNullOrWhiteSpace(fallback.Text))
        {
            throw new InvalidOperationException("No readable text could be extracted from this web page.");
        }

        return fallback;
    }

    private static Uri ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Enter a valid http or https URL.");
        }

        return uri;
    }

    private static async Task<WebPageExtractionResult> ExtractWithPlaywrightAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        await page.GotoAsync(uri.ToString(), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 30_000
        });

        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
            {
                Timeout = 8_000
            });
        }
        catch (TimeoutException)
        {
            // Some SPAs keep background requests open; DOMContentLoaded text is still useful.
        }

        cancellationToken.ThrowIfCancellationRequested();
        var title = await page.TitleAsync();
        var text = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions
        {
            Timeout = 8_000
        });

        return new WebPageExtractionResult(
            string.IsNullOrWhiteSpace(title) ? uri.Host : title.Trim(),
            NormalizeWhitespace(text),
            uri.ToString(),
            UsedBrowserRenderer: true);
    }

    private async Task<WebPageExtractionResult> ExtractStaticHtmlAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("CourseAssistant/1.0");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var title = ExtractTitle(html) ?? uri.Host;
        var text = ExtractTextFromHtml(html);

        return new WebPageExtractionResult(title, text, uri.ToString(), UsedBrowserRenderer: false);
    }

    private static string? ExtractTitle(string html)
    {
        var match = TitleRegex.Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["title"].Value).Trim() : null;
    }

    private static string ExtractTextFromHtml(string html)
    {
        var withoutScripts = ScriptAndStyleRegex.Replace(html, " ");
        var withBreaks = withoutScripts
            .Replace("</p>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</div>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase);

        var withoutTags = TagRegex.Replace(withBreaks, " ");
        return NormalizeWhitespace(WebUtility.HtmlDecode(withoutTags));
    }

    private static string NormalizeWhitespace(string text)
    {
        var builder = new StringBuilder();
        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var compact = WhitespaceRegex.Replace(line, " ").Trim();
            if (!string.IsNullOrWhiteSpace(compact))
            {
                builder.AppendLine(compact);
            }
        }

        return builder.ToString().Trim();
    }
}
