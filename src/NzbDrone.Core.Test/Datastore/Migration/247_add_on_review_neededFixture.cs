using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class add_on_review_neededFixture : MigrationTest<add_on_review_needed>
    {
        [Test]
        public void should_add_the_on_review_needed_column()
        {
            var db = WithMigrationTestDb();

            db.Query<bool>("SELECT \"OnReviewNeeded\" FROM \"Notifications\"").Should().BeEmpty();
        }
    }
}
