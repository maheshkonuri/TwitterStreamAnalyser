using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;

namespace TwitterStreamAnalyser
{
    public interface ITwitterStreamProcessor
    {
        Task ProcessTweetsAsync();
    }

    public class TwitterStreamProcessor: ITwitterStreamProcessor
    {
        private readonly HttpClient _httpClient;
        private readonly ITweetStatistic _tweetStatistics;
        private readonly Timer _timer;
        private readonly TimeSpan _logInterval;

        public TwitterStreamProcessor(ITweetStatistic tweetStatistic, TimeSpan logInterval)
        {
            _httpClient = new HttpClient();
            _tweetStatistics = tweetStatistic;
            _logInterval = logInterval;
            _timer = new Timer(LogStatistics, null, logInterval, logInterval);
        }

        public async Task ProcessTweetsAsync()
        {
            var (streamApiUrl, bearerToken) = GetTwitterApiUrlAndBearerToken();
            var additionalFieldsToIncludeInResponsePayload = "tweet.fields=public_metrics,entities";

            var request = new HttpRequestMessage(HttpMethod.Get, streamApiUrl + "?" + additionalFieldsToIncludeInResponsePayload);
            request.Headers.Add("Authorization", "Bearer " + bearerToken);

            try {

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                            Console.WriteLine("Error: Invalid credentials");
                            break;
                        case HttpStatusCode.TooManyRequests:
                            Console.WriteLine("Error: Rate limit exceeded");
                            break;
                        default:
                            Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                            break;
                    }

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var reader = new StreamReader(stream))
                    using (var jsonTextReader = new JsonTextReader(reader))
                    {
                        while (await jsonTextReader.ReadAsync())
                        {
                            if (jsonTextReader.TokenType == JsonToken.StartObject)
                            {
                                var tweet = JObject.Load(jsonTextReader);
                                try
                                {
                                    //Api response for invalid message is diferent from connection exception error
                                    //Sample payloads are available in SampleErrorResponses.txt file.
                                    if (tweet["title"] != null && (tweet["title"].ToString() == "Invalid Request") ||
                                        (tweet["title"].ToString() == "ConnectionException"))
                                    {
                                        Console.WriteLine("Error: " + tweet["detail"].ToString());
                                        break;
                                    }
                                    else
                                    {
                                        _tweetStatistics.ReportStatistic(tweet);
                                    }
                                }
                                catch(Exception ex)
                                {
                                    Console.WriteLine("Error: Processing Tweet \n" + ex.StackTrace);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Twitter Stream Processing Error" + ex.StackTrace);
            }
        }
        private void LogStatistics(object state)
        {
            Console.WriteLine("Total Tweets Received" + _tweetStatistics.GetTotalTweets());
            Console.WriteLine("Top 10 HashTags");
            foreach(var (hashTag,count) in _tweetStatistics.GetTopHashTags())
            {
                Console.WriteLine($"{hashTag}: {count}");
            }
        }
        public (string,string) GetTwitterApiUrlAndBearerToken()
        {
            var builder = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            IConfigurationRoot configuration = builder.Build();
            var apiUrl = configuration["TwitterApiUrl"];
            var bearerToken = configuration["BearerToken"];
            return (apiUrl, bearerToken);
        }

        public int GetTotalTweets()
        {
            return _tweetStatistics.GetTotalTweets();
        }

        public (string, int)[] GetTopHashTags()
        {
            return _tweetStatistics.GetTopHashTags();
        }
    }

    public interface ITweetStatistic
    {
        void ReportStatistic(JObject tweets);
        int GetTotalTweets();
        (string, int)[] GetTopHashTags();

    }

    public class TweetStatistics : ITweetStatistic
    {
        private int _tweetCount;
        private Dictionary<string, int> _hashTagCounts = new Dictionary<string, int>();
        public void ReportStatistic(JObject tweet)
        {
            _tweetCount++;

            if (tweet["entities"] != null && tweet["entities"]["hastags"] != null)
            {
                JArray hashTags = (JArray)tweet["entities"]["hastags"];
                foreach (JObject hashTag in hashTags)
                {
                    string hashTagText = hashTag["Text"].ToString();
                    Console.WriteLine(hashTagText);
                    if (_hashTagCounts.ContainsKey(hashTagText))
                    {
                        _hashTagCounts[hashTagText] = _hashTagCounts[hashTagText] + 1;
                    }
                    else
                    {
                        _hashTagCounts.Add(hashTagText, 1);
                    }
                }
            }
        }

        public int GetTotalTweets()
        {
            return _tweetCount;
        }

        public (string, int)[] GetTopHashTags()
        {
            return _hashTagCounts.OrderByDescending(kvp => kvp.Value).Take(10).Select(kvp => (kvp.Key, kvp.Value)).ToArray();
        }
    }

    public class TwitterStreamAnalyser
    {
        public static async Task Main()
        {
            Console.WriteLine("Hello !! This is twitter analyser");
            TwitterStreamProcessor processor = new TwitterStreamProcessor(new TweetStatistics(), new TimeSpan(0, 0, 2));
            await processor.ProcessTweetsAsync();
        }
    }

}