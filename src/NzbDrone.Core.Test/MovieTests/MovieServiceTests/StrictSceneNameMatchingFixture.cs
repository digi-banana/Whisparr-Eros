using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Credits;
using NzbDrone.Core.Movies.Performers;
using NzbDrone.Core.Movies.Studios;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests.MovieServiceTests
{
    [TestFixture]
    public class StrictSceneNameMatchingFixture : CoreTest<MovieService>
    {
        private const string StudioForeignId = "helix-studios";

        // Grabbed for "Peer Pressure", whose StashDB title isn't in the release name
        private const string ExtraCredit = "HelixStudios - Helix Academy Extra Credit - Alex Killborn & Tyler Hill - 1080p.mp4";

        private List<Movie> _scenes;

        [SetUp]
        public void Setup()
        {
            _scenes = new List<Movie>
            {
                CreateScene(1, "Alex", "Alex"),
                CreateScene(2, "Peer Pressure", "Alex Killborn", "Tyler Hill"),
                CreateScene(3, "Shower Sex", "Joey Mills", "Landon Vega"),
                CreateScene(4, "Poolside", "Dakota Lovell"),
                CreateScene(5, "Roommates", "Kyle Ross"),
                CreateScene(6, "Kyle Ross Returns", "Kyle Ross")
            };

            Mocker.GetMock<IStudioService>()
                .Setup(s => s.FindAllByTitle(It.Is<string>(t => t == "HelixStudios" || t == "Helix Studios")))
                .Returns(new List<Studio> { new Studio { ForeignId = StudioForeignId } });

            Mocker.GetMock<IMovieRepository>()
                .Setup(s => s.GetByStudioForeignId(StudioForeignId))
                .Returns(() => _scenes.ToList());

            GivenStrictNameMatching(true);
        }

        private void GivenStrictNameMatching(bool strict)
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.StrictSceneNameMatching)
                .Returns(strict);
        }

        private static Movie CreateScene(int id, string title, params string[] performers)
        {
            var movie = new Movie
            {
                Id = id,
                Title = title,
                ForeignId = $"00000000-0000-0000-0000-00000000000{id}"
            };

            movie.MovieMetadata.Value.ReleaseDate = "2020-01-01";
            movie.MovieMetadata.Value.Credits = performers.Select(p => new Credit { Character = "", Performer = new CreditPerformer { Name = p, Gender = Gender.Male } }).ToList();

            return movie;
        }

        private SceneMatchResult FindSceneMatch(string title, bool interactive)
        {
            var parsedMovieInfo = Parser.Parser.ParseMovieTitle(title);

            parsedMovieInfo.IsDatelessScene.Should().BeTrue();

            return Subject.FindSceneMatch(parsedMovieInfo, interactive, null);
        }

        [Test]
        public void should_match_one_word_title_inside_a_longer_name_when_strict_matching_is_off()
        {
            GivenStrictNameMatching(false);

            var match = FindSceneMatch(ExtraCredit, false);

            match.Movie.Should().NotBeNull();
            match.Movie.Id.Should().Be(1);
        }

        [Test]
        public void should_send_one_word_title_inside_a_longer_name_to_review()
        {
            var match = FindSceneMatch(ExtraCredit, false);

            match.Movie.Should().BeNull();
            match.ReviewCandidates.Should().ContainSingle();
            match.ReviewCandidates[0].Movie.Id.Should().Be(1);
            match.ReviewCandidates[0].MatchType.Should().Be(MovieParseMatchType.PerformerTitleUnconfirmed);
        }

        [Test]
        public void should_not_count_title_that_is_only_part_of_a_word()
        {
            _scenes = new List<Movie> { CreateScene(1, "Alex", "Kyle Ross") };

            FindSceneMatch("HelixStudios - Alexander Gets Lucky [720p]", false).NeedsReview.Should().BeFalse();
            FindSceneMatch("HelixStudios - Alexander Gets Lucky [720p]", false).Movie.Should().BeNull();
            FindSceneMatch("HelixStudios - Alexander Gets Lucky [720p]", true).Movie.Should().BeNull();
        }

        [Test]
        public void should_keep_the_performer_when_title_is_only_part_of_a_word()
        {
            _scenes = new List<Movie> { CreateScene(1, "Alex", "Gato") };

            var match = FindSceneMatch("HelixStudios - Alexander & Gato [720p]", false);

            match.Movie.Should().BeNull();
            match.ReviewCandidates.Should().ContainSingle();
            match.ReviewCandidates[0].MatchType.Should().Be(MovieParseMatchType.PerformersNotTitle);
        }

        [TestCase("HelixStudios - Alexander & Gato [720p]", "Gato")]
        [TestCase("HelixStudios - Alexander Gets Lucky [720p]", "Kyle Ross")]
        public void should_match_title_part_of_a_word_when_strict_matching_is_off(string title, string performer)
        {
            GivenStrictNameMatching(false);
            _scenes = new List<Movie> { CreateScene(1, "Alex", performer) };

            FindSceneMatch(title, true).Movie.Id.Should().Be(1);
        }

        [TestCase("Helix Studios - Shower Sex - Joey Mills & Landon Vega [720p].mp4", 3)]
        [TestCase("Helix Studios - Poolside - Dakota Lovell [720p]", 4)]
        [TestCase("Helix Studios - Roommates - Kyle Ross (1080p)", 5)]
        [TestCase("Helix Studios - Dakota Lovell - Poolside [720p]", 4)]
        public void should_still_match_title_and_performer_named_apart(string title, int id)
        {
            var match = FindSceneMatch(title, false);

            match.Movie.Should().NotBeNull();
            match.Movie.Id.Should().Be(id);
        }

        // A one-word title next to other words is only part of the release's title
        [TestCase("Helix Studios - Hot Roommates - Kyle Ross [720p]", 5)]

        // The performer is the same words as the title
        [TestCase("Helix Studios - Kyle Ross Returns [720p]", 6)]
        public void should_send_title_and_performer_that_are_not_apart_to_review(string title, int id)
        {
            var match = FindSceneMatch(title, false);

            match.Movie.Should().BeNull();
            match.ReviewCandidates.Should().ContainSingle();
            match.ReviewCandidates[0].Movie.Id.Should().Be(id);
            match.ReviewCandidates[0].MatchType.Should().Be(MovieParseMatchType.PerformerTitleUnconfirmed);

            FindSceneMatch(title, true).Movie.Id.Should().Be(id);
        }

        [TestCase("Helix Studios - Hot Roommates - Kyle Ross [720p]", 5)]
        [TestCase("Helix Studios - Kyle Ross Returns [720p]", 6)]
        public void should_match_title_and_performer_that_are_not_apart_when_strict_matching_is_off(string title, int id)
        {
            GivenStrictNameMatching(false);

            FindSceneMatch(title, false).Movie.Id.Should().Be(id);
        }

        [Test]
        public void should_not_change_dated_release_matching()
        {
            Mocker.GetMock<IMovieRepository>()
                .Setup(s => s.FindByStudioAndDate(StudioForeignId, "2020-01-01"))
                .Returns(new List<Movie> { _scenes[0], _scenes[1] });

            var parsedMovieInfo = Parser.Parser.ParseMovieTitle("Helix Studios - 2020-01-01 - Alex Killborn & Tyler Hill [720p]");

            parsedMovieInfo.IsDatelessScene.Should().BeFalse();

            var strict = Subject.FindScene(parsedMovieInfo, false, null);

            GivenStrictNameMatching(false);

            Subject.FindScene(parsedMovieInfo, false, null).Should().Be(strict);
        }
    }
}
