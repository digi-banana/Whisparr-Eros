using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.IndexerSearch
{
    public class PacedMissingSearchCommand : Command
    {
        public override bool SendUpdatesToClient => true;
    }
}
