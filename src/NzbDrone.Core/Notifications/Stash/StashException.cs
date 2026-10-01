using System;
using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.Notifications.Stash
{
    public class StashException : NzbDroneException
    {
        public StashException(string message)
            : base(message)
        {
        }

        public StashException(string message, params object[] args)
            : base(message, args)
        {
        }

        public StashException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
