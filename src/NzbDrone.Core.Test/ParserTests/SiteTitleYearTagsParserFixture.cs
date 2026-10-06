using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    // Release names as PornoLab lists them: "[Site.com] Title (Performers) [Year г., tags]", Cyrillic often stripped by the indexer
    [TestFixture]
    public class SiteTitleYearTagsParserFixture : CoreTest
    {
        [TestCase("[HelixStudios.net] Joy Ride / 5003 (Blake Mitchell, Noah White) [2017 ., Blowjob, Anal, Big Dick, Rimming, Fingering, 1080p]", "HelixStudios", "Joy Ride / 5003 (Blake Mitchell, Noah White)", 2017)]
        [TestCase("[HelixStudios.net] Joy Ride / 5003 (Blake Mitchell, Noah White) [2017 г., Blowjob, Anal, 1080p]", "HelixStudios", "Joy Ride / 5003 (Blake Mitchell, Noah White)", 2017)]
        [TestCase("[HelixStudios.com] In Awe of Shaw (Derek Shaw, Vincent Revero) [2025 ., Oral/Anal Sex, Bareback, Big Dick, Muscles]", "HelixStudios", "In Awe of Shaw (Derek Shaw, Vincent Revero)", 2025)]
        [TestCase("[8teenboy.com / HelixStudios.net] Pretty Boy Pound Down (Trevor Harris, Austin Lovett) [2020 ., Twinks, Bareback, Oral]", "8teenboy", "Pretty Boy Pound Down (Trevor Harris, Austin Lovett)", 2020)]
        [TestCase("[8teenboy.com / HelixStudios.com] Ashton Michaels and Matthew Summers /[2010 ., Bedroom, Blonde, Piercing, Smooth]", "8teenboy", "Ashton Michaels and Matthew Summers", 2010)]
        [TestCase("[SouthernStrokes.com] Joy Ride (Bert Kuna, Ollie Barn) [2023 ., Bareback, Oral/Anal Sex, Big Dick]", "SouthernStrokes", "Joy Ride (Bert Kuna, Ollie Barn)", 2023)]
        [TestCase("[onlyfans.com] Tina Yoshi, Melody Marks, Titus Low - Threesome [2026, Amateur, All Sex]", "onlyfans", "Tina Yoshi, Melody Marks, Titus Low - Threesome", 2026)]
        [TestCase("[MommysBoy.net / AdultTime.com]RayVeness ( Sketchy Behavior) [2022 . GonzoHardcore, MILF]", "MommysBoy", "RayVeness ( Sketchy Behavior)", 2022)]
        [TestCase("[Site.com] Some Title /  (Performer One, Performer Two) [2019 ., Tag]", "Site", "Some Title (Performer One, Performer Two)", 2019)]
        public void should_parse_site_title_year_tags_release(string title, string studio, string releaseTokens, int year)
        {
            var result = Parser.Parser.ParseMovieTitle(title);

            result.Should().NotBeNull();
            result.IsScene.Should().BeFalse();
            result.IsDatelessScene.Should().BeTrue();
            result.StudioTitle.Should().Be(studio);
            result.ReleaseTokens.Should().Be(releaseTokens);
            result.Year.Should().Be(year);
            result.ReleaseDate.Should().BeNullOrEmpty();
        }

        [TestCase("[8teenboy.com / HelixStudios.net] Pretty Boy Pound Down (Trevor Harris, Austin Lovett) [2020 ., Twinks]", new[] { "HelixStudios" })]
        [TestCase("[DirtyFuckers.staxus.com / Staxus.com] Prison Bitch (Kevin Ateah, Max Lorenzo) [2021 ., Bareback]", new[] { "DirtyFuckers", "staxus" })]
        [TestCase("[HelixStudios.net] Joy Ride (Blake Mitchell, Noah White) [2017 ., Anal]", new string[0])]
        public void should_keep_the_other_brands_of_a_release(string title, string[] alternativeStudioTitles)
        {
            Parser.Parser.ParseMovieTitle(title).AlternativeStudioTitles.Should().Equal(alternativeStudioTitles);
        }

        // Compilations (a range of years, a numbered pack) and plain movie names are not scenes of this format
        [TestCase("[HelixStudios.net] Helix Studios: Performer Tyler Moore -   [25] [2024-2025 ., Twinks, Bareback, Oral]")]
        [TestCase("[Studio] Movie Title [2024]")]
        [TestCase("Spanking Curiosity 3 /   3 (Keith Miller, Spank This / Helix Studios) [2009 ., Twinks, Spanking]")]
        public void should_not_parse_as_site_title_year_tags_release(string title)
        {
            var result = Parser.Parser.ParseMovieTitle(title);

            if (result != null)
            {
                (result.IsDatelessScene && result.Year > 0).Should().BeFalse();
            }
        }

        [TestCase("[Studio] Movie Title [2024]", 2024)]
        public void should_still_parse_studio_title_year_movie(string title, int year)
        {
            var result = Parser.Parser.ParseMovieTitle(title);

            result.IsScene.Should().BeFalse();
            result.Year.Should().Be(year);
        }

        // A date wins over the year in the tags
        [TestCase("[JimSlip.com / AdultPrime.com] Kiki Helix - Kiki Helix - Part 2 (05.06.2026) [2026, All Sex, 1080p]", "JimSlip", "2026-06-05")]
        [TestCase("[Vixen.com] Nicole Doshi (Sketchy) [2022-02-18, All Sex, Asian, Brunette]", "Vixen", "2022-02-18")]
        public void should_prefer_the_date_of_a_dated_release(string title, string studio, string releaseDate)
        {
            var result = Parser.Parser.ParseMovieTitle(title);

            result.IsDatelessScene.Should().BeFalse();
            result.StudioTitle.Should().Be(studio);
            result.ReleaseDate.Should().Be(releaseDate);
        }
    }
}
