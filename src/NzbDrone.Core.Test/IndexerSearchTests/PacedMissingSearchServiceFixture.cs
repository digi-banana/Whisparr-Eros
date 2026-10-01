using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    [TestFixture]
    public class PacedMissingSearchServiceFixture : CoreTest<PacedMissingSearchService>
    {
        private List<Movie> _movies;
        private List<Queue.Queue> _queue;
        private List<CommandModel> _commands;

        [SetUp]
        public void Setup()
        {
            _movies = new List<Movie>();
            _queue = new List<Queue.Queue>();
            _commands = new List<CommandModel>();

            GivenConfig(enabled: true, itemsPerRun: 20);

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.MoviesWithoutFiles(It.IsAny<PagingSpec<Movie>>(), It.IsAny<HashSet<int>>()))
                  .Returns<PagingSpec<Movie>, HashSet<int>>((spec, _) =>
                  {
                      spec.Records = _movies;
                      return spec;
                  });

            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(() => _queue);

            Mocker.GetMock<IManageCommandQueue>()
                  .Setup(s => s.All())
                  .Returns(() => _commands);
        }

        private void GivenConfig(bool enabled = true, int itemsPerRun = 20, int interval = 60, bool includeMovies = true, bool includeScenes = true)
        {
            var config = Mocker.GetMock<IConfigService>();

            config.SetupGet(s => s.PacedMissingSearchEnabled).Returns(enabled);
            config.SetupGet(s => s.PacedMissingSearchItemsPerRun).Returns(itemsPerRun);
            config.SetupGet(s => s.PacedMissingSearchInterval).Returns(interval);
            config.SetupGet(s => s.PacedMissingSearchIncludeMovies).Returns(includeMovies);
            config.SetupGet(s => s.PacedMissingSearchIncludeScenes).Returns(includeScenes);
        }

        private Movie GivenMovie(int id, DateTime? lastSearchTime = null, ItemType itemType = ItemType.Scene, bool monitored = true, MovieStatusType status = MovieStatusType.Released, int movieFileId = 0)
        {
            var movie = new Movie
            {
                Id = id,
                Monitored = monitored,
                MovieFileId = movieFileId,
                LastSearchTime = lastSearchTime,
                MovieMetadata = new MovieMetadata
                {
                    Title = $"Item {id}",
                    ItemType = itemType,
                    Status = status
                }
            };

            _movies.Add(movie);

            return movie;
        }

        private List<int> SelectedIds()
        {
            return Subject.GetMoviesToSearch().Select(m => m.Id).ToList();
        }

        [Test]
        public void should_do_nothing_when_disabled()
        {
            GivenConfig(enabled: false);
            GivenMovie(1);

            Subject.Execute(new PacedMissingSearchCommand());

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.MoviesWithoutFiles(It.IsAny<PagingSpec<Movie>>(), It.IsAny<HashSet<int>>()), Times.Never());

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(v => v.Push(It.IsAny<MoviesSearchCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());
        }

        [Test]
        public void should_search_never_searched_first_then_least_recently_searched()
        {
            var now = DateTime.UtcNow;

            GivenMovie(1, now.AddDays(-1));
            GivenMovie(2, now.AddDays(-10));
            GivenMovie(3);
            GivenMovie(4, now.AddHours(-1));
            GivenMovie(5);

            SelectedIds().Should().Equal(3, 5, 2, 1, 4);
        }

        [Test]
        public void should_limit_to_items_per_run()
        {
            GivenConfig(itemsPerRun: 2);

            GivenMovie(1, DateTime.UtcNow.AddDays(-1));
            GivenMovie(2);
            GivenMovie(3, DateTime.UtcNow.AddDays(-5));

            SelectedIds().Should().Equal(2, 3);
        }

        [TestCase(0, 1)]
        [TestCase(-5, 1)]
        [TestCase(10000, PacedMissingSearchService.MaximumItemsPerRun)]
        public void should_clamp_items_per_run(int configured, int expected)
        {
            GivenConfig(itemsPerRun: configured);

            for (var i = 1; i <= PacedMissingSearchService.MaximumItemsPerRun + 5; i++)
            {
                GivenMovie(i);
            }

            SelectedIds().Should().HaveCount(expected);
        }

        [Test]
        public void should_skip_unmonitored_unreleased_queued_and_items_with_files()
        {
            GivenMovie(1, monitored: false);
            GivenMovie(2, status: MovieStatusType.Announced);
            GivenMovie(3, movieFileId: 7);
            var queued = GivenMovie(4);
            GivenMovie(5);

            _queue.Add(new Queue.Queue { Movie = queued });

            SelectedIds().Should().Equal(5);
        }

        [Test]
        public void should_only_include_movies_when_scenes_are_excluded()
        {
            GivenConfig(includeScenes: false);

            GivenMovie(1, itemType: ItemType.Scene);
            GivenMovie(2, itemType: ItemType.Movie);

            SelectedIds().Should().Equal(2);
        }

        [Test]
        public void should_only_include_scenes_when_movies_are_excluded()
        {
            GivenConfig(includeMovies: false);

            GivenMovie(1, itemType: ItemType.Scene);
            GivenMovie(2, itemType: ItemType.Movie);

            SelectedIds().Should().Equal(1);
        }

        [Test]
        public void should_select_nothing_when_movies_and_scenes_are_excluded()
        {
            GivenConfig(includeMovies: false, includeScenes: false);

            GivenMovie(1, itemType: ItemType.Scene);
            GivenMovie(2, itemType: ItemType.Movie);

            SelectedIds().Should().BeEmpty();
        }

        [Test]
        public void should_push_movies_search_command_with_selected_items()
        {
            GivenMovie(1, DateTime.UtcNow.AddDays(-1));
            GivenMovie(2);

            Subject.Execute(new PacedMissingSearchCommand());

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(v => v.Push(It.Is<MoviesSearchCommand>(c => c.MovieIds.SequenceEqual(new[] { 2, 1 })),
                                      CommandPriority.Low,
                                      CommandTrigger.Scheduled),
                          Times.Once());
        }

        [Test]
        public void should_not_push_command_when_nothing_is_missing()
        {
            Subject.Execute(new PacedMissingSearchCommand());

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(v => v.Push(It.IsAny<MoviesSearchCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());
        }

        [TestCase(CommandStatus.Queued)]
        [TestCase(CommandStatus.Started)]
        public void should_skip_run_while_previous_paced_search_is_pending(CommandStatus status)
        {
            GivenMovie(1);

            _commands.Add(new CommandModel
            {
                Body = new MoviesSearchCommand { MovieIds = new List<int> { 1 } },
                Trigger = CommandTrigger.Scheduled,
                Status = status
            });

            Subject.Execute(new PacedMissingSearchCommand());

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(v => v.Push(It.IsAny<MoviesSearchCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());
        }

        [Test]
        public void should_not_be_blocked_by_manual_or_completed_searches()
        {
            GivenMovie(1);

            _commands.Add(new CommandModel
            {
                Body = new MoviesSearchCommand { MovieIds = new List<int> { 9 } },
                Trigger = CommandTrigger.Manual,
                Status = CommandStatus.Started
            });

            _commands.Add(new CommandModel
            {
                Body = new MoviesSearchCommand { MovieIds = new List<int> { 8 } },
                Trigger = CommandTrigger.Scheduled,
                Status = CommandStatus.Completed
            });

            Subject.Execute(new PacedMissingSearchCommand());

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(v => v.Push(It.IsAny<MoviesSearchCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Once());
        }

        [TestCase(false, 60, 0)]
        [TestCase(true, 60, 60)]
        [TestCase(true, 5, 15)]
        [TestCase(true, 0, 15)]
        [TestCase(true, 240, 240)]
        public void should_get_interval(bool enabled, int configured, int expected)
        {
            GivenConfig(enabled: enabled, interval: configured);

            PacedMissingSearchService.GetInterval(Mocker.GetMock<IConfigService>().Object).Should().Be(expected);
        }
    }
}
