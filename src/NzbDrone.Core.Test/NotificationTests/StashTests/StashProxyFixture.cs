using System;
using System.Linq;
using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Notifications.Stash;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests.StashTests
{
    [TestFixture]
    public class StashProxyFixture : CoreTest<StashProxy>
    {
        private const string ScenePath = "/stash/scenes/Studio/2024-01-01 - Scene \"Title\" {abc}";

        private StashSettings _settings;
        private HttpRequest _sentRequest;
        private string _responseContent;

        [SetUp]
        public void Setup()
        {
            _settings = new StashSettings
            {
                Host = "localhost",
                Port = 5555
            };

            _sentRequest = null;
            _responseContent = "{\"data\":{\"metadataScan\":\"1\"}}";

            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Post(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _sentRequest = r)
                  .Returns((HttpRequest r) => new HttpResponse(r, new HttpHeader(), _responseContent));
        }

        private JObject GetBody()
        {
            return JObject.Parse(Encoding.UTF8.GetString(_sentRequest.ContentData));
        }

        [Test]
        public void scan_should_post_mutation_with_variables()
        {
            Subject.Scan(_settings, ScenePath);

            _sentRequest.Url.FullUri.Should().Be("http://localhost:5555/graphql");
            _sentRequest.Method.Should().Be(System.Net.Http.HttpMethod.Post);

            var body = GetBody();

            body["query"].Value<string>().Should().Be(StashProxy.ScanMutation);

            var input = body["variables"]["input"];
            input["paths"].Values<string>().Should().Equal(ScenePath);
            input["scanGenerateCovers"].Value<bool>().Should().BeTrue();
            input["scanGeneratePreviews"].Value<bool>().Should().BeTrue();
            input["scanGenerateImagePreviews"].Value<bool>().Should().BeFalse();
            input["scanGenerateSprites"].Value<bool>().Should().BeTrue();
            input["scanGeneratePhashes"].Value<bool>().Should().BeTrue();
            input["scanGenerateThumbnails"].Value<bool>().Should().BeFalse();
        }

        [Test]
        public void scan_should_honour_generate_settings()
        {
            _settings.GenerateCovers = false;
            _settings.GeneratePreviews = false;
            _settings.GenerateSprites = false;
            _settings.GeneratePhashes = false;
            _settings.GenerateThumbnails = true;

            Subject.Scan(_settings, ScenePath);

            var input = GetBody()["variables"]["input"];
            input["scanGenerateCovers"].Value<bool>().Should().BeFalse();
            input["scanGeneratePreviews"].Value<bool>().Should().BeFalse();
            input["scanGenerateSprites"].Value<bool>().Should().BeFalse();
            input["scanGeneratePhashes"].Value<bool>().Should().BeFalse();
            input["scanGenerateThumbnails"].Value<bool>().Should().BeTrue();
        }

        [Test]
        public void should_use_https_and_url_base()
        {
            _settings.UseSsl = true;
            _settings.UrlBase = "/stash";

            Subject.Scan(_settings, ScenePath);

            _sentRequest.Url.FullUri.Should().Be("https://localhost:5555/stash/graphql");
        }

        [Test]
        public void should_send_api_key_header_when_set()
        {
            _settings.ApiKey = "test-key";

            Subject.Scan(_settings, ScenePath);

            _sentRequest.Headers.GetSingleValue("ApiKey").Should().Be("test-key");
        }

        [Test]
        public void should_not_send_api_key_header_when_not_set()
        {
            Subject.Scan(_settings, ScenePath);

            _sentRequest.Headers.ContainsKey("ApiKey").Should().BeFalse();
        }

        [Test]
        public void clean_should_post_mutation_with_variables()
        {
            _responseContent = "{\"data\":{\"metadataClean\":\"2\"}}";

            Subject.Clean(_settings, ScenePath);

            var body = GetBody();

            body["query"].Value<string>().Should().Be(StashProxy.CleanMutation);
            body["variables"]["input"]["paths"].Values<string>().Should().Equal(ScenePath);
            body["variables"]["input"]["dryRun"].Value<bool>().Should().BeFalse();
        }

        [Test]
        public void identify_should_post_sources_and_options()
        {
            _settings.BuiltinAutotag = true;
            _settings.SkipMultipleMatches = true;
            _settings.SkipMultipleMatchTag = 12;

            Subject.Identify(_settings, ScenePath);

            var body = GetBody();

            body["query"].Value<string>().Should().Be(StashProxy.IdentifyMutation);

            var input = body["variables"]["input"];
            input["paths"].Values<string>().Should().Equal(ScenePath);
            input["sources"][0]["source"]["stash_box_endpoint"].Value<string>().Should().Be("https://stashdb.org/graphql");
            input["sources"][1]["source"]["scraper_id"].Value<string>().Should().Be("builtin_autotag");
            input["options"]["skipMultipleMatches"].Value<bool>().Should().BeTrue();
            input["options"]["skipMultipleMatchTag"].Value<string>().Should().Be("12");
            input["options"]["fieldOptions"].Select(f => f["field"].Value<string>()).Should().Contain("performers");
            input["options"]["fieldOptions"].Select(f => f["strategy"].Value<string>()).Should().OnlyContain(s => s == "MERGE");
        }

        [Test]
        public void identify_should_not_send_skip_tag_when_not_set()
        {
            _settings.SkipMultipleMatches = true;
            _settings.SkipMultipleMatchTag = 0;

            Subject.Identify(_settings, ScenePath);

            GetBody()["variables"]["input"]["options"]["skipMultipleMatchTag"].Should().BeNull();
        }

        [Test]
        public void identify_should_do_nothing_without_sources()
        {
            _settings.StashBoxEndpoint = string.Empty;
            _settings.BuiltinAutotag = false;

            Subject.Identify(_settings, ScenePath);

            _sentRequest.Should().BeNull();
        }

        [Test]
        public void should_throw_when_stash_returns_graphql_errors()
        {
            _responseContent = "{\"errors\":[{\"message\":\"Unknown type ScanMetadataInput\"}],\"data\":null}";

            Assert.Throws<StashException>(() => Subject.Scan(_settings, ScenePath))
                  .Message.Should().Contain("Unknown type ScanMetadataInput");
        }

        [Test]
        public void get_status_should_return_status()
        {
            _responseContent = "{\"data\":{\"systemStatus\":{\"status\":\"OK\"},\"version\":{\"version\":\"v0.28.1\"}}}";

            var status = Subject.GetStatus(_settings);

            status.SystemStatus.Status.Should().Be("OK");
            status.Version.Version.Should().Be("v0.28.1");
            GetBody()["query"].Value<string>().Should().Be(StashProxy.StatusQuery);
        }

        [Test]
        public void should_let_http_errors_through()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Post(It.IsAny<HttpRequest>()))
                  .Returns((HttpRequest r) => throw new HttpException(new HttpResponse(r, new HttpHeader(), Array.Empty<byte>(), HttpStatusCode.Unauthorized)));

            Assert.Throws<HttpException>(() => Subject.GetStatus(_settings));
        }
    }
}
