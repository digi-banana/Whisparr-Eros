using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Movies.Credits;
using NzbDrone.Core.Movies.Performers;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests.CreditRepositoryTests
{
    [TestFixture]
    public class CreditRepositoryFixture : DbTest<CreditRepository, Credit>
    {
        private void GivenCredit(string performerForeignId, string personName)
        {
            Db.Insert(Builder<Credit>.CreateNew()
                .With(c => c.Id = 0)
                .With(c => c.MovieMetadataId = 1)
                .With(c => c.PerformerForeignId = performerForeignId)
                .With(c => c.PersonName = personName)
                .With(c => c.Images = new List<MediaCover.MediaCover>())
                .BuildNew());
        }

        [Test]
        public void should_load_performer_aliases_and_disambiguation_with_credits()
        {
            Db.Insert(Builder<Performer>.CreateNew()
                .With(p => p.Id = 0)
                .With(p => p.ForeignId = "626b6adb")
                .With(p => p.Name = "Michal Renok")
                .With(p => p.Disambiguation = "Czech")
                .With(p => p.Aliases = new List<string> { "Gene Allen", "Tony Falco" })
                .With(p => p.Tags = new HashSet<int>())
                .With(p => p.Tattoos = new List<string>())
                .With(p => p.Piercings = new List<string>())
                .With(p => p.Images = new List<MediaCover.MediaCover>())
                .BuildNew());

            GivenCredit("626b6adb", "Gene Allen");
            GivenCredit("unknown", "Ashton Montana");

            var credits = Subject.FindByMovieMetadataId(1);

            credits.Should().HaveCount(2);

            var michal = credits.Single(c => c.PerformerForeignId == "626b6adb");
            michal.PersonName.Should().Be("Michal Renok");
            michal.Performer.Name.Should().Be("Michal Renok");
            michal.Performer.Disambiguation.Should().Be("Czech");
            michal.Performer.Aliases.Should().BeEquivalentTo(new[] { "Gene Allen", "Tony Falco" });

            var unknown = credits.Single(c => c.PerformerForeignId == "unknown");
            unknown.PersonName.Should().Be("Ashton Montana");
            unknown.Performer.Aliases.Should().BeEmpty();
        }
    }
}
