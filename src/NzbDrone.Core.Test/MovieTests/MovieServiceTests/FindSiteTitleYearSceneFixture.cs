using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Credits;
using NzbDrone.Core.Movies.Performers;
using NzbDrone.Core.Movies.Studios;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests.MovieServiceTests
{
    // "[Site.com] Title (Performers) [Year, tags]" releases, as on PornoLab
    [TestFixture]
    public class FindSiteTitleYearSceneFixture : CoreTest<MovieService>
    {
        private const string StudioForeignId = "helix-studios";

        [SetUp]
        public void Setup()
        {
            var scenes = new List<Movie>
            {
                CreateScene(1, "Joy Ride", "2017-06-09", "Blake Mitchell", "Noah White"),
                CreateScene(2, "Joy Ride", "2021-03-12", "Kyle Ross", "Joey Mills"),
                CreateScene(3, "Pretty Boy Pound Down", "2020-05-01", "Trevor Harris", "Austin Lovett")
            };

            Mocker.GetMock<IStudioService>()
                .Setup(s => s.FindAllByTitle(It.IsAny<string>()))
                .Returns(new List<Studio>());

            Mocker.GetMock<IStudioService>()
                .Setup(s => s.FindAllByTitle("HelixStudios"))
                .Returns(new List<Studio> { new Studio { ForeignId = StudioForeignId } });

            Mocker.GetMock<IMovieRepository>()
                .Setup(s => s.GetByStudioForeignId(StudioForeignId))
                .Returns(() => scenes.ToList());
        }

        private static Movie CreateScene(int id, string title, string releaseDate, params string[] performers)
        {
            var movie = new Movie
            {
                Id = id,
                Title = title,
                ForeignId = $"00000000-0000-0000-0000-00000000000{id}"
            };

            movie.MovieMetadata.Value.ReleaseDate = releaseDate;
            movie.MovieMetadata.Value.Credits = performers.Select(p => new Credit { Character = "", Performer = new CreditPerformer { Name = p, Gender = Gender.Male } }).ToList();

            return movie;
        }

        private SceneMatchResult FindSceneMatch(string title, bool interactive = false)
        {
            var parsedMovieInfo = Parser.Parser.ParseMovieTitle(title);

            parsedMovieInfo.IsDatelessScene.Should().BeTrue();

            // From a search: RSS only uses an exact title or StashId automatically, see FindDatelessSceneFixture
            return Subject.FindSceneMatch(parsedMovieInfo, interactive, new MovieSearchCriteria { InteractiveSearch = interactive });
        }

        [TestCase("[HelixStudios.net] Joy Ride / 5003 (Blake Mitchell, Noah White) [2017 ., Blowjob, Anal, Big Dick, 1080p]", 1)]
        [TestCase("[HelixStudios.net] Joy Ride (Kyle Ross, Joey Mills) [2021 г., Twinks, Bareback]", 2)]
        [TestCase("[HelixStudios.com] Pretty Boy Pound Down (Trevor Harris, Austin Lovett) [2020 ., Twinks, Bareback, Oral]", 3)]
        public void should_match_site_title_year_release(string title, int id)
        {
            var match = FindSceneMatch(title);

            match.Movie.Should().NotBeNull();
            match.Movie.Id.Should().Be(id);
        }

        [Test]
        public void should_match_release_under_its_other_brand()
        {
            var match = FindSceneMatch("[8teenboy.com / HelixStudios.net] Pretty Boy Pound Down (Trevor Harris, Austin Lovett) [2020 ., Twinks, Bareback, Oral]");

            match.Movie.Should().NotBeNull();
            match.Movie.Id.Should().Be(3);
        }

        [Test]
        public void should_only_consider_scenes_released_around_the_year()
        {
            // Both "Joy Ride" scenes have the title, only the 2021 one is from around 2021
            var match = FindSceneMatch("[HelixStudios.net] Joy Ride (Someone Else) [2021 ., Twinks]");

            match.Movie.Should().BeNull();
            match.ReviewCandidates.Select(c => c.Movie.Id).Should().Equal(2);
        }

        [Test]
        public void should_not_match_scene_from_another_year()
        {
            var match = FindSceneMatch("[HelixStudios.net] Pretty Boy Pound Down (Trevor Harris, Austin Lovett) [2015 ., Twinks]", true);

            match.Movie.Should().BeNull();
            match.ReviewCandidates.Should().BeEmpty();
        }

        [Test]
        public void should_not_match_unknown_site()
        {
            FindSceneMatch("[SouthernStrokes.com] Joy Ride (Bert Kuna, Ollie Barn) [2023 ., Bareback]", true).Movie.Should().BeNull();
        }
    }
}
