using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using TwitterStreamAnalyser;

namespace TwitterStreamAnalyser.Test
{
    [TestClass]
    public class TwitterStreamProcessorTests
    {
        [TestMethod]
        public async Task ProcessTweetsAsync_Test()
        {
            //Arrange
            var processor = new TwitterStreamProcessor(new TweetStatistics(), new TimeSpan(0, 0, 2));

            //Act
            await processor.ProcessTweetsAsync();
            var totalTweets = processor.GetTotalTweets();
            var topHashTags = processor.GetTopHashTags();

            //Assert
            Assert.IsTrue(totalTweets > 0);
            Assert.IsTrue(topHashTags.Length > 0);
        }
    }
}