using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // The "On Review Needed" notification trigger. Databases that ran an earlier build of migration 246, which added the column
    // together with the review queue, already have it.
    [Migration(247)]
    public class add_on_review_needed : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("Notifications").Column("OnReviewNeeded").Exists())
            {
                Alter.Table("Notifications").AddColumn("OnReviewNeeded").AsBoolean().WithDefaultValue(false);
            }
        }
    }
}
