using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.IndexerSearch
{
    public interface IPacedMissingSearchService
    {
        List<Movie> GetMoviesToSearch();
    }

    public class PacedMissingSearchService : IPacedMissingSearchService, IExecute<PacedMissingSearchCommand>
    {
        public const int MinimumInterval = 15;
        public const int MaximumItemsPerRun = 500;

        private readonly IConfigService _configService;
        private readonly IMovieService _movieService;
        private readonly IQueueService _queueService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public PacedMissingSearchService(IConfigService configService,
                                         IMovieService movieService,
                                         IQueueService queueService,
                                         IManageCommandQueue commandQueueManager,
                                         Logger logger)
        {
            _configService = configService;
            _movieService = movieService;
            _queueService = queueService;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public static int GetInterval(IConfigService configService)
        {
            if (!configService.PacedMissingSearchEnabled)
            {
                return 0;
            }

            return Math.Max(MinimumInterval, configService.PacedMissingSearchInterval);
        }

        public void Execute(PacedMissingSearchCommand message)
        {
            if (!_configService.PacedMissingSearchEnabled)
            {
                _logger.Debug("Paced missing search is disabled");
                return;
            }

            if (PreviousSearchPending())
            {
                _logger.ProgressInfo("Previous paced missing search is still running, skipping this run");
                return;
            }

            var movies = GetMoviesToSearch();

            if (movies.Empty())
            {
                _logger.ProgressInfo("No missing items to search");
                return;
            }

            _logger.ProgressInfo("Queueing search for {0} missing items", movies.Count);

            _commandQueueManager.Push(new MoviesSearchCommand { MovieIds = movies.Select(m => m.Id).ToList() },
                                      CommandPriority.Low,
                                      CommandTrigger.Scheduled);
        }

        public List<Movie> GetMoviesToSearch()
        {
            var includeMovies = _configService.PacedMissingSearchIncludeMovies;
            var includeScenes = _configService.PacedMissingSearchIncludeScenes;

            if (!includeMovies && !includeScenes)
            {
                return new List<Movie>();
            }

            var count = Math.Clamp(_configService.PacedMissingSearchItemsPerRun, 1, MaximumItemsPerRun);

            var pagingSpec = new PagingSpec<Movie>
            {
                Page = 1,
                PageSize = 100000,
                SortDirection = SortDirection.Ascending,
                SortKey = "Id"
            };

            pagingSpec.FilterExpressions.Add(v => v.Monitored == true);

            var movies = _movieService.MoviesWithoutFiles(pagingSpec).Records;

            var queued = _queueService.GetQueue()
                                      .Where(q => q.Movie != null)
                                      .Select(q => q.Movie.Id)
                                      .ToHashSet();

            return movies.Where(m => m.Monitored && m.MovieFileId == 0 && m.IsAvailable())
                         .Where(m => m.MovieMetadata.Value.ItemType == ItemType.Scene ? includeScenes : includeMovies)
                         .Where(m => !queued.Contains(m.Id))
                         .OrderBy(m => m.LastSearchTime ?? DateTime.MinValue)
                         .ThenBy(m => m.Id)
                         .Take(count)
                         .ToList();
        }

        private bool PreviousSearchPending()
        {
            return _commandQueueManager.All()
                                       .Any(c => c.Body is MoviesSearchCommand &&
                                                 c.Trigger == CommandTrigger.Scheduled &&
                                                 (c.Status == CommandStatus.Queued || c.Status == CommandStatus.Started));
        }
    }
}
