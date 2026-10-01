using System.ServiceModel.Syndication;
using System.Text.Json;
using System.Xml;
using Confluent.Kafka;

Console.WriteLine("Starting Global News Kafka Producer...");

var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";
var config = new ProducerConfig { BootstrapServers = bootstrapServers };
using var producer = new ProducerBuilder<Null, string>(config).Build();

var seenUrls = new HashSet<string>();
var rssUrls = new Dictionary<string, string>
{
    { "CNBC Top News", "https://search.cnbc.com/rs/search/combinedcms/view.xml?partnerId=wrss01&id=100003114" },
    { "BBC World", "http://feeds.bbci.co.uk/news/world/rss.xml" },
    { "NYT Technology", "https://rss.nytimes.com/services/xml/rss/nyt/Technology.xml" }
};

while (true)
{
    foreach (var feedInfo in rssUrls)
    {
        try
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Fetching latest news from {feedInfo.Key}...");
            using var reader = XmlReader.Create(feedInfo.Value);
            var feed = SyndicationFeed.Load(reader);

            foreach (var item in feed.Items.OrderBy(i => i.PublishDate))
            {
                var link = item.Links.FirstOrDefault()?.Uri.ToString() ?? "";
                if (string.IsNullOrEmpty(link) || seenUrls.Contains(link)) continue;

                seenUrls.Add(link);
                if (seenUrls.Count > 1000) seenUrls.Clear();

                var summary = item.Summary?.Text ?? "No summary provided.";
                
                var newsEvent = new
                {
                    Source = feedInfo.Key,
                    Title = item.Title.Text,
                    Link = link,
                    PublishDate = item.PublishDate.UtcDateTime,
                    Content = $"Headline: {item.Title.Text}\nSummary: {summary}\nLink: {link}"
                };

                var message = JsonSerializer.Serialize(newsEvent);
                await producer.ProduceAsync("global-news", new Message<Null, string> { Value = message });
                Console.WriteLine($"Produced to Kafka: [{feedInfo.Key}] {newsEvent.Title}");
                
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching {feedInfo.Key}: {ex.Message}");
        }
    }

    Console.WriteLine("Waiting 60 seconds for next poll cycle...");
    await Task.Delay(TimeSpan.FromSeconds(60));
}
