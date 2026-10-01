using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;

namespace NzbDrone.Core.Notifications.Stash
{
    public interface IStashProxy
    {
        void Scan(StashSettings settings, string path);
        void Identify(StashSettings settings, string path);
        void Clean(StashSettings settings, string path);
        StashStatusData GetStatus(StashSettings settings);
    }

    public class StashProxy : IStashProxy
    {
        public const string ScanMutation = "mutation MetadataScan($input: ScanMetadataInput!) { metadataScan(input: $input) }";
        public const string IdentifyMutation = "mutation MetadataIdentify($input: IdentifyMetadataInput!) { metadataIdentify(input: $input) }";
        public const string CleanMutation = "mutation MetadataClean($input: CleanMetadataInput!) { metadataClean(input: $input) }";
        public const string StatusQuery = "query SystemStatus { systemStatus { status } version { version } }";

        private static readonly string[] IdentifyFields = { "title", "studio", "performers", "tags", "date", "stash_ids" };

        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public StashProxy(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public void Scan(StashSettings settings, string path)
        {
            var input = new
            {
                Paths = new[] { path },
                ScanGenerateCovers = settings.GenerateCovers,
                ScanGeneratePreviews = settings.GeneratePreviews,
                ScanGenerateImagePreviews = settings.GenerateImagePreviews,
                ScanGenerateSprites = settings.GenerateSprites,
                ScanGeneratePhashes = settings.GeneratePhashes,
                ScanGenerateThumbnails = settings.GenerateThumbnails
            };

            Execute<object>(settings, ScanMutation, new { Input = input });
        }

        public void Identify(StashSettings settings, string path)
        {
            var sources = new List<object>();

            if (settings.StashBoxEndpoint.IsNotNullOrWhiteSpace())
            {
                sources.Add(new { Source = new { stash_box_endpoint = settings.StashBoxEndpoint } });
            }

            if (settings.BuiltinAutotag)
            {
                sources.Add(new { Source = new { scraper_id = "builtin_autotag" }, Options = new { SetOrganized = false } });
            }

            if (sources.Empty())
            {
                _logger.Debug("No Stash identify sources configured, skipping identify for {0}", path);
                return;
            }

            var input = new
            {
                Sources = sources,
                Options = new
                {
                    IncludeMalePerformers = settings.IncludeMalePerformers,
                    SetCoverImage = settings.SetCoverImage,
                    SetOrganized = settings.SetOrganized,
                    SkipMultipleMatches = settings.SkipMultipleMatches,
                    SkipMultipleMatchTag = settings.SkipMultipleMatches && settings.SkipMultipleMatchTag > 0 ? settings.SkipMultipleMatchTag.ToString() : null,
                    FieldOptions = IdentifyFields.Select(f => new
                    {
                        Field = f,
                        Strategy = "MERGE",
                        CreateMissing = f is "studio" or "performers" or "tags"
                    }).ToList()
                },
                Paths = new[] { path }
            };

            Execute<object>(settings, IdentifyMutation, new { Input = input });
        }

        public void Clean(StashSettings settings, string path)
        {
            var input = new
            {
                Paths = new[] { path },
                DryRun = false
            };

            Execute<object>(settings, CleanMutation, new { Input = input });
        }

        public StashStatusData GetStatus(StashSettings settings)
        {
            return Execute<StashStatusData>(settings, StatusQuery, null);
        }

        private T Execute<T>(StashSettings settings, string query, object variables)
        {
            var request = BuildRequest(settings);

            request.SetContent(new
            {
                Query = query,
                Variables = variables
            }.ToJson());

            var response = _httpClient.Post(request);
            _logger.Trace("Response: {0}", response.Content);

            var result = Json.Deserialize<StashResponse<T>>(response.Content);

            if (result?.Errors?.Any() == true)
            {
                throw new StashException("Stash returned an error: {0}", string.Join(", ", result.Errors.Select(e => e.Message)));
            }

            if (result == null)
            {
                throw new StashException("Stash returned an empty response");
            }

            return result.Data;
        }

        private HttpRequest BuildRequest(StashSettings settings)
        {
            var scheme = settings.UseSsl ? "https" : "http";
            var url = $"{scheme}://{settings.Address.TrimEnd('/')}/graphql";

            var request = new HttpRequestBuilder(url)
                .Accept(HttpAccept.Json)
                .Post()
                .Build();

            request.Headers.ContentType = "application/json";

            if (settings.ApiKey.IsNotNullOrWhiteSpace())
            {
                request.Headers.Add("ApiKey", settings.ApiKey);
            }

            return request;
        }
    }
}
