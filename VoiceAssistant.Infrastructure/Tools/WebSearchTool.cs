using VoiceAssistant.Core.Interfaces;

namespace VoiceAssistant.Infrastructure.Tools;

public class WebSearchTool : ITool
{
    private readonly IWebSearchService _search;
    private readonly ILLMService _llm;

    public WebSearchTool(IWebSearchService search, ILLMService llm)
    {
        _search = search;
        _llm = llm;
    }

    public string Name => "web_search";
    public string Description => "Search the internet for current information, news, prices, weather, or anything that requires up to date knowledge. Use when user asks about recent events, current affairs, today's news, live scores, or anything that may have changed recently.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var query = input.Trim();
            if (string.IsNullOrWhiteSpace(query))
                return "Please provide a search query.";

            var searchResults = await _search.SearchAsync(query, 3, ct);

            if (string.IsNullOrWhiteSpace(searchResults))
                return "I could not find any results for that query.";

            var systemPrompt =
                "You are a helpful voice assistant. Answer the user's question using ONLY " +
                "the search results provided below. Be concise and conversational. " +
                "Never use markdown, bullet points, or special formatting. " +
                "If the results don't contain the answer, say so clearly.\n\n" +
                "Search results:\n" + searchResults;

            return await _llm.ChatAsync(query, systemPrompt, ct);
        }
        catch (Exception ex)
        {
            return $"Search failed: {ex.GetType().Name} — {ex.Message} — {ex.InnerException?.Message}";
        }
    }
}