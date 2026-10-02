using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Credits;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests
{
    [TestFixture]
    public class DatelessSceneEvidenceFixture : CoreTest
    {
        private static Credit Credit(string name, string character = null, params string[] aliases)
        {
            return new Credit
            {
                PersonName = name,
                Character = character,
                Performer = new CreditPerformer { Name = name, ForeignId = name.ToLowerInvariant(), Aliases = aliases.ToList() }
            };
        }

        private static Movie Scene(string title, string studio, params Credit[] credits)
        {
            var movie = new Movie { Title = title };

            movie.MovieMetadata.Value.StudioTitle = studio;
            movie.MovieMetadata.Value.Credits = credits.ToList();

            return movie;
        }

        private static Movie GeneAndAshton()
        {
            return Scene("Issue 388, Sex Scene 2: Gene Allen & Ashton Montana",
                         "Freshmen",
                         Credit("Ashton Montana"),
                         Credit("Michal Renok", null, "Dominic Easton", "Gene Allen", "Tony Falco"));
        }

        [TestCase("Issue 389 - Gene Allen and Ashton Montana")]
        [TestCase("Issue 389 - Ashton Montana & Gene Allen")]
        [TestCase("Issue 389 - Gene Allen + Ashton Montana")]
        [TestCase("Issue 389 - Gene Allen x Ashton Montana")]
        [TestCase("Issue 389, Gene Allen, Ashton Montana")]
        [TestCase("Issue 389 - Tony Falco and Ashton Montana")]
        [TestCase("Issue 389 - Michal Renok and Ashton Montana")]
        [TestCase("issue.389.gene.allen.and.ashton.montana")]
        [TestCase("Gene Allen and Ashton Montana")]
        [TestCase("Freshmen of the Month - Gene Allen and Ashton Montana")]
        [TestCase("Gene Allen and Ashton Montana - part 2")]
        [TestCase("Gene Allen & Ashton Montana, Part 2")]
        public void should_find_exact_performers(string releaseTokens)
        {
            DatelessSceneEvidence.HasExactPerformers(releaseTokens, GeneAndAshton()).Should().BeTrue();
        }

        // Missing performer
        [TestCase("Issue 389 - Ashton Montana")]
        [TestCase("Issue 389 - Gene and Ashton Montana")]

        // Another performer in the list
        [TestCase("Issue 389 - Gene Allen, Ashton Montana & Oscar Scholz")]
        [TestCase("Issue 389 - Oscar Scholz, Gene Allen & Ashton Montana")]
        [TestCase("Issue 389 - Gene Allen, Oscar Scholz and Ashton Montana")]
        [TestCase("Issue 389 - Gene Allen fucks Ashton Montana")]

        // Nothing to match
        [TestCase("")]
        public void should_not_find_exact_performers(string releaseTokens)
        {
            DatelessSceneEvidence.HasExactPerformers(releaseTokens, GeneAndAshton()).Should().BeFalse();
        }

        [Test]
        public void should_find_exact_performers_by_credited_name()
        {
            var scene = Scene("Ben & Jamie", "Freshmen", Credit("Ben Harington"), Credit("Antony Carter", "Jamie Eliot"));

            DatelessSceneEvidence.HasExactPerformers("Jamie Eliot And Ben Harington", scene).Should().BeTrue();
        }

        [Test]
        public void should_find_exact_performers_with_accents_and_apostrophes()
        {
            var scene = Scene("Issue 242, Sex Scene 2: Allan Aimée & Eluan Jeunet", "Freshmen", Credit("Eluan Jeunet"), Credit("Allan Aimée"));

            DatelessSceneEvidence.HasExactPerformers("Allan Aimee & Eluan Jeunet", scene).Should().BeTrue();

            var obrien = Scene("Ethan & Kyle", "BelAmi", Credit("Ethan O'Pry"), Credit("Kyle Brady"));

            DatelessSceneEvidence.HasExactPerformers("Ethan O’Pry & Kyle Brady", obrien).Should().BeTrue();
        }

        [Test]
        public void should_not_find_exact_performers_for_a_single_performer()
        {
            var scene = Scene("Issue 300, Solo: Ashton Montana", "Freshmen", Credit("Ashton Montana"));

            DatelessSceneEvidence.HasExactPerformers("Ashton Montana", scene).Should().BeFalse();
        }

        [Test]
        public void should_not_find_exact_performers_without_credits()
        {
            var scene = Scene("Issue 300", "Freshmen");
            scene.MovieMetadata.Value.Credits = null;

            DatelessSceneEvidence.HasExactPerformers("Ashton Montana & Gene Allen", scene).Should().BeFalse();
        }

        [Test]
        public void should_not_find_exact_performers_when_two_performers_share_the_name_found()
        {
            var scene = Scene("Twins", "Studio", Credit("Jamie Eliot"), Credit("Jamie Eliot Junior", null, "Jamie Eliot"));

            DatelessSceneEvidence.HasExactPerformers("Jamie Eliot", scene).Should().BeFalse();
        }

        [Test]
        public void should_count_a_performer_credited_twice_once()
        {
            var scene = Scene("Ben & Gene", "Freshmen", Credit("Ben Harington"), Credit("Michal Renok", null, "Gene Allen"), Credit("Ben Harington"));

            DatelessSceneEvidence.HasExactPerformers("Gene Allen and Ben Harington", scene).Should().BeTrue();
        }

        [TestCase("Shower Sex - Joey Mills & Landon Vega", "Shower Sex", true)]
        [TestCase("Shower - Joey Mills & Landon Vega", "Shower Sex Fun", true)]
        [TestCase("Hot Afternoon - Joey Mills & Landon Vega", "Shower Sex", false)]
        [TestCase("Hot Afternoon - Joey Mills & Landon Vega", "Joey & Landon", true)]
        [TestCase("Helix Studios Exclusive - Joey Mills & Landon Vega", "Shower Sex", false)]
        [TestCase("Helix - Joey Mills & Landon Vega", "Shower Sex", true)]
        public void should_not_find_exact_performers_when_title_contradicts_scene_title(string releaseTokens, string sceneTitle, bool expected)
        {
            var scene = Scene(sceneTitle, "Helix", Credit("Joey Mills"), Credit("Landon Vega"));

            DatelessSceneEvidence.HasExactPerformers(releaseTokens, scene).Should().Be(expected);
        }

        [TestCase("Issue 389 - A & B", "Issue 388, Sex Scene 2: A & B", false, 0)]
        [TestCase("Issue 389 - A & B", "Issue 389, Sex Scene 2: A & B", false, 1)]
        [TestCase("Issue 389 - A & B", "Issue 390, Sex Scene 2: A & B", false, 0)]
        [TestCase("Issue 390 - A & B", "Issue 397, Sex Scene 1: A & B part 2", true, 0)]
        [TestCase("Issue 379 Part 2 - A & B", "Issue 379, Sex Scene 2: A & B part 2", false, 2)]
        [TestCase("Issue 379 Part 2 - A & B", "Issue 379, Sex Scene 1: A & B part 1", true, 1)]
        [TestCase("Ethan & Kyle Part 1", "Ethan & Kyle - part 2", true, 0)]
        [TestCase("Issue 379, Scene 1 - A & B", "Issue 379, Sex Scene 2: A & B", true, 1)]
        [TestCase("Vol 3 - A & B", "Volume 4: A & B", false, 0)]
        [TestCase("Episode 12 - A & B", "Episode 14 - A & B", true, 0)]
        [TestCase("Issue 379 Part 4 - A & B", "Issue 379, Sex Scene 1: A & B part 1", true, 1)]
        [TestCase("Iss. 379, Sc. 2 - A & B", "Issue 379, Sex Scene 2: A & B", false, 2)]
        [TestCase("Vol 3 - A & B", "Volume 3: A & B", false, 1)]
        [TestCase("Episode #12 - A & B", "Ep. 12 - A & B", false, 1)]
        [TestCase("Issue 389 - A & B", "Part 7: A & B", false, 0)]
        [TestCase("A & B Part 2_1", "A & B Part 2", false, 1)]
        [TestCase("A & B", "Issue 389, Sex Scene 2: A & B", false, 0)]
        [TestCase("Tissue 389 - A & B", "Issue 1: A & B", false, 0)]
        [TestCase(null, "Issue 1: A & B", false, 0)]
        public void should_compare_numbers(string releaseTokens, string sceneTitle, bool conflict, int exact)
        {
            var result = DatelessSceneEvidence.CompareNumbers(releaseTokens, sceneTitle);

            result.Conflict.Should().Be(conflict);
            result.ExactMatches.Should().Be(exact);
        }

        [TestCase("Shower Sex - Joey Mills & Landon Vega", "Shower Sex", "Joey Mills")]
        [TestCase("Joey Mills & Landon Vega - Shower Sex", "Shower Sex", "Landon Vega")]
        [TestCase("Poolside - Dakota Lovell", "Poolside", "Dakota Lovell")]
        [TestCase("Poolside (Dakota Lovell)", "Poolside", "Dakota Lovell")]
        [TestCase("Joy Ride / 5003 (Blake Mitchell, Noah White)", "Joy Ride", "Noah White")]
        [TestCase("Alex - Alex", "Alex", "Alex")]
        [TestCase("Alex & Gato - Alex", "Alex", "Gato")]
        [TestCase("Fun and Games - Kyle Ross", "Fun & Games", "Kyle Ross")]
        [TestCase("Blake Mitchell's Birthday - Blake Mitchell & Noah White", "Blake Mitchell's Birthday", "Blake Mitchell")]
        public void should_find_title_and_performer_apart(string releaseTokens, string title, string performer)
        {
            DatelessSceneEvidence.HasSeparateTitleAndPerformer(releaseTokens, Scene(title, "Helix Studios", Credit(performer))).Should().BeTrue();
        }

        // A one-word title or name that is part of a longer name
        [TestCase("Helix Academy Extra Credit - Alex Killborn & Tyler Hill", "Alex", "Alex")]
        [TestCase("Hot Roommates - Kyle Ross", "Roommates", "Kyle Ross")]
        [TestCase("Poolside - Dakota Lovell", "Poolside", "Dakota")]

        // The performer is the same words as the title
        [TestCase("Kyle Ross Returns", "Kyle Ross Returns", "Kyle Ross")]

        // Only part of a word
        [TestCase("Alexander & Gato", "Alex", "Gato")]
        [TestCase("Shower Sex - Joey Millson", "Shower Sex", "Joey Mills")]
        public void should_not_find_title_and_performer_apart(string releaseTokens, string title, string performer)
        {
            DatelessSceneEvidence.HasSeparateTitleAndPerformer(releaseTokens, Scene(title, "Helix Studios", Credit(performer))).Should().BeFalse();
        }

        [Test]
        public void should_find_title_and_performer_by_alias()
        {
            var scene = Scene("Poolside", "Helix Studios", Credit("Michal Renok", null, "Gene Allen"));

            DatelessSceneEvidence.HasSeparateTitleAndPerformer("Poolside - Gene Allen", scene).Should().BeTrue();
        }

        [TestCase("Helix Academy Extra Credit - Alex Killborn", "Alex", true)]
        [TestCase("Alexander & Gato", "Alex", false)]
        [TestCase("Fun and Games", "Fun & Games", true)]
        [TestCase("Shower.Sex.Joey.Mills", "Shower Sex", true)]
        [TestCase("Showers Sex", "Shower Sex", false)]
        public void should_find_title_as_whole_words(string releaseTokens, string title, bool expected)
        {
            DatelessSceneEvidence.ContainsTitle(releaseTokens, title).Should().Be(expected);
        }
    }
}
