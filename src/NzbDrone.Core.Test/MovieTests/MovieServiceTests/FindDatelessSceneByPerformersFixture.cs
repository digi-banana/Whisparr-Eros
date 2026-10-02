using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Credits;
using NzbDrone.Core.Movies.Performers;
using NzbDrone.Core.Movies.Studios;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests.MovieServiceTests
{
    // Real FreshMen releases and StashDB scenes: the release names performers by an alias ("Gene Allen" is Michal Renok,
    // "Jamie Eliot" is Antony Carter) and StashDB's issue number is one lower than the studio's.
    [TestFixture]
    public class FindDatelessSceneByPerformersFixture : CoreTest<MovieService>
    {
        private const string StudioForeignId = "freshmen";

        private static readonly Performer GeneAllen = new () { Name = "Michal Renok", ForeignId = "626b6adb", Aliases = new List<string> { "Dominic Easton", "Gene Allen", "Nikolaus Viklund", "Tony Falco" } };
        private static readonly Performer JamieEliot = new () { Name = "Antony Carter", ForeignId = "d28542db", Aliases = new List<string> { "Jake Hurley", "Jamie Eliot", "Jamie Elliot", "Jayden Harwey", "Tim Ptacek" } };
        private static readonly Performer AshtonMontana = new () { Name = "Ashton Montana", ForeignId = "5fa72638", Aliases = new List<string>() };
        private static readonly Performer OscarScholz = new () { Name = "Oscar Scholz", ForeignId = "112fd001", Aliases = new List<string>() };
        private static readonly Performer AydenMallory = new () { Name = "Ayden Mallory", ForeignId = "ayden", Aliases = new List<string>() };
        private static readonly Performer CharlieMusk = new () { Name = "Charlie Musk", ForeignId = "charlie", Aliases = new List<string>() };
        private static readonly Performer SteveCollins = new () { Name = "Steve Collins", ForeignId = "steve", Aliases = new List<string>() };
        private static readonly Performer BenHarington = new () { Name = "Ben Harington", ForeignId = "ben", Aliases = new List<string>() };
        private static readonly Performer RamiFerris = new () { Name = "Rami Ferris", ForeignId = "rami", Aliases = new List<string>() };
        private static readonly Performer JoeAngelli = new () { Name = "Joe Angelli", ForeignId = "joe", Aliases = new List<string>() };
        private static readonly Performer TomHouston = new () { Name = "Tom Houston", ForeignId = "tom", Aliases = new List<string>() };

        private List<Movie> _scenes;

        [SetUp]
        public void Setup()
        {
            _scenes = new List<Movie>
            {
                CreateScene(1, "Issue 388, Sex Scene 2: Gene Allen & Ashton Montana", Credit(AshtonMontana), Credit(GeneAllen)),
                CreateScene(2, "Issue 389, Sex Scene 2: Jamie Eliot & Oscar Scholz", Credit(JamieEliot), Credit(OscarScholz)),
                CreateScene(3, "Issue 393, Sex Scene 1: Ayden Mallory & Gene Allen", Credit(AydenMallory), Credit(GeneAllen)),
                CreateScene(4, "Issue 394, Sex Scene 2: Charlie Musk & Jamie Eliot", Credit(CharlieMusk), Credit(JamieEliot)),
                CreateScene(5, "Issue 397, Sex Scene 1: Jamie Eliot & Oscar Scholz part 2", Credit(JamieEliot), Credit(OscarScholz)),
                CreateScene(6, "Issue 410, Sex Scene 2: Steve Collins & Jamie Eliot", Credit(SteveCollins), Credit(JamieEliot)),

                // The scenes the releases were wrongly suggested for: the release named one of their credited names
                CreateScene(7, "Ben & Jamie", Credit(BenHarington), Credit(JamieEliot, "Jamie Eliot")),
                CreateScene(8, "Ben & Gene", Credit(GeneAllen), Credit(BenHarington)),
                CreateScene(9, "Lorenzo & Ben", Credit(BenHarington), Credit(RamiFerris, "Lorenzo Ricci")),

                // Same pair twice in one issue
                CreateScene(10, "Issue 379, Sex Scene 1: Joe Angelli & Tom Houston part 1", Credit(JoeAngelli), Credit(TomHouston)),
                CreateScene(11, "Issue 379, Sex Scene 2: Joe Angelli & Tom Houston part 2", Credit(JoeAngelli), Credit(TomHouston)),

                // Only one of the performers
                CreateScene(12, "Issue 300, Solo: Ashton Montana", Credit(AshtonMontana)),
            };

            Mocker.GetMock<IStudioService>()
                .Setup(s => s.FindAllByTitle(It.Is<string>(t => t == "FreshMen")))
                .Returns(new List<Studio> { new Studio { ForeignId = StudioForeignId } });

            Mocker.GetMock<IMovieRepository>()
                .Setup(s => s.GetByStudioForeignId(StudioForeignId))
                .Returns(() => _scenes.ToList());
        }

        private static Credit Credit(Performer performer, string character = "")
        {
            return new Credit
            {
                PersonName = performer.Name,
                PerformerForeignId = performer.ForeignId,
                Character = character,
                Performer = new CreditPerformer
                {
                    Name = performer.Name,
                    ForeignId = performer.ForeignId,
                    Aliases = performer.Aliases,
                    Gender = Gender.Male
                }
            };
        }

        private static Movie CreateScene(int id, string title, params Credit[] credits)
        {
            var movie = new Movie
            {
                Id = id,
                Title = title,
                ForeignId = $"00000000-0000-0000-0000-0000000000{id:00}"
            };

            movie.MovieMetadata.Value.StudioTitle = "Freshmen";
            movie.MovieMetadata.Value.ReleaseDate = "2024-01-01";
            movie.MovieMetadata.Value.Credits = credits.ToList();

            return movie;
        }

        private SceneMatchResult FindSceneMatch(string title, bool interactive = false)
        {
            var parsedMovieInfo = Parser.Parser.ParseMovieTitle(title);

            parsedMovieInfo.IsDatelessScene.Should().BeTrue();
            parsedMovieInfo.StudioTitle.Should().Be("FreshMen");

            return Subject.FindSceneMatch(parsedMovieInfo, interactive, null);
        }

        [TestCase("FreshMen - Issue 389 - Gene Allen and Ashton Montana.mp4", 1)]
        [TestCase("FreshMen - Issue 390 - Jamie Eliot and Oscar Scholz.mp4", 2)]
        [TestCase("FreshMen - Issue 394 - Ayden Mallory and Gene Allen.mp4", 3)]
        [TestCase("FreshMen – Issue 395 – Freshmen of the Month – Charlie Musk and Jamie Eliot.mp4", 4)]
        [TestCase("FreshMen – Issue 398 – Jamie Eliot and Oscar Scholz.mp4", 5)]
        [TestCase("FreshMen - Issue 411 - Freshmen of the Month - Steve Collins and Jamie Eliot.mp4", 6)]
        public void should_match_release_naming_performers_by_alias_with_issue_one_apart(string title, int id)
        {
            var match = FindSceneMatch(title);

            match.Movie.Should().NotBeNull();
            match.Movie.Id.Should().Be(id);
            match.ReviewCandidates.Should().BeEmpty();
        }

        [TestCase("FreshMen – Jamie Eliot And Ben Harington.mp4", 7)]
        [TestCase("FreshMen – Gene Allen And Ben Harington.mp4", 8)]
        [TestCase("FreshMen – Lorenzo Ricci And Ben Harington.mp4", 9)]
        [TestCase("FreshMen - Issue 389 - Gene Allen & Ashton Montana", 1)]
        [TestCase("FreshMen - Issue 389, Gene Allen, Ashton Montana", 1)]
        [TestCase("FreshMen - Issue 389 - Michal Renok and Ashton Montana", 1)]
        [TestCase("FreshMen - Issue 389 - Ashton Montana & Gene Allen [1080p]", 1)]
        public void should_match_release_naming_exactly_the_scene_performers(string title, int id)
        {
            var match = FindSceneMatch(title);

            match.Movie.Should().NotBeNull();
            match.Movie.Id.Should().Be(id);
        }

        [Test]
        public void should_match_exact_performers_in_interactive_search()
        {
            var match = FindSceneMatch("FreshMen - Issue 389 - Gene Allen and Ashton Montana.mp4", true);

            match.Movie.Should().NotBeNull();
            match.Movie.Id.Should().Be(1);
        }

        // A pair no scene has
        [TestCase("FreshMen - Issue 389 - Lorenzo Ricci and Joe Angelli.mp4")]
        [TestCase("FreshMen - Issue 420 - Gene Allen and Oscar Scholz.mp4")]

        // A performer more than the scene has, before, between or after the scene's performers
        [TestCase("FreshMen - Issue 389 - Gene Allen, Ashton Montana and Oscar Scholz.mp4")]
        [TestCase("FreshMen - Issue 389 - Oscar Scholz, Gene Allen & Ashton Montana.mp4")]
        [TestCase("FreshMen - Issue 389 - Gene Allen, Oscar Scholz & Ashton Montana.mp4")]
        public void should_not_match_release_with_other_performers_automatically(string title)
        {
            var match = FindSceneMatch(title);

            match.Movie.Should().BeNull();
            match.ReviewCandidates.Should().NotContain(c => c.MatchType == MovieParseMatchType.PerformersExact);
        }

        [Test]
        public void should_keep_partial_performer_overlap_for_review()
        {
            // Ashton Montana's solo scene has all its performers in the release, but the release names one more
            _scenes.RemoveAll(s => s.Id == 1);

            var match = FindSceneMatch("FreshMen - Issue 389 - Gene Allen and Ashton Montana.mp4");

            match.Movie.Should().BeNull();
            match.NeedsReview.Should().BeTrue();
            match.ReviewCandidates.Should().ContainSingle();
            match.ReviewCandidates[0].Movie.Id.Should().Be(12);
            match.ReviewCandidates[0].MatchType.Should().Be(MovieParseMatchType.PerformersNotTitle);
        }

        [Test]
        public void should_prefer_exact_performers_over_partial_overlap()
        {
            var match = FindSceneMatch("FreshMen - Issue 389 - Gene Allen and Ashton Montana.mp4");

            match.Movie.Should().NotBeNull();
            match.Movie.Id.Should().Be(1);
        }

        [Test]
        public void should_offer_scenes_with_the_same_performers_for_review()
        {
            var match = FindSceneMatch("FreshMen - Issue 379 - Joe Angelli and Tom Houston.mp4");

            match.Movie.Should().BeNull();
            match.ReviewCandidates.Select(c => c.Movie.Id).Should().BeEquivalentTo(new[] { 10, 11 });
            match.ReviewCandidates.Should().OnlyContain(c => c.MatchType == MovieParseMatchType.PerformersExact);
        }

        [Test]
        public void should_not_match_scenes_with_the_same_performers_in_interactive_search()
        {
            var match = FindSceneMatch("FreshMen - Issue 379 - Joe Angelli and Tom Houston.mp4", true);

            match.Movie.Should().BeNull();
            match.ReviewCandidates.Should().BeEmpty();
        }

        [Test]
        public void should_offer_scenes_with_the_same_performers_for_review_when_issue_is_one_apart_from_both()
        {
            var match = FindSceneMatch("FreshMen - Issue 380 - Joe Angelli and Tom Houston.mp4");

            match.Movie.Should().BeNull();
            match.ReviewCandidates.Select(c => c.Movie.Id).Should().BeEquivalentTo(new[] { 10, 11 });
        }

        [TestCase("FreshMen - Issue 379 Part 2 - Joe Angelli and Tom Houston.mp4", 11)]
        [TestCase("FreshMen - Issue 379 - Joe Angelli and Tom Houston part 1.mp4", 10)]
        [TestCase("FreshMen - Issue 379, Scene 2 - Joe Angelli and Tom Houston.mp4", 11)]
        public void should_use_exact_number_to_pick_between_scenes_with_the_same_performers(string title, int id)
        {
            var match = FindSceneMatch(title);

            match.Movie.Should().NotBeNull();
            match.Movie.Id.Should().Be(id);
        }

        [Test]
        public void should_use_exact_issue_to_pick_between_scenes_with_the_same_performers()
        {
            _scenes.Add(CreateScene(13, "Issue 380, Sex Scene 1: Joe Angelli & Tom Houston", Credit(JoeAngelli), Credit(TomHouston)));
            _scenes.RemoveAll(s => s.Id == 11);

            var match = FindSceneMatch("FreshMen - Issue 380 - Joe Angelli and Tom Houston.mp4");

            match.Movie.Should().NotBeNull();
            match.Movie.Id.Should().Be(13);
        }

        [Test]
        public void should_not_match_when_issue_is_more_than_one_apart()
        {
            // Only Issue 397 has the pair in the library, the release is Issue 390
            _scenes.RemoveAll(s => s.Id == 2);

            var match = FindSceneMatch("FreshMen - Issue 390 - Jamie Eliot and Oscar Scholz.mp4");

            match.Movie.Should().BeNull();
            match.ReviewCandidates.Should().NotContain(c => c.MatchType == MovieParseMatchType.PerformersExact);
        }

        [TestCase("FreshMen - Joe Angelli & Tom Houston Part 1.mp4")]
        [TestCase("FreshMen - Issue 379, Scene 1 - Joe Angelli & Tom Houston.mp4")]
        public void should_not_match_when_part_or_scene_number_differs(string title)
        {
            // Only part 2 / scene 2 is in the library: part 1 is another scene, even though the numbers are one apart
            _scenes.RemoveAll(s => s.Id == 10);

            var match = FindSceneMatch(title);

            match.Movie.Should().BeNull();
            match.ReviewCandidates.Should().ContainSingle(c => c.Movie.Id == 11 && c.MatchType == MovieParseMatchType.PerformersNotTitle);
        }

        [TestCase("FreshMen - Issue 388 - Behind the Scenes.mp4")]
        [TestCase("FreshMen - Issue 388.mp4")]
        public void should_not_match_on_issue_number_alone(string title)
        {
            var match = FindSceneMatch(title);

            match.Movie.Should().BeNull();
            match.ReviewCandidates.Should().BeEmpty();
        }

        [Test]
        public void should_not_match_single_performer_exactly()
        {
            _scenes.RemoveAll(s => s.Id != 12);

            var match = FindSceneMatch("FreshMen - Issue 300 - Ashton Montana.mp4");

            match.Movie.Should().BeNull();
            match.ReviewCandidates.Should().ContainSingle(c => c.Movie.Id == 12 && c.MatchType != MovieParseMatchType.PerformersExact);
        }

        [Test]
        public void should_match_release_naming_canonical_names_when_credits_have_no_aliases()
        {
            var noAliases = new Performer { Name = "Michal Renok", ForeignId = "626b6adb", Aliases = new List<string>() };
            _scenes = new List<Movie> { CreateScene(1, "Issue 388, Sex Scene 2: Gene Allen & Ashton Montana", Credit(AshtonMontana), Credit(noAliases)) };

            FindSceneMatch("FreshMen - Issue 389 - Gene Allen and Ashton Montana.mp4").Movie.Should().BeNull();
            FindSceneMatch("FreshMen - Issue 389 - Michal Renok and Ashton Montana.mp4").Movie.Id.Should().Be(1);
        }

        [Test]
        public void should_load_credits_with_aliases_when_not_loaded()
        {
            var scene = CreateScene(1, "Issue 388, Sex Scene 2: Gene Allen & Ashton Montana");
            scene.MovieMetadata.Value.Id = 42;
            _scenes = new List<Movie> { scene };

            Mocker.GetMock<ICreditService>()
                .Setup(s => s.GetAllCreditsForMovieMetadata(42))
                .Returns(new List<Credit> { Credit(AshtonMontana), Credit(GeneAllen) });

            var match = FindSceneMatch("FreshMen - Issue 389 - Gene Allen and Ashton Montana.mp4");

            match.Movie.Should().NotBeNull();
            match.Movie.Id.Should().Be(1);
        }

        [Test]
        public void should_not_use_aliases_for_dated_release()
        {
            Mocker.GetMock<IMovieRepository>()
                .Setup(s => s.FindByStudioAndDate(StudioForeignId, "2024-05-07"))
                .Returns(new List<Movie>());

            var parsedMovieInfo = Parser.Parser.ParseMovieTitle("FreshMen - 2024-05-07 - Gene Allen and Ashton Montana.mp4");

            parsedMovieInfo.IsDatelessScene.Should().BeFalse();

            // The date isn't one of the studio's, the performers alone aren't enough
            Subject.FindSceneMatch(parsedMovieInfo, false, null).Movie.Should().BeNull();
        }
    }
}
